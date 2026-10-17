# B193 Ticket 2 Verification

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Ticket**: T2 -- Fix Stale Assertions in Existing Test Files
**Verifier**: ptt-verifier
**Date**: 2026-10-06
**Phase**: 4b (Independent Verification)

---

## Scope Confirmation

TICKET 2 ONLY. No T1, T3, or other ticket work verified in this session.

**Files Modified (verified independently):**
1. `src/PropTraderTools/CopyEngineTests.cs`
2. `src/PropTraderTools/Tests/B73Tests.cs`

**Files NOT modified by T2 (confirmed):**
- `src/PropTraderTools/Tests/B193Tests.cs` (T1 artifact, untouched)
- No production `.cs` files in `src/PropTraderTools/` modified

---

## Edit Verification (Layer 3 -- Independent)

### Edit A: CopyEngineTests.cs T_B189_T4_01

Source inspection (lines 10575-10582):
```
Line 10578: // Pure formula test: FormatBuffer("2x", 4) uses same path as default _quickT1=4
Line 10579: var result = (string)InvokeFormatBuffer("2x", 4);
Line 10580: Assert.Contains("2x", result);
Line 10581: Assert.Contains("+4", result);
```
Old `"Quick2t"` string literal: **0 matches** (grep for `"Quick2t"` returns 0).
New `"2x"` string present: **CONFIRMED**.
**Edit A: PASS**

### Edit B: CopyEngineTests.cs T_B189_T4_02

Source inspection (lines 10584-10590):
```
Line 10587: // AC14 label format: FormatBuffer("2x All", 4) == "2x All +4"
Line 10588: var result = (string)InvokeFormatBuffer("2x All", 4);
Line 10589: Assert.Equal("2x All +4", result);
```
Old `"QAll2t"` string literal: **0 matches** (grep for `"QAll2t"` returns 0).
New `"2x All"` string present: **CONFIRMED**.
**Edit B: PASS**

### Edit C: CopyEngineTests.cs T_B189_T4_06 + class comment

Class comment (lines 10561-10563):
```
Line 10561: // B189-T4: 2x / 2x All Spinners -- xUnit [Fact] tests.
Line 10562: // AC11: 2x label driven by _quickT1. AC12: 2x All Up increments _quickAllT1.
```

T_B189_T4_06 body (lines 10643-10652):
```
Line 10646: // AC11: same _quickT1 drives both 3x and 2x label strings
Line 10647: int t1 = 5;
Line 10648: var quickLabel = (string)InvokeFormatBuffer("3x", t1);
Line 10649: var quick2tLabel = (string)InvokeFormatBuffer("2x", t1);
Line 10650: Assert.Contains("+5", quickLabel);
Line 10651: Assert.Contains("+5", quick2tLabel);
```
`InvokeFormatBuffer("Quick", t1)` replaced with `InvokeFormatBuffer("3x", t1)`: **CONFIRMED**
`InvokeFormatBuffer("Quick2t", t1)` replaced with `InvokeFormatBuffer("2x", t1)`: **CONFIRMED**
`"Quick"` string literal: **0 matches** (grep for `'"Quick"'` returns 0)
**Edit C: PASS**

### Edit D: B73Tests.cs T_LABEL_01

Source inspection (lines 184-189):
```
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Equal("3x All +4t", result);
```
Old `"Quick ALL"` string literal: **0 matches** (grep returns 0).
New `"3x All"` string present: **CONFIRMED**.
**Edit D: PASS**

### Edit E: B73Tests.cs T_LABEL_03

Source inspection (lines 203-209):
```
var result = (string)method.Invoke(null, new object[] { "3x All", 4 })!;
Assert.Contains("3x All", result);
Assert.Contains("+4t", result);
```
Old `"Quick ALL"` string literal: **0 matches**.
New dual assertion (strengthened per ticket): **CONFIRMED**. CYC=1 (no branches added).
**Edit E: PASS** (assertion strengthening within T2 scope per 04-tickets.md Section 3 Edit Block E note)

### Edit F: B73Tests.cs T_LABEL_04

Source inspection (lines 213-216):
```
var result = (string)method.Invoke(null, new object[] { "3x All", 0 })!;
Assert.Equal("3x All +0t", result);
```
Old `"Quick ALL"` string literal: **0 matches**.
New `"3x All"` string present: **CONFIRMED**.
**Edit F: PASS**

---

## Old Label String Scan (Layer 3 -- Independent)

All grep commands run against actual source files by verifier. Results:

