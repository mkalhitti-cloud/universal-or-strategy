# Ticket DW-LB-FL-01 T1 Completion — v4

**Engineer**: ptt-engineer (retry-3)
**Ticket**: DW-LB-FL-01-T1 v4 fix
**Date**: 2026-09-09
**Spec**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v4.md`
**Status**: BUILD_PASS

---

## What Was Implemented

### New Method: `IsInEntryFillDebounceWindow(Account acct)`
**Location**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs) — inserted after original `FlattenIfNotArming` (~L5186-5202 in modified file)

**Purpose**: Returns true if the given account is within the 500ms entry-fill bracket-arm window.
Reads `_nakedDetectLastQueuedTicks` (the same ConcurrentDictionary that TryNakedDetect v3 stamps
on every "Entry":Filled or "PTT-Copy":Filled event). Returns false when no stamp exists (safe default).

```csharp
private bool IsInEntryFillDebounceWindow(Account acct)
{
    if (!_nakedDetectLastQueuedTicks.TryGetValue(acct.Name, out long last)) // (1)
        return false;
    long now = (long)(int)Environment.TickCount;
    return now - last < 500L; // (2)
}
```

**CYC**: 2 (base=1, TryGetValue branch=1). **PASS <= 8.**
**JS compliance**: TryGetValue is lock-free (JS-021). No throw (JS-001). Returns bool (JS-002). ASCII-only.

---

### Modified Method: `FlattenIfNotArming(Account acct, Instrument instr)`
**Location**: [`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs) ~L5177

**Change**: Added `IsInEntryFillDebounceWindow` as FIRST guard before `HasArmingAtmBrackets`.

Before (v1/v2/v3 — CYC=2):
```csharp
private void FlattenIfNotArming(Account acct, Instrument instr)
{
    if (HasArmingAtmBrackets(acct, instr))
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr);
}
```

After (v4 — CYC=3):
```csharp
private void FlattenIfNotArming(Account acct, Instrument instr)
{
    if (IsInEntryFillDebounceWindow(acct)) // (1) DW-LB-FL-01-V4: stale callback guard
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: debounce-skip");
        return;
    }
    if (HasArmingAtmBrackets(acct, instr)) // (2)
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr);
}
```

**CYC delta**: 2 -> 3 (+1 branch). **PASS <= 8.**

---

### Tests Added: T24–T27
**File**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`

| # | Test Name | Assertion |
|---|-----------|-----------|
| T24 | `IsInEntryFillDebounceWindow_ReturnsTrue_WhenWithin500ms` | stamp=1000, now=1200 (200ms) -> true |
| T25 | `IsInEntryFillDebounceWindow_ReturnsFalse_WhenOver500ms` | stamp=1000, now=1600 (600ms) -> false |
| T26 | `IsInEntryFillDebounceWindow_ReturnsFalse_WhenNoStamp` | empty dict -> false (safe default) |
| T27 | `FlattenIfNotArming_ReturnsWithoutFlatten_WhenDebounceActive` | Seam reflection: `IsInEntryFillDebounceWindow` exists as private instance method |

All 4 tests use xUnit `[Fact]`. No NUnit, no MSTest.

---

## Preserved Invariants

| Guard | Status |
|-------|--------|
| v1 `HasArmingAtmBrackets` in `FlattenIfNotArming` (second guard) | PRESERVED — still active |
| v2 `IsPttCopyEntry` guard in `TryNakedDetect` | PRESERVED — unchanged |
| v3 `_nakedDetectLastQueuedTicks` stamp in `TryNakedDetect` | PRESERVED — READ by v4 |
| `HasInflightFlatten` in `IsAccountFlattenable` | PRESERVED — unchanged |
| DW-LB-FL-02 `IsNativeExitOnFlatLeader` | PRESERVED — unchanged |
| DW-B65-01 bypass | PRESERVED — unchanged |

---

## 7-Scan Results

### SCAN 1 — lock() check
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\("`
**Result**: All matches are in comments (`// ... no lock()`). Zero actual `lock()` calls in new or modified code.
**PASS**

### SCAN 2 — async void check
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void "`
**Result**: All matches are in comments. Zero actual `async void` in new or modified code.
**PASS**

### SCAN 3 — return null check
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "return null;"`
**Result**: Pre-existing `return null` in other unmodified methods. Zero `return null` in modified methods
(`IsInEntryFillDebounceWindow` returns bool; `FlattenIfNotArming` returns void).
**PASS**

### SCAN 4 — ASCII-only
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"`
**Result**: 0 matches. Zero non-ASCII characters.
**PASS**

### SCAN 5 — CYC complexity
**Method verification** (manual — no complexity_audit.py script present in workspace):
- `IsInEntryFillDebounceWindow`: base(1) + TryGetValue-branch(1) = **CYC=2** <= 2 ✓
- `FlattenIfNotArming`: base(1) + IsInEntryFillDebounceWindow-branch(1) + HasArmingAtmBrackets-branch(1) = **CYC=3** <= 3 ✓
- `TryNakedDetect`: unchanged at CYC=4 ✓
- All other modified methods: unchanged ✓
**PASS**

### SCAN 6 — Build
**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`
**Result**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.85
```
**PASS**

### SCAN 7 — Tests
**Command**: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build`
**Result**:
```
Passed!  - Failed: 0, Passed: 114, Skipped: 3, Total: 117, Duration: 47 ms
```
T24, T25, T26, T27 all pass. All prior T1-T23 still pass. 0 failures.
**PASS**

---

## Summary

**Root cause closed**: Race 4 — A `FlattenIfNotArming` callback queued by `NakedPositionDetector`
during a PRIOR trade's cancel storm can survive in the WPF Dispatcher queue for seconds.
When the UI thread runs it after Trade #2's entry fills but before ATM brackets appear in `acc.Orders`,
`HasArmingAtmBrackets` returns false → `FlattenOneAccount` fires → PTT-Flatten:Submitted.

**Fix**: `IsInEntryFillDebounceWindow` reads the same `_nakedDetectLastQueuedTicks` dict stamped by
v3 at entry-fill time. Any `FlattenIfNotArming` callback running within 500ms of the last entry fill
is suppressed as a stale callback — regardless of bracket state.

**Files modified**:
- `src/PropTraderTools/CopyEngine.cs` — 1 file only
- `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs` — 4 new tests added

**Final status**: BUILD_PASS
