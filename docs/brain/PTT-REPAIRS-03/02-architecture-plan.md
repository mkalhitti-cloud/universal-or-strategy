# PTT-REPAIRS-03 Architecture Plan

**Epic**: PTT-REPAIRS-03
**Block output**: `docs/brain/PTT-REPAIRS-03/`
**Source file**: `src/PropTraderTools/CopyEngine.cs`
**Test file**: `src/PropTraderTools/CopyEngineTests.cs`
**Plan status**: REVIEW_PENDING
**Architect**: ptt-architect
**Date**: 2026-09-08 (Revision 2 — V-02 fix)

---

## STEP 0: MANDATORY BASELINE MEASUREMENT

Command run:
```
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
```

**Authoritative [Fact] baseline: 475**

Note: DW-REPAIRS-02-02 (OPEN) required fresh measurement at next block start — done.
DW-REPAIRS-02-02 is CLOSED by this measurement.
Prior block PTT-REPAIRS-02 stated count 475 (+1 from 474); this confirms 475 as current state.
Two new [Fact] methods will be added this block → final target count: **477**.

---

## A. LANE-SPLIT GATE

### Q1. Same method or within 50 lines?

- BUG-A target: `ShouldSkipForReversalGuard` at line ~2584
- BUG-B primary target: `IsLiveEntryBlocked` at line ~5748; secondary: `DispatchCopy` at line ~2428
- `ShouldSkipForReversalGuard` vs `DispatchCopy`: ~156 lines apart (NOT within 50 lines)
- `ShouldSkipForReversalGuard` vs `IsLiveEntryBlocked`: ~3164 lines apart
- **Q1 answer: NO**

### Q2. Fix B design depends on Fix A final design?

T2 adds a `dispatched` counter to `DispatchCopy` and calls `SetLiveEntryDispatched` only when
`dispatched > 0`. This mechanism works regardless of WHY a follower is skipped — whether the
reversal guard (T1), daily cap check, or any other skip reason causes `dispatched == 0`. T2's
design does not reference, import, or depend on T1's implementation.

**Q2 answer: NO**

### Q3. Each fix has standalone value if the other is blocked?

- T1 standalone: Restores correct follower copy behavior after a leader reversal when followers
  have working (but unfilled) entry orders. Clear, independent correctness value.
- T2 standalone: Prevents `_liveEntryInstruments` phantom lock for ANY zero-dispatch scenario
  (not limited to the reversal guard). Guards future cases where all followers are skipped
  for any reason (daily cap, null account, etc.).

**Q3 answer: YES**

### Q4. Each fix has an independent SIM verification path?

- T1 SIM: Leader dispatches Buy (reversal). Follower has no filled position but has Working Sell
  entry order. Verify: follower receives the Buy copy.
- T2 SIM: All followers are skipped (any skip reason). Verify: next same-instrKey order is NOT
  blocked at gate5(a).

These are independent observable outcomes with no shared preconditions.

**Q4 answer: YES**

### LANE-SPLIT GATE RESULT: LANES-APPROVED

Default is single pipeline. Lanes require Q1=NO and Q2=NO (both met) AND Q3=YES and Q4=YES
(both met). Two independent tickets: T1 (BUG-A) and T2 (BUG-B).

---

## B. BASELINE MEASUREMENT

| Metric | Value |
|--------|-------|
| `[Fact]` count (measured) | 475 |
| `ShouldSkipForReversalGuard` CYC (current) | 3 |
| `IsLiveEntryBlocked` CYC (current) | 4 |
| `HasWorkingEntries` CYC (current) | 3 |
| `DispatchCopy` CYC (current, **from source branch count**) | **8** |
| `EvictDedup` CYC (current, per comment line 2776) | 6 |

**DispatchCopy CYC re-measurement** (lines 2428–2537, directly from source — stale comment at
line 2427 was NOT used):

| Branch | Line | Running CYC |
|--------|------|-------------|
| base | — | 1 |
| `if (IsExitSignalName(order.Name))` | 2431 | 2 |
| `if (!IsDispatchTriggerState(...))` | 2444 | 3 |
| `if (!IsDispatchableOrderType(...))` | 2457 | 4 |
| `if (IsLiveEntryBlocked(...))` | 2474 | 5 |
| `foreach (var acc in rule.FollowerAccounts)` | 2508 | 6 |
| `if (ShouldSkipFollowerDispatch(acc))` | 2510 | 7 |
| `if (ShouldSkipForReversalGuard(...))` | 2516 | **8** |

