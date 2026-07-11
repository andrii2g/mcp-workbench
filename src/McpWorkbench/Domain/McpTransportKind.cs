using System.Text.Json.Serialization;
using A2G.McpWorkbench.Serialization;

namespace A2G.McpWorkbench.Domain;

[JsonConverter(typeof(McpTransportKindJsonConverter))]
internal enum McpTransportKind
{
    Stdio,
    Http
}
