using A2G.McpWorkbench.Api;
using A2G.McpWorkbench.Mcp;
using A2G.McpWorkbench.Options;
using A2G.McpWorkbench.Persistence;
using A2G.McpWorkbench.Security;
using A2G.McpWorkbench.Serialization;
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
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<WorkbenchOptions>, WorkbenchOptionsValidator>();
builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .Validate(static options => options.TrustedProxyCount >= 0, "Trusted proxy count cannot be negative.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
builder.Services.AddSingleton<IEnvironmentValueProvider, ProcessEnvironmentValueProvider>();
builder.Services.AddSingleton<SecretReferenceResolver>();
builder.Services.AddSingleton<IMcpClientSessionFactory, McpClientSessionFactory>();
builder.Services.AddSingleton<IMcpConnectionManager, McpConnectionManager>();
builder.Services.AddHostedService<McpRuntimeShutdownService>();
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

var workbenchOptions = app.Services.GetRequiredService<IOptions<WorkbenchOptions>>().Value;
var securityOptions = app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value;
var configuredUrls = (builder.Configuration["urls"] ?? "http://127.0.0.1:5070")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var bindingSecurity = BindingSecurityPolicy.Evaluate(configuredUrls, workbenchOptions.BindToLoopbackOnly, !string.IsNullOrEmpty(securityOptions.ApiKey));
if (bindingSecurity == BindingSecurityResult.RemoteRejected)
{
    throw new InvalidOperationException("Remote binding is disabled by McpWorkbench:BindToLoopbackOnly.");
}
if (bindingSecurity == BindingSecurityResult.RemoteUnprotected)
{
    StartupLog.RemoteBindingWithoutApiKey(app.Logger);
}

app.UseMiddleware<ApiMiddleware>();
app.UseMiddleware<SecurityMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = "no-store, no-cache"
});

await app.Services.GetRequiredService<IServerDefinitionStore>()
    .InitializeAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/health/live", static () => TypedResults.Ok(new HealthResponse("live")));
app.MapGet("/health/ready", static () => TypedResults.Ok(new HealthResponse("ready")));
app.MapWorkbenchApi();

app.Run();

public partial class Program;

internal static partial class StartupLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "MCP Workbench is bound beyond loopback without API-key protection")]
    public static partial void RemoteBindingWithoutApiKey(ILogger logger);
}
