namespace A2G.McpWorkbench.Mcp;

internal sealed class McpRuntimeShutdownService(IMcpConnectionManager manager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await manager.DisconnectAllAsync(cancellationToken);
}
