namespace A2G.McpWorkbench.Options;

internal sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string? ApiKey { get; set; }
    public bool ProtectStaticUi { get; set; }
    public int TrustedProxyCount { get; set; }
}
