# Testing Strategy

Testing is organized around pure domain behavior, persistence/security boundaries,
MCP adapter integration, HTTP contracts, and published Native AOT binaries.

## Test projects

```text
tests/
├── McpWorkbench.Tests/
├── McpWorkbench.IntegrationTests/
└── McpWorkbench.TestServer/
```

### `McpWorkbench.Tests`

Fast unit tests with no network, no child process, and isolated temporary files only when
testing persistence.

### `McpWorkbench.IntegrationTests`

Runs the ASP.NET Core application, real registry I/O, fake MCP sessions, real stdio MCP
transport, and test HTTP MCP endpoints.

### `McpWorkbench.TestServer`

A deterministic MCP server used only by tests and local development. It writes protocol
traffic to stdout and diagnostics to stderr.

## Test framework baseline

Central package management pins stable test dependencies. Phase 0 must restore them and
may update only to stable compatible versions, recording the change.

Suggested packages:

```text
xunit.v3
Microsoft.NET.Test.Sdk
coverlet.collector
Microsoft.AspNetCore.Mvc.Testing
```

Do not add a mocking framework unless manual fakes become materially harder to maintain.

## Deterministic test MCP tools

The test server exposes:

### `echo`

Input:

```json
{
  "type": "object",
  "properties": {
    "text": { "type": "string" }
  },
  "required": ["text"],
  "additionalProperties": false
}
```

Returns one text content block and structured content `{ "text": ... }`.

### `add`

Input requires numeric `a` and `b`. Returns numeric sum in text and structured content.

### `fail`

Input contains optional `message`. Returns a normal MCP tool response with
`isError: true`; it does not break the transport.

### `delay`

Input contains `milliseconds`, bounded in the test server. Used to verify timeout,
request cancellation, disconnect cancellation, and invocation serialization.

### `structured`

Returns nested structured JSON plus at least one text block.

### `large-text`

Returns requested bounded text length to test result-size enforcement.

The test server should optionally expose deterministic startup modes through arguments:

```text
--fail-initialize
--exit-after-initialize
--duplicate-tools
--write-stderr <bytes>
```

## Unit-test matrix

### Validation

- accepts valid stdio definition;
- accepts valid HTTPS definition;
- accepts loopback HTTP;
- rejects missing/blank/oversized name;
- rejects duplicate normalized name;
- rejects mismatched transport option objects;
- rejects empty stdio command;
- rejects excessive argument/environment/header counts;
- rejects invalid timeout values;
- rejects URI user-info, fragments, and unsupported schemes;
- rejects prohibited headers;
- validates exact allowlists.

### Persistence

- missing file creates empty registry;
- valid file loads;
- malformed file fails without overwrite;
- unsupported schema version fails;
- create/update/delete increments revision;
- failed serialization/write leaves original intact;
- concurrent mutations serialize correctly;
- temporary file is on the same directory;
- stale temporary file is not promoted;
- no UTF-8 BOM is written;
- source-generated serialization round-trips all definition types.

### Secret references and redaction

- resolves one/multiple references;
- rejects invalid/unresolved variable;
- does not recurse;
- resolves only approved locations;
- redacts conventional key names;
- redacts exact resolved values;
- never redacts unrelated safe values accidentally beyond documented policy.

### Runtime state machine

- every valid transition;
- every invalid transition;
- idempotent connect/disconnect;
- forced reconnect order;
- connect failure disposes partial session;
- disconnect cancels active call;
- fault recovery;
- different servers operate concurrently;
- same-server invocations serialize.

### Result mapping

- maps text/image/resource/link;
- preserves unknown block;
- clones `JsonElement`;
- maps structured content;
- preserves `isError`;
- enforces depth/byte limits;
- handles malformed SDK result safely;
- does not include arguments/results in history.

### API mapping

- every domain error maps to intended status/code;
- `isError=true` remains HTTP 200;
- timeout maps to 504;
- request ID echoed/generated;
- errors contain no stack trace or secret.

## Integration-test matrix

### In-process API

Use a test host with:

- temporary registry;
- deterministic `TimeProvider`;
- fake `IMcpClientFactory`;
- API key on/off configurations.

Verify CRUD, lifecycle, tools, error envelopes, static UI, CSP/security headers, health,
and application shutdown.

### Real stdio transport

Build and launch `McpWorkbench.TestServer` via the actual SDK stdio client adapter.

Scenarios:

1. connect and ping;
2. list six tools;
3. invoke echo/add/structured;
4. receive `isError` from fail;
5. cancel delay;
6. timeout delay;
7. server exits unexpectedly;
8. reconnect;
9. capture bounded stderr;
10. disconnect terminates process;
11. application shutdown leaves no child process.

### HTTP MCP transport

Host a deterministic MCP server endpoint in the integration test process or a dedicated
test host using the official server-capable package only in the test project.

Verify:

- Streamable HTTP;
- auto mode;
- legacy SSE only when feasible with the pinned SDK;
- header injection;
- no redirect credential leak;
- timeout/cancellation;
- connection failure classification.

### Persistence restart

1. Start application with empty temp data directory.
2. Create definitions.
3. Stop application.
4. Start a new application instance.
5. Verify definitions persisted and runtime state did not.
6. Verify no resolved secret was written.

## Browser/UI testing

Initial scope avoids a Node.js test stack. Use:

- JavaScript modules kept small and deterministic;
- manual browser acceptance checklist;
- HTTP tests for static assets and security headers;
- optional minimal browser automation only if it can be added without turning Node.js into
  a runtime/build dependency.

Manual UI checklist:

- create/edit/delete stdio and HTTP definitions;
- errors shown next to fields;
- connect/disconnect/ping;
- tool catalog and schema display;
- raw JSON editor;
- basic generated form;
- invoke and render each known result type;
- malicious text appears literally;
- keyboard navigation and focus visibility;
- narrow viewport remains usable;
- page refresh preserves registered servers;
- no secrets shown in DOM/network responses.

## Coverage expectations

Coverage is a diagnostic, not the sole quality gate.

Targets:

- domain/validation/persistence/security utilities: at least 90% line coverage;
- runtime manager and adapter mapping: at least 85%;
- endpoint behavior: all routes and status branches exercised;
- overall solution: at least 80%.

Do not write meaningless assertions solely to increase percentages.

## Test naming

Use behavior-oriented names:

```text
ConnectAsync_WhenInitializationFails_DisposesPartialSession
InvokeAsync_WhenToolReturnsIsError_ReturnsSuccessfulProtocolResult
LoadAsync_WhenRegistryHasBom_ReadsOrRejectsAccordingToPolicy
```

Choose one consistent test class organization.

## Temporary resources

Each test owns a unique temporary directory and port.
Cleanup occurs in `finally`/async disposal.
Processes receive a bounded shutdown and forced cleanup fallback.
Tests must be parallel-safe unless explicitly placed in a nonparallel collection.

## Timeouts

Every integration wait is bounded. Avoid arbitrary long sleeps.
Use readiness polling and deterministic signals.

## CI commands

```bash
dotnet restore --force-evaluate
dotnet build -c Release --no-restore
dotnet test -c Release --no-build \
  --collect:"XPlat Code Coverage"
```

Native AOT smoke tests run after the normal test suite.

## Regression rule

Every defect fix includes:

1. a test that fails before the fix;
2. the smallest correct fix;
3. normal tests;
4. affected transport integration tests;
5. Native AOT smoke test when the fix touches serialization, DI, endpoints, SDK adapter,
   process/network behavior, or publishing.
