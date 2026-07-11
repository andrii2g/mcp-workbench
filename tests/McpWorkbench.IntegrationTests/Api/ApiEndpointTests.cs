using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using McpWorkbench.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpWorkbench.IntegrationTests.Api;

public sealed class ApiEndpointTests
{
    [Fact]
    public async Task Api_WhenRunningCompleteWorkflow_ReturnsStableContractsForEveryRoute()
    {
        using var application = new ApiApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Request-Id", "phase-seven-request");

        using var invalid = await client.PostAsJsonAsync(
            "/api/v1/servers",
            new { name = "", enabled = true, transport = "stdio", operationTimeoutSeconds = 30 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("phase-seven-request", invalid.Headers.GetValues("X-Request-Id").Single());
        Assert.Equal("server_definition_invalid", (await Json(invalid)).RootElement.GetProperty("error").GetProperty("code").GetString());

        const string createJson = """
                {
                  "name":"Local API test",
                  "description":"created",
                  "enabled":true,
                  "transport":"stdio",
                  "stdio":{
                    "command":"dotnet",
                    "arguments":["server.dll"],
                    "workingDirectory":null,
                    "environment":{"TOKEN":"literal-secret"},
                    "shutdownTimeoutSeconds":5
                  },
                  "http":null,
                  "operationTimeoutSeconds":30
                }
                """;
        using var created = await client.PostAsync(
            "/api/v1/servers",
            Content(createJson),
            TestContext.Current.CancellationToken);
        var createdJson = await Json(created);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var serverId = createdJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal("[REDACTED]", createdJson.RootElement.GetProperty("data").GetProperty("stdio")
            .GetProperty("environment").GetProperty("TOKEN").GetString());
        Assert.NotNull(created.Headers.Location);

        using (var duplicate = await client.PostAsync(
            "/api/v1/servers",
            Content(createJson),
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal("server_name_conflict", (await Json(duplicate)).RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        await AssertOk(client, $"/api/v1/servers?includeRuntime=true&search=local", HttpMethod.Get);
        using (var withoutRuntime = await client.GetAsync(
            "/api/v1/servers?includeRuntime=false",
            TestContext.Current.CancellationToken))
        {
            var withoutRuntimeJson = await Json(withoutRuntime);
            Assert.Equal(
                JsonValueKind.Null,
                withoutRuntimeJson.RootElement.GetProperty("data")[0].GetProperty("runtime").ValueKind);
        }

        using (var invalidQuery = await client.GetAsync(
            "/api/v1/servers?includeRuntime=invalid",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidQuery.StatusCode);
            Assert.True((await Json(invalidQuery)).RootElement.TryGetProperty("error", out _));
        }

        await AssertOk(client, $"/api/v1/servers/{serverId}", HttpMethod.Get);

        using var updated = await client.PutAsync(
            $"/api/v1/servers/{serverId}",
            Content("""
                {
                  "name":"Updated API test",
                  "description":null,
                  "enabled":true,
                  "transport":"stdio",
                  "stdio":{"command":"dotnet","arguments":["server.dll"],"environment":{},"shutdownTimeoutSeconds":5},
                  "http":null,
                  "operationTimeoutSeconds":20
                }
                """),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        await AssertOk(client, $"/api/v1/servers/{serverId}/runtime", HttpMethod.Get);
        using (var connected = await client.PostAsync(
            $"/api/v1/servers/{serverId}/connect",
            Content("{\"forceReconnect\":false}"),
            TestContext.Current.CancellationToken))
        {
            var connectedJson = await Json(connected);
            Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
            Assert.True(connectedJson.RootElement.GetProperty("data").GetProperty("capabilities").GetProperty("tools").GetBoolean());
            Assert.True(connectedJson.RootElement.GetProperty("data").TryGetProperty("connectDurationMilliseconds", out _));
        }

        await AssertOk(client, $"/api/v1/servers/{serverId}/connect", HttpMethod.Post);
        await AssertOk(client, $"/api/v1/servers/{serverId}/ping", HttpMethod.Post);
        var fakeManager = Assert.IsType<FakeConnectionManager>(application.Services.GetRequiredService<IMcpConnectionManager>());
        fakeManager.PingException = new McpSessionException("tool_protocol_error", "Safe protocol failure.");
        using (var protocolFailure = await client.PostAsync(
            $"/api/v1/servers/{serverId}/ping",
            null,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadGateway, protocolFailure.StatusCode);
        }

        fakeManager.PingException = new McpSessionException("ping_timeout", "Ping timed out.");
        using (var timeoutFailure = await client.PostAsync(
            $"/api/v1/servers/{serverId}/ping",
            null,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.GatewayTimeout, timeoutFailure.StatusCode);
        }

        fakeManager.PingException = null;
        fakeManager.BlockPings = true;
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await client.PostAsync($"/api/v1/servers/{serverId}/ping", null, cancellation.Token));
        }

        await fakeManager.PingCancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
        fakeManager.BlockPings = false;
        await AssertOk(client, $"/api/v1/servers/{serverId}/tools?refresh=true", HttpMethod.Get);
        Assert.True(fakeManager.LastRefresh);
        using (var invalidRefresh = await client.GetAsync(
            $"/api/v1/servers/{serverId}/tools?refresh=invalid",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidRefresh.StatusCode);
        }

        fakeManager.ToolsException = new McpSessionException("tool_catalog_unavailable", "Catalog rejected.");
        using (var catalogRejected = await client.GetAsync(
            $"/api/v1/servers/{serverId}/tools",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadGateway, catalogRejected.StatusCode);
        }

        fakeManager.ToolsException = new McpSessionException("tool_catalog_timeout", "Catalog timed out.");
        using (var catalogTimeout = await client.GetAsync(
            $"/api/v1/servers/{serverId}/tools",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.GatewayTimeout, catalogTimeout.StatusCode);
        }

        fakeManager.ToolsException = null;
        await AssertOk(client, $"/api/v1/servers/{serverId}/tools/refresh", HttpMethod.Post);
        await AssertOk(client, $"/api/v1/servers/{serverId}/tools/echo", HttpMethod.Get);

        using var invoked = await client.PostAsync(
            $"/api/v1/servers/{serverId}/tools/echo/invoke",
            Content("{\"arguments\":{\"text\":\"hello\"},\"timeoutSeconds\":5}"),
            TestContext.Current.CancellationToken);
        var invokedJson = await Json(invoked);
        Assert.Equal(HttpStatusCode.OK, invoked.StatusCode);
        Assert.False(invokedJson.RootElement.GetProperty("data").GetProperty("isError").GetBoolean());
        Assert.Equal("hello", invokedJson.RootElement.GetProperty("data").GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("text", invokedJson.RootElement.GetProperty("data").GetProperty("content")[0].GetProperty("type").GetString());
        Assert.False(invokedJson.RootElement.GetProperty("data").GetProperty("content")[0].TryGetProperty("kind", out _));

        using (var invalidArguments = await client.PostAsync(
            $"/api/v1/servers/{serverId}/tools/echo/invoke",
            Content("{\"arguments\":[]}"),
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidArguments.StatusCode);
        }

        using var toolError = await client.PostAsync(
            $"/api/v1/servers/{serverId}/tools/fail/invoke",
            Content("{\"arguments\":{}}"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, toolError.StatusCode);
        Assert.True((await Json(toolError)).RootElement.GetProperty("data").GetProperty("isError").GetBoolean());

        using (var oversizedResult = await client.PostAsync(
            $"/api/v1/servers/{serverId}/tools/large/invoke",
            Content("{\"arguments\":{}}"),
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadGateway, oversizedResult.StatusCode);
            Assert.Equal("result_too_large", (await Json(oversizedResult)).RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        using var disconnected = await client.PostAsync(
            $"/api/v1/servers/{serverId}/disconnect",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);

        using var unavailableTools = await client.GetAsync(
            $"/api/v1/servers/{serverId}/tools",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, unavailableTools.StatusCode);
        Assert.Equal("server_not_connected", (await Json(unavailableTools)).RootElement.GetProperty("error").GetProperty("code").GetString());

        using var deleted = await client.DeleteAsync($"/api/v1/servers/{serverId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await client.GetAsync($"/api/v1/servers/{serverId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using (var unknownRoute = await client.GetAsync("/api/v1/unknown", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
            Assert.True((await Json(unknownRoute)).RootElement.TryGetProperty("error", out _));
        }

        using (var wrongMethod = await client.PatchAsync("/api/v1/servers", null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
            Assert.Equal("method_not_allowed", (await Json(wrongMethod)).RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        using (var unsupportedContent = await client.PostAsync(
            "/api/v1/servers",
            new StringContent("{}", Encoding.UTF8, "text/plain"),
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedContent.StatusCode);
            Assert.Equal(
                "unsupported_media_type",
                (await Json(unsupportedContent)).RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }

    private static async Task AssertOk(HttpClient client, string path, HttpMethod method, string? json = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = Content(json);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 for {method} {path}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        using var body = await Json(response);
        Assert.True(body.RootElement.TryGetProperty("data", out _));
        Assert.Equal("phase-seven-request", body.RootElement.GetProperty("meta").GetProperty("requestId").GetString());
    }

    private static StringContent Content(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

    private sealed class ApiApplication : WebApplicationFactory<Program>
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "mcp-workbench-api", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_directory);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["McpWorkbench:RegistryPath"] = Path.Combine(_directory, "servers.json"),
                    ["McpWorkbench:LoadToolsOnConnect"] = "false",
                    ["McpWorkbench:MaximumResultBytes"] = "1024"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMcpConnectionManager>();
                services.AddSingleton<IMcpConnectionManager>(provider =>
                    new FakeConnectionManager(provider.GetRequiredService<IServerDefinitionStore>()));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing || !Directory.Exists(_directory))
            {
                return;
            }

            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FakeConnectionManager(IServerDefinitionStore store) : IMcpConnectionManager
    {
        private readonly HashSet<Guid> _connected = [];

        public Exception? PingException { get; set; }
        public bool BlockPings { get; set; }
        public TaskCompletionSource PingCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? ToolsException { get; set; }
        public bool LastRefresh { get; private set; }

        public async ValueTask<ServerRuntimeSnapshot> ConnectAsync(Guid serverId, bool forceReconnect, CancellationToken cancellationToken)
        {
            await Required(serverId, cancellationToken);
            _connected.Add(serverId);
            return Runtime(serverId, true);
        }

        public ValueTask DisconnectAsync(Guid serverId, CancellationToken cancellationToken)
        {
            _connected.Remove(serverId);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ServerRuntimeSnapshot> PingAsync(Guid serverId, CancellationToken cancellationToken)
        {
            if (!_connected.Contains(serverId))
            {
                throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            }

            if (PingException is not null)
            {
                throw PingException;
            }

            if (BlockPings)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    PingCancelled.TrySetResult();
                    throw;
                }
            }

            return Runtime(serverId, true);
        }

        public async ValueTask<IReadOnlyList<ToolCatalogEntry>> GetToolsAsync(Guid serverId, bool refresh, CancellationToken cancellationToken)
        {
            await RequiredConnected(serverId, cancellationToken);
            LastRefresh = refresh;
            if (ToolsException is not null)
            {
                throw ToolsException;
            }

            return [Tool("echo"), Tool("fail"), Tool("large")];
        }

        public async ValueTask<ToolInvocationOutcome> InvokeToolAsync(
            Guid serverId,
            string toolName,
            JsonElement arguments,
            int? timeoutSeconds,
            CancellationToken cancellationToken)
        {
            await RequiredConnected(serverId, cancellationToken);
            if (arguments.ValueKind != JsonValueKind.Object)
            {
                throw new McpSessionException("tool_arguments_invalid", "Tool arguments must be an object.");
            }

            var isError = toolName == "fail";
            var text = toolName == "large"
                ? new string('x', 700)
                : isError ? "failed" : arguments.TryGetProperty("text", out var value) ? value.GetString() ?? "" : "";
            using var document = JsonDocument.Parse($"{{\"content\":[{{\"type\":\"text\",\"text\":{JsonSerializer.Serialize(text)}}}],\"isError\":{isError.ToString().ToLowerInvariant()}}}");
            return new ToolInvocationOutcome(
                isError,
                [new McpContentBlock(McpContentKind.Text, text, null, null, null, null, null, null)],
                null,
                document.RootElement.Clone(),
                false);
        }

        public ValueTask<IReadOnlyList<ToolExecutionRecord>> GetExecutionHistoryAsync(Guid serverId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ToolExecutionRecord>>([]);

        public async ValueTask<ServerRuntimeSnapshot> GetRuntimeAsync(Guid serverId, CancellationToken cancellationToken)
        {
            await Required(serverId, cancellationToken);
            return Runtime(serverId, _connected.Contains(serverId));
        }

        public ValueTask<McpServerDefinition> ReplaceDefinitionAsync(McpServerDefinition definition, CancellationToken cancellationToken) =>
            store.ReplaceAsync(definition, cancellationToken);

        public async ValueTask<bool> DeleteDefinitionAsync(Guid serverId, CancellationToken cancellationToken)
        {
            _connected.Remove(serverId);
            return await store.DeleteAsync(serverId, cancellationToken);
        }

        public ValueTask DisconnectAllAsync(CancellationToken cancellationToken)
        {
            _connected.Clear();
            return ValueTask.CompletedTask;
        }

        private async ValueTask RequiredConnected(Guid serverId, CancellationToken cancellationToken)
        {
            await Required(serverId, cancellationToken);
            if (!_connected.Contains(serverId))
            {
                throw new McpSessionException("server_not_connected", "The MCP server is not connected.");
            }
        }

        private async ValueTask Required(Guid serverId, CancellationToken cancellationToken)
        {
            if (await store.GetAsync(serverId, cancellationToken) is null)
            {
                throw new McpSessionException("server_not_found", "The MCP server was not found.");
            }
        }

        private static ServerRuntimeSnapshot Runtime(Guid id, bool connected) => new(
            id,
            connected ? McpConnectionState.Connected : McpConnectionState.Disconnected,
            connected ? DateTimeOffset.UnixEpoch : null,
            connected ? DateTimeOffset.UnixEpoch : null,
            null,
            connected ? "2025-11-25" : null,
            connected ? "Fake" : null,
            connected ? "1.0" : null,
            connected,
            false,
            connected ? 2 : null);

        private static ToolCatalogEntry Tool(string name) => new(
            name,
            name,
            $"{name} tool",
            JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
            null,
            new McpToolAnnotations(null, true, false, true, false));
    }
}
