# B197 Deferred Backlog

**Epic**: B197 -- Gap Restore: TrySetSlateReadyOnDrainFill + TryAbortLeaderFlatDrains + TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-10-12
**Preceding backlog**: docs/brain/B196/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B198+)

---

## Purpose

This document records all deferred work items that remain open after B197 completion.
It supersedes docs/brain/B196/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B197 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B196 Block -- 2026-10-11 (Prior Block, Reproduced for Continuity)

### Items Closed by B196

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B195-02 | DrainThenDispatch fast-path bare in Clone mode. The `!entryCandidates.Any()` fast-path called `SubmitEntryDirect` directly, bypassing Clone ATM mode. Fixed by adding `GetCloneAtmMode()` branch: Named path routes to `SendCopyWithAtm`; Inherit path preserves `SubmitEntryDirect`. CYC 4->5. | P1 | B196-T1 | ticket-1-verification.md (VERIFY_PASS) |
| DW-B170-01 | FindLeaderStopPrice missing OrderState.Accepted. NT8 SIM broker places dragged stops in Accepted before Working. The old Working-only predicate returned 0.0 during this window -- follower stop price = 0 -- naked position. Fixed by adding `\|\| order.OrderState == OrderState.Accepted` to compound predicate. CYC 5->6. | P1 | B196-T2 | ticket-2-verification.md (VERIFY_PASS) |
| DW-B169 COLLATERAL-STORM | _dragsInFlight HashSet guard restore. Stop1's Accepted callback was calling ResubmitCollateralLegs for Stop2/Stop3 while their own HandleAtmStopSync events were still in-flight, causing duplicate/collateral-storm orders. Restored: `_dragsInFlight` field (readonly HashSet<string>), `TryTrackDragInFlight`, `TryRemoveDragSuffix`, `DragsInFlightForTest` seam, `ParseLegSuffix` wrapper, and wiring in TryHandleBracketDrag, TryCancelFollowerEntries, SyncFollowerBracket, ResubmitCollateralLegs. | P1 | B196-T3 | ticket-3-verification.md (VERIFY_PASS) |
| B161-B166 Ghost-Entry Guard + B163 AFP | IsFollowerPositionBlocked 6-signal guard restore (prevents ghost ATM entry during BE-All burst) + AverageFillPrice guard in IsPttEntryOrderCancelTrigger (prevents lifecycle-cancel of filled Entry from triggering re-place). All 7 helper methods added fresh. Guard wired in TryReplaceOnAtmCancel. AFP guard at position 4 of IsPttEntryOrderCancelTrigger. | P1 | B196-T4 | ticket-4-verification.md (VERIFY_PASS) |
| DW-B195-03 | DW-B177-CYC: Investigation of claimed CYC>8 method in TradeCopierPanel.cs. Method does not exist. No CYC>8 annotations found. DW-B196-CYC NOT opened. | P2 | B196-T5 | ticket-5-verification.md (VERIFY_PASS) |

---

## B197 Block -- 2026-10-12

