using McpWorkbench.Domain;

namespace McpWorkbench.Persistence;

internal sealed record RegistryDocument(
    int SchemaVersion,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<McpServerDefinition> Servers)
{
    public const int CurrentSchemaVersion = 1;

    public static RegistryDocument Empty(DateTimeOffset timestamp) =>
        new(CurrentSchemaVersion, 0, timestamp, []);
}
