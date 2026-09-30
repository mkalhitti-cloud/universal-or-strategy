# PTT-COPIER-B9 — Architecture Plan
**Status**: PLAN_COMPLETE
**Architect**: PTT Architect (Phase 1)
**Date**: 2026-07-09
**Prerequisite**: B8 FINAL_PASS (40 [Fact] tests, 5 files, 7/7 scans green)
**Input deferred backlog**: docs/brain/PTT-COPIER-B8/06-deferred-backlog.md

---

## Section 1: B9 Scope Decision

### P1 Items — MANDATORY (both in scope)

| ID | Item | File | Ticket |
|----|------|------|--------|
| DW-B8-03 / DW-B7-02 | ATR Dynamic Sizing Engine | AtrSizingEngine.cs (NEW) | T1 |
| DW-B8-04 | Click Trader (chart-click limit entry) | TradeCopierPanel.cs + TradeCopierAddOn.cs | T2 |

### P2 Items — Scope Decision

| ID | Item | Decision | Rationale |
|----|------|----------|-----------|
| DW-B8-06 | Mirror Mode (MirrorOrderUpdate) | **IN SCOPE — T3** | B8 prerequisite (FollowerAtmMode.Named wiring) is satisfied. Adds ~55 lines to CopyEngine, independent ticket. Spec calls it "B9 candidate." |
| SPEC-2354 | Named ATM inline template TextBox | **IN SCOPE — T3** | UI-only, ~15 lines per surface. Co-located with T3 Panel/Window changes. No complication to P1. |
| DW-B8-01 | JS-002 return null cleanup | **CLOSED as already compliant** | FindFollowerBracketOrder returns `Order?` (nullable reference type). FindPosition returns nullable value. FindRule iterates inline. Callers null-check correctly. No code change needed. |
| DW-B8-02 | Gate hook path fix | **OUT OF SCOPE (non-source)** | `.bob/hooks/pre_task_rules_gate.py` config change — not a source ticket. Handled separately. |
| DW-B8-05 | ATR box visualization | **DEFERRED B10** | Depends on AtrSizingEngine but adds chart drawing complexity. Excluded to keep B9 focused. |

### Final B9 File Set

6 files in Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`):

| # | File | B9 Role |
|---|------|---------|
| 1 | `AtrSizingEngine.cs` | **NEW** — ATR sizing engine as NT8 Indicator subclass |
| 2 | `CopyEngine.cs` | Modified — ATR integration (T1) + Mirror mode (T3) |
| 3 | `TradeCopierAddOn.cs` | Modified — ATR engine lifecycle (T1) + click trader registration (T2) |
| 4 | `TradeCopierPanel.cs` | Modified — Click trader UI (T2) + Mirror toggle + Named ATM inline (T3) |
| 5 | `TradeCopierWindow.cs` | Modified — Named ATM inline TextBox (T3) |
| 6 | `CopyEngineTests.cs` | Modified — 20 new [Fact] tests across T1/T2/T3 |

---

## Section 2: New File — AtrSizingEngine.cs

### NT8 Architecture Note

`MarketData.Subscribe` and `Data[]` bar series are only available on `NinjaScriptBase` subclasses.
`AddOnBase` has no `OnBarUpdate`, no `Data[]`, no `MarketData` property.

**B9 design:** `AtrSizingEngine` extends `Indicator` — a detached NT8 Indicator whose lifecycle is managed
by `TradeCopierAddOn`. The AddOn creates one engine per chart, attaches it to the chart's indicator
collection, and destroys it when the chart closes.

> **IMPL-NOTE-1:** Exact attachment API for adding an Indicator to a Chart in AddOn context must be verified
> at T1 execution time. Candidates: `chart.NinjaScripts.Add(engine)` or `chart.Indicators.Add(engine)`.
> Fallback if neither works: subscribe to `chart.ChartControl.BarsArray[0].BarClosed` event from the AddOn
> and pass bar data to `AtrSizingEngine.OnBarClosed(object s, BarEventArgs e)`. Both paths write to the same
> `volatile double _lastAtr` field. The public interface (`SetParameters`, `GetSuggestedQty`,
> `CalcContracts`) is unchanged by the fallback path.

### Class Design

```csharp
// B9 T1 -- AtrSizingEngine.cs
// Detached NT8 Indicator providing ATR-based contract sizing.
// Managed by TradeCopierAddOn (one instance per chart).
// Jane Street rules: JS-021 (no lock), JS-023 (volatile for cross-thread fields)
// CYC <= 8 per method.

namespace PropTraderTools
{
    public class AtrSizingEngine : Indicator
    {
        // --- Cross-thread fields (volatile -- JS-023) ---
        private volatile int    _lastContracts = 1;     // written data thread, read UI thread
        private volatile double _lastAtr       = 0.0;   // written data thread, read UI thread
        private volatile bool   _hasData       = false; // true after first bar with valid ATR

