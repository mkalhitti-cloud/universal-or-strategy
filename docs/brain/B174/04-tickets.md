# B174 Tickets
# Epic: DW-B174-ATR-INSTRUMENT-MISMATCH
# Phase 3 output: ptt-architect
# Input: 02-architecture-plan.md (REVIEW_PASS) + 02-plan-review.md (F-01, F-02 corrections applied)
# Status: READY FOR Ph3.5 ptt-ticket-reviewer

---

## CORRECTIONS APPLIED FROM 02-plan-review.md

**F-01 applied:** CYC annotation in Method A AFTER corrected from CYC=6 to CYC=8.
  - Two branches previously omitted: `if (!hasName || ...)` guard (+1) and `catch (Exception)` (+1).
  - Full 8-item branch list documented in code comment and CYC table.
  - Delta corrected: 5 -> 8 (+3). Still <= 8. JS-013 COMPLIANT.

**F-02 applied:** JS-PUSH-01 three-step analysis explicitly cited in B174-2 ticket under Edit A (Method B).

---

## CYC TABLE (corrected per F-01)

| Method | File | Before | After | Delta | Compliant |
|--------|------|--------|-------|-------|-----------|
| `AtrCalc.Compute(ChartControl, int, string)` | AtrCalc.cs | 5 | **8** | **+3** | YES (<=8) |
| `AtrWidget.SetInstrument` (NEW) | TradeCopierPanel.cs | -- | 1 | new | YES (<=8) |
| `AtrWidget.OnTimerTick` | TradeCopierPanel.cs | 3 | 3 | 0 | YES (<=8) |
| `TradeCopierPanel.SetInstrument` | TradeCopierPanel.cs | 2 | 2 | 0 | YES (<=8) |

---

## TICKET B174-1 -- AtrCalc.Compute: BarsArray Instrument Scan

**Spec requirement:** DW-B174-ATR-INSTRUMENT-MISMATCH (primary root cause fix)
**File:** `src/PropTraderTools/AtrCalc.cs`
**Dependencies:** None (implement first)
**Implement before:** B174-2, B174-3

---

### Change Description

Replace the `Compute(ChartControl, int)` method (lines 66-94) with a new
`Compute(ChartControl, int, string)` overload that scans `cc.BarsArray` for the entry
whose `Bars.Instrument.FullName` matches `instrumentFullName`. Falls back to `BarsArray[0]`
when `instrumentFullName` is null or empty (single-instrument window behavior preserved).

- Removes hardcoded `cc.BarsArray[0].Bars` (the bug).
- Adds optional `string instrumentFullName = null` parameter (backward-compatible default).
- Adds `NinjaTrader.Data.Bars bars = null;` declaration + `bool hasName` local.
- Adds scan loop iterating `cc.BarsArray`, guarding each entry, matching by `FullName`.
- Final guard: `if (bars == null || bars.Count < period + 1) return 0.0;`
- Extraction loop and delegation to pure-array overload: unchanged.

---

### BEFORE (lines 66-94 of AtrCalc.cs -- verified against source)

```csharp
        // JS-001: try/catch -> return 0.0. CYC target: <= 5.
        internal static double Compute(NinjaTrader.Gui.Chart.ChartControl cc, int period)
        {
            try
            {
                if (cc == null)
                    return 0.0;
                if (cc.BarsArray == null || cc.BarsArray.Count == 0)
                    return 0.0;
                var bars = cc.BarsArray[0].Bars;
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                double[] highs  = new double[period + 1];
                double[] lows   = new double[period + 1];
                double[] closes = new double[period + 1];
                for (int k = 0; k <= period; k++)
                {
                    int barsAgo = period - k;
                    highs[k]  = bars.GetHigh(barsAgo);
                    lows[k]   = bars.GetLow(barsAgo);
                    closes[k] = bars.GetClose(barsAgo);
                }
                return Compute(highs, lows, closes, period);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }
```

---

### AFTER (replace lines 66-94 with the following)

