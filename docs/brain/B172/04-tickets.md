# B172 Tickets — ATR Display Widget Greenfield Replacement

**Epic:** B172  
**Phase:** 3 — Ticket Generation  
**Author:** ptt-architect  
**Source plan:** docs/brain/B172/02-architecture-plan.md (status: REVIEW_PASS)  
**Pipeline:** SINGLE-PIPELINE — T1 → T2 → T3 sequential  

---

## PIPELINE DEPENDENCY ORDER

T1 must compile clean before T2 begins.  
T2 must compile clean before T3 begins.  
Do NOT start T2 until `dotnet build` passes after T1.  
Do NOT start T3 until `dotnet build` passes after T2.

---

## T1 — Data Layer Cleanout

### Spec Requirements Satisfied

- Strip `AtrSizingEngine.cs` to pure ATR value provider (WHAT TO DELETE item 1)
- Remove ATR members from `CopyEngine.cs` (WHAT TO DELETE item 4)
- Remove `AtrSizing` from `FeatureFlags.cs` (WHAT TO DELETE item 5)

### Files Modified

- `src/PropTraderTools/AtrSizingEngine.cs`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/FeatureFlags.cs`

---

### T1 — AtrSizingEngine.cs: STRIP to pure value provider

**DO NOT delete the file. Strip it in place.**

#### DELETE the following members (exact names + lines confirmed by plan review):

| Member | Kind | Source Line(s) | Action |
|---|---|---|---|
| File header comment block | comment lines | 1–18 | DELETE entire block (B9/B10/B12/B21 history comments) |
| `AtrSizingEngine(int testContracts)` | `internal` constructor | 28–32 | DELETE entire constructor including comment on line 26–27 |
| `_lastContracts` | `private volatile int` field | 40 | DELETE line |
| `_maxRiskDollars` | `private double` field | 45 | DELETE line + comment on 44 |
| `_tickDollarValue` | `private double` field | 46 | DELETE line |
| `_atrFraction` | `private double` field | 49–50 | DELETE line + comment on 48–50 |
| `AtrUpdated` | `internal event Action<string>` | 83 | DELETE line + comment on 81–82 |
| `CalcContracts(double, double, double)` | `internal static int` | 156–165 | DELETE entire method + comment on 155 |
| `FireAtrUpdated(double, int)` | `private void` | 109–120 | DELETE entire method + comment on 107–108 |
| `SetAtrFraction(double)` | `internal void` | 125–128 | DELETE entire method + comment on 122–124 |
| `UpdateMaxRisk(double)` | `internal void` | 132–135 | DELETE entire method + comment on 130–131 |
| `SetParameters(double, double)` | `internal void` | 138–142 | DELETE entire method + comment on 137 |
| `GetSuggestedQty()` | `internal int` | 145–150 | DELETE entire method + comment on 144 |

#### MODIFY the following members:

**`OnStateChange()`** (lines 56–79) — in the `Terminated` branch (lines 73–78), DELETE the line:
```csharp
_lastContracts = 1;
```
Keep `_hasData = false;` and `_lastAtr = 0.0;` in the Terminated branch.

**`OnBarUpdate()`** (lines 86–96) — replace entire method body with:
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
(Removes the `CalcContracts`, `_lastContracts`, and `FireAtrUpdated` calls.)

**`ManualOnBarUpdate()`** (lines 102–105) — KEEP entirely. Change visibility from `public` to `internal`:
```csharp
internal void ManualOnBarUpdate()
{
    OnBarUpdate();
}
```

#### ADD the following member (does not exist yet):

```csharp
// B172: HasData property -- read path for AtrWidget timer. CYC=1.
internal bool HasData => _hasData;
```
Add immediately after the `GetLastAtr()` line (line 153).

#### KEEP without modification:

- `public AtrSizingEngine()` constructor (line 35)
- `_lastAtr` field (line 41)
- `_hasData` field (line 42)
- `Period` property with `[NinjaScriptProperty]` (lines 52–53)
- `OnStateChange()` method structure (lines 56–79) — only remove the `_lastContracts = 1;` line from Terminated branch
- `GetLastAtr()` method (line 153)
- `using System;` and `using NinjaTrader.NinjaScript.Indicators;` (lines 19–20)
- Namespace and class declaration

#### Post-strip shape of AtrSizingEngine.cs (expected):

```csharp
using System;
using NinjaTrader.NinjaScript.Indicators;

