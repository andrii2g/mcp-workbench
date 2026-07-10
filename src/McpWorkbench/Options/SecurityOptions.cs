namespace McpWorkbench.Options;

internal sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string? ApiKey { get; init; }
    public bool ProtectStaticUi { get; init; }
    public int TrustedProxyCount { get; init; }
}
