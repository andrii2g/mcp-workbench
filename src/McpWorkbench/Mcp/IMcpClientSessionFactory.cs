using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Security;

namespace A2G.McpWorkbench.Mcp;

internal interface IMcpClientSessionFactory
{
    ValueTask<IMcpClientSession> CreateAsync(
        McpServerDefinition definition,
        ResolvedTransportSettings resolvedSettings,
        CancellationToken cancellationToken);
}
