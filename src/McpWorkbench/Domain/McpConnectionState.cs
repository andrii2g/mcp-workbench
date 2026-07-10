using System.Text.Json.Serialization;
using McpWorkbench.Serialization;

namespace McpWorkbench.Domain;

[JsonConverter(typeof(McpConnectionStateJsonConverter))]
internal enum McpConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Faulted
}
