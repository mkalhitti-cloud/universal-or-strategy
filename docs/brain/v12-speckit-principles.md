# V12 Universal OR Strategy Constitution
<!-- Encoded via /speckit-constitution — GitHub Spec Kit v0.12.4 -->

## Core Principles

### I. Language and Platform
C# targeting NinjaTrader 8 on Windows (.NET Framework 4.8). All code must
compile with `dotnet build` returning zero errors. Hard links are managed by
`deploy-sync.ps1`; run it after every `src/` change batch.

### II. Cyclomatic Complexity Limit (NON-NEGOTIABLE)
Cyclomatic complexity MUST be <= 8 per function (Jane Street strict standard).
Functions exceeding this threshold are hard to reason about under microsecond
latency constraints, cannot be exhaustively tested, and are prone to race
conditions in lock-free code. Enforcement: `scripts/complexity_audit.py`.

### III. Lock-Free FSM/Actor Pattern (NON-NEGOTIABLE)
`lock()` blocks are STRICTLY BANNED. All state mutations MUST use the
FSM/Actor `Enqueue` model or atomic primitives (`Interlocked`, `volatile`).
Enforcement: `grep -r "lock(" src/` must return zero matches before every push.

### IV. ASCII-Only String Literals (NON-NEGOTIABLE)
NEVER use Unicode, emoji, curly quotes, or any non-ASCII character inside C#
string literals. ASCII-only enforcement is checked in pre-push validation
(check #1). Violations cause CI failure.

### V. Test Framework: xUnit ONLY (NON-NEGOTIABLE)
ALL tests MUST use xUnit. NUnit and MSTest are BANNED. Test files live in
`tests/`. The TDD cycle is: spec -> plan -> tasks -> implement -> xUnit tests
pass. See `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`.

### VI. Spec-Driven Development Workflow
Every epic follows the manifest-based V12 workflow:
`spec.md` -> `plan.md` -> `tasks.md` -> implement -> verify
Use `/speckit-specify` to generate spec.md, `/speckit-plan` for plan.md,
`/speckit-tasks` for tasks.md. All artifacts land in `docs/brain/EPIC-X/`.
The `/speckit-constitution` command keeps this file in sync with project evolution.

### VII. Surgical Changes — One Epic, One Concern
Each PR addresses exactly ONE epic and ONE concern. Scope creep is a P0
violation. If unrelated issues are discovered during an epic, STOP, report to
Director, and address in a separate PR. Reference: `docs/protocol/` for full
No Scope Creep Protocol (V12.23).

### VIII. Pre-Push Validation Mandatory
Run `powershell -File .\scripts\pre_push_validation.ps1` (or `-Fast` for speed)
before EVERY push. All 13 quality gates must pass. Blocking gates: ASCII-only,
build, unit tests, lint, CSharpier formatting, PR hygiene, complexity.

### IX. Jane Street KB as Primary Reference
Before every architectural decision, query the Jane Street Knowledge Base:
`python scripts/query_kb.py "<term>"`
The KB contains 100+ P0/P1/P2 rules covering HFT patterns, FSM/Actor
implementation, lock-free primitives, and testing strategies. Do not design
without consulting it.

### X. Build Integrity After Every Change
`dotnet build src/` must return zero errors after every batch of changes.
Never leave the codebase in a broken state between commits.

## Quality Standards

Hard-link sync: `deploy-sync.ps1` after every `src/` change batch.
Code formatting: CSharpier (`dotnet csharpier format src/`) — mandatory.
Complexity audit: `python scripts/complexity_audit.py` — pre-push gate.
Hotspot analysis: jCodemunch MCP `get_hotspots` + CodeScene for refactor priority.
Dead-code scan: `scripts/dead_code_scan.py` — warning gate.

## Development Workflow

1. Query Jane Street KB for architectural patterns
2. Use `/speckit-specify` to author `docs/brain/EPIC-X/spec.md`
3. Use `/speckit-plan` to generate `docs/brain/EPIC-X/plan.md`
4. Use `/speckit-tasks` to generate `docs/brain/EPIC-X/tasks.md`
5. Execute tickets via Bob CLI (`v12-engineer`) for `src/` work
6. Verify with `dotnet build src/` + `pre_push_validation.ps1`
7. Run `/speckit-analyze` before implementation for cross-artifact consistency
8. Run `/speckit-converge` to surface remaining work after partial implementation

## Governance

This constitution supersedes all other per-file coding preferences for the
V12 Universal OR Strategy project. Any amendment requires Director approval
and must update this file, AGENTS.md, and the relevant `docs/protocol/` file.

All PRs and code reviews MUST verify compliance with Principles I through X.
Complexity violations must be refactored before merge, never suppressed.

Jane Street KB (`scripts/query_kb.py`) is the runtime reference for all
architectural guidance during active development sessions.

**Version**: 1.0.0 | **Ratified**: 2026-07-01 | **Integration**: GitHub Spec Kit v0.12.4
