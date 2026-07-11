using System.Net;

namespace A2G.McpWorkbench.IntegrationTests.Api;

public sealed class StaticUiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StaticUiTests(TestWebApplicationFactory application) => _client = application.CreateClient();

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/app.css", "text/css")]
    [InlineData("/compact-form.css", "text/css")]
    [InlineData("/app.js", "text/javascript")]
    [InlineData("/assets/mark.svg", "image/svg+xml")]
    public async Task StaticAsset_IsBundledAndServed(string path, string mediaType)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Index_UsesModuleScriptWithoutInlineJavaScriptOrExternalOrigins()
    {
        var html = await _client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("type=\"module\" src=\"/app.js\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UiModules_DoNotUseUnsafeHtmlExecutionApis()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "McpWorkbench", "wwwroot"));
        var source = string.Join('\n', Directory.EnumerateFiles(root, "*.js", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.DoesNotContain("innerHTML", source, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Function", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiKeyFlow_UsesSessionStorageAndRequiredHeader()
    {
        var html = await _client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var client = await _client.GetStringAsync("/api-client.js", TestContext.Current.CancellationToken);

        Assert.Contains("id=\"api-key-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("sessionStorage", client, StringComparison.Ordinal);
        Assert.Contains("X-Mcp-Workbench-Key", client, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupScript_RendersExplicitFailureState()
    {
        var application = await _client.GetStringAsync("/app.js", TestContext.Current.CancellationToken);
        var client = await _client.GetStringAsync("/api-client.js", TestContext.Current.CancellationToken);

        Assert.Contains("MCP Workbench is unavailable", application, StringComparison.Ordinal);
        Assert.Contains("start().catch", application, StringComparison.Ordinal);
        Assert.Contains(".catch(() => false)", client, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerDetailsModule_ClosesNestedToolSectionExpression()
    {
        var module = await _client.GetStringAsync("/pages/server-details-page.js", TestContext.Current.CancellationToken);

        Assert.Contains("onclick: actions.refresh })));", module, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SchemaForm_HandlesNullableArrayTypes()
    {
        var module = await _client.GetStringAsync("/components/schema-form.js", TestContext.Current.CancellationToken);

        Assert.Contains("Array.isArray(spec?.type)", module, StringComparison.Ordinal);
        Assert.Contains("type !== \"null\"", module, StringComparison.Ordinal);
        Assert.Contains("type === \"array\"", module, StringComparison.Ordinal);
    }
}
