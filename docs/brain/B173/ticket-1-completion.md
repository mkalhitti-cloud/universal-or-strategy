# B173 -- AtrWidget Live ATR Fix
# Ticket 1 Completion Report
# Phase: 4a -- ptt-engineer
# Status: BUILD_PASS

---

## Files Written

| File | Action |
|------|--------|
| `src/PropTraderTools/AtrCalc.cs` | NEW -- pure static ATR helper (both Compute overloads) |
| `src/PropTraderTools/CopyEngineTests.cs` | APPEND -- B173AtrCalcTests class (2 [Fact] tests) |
| `src/PropTraderTools/PropTraderTools.Tests.csproj` | EDIT -- added `<Compile Include="AtrCalc.cs" />` |

### Note on .csproj edit
The test project uses `EnableDefaultCompileItems=false` with explicit `<Compile>` includes.
`AtrCalc.cs` was not auto-discovered; adding it to the csproj was required for build to succeed.
The `internal static class AtrCalc` is compiled into the same assembly as the tests, making
`internal` members accessible without `InternalsVisibleTo`.

---

## What Was Implemented

### AtrCalc.cs (new file)

`internal static class AtrCalc` with two overloads:

**Overload A -- pure math (testable, no NT8 runtime):**
- Guards: null arrays -> 0.0; period < 1 -> 0.0; array length < period+1 -> 0.0
- Formula: iterate i=1..period; TR[i] = Max(H-L, |H-pc|, |L-pc|) where pc=closes[i-1]
- ATR = sum(TR) / period
- JS-001: entire body in try/catch(Exception) -> return 0.0
- CYC = 5 (null-guard, period-guard, length-guard, for-loop, try/catch)

**Overload B -- NT8 runtime:**
- Guards: cc==null; cc.BarsArray==null||Count==0; bars==null||Count<period+1
- Extracts highs/lows/closes arrays from cc.BarsArray[0].Bars using GetHigh/GetLow/GetClose(barsAgo)
- Array ordering: k=0 -> barsAgo=period (oldest), k=period -> barsAgo=0 (newest)
- Delegates to Overload A
- JS-001: entire body in try/catch(Exception) -> return 0.0
- CYC = 4 (null-guard x3, for-loop, try/catch)

### CopyEngineTests.cs (appended)

New class `B173AtrCalcTests` with 2 xUnit [Fact] tests:

| Test ID | Method | Asserts |
|---------|--------|---------|
| T-B173-T1-01 | `AtrCalc_Compute_ReturnsZero_WhenInsufficientBars` | `Compute(new double[3], new double[3], new double[3], 14)` == 0.0 |
| T-B173-T1-02 | `AtrCalc_Compute_CorrectValue_KnownBars` | ATR(3) of known OHLC == 5.5/3.0 |

**T1-02 math verification (hand-computed):**
- highs=[2.0,3.0,4.0,3.0], lows=[1.0,2.0,2.0,1.0], closes=[1.5,2.5,3.0,2.0], period=3
- TR[1]: max(3-2=1.0, |3-1.5|=1.5, |2-1.5|=0.5) = 1.5
- TR[2]: max(4-2=2.0, |4-2.5|=1.5, |2-2.5|=0.5) = 2.0
- TR[3]: max(3-1=2.0, |3-3.0|=0.0, |1-3.0|=2.0) = 2.0
- ATR(3) = (1.5+2.0+2.0)/3 = 5.5/3.0 CORRECT

---

## Jane Street Rules Compliance

| Rule | Status |
|------|--------|
| JS-001: no throw, return 0.0 on exception | PASS -- both overloads wrapped in try/catch |
| JS-021: no lock() | PASS -- static class, no instance state |
| JS-023: volatile only bool/int | PASS -- no fields in static class |
| NT8-003: no volatile double | PASS -- no fields at all |
| ASCII-only: no Unicode string literals | PASS -- all ASCII |

---

## 7-Scan Results

| # | Scan Command | Result | Notes |
|---|-------------|--------|-------|
| SCAN-01 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\("` | **0 matches** | No lock in AtrCalc.cs or any file |
| SCAN-02 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "volatile double"` | **0 actual declarations** | 3 comment-line hits only (no `volatile double` field declarations anywhere) |
| SCAN-03 | Non-ASCII byte scan on all .cs files | **0 non-ASCII bytes** | All files ASCII-clean |
| SCAN-04 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "AtrSizingEngine"` | **> 0 matches (expected at T1)** | AtrSizingEngine.cs still exists; deletion deferred to T3 |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` | **0 errors** | Build succeeded; warnings are pre-existing CS0436 assembly conflicts |
| SCAN-06 | `dotnet test` (B173AtrCalcTests filter) | **2/2 passed** | T-B173-T1-01: PASS, T-B173-T1-02: PASS |
| SCAN-07 | N/A at T1 | **SKIP** | ptt-sync required at T3 only |

### SCAN-04 detail (expected at T1):
AtrSizingEngine references still exist in:
- `AtrSizingEngine.cs` (the file itself)
- `TradeCopierPanel.cs` lines 3079, 3168, 3211
- `TradeCopierAddOn.cs` line 397
- `AtrCalc.cs` line 3 (comment only: "Replaces AtrSizingEngine")
Zero-match requirement deferred to T3.

### SCAN-06 detail:
Pre-existing failures (8 tests) unrelated to B173:
- `DwB168_02Tests` (4 failures) -- pre-existing
- `DwB170Tests` (2 failures) -- pre-existing
- `DwB15701SlateReadyTests` (1 failure) -- pre-existing
- `DwB170Tests` (5 skipped) -- pre-existing
None of these are B173 tests. My 2 new tests pass cleanly.
Total run: Failed: 8 (pre-existing), Passed: 224+2=226, Skipped: 358.

---

## BUILD_PASS