namespace PropTraderTools
{
    public class AtrSizingEngine : Indicator
    {
        public AtrSizingEngine() { }

        private double _lastAtr = 0.0;
        private volatile bool _hasData = false;

        [NinjaTrader.NinjaScript.NinjaScriptProperty]
        public int Period { get; set; } = 14;

        protected override void OnStateChange()  // CYC=4
        {
            if (State == NinjaTrader.NinjaScript.State.SetDefaults) { ... }
            else if (State == NinjaTrader.NinjaScript.State.Configure) { ... }
            else if (State == NinjaTrader.NinjaScript.State.DataLoaded) { ... }
            else if (State == NinjaTrader.NinjaScript.State.Terminated)
            {
                _hasData = false;
                _lastAtr = 0.0;
            }
        }

        protected override void OnBarUpdate()    // CYC=2
        {
            if (CurrentBar < Period) return;
            _lastAtr = ATR(Period)[0];
            _hasData = true;
        }

        internal void ManualOnBarUpdate() { OnBarUpdate(); }  // CYC=1

        internal double GetLastAtr() => _lastAtr;             // CYC=1

        internal bool HasData => _hasData;                    // CYC=1 (NEW)
    }
}
```

---

### T1 — CopyEngine.cs: REMOVE ATR members

#### DELETE the following (confirmed by plan review):

| Member | Kind | Source Line(s) | Action |
|---|---|---|---|
| B9 T1 comment block | comment | 135 | DELETE comment line |
| `_atrEnabled` | `private volatile bool` field | 136 | DELETE line |
| `_atrEngine` | `private volatile AtrSizingEngine` field | 137 | DELETE line |
| `SetAtrEngine(AtrSizingEngine, bool)` | `internal void` method | 692–698 | DELETE entire method + comment on 691 |
| `UpdateMaxRisk(double)` (CopyEngine version) | `internal void` method | 700–706 | DELETE entire method + comment on 700 |
| `UpdateAtrFraction(double)` | `internal void` method | 708–714 | DELETE entire method + comment on 708 |
| `GetSuggestedQty(NinjaTrader.Cbi.Instrument)` | `internal int` method | 1549–1556 | DELETE entire method + comment on 1548 |

#### MODIFY the following:

**`ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)`** (lines 3088–3093):

Replace entire method body with:
```csharp
// B172: ATR removed. Return signal qty directly. CYC=1.
private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)
{
    return signalQty;
}
```

> **Engineer note:** If `ResolveBaseQty` is called from only one site in `DispatchCopy`, you may inline `signalQty` directly at the call site and delete the method entirely. If called from multiple sites, keep as a 1-line method. Verify call count before deciding.

---

### T1 — FeatureFlags.cs: REMOVE AtrSizing parameter

Source file: `src/PropTraderTools/FeatureFlags.cs` (40 lines, confirmed by plan review).

#### CHANGE the record declaration (lines 9–17):

**Before:**
```csharp
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool AtrSizing,
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)
```

**After:**
```csharp
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)
```

#### UPDATE all factory methods:

**`Starter()` (line 19–20):**
```csharp
// Before: new(false, false, false, false, false, false, false)
// After:
public static FeatureFlags Starter() =>
    new(false, false, false, false, false, false);
```

**`Pro()` (line 22):**
```csharp
// Before: new(true, true, true, false, false, false, false)
// After:
public static FeatureFlags Pro() => new(true, true, true, false, false, false);
```

**`Elite()` (line 24):**
```csharp
// Before: new(true, true, true, true, true, true, true)
// After:
public static FeatureFlags Elite() => new(true, true, true, true, true, true);
```

**`FromFeatureList()` (lines 26–37) — DELETE line 33:**
```csharp
// DELETE this line:
AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing"),
```

After deletion, the `FromFeatureList` named-arg list has 6 arguments (no `AtrSizing`).

---

### T1 — Compile Gate

After all T1 edits, run:
```
dotnet build src/PropTraderTools/
```
**Project must compile with zero errors before T2 begins.**

---

### T1 — 7-SCAN CHECKLIST

```
SCAN-01: grep -r "lock(" src/ → 0 matches required
         Rationale: T1 makes no threading changes; pre-existing zero-match must be maintained.

