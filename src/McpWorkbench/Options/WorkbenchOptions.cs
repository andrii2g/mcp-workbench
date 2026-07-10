namespace McpWorkbench.Options;

internal sealed class WorkbenchOptions
{
    public const string SectionName = "McpWorkbench";

    public string RegistryPath { get; init; } = "data/servers.json";
    public bool BindToLoopbackOnly { get; init; } = true;
    public int ConnectTimeoutSeconds { get; init; } = 15;
    public int PingTimeoutSeconds { get; init; } = 5;
    public int DefaultOperationTimeoutSeconds { get; init; } = 30;
    public int MaximumOperationTimeoutSeconds { get; init; } = 300;
    public int MaximumArgumentsBytes { get; init; } = 262_144;
    public int MaximumResultBytes { get; init; } = 4_194_304;
    public int MaximumHistoryEntriesPerServer { get; init; } = 50;
    public bool LoadToolsOnConnect { get; init; } = true;
    public bool AllowStdioServers { get; init; } = true;
    public bool AllowHttpServers { get; init; } = true;
    public string[] AllowedStdioCommands { get; init; } = [];
    public string[] AllowedHttpHosts { get; init; } = [];
}
