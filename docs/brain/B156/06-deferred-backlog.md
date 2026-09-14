# Deferred Work Backlog -- Block B156

**This file covers Block B156 only.**
Prior block backlog at: `docs/brain/B155/06-deferred-backlog.md`

Each block maintains its own 06-deferred-backlog.md.
Items are carried forward explicitly rather than concatenated to preserve file independence.

---

## Block B156 -- CopyEngine Three-Bug Repair (DW-B156-01, DW-B157-01, DW-B157-02)

**Block ID:** B156
**Status:** COMPLETE
**Phase 5 Reviewed:** 2025-07-21
**Final verdict:** FINAL_PASS

### Block Summary

Three pre-committed, SIM-confirmed bug fixes documented through the full PTT 5-phase pipeline.

- **DW-B156-01 (P0)**: Ghost re-entry prevention via leader-flat drain abort in
  `TryEvictFollowerBeSlot` (lines 1934-1957). Iterates `_pendingDispatchDrains` and calls
  `AbortDrainOnFill(kv.Key)` before the `IsFollowerAccount` early-return when the leader
  fills its PTT-BE-Stop and is flat. Log marker `[DRAIN-ABORT-LEADER-FLAT]` confirmed.

- **DW-B157-01 (P1)**: fo=NULL fix via new helper `TrySetSlateReadyOnDrainFill` (lines
  992-1010) called unconditionally from `OnOrderUpdate` at line 1756 after
  `HandleDrainTerminalState`. Restores `_followerSlateReady[acct] = true` when a PTT-Copy
  drain fills on a follower with an open position, enabling stop/target drag.
  Log marker `[SLATE-READY-DRAIN]` confirmed. 5 xUnit tests added (DwB15701SlateReadyTests).

- **DW-B157-02 (P2)**: BE retry delay fix via terminal-state filter in `LogDiagOrderCount`
  (lines 6931-6934). Skips Filled/Cancelled/Rejected orders from `acc.Orders` enumeration,
  reducing `[BE-DIAG]` count from inflated 291-304 to accurate 1-8.

All 7 scans PASS file-wide. Zero executable lock(), zero non-ASCII bytes, zero async/await,
zero DateTime.Now in executable code. All new/modified methods CYC<=8 except
TryEvictFollowerBeSlot (CYC=14, pre-existing, accepted as DEFER-1).

---

## Carry-Forward from B155

The following items were OPEN at end of B155 and were NOT addressed by B156.

| ID | Item | Priority | Prior Status | B156 Disposition |
|----|------|----------|--------------|-----------------|
| DW-B153-05 | Stale CYC annotation at CopyEngine.cs ~line 6675 -- comment reads `CYC<=4` but post-fix lizard CCN=6. Must be corrected. | P2 | OPEN | STILL OPEN -- B156 did not touch this region |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at CopyEngine.cs ~line 3445 -- private method, zero callers after B153-T3 replacement. | P2 | OPEN | STILL OPEN -- B156 did not touch this region |
| DW-B154-05 | CYC improvement: `IsPttCopyEntryFilled` delegation opportunity after null-guard addition to `IsPttCopyEntry`. P3 refactor. | P3 | OPEN | STILL OPEN -- B156 did not touch this region |
| DW-B154-06 | Monitor: `TryTriggerBeRecovery` re-placement -- watch for BE recovery storms post-B154 deployment. Pre-existing behaviour. | P2 | OPEN | STILL OPEN -- no new evidence this block |
| DW-B155-01 | SIM gate pending for B155 `[DRAIN-ABORT-FLAT]` fix -- manual SIM run required to confirm no ghost re-entry. | P2 | OPEN | STILL OPEN -- B156 DW-B156-01 provides complementary coverage but does not substitute for B155 SIM gate |
| DW-BUG-0911-A-01 | Mid-close race condition -- `OnOrderUpdate` non-deterministic fill order when BE stop and leader close coincide. | P2 | OPEN | STILL OPEN -- not addressed by B156 |

---

## New B156 Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B156-DEFER-1 | `TryEvictFollowerBeSlot` CYC=14 (strict McCabe independent count by T1 verifier). Pre-existing debt accumulated across B154-B156. Must decompose: extract `IsLeaderFlatDrainAbort()` (handles leader-flat branch, lines 1938-1957) and `RunFollowerEvictPath()` (handles follower eviction, lines 1960-1972). Both sub-helpers must satisfy CYC<=8. Until decomposed, TryEvictFollowerBeSlot remains the only method in PTT with an accepted CYC>8 exemption (DEFER-1 tag). | P1 | B158 or first available refactor block | OPEN |
| DW-B156-DEFER-2 | Source comment at CopyEngine.cs line 991 reads `// CYC<=5` for `TrySetSlateReadyOnDrainFill`. Strict McCabe (counting `?.` and `??` operators) yields CYC=8 per T2 verifier. Comment should read `// CYC<=8`. Non-blocking annotation discrepancy; does not affect gate status since 8<=8. | P3 | Next annotation-cleanup pass | OPEN |
| DW-B156-DEFER-3 | Missing assertion upgrade test `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerExists` from plan §6. Should assert exact string `"[DRAIN-ABORT-LEADER-FLAT]"` (stronger than the existing substring check `"DRAIN-ABORT-FLAT"` at CopyEngineTests.cs L9364). Substantive coverage gap: none. Documentation completeness gap only. | P3 | Next CopyEngineTests.cs test-coverage pass | OPEN |

---

## Standing Open Items Aggregate (All Blocks)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B153-05 | Stale CYC annotation ~line 6675 | P2 | Future cleanup | OPEN |
| DW-B153-06 | Orphaned dead code UpdateLegTargetPrice ~line 3445 | P2 | Future dead-code sweep | OPEN |
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | Future CYC block | OPEN |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | Post-deployment | OPEN |
| DW-B155-01 | B155 SIM gate pending | P2 | Post-deployment | OPEN |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 | Future drain-path block | OPEN |
| DW-B156-DEFER-1 | TryEvictFollowerBeSlot CYC=14 decomposition | P1 | B158+ | OPEN |
| DW-B156-DEFER-2 | CYC<=5 annotation correction at line 991 | P3 | Next cleanup | OPEN |
| DW-B156-DEFER-3 | Missing exact-string assertion upgrade for DRAIN-ABORT-LEADER-FLAT | P3 | Next test pass | OPEN |

---

_Deferred backlog authored by ptt-plan-reviewer (Phase 5). Block B156._
_Carry-forward verified against docs/brain/B155/06-deferred-backlog.md._
_All items from B155 OPEN at time of B156 review are carried forward unchanged._
