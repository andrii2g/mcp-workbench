using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using McpWorkbench.Options;
using McpWorkbench.Persistence;
using McpWorkbench.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpWorkbench.UnitTests.Mcp;

public sealed class McpConnectionManagerTests
{
    [Fact]
    public async Task ConnectAsync_WhenCalledConcurrently_CreatesOneSession()
    {
        var fixture = Fixture();
        fixture.Factory.Delay = TimeSpan.FromMilliseconds(50);

        var results = await Task.WhenAll(
            fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken).AsTask(),
            fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, fixture.Factory.CreateCount);
        Assert.All(results, snapshot => Assert.Equal(McpConnectionState.Connected, snapshot.Status));
    }

    [Fact]
    public async Task DisconnectAsync_WhenCalledConcurrently_DisposesSessionOnce()
    {
        var fixture = Fixture();
        var session = fixture.Factory.Sessions[0];
        await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken);

        await Task.WhenAll(
            fixture.Manager.DisconnectAsync(fixture.Definition.Id, TestContext.Current.CancellationToken).AsTask(),
            fixture.Manager.DisconnectAsync(fixture.Definition.Id, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(McpConnectionState.Disconnected,
            (await fixture.Manager.GetRuntimeAsync(fixture.Definition.Id, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ConnectAsync_WhenForceReconnectIsTrue_ReplacesLiveSession()
    {
        var fixture = Fixture(sessionCount: 2);
        var first = fixture.Factory.Sessions[0];
        await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken);

        var snapshot = await fixture.Manager.ConnectAsync(fixture.Definition.Id, true, TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.Factory.CreateCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(McpConnectionState.Connected, snapshot.Status);
    }

    [Fact]
    public async Task ConnectAsync_WhenInitialPingFails_DisposesPartialSessionAndFaultsRuntime()
    {
        var fixture = Fixture();
        fixture.Factory.Sessions[0].PingException = new McpSessionException("ping_failed", "Ping failed safely.");

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () =>
            await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken));
        var snapshot = await fixture.Manager.GetRuntimeAsync(fixture.Definition.Id, TestContext.Current.CancellationToken);

        Assert.Equal("ping_failed", exception.Code);
        Assert.Equal(1, fixture.Factory.Sessions[0].DisposeCount);
        Assert.Equal(McpConnectionState.Faulted, snapshot.Status);
        Assert.Equal("ping_failed", snapshot.LastError?.Code);
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelled_ReturnsRuntimeToDisconnected()
    {
        var fixture = Fixture();
        fixture.Factory.WaitForCancellation = true;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, cancellation.Token));
        var snapshot = await fixture.Manager.GetRuntimeAsync(fixture.Definition.Id, TestContext.Current.CancellationToken);

        Assert.Equal(McpConnectionState.Disconnected, snapshot.Status);
        Assert.Null(snapshot.LastError);
    }

    [Fact]
    public async Task ReplaceDefinitionAsync_WhenPingIsActive_CancelsOperationAndDisconnects()
    {
        var fixture = Fixture();
        await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken);
        var session = fixture.Factory.Sessions[0];
        session.BlockPings = true;
        var ping = fixture.Manager.PingAsync(fixture.Definition.Id, TestContext.Current.CancellationToken).AsTask();
        await session.PingStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var replacement = fixture.Definition with { Name = "Updated" };
        await fixture.Manager.ReplaceDefinitionAsync(replacement, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () => await ping);
        Assert.Equal("operation_cancelled", exception.Code);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal("Updated", (await fixture.Store.GetAsync(replacement.Id, TestContext.Current.CancellationToken))?.Name);
    }

    [Fact]
    public async Task DeleteDefinitionAsync_WhenConnected_DisposesAndRemovesDefinition()
    {
        var fixture = Fixture();
        await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken);

        var deleted = await fixture.Manager.DeleteDefinitionAsync(fixture.Definition.Id, TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.Equal(1, fixture.Factory.Sessions[0].DisposeCount);
        Assert.Null(await fixture.Store.GetAsync(fixture.Definition.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteDefinitionAsync_WhenPingIsActive_CancelsBeforeDisposal()
    {
        var fixture = Fixture();
        await fixture.Manager.ConnectAsync(fixture.Definition.Id, false, TestContext.Current.CancellationToken);
        var session = fixture.Factory.Sessions[0];
        session.BlockPings = true;
        var ping = fixture.Manager.PingAsync(fixture.Definition.Id, TestContext.Current.CancellationToken).AsTask();
        await session.PingStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var deleted = await fixture.Manager.DeleteDefinitionAsync(fixture.Definition.Id, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () => await ping);
        Assert.True(deleted);
        Assert.Equal("operation_cancelled", exception.Code);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task ShutdownService_WhenSessionsAreConnected_DisposesEverySession()
    {
        var first = Definition("First");
        var second = Definition("Second");
        var store = new FakeStore(first, second);
        var factory = new FakeSessionFactory([new FakeSession(), new FakeSession()]);
        var manager = Manager(store, factory);
        await manager.ConnectAsync(first.Id, false, TestContext.Current.CancellationToken);
        await manager.ConnectAsync(second.Id, false, TestContext.Current.CancellationToken);

        await new McpRuntimeShutdownService(manager).StopAsync(TestContext.Current.CancellationToken);

        Assert.All(factory.Sessions, session => Assert.Equal(1, session.DisposeCount));
    }

    [Fact]
    public async Task Runtime_WhenInvocationGateIsHeld_SerializesSecondWaiter()
    {
        var fixture = Fixture();
        var runtime = fixture.Manager.GetOrCreateRuntime(fixture.Definition.Id);
        await runtime.InvocationGate.WaitAsync(TestContext.Current.CancellationToken);
        var secondEntered = false;
        var second = Task.Run(async () =>
        {
            await runtime.InvocationGate.WaitAsync(TestContext.Current.CancellationToken);
            secondEntered = true;
            runtime.InvocationGate.Release();
        }, TestContext.Current.CancellationToken);

        await Task.Delay(25, TestContext.Current.CancellationToken);
        Assert.False(secondEntered);
        runtime.InvocationGate.Release();
        await second;
        Assert.True(secondEntered);
    }

    private static FixtureState Fixture(int sessionCount = 1)
    {
        var definition = Definition("Test");
        var store = new FakeStore(definition);
        var factory = new FakeSessionFactory(Enumerable.Range(0, sessionCount).Select(_ => new FakeSession()).ToArray());
        return new FixtureState(definition, store, factory, Manager(store, factory));
    }

    private static McpConnectionManager Manager(FakeStore store, FakeSessionFactory factory) => new(
        store,
        new SecretReferenceResolver(new EmptyEnvironmentValueProvider()),
        factory,
        TimeProvider.System,
        Microsoft.Extensions.Options.Options.Create(new WorkbenchOptions
        {
            ConnectTimeoutSeconds = 5,
            PingTimeoutSeconds = 5,
            MaximumHistoryEntriesPerServer = 2
        }),
        NullLogger<McpConnectionManager>.Instance);

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

    private sealed record FixtureState(
        McpServerDefinition Definition,
        FakeStore Store,
        FakeSessionFactory Factory,
        McpConnectionManager Manager);

    private sealed class FakeStore(params McpServerDefinition[] definitions) : IServerDefinitionStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, McpServerDefinition> _definitions = definitions.ToDictionary(item => item.Id);

        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<RegistryDocument> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return ValueTask.FromResult(new RegistryDocument(1, 0, DateTimeOffset.UnixEpoch, _definitions.Values.ToArray()));
            }
        }

        public ValueTask<McpServerDefinition?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return ValueTask.FromResult(_definitions.GetValueOrDefault(id));
            }
        }

        public ValueTask<McpServerDefinition> CreateAsync(McpServerDefinition definition, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _definitions.Add(definition.Id, definition);
                return ValueTask.FromResult(definition);
            }
        }

        public ValueTask<McpServerDefinition> ReplaceAsync(McpServerDefinition definition, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _definitions[definition.Id] = definition;
                return ValueTask.FromResult(definition);
            }
        }

        public ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return ValueTask.FromResult(_definitions.Remove(id));
            }
        }
    }

    private sealed class FakeSessionFactory(IReadOnlyList<FakeSession> sessions) : IMcpClientSessionFactory
    {
        private int _createCount;

        public IReadOnlyList<FakeSession> Sessions { get; } = sessions;
        public int CreateCount => _createCount;
        public TimeSpan Delay { get; set; }
        public bool WaitForCancellation { get; set; }

        public async ValueTask<IMcpClientSession> CreateAsync(
            McpServerDefinition definition,
            ResolvedTransportSettings resolvedSettings,
            CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _createCount) - 1;
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return Sessions[index];
        }
    }

    private sealed class FakeSession : IMcpClientSession
    {
        public int DisposeCount { get; private set; }
        public Exception? PingException { get; set; }
        public bool BlockPings { get; set; }
        public TaskCompletionSource PingStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<McpSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken) => ValueTask.FromResult(
            new McpSessionInfo("2025-11-25", new McpRemoteIdentity("Fake", "1.0", null), new McpCapabilitySnapshot(true, false), null));

        public async ValueTask PingAsync(CancellationToken cancellationToken)
        {
            if (PingException is not null)
            {
                throw PingException;
            }

            if (BlockPings)
            {
                PingStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new McpSessionException("operation_cancelled", "Operation cancelled.");
                }
            }
        }

        public ValueTask<IReadOnlyList<ToolCatalogEntry>> ListToolsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ToolCatalogEntry>>([]);

        public ValueTask<ToolInvocationOutcome> InvokeToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyEnvironmentValueProvider : IEnvironmentValueProvider
    {
        public string? GetValue(string name) => null;
    }
}
