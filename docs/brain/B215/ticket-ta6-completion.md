# Ticket TA6 Completion Report
## Block: B215-LaneA | Epic: B215 (Test-Debt Remediation)
## Ticket: TA6 -- Fix B189T4SpinnerTests (CopyEngine.cctor Skip + IL-to-source rewrite)

---

## Pre-Check Results (TradeCopierPanel.cs Verification)

### OnQuickAllBufferChanged exists:
```
LineNumber: 1054  private void OnQuickAllBufferChanged(int newT1)
Also subscribed at lines 607, 798
```
**Result: PASS** -- at least 1 match confirmed.

### _instrQAll2tBtn exists in TradeCopierPanel.cs:
```
LineNumber: 282   private Button _instrQAll2tBtn = null;
LineNumber: 537   if (_instrQAll2tBtn != null)
LineNumber: 538       _instrQAll2tBtn.Content = FormatBuffer("2x All", _quickAllT1);
LineNumber: 1060  if (_instrQAll2tBtn != null)
LineNumber: 1061      _instrQAll2tBtn.Content = FormatBuffer("QAll2t", newT1);
LineNumber: 1270  OnInstrQAll2tClick, ref _instrQAll2tBtn));
...
```
**Result: PASS** -- multiple matches confirmed.

### _instrQAll2tBtn within OnQuickAllBufferChanged body (lines 1054-1063):
```csharp
private void OnQuickAllBufferChanged(int newT1)
{
    Dispatcher.InvokeAsync(() =>
    {
        if (_quickAllBtn != null)
            _quickAllBtn.Content = FormatQuickAllBuffer("Quick All", newT1);
        if (_instrQAll2tBtn != null)          // line 1060
            _instrQAll2tBtn.Content = FormatBuffer("QAll2t", newT1);  // line 1061
    });
}
```
**Result: PASS** -- `_instrQAll2tBtn` appears within the method body at lines 1060-1061 (inside Dispatcher.InvokeAsync lambda). Source-text search approach confirmed correct.

---

## What Was Implemented

### Fix 1: T_B189_T4_03 Skip Attribute (line 10601 after edit)
**File:** `src/PropTraderTools/CopyEngineTests.cs`

Changed `[Fact]` to `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`
on the `T_B189_T4_03_QAll2tSpinner_Up_IncrementsGlobalQuickAllT1` method.

Rationale: This test calls `CopyEngine.Instance` which triggers a TypeInitializationException
from Agile.NET cctor when running outside NT8 host. Skip is the correct fix.

### Fix 2 Step A: GetPanelSourcePath helper (lines 10573-10579 after edit)
**File:** `src/PropTraderTools/CopyEngineTests.cs`

Added after closing brace of `InvokeFormatBuffer`, before first `[Fact]` in B189T4SpinnerTests:

```csharp
        private static string GetPanelSourcePath(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "")
        {
            string dir = System.IO.Path.GetDirectoryName(callerFile) ?? string.Empty;
            return System.IO.Path.Combine(dir, "TradeCopierPanel.cs");
        }
```

Uses `[CallerFilePath]` to resolve `TradeCopierPanel.cs` from the same directory as
`CopyEngineTests.cs` (`src/PropTraderTools/`). Same-directory path confirmed correct.

### Fix 2 Step B: T_B189_T4_05 body replacement (lines 10622-10637 after edit)
**File:** `src/PropTraderTools/CopyEngineTests.cs`

Replaced IL-token search (25 lines) with source-text search (12 lines):

Old approach: `GetMethodBody().GetILAsByteArray()` + `MetadataToken` byte scan.
Problem: `_instrQAll2tBtn` access is inside a `Dispatcher.InvokeAsync(() => {...})` lambda.
The compiler generates a closure class -- the field token is NOT in the outer method's IL bytes,
causing the byte-level search to fail.

New approach: `File.ReadAllText(path)` + `IndexOf("private void OnQuickAllBufferChanged")`
+ `Assert.Contains("_instrQAll2tBtn", body)`. Source-text is not affected by lambda hoisting.

