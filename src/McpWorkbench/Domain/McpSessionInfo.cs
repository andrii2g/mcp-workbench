namespace McpWorkbench.Domain;

internal sealed record McpRemoteIdentity(string Name, string? Version, string? Title);

internal sealed record McpCapabilitySnapshot(bool Tools, bool ToolsListChanged);

internal sealed record McpSessionInfo(
    string ProtocolVersion,
    McpRemoteIdentity Server,
    McpCapabilitySnapshot Capabilities,
    McpHttpMode? SelectedHttpMode);
