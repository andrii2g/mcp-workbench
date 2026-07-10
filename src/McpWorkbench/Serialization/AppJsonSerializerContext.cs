using System.Text.Json.Serialization;

namespace McpWorkbench.Serialization;

internal sealed record HealthResponse(string Status);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(HealthResponse))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
