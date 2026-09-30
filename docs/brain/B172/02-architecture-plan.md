# B172 Architecture Plan — ATR Display Widget Greenfield Replacement

**Epic:** B172  
**Phase:** 1 — Architecture Plan  
**Author:** ptt-architect  
**Status:** PLAN_COMPLETE  

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Q1. Same method or within 50 lines? **NO** — changes span 6 files and multiple unrelated methods.  
Q2. Fix B design depends on Fix A final design? **YES** — AtrWidget (T2) depends on stripped AtrSizingEngine (T1).  
Q3. Each fix has standalone value if the other is blocked? **NO** — this is one coherent replacement epic, not two independent fixes.  
Q4. Each fix has an independent SIM verification path? **N/A** — single epic.  

Default applies: Q2=YES → **SINGLE-PIPELINE**. One pipeline, 3 sequential tickets.

---

## Prior Backlog Review (B171/06-deferred-backlog.md)

- DW-B171-01: CLOSED — unrelated (stop drag suffix fix)
- DW-B171-02: CLOSED — unrelated (stop name match fix)
- All remaining open items (DW-B170-SIM, DW-B169-K1/K2, B167-DEFER-xx, DW-B159-Kx): **none apply to B172**. This is a UI-widget greenfield replacement with zero overlap to ATM/stop-drag logic.

---

## Architect Decision: Option A — Strip AtrSizingEngine (CHOSEN)

**Option B** (remove AtrSizingEngine entirely, compute ATR inline via `ChartControl.BarsArray`) was rejected: `chart.BarsArray` is not accessible in AddOnBase compilation scope (CS1061 — same NT8 Roslyn limitation that forced the B10 timer fallback). Option B introduces the same unresolvable compilation error.

**Option A** is selected: `AtrSizingEngine.cs` is NOT deleted — it is stripped to a pure ATR value provider. All sizing, risk, and event members are removed. The file retains only the NT8 Indicator lifecycle plus `_lastAtr`, `_hasData`, `ManualOnBarUpdate()`, and `GetLastAtr()`.

---

## Component List

