using A2G.McpWorkbench.Contracts;
using A2G.McpWorkbench.Domain;
using A2G.McpWorkbench.Validation;

namespace A2G.McpWorkbench.UnitTests.Validation;

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
    [InlineData("BAD\nNAME", "value")]
    [InlineData("NAME", "bad\nvalue")]
    public void Validate_WhenEnvironmentContainsControlCharacters_ReturnsEnvironmentError(string name, string value)
    {
        var environment = new Dictionary<string, string> { [name] = value };
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Environment = environment } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field.StartsWith("stdio.environment", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenEnvironmentValueIsNull_ReturnsEnvironmentError()
    {
        var environment = new Dictionary<string, string> { ["TOKEN"] = null! };
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Environment = environment } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.environment.TOKEN");
    }

    [Fact]
    public void Validate_WhenHeaderValueIsNull_ReturnsHeaderError()
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = null! };
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Headers = headers } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "http.headers.Authorization");
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

    [Fact]
    public void Validate_WhenUpdateIsValid_NormalizesValues()
    {
        var request = new UpdateServerRequest(
            "  Updated  ",
            "  Description  ",
            true,
            McpTransportKind.Http,
            null,
            ValidHttp().Http,
            30);

        var result = ServerDefinitionValidator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Equal("Updated", result.Value?.Name);
        Assert.Equal("Description", result.Value?.Description);
    }

    [Fact]
    public void Validate_WhenUpdateTransportSettingsMismatch_ReturnsTransportError()
    {
        var request = new UpdateServerRequest(
            "Updated",
            null,
            true,
            McpTransportKind.Http,
            ValidStdio().Stdio,
            ValidHttp().Http,
            30);

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "transport");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Validate_WhenShutdownTimeoutIsOutsideRange_ReturnsError(int timeout)
    {
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { ShutdownTimeoutSeconds = timeout } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.shutdownTimeoutSeconds");
    }

    [Fact]
    public void Validate_WhenNameExceedsUnicodeLimit_ReturnsError()
    {
        var request = ValidStdio() with { Name = string.Concat(Enumerable.Repeat("😀", 101)) };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "name" && error.Code == "too_long");
    }

    [Fact]
    public void Validate_WhenDescriptionExceedsLimit_ReturnsError()
    {
        var result = ServerDefinitionValidator.Validate(ValidStdio() with { Description = new string('x', 1001) });

        Assert.Contains(result.Errors, error => error.Field == "description");
    }

    [Fact]
    public void Validate_WhenArgumentCountExceedsLimit_ReturnsError()
    {
        var arguments = Enumerable.Repeat("x", 129).ToArray();
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Arguments = arguments } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.arguments" && error.Code == "too_many");
    }

    [Fact]
    public void Validate_WhenArgumentExceedsLengthLimit_ReturnsError()
    {
        var request = ValidStdio() with
        {
            Stdio = ValidStdio().Stdio! with { Arguments = [new string('x', 8193)] }
        };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.arguments" && error.Code == "invalid");
    }

    [Fact]
    public void Validate_WhenEnvironmentCountExceedsLimit_ReturnsError()
    {
        var environment = Enumerable.Range(0, 129).ToDictionary(index => $"KEY_{index}", _ => "value");
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Environment = environment } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.environment" && error.Code == "too_many");
    }

    [Fact]
    public void Validate_WhenHeaderCountExceedsLimit_ReturnsError()
    {
        var headers = Enumerable.Range(0, 65).ToDictionary(index => $"X-Test-{index}", _ => "value");
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Headers = headers } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "http.headers" && error.Code == "too_many");
    }

    [Fact]
    public void Validate_WhenHeaderValueExceedsLengthLimit_ReturnsError()
    {
        var headers = new Dictionary<string, string> { ["X-Test"] = new string('x', 8193) };
        var request = ValidHttp() with { Http = ValidHttp().Http! with { Headers = headers } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "http.headers.X-Test" && error.Code == "invalid");
    }

    [Fact]
    public void Validate_WhenCommandExceedsLengthLimit_ReturnsError()
    {
        var request = ValidStdio() with { Stdio = ValidStdio().Stdio! with { Command = new string('x', 1025) } };

        var result = ServerDefinitionValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.Field == "stdio.command" && error.Code == "invalid");
    }

    [Fact]
    public void Validate_WhenTransportEnumIsUnknown_ReturnsUnsupportedError()
    {
        var result = ServerDefinitionValidator.Validate(ValidStdio() with { Transport = (McpTransportKind)99 });

        Assert.Contains(result.Errors, error => error.Field == "transport" && error.Code == "unsupported");
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
