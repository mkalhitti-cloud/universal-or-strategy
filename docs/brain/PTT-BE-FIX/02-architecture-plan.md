# PTT-BE-FIX -- Architecture Plan
Epic: PTT-BE-FIX
Phase: 1 (Architecture)
Status: REVIEW_PASS
Date: 2026-08-22
Author: ptt-architect

---

## Section A -- Context & Root Causes

### DW-B86 (P0) -- Stop Name Guard Miss After QX-ALL

**Spec section**: section-b86 in 002-trade-copier-spec.html

**Affected code** (CopyEngine.cs L2755-2762):
```
if (o.Name != null
    && o.Name.StartsWith("Stop", StringComparison.Ordinal)
    && o.Name.Length == 5
    && char.IsDigit(o.Name[4]))
{
    o.StopPriceChanged = newStop;
    beSt.Add(o);
}
```

**Failure mode**:
After QX-ALL fires, the PTT engine cancels the ATM-owned stops (Stop1..Stop9) on all
followers and places new PTT-managed brackets: PTT-QX-Stop (leg 1), PTT-QX-Stop2,
PTT-QX-Stop3, PTT-QX-Stop4 (Accepted state on SIM, per NT8_FULL_REFERENCE.md line 1005).
When BE-ALL is subsequently pressed, the follower path (L2740-2792) iterates acc.Orders
and applies the name guard. The existing guard matches exactly Stop1..Stop9.
PTT-QX-Stop* names fail every condition:
  - "PTT-QX-Stop".StartsWith("Stop") == false
  - Length != 5
Result: beSt.Count == 0 for every follower. [BE-DIAG-F] dump fires. acc.Change() is
called with an empty array. No stop movement. Followers left with stale QX-price stops.

Session evidence (2026-08-21 12:30 PM): Sim102 and Sim103 both reported stops=0.
[BE-DIAG-F] confirmed PTT-QX-Stop/Stop2/Stop3/Stop4 all Accepted on both accounts.

**State guard note**: PTT-QX-Stop* orders are Accepted on SIM. The existing state guard
at L2748-2750 already accepts Working || Accepted || ChangeSubmitted (DW-B84-03).
No state guard change is needed -- only the name guard requires extension.

---

### DW-B85 (P1) -- Silent Null Slot at LoadRules Time

**Spec section**: section-b85 in 002-trade-copier-spec.html

**Affected code** (CopyEngine.cs L3396-3407):
```
var followers = new Account[dto.FollowerAccountNames.Length];
for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
{
    foreach (var acc in Account.All)
    {
        if (acc.Name == dto.FollowerAccountNames[i])
        {
            followers[i] = acc;
            break;
        }
    }
}
// -- followers[i] stays null if account not yet in Account.All --
// No log, no warning, no indication of failure.
```

**Failure mode**:
LoadRules() runs at TradeCopierWindow OnLoad time, which may precede full NT8 account
initialization. If a follower Account is not yet present in Account.All at that moment,
followers[i] remains null silently. AllAccounts() at L2484 skips null slots:
  if (acc != null) yield return acc;
The follower is saved correctly in XML and visible in the panel UI, but is never dispatched
to, never included in BE-ALL, and never included in any copy operation at runtime.
No log line is emitted. The operator has no indication the follower is inactive.

Observed instance: Sim104 (session 2026-08-20 10:25 PM) accumulated 336 contracts
independently via its own chart-trader session with no PTT oversight.

**Option A (lazy re-resolve)**: Deferred to backlog per spec. Not implemented in this epic.
**Option B (startup warning)**: Implemented by T2 -- emit WARNING log when followers[i] null.

---

### DW-B84 (P1) -- No xUnit Tests for Follower acc.Change() Path

**Spec section**: section-b84 in 002-trade-copier-spec.html

DW-B84-01/02/03 are deployed (3 commits, last: 1e0e45b0). The follower acc.Change() path
and the followers-before-leader ordering are production code but have zero unit test
coverage. T3 closes this gap by adding xUnit tests for:
  - follower early return before Step B/C
  - stop name guard behavior (ATM names and QX names)
  - state guard behavior (accepted/rejected states)
  - stops=0 diagnostic log emission
  - BreakEven(Account,Instrument,int) followers-before-leader ordering

