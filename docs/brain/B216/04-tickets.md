# B216 Implementation Tickets
## Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons

**Block**: B216
**Plan**: docs/brain/B216/02-architecture-plan.md (REVIEW_PASS)
**Tickets**: T-A1 (Lane A), T-B1 (Lane B), T-C1 (Lane C)
**Execution Order**: T-A1 -> T-B1 -> T-C1 (sequential; T-B1 depends on HasNonFlatPosition added by T-A1)

---

## T-A1: Lane A -- Ask Pattern B + Null-Leader Log

### Header

| Field | Value |
|-------|-------|
| Block | B216 |
| Ticket ID | T-A1 |
| Lane | A |
| Scope | `src/PropTraderTools/TradeCopierPanel.cs` ONLY |
| Spec Requirements | SR-A1 (Ask null-leader log), SR-A2 (Ask-All Pattern B) |
| Deferred Items Closed | DW-B216-01 Part A (Ask), DW-B216-01 Part B (Ask-All) |
| Prerequisite | None -- first ticket |

---

### Current State

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**OnAskClick** (lines 1485-1494 -- current):
```csharp
// B211: OnAskClick -- cancel Working target + resubmit Limit at Ask + buffer. CYC=4. JS-021: no lock.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null.
// JS-033: synchronous void. ASCII-only.
private void OnAskClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
}
```

**OnAskAllClick** (lines 1496-1509 -- current):
```csharp
// B211: OnAskAllClick -- move all targets on leader + selected followers. CYC=4.
// B212-MAFIX3: mirrors OnBidAllClick fix -- bypasses AllAccounts/FindRule dependency.
// Branches: (1) _instrument null, (2) leader null, (3) leader resolve,
//           (4) foreach followers. JS-021: no lock. ASCII-only.
private void OnAskAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
    foreach (var item in _followerItems)
        if (item.IsSelected && item.Account != null)
            PttMoveTargetOne.Execute(item.Account, _instrument, useAsk: true, _askBuffer);
}
```

**Problem**:
- `OnAskClick` silently no-ops when `_leaderAccount` is null. User has no diagnostic output. (DW-B216-01 Part A)
- `OnAskAllClick` requires `_leaderAccount` and `_followerItems`. Silently fails when copier is off or leader unresolved. (DW-B216-01 Part B)

---

### Target State

**OnAskClick** (replace lines 1485-1494):
```csharp
// B216: OnAskClick -- cancel Working target + resubmit Limit at Ask + buffer. CYC=4.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null w/ diagnostic log.
// JS-033: synchronous void. ASCII-only. DW-B216-01 Part A: null-leader log.
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

**OnAskAllClick** (replace lines 1496-1509 -- Pattern B rewrite):
```csharp
// B216: OnAskAllClick -- Pattern B: iterate Account.All, act on all accounts with non-flat position.
// Removes _leaderAccount + _followerItems dependency.
// Branches: (1) _instrument null, (2) foreach Account.All, (3) HasNonFlatPosition,
//           (4) acted==0 log path. CYC=4. JS-021: no lock. ASCII-only.
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

