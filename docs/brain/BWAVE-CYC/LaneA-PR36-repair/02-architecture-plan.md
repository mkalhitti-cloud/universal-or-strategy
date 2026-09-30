# BWAVE-CYC Lane A PR #36 Repair -- Architecture Plan

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Branch**: feature/bwave-cyc-lane-a (HEAD: 2270c544)
**PR**: #36
**Date**: 2026-09-03
**Architect**: ptt-architect
**LANE-SPLIT GATE RESULT**: SINGLE-PIPELINE

---

## Known Baseline

| Item | Status |
|------|--------|
| NT8-runtime pre-existing test failures | 80 -- accepted by Director |
| 10k diff waiver | Approved for PR #36 |
| Greptile check | SUCCESS on PR #36 (inline findings must be resolved) |
| CodeRabbit state | CHANGES_REQUESTED on PR #36 |

---

## Pre-Flight: Code Reality Audit

Before writing fixes, the architect read every file. Key discoveries that affect ticket scope:

1. **A-4 (SA1507/SA1508)**: The CSharpier format-pass commit `2270c544` has already resolved all consecutive-blank-line and trailing-blank-before-brace violations. A PowerShell scan confirms 0 SA1507 and 0 SA1508 violations in the current `CopyEngineTests.cs`. **Ticket A-4 is a NO-OP against the current HEAD; engineer must confirm and document as already-fixed.**

2. **A-5 (Teal button background regression)**: The `BuildArrowCluster` extracted method was present in LaneC R11 but was subsequently replaced by a full inline rewrite (`BuildBufferedButtonsRow` is now 270 lines, inline only). In the current HEAD, `BuildArrowCluster` does NOT exist. The teal buttons (`_beBtn2`, `_globalBeBtn2`, `_quickBtn`, `_quickAllBtn`) do NOT have `Background = BrushInactive` set on them. **Ticket A-5 is a NO-OP against the current HEAD; engineer must confirm and document as already-fixed by the inline rewrite.**

3. **A-6 (FindPositionForInstrument)**: The TA-R9 verify confirmed `FindPositionForInstrument` existed at `CopyEngine.cs:1182`. The method is NOT present in the current HEAD -- it was removed between the R9 verify and the `2270c544` CSharpier/LaneC merge. The test file `BwaveCycLaneAR9Tests.cs` (lines 160-176) and the misplaced block in `CopyEngineTests.cs` (lines 7364-7395) both reference it via reflection. Fix: re-introduce the method with the JS-002-compliant `TryFindPositionForInstrument` signature, and update ALL test references from `FindPositionForInstrument` to `TryFindPositionForInstrument`.

4. **A-1 (Unicode arrows)**: The ticket cited lines 1214, 1220, 2987, 2992. Actual scan finds Unicode `\u25B2`/`\u25BC` at lines **1147, 1153, 1184, 1190, 1226, 1232, 1265, 1271, 1311, 1317, 1350, 1356** in `TradeCopierPanel.cs` (all in the `BuildBufferedButtonsRow` block -- the buffered arrow buttons). Additionally lines 1781, 1815, 1831-1832, 1888, 2344, 2350, 2380-2381, 2388, 2390, 3159, 3164, 3202, 3207 contain other Unicode escapes (COPY ON/OFF toggle, Copier collapse chevron, QX-2T spinners). The Greptile P2 / CodeRabbit CR36-3 findings target the **newly added buffered-buttons code**. The pre-existing toggle/collapse/QX-2T Unicode is out of scope per Director waiver. Fix scope: replace `\u25B2` with `"^"` and `\u25BC` with `"v"` at lines **1147, 1153, 1184, 1190, 1226, 1232, 1265, 1271, 1311, 1317, 1350, 1356** only.

---

## Ticket Execution Order

Dependencies:
- A-6 touches `CopyEngine.cs` only (adds a new method).
- A-6 also updates test files to use the new method name.
- A-2 removes a block from `CopyEngineTests.cs`; A-3 fixes a body within the same file. These are independent.
- A-1 touches `TradeCopierPanel.cs` only.
- A-4 and A-5 are NO-OPs (confirm + document).