**Actual current CYC = 8.** The comment at line 2427 ("CYC<=6 after extraction") is stale — it
was written when TB-T4 extraction was completed. The subsequent DW-B128 addition of
`if (ShouldSkipForReversalGuard(...))` inside the foreach added a branch that the comment never
reflected. This comment must be updated as part of T2 (see Section D).

---

## C. T1 DESIGN — BUG-A Fix: `ShouldSkipForReversalGuard`

### Chosen option: A1 (with V-02 fix: `HasWorkingEntries` `.ToList()` promoted in-scope)

**Justification**:

Option A2 (clear `_lastLeaderDirection` on all-cancelled leader entries) introduces a race
window: `TryCancelFollowerEntries` may fire after the direction is cleared but before the
new direction is recorded. A1 modifies a single boolean assignment with one new operand and
has zero window risk. It is surgical, auditable, and directly addresses the confirmed root
cause (position-only flat check).

### Exact change

**File**: `src/PropTraderTools/CopyEngine.cs`

**Location**: `ShouldSkipForReversalGuard`, line ~2594

**Before**:
```csharp
bool followerIsFlat = IsFlat(FindPosition(acc, instr));
```

**After**:
```csharp
bool followerIsFlat = IsFlat(FindPosition(acc, instr))
                      && !HasWorkingEntries(acc, instr);
```

### Comment updates (same method header, lines 2578-2583)

Update the comment block above `ShouldSkipForReversalGuard` to reflect the new flat semantics:

**Before** (line 2580):
```
// CCN<=3: hasLastDirection + IsReversalToFlatFollower + IsFlat = 3 Lizard branches.
```

**After**:
```
// CCN<=4: hasLastDirection + IsReversalToFlatFollower + IsFlat + HasWorkingEntries = 4 Lizard branches.
```

Also update the semantic description line (line 2579):
```
// Returns true when hasLastDirection is true and follower is truly flat (no position AND
// no working entries) and direction reversed.
```

### DW-B128 reversal guard intent preserved

Original intent (per comment line 2578): block a reversal entry dispatch to a follower who is
already flat, to prevent the follower from accumulating a position in a direction the leader is
about to exit.

The fix narrows "flat" to its true meaning: a follower with a Working entry order is NOT flat —
it has a pending position commitment. Blocking the reversal dispatch in that case prevents the
follower from ADDING a second entry in the new direction on top of an existing working entry in
the old direction. The guard's purpose (avoid spurious double entries) is preserved; only the
false-positive path is eliminated.

`IsReversalToFlatFollower` (line 5916) is **unchanged** — it still returns `currentAction != lastAction && followerIsFlat`. The change is solely in how `followerIsFlat` is computed at the call site.

### CYC analysis

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `ShouldSkipForReversalGuard` | 3 | **4** | ≤8 ✓ |

Delta: +1 (`&&` operand on `followerIsFlat` assignment is one Lizard branch).

---

### C.3 — `HasWorkingEntries` `.ToList()` fix (in-scope, V-02 remediation)

**Status**: Promoted from DW-REPAIRS-03-01 (deferred) to T1 in-scope.

**Root cause**: `HasWorkingEntries` (source line 4840) enumerates `acc.Orders` without `.ToList()`.
`Account.Orders` is an NT8 live collection that can be mutated on a separate NT8 dispatcher thread
while `OnOrderUpdate` is executing. Enumerating without a snapshot can throw
`InvalidOperationException: Collection was modified`. The companion method `HasWorkingPttCopy`
(line 4861) already demonstrates the correct pattern: `acc.Orders.ToList()`.

T1 introduces a new call site for `HasWorkingEntries` inside the `DispatchCopy` gate chain (via
`ShouldSkipForReversalGuard`). Before T1, `HasWorkingEntries` was not on the `OnOrderUpdate` hot
path; after T1 it is. JS-001 ("no throw in gate chain") requires the `.ToList()` fix be in T1
scope.

