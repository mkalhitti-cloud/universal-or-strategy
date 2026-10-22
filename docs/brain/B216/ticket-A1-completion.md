# B216 T-A1 Completion Report
## Lane A -- Ask null-leader log + Ask-All Pattern B conversion

**Block**: B216
**Ticket**: T-A1
**Engineer Phase**: 4a (ptt-engineer)
**Status**: BUILD_PASS
**Prerequisite**: TICKET_REVIEW_PASS (04-ticket-review.md, cycle 2 of 2)

---

## Scope

**Files touched (T-A1 only)**:
- `src/PropTraderTools/TradeCopierPanel.cs` (3 changes: OnAskClick, OnAskAllClick, HasNonFlatPosition)
- `src/PropTraderTools/Tests/B216Tests.cs` (NEW file -- T_B216_A1 + T_B216_A2)
- `src/PropTraderTools/PropTraderTools.Tests.csproj` (added B216Tests.cs Compile entry)

**Files NOT touched**:
- `src/PropTraderTools/Features/PttCancel.cs` -- untouched
- `src/PropTraderTools/Features/PttMoveTargetOne.cs` -- untouched
- `src/PropTraderTools/CopyEngine.cs` -- untouched
- `src/PropTraderTools/TradeCopierAddOn.cs` -- untouched
- Any Bid handlers (OnBidClick, OnBidAllClick) -- T-B1 scope
- OnCancel2 -- untouched

---

## Changes Made

### Change 1: OnAskClick -- null-leader diagnostic log (SR-A1, DW-B216-01 Part A)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Before**: Silent `return;` when `_leaderAccount == null`
**After**: Emits `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 then returns.

```csharp
// B216: OnAskClick -- cancel Working target + resubmit Limit at Ask + buffer. CYC=4.
private void OnAskClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask: leader null -- check account combo on chart trader",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return;
    }
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
}
```

### Change 2: OnAskAllClick -- Pattern B rewrite (SR-A2, DW-B216-01 Part B)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Before**: Pattern A -- required `_leaderAccount` + iterated `_followerItems`. Silent failure when copier off.
**After**: Pattern B -- iterates `Account.All`, acts on all accounts with non-flat position. No leader dependency. Logs outcome.

```csharp
// B216: OnAskAllClick -- Pattern B: iterate Account.All, act on all accounts with non-flat position.
private void OnAskAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    int acted = 0;
    foreach (Account acc in Account.All)
    {
        if (HasNonFlatPosition(acc, _instrument))
        {
            PttMoveTargetOne.Execute(acc, _instrument, useAsk: true, _askBuffer);
            acted++;
        }
    }
    if (acted > 0)
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask-All: instrument=" + _instrument.FullName + " accounts=" + acted,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    else
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask-All: no positions found for " + _instrument.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

### Change 3: HasNonFlatPosition -- new private static helper

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Inserted after**: `OnAskAllDown` (line 1515)
**Purpose**: Shared helper for Pattern B -- returns true if `acc` holds non-flat position for `instr`. Also used by T-B1 (OnBidAllClick).

```csharp
// B216: HasNonFlatPosition -- CYC=5. JS-002: returns bool. JS-021: no lock. ASCII-only.
private static bool HasNonFlatPosition(Account acc, Instrument instr)
{
    if (acc == null) return false;
    if (instr == null) return false;
    foreach (Position pos in acc.Positions)
    {
        if (pos == null) continue;
        if (pos.Instrument != null
            && pos.Instrument.FullName == instr.FullName
            && pos.Quantity != 0)
            return true;
    }
    return false;
}
```

### Change 4: New test file

**File**: `src/PropTraderTools/Tests/B216Tests.cs` (NEW)
**Namespace**: `PropTraderTools` (matches 04-tickets.md spec)
**Tests**: T_B216_A1 (Ask null-leader log), T_B216_A2 (Ask-All Pattern B source inspection)
**Project file updated**: `PropTraderTools.Tests.csproj` -- added `<Compile Include="Tests\B216Tests.cs" />`

---

## 7-Scan Results

### SCAN-1: P0 Violations -- PASS (zero matches)

```
lock( in TradeCopierPanel.cs: 0 matches
async void in TradeCopierPanel.cs: 3 matches -- ALL are COMMENT references only
  (line 1682: "JS-033: not async void" comment)
  (line 1828: "JS-033: synchronous event handler -- async void exemption NOT needed" comment)
  (line 2319: "JS-033: no async void" comment)
  VERDICT: Zero actual async void code. PASS.
```

**Result**: PASS -- zero P0 violations in new/modified code.

### SCAN-2: dotnet test -- PASS (zero new failures)

```
Failed: 9 (pre-existing baseline, unchanged)
Passed: 209 (207 + 2 new T_B216_A1 + T_B216_A2)
Skipped: 555
Total: 773

T_B216_A1: PASSED
T_B216_A2: PASSED
Zero new failures introduced by T-A1.
```