**Mandated order**: A-1, A-2, A-3, A-4 (confirm), A-5 (confirm), A-6.

Rationale: A-2 removes lines 7181-7395 from `CopyEngineTests.cs` which INCLUDES the misplaced `FindPositionForInstrument` tests at 7364-7395. A-6 then adds proper `TryFindPositionForInstrument` tests back. A-2 must therefore be executed BEFORE A-6 to avoid conflicts.

---

## TICKET A-1: ASCII violation -- buffered button arrows

**Category**: VALID-DNA
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Rule**: V12 DNA ASCII-Only Compliance (AGENTS.md §2) — "NEVER use Unicode, emoji, or curly quotes in C# string literals"
**Source**: CodeRabbit CR36-3 + Greptile P2. Authority: V12 DNA mandate (AGENTS.md §2, Architectural Mandates).

### Confirmed Locations (from source scan)

| Line | Current Content | Fix |
|------|----------------|-----|
| 1147 | `Content = "\u25B2",` | `Content = "^",` |
| 1153 | `Content = "\u25BC",` | `Content = "v",` |
| 1184 | `Content = "\u25B2",` | `Content = "^",` |
| 1190 | `Content = "\u25BC",` | `Content = "v",` |
| 1226 | `Content = "\u25B2",` | `Content = "^",` |
| 1232 | `Content = "\u25BC",` | `Content = "v",` |
| 1265 | `Content = "\u25B2",` | `Content = "^",` |
| 1271 | `Content = "\u25BC",` | `Content = "v",` |
| 1311 | `Content = "\u25B2",` | `Content = "^",` |
| 1317 | `Content = "\u25BC",` | `Content = "v",` |
| 1350 | `Content = "\u25B2",` | `Content = "^",` |
| 1356 | `Content = "\u25BC",` | `Content = "v",` |

**Total replacements**: 12 occurrences (6 pairs of up/down arrows in 6 button clusters: Trim, Flatten, BE, BE ALL, Quick, Quick ALL).

**Out-of-scope Unicode** (pre-existing, Director waiver applies):
Lines 1781, 1815, 1831-1832, 1888, 2344, 2350, 2380-2381, 2388, 2390, 3159, 3164, 3202, 3207 -- COPY ON/OFF toggle, Copier collapse chevron, QX-2T spinners. Do NOT touch these.

### Exact old text pattern (12 sites, same shape):
```
                Content = "\u25B2",
```
and
```
                Content = "\u25BC",
```

### Exact new text (corresponding replacements):
```
                Content = "^",
```
and
```
                Content = "v",
```

### Verification
```powershell
Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "\\u25B[23]" | Where-Object { $_.LineNumber -ge 1130 -and $_.LineNumber -le 1400 }
# Expected: 0 results
```

---

## TICKET A-2: Misplaced TA-R9 test block in CopyEngineTests.cs

**Category**: VALID-MECHANICAL
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Rule**: Compile-time correctness (CS0103 -- name `_engine` / `GetField` does not exist in `BwaveCycTaR7HelperTests`).
**Source**: CodeRabbit CR36-1 (CHANGES_REQUESTED).

### Problem

The `BwaveCycTaR7HelperTests` class (starting at line 7099) contains a block beginning at line 7181 that tests TA-R9 helpers (`IsFollowerByName`, `IsOrderForInstrument`, `IsSnapshotBlocked`, `TryCancelOrders`, `FindPositionForInstrument`). Two of these tests (lines 7197-7209) reference `_engine` (CopyEngine instance) and `GetField` (a helper method) -- neither exists in `BwaveCycTaR7HelperTests`. This causes `CS0103` compile errors.

The canonical tests for these helpers are in `BwaveCycLaneAR9Tests.cs`. The misplaced block is a duplicate that causes compilation failure.

### Confirmed Block to Remove

```
Lines 7181-7395 of CopyEngineTests.cs
```

Exact start marker (line 7181):
```csharp

        // =====================================================================
        // TA-R9: New helper tests (ticket R9 -- CCN reduction extractions)
        // =====================================================================
```

