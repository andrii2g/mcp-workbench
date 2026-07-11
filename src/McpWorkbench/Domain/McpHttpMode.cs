using System.Text.Json.Serialization;
using A2G.McpWorkbench.Serialization;

namespace A2G.McpWorkbench.Domain;

[JsonConverter(typeof(McpHttpModeJsonConverter))]
internal enum McpHttpMode
{
    Auto,
    StreamableHttp,
    LegacySse
}
