# AGENTS.md — Instructions for Codex

## Mission

Implement MCP Workbench exactly as specified in `PLAN.md` and the topic documents under `docs/`. Produce a small, secure, Native-AOT-compatible developer tool rather than a general platform.

## Read-first order

Before changing code, read:

1. `PLAN.md`
2. `docs/REPOSITORY-STRUCTURE.md`
3. `docs/ARCHITECTURE.md`
4. the topic document relevant to the current phase
5. this file again before finalizing a phase

## Hard constraints

- Use .NET 10.
- Use ASP.NET Core Minimal APIs and `WebApplication.CreateSlimBuilder(args)`.
- Keep one production project: `src/McpWorkbench`.
- Enable Native AOT in the production project from the first commit.
- Use the stable MCP C# SDK package version pinned in `Directory.Packages.props`.
- Do not upgrade to a preview MCP SDK without an explicit task.
- Use vanilla HTML, CSS, and JavaScript modules. Do not add Node.js tooling.
- Use JSON-file persistence. Do not add a database.
- Use source-generated System.Text.Json metadata for application-owned types.
- Do not enable reflection-based JSON serialization.
- Do not add MVC, controllers, Razor Pages, Blazor Server, or runtime plugin loading.
- Do not add LLM integration.
- Do not expand into prompts/resources/sampling/OAuth/multi-user support.
- Treat warnings from Native AOT or trimming as defects that require resolution.
- Use UTF-8 without BOM for all text files.

## Implementation style

- Prefer small explicit types and functions over generic frameworks.
- Keep SDK types at the MCP adapter boundary.
- Use dependency injection only for long-lived services, options, clocks, environment access, and test seams.
- Avoid repository/service/interface layers that do not provide a real boundary.
- Use interfaces for persistence, environment lookup, time, and MCP SDK interaction because these are important test seams.
- Use sealed classes unless inheritance is intentional.
- Use records for immutable request/response and persisted data where practical.
- Use `CancellationToken` on every I/O-bound asynchronous operation.
- Use `TimeProvider` instead of direct `DateTimeOffset.UtcNow` where tests need control.
- Never construct a shell command line.
- Use `StringComparer.OrdinalIgnoreCase` for unique server names and HTTP header-name checks.
- Use `StringComparer.Ordinal` for MCP tool names.
- Preserve unknown MCP content as raw bounded JSON rather than discarding it.

## Native AOT checklist for every change

Before accepting a new package or API:

1. Check for reflection, runtime code generation, dynamic proxies, assembly scanning, or expression compilation.
2. Prefer source-generated or explicit alternatives.
3. Add all application-owned serialized types to `AppJsonSerializerContext`.
4. Build Release.
5. Publish Native AOT regularly, not only at the end.
6. Do not suppress IL2026, IL3050, or trim warnings without a written justification in `docs/NATIVE-AOT.md`.

## Security rules

- Treat all MCP output as untrusted.
- Never render MCP text using `innerHTML`.
- Never log tool arguments or result bodies by default.
- Never log resolved environment variables or authorization header values.
- Never execute a command supplied directly to a tool-invocation endpoint.
- Only persisted, validated stdio definitions can start processes.
- Never invoke a shell to start an MCP server.
- Resolve `${ENV:NAME}` into temporary connection configuration, not the persisted object.
- Keep loopback binding as the default.
- Warn on non-loopback binding without API-key protection.

## Scope control

When a convenient library or feature would violate the fixed architecture, implement the narrower behavior described by the plan:

- a small form renderer instead of a JSON Schema framework;
- atomic JSON instead of EF Core;
- static JS modules instead of a SPA framework;
- small API-key middleware instead of an identity platform.

Do not implement items listed as non-goals.

## Work sequence

- Implement one `PLAN.md` phase at a time.
- Add tests in the same phase as production behavior.
- Run formatting, build, and tests before moving on.
- Publish AOT at least after phases 0, 4, 7, 8, and 10.
- Keep `docs/IMPLEMENTATION-CHECKLIST.md` updated.
- Do not rewrite planning documents to make incomplete code appear compliant.

## Required commands

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet publish src/McpWorkbench/McpWorkbench.csproj -c Release -r linux-x64 --self-contained true
dotnet publish src/McpWorkbench/McpWorkbench.csproj -c Release -r win-x64 --self-contained true
```

When a command cannot run, say so and do not claim success.

## Testing expectations

- Add focused unit tests for validation, persistence, secret resolution, state transitions, and mapping.
- Add integration tests for all API endpoints and real MCP operations.
- Use the deterministic test MCP server; do not depend on public internet servers.
- Test cancellation and timeout behavior without flaky timing.
- Test corrupt persistence and failed writes.
- Test that representative secrets do not appear in captured logs or serialized errors.
- Test the published native executable with a process-level smoke test.

## Completion response

For each completed phase, provide:

- summary;
- files changed;
- tests added;
- commands and outcomes;
- known limitations;
- next incomplete phase.

Never state that the project is complete until every definition-of-done item passes.
