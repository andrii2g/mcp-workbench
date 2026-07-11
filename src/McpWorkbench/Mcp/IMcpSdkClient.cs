using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace A2G.McpWorkbench.Mcp;

internal interface IMcpSdkClient : IAsyncDisposable
{
    ValueTask PingAsync(CancellationToken cancellationToken);
    ValueTask<ListToolsResult> ListToolsAsync(string? cursor, CancellationToken cancellationToken);
    ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken cancellationToken);
}

internal sealed class McpSdkClient(McpClient client) : IMcpSdkClient
{
    public async ValueTask PingAsync(CancellationToken cancellationToken) =>
        await client.PingAsync(new PingRequestParams(), cancellationToken);

    public ValueTask<ListToolsResult> ListToolsAsync(string? cursor, CancellationToken cancellationToken) =>
        client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, cancellationToken);

    public ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken cancellationToken) =>
        client.CallToolAsync(request, cancellationToken);

    public ValueTask DisposeAsync() => client.DisposeAsync();
}