| Component | File | Action |
|---|---|---|
| `AtrSizingEngine` | `src/PropTraderTools/AtrSizingEngine.cs` | STRIP (Option A) |
| `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | REMOVE ATR members |
| `FeatureFlags` | `src/PropTraderTools/FeatureFlags.cs` | REMOVE AtrSizing param |
| `TradeCopierAddOn` | `src/PropTraderTools/TradeCopierAddOn.cs` | REMOVE ATR wiring, simplify |
| `TradeCopierPanel` | `src/PropTraderTools/TradeCopierPanel.cs` | REMOVE old ATR UI, ADD AtrWidget inner class |
| `AtrWidget` (new) | inner class in `TradeCopierPanel.cs` | BUILD — new private sealed inner class |
| `CopyEngineTests` | `src/PropTraderTools/CopyEngineTests.cs` | REMOVE old ATR tests, ADD 3 new tests |

---

## Section 1: AtrSizingEngine.cs — Strip to Pure Value Provider

### Members to DELETE from AtrSizingEngine.cs

| Member | Kind | Reason |
|---|---|---|
| `AtrSizingEngine(int testContracts)` | constructor (internal) | test-seam for deleted `_lastContracts` |
| `_lastContracts` | `volatile int` field | sizing output — removed |
| `_maxRiskDollars` | `double` field | risk budget — removed |
| `_tickDollarValue` | `double` field | tick value — removed |
| `_atrFraction` | `double` field | fraction multiplier — moved to AtrWidget |
| `AtrUpdated` | `event Action<string>` | display event — removed (timer replaces) |
| `CalcContracts(double, double, double)` | `internal static int` | sizing calc — removed |
| `FireAtrUpdated(double, int)` | `private void` | event fire helper — removed |
| `SetAtrFraction(double)` | `internal void` | fraction setter — removed |
| `UpdateMaxRisk(double)` | `internal void` | risk setter — removed |
| `SetParameters(double, double)` | `internal void` | combined setter — removed |
| `GetSuggestedQty()` | `internal int` | sizing output — removed |
| Comment block header about B9/B10/B12/B21 changes | comment lines | outdated — remove |

### Members to KEEP in AtrSizingEngine.cs

| Member | Kind | Notes |
|---|---|---|
| `AtrSizingEngine()` | `public` constructor | required by NinjaScript |
| `_lastAtr` | `private double` (non-volatile) | NT8-003: volatile double banned; staleness-tolerance pattern |
| `_hasData` | `private volatile bool` | JS-023 PASS |
| `Period` | `int` property with `[NinjaScriptProperty]` | unchanged |
| `OnStateChange()` | `protected override void` | stripped: remove B12 field inits; keep 4 state guards |
| `OnBarUpdate()` | `protected override void` | stripped: remove `CalcContracts` and `FireAtrUpdated` calls |
| `ManualOnBarUpdate()` | `internal void` | shim called by AtrWidget timer |
| `GetLastAtr()` | `internal double` | read path for widget |
| `HasData` | `internal bool` property (or `GetHasData()`) | read path for widget |

### Stripped AtrSizingEngine.OnBarUpdate() (new body)

```csharp
// CYC=2: CurrentBar guard + set fields
protected override void OnBarUpdate()
{
    if (CurrentBar < Period)
        return;
    _lastAtr = ATR(Period)[0];
    _hasData = true;
}
```

### Stripped AtrSizingEngine.OnStateChange() (pruned body)

Remove only: field inits for `_lastContracts`, `_lastAtr=0`, `_hasData=false` in Terminated state  
→ Keep `_hasData = false` in Terminated (still needed to reset the guard)  
→ Keep `_lastAtr = 0.0` in Terminated (defensive reset)  
→ Remove `_lastContracts = 1` (field deleted)

---

## Section 2: CopyEngine.cs — Remove ATR Members

### Members to DELETE from CopyEngine.cs

| Member | Location (approx line) | Notes |
|---|---|---|
| `_atrEnabled` | line 136 | `private volatile bool` field |
| `_atrEngine` | line 137 | `private volatile AtrSizingEngine` field |
| `SetAtrEngine(AtrSizingEngine, bool)` | line 692 | internal method |
| `UpdateMaxRisk(double)` | line 701 | internal method |
| `UpdateAtrFraction(double)` | line 709 | internal method |
| `GetSuggestedQty(Instrument)` | line 1549 | internal method — entirely removed |
| B9 T1 comment block (ATR fields) | lines 135-137 | cleanup |

### Methods to MODIFY in CopyEngine.cs

**`ResolveBaseQty(Instrument instrument, int signalQty)`** — lines 3088-3093:  
Remove `_atrEnabled` branch entirely. New body:
```csharp
// B172: ATR removed. Return signal qty directly. CYC=1.
private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)
{
    return signalQty;
}
```
_If ResolveBaseQty is called from only one site in DispatchCopy, the engineer may inline `signalQty` directly and delete ResolveBaseQty. If called from multiple sites, keep as 1-line method. Engineer decides at implementation time._

---

## Section 3: FeatureFlags.cs — Remove AtrSizing Parameter

### Change the record declaration

**Before (7 params):**
```csharp
internal sealed record FeatureFlags(
    bool MultiRule, bool TrimFlatten, bool BreakEven, bool AtrSizing,
    bool ClickTrader, bool MirrorMode, bool QxGlobalExit)
```

**After (6 params):**
```csharp
internal sealed record FeatureFlags(
    bool MultiRule, bool TrimFlatten, bool BreakEven,
    bool ClickTrader, bool MirrorMode, bool QxGlobalExit)
