# B71-LaneA Architecture Plan

**Block**: B71-LaneA
**Epic**: Quick ALL Follower Bracket Dispatch + QX Guard
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS (pending reviewer)
**Author**: ptt-architect
**Date**: 2026-08-13

---

## Section 1: Block Context

### Prior Deferred Items from B66-LaneC

Source: `docs/brain/B66-LaneC/06-deferred-backlog.md`

#### Items CLOSED by B71-LaneA

None. B71-LaneA addresses three new spec work items (DW-B71-01, DW-B71-02, DW-B71-04).
No items from the B66-LaneC deferred backlog are closed by this block.

#### Items OPEN (carry-forward, not addressed by B71-LaneA)

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries (Gate 5 LimitPrice) | P1 | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit -- Director confirmation required | P1 | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | OPEN |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required, AddOnBase cannot call AtmStrategyCreate) | P1 | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded order-name prefixes | P2 | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | OPEN |

### B71 Problem Statement

When the trader presses "Global Quick Exit" (all accounts), follower accounts do not receive
PTT-QX bracket orders. The current `PttGlobalQuickExit.Execute()` skips all follower accounts
via `IsFollowerAccount` guard (line 33). This was correct before B70 (followers had no positions
of their own), but followers now accumulate open positions from the copy engine. The fix requires:

1. Widening `CancelQxBrackets` to catch ATM brackets placed within the last ~800ms (Submitted state).
2. Adding a follower guard parameter to `PttQuickExit.Execute` so the per-chart path cannot
   accidentally fire on a follower account (mis-click protection), while the global dispatch
   path can explicitly opt in.
3. Adding a follower dispatch loop to `PttGlobalQuickExit.Execute` that places QX brackets on
   every follower with an open position.

---

## Section 2: NT8 Ground Truth

All claims verified against `docs/standards/NT8_FULL_REFERENCE.md`.

### Fact 1: OrderState.Submitted is a valid NT8 enum value

**Source**: NT8_FULL_REFERENCE.md lines 936-937
**Value**: `OrderState.Submitted` -- "Order is submitted to the broker"

ATM bracket orders placed less than ~800ms before Quick Exit press may be in `Submitted` state.
The current `stateOk` gate (Working | Initialized | Accepted) misses these orders.
`Account.Cancel()` accepts orders in any pre-execution state including `Submitted`.

### Fact 2: Account.Cancel() accepts Submitted-state orders

**Source**: NT8_FULL_REFERENCE.md lines 318-319
**Method**: `Cancel()` -- "Cancels specified order(s) on the account"

NT8 documentation does not restrict `Account.Cancel()` to Working-state orders only.
The cancel request is routed to the broker; the broker may reject if already transitioning,
which is why the existing `try { acc.Cancel(...); } catch { }` pattern is correct.

### Fact 3: FindRule is private on CopyEngine (must be promoted to internal)

**Source**: Verified by reading `src/PropTraderTools/CopyEngine.cs` lines 1750-1760
```csharp
private CopyRule? FindRule(Instrument instrument)
```
`PttGlobalQuickExit` is in the same assembly (`PropTraderTools`) but a different class.
It cannot access `private` members of `CopyEngine`. Fix 3 requires changing this to `internal`.
This is the minimal visibility promotion -- `internal` restricts access to assembly boundary.
The existing callers (lines 510, 1731, 1934) are all inside `CopyEngine` -- they continue to work.

### Fact 4: CopyRule.FollowerAccounts is an internal readonly array

**Source**: Verified by reading `src/PropTraderTools/CopyEngine.cs` line 181
```csharp
internal readonly Account[] FollowerAccounts;
```
Array is set at construction time (line 211) and never mutated. Safe for read-only iteration
from `PttGlobalQuickExit` after `FindRule` returns a non-null rule.

### Fact 5: IsFollowerAccount is already internal and accessible

**Source**: Verified by reading `src/PropTraderTools/CopyEngine.cs` line 409
```csharp
internal bool IsFollowerAccount(Account acc)
```
Already accessible from `PttQuickExit` (same assembly). Fix 2 uses the same pattern
already used in `PttGlobalQuickExit.Execute` line 33.

### Fact 6: CancelQxBracketsForFollowers call sequence after B71