**Exact change** (line 4842):

**Before**:
```csharp
foreach (var order in acc.Orders)
```

**After**:
```csharp
foreach (var order in acc.Orders.ToList())
```

**Comment update** (line 4839 — existing comment reads `// CYC=3`):

The plan review (Cycle 1) notes the actual CYC of `HasWorkingEntries` is 5, not 3 (base + foreach
+ `!=instrument` + `!=Working` + `!IsBracketLeg`). The source comment is stale. Update the comment
to reflect both the corrected CYC and the `.ToList()` thread-safety rationale:

**Before** (line 4839):
```
// CYC=3. Returns true if any working non-bracket order exists for the instrument.
```

**After**:
```
// CYC=5: foreach(1) + instrument check(2) + state check(3) + bracket check(4) + early-return path(5).
// Returns true if any working non-bracket order exists for the instrument.
// JS-001: acc.Orders.ToList() snapshot prevents InvalidOperationException on concurrent NT8 modification
//         (pattern mirrors HasWorkingPttCopy line 4861). Promoted in-scope by PTT-REPAIRS-03 V-02 fix.
```

**CYC impact**: `.ToList()` is a method call on the iterator source — it adds zero decision branches.
`HasWorkingEntries` CYC remains 5 (the stale source comment claiming 3 was already incorrect before
this fix). No CYC budget change. `HasWorkingEntries` CYC = 5 ≤ 8 ✓.

---

## D. T2 DESIGN — BUG-B Fix: `IsLiveEntryBlocked` / `DispatchCopy`

### Chosen option: B1 (split) + ShouldSkipFollower extraction

**Justification for B1 over B2**:

Option B2 (undo with TryRemove after zero-dispatch) was evaluated in two variants:

**B2a — conditional TryRemove** (`if (dispatched==0) { TryRemove }` after the loop):
  This adds a new `if`-branch inside `DispatchCopy` → +1 CYC. Since current CYC=8, and T2's
  `if (dispatched > 0)` also adds +1, the total would be CYC=10 (post-extraction: 7+1+1). Fails.
  Without extraction: current 8 + conditional TryRemove +1 = 9 before even adding dispatched>0
  guard. Fails regardless.

**B2b — unconditional TryRemove** (no `if`, always TryRemove after the loop, no-op if not set):
  No new CYC branch in DispatchCopy. However, this BREAKS the same-slot double-entry protection.
  Gate(a) (`_liveEntryInstruments.ContainsKey(instrKey)`) exists to block a SECOND distinct leader
  entry order for the same instrKey arriving before the first order fills or cancels. If we
  unconditionally TryRemove instrKey immediately after the dispatch loop, the gate(a) lock is
  released while the first dispatched order is still live. A concurrent second entry order for
  the same instrKey would bypass gate(a) and double-dispatch to followers. B2b is architecturally
  unsafe. Rejected.

**Conclusion**: B2 is not viable in any variant without either a CYC violation or a correctness
regression. B1 (split check from commit) remains the correct approach.

**Justification for ShouldSkipFollower extraction**:

With DispatchCopy actual CYC=8 (confirmed from source), B1's `if (dispatched > 0)` addition
would push CYC to 9, exceeding the JS-066 ≤8 budget. To create the required headroom, the two
per-follower skip guards inside the foreach loop are extracted into a single named helper
`ShouldSkipFollower`. This collapses two branches (7 + 8) into one (call to helper), reducing
DispatchCopy CYC from 8 to 7. After T2's +1 (the dispatched>0 guard), DispatchCopy CYC = 8.
Exactly at budget.

The extraction is behavior-preserving: `ShouldSkipFollower` calls `ShouldSkipFollowerDispatch`
first (same short-circuit order as before), then `ShouldSkipForReversalGuard`. No new logic is
introduced.

**Justification for B1 check/commit split**:

B1 also produces cleaner method contracts: the check predicate is a pure query; the setter is an
explicit commit. This is the JS-023 (immutable-where-possible) pattern applied to side effects.
The original `IsLiveEntryBlocked` had a subtle phantom hazard (writes maps before any follower
dispatch) that B1 eliminates at the source.

---

### New helper method: `ShouldSkipFollower`

