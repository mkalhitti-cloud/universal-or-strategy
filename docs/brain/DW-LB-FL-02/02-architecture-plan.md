# DW-LB-FL-02 Architecture Plan
# Clone mode + BE ALL -- Infinite PTT-Flatten Loop Fix

**Epic**: DW-LB-FL-02  
**Severity**: P1  
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PENDING (re-architecture after REVIEW_FAIL V-01)  
**Architect**: ptt-architect  
**Date**: 2026-08-22 (revised)  
**Revision**: 2 -- V-01 CYC resolution: extract IsDispatchableState from guard 1

---

## RULES CATALOG GATE: PASS

Pre-flight scan result against docs/standards/jane-street/RULES_CATALOG.md:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock ban) | No lock() in new code | PASS |
| JS-001 (no throw in hot path) | New guards return bool, no throw | PASS |
| JS-002 (no return null) | TryDispatchLeaderFlat returns bool | PASS |
| JS-033 (no async void) | No new async methods | PASS |
| JS-036/037 (no hot-path alloc) | No new allocations in any guard | PASS |
| ASCII-only | All identifiers and strings are ASCII | PASS |
| DateTime.Now ban | No DateTime usage | PASS |
| CYC <= 8 | TryDispatchLeaderFlat: 8 (at limit), IsDispatchableState: 2, IsNativeExitOnFlatLeader: 2 | PASS |

Gate result: **PASS -- work proceeds.**

---

## NT8 API GATE: PASS

Facts verified against docs/standards/NT8_FULL_REFERENCE.md + docs/standards/NT8_ADDON_KNOWLEDGE.md:

| API | Scope | Used In Fix |
|-----|-------|-------------|
| Account.Positions / Position.Quantity | AddOnBase readable | hasOpenPosition delegate (existing) |
| Account.Cancel() + CreateOrder() + Submit() | AddOnBase confirmed | Not touched in this fix |
| AtmStrategyCreate() | StrategyBase ONLY -- NOT AddOnBase | Not used |
| AtmStrategyChangeStopTarget() | StrategyBase ONLY -- NOT AddOnBase | Not used |
| Account.Change() | AddOnBase (silent no-op on ATM brackets) | Not used |

The new guard calls the existing `hasOpenPosition` delegate which reads `acc.Positions` from the
NT8 dispatch thread (OnOrderUpdate). NT8_FULL_REFERENCE.md confirms Positions is readable from
dispatch thread. No Dispatcher.InvokeAsync required for this read.

---

## LANE-SPLIT GATE

**Q1. Same method or within 50 lines?**
YES. New guard is inserted into TryDispatchLeaderFlat (line ~4686). Both new helpers
(`IsDispatchableState`, `IsNativeExitOnFlatLeader`) are placed adjacent (within 20 lines of
existing `HasOpenPosition` at 4651). All changes in `src/PropTraderTools/CopyEngine.cs`.
One logical unit.

**Q2. Fix B design depends on Fix A final design?**
N/A. Option A is chosen as the sole fix. No Fix B is being implemented.
If Option B were added later, it would be independent (different method: FlattenFollower).

**Q3. Each fix has standalone value if the other is blocked?**
N/A. Single fix. Option A alone eliminates the root cause entirely. Option B (cancel-before-flatten
in FlattenFollower) was evaluated and rejected: CancelAllAccountOrders already cancels all orders
including PTT-BE-Stop-*; the race condition cannot be fully eliminated at that layer; and the
`IsAccountFlattenable` guard in FlattenOneAccount (B76 HOTFIX-B76-FLATTEN-GUARD-01) already
handles the in-flight flatten guard. Option A is both necessary and sufficient.

**Q4. Each fix has an independent SIM verification path?**
YES. Single SIM verification path defined in NT8 SIM Gate Steps below.

### LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

One ticket. One method change. Two helpers extracted/added. One SIM verification path.

---

## CONFIRMED ROOT CAUSES

### RC-1: IsNativeExitName bypass fires on already-flat leader

**Location**: [`TryDispatchLeaderFlat`](src/PropTraderTools/CopyEngine.cs:4669) guard (3)

Current guard (3) logic:
```csharp
if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument))
    return false; // (3)
```

When `IsNativeExitName("Close") == true`, guard (3) evaluates as:
`!true && anything == false` -- the guard NEVER blocks for native exit names,
regardless of whether the leader has an open position.