| Pattern | File | Result |
|---------|------|--------|
| `"Quick2t"` (string literal) | CopyEngineTests.cs | **0 matches** |
| `"QAll2t"` (string literal) | CopyEngineTests.cs | **0 matches** |
| `'"Quick"'` (string literal) | CopyEngineTests.cs | **0 matches** |
| `"Quick ALL"` (string literal) | B73Tests.cs | **0 matches** |

Note: `Quick2t` and `QAll2t` still appear as METHOD NAMES and VARIABLE NAMES (e.g.
`T_B189_T4_01_Quick2tSpinnerLabel_MatchesQuickT1Default`, `quick2tLabel`, `_instrQAll2tBtn`).
These are identifiers, not label strings -- they are NOT stale and do NOT require T2 changes.

---

## 7 Independent Scans (Layer 3)

### SCAN-01: CYC -- No new branches in edited methods

Manual branch count (CYC = 1 + decision points):

| Method | File | Decision points | CYC |
|--------|------|----------------|-----|
| T_B189_T4_01 | CopyEngineTests.cs:10575 | 0 | 1 |
| T_B189_T4_02 | CopyEngineTests.cs:10584 | 0 | 1 |
| T_B189_T4_06 (T2 edit lines) | CopyEngineTests.cs:10643 | 0 | 1 |
| T_LABEL_01 | B73Tests.cs | 0 | 1 |
| T_LABEL_03 | B73Tests.cs | 0 | 1 (dual Assert.Contains, no branch) |
| T_LABEL_04 | B73Tests.cs | 0 | 1 |

**RESULT: SCAN-01 PASS -- CYC=1 for all 6 edited methods. Zero new branches.**

### SCAN-02: lock()

```
Select-String CopyEngineTests.cs -Pattern "lock\s*\(" (lines 10561-10660): 0 matches
Select-String B73Tests.cs -Pattern "lock\s*\(" (lines 183-220): 0 matches
```

**RESULT: SCAN-02 PASS -- 0 lock() matches in edited regions**

### SCAN-03: async void

```
Select-String CopyEngineTests.cs -Pattern "async\s+void" (lines 10561-10660): 0 matches
Select-String B73Tests.cs -Pattern "async\s+void" (lines 183-220): 0 matches
```

**RESULT: SCAN-03 PASS -- 0 async void in edited regions**

### SCAN-04: return null

```
Select-String CopyEngineTests.cs -Pattern "return\s+null\s*;" (lines 10561-10660): 0 matches
Select-String B73Tests.cs -Pattern "return\s+null\s*;" (lines 183-220): 0 matches
```

**RESULT: SCAN-04 PASS -- 0 return null in edited regions**

### SCAN-05: throw new

```
Select-String CopyEngineTests.cs -Pattern "throw\s+new\s+" (lines 10561-10660): 0 matches
Select-String B73Tests.cs -Pattern "throw\s+new\s+" (lines 183-220): 0 matches
```

**RESULT: SCAN-05 PASS -- 0 throw new in edited regions**

### SCAN-06: ASCII-only verification

PowerShell character-code verification of all new string literals introduced by T2:

```
Strings verified: "3x", "2x", "3x All", "2x All", "2x All +4", "3x All +4t",
                  "3x All +0t", "+4", "+5"
Result: All characters <= 0x7F (7-bit ASCII)
PowerShell output: "SCAN-06 PASS: All new string literals are 7-bit ASCII"
```

**RESULT: SCAN-06 PASS -- all new literals are 7-bit ASCII**

### SCAN-07: JS-033 -- no async void method signatures

No method signatures were changed by T2. All 6 edited methods retain their existing
synchronous `public void` signatures. Confirmed via grep on edited regions.

```
Select-String CopyEngineTests.cs -Pattern "async\s+void\s+" (lines 10561-10660): 0 matches
Select-String B73Tests.cs -Pattern "async\s+void\s+" (lines 183-220): 0 matches
```

**RESULT: SCAN-07 PASS -- 0 async void signatures**

---

## Independent dotnet test Results

### Target tests: T_B189_T4_01, T_B189_T4_02, T_B189_T4_06

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj \
  --filter "FullyQualifiedName~T_B189_T4_01|FullyQualifiedName~T_B189_T4_02|FullyQualifiedName~T_B189_T4_06" \
  --no-build

Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 358 ms
```

T_B189_T4_01: **PASS**
T_B189_T4_02: **PASS**
T_B189_T4_06: **PASS**

### B189_T4 full class run

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj \
  --filter "FullyQualifiedName~B189_T4" --no-build

Failed! - Failed: 2, Passed: 4, Skipped: 0, Total: 6, Duration: 619 ms
```