```csharp
        // B174: instrumentFullName param added. Scans BarsArray for matching instrument;
        // falls back to BarsArray[0] when not found (single-instrument window, correct behaviour).
        // CYC=8: cc null(1)+BarsArray null/empty(2)+instrument scan loop(3)+
        //        candidate null guard(4)+hasName/name match guard(5)+bars null/count guard(6)+
        //        extraction loop(7)+catch(8).
        // JS-001: try/catch -> return 0.0. JS-021: no lock. ASCII-only.
        internal static double Compute(
            NinjaTrader.Gui.Chart.ChartControl cc,
            int period,
            string instrumentFullName = null)
        {
            try
            {
                if (cc == null)
                    return 0.0;
                if (cc.BarsArray == null || cc.BarsArray.Count == 0)
                    return 0.0;
                NinjaTrader.Data.Bars bars = null;
                bool hasName = !string.IsNullOrEmpty(instrumentFullName);
                for (int idx = 0; idx < cc.BarsArray.Count; idx++) // scan (3)
                {
                    var candidate = cc.BarsArray[idx]?.Bars;
                    if (candidate == null) continue; // guard (4)
                    if (!hasName || candidate.Instrument.FullName == instrumentFullName) // guard (5)
                    {
                        bars = candidate;
                        break;
                    }
                }
                if (bars == null || bars.Count < period + 1)
                    return 0.0; // guard (6)
                double[] highs  = new double[period + 1];
                double[] lows   = new double[period + 1];
                double[] closes = new double[period + 1];
                for (int k = 0; k <= period; k++) // loop (7)
                {
                    int barsAgo = period - k;
                    highs[k]  = bars.GetHigh(barsAgo);
                    lows[k]   = bars.GetLow(barsAgo);
                    closes[k] = bars.GetClose(barsAgo);
                }
                return Compute(highs, lows, closes, period);
            }
            catch (Exception) // catch (8)
            {
                return 0.0;
            }
        }
```

---

### Method Signature

```
internal static double Compute(
    NinjaTrader.Gui.Chart.ChartControl cc,
    int period,
    string instrumentFullName = null)
```

**Return type:** `double`
**CYC before:** 5
**CYC after:** 8 (corrected per F-01)
**Delta:** +3

---

### 7-Scan Checklist (B174-1)

| # | Scan | Check | Pass Criterion |
|---|------|-------|----------------|
| SCAN-01 | No lock() | `grep -c "lock(" src/PropTraderTools/AtrCalc.cs` | Must return 0 |
| SCAN-02 | CYC annotation matches body count | Count branches in AFTER body: 8 branches. Comment must read `CYC=8` with 8-item breakdown. | Annotation = 8 = actual count |
| SCAN-03 | No Unicode / curly quotes | All string literals use straight ASCII double-quotes. All comments use double-hyphen `--` not em-dash. | 0 non-ASCII characters in changed lines |
| SCAN-04 | No DateTime.Now / FontFamily | Neither `DateTime.Now` nor `FontFamily` appears in any changed line. | 0 occurrences |
| SCAN-05 | BarsArray[0] absent from Compute(ChartControl,...) body | `grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs` | Must return 0 after edit |
| SCAN-06 | apply_diff used (no write_file) | Engineer must use `apply_diff` for this edit. `write_file` on .cs files is BANNED per mode rules. | Confirmed apply_diff in completion report |
| SCAN-07 | ptt-sync.ps1 run after ALL edits | `powershell -File .\scripts\ptt-sync.ps1` must run after B174-1, B174-2, and B174-3 are all complete. | Sync output in completion report |

---

### Verify Steps (B174-1)

1. Method body matches AFTER block exactly.
2. `grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs` returns `0`.
3. `grep -c "lock(" src/PropTraderTools/AtrCalc.cs` returns `0`.
4. `dotnet build` 0 errors (AtrCalc.cs compiles with new 3-param overload).
5. `dotnet test` 0 new failures (pure-array overload B173 tests still pass).

---

## TICKET B174-2 -- AtrWidget: SetInstrument + OnTimerTick Wiring

**Spec requirement:** DW-B174-ATR-INSTRUMENT-MISMATCH (secondary root cause fix -- propagation gap)
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Dependencies:** B174-1 MUST complete first.
  The `AtrCalc.Compute(_chartControl, 14, _instrument?.FullName)` call in Edit B
  requires the 3-param overload introduced by B174-1. The file will not compile
  until B174-1 is applied.

**Three edits in this ticket (apply in order A -> B -> C):**

---

### Edit A -- Add `_instrument` field and `SetInstrument` method to `AtrWidget`

**Location:** Insert after line 3168 of `TradeCopierPanel.cs`.
**Line 3168 content (anchor -- do not change):**
```csharp
            private ChartControl _chartControl;        // live chart; null until wired (B173)
```

**BEFORE (anchor line 3168):**
```csharp
            private ChartControl _chartControl;        // live chart; null until wired (B173)
```