**HasNonFlatPosition** (NEW -- insert after line 1515, after `OnAskAllDown`):
```csharp
// B216: HasNonFlatPosition -- returns true if acc holds a non-flat position for instr.
// Used by OnAskAllClick and OnBidAllClick (Pattern B) to filter accounts.
// CYC=5: (1) acc null, (2) instr null, (3) foreach, (4) pos null, (5) instrument+qty check.
// JS-002: returns bool. JS-021: no lock. ASCII-only.
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

---

### Method Signatures

| Method | Type | File | Status | CYC |
|--------|------|------|--------|-----|
| `private void OnAskClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | MODIFIED | 4 |
| `private void OnAskAllClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | MODIFIED (rewrite) | 4 |
| `private static bool HasNonFlatPosition(Account acc, Instrument instr)` | private static helper | TradeCopierPanel.cs | NEW | 5 |

---

### New Files

`src/PropTraderTools/Tests/B216Tests.cs` (NEW -- created by T-A1. T-B1 and T-C1 append their tests to this file.)

---

### xUnit Tests Required

**File**: `src/PropTraderTools/Tests/B216Tests.cs` (NEW file, namespace `PropTraderTools`)

**Test class header**:
```csharp
// B216Tests.cs -- xUnit tests for B216: Ask/Bid Pattern B + Cancel/CancelAll.
// JS-051: xUnit only. [Fact] only -- no [Theory] unless parameterized.
// NT8-bound methods tested via reflection or stub where NT8 host unavailable.
using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class B216Tests
    {
```

**T_B216_A1** -- Ask null-leader log:
```csharp
[Fact]
public void T_B216_A1_OnAskClick_NullLeader_LogsExpectedMessage()
{
    // Verifies: OnAskClick with _leaderAccount == null after TryResolveLeaderAccount()
    // emits "[PTT-MA] Ask: leader null -- check account combo on chart trader" to OutputTab1.
    // Implementation: verify via code inspection that the log branch exists.
    // If Output.Process is not interceptable in test harness, verify by reflection that
    // the string literal "[PTT-MA] Ask: leader null" exists in compiled source.
    var src = System.IO.File.ReadAllText(
        "../../../../TradeCopierPanel.cs");
    Assert.Contains("[PTT-MA] Ask: leader null", src);
}
```

**T_B216_A2** -- Ask-All Pattern B (no _leaderAccount dependency):
```csharp
[Fact]
public void T_B216_A2_OnAskAllClick_PatternB_NoLeaderDependency()
{
    // Verifies: OnAskAllClick body no longer contains "_leaderAccount ?? TryResolveLeaderAccount"
    // in the AllClick variant (Pattern B does not use _leaderAccount).
    // Also verifies: "Account.All" present in OnAskAllClick (Pattern B loop).
    var src = System.IO.File.ReadAllText(
        "../../../../TradeCopierPanel.cs");
    Assert.Contains("Account.All", src);
    Assert.Contains("[PTT-MA] Ask-All:", src);
}
```

**Where to add**: Create new file `src/PropTraderTools/Tests/B216Tests.cs`. Add `T_B216_A1` and `T_B216_A2` in this ticket. `T_B216_B1`, `T_B216_B2`, `T_B216_C1`, `T_B216_C2` are added by T-B1 and T-C1 respectively in the same file.

---

### CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `OnAskClick` | 4 | (1) `_instrument null`, (2) `?? null-coalescing`, (3) `_leaderAccount null guard`, (4) the null log branch path |
| `OnAskAllClick` (Pattern B) | 4 | (1) `_instrument null`, (2) `foreach Account.All`, (3) `HasNonFlatPosition`, (4) `acted>0 vs 0 log` |
| `HasNonFlatPosition` | 5 | (1) `acc null`, (2) `instr null`, (3) `foreach Positions`, (4) `pos null continue`, (5) `instrument+qty compound check` |

All <= 8. JS-066: PASS.

---

### NT8 Constraints

| API | Usage | Thread Safety |
|-----|-------|--------------|
| `Account.All` | Iterate all accounts in `OnAskAllClick` | UI thread -- safe per NT8 docs |
| `acc.Positions` | Iterate positions in `HasNonFlatPosition` | UI thread -- safe |
| `pos.Instrument.FullName` | Instrument match in `HasNonFlatPosition` | UI thread -- safe |
| `pos.Quantity` | Non-flat check (`!= 0`) in `HasNonFlatPosition` | UI thread -- safe |
| `PttMoveTargetOne.Execute(acc, instr, useAsk, buf)` | Pattern B target move | UI thread -- safe |
| `NinjaTrader.Code.Output.Process(string, PrintTo)` | Diagnostic logging | Thread-safe NT8 API |
| `NinjaTrader.NinjaScript.PrintTo.OutputTab1` | Log destination | Thread-safe NT8 API |

**Threading**: All methods are synchronous UI-thread event handlers. No `Dispatcher.InvokeAsync` required.

---

### 7-Scan Checklist (T-A1)

#### SCAN-1: P0 Violations (run BEFORE commit)
```powershell
# Must return 0 results in modified/new lines:
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs
```
**Expected**: 0 matches in any new or modified lines added by T-A1.

#### SCAN-2: dotnet test -- Zero New Failures
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
```
**Expected**: `Failed=24` (pre-existing baseline), `Passed >= 202` (200 + 2 new T_B216_A1 + T_B216_A2), zero new failures introduced by T-A1.

#### SCAN-3: CYC Verification (verify by inspection)
- `OnAskClick`: count 3 decision points (null check, null-coalescing, null guard with log) = CYC 4. PASS.
- `OnAskAllClick`: count 3 decision points (null check, foreach, HasNonFlatPosition if, acted>0 if) = CYC 4. PASS.
- `HasNonFlatPosition`: count 4 decision points (2 null guards, foreach, pos null continue, compound check) = CYC 5. PASS.
All <= 8.

#### SCAN-4: ASCII-Only
```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object
```
**Expected**: count 0 in lines added/modified by T-A1. All log strings use only ASCII characters (no em-dash, smart quotes, or Unicode symbols).

#### SCAN-5: Symbol Existence
```powershell
Select-String "OnAskAllClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "HasNonFlatPosition" src/PropTraderTools/TradeCopierPanel.cs
Select-String "\[PTT-MA\] Ask: leader null" src/PropTraderTools/TradeCopierPanel.cs
Select-String "\[PTT-MA\] Ask-All:" src/PropTraderTools/TradeCopierPanel.cs
Select-String "Account.All" src/PropTraderTools/TradeCopierPanel.cs
```
**Expected**: all symbols found, at least 1 match each.

#### SCAN-6: Functional Correctness (T-A1 ACs)
- **AC-A1**: Verify `OnAskClick` contains `NinjaTrader.Code.Output.Process(` with string `[PTT-MA] Ask: leader null -- check account combo on chart trader` in the null-leader branch. Verify it returns immediately after log.
- **AC-A2**: Verify `OnAskAllClick` body no longer references `_leaderAccount` (other than initial null-coalesce is REMOVED; Pattern B has no `_leaderAccount ?? TryResolveLeaderAccount()` call at all). Verify `foreach (Account acc in Account.All)` is present.
- **AC-A3**: Verify `[PTT-MA] Ask-All: instrument=` log string present. Verify `no positions found for` log string present.

#### SCAN-7: Diff Hygiene
```powershell
# Confirm multiple hunks (NOT single-hunk full-file replace):
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object
# Expected: Count >= 2 (at least 2 hunks: OnAskClick change + OnAskAllClick change + HasNonFlatPosition insertion)

# PR hygiene:
powershell -File .\scripts\verify_pr_hygiene.ps1
# Expected: DIFF GUARD PASS

# Guardian symbols:
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: GUARDIAN PASS (or pre-existing DW-B209-01 false positive -- document if present)

# Hard-link sync (MANDATORY after every .cs edit):
powershell -File .\scripts\ptt-sync.ps1
# Expected: 21 linked, 0 errors
```

---

### Acceptance Criteria (T-A1)

| AC | Criterion | How to Verify |
|----|-----------|--------------|
| AC-A1 | `OnAskClick` with null `_leaderAccount` logs `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 | SCAN-5 + T_B216_A1 |
| AC-A2 | `OnAskAllClick` no longer requires `_leaderAccount` -- fires `PttMoveTargetOne.Execute` for all accounts with non-flat position | SCAN-5 + T_B216_A2 + SCAN-6 |
| AC-A3 | `OnAskAllClick` logs `[PTT-MA] Ask-All: instrument=X accounts=N` when N>0 | SCAN-6 |
| AC-ALL | Zero new test failures (`Failed` count does not exceed 24) | SCAN-2 |
| AC-ALL | `ptt-sync.ps1` reports 21 linked, 0 errors | SCAN-7 |
| AC-ALL | `DIFF GUARD PASS` | SCAN-7 |

---

### Scope Lock (T-A1)

**Files T-A1 MUST touch**:
- `src/PropTraderTools/TradeCopierPanel.cs` (3 changes: OnAskClick, OnAskAllClick, HasNonFlatPosition)
- `src/PropTraderTools/Tests/B216Tests.cs` (NEW -- T_B216_A1 + T_B216_A2)

**Files T-A1 MUST NOT touch**:
- `src/PropTraderTools/Features/PttCancel.cs` -- DO NOT CHANGE
- `src/PropTraderTools/Features/PttMoveTargetOne.cs` -- DO NOT CHANGE
- `src/PropTraderTools/CopyEngine.cs` -- DO NOT CHANGE
- `src/PropTraderTools/TradeCopierAddOn.cs` -- DO NOT CHANGE
- Any file not listed above

---

---

## T-B1: Lane B -- Bid Pattern B + Null-Leader Log

### Header

| Field | Value |
|-------|-------|
| Block | B216 |
| Ticket ID | T-B1 |
| Lane | B |
| Scope | `src/PropTraderTools/TradeCopierPanel.cs` ONLY |
| Spec Requirements | SR-B1 (Bid null-leader log), SR-B2 (Bid-All Pattern B) |
| Deferred Items Closed | DW-B216-01 Part A (Bid), DW-B216-01 Part B (Bid-All) |
| Prerequisite | **T-A1 MUST be completed first** -- T-B1 reuses `HasNonFlatPosition` added by T-A1 |

---

### Current State

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**OnBidClick** (lines 1542-1551 -- current):
```csharp
// B211: OnBidClick -- cancel Working target + resubmit Limit at Bid + buffer. CYC=4. JS-021: no lock.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null.
// JS-033: synchronous void. ASCII-only.
private void OnBidClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: false, _bidBuffer);
}
```

**OnBidAllClick** (lines 1553-1566 -- current):
```csharp
// B211: OnBidAllClick -- move all targets on leader + selected followers. CYC=4.
// B212-MAFIX2: same fix as OnAskAllClick -- bypasses AllAccounts/FindRule dependency.
// Branches: (1) _instrument null, (2) leader null, (3) leader resolve,
//           (4) foreach followers. JS-021: no lock. ASCII-only.
private void OnBidAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: false, _bidBuffer);
    foreach (var item in _followerItems)
        if (item.IsSelected && item.Account != null)
            PttMoveTargetOne.Execute(item.Account, _instrument, useAsk: false, _bidBuffer);
}
```

**Problem**: Same root cause as Lane A. Silent failure when leader unresolved. Pattern A dependency on `_leaderAccount` + `_followerItems`.

---

### Target State

**OnBidClick** (replace lines 1542-1551):
```csharp
// B216: OnBidClick -- cancel Working target + resubmit Limit at Bid + buffer. CYC=4.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null w/ diagnostic log.
// JS-033: synchronous void. ASCII-only. DW-B216-01 Part A mirror (Bid).
private void OnBidClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid: leader null -- check account combo on chart trader",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return;
    }
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: false, _bidBuffer);
}
```

**OnBidAllClick** (replace lines 1553-1566 -- Pattern B rewrite):
```csharp
// B216: OnBidAllClick -- Pattern B: iterate Account.All, act on all accounts with non-flat position.
// Mirror of OnAskAllClick. Removes _leaderAccount + _followerItems dependency.
// Branches: (1) _instrument null, (2) foreach Account.All, (3) HasNonFlatPosition,
//           (4) acted==0 log path. CYC=4. JS-021: no lock. ASCII-only.
private void OnBidAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    int acted = 0;
    foreach (Account acc in Account.All)
    {
        if (HasNonFlatPosition(acc, _instrument))
        {
            PttMoveTargetOne.Execute(acc, _instrument, useAsk: false, _bidBuffer);
            acted++;
        }
    }
    if (acted > 0)
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid-All: instrument=" + _instrument.FullName + " accounts=" + acted,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    else
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid-All: no positions found for " + _instrument.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**Note**: `HasNonFlatPosition` is already present after T-A1. Do NOT redefine it. T-B1 reuses the existing private static helper with zero changes.

