using System.Text.Json;
using McpWorkbench.Domain;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.Mcp;

internal sealed class McpClientSession(
    McpClient client,
    McpSessionInfo sessionInfo,
    int maximumResultBytes) : IMcpClientSession
{
    private int _disposed;

    public ValueTask<McpSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(sessionInfo);
    }

    public async ValueTask PingAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        try
        {
            await client.PingAsync(new PingRequestParams(), cancellationToken);
        }
        catch (Exception exception)
        {
            throw McpSdkErrorNormalizer.Normalize(exception, "ping");
        }
    }

    public async ValueTask<IReadOnlyList<ToolCatalogEntry>> ListToolsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        try
        {
            var tools = await client.ListToolsAsync(options: null, cancellationToken);
            return ToolCatalogMapper.Map(tools);
        }
        catch (Exception exception)
        {
            throw McpSdkErrorNormalizer.Normalize(exception, "tools/list");
        }
    }

    public async ValueTask<ToolInvocationOutcome> InvokeToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            throw new McpSessionException("tool_arguments_invalid", "Tool arguments must be a JSON object.");
        }

        var mappedArguments = arguments.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone(),
            StringComparer.Ordinal);
        try
        {
            var result = await client.CallToolAsync(
                new CallToolRequestParams { Name = name, Arguments = mappedArguments },
                cancellationToken);
            return ToolResultMapper.Map(result, maximumResultBytes);
        }
        catch (Exception exception)
        {
            throw McpSdkErrorNormalizer.Normalize(exception, "tools/call");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await client.DisposeAsync();
        }
        catch
        {
            // Shutdown is best effort; operational failures are recorded before disposal.
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}
