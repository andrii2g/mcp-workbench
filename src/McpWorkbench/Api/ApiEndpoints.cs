using System.Diagnostics;
using System.Text.Json;
using McpWorkbench.Contracts;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using McpWorkbench.Options;
using McpWorkbench.Persistence;
using McpWorkbench.Security;
using McpWorkbench.Serialization;
using McpWorkbench.Validation;
using Microsoft.Extensions.Options;

namespace McpWorkbench.Api;

internal static class ApiEndpoints
{
    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    public static IEndpointRouteBuilder MapWorkbenchApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/servers");
        group.MapGet("", ListServersAsync);
        group.MapPost("", CreateServerAsync);
        group.MapGet("/{serverId:guid}", GetServerAsync);
        group.MapPut("/{serverId:guid}", UpdateServerAsync);
        group.MapDelete("/{serverId:guid}", DeleteServerAsync);
        group.MapPost("/{serverId:guid}/connect", ConnectAsync);
        group.MapPost("/{serverId:guid}/disconnect", DisconnectAsync);
        group.MapPost("/{serverId:guid}/ping", PingAsync);
        group.MapGet("/{serverId:guid}/runtime", GetRuntimeAsync);
        group.MapGet("/{serverId:guid}/tools", GetToolsAsync);
        group.MapPost("/{serverId:guid}/tools/refresh", RefreshToolsAsync);
        group.MapGet("/{serverId:guid}/tools/{toolName}", GetToolAsync);
        group.MapPost("/{serverId:guid}/tools/{toolName}/invoke", InvokeToolAsync);
        return endpoints;
    }

    private static async Task<IResult> ListServersAsync(
        HttpRequest request,
        string? search,
        IServerDefinitionStore store,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var includeRuntime = ParseBooleanQuery(request, "includeRuntime", defaultValue: true);
        var snapshot = await store.GetSnapshotAsync(cancellationToken);
        var definitions = snapshot.Servers
            .Where(server => string.IsNullOrWhiteSpace(search) || server.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new List<ServerDefinitionResponse>(definitions.Length);
        foreach (var definition in definitions)
        {
            var runtime = includeRuntime ? await manager.GetRuntimeAsync(definition.Id, cancellationToken) : null;
            result.Add(MapServer(definition, runtime));
        }

        return Results.Ok(Success(context, timeProvider, result.ToArray()));
    }

    private static async Task<IResult> CreateServerAsync(
        CreateServerRequest request,
        IServerDefinitionStore store,
        IOptions<WorkbenchOptions> options,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validation = ServerDefinitionValidator.Validate(request, options.Value.MaximumOperationTimeoutSeconds);
        if (!validation.IsValid)
        {
            return ValidationFailure(context, timeProvider, validation.Errors);
        }

        var now = timeProvider.GetUtcNow();
        var definition = MapDefinition(Guid.NewGuid(), validation.Value!, now, now);
        var created = await store.CreateAsync(definition, cancellationToken);
        var response = Success(context, timeProvider, MapServer(created, null));
        return Results.Created($"/api/v1/servers/{created.Id}", response);
    }

    private static async Task<IResult> GetServerAsync(
        Guid serverId,
        IServerDefinitionStore store,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var definition = await RequiredDefinitionAsync(store, serverId, cancellationToken);
        var runtime = await manager.GetRuntimeAsync(serverId, cancellationToken);
        return Results.Ok(Success(context, timeProvider, MapServer(definition, runtime)));
    }

    private static async Task<IResult> UpdateServerAsync(
        Guid serverId,
        UpdateServerRequest request,
        IServerDefinitionStore store,
        IMcpConnectionManager manager,
        IOptions<WorkbenchOptions> options,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validation = ServerDefinitionValidator.Validate(request, options.Value.MaximumOperationTimeoutSeconds);
        if (!validation.IsValid)
        {
            return ValidationFailure(context, timeProvider, validation.Errors);
        }

        var current = await RequiredDefinitionAsync(store, serverId, cancellationToken);
        var definition = MapDefinition(serverId, validation.Value!, current.CreatedAtUtc, timeProvider.GetUtcNow());
        var updated = await manager.ReplaceDefinitionAsync(definition, cancellationToken);
        return Results.Ok(Success(context, timeProvider, MapServer(updated, await manager.GetRuntimeAsync(serverId, cancellationToken))));
    }

    private static async Task<IResult> DeleteServerAsync(
        Guid serverId,
        IMcpConnectionManager manager,
        CancellationToken cancellationToken) =>
        await manager.DeleteDefinitionAsync(serverId, cancellationToken)
            ? Results.NoContent()
            : throw new McpSessionException("server_not_found", "The MCP server was not found.");

    private static async Task<IResult> ConnectAsync(
        Guid serverId,
        ConnectRequest? request,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var runtime = await manager.ConnectAsync(serverId, request?.ForceReconnect ?? false, cancellationToken);
        var response = new ConnectResponse(
            runtime.Status,
            runtime.ConnectedAtUtc,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            runtime.ProtocolVersion,
            new RemoteServerResponse(runtime.ServerName, runtime.ServerVersion),
            new McpCapabilitiesResponse(runtime.SupportsTools == true, runtime.ToolsListChanged == true));
        return Results.Ok(Success(context, timeProvider, response));
    }

    private static async Task<IResult> DisconnectAsync(
        Guid serverId,
        IMcpConnectionManager manager,
        CancellationToken cancellationToken)
    {
        await manager.DisconnectAsync(serverId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PingAsync(
        Guid serverId,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        await manager.PingAsync(serverId, cancellationToken);
        var timestamp = timeProvider.GetUtcNow();
        return Results.Ok(Success(
            context,
            timeProvider,
            new PingResponse(true, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, timestamp)));
    }

    private static async Task<IResult> GetRuntimeAsync(
        Guid serverId,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        Results.Ok(Success(context, timeProvider, await manager.GetRuntimeAsync(serverId, cancellationToken)));

    private static Task<IResult> GetToolsAsync(
        Guid serverId,
        HttpRequest request,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ToolListResultAsync(
            serverId,
            ParseBooleanQuery(request, "refresh", defaultValue: false),
            manager,
            context,
            timeProvider,
            cancellationToken);

    private static Task<IResult> RefreshToolsAsync(
        Guid serverId,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ToolListResultAsync(serverId, true, manager, context, timeProvider, cancellationToken);

    private static async Task<IResult> ToolListResultAsync(
        Guid serverId,
        bool refresh,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tools = await manager.GetToolsAsync(serverId, refresh, cancellationToken);
        return Results.Ok(Success(context, timeProvider, tools.ToArray()));
    }

    private static async Task<IResult> GetToolAsync(
        Guid serverId,
        string toolName,
        IMcpConnectionManager manager,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tools = await manager.GetToolsAsync(serverId, false, cancellationToken);
        var tool = tools.FirstOrDefault(item => string.Equals(item.Name, toolName, StringComparison.Ordinal)) ??
            throw new McpSessionException("tool_not_found", "The requested tool is not in the loaded catalog.");
        return Results.Ok(Success(context, timeProvider, tool));
    }

    private static async Task<IResult> InvokeToolAsync(
        Guid serverId,
        string toolName,
        InvokeToolRequest? request,
        IMcpConnectionManager manager,
        IOptions<WorkbenchOptions> options,
        HttpContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var arguments = request?.Arguments ?? EmptyArguments;
        var started = timeProvider.GetUtcNow();
        var outcome = await manager.InvokeToolAsync(serverId, toolName, arguments, request?.TimeoutSeconds, cancellationToken);
        var completed = timeProvider.GetUtcNow();
        var response = new ToolInvocationResponse(
            serverId,
            toolName,
            started,
            completed,
            Math.Max(0, (long)(completed - started).TotalMilliseconds),
            outcome.IsError,
            outcome.Content.Select(block => new McpContentBlockResponse(
                block.Kind switch
                {
                    McpContentKind.Text => "text",
                    McpContentKind.Image => "image",
                    McpContentKind.EmbeddedResource => "embeddedResource",
                    McpContentKind.ResourceLink => "resourceLink",
                    _ => "unknown"
                },
                block.Text,
                block.DataBase64,
                block.MimeType,
                block.Uri,
                block.Name,
                block.Size,
                block.Raw)).ToArray(),
            outcome.StructuredContent,
            outcome.RawResult,
            outcome.WasTruncated);
        EnsureInvocationResponseSize(response, options.Value.MaximumResultBytes);
        return Results.Ok(Success(context, timeProvider, response));
    }

    private static ApiResponse<T> Success<T>(HttpContext context, TimeProvider timeProvider, T data) =>
        new(data, Meta(context, timeProvider));

    private static ApiMeta Meta(HttpContext context, TimeProvider timeProvider) => new(
        (string)context.Items[ApiMiddleware.RequestIdItemKey]!,
        timeProvider.GetUtcNow());

    private static bool ParseBooleanQuery(HttpRequest request, string name, bool defaultValue)
    {
        if (!request.Query.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        if (value.Count != 1 || !bool.TryParse(value[0], out var parsed))
        {
            throw new McpSessionException("server_definition_invalid", $"Query parameter '{name}' must be a boolean.");
        }

        return parsed;
    }

    private static void EnsureInvocationResponseSize(ToolInvocationResponse response, int maximumBytes)
    {
        var buffer = new BoundedByteBufferWriter(maximumBytes);
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, response, AppJsonSerializerContext.Default.ToolInvocationResponse);
    }

    private static IResult ValidationFailure(
        HttpContext context,
        TimeProvider timeProvider,
        IReadOnlyList<ValidationError> errors) => Results.Json(
        new ApiErrorResponse(
            new ApiError("server_definition_invalid", "The server definition is invalid.", errors),
            Meta(context, timeProvider)),
        AppJsonSerializerContext.Default.ApiErrorResponse,
        statusCode: StatusCodes.Status400BadRequest);

    private static async ValueTask<McpServerDefinition> RequiredDefinitionAsync(
        IServerDefinitionStore store,
        Guid serverId,
        CancellationToken cancellationToken) =>
        await store.GetAsync(serverId, cancellationToken) ??
        throw new RegistryException("server_not_found", "The MCP server was not found.");

    private static McpServerDefinition MapDefinition(
        Guid id,
        CreateServerRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => new(
        id,
        request.Name!,
        request.Description,
        request.Enabled,
        request.Transport,
        request.Stdio is null ? null : new StdioTransportSettings(
            request.Stdio.Command!,
            request.Stdio.Arguments?.ToArray() ?? [],
            request.Stdio.WorkingDirectory,
            request.Stdio.Environment?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) ?? [],
            request.Stdio.ShutdownTimeoutSeconds),
        request.Http is null ? null : new HttpTransportSettings(
            request.Http.Endpoint!,
            request.Http.Mode,
            request.Http.Headers?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) ?? []),
        request.OperationTimeoutSeconds,
        createdAt,
        updatedAt);

    private static McpServerDefinition MapDefinition(
        Guid id,
        UpdateServerRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => MapDefinition(
        id,
        new CreateServerRequest(
            request.Name,
            request.Description,
            request.Enabled,
            request.Transport,
            request.Stdio,
            request.Http,
            request.OperationTimeoutSeconds),
        createdAt,
        updatedAt);

    private static ServerDefinitionResponse MapServer(McpServerDefinition definition, ServerRuntimeSnapshot? runtime) => new(
        definition.Id,
        definition.Name,
        definition.Description,
        definition.Transport,
        definition.Enabled,
        definition.Stdio is null ? null : new StdioTransportRequest(
            definition.Stdio.Command,
            definition.Stdio.Arguments,
            definition.Stdio.WorkingDirectory,
            SensitiveDataRedactor.RedactDictionary(definition.Stdio.Environment),
            definition.Stdio.ShutdownTimeoutSeconds),
        definition.Http is null ? null : new HttpTransportRequest(
            definition.Http.Endpoint,
            definition.Http.Mode,
            SensitiveDataRedactor.RedactDictionary(definition.Http.Headers)),
        definition.OperationTimeoutSeconds,
        definition.CreatedAtUtc,
        definition.UpdatedAtUtc,
        runtime);
}
