namespace A2G.McpWorkbench.Domain;

internal sealed record StdioTransportSettings(
    string Command,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    int ShutdownTimeoutSeconds);
