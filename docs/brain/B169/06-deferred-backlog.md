# B169 Deferred Backlog

## B169 -- DW-B168-02 COLLATERAL-STORM recurrence
Date: 2025-09-20
Status: CLOSED
Bug: IsLeaderLegBeingChanged guard was dead code in NT8 SIM batch-ACK scenario.
     NT8 SIM acknowledges all three dragged stops in a single event batch before
     any handler body runs, so leaderLeg.OrderState is already Accepted when
     Stop1's callback reads it. The B168 predicate can never return true.
Fix: _dragsInFlight HashSet<string> tracks in-flight stops at ChangePending time.
     ResubmitCollateralLegs checks set (not live order state).
     Dead methods IsLeaderLegBeingChanged and IsLeaderLegBeingChangedTestable removed.
     DwB168Tests class removed. DwB168_02Tests class added (4 [Fact] tests via seam).
Regression check: DW-B168-01 (single-stop drag) confirmed unaffected.
SIM gate: Director must verify 3-stop simultaneous drag + single-stop drag in SIM.
Deferred: none

---

## Prior OPEN Items Carried Forward

The following items from preceding blocks remain OPEN and are not addressed in B169:

| ID | Block origin | Description | Priority | Status |
|---|---|---|---|---|
| DW-B169-K1 | B169 | SIM GATE: Director must drag all 3 stops simultaneously in NinjaTrader SIM and confirm 0 missing follower stops (three-stop DW-B168-02 path) | P0 | OPEN |
| DW-B169-K2 | B169 | SIM GATE: Director must also test single-stop drag in same SIM session to confirm no DW-B168-01 regression | P0 | OPEN |
| DW-B168-K1 | B168 | Director SIM gate from B168 -- superseded by DW-B169-K1/K2 which cover both the new fix and single-stop regression path | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | DW-B167-02: follower ATM brackets never placed after leader fill (fo=NULL). Second output.md required. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race (pre-fill drag + manual fill). Low probability. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending (prerequisite for B168/B169 SIM gate) | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests for B159 features (infrastructure required) | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 in TryArmBePriceRetry | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing dependency | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line number in IsFollowerPositionBlocked comment | P3 | OPEN |

---

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Block B169.*
*DW-B168-02 COLLATERAL-STORM implementation is complete. FINAL_PASS granted subject to Director SIM gate.*
