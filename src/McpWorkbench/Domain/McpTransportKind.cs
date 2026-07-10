using System.Text.Json.Serialization;
using McpWorkbench.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(McpTransportKindJsonConverter))]
internal enum McpTransportKind
{
    Stdio,
    Http
}
