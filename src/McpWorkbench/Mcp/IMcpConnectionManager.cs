using McpWorkbench.Domain;

namespace McpWorkbench.Mcp;

internal interface IMcpConnectionManager
{
    ValueTask<ServerRuntimeSnapshot> ConnectAsync(Guid serverId, bool forceReconnect, CancellationToken cancellationToken);
    ValueTask DisconnectAsync(Guid serverId, CancellationToken cancellationToken);
    ValueTask<ServerRuntimeSnapshot> PingAsync(Guid serverId, CancellationToken cancellationToken);
    ValueTask<ServerRuntimeSnapshot> GetRuntimeAsync(Guid serverId, CancellationToken cancellationToken);
    ValueTask<McpServerDefinition> ReplaceDefinitionAsync(McpServerDefinition definition, CancellationToken cancellationToken);
    ValueTask<bool> DeleteDefinitionAsync(Guid serverId, CancellationToken cancellationToken);
    ValueTask DisconnectAllAsync(CancellationToken cancellationToken);
}