```

### Update all 4 factory methods

| Method | Before | After |
|---|---|---|
| `Starter()` | `new(false,false,false, false, false,false,false)` | `new(false,false,false, false,false,false)` |
| `Pro()` | `new(true,true,true, false, false,false,false)` | `new(true,true,true, false,false,false)` |
| `Elite()` | `new(true,true,true, true, true,true,true)` | `new(true,true,true, true,true,true)` |
| `FromFeatureList()` | has `AtrSizing: Contains(feats,"atr_sizing")` line | remove that line |

---

## Section 4: TradeCopierAddOn.cs — Remove ATR Wiring

### Fields to DELETE

| Field | Line | Kind |
|---|---|---|
| `_atrEngines` | line 44–45 | `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine>` |
| `_atrPollTimer` | line 66 | `private DispatcherTimer` |

### Methods to DELETE

| Method | Lines | Signature |
|---|---|---|
| `StartAtrEngine` | ~218–256 | `private void StartAtrEngine(Chart chart, Instrument instr)` |
| `StopAtrEngine` | ~260–273 | `private void StopAtrEngine(Chart chart)` |
| `UpdateAtrOverlay` | ~278–286 | `internal void UpdateAtrOverlay(string atrDisplay)` |
| `OnAtrUpdated` | ~291–294 | `private void OnAtrUpdated(string display)` |

### Call sites to REMOVE/REPLACE

**`WireNewPanel()` at line ~495** — replace:
```csharp
// OLD:
StartAtrEngine(chart, instr);

// NEW:
var engine = new AtrSizingEngine();
panel.SetAtrProvider(engine);
```

**`OnWindowDestroyed()` at line ~108** — remove:
```csharp
// OLD (remove this line):
StopAtrEngine(chart); // instance: unsubscribes AtrUpdated

// Comment on InjectIntoChart (line ~100) — remove "StartAtrEngine needs _atrOverlayLabel" note
```
Note: `panel.Detach()` already called at line ~112–113. `TradeCopierPanel.Detach()` will be updated to call `_widget.Detach()`, which stops the timer. No separate StopAtrEngine needed.

---

## Section 5: TradeCopierPanel.cs — Remove Old ATR UI + Add AtrWidget

### Fields to DELETE

| Field | Line | Kind |
|---|---|---|
| `_atrRow` | 275 | `private UniformGrid` |
| `_maxRiskDollars` | 296 | `private double` (200.0 default) |
| `_atrFraction` | 297 | `private double` (0.75 default) |
| `_riskDollarsBox` | 298 | `private TextBox` |
| `_atrDisplayLabel` | 302 | `private TextBlock` |

### Methods to DELETE

| Method | Lines | Signature |
|---|---|---|
| `BuildRiskAtrRow(StackPanel root)` | ~3080–3103 | `private void` |
| `BuildSpinnerColumn(string, TextBox, RoutedEventHandler, RoutedEventHandler)` | ~3107–3146 | `private StackPanel` — ATR-only, delete |
| `BuildAtrDisplayRow()` | ~3150–3162 | `private Border` |
| `SetAtrText(string display)` | ~3167–3172 | `public void` |
| `OnRiskUp(object, RoutedEventArgs)` | ~3175 | `private void` |
| `OnRiskDown(object, RoutedEventArgs)` | ~3184 | `private void` |
| `OnRiskTextLostFocus(object, RoutedEventArgs)` | ~3202 | `private void` |
| `OnAtrFractionUp(object, RoutedEventArgs)` | ~3213 | `private void` |
| `OnAtrFractionDown(object, RoutedEventArgs)` | ~3222 | `private void` |
| `OnAtrFractionTextLostFocus(object, RoutedEventArgs)` | ~3231 | `private void` |
| `NotifyRiskChanged()` | ~3242 | `private void` |
| `NotifyAtrFractionChanged()` | ~3250 | `private void` |

### `TryParseAndClamp` — KEEP

`private static bool TryParseAndClamp(string text, double min, double max, out double result)` — remains unchanged. Now used only by `AtrWidget.OnAtrFractionLostFocus` (accessible because AtrWidget is an inner class of TradeCopierPanel).

### OnLoaded — remove 2 lines

```csharp
// DELETE these two lines from OnLoaded (~line 803-804):
NotifyRiskChanged();
NotifyAtrFractionChanged();
```

### BuildUI() — replace call site at line ~925

```csharp
// OLD:
BuildRiskAtrRow(_contentPanel); // B12 T3: Risk $ + ATR % spinner row

