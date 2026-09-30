# docs/ - Documentation Rules

**Last Updated**: 2026-07-02
**Scope**: Project documentation, standards, and knowledge base

---

## Directory Structure

```
docs/
├── brain/              # Epic documentation and session tracking
│   └── EPIC-W7-NNN/   # Wave 7 epic artifacts (manifest-based)
├── intel/
│   └── jane-street/   # OKF knowledge wiki (Jane Street HFT patterns)
├── protocol/          # Workflow protocols and enforcement rules
└── workflow/          # Git, branch, and collaboration workflows
```

---

## Documentation Standards

### Epic Documentation (`docs/brain/EPIC-W7-NNN/`)

**Required Files** (manifest-based architecture):
1. `manifest.json` — Central state tracker (phase status, artifact paths)
2. `00-hotspots.md` — Phase 0: hotspot analysis with MCP evidence
3. `00-scope.md` — Phase 1: scope definition
4. `01-scope-boundary.md` — Phase 1.5: boundary validation
5. `02-architecture-plan.md` — Phase 2: extraction plan with signatures
6. `03-audit-report.md` — Phase 3: DNA compliance audit
7. `04-tickets.md` — Phase 4: ticket breakdown
8. `ticket-N-completion.md` — Phase 5.N: execution record
9. `ticket-N-verification.md` — Phase 5.N.V: independent verification
10. `05-completion-report.md` — Phase 6: final review

**Naming Convention**:
- Epic IDs: `EPIC-W7-NNN` (Wave 7, zero-padded three digits)
- Legacy: `EPIC-CCN-N` (Cyclomatic Complexity Normalization — Waves 1-6)

**Update Frequency**: After each phase completion (manifest.json updated automatically)

---

## Jane Street Knowledge Base (TWO SOURCES -- BOTH MANDATORY)

### Source 1: OKF Wiki -- Pattern Guide (`docs/intel/jane-street/`)

**Format**: Open Knowledge Format (OKF) v0.1
**Status**: MANDATORY -- HOW to implement patterns
**Index**: [`docs/intel/jane-street/index.md`](intel/jane-street/index.md)

| File | Topic |
|------|-------|
| `complexity-reduction.md` | CYC <= 8 extraction patterns |
| `lock-free-patterns.md` | Actor/Enqueue mandate |
| `testing-strategies.md` | xUnit [Fact] only |
| `how-to-build-an-exchange.md` | FSM determinism, one_in_flight |
| `microsecond-eternity.md` | Zero-alloc, JIT warmup, cache alignment |
| `ocaml-performance-engineering.md` | Struct locality, data race freedom |
| `concurrency-coordination.md` | Cache coherency, false sharing |
| `advanced-skylake-deep-dive.md` | CPU front-end, DSB cache, CYC <= 8 rationale |

**Query**: `python scripts/query_kb.py "<term>"`

### Source 2: Rules Catalog -- The Coding Bible (`docs/standards/jane-street/`)

**Status**: MANDATORY -- WHAT to enforce. 100+ numbered rules. Supersedes OKF wiki on conflicts.
**Catalog**: [`docs/standards/jane-street/RULES_CATALOG.md`](standards/jane-street/RULES_CATALOG.md)
**Index**: [`docs/standards/jane-street/INDEX.md`](standards/jane-street/INDEX.md)

| Category | Rules | Examples |
|----------|-------|---------|
| Type Safety | JS-001..020 | Result<T,E>, Option<T>, sealed hierarchies, #nullable enable |
| Concurrency | JS-021..035 | lock() ban, Actor/Channel, Interlocked, async void ban |
| Performance | JS-036..050 | Span, ArrayPool, readonly struct, zero-alloc hot paths |
| Testing | JS-051..065 | Property-based, seeded Random, BenchmarkDotNet |
| Code Review | JS-066..080 | Diff <10k chars, CYC<=8, ASCII-only, switch expressions |
| Serialization | JS-081..095 | Versioned messages, checksums, zero-copy deserialization |
| Philosophy | JS-096..110 | Illegal states unrepresentable, compile-time enforcement |

### Source 3: PR Enforcement Agent (`docs/Jane Street Sentinel`)

**Status**: MANDATORY -- PR review agent instructions (GODMODE).
**Enforcement**: Every violation labeled `[CRITICAL-JS-P0/P1/P2]`. All violations BLOCK merge.
**Read**: `docs/Jane Street Sentinel` for full workflow and quick-reference top-10 violations.

---

## Protocol Documentation (`docs/protocol/`)

**Key Documents**:
- `BRANCH_STRATEGY.md` — Three-tier branch model (src/docs/infra)
- `COMPLEXITY_REDUCTION_PROTOCOL.md` — CYC <= 8 enforcement
- `CODEFACTOR_PROTOCOL.md` — NEVER use "Apply fixes" button (320 error precedent)
- `TEST_FRAMEWORK_PROTOCOL.md` — xUnit only, never NUnit/MSTest
- `RECOVERY_LOOP_PROTOCOL.md` — Epic failure recovery

**Update Trigger**: When workflow changes

---

## Workflow Documentation (`docs/workflow/`)

**Key Documents**:
- `V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md` — Manifest-based epic architecture
- `WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` — Building-blocks method (copy, don't generate)
- `BRANCH_STRATEGY_ENFORCEMENT.md` — GitButler virtual branch mandate

**Update Trigger**: When epic or wave workflow changes

---

## Session Tracking (`docs/brain/`)

### Wave 7 Session Files
- `autonomous_refactor_session.json` — Session metadata
- `autonomous_refactor_progress.md` — Epic queue and log
- `nexus_a2a.json` — Agent-to-agent handoffs (Nexus Bridge)
- `wave7-okf-cache.json` — OKF runtime cache (13 Jane Street documents)

### Forensic Reports
**Template**:
```markdown
# FORENSIC REPORT: EPIC-W7-NNN
## Executive Summary
## Root Cause
## Prevention
## Lessons Learned
```

---

## Markdown Standards

- ATX-style headings (`#`, `##`, `###`). One H1 per document.
- Relative paths for links: `[text](../path/to/file.md)`
- Always specify language in code blocks: ` ```csharp`, ` ```bash`, ` ```json`
- Verify links: `powershell -File .\scripts\verify_links.ps1`

---

## Documentation Workflow

### Before Epic
1. Create `docs/brain/EPIC-W7-NNN/` directory
2. Run `epic-intake EPIC-W7-NNN "description"` to generate Phase 0 artifacts

### During Epic
1. Each phase writes its output artifact and updates `manifest.json`
2. Track blockers in the ticket completion file

### After Epic
1. Phase 6 generates `05-completion-report.md`
2. `manifest.json` status → all phases `completed`

---

## Index

**Parent**: [`../AGENTS.md`](../AGENTS.md) (root)
**Children**: None (leaf node)
**Related**:
- [`../src/AGENTS.md`](../src/AGENTS.md) — Source code rules
- [`../scripts/AGENTS.md`](../scripts/AGENTS.md) — Tooling rules
