using System.Text.Json;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Mcp;
using ModelContextProtocol.Protocol;

namespace A2G.McpWorkbench.UnitTests.Mcp;

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

    [Fact]
    public async Task ListToolsAsync_WhenCursorRepeats_ThrowsProtocolError()
    {
        var fake = new FakeMcpSdkClient
        {
            ListToolsHandler = _ => new ListToolsResult { Tools = [], NextCursor = "repeat" }
        };
        await using var session = Session(fake);

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () =>
            await session.ListToolsAsync(TestContext.Current.CancellationToken));

        Assert.Equal("tool_protocol_error", exception.Code);
    }

    [Fact]
    public async Task ListToolsAsync_WhenPageExceedsCatalogLimit_RejectsBeforeAccumulating()
    {
        var fake = new FakeMcpSdkClient
        {
            ListToolsHandler = cursor =>
            {
                var index = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
                return new ListToolsResult
                {
                    Tools = [new Tool { Name = "echo", InputSchema = Json("""{"type":"object"}""") }],
                    NextCursor = index < ToolCatalogMapper.MaximumToolCount ?
                        (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null
                };
            }
        };
        await using var session = Session(fake);

        var exception = await Assert.ThrowsAsync<McpSessionException>(async () =>
            await session.ListToolsAsync(TestContext.Current.CancellationToken));

        Assert.Null(fake.ListToolsHandlerException);
        Assert.Equal("tool_catalog_unavailable", exception.Code);
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
        public Func<string?, ListToolsResult>? ListToolsHandler { get; init; }
        public Exception? ListToolsHandlerException { get; private set; }

        public ValueTask PingAsync(CancellationToken cancellationToken)
        {
            PingCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<ListToolsResult> ListToolsAsync(string? cursor, CancellationToken cancellationToken)
        {
            try
            {
                return ValueTask.FromResult(ListToolsHandler?.Invoke(cursor) ?? new ListToolsResult
                {
                    Tools = [new Tool { Name = "echo", InputSchema = Json("""{"type":"object"}""") }]
                });
            }
            catch (Exception exception)
            {
                ListToolsHandlerException = exception;
                throw;
            }
        }

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
