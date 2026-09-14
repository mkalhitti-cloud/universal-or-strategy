# Deferred Work Backlog — Block B154

**This file covers Block B154 only.**
Prior block backlog remains at: `docs/brain/B153/06-deferred-backlog.md`

Each block maintains its own 06-deferred-backlog.md.
Items are carried forward explicitly rather than concatenated to preserve file independence.

---

## Carry-Forward from B153

The following items were OPEN at end of B153 and were NOT addressed by B154.

| ID | Item | Priority | Prior Status | B154 Disposition |
|----|------|----------|-------------|-----------------|
| DW-B153-05 | Stale CYC annotation at `CopyEngine.cs` ~line 6675 — comment still reads `CYC<=4: IsFlat(1) + SnapshotBeTargets+while-cap(2) + targets==0 early return(3)`. The `while-cap` branch was removed by DW-B153-02; lizard-measured post-fix CCN=6. Annotation must be corrected to reflect actual post-fix CYC. | P2 | OPEN | STILL OPEN — B154 touched `CopyEngine.cs`; engineer did not correct this annotation |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at `CopyEngine.cs` ~line 3445 — private method with `double[]` signature became unreachable after T3 replaced `CaptureOtherLegTargetPrices` with the `Dictionary<int,double>` implementation. Zero callers confirmed by full-codebase grep. | P2 | OPEN | STILL OPEN — not addressed in B154 |

---

## Block B154 — GTC Exit Order Survival / Drag Orphan Suffix Fix

**Block ID:** B154
**Status:** COMPLETE
**Phase 5 Reviewed:** 2025
**Final verdict:** FINAL_PASS

### Block Summary

Swept stale PTT-BE-* and PTT-QX-* GTC exit orders on new entry fill via
`TryCancelStaleGtcExitOnEntry` (DW-B154-01, DW-B154-02, HIGH+MEDIUM).
Fixed `IsPttDragOrphanCancellable` / `IsPttDragOrphanCancellableTestable` to use
`StartsWith(Ordinal)` instead of exact equality, enabling suffixed drag order names
(PTT-TGT-Drag-N, PTT-STP-Drag-N) to be matched by the orphan sweep (DW-B154-03, LOW).
12 new tests added across B154T1ContractTests and B154T2ContractTests. 192 total passing,
0 failures, 0 new lizard CCN warnings. DW-B154-04 (PTT-Copy late fills) remains deferred.

### New B154 Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B154-04 | PTT-Copy / Entry late fills (drain issue) — deferred, out of scope for B154. New entry fill + simultaneous drain may cause late copy-entry fill to be missed. Investigate in drain-path context. | P2 | B155 or nearest drain-path block | OPEN |
| DW-B154-05 | CYC improvement: `IsPttCopyEntryFilled` CYC=4 inline used (not CYC=3 IsPttCopyEntry delegation). Calling `IsPttCopyEntry` sub-check (line 7819) would reduce lizard CCN if null-safety is first added to `IsPttCopyEntry` (add null-check on `o.Name` before `StartsWith` call). Future refactor opportunity — add null guard to `IsPttCopyEntry`, then delegate from `IsPttCopyEntryFilled` for CYC reduction from CCN=7 to CCN<=5. | P3 | Future src-touching block | OPEN |
| DW-B154-06 | Monitor: `TryTriggerBeRecovery` re-placement after B154 sweep — if BE recovery storms are observed post-B154 deployment, review whether `TryTriggerBeRecovery` re-placements after the B154 stale-order sweep are causing double-bracket placements. Scenario: cancel ack from prior-trade PTT-BE-Stop arrives after new entry fill; `TryTriggerBeRecovery` (fires on Cancelled) re-places BE brackets for new position. Pre-existing behaviour; B154 does not introduce it. | P2 | Post-deployment observation | OPEN |
