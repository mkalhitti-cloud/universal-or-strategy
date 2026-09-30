# DW-B168-02 Design Spec
## Bug: COLLATERAL-STORM Persists -- B168 Guard Bypassed by NT8 Race Condition
## Selected via Best-of-N (5 designs scored, Design A wins)

---

## 1. Root Cause

`IsLeaderLegBeingChanged` reads `leaderLeg.OrderState` inside the `Accepted`
callback, but the NT8 SIM broker acknowledges all 3 dragged stops simultaneously
-- all transit `ChangeSubmitted -> Accepted` in the same NT8 event batch before
any handler body runs -- so the guard always observes `Accepted` and never fires.

---

## 2. Evidence (output.md lines 129-151)

- Lines 129-143: Stop1, Stop2, Stop3 all reach `ChangeSubmitted` on Sim101
  sequentially as the drag gesture dispatches three broker requests.
- Line 144: Stop1 `Accepted` callback fires. This is the FIRST handler body
  to execute after the round-trip batch.
- Line 147 (TP4-SFB): By the time Stop1's handler reads collateral leg state,
  Sim102 shows `Stop2:Accepted, Stop3:Accepted` -- both already out of
  `ChangeSubmitted`. B168 guard sees `Accepted` on both -> returns false ->
  no skip -> storm proceeds.
- Line 148: Stop2 `Accepted` fires only 4 log lines after Stop1, confirming
  the broker acknowledged all 3 in one burst.
- Line 151: `Stop1:CancelSubmitted, Target1:CancelSubmitted` with
  `PTT-STP-Drag-1:Submitted` -- the storm signature, identical to pre-B168.

