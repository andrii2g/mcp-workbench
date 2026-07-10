using McpWorkbench.Contracts;
using McpWorkbench.Domain;
using McpWorkbench.Validation;

namespace McpWorkbench.UnitTests.Validation;

public sealed class ServerDefinitionValidatorTests
{
    [Fact]
    public void Validate_WhenStdioDefinitionIsValid_NormalizesValues()
    {
        var request = ValidStdio() with { Name = "  Local server  ", Description = "  Test  " };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Equal("Local server", result.Value?.Name);
        Assert.Equal("Test", result.Value?.Description);
        Assert.Equal("dotnet", result.Value?.Stdio?.Command);
    }

    [Fact]
    public void Validate_WhenHttpDefinitionIsValid_AcceptsHttps()
    {
        var request = ValidHttp();

        var result = ServerDefinitionValidator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("https://user@example.test/mcp")]
    [InlineData("https://example.test/mcp#fragment")]
    [InlineData("ftp://example.test/mcp")]
    [InlineData("http://example.test/mcp")]
    public void Validate_WhenHttpEndpointIsUnsafe_ReturnsEndpointError(string endpoint)
    {
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Endpoint = endpoint } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "http.endpoint");
    }

    [Fact]
    public void Validate_WhenHttpEndpointUsesLoopbackHttp_AcceptsEndpoint()
    {
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Endpoint = "http://127.0.0.1:3000/mcp" } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData((int)McpTransportKind.Stdio, false, false)]
    [InlineData((int)McpTransportKind.Stdio, true, true)]
    [InlineData((int)McpTransportKind.Http, true, false)]
    [InlineData((int)McpTransportKind.Http, true, true)]
    public void Validate_WhenTransportSettingsDoNotMatch_ReturnsTransportError(
        int transportValue,
        bool hasStdio,
        bool hasHttp)
    {
        var transport = (McpTransportKind)transportValue;
        var request = new CreateServerRequest(
            "Server",
            null,
            true,
            transport,
            hasStdio ? ValidStdio().Stdio : null,
            hasHttp ? ValidHttp().Http : null,
            30);

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "transport");
    }

    [Fact]
    public void Validate_WhenRuntimeControlledHeaderIsConfigured_ReturnsHeaderError()
    {
        var headers = new Dictionary<string, string> { ["Host"] = "other.example" };
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Headers = headers } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "http.headers.Host");
    }

    [Fact]
    public void Validate_WhenEnvironmentNameIsInvalid_ReturnsEnvironmentError()
    {
        var environment = new Dictionary<string, string> { ["BAD=NAME"] = "value" };
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Environment = environment } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field.StartsWith("stdio.environment", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void Validate_WhenOperationTimeoutIsOutsideRange_ReturnsTimeoutError(int timeout)
    {
        var request = ValidStdio() with { OperationTimeoutSeconds = timeout };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "operationTimeoutSeconds");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNameIsMissing_ReturnsRequiredError(string? name)
    {
        var result = ServerDefinitionValidator.Validate(ValidStdio() with { Name = name });

        Assert.Contains(result.Errors, error => error.Field == "name" && error.Code == "required");
    }

    private static CreateServerRequest ValidStdio() => new(
        "Local server",
        null,
        true,
        McpTransportKind.Stdio,
        new StdioTransportRequest("dotnet", ["server.dll"], null, new Dictionary<string, string>(), 5),
        null,
        30);

    private static CreateServerRequest ValidHttp() => new(
        "Remote server",
        null,
        true,
        McpTransportKind.Http,
        null,
        new HttpTransportRequest("https://example.test/mcp", McpHttpMode.Auto, new Dictionary<string, string>()),
        30);
}
