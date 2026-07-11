# HTTP API Contract

This document defines the HTTP interface implemented by MCP Workbench.

The API is versioned under `/api/v1`. Breaking changes require a new major path.
All request and response bodies use `application/json; charset=utf-8`.

## Common behavior

### Request identifiers

The service accepts an optional `X-Request-Id` header. If absent, it creates one.
Every response includes `X-Request-Id`.

### Optional API-key authentication

When `Security:ApiKey` is configured, all `/api/v1/*` endpoints require:

```http
X-Mcp-Workbench-Key: <configured-secret>
```

Health endpoints and static UI assets remain accessible unless explicitly configured
otherwise. The API key is compared in constant time and is never logged.

### Success envelope

```json
{
  "data": {},
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:00:00Z"
  }
}
```

### Error envelope

```json
{
  "error": {
    "code": "server_not_found",
    "message": "MCP server '...' was not found.",
    "details": null
  },
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:00:00Z"
  }
}
```

`details` is optional and must not contain secrets, environment-variable values,
authorization headers, stack traces, process environment blocks, or raw exception text
that could expose sensitive data.

### Standard status mapping

| HTTP | Meaning |
|---:|---|
| 200 | Read/update succeeded, or an MCP tool returned a normal protocol response |
| 201 | Server definition created |
| 204 | Delete or disconnect completed with no response body |
| 400 | Invalid request or invalid server definition |
| 401 | API key missing or invalid |
| 404 | Server or tool not found |
| 405 | HTTP method is not supported by the API resource |
| 409 | Invalid lifecycle transition or duplicate normalized name |
| 413 | Request payload exceeds the configured limit |
| 415 | Request content type is unsupported |
| 422 | Tool arguments rejected before or by MCP invocation |
| 429 | Local invocation concurrency limit reached, when configured |
| 502 | MCP transport/protocol failure |
| 503 | Registry or required local service unavailable |
| 504 | MCP connect, ping, discovery, or call timeout |
| 500 | Unexpected application failure |

A tool result containing MCP `isError: true` is still a successfully transported MCP
response and therefore returns HTTP 200. The body reports `isError: true`.

## Data types

### Transport type

```text
stdio
http
```

### HTTP transport mode

```text
auto
streamableHttp
legacySse
```

### Runtime status

```text
disconnected
connecting
connected
disconnecting
faulted
```

## Server endpoints

### List server definitions

```http
GET /api/v1/servers
```

Query parameters:

| Name | Type | Default | Notes |
|---|---|---:|---|
| `includeRuntime` | boolean | true | Includes current in-memory status |
| `search` | string | empty | Case-insensitive name search |

Response:

```json
{
  "data": [
    {
      "id": "c5af558c-ef13-45f8-8cf5-a7d320292f72",
      "name": "Demo stdio server",
      "description": "Local deterministic MCP server",
      "transport": "stdio",
      "enabled": true,
      "createdAtUtc": "2026-07-11T00:00:00Z",
      "updatedAtUtc": "2026-07-11T00:00:00Z",
      "runtime": {
        "status": "connected",
        "connectedAtUtc": "2026-07-11T00:01:00Z",
        "lastOperationAtUtc": "2026-07-11T00:02:00Z",
        "lastError": null,
        "protocolVersion": "2025-11-25",
        "serverName": "McpWorkbench.TestServer",
        "serverVersion": "1.0.0",
        "toolCount": 6
      }
    }
  ],
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:00:00Z"
  }
}
```

Secrets and resolved environment references are never returned.

### Create a server definition

```http
POST /api/v1/servers
```

Stdio request:

```json
{
  "name": "Local test server",
  "description": "A local MCP process",
  "enabled": true,
  "transport": "stdio",
  "stdio": {
    "command": "dotnet",
    "arguments": [
      "./tools/McpWorkbench.TestServer.dll"
    ],
    "workingDirectory": null,
    "environment": {
      "DEMO_TOKEN": "${ENV:DEMO_TOKEN}"
    },
    "shutdownTimeoutSeconds": 5
  },
  "http": null,
  "operationTimeoutSeconds": 30
}
```

HTTP request:

```json
{
  "name": "Remote MCP",
  "description": null,
  "enabled": true,
  "transport": "http",
  "stdio": null,
  "http": {
    "endpoint": "https://mcp.example.test/mcp",
    "mode": "auto",
    "headers": {},
    "authorization": {
      "kind": "bearer",
      "credential": "${ENV:REMOTE_MCP_TOKEN}"
    }
  },
  "operationTimeoutSeconds": 30
}
```

Validation rules are specified in [CONFIGURATION.md](CONFIGURATION.md).

Response: HTTP 201 with the stored definition and a `Location` header.

### Read one server definition

```http
GET /api/v1/servers/{serverId}
```

Returns stored definition, redacted configuration, and current runtime snapshot.

### Update a server definition

```http
PUT /api/v1/servers/{serverId}
```

The body has the same editable fields as creation.

Rules:

1. `id`, `createdAtUtc`, and runtime fields are immutable.
2. A connected server is disconnected before the new definition replaces the old one.
3. Validation happens before disconnecting.
4. Persistence must succeed before the endpoint reports success.
5. Failure to disconnect leaves the old definition unchanged and returns 409 or 502.

### Delete a server definition

```http
DELETE /api/v1/servers/{serverId}
```

Rules:

1. Cancel outstanding local operations for this server.
2. Dispose the MCP client and transport.
3. Terminate a managed stdio process using the configured graceful shutdown timeout.
4. Remove the definition atomically from the registry.
5. Return HTTP 204.

