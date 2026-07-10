using System.Text.Json.Serialization;
using McpWorkbench.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(McpHttpModeJsonConverter))]
internal enum McpHttpMode
{
    Auto,
    StreamableHttp,
    LegacySse
}
