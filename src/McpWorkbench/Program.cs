using McpWorkbench.Options;
using McpWorkbench.Persistence;
using McpWorkbench.Serialization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
builder.Services.AddSingleton<IServerDefinitionStore>(services =>
{
    var options = services.GetRequiredService<IOptions<WorkbenchOptions>>().Value;
    return new JsonServerDefinitionStore(
        options.RegistryPath,
        services.GetRequiredService<IAtomicFileWriter>(),
        services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<ILogger<JsonServerDefinitionStore>>(),
        options.MaximumOperationTimeoutSeconds);
});

var app = builder.Build();

await app.Services.GetRequiredService<IServerDefinitionStore>()
    .InitializeAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/health/live", static () => TypedResults.Ok(new HealthResponse("live")));
app.MapGet("/health/ready", static () => TypedResults.Ok(new HealthResponse("ready")));

app.Run();

public partial class Program;
