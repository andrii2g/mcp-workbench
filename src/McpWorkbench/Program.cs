using McpWorkbench.Options;
using McpWorkbench.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5070");
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));
builder.Services.AddOptions<WorkbenchOptions>()
    .Bind(builder.Configuration.GetSection(WorkbenchOptions.SectionName))
    .Validate(static options => options.MaximumOperationTimeoutSeconds > 0, "Maximum operation timeout must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .Validate(static options => options.TrustedProxyCount >= 0, "Trusted proxy count cannot be negative.")
    .ValidateOnStart();

var app = builder.Build();

app.MapGet("/health/live", static () => TypedResults.Ok(new HealthResponse("live")));
app.MapGet("/health/ready", static () => TypedResults.Ok(new HealthResponse("ready")));

app.Run();

public partial class Program;
