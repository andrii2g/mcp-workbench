using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McpWorkbench.IntegrationTests.Api;

public sealed class StaticUiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StaticUiTests(WebApplicationFactory<Program> application) => _client = application.CreateClient();

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/app.css", "text/css")]
    [InlineData("/app.js", "text/javascript")]
    [InlineData("/assets/mark.svg", "image/svg+xml")]
    public async Task StaticAsset_IsBundledAndServed(string path, string mediaType)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
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
}