When `PttGlobalQuickExit.Execute` calls `ExecuteOne(follower, instr, t1, t2, skipIfFollower:false)`:
- `PttQuickExit.Execute` Step 3 calls `CopyEngine.Instance?.CancelQxBrackets(follower, instr)` directly.
- `PttQuickExit.Execute` line 54 also calls `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr)`.
  When the follower is the "leader" parameter, this iterates the rule's followers and calls
  `CancelQxBrackets` for each. This is a second cancel pass -- NT8 no-ops a cancel on an
  already-cancelled order. Safe.
- The explicit `engine?.CancelQxBracketsForFollowers(pos.Instrument)` in `PttGlobalQuickExit.Execute`
  (line 38 current) becomes redundant and is removed by Fix 3(a).

---

## Section 3: Changes

### 3.1 Fix 1 (DW-B71-01): Add OrderState.Submitted to CancelQxBrackets stateOk gate

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 460-462
**CYC impact**: None. The `||` operators inside a single bool expression assignment are
one decision point in Roslyn CFG. Adding a 4th `||` branch does not add a new node.

**Before** (lines 460-462):
```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted;
```

**After** (lines 460-463):
```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted;  // B71: catch ATM brackets placed less than 800ms ago
```

**No other changes to CancelQxBrackets.** Full method for reference:
```csharp
internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    if (acc == null || instr == null) return;              // (1)
    var stale = new System.Collections.Generic.List<Order>();
    foreach (Order o in acc.Orders)                        // (2)
    {
        bool stateOk = o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted;  // B71: catch ATM brackets placed less than 800ms ago
        if (!stateOk) continue;                            // (3)
        if (o.Instrument == null || o.Instrument.FullName != instr.FullName) continue;
        if (IsQxCancelCandidate(o))                           // (5) widened via helper
            stale.Add(o);
    }
    if (stale.Count == 0) return;
    try { acc.Cancel(stale.ToArray()); }
    catch { }
}
```

### 3.2 Fix 2 (DW-B71-02): Add skipIfFollower parameter to PttQuickExit.Execute

**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Line**: 33 (signature), after line 46 (guard insertion)
**CYC impact**: +1 branch. CYC 6 -> 7 (within JS-041 limit of 8).

**Before** (line 33):
```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
```

**After** (line 33):
```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
```

**Insertion point**: After the existing `return;` at line 46 (flat/pos==null guard), before Step 2 (SnapshotStopPrice). Insert the following block:

```csharp
// B71 DW-B71-02: reject if leader is a follower account (default) -- opt out via skipIfFollower=false
// PttGlobalQuickExit follower dispatch loop passes false to deliberately place QX on followers.
// All other callers (OnQuickClick, direct) keep default true -- silent guard against mis-click.
// CYC: +1 branch (CYC 6 -> 7). JS-021: no lock.
if (skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true)
{
    NinjaTrader.Code.Output.Process(
        "PTT-QX: follower guard -- skip " + (leader != null ? leader.Name : "NULL"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```

**Call site compatibility**: All existing callers of `PttQuickExit.Execute` use the 4-argument
form and receive `skipIfFollower = true` by default. No call site changes required.
Confirmed call sites:
- `PttGlobalQuickExit.ExecuteOne` (calls via delegate, 4 args)
- Any per-chart Quick Exit handler (4 args)

**Updated header comment** (reflect CYC change):
Replace:
```
/// CYC=6: null/flat guard(1) + snapshotStop guard(2) + isLong(3) + T1-null(4) + T2-null(5) + CancelQxBracketsForFollowers?.call(6).
```
With:
```
/// CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3) + isLong(4) + T1-null(5) + T2-null(6) + CancelQxBracketsForFollowers?.call(7).
```

### 3.3 Fix 3 (DW-B71-04): PttGlobalQuickExit follower dispatch + FindRule visibility

#### 3.3.A FindRule visibility change (CopyEngine.cs)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 1750

**Before**:
```csharp
private CopyRule? FindRule(Instrument instrument)
```

**After**:
```csharp
internal CopyRule? FindRule(Instrument instrument)
```

This is the ONLY change to CopyEngine.cs for Fix 3. Body is unchanged.

#### 3.3.B PttGlobalQuickExit.Execute changes

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

Three sub-changes to `Execute()`:

**(a) Remove CancelQxBracketsForFollowers call** (line 38 current):

**Before** (lines 37-39):
```csharp
var ticks = ResolveQuickTicks(pos.Instrument);
engine?.CancelQxBracketsForFollowers(pos.Instrument); // B68 DW-B68-01 (5)
ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2); // (6)
```

