# Configuration and Persistence

MCP Workbench uses ASP.NET Core configuration. Settings may come from:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. environment variables;
4. command-line arguments.

Environment variables use double underscores, for example:

```text
McpWorkbench__RegistryPath=/data/servers.json
Security__ApiKey=${actual deployment secret}
```

Do not commit real secrets.

## Application configuration

Suggested `appsettings.json`:

```json
{
  "McpWorkbench": {
    "RegistryPath": "data/servers.json",
    "SecretVaultPath": "data/secrets.vault",
    "SecretKeyRingPath": "data/secret-keys",
    "BindToLoopbackOnly": true,
    "ConnectTimeoutSeconds": 15,
    "PingTimeoutSeconds": 5,
    "DefaultOperationTimeoutSeconds": 30,
    "MaximumOperationTimeoutSeconds": 300,
    "MaximumArgumentsBytes": 262144,
    "MaximumResultBytes": 4194304,
    "MaximumHistoryEntriesPerServer": 50,
    "LoadToolsOnConnect": true,
    "AllowStdioServers": true,
    "AllowHttpServers": true,
    "AllowedStdioCommands": [],
    "AllowedHttpHosts": []
  },
  "Security": {
    "ApiKey": null,
    "ProtectStaticUi": false,
    "TrustedProxyCount": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "McpWorkbench": "Information"
    }
  }
}
```

## Option semantics

| Option | Meaning |
|---|---|
| `RegistryPath` | JSON registry file. Parent directory is created on startup. |
| `SecretVaultPath` | Encrypted managed-secret file. |
| `SecretKeyRingPath` | Data Protection key ring; Windows protects keys with current-user DPAPI. |
| `BindToLoopbackOnly` | Enforces loopback URLs unless explicitly disabled. |
| `ConnectTimeoutSeconds` | Overall initialize/connect limit. |
| `PingTimeoutSeconds` | MCP ping limit. |
| `DefaultOperationTimeoutSeconds` | Default discovery and invocation timeout. |
| `MaximumOperationTimeoutSeconds` | Upper bound accepted from API requests. |
| `MaximumArgumentsBytes` | UTF-8 byte limit for tool arguments. |
| `MaximumResultBytes` | Maximum normalized/raw tool result returned by API. |
| `MaximumHistoryEntriesPerServer` | In-memory ring-buffer capacity. |
| `LoadToolsOnConnect` | Load the catalog after initialization and ping. |
| `AllowStdioServers` | Operational kill switch for process execution. |
| `AllowHttpServers` | Operational kill switch for network MCP servers. |
| `AllowedStdioCommands` | Optional exact command allowlist. Empty means unrestricted. |
| `AllowedHttpHosts` | Optional case-insensitive host allowlist. Empty means unrestricted. |

## Registry document

Persisted registry schema version 1:

```json
{
  "schemaVersion": 1,
  "revision": 7,
  "updatedAtUtc": "2026-07-11T00:00:00Z",
  "servers": [
    {
      "id": "c5af558c-ef13-45f8-8cf5-a7d320292f72",
      "name": "Local test server",
      "description": "Deterministic development server",
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
      "operationTimeoutSeconds": 30,
      "createdAtUtc": "2026-07-11T00:00:00Z",
      "updatedAtUtc": "2026-07-11T00:00:00Z"
    }
  ]
}
```

Runtime status, resolved secrets, tool catalogs, and execution history are not persisted.

### Persistence algorithm

Every mutation follows:

1. Acquire the global registry write semaphore.
2. Clone the current immutable registry snapshot.
3. Apply and validate the mutation.
4. Increment `revision`.
5. Serialize using source-generated `System.Text.Json`.
6. Write to `<registry>.tmp` in the same directory.
7. Flush the file stream, including durable flush when supported.
8. Atomically replace the destination.
9. Update the in-memory snapshot.
10. Release the semaphore.

On platforms where atomic replacement differs, use a same-volume rename strategy.
Never write the destination file in place.

At startup:

- missing file: create an empty schema-v1 registry;
- empty file: fail readiness and report a clear error;
- malformed JSON: fail readiness; do not overwrite;
- unsupported schema version: fail startup/readiness; do not migrate silently;
- duplicate IDs or names: fail validation;
- stale `.tmp` file: leave it intact and log its presence; never promote automatically.

