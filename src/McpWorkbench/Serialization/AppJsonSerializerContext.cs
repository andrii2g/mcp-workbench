using System.Text.Json.Serialization;
using McpWorkbench.Contracts;
using McpWorkbench.Domain;
using McpWorkbench.Persistence;
using McpWorkbench.Validation;

namespace McpWorkbench.Serialization;

internal sealed record HealthResponse(string Status);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(McpServerDefinition))]
[JsonSerializable(typeof(McpServerDefinition[]))]
[JsonSerializable(typeof(CreateServerRequest))]
[JsonSerializable(typeof(UpdateServerRequest))]
[JsonSerializable(typeof(ServerRuntimeSnapshot))]
[JsonSerializable(typeof(ValidationError[]))]
[JsonSerializable(typeof(RegistryDocument))]
[JsonSerializable(typeof(McpSessionInfo))]
[JsonSerializable(typeof(ToolCatalogEntry[]))]
[JsonSerializable(typeof(ToolInvocationOutcome))]
[JsonSerializable(typeof(ToolExecutionRecord[]))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