**File**: `src/PropTraderTools/CopyEngine.cs`

**Purpose**: Extracted per-follower skip combinator for DispatchCopy. Collapses two skip-guard
branches into one to create CYC budget headroom for T2's dispatched>0 guard. Zero new logic.

```
Signature: private bool ShouldSkipFollower(Account acc, Instrument instr, OrderAction currentAction, OrderAction lastAction, bool hasLastDirection)
CYC: 3 (base + ShouldSkipFollowerDispatch check + ShouldSkipForReversalGuard check)
Side effects: NONE (pure predicate)
```

Logic (pseudo-code):
```
if (ShouldSkipFollowerDispatch(acc)) return true;
if (ShouldSkipForReversalGuard(acc, instr, currentAction, lastAction, hasLastDirection)) return true;
return false;
```

Comment to add above the method:
```
// Extracted from DispatchCopy loop to reduce DispatchCopy CYC budget (V-01 fix: PTT-REPAIRS-03).
// Short-circuits identically to the original two sequential ifs: ShouldSkipFollowerDispatch first,
// then ShouldSkipForReversalGuard. Zero new logic.
// CYC=3: base + dispatch-skip check + reversal-guard check.
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
```

---

### Three-map write decomposition

**Root cause of phantom**: `IsLiveEntryBlocked` writes to three maps BEFORE any follower dispatch:

| Map | Key | Written at |
|-----|-----|-----------|
| `_liveEntryInstruments` | instrKey | line 5756 |
| `_entryInstrKeyByOrderId` | orderId → instrKey | line 5757 |
| `_entryDispatchedOrders` | orderId | inside `IsEntryDispatched`, line 5735 |

All three writes must move to the post-dispatch commit path.

### New method: `IsLiveEntryBlocked_Check`

**File**: `src/PropTraderTools/CopyEngine.cs`

**Replaces**: `IsLiveEntryBlocked` at line 5748

```
Signature: private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)
CYC: 4 (3 guard branches + 1 base)
Side effects: NONE
```

Logic (pseudo-code):
```
if (_liveEntryInstruments.ContainsKey(instrKey)) return true;   // (a) instrKey gate
if (IsDedup(orderId, limitPrice)) return true;                   // (b) orderId dedup
if (_entryDispatchedOrders.ContainsKey(orderId)) return true;   // (c) dispatched guard (ContainsKey only, no TryAdd)
return false;
```

Note: `IsDedup` retains its existing side effect (`TryAdd` to `_dedupCache`). This is safe because
`_dedupCache` records the order event for idempotency across NT8 state transitions (Accepted +
Working for the same orderId). Recording early in `IsDedup` is correct even before dispatch; it
prevents the same order event from passing gate5 on a second NT8 callback.

### New method: `SetLiveEntryDispatched`

**File**: `src/PropTraderTools/CopyEngine.cs`

```
Signature: private void SetLiveEntryDispatched(string instrKey, string orderId)
CYC: 1
Side effects: writes _liveEntryInstruments, _entryInstrKeyByOrderId, _entryDispatchedOrders
```

Logic (pseudo-code):
```
_liveEntryInstruments.TryAdd(instrKey, 0);
_entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
_entryDispatchedOrders.TryAdd(orderId, 0);
```

### `DispatchCopy` changes

**File**: `src/PropTraderTools/CopyEngine.cs`, `DispatchCopy` method (line ~2428)

**Four changes to `DispatchCopy`** (three from B1 + one extraction for CYC headroom):

**Change 0 — ShouldSkipFollower extraction (CYC fix, PTT-REPAIRS-03 V-01 remediation)**:

Replace the two inner-loop skip-guard if-blocks with a single call to `ShouldSkipFollower`.

Before (lines 2510–2528):
```csharp
if (ShouldSkipFollowerDispatch(acc))
{
    idx++;
    continue;
}

if (
    ShouldSkipForReversalGuard(
        acc,
        instr,
        currentAction,
        lastAction,
        hasLastDirection
    )
)
{
    idx++;
    continue;
}
```

After:
```csharp
if (ShouldSkipFollower(acc, instr, currentAction, lastAction, hasLastDirection))
{
    idx++;
    continue;
}
```

