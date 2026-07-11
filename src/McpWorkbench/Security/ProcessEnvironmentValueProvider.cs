namespace A2G.McpWorkbench.Security;

internal sealed class ProcessEnvironmentValueProvider : IEnvironmentValueProvider
{
    public string? GetValue(string name) => Environment.GetEnvironmentVariable(name);
}
