# B196 Deferred Backlog

**Epic**: B196 — Gap Restore: DW-B195-02 + DW-B170-01 + DW-B169 COLLATERAL-STORM + B161-B166 Ghost-Entry Guard + B163 AFP Guard
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-10-11
**Preceding backlog**: docs/brain/B194/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B197+)

---

## Purpose

This document records all deferred work items that remain open after B196 completion.
It supersedes docs/brain/B194/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B196 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B196 Block — 2026-10-11

### Items Closed by B196

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B195-02 | DrainThenDispatch fast-path bare in Clone mode. The `!entryCandidates.Any()` fast-path called `SubmitEntryDirect` directly, bypassing Clone ATM mode. Fixed by adding `GetCloneAtmMode()` branch: Named path routes to `SendCopyWithAtm`; Inherit path preserves `SubmitEntryDirect`. CYC 4→5. | P1 | B196-T1 | ticket-1-verification.md (VERIFY_PASS) |
| DW-B170-01 | FindLeaderStopPrice missing OrderState.Accepted. NT8 SIM broker places dragged stops in Accepted before Working. The old Working-only predicate returned 0.0 during this window → follower stop price = 0 → naked position. Fixed by adding `|| order.OrderState == OrderState.Accepted` to compound predicate. CYC 5→6. | P1 | B196-T2 | ticket-2-verification.md (VERIFY_PASS) |
| DW-B169 COLLATERAL-STORM | _dragsInFlight HashSet guard restore. Stop1's Accepted callback was calling ResubmitCollateralLegs for Stop2/Stop3 while their own HandleAtmStopSync events were still in-flight, causing duplicate/collateral-storm orders. Restored: `_dragsInFlight` field (readonly HashSet<string>), `TryTrackDragInFlight`, `TryRemoveDragSuffix`, `DragsInFlightForTest` seam, `ParseLegSuffix` wrapper, and wiring in TryHandleBracketDrag, TryCancelFollowerEntries, SyncFollowerBracket, ResubmitCollateralLegs. | P1 | B196-T3 | ticket-3-verification.md (VERIFY_PASS) |
| B161-B166 Ghost-Entry Guard + B163 AFP | IsFollowerPositionBlocked 6-signal guard restore (prevents ghost ATM entry during BE-All burst) + AverageFillPrice guard in IsPttEntryOrderCancelTrigger (prevents lifecycle-cancel of filled Entry from triggering re-place). All 7 helper methods added fresh (IsBeSlotNonTerminal, IsBeRetryArmedFlat, IsBeSettleOrRetrySignalActive, HasActivePttBeExits, HasActivePttBeTargets, HasSettlingBeTargets, IsFollowerPositionBlocked). Guard wired in TryReplaceOnAtmCancel. AFP guard at position 4 of IsPttEntryOrderCancelTrigger. | P1 | B196-T4 | ticket-4-verification.md (VERIFY_PASS) |
| DW-B195-03 | DW-B177-CYC: Investigation of claimed CYC>8 method OnAtrBoxPreviewKeyDown in TradeCopierPanel.cs. Method does not exist. No CYC>8 annotations found in TradeCopierPanel.cs (max CYC=6) or CopyEngine.cs (max CYC=8 at ceiling). DW-B196-CYC NOT opened. | P2 | B196-T5 | ticket-5-verification.md (VERIFY_PASS) |

---

## Items Carried Forward (OPEN)

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B196.
  Future engineers modifying Lane E eligibility MUST extract a helper method first
  (using the same pattern as IsBracketStopOrder extraction performed in B194-T2).
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184-B196)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md -> docs/brain/B194/06-deferred-backlog.md -> B196
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
Status: OPEN (opened B193, carried forward through B194-B196)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs still uses old label strings
  "Quick ALL" and "QAll2t" in the singleton-sink callback path. The per-panel Up/Down path
  correctly uses "3x All" / "2x All" after B193-DIRECT-3. Low severity: visual label shows
  old name only on the singleton path. Not a DNA violation.
Blocking: No
Target Block: When OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193, carried forward through B194-B196)
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
Target Block: B197+ (when test infrastructure is available)
Status: OPEN (carried forward from B159 via B160-B196)
Owner: Director
```

---

### DW-B159-K3: Attempt Cap Hardcoded at 3 in TryArmBePriceRetry

```
Priority: P2
Description: TryArmBePriceRetry has a hardcoded cap of 3 retry attempts. This should be
  configurable or derived from the rule configuration.
