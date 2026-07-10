using McpWorkbench.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5070");
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = builder.Build();

app.MapGet("/health/live", static () => TypedResults.Ok(new HealthResponse("live")));
app.MapGet("/health/ready", static () => TypedResults.Ok(new HealthResponse("ready")));

app.Run();

public partial class Program;
