# B71-LaneA Tickets

**Block**: B71-LaneA
**Epic**: Quick ALL Follower Bracket Dispatch + QX Guard
**Phase**: 3 (Ticket Generation)
**Plan source**: docs/brain/B71-LaneA/02-architecture-plan.md (REVIEW_PASS 2026-08-13)
**Reviewer verdict**: docs/brain/B71-LaneA/02-plan-review.md (REVIEW_PASS -- 0 P0, 0 P1)
**Author**: ptt-architect
**Date**: 2026-08-13
**Ticket count**: 1

---

## Ticket T1: B71 Quick ALL Follower Bracket Dispatch + QX Guard

**Block**: B71-LaneA
**Spec Req IDs**: DW-B71-01, DW-B71-02, DW-B71-04
**Files**:
- `src/PropTraderTools/CopyEngine.cs` (Fix 1: Submitted state + Fix 1b: comment + Fix 1c: FindRule visibility)
- `src/PropTraderTools/Features/PttQuickExit.cs` (Fix 2: skipIfFollower parameter + follower guard block)
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (Fix 3: follower dispatch loop + ExecuteOne update)
- `src/PropTraderTools/Tests/B71Tests.cs` (10 new xUnit tests)

**csproj registration** (required so LSP picks up the new test file):
Add `<Compile Include="Tests\B71Tests.cs" />` to the `<ItemGroup>` in `src/PropTraderTools/PropTraderTools.csproj`
(follow the pattern of existing entries: `Tests\B70Tests.cs`, `Tests\B68Tests.cs`, `Tests\B66Tests.cs`).

---

### Method Signatures (exact, copy-pasteable)

**Fix 1c -- CopyEngine.FindRule** (visibility change only, body unchanged):
```csharp
// BEFORE:
private CopyRule? FindRule(Instrument instrument)
// AFTER:
internal CopyRule? FindRule(Instrument instrument)
```

**Fix 2 -- PttQuickExit.Execute** (add optional parameter):
```csharp
// BEFORE:
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
// AFTER:
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
```

**Fix 3d -- PttGlobalQuickExit.ExecuteOne** (add optional parameter, forward to Execute):
```csharp
// BEFORE:
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks);
}
// AFTER:
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks, skipIfFollower);
}
```

---

### Exact Before/After Code for Each Fix

#### FIX 1 -- CopyEngine.cs: Add OrderState.Submitted to stateOk gate (DW-B71-01)

**Lines**: 460-462 (before change)
**Description**: ATM bracket orders placed less than ~800ms before Quick Exit press may be in
`Submitted` state. The current stateOk gate misses them and they are not cancelled.

**BEFORE** (lines 460-462):
```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted;
```

**AFTER** (lines 460-463):
```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted;  // B71: catch ATM brackets placed less than 800ms ago
```

**CYC impact**: None. The `||` operators inside a single bool expression assignment are one
decision point in Roslyn CFG. Adding a 4th `||` branch does not add a new CFG node.

---

#### FIX 1b -- CopyEngine.cs: Update CYC comment at line 452

**Lines**: line 452 (the CYC comment header above the CancelQxBrackets method or first statement)

**BEFORE**:
```
// CYC=6: null guard(1) + foreach(2) + stateOk(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
```

**AFTER**:
```
// CYC=6: null guard(1) + foreach(2) + stateOk(4 branches, Roslyn=1)(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
```

---

#### FIX 1c -- CopyEngine.cs: FindRule private->internal (line 1750)

**Line**: 1750

**BEFORE**:
```csharp
private CopyRule? FindRule(Instrument instrument)
```

**AFTER**:
```csharp
internal CopyRule? FindRule(Instrument instrument)
```

**Body**: UNCHANGED. No other change to CopyEngine.cs for Fix 1c.

**Rationale**: `PttGlobalQuickExit` is in the same assembly (`PropTraderTools`) but a different
class. It cannot access `private` members of `CopyEngine`. `internal` is the minimal visibility
promotion. Existing callers at lines 510, 1731, and 1934 are all inside `CopyEngine` and continue
to compile without change.

