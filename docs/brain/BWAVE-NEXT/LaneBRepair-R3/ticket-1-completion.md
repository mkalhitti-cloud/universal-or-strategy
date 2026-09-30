# Ticket 1 Completion: BWAVE-NEXT LaneBRepair-R3
**Status**: BUILD_PASS
**Engineer**: ptt-engineer
**Date**: 2026-08-22
**Branch**: bwave-next-lane-b

---

## 1. Scope Lock Confirmation

SCOPE LOCK - TICKET 1 ONLY.
Only R3-F1, R3-F2, and R3-V1 addressed in this session. No other tickets touched.

---

## 2. Verify-First Results

### R3-F1 — BwaveNextLaneBTests.cs line 172
**Verified**: `FindFollowerEntryOrder` in `CopyEngine.cs` is declared `private static` (line 3703).
Test at line 172 confirmed to use `Priv` (`BindingFlags.NonPublic | BindingFlags.Instance`) — Instance flag does not match a static method.
**Status**: STATIC CONFIRMED. Fix applied.

### R3-F2 — CopyEngine.cs lines 6627-6651
**Verified**: `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _)` at lines 6641-6642 appeared BEFORE `SubmitEntryDirect` at line 6644.
Cleanup precedes submit — buggy order confirmed.
**Status**: CONFIRMED buggy. Fix applied.

### R3-V1 — Order.Name null guard
**Status**: DISMISSED. No source change applied.

---

## 3. Changes Made

### R3-F1: BwaveNextLaneBTests.cs line 172

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`
**Line**: 172

**Before**:
```csharp
var method = EngineType.GetMethod("FindFollowerEntryOrder", Priv);
```

**After**:
```csharp
var method = EngineType.GetMethod(
    "FindFollowerEntryOrder",
    BindingFlags.NonPublic | BindingFlags.Static);
```

- `Priv` constant at line 15 (`BindingFlags.NonPublic | BindingFlags.Instance`) is UNCHANGED.
- No other test methods modified.

### R3-F2: CopyEngine.cs SubmitDrainedEntry

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 6627-6655 (header + method body)

**Before** (WRONG — cleanup before submit):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) F3 cleanup foreach, (4) delegated to SubmitEntryDirect.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    // F3-repair: clear drain-owned IDs now that drain is complete.
    foreach (var id in payload.DrainedOrderIds) // (3)
        _drainOwnedOrderIds.TryRemove(id, out _);

    SubmitEntryDirect( // (4) delegated
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);
}
```

**After** (CORRECT — submit first, then cleanup):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
// R3-F2: cleanup moved after SubmitEntryDirect -- drain IDs preserved until submit completes.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);

    // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

---

## 4. R3-V1 DISMISSED Statement

R3-V1 DISMISSED: NT8 docs confirm Order.Name non-null for live orders. StartsWith is safe. No fix applied.

---

## 5. Scan Results

### SCAN 1 — lock()
**Command**: `Get-ChildItem -Path "src\PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch '^\s*//' }`
**Output**: (no output)
**Result**: PASS — 0 lock() usages found.

### SCAN 2 — async void
**Command**: `Get-ChildItem -Path "src\PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "async void " | Where-Object { $_.Line -notmatch '^\s*//' }`
**Output**: (no output)
**Result**: PASS — 0 async void usages found.

### SCAN 3 — return null
**Command**: `Get-ChildItem -Path "src\PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "return null;" | Where-Object { $_.Line -notmatch '^\s*//' }`
**Output**: Pre-existing return null occurrences found across CopyEngine.cs, LicenseClient.cs, TradeCopierAddOn.cs, TradeCopierPanel.cs (lines 1154, 1857, 2781, 2862, 2870, 3552, 3721, 5175, 5181, 5260, 6326, 6341, etc.)
**Result**: PASS — No new return null in modified methods (`SubmitDrainedEntry` or `FindFollowerEntryOrder` area). Line 3721 is `FindFollowerEntryOrder`'s pre-existing return null, not introduced by this ticket.

### SCAN 4 — CYC (complexity_audit.py)
**Note**: `scripts/complexity_audit.py` does not exist in the repository. Manual CYC analysis performed.
**SubmitDrainedEntry CYC** = 4:
  1. `!_pendingDispatchDrains.TryRemove(...)` early return
  2. `follower == null` early return
  3. `SubmitEntryDirect(...)` delegated call (counted as 1 per project convention)
  4. `foreach` loop body
No new branches added. No new decision points. CYC unchanged at 4.
**Result**: PASS — SubmitDrainedEntry CYC = 4 (confirmed by manual analysis, <= 8 budget).

