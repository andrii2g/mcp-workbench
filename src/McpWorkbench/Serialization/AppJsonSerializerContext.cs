using System.Text.Json.Serialization;
using A2G.McpWorkbench.Contracts;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Persistence;
using A2G.McpWorkbench.Security;
using A2G.McpWorkbench.Validation;

namespace A2G.McpWorkbench.Serialization;

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
[JsonSerializable(typeof(SecretVaultDocument))]
[JsonSerializable(typeof(McpSessionInfo))]
[JsonSerializable(typeof(ToolCatalogEntry[]))]
[JsonSerializable(typeof(ToolInvocationOutcome))]
[JsonSerializable(typeof(ToolExecutionRecord[]))]
[JsonSerializable(typeof(ApiErrorResponse))]
[JsonSerializable(typeof(ConnectRequest))]
[JsonSerializable(typeof(InvokeToolRequest))]
[JsonSerializable(typeof(ServerDefinitionResponse))]
[JsonSerializable(typeof(ApiResponse<ServerDefinitionResponse>))]
[JsonSerializable(typeof(ApiResponse<ServerDefinitionResponse[]>))]
[JsonSerializable(typeof(ApiResponse<ServerRuntimeSnapshot>))]
[JsonSerializable(typeof(ApiResponse<ConnectResponse>))]
[JsonSerializable(typeof(ApiResponse<PingResponse>))]
[JsonSerializable(typeof(ApiResponse<ToolCatalogEntry>))]
[JsonSerializable(typeof(ApiResponse<ToolCatalogEntry[]>))]
[JsonSerializable(typeof(ApiResponse<ToolInvocationResponse>))]
[JsonSerializable(typeof(IReadOnlyList<ValidationError>))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
