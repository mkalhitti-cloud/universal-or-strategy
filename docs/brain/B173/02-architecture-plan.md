# B173 — AtrWidget Live ATR Fix
# Architecture Plan — Phase 1
# Author: ptt-architect
# Status: PLAN_COMPLETE

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Gate inputs:**
- Q1. Same method or within 50 lines? **YES** — All changes are part of one data flow (AtrWidget poll → AtrCalc → display)
- Q2. Fix B design depends on Fix A final design? **YES** — AtrWidget.OnTimerTick calls AtrCalc.Compute; WireNewPanel calls panel.SetAtrChartControl — both depend on AtrCalc.Compute signature
- Q3. Each fix standalone value if the other is blocked? **NO** — deleting AtrSizingEngine without AtrCalc replacement = broken build
- Q4. Each fix independent SIM verification path? **NO** — single verification path: "widget shows ATR value"

**Decision: SINGLE-PIPELINE. No lane split.**

---

## Root Cause (confirmed — do not re-investigate)

`AtrSizingEngine extends NinjaTrader.NinjaScript.Indicators.Indicator`. It is instantiated via
`new AtrSizingEngine()` in `TradeCopierAddOn.WireNewPanel` (line 397). An Indicator created with `new`
outside the NinjaScript pipeline has no `BarsArray`, no `CurrentBar`, no data series.
`ManualOnBarUpdate()` calls `OnBarUpdate()` which accesses `ATR(Period)[0]` — this silently throws
(caught by the try/catch in `AtrWidget.OnTimerTick`) or returns 0.0. `_hasData` never becomes true.
Widget blank forever. `DW-B172-01` is the bug tracker item.

**Fix:** Replace `AtrSizingEngine` with `AtrCalc` — a pure static helper that reads bar data directly
from the already-live `ChartControl` visual tree object obtained in `WireNewPanel`.

**This epic closes: DW-B172-01.**

---

## Key NT8 Facts Embedded (mandatory — never rely on memory)

- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT AddOnBase. Not relevant here.
- `AtmStrategyCreate()` — StrategyBase-only. NOT AddOnBase. Not relevant here.
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets. Not relevant here.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available. Not relevant here.
- `ChartControl` — accessible from AddOnBase via `FindVisualChild<ChartControl>(chart)` (confirmed line 213 TradeCopierAddOn.cs).
- `Chart.ChartControl` property — NOT accessible from AddOnBase (confirmed comment line 200 TradeCopierAddOn.cs).

---

## Component List

| Component | Action | File |
|-----------|--------|------|
| `AtrSizingEngine` | **DELETE** entire file | `src/PropTraderTools/AtrSizingEngine.cs` |
| `AtrCalc` | **NEW** internal static class | `src/PropTraderTools/AtrCalc.cs` |
| `AtrWidget` (inner class) | **SURGICAL** — remove provider pattern, add ChartControl pattern | `src/PropTraderTools/TradeCopierPanel.cs` |
| `TradeCopierPanel` (outer class) | **SURGICAL** — rename wiring method | `src/PropTraderTools/TradeCopierPanel.cs` |
| `TradeCopierAddOn.WireNewPanel` | **SURGICAL** — remove AtrSizingEngine creation, wire ChartControl | `src/PropTraderTools/TradeCopierAddOn.cs` |
| `B173AtrCalcTests` | **NEW** test class | `src/PropTraderTools/CopyEngineTests.cs` |

---

## Section 1: DELETE — AtrSizingEngine.cs

**File:** `src/PropTraderTools/AtrSizingEngine.cs`

Delete the entire file (65 lines). No partial removal — the class is entirely replaced by `AtrCalc`.

**References to remove in other files (see Sections 3 and 4):**
- `TradeCopierPanel.cs`: `private AtrSizingEngine _provider;` field, `SetProvider(AtrSizingEngine)` method, `SetAtrProvider(AtrSizingEngine)` method
- `TradeCopierAddOn.cs`: `var engine = new AtrSizingEngine();` (line 397), `panel.SetAtrProvider(engine);` (line 398)