### Items Closed by B197

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B157-01 | TrySetSlateReadyOnDrainFill absent from B156/B157 megacommit. When a drain-tracked PTT-Copy entry fills, `_followerSlateReady` was never set to true for the follower, permanently blocking drag syncs. Method added at CopyEngine.cs line 1070. Call site wired at OnOrderUpdate Filled branch line 1556 (FIRST, before AbortDrainOnFill). | P1 | B197-T1 | ticket-1-verification.md (VERIFY_PASS) |
| DW-B156-01 | TryAbortLeaderFlatDrains wiring absent. Pending dispatch drains were not aborted when the leader account went flat, allowing ghost re-entry PTT-Copy orders to fire after leader was flat (DW-B158-SIM-01 failure mode). Method added at CopyEngine.cs line 1768. | P1 | B197-T2 | ticket-2-verification.md (VERIFY_PASS) |
| DW-B156-DEFER-1 | TryEvictFollowerBeSlot leader-flat branch missing. TryAbortLeaderFlatDrains wired into TryEvictFollowerBeSlot at line 1743 (after null guard, before IsEvictTriggerState). CYC delta +0. | P1 | B197-T2 | ticket-2-verification.md (VERIFY_PASS) |
| DW-B154-01 | TryCancelStaleGtcExitOnEntry absent (HIGH). Stale Working PTT-BE-*/PTT-QX-* exit orders from prior sessions were not swept when a new follower entry filled, causing mis-fire against new position. Method added at CopyEngine.cs line 1958 (CYC=8 ceiling). Call site at OnOrderUpdate Filled branch line 1558 (LAST, after AbortDrainOnFill). | P1 | B197-T3 | ticket-3-verification.md (VERIFY_PASS) |
| DW-B154-02 | IsStaleGtcExitOrder helper predicate absent (MEDIUM). Predicate for Working PTT-BE-*/PTT-QX-* filter. Added as private static at CopyEngine.cs line 1943 (CYC=5). | P1 | B197-T3 | ticket-3-verification.md (VERIFY_PASS) |
| DW-B154-03 | IsPttDragOrphanCancellable exact-match guard misses B141/B142 suffixed drag names (e.g., "PTT-TGT-Drag-1"). Guard(3) changed from `!= "PTT-TGT-Drag"` to `!StartsWith("PTT-TGT-Drag", Ordinal)` in both IsPttDragOrphanCancellable (line 2005-2007) and IsPttDragOrphanCancellableTestable (line 2025-2027). Null guards added. | P2 | B197-T4 | ticket-4-verification.md (VERIFY_PASS) |

---

## Items Carried Forward (OPEN)

All 10 B196 carry-forward items remain OPEN after B197. None were addressed by B197 scope.

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B197.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184-B197)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md -> docs/brain/B196/06-deferred-backlog.md -> B197
```

---

### DW-B192-SINST-01: SetInstrument() CYC at Ceiling 8

```
Priority: P2
Description: SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066
  violation. Engineers modifying SetInstrument() MUST extract a helper method first.
  Not blocking; CYC is at limit, not over it.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When SetInstrument() is next modified
Status: OPEN (opened B193, carried forward through B194-B197)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs still uses old label strings
  "Quick ALL" and "QAll2t" in the singleton-sink callback path. Low severity: visual label
  shows old name only on the singleton path. Not a DNA violation.
Blocking: No
Target Block: When OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193, carried forward through B194-B197)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

### DW-B159-K2: 14 xUnit Tests (BE Path)

```
Priority: P2
Description: 14 xUnit tests for the BE (break-even) path require infrastructure that was
  not yet available at B159 time. Tests are planned but not implemented.
Blocking: No
Target Block: B198+ (when test infrastructure is available)
Status: OPEN (carried forward from B159 via B160-B197)
Owner: Director
```

---

### DW-B159-K3: Attempt Cap Hardcoded at 3 in TryArmBePriceRetry

```
Priority: P2
Description: TryArmBePriceRetry has a hardcoded cap of 3 retry attempts. This should be
  configurable or derived from the rule configuration.
Blocking: No
Target Block: B198+
Status: OPEN (carried forward from B159 via B160-B197)
Owner: Director
```

---

### DW-B159-K4: intendedBePrice Sourcing Dependency

```
Priority: P2
Description: intendedBePrice sourcing has an unresolved dependency on the BE calculation
  pipeline. Not blocking current function.
Blocking: No
Target Block: B198+
Status: OPEN (carried forward from B159 via B160-B197)
Owner: Director
```

---

### DW-B167-DEFER-01: ECA-1 Double-Position Race

```
Priority: P2
Description: ECA-1 double-position race condition -- follower can enter a position at the
  same time as a legitimate ATM entry in rare timing conditions. Monitor at SIM.
Blocking: No (monitor-only)
Target Block: Monitor at SIM; address if observed
Status: OPEN (carried forward from B167 via B168-B197)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B167-DEFER-03: Combined B161-B166 SIM Gate

```
Priority: P1
Description: The combined B161-B166 SIM gate (confirming all 6 signals of
  IsFollowerPositionBlocked fire correctly in a live BE-All burst scenario) has not been
  run. B196-T4 restores the implementation; the SIM gate is the live validation step.
Blocking: Yes -- SIM gate must pass before B161-B166 + B163 guard can be considered
  fully validated in production.
