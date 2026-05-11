# Epic Brief: Phase 6 Hot Path Execution Hardening

Epic Brief — Phase 6: Hot Path Execution Hardening

<user_quoted_section>Status: Locked after refactor-intake alignment session.Trail: Inherits from master_roadmap.md Hotspot Map (rows 1, 5) + Architecture Heatmap (rows 1, 2, 4). Bridges roadmap milestones M5 (Zero-Allocation Hot Path) and M7 (Concurrency Hardening).</user_quoted_section>

## 1. Code Area (IN-SCOPE)

| # | God-Function | Physical File | Roadmap CYC | Notes |
| --- | --- | --- | --- | --- |
| T1 | `ManageTrailingStops` | file:src/V12_002.Trailing.cs (lines 39-451, 412 LOC) | **151** | Drives every stop-trail decision on every tick of every active position; adaptive throttle + circuit breaker + 4 per-trade-type branches + post-loop fleet symmetry sync + Shadow check. |
| T2 | `ProcessOnExecutionUpdate` cluster | file:src/V12_002.Orders.Callbacks.Execution.cs (lines 207-464) | **120 (file-level)** | Per A1 alignment: targets the file the architecture doc flagged as the 120 CYC carrier. The cluster is already heavily decomposed (Dedup / TrackCompliance / HandleStopFill / HandleTargetFill / HandleTrimFill / ExtractEntryName / RunShadowCheck). Remaining work: extract a shared "fully-closed via partial-exit cleanup" helper for HandleTargetFill + HandleTrimFill, and split the entry-pending / unfilled-position scans inside `HandleFlatPosition_SyncExpected`. |
| T3 | `ExecuteSmartDispatchEntry` | file:src/V12_002.SIMA.Dispatch.cs (lines 45-643, 599 LOC) | **100** | Fan-in from 11 entry call-sites (TREND/RETEST/OR/MOMO/FFMA + `_MNL` variants); fleet loop with two large branches (Market vs Limit), each dispatches via Photon ring (zero-alloc) with ConcurrentQueue fallback. |

**Architecture-doc symbol-name bug noted**: `docs/architecture.md` Heatmap row 2 cites the correct file (`V12_002.Orders.Callbacks.Execution.cs`) but the wrong symbol name (`OnOrderUpdate`). The actual god-cluster in that file is `ProcessOnExecutionUpdate`. Both fixes (symbol name correction + post-extraction CYC refresh) ship in the final Phase 6 ticket.

## 2. Validated Problem

The three functions sit on the **critical execution hot path** between price tick and broker submission. Their current cyclomatic complexity (151 / 120 / 100) prevents any reasoning agent (human or LLM) from holding the full state machine in mind during edits, which has historically produced:

- **Ghost orders** (orphaned stops/targets after cleanup race)
- **Concurrency violations** (torn reads when callbacks fire mid-iteration)
- **Verbatim log drops during prior extractions** (Phase 5 F-06 closed only after Arena Red Team caught 3 missing TREND `Print` lines)

The roadmap already designates `ManageTrailingStops` for **M5 Zero-Allocation Hot Path** and `ExecuteSmartDispatchEntry` for "Phase 4 scaffolds this" (now done). `ProcessOnOrderUpdate` is **net-new** to the roadmap — added because it owns the broker-callback fan-in for both master and follower order lifecycle events.

## 3. Scope Boundaries

### IN scope

- Surgical extraction of new sub-handlers from the three named methods so that:
  - Each new sub-handler measures **CYC < 20** (verified via `python scripts/csharp_hotspots.py`).
  - Each remaining parent dispatcher measures **CYC < 30**.
- **Co-locate** new sub-handlers in existing sibling files when natural (e.g., new TREND-trail handler may land in `Trailing.Breakeven.cs`); otherwise create new partial files following the `V12_002.<Module>.<Concern>.cs` convention.
- **Surgical + opportunistic adjacent fixes** spotted *during* extraction:
  - `DateTime.Now` -> `DateTime.UtcNow` on extracted lines (mirrors Phase 5 ticket T2 + F-01b precedent).
  - Brace standardization on Codacy-flagged single-line control statements *within touched lines only*.
  - Restoration of any verbatim `Print` strings that would otherwise be moved.
- Update `docs/architecture.md` heatmap: re-anchor `OnOrderUpdate` to `Orders.Callbacks.cs`, refresh CYC numbers post-extraction.
- Update `docs/brain/master_roadmap.md` to register **Phase 6** as a discrete milestone bridging M5 and M7.

### OUT of scope

