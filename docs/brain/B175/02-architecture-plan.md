# Architecture Plan — Block B175
# Defects: DW-B175-ATR-FORMULA + DW-B175-ATR-TEXTBOX-FOCUS
# Phase: 1 — Architecture (Retry 1 — test spec violations fixed)
# Status: PLAN_COMPLETE
# Author: ptt-architect
# Date: 2025-01-27

---

## 1. Executive Summary

Block B175 closes two confirmed P1 defects in the ATR widget subsystem:

**DW-B175-ATR-FORMULA**: `AtrCalc.Compute` computes ATR using a simple 14-period average of True
Range. NT8's chart header uses Wilder's ATR (exponential/smoothed moving average with
(period-1)/period decay). This makes the widget display consistently smaller ATR values than the
chart header — a calibration bug affecting all ATR-based position sizing decisions. Fix: replace
simple average with Wilder's smoothing using a 2*period warmup window. Falls back to the existing
simple average when the chart has fewer than 2*period+1 bars loaded (preserves existing behaviour
for short histories). The NT8 bar-read block is updated to request up to 2*period+1 bars. A new
private helper `ReadBarsToArrays` is extracted to keep the ChartControl overload CYC within bound.

**DW-B175-ATR-TEXTBOX-FOCUS**: Typing in the ATR% TextBox opens NT8's chart instrument search
dialog instead of editing the box. Root cause: NT8's chart-level `PreviewKeyDown` tunnel handler
fires in the bubbling phase before keystrokes reach the TextBox's own `KeyDown`. Fix: subscribe
`_atrFractionBox.PreviewKeyDown` in `BuildAtrWidgetRows`; in the new handler
`OnAtrBoxPreviewKeyDown`, set `e.Handled = true` when `_atrFractionBox.IsKeyboardFocused` is true.
This marks the event handled during the tunnel phase, blocking the chart-level interceptor.

Files touched: `AtrCalc.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs`.
Tickets: B175-1 (formula fix + tests), B175-2 (textbox focus fix), B175-3 (sync + verify).

---

## 2. Lane-Split Gate Result

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

| Question | Answer | Rationale |
|---|---|---|
| Q1. Same method or within 50 lines? | NO | ATR formula: `AtrCalc.cs`. TextBox focus: `TradeCopierPanel.cs` AtrWidget inner class. Different files entirely. |
| Q2. Fix B design depends on Fix A final design? | NO | `OnAtrBoxPreviewKeyDown` is a pure WPF event handler. No dependency on Wilder's formula. |
| Q3. Each fix has standalone value if the other is blocked? | YES | Formula fix alone: correct ATR value, keyboard still broken. TextBox fix alone: keyboard works, ATR still simple avg. Both independently shippable. |
| Q4. Each fix has an independent SIM verification path? | YES | Formula: compare widget ATR pts vs NT8 chart header ATR * fraction. TextBox: click box, type, verify no instrument search dialog opens. |

Gate conditions: Q1=NO, Q2=NO, Q3=YES, Q4=YES — all four conditions met.
**Routing decision: SINGLE-PIPELINE with 3 tickets.** Both fixes are small (< 20 lines each),
in non-overlapping files, and share a single sync/verify step. Running as SINGLE-PIPELINE
avoids pipeline management overhead while preserving independent SIM verification paths.

---

## 3. Root Cause Analysis

### DW-B175-ATR-FORMULA

**Symptom**: ATR widget shows a smaller ATR value than the NT8 chart header ATR indicator for the
same instrument and period.

**Root cause**: `AtrCalc.Compute(double[],double[],double[],int)` implements a simple arithmetic
mean of True Range over `period` bars:

```
ATR_simple = (TR[1] + TR[2] + ... + TR[period]) / period
```

