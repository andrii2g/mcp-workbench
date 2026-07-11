using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Validation;

namespace McpWorkbench.Contracts;

internal sealed record ApiMeta(string RequestId, DateTimeOffset TimestampUtc);
internal sealed record ApiResponse<T>(T Data, ApiMeta Meta);
internal sealed record ApiError(string Code, string Message, IReadOnlyList<ValidationError>? Details);
internal sealed record ApiErrorResponse(ApiError Error, ApiMeta Meta);
internal sealed record ConnectRequest(bool ForceReconnect = false);
internal sealed record InvokeToolRequest(JsonElement? Arguments, int? TimeoutSeconds);

internal sealed record ServerDefinitionResponse(
    Guid Id,
    string Name,
    string? Description,
    McpTransportKind Transport,
    bool Enabled,
    StdioTransportRequest? Stdio,
    HttpTransportRequest? Http,
    int OperationTimeoutSeconds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ServerRuntimeSnapshot? Runtime);

internal sealed record PingResponse(bool Success, long DurationMilliseconds, DateTimeOffset TimestampUtc);
internal sealed record RemoteServerResponse(string? Name, string? Version);
internal sealed record McpCapabilitiesResponse(bool Tools, bool ToolsListChanged);
internal sealed record ConnectResponse(
    McpConnectionState Status,
    DateTimeOffset? ConnectedAtUtc,
    long ConnectDurationMilliseconds,
    string? ProtocolVersion,
    RemoteServerResponse Server,
    McpCapabilitiesResponse Capabilities);

internal sealed record ToolInvocationResponse(
    Guid ServerId,
    string ToolName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMilliseconds,
    bool IsError,
    IReadOnlyList<McpContentBlockResponse> Content,
    JsonElement? StructuredContent,
    JsonElement Raw,
    bool WasTruncated);

internal sealed record McpContentBlockResponse(
    string Type,
    string? Text,
    string? DataBase64,
    string? MimeType,
    string? Uri,
    string? Name,
    long? Size,
    JsonElement? Raw);
