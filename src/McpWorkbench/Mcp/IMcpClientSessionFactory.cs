using McpWorkbench.Domain;
using McpWorkbench.Security;

namespace McpWorkbench.Mcp;

internal interface IMcpClientSessionFactory
{
    ValueTask<IMcpClientSession> CreateAsync(
        McpServerDefinition definition,
        ResolvedTransportSettings resolvedSettings,
        CancellationToken cancellationToken);
}