---

#### FIX 2a -- PttQuickExit.cs: Update Execute signature (line 33)

**Line**: 33

**BEFORE**:
```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
```

**AFTER**:
```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
```

**Call site compatibility**: All existing 4-argument callers receive `skipIfFollower = true` by
default. No call sites require modification.

---

#### FIX 2b -- PttQuickExit.cs: Update CYC header comment (line 28 area)

Find the existing header comment on `PttQuickExit.Execute` that reads:
```
/// CYC=6: null/flat guard(1) + snapshotStop guard(2) + isLong(3) + T1-null(4) + T2-null(5) + CancelQxBracketsForFollowers?.call(6).
```

Replace with:
```
/// CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3) + isLong(4) + T1-null(5) + T2-null(6) + CancelQxBracketsForFollowers?.call(7).
```

---

#### FIX 2c -- PttQuickExit.cs: Insert follower guard block after line 46

**Location**: After the existing `return;` at line 46 (the flat/pos==null guard block), before
Step 2 (SnapshotStopPrice). Insert the following block verbatim:

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

---

#### FIX 3a -- PttGlobalQuickExit.cs: Remove CancelQxBracketsForFollowers call (line 38)

**Location**: `PttGlobalQuickExit.Execute()`, line 38 (current)

**BEFORE** (lines 37-39):
```csharp
var ticks = ResolveQuickTicks(pos.Instrument);
engine?.CancelQxBracketsForFollowers(pos.Instrument); // B68 DW-B68-01 (5)
ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2); // (6)
```

**AFTER** (lines 37-38):
```csharp
var ticks = ResolveQuickTicks(pos.Instrument);
ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
```

**Rationale**: Follower bracket cancel now occurs inside `ExecuteOne(follower, ...)` via
`PttQuickExit.Execute` Step 3 (`CancelQxBrackets(follower, instr)`). The explicit
`CancelQxBracketsForFollowers` call is redundant after Fix 3b adds the follower dispatch loop.

---

#### FIX 3b -- PttGlobalQuickExit.cs: Add follower dispatch loop after ExecuteOne(acc,...) call

**Location**: Immediately after the `ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);` line
inserted in FIX 3a. Insert the following block verbatim:

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

**CYC impact on Execute()**: Removes 1 branch (the removed `engine?.CancelQxBracketsForFollowers`
null-propagation). Adds 3 branches: `if (rule != null)` (+1), `foreach (var follower in ...)` (+1),
`if (follower == null) continue` (+1). Net: 6 - 1 + 3 = CYC 8. Exactly at JS DNA limit of 8. PASS.

---

#### FIX 3c -- PttGlobalQuickExit.cs: Update Execute() header comment

Find the existing `Execute()` header comment. Replace it entirely with:

```csharp
/// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts in the leader loop.
/// CYC=8: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
///        rule null-check(5), follower foreach(6), follower null continue(7), delegate(8).
/// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in leader loop via IsFollowerAccount.
/// B71 DW-B71-04: follower dispatch loop added -- each follower with a position gets PTT-QX brackets.
/// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
```

---

#### FIX 3d -- PttGlobalQuickExit.cs: Update ExecuteOne signature

**BEFORE** (lines 60-64 approx):
```csharp
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks);
}
```

**AFTER**:
```csharp
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, t2Ticks, skipIfFollower);
}
```

**CYC impact**: ExecuteOne stays at CYC 1. Default value `= true` means the existing
`ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2)` call (leader path, FIX 3a) compiles
without change.

---

#### Complete PttGlobalQuickExit.Execute() After All Changes (reference)

After applying FIX 3a + FIX 3b + FIX 3c, the full `Execute()` body must read:

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

### JS Rule Constraints

Every line of new or modified code in this ticket must satisfy:

