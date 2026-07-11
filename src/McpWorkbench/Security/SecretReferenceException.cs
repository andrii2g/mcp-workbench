namespace A2G.McpWorkbench.Security;

internal sealed class SecretReferenceException : Exception
{
    public SecretReferenceException(string code, string variableName, string message)
        : base(message)
    {
        Code = code;
        VariableName = variableName;
    }

    public string Code { get; }
    public string VariableName { get; }
}
