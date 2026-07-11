namespace A2G.McpWorkbench.Persistence;

internal sealed class RegistryException : Exception
{
    public RegistryException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public RegistryException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
