namespace A2G.McpWorkbench.Security;

internal interface ISecretStore
{
    ValueTask InitializeAsync(CancellationToken cancellationToken);
    ValueTask SetAsync(string id, string value, CancellationToken cancellationToken);
    bool TryGet(string id, out string value);
    ValueTask DeleteAsync(string id, CancellationToken cancellationToken);
}