## Server definition validation

### Common fields

| Field | Rule |
|---|---|
| `id` | Server-generated GUID |
| `name` | Required, trimmed, 1–100 Unicode scalar values |
| `description` | Optional, max 1000 characters |
| `enabled` | Boolean |
| `transport` | Exactly `stdio` or `http` |
| `operationTimeoutSeconds` | 1 through configured maximum |
| `createdAtUtc` | Server-generated UTC timestamp |
| `updatedAtUtc` | Server-generated UTC timestamp |

Names are unique after trim and Unicode-aware case-insensitive comparison. The persisted
original casing is preserved.

Exactly one transport options object must be present and it must match `transport`.

### Stdio validation

| Field | Rule |
|---|---|
| `command` | Required, nonempty, max 1024 characters |
| `arguments` | Array, max 128 entries, each max 8192 characters |
| `workingDirectory` | Optional absolute or application-resolved path |
| `environment` | Max 128 entries; valid process environment names |
| `shutdownTimeoutSeconds` | 1–30 |

Important:

- Do not join the command and arguments into a shell command.
- Do not invoke `cmd.exe`, `/bin/sh`, PowerShell, or another shell implicitly.
- Pass each argument directly to `ProcessStartInfo.ArgumentList` or SDK-equivalent options.
- Standard output is owned by the MCP transport and must not be used for application logs.
- Child-server diagnostic output should use standard error.

If `AllowedStdioCommands` is nonempty, the normalized executable command must exactly
match one entry. Do not use substring matching.

### HTTP validation

| Field | Rule |
|---|---|
| `endpoint` | Absolute `https` URI by default |
| `mode` | `auto`, `streamableHttp`, or `legacySse` |
| `headers` | Max 64 entries |
| header name | Valid HTTP header token |
| header value | Max 8192 characters before resolution |

Plain HTTP is allowed only for loopback addresses unless a dedicated unsafe-development
setting is introduced and documented.

Reject:

- URI user-info;
- unsupported schemes;
- fragments;
- control characters;
- headers controlled by the runtime such as `Host` and `Content-Length`.

When `AllowedHttpHosts` is nonempty, compare the normalized IDN host exactly against it.

## Secret references

Only this syntax is recognized:

```text
${ENV:VARIABLE_NAME}
```

A string may contain one or more references:

```text
Bearer ${ENV:MCP_ACCESS_TOKEN}
```

Resolution occurs only when creating a transport. Persist the reference text, not the
resolved value.

Rules:

1. Variable names must match `[A-Za-z_][A-Za-z0-9_]*`.
2. An unresolved variable fails connection with `secret_reference_unresolved`.
3. No recursive expansion.
4. Do not resolve references in server name, command, arguments, working directory, or
   endpoint in version 1.
5. Resolve references only in stdio environment values and HTTP header values.
6. Never put resolved values into exceptions, activity tags, logs, runtime snapshots,
   API responses, or execution history.
7. Redact conventional secret headers even when they contain literal values.

## Redaction

Always redact values whose key matches, case-insensitively:

```text
authorization
proxy-authorization
cookie
set-cookie
x-api-key
api-key
token
access-token
client-secret
password
secret
```

Also redact:

- any value produced by secret resolution;
- registry values containing `${ENV:...}` when presenting a runtime diagnostic;
- command arguments only when explicitly marked secret in a future schema. Version 1
  therefore recommends passing secrets via environment variables, not arguments.

## Container configuration

Recommended deployment:

```yaml
services:
  mcp-workbench:
    image: ghcr.io/example/mcp-workbench:latest
    ports:
      - "127.0.0.1:5070:8080"
    environment:
      McpWorkbench__RegistryPath: /data/servers.json
      Security__ApiKey: "${MCP_WORKBENCH_API_KEY}"
    volumes:
      - ./data:/data
```

Stdio child servers inside a container must exist in the image or a mounted volume.
Host-installed commands are not automatically visible inside the container.

## Configuration validation timing

Validate application options at startup using `ValidateOnStart`.
Validate persisted definitions while loading the registry.
Validate API inputs before any lifecycle or persistence side effect.
Validate resolved values again immediately before creating a transport.
