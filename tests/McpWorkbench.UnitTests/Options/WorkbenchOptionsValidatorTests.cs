using McpWorkbench.Options;

namespace McpWorkbench.UnitTests.Options;

public sealed class WorkbenchOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenDefaultsAreUsed_Succeeds()
    {
        var result = new WorkbenchOptionsValidator().Validate(null, new WorkbenchOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenNumericLimitsAreInvalid_ReturnsAllFailures()
    {
        var options = new WorkbenchOptions
        {
            RegistryPath = " ",
            ConnectTimeoutSeconds = 0,
            PingTimeoutSeconds = 0,
            DefaultOperationTimeoutSeconds = 10,
            MaximumOperationTimeoutSeconds = 5,
            MaximumArgumentsBytes = 0,
            MaximumResultBytes = 0,
            MaximumHistoryEntriesPerServer = 0
        };

        var result = new WorkbenchOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(7, result.Failures.Count());
    }

    [Fact]
    public void Validate_WhenAllowlistsAreNullOrContainEmptyEntries_ReturnsFailures()
    {
        var options = new WorkbenchOptions
        {
            AllowedStdioCommands = null!,
            AllowedHttpHosts = ["example.test", " "]
        };

        var result = new WorkbenchOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("AllowedStdioCommands", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("AllowedHttpHosts", StringComparison.Ordinal));
    }
}
