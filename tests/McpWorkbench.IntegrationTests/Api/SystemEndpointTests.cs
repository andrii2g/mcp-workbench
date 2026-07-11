using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace A2G.McpWorkbench.IntegrationTests.Api;

public sealed class SystemEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _application;
    private readonly HttpClient _client;

    public SystemEndpointTests(WebApplicationFactory<Program> application)
    {
        _application = application;
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

    [Fact]
    public async Task Startup_WhenRegistryPathIsOverridden_CreatesRegistryAtConfiguredPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mcp-workbench-tests", Guid.NewGuid().ToString("N"));
        var registryPath = Path.Combine(directory, "servers.json");
        Directory.CreateDirectory(directory);

        try
        {
            using var application = _application.WithWebHostBuilder(webHost =>
                webHost.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpWorkbench:RegistryPath"] = registryPath,
                        ["McpWorkbench:SecretVaultPath"] = Path.Combine(directory, "secrets.vault"),
                        ["McpWorkbench:SecretKeyRingPath"] = Path.Combine(directory, "secret-keys")
                    })));
            using var client = application.CreateClient();

            using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(File.Exists(registryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record HealthBody(string Status);
}
