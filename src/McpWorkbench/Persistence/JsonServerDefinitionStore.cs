using System.Text.Json;
using A2G.McpWorkbench.Contracts;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Serialization;
using A2G.McpWorkbench.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2G.McpWorkbench.Persistence;

internal sealed class JsonServerDefinitionStore : IServerDefinitionStore, IDisposable
{
    private readonly string _path;
    private readonly IAtomicFileWriter _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonServerDefinitionStore> _logger;
    private readonly int _maximumOperationTimeoutSeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RegistryDocument? _snapshot;

    public JsonServerDefinitionStore(
        string path,
        IAtomicFileWriter writer,
        TimeProvider timeProvider,
        ILogger<JsonServerDefinitionStore>? logger = null,
        int maximumOperationTimeoutSeconds = 300)
    {
        _path = Path.GetFullPath(path);
        _writer = writer;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<JsonServerDefinitionStore>.Instance;
        _maximumOperationTimeoutSeconds = maximumOperationTimeoutSeconds;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is not null)
            {
                return;
            }

            if (File.Exists(_path + ".tmp"))
            {
                RegistryLog.StaleTemporaryFile(_logger, _path + ".tmp");
            }

            if (!File.Exists(_path))
            {
                var empty = RegistryDocument.Empty(_timeProvider.GetUtcNow());
                await PersistAsync(empty, cancellationToken);
                _snapshot = Clone(empty);
                return;
            }