1. **Gate 5 call-site (line ~2474)**: Replace `IsLiveEntryBlocked(...)` with `IsLiveEntryBlocked_Check(...)`.

2. **Follower loop (lines 2507-2532)**: Add `int dispatched = 0;` before the loop.
   Increment `dispatched++` inside the loop body, after `DispatchToFollower(...)` returns
   (i.e., only when no skip path was taken for that follower).

3. **After the loop (after line 2532, before line 2534)**: Add:
   ```csharp
   if (dispatched > 0)
       SetLiveEntryDispatched(instrKey, orderId);
   ```

**DispatchCopy comment update (line 2427)**:

The stale comment must be replaced. Before:
```
// CYC<=6 after extraction. JS-001: no throw in hot path. JS-021: no lock.
```

After:
```
// CYC=8 after ShouldSkipFollower extraction + T2 dispatched-guard (PTT-REPAIRS-03).
// JS-001: no throw in hot path. JS-021: no lock.
```

**Full DispatchCopy structure after all four changes** (structural outline, not verbatim code):
```
private void DispatchCopy(Order order, CopyRule rule)
{
    // Gate 0.5 [branch 2]
    if (IsExitSignalName(order.Name)) { log; return; }
    // Gate 3 [branch 3]
    if (!IsDispatchTriggerState(...)) { log; return; }
    // Gate 4 [branch 4]
    if (!IsDispatchableOrderType(...)) { log; return; }
    // Gate 5 [branch 5]
    var orderId = ...;
    var instrKey = ...;
    if (IsLiveEntryBlocked_Check(instrKey, orderId, order.LimitPrice)) { log; return; }
    // build baseSignal, baseQty, snapshot instr/direction
    int dispatched = 0;
    int idx = 0;
    foreach (var acc in rule.FollowerAccounts) // [branch 6]
    {
        if (ShouldSkipFollower(acc, instr, currentAction, lastAction, hasLastDirection)) // [branch 7]
        { idx++; continue; }
        DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
        idx++; dispatched++;
    }
    if (dispatched > 0)                         // [branch 8]
        SetLiveEntryDispatched(instrKey, orderId);
    _lastLeaderDirection[instr.FullName] = currentAction;
}
```

**CYC delta for `DispatchCopy`**:
  - Pre-extraction (measured from source): 8
  - After ShouldSkipFollower extraction (Change 0): 7  (−1: two skip-guard branches → one)
  - After T2 changes 1–3 (gate5 rename, dispatched counter, if(dispatched>0)): 8  (+1: the `if (dispatched > 0)`)
  - **Final CYC = 8 ≤ 8 ✓**

### Disposition of original `IsLiveEntryBlocked`

The original `IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)` at line
5748 is **deleted**. It has exactly one call site (`DispatchCopy` line 2474), which is updated
to call `IsLiveEntryBlocked_Check`. No other callers exist.

The original `IsEntryDispatched` private method (line 5731) is **retained but modified**:
its `TryAdd` side effect on `_entryDispatchedOrders` is **removed** (the TryAdd moves to
`SetLiveEntryDispatched`). `IsEntryDispatched` becomes a pure `ContainsKey` check:

**Before** (lines 5731-5737):
```csharp
private bool IsEntryDispatched(string orderId)
{
    if (_entryDispatchedOrders.ContainsKey(orderId))
        return true;
    _entryDispatchedOrders.TryAdd(orderId, 0);   // side effect, remove
    return false;
}
```

**After**:
```csharp
private bool IsEntryDispatched(string orderId)
{
    return _entryDispatchedOrders.ContainsKey(orderId);
}
```

CYC: 2 → 1. Comment update required: remove "Side-effect on first call: TryAdd records the
orderId as dispatched." and update CYC annotation from 2 to 1.

### MGC guard verification (DW-B142-MGC-02)

The MGC cancel+resubmit protection relies on three gates. Verify each is preserved by B1:

**Gate 5(a)**: `_liveEntryInstruments.ContainsKey(instrKey)` — blocks resubmit while original
order is still live.
- B1 change: `SetLiveEntryDispatched` is called only when `dispatched > 0`. When an order IS
  dispatched, instrKey IS set. Cancel → `EvictDedup` Cancelled branch removes instrKey via
  `_entryInstrKeyByOrderId`. Resubmit → instrKey absent → passes 5(a) → legitimate re-dispatch. ✓
