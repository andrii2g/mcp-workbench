# Native AOT Contract

Native AOT is a release requirement, not a later optimization.

## Project settings

The production project must include:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>

  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>

  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsAsErrors>IL2026;IL3050;IL2070;IL2072;IL2075</WarningsAsErrors>
</PropertyGroup>
```

Adjust the exact warning list only with a documented reason. Do not suppress whole
warning families globally.

Use:

```csharp
WebApplication.CreateSlimBuilder(args);
```

## Supported application shape

Use:

- ASP.NET Core Minimal APIs;
- static-file middleware;
- source-generated JSON;
- compile-time endpoint registration;
- explicit dependency injection registrations;
- built-in logging;
- built-in health checks where AOT compatible;
- vanilla browser JavaScript.

Do not use:

- MVC controllers;
- Razor Pages;
- Blazor Server;
- runtime compilation;
- runtime assembly scanning;
- dynamic proxy libraries;
- reflection-based validation frameworks;
- runtime plugin loading;
- unbounded polymorphic `object`;
- `dynamic`;
- formatter selection based on discovered CLR types.

## JSON serialization

Create one or more explicit source-generation contexts:

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ApiEnvelope<ServerSummaryResponse>))]
[JsonSerializable(typeof(ApiEnvelope<ServerSummaryResponse[]>))]
[JsonSerializable(typeof(CreateServerRequest))]
[JsonSerializable(typeof(UpdateServerRequest))]
[JsonSerializable(typeof(InvokeToolRequest))]
[JsonSerializable(typeof(InvokeToolResponse))]
internal partial class AppJsonContext : JsonSerializerContext;
```

Generic envelope types must be closed explicitly. An easier alternative is endpoint-
specific non-generic response records if generated metadata becomes cumbersome.

MCP-dynamic content remains `JsonElement`/`JsonNode`, which does not justify enabling
reflection serialization.

Configure Minimal API JSON options with the generated resolver. Add framework resolvers
only when required and verified AOT safe.

## Endpoint binding

Prefer explicit endpoint handlers:

```csharp
private static async Task<IResult> CreateServerAsync(
    CreateServerRequest request,
    IServerRegistry registry,
    CancellationToken cancellationToken)
```

Avoid implicit binding of arbitrary complex framework types.
Validate all request DTOs explicitly.

## Dependency injection

Register concrete services explicitly:

```csharp
services.AddSingleton<IServerRegistry, JsonServerRegistry>();
services.AddSingleton<IMcpConnectionManager, McpConnectionManager>();
services.AddSingleton<IMcpClientFactory, McpClientFactory>();
```

Do not scan assemblies for implementations.

## Logging

Use source-generated logging methods:

```csharp
[LoggerMessage(
    EventId = 1001,
    Level = LogLevel.Information,
    Message = "Connecting MCP server {ServerId} using {Transport}")]
internal static partial void ConnectingServer(
    ILogger logger,
    Guid serverId,
    string transport);
```

This improves predictable AOT behavior and avoids unnecessary allocations. Never include
secret-bearing values in parameters.

## Validation

Implement small explicit validators. Do not add a reflection-heavy validation package
for this project.

A validator returns structured errors:

```text
field
code
message
```

## Build matrix

Required release identifiers:

```text
linux-x64
linux-arm64
win-x64
```

`osx-arm64` may be added after CI runners and smoke tests are available.

Commands:

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet publish src/McpWorkbench/McpWorkbench.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishAot=true
```

Run publish separately for each RID. Do not use `PublishSingleFile` as a substitute for
AOT.

## AOT smoke test

The smoke test must run the actual native executable, not `dotnet <dll>`.

Procedure:

1. Publish to an isolated directory.
2. Create a temporary writable registry directory.
3. Start the executable on a dynamically allocated loopback port.
4. Poll `/health/live` with a bounded deadline.
5. Verify `/health/ready`.
6. Create a test HTTP MCP registration or use a packaged deterministic endpoint.
7. Perform at least one tool discovery and invocation where practical.
8. Send graceful process termination.
9. Require clean exit or force cleanup after a bounded deadline.
10. Capture stdout/stderr as CI artifacts on failure.

For stdio validation, integration tests should also start the deterministic test MCP
server from a normal managed build. A release packaging smoke test may ship its native or
framework-dependent test helper only in CI artifacts, never in the production image.

## Warning policy

A successful native binary is insufficient if the publish log contains unexplained trim
or AOT warnings.

CI must fail on:

- compiler warnings;
- trim warnings;
- AOT analysis warnings;
- package downgrade warnings;
- vulnerable package warnings at the selected enforcement level.

Any narrowly justified suppression belongs next to the affected member with a comment and
an architecture decision entry.

## Native dependencies

Avoid native libraries unless essential. The initial implementation needs none beyond the
runtime and operating system facilities used by .NET and the MCP SDK.

The JSON registry intentionally avoids SQLite/native database packaging.

## Globalization

`InvariantGlobalization=true` is acceptable because:

- API identifiers use ordinal comparisons;
- persisted names do not require culture-aware sorting;
- timestamps use invariant ISO formats;
- UI localization is outside initial scope.

Do not perform user-visible culture-specific formatting on the server. The browser may
format timestamps for display.

## AOT acceptance criteria

A phase is not release complete until:

- all target RIDs publish;
- binaries start;
- health checks pass;
- registry load/write works;
- stdio MCP connect/discover/invoke works on supported applicable RIDs;
- HTTP MCP connect/discover/invoke works;
- no reflection serialization fallback occurs;
- no unresolved trim/AOT warnings remain;
- startup and binary-size measurements are recorded in release notes.

## Development host note

Native AOT does not support cross-OS compilation. On Windows, the Phase 0
`linux-x64` publish reaches the native compiler and fails with
`Cross-OS native compilation is not supported`. Linux publishing is therefore verified
by the Linux CI job. The corresponding `win-x64` publish and native health smoke test
must pass locally before later phases proceed.