---

## Section 2: NEW FILE — AtrCalc.cs

**File:** `src/PropTraderTools/AtrCalc.cs`
**Namespace:** `PropTraderTools`
**Using directives:** `using System;`, `using NinjaTrader.Gui.Chart;`

### Class Signature

```csharp
internal static class AtrCalc
```

### Method 1 — Pure Math Overload (primary, testable, no NT8 dependency)

```csharp
internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
```

**Contract:**
- Guard: if `highs == null || lows == null || closes == null` → return `0.0`
- Guard: if any array length `< period + 1` → return `0.0` (need `period` TR values, each TR needs `closes[i-1]`)
- Guard: if `period < 1` → return `0.0`
- **Array ordering:** index `0` = oldest bar, index `length-1` = most recent bar (chronological)
- **Formula:** For `i` in `[1 .. period]`:
  ```
  pc = closes[i - 1]
  tr[i] = Max(highs[i] - lows[i], Abs(highs[i] - pc), Abs(lows[i] - pc))
  ```
  `ATR = sum(tr[1..period]) / period`
- **JS-001:** Entire body wrapped in try/catch; return `0.0` on any exception
- **CYC target:** <= 6 (null guards × 3 + period guard + for loop + try/catch = 5-6)
- **ASCII-only:** no Unicode string literals

### Method 2 — NT8 Runtime Overload

```csharp
internal static double Compute(NinjaTrader.Gui.Chart.ChartControl cc, int period)
```

**Contract:**
- Guard: if `cc == null` → return `0.0`
- Guard: if `cc.BarsArray == null || cc.BarsArray.Count == 0` → return `0.0`
- Extract `Bars bars = cc.BarsArray[0].Bars;`
- Guard: if `bars == null || bars.Count < period + 1` → return `0.0`
- Extract arrays (chronological order — index 0 = oldest, matching primary overload convention):
  ```
  For k = 0 to period (inclusive):
      barsAgo = period - k    // k=0 → oldest (barsAgo=period), k=period → newest (barsAgo=0)
      highs[k]  = bars.GetHigh(barsAgo)
      lows[k]   = bars.GetLow(barsAgo)
      closes[k] = bars.GetClose(barsAgo)
  ```
- Delegate: `return Compute(highs, lows, closes, period);`
- **JS-001:** Entire body wrapped in try/catch; return `0.0` on any exception
- **CYC target:** <= 5 (null guards × 3 + for loop + try/catch)

### ARCHITECT NOTE — NT8 API Verification Required at Implementation

`ChartControl.BarsArray` and `ChartBars.Bars.GetHigh/GetLow/GetClose` are NOT documented in
`docs/standards/NT8_FULL_REFERENCE.md` at the time of this plan. They are standard NT8 public API
present in `NinjaTrader.Gui.dll`.

**Engineer (ptt-engineer) MUST:**
1. Confirm `ChartControl.BarsArray` property type and accessor syntax by inspecting `NinjaTrader.Gui.dll`
   (e.g., Object Browser in Visual Studio or `ildasm NinjaTrader.Gui.dll`)
2. Confirm `ChartBars.Bars` accessor (may be `.Bars` or direct indexer)
3. Confirm `Bars.GetHigh(int barsAgo)` / `GetLow` / `GetClose` exist with this exact signature
4. If any API differs, use the correct form and update `docs/standards/NT8_FULL_REFERENCE.md` with the
   confirmed entry before marking the ticket complete
5. If `GetHigh/GetLow/GetClose` are not the correct methods, the alternative is indexer access:
   `bars[barsAgo].High` / `.Low` / `.Close` — verify which form compiles

**SCAN-07 constraint:** Build must produce zero errors/warnings after AtrCalc.cs is added.

---

## Section 3: SURGICAL CHANGES — TradeCopierPanel.cs

### 3a: AtrWidget Inner Class (lines 3155–3291)

**File:** `src/PropTraderTools/TradeCopierPanel.cs`

