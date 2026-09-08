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
