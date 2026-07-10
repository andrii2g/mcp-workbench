namespace McpWorkbench.Mcp;

internal sealed class McpSessionException : Exception
{
    public McpSessionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
