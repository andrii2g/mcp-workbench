using System.Text.Json;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Security;
using A2G.McpWorkbench.Serialization;

namespace A2G.McpWorkbench.UnitTests.Security;

public sealed class SecretReferenceResolverTests
{
    [Fact]
    public void ResolveValue_WhenValueHasMultipleReferences_ResolvesEachReference()
    {
        var resolver = Resolver(("USER", "andrii"), ("TOKEN", "sentinel-token"));
        var sensitiveValues = new HashSet<string>(StringComparer.Ordinal);

        var result = resolver.ResolveValue("${ENV:USER}:${ENV:TOKEN}", sensitiveValues);

        Assert.Equal("andrii:sentinel-token", result);
        Assert.Equal(["andrii", "sentinel-token"], sensitiveValues.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ResolveValue_WhenResolvedValueContainsReference_DoesNotResolveRecursively()
    {
        var resolver = Resolver(("FIRST", "${ENV:SECOND}"), ("SECOND", "sentinel-secret"));
        var sensitiveValues = new HashSet<string>(StringComparer.Ordinal);

        var result = resolver.ResolveValue("Value=${ENV:FIRST}", sensitiveValues);

        Assert.Equal("Value=${ENV:SECOND}", result);
        Assert.DoesNotContain("sentinel-secret", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("${ENV:1INVALID}")]
    [InlineData("${ENV:MISSING_BRACE")]
    [InlineData("${ENV:BAD-NAME}")]
    public void ResolveValue_WhenReferenceSyntaxIsInvalid_ThrowsTypedError(string value)
    {
        var exception = Assert.Throws<SecretReferenceException>(() =>
            Resolver().ResolveValue(value, new HashSet<string>()));

        Assert.Equal("secret_reference_invalid", exception.Code);
    }

    [Fact]
    public void ResolveValue_WhenVariableIsMissing_ThrowsTypedSafeError()
    {
        var exception = Assert.Throws<SecretReferenceException>(() =>
            Resolver().ResolveValue("Bearer ${ENV:NOT_SET}", new HashSet<string>()));

        Assert.Equal("secret_reference_missing", exception.Code);
        Assert.Equal("NOT_SET", exception.VariableName);
        Assert.DoesNotContain("Bearer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_WhenStdioDefinitionContainsReferences_ResolvesOnlyEnvironmentValues()
    {
        var definition = StdioDefinition() with
        {
            Name = "${ENV:TOKEN}",
            Stdio = StdioDefinition().Stdio! with
            {
                Command = "${ENV:TOKEN}",
                Arguments = ["${ENV:TOKEN}"],
                WorkingDirectory = "${ENV:TOKEN}",
                Environment = new Dictionary<string, string> { ["TOKEN"] = "Bearer ${ENV:TOKEN}" }
            }
        };

        var result = Resolver(("TOKEN", "sentinel-secret")).Resolve(definition);

        Assert.Equal("Bearer sentinel-secret", result.Stdio?.Environment["TOKEN"]);
        Assert.Equal("${ENV:TOKEN}", result.Stdio?.Command);
        Assert.Equal("${ENV:TOKEN}", result.Stdio?.Arguments[0]);
        Assert.Equal("${ENV:TOKEN}", result.Stdio?.WorkingDirectory);
        Assert.Equal("${ENV:TOKEN}", definition.Name);
    }

    [Fact]
    public void Resolve_WhenHttpDefinitionContainsHeaderReference_LeavesPersistedDefinitionUnchanged()
    {
        var definition = HttpDefinition();

        var result = Resolver(("TOKEN", "sentinel-secret")).Resolve(definition);
        var persistedJson = JsonSerializer.Serialize(definition, AppJsonSerializerContext.Default.McpServerDefinition);

        Assert.Equal("Bearer sentinel-secret", result.Http?.Headers["Authorization"]);
        Assert.Contains("${ENV:TOKEN}", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-secret", persistedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveValue_WhenStoredSecretExists_ResolvesIt()
    {
        var id = Guid.NewGuid().ToString();
        var store = new DictionarySecretStore(new Dictionary<string, string> { [id] = "stored-sentinel" });
        var resolver = new SecretReferenceResolver(new DictionaryEnvironmentValueProvider([]), store);
        var sensitive = new HashSet<string>();

        var result = resolver.ResolveValue($"Bearer ${{SECRET:{id}}}", sensitive);

        Assert.Equal("Bearer stored-sentinel", result);
        Assert.Contains("stored-sentinel", sensitive);
    }

    [Theory]
    [InlineData((int)HttpAuthorizationKind.Bearer, null, null, "Bearer sentinel-secret")]
    [InlineData((int)HttpAuthorizationKind.Basic, "alice", null, "Basic YWxpY2U6c2VudGluZWwtc2VjcmV0")]
    [InlineData((int)HttpAuthorizationKind.CustomScheme, null, "Token", "Token sentinel-secret")]
    [InlineData((int)HttpAuthorizationKind.CustomRaw, null, null, "sentinel-secret")]
    public void Resolve_WhenHttpAuthorizationIsConfigured_GeneratesHeader(
        int kindValue,
        string? username,
        string? scheme,
        string expected)
    {
        var definition = HttpDefinition() with
        {
            Http = HttpDefinition().Http! with
            {
                Headers = new Dictionary<string, string> { ["X-Tenant"] = "tenant" },
                Authorization = new HttpAuthorizationSettings((HttpAuthorizationKind)kindValue, username, scheme, "${ENV:TOKEN}")
            }
        };

        var result = Resolver(("TOKEN", "sentinel-secret")).Resolve(definition);

        Assert.Equal(expected, result.Http?.Headers["Authorization"]);
        Assert.Equal("tenant", result.Http?.Headers["X-Tenant"]);
        Assert.Contains(expected, result.SensitiveValues);
    }

    private static SecretReferenceResolver Resolver(params (string Name, string Value)[] values) =>
        new(new DictionaryEnvironmentValueProvider(values));

    private static McpServerDefinition StdioDefinition() => new(
        Guid.NewGuid(),
        "Local",
        null,
        true,
        McpTransportKind.Stdio,
        new StdioTransportSettings("dotnet", ["server.dll"], null, new Dictionary<string, string>(), 5),
        null,
        30,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    private static McpServerDefinition HttpDefinition() => new(
        Guid.NewGuid(),
        "Remote",
        null,
        true,
        McpTransportKind.Http,
        null,
        new HttpTransportSettings(
            "https://example.test/mcp",
            McpHttpMode.Auto,
            new Dictionary<string, string> { ["Authorization"] = "Bearer ${ENV:TOKEN}" }),
        30,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    private sealed class DictionaryEnvironmentValueProvider(IEnumerable<(string Name, string Value)> values) : IEnvironmentValueProvider
    {
        private readonly Dictionary<string, string> _values = values.ToDictionary(
            pair => pair.Name,
            pair => pair.Value,
            StringComparer.Ordinal);

        public string? GetValue(string name) => _values.GetValueOrDefault(name);
    }

    private sealed class DictionarySecretStore(IReadOnlyDictionary<string, string> values) : ISecretStore
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask SetAsync(string id, string value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public bool TryGet(string id, out string value) => values.TryGetValue(id, out value!);
        public ValueTask DeleteAsync(string id, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
