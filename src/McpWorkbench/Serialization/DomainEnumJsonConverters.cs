using System.Text.Json;
using System.Text.Json.Serialization;
using A2G.McpWorkbench.Domain;

namespace A2G.McpWorkbench.Serialization;

internal sealed class McpTransportKindJsonConverter : JsonConverter<McpTransportKind>
{
    public McpTransportKindJsonConverter()
    {
    }

    public override McpTransportKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "stdio" => McpTransportKind.Stdio,
            "http" => McpTransportKind.Http,
            _ => throw new JsonException("Transport must be 'stdio' or 'http'.")
        };

    public override void Write(Utf8JsonWriter writer, McpTransportKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpTransportKind.Stdio => "stdio",
            McpTransportKind.Http => "http",
            _ => throw new JsonException("Unsupported transport value.")
        });
}

internal sealed class McpHttpModeJsonConverter : JsonConverter<McpHttpMode>
{
    public McpHttpModeJsonConverter()
    {
    }

    public override McpHttpMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "auto" => McpHttpMode.Auto,
            "streamableHttp" => McpHttpMode.StreamableHttp,
            "legacySse" => McpHttpMode.LegacySse,
            _ => throw new JsonException("HTTP mode is unsupported.")
        };

    public override void Write(Utf8JsonWriter writer, McpHttpMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpHttpMode.Auto => "auto",
            McpHttpMode.StreamableHttp => "streamableHttp",
            McpHttpMode.LegacySse => "legacySse",
            _ => throw new JsonException("Unsupported HTTP mode value.")
        });
}

internal sealed class McpConnectionStateJsonConverter : JsonConverter<McpConnectionState>
{
    public McpConnectionStateJsonConverter()
    {
    }

    public override McpConnectionState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "disconnected" => McpConnectionState.Disconnected,
            "connecting" => McpConnectionState.Connecting,
            "connected" => McpConnectionState.Connected,
            "disconnecting" => McpConnectionState.Disconnecting,
            "faulted" => McpConnectionState.Faulted,
            _ => throw new JsonException("Connection state is unsupported.")
        };

    public override void Write(Utf8JsonWriter writer, McpConnectionState value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpConnectionState.Disconnected => "disconnected",
            McpConnectionState.Connecting => "connecting",
            McpConnectionState.Connected => "connected",
            McpConnectionState.Disconnecting => "disconnecting",
            McpConnectionState.Faulted => "faulted",
            _ => throw new JsonException("Unsupported connection state value.")
        });
}

internal sealed class McpContentKindJsonConverter : JsonConverter<McpContentKind>
{
    public override McpContentKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "text" => McpContentKind.Text,
            "image" => McpContentKind.Image,
            "embeddedResource" => McpContentKind.EmbeddedResource,
            "resourceLink" => McpContentKind.ResourceLink,
            "unknown" => McpContentKind.Unknown,
            _ => throw new JsonException("MCP content kind is unsupported.")
        };

    public override void Write(Utf8JsonWriter writer, McpContentKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpContentKind.Text => "text",
            McpContentKind.Image => "image",
            McpContentKind.EmbeddedResource => "embeddedResource",
            McpContentKind.ResourceLink => "resourceLink",
            McpContentKind.Unknown => "unknown",
            _ => throw new JsonException("Unsupported MCP content kind value.")
        });
}