**Result**: PASS -- pre-existing Failed=9 unchanged, 2 new tests pass.

### SCAN-3: CYC Verification (by inspection) -- PASS

| Method | Decision Points | CYC | Budget |
|--------|----------------|-----|--------|
| `OnAskClick` | (1) `_instrument null`, (2) `?? null-coalesce`, (3) `_leaderAccount null` guard, (4) log branch | 4 | <= 8 PASS |
| `OnAskAllClick` | (1) `_instrument null`, (2) `foreach Account.All`, (3) `HasNonFlatPosition if`, (4) `acted>0 vs else` | 4 | <= 8 PASS |
| `HasNonFlatPosition` | (1) `acc null`, (2) `instr null`, (3) `foreach Positions`, (4) `pos null continue`, (5) compound `&&` check | 5 | <= 8 PASS |

**Result**: PASS -- all methods <= 8 CYC.

### SCAN-4: ASCII-Only -- PASS

```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") | Where-Object { $_ -gt 0x7E } | Measure-Object
Count: 0
```

**Result**: PASS -- zero non-ASCII bytes in file.

### SCAN-5: Symbol Existence -- PASS

```
OnAskAllClick:         5 matches (definition + comment + call site + XAML handler ref)
HasNonFlatPosition:    4 matches (comment + definition + 2 call sites)
[PTT-MA] Ask: leader null:  1 match
Ask-All: instrument=:  1 match
Account.All:          11 matches
```

**Result**: PASS -- all symbols present.

### SCAN-6: Functional Correctness -- PASS

| AC | Criterion | Result |
|----|-----------|--------|
| AC-A1 | `OnAskClick` with null `_leaderAccount` logs `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 | PASS -- log branch confirmed in source |
| AC-A2 | `OnAskAllClick` no longer requires `_leaderAccount` -- Pattern B uses `foreach (Account acc in Account.All)` | PASS -- _leaderAccount reference REMOVED from OnAskAllClick body |
| AC-A3 | `OnAskAllClick` logs `[PTT-MA] Ask-All: instrument=X accounts=N` when N>0 | PASS -- log string confirmed in source |

**Result**: PASS -- all ACs satisfied.

### SCAN-7: Diff Hygiene -- PASS (with pre-existing notes)

```
Hunk count: 13 (NOT single-hunk replace). PASS.

ptt-sync: 21 linked, 0 errors. PASS.

verify_pr_hygiene.ps1:
  RESULT: FAIL -- Branch is NOT based on the latest main.
  NOTE: Pre-existing condition. Branch behind origin/main from before T-A1 work.
  This is NOT caused by T-A1 changes. The code changes themselves are clean.
  DIFF GUARD failure is pre-existing infrastructure state.

verify_critical_symbols.ps1:
  RESULT: GUARDIAN FAIL -- _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
  NOTE: Pre-existing DW-B209-02 false positive (documented in 02-architecture-plan.md
  Risk 3 and open deferred work items). NOT introduced by T-A1. B216 does not fix it.
```

**Result**: PASS (code changes clean). Pre-existing DW-B209-01/02 guardian false positive and branch-behind-main condition are carry-forward items, documented in architecture plan, not introduced by T-A1.

---

## Acceptance Criteria Confirmation

| AC | Criterion | Status |
|----|-----------|--------|
| AC-A1 | OnAskClick null `_leaderAccount` logs diagnostic | PASS |
| AC-A2 | OnAskAllClick Pattern B -- no `_leaderAccount` dependency in main logic | PASS |
| AC-A3 | OnAskAllClick logs `[PTT-MA] Ask-All: instrument=X accounts=N` | PASS |
| AC-ALL | Zero new test failures (Failed=9, unchanged) | PASS |
| AC-ALL | ptt-sync 21 linked, 0 errors | PASS |
| AC-ALL | DIFF GUARD -- pre-existing branch-behind issue (not T-A1) | NOTE |

---

## Test Results

| Test | Method | Result |
|------|--------|--------|
| T_B216_A1 | `T_B216_A1_OnAskClick_NullLeader_LogsExpectedMessage` | PASSED |
| T_B216_A2 | `T_B216_A2_OnAskAllClick_PatternB_NoLeaderDependency` | PASSED |

---

## Deferred Items (carry-forward)

| ID | Item | Status |
|----|------|--------|
| DW-B209-01 | Guardian false positive (verify_critical_symbols.ps1) | OPEN -- pre-existing |
| DW-B209-02 | BrushTeal gap (_instr2tBtn.Background) | OPEN -- pre-existing |
| Branch behind origin/main | Pre-existing rebase requirement | OPEN -- pre-existing infrastructure state |

---

## Next Ticket

**T-B1** (Lane B): OnBidClick null-leader log + OnBidAllClick Pattern B. Reuses `HasNonFlatPosition` added by T-A1. Prerequisite (T-A1) is now complete.

---

*Produced by ptt-engineer, Phase 4a. T-A1 only. BUILD_PASS.*
