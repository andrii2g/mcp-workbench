namespace McpWorkbench.Validation;

internal sealed record ValidationResult<T>(T? Value, IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
