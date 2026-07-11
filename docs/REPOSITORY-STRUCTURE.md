# Target repository structure

This document defines the intended repository tree after version 0.1.0 is implemented. Codex may introduce a narrowly necessary file, but it must not introduce new architectural layers without updating the decision record and explaining why.

```text
mcp-workbench/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── release.yml
├── data/
│   └── .gitkeep
├── docs/
│   ├── API.md
│   ├── ARCHITECTURE.md
│   ├── CONFIGURATION.md
│   ├── DECISIONS.md
│   ├── DOMAIN-MODEL.md
│   ├── IMPLEMENTATION-CHECKLIST.md
│   ├── MCP-INTEGRATION.md
│   ├── NATIVE-AOT.md
│   ├── QUICKSTART.md
│   ├── REFERENCES.md
│   ├── REPOSITORY-STRUCTURE.md
│   ├── SECURITY.md
│   ├── TESTING.md
│   └── UI-SPEC.md
├── requests/
│   └── McpWorkbench.http
├── samples/
│   └── servers.example.json
├── scripts/
│   ├── aot-smoke.ps1
│   ├── aot-smoke.sh
│   ├── build.ps1
│   ├── build.sh
│   ├── publish-aot.ps1
│   ├── publish-aot.sh
│   ├── test.ps1
│   └── test.sh
├── src/
│   └── McpWorkbench/
│       ├── Api/
│       │   ├── ApiEndpointExtensions.cs
│       │   ├── ApiErrorMapper.cs
│       │   ├── ConnectionEndpoints.cs
│       │   ├── ServerEndpoints.cs
│       │   ├── SystemEndpoints.cs
│       │   └── ToolEndpoints.cs
│       ├── Contracts/
│       │   ├── ApiErrorContracts.cs
│       │   ├── ConnectionContracts.cs
│       │   ├── ServerContracts.cs
│       │   └── ToolContracts.cs
│       ├── Domain/
│       │   ├── HttpTransportSettings.cs
│       │   ├── McpConnectionState.cs
│       │   ├── McpServerDefinition.cs
│       │   ├── McpTransportKind.cs
│       │   ├── ServerRuntimeSnapshot.cs
│       │   ├── StdioTransportSettings.cs
│       │   ├── ToolCatalogEntry.cs
│       │   └── ToolInvocationOutcome.cs
│       ├── Mcp/
│       │   ├── IMcpClientSession.cs
│       │   ├── IMcpClientSessionFactory.cs
│       │   ├── McpClientSession.cs
│       │   ├── McpClientSessionFactory.cs
│       │   ├── McpConnectionManager.cs
│       │   ├── McpServerRuntime.cs
│       │   ├── McpSdkErrorNormalizer.cs
│       │   ├── ToolCatalogMapper.cs
│       │   └── ToolResultMapper.cs
│       ├── Options/
│       │   ├── McpDefaultsOptions.cs
│       │   ├── RegistryOptions.cs
│       │   ├── SecurityOptions.cs
│       │   └── WorkbenchOptions.cs
│       ├── Persistence/
│       │   ├── AtomicFileWriter.cs
│       │   ├── IServerDefinitionStore.cs
│       │   ├── JsonServerDefinitionStore.cs
│       │   ├── RegistryDocument.cs
│       │   └── RegistryException.cs
│       ├── Security/
│       │   ├── ApiKeyMiddleware.cs
│       │   ├── IEnvironmentValueProvider.cs
│       │   ├── ProcessEnvironmentValueProvider.cs
│       │   ├── SecretReferenceException.cs
│       │   ├── SecretReferenceResolver.cs
│       │   └── SensitiveDataRedactor.cs
│       ├── Serialization/
│       │   ├── AppJsonSerializerContext.cs
│       │   └── JsonSerializerDefaultsFactory.cs
│       ├── Validation/
│       │   ├── ServerDefinitionValidator.cs
│       │   ├── ToolArgumentsValidator.cs
│       │   ├── ValidationError.cs
│       │   └── ValidationResult.cs
│       ├── wwwroot/
│       │   ├── assets/
│       │   │   └── icons.svg
│       │   ├── components/
│       │   │   ├── confirm-dialog.js
│       │   │   ├── json-editor.js
│       │   │   ├── result-viewer.js
│       │   │   ├── schema-form.js
│       │   │   ├── server-card.js
│       │   │   └── status-badge.js
│       │   ├── pages/
│       │   │   ├── dashboard-page.js
│       │   │   ├── server-details-page.js
│       │   │   ├── server-edit-page.js
│       │   │   └── tool-runner-page.js
│       │   ├── api-client.js
│       │   ├── app.css
│       │   ├── app.js
│       │   ├── dom.js
│       │   ├── index.html
│       │   ├── router.js
│       │   └── state.js
│       ├── appsettings.Development.json
│       ├── appsettings.json
│       ├── McpWorkbench.csproj
│       └── Program.cs
├── tests/
│   ├── McpWorkbench.IntegrationTests/
│   │   ├── Api/
│   │   │   ├── ConnectionEndpointTests.cs
│   │   │   ├── ServerEndpointTests.cs
│   │   │   ├── SystemEndpointTests.cs
│   │   │   └── ToolEndpointTests.cs
│   │   ├── Infrastructure/
│   │   │   ├── CapturingLogProvider.cs
│   │   │   ├── IntegrationTestApplication.cs
│   │   │   ├── TestDataDirectory.cs
│   │   │   └── TestServerProcess.cs
│   │   ├── Security/
│   │   │   └── SecretLeakageTests.cs
│   │   ├── StaticUiTests.cs
│   │   └── McpWorkbench.IntegrationTests.csproj
│   ├── McpWorkbench.TestServer/
│   │   ├── TestTools.cs
│   │   ├── McpWorkbench.TestServer.csproj
│   │   └── Program.cs
│   └── McpWorkbench.UnitTests/
│       ├── Mcp/
│       │   ├── McpConnectionManagerTests.cs
│       │   ├── ToolCatalogMapperTests.cs
│       │   └── ToolResultMapperTests.cs
│       ├── Persistence/
│       │   ├── AtomicFileWriterTests.cs
│       │   └── JsonServerDefinitionStoreTests.cs
│       ├── Security/
│       │   ├── SecretReferenceResolverTests.cs
│       │   └── SensitiveDataRedactorTests.cs
│       ├── Validation/
│       │   ├── ServerDefinitionValidatorTests.cs
│       │   └── ToolArgumentsValidatorTests.cs
│       └── McpWorkbench.UnitTests.csproj
├── .dockerignore
├── .editorconfig
├── .gitignore
├── AGENTS.md
├── CHANGELOG.md
├── Directory.Build.props
├── Directory.Packages.props
├── Dockerfile
├── McpWorkbench.slnx
├── PLAN.md
├── README.md
├── compose.yaml
└── global.json
```