#### Fields — remove old, add new

**REMOVE:**
```csharp
private AtrSizingEngine _provider;  // line 3168 (approximately)
```

**ADD:**
```csharp
private NinjaTrader.Gui.Chart.ChartControl _chartControl;
```

The type `ChartControl` is already imported via `using NinjaTrader.Gui.Chart;` at line 109 of the file.
Use the unqualified name `ChartControl _chartControl` in the field declaration.

#### SetProvider() → SetChartControl()

**REMOVE** entire `SetProvider(AtrSizingEngine engine)` method (lines ~3211–3226):
```csharp
internal void SetProvider(AtrSizingEngine engine) { ... }
```

**ADD** replacement method:
```csharp
// --- SetChartControl ---
// Called from TradeCopierPanel.SetAtrChartControl (called from TradeCopierAddOn.WireNewPanel).
// Stores ChartControl ref and starts/restarts the polling timer. CYC=2: null guard + timer create-once.
// UI thread only (called from Dispatcher.InvokeAsync context).
internal void SetChartControl(ChartControl cc)
{
    _chartControl = cc;
    if (_atrPollTimer == null)
    {
        _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = System.TimeSpan.FromSeconds(1),
        };
        _atrPollTimer.Tick += OnTimerTick;
    }
    if (cc != null)
        _atrPollTimer.Start();
    else
        _atrPollTimer.Stop();
}
```

**JS-021:** no lock. **CYC=2.** UI thread only.

#### Detach() — clear _chartControl

The existing `Detach()` method (lines ~3231–3239) sets `_provider = null` after stopping the timer.

**CHANGE** the `_provider = null;` line to:
```csharp
_chartControl = null;
```

Full updated `Detach()`:
```csharp
internal void Detach()
{
    if (_atrPollTimer != null)
    {
        _atrPollTimer.Stop();
        _atrPollTimer = null;
    }
    _chartControl = null;
}
```

**CYC=2.** No other changes.

#### OnTimerTick() — replace ManualOnBarUpdate() pattern

**OLD** (lines ~3244–3261):
```csharp
private void OnTimerTick(object sender, EventArgs e)
{
    if (_provider == null)
        return;
    try
    {
        _provider.ManualOnBarUpdate();
    }
    catch (System.Exception)
    { /* NT8 context not ready; next tick will retry */ }
    if (!_hasData && _provider.HasData)
        _hasData = true;
    if (_hasData)
    {
        _lastAtr = _provider.GetLastAtr();
        RecalcDisplay();
    }
}
```

**NEW:**
```csharp
// --- OnTimerTick ---
// DispatcherTimer fires on UI thread. CYC=3 (base + if-null + if-atr>0 + if-hasData).
// JS-021: no lock. JS-023: _hasData volatile bool read. NT8-003 PASS: no volatile double.
// AtrCalc.Compute is JS-001 safe (never throws) -- no try/catch needed here.
private void OnTimerTick(object sender, EventArgs e)
{
    if (_chartControl == null)
        return; // guard (1): ChartControl not wired yet
    double atr = AtrCalc.Compute(_chartControl, 14);
    if (atr > 0.0)
    {
        _lastAtr = atr;   // non-volatile: sizing hint only (NT8-003 PASS)
        _hasData = true;  // latch: set once, never cleared (volatile bool JS-023 PASS)
    }
    if (_hasData)         // guard (2): display only after first valid bar
        RecalcDisplay();
}
```

**CYC=3.** Improvement from previous CYC=6. No try/catch (AtrCalc is JS-001 safe). No lock.

**Comment update:** Update the class-level comment to reflect CYC=3 and removal of try/catch.

### 3b: TradeCopierPanel Outer Class — wiring method rename

**File:** `src/PropTraderTools/TradeCopierPanel.cs`

**REMOVE** `SetAtrProvider` method (lines ~3077–3082):
```csharp
internal void SetAtrProvider(AtrSizingEngine engine)
{
    _widget?.SetProvider(engine);
}
```