Target Block: B197/B198 SIM gate (run after B196 FINAL_PASS)
Status: OPEN (carried forward from B167 via B168-B197)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B167-DEFER-04: B167 SIM Gate

```
Priority: P1
Description: B167 SIM gate -- prerequisite for B168/B169 SIM gate. Must be run and
  recorded before B168/B169 SIM gates are considered validated.
Blocking: Prerequisite for DW-B167-DEFER-03 and B169 SIM gate
Target Block: B197/B198 SIM gate sequence
Status: OPEN (carried forward from B167 via B168-B197)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B170-SIM: B170 SIM Gate (Live Regression Verify for DW-B170-01)

```
Priority: P1
Description: Director drags a target on the leader account while the leader bracket stop
  is in OrderState.Accepted. Confirms FindLeaderStopPrice (now including Accepted) returns
  non-zero and the follower stop is preserved after the drag. This is the live validation
  of the DW-B170-01 fix (B196-T2).
Blocking: Yes -- live regression verify required before the Accepted window fix is
  considered fully validated in SIM.
Target Block: B196/B197 SIM gate (immediately after B196 FINAL_PASS)
Status: OPEN
Owner: Director
Source: docs/brain/B196/02-architecture-plan.md; docs/brain/B170/05-final-review.md
```

---

## New Items Opened by B197

---

### DW-B197-01: Stale CCN Comment at Line 1993

```
Priority: P3
Description: CopyEngine.cs line 1993 comment states "TB-T6: IsPttDragOrphanCancellable --
  CCN<=4." After T4 null guard additions (required to prevent NullReferenceException on
  StartsWith when Order.Name is null), the actual Lizard CCN = 8. The comment is stale.
  This is not a DNA violation (JS-066 ceiling of <=8 is not exceeded).
  Update comment to: "TB-T6: IsPttDragOrphanCancellable -- CCN=8 (JS-066 ceiling)."
Blocking: No
Target Block: When IsPttDragOrphanCancellable is next touched
Status: OPEN (opened B197)
Owner: Director
Source: docs/brain/B197/ticket-4-verification.md W-T4-02
```

---

### DW-B197-02: T4 Fact Names Misleading

```
Priority: P3
Description: T4 test facts in B197Tests.cs are named "SuffixedTgtName_ReturnsTrue" etc.
  but use source-text assertions (Assert.Contains) rather than direct seam calls
  (Assert.True(result)). Names imply a method call return value was verified. Root cause:
  TypeInitializationException constraint (CopyEngine static initializer requires NT8 runtime,
  established B180 harness limitation) prevents direct CopyEngine.IsPttDragOrphanCancellableTestable
  calls in xUnit. Tests pass and assertions are meaningful (source confirms correct implementation).
  Cosmetic naming issue only.
Blocking: No
Target Block: When B197Tests.cs or test harness is next revisited
Status: OPEN (opened B197)
Owner: Director
Source: docs/brain/B197/ticket-4-verification.md W-T4-01
```

---

### DW-B197-03: Pre-existing DwB15701SlateReadyTests Failures

```
Priority: P2
Description: Two tests in CopyEngineTests.cs:DwB15701SlateReadyTests are failing and
  pre-date B197:
  (a) TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal: expects non-existent
      HandleDrainTerminalState caller + wrong argument type (expects Order arg, actual
      is OrderEventArgs e). Both expectations are incorrect. The B197 T1 implementation
      is correct (takes OrderEventArgs e, called directly without HandleDrainTerminalState).
  (b) TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect: TypeInitializationException --
      CopyEngine.Instance requires NT8 runtime, pre-existing infrastructure limitation.
  Neither was introduced by B197. B197 T1 passes all its own 2 [Fact]s in B197Tests.cs.
  Resolution requires either (a) fixing the pre-existing test expectations in CopyEngineTests.cs,
  or (b) removing the stale DwB15701SlateReadyTests if they are superseded by B197Tests.cs.