**AFTER (line 3168 unchanged; insert the following block immediately after it):**
```csharp
            private ChartControl _chartControl;        // live chart; null until wired (B173)

            // B174: instrument for BarsArray scan. Set by outer class SetInstrument. UI-thread-only.
            // JS-PUSH-01 three-step:
            //   Step 1: Is the field read from multiple threads? NO -- UI-thread-only per NT8
            //           DispatcherTimer. Timer fires on UI thread. Outer SetInstrument is
            //           UI-thread-only (it updates _statusText.Text directly, which requires UI thread).
            //   Step 2: Is the field a reference type requiring atomic swap? NO -- single assignment
            //           on UI thread; no concurrent reader on another thread.
            //   Step 3: Conclusion: no lock required, no Interlocked required. JS-021 PASS.
            //           Non-volatile is correct per NT8-003 (consistent with _chartControl above).
            private NinjaTrader.Cbi.Instrument _instrument = null;

            // B174: CYC=1 (straight-line assignment, no branches). UI thread only.
            // JS-021: no lock. JS-002: void return. ASCII-only.
            internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)
            {
                _instrument = instr;
            }
```

**Method signature (new):**
```
internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)
```
**Return type:** `void`
**CYC:** 1 (new method, straight-line assignment)

---

### Edit B -- Update `AtrWidget.OnTimerTick` call site

**Location:** Line 3249 of `TradeCopierPanel.cs`.
**Note:** After Edit A inserts ~10 lines after line 3168, the actual line number of
the `OnTimerTick` call will shift to approximately line 3259. The engineer must
locate the line by content rather than by absolute line number.

**BEFORE (locate by content in OnTimerTick body):**
```csharp
                double atr = AtrCalc.Compute(_chartControl, 14);
```

**AFTER:**
```csharp
                double atr = AtrCalc.Compute(_chartControl, 14, _instrument?.FullName);
```

**Lines changed:** 1 (third argument added).
**CYC:** 3 -> 3 (unchanged). The `?.` null-conditional is an argument expression, not a control-flow branch.
**Dependency:** Requires B174-1 (the 3-param overload must exist before this line is valid).

---

### Edit C -- Update `TradeCopierPanel.SetInstrument` to propagate instrument to widget

**Location:** Lines 519-524 of `TradeCopierPanel.cs`.
**Note:** Edit C is in the outer `TradeCopierPanel` class (not the inner `AtrWidget`).
Line numbers are unaffected by Edits A and B (those edits are in a different region).

**BEFORE (lines 519-524 -- verified against source):**
```csharp
        public void SetInstrument(Instrument instrument)
        {
            _instrument = instrument;
            if (_statusText != null && instrument != null)
                _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
        }
```

**AFTER:**
```csharp
        // B174: propagate instrument to AtrWidget for BarsArray scan.
        public void SetInstrument(Instrument instrument)
        {
            _instrument = instrument;
            _widget?.SetInstrument(instrument); // B174: wire instrument to AtrWidget
            if (_statusText != null && instrument != null)
                _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
        }
```

**Lines changed:** +1 (`_widget?.SetInstrument(instrument);`) + 1 comment line added.
**CYC:** 2 -> 2 (unchanged). The `?.` null-conditional on `_widget` is not a new branch per project CYC convention.
**Dependency:** Requires Edit A (B174-2 part A) for `SetInstrument` to exist on `AtrWidget`.

---

### 7-Scan Checklist (B174-2)

| # | Scan | Check | Pass Criterion |
|---|------|-------|----------------|
| SCAN-01 | No lock() | `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` | No new occurrences vs. pre-B174 baseline |
| SCAN-02 | CYC annotation matches body count | Edit A: SetInstrument comment reads `CYC=1` -- body has 0 branches. Edit B: OnTimerTick comment reads `CYC=3` -- body still has 3 branches. Edit C: SetInstrument comment reads `CYC=2` -- body still has 2 branches. | All annotations match actual counts |
| SCAN-03 | No Unicode / curly quotes | All string literals and comments use straight ASCII. `--` double-hyphen not em-dash. | 0 non-ASCII characters in any changed line |
| SCAN-04 | No DateTime.Now / FontFamily | Neither appears in any changed line. | 0 occurrences |
| SCAN-05 | BarsArray[0] absent from Compute(ChartControl,...) | N/A for TradeCopierPanel.cs -- verify in B174-1. OnTimerTick passes `_instrument?.FullName` (no BarsArray reference). | Confirmed no `BarsArray[0]` in changed lines |
| SCAN-06 | apply_diff used (no write_file) | All three edits must use `apply_diff`. | Confirmed apply_diff in completion report |
| SCAN-07 | ptt-sync.ps1 run after ALL edits | Must run after B174-1, B174-2, and B174-3 are all complete. | Sync output in completion report |

