# MCP Workbench

MCP Workbench is a lightweight, self-hosted dashboard for registering Model Context Protocol servers, inspecting their capabilities and tools, and executing tool calls manually.

It is designed as a Postman-like utility for MCP development and troubleshooting. It does not require an LLM or model-provider account.

> This planning pack is Codex-ready. Follow `PLAN.md` and `AGENTS.md` to build version 0.1.0.

## Planned features

- Add, edit, and remove MCP server definitions.
- Connect to local MCP servers over stdio.
- Connect to remote MCP servers over Streamable HTTP, with legacy compatibility handled by the SDK.
- Ping servers and inspect negotiated metadata.
- Discover and refresh tool catalogs.
- Inspect tool descriptions, annotations, and JSON schemas.
- Invoke tools with raw JSON arguments.
- Render text, structured content, known content blocks, and raw results.
- Store definitions in one local JSON file.
- Resolve `${ENV:NAME}` secret references at connection time.
- Run as a small .NET 10 Native AOT executable.
- Use a dependency-free static browser UI.

## Planned architecture

```text
Browser
   |
   | HTTP/JSON
   v
ASP.NET Core 10 Minimal API + static UI
   |
   +-- JSON server-definition registry
   |
   +-- in-memory MCP connection manager
          |
          +-- stdio child-process MCP servers
          +-- remote HTTP MCP servers
```

## Requirements after implementation

- .NET 10 SDK for development
- Native AOT compiler toolchain for the selected runtime
- A browser
- Optional external runtimes used by configured MCP servers

MCP Workbench itself will not require Node.js.

## Target quick start

```bash
dotnet restore
dotnet run --project src/McpWorkbench
```

Open:

```text
http://127.0.0.1:5070
```

Native AOT publish:

```bash
dotnet publish src/McpWorkbench/McpWorkbench.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true
```

## Example stdio definition

```json
{
  "name": "Filesystem server",
  "transport": "stdio",
  "stdio": {
    "command": "npx",
    "arguments": [
      "-y",
      "@modelcontextprotocol/server-filesystem",
      "/workspace"
    ],
    "workingDirectory": null,
    "environment": {},
    "shutdownTimeoutSeconds": 5
  }
}
```

## Example HTTP definition

```json
{
  "name": "Remote MCP",
  "transport": "http",
  "http": {
    "endpoint": "https://example.test/mcp",
    "headers": {
      "Authorization": "Bearer ${ENV:MCP_API_TOKEN}"
    }
  }
}
```

## Security model

MCP Workbench is intended for trusted developer environments. Registering a stdio server grants the application permission to start that executable. A remote endpoint can reach network resources accessible from the host. Tool calls can perform any action implemented by the selected MCP server.

The service will therefore:

- bind to loopback by default;
- avoid shell execution;
- support optional API-key protection;
- resolve secrets from environment variables;
- redact sensitive values from logs;
- escape all MCP-provided UI content;
- document that the default configuration is not an internet-facing security boundary.

Read `docs/SECURITY.md` before exposing it beyond localhost.

## Planning documents

- `PLAN.md` — scope, phases, requirements, and definition of done.
- `AGENTS.md` — strict Codex instructions.
- `docs/REPOSITORY-STRUCTURE.md` — exact target file tree.
- `docs/ARCHITECTURE.md` — runtime design and workflows.
- `docs/API.md` — endpoint contracts.
- `docs/CONFIGURATION.md` — application and registry configuration.
- `docs/DOMAIN-MODEL.md` — persisted and runtime models.
- `docs/MCP-INTEGRATION.md` — SDK boundary and MCP behavior.
- `docs/NATIVE-AOT.md` — AOT requirements.
- `docs/SECURITY.md` — threat model and safeguards.
- `docs/TESTING.md` — test strategy and detailed cases.
- `docs/UI-SPEC.md` — static browser UI specification.
- `docs/DECISIONS.md` — architecture decisions.
- `docs/IMPLEMENTATION-CHECKLIST.md` — phase checklist.
- `docs/REFERENCES.md` — official specification, SDK, .NET, and security references.

## License

No license has been selected in this planning pack. Add one before public distribution.