## Root files

### `PLAN.md`

Authoritative scope, phases, and definition of done. Codex updates status/checklists, not requirements, unless the user changes scope.

### `AGENTS.md`

Implementation behavior, security rules, AOT constraints, and reporting requirements for coding agents.

### `Directory.Build.props`

Common compiler and build settings:

- nullable and implicit usings enabled;
- latest non-preview major C# language version;
- warnings as errors for project-owned builds;
- deterministic builds;
- analyzers enabled.

### `Directory.Packages.props`

Central package versions. Production should have only the MCP SDK package unless a framework package is genuinely necessary. Test packages remain test-only.

### `global.json`

Pins the .NET 10 SDK baseline and allows roll-forward within .NET 10 feature bands.

### `McpWorkbench.slnx`

Contains the production project, unit tests, integration tests, and deterministic test server.

## Production project responsibilities

### `Program.cs`

Composition root only:

- create slim builder;
- bind and validate options;
- register generated JSON metadata;
- register persistence, security, MCP, health, and shutdown behavior;
- build app;
- add middleware;
- map static files and APIs;
- run.

No business logic belongs here.

### `Api/*`

Minimal endpoint mapping and transport-level concerns. Endpoints translate contracts into store/manager calls and map typed failures into API errors. They do not perform file I/O or SDK operations directly.

### `Contracts/*`

Application-owned HTTP request and response records. Persisted domain objects are not reused as write contracts.

### `Domain/*`

Stable persisted and runtime data shapes. No ASP.NET Core or MCP SDK dependency.

### `Mcp/*`

The only production folder allowed to depend directly on MCP SDK client/protocol types.

### `Persistence/*`

Versioned JSON registry and atomic file operations. It does not know about live sessions.

### `Security/*`

Environment lookup, secret substitution, redaction, and optional API-key middleware.

### `Serialization/*`

Source-generated JSON context and shared explicit serializer options. No reflection fallback.

### `Validation/*`

Dependency-free validation with stable codes. Avoid reflection-heavy validation frameworks.

### `wwwroot/*`

Framework-free static UI. Components return DOM nodes and never concatenate untrusted HTML.

## Test project responsibilities

### Unit tests

Pure or mostly pure tests for validation, persistence, security, state transitions, and mapping.

### Integration tests

Start the ASP.NET Core app in-process and the deterministic stdio MCP test server as a child process.

### Test MCP server

A minimal server exposing deterministic tools without public internet dependencies.

## Files intentionally absent

Version 0.1.0 must not contain:

- `package.json`, `package-lock.json`, or `node_modules`;
- EF Core migrations;
- Kubernetes or Helm files;
- database schemas;
- generated client SDKs;
- controllers, Razor, or Blazor files;
- plugin folders;
- an authentication database.