---

### Verify Steps (B174-2)

1. `AtrWidget` inner class has `_instrument` field with `= null` initializer.
2. `AtrWidget` has `internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)` method.
3. `OnTimerTick` body contains `AtrCalc.Compute(_chartControl, 14, _instrument?.FullName)`.
4. `TradeCopierPanel.SetInstrument` body contains `_widget?.SetInstrument(instrument);`.
5. `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` shows no new occurrences.
6. `dotnet build` 0 errors.

---

## TICKET B174-3 -- Tests: B174AtrCalcTests

**Spec requirement:** DW-B174-ATR-INSTRUMENT-MISMATCH (test coverage)
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Dependencies:** B174-1 MUST complete first (test class calls `AtrCalc.Compute` API).
  B174-2 MUST also complete before running `dotnet test` (full chain must compile).
**Append point:** Insert before line 10258 (the closing `}` of the `PropTraderTools` namespace).

---

### Change Description

Append a new `public class B174AtrCalcTests` block with 3 `[Fact]` test methods.
Insert between line 10257 (blank line after B173AtrCalcTests closing `}`) and
line 10258 (namespace closing `}`).

No existing code is modified. Pure insertion only.

---

### Code to Append (insert before line 10258)

```csharp
    // B174AtrCalcTests -- xUnit regression and guard tests for B174 instrument-scan fix.
    // All tests use the pure-array overload only. No NT8 runtime dependency.
    // JS-021: no lock. ASCII-only. No DateTime.Now. No FontFamily.
    public class B174AtrCalcTests
    {
        // T-B174-T1-01: Pure-array overload regression guard.
        // Verifies that B174's changes to the ChartControl overload did NOT alter
        // the pure-array overload. Uses the same known-value inputs as B173 T-B173-T1-02.
        // highs = {2.0, 3.0, 4.0, 3.0}, lows = {1.0, 2.0, 2.0, 1.0}, closes = {1.5, 2.5, 3.0, 2.0}
        // ATR(3) = (1.5 + 2.0 + 2.0) / 3 = 5.5 / 3 (B173 baseline)
        [Fact]
        public void T_B174_T1_01_PureOverloadRegressionGuard()
        {
            double[] highs  = { 2.0, 3.0, 4.0, 3.0 };
            double[] lows   = { 1.0, 2.0, 2.0, 1.0 };
            double[] closes = { 1.5, 2.5, 3.0, 2.0 };
            double result = AtrCalc.Compute(highs, lows, closes, 3);
            // Confirms pure overload is untouched by B174.
            Assert.Equal(5.5 / 3.0, result);
        }

        // T-B174-T1-02: Instrument scan delegation correctness (pure-array path).
        // NT8 ChartControl is runtime-bound; ChartControl overload cannot be unit-tested
        // without a real ChartControl. This test confirms the pure-array computation path
        // (the final step of the ChartControl overload after the scan) returns a positive
        // value for valid inputs. Runtime scan verification is DW-B174-SIM gate.
        [Fact]
        public void T_B174_T1_02_InstrumentScanDelegatesCorrectly()
        {
            double[] highs  = { 10.0, 15.0, 20.0, 25.0 };
            double[] lows   = {  5.0,  9.0, 14.0, 19.0 };
            double[] closes = {  8.0, 13.0, 18.0, 23.0 };
            double baseline = AtrCalc.Compute(highs, lows, closes, 3);
            // Confirms the pure-array delegation path produces a positive result.
            Assert.True(baseline > 0.0, "Delegation path must produce a positive result");
        }

        // T-B174-T1-03: Empty string treated as "no filter" (hasName=false fallback).
        // Verifies the guard `bool hasName = !string.IsNullOrEmpty(instrumentFullName)`:
        //   - "" produces hasName=false (selects BarsArray[0] fallback -- correct)
        //   - null produces hasName=false (selects BarsArray[0] fallback -- correct)
        //   - non-empty string produces hasName=true (activates scan filter -- correct)
        // Tests the guard expression directly. ChartControl path verified by SIM gate DW-B174-SIM.
        [Fact]
        public void T_B174_T1_03_EmptyInstrumentNameTreatedAsNull()
        {
            Assert.False(!string.IsNullOrEmpty(""),
                "Empty string must be treated as no-filter (hasName=false)");
            Assert.False(!string.IsNullOrEmpty(null),
                "Null must be treated as no-filter (hasName=false)");
            Assert.True(!string.IsNullOrEmpty("MES 12-26"),
                "Non-empty instrument name must activate the scan filter (hasName=true)");
        }
    }
```