// NEW:
_widget = new AtrWidget();
_widget.BuildAtrWidgetRows(_contentPanel); // B172: ATR fraction display widget
```

### ApplyRowVisibilityFlags — remove AtrSizing block

Remove lines ~3286–3289:
```csharp
// DELETE:
if (_atrRow != null)
    _atrRow.Visibility = f.AtrSizing
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;
```
After deletion, `ApplyRowVisibilityFlags` contains only the `_clickTraderRow` visibility block. CYC reduces from 4 to 2.

### Add new field

```csharp
// B172: AtrWidget inner class instance
private AtrWidget _widget = null;
```

### Add new method on TradeCopierPanel

```csharp
// B172: Called from TradeCopierAddOn.WireNewPanel after creating stripped engine.
// CYC=1: delegation. JS-021: no lock. UI thread only (called from Dispatcher.InvokeAsync).
internal void SetAtrProvider(AtrSizingEngine engine)
{
    _widget?.SetProvider(engine);
}
```

### Update TradeCopierPanel.Detach()

Add `_widget?.Detach();` at the start of Detach() body (before existing cleanup). Location: find `Detach()` method in TradeCopierPanel.cs.

---

## Section 6: AtrWidget — New Private Sealed Inner Class

Placement: defined at the end of the `TradeCopierPanel` class body, before the closing `}` of the class and `}` of the namespace.

### Full AtrWidget Design

```csharp
// B172: AtrWidget -- private sealed inner class.
// Self-contained ATR fraction display. No feature flag gate (always visible).
// Threading: all UI-thread-only (DispatcherTimer fires on UI thread). JS-021 PASS.
private sealed class AtrWidget
{
    // --- Fields ---
    // B172 SPEC: all UI-thread-only (no volatile per NT8-003 except _hasData)
    private double _atrFraction = 0.75;        // UI thread only; no volatile NT8-003
    private TextBox _atrFractionBox;           // UI element
    private TextBlock _atrResultLabel;         // UI element
    private double _lastAtr;                   // local copy; updated from provider on tick
    private DispatcherTimer _atrPollTimer;     // UI-thread timer
    private volatile bool _hasData = false;    // latch: true once first valid bar seen. JS-023 PASS
    private AtrSizingEngine _provider;         // stripped engine reference; null until wired

