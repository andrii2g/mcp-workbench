# MCP Workbench — Codex Implementation Plan

## 1. Document purpose

This file is the authoritative implementation plan for **MCP Workbench**, a lightweight self-hosted service for registering Model Context Protocol (MCP) servers, inspecting their advertised tools, and manually invoking those tools.

Codex must treat this document, `AGENTS.md`, and the detailed documents under `docs/` as the implementation contract. When documents appear to disagree, use this priority order:

1. `AGENTS.md` for implementation behavior and hard constraints.
2. `PLAN.md` for scope, sequencing, and acceptance criteria.
3. Topic-specific files under `docs/` for detailed design.
4. `README.md` for user-facing descriptions.

All repository text files must be UTF-8 without a byte-order mark (BOM).

## 2. Product summary

MCP Workbench is a Postman-like developer utility for MCP servers. It runs as a small ASP.NET Core service with a static browser UI and supports two MCP client transports:

- local process communication through `stdio`;
- remote MCP communication through Streamable HTTP, with legacy SSE compatibility delegated to the official MCP C# SDK where supported.

The application is an MCP client and management UI. It is not an MCP server, an LLM client, a model host, an autonomous agent, or a general-purpose reverse proxy.

## 3. Repository identity

- Repository name: `mcp-workbench`
- Main namespace: `McpWorkbench`
- Main executable name: `mcp-workbench`
- Main project: `src/McpWorkbench/McpWorkbench.csproj`
- Target framework: `net10.0`
- Deployment model: .NET 10 Native AOT
- Default URL: `http://127.0.0.1:5070`
- Persistence file: `data/servers.json`

## 4. Fixed technology choices

These choices are mandatory for the initial implementation:

- .NET 10 and C# with nullable reference types enabled.
- ASP.NET Core Minimal APIs using `WebApplication.CreateSlimBuilder(args)`.
- Native AOT enabled with `<PublishAot>true</PublishAot>`.
- Official `ModelContextProtocol.Core` stable package line, pinned to `1.4.1` in `Directory.Packages.props`.
- Do not use the `2.0.0-preview.*` MCP SDK line in the initial release.
- Static HTML, CSS, and browser-native JavaScript modules under `wwwroot`.
- No Node.js, npm, bundler, TypeScript compiler, SPA framework, or CSS framework.
- JSON-file persistence with atomic replacement.
- `System.Text.Json` source generation for application-owned DTOs.
- Dynamic MCP payloads represented as `JsonElement`, `JsonDocument`, `JsonNode`, or explicit SDK types.
- xUnit for automated tests.
- Docker support with a multi-stage Native AOT image.

## 5. Goals

The first production-ready release must let a user:

1. Open a local dashboard in a browser.
2. Add a named MCP server definition.
3. Configure either a local stdio process or a remote HTTP endpoint.
4. Edit and remove existing definitions.
5. Connect and disconnect a server explicitly.
6. Ping a connected server.
7. Inspect server identity, negotiated protocol information, capabilities, and runtime status when the SDK exposes them.
8. Discover and refresh the server's tool catalog.
9. Inspect a tool's name, title, description, annotations, input schema, and output schema when supplied.
10. Enter tool arguments using raw JSON.
11. Use a basic generated form for supported top-level JSON Schema properties.
12. Invoke a tool with cancellation and timeout enforcement.
13. Distinguish transport/protocol errors from MCP tool results that carry `isError = true`.
14. View known result content blocks and raw JSON.
15. Persist server definitions while keeping live client sessions in memory only.
16. Resolve environment-variable secret references at connection time without persisting resolved values.
17. Publish and run as a Native AOT executable on Linux and Windows.

## 6. Explicit non-goals

Codex must not add the following unless the scope is changed in a later request:

