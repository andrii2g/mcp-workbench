using A2G.McpWorkbench.Security;
using Microsoft.Extensions.Logging;

namespace A2G.McpWorkbench.UnitTests.Security;

public sealed class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("Authorization")]
    [InlineData("authorization")]
    [InlineData("X-API-KEY")]
    [InlineData("client-secret")]
    [InlineData("Password")]
    public void RedactValue_WhenKeyIsSensitive_RedactsEntireValue(string key)
    {
        var result = SensitiveDataRedactor.RedactValue(key, "sentinel-secret");

        Assert.Equal(SensitiveDataRedactor.RedactedValue, result);
    }

    [Fact]
    public void RedactValue_WhenValueContainsReference_RedactsReferenceText()
    {
        var result = SensitiveDataRedactor.RedactValue("custom", "Bearer ${ENV:TOKEN}");

        Assert.Equal(SensitiveDataRedactor.RedactedValue, result);
    }

    [Fact]
    public void RedactText_WhenTextContainsResolvedValues_ReplacesExactValuesLongestFirst()
    {
        IReadOnlySet<string> values = new HashSet<string>(["secret", "secret-long"], StringComparer.Ordinal);

        var result = SensitiveDataRedactor.RedactText("A secret-long and secret", values);

        Assert.Equal("A [REDACTED] and [REDACTED]", result);
    }

    [Fact]
    public void RedactDictionary_WhenMixedKeys_PreservesSafeValues()
    {
        var values = new Dictionary<string, string>
        {
            ["Authorization"] = "sentinel-secret",
            ["X-Correlation-Id"] = "safe-value"
        };

        var result = SensitiveDataRedactor.RedactDictionary(values);

        Assert.Equal("[REDACTED]", result["Authorization"]);
        Assert.Equal("safe-value", result["X-Correlation-Id"]);
    }

    [Fact]
    public void RedactExceptionMessage_WhenCapturedInLog_DoesNotContainSentinelSecret()
    {
        const string sentinel = "phase-three-sentinel-secret";
        var logger = new CapturingLogger();
        var safeMessage = SensitiveDataRedactor.RedactExceptionMessage(
            new InvalidOperationException($"Remote failure included {sentinel}."),
            new HashSet<string>([sentinel], StringComparer.Ordinal));

        logger.Log(
            LogLevel.Error,
            new EventId(1, "SecretRedactionTest"),
            $"Connection failed: {safeMessage}",
            null,
            static (state, _) => state);

        Assert.DoesNotContain(sentinel, logger.Messages.Single(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", logger.Messages.Single(), StringComparison.Ordinal);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
