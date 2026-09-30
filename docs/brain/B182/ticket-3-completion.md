# B182 Ticket 3 Completion
## Ph4a Output -- ptt-engineer
### Ticket: B182-T3 -- DW-B181-G5 ATM Template Reverse Check
### Date: 2026-09-29
### Status: BUILD_PASS

---

## Summary of Changes

### 1. CopyEngine.cs -- Field added

**File:** `src/PropTraderTools/CopyEngine.cs`

Added after `private double _dailyCapFloor = -500.0;` (line ~233):

```csharp
// DW-B181-G5: ATM template Reverse check -- tracks which templates were already warned this session.
// Written only at State.Configure (single-threaded startup). No lock needed (JS-021).
private readonly HashSet<string> _warnedAtmTemplates = new HashSet<string>();
```

### 2. CopyEngine.cs -- CheckAtmTemplatesForReverse methods added

Added after `TryEmitBeFlattenWarn` method (line ~1709):

**Overload (1): production path (CYC=1)**
```csharp
// CYC=1: straight delegation. JS-001: no throw. JS-002: void. ASCII-only.
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "Templates", "ATM"));
```

**Overload (2): test seam (CYC=6)**
```csharp
// CYC=6: (1) outer try, (2) dir not found, (3) foreach file, (4) already warned,
//         (5) per-file try, (6) reverse check + emit.
// Ph2 amendment applied: per-file try/catch so one unreadable file does not abort scan.
internal void CheckAtmTemplatesForReverse(string atmDir)
{
    // ... (full implementation per ticket spec)
}
```

CYC audit: 6 decision points. PASS <= 8.

### 3. TradeCopierAddOn.cs -- Wire-up in State.Configure

**File:** `src/PropTraderTools/TradeCopierAddOn.cs`

Added after `CopyEngine.Instance.SetFlags(flags);` (line 78):

```csharp
CopyEngine.Instance.CheckAtmTemplatesForReverse(); // DW-B181-G5
```

### 4. B182Tests.cs -- B182AtmTemplateScanTests class added

**File:** `src/PropTraderTools/Tests/B182Tests.cs`

Added `B182AtmTemplateScanTests` class with 3 [Fact] methods:
- `StartupWarn_EmitsForReverseYesTemplate` -- crafts temp XML with `<Reverse>true</Reverse>`, verifies detection logic
- `StartupWarn_NoEmitForTemplateWithoutReverse` -- verifies `<Reverse>false</Reverse>` returns false
- `StartupWarn_DirNotFound_DoesNotThrow` -- verifies precondition for fail-open branch

Note: B182Tests.cs was also rebuilt from scratch to include T1 (B182BeFixTests) and T2 (B182BeFlattenWarnTests) content, as the file was 0 bytes (stub) at session start. All prior T1+T2 content re-applied from 04-tickets.md spec.

---

## Layer 2 Scan Results (all 7 scans)

### SCAN-01: lock() usage check
```
Select-String -Path src\PropTraderTools\*.cs -Pattern "lock\s*\("
(filtered: lines in comments excluded)
```
**Result: 0 actual lock() statements. All comment-text hits (e.g. "no lock"). PASS.**

### SCAN-02: Non-ASCII characters
```
Get-Content src\PropTraderTools\CopyEngine.cs | Where-Object { $_ -match '[^\x00-\x7F]' }
Get-Content src\PropTraderTools\TradeCopierAddOn.cs | Where-Object { $_ -match '[^\x00-\x7F]' }
Get-Content src\PropTraderTools\Tests\B182Tests.cs | Where-Object { $_ -match '[^\x00-\x7F]' }
```
**Result: 0 non-ASCII in CopyEngine.cs. 0 in TradeCopierAddOn.cs. 0 in B182Tests.cs. PASS.**

### SCAN-03: FontFamily usage
```
Select-String -Path src\PropTraderTools\CopyEngine.cs,src\PropTraderTools\TradeCopierAddOn.cs -Pattern "FontFamily"
(filtered: comment-only hits excluded)
```
**Result: 0 FontFamily API calls in code (3 comment-only pre-existing hits not in code). PASS.**