        // --- Configuration (set before attachment, single-writer UI thread) ---
        private double _maxRiskDollars  = 150.0;  // default max risk $150
        private double _tickDollarValue = 5.0;    // default MES: 1 point = $5

        // --- NT8 lifecycle ---
        protected override void OnStateChange();   // CYC=4: SetDefaults/Configure/DataLoaded/Terminated
        protected override void OnBarUpdate();     // CYC=2: CurrentBar guard + update _lastAtr + _lastContracts

        // --- Public interface (called by CopyEngine and TradeCopierPanel) ---
        internal void SetParameters(double maxRiskDollars, double tickDollarValue);  // CYC=1
        internal int  GetSuggestedQty();                                              // CYC=2: hasData guard + return
        internal double GetLastAtr();                                                 // CYC=1: read volatile

        // --- Pure math: static -- fully unit-testable without NT8 context ---
        // CYC=3: three guard branches (atr<=0, tickVal<=0, result<1)
        internal static int CalcContracts(double atrPoints, double maxRisk, double tickDollarValue);
    }
}
```

### Method Details

**`OnStateChange()` — CYC=4**
```
State.SetDefaults : Description = "PTT ATR Sizing Engine", Name = "AtrSizingEngine"
State.Configure   : AddDataSeries(instrument, BarsPeriodType.Minute, 1) — subscribe to 1-min bars
State.DataLoaded  : Add(ATR(Period)) as child indicator — read via Values[0][0]
State.Terminated  : _hasData = false; _lastContracts = 1; _lastAtr = 0.0
```
`Period` is an `[NinjaScriptProperty]` int, default 14.

**`OnBarUpdate()` — CYC=2**
```
if (CurrentBar < Period) return;                // guard (1)
double atr = ATR(Period)[0];                    // read NT8 built-in ATR
_lastAtr      = atr;                            // volatile write
_lastContracts = CalcContracts(atr, _maxRiskDollars, _tickDollarValue); // volatile write
_hasData = true;                                // volatile write -- no branch (1)
```

**`CalcContracts(double atrPoints, double maxRisk, double tickDollarValue)` — CYC=3**
```
if (atrPoints     <= 0) return 1;              // guard (1): zero/negative ATR
if (tickDollarValue <= 0) return 1;            // guard (2): zero tick value
double riskPerContract = atrPoints * tickDollarValue;
int contracts = (int)Math.Floor(maxRisk / riskPerContract);
return contracts < 1 ? 1 : contracts;          // guard (3): minimum 1 contract
```

**`GetSuggestedQty()` — CYC=2**
```
if (!_hasData) return 1;                        // guard (1): no bar data yet
return _lastContracts;                          // volatile read (2 -- return branch)
```

### CYC Summary for AtrSizingEngine

| Method | CYC | Under Limit? |
|--------|-----|-------------|
| `OnStateChange` | 4 | ✅ |
| `OnBarUpdate` | 2 | ✅ |
| `CalcContracts` | 3 | ✅ |
| `GetSuggestedQty` | 2 | ✅ |
| `SetParameters` | 1 | ✅ |
| `GetLastAtr` | 1 | ✅ |

---

## Section 3: Click Trader Design

### Summary

Tap a price level on the chart → limit order placed at that price on the master account
→ `CopyEngine.OnOrderUpdate` copies to all followers as normal ("PTT-Click" signal name).

### Visual Overlay

When armed: `chartControl.BorderBrush = MakeBrush(34, 197, 94)` (green) + `BorderThickness = 2`.
When disarmed: `chartControl.BorderBrush = null` + `BorderThickness = 0`.
Brush created via `MakeBrush(r,g,b)` (calls `Freeze()` — JS-008). No hex literals.

### New Panel UI Elements (`TradeCopierPanel.cs` — T2)

Appended as a new row in `BuildUI()`:

```
Row: [Buy] [Sell] toggle pair  |  [Arm] button  |  ATR: [MaxRisk TextBox] [$]
```

- `_buyToggle` / `_sellToggle` — `ToggleButton` pair (mutual exclusion). Default: Buy selected.
- `_armBtn` — `Button` with label "Arm" / "Disarm". Background: `BrushActive` when armed, `BrushInactive` when not.
- `volatile bool _clickArmed` — JS-023.
- `volatile bool _clickBuy` — JS-023: true=Buy, false=Sell direction.
- `Chart _currentChart` — set by `TradeCopierAddOn` via `panel.SetChart(chart)` in `DoInject`.

### New Methods (`TradeCopierPanel.cs`)

```csharp
public  void SetChart(Chart chart)                                            // CYC=1 — called by AddOn
private void BuildClickTraderRow()                                            // CYC=1 — called from BuildUI
private void OnArmClick(object sender, RoutedEventArgs e)                    // CYC=2 — arm/disarm toggle
private void UpdateArmVisuals(bool armed)                                     // CYC=2 — border + button color
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)        // CYC=4 — see below
private void OnBuyToggleClick(object sender, RoutedEventArgs e)              // CYC=1 — sets _clickBuy=true
private void OnSellToggleClick(object sender, RoutedEventArgs e)             // CYC=1 — sets _clickBuy=false
```

**`OnChartMouseDown(object sender, MouseButtonEventArgs e)` — CYC=4**
```
if (!_clickArmed)          return;  // guard (1)
if (_leaderAccount == null) return;  // guard (2)
if (_instrument == null)   return;  // guard (3)

