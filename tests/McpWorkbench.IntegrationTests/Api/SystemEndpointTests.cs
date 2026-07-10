using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McpWorkbench.IntegrationTests.Api;

public sealed class SystemEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemEndpointTests(WebApplicationFactory<Program> application)
    {
        _client = application.CreateClient();
    }

    [Theory]
    [InlineData("/health/live", "live")]
    [InlineData("/health/ready", "ready")]
    public async Task HealthEndpoint_ReturnsExpectedStatus(string path, string expectedStatus)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedStatus, body?.Status);
    }

    private sealed record HealthBody(string Status);
}