SCAN-02: grep -rn "AtrSizingEngine\|SetAtrEngine\|UpdateMaxRisk\|UpdateAtrFraction\|_atrEngine\b\|_atrEnabled\|CalcContracts\|SetAtrFraction\|GetSuggestedQty\|FireAtrUpdated\|AtrUpdated\|NotifyAtrFractionChanged\|NotifyRiskChanged\|BuildRiskAtrRow\|BuildAtrDisplayRow\|SetAtrText\|OnRiskUp\|OnRiskDown\|OnRiskTextLostFocus\|StartAtrEngine\|StopAtrEngine\|UpdateAtrOverlay\|OnAtrUpdated\|_atrEngines\|_atrPollTimer\|_maxRiskDollars\|_riskDollarsBox\|_atrDisplayLabel\|_atrRow\b" src/
         → After T1: AtrSizingEngine class name may still appear (class declaration itself is kept).
           _atrEngine, _atrEnabled, CalcContracts, SetAtrFraction, GetSuggestedQty (CopyEngine version),
           FireAtrUpdated, AtrUpdated, UpdateMaxRisk (CopyEngine), UpdateAtrFraction must show 0 matches.
           Remaining legitimate uses: AtrSizingEngine type name in TradeCopierAddOn.cs + TradeCopierPanel.cs (T2 scope).

SCAN-03: grep -rn "volatile double" src/ → 0 matches required
         Rationale: _lastContracts was volatile int (not double); deleting it cannot introduce a volatile double.
         Verify no new volatile double was introduced.

SCAN-04: Check new string literals in modified files for non-ASCII chars → 0 violations
         Rationale: T1 adds no new string literals. Verify no accidental unicode introduced during edits.

SCAN-05: Cyclomatic complexity check — all new/modified methods ≤ 10
         Methods to check after T1:
           AtrSizingEngine.OnBarUpdate()  → expected CYC=2 (guard + set)
           AtrSizingEngine.OnStateChange() → expected CYC=4 (4 state branches; unchanged)
           AtrSizingEngine.ManualOnBarUpdate() → expected CYC=1
           AtrSizingEngine.HasData → expected CYC=1
           AtrSizingEngine.GetLastAtr() → expected CYC=1
           CopyEngine.ResolveBaseQty() → expected CYC=1

SCAN-06: grep -rn "new Solid\|new Linear\|new Radial" src/ → all brushes Freeze()d via MakeBrush()
         Rationale: T1 modifies no UI code. Verify no new raw brush constructors.

SCAN-07: grep -rn "AtrSizing" src/PropTraderTools/FeatureFlags.cs → 0 matches required
         Rationale: AtrSizing parameter fully removed from FeatureFlags.cs in T1.
