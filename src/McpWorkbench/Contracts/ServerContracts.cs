using A2G.McpWorkbench.Domain;

namespace A2G.McpWorkbench.Contracts;

internal sealed record StdioTransportRequest(
    string? Command,
    IReadOnlyList<string>? Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment,
    int ShutdownTimeoutSeconds = 5);

internal sealed record HttpTransportRequest(
    string? Endpoint,
    McpHttpMode Mode,
    IReadOnlyDictionary<string, string>? Headers,
    HttpAuthorizationSettings? Authorization = null);

internal sealed record CreateServerRequest(
    string? Name,
    string? Description,
    bool Enabled,
    McpTransportKind Transport,
    StdioTransportRequest? Stdio,
    HttpTransportRequest? Http,
    int OperationTimeoutSeconds = 30,
    IReadOnlyDictionary<string, string>? Secrets = null);

internal sealed record UpdateServerRequest(
    string? Name,
    string? Description,
    bool Enabled,
    McpTransportKind Transport,
    StdioTransportRequest? Stdio,
    HttpTransportRequest? Http,
    int OperationTimeoutSeconds = 30,
    IReadOnlyDictionary<string, string>? Secrets = null);