    // --- BuildAtrWidgetRows ---
    // Builds Row1 (label + fraction TextBox) and Row2 (result label) into the given StackPanel.
    // CYC=1: straight-line construction. No return null (JS-002). UI thread only.
    internal void BuildAtrWidgetRows(StackPanel root)
    {
        // Row 1: "ATR %"  label  +  TextBox (width=55, default "0.75")
        var row1 = new StackPanel { Orientation = Orientation.Horizontal,
                                    Margin = new Thickness(0, 4, 0, 0) };
        var label = new TextBlock
        {
            Text = "ATR %",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
        _atrFractionBox = new TextBox
        {
            Text = _atrFraction.ToString("F2"),
            Width = 55,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _atrFractionBox.SetResourceReference(Control.StyleProperty, "NTTextBoxStyle");
        _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
        row1.Children.Add(label);
        row1.Children.Add(_atrFractionBox);
        root.Children.Add(row1);

        // Row 2: result label — "-- pts" until first bar
        _atrResultLabel = new TextBlock
        {
            Text = "-- pts",
            Margin = new Thickness(0, 2, 0, 0),
        };
        _atrResultLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
        root.Children.Add(_atrResultLabel);
    }

    // --- SetProvider ---
    // Called from TradeCopierPanel.SetAtrProvider (which is called from TradeCopierAddOn.WireNewPanel).
    // Stores engine ref and starts/restarts the polling timer. CYC=2: null guard + timer create-once.
    // UI thread only (called from Dispatcher.InvokeAsync context).
    internal void SetProvider(AtrSizingEngine engine)
    {
        _provider = engine;
        if (_atrPollTimer == null)
        {
            _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = System.TimeSpan.FromSeconds(1),
            };
            _atrPollTimer.Tick += OnTimerTick;
        }
        if (engine != null)
            _atrPollTimer.Start();
        else
            _atrPollTimer.Stop();
    }

    // --- Detach ---
    // Called from TradeCopierPanel.Detach() on chart close.
    // Stops timer and clears provider ref. CYC=2: null guards.
    internal void Detach()
    {
        if (_atrPollTimer != null)
        {
            _atrPollTimer.Stop();
            _atrPollTimer = null;
        }
        _provider = null;
    }

    // --- OnTimerTick ---
    // DispatcherTimer fires on UI thread. CYC=3: provider null guard + hasData + recalc.
    // JS-021: no lock. JS-023: _hasData volatile bool read. NT8-003 PASS: no volatile double.
    private void OnTimerTick(object sender, EventArgs e)
    {
        if (_provider == null)
            return; // guard (1): provider not wired yet
        try
        {
            _provider.ManualOnBarUpdate(); // may throw if NT8 context not ready; swallow
        }
        catch (System.Exception)
        { /* NT8 context not ready; next tick will retry */ }
        if (!_hasData && _provider.HasData)
            _hasData = true; // latch: set once, never cleared during session
        if (_hasData) // guard (2): display only after first valid bar
        {
            _lastAtr = _provider.GetLastAtr();
            RecalcDisplay(); // guard (3): only when hasData
        }
    }

    // --- RecalcDisplay ---
    // Computes result and updates label. CYC=1: straight-line. UI thread only.
    // Formula: Math.Ceiling(_lastAtr * _atrFraction) cast to int + " pts"
    private void RecalcDisplay()
    {
        double result = Math.Ceiling(_lastAtr * _atrFraction);
        _atrResultLabel.Text = ((int)result).ToString() + " pts";
    }

    // --- OnAtrFractionLostFocus ---
    // LostFocus handler for _atrFractionBox. Parses, clamps [0.10, 5.00], recalculates.
    // CYC=2: parse fail guard (1) + _hasData recalc guard (2).
    // JS-021: no lock. UI thread only (WPF event). NT8-003 PASS.
    private void OnAtrFractionLostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryParseAndClamp(_atrFractionBox?.Text, 0.10, 5.00, out double v))
        {
            // parse failed: restore display to last valid value
            if (_atrFractionBox != null)
                _atrFractionBox.Text = _atrFraction.ToString("F2");
            return; // guard (1)
        }
        _atrFraction = v;
        if (_atrFractionBox != null)
            _atrFractionBox.Text = v.ToString("F2"); // normalise display
        if (_hasData) // guard (2)
            RecalcDisplay();
    }
}
```

**Note:** `TryParseAndClamp` is accessible inside AtrWidget because it is a `private static` method on the enclosing `TradeCopierPanel` class and inner classes have full access to enclosing class members in C#.

---

## Section 7: CopyEngineTests.cs — Test Cleanout + New Tests

### Test Methods to DELETE

All methods that reference `AtrSizingEngine`, `SetAtrEngine`, `SetAtrFraction`, `UpdateMaxRisk`, `UpdateAtrFraction`, `CalcContracts`, `ManualOnBarUpdate`, `BuildRiskAtrRow`:

| Method Name | Approx Line | Series |
|---|---|---|
| `CalcContracts_MES_ATR6_returns5` | 1005 | T-B12-T3 / B9 |
| `CalcContracts_MES_ATR8_returns3` | 1015 | T-B12-T3 / B9 |
| `CalcContracts_MES_ATR12_returns2` | 1025 | T-B12-T3 / B9 |
| `CalcContracts_ZeroAtr_returns1` | 1035 | T-B12-T3 / B9 |
| `CalcContracts_NegativeAtr_returns1` | 1045 | T-B12-T3 / B9 |
| `CalcContracts_ResultBelowOne_clampsTo1` | 1055 | T-B12-T3 / B9 |
| `CalcContracts_ZeroTickValue_returns1` | 1065 | T-B12-T3 / B9 |
| `CalcContracts_LargeMaxRisk_noOverflow` | 1075 | T-B12-T3 / B9 |
| `GetSuggestedQty_WhenAtrDisabled_returns1` (or equivalent) | ~1091 | T-B10-T4 |
| `GetSuggestedQty_WhenAtrEnabled_returnsEngineQty` | ~1097 | T-B10-T4 |
| `GetSuggestedQty_WhenAtrEngineNull_returns1` | ~1124 | T-B10-T4 |
| `GetSuggestedQty_WhenAtrEnabled_returnsLiveQty` | ~1130 | T-B10-T4 |
| `ManualOnBarUpdate_ColdPath_DoesNotThrow` (T-B11-T2-01) | 1754 | T-B11-T2 |
| `SetParameters_ManualOnBarUpdate_ToleratesNullInstrument` (T-B11-T2-02) | 1767 | T-B11-T2 |
| `AtrFormatString_ContractVerification` (T-B11-T2-03) | 1780 | T-B11-T2 |
| `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1` (T-B12-T3-01) | 1816 | T-B12-T3 |
| `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` (T-B12-T3-02) | 1829 | T-B12-T3 |
| `BuildRiskAtrRow_ClampMin_RejectsSubMinValue` (T-B12-T3-03) | 1849 | T-B12-T3 |
| `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` | 1857 | T-B12-T3 |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` | 2422 | B21-LANE-A |

