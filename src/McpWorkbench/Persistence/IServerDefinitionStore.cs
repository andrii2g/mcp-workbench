using McpWorkbench.Domain;

namespace McpWorkbench.Persistence;

internal interface IServerDefinitionStore
{
    ValueTask InitializeAsync(CancellationToken cancellationToken);
    ValueTask<RegistryDocument> GetSnapshotAsync(CancellationToken cancellationToken);
    ValueTask<McpServerDefinition?> GetAsync(Guid id, CancellationToken cancellationToken);
    ValueTask<McpServerDefinition> CreateAsync(McpServerDefinition definition, CancellationToken cancellationToken);
    ValueTask<McpServerDefinition> ReplaceAsync(McpServerDefinition definition, CancellationToken cancellationToken);
    ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