| Rule | ID | Requirement | How Enforced |
|------|----|-------------|-------------|
| No lock() | JS-021 (P0) | Zero `lock(` in any modified or new code | SCAN-04 (grep) |
| No throw in hot paths | JS-001 (P0) | Zero `throw new` in execution-path methods | SCAN-05 (grep) |
| No return null | JS-002 (P0) | Nullable returns guarded by callers (`if (rule != null)`) | SCAN-05 (grep) |
| No async void | JS-033 (P0) | No `async void` methods | Inspect signatures |
| CYC <= 8 | Project DNA | All modified methods at or below CYC 8 | SCAN-06 (complexity_audit.py) |
| ASCII only | AGENTS.md §2 | No Unicode in C# string literals or identifiers | SCAN-01 (grep) |

**Pre-existing violations out of scope**: Non-ASCII em-dash at CopyEngine.cs lines 398, 499 and
arrow at ~1449-1450 are tracked as PRE-EXISTING-01/02. Do NOT touch these lines.

---

### xUnit Test Names and Assertions

**Test file**: `src/PropTraderTools/Tests/B71Tests.cs`
**Framework**: xUnit `[Fact]` (mandatory -- no NUnit, no MSTest)
**Total**: 10 tests covering all 3 fixes

Use the same stub/fake pattern established in `CopyEngineB66Tests.cs`. Where direct NT8 type
construction is blocked by internals, use reflection-based property setters or test helper
factories from the existing test file. `CopyEngine.Instance` is set via the singleton setter
pattern used in prior blocks.

---

#### T_B71_01: CancelQxBrackets_SubmittedOrder_IsCancelled

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: When an order has `OrderState == OrderState.Submitted`, passes `IsQxCancelCandidate`
(e.g. name `"PTT-QX-Stop"`), and the instrument matches, the order is included in the `acc.Cancel()`
call.
**Mock setup**:
- Fake `Account` with one order: `OrderState.Submitted`, instrument matches, name = `"PTT-QX-Stop"`.
- Assert `acc.Cancel(...)` is called with an array that contains that order.

---

#### T_B71_02: CancelQxBrackets_WorkingOrder_IsCancelled_Regression

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Working` orders are still included in `acc.Cancel()` (regression guard).
**Mock setup**: Same as T_B71_01 but `OrderState.Working`.

---

#### T_B71_03: CancelQxBrackets_AcceptedOrder_IsCancelled_Regression

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Accepted` orders are still included in `acc.Cancel()` (regression guard).
**Mock setup**: Same as T_B71_01 but `OrderState.Accepted`.

---

#### T_B71_04: CancelQxBrackets_FilledOrder_IsIgnored_Regression

**Method under test**: `CopyEngine.CancelQxBrackets`
**Assertion**: `OrderState.Filled` order is NOT included in the stale list; `acc.Cancel()` is
never called.
**Mock setup**: Fake Account with one order: `OrderState.Filled`, instrument matches, name =
`"PTT-QX-Stop"`. Assert `acc.Cancel()` was NOT called.

---

#### T_B71_05: PttQuickExit_SkipIfFollowerTrue_ReturnEarlyWhenFollower

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When `skipIfFollower = true` (default) and `CopyEngine.IsFollowerAccount(leader)
== true`, the method returns before Step 2. Zero `CreateOrder` calls and zero `CancelQxBrackets`
calls are made.
**Mock setup**:
- CopyEngine configured with the leader account as a follower of another rule.
- Leader has a non-flat position.
- Assert zero `CreateOrder` calls and zero `CancelQxBrackets` calls.

---

#### T_B71_06: PttQuickExit_SkipIfFollowerFalse_FiresOnFollower

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When `skipIfFollower = false`, the follower guard is skipped and execution reaches
Step 3 (`CancelQxBrackets` is called for the follower account).
**Mock setup**: Same as T_B71_05 but call with `skipIfFollower: false`.
Assert `CancelQxBrackets` IS called for the follower account.

---

#### T_B71_07: PttQuickExit_SkipIfFollowerTrue_LogsFollowerGuardMessage