- If dispatched was 0 (all followers skipped): instrKey is never set → gate5(a) never blocks the
  subsequent order. This is correct: if no followers received the original, the next order for
  the same instrKey should pass freely. ✓

**Gate 5(b)**: `IsDedup(orderId, limitPrice)` — unchanged. Same orderId + price arriving twice
(NT8 double-fire on Accepted + Working) is blocked. `IsDedup`'s early side effect in
`IsLiveEntryBlocked_Check` is preserved. ✓

**Gate 5(c)**: `_entryDispatchedOrders.ContainsKey(orderId)` in `IsLiveEntryBlocked_Check` —
pure check only.
- When `dispatched > 0`: `SetLiveEntryDispatched` writes orderId to `_entryDispatchedOrders`.
  Subsequent same orderId passes through check → ContainsKey=true → blocked. ✓
- When `dispatched == 0`: orderId is NOT in `_entryDispatchedOrders`. This is correct — we
  never dispatched for this orderId, so it should not be treated as "already dispatched." ✓

**`EvictDedup` (unchanged)**: Cancelled branch removes from `_entryDispatchedOrders`,
`_entryInstrKeyByOrderId`, `_liveEntryInstruments`. Filled branch removes from
`_entryInstrKeyByOrderId`, `_liveEntryInstruments`. No changes to `EvictDedup` required. ✓

**MGC GUARD RESULT: VERIFIED. B1 + ShouldSkipFollower extraction preserves DW-B142-MGC-02 at all three gate levels.**

### CYC analysis

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `IsLiveEntryBlocked` (original) | 4 | **deleted** | — |
| `ShouldSkipFollower` (new) | — | **3** | ≤8 ✓ |
| `IsLiveEntryBlocked_Check` (new) | — | **4** | ≤8 ✓ |
| `SetLiveEntryDispatched` (new) | — | **1** | ≤8 ✓ |
| `IsEntryDispatched` | 2 | **1** | ≤8 ✓ |
| `DispatchCopy` | **8** (measured) | **7** (post-extraction) → **8** (post-T2) | ≤8 ✓ |

---

## E. TEST DESIGN

### Baseline: 475 [Fact] methods. Two new tests. Final target: 477.

`ShouldSkipFollower` requires no dedicated test — it is a pure extraction of existing branches
with no new logic. It is exercised indirectly by the T2 test's follower loop path and by any
existing test that exercises the DispatchCopy loop. Adding a test for a behavior-preserving
extraction is out of scope per the minimal-change rule.

---

### T1 Test

**Test method name**: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Spec requirement satisfied**: BUG-A fix (Option A1)

**Scenario**: Follower has no filled position (IsFlat returns true) but HAS a working entry order
(HasWorkingEntries returns true). Leader dispatches a Buy (reversal from last=Sell).
With the fix: `followerIsFlat = true && !true = false` → `IsReversalToFlatFollower(Buy, Sell, false) = false`
→ `ShouldSkipForReversalGuard` returns `false` → dispatch allowed.

**Assert**: `ShouldSkipForReversalGuard(acc, instr, OrderAction.Buy, OrderAction.Sell, hasLastDirection: true)` returns `false`.

**Harness pattern**: Since `Account` is an NT8 type not constructable in xUnit, this test must
use one of two patterns:
- **Preferred (if Account can be stubbed via InternalsVisibleTo)**: Use a test-double Account
  with an Orders collection containing one Working non-bracket order for the instrument.
- **Fallback**: Test the two sub-predicates independently:
  - Assert `HasWorkingEntries` returns `true` when a working non-bracket order is present (this
    may require its own direct test via the existing Account mock pattern).
  - Assert `IsReversalToFlatFollower(OrderAction.Buy, OrderAction.Sell, followerIsFlat: false)`
    returns `false` (pure static call, no NT8 types needed).

The engineer must implement whichever pattern is consistent with the existing test infrastructure.
The ticket will specify both options; the engineer chooses based on what the harness supports.

