using System.Net;
using Microsoft.Extensions.Configuration;

namespace A2G.McpWorkbench.IntegrationTests.Security;

public sealed class SecurityEndpointTests
{
    [Fact]
    public async Task ApiKeyAndHeaders_AreEnforcedWithoutProtectingHealth()
    {
        using var factory = new TestWebApplicationFactory();
        using var application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:ApiKey"] = "phase-nine-secret" })));
        using var client = application.CreateClient();
        using var unauthorized = await client.GetAsync("/api/v1/servers", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("application/json", unauthorized.Content.Headers.ContentType?.MediaType);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/servers");
        request.Headers.Add("X-Mcp-Workbench-Key", "phase-nine-secret");
        using var authorized = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal("nosniff", authorized.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", authorized.Headers.GetValues("Content-Security-Policy").Single());

        using var health = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task OversizedBody_IsRejectedPredictably()
    {
        using var application = new TestWebApplicationFactory();
        using var client = application.CreateClient();
        using var content = new StringContent("{\"name\":\"" + new string('x', 1_100_000) + "\"}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/servers", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
