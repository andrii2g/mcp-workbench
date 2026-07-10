using Microsoft.Extensions.Options;

namespace McpWorkbench.Options;

internal sealed class WorkbenchOptionsValidator : IValidateOptions<WorkbenchOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkbenchOptions options)
    {
        var failures = new List<string>();

        RequireText(options.RegistryPath, nameof(options.RegistryPath), failures);
        RequirePositive(options.ConnectTimeoutSeconds, nameof(options.ConnectTimeoutSeconds), failures);
        RequirePositive(options.PingTimeoutSeconds, nameof(options.PingTimeoutSeconds), failures);
        RequirePositive(options.DefaultOperationTimeoutSeconds, nameof(options.DefaultOperationTimeoutSeconds), failures);
        RequirePositive(options.MaximumOperationTimeoutSeconds, nameof(options.MaximumOperationTimeoutSeconds), failures);
        RequirePositive(options.MaximumArgumentsBytes, nameof(options.MaximumArgumentsBytes), failures);
        RequirePositive(options.MaximumResultBytes, nameof(options.MaximumResultBytes), failures);
        RequirePositive(options.MaximumHistoryEntriesPerServer, nameof(options.MaximumHistoryEntriesPerServer), failures);

        if (options.DefaultOperationTimeoutSeconds > options.MaximumOperationTimeoutSeconds)
        {
            failures.Add("DefaultOperationTimeoutSeconds must not exceed MaximumOperationTimeoutSeconds.");
        }

        ValidateAllowlist(options.AllowedStdioCommands, nameof(options.AllowedStdioCommands), failures);
        ValidateAllowlist(options.AllowedHttpHosts, nameof(options.AllowedHttpHosts), failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireText(string? value, string optionName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{optionName} must not be empty.");
        }
    }

    private static void RequirePositive(int value, string optionName, List<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{optionName} must be positive.");
        }
    }

    private static void ValidateAllowlist(string[]? values, string optionName, List<string> failures)
    {
        if (values is null)
        {
            failures.Add($"{optionName} must not be null.");
            return;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{optionName} must not contain empty entries.");
        }
    }
}
