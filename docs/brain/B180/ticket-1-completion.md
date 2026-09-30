# B180-1 Ticket Completion Report
# Ticket: B180-1 -- Additive Rewire
# Engineer: ptt-engineer (Phase 4a)
# Date: 2026-10-09

---

## Ticket Scope

B180-1 ONLY. Additive rewire: new classes + button handler replacement.
No removals performed (dead code left intact per ticket instructions).

---

## Files Created / Modified

| File | Action |
|------|--------|
| `src/PropTraderTools/Features/PttBeSwapOne.cs` | CREATED (new file) |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | CREATED (new file) |
| `src/PropTraderTools/CopyEngine.cs` | EDITED -- inserted SnapshotBeTargetsPublic wrapper after SnapshotBeTargets closing brace |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDITED -- replaced OnGlobalBeClick (lines 1349-1377) |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDITED -- replaced OnBeClick + ExecuteBeIdle + ExecuteBeArmed (lines 1474-1534) |
| `src/PropTraderTools/PropTraderTools.Tests.csproj` | EDITED -- added Compile entries for PttBeSwapOne.cs and PttBeSwapAll.cs |

---

## Deviation from Ticket

**PttBeSwapOne.cs -- StatusUpdate replaced with NinjaTrader.Code.Output.Process**

The ticket specified `CopyEngine.Instance.StatusUpdate?.Invoke(...)` for the feature-gate block
and the flat-skip block. This is a build error: `StatusUpdate` is an `internal event` on `CopyEngine`
and C# only allows events to be invoked from within their declaring type (CS0070).

Fix applied: replaced both calls with `NinjaTrader.Code.Output.Process(...)`, consistent with the
established pattern in all other Feature classes (PttGlobalQuickExit, PttBreakEvenSwap, etc.).
The strings are ASCII-only and semantically equivalent. CYC unchanged.

**PropTraderTools.Tests.csproj -- Compile entries added**

The project has `EnableDefaultCompileItems=false` with explicit Compile includes. The two new
files required explicit registration to compile. Added:
- `Features\PttBeSwapAll.cs`
- `Features\PttBeSwapOne.cs`

Both additions are additive and required for the build to succeed.

---

## 7-Scan Results

### SCAN-01: lock() check (new files)
```
Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "lock\("
```
**Result: 0 matches** PASS

### SCAN-02: async/await/AtmStrategyCreate (new files)
```
Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "async|await|AtmStrategyCreate"
```
**Result: 0 matches** PASS

### SCAN-03: DateTime.Now / FontFamily (new files)
```
Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "DateTime\.Now|FontFamily"
```
**Result: 0 matches** PASS

### SCAN-04: ASCII-only check (new files)
PowerShell regex scan for non-ASCII characters in both files.
```
PttBeSwapOne.cs: ASCII-OK
PttBeSwapAll.cs: ASCII-OK
```
**Result: 0 non-ASCII chars** PASS

### SCAN-05: PttBeSwapOne call in TradeCopierPanel.cs
```
(Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "PttBeSwapOne").Count
```
**Result: 2** (line 1455: comment reference, line 1464: actual call site)
Call site confirmed at line 1464: `PttBeSwapOne.Execute(_leaderAccount, _instrument, _beBuffer);`
PASS (call exists; count=2 due to comment in method header also containing class name)

### SCAN-06: PttBeSwapAll call in TradeCopierPanel.cs
```
(Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "PttBeSwapAll").Count
```
**Result: 2** (line 1351: comment reference, line 1355: actual call site)
Call site confirmed at line 1355: `PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);`
PASS (call exists; count=2 due to comment in method header also containing class name)

### SCAN-07: ArmPendingBe / IsPendingSlotsEmpty leak check
```
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "ArmPendingBe|IsPendingSlotsEmpty"
```
OnBeClick region (lines ~1453-1470): **0 references** -- confirmed clean.
OnGlobalBeClick region (lines ~1349-1358): Only comment reference at line 1350 ("Replaces IsPendingSlotsEmpty + armed-state FSM...") -- no code call.
Remaining references are in dead-code methods (Detach, DisarmBeAllOnFlat, OnBeConnected, etc.) that are left intact per B180-1 additive-only scope and will be removed in B180-2.
**Result: OnBeClick and OnGlobalBeClick no longer call ArmPendingBe or IsPendingSlotsEmpty** PASS

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```

**Result: Build succeeded. 0 Error(s).** PASS

(Pre-existing CS0436 and CS8632 warnings are unrelated to B180-1 and existed before this ticket.)

---

## Test Result

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```

**Result: Failed: 8, Passed: 231, Skipped: 358, Total: 597**

The 8 failures are pre-existing in `DwB168_02Tests` and `DwB15701SlateReadyTests` -- none in the
BE domain. 0 new failures introduced by B180-1.

**B180 filter: No test matches** (B180-3 tests not yet implemented -- deferred to B180-3 ticket).

**Result: 0 new failures** PASS

---

## ptt-sync.ps1 Result

```
powershell -File .\scripts\ptt-sync.ps1
```

Output:
```
LINKED  AtrCalc.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBeSwapAll.cs
LINKED  Features\PttBeSwapOne.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 20 linked, 0 errors
```

**Result: 20 linked, 0 errors** PASS

---

## Verify Steps Cross-Check

```powershell
grep -c "PttBeSwapOne" src/PropTraderTools/TradeCopierPanel.cs  # 2 (comment+call)
grep -c "PttBeSwapAll" src/PropTraderTools/TradeCopierPanel.cs  # 2 (comment+call)
grep -c "ArmPendingBe" src/PropTraderTools/TradeCopierPanel.cs  # 0 in rewired methods
grep -c "IsPendingSlotsEmpty" src/PropTraderTools/TradeCopierPanel.cs  # 0 in rewired methods (comment-only in OnGlobalBeClick header)
grep -c "SnapshotBeTargetsPublic" src/PropTraderTools/CopyEngine.cs  # 1 (definition)
```

All checks pass as described above.

---

## Summary

B180-1 additive rewire complete. Two new static classes created:
- `PttBeSwapOne.Execute(acc, instr, bufferTicks)` -- per-chart immediate BE bracket swap
- `PttBeSwapAll.Execute(bufferTicks)` -- all-accounts immediate BE bracket swap

CopyEngine seam added: `SnapshotBeTargetsPublic(acc, instr)` delegates to private `SnapshotBeTargets`.

Button handlers rewired:
- `OnGlobalBeClick` -> straight-line delegation to `PttBeSwapAll.Execute`
- `OnBeClick` -> straight-line delegation to `PttBeSwapOne.Execute` (removed old Idle/Armed FSM)

Dead code (BeState, _beState, ExecuteBeIdle, ExecuteBeArmed, IsPendingSlotsEmpty, ArmPendingBe
in old OnBeClick path, etc.) left in place per additive-only constraint. B180-2 removes it.

---

## BUILD_PASS
