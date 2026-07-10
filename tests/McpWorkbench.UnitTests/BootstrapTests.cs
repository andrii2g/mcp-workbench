namespace McpWorkbench.UnitTests;

public sealed class BootstrapTests
{
    [Fact]
    public void TestAssembly_Loads()
    {
        Assert.NotNull(typeof(Program).Assembly);
    }
}
