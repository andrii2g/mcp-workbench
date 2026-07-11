using System.Text;
using System.Text.RegularExpressions;
using A2G.McpWorkbench.Domain;

namespace A2G.McpWorkbench.Security;

internal sealed record ResolvedStdioTransportSettings(
    string Command,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    int ShutdownTimeoutSeconds);

internal sealed record ResolvedHttpTransportSettings(
    Uri Endpoint,
    McpHttpMode Mode,
    IReadOnlyDictionary<string, string> Headers);

internal sealed record ResolvedTransportSettings(
    McpTransportKind Transport,
    ResolvedStdioTransportSettings? Stdio,
    ResolvedHttpTransportSettings? Http,
    IReadOnlySet<string> SensitiveValues);

internal sealed partial class SecretReferenceResolver(IEnvironmentValueProvider environment)
{
    public ResolvedTransportSettings Resolve(McpServerDefinition definition)
    {
        var sensitiveValues = new HashSet<string>(StringComparer.Ordinal);
        return definition.Transport switch
        {
            McpTransportKind.Stdio when definition.Stdio is not null => new ResolvedTransportSettings(
                definition.Transport,
                ResolveStdio(definition.Stdio, sensitiveValues),
                null,
                sensitiveValues),
            McpTransportKind.Http when definition.Http is not null => new ResolvedTransportSettings(
                definition.Transport,
                null,
                ResolveHttp(definition.Http, sensitiveValues),
                sensitiveValues),
            _ => throw new InvalidOperationException("Validated server definition has mismatched transport settings.")
        };
    }

    public string ResolveValue(string value, ISet<string> sensitiveValues)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(sensitiveValues);

        var matches = SecretReferencePattern().Matches(value);
        if (value.Contains("${ENV:", StringComparison.Ordinal) &&
            (matches.Count == 0 || HasUnmatchedReferenceSyntax(value)))
        {
            throw new SecretReferenceException(
                "secret_reference_invalid",
                ExtractUnsafeVariableName(value),
                "Environment secret reference syntax is invalid.");
        }

        if (matches.Count == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var position = 0;
        foreach (Match match in matches)
        {
            builder.Append(value, position, match.Index - position);
            var variableName = match.Groups[1].Value;
            var resolvedValue = environment.GetValue(variableName);
            if (resolvedValue is null)
            {
                throw new SecretReferenceException(
                    "secret_reference_missing",
                    variableName,
                    $"Environment variable '{variableName}' is not available.");
            }

            builder.Append(resolvedValue);
            if (resolvedValue.Length > 0)
            {
                sensitiveValues.Add(resolvedValue);
            }

            position = match.Index + match.Length;
        }

        builder.Append(value, position, value.Length - position);
        return builder.ToString();
    }

    private ResolvedStdioTransportSettings ResolveStdio(
        StdioTransportSettings settings,
        ISet<string> sensitiveValues)
    {
        var environmentValues = settings.Environment.ToDictionary(
            pair => pair.Key,
            pair => ResolveValue(pair.Value, sensitiveValues),
            StringComparer.Ordinal);
        return new ResolvedStdioTransportSettings(
            settings.Command,
            settings.Arguments.ToArray(),
            settings.WorkingDirectory,
            environmentValues,
            settings.ShutdownTimeoutSeconds);
    }

    private ResolvedHttpTransportSettings ResolveHttp(
        HttpTransportSettings settings,
        ISet<string> sensitiveValues)
    {
        var headers = settings.Headers.ToDictionary(
            pair => pair.Key,
            pair => ResolveValue(pair.Value, sensitiveValues),
            StringComparer.OrdinalIgnoreCase);
        return new ResolvedHttpTransportSettings(new Uri(settings.Endpoint, UriKind.Absolute), settings.Mode, headers);
    }

    private static bool HasUnmatchedReferenceSyntax(string value)
    {
        var withoutMatches = SecretReferencePattern().Replace(value, string.Empty);
        return withoutMatches.Contains("${ENV:", StringComparison.Ordinal);
    }

    private static string ExtractUnsafeVariableName(string value)
    {
        var start = value.IndexOf("${ENV:", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += "${ENV:".Length;
        var end = value.IndexOf('}', start);
        return end < 0 ? value[start..] : value[start..end];
    }

    [GeneratedRegex(@"\$\{ENV:([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex SecretReferencePattern();
}