**ADD** replacement:
```csharp
// B173: SetAtrChartControl -- called from TradeCopierAddOn.WireNewPanel after obtaining ChartControl.
// CYC=1: delegation. JS-021: no lock. UI thread only (called from Dispatcher.InvokeAsync).
internal void SetAtrChartControl(ChartControl cc)
{
    _widget?.SetChartControl(cc);
}
```

**CYC=1.** No lock. UI thread only.

---

## Section 4: SURGICAL CHANGES — TradeCopierAddOn.cs

**File:** `src/PropTraderTools/TradeCopierAddOn.cs`

### WireNewPanel — lines 396-399 (verified from file read)

**Current code (lines 396-399):**
```csharp
var instr = TrySetPanelInstrument(chartTrader, panel);  // line 396
var engine = new AtrSizingEngine();                      // line 397 -- DELETE
panel.SetAtrProvider(engine);                            // line 398 -- DELETE
panel.SetChart(chart);                                   // line 399 -- KEEP
```

**New code:**
```csharp
var instr = TrySetPanelInstrument(chartTrader, panel);
var cc = FindVisualChild<ChartControl>(chart);
panel.SetAtrChartControl(cc);
panel.SetChart(chart);
```

**Notes:**
- `FindVisualChild<ChartControl>` is an existing static method on `TradeCopierAddOn` (confirmed lines 492-503). No new helper needed.
- `ChartControl` is already in scope via `using NinjaTrader.Gui.Chart;` (confirmed line 28 TradeCopierAddOn.cs).
- `cc` may be null if the chart visual tree is not yet fully loaded. `AtrWidget.SetChartControl(null)` calls `_atrPollTimer?.Stop()` — safe null path.
- `panel.SetChart(chart)` at line 399 is **unchanged**. The net change is: delete 2 lines, add 2 lines.
- WireNewPanel's documented CCN=5 increases to CCN=5 (no branch added — cc assignment is straight-line).

**ARCHITECT NOTE:** `cc` is obtained at `WireNewPanel` time. If the chart renders bars before the panel opens (the typical case), `cc.BarsArray` will be populated. If the chart has no bars yet, `AtrCalc.Compute` returns 0.0 and the timer retries every second — eventually showing the value once bars load. This is the correct polling behavior.

---

## Section 5: NEW TEST CLASS — CopyEngineTests.cs

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Location:** Append a new standalone class after `B172AtrWidgetTests` (after line 10219, before the final `}` at line 10221).

### Test Class

```csharp
// =====================================================================
// B173: AtrCalc static helper tests (T-B173-T1-01, T-B173-T1-02)
// Pure arithmetic -- no NT8 runtime required. No [Skip].
// Standalone class: no CopyEngine.Instance dependency.
// =====================================================================
public class B173AtrCalcTests
{
    // T-B173-T1-01: Verify guard -- arrays shorter than period+1 return 0.0
    [Fact]
    public void AtrCalc_Compute_ReturnsZero_WhenInsufficientBars()
    {
        // 3 bars with period=14: need period+1=15 bars minimum. Returns 0.0.
        double result = AtrCalc.Compute(new double[3], new double[3], new double[3], 14);
        Assert.Equal(0.0, result);
    }

    // T-B173-T1-02: Verify correct ATR(3) value for known OHLC data.
    // Array ordering: index 0 = oldest bar.
    // highs  = [2.0, 3.0, 4.0, 3.0]
    // lows   = [1.0, 2.0, 2.0, 1.0]
    // closes = [1.5, 2.5, 3.0, 2.0]
    // TR[1]: max(3-2, |3-1.5|, |2-1.5|) = max(1.0, 1.5, 0.5) = 1.5
    // TR[2]: max(4-2, |4-2.5|, |2-2.5|) = max(2.0, 1.5, 0.5) = 2.0
    // TR[3]: max(3-1, |3-3.0|, |1-3.0|) = max(2.0, 0.0, 2.0) = 2.0
    // ATR(3) = (1.5 + 2.0 + 2.0) / 3 = 5.5 / 3
    [Fact]
    public void AtrCalc_Compute_CorrectValue_KnownBars()
    {
        double[] highs  = { 2.0, 3.0, 4.0, 3.0 };
        double[] lows   = { 1.0, 2.0, 2.0, 1.0 };
        double[] closes = { 1.5, 2.5, 3.0, 2.0 };
        double result = AtrCalc.Compute(highs, lows, closes, 3);
        Assert.Equal(5.5 / 3.0, result);
    }
}
```

