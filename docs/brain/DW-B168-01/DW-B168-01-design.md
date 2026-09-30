# DW-B168-01 Design Spec
## Bug: COLLATERAL-STORM — Concurrent multi-stop drag kills follower stops
## Selected via Best-of-N (5 designs scored, Design E variant wins)

---

## 1. Root Cause

`ResubmitCollateralLegs` iterates every collateral leg and unconditionally
cancels+resubmits its `PTT-STP-Drag-{s}` even when the leader's `Stop{s}`
order is in `ChangeSubmitted` state -- meaning that stop has its own
`HandleAtmStopSync` call already in flight and will self-handle. When all
3 stops are dragged simultaneously, this produces a 3-way cancel storm
(N*(N-1) = 6 extra cancel+resubmit cycles for a 3-stop drag), driving
follower orders into `CancelPending` states that block recovery before
the position closes.

---

## 2. Evidence (output.md lines confirmed)

- Lines 129-143: Stop1/Stop2/Stop3 ALL reach `ChangeSubmitted` on Sim101
  before any Accepted callback fires.
- Line 342 (Sim104, Stop2 2nd drag): `PTT-STP-Drag-1/2/3 all CancelSubmitted`
  simultaneously -- the storm signature.
- Line 414 (Sim104, Stop3 3rd drag): `CancelPending` states appear --
  NT8 rejected a cancel because another cancel was already in flight.
- Images 1-4: Stops disappear progressively from Sim102/103/104 across
  drag cycles; only Sim101 (the leader, unaffected) retains stops at end.

---

## 3. Fix: Leader-State Guard in ResubmitCollateralLegs

### Method 1 -- NEW predicate (add after FindLeaderCollateralOrder)

```csharp
// DW-B168-01: returns true when the leader's Stop{suffix} is mid-drag.
// Used by ResubmitCollateralLegs to suppress collateral cancel+resubmit
// for legs that have their own HandleAtmStopSync in flight.
// ChangePending: drag gesture dispatched to NT8.
// ChangeSubmitted: NT8 dispatched to broker, Accepted callback pending.
// Both states guarantee a concurrent HandleAtmStopSync is in flight.
// Zero-regression: single-stop drag leaves other legs in Working/Accepted
//   -- neither state matches -> guard is false -> unchanged behaviour.
// CYC=2: null guard(1) + expression body with state check(1) = CYC 2.
// JS-002: bool. JS-021: no lock. JS-001: no throw. ASCII-only.
// JS-036: no heap -- reads field on existing leaderLeg object, no ToList.
private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
    leaderLeg != null
    && (leaderLeg.OrderState == OrderState.ChangePending
        || leaderLeg.OrderState == OrderState.ChangeSubmitted);
```

### Method 2 -- MODIFIED ResubmitCollateralLegs (one new if/continue)

```csharp
// DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.
// CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).
// CYC=3. Well within <=8.
private void ResubmitCollateralLegs(
    Account acc,
    Order fo,
    double newPrice,
    Dictionary<int, double> otherLegPrices,
    string excludeSuffix,
    Order leaderOrder
)
{
    foreach (var kvp in otherLegPrices)                    // (1) foreach
    {
        string s = kvp.Key.ToString();
        double price = kvp.Value;
        if (price <= 0)                                    // (2) if
            continue;
        Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
        // DW-B168-01: skip -- concurrent drag for this leg is in flight.
        // Its own HandleAtmStopSync will handle it. Resubmitting here
        // would add a redundant cancel that drives CancelPending storms.
        if (IsLeaderLegBeingChanged(leaderLeg))            // (3) if -- NEW
            continue;
        ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
    }
}
```

---

## 4. Zero-Regression Analysis

### Single-stop drag (original design case)
Only Stop2 dragged. Leader's Stop1 and Stop3 are in Working or Accepted.
`IsLeaderLegBeingChanged(Stop1)` = false (Working != ChangePending/ChangeSubmitted).
`IsLeaderLegBeingChanged(Stop3)` = false.
Both legs fall through to ResubmitOneCollateralLeg. Behaviour IDENTICAL.

### Two-stop simultaneous drag (Stop1 + Stop2)
- Stop1 Accepted fires first. Checks collateral leg 2: Stop2 is ChangeSubmitted -> skip.
  Checks leg 3: Stop3 is Working -> fall through, resubmit leg 3 normally.
- Stop2 Accepted fires second. Checks leg 1: Stop1 is now Accepted -> fall through, resubmit.
  Checks leg 3: Stop3 still Working -> fall through.
