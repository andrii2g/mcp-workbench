# Quick Start

This guide gets MCP Workbench running and connects the first MCP server.

## Run from source

Prerequisites:

- .NET 10 SDK
- A browser
- The runtime required by any stdio MCP server you register

Clone and start the service:

```powershell
git clone https://github.com/andrii2g/mcp-workbench.git
cd mcp-workbench
dotnet restore McpWorkbench.slnx --locked-mode
dotnet run --project src/McpWorkbench
```

Open `http://127.0.0.1:5070`. The default registry is `data/servers.json`.

## Run a native Windows build

Publish and start the Native AOT executable:

```powershell
./scripts/publish-aot.ps1 -RuntimeIdentifiers win-x64
./artifacts/win-x64/mcp-workbench.exe
```

Native AOT publishing requires the Visual Studio C++ build tools on Windows. Native
builds must be produced on the target operating system.

## Register the test server

Build the deterministic test MCP server:

```powershell
dotnet build tests/McpWorkbench.TestServer -c Release
```

In the browser, select **Add server** and enter:

| Field | Value |
| --- | --- |
| Name | `Local test server` |
| Transport | `Stdio process` |
| Command | `dotnet` |
| Arguments | `./tests/McpWorkbench.TestServer/bin/Release/net10.0/McpWorkbench.TestServer.dll` |
| Operation timeout | `30` |

Select **Save and connect**. Open the server, select a discovered tool, provide its JSON
arguments, and run it.

## Register an HTTP server

Select **Add server**, choose **HTTP endpoint**, and enter the MCP endpoint. Use an
environment reference for credentials:

```text
Authorization = Bearer ${ENV:REMOTE_MCP_TOKEN}
```

Set the referenced variable before starting MCP Workbench:

```powershell
$env:REMOTE_MCP_TOKEN = "replace-with-your-token"
dotnet run --project src/McpWorkbench
```

The resolved value is used only when connecting and is not persisted in the registry.

## Enable API-key protection

```powershell
$env:Security__ApiKey = "replace-with-a-strong-secret"
dotnet run --project src/McpWorkbench
```

The browser prompts for the key when its first API request is rejected. The key is kept
only for the browser session.

## Run with Docker

On a Linux Docker host:

```bash
MCP_WORKBENCH_API_KEY=replace-with-a-strong-secret docker compose up --build
```

Open `http://127.0.0.1:5070`. Registry data is stored in the Compose volume. Commands for
stdio servers must exist inside the container.

## Next steps

- Review all settings in [Configuration](CONFIGURATION.md).
- Use the request collection in [McpWorkbench.http](../requests/McpWorkbench.http).
- Read [Security](SECURITY.md) before binding beyond loopback.
- See [Native AOT](NATIVE-AOT.md) for platform build requirements.
