using System.Collections.Concurrent;
using System.Diagnostics;
using McpWorkbench.Domain;
using McpWorkbench.Options;
using McpWorkbench.Persistence;
using McpWorkbench.Security;
using Microsoft.Extensions.Options;

namespace McpWorkbench.Mcp;

internal sealed class McpConnectionManager(
    IServerDefinitionStore store,
    SecretReferenceResolver secretResolver,
    IMcpClientSessionFactory sessionFactory,
    TimeProvider timeProvider,
    IOptions<WorkbenchOptions> options,
    ILogger<McpConnectionManager> logger) : IMcpConnectionManager
{
    private readonly ConcurrentDictionary<Guid, McpServerRuntime> _runtimes = new();

    public async ValueTask<ServerRuntimeSnapshot> ConnectAsync(
        Guid serverId,
        bool forceReconnect,
        CancellationToken cancellationToken)
    {
        var definition = await store.GetAsync(serverId, cancellationToken) ??
            throw new McpSessionException("server_not_found", "The MCP server was not found.");
        if (!definition.Enabled)
        {
            throw new McpSessionException("server_disabled", "The MCP server is disabled.");
        }

        var runtime = GetOrCreateRuntime(serverId);
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (runtime.Status == McpConnectionState.Connected && !forceReconnect)
            {
                return Snapshot(runtime);
            }

            if (runtime.Status == McpConnectionState.Connected || runtime.Status == McpConnectionState.Faulted)
            {
                await DisconnectLockedAsync(runtime);
            }

            runtime.Status = McpConnectionState.Connecting;
            runtime.LastError = null;
            ResetLifetime(runtime);
            RuntimeLog.Connecting(logger, serverId, definition.Transport);

            IMcpClientSession? session = null;
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(options.Value.ConnectTimeoutSeconds),
                timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                runtime.LifetimeCancellation.Token,
                timeout.Token);
            try
            {
                var resolved = secretResolver.Resolve(definition);
                session = await sessionFactory.CreateAsync(definition, resolved, linked.Token);
                await session.PingAsync(linked.Token);
                var info = await session.GetSessionInfoAsync(linked.Token);
                runtime.Session = session;
                runtime.SessionInfo = info;
                runtime.ConnectedAtUtc = timeProvider.GetUtcNow();
                runtime.LastOperationAtUtc = runtime.ConnectedAtUtc;
                runtime.Status = McpConnectionState.Connected;
                RuntimeLog.Connected(logger, serverId);
                return Snapshot(runtime);
            }
            catch (Exception exception)
            {
                if (session is not null)
                {
                    await session.DisposeAsync();
                }

                runtime.Session = null;
                runtime.SessionInfo = null;
                runtime.ToolCatalog = null;
                runtime.ConnectedAtUtc = null;
                if (exception is OperationCanceledException or McpSessionException { Code: "operation_cancelled" })
                {
                    runtime.Status = McpConnectionState.Disconnected;
                    runtime.LastError = null;
                    if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new McpSessionException("connection_timeout", "MCP connection timed out.");
                    }
                }
                else
                {
                    var normalized = exception switch
                    {
                        McpSessionException sessionException => sessionException,
                        SecretReferenceException secretException => new McpSessionException(secretException.Code, secretException.Message),
                        _ => McpSdkErrorNormalizer.Normalize(exception, "connection")
                    };
                    runtime.Status = McpConnectionState.Faulted;
                    runtime.LastError = new SafeRuntimeError(normalized.Code, normalized.Message);
                    RuntimeLog.ConnectionFailed(logger, serverId, normalized.Code);
                    throw normalized;
                }

                throw;
            }
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAsync(Guid serverId, CancellationToken cancellationToken)
    {
        if (!_runtimes.TryGetValue(serverId, out var runtime))
        {
            if (await store.GetAsync(serverId, cancellationToken) is null)
            {
                throw new McpSessionException("server_not_found", "The MCP server was not found.");
            }

            return;
        }

        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectLockedAsync(runtime);
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask<ServerRuntimeSnapshot> PingAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var runtime = await GetConnectedRuntimeAsync(serverId, cancellationToken);
        IMcpClientSession session;
        CancellationToken lifetimeToken;
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            session = runtime.Session ?? throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            lifetimeToken = runtime.LifetimeCancellation.Token;
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.PingTimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken, timeout.Token);
        try
        {
            await session.PingAsync(linked.Token);
        }
        catch (McpSessionException exception) when (exception.Code == "operation_cancelled" && timeout.IsCancellationRequested)
        {
            throw new McpSessionException("ping_timeout", "MCP ping timed out.");
        }

        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            runtime.LastOperationAtUtc = timeProvider.GetUtcNow();
            runtime.LastError = null;
            return Snapshot(runtime);
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask<ServerRuntimeSnapshot> GetRuntimeAsync(Guid serverId, CancellationToken cancellationToken)
    {
        if (!_runtimes.TryGetValue(serverId, out var runtime))
        {
            if (await store.GetAsync(serverId, cancellationToken) is null)
            {
                throw new McpSessionException("server_not_found", "The MCP server was not found.");
            }

            return DisconnectedSnapshot(serverId);
        }

        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            return Snapshot(runtime);
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask<McpServerDefinition> ReplaceDefinitionAsync(
        McpServerDefinition definition,
        CancellationToken cancellationToken)
    {
        var runtime = GetOrCreateRuntime(definition.Id);
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectLockedAsync(runtime);
            return await store.ReplaceAsync(definition, cancellationToken);
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask<bool> DeleteDefinitionAsync(Guid serverId, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(serverId, cancellationToken) is null)
        {
            return false;
        }

        var runtime = GetOrCreateRuntime(serverId);
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectLockedAsync(runtime);
            var deleted = await store.DeleteAsync(serverId, cancellationToken);
            if (deleted)
            {
                _runtimes.TryRemove(serverId, out _);
            }

            return deleted;
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in _runtimes.Values)
        {
            await runtime.LifecycleGate.WaitAsync(cancellationToken);
            try
            {
                await DisconnectLockedAsync(runtime);
            }
            finally
            {
                runtime.LifecycleGate.Release();
            }
        }
    }

    internal McpServerRuntime GetOrCreateRuntime(Guid serverId) =>
        _runtimes.GetOrAdd(serverId, id => new McpServerRuntime(id, options.Value.MaximumHistoryEntriesPerServer));

    private async ValueTask<McpServerRuntime> GetConnectedRuntimeAsync(Guid serverId, CancellationToken cancellationToken)
    {
        if (!_runtimes.TryGetValue(serverId, out var runtime) || runtime.Status != McpConnectionState.Connected)
        {
            if (await store.GetAsync(serverId, cancellationToken) is null)
            {
                throw new McpSessionException("server_not_found", "The MCP server was not found.");
            }

            throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
        }

        return runtime;
    }

    private async ValueTask DisconnectLockedAsync(McpServerRuntime runtime)
    {
        if (runtime.Status == McpConnectionState.Disconnected && runtime.Session is null)
        {
            runtime.ToolCatalog = null;
            return;
        }

        runtime.Status = McpConnectionState.Disconnecting;
        runtime.LifetimeCancellation.Cancel();
        var session = runtime.Session;
        runtime.Session = null;
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        runtime.SessionInfo = null;
        runtime.ToolCatalog = null;
        runtime.ConnectedAtUtc = null;
        runtime.LastError = null;
        runtime.Status = McpConnectionState.Disconnected;
        RuntimeLog.Disconnected(logger, runtime.ServerId);
    }

    private static void ResetLifetime(McpServerRuntime runtime)
    {
        runtime.LifetimeCancellation.Dispose();
        runtime.LifetimeCancellation = new CancellationTokenSource();
    }

    private static ServerRuntimeSnapshot Snapshot(McpServerRuntime runtime) => new(
        runtime.ServerId,
        runtime.Status,
        runtime.ConnectedAtUtc,
        runtime.LastOperationAtUtc,
        runtime.LastError,
        runtime.SessionInfo?.ProtocolVersion,
        runtime.SessionInfo?.Server.Name,
        runtime.SessionInfo?.Server.Version,
        runtime.ToolCatalog?.Count);

    private static ServerRuntimeSnapshot DisconnectedSnapshot(Guid serverId) => new(
        serverId,
        McpConnectionState.Disconnected,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}

internal static partial class RuntimeLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Connecting MCP server {ServerId} using {Transport}")]
    public static partial void Connecting(ILogger logger, Guid serverId, McpTransportKind transport);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Connected MCP server {ServerId}")]
    public static partial void Connected(ILogger logger, Guid serverId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Disconnected MCP server {ServerId}")]
    public static partial void Disconnected(ILogger logger, Guid serverId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "MCP server {ServerId} connection failed with {ErrorCode}")]
    public static partial void ConnectionFailed(ILogger logger, Guid serverId, string errorCode);
}