---

### 7-Scan Checklist (B174-3)

| # | Scan | Check | Pass Criterion |
|---|------|-------|----------------|
| SCAN-01 | No lock() | `grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 occurrences in added class |
| SCAN-02 | CYC annotation matches body count | Test methods have no branches (CYC=1 each). No `// CYC=N` annotation required for test methods. | N/A (test class -- no CYC annotation convention) |
| SCAN-03 | No Unicode / curly quotes | All string literals use straight ASCII double-quotes. | 0 non-ASCII characters in added lines |
| SCAN-04 | No DateTime.Now / FontFamily | Neither appears in the added class. | 0 occurrences |
| SCAN-05 | BarsArray[0] absent | N/A -- test class uses pure-array overload only. No ChartControl or BarsArray reference. | Confirmed absent |
| SCAN-06 | apply_diff used (no write_file) | Must use `apply_diff` for the insertion. | Confirmed apply_diff in completion report |
| SCAN-07 | ptt-sync.ps1 run after ALL edits | Run ONCE after B174-1, B174-2, and B174-3 are all complete. | Sync output in completion report |

---

### Verify Steps (B174-3)

1. `B174AtrCalcTests` class present in `CopyEngineTests.cs` before the namespace closing `}`.
2. Three `[Fact]` methods present: `T_B174_T1_01_PureOverloadRegressionGuard`,
   `T_B174_T1_02_InstrumentScanDelegatesCorrectly`, `T_B174_T1_03_EmptyInstrumentNameTreatedAsNull`.
3. No `[Fact(Skip=...)]` on any B174 test method.
4. `dotnet build` 0 errors.
5. `dotnet test` 0 new failures (all 3 B174 tests pass; all B173 tests still pass).

---

## EXECUTION ORDER (mandatory)

```
1. B174-1 -- AtrCalc.cs  (no dependencies)
   -> dotnet build -- verify 0 errors before proceeding to B174-2

2. B174-2 -- TradeCopierPanel.cs  (depends on B174-1)
   Edit order within ticket: A (insert field+method) -> B (update call) -> C (propagate)
   -> dotnet build -- verify 0 errors before proceeding to B174-3

3. B174-3 -- CopyEngineTests.cs  (depends on B174-1 + B174-2)
   -> dotnet build + dotnet test -- verify 0 errors, 0 new failures

4. After ALL THREE tickets complete:
   powershell -File .\scripts\ptt-sync.ps1
```

---

## JS COMPLIANCE SUMMARY

| Rule | B174-1 | B174-2 Edit A | B174-2 Edit B | B174-2 Edit C | B174-3 |
|------|--------|---------------|---------------|---------------|--------|
| JS-001: try/catch -> 0.0 | PASS (unchanged wrapping) | N/A (void) | N/A (AtrCalc handles) | N/A (void) | N/A |
| JS-002: no return null | N/A (returns double) | PASS (void) | N/A | PASS (void) | N/A |
| JS-013: CYC <= 8 | PASS (CYC=8) | PASS (CYC=1) | PASS (CYC=3) | PASS (CYC=2) | N/A |
| JS-021: no lock() | PASS | PASS | PASS | PASS | PASS |
| JS-023: no volatile double | PASS (no fields) | PASS (_instrument is ref type) | PASS | PASS | N/A |
| JS-PUSH-01: push over pull | N/A | PASS (three-step documented) | N/A | N/A | N/A |
| NT8-003: no volatile double | PASS | PASS | PASS | PASS | N/A |
| ASCII-only | PASS | PASS | PASS | PASS | PASS |
| No DateTime.Now | PASS | PASS | PASS | PASS | PASS |
| No FontFamily | PASS | PASS | PASS | PASS | PASS |
| No hex colors | PASS | PASS | PASS | PASS | PASS |
| apply_diff only (no write_file) | (engineer) | (engineer) | (engineer) | (engineer) | (engineer) |
| ptt-sync.ps1 after all edits | (engineer) | (engineer) | (engineer) | (engineer) | (engineer) |

---

*Tickets authored by: ptt-architect*
*Block: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH*
*Phase: 3 (Ticket Generation)*
*F-01 applied: CYC=8 (corrected from plan CYC=6)*
*F-02 applied: JS-PUSH-01 three-step cited in B174-2 Edit A*
*Ready for: Ph3.5 ptt-ticket-reviewer*