- LLM, OpenAI, Anthropic, Gemini, or local-model integration.
- Chat or agent orchestration.
- Hosting or proxying MCP servers through MCP Workbench.
- MCP prompts, resources, roots, sampling, elicitation, tasks, or MCP Apps support.
- OAuth authorization flows.
- Automatic package installation or MCP server discovery from npm, NuGet, PyPI, Docker Hub, or local IDE configuration.
- Multi-user accounts, tenancy, RBAC, or identity-provider integration.
- SQL, SQLite, Redis, or external database persistence.
- Persistent tool-call history.
- Distributed deployment or multiple replicas writing one registry file.
- Plugin loading, reflection-based extension discovery, or dynamic assembly loading.
- Arbitrary shell command execution from a tool-call request.
- Blazor Server, Razor Pages, MVC controllers, SignalR, or a JavaScript framework.
- Full JSON Schema support. The generated form is intentionally limited; raw JSON is always authoritative.

## 7. Primary user journeys

### 7.1 Add a local stdio server

1. User selects **Add server**.
2. User chooses `stdio`.
3. User enters a display name, executable, individual argument values, optional working directory, optional environment variables, and timeout settings.
4. API validates the definition.
5. Definition is atomically persisted.
6. UI returns to the server details page.
7. User selects **Connect**.
8. The service starts the process without invoking a shell, creates an MCP client, performs the SDK connection lifecycle, and records runtime metadata.
9. User selects **Load tools** and sees the catalog.

### 7.2 Add a remote HTTP server

1. User selects `http`.
2. User provides an absolute `http` or `https` MCP endpoint.
3. User optionally adds header values, preferably `${ENV:VARIABLE_NAME}` references.
4. The service persists the unresolved values.
5. On connection, the service resolves environment references and creates the SDK HTTP transport.
6. The service never returns or logs resolved secret values.

### 7.3 Execute a tool

1. User opens a connected server.
2. User selects a tool.
3. UI displays metadata and input schema.
4. User enters raw JSON or fills supported generated fields.
5. API validates that the top-level value is an object.
6. Runtime manager invokes the selected tool with a linked cancellation token and configured timeout.
7. UI displays duration, protocol outcome, tool `isError`, known content blocks, structured content, and raw result.

### 7.4 Remove a connected server

1. User confirms deletion.
2. Runtime manager cancels pending operations and disposes the MCP client.
3. Stdio child-process shutdown follows SDK behavior and configured shutdown timeout.
4. Definition is removed from the registry.
5. UI returns to the dashboard.

## 8. Functional requirements

### Server registry

- **FR-001** The system shall list all persisted server definitions.
- **FR-002** The system shall create a server with a generated GUID identifier.
- **FR-003** Server names shall be required, trimmed, and unique case-insensitively.
- **FR-004** The system shall update a server definition.
- **FR-005** Updating a connected server shall disconnect the existing runtime before replacing the persisted definition.
- **FR-006** The system shall remove a server definition.
- **FR-007** Removing a connected server shall dispose its runtime before removing persistence.
- **FR-008** Definitions shall be stored in a versioned JSON document.
- **FR-009** Writes shall use temp-file creation followed by same-directory atomic replacement where the operating system permits it.
- **FR-010** Corrupt registry JSON shall prevent unsafe overwrites and return a clear startup/read error.

### Transport definitions

- **FR-011** A stdio definition shall require an executable command.
- **FR-012** Stdio arguments shall be stored as an array, not as one shell-formatted string.
- **FR-013** The process shall be started without `cmd.exe`, `/bin/sh`, PowerShell, or shell argument concatenation.
- **FR-014** An HTTP definition shall require an absolute HTTP or HTTPS URI.
- **FR-015** HTTP header names and values shall be stored separately.
- **FR-016** The first release shall allow only one transport definition per server.

### Runtime lifecycle

- **FR-017** Runtime states shall include `Disconnected`, `Connecting`, `Connected`, `Disconnecting`, and `Faulted`.
- **FR-018** Connection and disconnection shall be serialized per server.
- **FR-019** A second connection request for an already connected server shall be idempotent.
- **FR-020** A disconnection request for an already disconnected server shall be idempotent.
- **FR-021** Connection failures shall dispose partially created SDK objects.
- **FR-022** The application shall dispose all live clients during host shutdown.
- **FR-023** Runtime state and cached tool metadata shall not be persisted.
- **FR-024** Tool invocation shall be serialized per server in the first release.
- **FR-025** Tool calls shall support client cancellation and an enforced server-side timeout.

