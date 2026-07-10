using System.Text;
using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Persistence;
using McpWorkbench.Serialization;

namespace McpWorkbench.UnitTests.Persistence;

public sealed class JsonServerDefinitionStoreTests
{
    [Fact]
    public async Task InitializeAsync_WhenFileIsAbsent_CreatesEmptySchemaVersionOneDocument()
    {
        using var directory = new TestDirectory();
        using var store = CreateStore(directory.RegistryPath);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var snapshot = await store.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RegistryDocument.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.Equal(0, snapshot.Revision);
        Assert.Empty(snapshot.Servers);
        Assert.False(HasUtf8Bom(await File.ReadAllBytesAsync(directory.RegistryPath, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task CrudAsync_WhenDefinitionsChange_PersistsRevisionAndValues()
    {
        using var directory = new TestDirectory();
        using var store = CreateStore(directory.RegistryPath);
        var first = Definition("First");
        var second = Definition("Second");

        await store.CreateAsync(first, TestContext.Current.CancellationToken);
        await store.CreateAsync(second, TestContext.Current.CancellationToken);
        var replacement = first with { Name = "Renamed", UpdatedAtUtc = first.UpdatedAtUtc.AddMinutes(1) };
        await store.ReplaceAsync(replacement, TestContext.Current.CancellationToken);
        var deleted = await store.DeleteAsync(second.Id, TestContext.Current.CancellationToken);
        var snapshot = await store.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.Equal(4, snapshot.Revision);
        Assert.Collection(snapshot.Servers, server => Assert.Equal("Renamed", server.Name));

        using var reloaded = CreateStore(directory.RegistryPath);
        await reloaded.InitializeAsync(TestContext.Current.CancellationToken);
        var reloadedSnapshot = await reloaded.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(snapshot.SchemaVersion, reloadedSnapshot.SchemaVersion);
        Assert.Equal(snapshot.Revision, reloadedSnapshot.Revision);
        Assert.Equal(snapshot.UpdatedAtUtc, reloadedSnapshot.UpdatedAtUtc);
        Assert.Equal(
            JsonSerializer.Serialize(snapshot, AppJsonSerializerContext.Default.RegistryDocument),
            JsonSerializer.Serialize(reloadedSnapshot, AppJsonSerializerContext.Default.RegistryDocument));
    }

    [Fact]
    public async Task CreateAsync_WhenNormalizedNameExists_ReturnsConflictWithoutChangingRevision()
    {
        using var directory = new TestDirectory();
        using var store = CreateStore(directory.RegistryPath);
        await store.CreateAsync(Definition("Demo"), TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.CreateAsync(Definition("  demo  "), TestContext.Current.CancellationToken));

        Assert.Equal("server_name_conflict", exception.Code);
        Assert.Equal(1, (await store.GetSnapshotAsync(TestContext.Current.CancellationToken)).Revision);
    }

    [Fact]
    public async Task InitializeAsync_WhenJsonIsMalformed_DoesNotOverwriteFile()
    {
        using var directory = new TestDirectory();
        const string malformed = "{ not-json";
        await File.WriteAllTextAsync(directory.RegistryPath, malformed, new UTF8Encoding(false), TestContext.Current.CancellationToken);
        using var store = CreateStore(directory.RegistryPath);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("registry_corrupt", exception.Code);
        Assert.Equal(malformed, await File.ReadAllTextAsync(directory.RegistryPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenSchemaVersionIsUnsupported_ReturnsTypedError()
    {
        using var directory = new TestDirectory();
        var document = RegistryDocument.Empty(DateTimeOffset.UnixEpoch) with { SchemaVersion = 99 };
        await File.WriteAllBytesAsync(
            directory.RegistryPath,
            JsonSerializer.SerializeToUtf8Bytes(document, AppJsonSerializerContext.Default.RegistryDocument),
            TestContext.Current.CancellationToken);
        using var store = CreateStore(directory.RegistryPath);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("unsupported_registry_version", exception.Code);
    }

    [Fact]
    public async Task InitializeAsync_WhenRegistryContainsDuplicateNames_ReturnsCorruptError()
    {
        using var directory = new TestDirectory();
        var document = RegistryDocument.Empty(DateTimeOffset.UnixEpoch) with
        {
            Servers = [Definition("Demo"), Definition(" demo ")]
        };
        await File.WriteAllBytesAsync(
            directory.RegistryPath,
            JsonSerializer.SerializeToUtf8Bytes(document, AppJsonSerializerContext.Default.RegistryDocument),
            TestContext.Current.CancellationToken);
        using var store = CreateStore(directory.RegistryPath);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("registry_corrupt", exception.Code);
    }

    [Fact]
    public async Task InitializeAsync_WhenPersistedDefinitionIsInvalid_ReturnsCorruptError()
    {
        using var directory = new TestDirectory();
        var document = RegistryDocument.Empty(DateTimeOffset.UnixEpoch) with
        {
            Servers = [Definition("Demo") with { Id = Guid.Empty }]
        };
        await File.WriteAllBytesAsync(
            directory.RegistryPath,
            JsonSerializer.SerializeToUtf8Bytes(document, AppJsonSerializerContext.Default.RegistryDocument),
            TestContext.Current.CancellationToken);
        using var store = CreateStore(directory.RegistryPath);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("registry_corrupt", exception.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenCalledConcurrently_SerializesAllWrites()
    {
        using var directory = new TestDirectory();
        using var store = CreateStore(directory.RegistryPath);
        var operations = Enumerable.Range(0, 20)
            .Select(index => store.CreateAsync(Definition($"Server {index}"), TestContext.Current.CancellationToken).AsTask());

        await Task.WhenAll(operations);
        var snapshot = await store.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(20, snapshot.Revision);
        Assert.Equal(20, snapshot.Servers.Count);
    }

    [Fact]
    public async Task CreateAsync_WhenAtomicWriteFails_PreservesCurrentFileAndSnapshot()
    {
        using var directory = new TestDirectory();
        var writer = new FailAfterWriteAtomicFileWriter(allowedWrites: 2);
        using var store = new JsonServerDefinitionStore(directory.RegistryPath, writer, TimeProvider.System);
        var first = Definition("First");
        await store.CreateAsync(first, TestContext.Current.CancellationToken);
        var bytesBefore = await File.ReadAllBytesAsync(directory.RegistryPath, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<RegistryException>(async () =>
            await store.CreateAsync(Definition("Second"), TestContext.Current.CancellationToken));

        Assert.Equal("registry_unavailable", exception.Code);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(directory.RegistryPath, TestContext.Current.CancellationToken));
        var snapshot = await store.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, snapshot.Revision);
        Assert.Collection(snapshot.Servers, server => Assert.Equal(first.Id, server.Id));
    }

    private static JsonServerDefinitionStore CreateStore(string path) =>
        new(path, new AtomicFileWriter(), TimeProvider.System);

    private static McpServerDefinition Definition(string name) => new(
        Guid.NewGuid(),
        name,
        null,
        true,
        McpTransportKind.Stdio,
        new StdioTransportSettings("dotnet", ["server.dll"], null, new Dictionary<string, string>(), 5),
        null,
        30,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    private static bool HasUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private sealed class FailAfterWriteAtomicFileWriter(int allowedWrites) : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private int _writeCount;

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _writeCount) > allowedWrites)
            {
                throw new IOException("Injected write failure.");
            }

            return _inner.WriteAsync(path, content, cancellationToken);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcp-workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            RegistryPath = System.IO.Path.Combine(Path, "servers.json");
        }

        public string Path { get; }
        public string RegistryPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
