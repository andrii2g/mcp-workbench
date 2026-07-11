using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        while (true)
        {
            var runtime = GetOrCreateRuntime(serverId);
            await runtime.LifecycleGate.WaitAsync(cancellationToken);
            try
            {
                if (!_runtimes.TryGetValue(serverId, out var currentRuntime) || !ReferenceEquals(currentRuntime, runtime))
                {
                    continue;
                }

                var definition = await store.GetAsync(serverId, cancellationToken) ??
                    throw new McpSessionException("server_not_found", "The MCP server was not found.");
                if (!definition.Enabled)
                {
                    throw new McpSessionException("server_disabled", "The MCP server is disabled.");
                }

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
                var operationStarted = Stopwatch.GetTimestamp();
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
                    var tools = options.Value.LoadToolsOnConnect
                        ? await session.ListToolsAsync(linked.Token)
                        : null;
                    runtime.Session = session;
                    runtime.SessionInfo = info;
                    runtime.ToolCatalog = tools;
                    runtime.ConnectedAtUtc = timeProvider.GetUtcNow();
                    runtime.LastOperationAtUtc = runtime.ConnectedAtUtc;
                    runtime.Status = McpConnectionState.Connected;
                    RuntimeLog.Connected(logger, serverId);
                    LogOperationCompleted(serverId, "connect", "connected", operationStarted);
                    return Snapshot(runtime);
                }
                catch (Exception exception)
                {
                    Exception? disposalException = null;
                    if (session is not null)
                    {
                        try
                        {
                            await session.DisposeAsync();
                        }
                        catch (Exception disposeFailure)
                        {
                            disposalException = disposeFailure;
                        }
                    }

                    runtime.Session = null;
                    runtime.SessionInfo = null;
                    runtime.ToolCatalog = null;
                    runtime.ConnectedAtUtc = null;
                    if (disposalException is not null)
                    {
                        var disposalFailure = new McpSessionException(
                            "disconnection_failed",
                            "The partially connected MCP session could not be disposed cleanly.");
                        runtime.Status = McpConnectionState.Faulted;
                        runtime.LastError = new SafeRuntimeError(disposalFailure.Code, disposalFailure.Message);
                        RuntimeLog.ConnectionFailed(logger, serverId, disposalFailure.Code);
                        LogOperationCompleted(serverId, "connect", disposalFailure.Code, operationStarted);
                        throw disposalFailure;
                    }

                    if (exception is OperationCanceledException or McpSessionException { Code: "operation_cancelled" })
                    {
                        runtime.Status = McpConnectionState.Disconnected;
                        runtime.LastError = null;
                        if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                        {
                            LogOperationCompleted(serverId, "connect", "connection_timeout", operationStarted);
                            throw new McpSessionException("connection_timeout", "MCP connection timed out.");
                        }

                        LogOperationCompleted(serverId, "connect", "cancelled", operationStarted);
                        throw;
                    }

                    var normalized = exception switch
                    {
                        McpSessionException sessionException => sessionException,
                        SecretReferenceException secretException => new McpSessionException(secretException.Code, secretException.Message),
                        _ => McpSdkErrorNormalizer.Normalize(exception, "connection")
                    };
                    runtime.Status = McpConnectionState.Faulted;
                    runtime.LastError = new SafeRuntimeError(normalized.Code, normalized.Message);
                    RuntimeLog.ConnectionFailed(logger, serverId, normalized.Code);
                    LogOperationCompleted(serverId, "connect", normalized.Code, operationStarted);
                    throw normalized;
                }
            }
            finally
            {
                runtime.LifecycleGate.Release();
            }
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
        var operationStarted = Stopwatch.GetTimestamp();
        var runtime = await GetConnectedRuntimeAsync(serverId, cancellationToken);
        IMcpClientSession session;
        CancellationToken lifetimeToken;
        var invocationAcquired = false;
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await runtime.InvocationGate.WaitAsync(cancellationToken);
            invocationAcquired = true;
            session = runtime.Session ?? throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            lifetimeToken = runtime.LifetimeCancellation.Token;
        }
        catch
        {
            if (invocationAcquired)
            {
                runtime.InvocationGate.Release();
            }

            throw;
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.PingTimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken, timeout.Token);
        McpSessionException? failure = null;
        try
        {
            await session.PingAsync(linked.Token);
        }
        catch (Exception exception)
        {
            failure = exception switch
            {
                McpSessionException { Code: "operation_cancelled" } when timeout.IsCancellationRequested =>
                    new McpSessionException("ping_timeout", "MCP ping timed out."),
                OperationCanceledException when timeout.IsCancellationRequested =>
                    new McpSessionException("ping_timeout", "MCP ping timed out."),
                McpSessionException sessionException => sessionException,
                _ => McpSdkErrorNormalizer.Normalize(exception, "ping")
            };
        }
        finally
        {
            runtime.InvocationGate.Release();
        }

        ServerRuntimeSnapshot snapshot;
        await runtime.LifecycleGate.WaitAsync(CancellationToken.None);
        try
        {
            if (runtime.Status == McpConnectionState.Connected && ReferenceEquals(runtime.Session, session))
            {
                runtime.LastOperationAtUtc = timeProvider.GetUtcNow();
                runtime.LastError = failure is null ? null : new SafeRuntimeError(failure.Code, failure.Message);
            }

            snapshot = Snapshot(runtime);
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }

        if (failure is not null)
        {
            LogOperationCompleted(serverId, "ping", failure.Code, operationStarted);
            throw failure;
        }

        LogOperationCompleted(serverId, "ping", "succeeded", operationStarted);
        return snapshot;
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

    public async ValueTask<IReadOnlyList<ToolCatalogEntry>> GetToolsAsync(
        Guid serverId,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var operationStarted = Stopwatch.GetTimestamp();
        var runtime = await GetConnectedRuntimeAsync(serverId, cancellationToken);
        IMcpClientSession session;
        CancellationToken lifetimeToken;
        var invocationAcquired = false;
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && runtime.ToolCatalog is not null)
            {
                return runtime.ToolCatalog.ToArray();
            }

            await runtime.InvocationGate.WaitAsync(cancellationToken);
            invocationAcquired = true;
            session = runtime.Session ?? throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            lifetimeToken = runtime.LifetimeCancellation.Token;
        }
        catch
        {
            if (invocationAcquired)
            {
                runtime.InvocationGate.Release();
            }

            throw;
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }

        IReadOnlyList<ToolCatalogEntry> tools;
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(options.Value.DefaultOperationTimeoutSeconds),
            timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken, timeout.Token);
        try
        {
            tools = await session.ListToolsAsync(linked.Token);
        }
        catch (McpSessionException exception) when (exception.Code == "operation_cancelled" && timeout.IsCancellationRequested)
        {
            throw new McpSessionException("tool_catalog_timeout", "Loading the MCP tool catalog timed out.");
        }
        finally
        {
            runtime.InvocationGate.Release();
        }

        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (runtime.Status != McpConnectionState.Connected || !ReferenceEquals(runtime.Session, session))
            {
                throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            }

            runtime.ToolCatalog = tools.ToArray();
            runtime.LastOperationAtUtc = timeProvider.GetUtcNow();
            LogOperationCompleted(serverId, refresh ? "refresh_tools" : "list_tools", "succeeded", operationStarted);
            return runtime.ToolCatalog.ToArray();
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }
    }

    public async ValueTask<ToolInvocationOutcome> InvokeToolAsync(
        Guid serverId,
        string toolName,
        JsonElement arguments,
        int? timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            throw new McpSessionException("tool_arguments_invalid", "Tool arguments must be a JSON object.");
        }

        if (Encoding.UTF8.GetByteCount(arguments.GetRawText()) > options.Value.MaximumArgumentsBytes)
        {
            throw new McpSessionException("request_too_large", "Tool arguments exceed the configured size limit.");
        }

        var definition = await store.GetAsync(serverId, cancellationToken) ??
            throw new McpSessionException("server_not_found", "The MCP server was not found.");
        var effectiveTimeout = timeoutSeconds ?? definition.OperationTimeoutSeconds;
        if (effectiveTimeout < 1 || effectiveTimeout > options.Value.MaximumOperationTimeoutSeconds)
        {
            throw new McpSessionException("invalid_operation_timeout", "The operation timeout is outside the configured range.");
        }

        await GetToolsAsync(serverId, false, cancellationToken);

        var runtime = await GetConnectedRuntimeAsync(serverId, cancellationToken);
        IMcpClientSession session;
        CancellationToken lifetimeToken;
        var invocationAcquired = false;
        await runtime.LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await runtime.InvocationGate.WaitAsync(cancellationToken);
            invocationAcquired = true;
            session = runtime.Session ?? throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            if (runtime.ToolCatalog is null ||
                !runtime.ToolCatalog.Any(tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal)))
            {
                throw new McpSessionException("tool_not_found", "The requested tool is not in the loaded catalog.");
            }

            lifetimeToken = runtime.LifetimeCancellation.Token;
        }
        catch
        {
            if (invocationAcquired)
            {
                runtime.InvocationGate.Release();
            }

            throw;
        }
        finally
        {
            runtime.LifecycleGate.Release();
        }

        var startedAt = timeProvider.GetUtcNow();
        ToolInvocationOutcome? outcome = null;
        McpSessionException? failure = null;
        var status = ToolExecutionStatus.Failed;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken, timeout.Token);
        try
        {
            outcome = await session.InvokeToolAsync(toolName, arguments, linked.Token);
            status = outcome.IsError ? ToolExecutionStatus.ToolError : ToolExecutionStatus.Succeeded;
            return outcome;
        }
        catch (Exception exception)
        {
            failure = exception switch
            {
                McpSessionException { Code: "operation_cancelled" } when timeout.IsCancellationRequested =>
                    new McpSessionException("tool_call_timeout", "The MCP tool call timed out."),
                McpSessionException { Code: "operation_cancelled" } =>
                    new McpSessionException("tool_call_cancelled", "The MCP tool call was cancelled."),
                OperationCanceledException when timeout.IsCancellationRequested =>
                    new McpSessionException("tool_call_timeout", "The MCP tool call timed out."),
                OperationCanceledException =>
                    new McpSessionException("tool_call_cancelled", "The MCP tool call was cancelled."),
                McpSessionException { Code: "mcp_protocol_error" } =>
                    new McpSessionException("tool_protocol_error", "The MCP tool call returned an invalid protocol response."),
                McpSessionException sessionException => sessionException,
                _ => McpSdkErrorNormalizer.Normalize(exception, "tool_call")
            };
            status = failure.Code == "tool_call_timeout"
                ? ToolExecutionStatus.TimedOut
                : failure.Code == "tool_call_cancelled"
                    ? ToolExecutionStatus.Cancelled
                    : ToolExecutionStatus.Failed;
            throw failure;
        }
        finally
        {
            runtime.InvocationGate.Release();
            var completedAt = timeProvider.GetUtcNow();
            LogOperationCompleted(serverId, "invoke_tool", status, startedAt, completedAt);
            runtime.History.Add(new ToolExecutionRecord(
                Guid.NewGuid(),
                serverId,
                toolName,
                startedAt,
                completedAt,
                Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
                status,
                outcome?.IsError,
                failure?.Code));
            await runtime.LifecycleGate.WaitAsync(CancellationToken.None);
            try
            {
                runtime.LastOperationAtUtc = completedAt;
            }
            finally
            {
                runtime.LifecycleGate.Release();
            }
        }
    }

    public async ValueTask<IReadOnlyList<ToolExecutionRecord>> GetExecutionHistoryAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        if (!_runtimes.TryGetValue(serverId, out var runtime))
        {
            if (await store.GetAsync(serverId, cancellationToken) is null)
            {
                throw new McpSessionException("server_not_found", "The MCP server was not found.");
            }

            return [];
        }

        return runtime.History.Snapshot();
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
        McpSessionException? firstFailure = null;
        foreach (var runtime in _runtimes.Values)
        {
            await runtime.LifecycleGate.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    await DisconnectLockedAsync(runtime);
                }
                catch (McpSessionException exception)
                {
                    firstFailure ??= exception;
                }
            }
            finally
            {
                runtime.LifecycleGate.Release();
            }
        }

        if (firstFailure is not null)
        {
            throw firstFailure;
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

        var operationStarted = Stopwatch.GetTimestamp();
        runtime.Status = McpConnectionState.Disconnecting;
        runtime.LifetimeCancellation.Cancel();
        await runtime.InvocationGate.WaitAsync(CancellationToken.None);
        var session = runtime.Session;
        Exception? disposalException = null;
        try
        {
            runtime.Session = null;
            if (session is not null)
            {
                try
                {
                    await session.DisposeAsync();
                }
                catch (Exception exception)
                {
                    disposalException = exception;
                }
            }
        }
        finally
        {
            runtime.InvocationGate.Release();
        }

        runtime.SessionInfo = null;
        runtime.ToolCatalog = null;
        runtime.ConnectedAtUtc = null;
        if (disposalException is not null)
        {
            var failure = new McpSessionException("disconnection_failed", "The MCP session could not be disposed cleanly.");
            runtime.LastError = new SafeRuntimeError(failure.Code, failure.Message);
            runtime.Status = McpConnectionState.Faulted;
            LogOperationCompleted(runtime.ServerId, "disconnect", failure.Code, operationStarted);
            throw failure;
        }

        runtime.LastError = null;
        runtime.Status = McpConnectionState.Disconnected;
        RuntimeLog.Disconnected(logger, runtime.ServerId);
        LogOperationCompleted(runtime.ServerId, "disconnect", "disconnected", operationStarted);
    }

    private void LogOperationCompleted(Guid serverId, string operation, string status, long started)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var durationMilliseconds = Math.Max(0, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            RuntimeLog.OperationCompleted(
                logger,
                serverId,
                operation,
                status,
                durationMilliseconds);
        }
    }

    private void LogOperationCompleted(
        Guid serverId,
        string operation,
        ToolExecutionStatus status,
        DateTimeOffset started,
        DateTimeOffset completed)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var statusText = status.ToString();
            var durationMilliseconds = Math.Max(0, (long)(completed - started).TotalMilliseconds);
            RuntimeLog.OperationCompleted(
                logger,
                serverId,
                operation,
                statusText,
                durationMilliseconds);
        }
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
        runtime.SessionInfo?.Capabilities.Tools,
        runtime.SessionInfo?.Capabilities.ToolsListChanged,
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

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "MCP server {ServerId} operation {Operation} completed with {Status} in {DurationMilliseconds} ms")]
    public static partial void OperationCompleted(ILogger logger, Guid serverId, string operation, string status, long durationMilliseconds);
}
