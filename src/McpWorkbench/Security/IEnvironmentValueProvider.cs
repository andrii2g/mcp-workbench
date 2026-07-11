namespace A2G.McpWorkbench.Security;

internal interface IEnvironmentValueProvider
{
    string? GetValue(string name);
}
