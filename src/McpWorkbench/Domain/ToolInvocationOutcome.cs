using System.Text.Json;
using System.Text.Json.Serialization;
using McpWorkbench.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(McpContentKindJsonConverter))]
internal enum McpContentKind
{
    Text,
    Image,
    EmbeddedResource,
    ResourceLink,
    Unknown
}

internal sealed record McpContentBlock(
    McpContentKind Kind,
    string? Text,
    string? DataBase64,
    string? MimeType,
    string? Uri,
    string? Name,
    long? Size,
    JsonElement? Raw);

internal sealed record ToolInvocationOutcome(
    bool IsError,
    IReadOnlyList<McpContentBlock> Content,
    JsonElement? StructuredContent,
    JsonElement RawResult,
    bool WasTruncated);