This was intentional for DW-B65-01 (position lag): when the leader's native close
order fills, `acc.Positions` has not yet updated (NT8_FULL_REFERENCE.md line 1721:
"Changes to positions will not be reflected till at least the next OnBarUpdate()").
Bypassing the guard ensures immediate propagation to followers.

**The defect**: when the leader was already made flat by an EARLIER PTT-BE-Stop-* fill,
and the user then clicks Close on ChartTrader (which generates a new "Close" order on the
already-flat leader account), the bypass still fires. Followers still hold Working PTT-BE-Stop-*
orders. PTT-Flatten + Working StopMarket on flat follower = inverted position = loop.

### RC-2: IsNonFlatDispatchName does not block "Close" on flat account

**Location**: [`IsNonFlatDispatchName`](src/PropTraderTools/CopyEngine.cs:2379)

`IsNonFlatDispatchName` blocks: PTT- prefix, "Entry", ATM bracket names (Stop1..Stop9, Target1..Target9).
It does NOT and SHOULD NOT block "Close" generically -- "Close" is a valid exit trigger when the
leader has a position. The fix belongs in `TryDispatchLeaderFlat`, not here.

---

## CHOSEN FIX: OPTION A -- Minimal Guard in TryDispatchLeaderFlat

### Rationale for Option A over Option B

Option B would cancel PTT-BE-Stop-* in `FlattenFollower` or `FlattenOneAccount` before issuing
PTT-Flatten. This approach was rejected because:

1. `CancelAllAccountOrders` (B69 DW-B69-01) already cancels ALL active orders before flatten.
   Adding a targeted PTT-BE-Stop-* cancel before the all-cancel is redundant.
2. The race condition (PTT-BE-Stop fills between cancel submission and ACK) cannot be eliminated
   at the follower-cancel layer. B76 HOTFIX-B76-FLATTEN-RACE-01 already addresses the
   post-cancel position re-read. Adding another cancel layer does not help.
3. Option B addresses the CONSEQUENCE (follower inversion) not the ROOT CAUSE (spurious dispatch
   when leader is flat). Option A blocks at the source.
4. Option B would increase CYC in `FlattenFollower` (CYC=3 currently) and require new name-matching
   logic, creating regression risk in the critical flatten path.

Option A is minimal, targeted, and fully preserves DW-B65-01.

---

## COMPONENT DESIGN

### File Changed

`src/PropTraderTools/CopyEngine.cs` -- ONE file, THREE changes:
1. New internal static helper method `IsDispatchableState` (add near line 4658, before TryDispatchLeaderFlat)
2. New internal static helper method `IsNativeExitOnFlatLeader` (add after IsDispatchableState)
3. Modified `TryDispatchLeaderFlat` body: guard 1 uses `IsDispatchableState`, guard 3.5 inserted

### New Method: IsDispatchableState

**Purpose**: Extracts the compound state condition from guard (1) of TryDispatchLeaderFlat.
This extraction is required to keep TryDispatchLeaderFlat at CYC=8 after guard 3.5 is added
(V-01 resolution: moves the `&&` out of the caller, reclaiming 1 decision-point budget).

```csharp
// DW-LB-FL-02 (V-01 extraction): extracted from TryDispatchLeaderFlat guard (1).
// Returns true when the order state is one that triggers a flat dispatch (Filled or Cancelled).
// Extraction moves the && out of TryDispatchLeaderFlat, freeing 1 CYC budget for guard (3.5).
// CYC=2: 1 base + 1 boolean short-circuit (||).
// JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
internal static bool IsDispatchableState(OrderState state)
{
    return state == OrderState.Filled || state == OrderState.Cancelled;
}
```

**Signature**: `internal static bool IsDispatchableState(OrderState state)`  
**Return type**: `bool`  
**CYC**: 2 (1 base + 1 `||` short-circuit)  
**Allocation**: None  
**Equivalence**: `IsDispatchableState(s)` is logically equivalent to
`s == OrderState.Filled || s == OrderState.Cancelled`, which is the De Morgan negation of the
original `s != OrderState.Filled && s != OrderState.Cancelled`. No semantic change.

### New Method: IsNativeExitOnFlatLeader

**Purpose**: Returns true when a native exit order arrived on an already-flat leader account.
In that state the DW-B65-01 bypass must NOT propagate a PTT-Flatten to followers because no
follower position needs closing.

