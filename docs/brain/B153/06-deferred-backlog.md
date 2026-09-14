# Deferred Work Backlog

This file accumulates deferred work items across all blocks in the B153 pipeline.
Each block appends a new section. Prior sections are never removed.
Items are closed only when a subsequent block explicitly addresses them.

---

## Block B153 — Remove Hardcoded 3-Leg Ceiling

**Block ID:** B153
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-13
**Final verdict:** FINAL_PASS

### Block Summary

Removed four independent hardcoded 3-leg ceilings from bracket management paths
across three files (`PttQuickExit.cs`, `CopyEngine.cs`, `PttBreakEven.cs`).
Delivered via 4 tickets (DW-B153-01 through DW-B153-04), each targeting a convergence
point (JS-CONV-01). NT8 ATM templates now support N targets (tested to 22+) for all
QX, BE, stop drag, and BE-after-QX paths. DW-B107 and DW-B106 ceiling guards confirmed
CLOSED — `DeduplicateByPrice` is the active residue guard for both paths.

### Prior Block Items Carried Forward

No prior deferred-backlog file exists for blocks preceding B153.
DW-B107 and DW-B106 were tracked in the architecture plan (not as a prior 06-deferred-backlog.md entry);
both are confirmed CLOSED by this block.

| ID | Item | Prior Status | B153 Disposition |
|----|------|-------------|-----------------|
| DW-B107 | BE residue-protection ceiling guard (`while targets.Count > 3`) | OPEN (B107 era) | CLOSED — removed by DW-B153-02 |
| DW-B106 | QX residue-protection ceiling guard (`Math.Min(raw, 3)`) | OPEN (B106 era) | CLOSED — removed by DW-B153-01 |

### B153 Items (New This Block)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B153-05 | **Stale CYC annotation at `CopyEngine.cs` line 6675**: comment still reads `CYC<=4: IsFlat(1) + SnapshotBeTargets+while-cap(2) + targets==0 early return(3)`. The `while-cap` branch was removed by DW-B153-02; lizard-measured post-fix CCN=6. Annotation must be corrected to reflect actual post-fix CYC. | P2 | B154 or nearest src-touching block for `MoveStopToBreakEven` | OPEN |
| DW-B153-06 | **Orphaned dead code `UpdateLegTargetPrice` at `CopyEngine.cs` line 3445**: private method with `double[]` signature became unreachable after T3 replaced `CaptureOtherLegTargetPrices` with the `Dictionary<int,double>` implementation. Zero callers confirmed by full-codebase grep. Should be removed to eliminate confusion and dead surface area. | P2 | B154 or nearest src-touching block for stop-drag region in `CopyEngine.cs` | OPEN |
