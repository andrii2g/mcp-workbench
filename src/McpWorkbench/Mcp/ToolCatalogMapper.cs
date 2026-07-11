using System.Text;
using McpWorkbench.Domain;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.Mcp;

internal static class ToolCatalogMapper
{
    internal const int MaximumToolCount = 1_000;
    private const int MaximumToolNameLength = 256;
    private const int MaximumDescriptionLength = 8_192;
    private const int MaximumAggregateSchemaBytes = 1_048_576;

    public static IReadOnlyList<ToolCatalogEntry> Map(IReadOnlyList<Tool> tools)
    {
        if (tools.Count > MaximumToolCount)
        {
            throw new McpSessionException("tool_catalog_unavailable", "MCP tool catalog exceeds the supported tool count.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var mapped = new List<ToolCatalogEntry>(tools.Count);
        var schemaBytes = 0;
        foreach (var tool in tools)
        {
            if (string.IsNullOrEmpty(tool.Name) || tool.Name.Length > MaximumToolNameLength || !names.Add(tool.Name))
            {
                throw new McpSessionException("tool_protocol_error", "MCP tool catalog contains an invalid or duplicate tool name.");
            }

            if (tool.Description?.Length > MaximumDescriptionLength)
            {
                throw new McpSessionException("tool_catalog_unavailable", "MCP tool description exceeds the supported size.");
            }

            var inputSchema = tool.InputSchema.Clone();
            var outputSchema = tool.OutputSchema?.Clone();
            schemaBytes += Encoding.UTF8.GetByteCount(inputSchema.GetRawText());
            if (outputSchema is not null)
            {
                schemaBytes += Encoding.UTF8.GetByteCount(outputSchema.Value.GetRawText());
            }

            if (schemaBytes > MaximumAggregateSchemaBytes)
            {
                throw new McpSessionException("tool_catalog_unavailable", "MCP tool schemas exceed the supported aggregate size.");
            }

            var annotations = tool.Annotations;
            mapped.Add(new ToolCatalogEntry(
                tool.Name,
                tool.Title,
                tool.Description,
                inputSchema,
                outputSchema,
                new McpToolAnnotations(
                    annotations?.Title,
                    annotations?.ReadOnlyHint,
                    annotations?.DestructiveHint,
                    annotations?.IdempotentHint,
                    annotations?.OpenWorldHint)));
        }

        return mapped;
    }
}
