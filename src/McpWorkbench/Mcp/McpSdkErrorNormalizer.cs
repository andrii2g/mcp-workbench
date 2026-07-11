using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace A2G.McpWorkbench.Mcp;

internal static class McpSdkErrorNormalizer
{
    public static McpSessionException Normalize(Exception exception, string operation) => exception switch
    {
        McpSessionException normalized => normalized,
        ClientTransportClosedException => new McpSessionException(
            "mcp_transport_closed",
            $"MCP transport closed during {operation}."),
        HttpRequestException => new McpSessionException(
            "mcp_transport_failed",
            $"MCP HTTP transport failed during {operation}."),
        JsonException => new McpSessionException(
            "mcp_protocol_error",
            $"MCP protocol operation '{operation}' returned malformed JSON."),
        McpException => new McpSessionException(
            operation == "initialization" ? "mcp_initialization_failed" : "mcp_protocol_error",
            $"MCP protocol operation '{operation}' failed."),
        OperationCanceledException => new McpSessionException(
            "operation_cancelled",
            $"MCP operation '{operation}' was cancelled."),
        _ => new McpSessionException(
            "mcp_transport_failed",
            $"MCP operation '{operation}' failed.")
    };
}
