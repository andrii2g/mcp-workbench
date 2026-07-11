namespace A2G.McpWorkbench.Validation;

internal sealed record ValidationError(string Field, string Code, string Message);