Exact end marker (line 7395):
```csharp
        }
```
(closing brace of `FindPositionForInstrument_ShouldReturnNull_WhenInstrumentIsNull`)

After removal, line 7396 (now 7181) begins:
```csharp

        // =====================================================================
        // TA-R10: GetFollowerMultiplier + BuildAtmModeMap (DtoToRule/RuleToDto helpers)
        // =====================================================================
```

**Lines preserved**: Everything before line 7181 (TA-R7 tests) and everything from line 7396 onward (TA-R10 tests through end of file).

### Note on A-6 interaction
Lines 7364-7395 (inside the removed block) contain `FindPositionForInstrument` tests. After A-2 removes them, A-6 will NOT re-add them to `CopyEngineTests.cs`. The canonical tests are in `BwaveCycLaneAR9Tests.cs`, which will be updated by A-6 to use `TryFindPositionForInstrument`.

### Verification
```powershell
# Must return 0 results after removal:
Select-String -Path src\PropTraderTools\CopyEngineTests.cs -Pattern "_engine\.SetEnabled|GetField\(" | Where-Object { $_.LineNumber -gt 7095 }
# Expected: 0 results within BwaveCycTaR7HelperTests context
```

---

## TICKET A-3: Vacuous test assertions (swallowed exceptions)

**Category**: VALID-MECHANICAL
**Files**:
1. `src/PropTraderTools/CopyEngineTests.cs` -- line ~7352 (after A-2 line shifts)
2. `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- lines 146-153

**Rule**: Test integrity -- `Record.Exception` must observe exceptions directly; inner `try/catch(TargetInvocationException){}` swallows the exception so `Assert.Null(ex)` always passes.
**Source**: CodeRabbit CR36-2 + Cubic confidence=10.

### Instance 1: CopyEngineTests.cs -- TryCancelOrders test

**NOTE**: After A-2 removes lines 7181-7395, line numbers shift. The original line 7341 test `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` will move to approximately line 7156. The old lines are noted; engineer must locate by method name.

**Method**: `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` in `BwaveCycTaR7HelperTests`

**Current body (old lines 7352-7361)**:
```csharp
            var ex = Record.Exception(() =>
            {
                try
                {
                    mi.Invoke(null, new object[] { (Account)null, staleList });
                }
                catch (System.Reflection.TargetInvocationException) { }
                // Any exception is caught inside TryCancelOrders -- nothing escapes.
            });
            Assert.Null(ex);
```

**Fixed body**:
```csharp
            var ex = Record.Exception(() =>
                mi.Invoke(null, new object[] { (Account)null, staleList })
            );
            Assert.Null(ex);
```

**Rationale**: `TryCancelOrders` internally wraps `acc.Cancel` in `try/catch`. With a null account and empty stale list, `stale.Count == 0` so `acc.Cancel` is never called, meaning no exception propagates. `Record.Exception` directly observing `mi.Invoke` is sufficient and honest.

### Instance 2: BwaveCycLaneAR9Tests.cs -- T_R9_09 test

**Method**: `T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` (lines 141-154)

**Current body (lines 146-153)**:
```csharp
            var ex = Record.Exception(() =>
            {
                try
                {
                    mi.Invoke(null, new object[] { (Account)null, stale });
                }
                catch (TargetInvocationException) { }
            });
            Assert.Null(ex);
```

**Fixed body**:
```csharp
            var ex = Record.Exception(() =>
                mi.Invoke(null, new object[] { (Account)null, stale })
            );
            Assert.Null(ex);
