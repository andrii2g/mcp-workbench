namespace McpWorkbench.Domain;

internal sealed record SafeRuntimeError(string Code, string Message);

internal sealed record ServerRuntimeSnapshot(
    Guid ServerId,
    McpConnectionState Status,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? LastOperationAtUtc,
    SafeRuntimeError? LastError,
    string? ProtocolVersion,
    string? ServerName,
    string? ServerVersion,
    bool? SupportsTools,
    bool? ToolsListChanged,
    int? ToolCount);
