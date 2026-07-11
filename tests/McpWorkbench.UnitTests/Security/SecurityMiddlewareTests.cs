using A2G.McpWorkbench.Security;

namespace A2G.McpWorkbench.UnitTests.Security;

public sealed class SecurityMiddlewareTests
{
    [Theory]
    [InlineData("secret", "secret", true)]
    [InlineData("secret", "wrong", false)]
    [InlineData("secret", null, false)]
    public void Matches_ReturnsExpectedResult(string expected, string? supplied, bool result) =>
        Assert.Equal(result, SecurityMiddleware.Matches(expected, supplied));
}