```

---

## T2 — UI Layer + AtrWidget Build

**Depends on: T1 compile-clean.**

### Spec Requirements Satisfied

- Remove ATR wiring from `TradeCopierAddOn.cs` (WHAT TO DELETE item 2)
- Remove old ATR UI from `TradeCopierPanel.cs` (WHAT TO DELETE item 3)
- Build `AtrWidget` inner class in `TradeCopierPanel.cs` (WHAT TO BUILD)
- No feature flag gate on widget (spec: "always visible")

### Files Modified

- `src/PropTraderTools/TradeCopierAddOn.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`

---

### T2 — TradeCopierAddOn.cs: REMOVE ATR wiring

#### DELETE the following fields (confirmed by plan review):

| Field | Source Line(s) | Action |
|---|---|---|
| `_atrEngines` ConcurrentDictionary | 43–45 | DELETE declaration + comment on line 43 |
| `_atrPollTimer` DispatcherTimer | 64–66 | DELETE declaration + comment on lines 64–65 |

#### DELETE the following methods (confirmed by plan review):

| Method | Source Lines | Signature |
|---|---|---|
| `StartAtrEngine` | 215–256 | `private void StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)` |
| `StopAtrEngine` | 258–273 | `private void StopAtrEngine(Chart chart)` |
| `UpdateAtrOverlay` | 275–286 | `internal void UpdateAtrOverlay(string atrDisplay)` |
| `OnAtrUpdated` | 288–294 | `private void OnAtrUpdated(string display)` |

Delete all associated comment blocks immediately preceding each method.

#### MODIFY call sites:

**`OnWindowDestroyed()` (line 103–114) — DELETE line 108:**
```csharp
// DELETE this line:
StopAtrEngine(chart); // instance: unsubscribes AtrUpdated
```
The panel cleanup chain (`panel.Detach()` at line 113) handles timer stop via `_widget?.Detach()` added in T2.

**`InjectIntoChart()` (line ~100) — DELETE the inline comment:**
```csharp
// DELETE the comment portion:
InjectIntoChart(chart); // instance call: StartAtrEngine needs _atrOverlayLabel
// Replace comment with:
InjectIntoChart(chart);
```

**`WireNewPanel()` (line ~494–495) — REPLACE call site:**

Find the line:
```csharp
StartAtrEngine(chart, instr);
```

Replace with:
```csharp
var engine = new AtrSizingEngine();
panel.SetAtrProvider(engine);
```

> **Threading note:** `WireNewPanel` is called from within a `Dispatcher.InvokeAsync` lambda. `panel.SetAtrProvider(engine)` is therefore always on the UI thread. This is correct — `AtrWidget.SetProvider()` creates the `DispatcherTimer` on the UI thread.

---

### T2 — TradeCopierPanel.cs: REMOVE old ATR UI members

#### DELETE the following fields (confirmed by plan review):

| Field | Source Line(s) | Action |
|---|---|---|
| `_atrRow` | 275 | DELETE line + comment on 273–274 (keep `_clickTraderRow` comment; delete only `_atrRow` line) |
| `_maxRiskDollars` | 296 | DELETE line + comment on 295 |
| `_atrFraction` (Panel-level) | 297 | DELETE line |
| `_riskDollarsBox` | 298 | DELETE line |
| `_atrDisplayLabel` | 302 | DELETE lines 301–302 (field + comment) |

> **Note:** `_atrFractionBox` at line 299 is ALSO deleted (it is the Panel-level field; the inner `AtrWidget` class has its own private `_atrFractionBox`).

#### DELETE the following methods (confirmed by plan review):

| Method | Source Lines | Signature |
|---|---|---|
| `BuildRiskAtrRow(StackPanel root)` | 3077–3103 | `private void` — DELETE entire method + comment lines 3077–3079 |
| `BuildSpinnerColumn(string, TextBox, RoutedEventHandler, RoutedEventHandler)` | 3105–3146 | `private StackPanel` — DELETE entire method + comment on 3105–3106 |
| `BuildAtrDisplayRow()` | 3148–3162 | `private Border` — DELETE entire method + comment on 3148–3149 |
| `SetAtrText(string display)` | 3164–3172 | `public void` — DELETE entire method + comment on 3164–3166 |
| `OnRiskUp(object, RoutedEventArgs)` | 3174–3181 | `private void` — DELETE entire method + comment on 3174 |
| `OnRiskDown(object, RoutedEventArgs)` | 3183–3190 | `private void` — DELETE entire method + comment on 3183 |
| `OnRiskTextLostFocus(object, RoutedEventArgs)` | 3201–3210 | `private void` — DELETE entire method + comment on 3201 |
| `OnAtrFractionUp(object, RoutedEventArgs)` | 3212–3219 | `private void` — DELETE entire method + comment on 3212 |
| `OnAtrFractionDown(object, RoutedEventArgs)` | 3221–3228 | `private void` — DELETE entire method + comment on 3221 |
| `OnAtrFractionTextLostFocus(object, RoutedEventArgs)` | 3230–3239 | `private void` — DELETE entire method + comment on 3230 |
| `NotifyRiskChanged()` | 3241–3247 | `private void` — DELETE entire method + comment on 3241 |
| `NotifyAtrFractionChanged()` | 3249–3255 | `private void` — DELETE entire method + comment on 3249 |

**KEEP without modification:**
- `TryParseAndClamp(string text, double min, double max, out double result)` (lines 3192–3199) — retained for use by `AtrWidget.OnAtrFractionLostFocus`.

#### MODIFY `OnLoaded()` (lines 797–812) — DELETE lines 803–804:
```csharp
// DELETE these two lines:
NotifyRiskChanged();
NotifyAtrFractionChanged();
```

#### MODIFY `BuildUI()` (line ~925) — REPLACE call site:
```csharp
// OLD (delete this line):
BuildRiskAtrRow(_contentPanel); // B12 T3: Risk $ + ATR % spinner row