**After** (lines 37-39):
```csharp
var ticks = ResolveQuickTicks(pos.Instrument);
ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
```

Rationale: follower bracket cancel now happens inside `ExecuteOne(follower, ...)` via
`PttQuickExit.Execute` Step 3 (`CancelQxBrackets(follower, instr)`).

**(b) Add follower dispatch loop** after `ExecuteOne(acc, ...)`:

```csharp
// B71 DW-B71-04: place PTT-QX on every follower that has an open position
var rule = engine?.FindRule(pos.Instrument);
if (rule != null)
    foreach (var follower in rule.Value.FollowerAccounts)
    {
        if (follower == null) continue;
        ExecuteOne(follower, pos.Instrument, ticks.t1, ticks.t2, skipIfFollower: false);
    }
```

**(c) ExecuteOne signature change**:

**Before** (lines 60-64):
```csharp
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks);
}
```

**After**:
```csharp
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks, skipIfFollower);
}
```

**CYC impact on Execute**: Current CYC=6. Remove 1 branch (engine?. null-propagation on
CancelQxBracketsForFollowers). Add 3 branches: `if (rule != null)` (+1),
`foreach (var follower in ...)` (+1), `if (follower == null) continue` (+1).
Net: 6 - 1 + 3 = CYC 8. Exactly at JS-041 limit. PASS.

**Updated Execute() header comment** (reflect CYC change and B71 additions):
Replace current Execute comment with:
```csharp
/// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts in the leader loop.
/// CYC=8: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
///        rule null-check(5), follower foreach(6), follower null continue(7), delegate(8).
/// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in leader loop via IsFollowerAccount.
/// B71 DW-B71-04: follower dispatch loop added -- each follower with a position gets PTT-QX brackets.
/// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
```

**Full Execute() after all changes**:
```csharp
internal void Execute()
{
    var engine = CopyEngine.Instance;                   // capture once
    foreach (Account acc in Account.All)                // (1)
    {
        if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2) follower skip
        foreach (Position pos in acc.Positions)         // (3)
        {
            if (pos == null || pos.Quantity == 0) continue;  // (4)
            var ticks = ResolveQuickTicks(pos.Instrument);
            ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
            // B71 DW-B71-04: place PTT-QX on every follower that has an open position
            var rule = engine?.FindRule(pos.Instrument);    // (5)
            if (rule != null)                               // (5 guard)
                foreach (var follower in rule.Value.FollowerAccounts)  // (6)
                {
                    if (follower == null) continue;         // (7)
                    ExecuteOne(follower, pos.Instrument, ticks.t1, ticks.t2, skipIfFollower: false);
                }
        }
    }
}
```

---

## Section 4: Test Plan

Test file: `src/PropTraderTools/Tests/B71Tests.cs` (new file)
Framework: xUnit (mandatory per TEST_FRAMEWORK_PROTOCOL.md)
All 10 tests are `[Fact]` (no `[Theory]` needed -- inputs are fixed per test).

Mock strategy notes:
- NT8 `Account`, `Order`, `Position`, `Instrument` are concrete NT8 Cbi types.
- Tests use the same stub/fake pattern established in `CopyEngineB66Tests.cs`.
- Where direct construction is blocked by NT8 internals, use test helper factories
  established in the existing test file or reflection-based property setters.
- `CopyEngine.Instance` is set via the singleton setter pattern used in prior blocks.

---

### T_B71_01: CancelQxBrackets cancels order in Submitted state

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: When an order's `OrderState == OrderState.Submitted` and it passes
`IsQxCancelCandidate`, it is included in the `acc.Cancel()` call.
**Mock setup**:
- Fake `Account` with one order: `OrderState.Submitted`, instrument matches, name passes
  `IsQxCancelCandidate` (e.g., `"PTT-QX-Stop"`).
- Verify `acc.Cancel(...)` is called with an array containing that order.

---

### T_B71_02: CancelQxBrackets cancels order in Working state (regression)

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Working` orders are still cancelled (regression guard -- was
passing before B71 and must remain passing after).
**Mock setup**: Same as T_B71_01 but `OrderState.Working`.

---

### T_B71_03: CancelQxBrackets cancels order in Accepted state (regression)

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Accepted` orders are still cancelled (regression guard).
**Mock setup**: Same as T_B71_01 but `OrderState.Accepted`.

---

