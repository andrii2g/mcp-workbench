namespace A2G.McpWorkbench.Domain;

internal sealed record HttpTransportSettings(
    string Endpoint,
    McpHttpMode Mode,
    IReadOnlyDictionary<string, string> Headers,
    HttpAuthorizationSettings? Authorization = null);
