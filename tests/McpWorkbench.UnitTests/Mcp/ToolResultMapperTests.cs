using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.UnitTests.Mcp;

public sealed class ToolResultMapperTests
{
    [Fact]
    public void Map_WhenResultContainsKnownBlocksAndStructuredContent_MapsFields()
    {
        using var structured = JsonDocument.Parse("""{"answer":42}""");
        var result = new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock { Text = "failed" },
                ImageContentBlock.FromBytes(new byte[] { 1, 2, 3 }, "image/png"),
                new ResourceLinkBlock { Uri = "file:///test", Name = "test", MimeType = "text/plain", Size = 3 }
            ],
            StructuredContent = structured.RootElement.Clone()
        };

        var mapped = ToolResultMapper.Map(result, 64_000);

        Assert.True(mapped.IsError);
        Assert.Equal([McpContentKind.Text, McpContentKind.Image, McpContentKind.ResourceLink], mapped.Content.Select(block => block.Kind));
        Assert.Equal(42, mapped.StructuredContent?.GetProperty("answer").GetInt32());
        Assert.True(mapped.RawResult.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void Map_WhenContentTypeIsUnknown_PreservesBoundedRawJson()
    {
        var result = new CallToolResult
        {
            Content = [AudioContentBlock.FromBytes(new byte[] { 1, 2 }, "audio/wav")]
        };

        var mapped = ToolResultMapper.Map(result, 64_000);

        Assert.Equal(McpContentKind.Unknown, mapped.Content[0].Kind);
        Assert.Equal("audio", mapped.Content[0].Raw?.GetProperty("type").GetString());
    }

    [Fact]
    public void Map_WhenResultExceedsLimit_ThrowsSizeError()
    {
        var result = new CallToolResult { Content = [new TextContentBlock { Text = new string('x', 1_000) }] };

        var exception = Assert.Throws<McpSessionException>(() => ToolResultMapper.Map(result, 100));

        Assert.Equal("result_too_large", exception.Code);
    }
}
