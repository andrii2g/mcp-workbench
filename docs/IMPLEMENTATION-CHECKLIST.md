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

- [ ] Implement schema-v1 registry document.
- [ ] Implement initial missing-file creation.
- [ ] Implement strict load and validation.
- [ ] Implement immutable snapshot reads.
- [ ] Implement serialized create/replace/delete.
- [ ] Implement temp-write, flush, atomic replace.
- [ ] Preserve original file on write failure.
- [ ] Add revision handling.
- [ ] Add registry readiness state.
- [ ] Test malformed/unsupported/concurrent/failure cases.
- [ ] Verify UTF-8 without BOM output.

## Phase 3 — Secrets and redaction

- [ ] Implement `${ENV:NAME}` parser.
- [ ] Resolve only header/environment values.
- [ ] Reject unresolved/invalid references safely.
- [ ] Implement key-based redaction.
- [ ] Implement exact resolved-value redaction.
- [ ] Ensure definitions API returns reference text, never values.
- [ ] Add log-capture tests proving no secret leakage.

## Phase 4 — MCP adapter

- [ ] Define `IMcpClientSession`.
- [ ] Define app-owned session/tool/result records.
- [ ] Implement stdio SDK adapter.
- [ ] Implement HTTP SDK adapter.
- [ ] Implement initialization metadata mapping.
- [ ] Implement ping.
- [ ] Implement tool discovery mapping.
- [ ] Implement tool invocation mapping.
- [ ] Implement known and unknown content mapping.
- [ ] Enforce catalog/result bounds.
- [ ] Implement idempotent async disposal.
- [ ] Add adapter unit tests with fakes.
- [ ] Add real stdio integration tests.

## Phase 5 — Runtime manager

- [ ] Implement runtime dictionary.
- [ ] Implement lifecycle semaphore per server.
- [ ] Implement invocation semaphore per server.
- [ ] Implement state transitions.
- [ ] Implement idempotent connect/disconnect.
- [ ] Implement force reconnect.
- [ ] Link cancellation tokens correctly.
- [ ] Cancel operations on definition update/delete.
- [ ] Dispose all sessions during shutdown.
- [ ] Implement bounded metadata history.
- [ ] Test concurrency, failures, and process cleanup.

## Phase 6 — HTTP API

- [ ] Implement common response/error mapping.
- [ ] Implement request ID middleware.
- [ ] Implement optional API-key middleware/filter.
- [ ] Implement server CRUD endpoints.
- [ ] Implement connect/disconnect/ping/runtime endpoints.
- [ ] Implement list/refresh/read tool endpoints.
- [ ] Implement invoke endpoint.
- [ ] Enforce request/body/time limits.
- [ ] Add security headers.
- [ ] Update OpenAPI only if it remains AOT-safe and explicitly selected; otherwise keep
      `.http` contract file authoritative.
- [ ] Add route/status/contract integration tests.

## Phase 7 — Static UI

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

## Phase 8 — Hardening

- [ ] Enforce loopback-only default.
- [ ] Enforce command/host allowlists.
- [ ] Validate redirects and protect headers.
- [ ] Enforce result/catalog/stderr limits.
- [ ] Verify no static access to data directory.
- [ ] Add CSP and related headers.
- [ ] Add malicious-content tests.
- [ ] Add graceful shutdown and orphan-process tests.
- [ ] Complete documented threat review.

## Phase 9 — AOT and packaging

- [ ] Publish `linux-x64`.
- [ ] Publish `linux-arm64`.
- [ ] Publish `win-x64`.
- [ ] Resolve every trim/AOT warning.
- [ ] Run published executable smoke tests.
- [ ] Build minimal container image.
- [ ] Run container with persistent registry volume.
- [ ] Verify non-root container operation where supported.
- [ ] Generate checksums for release artifacts.

## Phase 10 — Documentation and samples

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