```csharp
// DW-LB-FL-02: Guard helper -- returns true when a native exit order arrived on an
// already-flat leader account. In that state the DW-B65-01 bypass must NOT propagate
// a PTT-Flatten to followers because no follower position needs closing.
// CYC=2: 1 base + 1 boolean short-circuit (&&).
// JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
internal static bool IsNativeExitOnFlatLeader(
    string orderName,
    Account account,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition
)
{
    return IsNativeExitName(orderName) && !hasOpenPosition(account, instrument);
}
```

**Signature**: `internal static bool IsNativeExitOnFlatLeader(string orderName, Account account, Instrument instrument, Func<Account, Instrument, bool> hasOpenPosition)`  
**Return type**: `bool`  
**CYC**: 2 (1 base + 1 `&&` short-circuit)  
**Allocation**: None  
**Thread safety**: Reads `acc.Positions` via injected delegate -- same thread safety contract as existing guard (3)

### Modified Method: TryDispatchLeaderFlat

**Exact current signature (from source):**
```csharp
private static bool TryDispatchLeaderFlat(
    Account account,
    Instrument instrument,
    OrderState state,
    string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne
)
```

**Signature unchanged.** Only the body changes:
- Guard 1: replace direct `&&` comparison with `!IsDispatchableState(state)`
- Guard 3.5: insert new `if (IsNativeExitOnFlatLeader(...))` between guard 2.5 and guard 3

**New guard 3.5 -- exact C# code:**
```csharp
if (IsNativeExitOnFlatLeader(orderName, account, instrument, hasOpenPosition))
    return false; // (3.5) DW-LB-FL-02: native exit on already-flat leader -- nothing to propagate
```

**Full modified method body (exact -- engineer implements verbatim):**
```csharp
{
    if (!IsDispatchableState(state))
        return false; // (1)
    if (isFollower(account))
        return false; // (2)
    if (IsNonFlatDispatchName(orderName))
        return false; // (2.5+2.6)
    if (IsNativeExitOnFlatLeader(orderName, account, instrument, hasOpenPosition))
        return false; // (3.5) DW-LB-FL-02: native exit on already-flat leader -- nothing to propagate
    if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument))
        return false; // (3)
    foreach (var acc in rule.FollowerAccounts) // (4)
        FlattenFollower(acc, instrument, hasOpenPosition, flattenOne); // DW-B91-B
    return true;
}
```

**Updated comment block for TryDispatchLeaderFlat:**
Replace the existing comment starting at line 4659 with:
```
// B65 T1 / DW-B91-B / DW-LB-FL-02: TryDispatchLeaderFlat -- CYC=8 (strict McCabe, at limit).
// Guards: (1) state via IsDispatchableState, (2) follower, (2.5+2.6) non-flat-dispatch name,
// (3.5) native-exit on flat leader (DW-LB-FL-02), (3) open-position race-safe, (4) foreach follower.
// DW-B91-B: foreach body extracted to FlattenFollower. DW-LB-FL-02: guard (3.5) -- when a native
// NT8 exit fires on a leader account that is already flat (e.g. user clicks Close after
// PTT-BE-Stop filled), skip dispatch. Preserves DW-B65-01: when leader HAS position,
// IsNativeExitOnFlatLeader returns false and guard (3.5) does not block.
// DW-LB-FL-02 V-01: guard (1) uses IsDispatchableState to free 1 CYC budget for guard (3.5).
// JS-021: no lock. JS-001: no throw. JS-002: no null return.
```

---

## CYC COMPLEXITY ANALYSIS

**Source measurement**: TryDispatchLeaderFlat live source at CopyEngine.cs:4679-4691 (read 2026-08-22).
Stale source comment at line 4659 reads "CYC=6" -- that comment pre-dates guard (2.5) and is WRONG.
Live CYC is measured here from the actual source, not the comment.

**Pre-fix CYC measurement** (from source lines 4679-4691):
- Guard (1): `if (state != Filled && state != Cancelled)` = if(1) + &&(1) = 2
- Guard (2): `if (isFollower(account))` = 1
- Guard (2.5): `if (IsNonFlatDispatchName(orderName))` = 1
- Guard (3): `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` = if(1) + &&(1) = 2
- foreach (4): = 1
- Total decision points: 2+1+1+2+1 = 7. CYC_before = 1+7 = **8**