Deleting an unknown identifier returns 404. Deleting an already disconnected known server
is valid.

## Lifecycle endpoints

### Connect

```http
POST /api/v1/servers/{serverId}/connect
```

Optional body:

```json
{
  "forceReconnect": false
}
```

Behavior:

- Resolves environment references.
- Creates the selected transport.
- Performs MCP initialization.
- Executes an MCP ping.
- Stores negotiated server metadata and capabilities.
- Optionally loads the initial tool catalog.

Response:

```json
{
  "data": {
    "status": "connected",
    "connectedAtUtc": "2026-07-11T00:01:00Z",
    "connectDurationMilliseconds": 83,
    "protocolVersion": "2025-11-25",
    "server": {
      "name": "Example",
      "version": "1.0.0"
    },
    "capabilities": {
      "tools": true,
      "toolsListChanged": false
    }
  },
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:01:00Z"
  }
}
```

Calling connect for an already connected server is idempotent unless
`forceReconnect` is true.

### Disconnect

```http
POST /api/v1/servers/{serverId}/disconnect
```

Return HTTP 204. Calling disconnect for a disconnected server is idempotent.

### Ping

```http
POST /api/v1/servers/{serverId}/ping
```

A disconnected server returns 409. It is not implicitly connected by this endpoint.

Response:

```json
{
  "data": {
    "success": true,
    "durationMilliseconds": 7,
    "timestampUtc": "2026-07-11T00:02:00Z"
  },
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:02:00Z"
  }
}
```

### Read runtime state

```http
GET /api/v1/servers/{serverId}/runtime
```

This endpoint never initiates a connection.

## Tool endpoints

### List tools

```http
GET /api/v1/servers/{serverId}/tools
```

Query:

| Name | Type | Default |
|---|---|---:|
| `refresh` | boolean | false |

The server must be connected. `refresh=true` executes a new MCP `tools/list` request;
otherwise the cached catalog is returned when present.

Response:

```json
{
  "data": [
    {
      "name": "add",
      "title": "Add numbers",
      "description": "Adds two numbers.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "a": { "type": "number" },
          "b": { "type": "number" }
        },
        "required": ["a", "b"],
        "additionalProperties": false
      },
      "outputSchema": null,
      "annotations": {
        "readOnlyHint": true,
        "destructiveHint": false,
        "idempotentHint": true,
        "openWorldHint": false
      }
    }
  ],
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:03:00Z"
  }
}
```

Unknown schema members must be retained in the JSON returned to the UI.

### Refresh tool catalog

```http
POST /api/v1/servers/{serverId}/tools/refresh
```

Equivalent to `GET .../tools?refresh=true`, but preferred for a UI command because it
clearly performs remote work.

### Read one tool

```http
GET /api/v1/servers/{serverId}/tools/{toolName}
```

`toolName` is URL encoded. Tool lookup is ordinal and case-sensitive, matching MCP names.

### Invoke a tool

```http
POST /api/v1/servers/{serverId}/tools/{toolName}/invoke
```

Request:

```json
{
  "arguments": {
    "a": 12,
    "b": 30
  },
  "timeoutSeconds": 10
}
```

`arguments` defaults to an empty object. Its values remain JSON values; the application
must not deserialize them into arbitrary CLR types.

Response:

```json
{
  "data": {
    "serverId": "c5af558c-ef13-45f8-8cf5-a7d320292f72",
    "toolName": "add",
    "startedAtUtc": "2026-07-11T00:04:00Z",
    "completedAtUtc": "2026-07-11T00:04:00.011Z",
    "durationMilliseconds": 11,
    "isError": false,
    "content": [
      {
        "type": "text",
        "text": "42"
      }
    ],
    "structuredContent": {
      "result": 42
    },
    "raw": {
      "content": [
        {
          "type": "text",
          "text": "42"
        }
      ],
      "structuredContent": {
        "result": 42
      },
      "isError": false
    }
  },
  "meta": {
    "requestId": "01J...",
    "timestampUtc": "2026-07-11T00:04:00Z"
  }
}
```

The API returns normalized known content fields and a bounded raw result. Binary image
data may be retained for direct preview only up to the configured response limit.

## Health endpoints

### Liveness

```http
GET /health/live
```

Returns 200 whenever the process is running and the request pipeline is responsive.

### Readiness

```http
GET /health/ready
```

Returns 200 when:

- configuration loaded;
- the registry file was parsed or initialized successfully;
- the registry path is writable when persistence is enabled.

Readiness does not depend on any registered MCP server being reachable.

## Limits

Defaults:

| Limit | Default |
|---|---:|
| HTTP request body | 1 MiB |
| Tool arguments JSON | 256 KiB |
| Tool result retained by API | 4 MiB |
| Tool execution timeout | 30 seconds |
| Connect timeout | 15 seconds |
| Ping timeout | 5 seconds |
| Stdio shutdown timeout | 5 seconds |
| Execution history | 50 entries per server, memory only |

Limits are configuration driven. A response exceeding the limit is rejected or truncated
according to the exact rule in [CONFIGURATION.md](CONFIGURATION.md); truncation is always
reported explicitly.

## Idempotency and concurrency

- GET endpoints are side-effect free except `GET tools?refresh=true`; the POST refresh
  command is preferred.
- Connect and disconnect are idempotent.
- Create is not idempotent; normalized duplicate names return 409.
- Update is a full replacement of editable fields.
- Calls are serialized per server in version 1.
- Different servers may connect and invoke concurrently.
- Registry writes are serialized globally.
