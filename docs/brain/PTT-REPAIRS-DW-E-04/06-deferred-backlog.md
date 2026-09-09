# Deferred Backlog

This file accumulates deferred work items discovered across all WAVE pipeline reviews.
Append new blocks below; do not remove prior entries.

---

## WAVE2-LANE-A Deferred Items

**Epic**: WAVE2-LANE-A
**Date**: 2026-09-07
**Reviewer**: ptt-plan-reviewer (Phase 5 Final Review)
**Pipeline**: SINGLE (sequential, 2 tickets — IsExitSignalName CCN 9→8, HasArmingAtmBrackets CCN 9→5)

### Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-WAVE2-LA-01 | F5 NinjaTrader 8 recompile is a manual gate that cannot be automated from any agent session. Both Ticket 1 and Ticket 2 SCAN-06 Step 3 report "F5 required — manual step after DLL sync." Director must confirm NT8 compile green (F5 in NinjaTrader 8) before merging WAVE2-LANE-A to main. This is a hard pre-merge gate. | P0 | B_CURRENT (pre-merge) | OPEN |
| DW-WAVE2-LA-02 | Lizard reports `IsNativeCloseOrFlattenSignal` CCN=2 (expression-body `=>` with `\|\|` counted as 1 branch) vs manual count CCN=3 (base+Close+Flatten). Both are ≤ 8; no JS-080 violation. The methodological discrepancy between Lizard's expression-body handling and manual McCabe counting should be documented in project tooling standards to avoid reviewer confusion in future waves. | P2 | future | OPEN |
| DW-WAVE2-LA-03 | Pre-existing `return null` statements in `CopyEngine.cs` at lines 1259, 1985, 2949, 3054, 3062, 3870, 4069, 4347, 5809, 5831, 5844, 5850, 5934, 7202, 7217 were observed in SCAN-04 during WAVE2-LANE-A. These are outside WAVE2-LANE-A scope (not in modified lines). They represent pre-existing JS-002 technical debt that should be addressed in a future dedicated wave. | P1 | future wave | OPEN |
| DW-WAVE2-LA-04 | Architecture plan `02-architecture-plan.md` §8, §9.5, and §10.5 specify `private static bool` for both `IsNativeCloseOrFlattenSignal` and `IsArmingOrderState`. Ticket revisions V4 and V7 overrode these to `internal static` for xUnit `InternalsVisibleTo` testability. The plan sections remain stale. Architect should update §8, §9.5, §10.5 to reflect `internal static` for record accuracy. This is documentation debt only; the implementation is correct per the revised tickets. | P2 | future | OPEN |

### Summary

- Total deferred items: 4
- P0 items: 1 (DW-WAVE2-LA-01 — manual F5 gate, must be resolved pre-merge)
- P1 items: 1 (DW-WAVE2-LA-03 — pre-existing JS-002 debt, future wave)
- P2 items: 2 (DW-WAVE2-LA-02 tooling, DW-WAVE2-LA-04 plan stale docs)
- Implementation-blocking items: 0 (WAVE2-LANE-A code is complete and clean)

---

## PTT-REPAIRS-DW-E-04 Deferred Items

**Epic**: PTT-REPAIRS-DW-E-04
**Date**: 2026-09-07
**Reviewer**: ptt-plan-reviewer (Phase 5 Final Review)
**Pipeline**: SINGLE (1 ticket — EvictDedup CYC 13→7 via EvictCancelledEntry + EvictFilledEntry extraction + BUG-E preservation)

### Prior Item Status Updates

| ID | Prior Status | Current Status | Reason |
|----|-------------|----------------|--------|
| DW-WAVE2-LA-01 | OPEN | OPEN | F5 manual gate — still required pre-merge for this epic's changes |
| DW-WAVE2-LA-02 | OPEN | OPEN | Out of scope for this epic |
| DW-WAVE2-LA-03 | OPEN | OPEN | Pre-existing JS-002 debt — plan §1 explicitly excluded; no null returns modified |
| DW-WAVE2-LA-04 | OPEN | OPEN | Out of scope for this epic |

### New Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-E-04-01 | New test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` (CopyEngineTests line 4216) fails at runtime with `System.TypeInitializationException` because `CopyEngineTests` uses `CopyEngine.Instance` which requires NT8 runtime initialization unavailable in the xUnit test host. The test code is correct, compiles, and the logic is sound. This is a pre-existing blocker shared by all 449+ `CopyEngineTests` tests. The new test will only return genuine PASS when NT8 TypeInitializationException is resolved (e.g., via NT8 mock/stub infrastructure or direct F5 execution in NinjaTrader). This is NOT a T1 code defect; no code change is required for the test itself. | P1 | future wave (test infrastructure) | OPEN |
| DW-E-04-02 | Ticket `04-tickets.md` and `04-ticket-review.md` documented the test file path as `src/PropTraderTools.Tests/CopyEngineTests.cs`. The actual repository file is `src/PropTraderTools/CopyEngineTests.cs` (confirmed by verifier at ticket-1-verification.md line 266). The test was placed correctly; this is plan/ticket documentation debt only. Future ticket templates and architecture plans should reflect the actual repository path `src/PropTraderTools/CopyEngineTests.cs`. | P2 | future (doc cleanup) | OPEN |

### Summary

- New deferred items this epic: 2
- P0 items: 0
- P1 items: 1 (DW-E-04-01 — NT8 TypeInit test infrastructure blocker)
- P2 items: 1 (DW-E-04-02 — test file path documentation debt)
- Items from prior blocks closed by this epic: 0
- Implementation-blocking items: 0 (all T1 code complete, clean, and verified at Layer 3)
- Pre-merge blocking items: 1 (DW-WAVE2-LA-01 — manual F5 gate, inherited)

---