### MCP operations

- **FR-026** The system shall expose a ping operation.
- **FR-027** The system shall list tools using the connected SDK client.
- **FR-028** Tool lists shall be cached in memory until explicit refresh, reconnect, definition update, or disconnect.
- **FR-029** The API shall reject tool invocation when the server is not connected.
- **FR-030** The API shall reject invocation of a tool not present in the most recently loaded catalog, unless the catalog is refreshed successfully first.
- **FR-031** Tool arguments shall be transmitted without converting arbitrary JSON values into reflection-driven CLR object graphs.
- **FR-032** Result mapping shall preserve unknown MCP content as raw JSON rather than discarding it.
- **FR-033** Protocol/transport exceptions and tool-level error results shall be represented separately.

### User interface

- **FR-034** The dashboard shall show all servers and current runtime status.
- **FR-035** The UI shall provide create, edit, connect, disconnect, ping, refresh tools, invoke, and delete actions.
- **FR-036** Destructive actions shall require confirmation.
- **FR-037** The tool runner shall provide raw JSON editing.
- **FR-038** The generated form shall support top-level object properties of types string, number, integer, boolean, enum, and simple arrays of primitive values.
- **FR-039** Unsupported schemas shall fall back to raw JSON without preventing invocation.
- **FR-040** The result viewer shall escape untrusted text and shall never inject MCP-provided HTML into the DOM.
- **FR-041** The interface shall remain functional without external CDN resources.

### Secrets and access

- **FR-042** `${ENV:NAME}` references shall be resolved only when creating a live transport.
- **FR-043** Missing referenced environment variables shall fail connection with the variable name but never with a secret value.
- **FR-044** Logs and API responses shall redact configured sensitive headers and environment values.
- **FR-045** The service shall listen on loopback by default.
- **FR-046** Optional API-key protection shall be enabled when a configured environment variable or configuration value is present.
- **FR-047** API requests shall use `X-Mcp-Workbench-Key`; static asset requests do not need the key.

## 9. Non-functional requirements

- **NFR-001 Native AOT:** `dotnet publish -c Release -r linux-x64` and `win-x64` with AOT enabled shall complete without unreviewed trimming or AOT warnings.
- **NFR-002 Startup:** The service shall answer `/health/live` without external dependencies.
- **NFR-003 Portability:** Paths shall use `Path` APIs and work on Linux and Windows.
- **NFR-004 Reliability:** Registry writes shall not truncate the current valid file if serialization or replacement fails.
- **NFR-005 Observability:** Structured logs shall identify server ID, operation, status, and duration without recording secrets or complete tool payloads.
- **NFR-006 Testability:** MCP SDK interaction shall be behind narrow interfaces that allow deterministic tests.
- **NFR-007 Maintainability:** Production code shall remain in one project with feature-oriented folders.
- **NFR-008 Security:** The application shall be documented as a trusted developer tool, not as an internet-facing SaaS product.
- **NFR-009 Accessibility:** Core UI workflows shall be keyboard usable and status shall not rely on color alone.
- **NFR-010 Determinism:** Build configuration and NuGet versions shall be centrally pinned.

## 10. Architecture summary

The application contains four logical areas within one production project:

1. **API and static UI** — Minimal API endpoints and `wwwroot` assets.
2. **Registry** — validation and JSON persistence of server definitions.
3. **MCP runtime** — SDK transport creation, connection lifecycle, tool discovery, and invocation.
4. **Security and support** — environment reference resolution, API-key middleware, redaction, logging, health checks, and source-generated JSON metadata.

Live sessions are keyed by server GUID in a `ConcurrentDictionary<Guid, McpServerRuntime>`. Each runtime contains a lifecycle semaphore and an invocation semaphore. Persistence operations are serialized through a separate store semaphore.

See `docs/ARCHITECTURE.md` and `docs/REPOSITORY-STRUCTURE.md`.