// NEW (replace with these two lines):
_widget = new AtrWidget();
_widget.BuildAtrWidgetRows(_contentPanel); // B172: ATR fraction display widget
```

#### MODIFY `ApplyRowVisibilityFlags(FeatureFlags f)` (lines 3279–3290) — DELETE lines 3286–3290:
```csharp
// DELETE these lines:
if (_atrRow != null)
    _atrRow.Visibility = f.AtrSizing
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;
```
After deletion, `ApplyRowVisibilityFlags` contains only the `_clickTraderRow` visibility block. CYC reduces from 4 to 2.

#### MODIFY `Detach()` — ADD at the START of the method body (before existing cleanup):
```csharp
_widget?.Detach(); // B172: stop AtrWidget poll timer on chart close
```

#### ADD the following field in the field block (near line 295, after removing old ATR fields):
```csharp
// B172: AtrWidget inner class instance. Built in BuildUI(). Detached in Detach().
private AtrWidget _widget = null;
```

#### ADD the following method on `TradeCopierPanel` (after `Detach()` or in the appropriate existing section):
```csharp
// B172: SetAtrProvider -- called from TradeCopierAddOn.WireNewPanel after creating stripped engine.
// CYC=1: delegation. JS-021: no lock. UI thread only (called from Dispatcher.InvokeAsync).
internal void SetAtrProvider(AtrSizingEngine engine)
{
    _widget?.SetProvider(engine);
}
```

---

### T2 — TradeCopierPanel.cs: ADD AtrWidget inner class

**Placement:** Add as a `private sealed class` at the end of the `TradeCopierPanel` class body, immediately before the closing `}` of the `TradeCopierPanel` class (and before the `}` of the namespace).

#### Full AtrWidget implementation to ADD:

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

        // Row 2: result label -- "-- pts" until first bar
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
    // DispatcherTimer fires on UI thread. CYC=3: provider null guard + hasData latch + recalc guard.
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

> **Access note:** `TryParseAndClamp` is a `private static` method on the enclosing `TradeCopierPanel` class (lines 3192–3199, retained). Inner classes in C# have full access to all private members of the enclosing class. No change to `TryParseAndClamp` is needed.

---

### T2 — Compile Gate

After all T2 edits, run:
```
dotnet build src/PropTraderTools/
```
**Project must compile with zero errors before T3 begins.**

---

### T2 — 7-SCAN CHECKLIST

```
SCAN-01: grep -r "lock(" src/ → 0 matches required
         Rationale: AtrWidget uses DispatcherTimer (UI-thread-only) and volatile bool. No lock() anywhere.

SCAN-02: grep -rn "AtrSizingEngine\|SetAtrEngine\|UpdateMaxRisk\|UpdateAtrFraction\|_atrEngine\b\|_atrEnabled\|CalcContracts\|SetAtrFraction\|GetSuggestedQty\|FireAtrUpdated\|AtrUpdated\|NotifyAtrFractionChanged\|NotifyRiskChanged\|BuildRiskAtrRow\|BuildAtrDisplayRow\|SetAtrText\|OnRiskUp\|OnRiskDown\|OnRiskTextLostFocus\|StartAtrEngine\|StopAtrEngine\|UpdateAtrOverlay\|OnAtrUpdated\|_atrEngines\|_atrPollTimer\|_maxRiskDollars\|_riskDollarsBox\|_atrDisplayLabel\|_atrRow\b" src/
         → After T2: AtrSizingEngine type name may appear in TradeCopierAddOn.cs (engine instantiation)
           and TradeCopierPanel.cs (SetAtrProvider signature + AtrWidget._provider field).
           ALL others must be 0 matches.
           Specifically verify: StartAtrEngine, StopAtrEngine, UpdateAtrOverlay, OnAtrUpdated,
           _atrEngines, _atrPollTimer (AddOn-level), _maxRiskDollars, _riskDollarsBox,
           _atrDisplayLabel, _atrRow, BuildRiskAtrRow, SetAtrText, NotifyRiskChanged,
           NotifyAtrFractionChanged = 0 matches.

