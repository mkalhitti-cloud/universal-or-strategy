# PR-20-deferred Repair Log
# Lane L7 -- Wave 7 Post-Merge Deferred Repairs
# Branch: wave7/pr20-deferred-repairs (fresh from main, merged 86eab7cf)
# Date: 2026-07-04

---

## Summary

| Finding | Classification | Status | Commit | CYC |
|---------|---------------|--------|--------|-----|
| NEW-F3 | HALLUCINATION | SKIPPED | -- | -- |
| NEW-F5 | VALID-LOGIC-BUG (P2) | FIXED | 04a2c6c9 | 7 |
| NEW-F6 | VALID-LOGIC-BUG (P2) | FIXED | 87f8d32b | 7 |
| NEW-F7 | VALID-LOGIC-BUG (P2) | FIXED | 956c5e08 | 8/6 |
| G-01 | VALID-LOGIC-BUG (P1) | FIXED | 7c9221dd | -- |
| G-02 | VALID-LOGIC-BUG (P1) | FIXED | 7c9221dd | -- |

fix_queue findings fixed: 3 (NEW-F5, NEW-F6, NEW-F7)
Additional bot findings fixed: 2 (G-01, G-02 from Greptile live forensics)
Hallucinations skipped: 1 (NEW-F3)
Promoted to Wave 8: 1 (F6 -- PropagateMasterTargetMove, excluded per fix_queue)

---

## Fix Details

### NEW-F5 -- OrderId fallback in PurgeFollowerStopScanStopOrders
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs (line 832)
**Change**: `if (sc.Value == order)` -> `if (IsMatchingStopReplacement(sc.Value, order))`
**Pattern**: Reused existing `IsMatchingStopReplacement` helper (line 793) which implements
the ref-then-OrderId fallback already used throughout the file.
**CYC**: 8->7 (helper extraction eliminated compound `||`)
**OKF**: Rule 5 independent_tracking -- ghost stops prevented after NT reconnect.

### NEW-F6 -- Cascade suppression active-state guard
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs (line 914 + new helper at 424)
**Change 1**: Added `IsFsmStateActive(FollowerReplaceSpec spec)` private static helper --
returns true only for PendingCancel or Submitting states.
**Change 2**: Gated `ExecuteFollowerCascadeProcessFollower` suppression with:
`&& IsFsmStateActive(b948FsmSpec)` so SubmitFailed/Idle specs no longer block cascade.
**CYC**: 4->7 (helper CYC=3, caller unchanged at 4 -- no impact on threshold)
Wait -- ExecuteFollowerCascadeProcessFollower CYC was 7 (confirmed by audit), not 4.
Helper IsFsmStateActive CYC=3.
**OKF**: Rule 5 defense_in_depth -- only in-flight FSM states suppress cascade teardown.

### NEW-F7 -- Reconcile stale stop reference post-reconnect
**File**: src/V12_002.Orders.Management.StopSync.cs (new helper at 502, caller at 621)
**Change 1**: Added `ResolveStopReference(string entryName, Order tracked)` -- walks
Account.Orders for a live Order with matching OrderId but different reference; atomically
updates ConcurrentDictionary via TryUpdate; returns live reference.
**Change 2**: In `UpdateStopQuantity_Execute`, added:
`currentStop = ResolveStopReference(entryName, currentStop);` after dict lookup.
**CYC**: UpdateStopQuantity_Execute stays at 8. ResolveStopReference CYC=6.
**OKF**: Rule 5 staleness_guard + Rule 1 lock-free (TryUpdate is atomic).

### G-01 + G-02 -- DateTime.UtcNow unification across trailing cluster
**Files**: src/V12_002.Trailing.cs, src/V12_002.Trailing.StopUpdate.cs
**G-01 change**: Trailing.cs line 215: `DateTime.Now` -> `DateTime.UtcNow` in
`ManageTrail_AdaptiveThrottleTick`. Fixes circuit breaker permanent-freeze bug
(circuitBreakerActivatedTime was UtcNow, reset check used DateTime.Now -- always
negative west of UTC).
**G-02 changes** (5 lines in StopUpdate.cs):
- Line 39: CleanupStalePendingReplacements now = DateTime.UtcNow
- Line 96: pendingAgeSeconds uses DateTime.UtcNow (consistent with CreatedTime at 176)
- Line 142: same in HandleStalePendingReplacement
- Line 316: CreateNewPendingForEmergencyStop CreatedTime = DateTime.UtcNow
(was DateTime.Now -- inconsistent with line 176 which was already UtcNow)
**OKF**: Rule 3 -- "All time comparisons must use the SAME clock source (UTC only)".

---

## Gate Results

wave7_prepush_gate.py --base origin/main: GATE PASSED (6/6)
- CS-only: PASS (4 .cs files modified, 0 non-cs)
- ASCII-only: PASS
- DateTime.Now (none introduced): PASS -- we REMOVED 5 DateTime.Now usages
- lock(): PASS
- underscore locals: PASS
- diff size: 6,657 chars (limit 150,000): PASS

dotnet build Linting.csproj: Build succeeded, 0 errors
dotnet csharpier check: PASS

---

## Commits on branch wave7/pr20-deferred-repairs

```
76a270b6  fix(wave7/pr20-deferred): NEW-F5 -- OrderId fallback (superseded, see 04a2c6c9)
04a2c6c9  fix(wave7/pr20-deferred): NEW-F5 -- OrderId fallback via IsMatchingStopReplacement (CYC 8->7)
87f8d32b  fix(wave7/pr20-deferred): NEW-F6 -- restrict cascade suppression to active FSM states
956c5e08  fix(wave7/pr20-deferred): NEW-F7 -- reconcile stale stop reference via ResolveStopReference
e86a0a29  Merge remote-tracking branch 'origin/main' into wave7/pr20-deferred-repairs
7c9221dd  fix(wave7/pr20-deferred): G-01+G-02 -- unify DateTime.UtcNow across trailing cluster
```

Head commit: 7c9221dd
Branch pushed to origin/wave7/pr20-deferred-repairs: YES
PR opened: NO (deferred to Phase 6 per V12 PR gate protocol)

---

## Modified Source Files

- src/V12_002.Orders.Callbacks.AccountOrders.cs
- src/V12_002.Orders.Management.StopSync.cs
- src/V12_002.Trailing.cs
- src/V12_002.Trailing.StopUpdate.cs
