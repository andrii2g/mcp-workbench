using McpWorkbench.Domain;

namespace McpWorkbench.Contracts;

internal sealed record StdioTransportRequest(
    string? Command,
    IReadOnlyList<string>? Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment,
    int ShutdownTimeoutSeconds = 5);

internal sealed record HttpTransportRequest(
    string? Endpoint,
    McpHttpMode Mode,
    IReadOnlyDictionary<string, string>? Headers);

internal sealed record CreateServerRequest(
    string? Name,
    string? Description,
    bool Enabled,
    McpTransportKind Transport,
    StdioTransportRequest? Stdio,
    HttpTransportRequest? Http,
    int OperationTimeoutSeconds = 30);

internal sealed record UpdateServerRequest(
    string? Name,
    string? Description,
    bool Enabled,
    McpTransportKind Transport,
    StdioTransportRequest? Stdio,
    HttpTransportRequest? Http,
    int OperationTimeoutSeconds = 30);
