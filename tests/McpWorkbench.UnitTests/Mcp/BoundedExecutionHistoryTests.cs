using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Mcp;

namespace A2G.McpWorkbench.UnitTests.Mcp;

public sealed class BoundedExecutionHistoryTests
{
    [Fact]
    public void Add_WhenCapacityIsExceeded_KeepsNewestEntriesInOrder()
    {
        var history = new BoundedExecutionHistory(2);
        history.Add(Record("first"));
        history.Add(Record("second"));
        history.Add(Record("third"));

        var snapshot = history.Snapshot();

        Assert.Equal(["second", "third"], snapshot.Select(entry => entry.ToolName));
    }

    private static ToolExecutionRecord Record(string name) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        name,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        0,
        ToolExecutionStatus.Succeeded,
        false,
        null);
}