**Post-fix CYC measurement** (after IsDispatchableState extraction + guard 3.5 insertion):
- Guard (1): `if (!IsDispatchableState(state))` = if(1) -- && moved to helper = **1**
- Guard (2): `if (isFollower(account))` = 1
- Guard (2.5): `if (IsNonFlatDispatchName(orderName))` = 1
- Guard (3.5): `if (IsNativeExitOnFlatLeader(...))` = if(1) -- && lives in helper = **1** (NEW)
- Guard (3): `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` = if(1) + &&(1) = 2
- foreach (4): = 1
- Total decision points: 1+1+1+1+2+1 = 7. CYC_after = 1+7 = **8**

| Method | Before (live source) | After (post-fix) | Limit | Status |
|--------|---------------------|------------------|-------|--------|
| TryDispatchLeaderFlat | 8 | 8 | 8 | PASS (at limit) |
| IsDispatchableState | N/A (new) | 2 | 8 | PASS |
| IsNativeExitOnFlatLeader | N/A (new) | 2 | 8 | PASS |
| IsNativeExitName | 6 (unchanged) | 6 | 8 | PASS |
| FlattenFollower | 3 (unchanged) | 3 | 8 | PASS |
| FlattenOneAccount | 2 (unchanged) | 2 | 8 | PASS |

Note: `IsDispatchableState` extraction is the V-01 resolution. It moves the `&&` from guard (1)
into the helper body, freeing exactly 1 decision-point budget in `TryDispatchLeaderFlat`.
Guard (3.5) then consumes that budget (+1 for the `if`). Net CYC change to `TryDispatchLeaderFlat`: 0.
Post-fix CYC = 8 = pre-fix CYC = at limit = PASS.

---

## DATA FLOW: FIXED PATH

```
NT8 OnOrderUpdate fires:
  order.Name = "Close"
  order.State = Filled
  order.Account = LEADER (already flat -- PTT-BE-Stop filled earlier)

TryDispatchLeaderFlat(account=LEADER, instrument=ES, state=Filled, orderName="Close", ...)
  Guard 1: IsDispatchableState(Filled) == true => !true == false -- not blocked (passes)
  Guard 2: isFollower(LEADER) == false -- not blocked (passes)
  Guard 2.5: IsNonFlatDispatchName("Close") == false -- not blocked (passes)
  Guard 3.5: IsNativeExitOnFlatLeader("Close", LEADER, ES, hasOpenPosition)
             = IsNativeExitName("Close") && !hasOpenPosition(LEADER, ES)
             = true && !false
             = true && true
             = true
             --> RETURNS FALSE (exits method, no follower dispatch)

RESULT: Zero PTT-Flatten orders created. Loop never starts.
```

## DATA FLOW: DW-B65-01 REGRESSION CHECK (MUST PASS)

```
NT8 OnOrderUpdate fires:
  order.Name = "Close"
  order.State = Filled
  order.Account = LEADER (HAS position -- just closing, NT8 position lag)
  hasOpenPosition(LEADER, ES) == true (NT8 position still shows open)

TryDispatchLeaderFlat(...)
  Guard 1: IsDispatchableState(Filled) == true => !true == false -- passes
  Guard 2: passes
  Guard 2.5: passes
  Guard 3.5: IsNativeExitOnFlatLeader("Close", LEADER, ES, hasOpenPosition)
             = IsNativeExitName("Close") && !hasOpenPosition(LEADER, ES)
             = true && !true
             = true && false
             = false
             --> DOES NOT BLOCK

  Guard 3: !IsNativeExitName("Close") && hasOpenPosition(LEADER, ES)
           = !true && anything
           = false
           --> DOES NOT BLOCK

  foreach follower --> FlattenFollower --> followers flattened ✓

DW-B65-01 PRESERVED.
```

---

## NT8 THREADING MODEL

`TryDispatchLeaderFlat` is called from `OnOrderUpdate` (NT8 dispatch thread).
The new guard calls `IsNativeExitOnFlatLeader` which calls `hasOpenPosition(account, instrument)`.
`hasOpenPosition` is the existing `HasOpenPosition` instance method delegate, which reads
`acc.Positions` from the NT8 dispatch thread -- same thread safety contract as existing guard (3).
`IsDispatchableState` is a pure value comparison with no external reads.
No `Dispatcher.InvokeAsync` required. No thread-safety regression.

---

## 7-SCAN CHECKLIST

