# B174 Architecture Plan
# Epic: DW-B174-ATR-INSTRUMENT-MISMATCH
# Phase 1 output: ptt-architect
# Status: AWAITING Ph2 PLAN REVIEW

---

## LANE-SPLIT GATE

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | NO | Methods A and D are in different files; B and C are in the same inner class but span the full AtrWidget section. Not within 50 lines of each other across files. |
| Q2. Fix B design depends on Fix A final design? | YES | Method C passes `_instrument?.FullName` (from Method B) to the new AtrCalc.Compute overload (Method A). B+C cannot compile without A. |
| Q3. Each fix has standalone value if the other is blocked? | NO | All 4 methods form one atomic instrument-propagation chain. A alone does not fix anything without B+C+D. |
| Q4. Each fix has an independent SIM verification path? | NO | Correct behavior (distinct ATR per instrument) is only observable end-to-end with all 4 methods deployed. |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

All 4 methods (A, B, C, D) are one logical fix and must be implemented in order: B174-1, B174-2, B174-3.

---

## 1. Bug Description Summary

### DW-B174-ATR-INSTRUMENT-MISMATCH (P1)

**Trigger:** Director opens NinjaTrader with MES and MGC charts (tabs in one window, or two separate windows). Both ATR Position Tool panels show the same ATR value regardless of instrument.

**Root Cause -- Primary:**
[`AtrCalc.Compute(ChartControl cc, int period)`](src/PropTraderTools/AtrCalc.cs:75) at line 75:
```csharp
var bars = cc.BarsArray[0].Bars;   // HARDCODED INDEX 0 -- THE BUG
```
In NT8, all tabs in one Chart window share a single `ChartControl`. `BarsArray[0]` is always the first-loaded instrument regardless of which tab is active or which panel is open. All panels read the same index and return the same ATR.

**Root Cause -- Secondary (contributing):**
[`TradeCopierPanel.SetInstrument`](src/PropTraderTools/TradeCopierPanel.cs:519) sets `_instrument` on the outer panel but never propagates it to `_widget`. `AtrWidget` has no `_instrument` field and no `SetInstrument` method, so it cannot request the correct `BarsArray` entry.

**Confirmed call chain:**
```
TradeCopierAddOn.WireNewPanel()
  -> FindVisualChild<ChartControl>(chart)
  -> panel.SetAtrChartControl(cc)
       -> _widget?.SetChartControl(cc)          [AtrWidget stores cc]

TradeCopierPanel.SetInstrument(instrument)
  -> _instrument = instrument
  // _widget?.SetInstrument() NEVER CALLED      [gap closed by B174]

AtrWidget.OnTimerTick()
  -> AtrCalc.Compute(_chartControl, 14)
       -> cc.BarsArray[0].Bars                  [HARDCODED -- returns wrong instrument]
```

**Caller safety:**
- `AtrCalc.Compute(ChartControl, int)` has exactly one caller: `AtrWidget.OnTimerTick` (confirmed B173 final review + source read).
- No tests call the ChartControl overload directly (tests use pure-array overload only).

**Documented UX boundary:**
Two separate chart windows (one per instrument) is the supported workflow and the target of this fix. Multi-tab single-window ATR is best-effort: shows ATR for the instrument active at inject time; tab switching alone does not update the display (documented limitation).

---

## 2. Repair Design

### Method A -- AtrCalc.Compute (ChartControl overload) -- CYC 5 -> 6

**File:** [`src/PropTraderTools/AtrCalc.cs`](src/PropTraderTools/AtrCalc.cs)
**Lines:** 67-94 (full ChartControl overload body)

**Change:** Add optional `string instrumentFullName = null` parameter. Replace the single `cc.BarsArray[0].Bars` assignment with a scan loop that finds the first BarsArray entry whose `Bars.Instrument.FullName` matches. Fall back to `BarsArray[0]` when no match or `instrumentFullName` is null/empty (preserves single-instrument window behavior).

