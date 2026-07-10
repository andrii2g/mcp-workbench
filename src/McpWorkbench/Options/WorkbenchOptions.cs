namespace McpWorkbench.Options;

internal sealed class WorkbenchOptions
{
    public const string SectionName = "McpWorkbench";

    public string RegistryPath { get; set; } = "data/servers.json";
    public bool BindToLoopbackOnly { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 15;
    public int PingTimeoutSeconds { get; set; } = 5;
    public int DefaultOperationTimeoutSeconds { get; set; } = 30;
    public int MaximumOperationTimeoutSeconds { get; set; } = 300;
    public int MaximumArgumentsBytes { get; set; } = 262_144;
    public int MaximumResultBytes { get; set; } = 4_194_304;
    public int MaximumHistoryEntriesPerServer { get; set; } = 50;
    public bool LoadToolsOnConnect { get; set; } = true;
    public bool AllowStdioServers { get; set; } = true;
    public bool AllowHttpServers { get; set; } = true;
    public string[] AllowedStdioCommands { get; set; } = [];
    public string[] AllowedHttpHosts { get; set; } = [];
}
