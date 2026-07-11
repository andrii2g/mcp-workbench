using System.Text.Json;
using A2G.McpWorkbench.Domain;

namespace A2G.McpWorkbench.Mcp;

internal interface IMcpClientSession : IAsyncDisposable
{
    ValueTask<McpSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken);
    ValueTask PingAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<ToolCatalogEntry>> ListToolsAsync(CancellationToken cancellationToken);
    ValueTask<ToolInvocationOutcome> InvokeToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
