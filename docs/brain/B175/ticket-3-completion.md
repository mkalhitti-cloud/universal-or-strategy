# Ticket Completion — B175-3
# Phase 4a — Engineer
# Ticket: B175-3 — New xUnit Tests (CopyEngineTests.cs)
# Date: 2025-01-27

---

## What Was Implemented

### File Modified
`src/PropTraderTools/CopyEngineTests.cs`

### Insertion Location
Lines 10310–10313 (end of file) replaced using `apply_diff` with the `B175AtrCalcTests` class
inserted before the namespace closing brace.

**BEFORE** (lines 10310–10313):
```csharp
        }
    }

}
```

**AFTER** — `B175AtrCalcTests` class inserted before namespace closing brace:
- New class starts at line 10312 (after the blank line following `B174AtrCalcTests` close)
- New class ends with a closing `}` followed by a blank line and the namespace `}`
- Total lines added: ~111

### New Class Summary
`public class B175AtrCalcTests` containing 4 [Fact] methods:

| Method | Purpose | CYC |
|---|---|---|
| `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` | 29 bars, period=14, constant TR=2.0 → Wilder's ATR must == 2.0 | 2 (base+for) |
| `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` | 29-bar spike (TR=1→3) Wilder's result must exceed 15-bar simple avg | 3 (base+for+for) |
| `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg` | 15-bar (len<2*period+1), period=14, constant TR=2.0 → simple avg = 2.0 | 2 (base+for) |
| `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` | B174 regression guard: exact H/L/C inputs, period=3, ATR=7/3 | 1 (straight-line) |

All methods call `AtrCalc.Compute(double[], double[], double[], int)` (internal static, no NT8 runtime).

---

## 7-Scan Results

### SCAN-01: [Fact] count in file
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "\[Fact\]").Count
Result: 251
```
Previous count was 247 (pre-B175-3). Delta = +4. PASS (4 new [Fact] methods confirmed).

### SCAN-02: B175AtrCalcTests class present
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "B175AtrCalcTests").Count
Result: 2
```
Count >= 1 (class declaration + comment header). PASS.

### SCAN-03: No lock() in new code
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\(").Count
Result: 5
```
All 5 hits are at lines 9223, 9350, 9471, 9885, 9970 — all pre-existing comments containing
"no lock()". Zero lock() statements in new B175AtrCalcTests class (starts at line 10312+). PASS.

### SCAN-04: ASCII-only in new code
```powershell
$content = Get-Content "src/PropTraderTools/CopyEngineTests.cs" -Raw
$nonAscii = [regex]::Matches($content, '[^\x00-\x7F]')
$nonAscii.Count
Result: 0
```
Zero non-ASCII characters in entire file. PASS.

### SCAN-05: No FontFamily in new code
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FontFamily").Count
Result: 1
```
Single hit at line 10260 — pre-existing comment "No FontFamily". Zero FontFamily in new B175 code. PASS.

### SCAN-06: No hex colors (#RRGGBB) in new code
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "#[0-9A-Fa-f]{6}").Count
Result: 0
```
Zero hex color literals in entire file. PASS.

### SCAN-07: No DateTime.Now in new code
```
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "DateTime\.Now[^U]").Count
Result: 1
```
Single hit at line 10260 — pre-existing comment "No DateTime.Now". Zero DateTime.Now in new B175 code. PASS.

### Scan Summary

| Scan | Check | Result | Status |
|---|---|---|---|
| SCAN-01 | [Fact] count increased by 4 | 251 (delta=+4) | PASS |
| SCAN-02 | B175AtrCalcTests class present | 2 occurrences | PASS |
| SCAN-03 | No lock() in new code | 0 new in B175 class | PASS |
| SCAN-04 | ASCII-only | 0 non-ASCII chars | PASS |
| SCAN-05 | No FontFamily in new code | 0 new in B175 class | PASS |
| SCAN-06 | No #RRGGBB hex colors | 0 | PASS |
| SCAN-07 | No DateTime.Now in new code | 0 new in B175 class | PASS |

**All 7 scans: PASS**

---

## dotnet build Output

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
Build succeeded.
    0 Error(s)
```

Pre-existing CS0436 and CS8632 warnings only (all pre-existing, unrelated to B175-3). **0 errors. PASS.**

---

## dotnet test Output

### B175 tests only (filter):
```
dotnet test ... --filter "FullyQualifiedName~B175AtrCalcTests"

Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR

Test Run Successful.
Total tests: 4
     Passed: 4
Total time: 2.7942 Seconds
```

### Full suite:
```
dotnet test ... (full suite)
Failed!  - Failed: 8, Passed: 231, Skipped: 358, Total: 597, Duration: 897 ms
```

**8 pre-existing failures** in DwB170Tests, DwB16702SlateReadyTests, DwB168_02Tests,
DwB15701SlateReadyTests — all pre-existing, unrelated to B175-3. **0 new failures introduced.**

4 new passes from B175AtrCalcTests confirmed. PASS.

---

## AtrCalc.Compute Signature Verification

`src/PropTraderTools/AtrCalc.cs` verified before implementation:
```csharp
internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
```
B175 Wilder's smoothing already applied (B175-1 complete). All 4 tests exercise this overload. PASS.

---

## Jane Street Compliance

| Rule | Status | Notes |
|---|---|---|
| JS-001 (try/catch / safe return) | N/A | Test methods; xUnit captures exceptions as failures |
| JS-021 (no lock) | PASS | Zero lock() statements in new class |
| JS-023 (no volatile double) | PASS | No fields declared in test class |
| ASCII-only | PASS | All text 7-bit ASCII; "Wilder's" uses straight apostrophe 0x27 |
| CYC <= 8 | PASS | T1-01: 2, T1-02: 3, T1-03: 2, T1-04: 1 |
| No DateTime.Now | PASS | Not used |
| No hex colors | PASS | Not used |
| No FontFamily | PASS | Not used |
| No NT8 mock | PASS | Pure-array tests only |
| apply_diff used | PASS | One SEARCH/REPLACE block per ticket spec |

---

## BUILD_PASS
