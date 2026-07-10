# Architecture Decision Record

This file records initial decisions. Later changes should append a new numbered decision
instead of silently rewriting the rationale.

## ADR-001 — Build an MCP client workbench, not an MCP gateway

**Status:** Accepted

The product registers MCP servers, inspects them, and invokes tools manually. It does not
proxy requests for external clients, aggregate servers behind one endpoint, or host an
LLM chat.

This keeps the first release focused and testable.

## ADR-002 — .NET 10 ASP.NET Core Minimal API

**Status:** Accepted

Use one ASP.NET Core service built with `CreateSlimBuilder`. Minimal APIs provide the
required JSON and static-file surface with a small Native AOT-compatible runtime shape.

MVC controllers, Razor Pages, and Blazor Server are excluded.

## ADR-003 — Native AOT is mandatory from the first phase

**Status:** Accepted

Every architectural choice must survive trim analysis and native publication. Reflection
fallbacks are disabled, JSON metadata is source generated, and release CI runs the actual
native executable.

## ADR-004 — Official stable MCP C# SDK, isolated behind an adapter

**Status:** Accepted

Use the stable official SDK package line, initially `ModelContextProtocol.Core` 1.4.1,
subject to Phase-0 restore verification. No preview package is permitted without a new
decision.

SDK types do not cross the `Mcp/` adapter boundary.

## ADR-005 — Support stdio and remote HTTP transports

**Status:** Accepted

Initial transport types are:

- local stdio;
- Streamable HTTP;
- legacy SSE compatibility through HTTP mode;
- automatic mode where supported.

Other custom transports are deferred.

## ADR-006 — JSON file registry instead of a database

**Status:** Accepted

Definitions are low-volume configuration, not transactional business data. A versioned
JSON file with serialized atomic replacement is sufficient and avoids database/native
dependencies.

The service is single-instance with respect to one registry file.

## ADR-007 — Static vanilla JavaScript UI

**Status:** Accepted

The UI is served from `wwwroot` and has no Node.js build/runtime dependency. It uses
browser-native modules, semantic HTML, and CSS.

This reduces repository complexity and packaging surface.

## ADR-008 — Loopback-first security model

**Status:** Accepted

Default binding is loopback. Optional static API-key protection and deployment allowlists
support deliberate remote use, but multi-user authentication/RBAC is not part of version
1.

## ADR-009 — Secret references, not persisted resolved secrets

**Status:** Accepted

Registry values may reference `${ENV:NAME}` in HTTP headers and stdio environment values.
Resolution occurs only while constructing a connection.

No secret vault abstraction is introduced initially.

## ADR-010 — One active connection per registered server

**Status:** Accepted

A runtime dictionary maps one server ID to one MCP client session. This makes lifecycle,
process ownership, and catalog state unambiguous.

## ADR-011 — Serialize tool calls per server

**Status:** Accepted

Version 1 permits one active tool invocation for each server. Different servers operate
concurrently.

This avoids assuming that every MCP server and transport supports safe parallel tool calls.
A later bounded concurrency option can supersede this decision.

## ADR-012 — Raw JSON is authoritative for tool arguments

**Status:** Accepted

The UI generates forms for common JSON Schema constructs, but raw JSON remains the final
representation. The application does not implement a complete JSON Schema engine.

## ADR-013 — Tool `isError` is not an HTTP transport error

**Status:** Accepted

When MCP successfully returns a tool result with `isError: true`, the API responds HTTP
200 and preserves that flag. Protocol, connection, timeout, and local validation failures
use non-2xx statuses.

## ADR-014 — Execution history is bounded and in memory

**Status:** Accepted

Keep metadata for the latest 50 operations per server by default. Do not retain arguments
or full results and do not persist history.

This supports diagnostics without creating an audit-data/security burden.

## ADR-015 — Unknown MCP content is preserved safely

**Status:** Accepted

Known content types receive typed app-owned views. Future/unknown content is represented as
bounded raw JSON rather than dropped or deserialized dynamically.

## ADR-016 — No automatic package installation

**Status:** Accepted

MCP Workbench starts explicitly configured commands but does not search registries, run
installers, download packages, or generate `npx -y` commands on behalf of the user.

Examples may show user-supplied commands, while the security implication remains explicit.

## ADR-017 — Readiness is independent of remote MCP availability

**Status:** Accepted

Readiness checks application configuration and registry health. An unavailable registered
MCP server does not make the management application unready.

## ADR-018 — Source-generated logging and explicit validation

**Status:** Accepted

Use source-generated `LoggerMessage` methods and small explicit validators. Avoid
reflection-heavy frameworks solely for convenience.

## ADR-019 — Versioned `/api/v1` contract

**Status:** Accepted

All management endpoints are under `/api/v1`; health endpoints are outside it. Breaking
contract changes require a new path or a documented migration.

## ADR-020 — UTF-8 without BOM and LF in repository text

**Status:** Accepted

All generated text uses UTF-8 without BOM and LF line endings. `.editorconfig` enforces the
policy. CI includes a repository-format check.