**Method under test**: `PttQuickExit.Execute`
**Assertion**: When the follower guard fires (`skipIfFollower=true` + `IsFollowerAccount=true`),
`NinjaTrader.Code.Output.Process` is called with a message containing
`"PTT-QX: follower guard -- skip Sim102"` (where `"Sim102"` is `leader.Name`).
**Mock setup**:
- `leader.Name = "Sim102"`.
- CopyEngine configured so `IsFollowerAccount("Sim102") == true`.
- Capture `Output.Process` calls via the existing test output-capture mechanism.
- Assert captured message contains `"follower guard -- skip Sim102"`.

---

#### T_B71_08: PttGlobalQuickExit_Execute_CallsExecuteOneForLeader

**Method under test**: `PttGlobalQuickExit.Execute`
**Assertion**: For a leader account with a non-flat position, `ExecuteOne` (via `PttQuickExit.Execute`
or `CancelQxBrackets`) is called with the leader account and `skipIfFollower = true` (default).
**Mock setup**:
- `Account.All` contains one leader account with one non-flat position.
- `CopyEngine.IsFollowerAccount` returns `false` for the leader.
- Assert `PttQuickExit.Execute` (or `CancelQxBrackets`) is called with the leader account.

---

#### T_B71_09: PttGlobalQuickExit_Execute_CallsExecuteOneForEachFollowerWithOpenPosition

**Method under test**: `PttGlobalQuickExit.Execute`
**Assertion**: For a leader with a non-flat position and a `CopyRule` with 2 followers (Sim101,
Sim102), `ExecuteOne` is called for both followers with `skipIfFollower = false`.
**Mock setup**:
- Leader account with non-flat position on instrument `INS`.
- `CopyRule` for `INS` with `FollowerAccounts = [Sim101, Sim102]`.
- Both followers have non-zero positions.
- Assert `PttQuickExit.Execute` called with `Sim101, skipIfFollower: false`.
- Assert `PttQuickExit.Execute` called with `Sim102, skipIfFollower: false`.

---

#### T_B71_10: PttGlobalQuickExit_Execute_DoesNotCallExecuteOneForFollowerWithFlatPosition

**Method under test**: `PttGlobalQuickExit.Execute` + `PttQuickExit.Execute`
**Assertion**: If a follower account has `pos.Quantity == 0` (flat), `PttQuickExit.Execute`
returns immediately at the flat-skip guard. `ExecuteOne` is called (dispatch loop fires for all
followers), but zero `CreateOrder` calls are made for the flat follower.
**Mock setup**:
- Leader with non-flat position. `CopyRule` with follower Sim101 (`Quantity = 0`, flat).
- Assert `PttQuickExit.Execute` was called for Sim101.
- Assert zero `CreateOrder` calls attributed to Sim101.

---

### 7-Scan Checklist (engineer MUST run all 7; report results in ticket-1-completion.md)

The engineer MUST execute every scan below, record each result in
`docs/brain/B71-LaneA/ticket-1-completion.md`, and confirm PASS before marking the ticket done.
Any FAIL result is a blocking defect -- fix before marking complete.

---

#### SCAN-01: ASCII-Only Compliance

**Rule**: AGENTS.md §2 ASCII mandate -- no Unicode, emoji, or curly quotes in C# string literals
or identifiers in modified lines.

**Engineer action**:
```powershell
grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/Tests/B71Tests.cs
```

**Expected result**: Zero matches in lines you modified. Pre-existing non-ASCII at
`CopyEngine.cs` lines 398, 499, ~1449-1450 are out of scope (PRE-EXISTING-01/02) -- if they
appear in grep output, confirm they are on those pre-existing lines only.

**Scope note**: New string `"PTT-QX: follower guard -- skip "` is ASCII. All comment text is
ASCII. If any match appears in newly written code, fix it before proceeding.

---

#### SCAN-02: Build Passes

**Rule**: Zero compilation errors after all 3 fixes are applied.

**Engineer action**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