SCAN-03: grep -rn "volatile double" src/ → 0 matches required
         Rationale: AtrWidget._lastAtr is non-volatile double (NT8-003 pattern). Verify not introduced.

SCAN-04: Check new string literals for non-ASCII chars → 0 violations
         Strings to verify in AtrWidget:
           "ATR %"   → pure ASCII ✓
           "-- pts"  → pure ASCII ✓
           " pts"    → pure ASCII ✓
           "F2"      → pure ASCII ✓
         No Unicode arrows (unlike deleted BuildSpinnerColumn which used \u25B2 / \u25BC).

SCAN-05: Cyclomatic complexity check — all new/modified methods ≤ 10
         Methods to check after T2:
           AtrWidget.BuildAtrWidgetRows()   → CYC=1 (straight-line)
           AtrWidget.SetProvider()          → CYC=2 (null guard + create-once)
           AtrWidget.Detach()               → CYC=2 (null guards)
           AtrWidget.OnTimerTick()          → CYC=3 (provider null + hasData latch + recalc guard)
           AtrWidget.RecalcDisplay()        → CYC=1 (straight-line)
           AtrWidget.OnAtrFractionLostFocus() → CYC=2 (parse guard + hasData guard)
           TradeCopierPanel.SetAtrProvider() → CYC=1 (delegation)
           TradeCopierPanel.ApplyRowVisibilityFlags() → CYC=2 (reduced from 4; clickTrader block only)

SCAN-06: grep -rn "new Solid\|new Linear\|new Radial" src/ → all brushes Freeze()d via MakeBrush()
         Rationale: AtrWidget uses only SetResourceReference("NTBrushes.SubtleBrush") -- no raw brush.
         Verify no new SolidColorBrush constructor in AtrWidget code.

SCAN-07: grep -rn "AtrSizing" src/PropTraderTools/FeatureFlags.cs → 0 matches required
         (T1 already removed AtrSizing from FeatureFlags; this SCAN-07 confirms it is still 0 after T2.)
         Also verify: grep -rn "\.AtrSizing" src/ → 0 matches (no remaining feature flag access).
```

---

## T3 — Test Cleanout + New Tests

**Depends on: T2 compile-clean.**

### Spec Requirements Satisfied

- Delete obsolete ATR tests that reference deleted members (WHAT TO DELETE item 6)
- Add 3 new tests covering AtrWidget calculation logic (NEW TESTS)

### File Modified

- `src/PropTraderTools/CopyEngineTests.cs`

---

### T3 — CopyEngineTests.cs: DELETE obsolete test methods

**Primary key is the method name. Line numbers are approximate — find by name before deleting.**

| Method Name | Approx Source Line | Test Series | Reason for Deletion |
|---|---|---|---|
| `CalcContracts_MES_ATR6_returns5` | 1005 | T-B9-01 | `AtrSizingEngine.CalcContracts` deleted in T1 |
| `CalcContracts_MES_ATR8_returns3` | 1015 | T-B9-02 | same |
| `CalcContracts_MES_ATR12_returns2` | 1025 | T-B9-03 | same |
| `CalcContracts_ZeroAtr_returns1` | 1035 | T-B9-04 | same |
| `CalcContracts_NegativeAtr_returns1` | 1045 | T-B9-05 | same |
| `CalcContracts_ResultBelowOne_clampsTo1` | 1055 | T-B9-06 | same |
| `CalcContracts_ZeroTickValue_returns1` | 1065 | T-B9-07 | same |
| `CalcContracts_LargeMaxRisk_noOverflow` | 1075 | T-B9-08 | same |
| `GetSuggestedQty_returns1_when_no_engine` | ~1089 | T-B9-09 | `SetAtrEngine` + `GetSuggestedQty` deleted |
| `GetSuggestedQty_returns_engine_qty_when_set` | ~1099 | T-B9-10 | same |
| `ClickTrader_atr_disabled_fallback_qty_is_1` | ~1122 | T-B9-12 | same |
| `ClickTrader_atr_enabled_uses_engine_qty` | ~1132 | T-B9-13 | same |
| `StartAtrEngine_NullChart_DoesNotThrow` | 1760 | T-B11-T2-01 | `AtrSizingEngine` test-seam ctor deleted |
| `StartAtrEngine_NullInstrument_DoesNotThrow` | 1773 | T-B11-T2-02 | `SetParameters` deleted |
| `UpdateAtrOverlay_FormatsDisplayString_CorrectText` | 1786 | T-B11-T2-03 | `FireAtrUpdated` + `CalcContracts` deleted |
| `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1` | 1816 | T-B12-T3-01 | `CalcContracts` deleted |
| `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` | 1829 | T-B12-T3-02 | `SetAtrEngine` + `UpdateMaxRisk` deleted |
| `BuildRiskAtrRow_ClampMin_RejectsSubMinValue` | 1849 | T-B12-T3-03 | `BuildRiskAtrRow` deleted |
| `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` | 1857 | T-B12-T3 | `UpdateAtrFraction` + `SetAtrEngine` deleted |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` | 2422 | B21-LANE-A | `CalcContracts` + `_atrFraction` + `_maxRiskDollars` deleted |