```

### Verification
```powershell
# Both files: zero TargetInvocationException catches inside Record.Exception lambdas
Select-String -Path src\PropTraderTools\CopyEngineTests.cs, src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "TargetInvocationException"
# Expected: 0 results
```

---

## TICKET A-4: SA1507/SA1508 StyleCop violations

**Category**: VALID-MECHANICAL (NO-OP)
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Source**: CodeFactor FAILURE (original PR submission).

### Assessment

PowerShell scan of the current HEAD confirms:
- SA1507 (multiple consecutive blank lines): **0 violations found**
- SA1508 (closing brace preceded by blank line): **0 violations found**

The CSharpier format-pass commit `2270c544` resolved these violations. The CodeFactor FAILURE was against a prior commit; the current HEAD is clean.

**Engineer action**: Run the following scan and confirm:
```powershell
$content = Get-Content src\PropTraderTools\CopyEngineTests.cs
$prev = $false
$sa1507 = 0
for ($i = 0; $i -lt $content.Length; $i++) {
    $blank = ($content[$i].Trim() -eq "")
    if ($blank -and $prev) { $sa1507++ }
    $prev = $blank
}
Write-Host "SA1507 violations: $sa1507"
$sa1508 = 0
for ($i = 1; $i -lt $content.Length; $i++) {
    if ($content[$i-1].Trim() -eq "" -and $content[$i].Trim() -eq "}") { $sa1508++ }
}
Write-Host "SA1508 violations: $sa1508"
# Expected: both 0
```

Document result as CONFIRMED-ALREADY-FIXED. No source edit required.

---

## TICKET A-5: Teal button background regression

**Category**: VALID-LOGIC-BUG (NO-OP)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Source**: Greptile P2 + Cubic confidence=10 + CodeRabbit CHANGES_REQUESTED.

### Assessment

The CodeRabbit/Greptile finding referenced `BuildArrowCluster` unconditionally assigning `mainBackground` to `btn.Background` for teal-bordered buttons. `BuildArrowCluster` was extracted during LaneC R11. However, LaneC remediation subsequently replaced the data-driven loop + `BuildArrowCluster` with a full inline `BuildBufferedButtonsRow`.

**Current HEAD state** (confirmed by source read, lines 1245-1375):
- `_beBtn2` (BE): `BorderBrush = BrushTeal, Foreground = BrushTeal, BorderThickness = new Thickness(2)` -- **no Background property set**
- `_globalBeBtn2` (BE ALL): same pattern -- **no Background property set**
- `_quickBtn` (Quick): same pattern -- **no Background property set**
- `_quickAllBtn` (Quick ALL): same pattern -- **no Background property set**
- `_trimBtn2` (Trim): `Background = BrushInactive` -- correct
- `_flattenBtn2` (Flatten): `Background = BrushInactive` -- correct

The bug does NOT exist in the current inline code. `NTButtonStyle` default applies to teal buttons; `BrushInactive` is not incorrectly assigned.

**Engineer action**: Confirm by searching:
```powershell
Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "BuildArrowCluster"
# Expected: 0 results (method does not exist)
```
Document result as CONFIRMED-ALREADY-FIXED. No source edit required.

---

## TICKET A-6: JS-002 -- FindPositionForInstrument returns null; rename to TryFindPositionForInstrument

**Category**: VALID-LOGIC-BUG
**Files**:
1. `src/PropTraderTools/CopyEngine.cs` -- ADD method (was removed between TA-R9 verify and current HEAD)
2. `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- UPDATE tests T_R9_10 and T_R9_11
3. `src/PropTraderTools/CopyEngineTests.cs` -- NOTE: lines 7364-7395 referencing `FindPositionForInstrument` are removed by A-2; no further update needed in this file for A-6.

**Rule**: JS-002 -- no `return null` for missing values; mandate explicit optional-value representation. Director decision: fix before merge.
**Source**: Greptile P0 JS-002.

### Background

TA-R9 added `FindPositionForInstrument` (private static, CopyEngine.cs) as a helper for `SubmitBeStop`. The verify report confirmed it at line 1182. This method was lost between commit 68a1c1c4 (TA-R9 complete) and 2270c544 (CSharpier format pass + LaneC merge) -- likely a merge conflict or accidental revert. Current `FindBePosition` (lines 1119-1131) is structurally related but is `internal` instance method for a different caller (`SubmitBeStop` directly calls it).

The A-6 plan: Re-introduce the method with the bool-out pattern (JS-002 compliant) instead of the original null-return pattern.

### Method to Add to CopyEngine.cs

**Location**: Insert after the existing `FindBePosition` method (after line 1131, before the `SubmitBeStopOrder` comment block).