---

## All 7 Scan Results

**SCAN-1: JS-021 -- No lock() in new helper or test body**
```powershell
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "lock\(" |
  Where-Object { $_.LineNumber -ge 10573 -and $_.LineNumber -le 10660 }
```
Result: 0 matches. **PASS**

**SCAN-2: T_03 Skip + T_05 Pass (targeted test run)**
```
dotnet test --filter "FullyQualifiedName~T_B189_T4_03|FullyQualifiedName~T_B189_T4_05"
```
Result: T_03 SKIPPED, T_05 PASSED. Failed: 0. **PASS**

**SCAN-3: async void in new code**
```powershell
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "async void" |
  Where-Object { $_.LineNumber -ge 10573 -and $_.LineNumber -le 10660 }
```
Result: 0 matches. **PASS**

**SCAN-4: return null; in new code**
```powershell
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "return null;" |
  Where-Object { $_.LineNumber -ge 10573 -and $_.LineNumber -le 10660 }
```
Result: 0 matches. **PASS**

**SCAN-5: return null; (global -- sanity)**
No new `return null;` introduced in any new code. **PASS**

**SCAN-6: IL-search gone from T_05; _instrQAll2tBtn present in TradeCopierPanel.cs OnQuickAllBufferChanged**

IL-search scan (lines 10610-10660):
```powershell
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "MetadataToken|tokenBytes|GetILAsByteArray" |
  Where-Object { $_.LineNumber -ge 10610 -and $_.LineNumber -le 10660 }
```
Result: 1 match at line 10618 -- this is T_B189_T4_04 (`OnInstr2tClick` opcode test), NOT T_05.
T_05 (lines 10622-10637) contains zero IL-search patterns. **PASS**

_instrQAll2tBtn in OnQuickAllBufferChanged: confirmed at TradeCopierPanel.cs lines 1060-1061. **PASS**

**SCAN-7: verify_pr_hygiene.ps1 -- branch divergence**
```
FAIL: Branch is NOT based on the latest main.
```
Pre-existing condition: local main is 75 commits ahead / 839 commits behind origin/main.
This divergence exists before TA6 and is NOT caused by this ticket.
Per ticket spec: document as pre-existing. DO NOT rebase. **DOCUMENTED (pre-existing)**

---

## Full B189T4SpinnerTests Suite
```
dotnet test --filter "FullyQualifiedName~B189T4SpinnerTests" --no-build
Result: Failed: 0, Passed: 5, Skipped: 1, Total: 6
```
**PASS** -- 0 failures, 0 regressions.

---

## Full Test Suite
```
dotnet test --no-build
Result: Failed: 9, Passed: 207, Skipped: 555, Total: 771
```
**PASS** -- matches expected post-TA6 baseline.
- Baseline (pre-TA6): Failed=11, Passed=206, Skipped=554
- After TA6: Failed=9 (drop of 2), Passed=207 (+1), Skipped=555 (+1)
- T_03: Fail -> Skip (net: -1 fail, +1 skip)
- T_05: Fail -> Pass (net: -1 fail, +1 pass)

---

## ptt-sync.ps1
```
ptt-sync complete: 21 linked, 0 errors
```
**PASS**

---

## Jane Street Rules Check

- **JS-021 (lock):** No lock() in new code. PASS.
- **JS-001 (throw):** No explicit `throw new XxxException`. PASS.
- **JS-002 (return null):** No `return null` in new code. PASS.
- **JS-033 (async void):** No async void in new code. PASS.
- **ASCII-only:** All string literals are ASCII. PASS.
- **CYC:** `GetPanelSourcePath` CYC=1. `T_B189_T4_05` new body CYC=2. Both <= 8. PASS.
- **Diff size:** ~25 lines changed in CopyEngineTests.cs. Well under 10k chars. PASS.

---

## Return Value

**BUILD_PASS**
