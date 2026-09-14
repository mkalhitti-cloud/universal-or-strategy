# Deferred Work Backlog — Block B155

**This file covers Block B155 only.**
Prior block backlog at: `docs/brain/B154/06-deferred-backlog.md`

Each block maintains its own 06-deferred-backlog.md.
Items are carried forward explicitly rather than concatenated to preserve file independence.

---

## Carry-Forward from B154

The following items were OPEN at end of B154 and were NOT addressed by B155.

| ID | Item | Priority | Prior Status | B155 Disposition |
|----|------|----------|--------------|-----------------|
| DW-B153-05 | Stale CYC annotation at `CopyEngine.cs` ~line 6675 — comment still reads `CYC<=4: IsFlat(1) + SnapshotBeTargets+while-cap(2) + targets==0 early return(3)`. The `while-cap` branch was removed by DW-B153-02; lizard-measured post-fix CCN=6. Annotation must be corrected to reflect actual post-fix CYC. | P2 | OPEN | STILL OPEN — B155 did not touch these items |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at `CopyEngine.cs` ~line 3445 — private method with `double[]` signature became unreachable after T3 replaced `CaptureOtherLegTargetPrices` with the `Dictionary<int,double>` implementation. Zero callers confirmed by full-codebase grep. | P2 | OPEN | STILL OPEN — B155 did not touch these items |
| DW-B154-05 | CYC improvement: `IsPttCopyEntryFilled` CYC=4 inline used (not CYC=3 `IsPttCopyEntry` delegation). Calling `IsPttCopyEntry` sub-check (line 7819) would reduce lizard CCN if null-safety is first added to `IsPttCopyEntry` (add null-check on `o.Name` before `StartsWith` call). Future refactor opportunity — add null guard to `IsPttCopyEntry`, then delegate from `IsPttCopyEntryFilled` for CYC reduction from CCN=7 to CCN<=5. | P3 | OPEN | STILL OPEN — B155 did not touch these items |
| DW-B154-06 | Monitor: `TryTriggerBeRecovery` re-placement after B154 sweep — if BE recovery storms are observed post-B154 deployment, review whether `TryTriggerBeRecovery` re-placements after the B154 stale-order sweep are causing double-bracket placements. Scenario: cancel ack from prior-trade PTT-BE-Stop arrives after new entry fill; `TryTriggerBeRecovery` (fires on Cancelled) re-places BE brackets for new position. Pre-existing behaviour; B154 does not introduce it. | P2 | OPEN | STILL OPEN — B155 did not touch these items |

---

## Block B155 — Abort stale drain payload on position-flat (DW-B154-04)

**Block ID:** B155
**Status:** COMPLETE
**Phase 5 Reviewed:** 2025
**Final verdict:** FINAL_PASS

### Block Summary

DW-B154-04 closed. Drain payload abort on position-flat implemented via
(a) `IsFlat` guard in `SubmitDrainedEntry` (`CopyEngine.cs:8246`) — CYC delta +1 (lizard 3→4), catches
all paths (countdown via `OnDrainCancelAck` and 2-second `TryDrainWatchdog` timeout) before
`SubmitEntryDirect` fires; (b) `AbortDrainOnFill(accName)` call in `TryEvictFollowerBeSlot`
(`CopyEngine.cs:1912`) — CYC delta 0, proactive early abort when BE-stop fill flattens position,
fires before `HandleDrainTerminalState` in `OnOrderUpdate` call order.
`DrainedOrderIds` cleanup confirmed on BOTH abort path and submit path.
`OnDrainCancelAck` and `TryDrainWatchdog` untouched by design.
`SubmitDrainedEntryTestable` seam added as `internal` at `CopyEngine.cs:7917`.
3 new `[Fact]` tests added in `B155ContractTests` (`CopyEngineTests.cs:9341`).
All 8 scans pass. Zero lock(), zero non-ASCII, 0 build errors, 0 new CCN>8 violations.
SIM gate deferred as DW-B155-01 (P2, post-deployment).

---

## New B155 Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B155-01 | SIM gate pending — field SIM verification with `[DRAIN-ABORT-FLAT]` log check and no-ghost-re-entry confirmation has NOT been run by automated pipeline. Requires manual SIM run per `04-tickets.md` Verify steps 1–6: enter trade → BE ALL → wait flat → confirm no new entry in 3 seconds → confirm `[DRAIN-ABORT-FLAT] acct=<acctName>` in Output tab → confirm no `[DRAIN-TIMEOUT-SUBMIT]` for flat account. | P2 | Post-deployment | OPEN |

---

## Standing Open Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BUG-0911-A-01 | Mid-close race condition — `OnOrderUpdate` may receive fill events in non-deterministic order when BE stop fills coincide with leader close. Not addressed in B155. | P2 | Future drain-path block | OPEN |
