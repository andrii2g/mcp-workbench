# Codex start instruction

Use this instruction when handing the repository to Codex:

```text
Read AGENTS.md, PLAN.md, and all files under docs/ before changing the repository.
Implement MCP Workbench phase by phase, beginning with Phase 0.
Follow the exact target file tree and Native AOT constraints.
Add tests in the same phase as implementation.
Do not add features listed as non-goals.
After each phase, run required commands and report changed files, test results,
AOT results, remaining limitations, and the next phase.
Do not claim a phase is complete if any required command failed or was skipped.
```

For continuous implementation, append:

```text
After a phase passes its acceptance criteria, continue to the next phase until a real blocker is encountered or version 0.1.0 satisfies the definition of done.
```