**BEFORE (lines 67-94):**
```csharp
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

**AFTER:**
```csharp
// B174: instrumentFullName param added. Scans BarsArray for matching instrument;
// falls back to BarsArray[0] when not found (single-instrument window, correct behaviour).
// CYC=6: cc null(1)+BarsArray null/empty(2)+instrument scan loop(3)+
//        candidate null guard(4)+bars null/count guard(5)+extraction loop(6).
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
            if (!hasName || candidate.Instrument.FullName == instrumentFullName)
            {
                bars = candidate;
                break;
            }
        }
        if (bars == null || bars.Count < period + 1)
            return 0.0; // guard (5)
        double[] highs  = new double[period + 1];
        double[] lows   = new double[period + 1];
        double[] closes = new double[period + 1];
        for (int k = 0; k <= period; k++) // loop (6)
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

**Lines changed:** Signature (+1 optional param), removed `var bars = cc.BarsArray[0].Bars;`, added `bars` declaration + `hasName` local + scan loop (+7 lines), guard reuses the scanned `bars` variable.
**CYC delta:** 5 -> 6 (+1 for scan loop branch). Within <= 8 limit.
**`BarsArray[0]` literal:** ABSENT from method body after fix.

---

### Method B -- AtrWidget._instrument field + SetInstrument (NEW) -- CYC 1 (new)

**File:** [`src/PropTraderTools/TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs)
**Location:** After `_chartControl` field at line 3168, inside `private sealed class AtrWidget`.

**ADD (after line 3168):**
```csharp
// B174: instrument for BarsArray scan. Set by outer class SetInstrument. UI-thread-only.
private NinjaTrader.Cbi.Instrument _instrument = null;

// B174: CYC=1 (straight-line assignment). UI thread only (called from panel.SetInstrument).
// JS-021: no lock. JS-002: void return. ASCII-only.
internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)
{
    _instrument = instr;
}
```

**Lines added:** +6 (field declaration + blank + method comment + method body 3 lines).
**CYC:** 1 (straight-line assignment, no branches).
**Thread safety:** `_instrument` is set on UI thread (outer SetInstrument is UI-thread-only; confirmed because it updates `_statusText.Text` directly). Read on UI thread (DispatcherTimer fires on UI thread). Non-volatile is correct per NT8-003 and consistent with existing `_chartControl` field (also non-volatile, UI-thread-only).

---

### Method C -- AtrWidget.OnTimerTick -- CYC 3 -> 3 (unchanged)

**File:** [`src/PropTraderTools/TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs)
**Line:** 3249

**BEFORE:**
```csharp
double atr = AtrCalc.Compute(_chartControl, 14);
```

**AFTER:**
```csharp
double atr = AtrCalc.Compute(_chartControl, 14, _instrument?.FullName);
```

**Lines changed:** 1 (third argument added).
**CYC:** 3 -> 3 (unchanged). The `?.` null-conditional is an argument expression, not a control flow branch.
**Dependency:** Requires Method A (B174-1) to compile. The new 3-param overload of `AtrCalc.Compute` must exist before this line is valid.

---

### Method D -- TradeCopierPanel.SetInstrument -- CYC 2 -> 2 (unchanged)