## 11. API summary

All management endpoints are versioned under `/api/v1`:

```text
GET    /api/v1/servers
POST   /api/v1/servers
GET    /api/v1/servers/{serverId}
PUT    /api/v1/servers/{serverId}
DELETE /api/v1/servers/{serverId}

POST   /api/v1/servers/{serverId}/connect
POST   /api/v1/servers/{serverId}/disconnect
POST   /api/v1/servers/{serverId}/ping
GET    /api/v1/servers/{serverId}/runtime

GET    /api/v1/servers/{serverId}/tools
POST   /api/v1/servers/{serverId}/tools/refresh
GET    /api/v1/servers/{serverId}/tools/{toolName}
POST   /api/v1/servers/{serverId}/tools/{toolName}/invoke

GET    /health/live
GET    /health/ready
```

Detailed contracts are in `docs/API.md`.

## 12. Persistence summary

The registry document is versioned:

```json
{
  "schemaVersion": 1,
  "servers": []
}
```

It never stores resolved secrets, client sessions, child-process IDs, tool catalogs, invocation arguments, or results.

## 13. Native AOT rules

Codex must:

- start with `WebApplication.CreateSlimBuilder(args)`;
- enable `<PublishAot>true</PublishAot>`;
- treat AOT and trim warnings as defects;
- register all application-owned JSON models with `AppJsonSerializerContext`;
- avoid reflection serialization, `dynamic`, runtime scanning, and `Activator.CreateInstance`;
- use explicit DTOs rather than anonymous responses;
- smoke-test the published native executable.

See `docs/NATIVE-AOT.md`.

## 14. Implementation sequence

A phase is complete only when its acceptance criteria pass.

### Phase 0 — Repository bootstrap

Create:

- `global.json`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- `.editorconfig`;
- `.gitignore`;
- `McpWorkbench.slnx`;
- production and test projects;
- minimal `Program.cs`;
- initial generated JSON context;
- `/health/live`.

Acceptance:

- restore/build/tests pass;
- production project uses `CreateSlimBuilder` and `PublishAot`;
- no project warnings;
- initial Linux AOT publish is attempted and warnings are resolved or documented as a blocker.

### Phase 1 — Options, domain model, and validation

Create persisted definition models, transport settings, runtime snapshots, API contracts, options, and validators.

Acceptance:

- valid and invalid stdio/HTTP definitions are unit-tested;
- no polymorphic persisted inheritance;
- exactly one transport configuration is permitted;
- DTO serialization uses generated metadata.

### Phase 2 — JSON registry persistence

Implement registry initialization, schema validation, atomic save, CRUD, duplicate-name enforcement, and corrupt-file protection.

Acceptance:

- unit tests cover absent file, CRUD, duplicate names, malformed JSON, unsupported version, concurrent writes, and failed replacement;
- current valid data survives failed writes;
- output is UTF-8 without BOM.

### Phase 3 — Secret resolution and redaction

Implement `${ENV:NAME}` substitution, sensitive-header policy, and error/log sanitization.

Acceptance:

- missing variables produce typed errors;
- resolved values are never persisted;
- sentinel secrets do not appear in captured logs or API errors.

### Phase 4 — MCP SDK adapter and transports

Implement the application-owned SDK boundary, stdio/HTTP transport construction, client creation, disposal, metadata mapping, and SDK error normalization.

Acceptance:

- SDK-specific types do not leak into contracts/persistence;
- deterministic test server proves real stdio connection;
- no shell is involved;
- Native AOT publish still passes.

### Phase 5 — Runtime connection manager

Implement runtime dictionary, state machine, connection/disconnection/ping, shutdown, per-server lifecycle lock, and per-server invocation lock.

Acceptance:

- tests cover duplicate concurrent connect/disconnect, failures, cancellation, update/delete while connected, and host shutdown;
- partially created resources are always disposed.

### Phase 6 — Tool catalog and invocation

Implement list/refresh/cache, tool mapping, argument validation, timeout/cancellation, known result blocks, unknown raw fallback, and distinct tool-error semantics.

