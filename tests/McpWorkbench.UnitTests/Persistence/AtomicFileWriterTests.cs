using System.Text;
using McpWorkbench.Persistence;

namespace McpWorkbench.UnitTests.Persistence;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public async Task WriteAsync_WhenDestinationExists_ReplacesContentsAndRemovesTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mcp-workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "servers.json");
        await File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken);

        try
        {
            await new AtomicFileWriter().WriteAsync(path, Encoding.UTF8.GetBytes("new"), TestContext.Current.CancellationToken);

            Assert.Equal("new", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_WhenStaleTemporaryFileExists_PreservesTemporaryAndDestinationFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mcp-workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "servers.json");
        await File.WriteAllTextAsync(path, "current", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path + ".tmp", "stale", TestContext.Current.CancellationToken);

        try
        {
            await Assert.ThrowsAsync<IOException>(async () =>
                await new AtomicFileWriter().WriteAsync(path, Encoding.UTF8.GetBytes("new"), TestContext.Current.CancellationToken));

            Assert.Equal("current", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal("stale", await File.ReadAllTextAsync(path + ".tmp", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
