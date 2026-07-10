using System.Text.Json.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<McpTransportKind>))]
internal enum McpTransportKind
{
    Stdio,
    Http
}