**Expected result**: Exit code 0, zero errors. Warnings are acceptable.

**Known risk**: If `PttGlobalQuickExit` calls `engine?.FindRule(...)` and `FindRule` is still
`private`, the build will fail with CS0122. Confirm FIX 1c (private->internal) is applied before
building. All other changes use default-parameter patterns that do not break existing call sites.

---

#### SCAN-03: All 10 xUnit Tests Pass

**Rule**: 100% pass rate required. All T_B71_01 through T_B71_10 must be green.

**Engineer action**:
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "T_B71"
```

**Expected result**: 10 passed, 0 failed, 0 skipped.

**Coverage by fix**:
- Fix 1 (DW-B71-01): T_B71_01..T_B71_04
- Fix 2 (DW-B71-02): T_B71_05..T_B71_07
- Fix 3 (DW-B71-04): T_B71_08..T_B71_10

---

#### SCAN-04: No lock() Usage

**Rule**: JS-021 (P0) -- zero `lock(` in new or modified code.

**Engineer action**:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/Features/PttGlobalQuickExit.cs
```

**Expected result**: Zero matches in lines modified or added for B71. If any pre-existing
`lock(` appears in output, confirm it is on a line not in scope and do NOT touch it.

---

#### SCAN-05: No throw new / No return null in Hot Paths

**Rule**: JS-001 (P0) -- no `throw new XxxException()` in execution-path code.
JS-002 (P0) -- nullable returns must be guarded by callers (not hidden behind raw `null`).

**Engineer action**:
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/Features/PttGlobalQuickExit.cs
```

**Expected result**: Zero matches in lines modified or added for B71. New code uses only:
`return;`, `continue;`, `NinjaTrader.Code.Output.Process(...)`, `executor.Execute(...)`.
No new catch blocks or throw statements are introduced.

---

#### SCAN-06: CYC <= 8 on All Modified Methods

**Rule**: Project DNA (AGENTS.md §3.5, CYC <= 8 -- Jane Street strict standard).

**Engineer action**:
```powershell
python scripts/complexity_audit.py src/PropTraderTools/Features/PttGlobalQuickExit.cs
python scripts/complexity_audit.py src/PropTraderTools/Features/PttQuickExit.cs
```

**Expected result**: All methods <= 8.

**Contingency**: If `PttGlobalQuickExit.Execute` reports CYC 9+, extract the follower dispatch
loop into a private helper method `private void DispatchFollowerQx(CopyRule rule, Instrument instr, int t1, int t2)`.
This reduces Execute's CYC by 3 (removes rule-check + foreach + null-continue) and assigns them
to the helper.

**Expected CYC table**:

| Method | File | CYC Before | CYC After | Status |
|--------|------|-----------|-----------|--------|
| `CancelQxBrackets` | CopyEngine.cs | ~6 | ~6 | PASS |
| `PttQuickExit.Execute` | PttQuickExit.cs | 6 | 7 | PASS |
| `PttGlobalQuickExit.Execute` | PttGlobalQuickExit.cs | 6 | 8 | PASS (limit) |
| `ExecuteOne` | PttGlobalQuickExit.cs | 1 | 1 | PASS |
| `FindRule` | CopyEngine.cs | 3 | 3 | PASS (body unchanged) |

---

#### SCAN-07: NT8 API References Verified

**Rule**: All NT8 API usage must be grounded in `docs/standards/NT8_FULL_REFERENCE.md`.
Before claiming any NT8 type or method that is NOT in the table below, grep
`docs/standards/NT8_FULL_REFERENCE.md` first.

**Engineer action**:
```powershell
grep -n "Cancel" docs/standards/NT8_FULL_REFERENCE.md | grep -i "Submitted"
```

**Expected result**: Confirms `OrderState.Submitted` is a documented NT8 enum value and
`Account.Cancel()` places no documented restriction on order state at cancel time.

**Pre-verified NT8 facts (no re-verification needed)**:

| Claim | NT8_FULL_REFERENCE Evidence | Line |
|-------|---------------------------|------|
| `OrderState.Submitted` exists | "OrderState.Submitted -- Order is submitted to the broker" | 936-937 |
| `Account.Cancel()` exists | "Cancel() -- Cancels specified order(s) on the account" | 318-319 |
| `Account.Cancel()` accepts pre-execution orders | No OrderState restriction documented in Cancel() spec | 318-319 |
| `CopyRule.FollowerAccounts` is `Account[]` internal readonly | Verified in source: CopyEngine.cs line 181 | source |
| `FindRule` returns `CopyRule?` | Verified in source: CopyEngine.cs line 1750 | source |
| `IsFollowerAccount` is `internal bool` | Verified in source: CopyEngine.cs line 409 | source |

---

### Completion Artifact

**Engineer writes after full implementation**: `docs/brain/B71-LaneA/ticket-1-completion.md`

Required sections in completion artifact:
1. Implementation summary (list each fix applied with file + line range)
2. SCAN results (one entry per SCAN-01..SCAN-07 with PASS/FAIL and command output excerpt)
3. Test results (output of `dotnet test --filter "T_B71"` -- 10 passed, 0 failed)
4. Build result (exit code 0 confirmation)
5. Any deviations from this ticket (none expected; if any, describe and justify)
6. DW items closed: DW-B71-01, DW-B71-02, DW-B71-04

---

### Pre-Implementation Checklist (engineer reads before writing any code)

Before writing a single line of code, confirm:

- [ ] `git status` shows `main` branch, no uncommitted `.cs` files from prior sessions
- [ ] `dotnet build src/PropTraderTools/PropTraderTools.csproj` passes clean (no pre-existing errors)
- [ ] `src/PropTraderTools/CopyEngine.cs` line 1750 reads `private CopyRule? FindRule(Instrument instrument)` (confirm Fix 1c target exists)
- [ ] `src/PropTraderTools/Features/PttQuickExit.cs` line 33 reads the 4-argument Execute signature (confirm Fix 2a target exists)
- [ ] `src/PropTraderTools/Features/PttGlobalQuickExit.cs` contains `engine?.CancelQxBracketsForFollowers(pos.Instrument)` (confirm Fix 3a removal target exists)
- [ ] Understand that FIX 3a (remove line) and FIX 3b (add follower dispatch) are adjacent edits in the same method -- apply them together in one pass

---

### Post-Implementation Checklist (engineer runs before marking T1 done)

- [ ] SCAN-01: Zero non-ASCII in modified lines
- [ ] SCAN-02: `dotnet build` exits 0
- [ ] SCAN-03: 10/10 T_B71 tests pass
- [ ] SCAN-04: Zero `lock(` in modified code
- [ ] SCAN-05: Zero `throw new` in modified code
- [ ] SCAN-06: All modified methods CYC <= 8
- [ ] SCAN-07: NT8 API grep confirms Submitted state and Cancel()
- [ ] `docs/brain/B71-LaneA/ticket-1-completion.md` written with all sections
- [ ] `git add src/PropTraderTools/ docs/brain/B71-LaneA/` -- stage all changes
- [ ] `git commit -m "feat(ptt): B71 follower QX dispatch + Submitted state fix [10 tests]"`

---

## Block Summary

| Field | Value |
|-------|-------|
| Block | B71-LaneA |
| Ticket count | 1 (T1) |
| Files modified | 3 source + 1 new test file |
| Tests added | 10 ([Fact] T_B71_01..T_B71_10) |
| DW items closed | DW-B71-01, DW-B71-02, DW-B71-04 |
| DW items opened | DW-B71-03 (P2 double-cancel awareness, deferred B72+) |
| JS P0 violations | 0 |
| CYC max after | 8 (PttGlobalQuickExit.Execute -- at limit) |
| NT8 facts cited | 6 (all verified against NT8_FULL_REFERENCE.md) |
| Plan review | REVIEW_PASS (2026-08-13) |
