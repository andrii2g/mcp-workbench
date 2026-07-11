using System.Text.Json;
using McpWorkbench.Contracts;
using McpWorkbench.Mcp;
using McpWorkbench.Persistence;
using McpWorkbench.Serialization;

namespace McpWorkbench.Api;

internal sealed class ApiMiddleware(RequestDelegate next, TimeProvider timeProvider, ILogger<ApiMiddleware> logger)
{
    public const string RequestIdItemKey = "McpWorkbench.RequestId";
    public const string RequestIdHeader = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = GetRequestId(context);
        context.Items[RequestIdItemKey] = requestId;
        context.Response.Headers[RequestIdHeader] = requestId;
        try
        {
            await next(context);
            if (IsApiRequest(context) && !context.Response.HasStarted && context.Response.StatusCode >= 400)
            {
                var (code, message) = MapFrameworkStatus(context.Response.StatusCode);
                await WriteErrorAsync(context, requestId, context.Response.StatusCode, code, message);
            }
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, code, message) = Map(exception);
            ApiLog.RequestFailed(logger, requestId, code);
            await WriteErrorAsync(context, requestId, status, code, message);
        }
    }

    private static string GetRequestId(HttpContext context)
    {
        var supplied = context.Request.Headers[RequestIdHeader].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 128
            ? supplied
            : context.TraceIdentifier;
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        RegistryException registry => (StatusFor(registry.Code), registry.Code, registry.Message),
        McpSessionException session => (StatusFor(session.Code), session.Code, session.Message),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "server_definition_invalid", "The request body is invalid."),
        OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "tool_call_cancelled", "The request was cancelled."),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.")
    };

    private static int StatusFor(string code) => code switch
    {
        "server_not_found" or "tool_not_found" => StatusCodes.Status404NotFound,
        "server_name_conflict" or "server_id_conflict" or "server_not_connected" or "server_disabled" or
            "disconnection_failed" => StatusCodes.Status409Conflict,
        "server_definition_invalid" or "invalid_operation_timeout" => StatusCodes.Status400BadRequest,
        "request_too_large" => StatusCodes.Status413PayloadTooLarge,
        "tool_arguments_invalid" => StatusCodes.Status422UnprocessableEntity,
        "connection_timeout" or "ping_timeout" or "tool_catalog_timeout" or "tool_call_timeout" =>
            StatusCodes.Status504GatewayTimeout,
        "mcp_transport_failed" or "mcp_transport_closed" or "mcp_protocol_error" or "tool_protocol_error" or
            "tool_catalog_unavailable" or "result_too_large" => StatusCodes.Status502BadGateway,
        "registry_unavailable" or "registry_corrupt" or "unsupported_registry_version" =>
            StatusCodes.Status503ServiceUnavailable,
        "tool_call_cancelled" or "operation_cancelled" => StatusCodes.Status499ClientClosedRequest,
        _ => StatusCodes.Status500InternalServerError
    };

    private async Task WriteErrorAsync(
        HttpContext context,
        string requestId,
        int status,
        string code,
        string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new ApiErrorResponse(
                new ApiError(code, message, null),
                new ApiMeta(requestId, timeProvider.GetUtcNow())),
            AppJsonSerializerContext.Default.ApiErrorResponse,
            context.RequestAborted);
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);

    private static (string Code, string Message) MapFrameworkStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => ("server_not_found", "The requested API resource was not found."),
        StatusCodes.Status405MethodNotAllowed => ("method_not_allowed", "The HTTP method is not allowed for this API resource."),
        StatusCodes.Status415UnsupportedMediaType => ("unsupported_media_type", "The request content type is not supported."),
        _ => ("invalid_request", "The API request could not be completed.")
    };
}

internal static partial class ApiLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "API request {RequestId} failed with {ErrorCode}")]
    public static partial void RequestFailed(ILogger logger, string requestId, string errorCode);
}
