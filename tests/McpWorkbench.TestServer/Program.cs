using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpWorkbench.TestServer;

internal static class Program
{
    public static async Task Main()
    {
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
}