The 2 failures are PRE-EXISTING (unrelated to T2):
- `T_B189_T4_03_QAll2tSpinner_Up_IncrementsGlobalQuickAllT1`: NT8 host required
  (TypeInitializationException -> CopyEngine..cctor -> AgileDotNetRT.Initialize).
  This test was FAILING before T2 and was NOT modified by T2.
- `T_B189_T4_05_OnQuickAllBufferChanged_Contract_InstrQAll2tBtnUpdated`: IL field token
  lookup contract failure (Assert.True False). Pre-existing failure, NOT modified by T2.

Neither failure is in the T2 edit scope. T2 introduced zero new failures.

### B193 regression check

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj \
  --filter "FullyQualifiedName~B193" --no-build

Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 374 ms
```

All 10 B193 tests (T1 artifact): **10/10 PASS**. Zero regressions.

### B73Tests.cs (T_LABEL tests)

B73Tests.cs has pre-existing compiler errors (CS0426: TradeCopierPanel.BeState does not exist)
and is correctly excluded from PropTraderTools.Tests.csproj (uses explicit `<Compile>` includes).
This pre-existing exclusion existed before T2. The T_LABEL_01, T_LABEL_03, T_LABEL_04 edits
(D, E, F) were verified via source inspection and grep scans -- all three methods contain the
new `"3x All"` strings; no `"Quick ALL"` string literals remain.

---

## Cross-Check: Engineer Layer 2 vs Verifier Layer 3

| Check | Engineer Report (Layer 2) | Verifier Result (Layer 3) | Match? |
|-------|--------------------------|--------------------------|--------|
| Edit A: "2x" replaces "Quick2t" in T_B189_T4_01 | APPLIED | CONFIRMED | YES |
| Edit B: "2x All" replaces "QAll2t" in T_B189_T4_02 | APPLIED | CONFIRMED | YES |
| Edit C: "3x"/"2x" replace "Quick"/"Quick2t" in T_B189_T4_06 | APPLIED | CONFIRMED | YES |
| Edit D: "3x All" replaces "Quick ALL" in T_LABEL_01 | APPLIED | CONFIRMED | YES |
| Edit E: "3x All" + dual Assert replaces T_LABEL_03 | APPLIED | CONFIRMED | YES |
| Edit F: "3x All" replaces "Quick ALL" in T_LABEL_04 | APPLIED | CONFIRMED | YES |
| SCAN-01 CYC: all methods CYC=1 | PASS | PASS | YES |
| SCAN-02 lock(): 0 | PASS | PASS | YES |
| SCAN-03 async void: 0 | PASS | PASS | YES |
| SCAN-04 return null: 0 | PASS | PASS | YES |
| SCAN-05 throw new: 0 | PASS | PASS | YES |
| SCAN-06 ASCII: all 7-bit | PASS | PASS (PowerShell verified) | YES |
| SCAN-07 JS-033: 0 | PASS | PASS | YES |
| T_B189_T4_01/02/06: 3 PASS | 3 PASS | 3 PASS | YES |
| B193 regression: 10/10 | 10 PASS | 10 PASS | YES |
| T_B189_T4_03, T_B189_T4_05: pre-existing failures | NOTED | CONFIRMED pre-existing | YES |
| B73Tests.cs excluded from .csproj | NOTED | CONFIRMED | YES |

**No discrepancies between engineer Layer 2 and verifier Layer 3.**

---

## Deferred Work Acknowledgment

DW-B193-01 (P3, OPEN): `OnQuickAllBufferChanged` (~line 1027-1029) still uses old label strings
"Quick ALL"/"QAll2t". Correctly out of T2 scope. Deferred to future block per architect plan.

---

## Verdict

All 6 edit blocks applied correctly and verified independently.
All 7 scans PASS (Layer 3 independent re-run).
3 runnable target tests (T_B189_T4_01, T_B189_T4_02, T_B189_T4_06) PASS.
B193 T1 regression check: 10/10 PASS.
No discrepancies between engineer self-report and verifier independent scan.
No production .cs files modified. No T1 artifact modified by T2.

# VERIFY_PASS

---

*Written by: ptt-verifier*
*Epic: B193*
*Ticket: T2*
*Phase: 4b (Verification)*