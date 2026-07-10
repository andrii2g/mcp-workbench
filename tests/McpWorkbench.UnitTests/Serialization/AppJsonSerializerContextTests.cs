using System.Text.Json;
using McpWorkbench.Contracts;
using McpWorkbench.Domain;
using McpWorkbench.Serialization;

namespace McpWorkbench.UnitTests.Serialization;

public sealed class AppJsonSerializerContextTests
{
    [Fact]
    public void CreateServerRequest_WhenSerialized_UsesGeneratedCamelCaseMetadata()
    {
        var request = new CreateServerRequest(
            "Remote",
            null,
            true,
            McpTransportKind.Http,
            null,
            new HttpTransportRequest("https://example.test/mcp", McpHttpMode.StreamableHttp, new Dictionary<string, string>()),
            30);

        var json = JsonSerializer.Serialize(request, AppJsonSerializerContext.Default.CreateServerRequest);

        Assert.Contains("\"transport\":\"http\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"streamableHttp\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Description", json, StringComparison.Ordinal);
    }
}