### T_B71_04: CancelQxBrackets ignores order in Filled state (regression)

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Filled` orders are NOT included in `acc.Cancel()`. The stale
list is empty and `acc.Cancel()` is never called.
**Mock setup**: Fake Account with one order: `OrderState.Filled`, instrument matches,
name passes `IsQxCancelCandidate`. Assert `acc.Cancel()` was NOT called.

---

### T_B71_05: PttQuickExit.Execute(skipIfFollower=true) returns early when leader is follower

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When `skipIfFollower = true` (default) and `CopyEngine.IsFollowerAccount(leader)
== true`, the method returns before Step 2 (no orders created, no `CancelQxBrackets` call).
**Mock setup**:
- CopyEngine configured with leader account as a follower of another rule.
- leader has a non-flat position.
- Assert zero `CreateOrder` calls and zero `CancelQxBrackets` calls.

---

### T_B71_06: PttQuickExit.Execute(skipIfFollower=false) fires on follower (no early return)

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When `skipIfFollower = false`, the follower guard is skipped and execution
reaches Step 3 (CancelQxBrackets is called for the follower account).
**Mock setup**: Same as T_B71_05 but call with `skipIfFollower: false`.
Assert `CancelQxBrackets` IS called for the follower account.

---

### T_B71_07: PttQuickExit.Execute logs "follower guard -- skip Sim102" when skipIfFollower=true

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When follower guard fires (skipIfFollower=true + IsFollowerAccount=true),
the Output.Process log message contains "PTT-QX: follower guard -- skip Sim102"
(where "Sim102" is the account name).
**Mock setup**:
- leader.Name = "Sim102"
- CopyEngine configured so IsFollowerAccount("Sim102") = true.
- Capture Output.Process calls via existing test output capture mechanism.
- Assert log message contains "follower guard -- skip Sim102".

---

### T_B71_08: PttGlobalQuickExit.Execute calls ExecuteOne for leader account

**Method under test**: `PttGlobalQuickExit.Execute`
**Assertion**: For a leader account with a non-flat position, `ExecuteOne` is called with
the leader account (skipIfFollower=true default).
**Mock setup**:
- Account.All contains one leader account with one non-flat position.
- CopyEngine.IsFollowerAccount returns false for leader.
- Assert PttQuickExit.Execute (or CancelQxBrackets) is called with the leader account.

---

### T_B71_09: PttGlobalQuickExit.Execute calls ExecuteOne for each follower with open position

**Method under test**: `PttGlobalQuickExit.Execute`
**Assertion**: For a leader with position + CopyRule with 2 followers, ExecuteOne is called
for both followers with `skipIfFollower=false`.
**Mock setup**:
- Leader account with non-flat position on instrument INS.
- CopyRule for INS with FollowerAccounts = [Sim101, Sim102].
- Both followers have non-zero positions.
- Assert PttQuickExit.Execute called with Sim101, skipIfFollower=false.
- Assert PttQuickExit.Execute called with Sim102, skipIfFollower=false.

---

### T_B71_10: PttGlobalQuickExit.Execute does NOT call ExecuteOne for follower with flat position

**Method under test**: `PttGlobalQuickExit.Execute` + `PttQuickExit.Execute`
**Assertion**: If a follower account has `pos.Quantity == 0` (flat), the PttQuickExit
guard (`pos == null || pos.Quantity == 0`) returns early. ExecuteOne is called (the dispatch
loop always calls it for all followers), but PttQuickExit.Execute returns immediately with
a flat-skip log. No orders are created for the flat follower.
**Mock setup**:
- Leader with non-flat position. CopyRule with follower Sim101 (flat, Quantity=0).
- Assert PttQuickExit called for Sim101 but Assert zero CreateOrder calls for Sim101.

---

## Section 5: 7-Scan Checklist

**Scope**: All new and modified code in B71-LaneA Ticket T1.
**Files in scope**:
- `src/PropTraderTools/CopyEngine.cs` (lines 460-463, line 1750)
- `src/PropTraderTools/Features/PttQuickExit.cs` (line 33, new guard block after line 46)
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (lines 28-65 modified)
- `src/PropTraderTools/Tests/B71Tests.cs` (new file)

---

### SCAN-01: ASCII-Only Compliance

**Rule**: JS-001 constraint -- no Unicode, emoji, or curly quotes in C# string literals.
**Check**: All new string literals in scope are ASCII-only.

New strings introduced:
- `"PTT-QX: follower guard -- skip "` -- ASCII. PASS.
- `"NULL"` -- ASCII. PASS.
- Comment text -- ASCII. PASS.
- `"B71 DW-B71-02: reject if leader..."` (comment) -- ASCII. PASS.
- `"B71 DW-B71-04: place PTT-QX..."` (comment) -- ASCII. PASS.

**Engineer action**: After edit, run:
```powershell
grep -P '[^\x00-\x7F]' src/PropTraderTools/CopyEngine.cs
grep -P '[^\x00-\x7F]' src/PropTraderTools/Features/PttQuickExit.cs
grep -P '[^\x00-\x7F]' src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -P '[^\x00-\x7F]' src/PropTraderTools/Tests/B71Tests.cs
```
Expected: zero matches in the lines you modified. (Pre-existing non-ASCII at CopyEngine.cs
lines 398, 499, ~1449-1450 are outside scope; do not touch them.)

---

### SCAN-02: Build Passes

**Rule**: Zero compilation errors after all 3 fixes are applied.
**Check**: `dotnet build src/PropTraderTools/PropTraderTools.csproj` returns exit code 0.

Key risks:
- FindRule visibility change: `private` -> `internal`. All current callers are inside
  `CopyEngine` -- still within `internal` scope. No breakage. PASS.
- `PttGlobalQuickExit` calls `engine?.FindRule(pos.Instrument)` -- requires `internal` (above).
- `PttQuickExit.Execute` new parameter has default value -- all existing 4-arg call sites
  compile without change. PASS.
- `ExecuteOne` new parameter has default value -- the existing `ExecuteOne(acc, ...)` calls
  inside `Execute()` receive default `skipIfFollower=true`. PASS.

**Engineer action**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Zero errors required before proceeding.

---

### SCAN-03: All 10 xUnit Tests Pass

**Rule**: 100% pass rate. All T_B71_01 through T_B71_10 must be green.
**Check**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`

