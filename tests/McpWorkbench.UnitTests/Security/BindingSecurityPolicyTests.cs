using McpWorkbench.Security;
using Microsoft.Extensions.Logging;

namespace McpWorkbench.UnitTests.Security;

public sealed class BindingSecurityPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5070", true, false, 0)]
    [InlineData("http://0.0.0.0:5070", true, false, 3)]
    [InlineData("http://0.0.0.0:5070", false, false, 2)]
    [InlineData("http://0.0.0.0:5070", false, true, 1)]
    public void Evaluate_ClassifiesBinding(string url, bool loopbackOnly, bool apiKey, int expected) =>
        Assert.Equal(expected, (int)BindingSecurityPolicy.Evaluate([url], loopbackOnly, apiKey));

    [Fact]
    public void RemoteBindingWarning_IsClearAndContainsNoCredential()
    {
        var logger = new CapturingLogger();

        StartupLog.RemoteBindingWithoutApiKey(logger);

        Assert.Equal("MCP Workbench is bound beyond loopback without API-key protection", logger.Message);
        Assert.DoesNotContain("key=", logger.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLogger : ILogger
    {
        public string Message { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Message = formatter(state, exception);
    }
}
