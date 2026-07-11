# MCP Workbench

MCP Workbench is a small self-hosted dashboard for registering Model Context Protocol
servers, inspecting their tools, and invoking tools without an LLM or model-provider
account. It supports local stdio servers and remote Streamable HTTP or legacy SSE
servers through the official MCP C# SDK.

## Requirements

- .NET 10 SDK for source builds
- A browser
- The runtime required by each configured stdio MCP server
- A Native AOT toolchain when producing native releases

MCP Workbench itself has no Node.js build or runtime dependency.

## Run locally

```powershell
dotnet restore McpWorkbench.slnx --locked-mode
dotnet run --project src/McpWorkbench
```

Open `http://127.0.0.1:5070`. Definitions are stored in `data/servers.json` by default.

Run the Windows verification suite:

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/publish-aot.ps1 -RuntimeIdentifiers win-x64
./scripts/aot-smoke.ps1
```

The equivalent `.sh` scripts are provided for Linux. Linux Native AOT and container
execution remain scheduled for WSL validation.

## Register servers

Create a stdio definition through the UI or `POST /api/v1/servers`:

```json
{
  "name": "Local MCP server",
  "enabled": true,
  "transport": "stdio",
  "stdio": {
    "command": "dotnet",
    "arguments": ["./path/to/server.dll"],
    "workingDirectory": null,
    "environment": { "SERVICE_TOKEN": "${ENV:SERVICE_TOKEN}" },
    "shutdownTimeoutSeconds": 5
  },
  "http": null,
  "operationTimeoutSeconds": 30
}
```

Create an HTTP definition:

```json
{
  "name": "Remote MCP server",
  "enabled": true,
  "transport": "http",
  "stdio": null,
  "http": {
    "endpoint": "https://mcp.example.test/mcp",
    "mode": "auto",
    "headers": { "Authorization": "Bearer ${ENV:REMOTE_MCP_TOKEN}" }
  },
  "operationTimeoutSeconds": 30
}
```

See [servers.example.json](samples/servers.example.json) for a complete registry and
[McpWorkbench.http](requests/McpWorkbench.http) for API requests covering registration,
connection, discovery, invocation, and deletion.

## Configuration

ASP.NET Core configuration sources are supported. Environment-variable keys use double
underscores:

```powershell
$env:McpWorkbench__RegistryPath = "C:\data\mcp-workbench\servers.json"
$env:Security__ApiKey = "replace-with-a-secret"
dotnet run --project src/McpWorkbench
```

The application binds to loopback by default. Before allowing remote binding:

- set `McpWorkbench__BindToLoopbackOnly=false` intentionally;
- configure `Security__ApiKey` and network-level access controls;
- restrict `AllowedStdioCommands` and `AllowedHttpHosts` where possible;
- protect the registry file and secret-providing environment;
- treat every configured stdio executable and HTTP endpoint as trusted code.

Do not expose the default configuration directly to an untrusted network. The API key is
a shared secret, not a multi-user authentication or authorization system.

## Container

`Dockerfile` and `compose.yaml` provide a non-root Alpine Native AOT deployment with a
persistent `/app/data` volume. Stdio commands must exist inside the container or a mounted
volume; host-installed commands are not automatically available.

```bash
MCP_WORKBENCH_API_KEY=replace-with-a-secret docker compose up --build
```

Container execution is currently deferred to the Linux/WSL validation pass.

## Troubleshooting

**The application rejects its bind address**

Remote URLs are rejected while `BindToLoopbackOnly` is enabled. Prefer loopback or disable
the guard explicitly and configure an API key.

**A stdio server does not start**

Confirm the executable is installed, permitted by `AllowedStdioCommands`, and reachable
under the Workbench process identity. Arguments are passed directly without a shell.

**A connection reports an unresolved secret**

Set every environment variable referenced by `${ENV:NAME}` before connecting. References
are resolved at connection time and their values are not persisted.

**The registry fails during startup**

Inspect the configured JSON file for malformed data, duplicate names/IDs, or an unsupported
schema version. Workbench deliberately does not overwrite a corrupt registry.

**Native publish fails on another operating system**

Native AOT does not support cross-OS compilation. Run the matching publish script on the
target OS with its native compiler toolchain installed.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [API contract](docs/API.md)
- [Configuration and persistence](docs/CONFIGURATION.md)
- [MCP integration](docs/MCP-INTEGRATION.md)
- [Native AOT](docs/NATIVE-AOT.md)
- [Security](docs/SECURITY.md) and [threat review](docs/THREAT-REVIEW.md)
- [Testing](docs/TESTING.md)
- [Implementation status](docs/IMPLEMENTATION-CHECKLIST.md)
- [Project scope](PLAN.md) and [architecture decisions](docs/DECISIONS.md)

## License

No license has been selected. Repository visibility does not grant permission to copy,
modify, or redistribute the project.
