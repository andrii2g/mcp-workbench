namespace McpWorkbench.Domain;

internal sealed record McpServerDefinition(
    Guid Id,
    string Name,
    string? Description,
    bool Enabled,
    McpTransportKind Transport,
    StdioTransportSettings? Stdio,
    HttpTransportSettings? Http,
    int OperationTimeoutSeconds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
