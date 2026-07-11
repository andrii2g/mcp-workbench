using A2G.McpWorkbench.Mcp;
using ModelContextProtocol;

namespace A2G.McpWorkbench.UnitTests.Mcp;

public sealed class McpSdkErrorNormalizerTests
{
    [Fact]
    public void Normalize_WhenInitializationThrowsMcpException_ReturnsSafeInitializationError()
    {
        var exception = McpSdkErrorNormalizer.Normalize(new McpException("sentinel remote details"), "initialization");

        Assert.Equal("mcp_initialization_failed", exception.Code);
        Assert.DoesNotContain("sentinel", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_WhenOperationIsCancelled_ReturnsCancellationError()
    {
        var exception = McpSdkErrorNormalizer.Normalize(new OperationCanceledException(), "ping");

        Assert.Equal("operation_cancelled", exception.Code);
    }
}