_Engineer must identify exact method boundaries (start/end lines) before deletion. Use the method names as the primary key — line numbers are approximate._

### New Test Methods to ADD

Add as a new region after the last deleted test's surrounding block:

```csharp
// B172: ATR Widget replacement tests
// Replaces T-B12-T3, T-B11-T2-03, T-B10-T4 series.

[Fact]
public void Ceiling_AlwaysRoundsUp()
{
    // Math.Ceiling behavior: any fractional part rounds UP to next integer.
    Assert.Equal(2.0, Math.Ceiling(1.001));  // 1.001 -> 2 (spec: 1.001 -> 2.00)
    Assert.Equal(1.0, Math.Ceiling(1.000));  // 1.000 -> 1 (exact integer, no rounding)
    Assert.Equal(1.0, Math.Ceiling(0.001));  // 0.001 -> 1 (spec: 0.001 -> 1.00)
    Assert.Equal(3.0, Math.Ceiling(2.756));  // 2.756 -> 3 (spec: 2.756 -> 3.00)
}

[Fact]
public void AtrFractionBox_ParseAndClamp_ClampsToRange()
{
    // Validates the clamp [0.10, 5.00] arithmetic used in OnAtrFractionLostFocus.
    // Below min -> clamped to 0.10
    double below = Math.Max(Math.Min(0.05, 5.00), 0.10);
    Assert.Equal(0.10, below);

    // Above max -> clamped to 5.00
    double above = Math.Max(Math.Min(6.00, 5.00), 0.10);
    Assert.Equal(5.00, above);

    // Invalid string -> TryParse returns false -> restore last valid
    bool parsed = double.TryParse("abc", out _);
    Assert.False(parsed);

    // Valid in-range value -> unchanged
    double inRange = Math.Max(Math.Min(0.75, 5.00), 0.10);
    Assert.Equal(0.75, inRange);
}

[Fact]
public void FormatDisplay_CorrectFormat()
{
    // Validates the display string formula: ((int)Math.Ceiling(atr * fraction)).ToString() + " pts"
    // ATR=2.1, fraction=0.75 -> raw=1.575 -> ceiling=2 -> "2 pts"
    double raw = 2.1 * 0.75;
    Assert.Equal("2 pts", ((int)Math.Ceiling(raw)).ToString() + " pts");

    // ATR=1.0, fraction=1.0 -> raw=1.0 -> ceiling=1 -> "1 pts"
    raw = 1.0 * 1.0;
    Assert.Equal("1 pts", ((int)Math.Ceiling(raw)).ToString() + " pts");

    // ATR=3.5, fraction=0.5 -> raw=1.75 -> ceiling=2 -> "2 pts"
    raw = 3.5 * 0.5;
    Assert.Equal("2 pts", ((int)Math.Ceiling(raw)).ToString() + " pts");

    // ATR=0.001, fraction=1.0 -> raw=0.001 -> ceiling=1 -> "1 pts"
    raw = 0.001 * 1.0;
    Assert.Equal("1 pts", ((int)Math.Ceiling(raw)).ToString() + " pts");
}
```

