using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace A2G.McpWorkbench.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "mcp-workbench-host-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpWorkbench:RegistryPath"] = Path.Combine(_directory, "servers.json"),
                ["McpWorkbench:SecretVaultPath"] = Path.Combine(_directory, "secrets.vault"),
                ["McpWorkbench:SecretKeyRingPath"] = Path.Combine(_directory, "secret-keys")
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !Directory.Exists(_directory)) return;
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
