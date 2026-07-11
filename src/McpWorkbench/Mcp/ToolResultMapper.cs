using System.Text.Json;
using McpWorkbench.Domain;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.Mcp;

internal static class ToolResultMapper
{
    public static ToolInvocationOutcome Map(CallToolResult result, int maximumResultBytes)
    {
        var resultTypeInfo = McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(CallToolResult));
        var rawBytes = new BoundedByteBufferWriter(maximumResultBytes);
        using (var writer = new Utf8JsonWriter(rawBytes))
        {
            JsonSerializer.Serialize(writer, result, resultTypeInfo);
        }

        using var rawDocument = JsonDocument.Parse(rawBytes.WrittenMemory);
        var blocks = result.Content.Select(MapBlock).ToArray();
        return new ToolInvocationOutcome(
            result.IsError == true,
            blocks,
            result.StructuredContent?.Clone(),
            rawDocument.RootElement.Clone(),
            false);
    }

    private static McpContentBlock MapBlock(ContentBlock block) => block switch
    {
        TextContentBlock text => new McpContentBlock(
            McpContentKind.Text, text.Text, null, null, null, null, null, null),
        ImageContentBlock image => new McpContentBlock(
            McpContentKind.Image, null, Convert.ToBase64String(image.Data.Span), image.MimeType, null, null, image.Data.Length, null),
        EmbeddedResourceBlock embedded => MapEmbeddedResource(embedded),
        ResourceLinkBlock link => new McpContentBlock(
            McpContentKind.ResourceLink, null, null, link.MimeType, link.Uri, link.Name, link.Size, null),
        _ => new McpContentBlock(
            McpContentKind.Unknown, null, null, null, null, null, null, SerializeUnknown(block))
    };

    private static McpContentBlock MapEmbeddedResource(EmbeddedResourceBlock embedded) => embedded.Resource switch
    {
        TextResourceContents text => new McpContentBlock(
            McpContentKind.EmbeddedResource, text.Text, null, text.MimeType, text.Uri, null, null, null),
        BlobResourceContents blob => new McpContentBlock(
            McpContentKind.EmbeddedResource, null, Convert.ToBase64String(blob.Blob.Span), blob.MimeType, blob.Uri, null, blob.Blob.Length, null),
        _ => new McpContentBlock(
            McpContentKind.Unknown, null, null, embedded.Resource.MimeType, embedded.Resource.Uri, null, null, SerializeUnknown(embedded))
    };

    private static JsonElement SerializeUnknown(ContentBlock block) =>
        JsonSerializer.SerializeToElement(
            block,
            McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ContentBlock)));
}