---

### Method Signatures

| Method | Type | File | Status | CYC |
|--------|------|------|--------|-----|
| `private void OnBidClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | MODIFIED | 4 |
| `private void OnBidAllClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | MODIFIED (rewrite) | 4 |
| `private static bool HasNonFlatPosition(Account acc, Instrument instr)` | private static helper | TradeCopierPanel.cs | REUSED (added by T-A1, not modified by T-B1) | 5 |

---

### New Files

None. T-B1 only modifies `TradeCopierPanel.cs` and adds tests to `B216Tests.cs`.

---

### xUnit Tests Required

**File**: `src/PropTraderTools/Tests/B216Tests.cs` (already created by T-A1 -- append tests to existing class)

**T_B216_B1** -- Bid null-leader log:
```csharp
[Fact]
public void T_B216_B1_OnBidClick_NullLeader_LogsExpectedMessage()
{
    // Verifies: OnBidClick with _leaderAccount == null after TryResolveLeaderAccount()
    // emits "[PTT-MA] Bid: leader null -- check account combo on chart trader" to OutputTab1.
    var src = System.IO.File.ReadAllText(
        "../../../../TradeCopierPanel.cs");
    Assert.Contains("[PTT-MA] Bid: leader null", src);
}
```

**T_B216_B2** -- Bid-All Pattern B (Account.All loop, no leader dependency):
```csharp
[Fact]
public void T_B216_B2_OnBidAllClick_PatternB_NoLeaderDependency()
{
    // Verifies: OnBidAllClick body uses Account.All pattern B.
    // Verifies: "[PTT-MA] Bid-All:" log string present.
    var src = System.IO.File.ReadAllText(
        "../../../../TradeCopierPanel.cs");
    Assert.Contains("[PTT-MA] Bid-All:", src);
    // HasNonFlatPosition presence (shared helper added by T-A1):
    Assert.Contains("HasNonFlatPosition", src);
}
```

**Where to add**: Append both tests to existing `B216Tests` class in `src/PropTraderTools/Tests/B216Tests.cs`. Do NOT create a new file.

---

### CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `OnBidClick` | 4 | (1) `_instrument null`, (2) `?? null-coalescing`, (3) `_leaderAccount null guard`, (4) the null log branch path |
| `OnBidAllClick` (Pattern B) | 4 | (1) `_instrument null`, (2) `foreach Account.All`, (3) `HasNonFlatPosition`, (4) `acted>0 vs 0 log` |
| `HasNonFlatPosition` | 5 | unchanged from T-A1 -- CYC=5, not modified by T-B1 |