**What this test asserts**:
1. When `HasWorkingEntries` returns `true` for the follower, the computed `followerIsFlat` is
   `false`, and the reversal guard returns `false` (no skip). Dispatch is allowed.

---

### T2 Test

**Test method name**: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Spec requirement satisfied**: BUG-B fix (Option B1)

**Scenario**: A `DispatchCopy` call runs but all followers are skipped (zero dispatch). After the
call, `_liveEntryInstruments` must NOT contain the instrKey. A subsequent `IsLiveEntryBlocked_Check`
call for the same instrKey must return `false` (not blocked).

**Arrange**:
- Instantiate `CopyEngine` (or use the test engine wrapper already established in the suite).
- Configure a `CopyRule` with at least one follower account.
- Configure all follower accounts to fail `ShouldSkipFollower` (i.e., fail
  `ShouldSkipFollowerDispatch`, e.g. null account or daily cap exceeded for all) — this produces
  `dispatched == 0` without requiring T1's reversal guard scenario, making T2 independently
  verifiable.
- Alternatively: configure all followers to be in reversal-guard state (if Account/position
  mocking is available in the test harness).

**Act**: Call `DispatchCopy` with an order for the configured instrument+direction.
  Note: `DispatchCopy` is `private`. The test must invoke it via `OnOrderUpdate` (which calls
  `DispatchCopy` internally) OR the engineer must promote `DispatchCopy` to `internal` for the
  tests assembly (following the `InternalsVisibleTo` pattern at line 46).

**Assert**:
1. `engine._liveEntryInstruments.ContainsKey(instrKey)` is `false` after the call.
2. A second call to `IsLiveEntryBlocked_Check(instrKey, newOrderId, price)` returns `false`
   (the instrKey is not phantom-locked).

**What this test asserts**: That the B1 split correctly prevents `_liveEntryInstruments` from
being set when no followers were dispatched.

---

## F. PTT-DIAG LOG PRESERVATION

All existing diagnostic log lines are **PERMANENT** and must not be removed or modified by the
engineer. This block introduces no new log lines. The following log statements are preserved:

| Location | Log prefix | Preserved |
|----------|-----------|-----------|
| `DispatchCopy` gate0.5 | `[PTT-COPY-DIAG] gate0.5 exit:` | ✓ |
| `DispatchCopy` gate3 | `[PTT-COPY-DIAG] gate3 exit:` | ✓ |
| `DispatchCopy` gate4 | `[PTT-COPY-DIAG] gate4 exit:` | ✓ |
| `DispatchCopy` gate5 | `[PTT-COPY-DIAG] gate5 exit:` | ✓ |
| `ShouldSkipForReversalGuard` guard | `[PTT-COPY-GUARD] skip reversal entry:` | ✓ |

The gate5 exit log (lines 2476-2482) fires on `IsLiveEntryBlocked_Check` returning `true`. Since
`IsLiveEntryBlocked_Check` is the direct replacement for `IsLiveEntryBlocked` at gate5, the log
fires under the same conditions as before. ✓

The `ShouldSkipFollower` extraction does NOT affect the `[PTT-COPY-GUARD]` log — that log fires
inside `ShouldSkipForReversalGuard` itself (line ~2597-2605), not in the DispatchCopy loop
call site. The extraction leaves all log-emitting code inside the original methods. ✓

---

## G. CONSTRAINTS CHECKLIST

