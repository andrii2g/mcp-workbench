using System.Text.Json;

namespace McpWorkbench.Domain;

internal sealed record McpToolAnnotations(
    string? Title,
    bool? ReadOnlyHint,
    bool? DestructiveHint,
    bool? IdempotentHint,
    bool? OpenWorldHint);

internal sealed record ToolCatalogEntry(
    string Name,
    string? Title,
    string? Description,
    JsonElement InputSchema,
    JsonElement? OutputSchema,
    McpToolAnnotations Annotations);
