namespace McpWorkbench.Security;

internal static class SensitiveDataRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "cookie",
        "set-cookie",
        "x-api-key",
        "api-key",
        "token",
        "access-token",
        "client-secret",
        "password",
        "secret"
    };

    public static string RedactValue(string key, string value, IReadOnlySet<string>? sensitiveValues = null)
    {
        if (SensitiveKeys.Contains(key) || value.Contains("${ENV:", StringComparison.Ordinal))
        {
            return RedactedValue;
        }

        return RedactText(value, sensitiveValues);
    }

    public static string RedactText(string text, IReadOnlySet<string>? sensitiveValues)
    {
        if (sensitiveValues is null || sensitiveValues.Count == 0)
        {
            return text;
        }

        var result = text;
        foreach (var sensitiveValue in sensitiveValues.Where(static value => value.Length > 0).OrderByDescending(static value => value.Length))
        {
            result = result.Replace(sensitiveValue, RedactedValue, StringComparison.Ordinal);
        }

        return result;
    }

    public static string RedactExceptionMessage(Exception exception, IReadOnlySet<string>? sensitiveValues = null) =>
        RedactText(exception.Message, sensitiveValues);

    public static IReadOnlyDictionary<string, string> RedactDictionary(
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? sensitiveValues = null) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => RedactValue(pair.Key, pair.Value, sensitiveValues),
            StringComparer.OrdinalIgnoreCase);
}
