using System.Text.Json.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<McpHttpMode>))]
internal enum McpHttpMode
{
    Auto,
    StreamableHttp,
    LegacySse
}