Acceptance:

- deterministic server exposes `echo`, `add`, `fail`, `delay`, `structured`, and `large-text`;
- integration tests cover success, tool error, protocol error, timeout, cancellation, and size limits;
- full payloads are not logged.

### Phase 7 — Minimal API endpoints

Map all endpoints in `docs/API.md`, with explicit DTOs and stable errors.

Acceptance:

- integration tests exercise every endpoint and important failure status;
- no anonymous JSON or SDK types in responses;
- request cancellation propagates;
- AOT publish passes.

### Phase 8 — Static web UI

Implement dashboard, create/edit, details, catalog, runner, JSON editor, limited schema form, result viewer, confirmation, and notifications using vanilla JS.

Acceptance:

- complete add/connect/list/invoke/disconnect/remove workflow works from the native executable;
- no CDN requests;
- keyboard/label basics work;
- malicious HTML result is displayed as text.

### Phase 9 — Security and operations

Implement optional API-key middleware, CSP/security headers, request/result limits, safe errors, readiness, structured logging, and remote-bind warning.

Acceptance:

- security tests pass;
- no sentinel secret appears in logs;
- oversized requests fail predictably;
- remote binding without API key logs a clear warning.

### Phase 10 — Native AOT, containers, and scripts

Add cross-platform scripts, Linux/Windows AOT publish, native smoke tests, Dockerfile, compose file, non-root runtime, mounted data, and health check.

Acceptance:

- linux-x64 and win-x64 AOT publish without unreviewed warnings;
- native smoke workflow passes;
- container persists definitions and passes health.

### Phase 11 — Documentation and release readiness

Finalize README, samples, `.http` file, troubleshooting, CI, release workflow, changelog, and UTF-8 validation.

Acceptance:

- clean-clone commands work;
- examples contain no real secrets;
- all definition-of-done items pass.

## 15. Deterministic test server

`tests/McpWorkbench.TestServer` shall expose:

- `echo`: return supplied text;
- `add`: return numeric sum;
- `fail`: return `isError = true`;
- `delay`: bounded delay observing cancellation;
- `structured`: return nested structured JSON when supported;
- `large-text`: bounded large result.

It must require no network and emit protocol traffic only where the SDK requires it.

## 16. Error codes

Use stable codes:

```text
server_not_found
server_name_conflict
server_definition_invalid
registry_unavailable
registry_corrupt
unsupported_registry_version
server_already_connecting
server_not_connected
connection_failed
connection_timeout
disconnection_failed
ping_failed
ping_timeout
tool_catalog_unavailable
tool_not_found
tool_arguments_invalid
tool_call_timeout
tool_call_cancelled
tool_protocol_error
secret_reference_missing
request_too_large
result_too_large
unauthorized
internal_error
```

Every error response includes a safe message, trace ID, and optional field errors. It never includes a stack trace or resolved secret.

## 17. Logging rules

Allowed:

- operation;
- server ID;
- sanitized server name;
- transport kind;
- tool name;
- elapsed milliseconds;
- runtime state;
- normalized error code;
- trace ID.

Prohibited by default:

- authorization values;
- resolved environment values;
- full definitions;
- tool arguments;
- tool result bodies;
- raw protocol frames;
- child-process stdout.

## 18. Definition of done

Version `0.1.0` is complete only when:

- FR-001 through FR-047 are implemented or explicitly SDK-limited with tests;
- NFR-001 through NFR-010 are met;
- Release build and all tests pass;
- Linux and Windows Native AOT publish pass;
- native and Docker smoke tests pass;
- static UI completes the primary workflow;
- corrupt persistence is not overwritten;
- secret-leakage tests pass;
- README commands are verified;
- every text file is UTF-8 without BOM.

## 19. Codex phase report

After each phase, Codex reports:

1. phase completed;
2. files changed;
3. key decisions;
4. commands run;
5. test/build/AOT results;
6. warnings or blockers;
7. next phase.

Codex must not claim completion when required tests were skipped or failed.