**Exact new method text**:
```csharp
        // BWAVE-CYC TA-R9 (restored): TryFindPositionForInstrument -- locate open position for acc+instr.
        // JS-002: bool + out parameter replaces null return (original FindPositionForInstrument pattern).
        // JS-021: acc.Positions is NT8 read-only collection -- no lock needed.
        // JS-001: no throw. JS-033: synchronous. ASCII-only.
        // CYC=3: base(1) + foreach(1) + inner null-guard(1).
        private static bool TryFindPositionForInstrument(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            out NinjaTrader.Cbi.Position pos
        )
        {
            pos = null;
            if (acc == null || instr == null) // (1)
                return false;
            foreach (NinjaTrader.Cbi.Position p in acc.Positions) // (2)
                if (p.Instrument != null && p.Instrument.FullName == instr.FullName) // (3)
                {
                    pos = p;
                    return true;
                }
            return false;
        }
```

**Caller update**: The original `FindPositionForInstrument` was called from `SubmitBeStop` at approx line 1129 as:
```csharp
var pos = FindPositionForInstrument(acc, instr);
```
That caller pattern no longer exists (the method was removed). Verify after insertion that no other caller in `CopyEngine.cs` references `FindPositionForInstrument`:
```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "FindPositionForInstrument"
# Expected: 0 results (only TryFindPositionForInstrument exists)
```

### Test Updates: BwaveCycLaneAR9Tests.cs

**Test T_R9_10** (lines 159-166) -- update method name:
```csharp
        [Fact]
        public void T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(2, mi.GetParameters().Length);
        }
```
Replace with:
```csharp
        [Fact]
        public void T_R9_10_TryFindPositionForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("TryFindPositionForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(bool), mi.ReturnType);
            Assert.Equal(3, mi.GetParameters().Length);
        }
```

**Test T_R9_11** (lines 168-176) -- update method name and parameter assertions:
```csharp
        [Fact]
        public void T_R9_11_FindPositionForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("acc", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
        }
```
Replace with:
```csharp
        [Fact]
        public void T_R9_11_TryFindPositionForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("TryFindPositionForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("acc", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
            Assert.Equal("pos", parms[2].Name);
            Assert.True(parms[2].IsOut, "Third parameter must be out Position");
        }
```

### Verification
```powershell
# 1. Confirm method exists in CopyEngine.cs
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "TryFindPositionForInstrument"
# Expected: 1+ result (the method declaration)

# 2. Confirm old name is gone from production code
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "FindPositionForInstrument"
# Expected: 0 results

# 3. Confirm tests updated
Select-String -Path src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "TryFindPositionForInstrument"
# Expected: 2 results (T_R9_10, T_R9_11)

# 4. Confirm no old name in test file
Select-String -Path src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern '"FindPositionForInstrument"'
# Expected: 0 results
```

---

## Cross-Cutting Concerns

### A-2 / A-6 Ordering Dependency

A-2 removes lines 7181-7395 from `CopyEngineTests.cs`. Lines 7364-7395 within that block contain tests that look for `FindPositionForInstrument`. A-6 adds `TryFindPositionForInstrument` with 3 parameters. The line removal by A-2 removes the misplaced tests. The canonical test file (`BwaveCycLaneAR9Tests.cs`) is updated by A-6.

**Mandatory sequence**: A-2 before A-6.

### A-6: No New Callers

`TryFindPositionForInstrument` is `private static`. It is NOT called from any production code in the current HEAD (the original caller in `SubmitBeStop` was removed along with the method). The method is introduced as an available helper for future use, matching the TA-R9 architect's design intent. Tests verify its existence and signature.

**Decision**: If Director decides that `SubmitBeStop` should be updated to call `TryFindPositionForInstrument` instead of `FindBePosition`, that is a separate ticket. This plan does NOT change `SubmitBeStop`.

### Return-Null Audit (SCAN-03 compliance)

After A-6, `FindPositionForInstrument` (null-returning) is absent from production code. `TryFindPositionForInstrument` returns `bool`, no null. The existing `FindBePosition` at lines 1119-1131 still returns `null` (return null at line 1130) -- this is a pre-existing site that is NOT newly introduced by this repair. Director accepted this via JS-002 footnote in TA-R9 verify.

