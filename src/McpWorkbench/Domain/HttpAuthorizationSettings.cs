using System.Text.Json.Serialization;
using A2G.McpWorkbench.Serialization;

namespace A2G.McpWorkbench.Domain;

[JsonConverter(typeof(HttpAuthorizationKindJsonConverter))]
internal enum HttpAuthorizationKind
{
    Bearer,
    Basic,
    CustomScheme,
    CustomRaw
}

internal sealed record HttpAuthorizationSettings(
    HttpAuthorizationKind Kind,
    string? Username,
    string? Scheme,
    string? Credential);