### SCAN 5 — dotnet build
**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`
**Output**:
```
Build succeeded.
B131Tests.cs(165,13): warning xUnit2004: Do not use Assert.Equal() to check for boolean conditions. [pre-existing]
    1 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.56
```
**Result**: PASS — 0 errors. 1 pre-existing warning in B131Tests.cs (not in modified files). No new warnings.

### SCAN 6 — dotnet test
**Command**: `dotnet test --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog|ActiveOrders|NakedDetector|AbortDrainOnFill|FindFollowerEntryOrder"`
**Output**:
```
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 2 s - PropTraderTools.dll (net48)
```
**Result**: PASS — All 11 matching tests pass. `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` passes (Assert.NotNull no longer fails).

### SCAN 7 — ptt-sync-and-verify.ps1
**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1`
**Output**:
```
COPIED:  CopyEngine.cs
  Copied:   1  |  In-sync: 17  |  Excluded: 70

=== PTT VERIFY: MD5 check every synced file ===
  OK       AtrSizingEngine.cs
  OK       CopyEngine.cs
  OK       FeatureFlags.cs
  OK       LicenseClient.cs
  OK       TradeCopierAddOn.cs
  OK       TradeCopierPanel.cs
  OK       TradeCopierWindow.cs
  OK       Core\PttContracts.cs
  OK       Features\PttBreakEven.cs
  OK       Features\PttBreakEvenSwap.cs
  OK       Features\PttCancel.cs
  OK       Features\PttCopier.cs
  OK       Features\PttFlatten.cs
  OK       Features\PttFollowerStrategy.cs
  OK       Features\PttGlobalBreakEven.cs
  OK       Features\PttGlobalQuickExit.cs
  OK       Features\PttQuickExit.cs
  OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```
**Result**: PASS — 18 files OK, 0 MISMATCH. CopyEngine.cs copied and verified. BwaveNextLaneBTests.cs is a test file (excluded from NT8 sync — correct).

---

## 6. CYC Per Modified Method

| Method | File | CYC Before | CYC After | Budget |
|--------|------|-----------|-----------|--------|
| `SubmitDrainedEntry` | `CopyEngine.cs` | 4 | 4 | PASS (<=8) |
| `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | `BwaveNextLaneBTests.cs` | N/A (test) | N/A (test) | N/A |

No new decision branches introduced in either change.

---

## 7. Acceptance Criteria Checklist

```
[x] R3-F1: FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode test passes
[x] R3-F1: Priv constant at BwaveNextLaneBTests.cs line 15 is UNCHANGED
[x] R3-F1: Only line ~172 modified in BwaveNextLaneBTests.cs
[x] R3-F2: SubmitEntryDirect appears BEFORE foreach cleanup in SubmitDrainedEntry
[x] R3-F2: _pendingDispatchDrains.TryRemove remains the FIRST statement in SubmitDrainedEntry
[x] R3-F2: SubmitDrainedEntry CYC = 4 (manual analysis confirms)
[x] R3-F2: No try/catch added anywhere in the method
[x] R3-F2: No new branches added
[x] R3-V1: "R3-V1 DISMISSED: NT8 docs confirm Order.Name non-null for live orders. StartsWith is safe. No fix applied." documented
[x] SCAN-01: 0 lock() results
[x] SCAN-02: 0 async void results (non-handler)
[x] SCAN-03: no new return null in modified files
[x] SCAN-04: SubmitDrainedEntry CYC <= 4
[x] SCAN-05: dotnet build exits 0, 0 errors
[x] SCAN-06: all 7 test-filter names pass (11 total passed)
[x] SCAN-07: ptt-sync-and-verify.ps1 exits 0, 0 MISMATCH
[x] (long)(int)Environment.TickCount preserved (not changed to TickCount64)
[x] .ToList() on ActiveOrders preserved (not removed)
[x] No new try/catch in hot paths
```

---

## 8. Verdict

**BUILD_PASS**

All 7 scans zero. 0 build errors. 11 tests pass. 0 MISMATCH in NT8 sync.
R3-F1 fix: `BindingFlags.NonPublic | BindingFlags.Static` at BwaveNextLaneBTests.cs line 172.
R3-F2 fix: `SubmitEntryDirect` moved before `foreach` cleanup in `SubmitDrainedEntry`.
R3-V1: DISMISSED per NT8 documentation evidence.

**NOTE**: F5 compilation in NinjaTrader 8 required as next mandatory step per `ptt-sync-and-verify.ps1` output.
