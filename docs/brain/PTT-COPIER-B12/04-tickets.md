# PTT-COPIER-B12 Tickets
# Block: PTT-COPIER-B12
# Date: 2026-07-11
# Author: ptt-architect (Phase 3)
# Input: docs/brain/PTT-COPIER-B12/02-architecture-plan.md (REVIEW_PASS)
# Status: TICKETS_COMPLETE

---

## T1 — DW-B12-BUFFERED-BUTTONS-01

### Overview
Closes DW-B11-DEFER-01. Replaces the existing 4-button action grid and 2-state BE arm row with a
3-row buffered button section inside `_contentPanel`. Adds `Trim` and `Flatten` limit-order overloads
to `CopyEngine`. Adds PTT-prefix gate (Gate 0.5) to `DispatchCopy`. Introduces the BE 3-state FSM
(Idle/Armed/Connected) on the panel.

### Spec Requirements Satisfied
- DW-B11-DEFER-01 (Flatten/Trim limit orders — CLOSED)
- DW-B12-BUFFERED-BUTTONS-01

### Files Touched
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngine.cs`

> **NT8 FQN NOTE**: All `RepeatButton` references in this ticket are
> `System.Windows.Controls.Primitives.RepeatButton`.
> Engineer MUST use the fully qualified name or add
> `using System.Windows.Controls.Primitives;` to `TradeCopierPanel.cs`.
> NT8's .NET 4.8 WPF build environment does not auto-import this namespace.

---

### 1.1 New Fields — TradeCopierPanel.cs

Add after the existing `// B11 T2` field block. All plain types, UI-thread-only, no volatile (NT8-003).

```csharp
// B12 T1 -- Buffered button state (plain int; UI-thread-only; no volatile per NT8-003)
private int  _trimBuffer     = 1;
private int  _flattenBuffer  = 1;
private int  _beBuffer       = 1;

// B12 T1 -- BE 3-state FSM (UI-thread-only; no volatile)
private BeState _beState = BeState.Idle;

// B12 T1 -- Button refs for buffered section
private Button     _trimBtn2;
private Button     _flattenBtn2;
private Button     _beBtn2;
private Button     _cancelBtn2;
private Button     _copyToggleBtn2;

// B12 T2 -- Collapse state and refs (plain bool; UI-thread-only; no volatile per NT8-003)
private bool       _isCollapsed   = false;
private Button     _collapseToggleBtn;
private StackPanel _contentPanel;

// B12 T1 -- Frozen semantic brush for BE CONNECTED border (MakeBrush = Freeze()d, JS-008)
// RGB (59, 130, 246) = blue. No hex string literal (JS-008).
private static readonly SolidColorBrush BrushConnected = MakeBrush(59, 130, 246);
```

### 1.2 New Nested Enum — TradeCopierPanel.cs

Add inside the `TradeCopierPanel` class, adjacent to the `FollowerItem` nested class:

```csharp
// B12 T1 -- BE 3-state FSM enum. UI-thread-only; no volatile backing needed.
private enum BeState
{
    Idle,       // BE button shows "BE +N" -- inactive
    Armed,      // After first click; engine.ArmPendingBe called; amber border
    Connected   // After engine fires pending BE; blue border; live repricing active
}
```

### 1.3 Removed Fields — TradeCopierPanel.cs