**Total: 20 test methods deleted.**

> Delete the entire method body including all `[Fact]`/`[Fact(Skip=...)]` attribute lines, the `public void` declaration, all `{ ... }` body lines, and any immediately preceding comment block belonging to that test. Do NOT delete comment blocks that belong to adjacent test series headers (e.g., the `// B9 T1: ATR Sizing Engine tests` section header at line 1000–1001 should be deleted only if all tests in that block are deleted).

**After deletion, verify the surrounding tests compile correctly** — specifically check that `GetSuggestedQty_returns_engine_qty_when_set` teardown (line 1104: `CopyEngine.Instance.SetAtrEngine(null, enabled: false)`) doesn't leave a dangling close-brace.

---

### T3 — CopyEngineTests.cs: ADD 3 new test methods

**Add the following block after the last deleted test's surrounding section and before the next surviving test region** (e.g., after the B12 T3 section, before the B14 T1 section at ~line 1877):

```csharp
// =====================================================================
// B172: ATR Widget replacement tests (T-B172-T3-01 through T-B172-T3-03)
// Replaces T-B12-T3, T-B11-T2-03, T-B10-T4 series.
// Pure arithmetic -- no NT8 runtime required. No [Skip].
// =====================================================================

[Fact]
public void Ceiling_AlwaysRoundsUp()
{
    // Math.Ceiling behavior: any fractional part rounds UP to next integer.
    Assert.Equal(2.0, Math.Ceiling(1.001));  // 1.001 -> 2 (fractional -> next int)
    Assert.Equal(1.0, Math.Ceiling(1.000));  // 1.000 -> 1 (exact integer, no rounding)
    Assert.Equal(1.0, Math.Ceiling(0.001));  // 0.001 -> 1 (near-zero fraction -> 1)
    Assert.Equal(3.0, Math.Ceiling(2.756));  // 2.756 -> 3 (representative case)
}

[Fact]
public void AtrFractionBox_ParseAndClamp_ClampsToRange()
{
    // Validates the clamp [0.10, 5.00] arithmetic used in AtrWidget.OnAtrFractionLostFocus.
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

**New test identifiers:** T-B172-T3-01, T-B172-T3-02, T-B172-T3-03.

> These tests require no `[Fact(Skip=...)]` — they contain zero NT8 runtime dependencies and will run in any standard xUnit host.

---

### T3 — Test Run Gate

After all T3 edits, run:
```
dotnet test src/PropTraderTools/
```
Expected: all existing passing tests continue to pass. The 3 new tests pass. Zero compilation errors.

---

### T3 — 7-SCAN CHECKLIST

```
SCAN-01: grep -r "lock(" src/ → 0 matches required
         Rationale: CopyEngineTests.cs has no lock() usage; deleting tests cannot introduce one.

