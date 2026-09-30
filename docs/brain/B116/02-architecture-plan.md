# B116 Architecture Plan -- DW-B124 Fix
# CalcTNQty Fallback Wrong Split When BE-ALL Consumes Native ATM Brackets Before QX-ALL

**Pipeline**: B116 Ph1 (ptt-architect)
**Date**: 2026-08-28
**Defect**: DW-B124 (P0)
**Fix Option**: Option B (Director-approved) -- pass leader qty array to follower ExecuteOne

---

## 1. Problem Statement

### Root Cause (confirmed by code read 2026-08-28)

In `PttGlobalQuickExit.Execute` (L89):
```csharp
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

In the **Combo C** path (BE-ALL fired before QX-ALL):
- BE-ALL cancelled native `Target1/2/3` on all followers.
- BE-ALL submitted `PTT-BE-Target-1/2/3` on all followers.
- At QX-ALL snapshot time, `PTT-BE-Target-*` orders are **in-flight** -- NOT yet
  `Working` or `Accepted`. `SnapshotTargetOrders` requires Working or Accepted state.
- Result: `nativeTargets.Count = 0`, `pttTargets.Count = 0`.
  `SnapshotTargetOrders` returns empty list.

In `PttQuickExit.Execute` (L117-120):
```csharp
int tNQty = (targets != null && i < targets.Count)
    ? targets[i].Qty
    : CalcTNQty(pos.Quantity, targetCount, i);
```

When `followerTargets` is empty, `i < targets.Count` is always false.
`CalcTNQty(7, 3, 0)=2, (7,3,1)=2, (7,3,2)=3` fires -- arithmetic floor+remainder.
Correct leader split is T1=4, T2=2, T3=1.
Wrong follower split is T1=2, T2=2, T3=3 -- 2 contracts overexposed at T3.

### Sim104 Variant (count=1, not count=0)
If one PTT-BE-Target-* reaches Working before snapshot, `pttTargets.Count=1`.
Only T1 gets a qty; T2 and T3 iteration falls off the list.
`targets[i].Qty` used for i=0 only; `CalcTNQty` fires for i=1,2 but
`targets[1]` and `targets[2]` are out-of-range => the loop runs but submits
wrong qty, OR (if the index check gates submission) T2/T3 orders are never
placed. Live evidence: Sim104 T2/T3 missing from order event CSV.

### DW-B120 Independence
DW-B120 (Sim103 count=0 -- no PTT-BE, just async ATM lag) is **not** this defect.
DW-B120 fallback now uses `leaderTargetCount=3` (correct count) but still uses
`CalcTNQty` arithmetic split. DW-B120 is a P1 monitor; arithmetic split is
acceptable for the no-BE path because qty ratio 7:7 -> scale factor 1.0 ->
same arithmetic result. DOES NOT require this fix.

---

## 2. Fix: Option B -- Leader Qty Array Passthrough

### Core Idea
When a follower's own snapshot is empty or partial, use the **leader's per-target
qty array** (already snapshotted at L47) scaled by `followerPosQty / leaderPosQty`.

This gives the follower the exact same proportional allocation the leader has,
rather than the arithmetic floor+remainder split.

For equal-qty accounts (most common case): scale = 1.0, result identical to leader.
For different-qty accounts: proportional scaling, last target absorbs rounding.

### Data Flow (before fix)

```
PttGlobalQuickExit.Execute
  L47:  var targets = SnapshotTargetOrders(leader, pos.Instrument)
        --> targets = [(price1,4),(price2,2),(price3,1)]  [leader, count=3 OK]

  L89:  var followerTargets = SnapshotTargetOrders(follower, pos.Instrument)
        --> followerTargets = []  [empty -- BE-ALL consumed native brackets]

  L133: ExecuteOne(follower, ... targets=followerTargets, leaderTargetCount=targets.Count=3)
        --> PttQuickExit.Execute receives targets=[]
        --> CalcTNQty(7,3,i) fallback fires
        --> T1=2 T2=2 T3=3  [WRONG]
```

### Data Flow (after fix)

```
PttGlobalQuickExit.Execute
  L47:  var targets = SnapshotTargetOrders(leader, pos.Instrument)
        --> leaderTargets = [(price1,4),(price2,2),(price3,1)]  [count=3]

  L89:  var followerTargets = SnapshotTargetOrders(follower, pos.Instrument)
        --> followerTargets = []

  NEW:  if (followerTargets.Count == 0 && leaderTargets.Count > 0)
            followerTargets = ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty)
        --> followerTargets = [(price1,4),(price2,2),(price3,1)]  [scale=1.0, same]

  L133: ExecuteOne(follower, ... targets=followerTargets, leaderTargetCount=3)
        --> PttQuickExit.Execute receives targets=[(4),(2),(1)]
        --> targets[i].Qty used directly
        --> T1=4 T2=2 T3=1  [CORRECT]
