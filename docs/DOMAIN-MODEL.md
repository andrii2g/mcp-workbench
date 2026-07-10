# Domain Model

The domain model separates persisted server definitions, transient MCP runtime state,
API contracts, and SDK transport objects.

## Aggregate: MCP server registration

`McpServerDefinition` is the persisted aggregate root.

```text
McpServerDefinition
├── Id: Guid
├── Name: string
├── Description: string?
├── Enabled: bool
├── Transport: McpTransportKind
├── Stdio: StdioServerOptions?
├── Http: HttpServerOptions?
├── OperationTimeoutSeconds: int
├── CreatedAtUtc: DateTimeOffset
└── UpdatedAtUtc: DateTimeOffset
```

### Transport discriminant

```csharp
internal enum McpTransportKind
{
    Stdio,
    Http
}
```

String serialization uses explicit lower camel-case values through source-generated
serialization configuration. Do not rely on reflection-based enum converters.

### Stdio options

```text
StdioServerOptions
├── Command: string
├── Arguments: IReadOnlyList<string>
├── WorkingDirectory: string?
├── Environment: IReadOnlyDictionary<string, string>
└── ShutdownTimeoutSeconds: int
```

### HTTP options

```text
HttpServerOptions
├── Endpoint: Uri
├── Mode: McpHttpMode
└── Headers: IReadOnlyDictionary<string, string>
```

The persisted model may represent the endpoint as a string DTO and convert it to `Uri`
inside validated domain construction. This is preferable when source generation or
error reporting is clearer.

## Runtime state

`McpServerRuntime` is never serialized directly and never persisted.

```text
McpServerRuntime
├── ServerId: Guid
├── Status: McpRuntimeStatus
├── Client: IMcpClientSession?
├── ConnectedAtUtc: DateTimeOffset?
├── LastOperationAtUtc: DateTimeOffset?
├── LastError: SafeRuntimeError?
├── ServerIdentity: McpRemoteIdentity?
├── Capabilities: McpCapabilitySnapshot?
├── ToolCatalog: ToolCatalogSnapshot?
├── ExecutionHistory: BoundedExecutionHistory
├── LifetimeCts: CancellationTokenSource
├── LifecycleGate: SemaphoreSlim(1,1)
└── InvocationGate: SemaphoreSlim(1,1)
```

The runtime container owns the client and transport disposal lifecycle.

### State machine

Valid transitions:

```text
Disconnected -> Connecting
Connecting   -> Connected
Connecting   -> Faulted
Connected    -> Disconnecting
Connected    -> Faulted
Disconnecting-> Disconnected
Disconnecting-> Faulted
Faulted      -> Connecting
Faulted      -> Disconnecting
Faulted      -> Disconnected
```

Invalid transitions return a domain result rather than throwing for normal control flow.

`forceReconnect` performs:

```text
Connected -> Disconnecting -> Disconnected -> Connecting -> Connected|Faulted
```

## SDK isolation

Application services depend on:

```csharp
internal interface IMcpClientSession : IAsyncDisposable
{
    ValueTask<McpSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken);
    ValueTask PingAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(
        CancellationToken cancellationToken);
    ValueTask<McpInvocationResult> InvokeToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
```

The concrete SDK adapter lives in `Mcp/`. No endpoint, persistence class, or UI contract
uses SDK protocol types.

Benefits:

- SDK upgrades are localized;
- deterministic fake sessions are easy to test;
- app-owned DTOs can be source-generated;
- result size and redaction policies are enforced at the boundary.

## Tool descriptor

```text
McpToolDescriptor
├── Name: string
├── Title: string?
├── Description: string?
├── InputSchema: JsonElement
├── OutputSchema: JsonElement?
└── Annotations: McpToolAnnotations
```

`JsonElement` values must be cloned when their owning `JsonDocument` is disposed.

Tool names are unique within one catalog using ordinal comparison. If a remote server
returns duplicates, catalog refresh fails with a safe protocol error.

## Tool invocation result

```text
McpInvocationResult
├── IsError: bool
├── Content: IReadOnlyList<McpContentBlock>
├── StructuredContent: JsonElement?
├── RawResult: JsonElement
└── WasTruncated: bool
```

Known content blocks:

```text
TextContentBlock
ImageContentBlock
EmbeddedResourceContentBlock
ResourceLinkContentBlock
UnknownContentBlock
```

Unknown content is retained as bounded raw JSON rather than discarded.

Do not model remote content using unbounded object graphs. Enforce byte limits during
mapping and response construction.

## Execution history

History is an in-memory diagnostic aid, not an audit log.

```text
ToolExecutionRecord
├── Id: Guid
├── ServerId: Guid
├── ToolName: string
├── StartedAtUtc: DateTimeOffset
├── CompletedAtUtc: DateTimeOffset
├── DurationMilliseconds: long
├── Outcome: ToolExecutionOutcome
├── IsError: bool?
└── SafeErrorCode: string?
```

Tool arguments and full results are excluded from history by default.

A bounded ring buffer replaces the oldest entry when full. No history survives restart.

## Domain services

### `IServerRegistry`

Responsibilities:

- load immutable snapshot;
- get/list definitions;
- create, replace, and delete with atomic persistence;
- enforce revision and uniqueness;
- expose registry health.

### `IServerDefinitionValidator`

Pure validation for common and transport-specific rules.

### `ISecretReferenceResolver`

Resolves approved reference locations into ephemeral strings and returns a collection of
values that must be redacted.

### `IMcpConnectionManager`

Coordinates runtime lookup, lifecycle state, client ownership, catalog refresh, and
invocation serialization.

### `IMcpClientFactory`

Builds an SDK-backed session from a validated and resolved definition.

### `IToolResultMapper`

Maps SDK protocol results to bounded application content blocks and raw JSON.

## Result pattern

Expected validation, not-found, conflict, transport, timeout, and protocol failures use a
typed result:

```csharp
internal readonly record struct Result<T>(
    bool IsSuccess,
    T? Value,
    AppError? Error);
```

Do not throw exceptions to represent user input validation or normal lifecycle conflicts.
Exceptions are caught once at the appropriate boundary, translated to `AppError`, logged
safely, and never exposed verbatim.

## Time and identifiers

Inject `TimeProvider` for timestamps and timeout tests.
Generate persisted identifiers with `Guid.NewGuid()` unless the project introduces a
testable `IIdGenerator`. Request IDs may use framework trace identifiers; no extra
dependency is required.

All stored and returned times are UTC ISO 8601 values.

## Immutability

Persisted definitions and catalog snapshots should be immutable records or classes with
init-only members. Mutable synchronization primitives and SDK sessions stay only inside
runtime holders.

Never expose mutable dictionary/list instances from the registry.
