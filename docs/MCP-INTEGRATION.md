# MCP SDK Integration

## Package baseline

Use the official C# SDK and pin the stable package line selected in the root central
package file:

```xml
<PackageVersion Include="ModelContextProtocol.Core" Version="1.4.1" />
```

Do not use a preview SDK. During Phase 0, Codex must run restore and verify that the
selected stable `Core` package version is available and compatible with `net10.0`.
If the exact package is unavailable, stop and report the package-resolution evidence;
do not silently switch to preview packages or broaden the dependency to an unrelated
implementation.

The intent is to use the smallest official client/low-level package needed for:

- MCP client initialization;
- stdio client transport;
- HTTP client transport;
- ping;
- `tools/list`;
- `tools/call`;
- protocol DTOs internal to the adapter.

## Adapter boundary

All references to the official SDK are restricted to:

```text
src/McpWorkbench/Mcp/
```

Allowed exceptions:

- dependency registration in `Program.cs`;
- package reference in the project file;
- adapter-focused tests.

The rest of the application uses interfaces and app-owned records from `Application/`
and `Domain/`.

## Transport factory

```text
McpClientFactory
├── CreateStdioSessionAsync(...)
└── CreateHttpSessionAsync(...)
```

The factory receives:

- validated server definition;
- already resolved ephemeral secret values;
- `TimeProvider`;
- logger;
- cancellation token.

It returns `IMcpClientSession`, which owns all SDK client and transport resources.

## Stdio transport

Expected mapping:

```text
command           -> SDK stdio command
arguments[]       -> SDK argument collection
workingDirectory  -> SDK working directory
environment{}     -> SDK environment
shutdown timeout  -> adapter disposal policy
```

Rules:

1. Start no shell.
2. Preserve argument boundaries.
3. Treat stdout as MCP protocol traffic.
4. Allow child stderr to be captured only as bounded diagnostic text.
5. Do not write resolved environment values to logs.
6. Cancellation during connection must dispose the partially constructed transport.
7. Disposal first requests graceful termination through the SDK/transport.
8. After the configured timeout, force-kill the child process tree when the adapter owns
   the process and the SDK has not already done so.
9. Never adopt or terminate processes not created by this runtime.

Stdio server logging documentation should instruct test and sample servers to write
diagnostics to stderr.

## HTTP transport

Supported modes:

```text
auto
streamableHttp
legacySse
```

`auto` is the UI default and maps to SDK-supported automatic behavior where available.
If the SDK version requires explicit probing, keep that logic inside the adapter:

1. attempt Streamable HTTP;
2. fall back to legacy SSE only for a failure that indicates unsupported transport;
3. do not fall back for authentication, authorization, DNS, TLS, or generic 5xx errors;
4. retain a safe diagnostic stating which mode connected.

Use `IHttpClientFactory` only if its chosen configuration remains compatible with Native
AOT and the SDK adapter. Otherwise construct the SDK's documented HTTP transport using a
named, bounded-lifetime handler owned by the session.

HTTP security:

- HTTPS by default;
- loopback HTTP permitted for local development;
- exact optional host allowlist;
- redirects disabled or validated so headers cannot leak across hosts;
- no automatic cookie persistence;
- decompression may be enabled with response-size enforcement;
- TLS certificate validation is never disabled.

## Initialization and metadata

Connection sequence:

```text
resolve references
    -> create transport
    -> create MCP client/session
    -> initialize
    -> capture negotiated protocol and server identity
    -> ping
    -> optionally list tools
    -> publish Connected runtime state
```

The runtime must not become `Connected` before initialization and ping complete.

Capture only safe metadata:

- negotiated protocol version;
- server implementation name/version;
- capability flags;
- connection duration;
- selected transport mode.

Do not expose complete SDK objects or arbitrary server metadata without bounded mapping.

## Ping

`PingAsync` has a dedicated short timeout. A failed ping does not automatically
disconnect an otherwise connected session, but the runtime records the safe error and may
transition to `Faulted` when the underlying transport is definitively closed.

## Tool discovery

Use the SDK's tool-list operation. Map every returned tool to `McpToolDescriptor`.

Requirements:

- clone JSON schemas;
- preserve unknown schema keywords;
- preserve tool annotations;
- enforce maximum catalog count, name length, description length, and aggregate schema
  size;
- reject duplicate tool names;
- replace the catalog atomically only after the complete new list maps successfully;
- retain the previous catalog if refresh fails.

Version 1 does not subscribe to tool-list-changed notifications. A capability flag may be
shown, but the user refreshes manually.

## Invocation

Input boundary:

```text
toolName: exact ordinal string
arguments: one JSON object
timeout: bounded effective duration
```

Map JSON properties into the SDK call argument representation without converting to
runtime-defined object types.

Invocation sequence:

1. Verify connected state.
2. Verify tool exists in current catalog, refreshing only when explicitly requested.
3. Validate argument payload byte size.
4. Optionally perform limited client-side JSON Schema checks used by the form UI.
5. Acquire per-server invocation semaphore.
6. Link request-abort, server-lifetime, and timeout cancellation tokens.
7. Call the SDK tool method.
8. Map known/unknown content and structured content.
9. Enforce output size.
10. Record metadata-only history.
11. Release the semaphore.

Remote schema validation is authoritative. The local validator must not claim complete
JSON Schema support.

## MCP errors

Classify errors into application codes:

| Category | Application code |
|---|---|
| Initialization rejected | `mcp_initialization_failed` |
| Transport unavailable | `mcp_transport_failed` |
| Transport closed | `mcp_transport_closed` |
| Protocol malformed | `mcp_protocol_error` |
| MCP method not found | `mcp_method_not_found` |
| Tool unknown locally | `tool_not_found` |
| Tool arguments rejected | `tool_arguments_invalid` |
| Tool returned `isError` | normal HTTP response with `isError=true` |
| Timeout | operation-specific timeout code |
| User cancellation | `operation_cancelled` |

Never expose SDK exception `ToString()` in API responses.

## Result mapping

Map these content types when present in the pinned SDK:

- text;
- image;
- embedded resource;
- resource link.

For each, copy only documented fields into app-owned DTOs. Unsupported future content
types become `UnknownContentBlock` with bounded raw JSON.

Raw result retention exists for interoperability and debugging, but it must pass through:

1. secret-value redaction where technically applicable;
2. maximum depth check;
3. maximum UTF-8 byte count;
4. explicit `wasTruncated` reporting if the selected strategy allows truncation.

Prefer rejecting an oversized result with `mcp_result_too_large` over producing invalid
JSON. Known text fields may be safely shortened only when the response clearly identifies
that shortening.

## Disposal

`IMcpClientSession.DisposeAsync` is idempotent.

It must:

- stop accepting operations;
- cancel session lifetime;
- wait only a bounded period for active invocation;
- dispose client;
- dispose transport/HTTP resources;
- ensure owned stdio process exits;
- clear ephemeral secret-bearing values;
- avoid throwing during application shutdown unless required for diagnostics.

## SDK upgrade procedure

For each stable SDK upgrade:

1. create a dedicated branch;
2. update only the central package version;
3. compile with warnings as errors;
4. run all unit/integration tests;
5. run real stdio tests;
6. run HTTP transport tests;
7. publish Native AOT for Linux and Windows;
8. run the published-binary smoke test;
9. inspect trim/AOT warnings;
10. update `docs/REFERENCES.md` and `CHANGELOG.md`.

No upgrade is accepted solely because normal JIT tests pass.