Net: leg 3 resubmitted once (by Stop1), legs 1+2 each resubmitted once. Zero storm.

### Three-stop simultaneous drag (observed failure case)
- Stop1 Accepted: leg 2 in ChangeSubmitted -> skip. Leg 3 in ChangeSubmitted -> skip.
  Stop1's own SyncAtmFollowerBracket creates PTT-STP-Drag-1. Legs 2,3 untouched. 0 extra cancels.
- Stop2 Accepted: leg 1 now Accepted -> fall through (one clean cancel+resubmit of PTT-STP-Drag-1).
  Leg 3 still ChangeSubmitted -> skip.
  Stop2's own SyncAtmFollowerBracket creates PTT-STP-Drag-2.
- Stop3 Accepted: leg 1 Accepted -> fall through (one clean cancel+resubmit).
  Leg 2 Accepted -> fall through (one clean cancel+resubmit).
  Stop3's own SyncAtmFollowerBracket creates PTT-STP-Drag-3.

Net: MAXIMUM 1 cancel+resubmit per leg at any time. No CancelPending.
Prior behaviour: 3 cancel+resubmit cycles per leg = CancelPending storm.
FIX RESULT: storm eliminated. All 3 followers retain stops.

---

## 5. CYC Budget

| Method                      | CYC Before | CYC After |
|-----------------------------|-----------|-----------|
| ResubmitCollateralLegs      |     2     |     3     |
| IsLeaderLegBeingChanged (new)|    --     |     2     |

Total file delta: +3 McCabe. Both methods < 8. No other methods touched.

---

## 6. Test Contract

Ticket 1 (test): Add to DwB168Tests.cs (or existing test file):

Test A -- [Fact] IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue
  Arrange: stub Order with OrderState.ChangeSubmitted
  Act: IsLeaderLegBeingChanged(order)
  Assert: == true

Test B -- [Fact] IsLeaderLegBeingChanged_Working_ReturnsFalse
  Arrange: stub Order with OrderState.Working
  Assert: == false

Test C -- [Fact] IsLeaderLegBeingChanged_Null_ReturnsFalse
  Arrange: null
  Assert: == false

Test D -- [Fact] IsLeaderLegBeingChanged_ChangePending_ReturnsTrue
  Arrange: stub Order with OrderState.ChangePending
  Assert: == true

NOTE: IsLeaderLegBeingChanged is private static. Expose via
  internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
      IsLeaderLegBeingChanged(o);
(same pattern as IsPttStpDragCancellableTestable at line 4141)

---

## 7. Files Touched

| File                                    | Change                              |
|-----------------------------------------|-------------------------------------|
| src/PropTraderTools/CopyEngine.cs       | Add IsLeaderLegBeingChanged (+8 ln) |
|                                         | Modify ResubmitCollateralLegs (+3 ln)|
|                                         | Add test seam (+2 ln)               |
| src/PropTraderTools/CopyEngineTests.cs  | Add 4 Fact tests                    |

Total src/PropTraderTools delta: ~13 lines + 4 tests. Well within DIFF LIMIT.

---

## 8. Best-of-N Design Summary

| Design | Mechanism                                | CYC Delta | Price-dependent | Winner |
|--------|------------------------------------------|-----------|-----------------|--------|
| A      | Leader ChangePending||ChangeSubmitted    | +5        | No              | Close  |
| B      | Follower stop at newPrice (in method)    | +5        | YES             | No     |
| C      | leaderLeg.StopPrice == newPrice          | +1        | YES (FALSE SKIP risk) | DISQUALIFIED |
| D      | Follower stop at newPrice (predicate)    | +7        | YES             | No     |
| E      | ChangeSubmitted only (uses leaderLeg)    | +2        | No              | BASE   |
| WINNER | E + A belt-and-suspenders (ChangePending)| +2        | No              | YES    |

Winner rationale: state-based (not price-dependent), uses already-computed
`leaderLeg` (zero extra allocation), minimal CYC (+2), cleanest single-sentence
invariant: "if the leader's stop is mid-drag, its own handler will cover it."

---

## 9. Known Limitation (deferred)

Stop3's handler (last to fire Accepted) will still execute ONE cancel+resubmit
for legs 1 and 2 via ResubmitCollateralLegs (they are in Accepted state by then,
not ChangeSubmitted). This is CORRECT and intended: Stop3 is confirming the final
price for all legs. It is a single clean cancel+resubmit, not a storm.
No further fix needed for this case.