SCAN-02: grep -rn "AtrSizingEngine\|SetAtrEngine\|UpdateMaxRisk\|UpdateAtrFraction\|_atrEngine\b\|_atrEnabled\|CalcContracts\|SetAtrFraction\|GetSuggestedQty\|FireAtrUpdated\|AtrUpdated\|NotifyAtrFractionChanged\|NotifyRiskChanged\|BuildRiskAtrRow\|BuildAtrDisplayRow\|SetAtrText\|OnRiskUp\|OnRiskDown\|OnRiskTextLostFocus\|StartAtrEngine\|StopAtrEngine\|UpdateAtrOverlay\|OnAtrUpdated\|_atrEngines\|_atrPollTimer\|_maxRiskDollars\|_riskDollarsBox\|_atrDisplayLabel\|_atrRow\b" src/
         → After T3 (all 3 tickets complete): run full scan across all of src/.
           AtrSizingEngine class name may appear only in:
             - AtrSizingEngine.cs (class declaration itself)
             - TradeCopierAddOn.cs (new engine instantiation: new AtrSizingEngine())
             - TradeCopierPanel.cs (SetAtrProvider parameter + AtrWidget._provider field type)
           All other pattern matches must be 0.
           Specifically: CalcContracts, SetAtrFraction, GetSuggestedQty (CopyEngine), SetAtrEngine,
           UpdateAtrFraction, FireAtrUpdated, AtrUpdated, UpdateMaxRisk (CopyEngine),
           NotifyRiskChanged, NotifyAtrFractionChanged, BuildRiskAtrRow, BuildAtrDisplayRow,
           SetAtrText, StartAtrEngine, StopAtrEngine, UpdateAtrOverlay, OnAtrUpdated,
           _atrEngines, _atrPollTimer (AddOn-level), _maxRiskDollars, _riskDollarsBox,
           _atrDisplayLabel, _atrRow = 0 matches.

SCAN-03: grep -rn "volatile double" src/ → 0 matches required
         Final check across entire src/ after all 3 tickets.

SCAN-04: Check new string literals for non-ASCII chars → 0 violations
         New strings in T3: "ATR %" (in comment only), " pts", "-- pts", "abc" (in TryParse test).
         All pure ASCII.

SCAN-05: Cyclomatic complexity check — all new/modified methods ≤ 10
         T3 adds only test methods (no production code). Verify the 3 new test methods have CYC=1
         (straight-line assertion sequences, no branches).

SCAN-06: grep -rn "new Solid\|new Linear\|new Radial" src/ → all brushes Freeze()d via MakeBrush()
         Final check after all 3 tickets. T3 adds no UI code.

SCAN-07: grep -rn "AtrSizing" src/PropTraderTools/FeatureFlags.cs → 0 matches required
         Final check. AtrSizing removed in T1; still 0 after T3.
```

---

## Summary of New Signatures Introduced in B172

| Member | Class | File | Kind | CYC | Rule compliance |
|---|---|---|---|---|---|
| `HasData` | `AtrSizingEngine` | AtrSizingEngine.cs | `internal bool` property | 1 | JS-023: volatile bool backing |
| `SetAtrProvider(AtrSizingEngine)` | `TradeCopierPanel` | TradeCopierPanel.cs | `internal void` | 1 | JS-021: no lock |
| `BuildAtrWidgetRows(StackPanel)` | `AtrWidget` (inner) | TradeCopierPanel.cs | `internal void` | 1 | JS-008: SetResourceReference only |
| `SetProvider(AtrSizingEngine)` | `AtrWidget` (inner) | TradeCopierPanel.cs | `internal void` | 2 | JS-021: DispatcherTimer |
| `Detach()` | `AtrWidget` (inner) | TradeCopierPanel.cs | `internal void` | 2 | JS-021: no lock |
| `OnTimerTick(object, EventArgs)` | `AtrWidget` (inner) | TradeCopierPanel.cs | `private void` | 3 | JS-023: volatile bool read |
| `RecalcDisplay()` | `AtrWidget` (inner) | TradeCopierPanel.cs | `private void` | 1 | NT8-003: non-volatile double |
| `OnAtrFractionLostFocus(object, RoutedEventArgs)` | `AtrWidget` (inner) | TradeCopierPanel.cs | `private void` | 2 | JS-021: UI thread |

All CYC values ≤ 8. **PASS (JS-013).**

---

## Feature Flag Gate: NONE

`AtrWidget.BuildAtrWidgetRows()` is called unconditionally from `TradeCopierPanel.BuildUI()`. No `Visibility` toggle. No `FeatureFlags` check. `ApplyRowVisibilityFlags()` no longer references `_atrRow` or `AtrSizing` after T1+T2 complete.

---

_End of B172 04-tickets.md_

TICKETS_COMPLETE