| Constraint | T1 | T2 |
|------------|----|----|
| No `lock()` anywhere | ✓ (no new locks) | ✓ (no new locks) |
| ASCII-only identifiers/strings | ✓ | ✓ (`ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`, `dispatched` all ASCII) |
| No `DateTime.Now` | ✓ (not applicable) | ✓ (not applicable) |
| No `FontFamily` | ✓ (not applicable) | ✓ (not applicable) |
| No hex color literals | ✓ (not applicable) | ✓ (not applicable) |
| JS-001: no throw in hot path | ✓ (`&&` is safe; `HasWorkingEntries` uses `.ToList()` snapshot — no concurrent-modification throw) | ✓ (ContainsKey/TryAdd no-throw; ShouldSkipFollower delegates to no-throw methods) |
| JS-002: predicate returns bool | ✓ | ✓ (`IsLiveEntryBlocked_Check`, `ShouldSkipFollower` return bool) |
| JS-003: no mutable shared state w/o lock-free idiom | ✓ | ✓ (ConcurrentDictionary only) |
| JS-021: no `lock()`, use lock-free structures | ✓ | ✓ |
| JS-023: immutable where possible | ✓ | ✓ (check path is now pure; `ShouldSkipFollower` is pure) |
| JS-025: ConcurrentDictionary TryRemove lock-free | ✓ | ✓ (no new TryRemove) |
| JS-033: no `DateTime.Now` | ✓ | ✓ |
| JS-042: ASCII-only | ✓ | ✓ |
| JS-066: CYC≤8 per method | ✓ (`ShouldSkipForReversalGuard`: 4) | ✓ (`ShouldSkipFollower`: 3; `IsLiveEntryBlocked_Check`: 4; `SetLiveEntryDispatched`: 1; `IsEntryDispatched`: 1; `DispatchCopy`: 8) |
| No `.cs` edits in architect phase | ✓ (plan only) | ✓ (plan only) |

---

## H. COMPONENT SUMMARY

### T1 — BUG-A Fix

| Artifact | Change |
|----------|--------|
| `ShouldSkipForReversalGuard` (line ~2594) | One-line change: `&& !HasWorkingEntries(acc, instr)` |
| Comment block above `ShouldSkipForReversalGuard` (line ~2579-2580) | Update CCN and semantic description |
| `HasWorkingEntries` (line 4840) | **`.ToList()` fix** — `foreach (var order in acc.Orders)` → `foreach (var order in acc.Orders.ToList())` (V-02 remediation, DW-REPAIRS-03-01 promoted in-scope) |
| Comment at `HasWorkingEntries` (line 4839) | Update stale `// CYC=3` to `// CYC=5` with `.ToList()` thread-safety note (mirrors `HasWorkingPttCopy` pattern) |
| `IsReversalToFlatFollower` (line 5916) | **No change** |
| Test: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` | New [Fact] |

### T2 — BUG-B Fix

| Artifact | Change |
|----------|--------|
| `ShouldSkipFollower` (new) | New method: extracted per-follower skip combinator, CYC=3; reduces DispatchCopy CYC 8→7 |
| `IsLiveEntryBlocked` (line ~5748) | **Deleted** (replaced by `IsLiveEntryBlocked_Check`) |
| `IsLiveEntryBlocked_Check` (new) | New method: pure predicate, CYC=4 |
| `SetLiveEntryDispatched` (new) | New method: three TryAdds, CYC=1 |
| `IsEntryDispatched` (line 5731) | Simplified: remove TryAdd side effect, CYC 2→1; comment updated |
| `DispatchCopy` (line ~2427–2537) | Four changes: loop extraction (Change 0), gate5 call-site rename, dispatched counter, post-loop setter; stale CYC comment updated |
| `EvictDedup` (line 5778) | **No change** |
| Test: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped` | New [Fact] |

---

## I. DEFERRED BACKLOG STATUS

| ID | Item | Status |
|----|------|--------|
| DW-REPAIRS-02-02 | [Fact] count baseline — measure fresh at next block start | **CLOSED** (measured: 475) |
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` NOT cleared in Filled branch | OPEN — not in scope PTT-REPAIRS-03 |
| DW-B24-02 | Manual E2E runtime verification (escalated) | OPEN — runtime only |
| DW-B24-03 | Skip-duplicate guard `[Fact]` | OPEN — out of scope |
| DW-B25-01 | Companion field race | OPEN — out of scope |
| DW-B26-01 | Reflection test upgrade | OPEN — out of scope |
| DW-REPAIRS-01-01 | R5 `Account.All` constructor-path risk | OPEN — out of scope |
| DW-REPAIRS-01-02 | `TryCancelBeOrders` wrapper `-1`-path test | OPEN — out of scope |
| DW-B24-01 | NT8-043 formal rule entry | OPEN — out of scope |

**Deferred items from this block**: No new deferred items.

DW-REPAIRS-03-01 has been promoted to T1 in-scope (see Section C.3). It is no longer deferred.

---

*ptt-architect · PTT-REPAIRS-03 · 2026-09-08 (Revision 2 — V-02 corrected)*