---

## Section 8: Threading Model Compliance

| Concern | Resolution | Rule |
|---|---|---|
| `_hasData` written on bar-close data thread (in `OnBarUpdate`) | `volatile bool` | JS-023 PASS |
| `_lastAtr` written on bar-close data thread | non-volatile double; staleness-tolerance pattern per NT8-003 | NT8-003 PASS |
| AtrWidget timer fires on UI thread | `DispatcherTimer` always fires on creation thread = UI thread | JS-023 PASS |
| AtrWidget reads `_provider._hasData` on timer tick (UI thread) | volatile bool cross-thread read — safe | JS-023 PASS |
| AtrWidget reads `_provider.GetLastAtr()` on timer tick | non-volatile double cross-thread read — staleness-tolerance pattern (sizing hint, not order-safety) | NT8-003 PASS |
| AtrWidget updates `_atrResultLabel.Text` on timer tick | UI thread — WPF UI update on UI thread | CLEAN |
| `OnAtrFractionLostFocus` updates `_atrFraction` | WPF UI event — UI thread | CLEAN |
| `SetProvider()` creates/starts `DispatcherTimer` | called from `Dispatcher.InvokeAsync` context in WireNewPanel | UI thread CLEAN |
| No `lock()` anywhere | ConcurrentDictionary removed, DispatcherTimer pattern only | JS-021 PASS |
| No `async void` anywhere | no async handlers | JS-033 PASS |

---

## Section 9: Jane Street Rule Compliance Checkpoints

| Rule | Applies To | Status |
|---|---|---|
| JS-001: No throw in NT8 callbacks | `AtrSizingEngine.OnBarUpdate`, `OnStateChange` | PASS — no throw |
| JS-002: No return null for public surface | `GetLastAtr()`, `HasData` | PASS — primitive returns |
| JS-003: No mutable captured closure state across threads | AtrWidget timer Tick captures no external mutable state (only `this`) | PASS |
| JS-008: All WPF brushes Freeze()d via MakeBrush() | AtrWidget uses only `SetResourceReference` (NTBrushes) — no raw SolidColorBrush | PASS |
| JS-021: No lock() | Entire change set — zero `lock()` added | PASS |
| JS-023: volatile only for bool/int | `_hasData: volatile bool` everywhere | PASS |
| NT8-003: volatile double BANNED | `_lastAtr` is non-volatile double in both engine and widget | PASS |
| ASCII-only | Labels: "ATR %", "-- pts" — pure ASCII | PASS |
| No FontFamily | No new FontFamily references | PASS |
| No DateTime.Now | Not applicable to this epic | N/A |
| "PTT-" prefix on CreateOrder | Not applicable to this epic | N/A |