**No [Skip] attribute** — pure arithmetic, no NT8 runtime required.

---

## Section 6: Jane Street Rules Summary

| Rule | Application in B173 | Status |
|------|---------------------|--------|
| JS-001: no throw | `AtrCalc.Compute` both overloads: try/catch → return 0.0 | ENFORCED |
| JS-002: no return null | Both overloads return `double` — null impossible | PASS (trivial) |
| JS-003: no mutable captured closure state across threads | No lambdas capturing mutable state. DispatcherTimer is UI-thread-only | PASS |
| JS-008: WPF brushes Freeze via MakeBrush | No brush changes in this block | PASS (no scope) |
| JS-021: no lock | No lock() anywhere. Static class has no shared state. | ENFORCED |
| JS-023: volatile only for bool/int | `_hasData` volatile bool kept. `_lastAtr` non-volatile double. `_chartControl` written and read on UI thread — no volatile needed. | ENFORCED |
| NT8-003: volatile double BANNED | `_lastAtr` non-volatile. No new volatile double introduced. | ENFORCED |
| ASCII-only | No Unicode string literals anywhere in AtrCalc.cs or changes | ENFORCED |
| No DateTime.Now | Not used in this block | PASS |
| No hex color literals | Not used in this block | PASS |
| No FontFamily | Not used in this block | PASS |

---

## Section 7: Threading Model

All AtrWidget operations remain on the UI thread:

| Operation | Thread | Mechanism |
|-----------|--------|-----------|
| `WireNewPanel` calls `panel.SetAtrChartControl(cc)` | UI thread | Inside `chart.Dispatcher.InvokeAsync(() => DoInject(...))` |
| `AtrWidget.SetChartControl(cc)` | UI thread | Called from `SetAtrChartControl` |
| `DispatcherTimer.Tick` → `OnTimerTick` | UI thread | `DispatcherTimer` always fires on dispatcher thread |
| `AtrCalc.Compute(_chartControl, 14)` | UI thread | Called from `OnTimerTick` |
| `ChartControl.BarsArray[0].Bars` read | UI thread | Same UI thread as chart rendering |
| `_chartControl` field write | UI thread | `SetChartControl` on UI thread |
| `_chartControl` field read | UI thread | `OnTimerTick` on UI thread |

**No cross-thread state sharing. No ConcurrentQueue needed. No Dispatcher.InvokeAsync inside AtrCalc.**

`_chartControl` is written once (at panel wire-up) and read repeatedly (on timer tick). Both operations on the same UI thread → no volatile needed. `_hasData` volatile bool is kept (harmless, documents latch intent).

---

## Section 8: Data Flow

```
WireNewPanel (UI thread)
  └─ FindVisualChild<ChartControl>(chart) → cc (live chart already rendering bars)
  └─ panel.SetAtrChartControl(cc)
       └─ _widget?.SetChartControl(cc)
            └─ _chartControl = cc
            └─ DispatcherTimer.Start()

Every 1 second (UI thread):
  OnTimerTick()
  └─ if _chartControl == null → return (guard)
  └─ AtrCalc.Compute(_chartControl, 14)
       └─ cc.BarsArray[0].Bars → bars (live data from chart)
       └─ extract highs[], lows[], closes[] (period+1 bars)
       └─ Compute(highs, lows, closes, 14)
            └─ compute TR per bar → ATR = sum/14
            └─ return ATR value
  └─ if atr > 0.0: _lastAtr = atr, _hasData = true
  └─ if _hasData: RecalcDisplay()
       └─ _atrResultLabel.Text = ((int)Math.Ceiling(_lastAtr * _atrFraction)) + " pts"
```

