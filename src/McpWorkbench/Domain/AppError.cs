namespace A2G.McpWorkbench.Domain;

internal sealed record AppError(string Code, string Message);

internal readonly record struct Result<T>(bool IsSuccess, T? Value, AppError? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string code, string message) =>
        new(false, default, new AppError(code, message));
}
