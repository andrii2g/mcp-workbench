using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpWorkbench.TestServer;

internal static class TestTools
{
    private static readonly JsonElement StringInputSchema = ParseSchema("""
        {"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}
        """);
    private static readonly JsonElement AddInputSchema = ParseSchema("""
        {"type":"object","properties":{"a":{"type":"number"},"b":{"type":"number"}},"required":["a","b"]}
        """);
    private static readonly JsonElement DelayInputSchema = ParseSchema("""
        {"type":"object","properties":{"milliseconds":{"type":"integer","minimum":0,"maximum":30000}},"required":["milliseconds"]}
        """);
    private static readonly JsonElement EmptyInputSchema = ParseSchema("""{"type":"object"}""");

    public static IList<Tool> Catalog { get; } =
    [
        Tool("echo", "Echo", "Returns supplied text.", StringInputSchema),
        Tool("add", "Add", "Adds two numbers.", AddInputSchema),
        Tool("fail", "Fail", "Returns an MCP tool error.", EmptyInputSchema),
        Tool("delay", "Delay", "Waits for a bounded duration.", DelayInputSchema),
        Tool("structured", "Structured", "Returns nested structured JSON.", EmptyInputSchema),
        Tool("large-text", "Large text", "Returns bounded large text.", EmptyInputSchema)
    ];

    public static async ValueTask<CallToolResult> CallAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken) => context.Params?.Name switch
        {
            "echo" => Text(GetString(context.Params.Arguments, "text")),
            "add" => Text((GetDouble(context.Params.Arguments, "a") + GetDouble(context.Params.Arguments, "b")).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "fail" => new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "intentional failure" }] },
            "delay" => await DelayAsync(GetInt32(context.Params.Arguments, "milliseconds"), cancellationToken),
            "structured" => Structured(),
            "large-text" => Text(new string('x', 65_536)),
            _ => new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "unknown tool" }] }
        };

    private static Tool Tool(string name, string title, string description, JsonElement inputSchema) => new()
    {
        Name = name,
        Title = title,
        Description = description,
        InputSchema = inputSchema.Clone(),
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = name is not "fail" and not "delay",
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        }
    };

    private static CallToolResult Text(string text) => new()
    {
        Content = [new TextContentBlock { Text = text }]
    };

    private static async ValueTask<CallToolResult> DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        await Task.Delay(Math.Clamp(milliseconds, 0, 30_000), cancellationToken);
        return Text("completed");
    }

    private static CallToolResult Structured()
    {
        using var document = JsonDocument.Parse("""{"result":{"value":42,"items":[1,2,3]}}""");
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = "structured" }],
            StructuredContent = document.RootElement.Clone()
        };
    }

    private static string GetString(IDictionary<string, JsonElement>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static double GetDouble(IDictionary<string, JsonElement>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var value) ? value.GetDouble() : 0;

    private static int GetInt32(IDictionary<string, JsonElement>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var value) ? value.GetInt32() : 0;

    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