### SCAN-04: Hex color literals (#RRGGBB)
```
Select-String ... -Pattern "#[0-9A-Fa-f]{6}" (comment lines excluded)
```
**Result: 0 hits in code. PASS.**

### SCAN-05: CreateOrder name prefix ("PTT-")
```
T3 new methods (CheckAtmTemplatesForReverse, _warnedAtmTemplates field):
No CreateOrder calls introduced by T3.
```
**Result: 0 new violations. PASS.**

### SCAN-06: DateTime.Now usage
```
Select-String ... -Pattern "DateTime\.Now[^U]" (comment lines excluded)
```
**Result: 0 hits. PASS.**

### SCAN-07: lock\b pattern (regex variant)
```
Select-String ... -Pattern "\block\s*\(" (comment lines excluded)
```
**Result: 0 actual lock statements. PASS.**

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.54
```

**PASS: 0 errors.**

---

## Test Result (B182 filter)

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B182" --no-build
```

```
Test run for PropTraderTools.Tests.dll (.NETFramework,Version=v4.8)
[xUnit.net] Skipped PropTraderTools.Tests.B182BeFlattenWarnTests.BE_Flatten_AfterBeAll_EmitsWarnLog [1 ms]

Passed!  - Failed: 0, Passed: 10, Skipped: 1, Total: 11, Duration: 169 ms
```

**PASS: 10 pass + 1 skip (deferred NT8 integration test).**

Tests passing (10):
1. `B182BeFixTests.BE_SubmitSwapPair_TargetPriceIsEntryNotOriginalAtm` (T1)
2. `B182BeFixTests.BE_SubmitSwapPair_StopPriceIsSnapshotNotEntry` (T1)
3. `B182BeFixTests.BE_SubmitBareStopSwap_UsesSnapshotStop` (T1)
4. `B182BeFixTests.BE_Execute_NoIsStopPriceSubmittableGuard` (T1)
5. `B182BeFlattenWarnTests.BE_FlattenWarn_LogicCheck_BePrefixAndFlatPosition` (T2)
6. `B182BeFlattenWarnTests.BE_FlattenWarn_LogicCheck_PositionOpen_NoEmit` (T2)
7. `B182BeFlattenWarnTests.BE_FlattenWarn_LogicCheck_NonPttName_NoEmit` (T2)
8. `B182AtmTemplateScanTests.StartupWarn_EmitsForReverseYesTemplate` (T3)
9. `B182AtmTemplateScanTests.StartupWarn_NoEmitForTemplateWithoutReverse` (T3)
10. `B182AtmTemplateScanTests.StartupWarn_DirNotFound_DoesNotThrow` (T3)

Skipped (1):
- `B182BeFlattenWarnTests.BE_Flatten_AfterBeAll_EmitsWarnLog` [DEFERRED: NT8 runtime]

---

## Full Test Suite Regression Check

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```

```
Failed!  - Failed: 14, Passed: 48, Skipped: 527, Total: 589, Duration: ~1s
```

**Regressions introduced by T3: 0.**

The 14 failures are pre-existing (verified by git stash test: same 14 failures exist on HEAD before T3 changes). All 14 are in B153/B154/B155/DwB157 test classes that are deferred structural contracts unrelated to T3.

---

## ptt-sync Result

```
powershell -File .\scripts\ptt-sync.ps1
```

```
LINKED  AtrCalc.cs
LINKED  AtrSizingEngine.cs
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

ptt-sync complete: 21 linked, 0 errors
```

**PASS: 21/21 LINKED.**

---

## Jane Street Compliance

| Rule | Status |
|------|--------|
| JS-001: no throw in hot paths | PASS -- try/catch absorbs exceptions, no rethrow |
| JS-002: no return null | PASS -- both methods are void |
| JS-021: no lock() | PASS -- HashSet written single-threaded at startup, no lock needed |
| JS-033: no async void | PASS -- synchronous void methods |
| CYC <= 8 | PASS -- CheckAtmTemplatesForReverse() CYC=1, overload CYC=6 |
| ASCII-only | PASS -- all string literals ASCII |
| NT8-014 log prefix | PASS -- [PTT-STARTUP-INFO] and [PTT-STARTUP-WARN] on all Output.Process calls |

---

## GATE RESULT

```
BUILD_PASS
```
