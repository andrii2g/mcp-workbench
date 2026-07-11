using McpWorkbench.Domain;

namespace McpWorkbench.Mcp;

internal sealed class McpServerRuntime : IDisposable
{
    public McpServerRuntime(Guid serverId, int historyCapacity)
    {
        ServerId = serverId;
        History = new BoundedExecutionHistory(historyCapacity);
    }

    public Guid ServerId { get; }
    public McpConnectionState Status { get; set; } = McpConnectionState.Disconnected;
    public IMcpClientSession? Session { get; set; }
    public McpSessionInfo? SessionInfo { get; set; }
    public DateTimeOffset? ConnectedAtUtc { get; set; }
    public DateTimeOffset? LastOperationAtUtc { get; set; }
    public SafeRuntimeError? LastError { get; set; }
    public IReadOnlyList<ToolCatalogEntry>? ToolCatalog { get; set; }
    public CancellationTokenSource LifetimeCancellation { get; set; } = new();
    public SemaphoreSlim LifecycleGate { get; } = new(1, 1);
    public SemaphoreSlim InvocationGate { get; } = new(1, 1);
    public BoundedExecutionHistory History { get; }

    public void Dispose()
    {
        LifetimeCancellation.Dispose();
        LifecycleGate.Dispose();
        InvocationGate.Dispose();
    }
}