var chartControl = sender as ChartControl;
if (chartControl == null)  return;  // guard (4)

double price = chartControl.GetValueByY(e.GetPosition(chartControl).Y);
bool   isBuy = _clickBuy;  // volatile read
int    qty   = CopyEngine.Instance.GetSuggestedQty(_instrument) ;  // ATR qty or 1
var    action = isBuy ? OrderAction.Buy : OrderAction.SellShort;   // NOT a branch — ternary, CYC unchanged

try
{
    _leaderAccount.CreateOrder(
        _instrument, action, OrderType.Limit, OrderEntry.Manual,
        TimeInForce.Day, qty, price, 0, null,
        "PTT-Click",          // signal name — starts with "PTT-" (NT8 constraint)
        DateTime.MaxValue,
        null                  // ATM template: user selects natively in ChartTrader
    );
}
catch (Exception ex)          // try/catch does NOT add to CYC
{
    StatusUpdate?.Invoke("PTT-Click error: " + ex.Message);
}
```

**`OnArmClick` — CYC=2**
```
if (_currentChart == null) return;              // guard (1)
_clickArmed = !_clickArmed;                     // volatile flip
if (_clickArmed)                                // branch (2)
    TradeCopierAddOn.RegisterClickTrader(_currentChart, this);
else
    TradeCopierAddOn.UnregisterClickTrader(_currentChart);
UpdateArmVisuals(_clickArmed);
```

### New Methods (`TradeCopierAddOn.cs`)

```csharp
// Track which panels have click handlers registered
private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _clickHandlers
    = new ConcurrentDictionary<Chart, TradeCopierPanel>();

internal static void RegisterClickTrader(Chart chart, TradeCopierPanel panel)   // CYC=2
internal static void UnregisterClickTrader(Chart chart)                          // CYC=2
```

**`RegisterClickTrader` — CYC=2**
```
if (chart == null) return;                                  // guard (1)
_clickHandlers[chart] = panel;
chart.ChartControl.MouseDown += panel.OnChartMouseDown;     // subscribe
```
Note: If chart already had a handler registered (re-arm), the existing handler is removed first:
```
if (_clickHandlers.TryRemove(chart, out var old))           // guard (2)
    chart.ChartControl.MouseDown -= old.OnChartMouseDown;
```

**`UnregisterClickTrader` — CYC=2**
```
TradeCopierPanel panel;
if (!_clickHandlers.TryRemove(chart, out panel)) return;    // guard (1)
if (chart.ChartControl == null) return;                      // guard (2)
chart.ChartControl.MouseDown -= panel.OnChartMouseDown;
```

**`DoInject` modification:** After creating the panel, call `panel.SetChart(chart)`.
**`OnWindowDestroyed` modification:** Call `UnregisterClickTrader(chart)`.

### AddOn ATR Engine Lifecycle

```csharp
// Per-chart engine tracking
private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines
    = new ConcurrentDictionary<Chart, AtrSizingEngine>();

private static void StartAtrEngine(Chart chart, Instrument instr)   // CYC=3
private static void StopAtrEngine(Chart chart)                       // CYC=2
```

**`StartAtrEngine` — CYC=3**
```
if (chart == null)  return;                               // guard (1)
if (instr == null)  return;                               // guard (2)
var engine = new AtrSizingEngine();
engine.SetParameters(150.0, instr.MasterInstrument?.PointValue ?? 5.0);
// IMPL-NOTE-1: exact attachment verified at T1 execution time
chart.NinjaScripts.Add(engine);                           // OR fallback (see §2 IMPL-NOTE-1)
_atrEngines[chart] = engine;
CopyEngine.Instance.SetAtrEngine(engine, enabled: false); // disabled until user turns ON (3)
```

**`StopAtrEngine` — CYC=2**
```
AtrSizingEngine engine;
if (!_atrEngines.TryRemove(chart, out engine)) return;   // guard (1)
try { chart.NinjaScripts.Remove(engine); } catch { }     // guard (2) — silent on failure
CopyEngine.Instance.SetAtrEngine(null, enabled: false);
```

---

## Section 4: Mirror Mode Design

### Scope

Mirror Mode extends existing bracket-change and copy infrastructure to also relay bracket fill events
to follower accounts. The B8 bracket drag relay (`HandleBracketChange`) already handles stop/target
price moves. Mirror mode adds: when master's bracket leg **fills** (order exits), flatten followers.

`CopyMode.Signal` (default, current behavior) = entry-only copy + independent bracket management per follower.
`CopyMode.Mirror` = entry copy + bracket price relay (existing) + close relay (new).

### New Engine Members (`CopyEngine.cs`)

```csharp
// B9 T3: Copy mode enum + volatile backing int (JS-023)
internal enum CopyMode { Signal = 0, Mirror = 1 }
private volatile int _copyModeValue = 0;               // JS-023: 0=Signal, 1=Mirror

