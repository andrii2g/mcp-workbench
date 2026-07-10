using System.Text.Json.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<McpConnectionState>))]
internal enum McpConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Faulted
}