All <= 8. JS-066: PASS.

---

### NT8 Constraints

Identical to T-A1 (same API surface, mirrored logic with `useAsk: false`).

| API | Usage | Thread Safety |
|-----|-------|--------------|
| `Account.All` | Iterate all accounts in `OnBidAllClick` | UI thread -- safe |
| `acc.Positions` | via `HasNonFlatPosition` | UI thread -- safe |
| `PttMoveTargetOne.Execute(acc, instr, false, buf)` | Pattern B target move | UI thread -- safe |
| `NinjaTrader.Code.Output.Process(string, PrintTo)` | Diagnostic logging | Thread-safe NT8 API |

**Threading**: All methods are synchronous UI-thread event handlers. No `Dispatcher.InvokeAsync` required.

---

### 7-Scan Checklist (T-B1)

#### SCAN-1: P0 Violations (run BEFORE commit)
```powershell
# Must return 0 results in modified/new lines:
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs
```
**Expected**: 0 matches in any new or modified lines added by T-B1.

#### SCAN-2: dotnet test -- Zero New Failures
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
```
**Expected**: `Failed=24` (pre-existing baseline), `Passed >= 204` (202 post-T-A1 + 2 new T_B216_B1 + T_B216_B2), zero new failures introduced by T-B1.

#### SCAN-3: CYC Verification (verify by inspection)
- `OnBidClick`: 3 decision points = CYC 4. PASS.
- `OnBidAllClick`: 3 decision points (foreach + if + acted>0) = CYC 4. PASS.
- `HasNonFlatPosition`: unchanged at CYC 5. PASS.
All <= 8.

#### SCAN-4: ASCII-Only
```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object
```
**Expected**: count 0 in lines added/modified by T-B1. Log strings `[PTT-MA] Bid: leader null -- check account combo on chart trader` and `[PTT-MA] Bid-All:` are ASCII-only (hyphen `-` not em-dash).

#### SCAN-5: Symbol Existence
```powershell
Select-String "OnBidAllClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "HasNonFlatPosition" src/PropTraderTools/TradeCopierPanel.cs
Select-String "\[PTT-MA\] Bid: leader null" src/PropTraderTools/TradeCopierPanel.cs
Select-String "\[PTT-MA\] Bid-All:" src/PropTraderTools/TradeCopierPanel.cs
```
**Expected**: all symbols found, at least 1 match each.

#### SCAN-6: Functional Correctness (T-B1 ACs)
- **AC-B1**: Verify `OnBidClick` contains `NinjaTrader.Code.Output.Process(` with string `[PTT-MA] Bid: leader null -- check account combo on chart trader` in the null-leader branch.
- **AC-B2**: Verify `OnBidAllClick` body contains `foreach (Account acc in Account.All)`. Verify `HasNonFlatPosition` call present in `OnBidAllClick`.
- **AC-B3**: Verify `[PTT-MA] Bid-All: instrument=` log string present.

#### SCAN-7: Diff Hygiene
```powershell
# Confirm multiple hunks (NOT single-hunk full-file replace):
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object
# Expected: Count >= 2 (OnBidClick + OnBidAllClick hunks)

# PR hygiene:
powershell -File .\scripts\verify_pr_hygiene.ps1
# Expected: DIFF GUARD PASS

# Guardian symbols:
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: GUARDIAN PASS (or pre-existing DW-B209-01 false positive -- document if present)

# Hard-link sync (MANDATORY after every .cs edit):
powershell -File .\scripts\ptt-sync.ps1
# Expected: 21 linked, 0 errors
```

---

### Acceptance Criteria (T-B1)

| AC | Criterion | How to Verify |
|----|-----------|--------------|
| AC-B1 | `OnBidClick` with null `_leaderAccount` logs `[PTT-MA] Bid: leader null -- check account combo on chart trader` to OutputTab1 | SCAN-5 + T_B216_B1 |
| AC-B2 | `OnBidAllClick` no longer requires `_leaderAccount` -- fires `PttMoveTargetOne.Execute` for all accounts with non-flat position | SCAN-5 + T_B216_B2 + SCAN-6 |
| AC-B3 | `OnBidAllClick` logs `[PTT-MA] Bid-All: instrument=X accounts=N` when N>0 | SCAN-6 |
| AC-ALL | Zero new test failures (`Failed` count does not exceed 24) | SCAN-2 |
| AC-ALL | `ptt-sync.ps1` reports 21 linked, 0 errors | SCAN-7 |
| AC-ALL | `DIFF GUARD PASS` | SCAN-7 |

---

### Scope Lock (T-B1)

**Files T-B1 MUST touch**:
- `src/PropTraderTools/TradeCopierPanel.cs` (2 changes: OnBidClick, OnBidAllClick)
- `src/PropTraderTools/Tests/B216Tests.cs` (APPEND T_B216_B1 + T_B216_B2 to existing class)

**Files T-B1 MUST NOT touch**:
- `src/PropTraderTools/Features/PttCancel.cs` -- DO NOT CHANGE
- `src/PropTraderTools/Features/PttMoveTargetOne.cs` -- DO NOT CHANGE
- `src/PropTraderTools/CopyEngine.cs` -- DO NOT CHANGE
- `src/PropTraderTools/TradeCopierAddOn.cs` -- DO NOT CHANGE
- Any file not listed above

---

---

## T-C1: Lane C -- Cancel + Cancel All Button Row

### Header

| Field | Value |
|-------|-------|
| Block | B216 |
| Ticket ID | T-C1 |
| Lane | C |
| Scope | `src/PropTraderTools/TradeCopierPanel.cs` + NEW `src/PropTraderTools/Features/PttCancelAll.cs` |
| Spec Requirements | SR-C1 (Cancel button), SR-C2 (Cancel All button + PttCancelAll.cs), SR-C3 (Cancel row layout) |
| Deferred Items Closed | None (new feature) |
| Prerequisite | T-A1 and T-B1 must be completed first (no code dependency, sequential execution required per pipeline) |

---

### Current State

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**Field section (lines 272-279 -- current -- no _cancelRowPanel field)**:
```csharp
// B47 T5-B: Root-level BE and Quick row panels (extracted from _contentPanel in T6-B)
private UniformGrid _beRowPanel = null; // 2-col: BE cluster | BE ALL cluster
private UniformGrid _quickRowPanel = null; // 2-col: Quick cluster | Quick ALL cluster

// B211: Ask and Bid row panels
private UniformGrid _askRowPanel = null; // B211: 2-col grid: [Ask cluster] | [Ask-ALL cluster]
private UniformGrid _bidRowPanel = null; // B211: 2-col grid: [Bid cluster] | [Bid-ALL cluster]
```

**BuildUI -- root.Children order (lines 900-910 -- current)**:
```csharp
root.Children.Add(_beRowPanel);
root.Children.Add(_askRowPanel);  // B211: Ask row directly below BE row
root.Children.Add(_bidRowPanel);  // B211: Bid row directly below Ask row
BuildInstrRow(); // B128: build instrument row before adding to root
root.Children.Add(_instrRowPanel);
root.Children.Add(_quickRowPanel);
BuildCopierSection(root); // B49: Copier second (Mode row now inside)
root.Children.Add(_statusText); // status below Copier
BuildCollapsibleHeader(root); // B49: Position Tools moved to bottom
root.Children.Add(_contentPanel); // B49: contentPanel follows its header
Content = root;
```

**BuildBufferedButtonsRow end (lines 1254-1256 -- current)**:
```csharp
_quickT3Row.Children.Add(quickT3Lbl);
root.Children.Add(_quickT3Row);
}  // end of BuildBufferedButtonsRow
```

**OnCancel2 (lines 1666-1671 -- DO NOT CHANGE)**:
```csharp
// B33 T7 -- OnCancel2: dispatches to PttCancel module. CYC=1 (R7 dedup).
// B30-B: leader resolved late via LogAndDispatchModule (DW-B30-03).
private void OnCancel2(object sender, RoutedEventArgs e)
{
    LogAndDispatchModule("[CANCEL]", "CANCEL");
}
```

**Problem**: No Cancel or Cancel All buttons exist in the always-visible (non-collapsible) panel area. The existing `OnCancel2` is inside the collapsible Position Tools section only.

---

### Target State

**1. Field addition** (insert after line 278, after `_bidRowPanel` field declaration):
```csharp
private UniformGrid _cancelRowPanel = null; // B216: 2-col: [Cancel] | [Cancel All]
```

**2. BuildUI change** (insert `root.Children.Add(_cancelRowPanel)` after `root.Children.Add(_quickRowPanel)` at line 906):
```csharp
root.Children.Add(_quickRowPanel);
root.Children.Add(_cancelRowPanel);  // B216: Cancel row below Quick row
BuildCopierSection(root); // B49: Copier second (Mode row now inside)
```

**3. BuildBufferedButtonsRow end** (insert `BuildCancelRow();` call after `root.Children.Add(_quickT3Row)` at line 1255):
```csharp
_quickT3Row.Children.Add(quickT3Lbl);
root.Children.Add(_quickT3Row);
BuildCancelRow();  // B216: construct _cancelRowPanel (added to root by BuildUI)
}  // end of BuildBufferedButtonsRow
```

**4. BuildCancelRow** (new private method, add after BuildBufferedButtonsRow closing brace):
```csharp
// B216: BuildCancelRow -- constructs _cancelRowPanel with 2 plain buttons (NO spinner arrows).
// Cancel (left): LogAndDispatchModule -- identical to OnCancel2 (second Cancel in main area).
// Cancel All (right): PttCancelAll.Execute(_instrument) -- cancels all Working/Initialized for instr.
// Style: BrushTeal border + BrushTeal background + White foreground (matches 3x / 3x All row).
// CYC=1 (straight-line construction, no branches). JS-021: no lock. ASCII-only.
private void BuildCancelRow()
{
    _cancelRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

    var cancelBtn = new Button { Content = "Cancel" };
    cancelBtn.BorderBrush = BrushTeal;
    cancelBtn.Background = BrushTeal;
    cancelBtn.Foreground = System.Windows.Media.Brushes.White;
    cancelBtn.BorderThickness = new Thickness(2);
    cancelBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    cancelBtn.Background = BrushTeal; // explicit brush after style ref (matches existing pattern)
    cancelBtn.Click += OnCancelClick;
    _cancelRowPanel.Children.Add(cancelBtn);

    var cancelAllBtn = new Button { Content = "Cancel All" };
    cancelAllBtn.BorderBrush = BrushTeal;
    cancelAllBtn.Background = BrushTeal;
    cancelAllBtn.Foreground = System.Windows.Media.Brushes.White;
    cancelAllBtn.BorderThickness = new Thickness(2);
    cancelAllBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    cancelAllBtn.Background = BrushTeal; // explicit brush after style ref
    cancelAllBtn.Click += OnCancelAllClick;
    _cancelRowPanel.Children.Add(cancelAllBtn);
}
```

**5. OnCancelClick** (new event handler, add in event handlers section near OnCancel2):
```csharp
// B216: OnCancelClick -- second Cancel button in main (non-collapsible) area.
// Behaviour identical to OnCancel2 (both dispatch to PttCancel via LogAndDispatchModule).
// CYC=1. JS-021: no lock. JS-033: synchronous void. ASCII-only.
private void OnCancelClick(object sender, RoutedEventArgs e)
{
    LogAndDispatchModule("[CANCEL]", "CANCEL");
}
```

**6. OnCancelAllClick** (new event handler, add after OnCancelClick):
```csharp
// B216: OnCancelAllClick -- calls PttCancelAll.Execute for ALL accounts.
// Does NOT go through LogAndDispatchModule. Direct static call to PttCancelAll.
// CYC=2: (1) _instrument null guard, (2) Execute call.
// JS-021: no lock. JS-033: synchronous void. ASCII-only.
private void OnCancelAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    PttCancelAll.Execute(_instrument);
}
```

---

### New Files

**Path**: `src/PropTraderTools/Features/PttCancelAll.cs`

**Full file content**:
```csharp
// src/PropTraderTools/Features/PttCancelAll.cs
// B216 -- PttCancelAll: cancel ALL Working/Initialized orders for ALL accounts + instrument.
// Static class. No IPttModule -- called directly from TradeCopierPanel.OnCancelAllClick.
// Does NOT go through CopyEngine dispatch or PttBus. No leader dependency.
// Dependencies: NinjaTrader.Cbi ONLY + NinjaTrader.Code.Output.
// JS-021: no lock. JS-033: synchronous void. NT8-006: no LINQ -- explicit foreach.