internal void    SetCopyMode(CopyMode mode)            // CYC=1: _copyModeValue = (int)mode
internal CopyMode GetCopyMode()                        // CYC=1: return (CopyMode)_copyModeValue

// Mirror relay methods
private void MirrorOrderUpdate(Order masterOrder, CopyRule rule) // CYC=3: dispatch hub
private void MirrorBracketMove(Order masterOrder, CopyRule rule) // CYC=5: price relay (see §4.2)
private void MirrorClose(Order masterOrder, CopyRule rule)       // CYC=4: flatten relay
private static bool ShouldMirrorClose(Order order)               // CYC=2: state check predicate
```

### `OnOrderUpdate` Modification — CYC Impact

Existing `OnOrderUpdate` CYC=7. Adding mirror mode branch:

```csharp
// After Gate 2.5 (per-rule enable check), before Gate B:
if ((CopyMode)_copyModeValue == CopyMode.Mirror)       // new branch (+1 CYC)
    MirrorOrderUpdate(e.Order, matchedRule.Value);
// existing Gate B continues:
if (IsWorkingBracket(e.Order)) { ... HandleBracketChange ... return; }
DispatchCopy(e.Order, matchedRule.Value);
```

`OnOrderUpdate` CYC after B9: **7 + 1 = 8** (at limit, does not exceed). ✅

### `MirrorOrderUpdate` — CYC=3

```
if (masterOrder == null) return;                                // guard (1)
if (ShouldMirrorClose(masterOrder))                             // branch (2)
{
    MirrorClose(masterOrder, rule);
    return;
}
if (IsWorkingBracket(masterOrder))                              // branch (3)
    MirrorBracketMove(masterOrder, rule);
// else: entry order state -- DispatchCopy handles it already, no-op here
```

### `ShouldMirrorClose` — CYC=2

```
// True when master bracket leg fills -- signal to close followers.
return order.OrderState == OrderState.Filled     // branch (1)
    && IsBracketLeg(order);                      // branch (2 -- AND evaluation)
