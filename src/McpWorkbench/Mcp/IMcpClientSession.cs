using System.Text.Json;
using McpWorkbench.Domain;

namespace McpWorkbench.Mcp;

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
