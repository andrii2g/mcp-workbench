using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using McpWorkbench.Api;
using McpWorkbench.Contracts;
using McpWorkbench.Mcp;
using McpWorkbench.Options;
using McpWorkbench.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace McpWorkbench.Security;

internal sealed class SecurityMiddleware(RequestDelegate next, IOptions<SecurityOptions> options, TimeProvider timeProvider)
{
    private const long MaximumBodyBytes = 1_048_576;

    public async Task InvokeAsync(HttpContext context)
    {
        SetHeaders(context.Response.Headers);
        var bodyFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyFeature is { IsReadOnly: false }) bodyFeature.MaxRequestBodySize = MaximumBodyBytes;
        if (context.Request.ContentLength > MaximumBodyBytes)
        {
            throw new McpSessionException("request_too_large", "The request body exceeds the configured size limit.");
        }

        var configured = options.Value.ApiKey;
        var protectedPath = context.Request.Path.StartsWithSegments("/api/v1") ||
            (options.Value.ProtectStaticUi && !context.Request.Path.StartsWithSegments("/health"));
        if (protectedPath && !string.IsNullOrEmpty(configured) && !Matches(configured, context.Request.Headers["X-Mcp-Workbench-Key"].FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            var requestId = context.Items.TryGetValue(ApiMiddleware.RequestIdItemKey, out var value) ? value?.ToString() ?? context.TraceIdentifier : context.TraceIdentifier;
            await JsonSerializer.SerializeAsync(context.Response.Body, new ApiErrorResponse(new ApiError("unauthorized", "The API key is missing or invalid.", null), new ApiMeta(requestId, timeProvider.GetUtcNow())), AppJsonSerializerContext.Default.ApiErrorResponse, context.RequestAborted);
            return;
        }

        await next(context);
    }

    internal static bool Matches(string expected, string? supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private static void SetHeaders(IHeaderDictionary headers)
    {
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Frame-Options"] = "DENY";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
    }
}