```

### `MirrorClose` — CYC=4

```
var instr = masterOrder.Instrument;
if (instr == null) return;                                       // guard (1)
foreach (var acc in rule.FollowerAccounts)                       // loop (2)
{
    if (acc == null) continue;                                   // guard (3)
    var pos = FindPosition(acc, instr);
    if (pos == null || pos.Quantity == 0) continue;             // guard (4)
    var action = pos.MarketPosition == MarketPosition.Long
        ? OrderAction.Sell : OrderAction.BuyToCover;
    try
    {
        acc.CreateOrder(instr, action, OrderType.Market,
            OrderEntry.Manual, TimeInForce.Day,
            pos.Quantity, 0, 0, null,
            "PTT-Mirror-Close",               // signal name starts with "PTT-" ✅
            DateTime.MaxValue, null);
        StatusUpdate?.Invoke(acc.Name + ": mirror-close " + pos.Quantity);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-Mirror-Close error: " + ex.Message);
    }
}
```

### `MirrorBracketMove` — CYC=5

Reuses existing `HandleBracketChange` logic but called explicitly from `MirrorOrderUpdate`.
Rather than duplicating HandleBracketChange, `MirrorBracketMove` is a thin alias:

```csharp
private void MirrorBracketMove(Order masterOrder, CopyRule rule)  // CYC=5
{
    bool isStop  = IsStopLeg(masterOrder);                        // (1)
    var  instr   = masterOrder.Instrument;
    if (instr == null) return;                                     // (2)
    double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;    // (3)
    double rawPrice = isStop ? masterOrder.StopPrice : masterOrder.LimitPrice; // (4)
    double newPrice = tickSize > 0
        ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice;
    foreach (var acc in rule.FollowerAccounts)                    // loop (5)
    {
        if (acc == null) continue;
        var fo = FindFollowerBracketOrder(acc, masterOrder.FromEntrySignal, isStop);
        if (fo == null) continue;
        double cur = isStop ? fo.StopPrice : fo.LimitPrice;
        if (Math.Abs(newPrice - cur) < tickSize) continue;
        try { acc.Change(fo, fo.Quantity, fo.LimitPrice, fo.StopPrice); } catch { }
    }
}
```

Wait — this duplicates `HandleBracketChange`. The correct design is: Mirror mode calls `HandleBracketChange` directly from `MirrorOrderUpdate`. No new `MirrorBracketMove` needed. Update:

```csharp
private void MirrorOrderUpdate(Order masterOrder, CopyRule rule)   // CYC=3
{
    if (masterOrder == null) return;                                // guard (1)
    if (ShouldMirrorClose(masterOrder))                             // branch (2)
    {
        MirrorClose(masterOrder, rule);
        return;
    }
    if (IsWorkingBracket(masterOrder))                              // branch (3)
        HandleBracketChange(masterOrder, rule);  // reuse existing, no duplication
}
```

`MirrorBracketMove` is removed — `HandleBracketChange` is reused directly.

### Mode Selector UI

**Panel (`TradeCopierPanel.cs` — T3):**
New row in `BuildUI()`:
```
Row: "Copy Mode:" label  |  [Signal] [Mirror] RadioButton pair
```
- Two `RadioButton` controls: `_signalModeBtn`, `_mirrorModeBtn`.
- `OnSignalModeClick`: `CopyEngine.Instance.SetCopyMode(CopyMode.Signal)`.
- `OnMirrorModeClick`: `CopyEngine.Instance.SetCopyMode(CopyMode.Mirror)`.
- Each handler CYC=1.

**Window (`TradeCopierWindow.cs` — T3):**
Add a mode ComboBox to the header section:
```
Label: "Copy Mode"  |  ComboBox: ["Signal (default)", "Mirror"]
```
`OnCopyModeComboChanged`: CYC=1. Sets engine copy mode.

---

## Section 5: Named ATM Inline Template Input Design

### Scope

When "Named" is selected in the ATM ComboBox (per-follower in Panel, per-rule in Window), a `TextBox`
appears inline for the user to type the ATM template name directly, instead of relying on a pre-configured
name from a separate dialog.

### Panel (`TradeCopierPanel.cs` — T3)

In `BuildCheckItemTemplate()` (follower row template), after the ATM ComboBox:
```csharp
// Named ATM inline TextBox -- hidden by default
var namedBox = new TextBox { Width = 80, Visibility = Visibility.Collapsed,
    ToolTip = "ATM template name" };
