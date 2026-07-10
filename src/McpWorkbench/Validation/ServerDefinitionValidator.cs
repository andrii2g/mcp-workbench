using McpWorkbench.Contracts;
using McpWorkbench.Domain;

namespace McpWorkbench.Validation;

internal static class ServerDefinitionValidator
{
    private const int MaximumNameLength = 100;
    private const int MaximumDescriptionLength = 1000;
    private const int MaximumCommandLength = 1024;
    private const int MaximumArgumentCount = 128;
    private const int MaximumArgumentLength = 8192;
    private const int MaximumEnvironmentCount = 128;
    private const int MaximumHeaderCount = 64;
    private const int MaximumHeaderValueLength = 8192;

    private static readonly HashSet<string> RuntimeControlledHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Content-Length",
        "Transfer-Encoding",
        "Connection"
    };

    public static ValidationResult<CreateServerRequest> Validate(
        CreateServerRequest request,
        int maximumOperationTimeoutSeconds = 300)
    {
        var errors = new List<ValidationError>();
        var name = request.Name?.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Add(errors, "name", "required", "Name is required.");
        }
        else if (name.EnumerateRunes().Count() > MaximumNameLength)
        {
            Add(errors, "name", "too_long", $"Name must not exceed {MaximumNameLength} Unicode characters.");
        }

        if (description is not null && description.Length > MaximumDescriptionLength)
        {
            Add(errors, "description", "too_long", $"Description must not exceed {MaximumDescriptionLength} characters.");
        }

        if (request.OperationTimeoutSeconds < 1 || request.OperationTimeoutSeconds > maximumOperationTimeoutSeconds)
        {
            Add(errors, "operationTimeoutSeconds", "out_of_range", $"Operation timeout must be between 1 and {maximumOperationTimeoutSeconds} seconds.");
        }

        switch (request.Transport)
        {
            case McpTransportKind.Stdio:
                if (request.Stdio is null || request.Http is not null)
                {
                    Add(errors, "transport", "transport_mismatch", "Stdio transport requires only stdio settings.");
                }
                else
                {
                    ValidateStdio(request.Stdio, errors);
                }

                break;
            case McpTransportKind.Http:
                if (request.Http is null || request.Stdio is not null)
                {
                    Add(errors, "transport", "transport_mismatch", "HTTP transport requires only HTTP settings.");
                }
                else
                {
                    ValidateHttp(request.Http, errors);
                }

                break;
            default:
                Add(errors, "transport", "unsupported", "Transport must be stdio or http.");
                break;
        }

        if (errors.Count != 0)
        {
            return new(null, errors);
        }

        var normalized = request with
        {
            Name = name,
            Description = description,
            Stdio = Normalize(request.Stdio),
            Http = Normalize(request.Http)
        };
        return new(normalized, errors);
    }

    public static ValidationResult<UpdateServerRequest> Validate(
        UpdateServerRequest request,
        int maximumOperationTimeoutSeconds = 300)
    {
        var createResult = Validate(
            new CreateServerRequest(
                request.Name,
                request.Description,
                request.Enabled,
                request.Transport,
                request.Stdio,
                request.Http,
                request.OperationTimeoutSeconds),
            maximumOperationTimeoutSeconds);
        if (!createResult.IsValid)
        {
            return new(null, createResult.Errors);
        }

        var normalized = createResult.Value!;
        return new(
            new UpdateServerRequest(
                normalized.Name,
                normalized.Description,
                normalized.Enabled,
                normalized.Transport,
                normalized.Stdio,
                normalized.Http,
                normalized.OperationTimeoutSeconds),
            createResult.Errors);
    }

    private static void ValidateStdio(StdioTransportRequest settings, List<ValidationError> errors)
    {
        var command = settings.Command?.Trim();
        if (string.IsNullOrEmpty(command))
        {
            Add(errors, "stdio.command", "required", "Command is required.");
        }
        else if (command.Length > MaximumCommandLength || ContainsControlCharacter(command))
        {
            Add(errors, "stdio.command", "invalid", "Command is too long or contains control characters.");
        }

        var arguments = settings.Arguments ?? [];
        if (arguments.Count > MaximumArgumentCount)
        {
            Add(errors, "stdio.arguments", "too_many", $"No more than {MaximumArgumentCount} arguments are allowed.");
        }

        if (arguments.Any(argument => argument is null || argument.Length > MaximumArgumentLength || ContainsNull(argument)))
        {
            Add(errors, "stdio.arguments", "invalid", "Arguments must not contain null values, NUL characters, or values longer than 8192 characters.");
        }

        if (settings.WorkingDirectory is not null && ContainsControlCharacter(settings.WorkingDirectory))
        {
            Add(errors, "stdio.workingDirectory", "invalid", "Working directory contains control characters.");
        }

        var environment = settings.Environment ?? new Dictionary<string, string>();
        if (environment.Count > MaximumEnvironmentCount)
        {
            Add(errors, "stdio.environment", "too_many", $"No more than {MaximumEnvironmentCount} environment entries are allowed.");
        }

        foreach (var pair in environment)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Contains('=') ||
                ContainsControlCharacter(pair.Key) || pair.Value is null || ContainsControlCharacter(pair.Value))
            {
                Add(errors, $"stdio.environment.{pair.Key}", "invalid", "Environment names and values must be valid process environment entries.");
            }
        }

        if (settings.ShutdownTimeoutSeconds is < 1 or > 30)
        {
            Add(errors, "stdio.shutdownTimeoutSeconds", "out_of_range", "Shutdown timeout must be between 1 and 30 seconds.");
        }
    }

    private static void ValidateHttp(HttpTransportRequest settings, List<ValidationError> errors)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment) ||
            (endpoint.Scheme == Uri.UriSchemeHttp && !IsLoopback(endpoint)))
        {
            Add(errors, "http.endpoint", "invalid", "Endpoint must be an absolute HTTPS URI, or HTTP on loopback, without user info or a fragment.");
        }

        var headers = settings.Headers ?? new Dictionary<string, string>();
        if (headers.Count > MaximumHeaderCount)
        {
            Add(errors, "http.headers", "too_many", $"No more than {MaximumHeaderCount} headers are allowed.");
        }

        foreach (var pair in headers)
        {
            if (!IsHeaderName(pair.Key) || RuntimeControlledHeaders.Contains(pair.Key) || pair.Value is null ||
                pair.Value.Length > MaximumHeaderValueLength || ContainsControlCharacter(pair.Value, allowTab: true))
            {
                Add(errors, $"http.headers.{pair.Key}", "invalid", "Header name or value is invalid or controlled by the HTTP runtime.");
            }
        }
    }

    private static StdioTransportRequest? Normalize(StdioTransportRequest? settings) => settings is null ? null : settings with
    {
        Command = settings.Command!.Trim(),
        WorkingDirectory = string.IsNullOrWhiteSpace(settings.WorkingDirectory) ? null : settings.WorkingDirectory.Trim(),
        Arguments = settings.Arguments ?? [],
        Environment = settings.Environment ?? new Dictionary<string, string>()
    };

    private static HttpTransportRequest? Normalize(HttpTransportRequest? settings) => settings is null ? null : settings with
    {
        Endpoint = settings.Endpoint!.Trim(),
        Headers = settings.Headers ?? new Dictionary<string, string>()
    };

    private static bool IsHeaderName(string value) =>
        !string.IsNullOrEmpty(value) && !value.Any(character => !IsTokenCharacter(character));

    private static bool IsTokenCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || "!#$%&'*+-.^_`|~".Contains(value, StringComparison.Ordinal);

    private static bool IsLoopback(Uri endpoint) =>
        endpoint.IsLoopback || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsNull(string value) => value.Contains('\0');

    private static bool ContainsControlCharacter(string value, bool allowTab = false) =>
        value.Any(character => char.IsControl(character) && !(allowTab && character == '\t'));

    private static void Add(List<ValidationError> errors, string field, string code, string message) =>
        errors.Add(new ValidationError(field, code, message));
}