---

## 7-Scan Checklist

After all 6 tickets applied, the following scans MUST pass:

### SCAN-01: lock() -- Zero

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs, src\PropTraderTools\CopyEngineTests.cs, src\PropTraderTools\Tests\BwaveCycLaneAR9Tests.cs -Pattern "lock\s*\(" | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: 0 results.

### SCAN-02: async void -- Zero

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs -Pattern "async void " | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: 0 results.

### SCAN-03: return null -- 0 NEW

```powershell
# Pre-existing null returns are accepted; verify count does not increase vs baseline
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "return null" | Where-Object { $_.Line.Trim() -notmatch "^//" } | Measure-Object | Select-Object Count
```
**Expected**: Count unchanged from pre-repair baseline (A-6 adds `TryFindPositionForInstrument` which does NOT use `return null`). The `pos = null` assignment is NOT a `return null`; it is a parameter assignment before an early return.

### SCAN-04: throw new -- 0 NEW

```powershell
Select-String -Path src\PropTraderTools\CopyEngine.cs, src\PropTraderTools\TradeCopierPanel.cs -Pattern "throw new " | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Expected**: Pre-existing 2 (TradeCopierWindow.cs:871, B42Tests.cs:72). Zero in repair-modified files.

### SCAN-05: build -- 0 errors

```powershell
dotnet build src\PropTraderTools\PropTraderTools.csproj 2>&1 | Select-String "error|Error" | Where-Object { $_ -notmatch "0 Error" }
```
**Expected**: 0 errors, 0 new warnings.

### SCAN-06: ASCII -- 0 non-ASCII in repair scope

```powershell
# Verify A-1 fixed: no \u25B[23] in buffered buttons area
Select-String -Path src\PropTraderTools\TradeCopierPanel.cs -Pattern "\\u25B[23]" | Where-Object { $_.LineNumber -ge 1130 -and $_.LineNumber -le 1400 }
```
**Expected**: 0 results.

### SCAN-07: dotnet test -- 0 NEW failures

```powershell
dotnet test src\PropTraderTools\PropTraderTools.csproj --no-build 2>&1 | Select-String "Failed:"
```
**Expected**: Failed count = 80 (pre-existing accepted failures). No new failures from A-1 through A-6.

---

## Component Summary

| Ticket | Category | File(s) | Type | Lines |
|--------|----------|---------|------|-------|
| A-1 | DNA | TradeCopierPanel.cs | 12x string replace | 1147-1356 |
| A-2 | MECHANICAL | CopyEngineTests.cs | Block delete | 7181-7395 |
| A-3 | MECHANICAL | CopyEngineTests.cs, BwaveCycLaneAR9Tests.cs | Body rewrite (2 tests) | ~7352, 146-153 |
| A-4 | CONFIRM-NOOP | CopyEngineTests.cs | Verify + document | -- |
| A-5 | CONFIRM-NOOP | TradeCopierPanel.cs | Verify + document | -- |
| A-6 | LOGIC-BUG | CopyEngine.cs, BwaveCycLaneAR9Tests.cs | Add method + update 2 tests | after line 1131 |

**Total source files touched**: 4
- `src/PropTraderTools/TradeCopierPanel.cs` (A-1)
- `src/PropTraderTools/CopyEngineTests.cs` (A-2, A-3)
- `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` (A-3, A-6)
- `src/PropTraderTools/CopyEngine.cs` (A-6)

**Total doc files produced**: 1 (this plan)
**No .cs files written by architect** (protocol mandate).

---

## NT8 API Notes

- `TryFindPositionForInstrument` reads `acc.Positions` -- NT8 AddOnBase read-only enumeration; no lock required (JS-021 compliant per existing `FindBePosition` precedent).
- `out NinjaTrader.Cbi.Position` -- standard .NET out parameter pattern; no NT8 API interaction for the parameter binding itself.
- No `CreateOrder`, `AtmStrategyCreate`, `AtmStrategyChangeStopTarget` involved.

---

*Plan status: REVIEW_PENDING -- awaiting ptt-plan-reviewer.*