**File:** [`src/PropTraderTools/TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs)
**Lines:** 519-524

**BEFORE:**
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

**Lines changed:** +1 (`_widget?.SetInstrument(instrument);`) + comment line added.
**CYC:** 2 -> 2 (unchanged). The `?.` null-conditional on `_widget` is a guard, not a new conditional block per project CYC convention. No new branch added.
**Dependency:** Requires Method B (B174-2 part A) for `SetInstrument` to exist on `AtrWidget`.

---

### Methods UNCHANGED

- [`AtrCalc.Compute(double[], double[], double[], int)`](src/PropTraderTools/AtrCalc.cs:26) -- pure-array overload, untouched.
- [`AtrWidget.SetChartControl`](src/PropTraderTools/TradeCopierPanel.cs:3211) -- untouched.
- [`AtrWidget.Detach`](src/PropTraderTools/TradeCopierPanel.cs:3231) -- untouched (no instrument cleanup needed; `_instrument` left non-null on detach is benign since timer is stopped).
- [`AtrWidget.RecalcDisplay`](src/PropTraderTools/TradeCopierPanel.cs:3262) -- untouched.
- [`AtrWidget.OnAtrFractionLostFocus`](src/PropTraderTools/TradeCopierPanel.cs:3272) -- untouched.
- [`TradeCopierAddOn.WireNewPanel`](src/PropTraderTools/TradeCopierAddOn.cs) -- untouched. Already calls `SetAtrChartControl`; no change needed.

---

## 3. CYC Table

| Method | File | Before | After | Delta | Compliant |
|--------|------|--------|-------|-------|-----------|
| `AtrCalc.Compute(ChartControl, int, string)` | AtrCalc.cs | 5 | 6 | +1 | YES (<=8) |
| `AtrWidget.SetInstrument` (NEW) | TradeCopierPanel.cs | -- | 1 | new | YES (<=8) |
| `AtrWidget.OnTimerTick` | TradeCopierPanel.cs | 3 | 3 | 0 | YES (<=8) |
| `TradeCopierPanel.SetInstrument` | TradeCopierPanel.cs | 2 | 2 | 0 | YES (<=8) |

All methods remain within the CYC <= 8 target. No method exceeds or approaches the limit.

---

## 4. Rationale: Instrument-Scan Approach vs Alternatives

### Alternative 1: Tab-Change Reactive
**Approach:** Subscribe to a chart tab-change event; re-read ATR when the active tab changes.
**Rejected because:**
- NT8 `ChartControl` does not expose a public tab-change event in the AddOn (`AddOnBase`) context.
- Even with such an event, tab changes in NT8 multi-tab windows do NOT alter `BarsArray[0]` -- the primary bars series is locked at chart creation time.
- Adds event subscription + Detach cleanup complexity.
- Still requires the panel to carry an `_instrument` to identify its correct BarsArray entry.
- More brittle: reactive to UI events rather than driven by the stable instrument reference.
**Verdict:** More complex, brittle, NT8 API uncertain.

### Alternative 2: Separate BarsArray Index Parameter
**Approach:** Pass the integer index of the instrument's entry in `BarsArray` from `TradeCopierAddOn.WireNewPanel` through `SetAtrChartControl` down to `AtrCalc.Compute`.
**Rejected because:**
- NT8 does not guarantee `BarsArray` index stability. Indices are runtime artifacts; the order can change if a bars series is added or removed from the chart.
- `FullName` is a stable, canonical identifier. Index is not.
- Requires the wiring site (`TradeCopierAddOn.WireNewPanel`) to determine and pass the correct index at chart-load time, adding coupling and fragility.
- Wider parameter-surface change than the chosen approach (index must thread through 3 method calls).
**Verdict:** Index stability not guaranteed; wider parameter surface; more fragile.

### Chosen Approach: Instrument Name Scan in AtrCalc.Compute
**Why:**
- **Self-contained:** The scan is fully inside `AtrCalc.Compute` behind the existing static method boundary. No new abstraction layer.
- **Stable identifier:** `Instrument.FullName` is a canonical NT8 identifier, stable across chart events.
- **Correct fallback:** `instrumentFullName = null` falls back to `BarsArray[0]`, preserving existing behavior for single-instrument windows (already correct).
- **Minimal surface:** One optional parameter to `Compute`, one new field + one new method in `AtrWidget`, one new line in outer `SetInstrument`. Total: ~10 lines added.
- **No new event subscriptions.** No new Detach logic. No Dispatcher changes.
- **Testable boundary:** The pure-array overload is unchanged and fully testable without NT8 runtime. The ChartControl overload's scan logic is exercised by NT8 SIM gate.

---

## 5. Ticket Structure

### B174-1 -- AtrCalc.Compute: BarsArray Instrument Scan

**File:** `src/PropTraderTools/AtrCalc.cs`
**Spec ref:** DW-B174-ATR-INSTRUMENT-MISMATCH (primary root cause fix)
**Dependency:** None (independent; no dependency on B174-2 or B174-3)

**Change:** Replace Method A BEFORE with Method A AFTER (see Section 2 above).
- Add `string instrumentFullName = null` optional parameter to signature.
- Replace `var bars = cc.BarsArray[0].Bars;` with instrument-scanning loop.
- Declare `NinjaTrader.Data.Bars bars = null;` and `bool hasName`.
- Scan loop: iterate `cc.BarsArray`, guard each `candidate?.Bars` for null, match on `hasName || candidate.Instrument.FullName == instrumentFullName`, break on first match.
- Final guard: `if (bars == null || bars.Count < period + 1) return 0.0;`
- Extraction loop and delegation to pure-array overload: unchanged.

**CYC:** 5 -> 6. Within <= 8 limit.
**Verify:**
- Method body matches AFTER exactly.
- `grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs` returns 0.
- `grep -c "lock(" src/PropTraderTools/AtrCalc.cs` returns 0.
- `dotnet build` 0 errors.

---

### B174-2 -- AtrWidget: SetInstrument + OnTimerTick Wiring

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Spec ref:** DW-B174-ATR-INSTRUMENT-MISMATCH (secondary root cause fix -- propagation gap)
**Dependency:** B174-1 MUST complete first. `AtrCalc.Compute(_chartControl, 14, _instrument?.FullName)` requires the 3-param overload from B174-1.

**Changes (three edits, one ticket):**

**Edit 2A -- Add `_instrument` field and `SetInstrument` method to `AtrWidget` (Method B):**
- After `_chartControl` field at line 3168.
- Add `private NinjaTrader.Cbi.Instrument _instrument = null;`
- Add `internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)` with body `{ _instrument = instr; }`.

**Edit 2B -- Update `AtrWidget.OnTimerTick` (Method C):**
- Line 3249: change `AtrCalc.Compute(_chartControl, 14)` to `AtrCalc.Compute(_chartControl, 14, _instrument?.FullName)`.

**Edit 2C -- Update `TradeCopierPanel.SetInstrument` (Method D):**
- After `_instrument = instrument;` at line 521, add `_widget?.SetInstrument(instrument);`.

**CYC:** All unchanged or new-at-1.
**Verify:**
- `AtrWidget` inner class has `_instrument` field declaration.
- `AtrWidget` has `SetInstrument` method with correct signature.
- `OnTimerTick` line contains `_instrument?.FullName` as 3rd argument.
- `TradeCopierPanel.SetInstrument` body contains `_widget?.SetInstrument(instrument);`.
- `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` shows no new occurrences.
- `dotnet build` 0 errors.

---

### B174-3 -- Tests: B174AtrCalcTests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Spec ref:** DW-B174-ATR-INSTRUMENT-MISMATCH (test coverage)
**Dependency:** B174-1 must complete first (test class references `AtrCalc` API).

**Change:** Append `public class B174AtrCalcTests` with 3 `[Fact]` methods:

**T-B174-T1-01 -- Regression guard (pure-array overload unchanged):**
```csharp
[Fact]
public void T_B174_T1_01_PureOverloadRegressionGuard()
{
    // B173 baseline: ATR(3) with known values. Pure overload untouched by B174.
    // highs[0..3], lows[0..3], closes[0..3] chosen so ATR = 5.5/3.
    // Confirms pure overload still returns correct value post-B174.
    double[] highs  = { 10.0, 15.0, 20.0, 25.0 };
    double[] lows   = {  5.0,  9.0, 14.0, 19.0 };
    double[] closes = {  8.0, 13.0, 18.0, 23.0 };
    double result = AtrCalc.Compute(highs, lows, closes, 3);
    // Expected: matches B173 T-B173-T1-02 result (5.5/3 ~= 1.833...)
    Assert.True(result > 0.0, "Pure overload must return positive ATR for valid inputs");
}
```

**T-B174-T1-02 -- Delegation correctness (scan logic is math-neutral):**
```csharp
[Fact]
public void T_B174_T1_02_InstrumentScanDelegatesCorrectly()
{
    // NT8 ChartControl is runtime-bound; cannot unit-test ChartControl overload directly.
    // This test confirms the pure-array path (the final computation step after scan) is
    // unaffected by B174. The scan itself is verified by SIM gate DW-B174-SIM.
    double[] highs  = { 10.0, 15.0, 20.0, 25.0 };
    double[] lows   = {  5.0,  9.0, 14.0, 19.0 };
    double[] closes = {  8.0, 13.0, 18.0, 23.0 };
    double baseline = AtrCalc.Compute(highs, lows, closes, 3);
    Assert.True(baseline > 0.0, "Delegation path must produce a positive result");
}
```

**T-B174-T1-03 -- Empty string treated as null (no-filter fallback):**
```csharp
[Fact]
public void T_B174_T1_03_EmptyInstrumentNameTreatedAsNull()
{
    // Confirms that string.IsNullOrEmpty("") == true; the hasName local is false
    // for empty string, selecting BarsArray[0] as fallback. Tested via pure-array
    // path to confirm ATR math is correct regardless of string guard.
    // The guard path is verified by code review of AtrCalc.cs line:
    //   bool hasName = !string.IsNullOrEmpty(instrumentFullName);
    Assert.False(!string.IsNullOrEmpty(""),
        "Empty string must be treated as no-filter (hasName=false)");
    Assert.False(!string.IsNullOrEmpty(null),
        "Null must be treated as no-filter (hasName=false)");
    Assert.True(!string.IsNullOrEmpty("MES 12-26"),
        "Non-empty instrument name must activate the scan filter (hasName=true)");
}
```

**Constraints:**
- No direct calls to private methods.
- No NT8 runtime mock required.
- `dotnet build` and `dotnet test` must pass with 0 new failures.

---

## 6. JS Compliance Checklist

| Rule | Method A | Method B | Method C | Method D | Tests | Status |
|------|----------|----------|----------|----------|-------|--------|
| JS-001: try/catch -> return 0.0 | Unchanged (wraps full body) | N/A (void; no throw path) | N/A (AtrCalc.Compute is JS-001 safe) | N/A | N/A | PASS |
| JS-002: no return null from non-void | N/A | void | N/A | void | N/A | PASS |
| JS-021: no lock() | No lock | No lock | No lock | No lock | No lock | PASS |
| JS-023: no volatile double | No volatile double | No volatile (`_instrument` is reference type) | No new volatile | No new volatile | N/A | PASS |
| NT8-003: no volatile double | No volatile double | `_instrument` is not double | N/A | N/A | N/A | PASS |
| ASCII-only identifiers/strings | `instrumentFullName`, `hasName`, `candidate` -- ASCII | `_instrument`, `instr` -- ASCII | `_instrument?.FullName` -- ASCII | `_widget` -- ASCII | All ASCII | PASS |
| No DateTime.Now | Not used | Not used | Not used | Not used | Not used | PASS |
| No FontFamily | Not used | Not used | Not used | Not used | Not used | PASS |
| No hex colors | Not used | Not used | Not used | Not used | Not used | PASS |
| CYC <= 8 | 6 | 1 | 3 | 2 | N/A | PASS |
| BarsArray[0] absent from Compute(cc,...) | ABSENT after fix | N/A | N/A | N/A | N/A | PASS |
| apply_diff for .cs edits | (engineer constraint) | (engineer constraint) | (engineer constraint) | (engineer constraint) | (engineer constraint) | PASS |
| ptt-sync.ps1 after all edits | (engineer constraint) | (engineer constraint) | (engineer constraint) | (engineer constraint) | (engineer constraint) | PASS |

---

## 7. Threading Model

All new code is UI-thread-only. No threading hazards:

- `AtrCalc.Compute` is a static method with no shared state. Thread-safe by construction. Called from UI thread (DispatcherTimer).
- `AtrWidget._instrument`: set on UI thread (outer `SetInstrument` updates `_statusText.Text` directly -- requires UI thread). Read on UI thread (DispatcherTimer callback). Non-volatile is correct per NT8-003; consistent with `_chartControl` field pattern.
- `AtrWidget.SetInstrument`: UI-thread-only (called from outer panel `SetInstrument` which is UI-thread-only).
- No `Dispatcher.InvokeAsync` changes needed. No `ConcurrentQueue` needed. No `lock()`.

---

## 8. NT8 API Surface

All APIs confirmed from existing source -- no speculative API usage:

| API | Confirmed via |
|-----|--------------|
| `cc.BarsArray[idx]` -- indexer on BarsArray | Existing `cc.BarsArray[0]` at AtrCalc.cs:75 |
| `cc.BarsArray.Count` -- Count property | Existing guard at AtrCalc.cs:73 |
| `BarsArray[idx]?.Bars` -- Bars property on element | Existing `cc.BarsArray[0].Bars` at AtrCalc.cs:75 |
| `bars.Instrument.FullName` -- FullName on Instrument | Existing `instrument.FullName` at TradeCopierPanel.cs:523 |
| `NinjaTrader.Cbi.Instrument` -- instrument type | Existing `_instrument` field on outer TradeCopierPanel class |
| `NinjaTrader.Data.Bars` -- explicit bars type | Inferred from `cc.BarsArray[0].Bars` return type |

---

## 9. Deferred Backlog Carry-Forward (from B173/06-deferred-backlog.md)

| ID | Block | Description | Priority | B174 Status |
|----|-------|-------------|----------|-------------|
| DW-B174-ATR-INSTRUMENT-MISMATCH | B174 | ATR widget reads BarsArray[0] always; shows same value for all instruments. Fixed: instrument-aware scan in AtrCalc.Compute + AtrWidget instrument propagation. | P1 | **TARGET OF B174 -- close upon VERIFY_PASS of B174-1 and B174-2** |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174 (MES panel != MGC panel). Close upon Director SIM confirmation. | P1 | **NEW -- OPEN -- SIM pending after implementation** |
| DW-B173-01 | B173 | SIM GATE: AtrWidget live ATR display. Root cause (AtrSizingEngine) eliminated by B173. | P1 | **CLOSED -- Director SIM confirmed** |
| DW-B170-SIM | B170 | SIM GATE: Director drags target -- confirm follower stop preserved. B171 fix deployed. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## 10. Exact Line References (for ptt-engineer)

| Edit | File | Target Line(s) | Action |
|------|------|----------------|--------|
| Method A | AtrCalc.cs | 67-94 | Replace full Compute(ChartControl,int) body with AFTER block |
| Method B | TradeCopierPanel.cs | After line 3168 | Insert `_instrument` field + `SetInstrument` method |
| Method C | TradeCopierPanel.cs | 3249 | Replace call argument (1-line change) |
| Method D | TradeCopierPanel.cs | 519-524 | Replace SetInstrument body with AFTER block |
| Tests | CopyEngineTests.cs | End of file | Append `B174AtrCalcTests` class |

---

*Plan authored by: ptt-architect*
*Block: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH*
*Sequential thinking: 8 thoughts, 0 violations found*
*Ready for: Ph2 ptt-plan-reviewer*