### SCAN-01: lock() scan
```
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: zero matches in modified methods. No lock() in `IsDispatchableState`,
`IsNativeExitOnFlatLeader`, or updated `TryDispatchLeaderFlat`. **PASS.**

### SCAN-02: async void scan
```
grep -n "async void " src/PropTraderTools/CopyEngine.cs
```
Expected: zero new `async void` (no new async methods introduced by this fix). **PASS.**

### SCAN-03: return null scan
```
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
Expected: no new `return null` in changed methods. `IsDispatchableState` returns `bool`.
`IsNativeExitOnFlatLeader` returns `bool`. `TryDispatchLeaderFlat` returns `bool`. **PASS.**

### SCAN-04: CYC complexity check
Manual McCabe count of modified/new methods:

`TryDispatchLeaderFlat` (after change) -- decision points:
- Guard (1): `if (!IsDispatchableState(state))` = 1 branch (&&moved to helper)
- Guard (2): `if (isFollower(account))` = 1
- Guard (2.5): `if (IsNonFlatDispatchName(orderName))` = 1
- Guard (3.5): `if (IsNativeExitOnFlatLeader(...))` = 1
- Guard (3): `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` = 2 (if + &&)
- foreach (4): = 1
- Total: 7. CYC = 1+7 = **8 (at limit). PASS.**

`IsDispatchableState`:
- `return state == OrderState.Filled || state == OrderState.Cancelled;` = 1 base + 1 `||`
- CYC = **2. PASS.**

`IsNativeExitOnFlatLeader`:
- `return IsNativeExitName(orderName) && !hasOpenPosition(account, instrument);` = 1 base + 1 `&&`
- CYC = **2. PASS.**

### SCAN-05: ASCII-only scan
```
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
New identifiers: `IsDispatchableState`, `IsNativeExitOnFlatLeader`, `orderName`, `account`,
`instrument`, `hasOpenPosition`, `state` -- all ASCII. Comment text is ASCII. **PASS.**

### SCAN-06: NT8 API compliance
- No `AtmStrategyCreate()` -- PASS
- No `AtmStrategyChangeStopTarget()` -- PASS
- No `Account.Change()` -- PASS
- No `DateTime.Now` -- PASS
- No `FontFamily` -- PASS
- `Account.Positions` read via existing `hasOpenPosition` delegate -- confirmed AddOnBase-safe. **PASS.**

### SCAN-07: xUnit test coverage plan
New tests required in `tests/` project. All use delegate injection (no NT8 runtime needed).
Both `IsDispatchableState` and `IsNativeExitOnFlatLeader` declared `internal static` for test access.
Confirm `[InternalsVisibleTo("tests")]` exists or add it (match existing pattern for `IsNativeExitName`).

```
[Fact] IsDispatchableState_WhenFilled_ReturnsTrue
  Arrange: state = OrderState.Filled
  Assert: IsDispatchableState(state) == true

[Fact] IsDispatchableState_WhenCancelled_ReturnsTrue
  Arrange: state = OrderState.Cancelled
  Assert: IsDispatchableState(state) == true

[Fact] IsDispatchableState_WhenWorking_ReturnsFalse
  Arrange: state = OrderState.Working
  Assert: IsDispatchableState(state) == false

[Fact] IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlat_ReturnsTrue
  Arrange: orderName="Close", hasOpenPosition delegate returns false
  Assert: result == true

[Fact] IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse
  Arrange: orderName="Close", hasOpenPosition delegate returns true
  Assert: result == false

[Fact] IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse
  Arrange: orderName="PTT-BE-Stop-12345", hasOpenPosition delegate returns false
  Assert: result == false (IsNativeExitName returns false for PTT- prefix)

[Fact] TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers
  Arrange: orderName="Close", state=Filled, isFollower=false, hasOpenPosition(leader)=false
  Assert: flattenOne delegate never called, method returns false
  COVERS: DW-LB-FL-02 root cause

[Fact] TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers
  Arrange: orderName="Close", state=Filled, isFollower=false, hasOpenPosition(leader)=true
           hasOpenPosition(follower)=true, rule has 1 follower
  Assert: flattenOne delegate called once, method returns true
  COVERS: DW-B65-01 regression guard (must PASS)

[Fact] TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers
  Arrange: orderName="Flatten", state=Filled, isFollower=false, hasOpenPosition(leader)=false
  Assert: flattenOne never called, method returns false

