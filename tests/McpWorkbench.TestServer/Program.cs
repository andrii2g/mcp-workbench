using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpWorkbench.TestServer;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("--malformed-tools", StringComparer.Ordinal))
        {
            await RunMalformedToolsServerAsync();
            return;
        }

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "McpWorkbench.TestServer",
                Title = "MCP Workbench deterministic test server",
                Version = "1.0.0"
            },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = false }
            },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = static (_, _) => ValueTask.FromResult(new ListToolsResult
                {
                    Tools = TestTools.Catalog
                }),
                CallToolHandler = TestTools.CallAsync
            }
        };
        await using var transport = new StdioServerTransport(options, NullLoggerFactory.Instance);
        await using var server = McpServer.Create(transport, options, NullLoggerFactory.Instance);
        await server.RunAsync(CancellationToken.None);
    }

    private static async Task RunMalformedToolsServerAsync()
    {
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var method) || !root.TryGetProperty("id", out var id))
            {
                continue;
            }

            var idJson = id.GetRawText();
            if (method.GetString() == "initialize")
            {
                await Console.Out.WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":" + idJson +
                    ",\"result\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{\"tools\":{}}," +
                    "\"serverInfo\":{\"name\":\"MalformedTools\",\"version\":\"1.0\"}}}");
                await Console.Out.FlushAsync();
            }
            else if (method.GetString() == "tools/list")
            {
                await Console.Out.WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":" + idJson + ",\"result\":{\"tools\":\"not-an-array\"}}");
                await Console.Out.FlushAsync();
            }
        }
    }
}