Blocking: No
Target Block: B197+
Status: OPEN (carried forward from B159 via B160-B196)
Owner: Director
```

---

### DW-B159-K4: intendedBePrice Sourcing Dependency

```
Priority: P2
Description: intendedBePrice sourcing has an unresolved dependency on the BE calculation
  pipeline. Not blocking current function.
Blocking: No
Target Block: B197+
Status: OPEN (carried forward from B159 via B160-B196)
Owner: Director
```

---

### DW-B167-DEFER-01: ECA-1 Double-Position Race

```
Priority: P2
Description: ECA-1 double-position race condition — follower can enter a position at the
  same time as a legitimate ATM entry in rare timing conditions. Monitor at SIM.
Blocking: No (monitor-only)
Target Block: Monitor at SIM; address if observed
Status: OPEN (carried forward from B167 via B168-B196)
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
Blocking: Yes — SIM gate must pass before B161-B166 + B163 guard can be considered
  fully validated in production.
Target Block: B197 SIM gate (run after B196 FINAL_PASS)
Status: OPEN (carried forward from B167 via B168-B196)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B167-DEFER-04: B167 SIM Gate

```
Priority: P1
Description: B167 SIM gate — prerequisite for B168/B169 SIM gate. Must be run and
  recorded before B168/B169 SIM gates are considered validated.
Blocking: Prerequisite for DW-B167-DEFER-03 and B169 SIM gate
Target Block: B197 SIM gate sequence
Status: OPEN (carried forward from B167 via B168-B196)
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
  Was OPEN in B170/05-final-review.md Section K but was not carried through B171-B194
  backlogs. Reactivated in B196 plan §8.
Blocking: Yes — live regression verify required before the Accepted window fix is
  considered fully validated in SIM.
Target Block: B196 SIM gate (immediately after B196 FINAL_PASS)
Status: OPEN
Owner: Director
Source: docs/brain/B196/02-architecture-plan.md §8; docs/brain/B170/05-final-review.md §K
```

---

## New Items Opened by B196

**None.**

All engineering decision deviations (OrderState.Rejected substitution, IsPttBeStopActiveState addition,
_beReplaceAttempts field name correction, ParseLegSuffix wrapper) were resolved at implementation
time by the engineer and independently confirmed by the verifier. No follow-up DW cards are required.

DW-B196-CYC: NOT opened. T5 investigation confirmed zero CYC>8 methods in either file.

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | (None) |
| P1 | 4 | DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM |
| P2 | 5 | DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01 |
| P3 | 1 | DW-B193-01 |

**Total open items**: 10
**Items closed by B196**: 5 (DW-B195-02, DW-B170-01, DW-B169 COLLATERAL-STORM, B161-B166+B163, DW-B195-03)
**New items opened by B196**: 0
**Items carried forward unchanged**: 10 (DW-B183-03, DW-B192-SINST-01, DW-B193-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM)

---

## Recommended Next Block Priorities

1. **B196 SIM Gate** (Required immediately after FINAL_PASS):
   Run B196 SIM gate per docs/brain/B196/02-architecture-plan.md §9.
   Record results in `docs/brain/B196/sim-gate-completion.md`.
   Four per-ticket SIM scenarios (T1-T4). DW-B170-SIM closes with T2 SIM step.

2. **DW-B167-DEFER-03 + DW-B167-DEFER-04** (P1): Combined B161-B166 and B167 SIM gates.
   These must be run as part of the B196 SIM gate sequence (T4 SIM criteria directly addresses
   DW-B167-DEFER-03). Director should schedule B167/B168/B169 SIM gate cascade.

3. **DW-B183-03** (P1, Non-blocking): IsLaneEEligible CYC=8 frozen.
   No action required unless Lane E is modified. Helper extraction required first if touched.

4. **DW-B192-SINST-01** (P2, Non-blocking): SetInstrument() CYC=8 ceiling.
   No action required unless SetInstrument() is modified.

5. **DW-B159-K2/K3/K4** (P2): BE path test infrastructure.
   Address when xUnit infrastructure is available.

---

*Written by: ptt-plan-reviewer*
*Epic: B196*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B194/06-deferred-backlog.md (as authoritative carry-forward list)*