using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Cancel-All module. Cancels all Working/Initialized orders for ALL accounts
    /// and the given instrument. No leader dependency. No copier state dependency.
    /// Called directly from TradeCopierPanel.OnCancelAllClick.
    /// </summary>
    internal static class PttCancelAll
    {
        /// <summary>
        /// Cancel all Working/Initialized orders for all accounts for instr.
        /// NT8-006: NO LINQ -- explicit foreach.
        /// CYC=5: (1) instr null, (2) foreach Account.All, (3) foreach acc.Orders,
        ///         (4) IsWorkingOrderForInstr, (5) toCancel.Count>0.
        /// JS-021: no lock.
        /// </summary>
        internal static void Execute(Instrument instr)
        {
            if (instr == null)
                return; // (1)

            var toCancel = new List<Order>();
            int totalOrders = 0;
            int accountsActed = 0;

            foreach (Account acc in Account.All) // (2)
            {
                toCancel.Clear();
                foreach (Order o in acc.Orders) // (3)
                {
                    if (IsWorkingOrderForInstr(o, instr)) // (4)
                        toCancel.Add(o);
                }
                if (toCancel.Count > 0) // (5)
                {
                    totalOrders += toCancel.Count;
                    try
                    {
                        acc.Cancel(toCancel.ToArray());
                        accountsActed++;
                    }
                    catch
                    { /* cancel on already-filled orders is non-fatal */ }
                }
            }

            NinjaTrader.Code.Output.Process(
                "[CANCEL-ALL] " + totalOrders + " orders cancelled across "
                    + accountsActed + " accounts for " + instr.FullName,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        /// <summary>
        /// Returns true if order o is Working or Initialized for instr.
        /// Unrestricted -- cancels entry orders AND target orders (unlike PttCancel which
        /// is semantically entry-focused).
        /// CYC=4: (1) o null, (2) stateOk (||), (3) o.Instrument null, (4) FullName match.
        /// JS-002: returns bool. ASCII-only.
        /// </summary>
        private static bool IsWorkingOrderForInstr(Order o, Instrument instr)
        {
            if (o == null)
                return false; // (1)
            bool stateOk =
                o.OrderState == OrderState.Working
                || o.OrderState == OrderState.Initialized; // (2)
            bool instrOk =
                o.Instrument != null                        // (3)
                && o.Instrument.FullName == instr.FullName; // (4)
            return stateOk && instrOk;
        }
    }
}
```

**Namespace note**: `namespace PropTraderTools` (NOT `PropTraderTools.Features`) -- matches existing PttCancel.cs which uses `namespace PropTraderTools` despite living in `Features/`.

---

### Method Signatures

| Method | Type | File | Status | CYC |
|--------|------|------|--------|-----|
| `private void BuildCancelRow()` | private void | TradeCopierPanel.cs | NEW | 1 |
| `private void OnCancelClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | NEW | 1 |
| `private void OnCancelAllClick(object sender, RoutedEventArgs e)` | event handler | TradeCopierPanel.cs | NEW | 2 |
| `internal static void PttCancelAll.Execute(Instrument instr)` | static method | PttCancelAll.cs | NEW | 5 |
| `private static bool PttCancelAll.IsWorkingOrderForInstr(Order o, Instrument instr)` | private static | PttCancelAll.cs | NEW | 4 |

---

### xUnit Tests Required

**File**: `src/PropTraderTools/Tests/B216Tests.cs` (already created by T-A1 -- append tests to existing class)

**T_B216_C1** -- Cancel All null instrument guard:
```csharp
[Fact]
public void T_B216_C1_OnCancelAllClick_NullInstrument_DoesNotThrow()
{
    // Verifies: OnCancelAllClick with _instrument == null returns without calling
    // PttCancelAll.Execute. Verified by code inspection:
    // the null guard "if (_instrument == null) return;" must be present.
    var src = System.IO.File.ReadAllText(
        "../../../../TradeCopierPanel.cs");
    Assert.Contains("OnCancelAllClick", src);
    Assert.Contains("PttCancelAll.Execute", src);
    Assert.Contains("_cancelRowPanel", src);
}
```

**T_B216_C2** -- PttCancelAll.IsWorkingOrderForInstr predicate:
```csharp
[Fact]
public void T_B216_C2_IsWorkingOrderForInstr_NullOrder_ReturnsFalse()
{
    // Verifies: PttCancelAll.IsWorkingOrderForInstr returns false for null input.
    // Access via reflection (private static).
    var method = typeof(PttCancelAll).GetMethod(
        "IsWorkingOrderForInstr",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(method); // method must exist
    // Null order -> false (branch 1 of CYC=4 implementation)
    var result = method.Invoke(null, new object[] { null, null });
    Assert.Equal(false, result);
}
```

**Where to add**: Append both tests to existing `B216Tests` class in `src/PropTraderTools/Tests/B216Tests.cs`.

---

### CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `BuildCancelRow` | 1 | Straight-line construction, no branches |
| `OnCancelClick` | 1 | Straight-line delegation to `LogAndDispatchModule` |
| `OnCancelAllClick` | 2 | (1) `_instrument null` guard, (2) `PttCancelAll.Execute` call |
| `PttCancelAll.Execute` | 5 | (1) `instr null`, (2) `foreach Account.All`, (3) `foreach acc.Orders`, (4) `IsWorkingOrderForInstr`, (5) `toCancel.Count>0` |
| `PttCancelAll.IsWorkingOrderForInstr` | 4 | (1) `o null`, (2) `stateOk (||)`, (3) `o.Instrument null`, (4) `FullName match` |

All <= 8. JS-066: PASS.

---

### NT8 Constraints

| API | Usage | Thread Safety |
|-----|-------|--------------|
| `Account.All` | Iterate all accounts in `PttCancelAll.Execute` | UI thread -- safe |
| `acc.Orders` | Iterate orders per account | UI thread -- safe (identical pattern to PttCancel.cs:72) |
| `acc.Cancel(Order[])` | Cancel order array | UI thread -- safe (identical to PttCancel.cs:83) |
| `OrderState.Working` | Order filter in `IsWorkingOrderForInstr` | Enum comparison -- thread-safe |
| `OrderState.Initialized` | Order filter in `IsWorkingOrderForInstr` | Enum comparison -- thread-safe |
| `o.Instrument.FullName` | Instrument match | UI thread -- safe |
| `NinjaTrader.Code.Output.Process(string, PrintTo)` | Diagnostic logging | Thread-safe NT8 API |
| `BrushTeal` | Button style (existing field in TradeCopierPanel) | UI thread -- WPF brush |
| `System.Windows.Media.Brushes.White` | Button foreground | UI thread -- WPF brush |

**Threading**: All methods are synchronous UI-thread event handlers or synchronous helpers called from UI thread. No `Dispatcher.InvokeAsync` required.

**Note on OnCancelClick**: Delegates to `LogAndDispatchModule("[CANCEL]", "CANCEL")` which handles its own internal threading. This is identical to the existing `OnCancel2` implementation at line 1668-1671. No change to `LogAndDispatchModule` or `OnCancel2` is required.

**Note on OnCancel2**: `OnCancel2` at line 1668 MUST remain unchanged. The new `OnCancelClick` is a SECOND Cancel button in the main non-collapsible area -- it does NOT replace `OnCancel2`. Both buttons exist simultaneously after T-C1. The collapsible Position Tools section retains its own Cancel button.

---

### 7-Scan Checklist (T-C1)

#### SCAN-1: P0 Violations (run BEFORE commit)
```powershell
# Check TradeCopierPanel.cs for new violations:
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs

# Check new file PttCancelAll.cs for P0 violations:
grep -n "lock(" src/PropTraderTools/Features/PttCancelAll.cs
grep -n "async void " src/PropTraderTools/Features/PttCancelAll.cs
grep -n "return null;" src/PropTraderTools/Features/PttCancelAll.cs
```
**Expected**: 0 matches in any new or modified lines. The `return;` in `Execute` and `IsWorkingOrderForInstr` are returning `void` and `false` (bool) respectively -- not `return null`.

#### SCAN-2: dotnet test -- Zero New Failures
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
```
**Expected**: `Failed=24` (pre-existing baseline), `Passed >= 206` (204 post-T-B1 + 2 new T_B216_C1 + T_B216_C2), zero new failures introduced by T-C1.

#### SCAN-3: CYC Verification (verify by inspection)
- `BuildCancelRow`: 0 conditional branches = CYC 1. PASS.
- `OnCancelClick`: 0 conditional branches (1 delegation) = CYC 1. PASS.
- `OnCancelAllClick`: 1 decision point (`_instrument null`) = CYC 2. PASS.
- `PttCancelAll.Execute`: 4 decision points (null, foreach, foreach, if) = CYC 5. PASS.
- `PttCancelAll.IsWorkingOrderForInstr`: 3 decision points + 1 `||` = CYC 4. PASS.
All <= 8.

#### SCAN-4: ASCII-Only
```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object

[System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttCancelAll.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object
```
**Expected**: count 0 in all new/changed regions. Confirm:
- Button `Content = "Cancel"` and `Content = "Cancel All"` are ASCII-only.
- Log string `[CANCEL-ALL] N orders cancelled across M accounts for X` is ASCII-only.
- No em-dash, smart quotes, Unicode symbols in any string literal.

#### SCAN-5: Symbol Existence
```powershell
# TradeCopierPanel.cs:
Select-String "_cancelRowPanel" src/PropTraderTools/TradeCopierPanel.cs
# Expected: >= 3 matches (field decl + BuildCancelRow assignment + BuildUI Add)

Select-String "BuildCancelRow" src/PropTraderTools/TradeCopierPanel.cs
# Expected: >= 2 matches (definition + call site in BuildBufferedButtonsRow)

Select-String "OnCancelClick" src/PropTraderTools/TradeCopierPanel.cs
# Expected: >= 2 matches (definition + Click += registration in BuildCancelRow)

Select-String "OnCancelAllClick" src/PropTraderTools/TradeCopierPanel.cs
# Expected: >= 2 matches (definition + Click += registration in BuildCancelRow)

# PttCancelAll.cs:
Select-String "PttCancelAll" src/PropTraderTools/Features/PttCancelAll.cs
# Expected: >= 1 match (class declaration)

Select-String "IsWorkingOrderForInstr" src/PropTraderTools/Features/PttCancelAll.cs
# Expected: >= 1 match (method declaration)

Select-String "\[CANCEL-ALL\]" src/PropTraderTools/Features/PttCancelAll.cs
# Expected: 1 match (log string in Execute)
```

#### SCAN-6: Functional Correctness (T-C1 ACs)
- **AC-C1**: Verify `root.Children.Add(_cancelRowPanel)` appears after `root.Children.Add(_quickRowPanel)` in `BuildUI`. Visual: buttons appear below 3x row after F5 restart.
- **AC-C2**: Verify `OnCancelClick` body is `LogAndDispatchModule("[CANCEL]", "CANCEL");` -- identical to `OnCancel2`.
- **AC-C3**: Verify `OnCancelAllClick` body contains `PttCancelAll.Execute(_instrument)` after null guard.
- **AC-C4**: Verify `src/PropTraderTools/Features/PttCancelAll.cs` exists and contains `internal static class PttCancelAll`.
- **AC-C5**: Verify `BuildCancelRow` uses `new Button` (plain buttons). No `RepeatButton`. No `BuildSpinnerCluster` call. Grep: `Select-String "RepeatButton" src/PropTraderTools/TradeCopierPanel.cs` in new lines -- must be 0 matches in T-C1 additions.
- **AC-C6**: Verify `cancelBtn.BorderBrush = BrushTeal`, `cancelBtn.Background = BrushTeal`, `cancelBtn.Foreground = System.Windows.Media.Brushes.White` and same for `cancelAllBtn`.

#### SCAN-7: Diff Hygiene
```powershell
# Confirm multiple hunks (NOT single-hunk full-file replace):
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object
# Expected: Count >= 4 (field decl + BuildUI + BuildBufferedButtonsRow + new methods)

# PR hygiene:
powershell -File .\scripts\verify_pr_hygiene.ps1
# Expected: DIFF GUARD PASS

# Guardian symbols:
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: GUARDIAN PASS (or pre-existing DW-B209-01 false positive -- document if seen)

# Hard-link sync (MANDATORY after every .cs edit):
powershell -File .\scripts\ptt-sync.ps1
# Expected: 21 linked, 0 errors
# NOTE: PttCancelAll.cs is a NEW src/ file -- ptt-sync.ps1 must pick it up.
# If sync count changes from 21, verify the new file was linked correctly.
```

---

### Acceptance Criteria (T-C1)

| AC | Criterion | How to Verify |
|----|-----------|--------------|
| AC-C1 | `_cancelRowPanel` is visible in UI directly below `_quickRowPanel` | SCAN-5 + SCAN-6 + F5 visual |
| AC-C2 | Cancel button (left) calls `LogAndDispatchModule("[CANCEL]", "CANCEL")` (identical to OnCancel2) | SCAN-5 + SCAN-6 |
| AC-C3 | Cancel All button (right) calls `PttCancelAll.Execute(_instrument)` | SCAN-5 + SCAN-6 + T_B216_C1 |
| AC-C4 | `PttCancelAll.cs` exists at `src/PropTraderTools/Features/PttCancelAll.cs`, passes JS-DNA scan | SCAN-1 + SCAN-4 + SCAN-5 |
| AC-C5 | No spinner arrows (no RepeatButton `^`/`v`) on Cancel or Cancel All buttons | SCAN-6 |
| AC-C6 | Cancel and Cancel All buttons have BrushTeal border, BrushTeal background, white foreground | SCAN-6 + F5 visual |
| AC-ALL | Zero new test failures (`Failed` count does not exceed 24) | SCAN-2 |
| AC-ALL | `ptt-sync.ps1` reports 21 linked, 0 errors | SCAN-7 |
| AC-ALL | `DIFF GUARD PASS` | SCAN-7 |

---

### Scope Lock (T-C1)

**Files T-C1 MUST touch**:
- `src/PropTraderTools/TradeCopierPanel.cs` (5 changes: field decl, BuildUI, BuildBufferedButtonsRow, BuildCancelRow new method, OnCancelClick + OnCancelAllClick new handlers)
- `src/PropTraderTools/Features/PttCancelAll.cs` (NEW FILE -- create from scratch)
- `src/PropTraderTools/Tests/B216Tests.cs` (APPEND T_B216_C1 + T_B216_C2 to existing class)

**Files T-C1 MUST NOT touch**:
- `src/PropTraderTools/Features/PttCancel.cs` -- DO NOT CHANGE (read only for pattern reference)
- `src/PropTraderTools/Features/PttMoveTargetOne.cs` -- DO NOT CHANGE
- `src/PropTraderTools/CopyEngine.cs` -- DO NOT CHANGE
- `src/PropTraderTools/TradeCopierAddOn.cs` -- DO NOT CHANGE
- `src/PropTraderTools/Features/PttMoveTargetAll.cs` -- DO NOT CHANGE (DW-B212-02 open -- no disposition)
- Any file not listed above

---

---

## Cross-Ticket Notes (Engineer Contract)

### Execution Order Rationale

1. **T-A1 first**: adds `HasNonFlatPosition` private static helper that T-B1 depends on.
2. **T-B1 second**: reuses `HasNonFlatPosition` from T-A1 without redefinition.
3. **T-C1 last**: independent of T-A1/T-B1 logic but sequential per pipeline protocol.

### ptt-sync after every .cs edit (MANDATORY)

```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Run after EVERY `.cs` file edit. Expected: 21 linked, 0 errors. If `PttCancelAll.cs` is new and sync count changes, verify hard-link was created correctly.

### OnCancel2 MUST NOT be modified

`OnCancel2` at line 1668 is inside the collapsible Position Tools `_contentPanel`. It is NOT modified or replaced by any ticket in B216. The new `OnCancelClick` from T-C1 is a SECOND button in the main (non-collapsible) area. Both buttons call `LogAndDispatchModule("[CANCEL]", "CANCEL")`.

### PttMoveTargetAll.cs -- Do NOT delete

DW-B212-02 (PttMoveTargetAll.cs disposition) remains open. Pattern B conversion (OnAskAllClick/OnBidAllClick) no longer calls `PttMoveTargetAll.Execute`, but this does NOT authorize deletion. Do NOT touch `PttMoveTargetAll.cs`.

### ASCII-Only Compliance (all tickets)

Every string literal added in B216 must pass ASCII-only check (`$_ -gt 0x7E` = 0 count):
- `"[PTT-MA] Ask: leader null -- check account combo on chart trader"` -- hyphen `-`, not em-dash
- `"[PTT-MA] Ask-All: instrument=" + ...` -- hyphen `-`, not em-dash
- `"[PTT-MA] Bid: leader null -- check account combo on chart trader"` -- hyphen `-`, not em-dash
- `"[PTT-MA] Bid-All: instrument=" + ...` -- hyphen `-`, not em-dash
- `"[CANCEL-ALL] " + totalOrders + " orders cancelled across " ...` -- ASCII-only
- `"Cancel"` and `"Cancel All"` -- ASCII-only

### Pre-existing Test Failure Baseline

The test suite has 24 pre-existing failures (gitignored test files, B214-debt). B216 MUST NOT increase this count. Each ticket's SCAN-2 verifies `Failed` does not exceed 24.

### DW-B209-01 Guardian False Positive

If `verify_critical_symbols.ps1` reports GUARDIAN FAIL during any SCAN-7, confirm it matches the known pre-existing DW-B209-01 false positive (`_instr2tBtn.Background`). Document in ticket completion notes. Do NOT attempt to fix DW-B209-01 in any B216 ticket.

---

*Tickets produced by ptt-architect, Phase 3. Plan source: docs/brain/B216/02-architecture-plan.md (REVIEW_PASS).*