```

---

## 3. Implementation Plan

### File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

#### Change 1: Extract `leaderPosQty` from existing `pos.Quantity`
`pos` is already in scope at L47 (leader position). No new read needed.
`leaderPosQty = pos.Quantity`.

#### Change 2: Extract `followerPosQty` from `follower.Positions` loop
The DW-B115-DIAG block already reads `_fPosQty` from `follower.Positions` (L95-105).
Promote `_fPosQty` to a named local variable above the DIAG block for reuse.
After DIAG block is eventually removed, the variable persists.

#### Change 3: Add `ScaleLeaderTargets` static helper method
New private static method:

```csharp
/// <summary>
/// ScaleLeaderTargets: derive follower per-target qty from leader qty array.
/// Used when followerTargets snapshot is empty (BE-ALL consumed native brackets).
/// Scale = followerPosQty / leaderPosQty. Last target absorbs rounding.
/// CYC=3: (1) scale-guard, (2) for-loop, (3) last-target-rounding.
/// JS-001: no throw. JS-002: returns non-null list. ASCII-only.
/// </summary>
private static System.Collections.Generic.List<(double Price, int Qty)>
    ScaleLeaderTargets(
        System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
        int followerPosQty,
        int leaderPosQty
    )
{
    var result = new System.Collections.Generic.List<(double Price, int Qty)>(
        leaderTargets.Count
    );
    if (leaderPosQty <= 0)  // (1) guard: degenerate case, return empty (caller falls back)
        return result;
    int allocated = 0;
    for (int i = 0; i < leaderTargets.Count; i++)  // (2)
    {
        int qty;
        if (i == leaderTargets.Count - 1)  // (3) last target absorbs rounding
            qty = Math.Max(1, followerPosQty - allocated);
        else
            qty = Math.Max(1, (int)Math.Round(
                (double)leaderTargets[i].Qty * followerPosQty / leaderPosQty
            ));
        allocated += qty;
        result.Add((leaderTargets[i].Price, qty));
    }
    return result;
}
```

CYC = 3 (guard + loop + last-target branch). Jane Street compliant.

#### Change 4: Inline substitution in the follower loop (L89 region)

After the existing `SnapshotTargetOrders` call and DIAG block, insert:

```csharp
// DW-B124: if follower snapshot is empty (BE-ALL consumed native brackets),
// derive target qty array from leader snapshot scaled by posQty ratio.
// This prevents CalcTNQty arithmetic fallback from distributing remainder
// to the wrong tranche (e.g. T3=3 instead of T3=1 for a 4/2/1 leader split).
if (followerTargets.Count == 0 && targets.Count > 0 && followerPosQty > 0)
    followerTargets = ScaleLeaderTargets(targets, followerPosQty, pos.Quantity);