            try
            {
                await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous);
                var document = await JsonSerializer.DeserializeAsync(
                    stream,
                    AppJsonSerializerContext.Default.RegistryDocument,
                    cancellationToken) ?? throw Corrupt("Registry document is empty.");
                Validate(document);
                _snapshot = Clone(document);
            }
            catch (RegistryException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw Corrupt("Registry document contains malformed JSON.", exception);
            }
            catch (IOException exception)
            {
                throw Unavailable("Registry document could not be read.", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw Unavailable("Registry document could not be read.", exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<RegistryDocument> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Clone(RequiredSnapshot());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<McpServerDefinition?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.Servers.FirstOrDefault(server => server.Id == id);
    }

    public async ValueTask<McpServerDefinition> CreateAsync(McpServerDefinition definition, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = RequiredSnapshot();
            ValidateDefinition(definition, "server_definition_invalid");
            if (current.Servers.Any(server => server.Id == definition.Id))
            {
                throw new RegistryException("server_id_conflict", "A server with this identifier already exists.");
            }

            EnsureUniqueName(current.Servers, definition.Name, null);
            var servers = current.Servers.Append(Clone(definition)).ToArray();
            await CommitAsync(current, servers, cancellationToken);
            return Clone(definition);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<McpServerDefinition> ReplaceAsync(McpServerDefinition definition, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = RequiredSnapshot();
            ValidateDefinition(definition, "server_definition_invalid");
            var index = FindIndex(current.Servers, definition.Id);
            if (index < 0)
            {
                throw new RegistryException("server_not_found", "The MCP server was not found.");
            }

            EnsureUniqueName(current.Servers, definition.Name, definition.Id);
            var servers = current.Servers.Select(server => server.Id == definition.Id ? Clone(definition) : server).ToArray();
            await CommitAsync(current, servers, cancellationToken);
            return Clone(definition);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = RequiredSnapshot();
            if (FindIndex(current.Servers, id) < 0)
            {
                return false;
            }

            await CommitAsync(current, current.Servers.Where(server => server.Id != id).ToArray(), cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_snapshot is null)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private async ValueTask CommitAsync(
        RegistryDocument current,
        IReadOnlyList<McpServerDefinition> servers,
        CancellationToken cancellationToken)
    {
        var next = new RegistryDocument(
            RegistryDocument.CurrentSchemaVersion,
            checked(current.Revision + 1),
            _timeProvider.GetUtcNow(),
            servers);
        await PersistAsync(next, cancellationToken);
        _snapshot = Clone(next);
    }

    private async ValueTask PersistAsync(RegistryDocument document, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, AppJsonSerializerContext.Default.RegistryDocument);
            await _writer.WriteAsync(_path, bytes, cancellationToken);
        }
        catch (RegistryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Unavailable("Registry document could not be written.", exception);
        }
    }

    private void Validate(RegistryDocument document)
    {
        if (document.SchemaVersion != RegistryDocument.CurrentSchemaVersion)
        {
            throw new RegistryException("unsupported_registry_version", "Registry schema version is unsupported.");
        }

        if (document.Revision < 0 || document.UpdatedAtUtc.Offset != TimeSpan.Zero || document.Servers is null)
        {
            throw Corrupt("Registry document has invalid metadata.");
        }

        if (document.Servers.Any(server => server is null || server.Name is null))
        {
            throw Corrupt("Registry document contains a null server or server name.");
        }

        if (document.Servers.Select(server => server.Id).Distinct().Count() != document.Servers.Count ||
            document.Servers.Select(server => server.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Servers.Count)
        {
            throw Corrupt("Registry document contains duplicate server identifiers or names.");
        }

        foreach (var server in document.Servers)
        {
            ValidateDefinition(server, "registry_corrupt");
        }
    }

    private void ValidateDefinition(McpServerDefinition server, string errorCode)
    {
        if (server.Id == Guid.Empty || server.CreatedAtUtc.Offset != TimeSpan.Zero || server.UpdatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new RegistryException(errorCode, "Server definition contains invalid metadata.");
        }

        var request = new CreateServerRequest(
            server.Name,
            server.Description,
            server.Enabled,
            server.Transport,
            server.Stdio is null ? null : new StdioTransportRequest(
                server.Stdio.Command,
                server.Stdio.Arguments,
                server.Stdio.WorkingDirectory,
                server.Stdio.Environment,
                server.Stdio.ShutdownTimeoutSeconds),
            server.Http is null ? null : new HttpTransportRequest(
                server.Http.Endpoint,
                server.Http.Mode,
                server.Http.Headers),
            server.OperationTimeoutSeconds);
        if (!ServerDefinitionValidator.Validate(request, _maximumOperationTimeoutSeconds).IsValid)
        {
            throw new RegistryException(errorCode, "Server definition is invalid.");
        }
    }

    private static void EnsureUniqueName(IEnumerable<McpServerDefinition> servers, string name, Guid? exceptId)
    {
        if (servers.Any(server => server.Id != exceptId && string.Equals(server.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new RegistryException("server_name_conflict", "A server with this name already exists.");
        }
    }

    private static int FindIndex(IReadOnlyList<McpServerDefinition> servers, Guid id)
    {
        for (var index = 0; index < servers.Count; index++)
        {
            if (servers[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private RegistryDocument RequiredSnapshot() =>
        _snapshot ?? throw new InvalidOperationException("Registry has not been initialized.");

    private static RegistryDocument Clone(RegistryDocument document) => document with
    {
        Servers = document.Servers.Select(Clone).ToArray()
    };

    private static McpServerDefinition Clone(McpServerDefinition definition) => definition with
    {
        Stdio = definition.Stdio is null ? null : definition.Stdio with
        {
            Arguments = definition.Stdio.Arguments.ToArray(),
            Environment = new Dictionary<string, string>(definition.Stdio.Environment, StringComparer.Ordinal)
        },
        Http = definition.Http is null ? null : definition.Http with
        {
            Headers = new Dictionary<string, string>(definition.Http.Headers, StringComparer.OrdinalIgnoreCase)
        }
    };

    private static RegistryException Corrupt(string message, Exception? exception = null) =>
        exception is null
            ? new RegistryException("registry_corrupt", message)
            : new RegistryException("registry_corrupt", message, exception);

    private static RegistryException Unavailable(string message, Exception exception) =>
        new("registry_unavailable", message, exception);
}

internal static partial class RegistryLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Stale registry temporary file found at {TemporaryPath}; it will not be promoted")]
    public static partial void StaleTemporaryFile(ILogger logger, string temporaryPath);
}
