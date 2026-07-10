using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.UnitTests.Mcp;

public sealed class McpClientSessionTests
{
    [Fact]
    public async Task Operations_WhenSdkClientSucceeds_MapThroughAppOwnedBoundary()
    {
        var fake = new FakeMcpSdkClient();
        await using var session = Session(fake);

        await session.PingAsync(TestContext.Current.CancellationToken);
        var tools = await session.ListToolsAsync(TestContext.Current.CancellationToken);
        var result = await session.InvokeToolAsync("echo", Json("""{"text":"hello"}"""), TestContext.Current.CancellationToken);

        Assert.True(fake.PingCalled);
        Assert.Collection(tools, tool => Assert.Equal("echo", tool.Name));
        Assert.Equal("hello", result.Content[0].Text);
        Assert.Equal("echo", fake.LastCall?.Name);
        Assert.Equal("hello", fake.LastCall?.Arguments?["text"].GetString());
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_DisposesSdkClientOnce()
    {
        var fake = new FakeMcpSdkClient();
        var session = Session(fake);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, fake.DisposeCount);
    }

    [Fact]
    public async Task InvokeToolAsync_WhenArgumentsAreNotObject_RejectsBeforeSdkCall()
    {
        var fake = new FakeMcpSdkClient();
        await using var session = Session(fake);

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () =>
            await session.InvokeToolAsync("echo", Json("[]"), TestContext.Current.CancellationToken));

        Assert.Equal("tool_arguments_invalid", exception.Code);
        Assert.Null(fake.LastCall);
    }

    private static McpClientSession Session(IMcpSdkClient client) => new(
        client,
        new McpSessionInfo(
            "2025-11-25",
            new McpRemoteIdentity("Fake", "1.0", null),
            new McpCapabilitySnapshot(true, false),
            null),
        64_000);

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class FakeMcpSdkClient : IMcpSdkClient
    {
        public bool PingCalled { get; private set; }
        public CallToolRequestParams? LastCall { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask PingAsync(CancellationToken cancellationToken)
        {
            PingCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<ListToolsResult> ListToolsAsync(string? cursor, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ListToolsResult
            {
                Tools = [new Tool { Name = "echo", InputSchema = Json("""{"type":"object"}""") }]
            });

        public ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken cancellationToken)
        {
            LastCall = request;
            return ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = request.Arguments?["text"].GetString() ?? string.Empty }]
            });
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