namedBox.Tag = item;  // bind to FollowerItem for AtmModeName update
namedAtmCb.SelectionChanged += (s, e) =>
{
    var sel = namedAtmCb.SelectedItem as string ?? string.Empty;
    namedBox.Visibility = sel == "Named" ? Visibility.Visible : Visibility.Collapsed;
    if (sel != "Named") namedBox.Text = string.Empty;
};
namedBox.TextChanged += (s, e) =>
{
    if (namedBox.Tag is FollowerItem fi)
        fi.AtmModeName = namedBox.Text.Length > 0 ? "Named:" + namedBox.Text : "Inherit";
};
```

No new named methods needed — anonymous lambdas inside the template builder.
CYC of `BuildCheckItemTemplate` adds 2 branches (Named branch + text length branch) = still ≤ 8.

### Window (`TradeCopierWindow.cs` — T3)

In `BuildRuleRow()` and `BuildDynamicRuleRow()`, after the ATM ComboBox:
```csharp
// Named ATM inline TextBox
var namedBox = new TextBox { Width = 80, Visibility = Visibility.Collapsed };
atmCb.SelectionChanged += (s, e) =>
{
    var sel = atmCb.SelectedItem?.ToString() ?? string.Empty;
    namedBox.Visibility = sel == "Named" ? Visibility.Visible : Visibility.Collapsed;
};
```
`OnRowApply` reads `namedBox.Text` when ATM mode is "Named" and passes it as the template name suffix.
Modification to `OnRowApply`: if atmMode == "Named" and namedBox.Text.Length > 0, use "Named:" + namedBox.Text.
CYC of `OnRowApply` adds 1 branch — remains ≤ 8.

---

## Section 6: DW-B8-01 Cleanup Status (FindRule / FindPosition / FindFollowerBracketOrder)

After B9 analysis these methods are already compliant:

| Method | Return Type | Null handling | JS-002 Status |
|--------|-------------|---------------|---------------|
| `FindFollowerBracketOrder` | `Order?` (nullable reference type) | Callers guard with `if (fo == null) continue;` | **COMPLIANT** |
| `FindPosition` | `Position?` (nullable) | Callers guard with `if (pos == null \|\| pos.Quantity == 0)` | **COMPLIANT** |
| Inline rule match in `OnOrderUpdate` | `CopyRule?` (nullable struct) | `if (matchedRule == null) return;` | **COMPLIANT** |

**Action:** Close DW-B8-01. No code change. Document in deferred ledger as CLOSED.

---

## Section 7: File Impact Matrix

| File | T1 Added | T2 Added | T3 Added | Estimated B9 Lines | B8 Baseline |
|------|----------|----------|----------|--------------------|-------------|
| `AtrSizingEngine.cs` | **+150** (new) | — | — | ~150 | 0 (new) |
| `CopyEngine.cs` | +22 | — | +60 | ~1121 | 1039 |
| `TradeCopierAddOn.cs` | +28 | +30 | — | ~289 | ~231 |
| `TradeCopierPanel.cs` | — | +60 | +28 | ~631 | 543 |
| `TradeCopierWindow.cs` | — | — | +28 | ~588 | 560 |
| `CopyEngineTests.cs` | +80 | +40 | +55 | ~1066 | 891 |
| **Total** | **+280** | **+130** | **+171** | **~3845** | ~3264 |

_Line estimates are conservative. Actual counts may vary ±10%._

---

## Section 8: Ticket Boundaries

### T1 — ATR Sizing Engine

**Rationale:** Self-contained new file + engine integration. No UI. Independently buildable.
Zero dependency on T2 or T3.

**Files:**
- NEW: `AtrSizingEngine.cs` (~150 lines)
- MODIFY: `CopyEngine.cs` — `_atrEngine` field, `SetAtrEngine()`, `GetSuggestedQty(Instrument)`,
  `_atrEnabled` flag, DispatchCopy modification (ATR base qty before multiplier)
- MODIFY: `TradeCopierAddOn.cs` — `_atrEngines` dict, `StartAtrEngine()`, `StopAtrEngine()`,
  `DoInject` calls `StartAtrEngine`, `OnWindowDestroyed` calls `StopAtrEngine`
- MODIFY: `CopyEngineTests.cs` — T-B9-01 through T-B9-10 (10 tests)

**Build gate:** Compiles + 50 tests pass (40 B8 + 10 T1 new).

### T2 — Click Trader

**Rationale:** Chart mouse hook + Panel UI. Depends on T1 for `GetSuggestedQty()` (null-safe fallback
returns 1 if ATR not enabled). Independently verifiable after T1.

**Files:**
- MODIFY: `TradeCopierPanel.cs` — `SetChart()`, `_clickArmed`, `_clickBuy`, `_armBtn`,
  `_buyToggle`, `_sellToggle`, `BuildClickTraderRow()`, `OnArmClick()`, `OnChartMouseDown()`,
  `UpdateArmVisuals()`, `Detach()` extended to call `UnregisterClickTrader`
- MODIFY: `TradeCopierAddOn.cs` — `_clickHandlers` dict, `RegisterClickTrader()`,
  `UnregisterClickTrader()`, `DoInject` calls `panel.SetChart(chart)`,
  `OnWindowDestroyed` calls `UnregisterClickTrader(chart)`
- MODIFY: `CopyEngineTests.cs` — T-B9-11 through T-B9-14 (4 tests)

**Build gate:** Compiles + 54 tests pass (50 T1 + 4 T2 new).

### T3 — Mirror Mode + Named ATM Inline

**Rationale:** Independent of T1 and T2. Engine changes (new CopyMode enum) + UI additions
(mode selector + Named inline TextBox). No dependency on ATR or click trader.

**Files:**
- MODIFY: `CopyEngine.cs` — `CopyMode` enum, `_copyModeValue`, `SetCopyMode()`, `GetCopyMode()`,
  `MirrorOrderUpdate()`, `MirrorClose()`, `ShouldMirrorClose()`,
  `OnOrderUpdate` mirror branch (+1 CYC, now CYC=8)
- MODIFY: `TradeCopierPanel.cs` — `_signalModeBtn`, `_mirrorModeBtn` RadioButtons,
  `BuildModeRow()`, `OnSignalModeClick()`, `OnMirrorModeClick()`,
  Named ATM inline TextBox in `BuildCheckItemTemplate()`
- MODIFY: `TradeCopierWindow.cs` — mode ComboBox in header, `OnCopyModeComboChanged()`,
  Named ATM inline TextBox in `BuildRuleRow()` + `BuildDynamicRuleRow()`,
  `OnRowApply` +1 branch for Named text
- MODIFY: `CopyEngineTests.cs` — T-B9-15 through T-B9-20 (6 tests)

**Build gate:** Compiles + 60 tests pass (54 T2 + 6 T3 new).

---

## Section 9: New Tests Plan

Target: 60 [Fact] tests total (from 40 B8 baseline, +20 new).

### T1 Tests — AtrSizingEngine (10 new)

| ID | Test Name | What It Verifies |
|----|-----------|-----------------|
| T-B9-01 | `CalcContracts_MES_ATR6_returns5` | floor(150 / (6*5)) = 5 ✅ |
| T-B9-02 | `CalcContracts_MES_ATR8_returns3` | floor(150 / (8*5)) = 3 ✅ |
| T-B9-03 | `CalcContracts_MES_ATR12_returns2` | floor(150 / (12*5)) = 2 ✅ |
| T-B9-04 | `CalcContracts_ZeroAtr_returns1` | atr=0 guard → 1 ✅ |
| T-B9-05 | `CalcContracts_NegativeAtr_returns1` | atr=-3 guard → 1 ✅ |
| T-B9-06 | `CalcContracts_ResultBelowOne_clampsTo1` | floor(5/10)=0 → clamp to 1 ✅ |
| T-B9-07 | `CalcContracts_ZeroTickValue_returns1` | tickDollarValue=0 guard → 1 ✅ |
| T-B9-08 | `CalcContracts_LargeMaxRisk_noOverflow` | floor(10000/5)=2000, no overflow ✅ |
| T-B9-09 | `GetSuggestedQty_returns1_when_no_engine` | `CopyEngine.GetSuggestedQty(null engine)=1` ✅ |
| T-B9-10 | `GetSuggestedQty_returns_engine_qty_when_set` | engine mock with _lastContracts=3 → returns 3 ✅ |

Note: T-B9-01 through T-B9-08 call `AtrSizingEngine.CalcContracts(...)` directly — it is a static
`internal` method, no NT8 context required. T-B9-09/10 test the CopyEngine integration layer.

### T2 Tests — Click Trader (4 new)

| ID | Test Name | What It Verifies |
|----|-----------|-----------------|
| T-B9-11 | `ClickTrader_signalName_starts_PTT` | Verify "PTT-Click" starts with "PTT-" (static string check) ✅ |
| T-B9-12 | `ClickTrader_atr_disabled_fallback_qty_is_1` | `GetSuggestedQty` returns 1 when atr disabled ✅ |
| T-B9-13 | `ClickTrader_atr_enabled_uses_engine_qty` | `GetSuggestedQty` returns engine value when enabled ✅ |
| T-B9-14 | `ClickTrader_mirrorClose_signalName_starts_PTT` | "PTT-Mirror-Close" starts with "PTT-" ✅ |

### T3 Tests — Mirror Mode (6 new)

| ID | Test Name | What It Verifies |
|----|-----------|-----------------|
| T-B9-15 | `SetCopyMode_Signal_roundtrips` | SetCopyMode(Signal) → GetCopyMode() == Signal ✅ |
| T-B9-16 | `SetCopyMode_Mirror_roundtrips` | SetCopyMode(Mirror) → GetCopyMode() == Mirror ✅ |
| T-B9-17 | `DefaultCopyMode_is_Signal` | fresh CopyEngine → GetCopyMode() == Signal ✅ |
| T-B9-18 | `ShouldMirrorClose_true_when_bracket_filled` | `Filled` + `IsBracketLeg` → true ✅ |
| T-B9-19 | `ShouldMirrorClose_false_when_not_bracket` | `Filled` + not bracket leg → false ✅ |
| T-B9-20 | `ShouldMirrorClose_false_when_working` | `Working` state → false ✅ |

**Final test count: 40 (B8 baseline) + 10 (T1) + 4 (T2) + 6 (T3) = 60 [Fact] tests** ✅

---

## Section 10: 7-Scan Requirements

All 7 scans must return ZERO in B9 scope. New patterns introduced by B9 tracked here.

| Scan | Pattern | B9 New Risk | Mitigation |
|------|---------|-------------|------------|
| SCAN-01 | `lock\s*\(` in executable code | `volatile` fields in AtrSizingEngine — never lock | Use only `volatile int/double/bool` + ConcurrentDictionary ✅ |
| SCAN-02 | `throw new` in hot paths | `MirrorClose`, `OnChartMouseDown`, `MirrorOrderUpdate` | All order API calls wrapped in `try/catch { StatusUpdate?.Invoke(...) }`, no throw escapes ✅ |
| SCAN-03 | `return null` in new B9 methods | `CalcContracts` returns `int`, `GetSuggestedQty` returns `int`, `MirrorClose` returns `void` | All new methods use value types or void — null impossible ✅ |
| SCAN-04 | `new Dictionary<` mutable | `_atrEngines` and `_clickHandlers` in AddOn | Both use `ConcurrentDictionary<Chart, T>` ✅ |
| SCAN-05 | `DateTime\.Now[^U]` | None in ATR, click trader, mirror mode | No timestamps needed. DateTime.MaxValue (order expiry) does not match pattern ✅ |
| SCAN-06 | `async void` | No new async methods | All new event handlers: sync `void`. `OnBarUpdate`: sync NT8 override ✅ |
| SCAN-07 | `#[0-9A-Fa-f]{6}` hex in string literals | Green armed border color | `MakeBrush(34, 197, 94)` — RGB decimal only, no hex strings ✅ |

**Additional B9-specific checks:**

| Check | Expected | Enforcement |
|-------|----------|-------------|
| `CreateOrder` signal names start "PTT-" | "PTT-Click", "PTT-Mirror-Close" | Both hardcoded string literals ✅ |
| `AtrSizingEngine` class NOT sealed | Not sealed — NT8 may need to subclass | `public class AtrSizingEngine : Indicator` (no sealed) ✅ |
| `volatile` on all cross-thread fields in AtrSizingEngine | `_lastContracts`, `_lastAtr`, `_hasData` | All `volatile` ✅ |
| No `FontFamily` override | None added | ZERO matches expected ✅ |
| `SolidColorBrush.Freeze()` | Armed border brush | Via `MakeBrush(r,g,b)` which calls `Freeze()` ✅ |

---

## Section 11: Component List Summary

| Component | File | New/Modified | Lines (est.) |
|-----------|------|-------------|-------------|
| `AtrSizingEngine` class | `AtrSizingEngine.cs` | NEW | ~150 |
| ATR integration in `CopyEngine` | `CopyEngine.cs` | Modified | +22 |
| Mirror Mode in `CopyEngine` | `CopyEngine.cs` | Modified | +60 |
| ATR engine lifecycle in `AddOn` | `TradeCopierAddOn.cs` | Modified | +28 |
| Click trader registration in `AddOn` | `TradeCopierAddOn.cs` | Modified | +30 |
| Click trader UI in `Panel` | `TradeCopierPanel.cs` | Modified | +60 |
| Mirror mode + Named ATM in `Panel` | `TradeCopierPanel.cs` | Modified | +28 |
| Named ATM + mode in `Window` | `TradeCopierWindow.cs` | Modified | +28 |
| 20 new [Fact] tests | `CopyEngineTests.cs` | Modified | +175 |

---

## Section 12: Deferred Backlog Update (PTT-COPIER-B9)

Items carried forward or resolved in B9:

| ID | Item | B9 Decision | Target |
|----|------|-------------|--------|
| DW-B7-02 / DW-B8-03 | ATR dynamic sizing engine | **IN SCOPE — T1** | B9 |
| DW-B8-04 | Click trader | **IN SCOPE — T2** | B9 |
| DW-B8-06 | Mirror Mode | **IN SCOPE — T3** | B9 |
| SPEC-2354 | Named ATM inline input | **IN SCOPE — T3** | B9 |
| DW-B8-01 | return null cleanup | **CLOSED** — already compliant | — |
| DW-B8-02 | Gate hook path fix | **OUT OF SCOPE** — non-source | — |
| DW-B8-05 | ATR box visualization | **DEFERRED B10** | B10 |

New deferred items from B9 analysis:

| ID | Item | Priority | Target |
|----|------|----------|--------|
| DW-B9-01 | ATR box visualization on chart (draw stop/target zone around click-placed order) | P2 | B10 |
| DW-B9-02 | IMPL-NOTE-1 resolution: document verified chart attachment API for AtrSizingEngine in B9 ticket completion report | P1 | B9-T1 |
| DW-B9-03 | Bid+1 / Ask-1 offset for click trader (auto-adjust limit price to inside market) | P3 | B10 |

---

## JS Rule Compliance Summary

| Rule | B9 Application | Status |
|------|----------------|--------|
| JS-021 (no lock) | AtrSizingEngine: volatile fields only. AddOn: ConcurrentDictionary. No lock anywhere. | ✅ ZERO violations |
| JS-001 (no throw in hot path) | All NT8 API calls in try/catch. No throw escapes from DispatchCopy, OnOrderUpdate, MirrorClose, OnChartMouseDown. | ✅ ZERO violations |
| JS-002 (no return null for ref types) | All new methods return value types (int, void) or use nullable with caller guards. | ✅ ZERO violations |
| JS-010 (private constructor) | AtrSizingEngine: public constructor required by NT8. Not applicable. All domain types without NT8 lifecycle use private constructors. | ✅ N/A for NT8 Indicator |
| JS-023 (volatile bool for flags) | `_clickArmed`, `_clickBuy`, `_atrEnabled`, `_hasData`, `_copyModeValue`, `_lastContracts`, `_lastAtr` — all volatile. | ✅ COMPLIANT |
| JS-025 (ConcurrentDictionary for dedup) | `_atrEngines`, `_clickHandlers` both `ConcurrentDictionary`. | ✅ COMPLIANT |
| JS-033 (no async void) | All new handlers synchronous void. | ✅ ZERO violations |
| JS-008 (Freeze() all brushes) | Armed state border brush via `MakeBrush(r,g,b)` which calls `Freeze()`. | ✅ COMPLIANT |
| CYC ≤ 8 | All methods verified: max is `OnOrderUpdate` at CYC=8 (at limit, no violation). | ✅ ALL PASS |

---

**Status: PLAN_COMPLETE**