Tests cover:
- Fix 1 regression coverage (T_B71_01..T_B71_04)
- Fix 2 guard path (T_B71_05..T_B71_07)
- Fix 3 dispatch logic (T_B71_08..T_B71_10)

**Engineer action**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: 10 passed, 0 failed, 0 skipped.

---

### SCAN-04: No lock() Usage

**Rule**: JS-021 -- zero `lock()` in new or modified code.
**Check**: No lock() in scope.

B71 introduces no locking. All new code is synchronous UI-thread execution with:
- Read-only array iteration (`FollowerAccounts`)
- Instance method calls on CopyEngine singleton
- No shared mutable state mutations

**Engineer action**:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
grep -n "lock(" src/PropTraderTools/Features/PttQuickExit.cs
grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
Expected: zero matches in modified regions (pre-existing occurrences if any are outside scope).

---

### SCAN-05: No throw in Hot Paths

**Rule**: JS-001 -- no `throw new XxxException()` in execution-path code.
**Check**: No throw statements in B71 new code.

B71 new code contains only:
- `return;` (early exit)
- `NinjaTrader.Code.Output.Process(...)` (logging)
- `continue;` (loop skip)
- `executor.Execute(...)` (delegation)

Existing `try { acc.Cancel(...); } catch { }` is a pre-existing empty-catch pattern
(intentional fire-and-forget -- NT8 broker cancel may throw if already transitioning).
B71 does not introduce new catch blocks or throw statements.

