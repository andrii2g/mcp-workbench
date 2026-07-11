using System.Diagnostics;
using System.Text.Json;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Mcp;
using A2G.McpWorkbench.Options;
using A2G.McpWorkbench.Persistence;
using A2G.McpWorkbench.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2G.McpWorkbench.IntegrationTests.Mcp;

public sealed class StdioMcpClientSessionTests
{
    [Fact]
    public async Task Session_DisposeTerminatesOwnedStdioProcess()
    {
        var serverAssembly = GetTestServerAssemblyPath();
        var pidFile = Path.Combine(Path.GetTempPath(), $"mcp-workbench-{Guid.NewGuid():N}.pid");
        var definition = Definition(serverAssembly) with
        {
            Stdio = Definition(serverAssembly).Stdio! with
            {
                Environment = new Dictionary<string, string> { ["MCP_WORKBENCH_TEST_PID_FILE"] = pidFile }
            }
        };
        var resolved = new SecretReferenceResolver(new EmptyEnvironmentValueProvider()).Resolve(definition);
        var factory = new McpClientSessionFactory(
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new WorkbenchOptions { ConnectTimeoutSeconds = 10, MaximumResultBytes = 1_000_000 }));

        var session = await factory.CreateAsync(definition, resolved, TestContext.Current.CancellationToken);
        int processId;
        try
        {
            processId = await WaitForProcessIdAsync(pidFile, TestContext.Current.CancellationToken);
        }
        finally
        {
            await session.DisposeAsync();
        }

        await WaitForProcessExitAsync(processId, TestContext.Current.CancellationToken);
        File.Delete(pidFile);
    }

    [Fact]
    public async Task Session_WhenUsingDeterministicServer_ConnectsDiscoversInvokesAndCancels()
    {
        var serverAssembly = GetTestServerAssemblyPath();
        Assert.True(File.Exists(serverAssembly), $"Test server was not built at '{serverAssembly}'.");
        var definition = Definition(serverAssembly);
        var resolved = new SecretReferenceResolver(new EmptyEnvironmentValueProvider()).Resolve(definition);
        var capturedLogs = new CapturingLoggerFactory();
        var factory = new McpClientSessionFactory(
            capturedLogs,
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

            const string payloadSentinel = "phase-six-payload-sentinel";
            var sentinel = await session.InvokeToolAsync(
                "echo",
                Json($$"""{"text":"{{payloadSentinel}}"}"""),
                TestContext.Current.CancellationToken);
            Assert.Equal(payloadSentinel, sentinel.Content[0].Text);
            Assert.DoesNotContain(capturedLogs.Messages, message => message.Contains(payloadSentinel, StringComparison.Ordinal));

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var cancellationError = await Assert.ThrowsAsync<McpSessionException>(async () =>
                await session.InvokeToolAsync("delay", Json("""{"milliseconds":5000}"""), cancellation.Token));
            Assert.Equal("operation_cancelled", cancellationError.Code);

            var missing = await session.InvokeToolAsync("missing-tool", Json("{}"), TestContext.Current.CancellationToken);
            Assert.True(missing.IsError);
        }
        finally
        {
            await session.DisposeAsync();
            await session.DisposeAsync();
        }


        var limitedFactory = new McpClientSessionFactory(
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new WorkbenchOptions { ConnectTimeoutSeconds = 10, MaximumResultBytes = 1_024 }));
        var limitedSession = await limitedFactory.CreateAsync(definition, resolved, TestContext.Current.CancellationToken);
        try
        {
            var sizeError = await Assert.ThrowsAsync<McpSessionException>(async () =>
                await limitedSession.InvokeToolAsync("large-text", Json("{}"), TestContext.Current.CancellationToken));
            Assert.Equal("result_too_large", sizeError.Code);
        }
        finally
        {
            await limitedSession.DisposeAsync();
        }

        await VerifyProtocolErrorAsync(serverAssembly);
        await VerifyManagerTimeoutAsync(definition);
    }

    private static async Task VerifyProtocolErrorAsync(string serverAssembly)
    {
        var definition = Definition(serverAssembly) with
        {
            Stdio = new StdioTransportSettings(
                "dotnet",
                [serverAssembly, "--malformed-tools"],
                Path.GetDirectoryName(serverAssembly),
                new Dictionary<string, string>(),
                5)
        };
        var resolved = new SecretReferenceResolver(new EmptyEnvironmentValueProvider()).Resolve(definition);
        var factory = new McpClientSessionFactory(
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new WorkbenchOptions { ConnectTimeoutSeconds = 10, MaximumResultBytes = 1_000_000 }));
        var session = await factory.CreateAsync(definition, resolved, TestContext.Current.CancellationToken);
        try
        {
            var protocolError = await Assert.ThrowsAsync<McpSessionException>(async () =>
                await session.ListToolsAsync(TestContext.Current.CancellationToken));
            Assert.Equal("mcp_protocol_error", protocolError.Code);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    private static async Task VerifyManagerTimeoutAsync(McpServerDefinition definition)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mcp-workbench-phase-six", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var store = new JsonServerDefinitionStore(
                Path.Combine(directory, "servers.json"),
                new AtomicFileWriter(),
                TimeProvider.System);
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.CreateAsync(definition with { OperationTimeoutSeconds = 1 }, TestContext.Current.CancellationToken);
            var workbenchOptions = Microsoft.Extensions.Options.Options.Create(new WorkbenchOptions
            {
                ConnectTimeoutSeconds = 10,
                PingTimeoutSeconds = 5,
                DefaultOperationTimeoutSeconds = 5,
                MaximumOperationTimeoutSeconds = 30,
                MaximumArgumentsBytes = 262_144,
                MaximumResultBytes = 1_000_000,
                MaximumHistoryEntriesPerServer = 10,
                LoadToolsOnConnect = true
            });
            var manager = new McpConnectionManager(
                store,
                new SecretReferenceResolver(new EmptyEnvironmentValueProvider()),
                new McpClientSessionFactory(NullLoggerFactory.Instance, workbenchOptions),
                TimeProvider.System,
                workbenchOptions,
                NullLogger<McpConnectionManager>.Instance);
            await manager.ConnectAsync(definition.Id, false, TestContext.Current.CancellationToken);
            try
            {
                var timeout = await Assert.ThrowsAsync<McpSessionException>(async () =>
                    await manager.InvokeToolAsync(
                        definition.Id,
                        "delay",
                        Json("""{"milliseconds":5000}"""),
                        null,
                        TestContext.Current.CancellationToken));
                Assert.Equal("tool_call_timeout", timeout.Code);
            }
            finally
            {
                await manager.DisconnectAsync(definition.Id, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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

    private static async Task<int> WaitForProcessIdAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path) && int.TryParse(await File.ReadAllTextAsync(path, cancellationToken), out var processId))
            {
                return processId;
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException("The stdio test server did not publish its process ID.");
    }

    private static async Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException($"Stdio process {processId} remained alive after session disposal.");
    }

    private sealed class EmptyEnvironmentValueProvider : IEnvironmentValueProvider
    {
        public string? GetValue(string name) => null;
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly CapturingLogger _logger = new();

        public IReadOnlyList<string> Messages => _logger.Messages;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => _logger;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
