using System.Text.Json;
using McpWorkbench.Domain;
using McpWorkbench.Mcp;
using McpWorkbench.Options;
using McpWorkbench.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpWorkbench.IntegrationTests.Mcp;

public sealed class StdioMcpClientSessionTests
{
    [Fact]
    public async Task Session_WhenUsingDeterministicServer_ConnectsDiscoversInvokesAndCancels()
    {
        var serverAssembly = GetTestServerAssemblyPath();
        Assert.True(File.Exists(serverAssembly), $"Test server was not built at '{serverAssembly}'.");
        var definition = Definition(serverAssembly);
        var resolved = new SecretReferenceResolver(new EmptyEnvironmentValueProvider()).Resolve(definition);
        var factory = new McpClientSessionFactory(
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new WorkbenchOptions { ConnectTimeoutSeconds = 10, MaximumResultBytes = 1_000_000 }));

        var session = await factory.CreateAsync(definition, resolved, TestContext.Current.CancellationToken);
        try
        {
            var info = await session.GetSessionInfoAsync(TestContext.Current.CancellationToken);
            await session.PingAsync(TestContext.Current.CancellationToken);
            var tools = await session.ListToolsAsync(TestContext.Current.CancellationToken);
            var echo = await session.InvokeToolAsync("echo", Json("""{"text":"hello"}"""), TestContext.Current.CancellationToken);
            var add = await session.InvokeToolAsync("add", Json("""{"a":12,"b":30}"""), TestContext.Current.CancellationToken);
            var failed = await session.InvokeToolAsync("fail", Json("{}"), TestContext.Current.CancellationToken);
            var structured = await session.InvokeToolAsync("structured", Json("{}"), TestContext.Current.CancellationToken);

            Assert.Equal("McpWorkbench.TestServer", info.Server.Name);
            Assert.True(info.Capabilities.Tools);
            Assert.Equal(6, tools.Count);
            Assert.Equal("hello", echo.Content[0].Text);
            Assert.Equal("42", add.Content[0].Text);
            Assert.True(failed.IsError);
            Assert.Equal(42, structured.StructuredContent?.GetProperty("result").GetProperty("value").GetInt32());

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var cancellationError = await Assert.ThrowsAsync<McpSessionException>(async () =>
                await session.InvokeToolAsync("delay", Json("""{"milliseconds":5000}"""), cancellation.Token));
            Assert.Equal("operation_cancelled", cancellationError.Code);
        }
        finally
        {
            await session.DisposeAsync();
            await session.DisposeAsync();
        }
    }

    private static McpServerDefinition Definition(string serverAssembly) => new(
        Guid.NewGuid(),
        "Deterministic test server",
        null,
        true,
        McpTransportKind.Stdio,
        new StdioTransportSettings(
            "dotnet",
            [serverAssembly],
            Path.GetDirectoryName(serverAssembly),
            new Dictionary<string, string>(),
            5),
        null,
        30,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    private static string GetTestServerAssemblyPath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "McpWorkbench.TestServer", "bin", "Release", "net10.0", "McpWorkbench.TestServer.dll"));

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class EmptyEnvironmentValueProvider : IEnvironmentValueProvider
    {
        public string? GetValue(string name) => null;
    }
}