The invariant assumed by B168 ("leader leg is still ChangeSubmitted when Stop1
Accepted fires") is FALSE in practice. The state machine moved before any
handler body ran. The B168 predicate is dead code.

---

## 3. Fix: Design A -- _dragsInFlight HashSet<string>

Record which suffixes are in flight at `ChangePending` time (before any
broker round-trip). This timestamp precedes all `Accepted` callbacks and is
immune to the race.

### 3a. New Field

```csharp
// DW-B168-02: tracks which stop suffixes ("1","2","3") are mid-drag.
// Populated at ChangePending (before broker round-trip) so the set is
// correct at Accepted callback time regardless of broker batch ordering.
// JS-021: no lock -- all NT8 order callbacks execute on the UI/dispatch
//   thread; concurrent access is impossible within a single CopyEngine.
// JS-036: HashSet<string> -- bounded to max 3 entries (one per stop leg).
// ASCII-only. No Unicode.
private readonly HashSet<string> _dragsInFlight =
    new HashSet<string>(StringComparer.Ordinal);
```

### 3b. Add Site -- HandleAtmStopSync at ChangePending

Location: inside `HandleAtmStopSync`, in the block that handles
`OrderState.ChangePending` for the leader stop. Exact line numbers MUST be
confirmed by reading the source before implementation (see Section 9).

```csharp
// DW-B168-02: record this suffix as in-flight at the earliest possible
// moment -- ChangePending fires before the broker round-trip, so the set
// is populated before any Accepted callback can read it.
string dragSuffix = ExtractStopSuffix(fo.Name); // or existing suffix variable
if (!string.IsNullOrEmpty(dragSuffix))
    _dragsInFlight.Add(dragSuffix);
```

### 3c. Modified ResubmitCollateralLegs Guard

Replace the defunct `IsLeaderLegBeingChanged(leaderLeg)` check with a
`_dragsInFlight.Contains(s)` check. The suffix `s` is already computed from
`kvp.Key.ToString()` in the existing loop.

```csharp
// DW-B142-QTY-DESYNC-01 + DW-B168-02 guard.
// CYC was 3 (B168 added one if), net delta vs original CYC=2 is now 0
// because IsLeaderLegBeingChanged(CYC=2) is removed and replaced with a
// single Contains check (CYC stays 3 with the existing if + foreach).
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
        // DW-B168-02: skip if this suffix was recorded in-flight at
        // ChangePending time. Its own HandleAtmStopSync is in flight and
        // will handle it. Resubmitting here would produce a CancelPending
        // storm. HashSet.Contains is O(1) and alloc-free.
        if (_dragsInFlight.Contains(s))                    // (3) if -- DW-B168-02
            continue;
        Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
        ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
    }
}
```

Note: `FindLeaderCollateralOrder` call moves after the `_dragsInFlight` check
to avoid a wasted lookup when the leg will be skipped anyway.

### 3d. Clear Site -- HandleAtmStopSync at Accepted

Location: inside `HandleAtmStopSync`, after `SyncAtmFollowerBracket` completes
for the `Accepted` state. Exact line numbers MUST be confirmed by reading
source before implementation (see Section 9).

```csharp
// DW-B168-02: remove suffix from in-flight set once Accepted processing
// is complete. This allows the last handler (Stop3) to see a clear set
// for legs 1 and 2 and execute the normal ResubmitCollateralLegs path.
string dragSuffix = ExtractStopSuffix(fo.Name); // or existing suffix variable
if (!string.IsNullOrEmpty(dragSuffix))
    _dragsInFlight.Remove(dragSuffix);
```

### 3e. Clear Site -- HandleAtmStopSync at ChangeRejected / Cancelled

Location: inside `HandleAtmStopSync`, in the block(s) that handle
`OrderState.ChangeRejected` and `OrderState.Cancelled`. Exact line numbers
MUST be confirmed by reading source before implementation (see Section 9).

```csharp
// DW-B168-02: drag was rejected or cancelled -- remove suffix so the set
// does not permanently suppress future collateral resubmits for this leg.
// Without this clear a rejected drag would orphan the suffix in the set.
string dragSuffix = ExtractStopSuffix(fo.Name); // or existing suffix variable
if (!string.IsNullOrEmpty(dragSuffix))
    _dragsInFlight.Remove(dragSuffix);
```

### 3f. B168 Dead Code Removal

`IsLeaderLegBeingChanged` (private static, CYC=2) and
`IsLeaderLegBeingChangedTestable` (internal static seam, CYC=1) added in
B168 are now dead code.

Status: REMOVE both methods in this ticket.

Rationale: the predicate can never return true at Accepted callback time
(see Section 2). Retaining dead code that was documented as the fix creates
confusion. Remove cleanly; tests for these methods in CopyEngineTests.cs
(DwB168Tests class) are also removed in the test ticket.

---

## 4. Zero-Regression Analysis

### Single-stop drag (one stop dragged, two legs idle)

Only Stop2 dragged. At ChangePending: `_dragsInFlight = {"2"}`.
Stop2 Accepted fires. `ResubmitCollateralLegs` checks leg "1": not in set ->
`FindLeaderCollateralOrder` -> `ResubmitOneCollateralLeg`. Checks leg "3":
not in set -> resubmit. Suffix "2" cleared after.
Result: IDENTICAL to original behaviour. Zero regression.

### Two-stop simultaneous drag (Stop1 + Stop2)

ChangePending fires for both: `_dragsInFlight = {"1","2"}`.
- Stop1 Accepted: checks leg "2" -> in set -> skip. Checks leg "3" -> not in
  set -> resubmit leg 3 once. Clears "1". Set = {"2"}.
- Stop2 Accepted: checks leg "1" -> not in set -> resubmit leg 1 once.
  Checks leg "3" -> not in set -> resubmit leg 3 (second pass, correct --
  Stop2 updates leg 3 to the new stop price). Clears "2". Set = {}.
Net: leg 3 resubmitted twice (once per Accepted handler -- correct, both
handlers update it to the same new price), legs 1+2 each resubmitted once.
No CancelPending. Zero storm.

### Three-stop simultaneous drag (observed failure case)

ChangePending fires for all three: `_dragsInFlight = {"1","2","3"}`.
- Stop1 Accepted: checks leg "2" -> in set -> skip. Checks leg "3" -> in set
  -> skip. Stop1 handles its own PTT-STP-Drag-1 via SyncAtmFollowerBracket.
  Clears "1". Set = {"2","3"}.
- Stop2 Accepted: checks leg "1" -> NOT in set -> resubmit leg 1 (one clean
  cancel+resubmit of PTT-STP-Drag-1 to update price). Checks leg "3" -> in
  set -> skip. Stop2 handles its own PTT-STP-Drag-2. Clears "2". Set = {"3"}.
- Stop3 Accepted: checks leg "1" -> NOT in set -> resubmit. Checks leg "2"
  -> NOT in set -> resubmit. Stop3 handles its own PTT-STP-Drag-3. Clears
  "3". Set = {}.
Net: MAXIMUM 1 cancel+resubmit per leg at any time. No CancelPending.
Prior behaviour (storm): 3 cancel+resubmit cycles per leg = CancelPending.
FIX RESULT: storm eliminated. All 3 followers retain stops.

---

## 5. CYC Budget

| Method                          | CYC Before B168 | CYC After B168 | CYC After B168-02 |
|---------------------------------|-----------------|----------------|-------------------|
| ResubmitCollateralLegs          |        2        |       3        |         3         |
| IsLeaderLegBeingChanged (new)   |       --        |       2        |    REMOVED        |
| IsLeaderLegBeingChangedTestable |       --        |       1        |    REMOVED        |
| HandleAtmStopSync (add/clear)   |        *        |       *        |   +2 branches     |

* HandleAtmStopSync CYC baseline must be confirmed from source before
  committing to exact delta. The 4 new if-branches (add at ChangePending,
  clear at Accepted, clear at ChangeRejected, clear at Cancelled) each add
  +1 McCabe if they are in separate conditional blocks. If existing
  OrderState switch/if chains absorb them as additional arms the delta may
  be lower. Confirm at implementation time.

Net delta vs original (pre-B168): ResubmitCollateralLegs +1, HandleAtmStopSync
+2 to +4. All methods remain well within CYC <= 8 budget.

---

## 6. Test Contract

Add to DwB168_02Tests class in CopyEngineTests.cs (replacing DwB168Tests
which tested the now-removed predicate):

Test A -- [Fact] DragsInFlight_SingleStop_CollateralResubmitted
  Arrange: CopyEngine instance with _dragsInFlight = {} (empty).
           Simulate single-stop drag: suffix "2" NOT in set.
  Act: call ResubmitCollateralLegsTestable or exercise via existing seam.
  Assert: ResubmitOneCollateralLeg called for suffix "2" (not skipped).

Test B -- [Fact] DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg
  Arrange: _dragsInFlight = {"2","3"} (two other legs in flight).
           Evaluating leg "2" inside loop.
  Act: guard check -- _dragsInFlight.Contains("2").
  Assert: returns true -> continue (skip confirmed).

Test C -- [Fact] DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs
  Arrange: _dragsInFlight = {} (both other suffixes already cleared).
           Evaluating legs "1" and "2".
  Act: guard check for each.
  Assert: both return false -> ResubmitOneCollateralLeg called for both.

Test D -- [Fact] DragsInFlight_ClearOnCancelled_PreventsPermamentSkip
  Arrange: _dragsInFlight = {"1"} (suffix added at ChangePending).
  Act: simulate ChangeRejected/Cancelled path -> _dragsInFlight.Remove("1").
  Assert: after clear, _dragsInFlight.Contains("1") == false.

All tests use stub/fake pattern (no NT8 runtime). All use
internal-static seam for _dragsInFlight access (pattern:
IsPttStpDragCancellableTestable at line 4141). No lock(), no Unicode.

---

## 7. Files Touched

| File                                    | Change                                       |
|-----------------------------------------|----------------------------------------------|
| src/PropTraderTools/CopyEngine.cs       | Add _dragsInFlight field (+2 ln)             |
|                                         | Add/clear in HandleAtmStopSync (+6-8 ln)     |
|                                         | Modify ResubmitCollateralLegs guard (+3 ln)  |
|                                         | Move FindLeaderCollateralOrder call (-0 ln)  |
|                                         | REMOVE IsLeaderLegBeingChanged (-8 ln)       |
|                                         | REMOVE IsLeaderLegBeingChangedTestable (-2 ln)|
|                                         | Update CYC comment (+0 ln, in-place edit)    |
| src/PropTraderTools/CopyEngineTests.cs  | REMOVE DwB168Tests (4 tests, -~40 ln)        |
|                                         | ADD DwB168_02Tests (4 tests, +~40 ln)        |

Total estimated diff: ~60-80 lines changed. Well within 10,000 char DIFF GUARD.

---

## 8. Best-of-N Design Summary

| Design | Mechanism                                | CYC Delta | Race-Immune | Winner |
|--------|------------------------------------------|-----------|-------------|--------|
| A      | HashSet<string> _dragsInFlight (per-leg) | +2 to +4  | YES         | YES    |
| B      | int _dragInFlightCount (coarse counter)  | +1        | YES         | No     |
| C      | leaderLeg.StopPrice == newPrice          | +1        | No          | DISQUALIFIED |
| D      | int _dragEpoch + Dictionary bitmask      | +4        | YES         | No     |
| E      | leaderLeg.OrderState at callback time    | +2        | No (B168)   | DISQUALIFIED |

Weighted scores (race-immunity x3, correctness x3, simplicity x2,
regression-safety x2, max 10 each):

| Design | Race(x3) | Correct(x3) | Simple(x2) | RegSafe(x2) | Score |
|--------|----------|-------------|------------|-------------|-------|
| A      | 10       | 9           | 8          | 9           | 9.1   |
| B      | 9        | 8           | 9          | 9           | 8.7   |
| D      | 9        | 8           | 6          | 8           | 7.9   |
| C      | --       | --          | --         | --          | DISQ  |
| E      | --       | --          | --         | --          | DISQ  |

**WINNER: Design A** -- highest weighted score, per-leg precision avoids the
coarse-guard scope risk of Design B (single CopyEngine managing multiple
independent ATM groups), zero heap allocation (HashSet bounded to 3 entries),
cleanest single-sentence invariant: "if the suffix was recorded in-flight at
ChangePending, its own handler will cover it."

Belt-and-suspenders A+B rejected: adds complexity with no correctness gain.

---

## 9. Known Limitation: Line Numbers Must Be Read Before Implementation

`HandleAtmStopSync` is a large method. The exact lines for the ChangePending
add-site, Accepted clear-site, ChangeRejected clear-site, and Cancelled
clear-site MUST be located by reading `CopyEngine.cs` source before writing
any apply_diff. Do NOT assume line numbers. The ptt-engineer MUST:

  1. Read HandleAtmStopSync entry and ChangePending block (grep for
     "ChangePending" in HandleAtmStopSync context).
  2. Read Accepted block in HandleAtmStopSync (grep for "Accepted" near
     SyncAtmFollowerBracket call).
  3. Read ChangeRejected and Cancelled blocks in HandleAtmStopSync.
  4. Confirm ExtractStopSuffix (or equivalent existing suffix extraction)
     is available at each site, or determine the correct expression for
     the suffix string ("1", "2", or "3") from fo.Name.
  5. Only then apply_diff.

CopyEngine.cs is 499,694 bytes. apply_diff only. NEVER write_file.

---

