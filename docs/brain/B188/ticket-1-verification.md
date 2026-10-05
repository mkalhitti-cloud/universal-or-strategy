# B188 Ticket-1 Verification

## VERDICT: VERIFY_PASS

**Epic**: B188 -- leaderStop Forward Fix (Direct-Repair Stamp)
**Ticket**: 1 (the only ticket)
**Baseline commit**: ec99eb4f (B187-T1/T2/T3)
**Verifier**: ptt-verifier
**Date**: 2026-07-10
**Files changed by B188 working-tree edit**:
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (2 lines changed)
- `src/PropTraderTools/CopyEngineTests.cs` (76 lines added -- T_B188_01..04)

---

## Acceptance Criteria

### AC1 -- Line ~175: ExecuteOne(forcedTargets) receives leaderStop: leaderStop

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" `
    -Pattern "ExecuteOne\(acc, pos\.Instrument, ticks\.t1, forcedTargets, leaderStop: leaderStop\)"
```

**Output**:
```
src\PropTraderTools\Features\PttGlobalQuickExit.cs:175:                    ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop);
```

**Result**: PASS -- match found at line 175.

---

### AC2 -- Line ~94: ExecuteOne(targets) receives leaderStop: leaderStop

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" `
    -Pattern "ExecuteOne\(acc, pos\.Instrument, ticks\.t1, targets, leaderStop: leaderStop\)"
```

**Output**:
```
src\PropTraderTools\Features\PttGlobalQuickExit.cs:94:                    ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);
```

**Result**: PASS -- match found at line 94.

---

### AC3 -- Both ExecuteFollowers calls still receive leaderStop (B187 wiring unchanged)

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" -Pattern "ExecuteFollowers\(acc, pos,"
```

**Output**:
```
src\PropTraderTools\Features\PttGlobalQuickExit.cs:96:                    ExecuteFollowers(acc, pos, targets, ticks, leaderStop); // (8)
src\PropTraderTools\Features\PttGlobalQuickExit.cs:176:                    ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop); // (8)
```

**Result**: PASS -- 2 matches, both include `leaderStop`.

---

### AC4 -- Build: 0 errors, 0 new warnings (SCAN-07)

**Command**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10
```

**Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.39
```

**Result**: PASS -- 0 errors, 0 warnings.

---

### AC5 -- ptt-sync.ps1: 19 linked, 0 errors

**Command**:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**Output**:
```
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  LINKED  LicenseClient.cs
  LINKED  PttTickMath.cs
  LINKED  TradeCopierAddOn.cs
  LINKED  TradeCopierPanel.cs
  LINKED  TradeCopierWindow.cs
  LINKED  Core\PttContracts.cs
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  LINKED  Features\PttBreakEvenSwap.cs
  LINKED  Features\PttCancel.cs
  LINKED  Features\PttCopier.cs
  LINKED  Features\PttFlatten.cs
  LINKED  Features\PttFollowerStrategy.cs
  LINKED  Features\PttGlobalBreakEven.cs
  LINKED  Features\PttGlobalQuickExit.cs
  LINKED  Features\PttQuickExit.cs
  LINKED  Features\PttTrim.cs

ptt-sync complete: 19 linked, 0 errors
```

**Result**: PASS -- 19 linked, 0 errors.

---

### AC6 -- 4 structural [Fact] tests T_B188_01..04 present and passing

**Tests added** to `src/PropTraderTools/CopyEngineTests.cs` (class `LaneFTests`, lines 9164-9237):
- `T_B188_01_Execute_List_Instrument_ExecuteOne_Receives_leaderStop_forcedTargets`
- `T_B188_02_Execute_Instrument_ExecuteOne_Receives_leaderStop_targets`
- `T_B188_03_Execute_List_Instrument_ExecuteFollowers_Wired_leaderStop`
- `T_B188_04_Execute_Instrument_ExecuteFollowers_Wired_leaderStop`

**Command**:
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~T_B188_" 2>&1 | Select-Object -Last 5
```

**Output**:
```
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 151 ms - PropTraderTools.Tests.dll (net48)
```

**Result**: PASS -- 4/4 pass, 0 skipped.

---

## 7-Scan DNA Check (B188 working-tree diff only)

The diff is exactly 2 lines in `PttGlobalQuickExit.cs`:
```diff
-                    ExecuteOne(acc, pos.Instrument, ticks.t1, targets);
+                    ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);

