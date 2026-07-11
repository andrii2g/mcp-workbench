# Implementation Checklist

This checklist supplements `PLAN.md`. Codex marks an item complete only after code,
tests, and documentation for that item are present.

## Phase 0 — Bootstrap

- [x] Create `McpWorkbench.slnx`.
- [x] Create production, unit, integration, and test-server projects.
- [x] Set project references according to repository structure.
- [x] Add central package management.
- [x] Verify stable `ModelContextProtocol.Core` restore.
- [x] Enable nullable, analyzers, warnings as errors, deterministic build.
- [x] Add Native AOT properties to production project.
- [x] Add source-generated empty JSON context and prove a native publish.
- [x] Add basic `/health/live` and `/health/ready`.
- [x] Add scripts and CI skeleton.
- [x] Record exact SDK/package versions.

## Phase 1 — Domain and validation

- [x] Implement server definition and transport option records.
- [x] Implement transport/runtime enums.
- [x] Implement app error/result types.
- [x] Implement explicit common validation.
- [x] Implement stdio validation.
- [x] Implement HTTP validation.
- [x] Implement normalized unique-name rule.
- [x] Add exhaustive validator unit tests.
- [x] Register all DTOs in source-generated JSON context.

## Phase 2 — Registry persistence

- [x] Implement schema-v1 registry document.
- [x] Implement initial missing-file creation.
- [x] Implement strict load and validation.
- [x] Implement immutable snapshot reads.
- [x] Implement serialized create/replace/delete.
- [x] Implement temp-write, flush, atomic replace.
- [x] Preserve original file on write failure.
- [x] Add revision handling.
- [x] Add registry readiness state.
- [x] Test malformed/unsupported/concurrent/failure cases.
- [x] Verify UTF-8 without BOM output.

## Phase 3 — Secrets and redaction

- [x] Implement `${ENV:NAME}` parser.
- [x] Resolve only header/environment values.
- [x] Reject unresolved/invalid references safely.
- [x] Implement key-based redaction.
- [x] Implement exact resolved-value redaction.
- [x] Ensure definitions API returns reference text, never values.
- [x] Add log-capture tests proving no secret leakage.

## Phase 4 — MCP adapter

- [x] Define `IMcpClientSession`.
- [x] Define app-owned session/tool/result records.
- [x] Implement stdio SDK adapter.
- [x] Implement HTTP SDK adapter.
- [x] Implement initialization metadata mapping.
- [x] Implement ping.
- [x] Implement tool discovery mapping.
- [x] Implement tool invocation mapping.
- [x] Implement known and unknown content mapping.
- [x] Enforce catalog/result bounds.
- [x] Implement idempotent async disposal.
- [x] Add adapter unit tests with fakes.
- [x] Add real stdio integration tests.

## Phase 5 — Runtime manager

- [x] Implement runtime dictionary.
- [x] Implement lifecycle semaphore per server.
- [x] Implement invocation semaphore per server.
- [x] Implement state transitions.
- [x] Implement idempotent connect/disconnect.
- [x] Implement force reconnect.
- [x] Link cancellation tokens correctly.
- [x] Cancel operations on definition update/delete.
- [x] Dispose all sessions during shutdown.
- [x] Implement bounded metadata history.
- [x] Test concurrency, failures, and process cleanup.

## Phase 6 — Tool catalog and invocation

- [x] Implement cached tool discovery and explicit refresh.
- [x] Retain the previous catalog when refresh fails.
- [x] Enforce catalog count, cursor, schema, and metadata limits.
- [x] Validate exact tool names and bounded JSON-object arguments.
- [x] Serialize invocations per server with linked timeout and cancellation.
- [x] Preserve tool errors separately from protocol and transport failures.
- [x] Preserve known, structured, and bounded unknown result content.
- [x] Record bounded metadata-only execution history.
- [x] Add real stdio coverage for success, tool error, protocol error, timeout,
      cancellation, result size, and default logging safety.

## Phase 7 — HTTP API

- [x] Implement common response/error mapping.
- [x] Implement request ID middleware.
- [x] Implement server CRUD endpoints.
- [x] Implement connect/disconnect/ping/runtime endpoints.
- [x] Implement list/refresh/read tool endpoints.
- [x] Implement invoke endpoint.
- [x] Keep `docs/API.md` authoritative; OpenAPI was not selected for version 1.
- [x] Add route/status/contract integration tests.

## Phase 8 — Static UI

- [ ] Build accessible application shell.
- [ ] Build server list.
- [ ] Build add/edit form.
- [ ] Build server details and runtime controls.
- [ ] Build tool list and filtering.
- [ ] Build schema viewer.
- [ ] Build raw JSON editor.
- [ ] Build limited generated form.
- [ ] Build result renderer.
- [ ] Render all untrusted values with `textContent`.
- [ ] Add loading/empty/error states.
- [ ] Add responsive CSS.
- [ ] Complete manual accessibility/security checklist.

## Phase 9 — Hardening

- [ ] Enforce loopback-only default.
- [ ] Enforce command/host allowlists.
- [ ] Validate redirects and protect headers.
- [ ] Enforce result/catalog/stderr limits.
- [ ] Verify no static access to data directory.
- [ ] Add CSP and related headers.
- [ ] Add malicious-content tests.
- [ ] Add graceful shutdown and orphan-process tests.
- [ ] Complete documented threat review.

## Phase 10 — AOT and packaging

- [ ] Publish `linux-x64`.
- [ ] Publish `linux-arm64`.
- [ ] Publish `win-x64`.
- [ ] Resolve every trim/AOT warning.
- [ ] Run published executable smoke tests.
- [ ] Build minimal container image.
- [ ] Run container with persistent registry volume.
- [ ] Verify non-root container operation where supported.
- [ ] Generate checksums for release artifacts.

## Phase 11 — Documentation and samples

- [ ] Replace planning README with product README while preserving architecture links.
- [ ] Add local run instructions.
- [ ] Add stdio and HTTP registration examples.
- [ ] Add security/deployment warnings.
- [ ] Add troubleshooting guidance.
- [ ] Add sample registry without secrets.
- [ ] Add API request collection.
- [ ] Add repository topics and description.
- [ ] Add release/change log entry.

## Final acceptance

- [ ] All FR-001 through FR-047 are implemented or explicitly marked deferred with user
      approval.
- [ ] All NFR-001 through NFR-010 pass.
- [ ] Normal tests pass.
- [ ] Coverage thresholds pass.
- [ ] Native AOT builds and smoke tests pass for required RIDs.
- [ ] No preview dependencies.
- [ ] No unresolved compiler, analyzer, trim, or AOT warnings.
- [ ] No real secret in repository or test artifacts.
- [ ] All repository text is UTF-8 without BOM.
- [ ] Working tree contains no generated `bin`, `obj`, coverage, or publish output.
- [ ] `README.md`, `PLAN.md`, and implementation state agree.