NT8's built-in ATR indicator uses Wilder's smoothing (Wilder's Moving Average), which seeds with
the simple average of the first `period` TRs and then applies exponential decay for subsequent bars:

```
ATR_seed = (TR[1] + ... + TR[period]) / period   -- same initial seed
ATR[k]   = (ATR[k-1] * (period - 1) + TR[k]) / period   for k = period+1 .. N
```

The two formulas agree at bar `period` but diverge immediately after: Wilder's gives more weight
to recent True Range values via the decay factor `(period-1)/period`. For period=14, the decay is
`13/14 ≈ 0.929`. Over 14 additional bars of smoothing, the Wilder's value converges to the
"true" exponentially-smoothed ATR that NT8 charts display.

**Fix**: Extend `Compute` to apply Wilder's smoothing when the input array has at least
`2*period+1` values (period+1 for seed + period additional for Wilder's loop). The NT8 bar-read
block is expanded to request up to `2*period+1` bars. When fewer bars are available, the method
falls back to the existing simple average (preserves B173/B174 correctness for fresh charts).

### DW-B175-ATR-TEXTBOX-FOCUS

**Symptom**: Clicking into the ATR% TextBox and pressing any alphanumeric key opens NT8's chart
instrument search overlay dialog instead of typing into the box.

**Root cause**: NT8's `ChartControl` implements a chart-level `PreviewKeyDown` handler in the
tunneling phase (WPF PreviewKeyDown fires top-down before KeyDown). The chart's handler intercepts
alphanumeric keys and routes them to the instrument search subsystem, consuming them before they
can bubble down to the TextBox's own key handling. The standard WPF mechanism to prevent this is
to set `e.Handled = true` in the child element's `PreviewKeyDown` handler, which stops the event
from propagating further in the tunnel and prevents the chart-level handler from seeing it.

**Fix**: Subscribe `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown` in
`BuildAtrWidgetRows`. In the handler, check `_atrFractionBox.IsKeyboardFocused` (ensures the box
actually has focus, not just that a sibling element is being clicked). Set `e.Handled = true` to
mark the event consumed.

---

## 4. Method-by-Method BEFORE / AFTER

### 4a. AtrCalc.Compute(double[], double[], double[], int) — MODIFIED

**File**: `src/PropTraderTools/AtrCalc.cs` (lines 26–52)

**BEFORE**:
```csharp
internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
{
    try
    {
        if (highs == null || lows == null || closes == null)
            return 0.0;
        if (period < 1)
            return 0.0;
        if (highs.Length < period + 1 || lows.Length < period + 1 || closes.Length < period + 1)
            return 0.0;
        double sum = 0.0;
        for (int i = 1; i <= period; i++)
        {
            double pc = closes[i - 1];
            double tr = Math.Max(
                highs[i] - lows[i],
                Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
            );
            sum += tr;
        }
        return sum / period;
    }
    catch (Exception)
    {
        return 0.0;
    }
}
```

**AFTER**:
```csharp
// B175: Wilder's ATR. Seed = simple avg of first period TRs; then smooth.
// Requires length >= 2*period+1 for full Wilder's; falls back to simple avg
// when length < 2*period+1 (short history: period+1 bars minimum unchanged).
// CYC=8: null(1)+period<1(2)+length<period+1(3)+seed loop(4)+
//        Wilder's length check(5)+smooth loop(6)+simple-fallback path(7)+catch(8).
// JS-001 PASS. JS-021: no lock. ASCII-only.
internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
{
    try
    {
        if (highs == null || lows == null || closes == null)
            return 0.0;
        if (period < 1)
            return 0.0;
        if (highs.Length < period + 1 || lows.Length < period + 1 || closes.Length < period + 1)
            return 0.0;
        // Seed: simple average of first period TR values (indices 1..period)
        double atr = 0.0;
        for (int i = 1; i <= period; i++) // seed loop (4)
        {
            double pc = closes[i - 1];
            atr += Math.Max(
                highs[i] - lows[i],
                Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
            );
        }
        atr /= period;
        // Wilder's smoothing: apply if array is long enough (2*period+1 values)
        if (highs.Length >= 2 * period + 1) // Wilder's length check (5)
        {
            for (int i = period + 1; i <= 2 * period; i++) // smooth loop (6)
            {
                double pc = closes[i - 1];
                double tr = Math.Max(
                    highs[i] - lows[i],
                    Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                );
                atr = (atr * (period - 1) + tr) / period;
            }
        }
        // else: simple average already computed -- return seed as-is (fallback)
        return atr;
    }
    catch (Exception)
    {
        return 0.0;
    }
}
```

---

### 4b. AtrCalc.Compute(ChartControl, int, string) bar-read block — MODIFIED

**File**: `src/PropTraderTools/AtrCalc.cs` (lines 94–106)

**BEFORE**:
```csharp
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
```

**AFTER**:
```csharp
        // B175: read up to 2*period+1 bars for Wilder's warmup; fall back to
        // period+1 when chart has fewer bars loaded (Wilder's fallback to simple avg).
        if (bars == null || bars.Count < period + 1)
            return 0.0;
        int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;
        ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
        return Compute(highs, lows, closes, period);
```

**CYC impact**: The k-loop (+1) is extracted to `ReadBarsToArrays`. The ternary adds +1.
Net delta = 0. CYC remains 8.

---

### 4c. AtrCalc.ReadBarsToArrays — NEW private static helper

**File**: `src/PropTraderTools/AtrCalc.cs` (inserted after Compute ChartControl overload)

```csharp
// B175: Extract bar OHLC into arrays. readLen bars, oldest-first (index 0 = oldest).
// CYC=2 (base=1 + for=1). JS-001: called inside outer try/catch. ASCII-only.
private static void ReadBarsToArrays(
    NinjaTrader.Data.Bars bars,
    int readLen,
    out double[] highs,
    out double[] lows,
    out double[] closes)
{
    highs  = new double[readLen];
    lows   = new double[readLen];
    closes = new double[readLen];
    for (int k = 0; k < readLen; k++)
    {
        int barsAgo = readLen - 1 - k;
        highs[k]  = bars.GetHigh(barsAgo);
        lows[k]   = bars.GetLow(barsAgo);
        closes[k] = bars.GetClose(barsAgo);
    }
}
```

**Index safety proof**:
- `barsAgo` range: when k=0, barsAgo=readLen-1 (oldest). When k=readLen-1, barsAgo=0 (newest).
- Guard: `bars.Count >= period+1` is asserted in caller. `readLen <= bars.Count` by construction
  (ternary picks min of bars.Count-aware value). So `bars.GetHigh(readLen-1)` is always in bounds.

**Array ordering**: index 0 = oldest bar, index readLen-1 = newest bar. Consistent with existing
convention documented at line 18 of AtrCalc.cs.

---

### 4d. AtrWidget.OnAtrBoxPreviewKeyDown — NEW private method

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (AtrWidget inner class)

```csharp
// B175: PreviewKeyDown on _atrFractionBox. Marks event handled when box has
// keyboard focus, blocking NT8 chart-level search interceptor (tunnel phase).
// CYC=2: base + IsKeyboardFocused check. JS-021: no lock. UI thread only. ASCII-only.
private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
        e.Handled = true;
}
```

---

### 4e. AtrWidget.BuildAtrWidgetRows — MODIFIED (subscription line added)

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (line 3212)

**BEFORE** (lines 3212):
```csharp
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
                // PreviewKeyDown handler ABSENT
```

**AFTER**:
```csharp
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
                _atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;
```

CYC unchanged (still 1 — straight-line construction).

---

## 5. CYC Table

| Method | File | Before | After | Delta | Compliant (<=8) |
|---|---|---|---|---|---|
| `AtrCalc.Compute(double[],double[],double[],int)` | AtrCalc.cs | 6 | 8 | +2 | YES |
| `AtrCalc.Compute(ChartControl,int,string)` | AtrCalc.cs | 8 | 8 | 0 | YES |
| `AtrCalc.ReadBarsToArrays` (NEW) | AtrCalc.cs | -- | 2 | +2 | YES |
| `AtrWidget.OnAtrBoxPreviewKeyDown` (NEW) | TradeCopierPanel.cs | -- | 2 | +2 | YES |
| `AtrWidget.BuildAtrWidgetRows` | TradeCopierPanel.cs | 1 | 1 | 0 | YES |

**CYC counting convention**: Base=1. Each branch point (if, for, while, case, catch, &&, ||,
ternary) = +1. No method exceeds 8. All compliant.

---

## 6. Jane Street Compliance Checklist

| Rule | Requirement | Status |
|---|---|---|
| JS-001 | try/catch returning safe value in all fallible methods | PASS — Compute overloads both wrapped. ReadBarsToArrays is called inside outer try/catch (no own catch needed). OnAtrBoxPreviewKeyDown does not throw. |
| JS-002 | No return null from reference-returning methods | PASS — ReadBarsToArrays uses `out` params (void return). All Compute overloads return double. |
| JS-021 | No lock() anywhere | PASS — Zero lock statements introduced. All state is UI-thread-only (DispatcherTimer, WPF events). |
| JS-023 | No volatile on double fields | PASS — No new volatile fields. `_hasData` volatile bool (existing) unchanged. |
| NT8-003 | No volatile double (NT8-specific) | PASS — No new volatile double fields. |
| ASCII-only | No Unicode string literals, no emoji, no curly quotes | PASS — All string literals in new code are ASCII (none exist in math methods; comment text is ASCII-only). |
| DateTime.Now | Must use DateTime.UtcNow for any timestamp | PASS — No timestamp usage in this block. |
| Hex colors | No hardcoded #RRGGBB in C# | PASS — No color literals in this block. |
| FontFamily | No FontFamily usage | PASS — Not used. |
| CreateOrder | "PTT-" prefix on all CreateOrder calls | N/A — No CreateOrder calls in this block. |
| StrategyBase API | No AtmStrategyCreate / AtmStrategyChangeStopTarget from AddOnBase | PASS — Not used. Wilder's formula is pure math; WPF events are not NT8 API. |

---

## 7. Ticket Summary

### B175-1 — DW-B175-ATR-FORMULA: Wilder's ATR formula + bar-read expansion

**File**: `src/PropTraderTools/AtrCalc.cs`
**Spec requirement**: DW-B175-ATR-FORMULA
**Changes**:
- Replace simple average with Wilder's smoothing in `Compute(double[],double[],double[],int)`.
- Update bar-read block in `Compute(ChartControl,int,string)` to use ternary readLen + `ReadBarsToArrays`.
- Add new private static helper `ReadBarsToArrays`.
**Tests** (in `CopyEngineTests.cs`, new class `B175AtrCalcTests`):
- `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR`:
  Setup: 29-bar arrays (length=29, period=14) where every TR=2.0. Use `highs[i]=i+2, lows[i]=i, closes[i]=i+1`
  for all i in 0..28 (H-L=2.0 constant; no previous-close contribution since H-L dominates).
  Call: `AtrCalc.Compute(highs, lows, closes, 14)`.
  Assert: `result == 2.0`.
  Rationale: seed = (2.0*14)/14 = 2.0; each Wilder's step = (2.0*13 + 2.0)/14 = 2.0. Constant TR
  produces constant ATR at every step. Proves Wilder's formula is correct for the degenerate
  constant-TR case.
- `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg`:
  Setup: 29-bar arrays (length=29, period=14).
  Bars 0..14 (indices 0..14): TR=1.0 each → H=i+1, L=i, C=i+0.5 (or any construction giving H-L=1.0).
  Bars 15..28 (indices 15..28): TR=3.0 each → H=i+3, L=i, C=i+1 (or any construction giving H-L=3.0).
  Seed = (1.0*14)/14 = 1.0. Wilder's smoothing 14 steps with TR=3.0: atr rises from 1.0 toward 3.0
  but stays below 3.0 after finite steps. Final Wilder's result > 1.0.
  Comparison (simple avg fallback path): 15-bar arrays (length=15, period=14), all TR=1.0.
  `AtrCalc.Compute(15-bar-all-ones, 14)` → fallback path (15 < 29) → simple avg = 1.0.
  Assert: `AtrCalc.Compute(29-bar-spike-arrays, 14) > AtrCalc.Compute(15-bar-fallback-arrays, 14)`.
  Rationale: Wilder's carries the seed forward and rises above the simple-avg baseline when
  smoothing TRs are larger than the seed. Confirms Wilder's != simple avg in the non-constant case.
- `T_B175_T1_03_FallbackPathReturnsSimpleAvg`: array = period+1 → same as simple avg (regression guard vs B173/B174 baseline).
- `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg`:
  Input: `H=[10,12,11,13]`, `L=[8,9,10,10]`, `C=[9,10,11,12]`, `period=3`.
  Array length=4 = period+1=4 < 2*3+1=7 → fallback to simple avg; Wilder's smoothing NOT applied.
  TR[1]=max(12-9, |12-9|, |9-9|)=3, TR[2]=max(11-10, |11-10|, |10-10|)=1,
  TR[3]=max(13-10, |13-11|, |10-11|)=3 → sum=7.
  Assert: `result == 7.0/3.0`.
  Label: B174 regression guard -- unchanged from B174 baseline.

### B175-2 — DW-B175-ATR-TEXTBOX-FOCUS: PreviewKeyDown focus block

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Spec requirement**: DW-B175-ATR-TEXTBOX-FOCUS
**Changes**:
- Add `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;` in `BuildAtrWidgetRows`.
- Add new private method `OnAtrBoxPreviewKeyDown` in `AtrWidget` inner class.
**Tests**: No xUnit test (IsKeyboardFocused requires live WPF window). Verified by SIM gate DW-B175-SIM-FOCUS.

### B175-3 — Sync + Verify

**Action**: Run `powershell -File .\scripts\ptt-sync.ps1`. Confirm 0 link errors.
Run `dotnet test` to confirm 0 new failures (B173/B174 existing tests must still pass).
Confirm all 7 scans are zero.

---

## 8. Deferred Backlog Carry-Forward

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B175-SIM-FORMULA | B175 | SIM GATE: Verify ATR widget pts value matches NT8 chart header ATR * _atrFraction for the same instrument and period=14. Steps: (1) Open chart with >28 bars loaded (satisfies 2*period+1). (2) Wait ~2s for timer tick. (3) Note widget "X pts" value and chart header ATR. Confirm widget_pts ≈ round_up(NT8_ATR * fraction). Close upon Director SIM confirmation. | P1 | OPEN |
| DW-B175-SIM-FOCUS | B175 | SIM GATE: Verify ATR% TextBox accepts keyboard input without triggering NT8 instrument search. Steps: (1) Click in ATR% TextBox. (2) Type a number (e.g., "1"). Verify: (a) no instrument search dialog appears, (b) the typed character appears in the box. Close upon Director SIM confirmation. | P1 | OPEN |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174. Steps: (1) Open two separate chart windows: one MES, one MGC. (2) Wait ~2s for ATR poll timer to fire on both panels. (3) Confirm MES panel shows MES ATR value and MGC panel shows MGC ATR value -- the two values must differ. Close upon Director SIM confirmation. | P1 | OPEN -- SIM pending |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader -- confirm follower stop preserved (not missing) after drag. B171 fix deployed (unsuffixed stop name was root cause of stop-drag sync failure). SIM verification required; close upon Director SIM confirmation. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified -- Sim103/104 PASS, Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. Blocks DW-B169-K1 Sim102. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## Appendix A — Existing Test Regression Guard

The Wilder's change MUST NOT break existing B173/B174 tests. Verification:

**T_B173_T1_02 / T_B174_T1_01** both use:
- `highs  = { 2.0, 3.0, 4.0, 3.0 }` (length=4), `period=3`
- Array length 4 = period+1 = 4. Check: `4 < 2*3+1 = 7`. TRUE. → Fallback path (simple avg).
- Result: seed = (1.5 + 2.0 + 2.0) / 3 = 5.5/3. Wilder's block NOT entered.
- Expected assertion `Assert.Equal(5.5/3.0, result)` → STILL PASSES. ✓

**All other B173/B174 tests** use guard paths (null arrays, period+1 length) that return 0.0.
These paths are unchanged. All pass. ✓

---

## Appendix B — Wilder's Formula Derivation

Wilder's ATR (period P) is defined as:
```
ATR[P]   = (TR[1] + TR[2] + ... + TR[P]) / P          -- simple avg seed
ATR[P+k] = (ATR[P+k-1] * (P-1) + TR[P+k]) / P        -- for k = 1, 2, ..., P
```

This is equivalent to EMA with alpha = 1/P applied after the seed.
The implemented loop (i = period+1 to 2*period) applies exactly P additional smoothing steps.
This matches NT8's ATR indicator implementation for the standard 14-period setting.

For period=14: 2*14+1 = 29 bars minimum for full Wilder's. When chart has <29 bars loaded
(e.g., during initial warm-up), the simple average (seed) is returned, which matches the
B173/B174 behaviour. No regression.

---

## Appendix C — Threading Model Summary

| Component | Thread | Mechanism |
|---|---|---|
| AtrCalc.Compute(double[]) | Any (pure math, no shared state) | Static method |
| AtrCalc.Compute(ChartControl) | UI thread only | Called from DispatcherTimer.Tick |
| AtrCalc.ReadBarsToArrays | UI thread only | Called from Compute(ChartControl) |
| OnAtrBoxPreviewKeyDown | UI thread only | WPF PreviewKeyDown event guarantee |
| _atrFractionBox.IsKeyboardFocused | UI thread only | DependencyProperty read in UI handler |

No Dispatcher.InvokeAsync required (all new code is already on UI thread or thread-agnostic).
No lock() required (JS-021 PASS). No ConcurrentQueue changes.