-                    ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);
+                    ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop);
```

### SCAN-01: No new lock() in diff
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "lock\("
```
Output: (none)
**Result: PASS**

### SCAN-02: No new return null in diff
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "return null"
```
Output: (none)
**Result: PASS**

### SCAN-03: No new async void in diff
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "async void "
```
Output: (none)
**Result: PASS**

### SCAN-04: CYC Manual Analysis

The 2-line B188 edit adds named arguments to existing `ExecuteOne` call sites. Named arguments are
call-site syntax; they introduce zero conditional branches.

**Execute(Instrument instrument) -- CYC decision points**:
1. combined gate `if (!_isCopyEnabled || ...)` (1)
2. account loop `foreach (var acc ...)` (2)
3. follower guard `if (acc == leader) continue` (3)
4. position loop `foreach (var pos ...)` (4)
5. null/flat continue `if (pos == null || pos.Quantity == 0) continue` (5)
6. instrument filter `if (!Equals(pos.Instrument, instrument)) continue` (6)
7. `NeedsLeaderFallbackFlatten` guard `if (NeedsLeaderFallbackFlatten(...))` (7)
8. `ExecuteFollowers` call exits inner loop (marks decision point) (8)

CYC = 8. B188 edit: 0 new branches. CYC unchanged at **8**.

**Execute(List<QxTarget> forcedTargets, Instrument instrument) -- identical structure**: CYC = **8**.

**Result: PASS** -- Both overloads remain at CYC=8, within the JS P1 limit of <=8.

### SCAN-05: No new non-ASCII in diff
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "[^\x00-\x7F]"
```
Output: (none)
**Result: PASS**

### SCAN-06: No new throw new in diff
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "throw new"
```
Output: (none)
**Result: PASS**

### SCAN-07: dotnet build 0 errors, 0 warnings
See AC4 above.
**Result: PASS**

---

## Guardian Script (verify_critical_symbols.ps1)

**Command**: `powershell -File .\scripts\verify_critical_symbols.ps1`

**Output**: EXIT 1 -- SINGLE-HUNK-REPLACE-DETECTED: CopyEngine.cs

**Assessment**: PRE-EXISTING CONDITION from B187 HEAD commit (ec99eb4f). The script checks
`git diff HEAD~1 HEAD -- CopyEngine.cs` which evaluates the last committed change. B187 HEAD
touched CopyEngine.cs in a single hunk (@@-420,7 +420,9 -- a 6-line change). This is flagged
by the single-hunk ban regardless of hunk size.

**B188 impact on CopyEngine.cs**: ZERO. Confirmed:
```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Measure-Object -Line
# Lines: 0
```

B188 does not touch `CopyEngine.cs`. The guardian failure is attributable solely to B187 HEAD
and must be resolved in a future B187 guardian-cleanup ticket, not here.

**B188-scoped verdict**: PASS (no new violations introduced by B188).

---

## Summary Table

| AC/Scan | Check | Result |
|---------|-------|--------|
| AC1 | Line 175: ExecuteOne(forcedTargets, leaderStop: leaderStop) | PASS |
| AC2 | Line 94: ExecuteOne(targets, leaderStop: leaderStop) | PASS |
| AC3 | ExecuteFollowers still wired (2 matches) | PASS |
| AC4 | dotnet build 0 errors 0 warnings | PASS |
| AC5 | ptt-sync 19 linked 0 errors | PASS |
| AC6 | T_B188_01..04 all passing (4/4) | PASS |
| SCAN-01 | No new lock() | PASS |
| SCAN-02 | No new return null | PASS |
| SCAN-03 | No new async void | PASS |
| SCAN-04 | CYC=8 for both overloads (unchanged) | PASS |
| SCAN-05 | No new non-ASCII | PASS |
| SCAN-06 | No new throw new | PASS |
| SCAN-07 | Build 0/0 | PASS |
| AC5/Guardian | verify_critical_symbols.ps1 | PRE-EXISTING B187 flag; B188 adds 0 CopyEngine.cs changes |

---

## Blocking Items

None. All B188-scoped checks pass.

---

## VERIFY_PASS