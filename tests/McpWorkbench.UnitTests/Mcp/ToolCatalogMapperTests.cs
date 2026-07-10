using System.Text.Json;
using McpWorkbench.Mcp;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.UnitTests.Mcp;

public sealed class ToolCatalogMapperTests
{
    [Fact]
    public void Map_WhenToolIsValid_ClonesSchemaAndAnnotations()
    {
        using var document = JsonDocument.Parse("""{"type":"object","customKeyword":true}""");
        var tool = new Tool
        {
            Name = "echo",
            Title = "Echo",
            Description = "Returns text.",
            InputSchema = document.RootElement,
            Annotations = new ToolAnnotations { ReadOnlyHint = true }
        };

        var result = ToolCatalogMapper.Map([tool]);

        Assert.Equal("echo", result[0].Name);
        Assert.True(result[0].InputSchema.GetProperty("customKeyword").GetBoolean());
        Assert.True(result[0].Annotations.ReadOnlyHint);
    }

    [Fact]
    public void Map_WhenNamesAreDuplicatedOrdinally_ThrowsProtocolError()
    {
        var tools = new[] { Tool("echo"), Tool("echo") };

        var exception = Assert.Throws<McpSessionException>(() => ToolCatalogMapper.Map(tools));

        Assert.Equal("mcp_protocol_error", exception.Code);
    }

    [Fact]
    public void Map_WhenCatalogExceedsCountLimit_ThrowsSizeError()
    {
        var tools = Enumerable.Range(0, 1_001).Select(index => Tool($"tool-{index}")).ToArray();

        var exception = Assert.Throws<McpSessionException>(() => ToolCatalogMapper.Map(tools));

        Assert.Equal("tool_catalog_too_large", exception.Code);
    }

    private static Tool Tool(string name)
    {
        using var document = JsonDocument.Parse("""{"type":"object"}""");
        return new Tool { Name = name, InputSchema = document.RootElement.Clone() };
    }
}