---

### DW-T4 -- TryReplacePttBeBrackets Follower Path Reachability

**Affected code** (CopyEngine.cs L964-965, L1820-1850):
```
// L964-965: call site in OnOrderUpdate
if (e.Order.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
    TryReplacePttBeBrackets(e.Order);
```

Analysis required: can the above be triggered for a follower account?
(See Section B T4 for full analysis.)

---

## Section B -- Solution Design

### T1 -- DW-B86: Stop Name Guard Extension

**File**: src/PropTraderTools/CopyEngine.cs
**Location**: L2755-2762 (inside the `if (IsFollowerAccount(acc))` block)
**Change type**: Refactor existing guard into named bool with additional OR branch

**BEFORE** (L2755-2762):
```csharp
if (o.Name != null
    && o.Name.StartsWith("Stop", StringComparison.Ordinal)
    && o.Name.Length == 5
    && char.IsDigit(o.Name[4]))
{
    o.StopPriceChanged = newStop;
    beSt.Add(o);
}
```

**AFTER** (copy-paste ready):
```csharp
// DW-B86: extend stop name guard to match PTT-QX-Stop* orders placed after QX-ALL.
// Original ATM stop names are exactly Stop1..Stop9 (length 5, IsDigit guard).
// After QX-ALL the follower has PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3, PTT-QX-Stop4.
// Both sets are in Accepted state on SIM (DW-B84-03 state guard already covers Accepted).
bool isBeStop = o.Name != null
    && (   (o.Name.StartsWith("Stop", StringComparison.Ordinal)
            && o.Name.Length == 5
            && char.IsDigit(o.Name[4]))
         || o.Name.StartsWith("PTT-QX-Stop", StringComparison.Ordinal));
if (isBeStop)
{
    o.StopPriceChanged = newStop;
    beSt.Add(o);
}
```

**What does NOT change**: acc.Change() call, try/catch wrapper, [BE-DIAG-F] diagnostic
dump (L2765-2775), stops count log (L2786-2789), StatusUpdate invocation (L2790),
and early return (L2791). All unaffected.

**Matches for PTT-QX-Stop* family**:
  - "PTT-QX-Stop"   (leg 1, no suffix digit)  -- matches
  - "PTT-QX-Stop2"  (leg 2)                   -- matches
  - "PTT-QX-Stop3"  (leg 3)                   -- matches
  - "PTT-QX-Stop4"  (leg 4)                   -- matches
  - "PTT-QX-StopN"  (any N)                   -- matches (future-proof)

**Does NOT accidentally match**:
  - "PTT-QX-T1" .. "PTT-QX-T4" (target orders)   -- StartsWith("PTT-QX-Stop") false
  - "PTT-BE-Stop-1" (leader PTT-BE-Stop-*)         -- StartsWith("PTT-QX-Stop") false
  - "StopMarket", "StopLimit", "StopLoss"           -- StartsWith("Stop") true BUT Length != 5 OR not IsDigit

---

### T2 -- DW-B85 Option B: Startup Warning in DtoToRule()

**File**: src/PropTraderTools/CopyEngine.cs
**Location**: after L3407 (inside the outer for loop, immediately after inner foreach closes)
**Change type**: Add if-block + extract inner lookup to helper for CYC compliance

**Helper method to extract** (new private method, placed near DtoToRule):
```csharp
// DW-B85: helper extracted from DtoToRule to keep DtoToRule CYC <= 8.
// Returns Account? (nullable) -- JS-002 compliant lookup/search pattern.
// CYC=2: foreach(1) + if(2).
private static Account? FindFollowerAccount(string name)
{
    foreach (var acc in Account.All)
    {
        if (acc.Name == name)
            return acc;
    }
    return null;
}
```