- New public API surface (no new `protected`/`public` methods; all sub-handlers `private`).
- Behavioral changes — the bit-stream of `Print` output, broker order submissions, FSM state transitions must be **byte-identical** before and after.
- Lock introduction (`lock(stateLock)` is BANNED per AGENTS.md §2). All concurrency continues via `ConcurrentDictionary` + `Enqueue`/`TriggerCustomEvent`.
- Touching the already-extracted helpers: `UpdateStopOrder`, `CalculateStopForLevel`, `ShouldSkipFleetAccount`, `ProcessFleetSlot`, `PumpFleetDispatch`, `MoveStopsToBreakevenWithOffset`, `MoveSpecificTarget` — these stay as-is.
- Build-984 unmerged work, AMAL harness, Rithmic sidecar (M4), MMIO ring schema changes, Photon pool sizing.

## 4. Risk Level — **CORE / HIGH**

All three functions sit on the live execution hot path. Failure modes:

| Failure | Blast Radius | Detection Latency |
| --- | --- | --- |
| Trail stop missed | Single position runs without stop until next tick (~100ms-500ms) | OnExecutionUpdate Shadow callback (Build 1105) catches some; REAPER catches the rest within 1 audit cycle |
| Order callback not handled | Ghost order at broker; `expectedPositions` desync | REAPER detects within audit cycle (subsecond) |
| Dispatch fleet partial | Some accounts get follower order, others don't | Visible in `[DISPATCH]` log; manual flatten required if SIMA fan-out fails partway |
| Print string mutated | Operations greps + SOVEREIGN replay harness break | Caught only by Arena Red Team verbatim diff (Phase 5 F-06 precedent) |

## 5. Constraints (V12 Sovereign Protocol — NON-NEGOTIABLE)

| # | Constraint | Source | Verification gate |
| --- | --- | --- | --- |
| C1 | Zero-allocation on hot path | User Q1.1 | No `new` of reference types inside `ManageTrailingStops` foreach body; no new heap-allocating LINQ in `ExecuteSmartDispatchEntry` fleet loop |
| C2 | Lock-free concurrency (no `lock(...)`) | `AGENTS.md` section 2 + `codex_rules.md` rule 8 | Hardened case-sensitive `lock\s*\(` audit returns 0 hits across `src/` |
| C3 | Pure ASCII C# string literals | `AGENTS.md` section 2 + `codex_rules.md` rule 9 | `python check_ascii.py` PASS on touched files |
| C4 | Surgical extraction -- no behavioral drift | User Q1.4 | Per-target CYC report + per-Print-string verbatim diff |
| C5 | Diff under 150 KB per PR | `AGENTS.md` Karpathy Surgical Changes | `git diff --stat HEAD` |
| C6 | Verbatim `Print` fidelity | Q4.D | `git diff` stripped of position/whitespace shows zero string-literal changes |
| C7 | CYC verification gate | Q4.E | `python scripts/csharp_hotspots.py` post-extraction shows: each new sub-handler under 20 CYC, each parent dispatcher under 30 CYC |
| C8 | Hard-link sync after every `src/` edit | `AGENTS.md` section 2 | `powershell -File .\deploy-sync.ps1` EXIT 0 |

## 6. Phase Numbering Decision

Per Q2.B alignment: this work is registered as **"Phase 6: Hot Path Execution Hardening"** -- a discrete milestone in `docs/brain/master_roadmap.md` bridging the existing M5 (Zero-Allocation Hot Path) and M7 (Concurrency Hardening). The architecture blueprint (`docs/architecture.md`) already references "Phase 5/6 Status" in its heatmap heading, and individual `src/` comments already use `// Phase 6 [...]` tags for FSM-P1, MG-D1, FSM-P2 -- so the naming is consistent with code in flight.

## 7. Ticket Granularity Decision

Per Q4 alignment: **C + D + E**, plus pre-merge roadmap step per A5.

- **(C) Concern-cluster tickets**: 4 (T1) + 1 (T2, lighter because the cluster is already mostly decomposed) + 4 (T3) = 9 surgical tickets, each scoped to keep PR diff under 150 KB.
- **(D) Cross-cutting verbatim-log fidelity ticket**: one ticket spanning all three targets.
- **(E) Cross-cutting CYC verification gate ticket**: post-extraction, runs `python scripts/csharp_hotspots.py` and asserts under 20 sub-handler / under 30 parent thresholds; bundles the architecture-doc heatmap refresh and symbol-name fix.
- **(F) Pre-merge roadmap row** (per A5 alignment): register Phase 6 in `docs/brain/master_roadmap.md` BEFORE the surgical tickets land.

**Final count: 12 tickets** (1 pre-flight + 9 surgical + 1 verbatim-Print + 1 final CYC/doc gate).

## 8. Acceptance (this Brief)

Code area is mapped (3 targets, physical file paths, line ranges, CYC scores).Stated problem is validated against code reality (3 discrepancies surfaced and resolved with the user).Scope boundaries (IN / OUT) confirmed via 4-question alignment session.Risk level marked CORE/HIGH with explicit blast-radius table.Constraints C1-C8 cite their source in the V12 Sovereign Protocol.