---

## Section 9: CYC Summary

| Method | CYC | Status |
|--------|-----|--------|
| `AtrCalc.Compute(double[], double[], double[], int)` | 5-6 | PASS |
| `AtrCalc.Compute(ChartControl, int)` | 4-5 | PASS |
| `AtrWidget.SetChartControl(ChartControl)` | 2 | PASS (same as old SetProvider) |
| `AtrWidget.OnTimerTick` (new) | 3 | PASS (improved from 6) |
| `AtrWidget.Detach` | 2 | PASS (unchanged) |
| `TradeCopierPanel.SetAtrChartControl` | 1 | PASS |

All methods ≤ 6. No method exceeds CYC=8 limit.

---

## Section 10: File Change Summary

| File | Action | Net Lines |
|------|--------|-----------|
| `src/PropTraderTools/AtrSizingEngine.cs` | DELETE | -65 |
| `src/PropTraderTools/AtrCalc.cs` | NEW | +~55 |
| `src/PropTraderTools/TradeCopierPanel.cs` | SURGICAL | -~20 / +~18 |
| `src/PropTraderTools/TradeCopierAddOn.cs` | SURGICAL | -2 / +2 |
| `src/PropTraderTools/CopyEngineTests.cs` | APPEND | +~30 |

**Estimated total diff size:** ~200 lines net change. Well within 10,000-character diff limit.

---

## Section 11: Ticket Structure (for Phase 3)

**T1 — AtrCalc.cs new file + tests**
- Create `src/PropTraderTools/AtrCalc.cs`
- Append `B173AtrCalcTests` class to `src/PropTraderTools/CopyEngineTests.cs`
- No NT8 dependency for tests (pure math overload only tested)

**T2 — TradeCopierPanel.cs surgical changes**
- AtrWidget: remove `_provider` field, remove `SetProvider()`, add `_chartControl` field, add `SetChartControl()`, update `OnTimerTick()`, update `Detach()`
- Outer class: remove `SetAtrProvider()`, add `SetAtrChartControl()`
- Depends on T1 (AtrCalc.cs must exist for `AtrCalc.Compute` call in `OnTimerTick`)

**T3 — TradeCopierAddOn.cs + delete AtrSizingEngine.cs**
- Delete `src/PropTraderTools/AtrSizingEngine.cs`
- WireNewPanel: remove lines 397-398, add `cc` local + `panel.SetAtrChartControl(cc)`
- Depends on T2 (panel must expose `SetAtrChartControl` method)
- **Run ptt-sync.ps1 after this ticket** (hard link resync required after file delete)

---

## Section 12: Deferred Backlog

| ID | Description | Status | Action |
|----|-------------|--------|--------|
| DW-B172-01 | SIM GATE: AtrWidget live ATR display in NT8 | OPEN → **CLOSED by B173** | This epic fixes the root cause. Director SIM verification required to close. |

All other prior open items (DW-B170-SIM, B167-DEFER-02, etc.) are unaffected by this block.

---

## Section 13: 7-Scan Checklist (Pre-Implementation)

| Scan | Check | Expected |
|------|-------|----------|
| SCAN-01 | `grep -r "lock(" src/` | Zero matches |
| SCAN-02 | `grep -r "DateTime\.Now" src/` | Zero matches |
| SCAN-03 | `grep -r "volatile double" src/` | Zero matches |
| SCAN-04 | `grep -r "AtrSizingEngine" src/` | Zero matches (file deleted, all references removed) |
| SCAN-05 | CYC of all new/modified methods | All ≤ 6 |
| SCAN-06 | `grep -rP "[^\x00-\x7F]" src/` | Zero matches (ASCII-only) |
| SCAN-07 | Build: `dotnet build src/PropTraderTools/` | Zero errors, zero warnings |

---

*Plan written: B173 Phase 1. Ready for ptt-plan-reviewer (Phase 2).*
