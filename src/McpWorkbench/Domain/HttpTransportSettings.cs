namespace McpWorkbench.Domain;

internal sealed record HttpTransportSettings(
    string Endpoint,
    McpHttpMode Mode,
    IReadOnlyDictionary<string, string> Headers);