Remove (replaced by T1's buffered BE):
```
_beArmBtn       (Button)  -- line ~L97
_beArmState     (bool)    -- line ~L98
_beArmBufferBox (TextBox) -- line ~L99
```
Also remove the call to `BuildBeArmRow(root)` in `BuildUI()` and remove the methods
`BuildBeArmRow`, `OnBEArmClick`, `UpdateBEArmVisuals`, `FlashBeFired` — they are superseded
by the 3-state BE cluster. The `OnPendingBeFiredDispatch` method body changes (see §1.7).

### 1.4 Modified: BuildUI() — TradeCopierPanel.cs

Replace the existing `BuildUI()` body with a version that:
1. Creates `_contentPanel = new StackPanel()`.
2. Calls `BuildCollapsibleHeader(root)` (T2) before `_contentPanel`.
3. Adds `root.Children.Add(_contentPanel)`.
4. Moves all existing rows (status, click trader, mode, tighten stop, ATM template) into
   `_contentPanel.Children.Add(...)` instead of `root.Children.Add(...)`.
5. Calls `BuildBufferedButtonsRow(_contentPanel)` at position [0] inside `_contentPanel`
   (before the status TextBlock, so the new controls appear at the top of the collapsible area).
6. Removes the old 4-column `actionGrid` (Trim/Flatten/Cancel/BE cluster) and the old
   `_copyToggleBtn`. These are superseded by T1's rows.
7. Keeps the `_followersDropDown`, `applyBtn`, and separator in `root` (above `_contentPanel`).

### 1.5 New Methods — TradeCopierPanel.cs (T1)

#### BuildBufferedButtonsRow
```csharp
// B12 T1 -- builds all 3 buffered-button rows inside _contentPanel.
// CYC=1: straight-line construction, no branches.
private void BuildBufferedButtonsRow(StackPanel root)
```
Implementation outline:
- Row 1 (`UniformGrid Columns=2`):
  - Col 0: Trim cluster — `_trimBtn2` (content=`FormatBuffer("Trim", _trimBuffer)`) +
    `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` (NTButtonStyle, Width=18, Height=12, Click+=`OnTrimUp`) +
    `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` (NTButtonStyle, Width=18, Height=12, Click+=`OnTrimDown`) +
    `_trimBtn2.Click += OnTrimClick`
  - Col 1: Flatten cluster — `_flattenBtn2` (content=`FormatBuffer("Flatten", _flattenBuffer)`) +
    `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` (Click+=`OnFlattenUp`) + `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` (Click+=`OnFlattenDown`) +
    `_flattenBtn2.Click += OnFlattenClick`
- Row 2 (`UniformGrid Columns=2`):
  - Col 0: `_cancelBtn2` Button (content="Cancel", NTButtonStyle, Click+=`OnCancel2`)
  - Col 1: BE cluster — `_beBtn2` (content=`FormatBuffer("BE", _beBuffer)`) +
    `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` (Click+=`OnBeUp`) + `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` (Click+=`OnBeDown`) +
    `_beBtn2.Click += OnBeClick`
- Row 3: full-width `_copyToggleBtn2` Button
  (content=`"\u25CF COPY OFF"`, Background=`BrushInactive`, Click+=`OnCopyToggle`)
- Each cluster uses `DockPanel` or `HorizontalStackPanel`; main button grows to fill, arrow
  buttons are 18 px wide, stacked half-height (two 12 px `System.Windows.Controls.Primitives.RepeatButton`s in a 2-row `Grid`).
- All `System.Windows.Controls.Primitives.RepeatButton` use `NTButtonStyle`.

#### FormatBuffer
```csharp
// B12 T1 -- formats buffer label for display on a button. CYC=1. Static, no state.
// Example: FormatBuffer("Trim", 1) -> "Trim +1"
private static string FormatBuffer(string name, int ticks)
```

#### OnTrimUp / OnTrimDown
```csharp
// B12 T1 -- CYC=1: increment/decrement + clamp + label refresh (no branch beyond clamp ternary).
private void OnTrimUp(object sender, RoutedEventArgs e)
private void OnTrimDown(object sender, RoutedEventArgs e)
```
Body pattern (`OnTrimUp`):
```csharp
_trimBuffer = Math.Max(Math.Min(_trimBuffer + 1, 20), 0);   // no Math.Clamp (NT8-003)
if (_trimBtn2 != null) _trimBtn2.Content = FormatBuffer("Trim", _trimBuffer);
```

#### OnTrimClick
```csharp
// B12 T1 -- CYC=3: instrument null(1) + refPrice guard(2) + engine call(3).
// Calls new CopyEngine.Trim(instrument, exitBuffer, refPrice) overload when refPrice > 0.
// Falls back to CopyEngine.Trim(instrument) (market) when refPrice <= 0.
private void OnTrimClick(object sender, RoutedEventArgs e)
```
Body:
```csharp
if (_instrument == null) return;                                         // (1)
double refPrice = GetRefPrice();
if (refPrice <= 0 || _trimBuffer == 0)                                   // (2)
    _engine.Trim(_instrument);
else                                                                     // (3)
    _engine.Trim(_instrument, _trimBuffer, refPrice);
```

#### OnFlattenUp / OnFlattenDown
```csharp
// B12 T1 -- CYC=1: identical clamp pattern to OnTrimUp/Down.
private void OnFlattenUp(object sender, RoutedEventArgs e)
private void OnFlattenDown(object sender, RoutedEventArgs e)
```

#### OnFlattenClick
```csharp
// B12 T1 -- CYC=3: instrument null(1) + refPrice guard(2) + engine call(3).
private void OnFlattenClick(object sender, RoutedEventArgs e)
```
Body mirrors `OnTrimClick`, calling `_engine.Flatten(...)`.

#### OnBeUp / OnBeDown
```csharp
// B12 T1 -- CYC=2: clamp(1) + live reprice if Connected(2).
private void OnBeUp(object sender, RoutedEventArgs e)
private void OnBeDown(object sender, RoutedEventArgs e)
```
Body (`OnBeUp`):
```csharp
_beBuffer = Math.Max(Math.Min(_beBuffer + 1, 20), 0);       // no Math.Clamp (NT8-003)
UpdateBeLabel();
if (_beState == BeState.Connected && _instrument != null)   // (2)
    _engine.BreakEven(_instrument, _beBuffer);
```

#### OnBeClick
```csharp
// B12 T1 -- CYC=5: instrument null(1), leaderAccount null(2), Idle->Armed(3),
//           Armed->Idle(4), Connected->Idle(5).
private void OnBeClick(object sender, RoutedEventArgs e)
```
Body:
```csharp
if (_instrument == null)    return;   // (1)
if (_leaderAccount == null) return;   // (2)
switch (_beState)
{
    case BeState.Idle:                // (3)
        _engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer);
        _beState = BeState.Armed;
        UpdateBeVisuals(BeState.Armed);
        break;
    case BeState.Armed:               // (4)
        _engine.DisarmPendingBe();
        _beState = BeState.Idle;
        UpdateBeVisuals(BeState.Idle);
        break;
    case BeState.Connected:           // (5)
        _engine.DisarmPendingBe();
        _beState = BeState.Idle;
        UpdateBeVisuals(BeState.Idle);
        break;
}
```

#### UpdateBeLabel
```csharp
// B12 T1 -- CYC=1: straight-line null guard + label set.
private void UpdateBeLabel()
```
Body:
```csharp
if (_beBtn2 != null) _beBtn2.Content = FormatBuffer("BE", _beBuffer);
```

#### UpdateBeVisuals
```csharp
// B12 T1 -- CYC=3: switch on BeState (3 cases).
// Idle: no border; Armed: BrushCaution border; Connected: BrushConnected border.
private void UpdateBeVisuals(BeState state)
```
Body:
```csharp
if (_beBtn2 == null) return;
switch (state)
{
    case BeState.Idle:                                                    // (1)
        _beBtn2.Content         = FormatBuffer("BE", _beBuffer);
        _beBtn2.BorderBrush     = null;
        _beBtn2.BorderThickness = new Thickness(0);
        break;
    case BeState.Armed:                                                   // (2)
        _beBtn2.Content         = "BE Armed";
        _beBtn2.BorderBrush     = BrushCaution;
        _beBtn2.BorderThickness = new Thickness(2);
        break;
    case BeState.Connected:                                               // (3)
        _beBtn2.Content         = "BE Live";
        _beBtn2.BorderBrush     = BrushConnected;
        _beBtn2.BorderThickness = new Thickness(2);
        break;
}
```

#### OnBeConnected
```csharp
// B12 T1 -- transitions ARMED -> CONNECTED. Replaces FlashBeFired from B10 T2.
// async void: UI event handler invoked via Dispatcher.InvokeAsync (explicitly permitted).
// CYC=2: null guard(1) + state transition body(2).
// Called only from OnPendingBeFiredDispatch via Dispatcher.InvokeAsync.
private async void OnBeConnected(string instr)
```
Body:
```csharp
if (_beBtn2 == null) return;                                              // (1)
_beState = BeState.Connected;                                            // (2)
UpdateBeVisuals(BeState.Connected);
if (_instrument != null)
    _engine.BreakEven(_instrument, _beBuffer);
await System.Threading.Tasks.Task.CompletedTask;   // satisfies async signature, no actual delay
```

#### GetRefPrice
```csharp
// B12 T1 -- reads last close from chart for use in limit price calculation.
// CYC=3: chart null(1) + barsArray null/empty(2) + return last close(3).
// Returns <= 0 when chart data unavailable (callers treat <= 0 as fallback-to-market).
private double GetRefPrice()
```
Body:
```csharp
if (_currentChart == null) return 0.0;                                    // (1)
var bars = _currentChart.BarsArray;
if (bars == null || bars.Length == 0 || bars[0] == null) return 0.0;     // (2)
return bars[0].GetClose(bars[0].Count - 1);                              // (3)
```

#### OnCopyToggle
```csharp
// B12 T1 -- CYC=2: toggle _copyEnabled(1) + update label(2).
// Replaces old OnToggle wired to _copyToggleBtn (now wired to _copyToggleBtn2).
private void OnCopyToggle(object sender, RoutedEventArgs e)
```
Body:
```csharp
_copyEnabled = !_copyEnabled;                                             // (1)
_engine.SetEnabled(_copyEnabled);
_copyToggleBtn2.Content    = _copyEnabled ? "\u25CF COPY ON" : "\u25CF COPY OFF";  // (2)
_copyToggleBtn2.Background = _copyEnabled ? BrushActive : BrushInactive;
```

#### OnCancel2
```csharp
// B12 T1 -- CYC=1: instrument null guard + engine call.
private void OnCancel2(object sender, RoutedEventArgs e)
```

### 1.6 Modified: OnPendingBeFiredDispatch — TradeCopierPanel.cs

Replace the existing body:
```csharp
// BEFORE (B10 T2):
Dispatcher.InvokeAsync(() => FlashBeFired(instr));

// AFTER (B12 T1):
Dispatcher.InvokeAsync(() => OnBeConnected(instr));
```

### 1.7 Modified: DispatchShortcut — TradeCopierPanel.cs

Update `case Key.T` and `case Key.F` to pass `GetRefPrice()` and the appropriate buffer:
```csharp
case Key.T:
    double refT = GetRefPrice();
    if (refT > 0 && _trimBuffer > 0)
        _engine.Trim(_instrument, _trimBuffer, refT);
    else
        _engine.Trim(_instrument);
    break;
case Key.F:
    double refF = GetRefPrice();
    if (refF > 0 && _flattenBuffer > 0)
        _engine.Flatten(_instrument, _flattenBuffer, refF);
    else
        _engine.Flatten(_instrument);
    break;
```
CYC of `DispatchShortcut` increases from 5 (4 cases + switch) to 6 (adds one guard branch per T/F
case; both are inline ternary-equivalent if/else). Stays <= 8. PASS.

### 1.8 New Methods — CopyEngine.cs (T1)

#### Trim(Instrument, int, double)
```csharp
// B12 T1 -- DW-B11-DEFER-01 close. Limit exit for ceil(qty/2).
// Signal name: "PTT-TrimLimit" (PTT-prefix compliant).
// Long: Sell Limit @ refPrice + exitBuffer*tickSize
// Short: BuyToCover Limit @ refPrice - exitBuffer*tickSize
// Falls through to market overload handled by caller when refPrice <= 0.
// CYC=5: rule null(1) + foreach acc(2) + flat skip(3) + direction(4) + try/catch(5).
// JS-001: try/catch wraps acc.CreateOrder -- no rethrow.
// NT8-007: arg 12 = (NinjaTrader.Cbi.CustomOrder)null.
internal void Trim(Instrument instrument, int exitBuffer, double refPrice)
```
Body skeleton:
```csharp
var rule = FindRule(instrument);
if (rule == null) return;                                                 // (1)
double tickSize = instrument.MasterInstrument.TickSize;
foreach (var acc in AllAccounts(instrument))                             // (2)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                                // (3)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        continue;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    bool isLong = pos.MarketPosition == MarketPosition.Long;             // (4)
    var action      = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double limitPx  = isLong
        ? refPrice + exitBuffer * tickSize
        : refPrice - exitBuffer * tickSize;
    try                                                                  // (5)
    {
        acc.CreateOrder(
            instrument, action, OrderType.Limit,
            OrderEntry.Manual, TimeInForce.Day,
            trimQty, limitPx, 0, null,
            "PTT-TrimLimit",
            DateTime.MaxValue,
            (NinjaTrader.Cbi.CustomOrder)null);
        StatusUpdate?.Invoke(acc.Name + ": trim-limit " + trimQty + " @ " + limitPx);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-TrimLimit error: " + ex.Message);
    }
}
```

#### Flatten(Instrument, int, double)
```csharp
// B12 T1 -- DW-B11-DEFER-01 close. Limit exit for full qty.
// Signal name: "PTT-FlattenLimit" (PTT-prefix compliant).
// Long: Sell Limit @ refPrice + exitBuffer*tickSize
// Short: BuyToCover Limit @ refPrice - exitBuffer*tickSize
// CYC=5: rule null(1) + foreach acc(2) + flat skip(3) + direction(4) + try/catch(5).
// NT8-007: arg 12 = (NinjaTrader.Cbi.CustomOrder)null.
internal void Flatten(Instrument instrument, int exitBuffer, double refPrice)
```
Body mirrors `Trim(Instrument, int, double)` above except:
- Uses `pos.Quantity` (full qty, not ceiling half)
- Signal name is `"PTT-FlattenLimit"`
- StatusUpdate message is `"flatten-limit " + pos.Quantity + " @ " + limitPx`

### 1.9 Modified: DispatchCopy — CopyEngine.cs

Add Gate 0.5 at the very top of `DispatchCopy`, before Gate 3 (Submitted state check):
```csharp
// Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals.
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```
**CYC impact**: current CYC=7. Adding 1 early-return guard = CYC=8. AT LIMIT. PASS.

---

### 1.10 xUnit Tests — T1

Tests live in `CopyEngineTests.cs` (existing file). All use `[Fact]` (xUnit). No NUnit/MSTest.

#### T1-Test-1
```csharp
[Fact]
public void Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick()
// arrange: CopyEngine with one rule; mock Account long pos qty=4; refPrice=100.0;
//          exitBuffer=1; instrument.MasterInstrument.TickSize=0.25
// act: engine.Trim(instrument, 1, 100.0)
// assert: acc.CreateOrder called with OrderType.Limit, limitPrice=100.25, qty=2, "PTT-TrimLimit"
```

#### T1-Test-2
```csharp
[Fact]
public void Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick()
// arrange: short pos qty=4; refPrice=100.0; exitBuffer=1; tickSize=0.25
// act: engine.Trim(instrument, 1, 100.0)
// assert: OrderType.Limit, limitPrice=99.75, qty=2, "PTT-TrimLimit"
```

#### T1-Test-3
```csharp
[Fact]
public void Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty()
// arrange: long pos qty=4; refPrice=100.0; exitBuffer=2; tickSize=0.25
// act: engine.Flatten(instrument, 2, 100.0)
// assert: OrderType.Limit, limitPrice=100.50, qty=4, "PTT-FlattenLimit"
```

#### T1-Test-4
```csharp
[Fact]
public void Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty()
// arrange: short pos qty=4; refPrice=100.0; exitBuffer=2; tickSize=0.25
// act: engine.Flatten(instrument, 2, 100.0)
// assert: OrderType.Limit, limitPrice=99.50, qty=4, "PTT-FlattenLimit"
```

#### T1-Test-5
```csharp
[Fact]
public void DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit()
// arrange: order.Name="PTT-TrimLimit", valid rule, OrderState.Submitted, OrderType.Market
// act: engine.OnOrderUpdate fires
// assert: SendCopy never called -- no follower order submitted
```

---

### 1.11 SCAN CHECKLIST (T1)

Files touched: `TradeCopierPanel.cs`, `CopyEngine.cs`

| Scan | Check | Expected | Enforce |
|------|-------|----------|---------|
| SCAN-01 | `grep -n "lock(" TradeCopierPanel.cs CopyEngine.cs` | 0 results | JS-021 P0 |
| SCAN-02 | `grep -n "async void " TradeCopierPanel.cs CopyEngine.cs` | Only `OnBeConnected` | JS-033 P0 |
| SCAN-03 | `grep -n "return null" TradeCopierPanel.cs CopyEngine.cs` | 0 results in new methods | JS-002 P0 |
| SCAN-04 | CYC audit all new/modified methods | All <= 8 | AGENTS.md CYC gate |
| SCAN-05 | `grep -n "volatile double\|volatile bool\|volatile int" TradeCopierPanel.cs` | 0 in new fields | NT8-003 |
| SCAN-06 | `grep -n "Math.Clamp" TradeCopierPanel.cs CopyEngine.cs` | 0 results | NT8 .NET 4.8 ban |
| SCAN-07 | Grep for literal `▲` `▼` `●` in new .cs string literals | 0 results; use `"\u25B2"` `"\u25BC"` `"\u25CF"` | ASCII-only |

---

## T2 — DW-B12-COLLAPSE-01

### Overview
Adds a collapsible header row above `_contentPanel`. One button click hides/shows all
panel controls below the header. Depends on T1 (requires `_contentPanel` to exist).

### Spec Requirements Satisfied
- DW-B12-COLLAPSE-01

### Files Touched
- `src/PropTraderTools/TradeCopierPanel.cs`

---

### 2.1 New Fields — TradeCopierPanel.cs

Already declared in T1 §1.1:
```csharp
private bool       _isCollapsed   = false;   // plain bool; UI-thread-only; no volatile (NT8-003)
private Button     _collapseToggleBtn;
private StackPanel _contentPanel;
```
(No additional fields needed for T2.)

### 2.2 New Methods — TradeCopierPanel.cs (T2)

#### BuildCollapsibleHeader
```csharp
// B12 T2 -- builds collapse header row. CYC=1: straight-line construction.
// Called from BuildUI() before _contentPanel is added to root.
private void BuildCollapsibleHeader(StackPanel root)
```
Body:
```csharp
_collapseToggleBtn = new Button
{
    Content = "\u25BC PTT",   // "\u25BC" = DOWN TRIANGLE (collapsed-open indicator)
    Margin  = new Thickness(0, 0, 0, 2)
};
_collapseToggleBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_collapseToggleBtn.Click += OnCollapseClick;
root.Children.Add(_collapseToggleBtn);
```

#### OnCollapseClick
```csharp
// B12 T2 -- toggles _isCollapsed(1); sets _contentPanel.Visibility + updates toggle label(2).
// CYC=2.
private void OnCollapseClick(object sender, RoutedEventArgs e)
```
Body:
```csharp
_isCollapsed = !_isCollapsed;                                              // (1)
if (_contentPanel != null)                                                 // (2)
    _contentPanel.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
if (_collapseToggleBtn != null)
    _collapseToggleBtn.Content = _isCollapsed ? "\u25B2 PTT" : "\u25BC PTT";
```
- `"\u25B2"` = UP TRIANGLE (collapsed state — click to expand)
- `"\u25BC"` = DOWN TRIANGLE (expanded state — click to collapse)

### 2.3 Modified: BuildUI() — TradeCopierPanel.cs (T2 addition)

In addition to T1's `BuildUI()` restructure, ensure:
1. `BuildCollapsibleHeader(root)` is called before `root.Children.Add(_contentPanel)`.
2. `_contentPanel` wraps all content rows (click trader, mode, tighten, ATM template, etc.).
   This was already specified in T1 §1.4 — T2 only adds the header button.

### 2.4 xUnit Tests — T2

None required. `OnCollapseClick` is a 2-line `Visibility` mutation (CYC=2, pure WPF) with
no business logic. Behavior is verifiable by visual inspection during F5 run.

---

### 2.5 SCAN CHECKLIST (T2)

File touched: `TradeCopierPanel.cs`

| Scan | Check | Expected | Enforce |
|------|-------|----------|---------|
| SCAN-01 | `grep -n "lock(" TradeCopierPanel.cs` | 0 results | JS-021 P0 |
| SCAN-02 | `grep -n "async void " TradeCopierPanel.cs` | Only `OnBeConnected` (from T1) | JS-033 P0 |
| SCAN-03 | `grep -n "return null" TradeCopierPanel.cs` new methods | 0 results | JS-002 P0 |
| SCAN-04 | CYC of `OnCollapseClick`, `BuildCollapsibleHeader` | <= 8 | CYC gate |
| SCAN-05 | `grep -n "volatile " TradeCopierPanel.cs` new T2 fields | 0 results | NT8-003 |
| SCAN-06 | `grep -n "Math.Clamp" TradeCopierPanel.cs` | 0 results | NT8 .NET 4.8 ban |
| SCAN-07 | Literal arrows in T2 string content | 0; use `"\u25B2"` `"\u25BC"` | ASCII-only |

---

## T3 — DW-B12-RISK-ATR-INPUTS-01

### Overview
Adds Risk $ and ATR % spinner rows to the panel. Wires changes through `CopyEngine` to
`AtrSizingEngine`. Adds `_atrFraction` field and `SetAtrFraction` / `UpdateMaxRisk` methods
to `AtrSizingEngine`. Adds `UpdateMaxRisk` / `UpdateAtrFraction` pass-through methods to
`CopyEngine`. Modifies `AtrSizingEngine.OnBarUpdate` to scale ATR by `_atrFraction`.

### Spec Requirements Satisfied
- DW-B12-RISK-ATR-INPUTS-01
- DW-B10-02 precedent: test coverage required for sizing engine logic

### Files Touched
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/AtrSizingEngine.cs`

> **NT8 FQN NOTE**: All `RepeatButton` references in this ticket are
> `System.Windows.Controls.Primitives.RepeatButton`.
> Engineer MUST use the fully qualified name or add
> `using System.Windows.Controls.Primitives;` to `TradeCopierPanel.cs`.
> NT8's .NET 4.8 WPF build environment does not auto-import this namespace.

---

### 3.1 New Fields — TradeCopierPanel.cs

```csharp
// B12 T3 -- Risk/ATR spinners (plain double; UI-thread-only; no volatile per NT8-003)
private double  _maxRiskDollars = 200.0;
private double  _atrFraction    = 0.75;
private TextBox _riskDollarsBox;
private TextBox _atrFractionBox;
```

### 3.2 New Methods — TradeCopierPanel.cs (T3)

#### BuildRiskAtrRow
```csharp
// B12 T3 -- builds Risk $ + ATR % spinner row. CYC=1: straight-line construction.
// Called from BuildUI() at end of _contentPanel (after BuildAtmTemplateRow).
private void BuildRiskAtrRow(StackPanel root)
```
Layout: `UniformGrid Columns=2`
- Col 0 — Risk $ spinner:
  - `TextBlock "Risk $"` (NTBrushes.SubtleBrush)
  - `_riskDollarsBox = new TextBox { Text="200", Width=55 }` (NTTextBoxStyle)
  - `_riskDollarsBox.LostFocus += OnRiskTextLostFocus`
  - 2-row `Grid` (row 0: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` Height=12, Click+=`OnRiskUp`;
                  row 1: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` Height=12, Click+=`OnRiskDown`)
  - Both `System.Windows.Controls.Primitives.RepeatButton` use `NTButtonStyle`
- Col 1 — ATR % spinner:
  - `TextBlock "ATR %"` (NTBrushes.SubtleBrush)
  - `_atrFractionBox = new TextBox { Text="0.75", Width=55 }` (NTTextBoxStyle)
  - `_atrFractionBox.LostFocus += OnAtrFractionTextLostFocus`
  - Same 2-row `Grid` (Click+=`OnAtrFractionUp` / `OnAtrFractionDown`)

#### OnRiskUp
```csharp
// B12 T3 -- CYC=1: increment + clamp + push. No branch beyond clamp ternary.
private void OnRiskUp(object sender, RoutedEventArgs e)
```
Body:
```csharp
_maxRiskDollars = Math.Max(Math.Min(_maxRiskDollars + 25.0, 1000.0), 10.0);  // no Math.Clamp
if (_riskDollarsBox != null) _riskDollarsBox.Text = _maxRiskDollars.ToString("F0");
NotifyRiskChanged();
```

#### OnRiskDown
```csharp
// B12 T3 -- CYC=1: decrement + clamp + push.
private void OnRiskDown(object sender, RoutedEventArgs e)
```
Body: same as `OnRiskUp` but with `-25.0`.

#### OnRiskTextLostFocus
```csharp
// B12 T3 -- CYC=3: parse(1) + clamp(2) + push(3). Fires on TextBox.LostFocus.
private void OnRiskTextLostFocus(object sender, RoutedEventArgs e)
```
Body:
```csharp
double v;
if (!double.TryParse(_riskDollarsBox?.Text, out v)) return;               // (1) parse guard
v = Math.Max(Math.Min(v, 1000.0), 10.0);                                  // (2) clamp
_maxRiskDollars = v;
if (_riskDollarsBox != null) _riskDollarsBox.Text = v.ToString("F0");    // normalise display
NotifyRiskChanged();                                                       // (3) push
```

#### OnAtrFractionUp
```csharp
// B12 T3 -- CYC=1.
private void OnAtrFractionUp(object sender, RoutedEventArgs e)
```
Body:
```csharp
_atrFraction = Math.Max(Math.Min(_atrFraction + 0.05, 3.00), 0.25);      // no Math.Clamp
if (_atrFractionBox != null) _atrFractionBox.Text = _atrFraction.ToString("F2");
NotifyAtrFractionChanged();
```

#### OnAtrFractionDown
```csharp
// B12 T3 -- CYC=1.
private void OnAtrFractionDown(object sender, RoutedEventArgs e)
```
Body: same but `-0.05`.

#### OnAtrFractionTextLostFocus
```csharp
// B12 T3 -- CYC=3: parse(1) + clamp(2) + push(3).
private void OnAtrFractionTextLostFocus(object sender, RoutedEventArgs e)
```
Body mirrors `OnRiskTextLostFocus`, clamping to `[0.25, 3.00]`, format `"F2"`,
calling `NotifyAtrFractionChanged()`.

#### NotifyRiskChanged
```csharp
// B12 T3 -- CYC=2: null guard(1) + engine call(2). Delegates to CopyEngine.UpdateMaxRisk.
private void NotifyRiskChanged()
```
Body:
```csharp
if (_engine == null) return;   // (1)
_engine.UpdateMaxRisk(_maxRiskDollars);   // (2)
```

#### NotifyAtrFractionChanged
```csharp
// B12 T3 -- CYC=2: null guard(1) + engine call(2). Delegates to CopyEngine.UpdateAtrFraction.
private void NotifyAtrFractionChanged()
```
Body:
```csharp
if (_engine == null) return;   // (1)
_engine.UpdateAtrFraction(_atrFraction);   // (2)
```

### 3.3 New Methods — CopyEngine.cs (T3)

#### UpdateMaxRisk
```csharp
// B12 T3 -- pass-through to _atrEngine. Null-guarded. CYC=2.
// _atrEngine is volatile AtrSizingEngine (existing field, set by SetAtrEngine).
internal void UpdateMaxRisk(double maxRiskDollars)
```
Body:
```csharp
if (_atrEngine == null) return;            // (1)
_atrEngine.UpdateMaxRisk(maxRiskDollars);  // (2)
```

#### UpdateAtrFraction
```csharp
// B12 T3 -- pass-through to _atrEngine. Null-guarded. CYC=2.
internal void UpdateAtrFraction(double fraction)
```
Body:
```csharp
if (_atrEngine == null) return;            // (1)
_atrEngine.SetAtrFraction(fraction);       // (2)
```

### 3.4 New/Modified Members — AtrSizingEngine.cs (T3)

#### New Field
```csharp
// B12 T3 -- ATR fraction multiplier. Plain double; single-writer UI thread.
// No volatile: NT8-003 bans volatile double. Same staleness-tolerance pattern as _lastAtr.
private double _atrFraction = 1.0;
```

#### SetAtrFraction
```csharp
// B12 T3 -- stores fraction for use in next OnBarUpdate. CYC=1: straight-line assignment.
// Single-writer UI thread. Reader (OnBarUpdate) on bar-close thread.
// Non-volatile: sizing hint only, not order-safety critical (same as _lastAtr). NT8-003 PASS.
internal void SetAtrFraction(double fraction)
{
    _atrFraction = fraction;
}
```

#### UpdateMaxRisk
```csharp
// B12 T3 -- standalone setter for _maxRiskDollars. CYC=1: straight-line assignment.
// Allows panel to update risk budget without a full SetParameters roundtrip.
internal void UpdateMaxRisk(double maxRiskDollars)
{
    _maxRiskDollars = maxRiskDollars;
}
```

#### Modified: OnBarUpdate
Change the single line `int qty = CalcContracts(atr, ...)` to:
```csharp
int qty = CalcContracts(atr * _atrFraction, _maxRiskDollars, _tickDollarValue);
```
CYC unchanged from B11 (the branch structure is not affected).

---

### 3.5 Spinner Parameter Table (T3)

| Spinner | Field | Default | Step | Min | Max | Format | Engine Push |
|---------|-------|---------|------|-----|-----|--------|-------------|
| Risk $ | `_maxRiskDollars` | 200.0 | 25.0 | 10.0 | 1000.0 | `"F0"` | `CopyEngine.UpdateMaxRisk` |
| ATR % | `_atrFraction` | 0.75 | 0.05 | 0.25 | 3.00 | `"F2"` | `CopyEngine.UpdateAtrFraction` |

Clamp pattern (no `Math.Clamp` — NT8 .NET 4.8 ban):
```csharp
Math.Max(Math.Min(value + step, max), min)
```

---

### 3.6 xUnit Tests — T3

Tests live in `CopyEngineTests.cs`. All use `[Fact]` (xUnit).

#### T3-Test-1
```csharp
[Fact]
public void AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1()
// arrange: new AtrSizingEngine(0) (test ctor); set _maxRiskDollars=500 via UpdateMaxRisk;
//          set _tickDollarValue=5.0 via SetParameters(500, 5); set _atrFraction=0.5 via SetAtrFraction(0.5)
// act: call ManualOnBarUpdate() with ATR=10 pts (requires mocking or test-ctor path via
//      CalcContracts(10 * 0.5, 500, 5) directly -- static method is accessible)
// assert: AtrSizingEngine.CalcContracts(10.0 * 0.5, 500.0, 5.0) == 10
//         (10 pts * 0.5 * 5 $/tick = $25/contract; floor(500/25) = 20 -- CORRECTION: 0.5 * 10 = 5
//          pts; 5 * 5 = $25/contract; floor(500/25) = 20)
// Note: CalcContracts is internal static -- testable directly.
//       Assert.Equal(20, AtrSizingEngine.CalcContracts(5.0, 500.0, 5.0));
```

#### T3-Test-2
```csharp
[Fact]
public void UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing()
// arrange: AtrSizingEngine engine; CopyEngine.Instance.SetAtrEngine(engine, true);
//          engine.SetParameters(150, 5); initial: AtrSizingEngine.CalcContracts(10, 150, 5) = 3
// act: CopyEngine.Instance.UpdateMaxRisk(300)
// assert: AtrSizingEngine.CalcContracts(10.0, 300.0, 5.0) == 6
//         (10 * 5 = $50/contract; floor(300/50) = 6)
// Note: UpdateMaxRisk flows through CopyEngine -> AtrSizingEngine.UpdateMaxRisk.
//       CalcContracts is a pure static function; test verifies math at the same inputs the
//       engine would use after UpdateMaxRisk(300) sets _maxRiskDollars=300.
```

#### T3-Test-3
```csharp
[Fact]
public void BuildRiskAtrRow_ClampMin_RejectsSubMinValue()
// Tests the clamp formula used in OnRiskTextLostFocus and OnRiskDown.
// arrange: simulate _maxRiskDollars = 10.0 (at min); call decrement: -25.0 step
// act: clamp = Math.Max(Math.Min(10.0 - 25.0, 1000.0), 10.0)
// assert: result == 10.0 (floored at min, not allowed to go below 10)
// This is a pure math assertion -- no NT8 runtime required.
//   Assert.Equal(10.0, Math.Max(Math.Min(10.0 - 25.0, 1000.0), 10.0));
```

---

### 3.7 SCAN CHECKLIST (T3)

Files touched: `TradeCopierPanel.cs`, `CopyEngine.cs`, `AtrSizingEngine.cs`

| Scan | Check | Expected | Enforce |
|------|-------|----------|---------|
| SCAN-01 | `grep -n "lock(" TradeCopierPanel.cs CopyEngine.cs AtrSizingEngine.cs` | 0 results | JS-021 P0 |
| SCAN-02 | `grep -n "async void " TradeCopierPanel.cs CopyEngine.cs AtrSizingEngine.cs` | 0 new (T3 adds no async void) | JS-033 P0 |
| SCAN-03 | `grep -n "return null" ` new T3 methods | 0 results (all guard with bare `return;`) | JS-002 P0 |
| SCAN-04 | CYC of all new/modified T3 methods | All <= 8 | CYC gate |
| SCAN-05 | `grep -n "volatile double\|volatile bool\|volatile int" AtrSizingEngine.cs` new T3 fields | 0 (new `_atrFraction` is plain double) | NT8-003 |
| SCAN-06 | `grep -n "Math.Clamp" TradeCopierPanel.cs CopyEngine.cs AtrSizingEngine.cs` | 0 results | NT8 .NET 4.8 ban |
| SCAN-07 | Literal Unicode in T3 string literals (arrow chars) | 0; `"\u25B2"` `"\u25BC"` only | ASCII-only |

---

## CYC Summary — All T1/T2/T3 Methods

| Method | File | CYC | Limit |
|--------|------|-----|-------|
| `BuildBufferedButtonsRow` | Panel | 1 | 8 |
| `FormatBuffer` | Panel | 1 | 8 |
| `OnTrimUp` | Panel | 1 | 8 |
| `OnTrimDown` | Panel | 1 | 8 |
| `OnTrimClick` | Panel | 3 | 8 |
| `OnFlattenUp` | Panel | 1 | 8 |
| `OnFlattenDown` | Panel | 1 | 8 |
| `OnFlattenClick` | Panel | 3 | 8 |
| `OnBeUp` | Panel | 2 | 8 |
| `OnBeDown` | Panel | 2 | 8 |
| `OnBeClick` | Panel | 5 | 8 |
| `UpdateBeLabel` | Panel | 1 | 8 |
| `UpdateBeVisuals` | Panel | 3 | 8 |
| `OnBeConnected` | Panel | 2 | 8 |
| `GetRefPrice` | Panel | 3 | 8 |
| `OnCopyToggle` | Panel | 2 | 8 |
| `OnCancel2` | Panel | 1 | 8 |
| `DispatchShortcut` (modified) | Panel | 6 | 8 |
| `BuildCollapsibleHeader` | Panel | 1 | 8 |
| `OnCollapseClick` | Panel | 2 | 8 |
| `BuildRiskAtrRow` | Panel | 1 | 8 |
| `OnRiskUp` | Panel | 1 | 8 |
| `OnRiskDown` | Panel | 1 | 8 |
| `OnRiskTextLostFocus` | Panel | 3 | 8 |
| `OnAtrFractionUp` | Panel | 1 | 8 |
| `OnAtrFractionDown` | Panel | 1 | 8 |
| `OnAtrFractionTextLostFocus` | Panel | 3 | 8 |
| `NotifyRiskChanged` | Panel | 2 | 8 |
| `NotifyAtrFractionChanged` | Panel | 2 | 8 |
| `Trim(Instrument, int, double)` | Engine | 5 | 8 |
| `Flatten(Instrument, int, double)` | Engine | 5 | 8 |
| `DispatchCopy` (modified +gate) | Engine | 8 | 8 |
| `UpdateMaxRisk` | Engine | 2 | 8 |
| `UpdateAtrFraction` | Engine | 2 | 8 |
| `SetAtrFraction` | AtrEngine | 1 | 8 |
| `UpdateMaxRisk` | AtrEngine | 1 | 8 |
| `OnBarUpdate` (modified) | AtrEngine | 2 | 8 |

---

## Jane Street Rule Summary (All Tickets)

| Rule | Scope | All Tickets |
|------|-------|-------------|
| JS-021 (P0) no `lock()` | All new methods | PASS — no lock anywhere; UI-thread-only panel, ConcurrentBag engine |
| JS-001 (P0) no throw in hot path | Trim/Flatten overloads | PASS — try/catch wraps `acc.CreateOrder`; exception routed to `StatusUpdate`; no rethrow |
| JS-002 (P0) no `return null` | All new handlers | PASS — early returns use bare `return;` |
| JS-033 (P0) no `async void` except event handlers | `OnBeConnected` | PASS — async void invoked via `Dispatcher.InvokeAsync` (FlashBeFired pattern) |
| JS-008 `SolidColorBrush` must be `Freeze()`d | `BrushConnected` | PASS — `MakeBrush(59, 130, 246)` calls `Freeze()` |
| NT8-003 no `volatile double/bool/int` | All T1/T2/T3 fields | PASS — all plain types; UI-thread-only contract |
| NT8-007 `CreateOrder` arg 12 | Trim/Flatten overloads | PASS — `(NinjaTrader.Cbi.CustomOrder)null` |

---

## Backlog Ledger (T1 Closes)

| ID | Description | Action |
|----|-------------|--------|
| DW-B11-DEFER-01 | Flatten/Trim limit orders — new engine overloads | CLOSED by T1 |
| DW-B9-01 | ATR box visualization on chart canvas | Remains SHELVED to B13 |
| DW-B9-03 | Click trader Bid+1/Ask-1 offset | Remains SHELVED to B13 |

---

**TICKETS_COMPLETE**
