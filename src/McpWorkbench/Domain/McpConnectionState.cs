using System.Text.Json.Serialization;
using A2G.McpWorkbench.Serialization;

namespace A2G.McpWorkbench.Domain;

[JsonConverter(typeof(McpConnectionStateJsonConverter))]
internal enum McpConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Faulted
}