[Fact] TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers
  Arrange: orderName="RevToLong", state=Filled, isFollower=false, hasOpenPosition(leader)=false
  Assert: flattenOne never called, method returns false
```

All test methods use delegate injection (no NT8 runtime needed).

---

## RISK ASSESSMENT

| Risk | Severity | Mitigation |
|------|----------|------------|
| DW-B65-01 regression (position lag bypass breaks) | HIGH | Guard 3.5 checks `!hasOpenPosition` -- when leader HAS position, returns false, bypass still fires. Covered by xUnit test. |
| NT8 position lag: leader appears flat but isn't | LOW | Safe direction -- if position shows open, guard 3.5 does not block, dispatch happens (correct behavior). |
| CYC limit exceeded | NONE | Resolved: IsDispatchableState extraction removes &&from guard(1), freeing 1 CYC budget. TryDispatchLeaderFlat pre-fix CYC=8, post-fix CYC=8. At limit. |
| Thread safety of new hasOpenPosition call | NONE | Same delegate, same thread (NT8 dispatch), same contract as existing guard (3). |
| `IsNativeExitOnFlatLeader` called with null account | LOW | Handled: `hasOpenPosition` delegate calls `FindPosition(acc, instr)` which returns null -> `HasOpenPosition` returns false -> `!false=true` -> guard blocks. Correct: null account has no position. |
| "Rev*" native exit on flat leader dispatching incorrectly | NONE | Guard 3.5 blocks Rev* on flat leader same as Close. Correct: if leader is flat, no follower needs closing. |
| IsDispatchableState De Morgan equivalence | NONE | Confirmed: `s==Filled||s==Cancelled` is logical negation-equivalent to original `s!=Filled&&s!=Cancelled`. No semantic change to guard (1). |

---

## NT8 SIM GATE STEPS (SIM Verification Protocol)

**Setup**: NinjaTrader SIM, Clone mode, BE ALL enabled, 1 leader + 1 follower account.

**Step 1**: Enter a long position on leader (buy 1 ES at market).
Verify: both leader and follower show long 1 ES. PTT-BE-Stop-* Working on follower.

**Step 2**: Trigger BE stop fill. In SIM, move price to stop level or use SIM fill.
Verify: leader position = 0 (flat). Follower position = 0 (flat, closed by PTT-BE-Stop-*).
Output: No PTT-Flatten should fire here (follower was already flat via its BE stop).

**Step 3**: Click "Close" button on ChartTrader for the ALREADY-FLAT leader account.
Wait 2-3 seconds.

**Step 4 -- PASS criteria**:
- NT8 Output window: ZERO "PTT-Flatten Accepted" or "PTT-Flatten Working" messages on follower.
- NT8 Output window: ZERO "flat-guard: in-flight skip" repeating messages.
- Follower position: remains 0 (flat). No inversion.
- Order count: stable (no 40-100+ order loop).

**Step 5 -- FAIL criteria** (loop still present):
- Any "PTT-Flatten Accepted" message on follower after the Close click.
- Follower position flips to -1 (short) after Close click.
- Repeating status messages in Output.

**Step 6 -- DW-B65-01 regression test**:
Enter a fresh long position on leader.
Immediately (within the same bar) click Close on ChartTrader while position is live.
Verify: follower receives PTT-Flatten and flattens. If follower flattens = regression test PASS.

---

## SUMMARY: CHANGE SURFACE

| Change | Location | Lines Delta |
|--------|----------|-------------|
| Add `IsDispatchableState` helper | CopyEngine.cs ~4658 | +8 lines |
| Add `IsNativeExitOnFlatLeader` helper | CopyEngine.cs ~4666 | +12 lines |
| Modify guard 1 + insert guard 3.5 + update comment | CopyEngine.cs ~4679 | +4 lines net |
| Add 10 xUnit [Fact] tests | tests/ project | ~100 lines |

Total production code delta: ~24 lines in one file. Zero architectural changes. Zero new dependencies.

---

## REVISION HISTORY

| Rev | Date | Change | Trigger |
|-----|------|--------|---------|
| 1 | 2026-08-22 | Initial plan | Phase 1 |
| 2 | 2026-08-22 | V-01 resolution: extract IsDispatchableState from guard (1); correct CYC baseline to 8 (live source); single authoritative CYC table | REVIEW_FAIL V-01 |

---

## RETURN: PLAN_COMPLETE
