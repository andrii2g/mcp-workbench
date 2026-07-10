namespace McpWorkbench.Security;

internal interface IEnvironmentValueProvider
{
    string? GetValue(string name);
}