```

`targets` = leader snapshot (already in scope, L47).
`followerPosQty` = promoted from DIAG block.
`pos.Quantity` = leader pos qty (already in scope).

This replaces the empty list with a correctly-scaled list **before** passing to
`ExecuteOne`. No change to `ExecuteOne`, `PttQuickExit.Execute`, or `CalcTNQty`.

---

## 4. CYC Impact

| Method | Before | After |
|--------|--------|-------|
| `Execute` (PttGlobalQuickExit) | 8 | 9 |
| `ScaleLeaderTargets` (new) | -- | 3 |

**NOTE**: `Execute` CYC rises from 8 to 9 with the `if (followerTargets.Count == 0 ...)` guard.
This exceeds the JS-038 CYC<=8 ceiling.

**Mitigation option**: Extract the entire follower-targets-substitution block into a helper:

```csharp
private static System.Collections.Generic.List<(double Price, int Qty)> ResolveFollowerTargets(
    System.Collections.Generic.List<(double Price, int Qty)> followerSnapshot,
    System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
    int followerPosQty,
    int leaderPosQty
)
{
    if (followerSnapshot.Count > 0)         // (1)
        return followerSnapshot;
    if (leaderTargets.Count == 0 || followerPosQty <= 0)  // (2)
        return followerSnapshot;            // empty -- CalcTNQty fallback fires (DW-B120 path)
    return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);  // (3)
}
```

`ResolveFollowerTargets` CYC=3. `Execute` CYC stays at 8 (no new branch added inline).
Call site becomes:
```csharp
followerTargets = ResolveFollowerTargets(followerTargets, targets, followerPosQty, pos.Quantity);
```

**Recommendation**: Use the two-helper approach (`ScaleLeaderTargets` + `ResolveFollowerTargets`)
to keep `Execute` CYC<=8. Both helpers are private static, testable in isolation.

---

## 5. Updated CYC Table

| Method | Before | After |
|--------|--------|-------|
| `Execute` (PttGlobalQuickExit) | 8 | 8 (unchanged -- extracted) |
| `ResolveFollowerTargets` (new) | -- | 3 |
| `ScaleLeaderTargets` (new) | -- | 3 |

Total: CYC budget fully compliant. No method exceeds 8.

---

## 6. Testing Requirements (xUnit only -- JS-051)

### Test file: `src/PropTraderTools/Tests/B116Tests.cs`

#### Test 1: ScaleLeaderTargets_EqualQty_IdenticalSplit
- Input: leader=[4,2,1] leaderPosQty=7 followerPosQty=7
- Expected: result=[4,2,1]

#### Test 2: ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty
- Input: leader=[4,2,1] leaderPosQty=7 followerPosQty=4
- Expected: total=4, last absorbs rounding, T1+T2+T3=4, each>=1

#### Test 3: ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty
- Input: leaderPosQty=0
- Expected: empty list (guard fires, no divide-by-zero)

#### Test 4: ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf
- Input: followerSnapshot=[(p,4),(p,2),(p,1)] leaderTargets=[...] any qty
- Expected: returns followerSnapshot unchanged (snapshot path not bypassed)

#### Test 5: ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled
- Input: followerSnapshot=[] leaderTargets=[(p,4),(p,2),(p,1)] qtys 7/7
- Expected: returns [(p,4),(p,2),(p,1)]

#### Test 6: ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty
- Input: followerSnapshot=[] leaderTargets=[]
- Expected: returns [] (CalcTNQty fallback path, DW-B120 preserved)

---

## 7. Tickets

### Ticket 1: Add ScaleLeaderTargets + ResolveFollowerTargets helpers + substitution inline
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Changes**:
1. Promote `_fPosQty` from DIAG block to named local above DIAG block.
2. Add `ScaleLeaderTargets` static helper (CYC=3, as above).
3. Add `ResolveFollowerTargets` static helper (CYC=3, as above).
4. Insert `followerTargets = ResolveFollowerTargets(...)` call immediately after DIAG block.
**No changes** to `PttQuickExit.cs`, `CopyEngine.cs`, or any other file.

### Ticket 2: Add B116Tests.cs (6 xUnit [Fact] tests)
**File**: `src/PropTraderTools/Tests/B116Tests.cs`
**Tests**: 6 facts listed above.
**Framework**: xUnit only (JS-051 -- never NUnit or MSTest).

---

## 8. Scope Boundary

**IN SCOPE:**
- `PttGlobalQuickExit.cs`: two new private static helpers + one substitution call + `_fPosQty` promotion.
- `B116Tests.cs`: 6 xUnit tests.

**OUT OF SCOPE (do NOT touch):**
- `PttQuickExit.cs` (no changes to `CalcTNQty` or `Execute`).
- `CopyEngine.cs` (DW-B122 fix is deployed -- do not revert).
- DIAG logging blocks (leave in place -- Director will remove when gate passes).
- `SnapshotTargetOrders` (DW-B123 dedup is deployed -- do not change).

---

## 9. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| `ScaleLeaderTargets` called when followerPosQty=0 (flat account) | `ResolveFollowerTargets` guard (2) checks `followerPosQty <= 0`, returns empty -- `CalcTNQty` fires but `pos.Quantity=0` -> flat guard in `Execute` prevents submission |
| `ScaleLeaderTargets` called with partial followerTargets (count=1, not 0) | `ResolveFollowerTargets` (1): `followerSnapshot.Count > 0` -- returns partial snapshot unchanged. DW-B120/Sim104-variant behavior unchanged. Tracked as DW-B120 P1 monitor. |
| CYC creep in `Execute` | Two-helper extraction keeps `Execute` at CYC=8. |
| Scaling rounding error on non-divisible qty | Last-target absorption guarantees `sum = followerPosQty`. Verified by Test 2. |

---

## 10. Ph2 Gate Criteria (ptt-plan-reviewer)

- [ ] Option B fix targets correct call site (L89 region in `PttGlobalQuickExit.Execute`).
- [ ] No changes to `PttQuickExit.Execute` or `CalcTNQty`.
- [ ] `Execute` CYC stays at 8 (two-helper extraction confirmed).
- [ ] `ResolveFollowerTargets` CYC=3, `ScaleLeaderTargets` CYC=3.
- [ ] Non-empty follower snapshot path (`followerSnapshot.Count > 0`) is unchanged -- returns self.
- [ ] DW-B120 (Sim103 async lag, no BE-ALL) unaffected: empty snapshot with empty leaderTargets returns empty list, `CalcTNQty` fires as before.
- [ ] 6 xUnit tests defined, covering all branches of both helpers.
- [ ] No `lock()`, no `throw new XxxException`, no `return null`, no async void.
- [ ] ASCII-only strings in new code.

---

*Ph1 complete. Advance to Ph2 ptt-plan-reviewer.*