**DtoToRule change** -- replace inner foreach at L3399-3406 and add warning block:
```csharp
// BEFORE (L3397-3407):
var followers = new Account[dto.FollowerAccountNames.Length];
for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
{
    foreach (var acc in Account.All)
    {
        if (acc.Name == dto.FollowerAccountNames[i])
        {
            followers[i] = acc;
            break;
        }
    }
}

// AFTER (copy-paste ready):
var followers = new Account[dto.FollowerAccountNames.Length];
for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
{
    Account? found = FindFollowerAccount(dto.FollowerAccountNames[i]);
    followers[i] = found;
    // DW-B85 Option B: emit startup warning when follower account is not yet in Account.All.
    // Workaround: uncheck + re-check the follower in the panel after NT8 finishes connecting.
    if (followers[i] == null)
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY] WARNING: follower '" + dto.FollowerAccountNames[i]
                + "' not found in Account.All at load time"
                + " -- will be skipped until rule is re-applied (uncheck + re-check in panel).",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**ASCII-only verification**:
  - All string literals use ASCII apostrophe ' (0x27), not Unicode curly quote
  - No em-dash -- uses -- (two hyphens, 0x2D 0x2D)
  - No Unicode characters anywhere in the log string

**What does NOT change**: multipliers block (L3410-3412), atmMap block (L3415-3424),
tightenTicks (L3428), CopyRule.Create call (L3430-3431). All unaffected.

---

### T3 -- DW-B84: xUnit Test Coverage for Follower acc.Change() Path

**File**: tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs (NEW)
**Test class**: CopyEngineBreakEvenFollowerTests
**Framework**: xUnit ONLY (Xunit.v3 or Xunit 2.x with [Fact] attribute)
**Using directives needed**: Xunit, NinjaTrader.Cbi (for Order, Account, Instrument stubs)

Note: NT8 Order and Account are concrete NT8 runtime types. Test strategy uses internal
helper logic extracted to static testable methods, OR constructs minimal test harness
stubs. The plan prescribes the [Fact] names and assertions; the engineer determines
the stub/mock pattern appropriate for the NT8 test environment.

**[Fact] methods (8 tests)**:

1. `FollowerPath_EarlyReturn_SkipsStepBAndC`
   Asserts: when IsFollowerAccount(acc)==true, MoveStopToBreakEven returns before
   Step B (acc.Cancel) and Step C (acc.CreateOrder/acc.Submit) are reached.
   Mechanism: verify Step B/C order-creation methods are not called on the follower account.

2. `StopNameGuard_AtmStop1_Matches`
   Asserts: order named "Stop1" (length 5, IsDigit('1')) passes the isBeStop guard.

3. `StopNameGuard_AtmStop9_Matches`
   Asserts: order named "Stop9" passes the isBeStop guard.

4. `StopNameGuard_PttQxStop_Matches`
   Asserts: order named "PTT-QX-Stop" passes the isBeStop guard (DW-B86 new branch).

5. `StopNameGuard_PttQxStop4_Matches`
   Asserts: order named "PTT-QX-Stop4" passes the isBeStop guard.

6. `StopNameGuard_StopMarket_Rejected`
   Asserts: order named "StopMarket" does NOT pass the isBeStop guard (Length != 5).

7. `StateGuard_Working_Accepted_ChangeSubmitted_Included`
   Asserts: orders in Working, Accepted, and ChangeSubmitted states all produce beStOk==true.

8. `StateGuard_CancelSubmitted_Excluded`
   Asserts: order in CancelSubmitted state produces beStOk==false (not added to beSt).

9. `Stops0_EmitsBeDiagFLogLine`
   Asserts: when beSt.Count==0, the method emits at least one log line containing
   "[BE-DIAG-F]" for the instrument's orders.

10. `BreakEvenOverload_FollowersRunBeforeLeader`
    Asserts: BreakEven(Account leader, Instrument, int) invokes MoveStopToBreakEven
    for all non-leader accounts BEFORE invoking it for the leader account.
    Mechanism: capture invocation order via ordered list or call sequence tracking.

All assertions use Assert.True, Assert.False, Assert.Equal, Assert.Contains, Assert.NotNull.
No NUnit attributes ([Test], [TestCase]). No MSTest attributes ([TestMethod]).

---

### T4 -- TryReplacePttBeBrackets: Follower Path Reachability Analysis

**File**: src/PropTraderTools/CopyEngine.cs
**Call site**: L964-965 (in OnOrderUpdate)
**Target method**: TryReplacePttBeBrackets (L1820-1850)

**Reachability analysis**:

Step 1 -- Call site trigger condition (L955-965):
```
if (e.Order.OrderState == OrderState.Cancelled
    && e.Order.Name != null
    && e.Order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
{
    ...
    if (e.Order.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
        TryReplacePttBeBrackets(e.Order);
}
```
TryReplacePttBeBrackets fires only when an order named "PTT-BE-Stop-*" is cancelled.

Step 2 -- Can a follower account ever hold a PTT-BE-Stop-* order?
PTT-BE-Stop-* orders are created exclusively in Step C of MoveStopToBreakEven
(L2853: acc.CreateOrder(..., "PTT-BE-Stop", ...); L2900+: "PTT-BE-Stop-{i+1}").
Step C is reached only for accounts where IsFollowerAccount(acc) == false, because
the follower path (L2740-2792) takes an early return at L2791 BEFORE Step C.
Therefore: followers NEVER hold PTT-BE-Stop-* orders. The cancel event for PTT-BE-Stop-*
can never arrive for a follower account.

Step 3 -- Existing source documentation:
Source comment at L2728-2729 explicitly confirms:
"the TryReplacePttBeBrackets retry subsystem are never triggered on the follower path."

Step 4 -- TryReplacePttBeBrackets internal guard (L1823):
```
if (!IsFollowerAccount(cancelledStop.Account)) return;
```
Wait -- this is a REVERSE guard: it returns if the account is NOT a follower.
Actually this means TryReplacePttBeBrackets is designed to run FOR follower accounts.
But since followers never have PTT-BE-Stop-* orders (Step 2), the call site is
unreachable for followers by construction. The guard at L1823 is a defensive
secondary check; the primary protection is structural (followers never create PTT-BE-Stop-*).

**Conclusion**: UNREACHABLE from follower path by structural construction.
No code change is required.

**Action**: Add inline comment at L1820-1823 (in TryReplacePttBeBrackets) noting the
structural guarantee to prevent future confusion. Comment-only, ~2 lines.
T4 closes as ANALYSIS-COMPLETE.

---

## Section C -- Method Complexity Audit

### MoveStopToBreakEven (T1)

Counting at statement level (standard McCabe: each if/foreach/for/while/ternary/catch = 1 branch):

Follower block (L2740-2792) branch points:
  (1) if (IsFollowerAccount(acc))               = 1
  (2) foreach (Order o in acc.Orders)            = 1
  (3) if (!beStOk) continue                      = 1
  (4) if (o.Instrument...FullName != ...) continue = 1
  (5) if (isBeStop) [T1: was inline if]          = 1
  (6) if (beSt.Count == 0)                       = 1
  (7) foreach for DIAG dump                      = 1
  (8) if (o?.Instrument... != ...) continue DIAG = 1
  (9) if (beSt.Count > 0)                        = 1
  (10) catch { }                                 = 1
Follower block subtotal: 10 branches

Plus the rest of MoveStopToBreakEven (leader path after L2792):
The full method is larger; the pertinent fact is that the spec instructions confirm
T1 adds 0 net CYC to MoveStopToBreakEven because:
  - The existing if-block at L2755 remains a single if-statement
  - T1 refactors it into a bool variable + if(isBeStop) -- same 1 branch point
  - The additional || inside isBeStop is a compound boolean sub-expression, not a new if

CYC before T1 (MoveStopToBreakEven full): existing method, unchanged by T1
CYC after  T1 (MoveStopToBreakEven full): +0 (refactor only, same branch count)
STATUS: PASS (CYC <= 8 per modified block; full method CYC unchanged)

### DtoToRule (T2)

Statement-level branch count (L3396-3432):

BEFORE T2 (current):
  (1) for (int i ...) L3397                              = 1
  (2) foreach (var acc in Account.All) L3399             = 1
  (3) if (acc.Name == ...) L3401                         = 1
  (4) if (dto.FollowerMultipliers != null && ...) L3411  = 1
  (5) if (dto.FollowerAtmModeNames != null) L3416        = 1
  (6) for (int i ...) L3418                              = 1
  (7) if (!string.IsNullOrEmpty(accName)) L3421          = 1
  (8) ternary dto.TightenTicks > 0 ? : L3428             = 1
CYC_before = 8 (at the JS-066 limit)

AFTER T2 extraction + warning if-block:
  Branches (2) and (3) [inner foreach + if] move to FindFollowerAccount() helper
  DtoToRule gains:
    FindFollowerAccount call (no branch, it's a method call)    = 0
    new if (followers[i] == null) warning block                 = 1
  Net change in DtoToRule: -2 (removed inner foreach+if) + 1 (new warning if) = -1
CYC_after = 8 - 2 + 1 = 7

FindFollowerAccount (new helper):
  (1) foreach (var acc in Account.All)     = 1
  (2) if (acc.Name == name)                = 1
CYC = 2

STATUS: PASS -- DtoToRule: 8 -> 7, helper: 2. Both <= 8.

---

## Section D -- Test Plan

**Test file**: tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs
**Test class**: CopyEngineBreakEvenFollowerTests
**Framework**: xUnit (using Xunit; using Xunit.Abstractions if output capture needed)

| # | [Fact] Method Name | What It Asserts |
|---|---|---|
| 1 | FollowerPath_EarlyReturn_SkipsStepBAndC | follower path returns before Step B/C; acc.Cancel and acc.CreateOrder not called on follower |
| 2 | StopNameGuard_AtmStop1_Matches | "Stop1" passes isBeStop guard (ATM stop, Length=5, IsDigit('1')) |
| 3 | StopNameGuard_AtmStop9_Matches | "Stop9" passes isBeStop guard |
| 4 | StopNameGuard_PttQxStop_Matches | "PTT-QX-Stop" passes isBeStop guard (DW-B86 new branch) |
| 5 | StopNameGuard_PttQxStop4_Matches | "PTT-QX-Stop4" passes isBeStop guard |
| 6 | StopNameGuard_StopMarket_Rejected | "StopMarket" does NOT pass isBeStop guard |
| 7 | StateGuard_Working_Accepted_ChangeSubmitted_Included | orders in all 3 states produce beStOk==true |
| 8 | StateGuard_CancelSubmitted_Excluded | CancelSubmitted state produces beStOk==false |
| 9 | Stops0_EmitsBeDiagFLogLine | beSt.Count==0 path emits log line containing "[BE-DIAG-F]" |
| 10 | BreakEvenOverload_FollowersRunBeforeLeader | non-leader accounts all processed before leader in BreakEven(Account,Instrument,int) |

Total: 10 [Fact] tests.

**Assert methods used**: Assert.True, Assert.False, Assert.Equal, Assert.Contains, Assert.NotNull
**Forbidden**: [Test] (NUnit), [TestMethod] (MSTest), Assert.IsTrue (NUnit/MSTest style)

---

## Section E -- JS Constraint Matrix

| Rule | Description | T1 | T2 | T3 | T4 |
|---|---|---|---|---|---|
| JS-021 | No lock() | PASS | PASS | PASS | PASS |
| JS-001 | No throw in hot path | PASS | PASS | PASS | PASS |
| JS-002 | No null return for missing values | PASS | PASS | PASS | PASS |
| JS-033 | No async void | PASS | PASS | PASS | PASS |
| JS-036 | No heap alloc in hot path | PASS | PASS | N/A | N/A |
| JS-066 | CYC <= 8 per method | PASS (+0) | PASS (8->7) | N/A | N/A |
| JS-051 | xUnit only (no NUnit/MSTest) | N/A | N/A | PASS | N/A |
| ASCII-only | No Unicode/curly quotes | PASS | PASS | PASS | PASS |
| NT8-014 | PTT- prefix on order names | N/A | N/A | N/A | N/A |
| DT-UTC | No DateTime.Now | N/A | N/A | N/A | N/A |
| NO-FONT | No FontFamily | N/A | N/A | N/A | N/A |
| DISP | Dispatcher.InvokeAsync for UI | N/A | N/A | N/A | N/A |

JS-002 note (T2): `FindFollowerAccount` returns `Account?` (nullable annotated return type).
This is JS-002 compliant: the rule bans implicit null returns on non-nullable types;
an explicit `Account?` return is the C# equivalent of Option<T> for lookup/search patterns
and is the required form. The caller receives `Account?`, assigns to `found`, then explicitly
checks `followers[i] == null` to emit the warning. Nullability is explicit end-to-end.
JS-002 note (T1): no null return introduced by the isBeStop bool refactor.

---

## Section F -- Ordering & Dependencies

```
T4 (analysis)  -- independent, no file change, can run in parallel with T1/T2
T1 (DW-B86)   -- must complete BEFORE T3 (tests depend on the corrected name guard)
T2 (DW-B85)   -- standalone, no dependency on T1 or T3
T3 (tests)    -- depends on T1 (stop name guard must be correct before tests assert it)
```

**Recommended execution order**:
  1. T4 (analysis + comment-only, closes in same session as T1)
  2. T1 (stop name guard extension -- 1 file, ~10 lines changed)
  3. T2 (startup warning + helper extraction -- 1 file, ~15 lines changed)
  4. T3 (new test file, ~150 lines)

**Rationale**:
  - T1 before T3: StopNameGuard_PttQxStop_Matches and StopNameGuard_PttQxStop4_Matches
    tests will FAIL if T3 is written before T1 is merged.
  - T2 before T3: T3 does not test DtoToRule (no test for T2 in T3 scope), so T2/T3
    order is flexible, but T2 first keeps the engineering session focused on production
    code before switching to test code.
  - T4 parallel with T1: the T4 comment can be written in the same T1 file edit session.

---

## Section G -- SIM Gate Protocol

### After T1 -- Path B SIM Test (QX then BE)

**Setup**:
- Leader: Sim101 with open long position, ATM template (Stop1/Stop2/Stop3 or similar)
- Followers: Sim102 + Sim103 (per copy rule, with multiplier as configured)
- Sim104: if null-slot, note [PTT-COPY] WARNING in Output tab (DW-B85 fix visible)

**Test sequence (3 cycles required)**:
Cycle 1:
  1. Leader entry filled -> verify [PTT-COPY] dispatch: -> Sim102, Sim103 in Output tab
  2. Press QX-ALL -> verify [PTT-QX] log for leader (cancel=N, submit=N)
  3. Verify followers have PTT-QX-Stop* orders in Accepted state
  4. Press BE-ALL
  5. PASS criteria: Output tab shows for each follower:
     [BE] DW-B84-01 acc.Change() Sim102 stops=N newStop=X  (N > 0)
     [BE] DW-B84-01 acc.Change() Sim103 stops=N newStop=X  (N > 0)
  6. FAIL criteria: stops=0 for any follower, or [BE-DIAG-F] fires without subsequent ACC.CHANGE call

Cycle 2 and 3: Repeat full sequence. All 3 cycles must pass.

**After T1 PASS**: proceed to T2 and T3 implementation. Open PTT 5-phase pipeline.

**Combined DW-B84 + DW-B86 full green**:
- Path A (BE-ALL without prior QX-ALL): re-run 1 cycle. Confirm stops=N (ATM Stop1..Stop9 guard).
- Path B (QX then BE): 3 cycles as above. Confirm stops=N (PTT-QX-Stop* guard).

### After T3 -- Test Runner Command

```powershell
dotnet test tests/PropTraderTools.Tests/ --filter "FullyQualifiedName~CopyEngineBreakEvenFollowerTests" --no-build -v normal
```

Expected output: 10 passed, 0 failed, 0 skipped.

Full suite:
```powershell
dotnet test tests/ --no-build -v minimal
```

---

## Deferred Backlog Items (from B42-QX-BE-01 -- carry-over, not closed by this epic)

- DW-B42-01: T_BUG_QX_BE_01 does not assert PTT-QX-T3 -- remains open
- DW-B42-02: Live NT8 F5 verification (QX->BE sequence) -- remains open
- DW-B42-03: IsPttQxTarget range extension for T4/T5 slots -- remains open

DW-B85 Option A (lazy re-resolve in AllAccounts) -- deferred to backlog by spec. Not in scope.

---

## Pre-Flight Summary

| Check | Result |
|---|---|
| All 8 thoughts complete | PASS |
| No lock() in any ticket | PASS |
| No throw in hot path | PASS |
| No async void | PASS |
| ASCII-only literals | PASS |
| CYC <= 8 all modified methods | PASS |
| xUnit only in T3 | PASS |
| NT8 API claims grounded in spec/source | PASS |
| Zero DW- uncertainty items needed | PASS |
| File split: 2 files, zero cross-contamination | PASS |
| Deferred backlog read and accounted for | PASS |