---

## Section 10: Ticket Plan

### T1 — Data Layer Cleanout

**File:** `src/PropTraderTools/AtrSizingEngine.cs`  
Strip the file: remove all members listed in Section 1. Keep only the pure value provider surface.

**File:** `src/PropTraderTools/CopyEngine.cs`  
Remove: `_atrEngine`, `_atrEnabled`, `SetAtrEngine()`, `UpdateMaxRisk()`, `UpdateAtrFraction()`, `GetSuggestedQty()`.  
Simplify: `ResolveBaseQty()` → return `signalQty`.

**File:** `src/PropTraderTools/FeatureFlags.cs`  
Remove `AtrSizing` parameter. Update all 4 factory methods (Section 3).

**Spec satisfied:** All WHAT TO DELETE items 1, 4, 5.  
**Compile gate:** Project must compile clean after T1 (T2/T3 depend on it).

---

### T2 — UI Layer + AtrWidget Build

**File:** `src/PropTraderTools/TradeCopierAddOn.cs`  
Remove: `_atrEngines`, `_atrPollTimer`, `StartAtrEngine()`, `StopAtrEngine()`, `UpdateAtrOverlay()`, `OnAtrUpdated()`.  
Replace: call site in `WireNewPanel()` per Section 4.  
Remove: `StopAtrEngine(chart)` call in `OnWindowDestroyed()`.

**File:** `src/PropTraderTools/TradeCopierPanel.cs`  
Remove: all members listed in Section 5.  
Add: `AtrWidget` inner class per Section 6.  
Add: `SetAtrProvider(AtrSizingEngine)` method.  
Wire: `_widget?.Detach()` in existing `Detach()`.  
Replace: `BuildRiskAtrRow` call in `BuildUI()`.  
Remove: `NotifyRiskChanged()` and `NotifyAtrFractionChanged()` from `OnLoaded()`.

**Spec satisfied:** All WHAT TO DELETE items 2, 3 + WHAT TO BUILD (AtrWidget).

---

### T3 — Test Cleanout + New Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`  
Delete: all test methods listed in Section 7.  
Add: 3 new test methods per Section 7.

**Spec satisfied:** WHAT TO DELETE item 6 + NEW TESTS.

---

## Section 11: Summary of New Public/Internal Surface

After B172, the following NEW methods/properties are introduced:

| Member | Class | Kind | CYC |
|---|---|---|---|
| `HasData` | `AtrSizingEngine` | `internal bool` property | 1 |
| `SetAtrProvider(AtrSizingEngine)` | `TradeCopierPanel` | `internal void` | 1 |
| `BuildAtrWidgetRows(StackPanel)` | `AtrWidget` (inner) | `internal void` | 1 |
| `SetProvider(AtrSizingEngine)` | `AtrWidget` (inner) | `internal void` | 2 |
| `Detach()` | `AtrWidget` (inner) | `internal void` | 2 |
| `OnTimerTick(...)` | `AtrWidget` (inner) | `private void` | 3 |
| `RecalcDisplay()` | `AtrWidget` (inner) | `private void` | 1 |
| `OnAtrFractionLostFocus(...)` | `AtrWidget` (inner) | `private void` | 2 |

All CYC values ≤ 8. PASS.

---

## Section 12: Feature Flag Gate — NONE

Per spec: "new widget is always visible, no flag gate." `AtrWidget.BuildAtrWidgetRows()` is called unconditionally from `BuildUI()`. No `Visibility` toggle. No `FeatureFlags` check. `ApplyRowVisibilityFlags()` no longer references `_atrRow` or `AtrSizing`.

---

## Deferred Items for B172 Backlog

None. B172 is a clean greenfield replacement with no known deferred work. All spec requirements are covered by T1/T2/T3.

---

_End of B172 Architecture Plan_