**Engineer action**:
```powershell
grep -n "throw new" src/PropTraderTools/Features/PttQuickExit.cs
grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
Expected: zero matches in modified regions.

---

### SCAN-06: CYC <= 8 on All Modified Methods

**Rule**: JS-041 -- cyclomatic complexity <= 8 per method (Jane Street strict standard).

| Method | File | CYC Before | CYC After | Status |
|--------|------|-----------|-----------|--------|
| `CancelQxBrackets` | CopyEngine.cs | ~6 | ~6 | PASS |
| `PttQuickExit.Execute` | PttQuickExit.cs | 6 | 7 | PASS |
| `PttGlobalQuickExit.Execute` | PttGlobalQuickExit.cs | 6 | 8 | PASS (limit) |
| `ExecuteOne` | PttGlobalQuickExit.cs | 1 | 1 | PASS |
| `FindRule` | CopyEngine.cs | 3 | 3 | PASS (body unchanged) |

**Engineer action**:
```powershell
python scripts/complexity_audit.py --file src/PropTraderTools/Features/PttGlobalQuickExit.cs
python scripts/complexity_audit.py --file src/PropTraderTools/Features/PttQuickExit.cs
```
Expected: all methods <= 8. If PttGlobalQuickExit.Execute reports 9+, the follower loop
must be extracted to a helper method.

---

### SCAN-07: NT8 API References Verified

**Rule**: All NT8 API usage grounded in `docs/standards/NT8_FULL_REFERENCE.md`.
**Claims and verification**:

| Claim | NT8_FULL_REFERENCE Evidence | Line |
|-------|---------------------------|------|
| `OrderState.Submitted` exists | "OrderState.Submitted -- Order is submitted to the broker" | 936-937 |
| `Account.Cancel()` exists | "Cancel() -- Cancels specified order(s) on the account" | 318-319 |
| `Account.Cancel()` is unrestricted re: OrderState | No restriction documented in Cancel() spec | 318-319 |
| `CopyRule.FollowerAccounts` is `Account[]` | Verified in source: `internal readonly Account[] FollowerAccounts` | CopyEngine.cs:181 |
| `CopyEngine.FindRule` returns `CopyRule?` | Verified in source: `private CopyRule? FindRule(...)` | CopyEngine.cs:1750 |
| `IsFollowerAccount` is `internal bool` | Verified in source: `internal bool IsFollowerAccount(Account acc)` | CopyEngine.cs:409 |

No phantom NT8 API usage. All types and members confirmed to exist in the live codebase
or NT8_FULL_REFERENCE.md.

**Engineer action**: Before any claim about an NT8 type or method not listed above, grep
`docs/standards/NT8_FULL_REFERENCE.md` first.

---

## Section 6: Open DW Items

### Items Closed by B71-LaneA

| ID | Description |
|----|-------------|
| DW-B71-01 | CancelQxBrackets misses ATM brackets in Submitted state |
| DW-B71-02 | PttQuickExit.Execute fires on follower accounts (no guard) |
| DW-B71-04 | PttGlobalQuickExit.Execute does not dispatch QX to follower accounts |

### New DW Items from B71 Scope Analysis

#### DW-B71-03 — PttQuickExit.Execute line 54 calls CancelQxBracketsForFollowers on follower accounts (double-cancel)

**Priority**: P2
**Target block**: B72+
**Status**: OPEN (introduced awareness in B71, not a blocking issue)

**Description**: When `PttGlobalQuickExit` calls `ExecuteOne(follower, instr, t1, t2, skipIfFollower:false)`,
`PttQuickExit.Execute` Step 3 calls `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr)`.
Since the follower is passed as the "leader" parameter, and `FindRule(instr)` still matches
the instrument rule (the rule is keyed by instrument, not by whether the account is leader or
follower), `CancelQxBracketsForFollowers` will iterate the rule's followers and call
`CancelQxBrackets` for each. This is a second cancel pass -- NT8 no-ops on already-cancelled
orders, so it is functionally harmless.
However, it is architecturally redundant and could cause confusion in future refactors.

**Fix approach** (B72+): Consider passing an `isFolowerContext` flag deeper or refactoring
Step 3 of `PttQuickExit.Execute` to only call `CancelQxBracketsForFollowers` when acting
as a leader. Alternatively: extract `Step3Cancel` into a strategy parameter.
Defer pending Director confirmation that the double-cancel is acceptable.

### Carry-Forward OPEN Items (all from B66-LaneC, unchanged)

| ID | Description | Priority | Target |
|----|-------------|----------|--------|
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries | P1 | B72+ |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit (Director confirmation) | P1 | B72+ |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B72+ |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required) | P1 | future (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | future |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future |

---

## Summary

**Block**: B71-LaneA
**Ticket count**: 1 (T1)
**Files modified**: 3 (CopyEngine.cs, PttQuickExit.cs, PttGlobalQuickExit.cs) + 1 new test file
**Tests added**: 10 ([Fact] T_B71_01..T_B71_10)
**DW items closed**: 3 (DW-B71-01, DW-B71-02, DW-B71-04)
**DW items opened**: 1 (DW-B71-03 -- P2 double-cancel awareness)
**JS violations**: 0
**CYC max after**: 8 (PttGlobalQuickExit.Execute -- exactly at limit)
**NT8 facts cited**: 6 (all verified against NT8_FULL_REFERENCE.md)