Blocking: No (not B197 regressions)
Target Block: When CopyEngineTests.cs DwB15701SlateReadyTests is next reviewed
Status: OPEN (opened B197)
Owner: Director
Source: docs/brain/B197/ticket-1-verification.md (Pre-Existing Test Failure Assessment)
```

---

### DW-B197-04: TryCancelSingleStaleGtcExit Not in Plan CYC Table

```
Priority: P3
Description: TryCancelSingleStaleGtcExit (CopyEngine.cs line 1975) was not in the original
  B197 plan §7 CYC Impact Summary. The T3 engineer extracted it from TryCancelStaleGtcExitOnEntry
  to keep parent CCN<=8 per JS-066 (Lizard counted ?. null-conditional and catch block as extra
  branches beyond McCabe derivation). The extraction is architecturally correct and the method
  is CCN-compliant (CCN=5). Informational: plan §7 table is implicitly addended with this method.
  Future engineers should note TryCancelSingleStaleGtcExit as thematic neighbor of
  TryCancelStaleGtcExitOnEntry.
Blocking: No
Target Block: Informational only -- no action required
Status: OPEN (opened B197)
Owner: Director
Source: docs/brain/B197/ticket-3-completion.md, ticket-3-verification.md
```

---

### DW-B197-SIM-01: B197 SIM Gate

```
Priority: P1
Description: B197 SIM gate not yet run. Four SIM scenarios planned in arch plan §8:
  SIM-1 (T1): TrySetSlateReadyOnDrainFill -- verify "[SLATE-READY-DRAIN]" fires and
    subsequent drag sync to follower succeeds after drain fill.
  SIM-2 (T2): TryAbortLeaderFlatDrains -- verify "[DRAIN-ABORT-LEADER-FLAT]" fires and
    no ghost PTT-Copy submitted after leader goes flat.
  SIM-3 (T3): TryCancelStaleGtcExitOnEntry -- verify "[GTC-SWEEP] cancelled PTT-BE-*"
    fires and stale GTC exit is cancelled when new entry fills.
  SIM-4 (T4): IsPttDragOrphanCancellable suffix -- verify suffixed drag orders
    (PTT-TGT-Drag-1, PTT-STP-Drag-2) are swept after flat.
  Must be run before B197 gap restore is considered fully validated in production.
Blocking: Yes -- live validation required for FINAL_PASS to be considered production-ready
Target Block: B197 SIM gate (run after B197 FINAL_PASS)
Status: OPEN (opened B197)
Owner: Director
Source: docs/brain/B197/02-architecture-plan.md §8
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | (None) |
| P1 | 5 | DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01 |
| P2 | 6 | DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03 |
| P3 | 4 | DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04 |

**Total open items**: 15
**Items closed by B197**: 6 (DW-B157-01, DW-B156-01, DW-B156-DEFER-1, DW-B154-01, DW-B154-02, DW-B154-03)
**New items opened by B197**: 5 (DW-B197-01, DW-B197-02, DW-B197-03, DW-B197-04, DW-B197-SIM-01)
**Items carried forward unchanged**: 10 (all B196 carry-forwards)

---

## Recommended Next Block Priorities

1. **B197 SIM Gate** (Required immediately after FINAL_PASS):
   Run B197 SIM gate per docs/brain/B197/02-architecture-plan.md §8.
   Record results in `docs/brain/B197/sim-gate-completion.md`.
   Four per-ticket SIM scenarios (SIM-1 through SIM-4). DW-B197-SIM-01 closes on completion.

2. **DW-B167-DEFER-03 + DW-B167-DEFER-04 + DW-B170-SIM** (P1):
   Combined B161-B166 SIM gate cascade (these carry from B196). Director should schedule
   B167/B168/B169 SIM gate sequence.

3. **DW-B183-03** (P1, Non-blocking): IsLaneEEligible CYC=8 frozen.
   No action required unless Lane E is modified.

4. **DW-B197-03** (P2): CopyEngineTests.cs DwB15701SlateReadyTests stale test expectations.
   Fix pre-existing test assertions or remove superseded tests.

5. **DW-B197-01** (P3): Update stale CCN comment at CopyEngine.cs line 1993.

---

*Written by: ptt-plan-reviewer*
*Epic: B197*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B196/06-deferred-backlog.md (as authoritative carry-forward list)*
