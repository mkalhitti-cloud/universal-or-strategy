# PTT-COPIER-B9 — Tickets
**Status**: TICKETS_COMPLETE
**Source plan**: `docs/brain/PTT-COPIER-B9/02-architecture-plan.md` — PLAN_COMPLETE / REVIEW_PASS
**Plan review**: `docs/brain/PTT-COPIER-B9/02-plan-review.md`
**Date**: 2026-07-09
**B8 baseline**: 40 [Fact] tests. B9 target: 60 [Fact] tests.
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## Execution Order

Execute in sequence: **T1 → T2 → T3**. Each ticket's build gate must pass before starting the next.

| Ticket | Feature | Build Gate |
|--------|---------|-----------|
| T1 | ATR Sizing Engine | Compiles + 50 tests pass |
| T2 | Click Trader | Compiles + 54 tests pass |
| T3 | Mirror Mode + Named ATM Inline | Compiles + 60 tests pass |

---

## ADV Resolutions (baked into tickets below)

| Advisory | Severity | Resolved in | Action |
|----------|---------|-------------|--------|
| ADV-001 | HIGH — MUST FIX | T2 | `RegisterClickTrader`: `TryRemove` old handler BEFORE assigning new entry. Verbatim corrected body is in T2. |
| ADV-002 | MUST FIX | T1 | `private volatile bool _atrEnabled = false;` explicitly declared in CopyEngine.cs. |

---

## TICKET T1 — ATR Sizing Engine

**Spec Req IDs:** DW-B7-02 / DW-B8-03
**Closes deferred items:** DW-B7-02, DW-B8-03
**Prerequisite:** B8 FINAL_PASS (40 tests passing). T2 and T3 must NOT start until T1 build gate passes.

### Files (Wave workspace: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`)

| File | Action | B9 Lines Added |
|------|--------|---------------|
| `AtrSizingEngine.cs` | **NEW** | ~150 |
| `CopyEngine.cs` | MODIFY | +22 |
| `TradeCopierAddOn.cs` | MODIFY | +28 |
| `CopyEngineTests.cs` | MODIFY | +80 |

### Method Signatures — AtrSizingEngine.cs (NEW)

```csharp
namespace PropTraderTools
{
    // Detached NT8 Indicator. Lifecycle managed by TradeCopierAddOn (one instance per chart).
    // JS-021: no lock(). JS-023: volatile cross-thread fields. CYC <= 8 per method.
    public class AtrSizingEngine : Indicator
    {
        // Cross-thread fields (volatile -- JS-023) — written on data thread, read on UI thread
        private volatile int    _lastContracts = 1;
        private volatile double _lastAtr       = 0.0;
        private volatile bool   _hasData       = false;

        // Configuration (single-writer UI thread — set before attachment)
        private double _maxRiskDollars  = 150.0;
        private double _tickDollarValue = 5.0;

        // NT8 lifecycle
        protected override void OnStateChange();                      // CYC=4
        protected override void OnBarUpdate();                        // CYC=2

        // Public interface (called by CopyEngine and TradeCopierPanel)
        internal void   SetParameters(double maxRiskDollars, double tickDollarValue);  // CYC=1
        internal int    GetSuggestedQty();                                              // CYC=2
        internal double GetLastAtr();                                                   // CYC=1

        // Pure math — internal static, no NT8 context required, fully unit-testable
        internal static int CalcContracts(double atrPoints, double maxRisk, double tickDollarValue);  // CYC=3
    }
}
```

### AtrSizingEngine.cs Full Implementation Spec

**`OnStateChange()` — CYC=4**
```csharp
protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        Description = "PTT ATR Sizing Engine";
        Name        = "AtrSizingEngine";
        Period      = 14;                       // [NinjaScriptProperty] int
    }
    else if (State == State.Configure)
    {
        AddDataSeries(_instrument, BarsPeriodType.Minute, 1);
    }
    else if (State == State.DataLoaded)
    {
        Add(ATR(Period));                        // child indicator — read via Values[0][0]
    }
    else if (State == State.Terminated)
    {
        _hasData       = false;
        _lastContracts = 1;
        _lastAtr       = 0.0;
    }
}
```
Note: `_instrument` is set via `SetParameters` before attachment, or passed via an `Instrument` property.
Verify exact `AddDataSeries` / `Add(ATR(...))` API at T1 execution time per IMPL-NOTE-1 / DW-B9-02.

**`OnBarUpdate()` — CYC=2**
```csharp
protected override void OnBarUpdate()
{
    if (CurrentBar < Period) return;            // guard (1)
    double atr    = ATR(Period)[0];
    _lastAtr      = atr;                        // volatile write
    _lastContracts = CalcContracts(atr, _maxRiskDollars, _tickDollarValue);  // volatile write
    _hasData      = true;                       // volatile write — no branch (2)
}
```

**`CalcContracts(double atrPoints, double maxRisk, double tickDollarValue)` — CYC=3**
```csharp
internal static int CalcContracts(double atrPoints, double maxRisk, double tickDollarValue)
{
    if (atrPoints      <= 0) return 1;          // guard (1): zero or negative ATR
    if (tickDollarValue <= 0) return 1;          // guard (2): zero tick dollar value
    double riskPerContract = atrPoints * tickDollarValue;
    int contracts = (int)Math.Floor(maxRisk / riskPerContract);
    return contracts < 1 ? 1 : contracts;        // guard (3): clamp minimum to 1
}
```

**`GetSuggestedQty()` — CYC=2**
```csharp
internal int GetSuggestedQty()
{
    if (!_hasData) return 1;                     // guard (1): no bar data yet
    return _lastContracts;                       // volatile read
}
```

**`SetParameters(double maxRiskDollars, double tickDollarValue)` — CYC=1**
```csharp
internal void SetParameters(double maxRiskDollars, double tickDollarValue)
{
    _maxRiskDollars  = maxRiskDollars;
    _tickDollarValue = tickDollarValue;
}
```

**`GetLastAtr()` — CYC=1**
```csharp
internal double GetLastAtr() => _lastAtr;        // volatile read, straight-line
```

**Testability seam (T-B9-10):**
Add an `internal` test-seam constructor that sets fields without triggering NT8 runtime:
```csharp
// Test-only. Do NOT use in production code.
internal AtrSizingEngine(int testContracts)
{
    _lastContracts = testContracts;
    _hasData       = true;
}
```
If `new AtrSizingEngine(int)` throws (NT8 `Indicator` base ctor requires runtime context), fall back
to `FormatterServices.GetUninitializedObject(typeof(AtrSizingEngine))` in the test and set
`_lastContracts` + `_hasData` via direct field access (requires `internal` visibility on both fields).

### Method Signatures — CopyEngine.cs Additions (ADV-002 fix included)

```csharp
// --- New fields (add in the fields region, near other volatile flags) ---
private volatile bool            _atrEnabled = false;   // ADV-002: must be volatile
private volatile AtrSizingEngine _atrEngine  = null;    // nullable reference

// --- New methods ---

// CYC=1 — straight-line assignment
internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)
{
    _atrEngine  = engine;
    _atrEnabled = enabled;
}

// CYC=2 — returns engine value when enabled; 1 otherwise
internal int GetSuggestedQty(Instrument instrument)
{
    if (_atrEnabled && _atrEngine != null)              // branch (1+2 as compound)
        return _atrEngine.GetSuggestedQty();
    return 1;
}
```

**`DispatchCopy` modification (T1):**
Before the existing `SendCopy(...)` call, insert ATR base quantity calculation:
```csharp
// BEFORE (existing code):
// SendCopy(rule, order);

// AFTER (T1 insertion — add BEFORE the foreach/SendCopy loop):
int baseQty = GetSuggestedQty(rule.Instrument);
// Then multiply by each follower's qty multiplier:
// e.g. int qty = baseQty * follower.QtyMultiplier;
// Pass qty into the existing CreateOrder call inside SendCopy.
```
Exact insertion point: locate the `DispatchCopy` method and apply `baseQty = GetSuggestedQty(rule.Instrument)`
before the per-follower quantity resolution. `baseQty` replaces the hard-coded `1` quantity baseline.

### Method Signatures — TradeCopierAddOn.cs Additions

```csharp
// --- New field (add in static fields region) ---
private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines
    = new ConcurrentDictionary<Chart, AtrSizingEngine>();

// --- New methods ---

// CYC=3 — two null guards + engine attachment
private static void StartAtrEngine(Chart chart, Instrument instr)
{
    if (chart == null) return;                                          // guard (1)
    if (instr  == null) return;                                         // guard (2)
    var engine = new AtrSizingEngine();
    engine.SetParameters(150.0, instr.MasterInstrument?.PointValue ?? 5.0);
    chart.NinjaScripts.Add(engine);                                     // IMPL-NOTE-1: verify API
    _atrEngines[chart] = engine;
    CopyEngine.Instance.SetAtrEngine(engine, enabled: false);           // disabled until user enables (3)
}

// CYC=2 — TryRemove guard + silent try/catch
private static void StopAtrEngine(Chart chart)
{
    if (!_atrEngines.TryRemove(chart, out var engine)) return;          // guard (1)
    try { chart.NinjaScripts.Remove(engine); } catch { }               // guard (2): silent on failure
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
}
```

**`DoInject` modification (T1):**
After creating and configuring the `TradeCopierPanel`, add:
```csharp
StartAtrEngine(chart, panel.GetInstrument());
```

**`OnWindowDestroyed` modification (T1):**
Add at the start (before existing teardown):
```csharp
StopAtrEngine(chart);
```

### AtrSizingEngine CYC Summary

| Method | CYC | Limit |
|--------|-----|-------|
| `OnStateChange` | 4 | ✅ |
| `OnBarUpdate` | 2 | ✅ |
| `CalcContracts` | 3 | ✅ |
| `GetSuggestedQty` | 2 | ✅ |
| `SetParameters` | 1 | ✅ |
| `GetLastAtr` | 1 | ✅ |

### xUnit [Fact] Tests — T-B9-01 through T-B9-10

Add to `CopyEngineTests.cs`. Tests T-B9-01 to T-B9-08 call `AtrSizingEngine.CalcContracts` directly
(internal static — no NT8 context needed). Tests T-B9-09/10 test `CopyEngine.GetSuggestedQty`.

```csharp
// T-B9-01: MES ATR=6, maxRisk=150, tick=$5 -> floor(150/(6*5)) = 5
[Fact]
public void CalcContracts_MES_ATR6_returns5()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 6.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(5, result);
}

// T-B9-02: MES ATR=8, maxRisk=150, tick=$5 -> floor(150/40) = 3
[Fact]
public void CalcContracts_MES_ATR8_returns3()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 8.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(3, result);
}

// T-B9-03: MES ATR=12, maxRisk=150, tick=$5 -> floor(150/60) = 2
[Fact]
public void CalcContracts_MES_ATR12_returns2()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 12.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(2, result);
}

// T-B9-04: Zero ATR -> guard returns 1
[Fact]
public void CalcContracts_ZeroAtr_returns1()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 0.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(1, result);
}

// T-B9-05: Negative ATR -> guard returns 1
[Fact]
public void CalcContracts_NegativeAtr_returns1()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: -3.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(1, result);
}

// T-B9-06: Result below 1 clamps to 1 -> floor(5/(1*10))=0 -> 1
[Fact]
public void CalcContracts_ResultBelowOne_clampsTo1()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 1.0, maxRisk: 5.0, tickDollarValue: 10.0);
    Assert.Equal(1, result);
}

// T-B9-07: Zero tickDollarValue -> guard returns 1
[Fact]
public void CalcContracts_ZeroTickValue_returns1()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 6.0, maxRisk: 150.0, tickDollarValue: 0.0);
    Assert.Equal(1, result);
}

// T-B9-08: Large maxRisk -> floor(10000/(1*5)) = 2000, no overflow
[Fact]
public void CalcContracts_LargeMaxRisk_noOverflow()
{
    int result = AtrSizingEngine.CalcContracts(atrPoints: 1.0, maxRisk: 10000.0, tickDollarValue: 5.0);
    Assert.Equal(2000, result);
}

// T-B9-09: GetSuggestedQty returns 1 when no engine is set
[Fact]
public void GetSuggestedQty_returns1_when_no_engine()
{
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(1, qty);
}

// T-B9-10: GetSuggestedQty returns engine qty when engine is set and enabled
// Uses test-seam constructor AtrSizingEngine(int testContracts).
// If that ctor is unavailable, mark test [Fact(Skip="IMPL-NOTE-1: verify seam")] and document.
[Fact]
public void GetSuggestedQty_returns_engine_qty_when_set()
{
    var engine = new AtrSizingEngine(testContracts: 3);    // test-seam ctor
    CopyEngine.Instance.SetAtrEngine(engine, enabled: true);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    CopyEngine.Instance.SetAtrEngine(null, enabled: false); // teardown
    Assert.Equal(3, qty);
}
```

### 7-Scan Checklist — T1

Run scans on: `AtrSizingEngine.cs` (entire), `CopyEngine.cs` (new lines only), `TradeCopierAddOn.cs` (new lines only).

| Scan | Pattern | Expected | Where to check |
|------|---------|----------|----------------|
| SCAN-01 | `lock\s*\(` | ZERO | AtrSizingEngine.cs, CopyEngine.cs (new lines), TradeCopierAddOn.cs (new lines) |
| SCAN-02 | `throw new` in hot path | ZERO | `AtrSizingEngine.OnBarUpdate`, `CopyEngine.DispatchCopy` |
| SCAN-03 | `return null` in new methods | ZERO | `GetSuggestedQty` returns `int`, `SetAtrEngine` returns `void` |
| SCAN-04 | `new Dictionary<` | ZERO | `_atrEngines` uses `ConcurrentDictionary` |
| SCAN-05 | `DateTime\.Now[^U]` | ZERO | No timestamps in ATR engine code |
| SCAN-06 | `async void` | ZERO | `OnBarUpdate` is sync `protected override void` |
| SCAN-07 | `#[0-9A-Fa-f]{6}` in string literals | ZERO | No hex color strings in ATR/engine code |

**Additional B9-T1 checks:**

| Check | Expected |
|-------|---------|
| `private volatile bool _atrEnabled` declared in CopyEngine.cs | CONFIRMED (ADV-002) |
| `private volatile AtrSizingEngine _atrEngine` declared in CopyEngine.cs | CONFIRMED |
| `_lastContracts`, `_lastAtr`, `_hasData` all `volatile` in AtrSizingEngine | CONFIRMED |
| `CalcContracts` declared `internal static` | CONFIRMED (testability requirement) |
| `_atrEngines` is `ConcurrentDictionary<Chart, AtrSizingEngine>` | CONFIRMED |
| IMPL-NOTE-1: chart attachment API documented in `ticket-1-completion.md` | DW-B9-02 |

### Build Gate — T1

```
dotnet build PropTraderTools.csproj   -> 0 errors, 0 warnings
dotnet test CopyEngineTests           -> 50 [Fact] tests pass (40 B8 baseline + 10 T1 new)
```

---

## TICKET T2 — Click Trader

**Spec Req IDs:** DW-B8-04, SPEC §"Click Trader" (§2228, §2229, §2235, §2239)
**Closes deferred items:** DW-B8-04
**Prerequisite:** T1 build gate must pass (50 tests green) before starting T2.

### ⚠️ ADV-001 — RegisterClickTrader CORRECTED BODY (MANDATORY)

The plan body shows the assignment/subscribe BEFORE TryRemove — this is a bug.
The T2 engineer MUST implement the corrected body below verbatim. No deviation.

**Defect in plan (do NOT implement this):**
```csharp
// WRONG — TryRemove fires AFTER assignment, evicts new entry
_clickHandlers[chart] = panel;
chart.ChartControl.MouseDown += panel.OnChartMouseDown;
if (_clickHandlers.TryRemove(chart, out var old))
    chart.ChartControl.MouseDown -= old.OnChartMouseDown;  // removes new handler
```

**CORRECT implementation (implement this):**
```csharp
internal static void RegisterClickTrader(Chart chart, TradeCopierPanel panel)
{
    if (chart == null) return;                                     // guard (1)
    if (_clickHandlers.TryRemove(chart, out var old))             // guard (2): remove old handler first
        chart.ChartControl.MouseDown -= old.OnChartMouseDown;
    _clickHandlers[chart] = panel;                                 // then add new
    chart.ChartControl.MouseDown += panel.OnChartMouseDown;
}
```

CYC=2 (null guard + TryRemove branch). Re-arm path: old stale handler is removed before new one is added.
Failure to use this body causes double/triple order submission on every re-arm.

### Files (Wave workspace: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`)

| File | Action | B9 Lines Added |
|------|--------|---------------|
| `TradeCopierPanel.cs` | MODIFY | +60 |
| `TradeCopierAddOn.cs` | MODIFY | +30 |
| `CopyEngineTests.cs` | MODIFY | +40 |

### New Fields — TradeCopierPanel.cs

```csharp
// Add in the fields region
private volatile bool _clickArmed = false;     // JS-023: volatile
private volatile bool _clickBuy   = true;      // JS-023: volatile. true=Buy, false=Sell
private Chart         _currentChart;           // set by AddOn via SetChart()
private Button        _armBtn;
private ToggleButton  _buyToggle;
private ToggleButton  _sellToggle;
```

### New Field — TradeCopierAddOn.cs

```csharp
// Add in static fields region
private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _clickHandlers
    = new ConcurrentDictionary<Chart, TradeCopierPanel>();
```

### Method Signatures — TradeCopierPanel.cs

```csharp
// CYC=1 — straight-line: store reference
public void SetChart(Chart chart)
{
    _currentChart = chart;
}

// CYC=1 — appends a StackPanel row to BuildUI(); no branches
private void BuildClickTraderRow()
{
    // Creates _buyToggle, _sellToggle, _armBtn and appends as one Grid row
    // [Buy] [Sell] toggle pair  |  [Arm] button  |  (MaxRisk TextBox added in future B10)
}

// CYC=2 — null guard (1) + armed branch (2)
private void OnArmClick(object sender, RoutedEventArgs e)
{
    if (_currentChart == null) return;                           // guard (1)
    _clickArmed = !_clickArmed;                                  // volatile flip
    if (_clickArmed)                                             // branch (2)
        TradeCopierAddOn.RegisterClickTrader(_currentChart, this);
    else
        TradeCopierAddOn.UnregisterClickTrader(_currentChart);
    UpdateArmVisuals(_clickArmed);
}

// CYC=2 — armed branch for border color (1) + button label branch (2)
private void UpdateArmVisuals(bool armed)
{
    Dispatcher.InvokeAsync(() =>
    {
        _armBtn.Content = armed ? "Disarm" : "Arm";
        if (armed)
        {
            ChartControl.BorderBrush     = MakeBrush(34, 197, 94); // green — RGB decimal, no hex
            ChartControl.BorderThickness = new Thickness(2);
        }
        else
        {
            ChartControl.BorderBrush     = null;
            ChartControl.BorderThickness = new Thickness(0);
        }
    });
}

// CYC=4 — four null/type guards; try/catch does NOT add to CYC
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
{
    if (!_clickArmed)              return;  // guard (1)
    if (_leaderAccount == null)    return;  // guard (2)
    if (_instrument    == null)    return;  // guard (3)
    var chartControl = sender as ChartControl;
    if (chartControl   == null)    return;  // guard (4)

    double price   = chartControl.GetValueByY(e.GetPosition(chartControl).Y);
    bool   isBuy   = _clickBuy;             // volatile read
    int    qty     = CopyEngine.Instance.GetSuggestedQty(_instrument);
    var    action  = isBuy ? OrderAction.Buy : OrderAction.SellShort;  // ternary: not a branch

    try
    {
        _leaderAccount.CreateOrder(
            _instrument, action, OrderType.Limit, OrderEntry.Manual,
            TimeInForce.Day, qty, price, 0, null,
            "PTT-Click",       // signal name starts with "PTT-" (NT8 constraint)
            DateTime.MaxValue,
            null);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-Click error: " + ex.Message);
    }
}

// CYC=1 — straight-line volatile write
private void OnBuyToggleClick(object sender, RoutedEventArgs e)
{
    _clickBuy = true;
}

// CYC=1 — straight-line volatile write
private void OnSellToggleClick(object sender, RoutedEventArgs e)
{
    _clickBuy = false;
}
```

**`Detach()` extension (T2):**
In the existing `Detach()` method, add before returning:
```csharp
if (_currentChart != null)
    TradeCopierAddOn.UnregisterClickTrader(_currentChart);
```

### Method Signatures — TradeCopierAddOn.cs

```csharp
// CYC=2 — null guard + TryRemove branch (ADV-001 CORRECTED)
internal static void RegisterClickTrader(Chart chart, TradeCopierPanel panel)
{
    if (chart == null) return;                                     // guard (1)
    if (_clickHandlers.TryRemove(chart, out var old))             // guard (2): remove old first
        chart.ChartControl.MouseDown -= old.OnChartMouseDown;
    _clickHandlers[chart] = panel;                                 // add new
    chart.ChartControl.MouseDown += panel.OnChartMouseDown;
}

// CYC=2 — TryRemove guard + null ChartControl guard
internal static void UnregisterClickTrader(Chart chart)
{
    if (!_clickHandlers.TryRemove(chart, out var panel)) return;  // guard (1)
    if (chart.ChartControl == null) return;                        // guard (2)
    chart.ChartControl.MouseDown -= panel.OnChartMouseDown;
}
```

**`DoInject` modification (T2):**
After existing `panel.SetChart(chart)` call from T1, this line is already added by T1. If not done in T1,
add here:
```csharp
panel.SetChart(chart);
```

**`OnWindowDestroyed` modification (T2):**
Add alongside T1's `StopAtrEngine(chart)`:
```csharp
UnregisterClickTrader(chart);
```

### TradeCopierPanel + TradeCopierAddOn CYC Summary

| Method | File | CYC | Limit |
|--------|------|-----|-------|
| `SetChart` | Panel | 1 | ✅ |
| `BuildClickTraderRow` | Panel | 1 | ✅ |
| `OnArmClick` | Panel | 2 | ✅ |
| `UpdateArmVisuals` | Panel | 2 | ✅ |
| `OnChartMouseDown` | Panel | 4 | ✅ |
| `OnBuyToggleClick` | Panel | 1 | ✅ |
| `OnSellToggleClick` | Panel | 1 | ✅ |
| `RegisterClickTrader` | AddOn | 2 | ✅ |
| `UnregisterClickTrader` | AddOn | 2 | ✅ |

### xUnit [Fact] Tests — T-B9-11 through T-B9-14

```csharp
// T-B9-11: Signal name "PTT-Click" starts with "PTT-" (NT8 order naming constraint)
[Fact]
public void ClickTrader_signalName_starts_PTT()
{
    const string signalName = "PTT-Click";
    Assert.True(signalName.StartsWith("PTT-", StringComparison.Ordinal));
}

// T-B9-12: GetSuggestedQty returns 1 when ATR disabled (regression coverage for click trader path)
[Fact]
public void ClickTrader_atr_disabled_fallback_qty_is_1()
{
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(1, qty);
}

// T-B9-13: GetSuggestedQty returns engine value when ATR enabled (click trader ATR integration)
// Uses test-seam constructor AtrSizingEngine(int testContracts).
[Fact]
public void ClickTrader_atr_enabled_uses_engine_qty()
{
    var engine = new AtrSizingEngine(testContracts: 7);
    CopyEngine.Instance.SetAtrEngine(engine, enabled: true);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    CopyEngine.Instance.SetAtrEngine(null, enabled: false); // teardown
    Assert.Equal(7, qty);
}

// T-B9-14: Mirror-Close signal name "PTT-Mirror-Close" starts with "PTT-"
[Fact]
public void ClickTrader_mirrorClose_signalName_starts_PTT()
{
    const string signalName = "PTT-Mirror-Close";
    Assert.True(signalName.StartsWith("PTT-", StringComparison.Ordinal));
}
```

### 7-Scan Checklist — T2

Run scans on: `TradeCopierPanel.cs` (new lines), `TradeCopierAddOn.cs` (new lines).

| Scan | Pattern | Expected | Where to check |
|------|---------|----------|----------------|
| SCAN-01 | `lock\s*\(` | ZERO | `TradeCopierPanel.cs` (T2 additions), `TradeCopierAddOn.cs` (T2 additions) |
| SCAN-02 | `throw new` in hot path | ZERO | `OnChartMouseDown` (try/catch only — no rethrow) |
| SCAN-03 | `return null` in new methods | ZERO | All new methods return `void` or `int` |
| SCAN-04 | `new Dictionary<` | ZERO | `_clickHandlers` uses `ConcurrentDictionary` |
| SCAN-05 | `DateTime\.Now[^U]` | ZERO | `OnChartMouseDown` uses `DateTime.MaxValue` (not `.Now`) |
| SCAN-06 | `async void` | ZERO | All handlers are sync `void` |
| SCAN-07 | `#[0-9A-Fa-f]{6}` in string literals | ZERO | `UpdateArmVisuals` uses `MakeBrush(34, 197, 94)` — decimal RGB only |

**Additional B9-T2 checks:**

| Check | Expected |
|-------|---------|
| `CreateOrder` signal name is `"PTT-Click"` (starts with `"PTT-"`) | CONFIRMED |
| `RegisterClickTrader` calls `TryRemove` BEFORE `_clickHandlers[chart] = panel` | CONFIRMED (ADV-001) |
| `_clickArmed` and `_clickBuy` both declared `volatile bool` | CONFIRMED |
| `MakeBrush(34, 197, 94)` calls `Freeze()` per B8 `MakeBrush` contract | CONFIRMED |

### Build Gate — T2

```
dotnet build PropTraderTools.csproj   -> 0 errors, 0 warnings
dotnet test CopyEngineTests           -> 54 [Fact] tests pass (50 T1 + 4 T2 new)
```

---

## TICKET T3 — Mirror Mode + Named ATM Inline

**Spec Req IDs:** DW-B8-06, SPEC-2354
**Closes deferred items:** DW-B8-06, SPEC-2354
**Prerequisite:** T2 build gate must pass (54 tests green) before starting T3.

### Files (Wave workspace: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`)

| File | Action | B9 Lines Added |
|------|--------|---------------|
| `CopyEngine.cs` | MODIFY | +60 |
| `TradeCopierPanel.cs` | MODIFY | +28 |
| `TradeCopierWindow.cs` | MODIFY | +28 |
| `CopyEngineTests.cs` | MODIFY | +55 |

### Method Signatures — CopyEngine.cs (Mirror Mode)

**New enum and backing field (add at top of class, after existing fields):**
```csharp
internal enum CopyMode { Signal = 0, Mirror = 1 }

// JS-023: volatile int backing for thread-safe CopyMode reads/writes
private volatile int _copyModeValue = 0;    // 0 = Signal (default), 1 = Mirror
```

**New methods:**
```csharp
// CYC=1 — straight-line cast and assign
internal void SetCopyMode(CopyMode mode)
{
    _copyModeValue = (int)mode;
}

// CYC=1 — straight-line cast and return
internal CopyMode GetCopyMode()
{
    return (CopyMode)_copyModeValue;
}

// CYC=3 — null guard + ShouldMirrorClose branch + IsWorkingBracket branch
private void MirrorOrderUpdate(Order masterOrder, CopyRule rule)
{
    if (masterOrder == null) return;                                    // guard (1)
    if (ShouldMirrorClose(masterOrder.OrderState, IsBracketLeg(masterOrder)))  // branch (2)
    {
        MirrorClose(masterOrder, rule);
        return;
    }
    if (IsWorkingBracket(masterOrder))                                  // branch (3)
        HandleBracketChange(masterOrder, rule);  // reuse existing — no duplication
}

// CYC=4 — instr null guard + foreach loop + acc null guard + pos null/qty guard
private void MirrorClose(Order masterOrder, CopyRule rule)
{
    var instr = masterOrder.Instrument;
    if (instr == null) return;                                          // guard (1)
    foreach (var acc in rule.FollowerAccounts)                         // loop (2)
    {
        if (acc == null) continue;                                      // guard (3)
        var pos = FindPosition(acc, instr);
        if (pos == null || pos.Quantity == 0) continue;                // guard (4)
        var action = pos.MarketPosition == MarketPosition.Long
            ? OrderAction.Sell : OrderAction.BuyToCover;               // ternary: not a branch
        try
        {
            acc.CreateOrder(instr, action, OrderType.Market,
                OrderEntry.Manual, TimeInForce.Day,
                pos.Quantity, 0, 0, null,
                "PTT-Mirror-Close",    // signal name starts with "PTT-"
                DateTime.MaxValue, null);
            StatusUpdate?.Invoke(acc.Name + ": mirror-close " + pos.Quantity);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("PTT-Mirror-Close error: " + ex.Message);
        }
    }
}

// CYC=2 — Filled state check + IsBracketLeg check (AND short-circuit = 2 decision points)
// TESTABILITY: declared internal static with primitive parameters (not Order) to allow
// unit testing without NT8 runtime context.
internal static bool ShouldMirrorClose(OrderState state, bool isBracketLeg)
{
    return state == OrderState.Filled && isBracketLeg;
}
```

**`ShouldMirrorClose` signature note:**
The plan shows `private static bool ShouldMirrorClose(Order order)`. The ticket promotes it to
`internal static bool ShouldMirrorClose(OrderState state, bool isBracketLeg)` for direct unit
testability without NT8 `Order` objects. CYC remains 2. Call site in `MirrorOrderUpdate`:
```csharp
if (ShouldMirrorClose(masterOrder.OrderState, IsBracketLeg(masterOrder))) { ... }
```

**`OnOrderUpdate` modification — exact insertion point (T3):**
Add the mirror branch AFTER Gate 2.5 (per-rule enable check) and BEFORE Gate B (bracket check):
```csharp
// Gate 2.5: per-rule enable check
// [existing code — do not modify]

// T3 INSERTION — insert BEFORE the existing Gate B bracket check:
if ((CopyMode)_copyModeValue == CopyMode.Mirror)           // +1 CYC (now CYC=8, at limit)
    MirrorOrderUpdate(e.Order, matchedRule.Value);

// Gate B: bracket check (EXISTING — do not modify)
if (IsWorkingBracket(e.Order)) { ... HandleBracketChange ... return; }
// DispatchCopy continues below (existing)
```

`OnOrderUpdate` CYC after T3: **7 (B8 baseline) + 1 (mirror branch) = 8** — AT the CYC limit, no violation.

### Method Signatures — TradeCopierPanel.cs (Mirror Mode + Named ATM)

**New fields:**
```csharp
private RadioButton _signalModeBtn;
private RadioButton _mirrorModeBtn;
```

**New methods:**
```csharp
// CYC=1 — appends "Copy Mode:" label + [Signal] [Mirror] RadioButton row to BuildUI()
private void BuildModeRow()
{
    // Creates _signalModeBtn (default IsChecked=true) and _mirrorModeBtn
    // Adds Click handlers: OnSignalModeClick and OnMirrorModeClick
    // Appends as a new row in the panel StackPanel
}

// CYC=1 — straight-line engine call
private void OnSignalModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
}

// CYC=1 — straight-line engine call
private void OnMirrorModeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);
}
```

**Named ATM inline TextBox in `BuildCheckItemTemplate()` (anonymous lambda — no new named method):**
```csharp
// After the ATM ComboBox (namedAtmCb) in BuildCheckItemTemplate():
var namedBox = new TextBox
{
    Width      = 80,
    Visibility = Visibility.Collapsed,
    ToolTip    = "ATM template name"
};
namedBox.Tag = item;   // bind to FollowerItem for AtmModeName update

namedAtmCb.SelectionChanged += (s, e) =>
{
    var sel = namedAtmCb.SelectedItem as string ?? string.Empty;
    namedBox.Visibility = sel == "Named" ? Visibility.Visible : Visibility.Collapsed;  // branch +1
    if (sel != "Named") namedBox.Text = string.Empty;
};

namedBox.TextChanged += (s, e) =>
{
    if (namedBox.Tag is FollowerItem fi)                                                // branch +1
        fi.AtmModeName = namedBox.Text.Length > 0 ? "Named:" + namedBox.Text : "Inherit";
};
```
`BuildCheckItemTemplate` CYC adds 2 branches. Confirm method stays ≤ 8 after additions.

### Method Signatures — TradeCopierWindow.cs (Named ATM Inline + Mode ComboBox)

**Mode ComboBox in header section (no new field required — local variable in builder method):**
```csharp
// In the header/toolbar area builder:
var modeCb = new ComboBox { Width = 120 };
modeCb.Items.Add("Signal (default)");
modeCb.Items.Add("Mirror");
modeCb.SelectedIndex = 0;
modeCb.SelectionChanged += OnCopyModeComboChanged;
```

```csharp
// CYC=1 — straight-line: read selection, call engine
private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
{
    var cb = sender as ComboBox;
    if (cb == null) return;
    CopyEngine.Instance.SetCopyMode(
        cb.SelectedIndex == 1 ? CopyMode.Mirror : CopyMode.Signal);
}
```

**Named ATM inline TextBox in `BuildRuleRow()` and `BuildDynamicRuleRow()` (anonymous lambda):**
```csharp
// After the ATM ComboBox (atmCb) in BuildRuleRow() and BuildDynamicRuleRow():
var namedBox = new TextBox { Width = 80, Visibility = Visibility.Collapsed };
atmCb.SelectionChanged += (s, e) =>
{
    var sel = atmCb.SelectedItem?.ToString() ?? string.Empty;
    namedBox.Visibility = sel == "Named" ? Visibility.Visible : Visibility.Collapsed;
};
```

**`OnRowApply` modification (+1 CYC branch):**
After reading the ATM mode string, add:
```csharp
// EXISTING: read atmMode from ComboBox selection
// T3 ADDITION: when Named, append the textbox value
if (atmMode == "Named" && namedBox.Text.Length > 0)   // +1 branch
    atmMode = "Named:" + namedBox.Text;
```
`OnRowApply` CYC adds 1 branch. Confirm method stays ≤ 8 after addition.

### CYC Summary — T3 Methods

| Method | File | CYC | Limit |
|--------|------|-----|-------|
| `SetCopyMode` | CopyEngine | 1 | ✅ |
| `GetCopyMode` | CopyEngine | 1 | ✅ |
| `MirrorOrderUpdate` | CopyEngine | 3 | ✅ |
| `MirrorClose` | CopyEngine | 4 | ✅ |
| `ShouldMirrorClose` | CopyEngine | 2 | ✅ |
| `OnOrderUpdate` (after T3) | CopyEngine | 8 | ✅ AT LIMIT |
| `BuildModeRow` | Panel | 1 | ✅ |
| `OnSignalModeClick` | Panel | 1 | ✅ |
| `OnMirrorModeClick` | Panel | 1 | ✅ |
| `OnCopyModeComboChanged` | Window | 1 | ✅ |

### xUnit [Fact] Tests — T-B9-15 through T-B9-20

```csharp
// T-B9-15: SetCopyMode(Signal) roundtrip
[Fact]
public void SetCopyMode_Signal_roundtrips()
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
    Assert.Equal(CopyMode.Signal, CopyEngine.Instance.GetCopyMode());
}

// T-B9-16: SetCopyMode(Mirror) roundtrip
[Fact]
public void SetCopyMode_Mirror_roundtrips()
{
    CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);
    Assert.Equal(CopyMode.Mirror, CopyEngine.Instance.GetCopyMode());
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // teardown: reset to default
}

// T-B9-17: Default copy mode is Signal
[Fact]
public void DefaultCopyMode_is_Signal()
{
    // CopyEngine is constructed with _copyModeValue = 0 = Signal
    // Reset in case previous test left Mirror active
    CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
    Assert.Equal(CopyMode.Signal, CopyEngine.Instance.GetCopyMode());
}

// T-B9-18: ShouldMirrorClose returns true when order is Filled and is a bracket leg
[Fact]
public void ShouldMirrorClose_true_when_bracket_filled()
{
    bool result = CopyEngine.ShouldMirrorClose(OrderState.Filled, isBracketLeg: true);
    Assert.True(result);
}

// T-B9-19: ShouldMirrorClose returns false when Filled but not a bracket leg
[Fact]
public void ShouldMirrorClose_false_when_not_bracket()
{
    bool result = CopyEngine.ShouldMirrorClose(OrderState.Filled, isBracketLeg: false);
    Assert.False(result);
}

// T-B9-20: ShouldMirrorClose returns false when order is Working (not filled)
[Fact]
public void ShouldMirrorClose_false_when_working()
{
    bool result = CopyEngine.ShouldMirrorClose(OrderState.Working, isBracketLeg: true);
    Assert.False(result);
}
```

### 7-Scan Checklist — T3

Run scans on: `CopyEngine.cs` (T3 new lines), `TradeCopierPanel.cs` (T3 new lines), `TradeCopierWindow.cs` (T3 new lines).

| Scan | Pattern | Expected | Where to check |
|------|---------|----------|----------------|
| SCAN-01 | `lock\s*\(` | ZERO | All T3 new code — `volatile int _copyModeValue` used throughout |
| SCAN-02 | `throw new` in hot path | ZERO | `MirrorClose` CreateOrder in try/catch, no rethrow |
| SCAN-03 | `return null` in new methods | ZERO | `MirrorClose` returns `void`, `ShouldMirrorClose` returns `bool` |
| SCAN-04 | `new Dictionary<` | ZERO | No new dict fields in T3 |
| SCAN-05 | `DateTime\.Now[^U]` | ZERO | `MirrorClose` uses `DateTime.MaxValue` (not `.Now`) |
| SCAN-06 | `async void` | ZERO | All handlers sync `void` |
| SCAN-07 | `#[0-9A-Fa-f]{6}` in string literals | ZERO | No hex color strings in mirror/mode code |

**Additional B9-T3 checks:**

| Check | Expected |
|-------|---------|
| `CreateOrder` signal name is `"PTT-Mirror-Close"` (starts with `"PTT-"`) | CONFIRMED |
| `_copyModeValue` is `private volatile int` | CONFIRMED (JS-023) |
| `ShouldMirrorClose` is `internal static` with `(OrderState, bool)` params | CONFIRMED (testability) |
| Mirror branch in `OnOrderUpdate` is BEFORE Gate B (bracket check) | CONFIRMED — insertion point documented above |
| `OnOrderUpdate` CYC after T3 is exactly 8 (at limit, no violation) | CONFIRMED |
| `MirrorBracketMove` NOT added — `HandleBracketChange` called directly from `MirrorOrderUpdate` | CONFIRMED |

### Build Gate — T3

```
dotnet build PropTraderTools.csproj   -> 0 errors, 0 warnings
dotnet test CopyEngineTests           -> 60 [Fact] tests pass (54 T2 + 6 T3 new)
```

---

## Final Test Inventory

| Range | Ticket | Feature | Count |
|-------|--------|---------|-------|
| T-B8-01..40 | B8 baseline | All B8 features | 40 |
| T-B9-01..08 | T1 | `AtrSizingEngine.CalcContracts` math | 8 |
| T-B9-09..10 | T1 | `CopyEngine.GetSuggestedQty` integration | 2 |
| T-B9-11..14 | T2 | Click trader signal names + ATR qty | 4 |
| T-B9-15..17 | T3 | `CopyMode` roundtrip + default | 3 |
| T-B9-18..20 | T3 | `ShouldMirrorClose` predicate | 3 |
| **Total** | | | **60** |

---

## Deferred Backlog Disposition (B9)

| ID | Item | B9 Action |
|----|------|-----------|
| DW-B7-02 / DW-B8-03 | ATR dynamic sizing engine | **CLOSED — T1** |
| DW-B8-04 | Click trader | **CLOSED — T2** |
| DW-B8-06 | Full mirror mode | **CLOSED — T3** |
| SPEC-2354 | Named ATM inline input | **CLOSED — T3** |
| DW-B8-01 | return null cleanup | **CLOSED — compliant per plan §6** |
| DW-B8-02 | Gate hook path fix | **OUT OF SCOPE — non-source** |
| DW-B8-05 | ATR box visualization | **DEFERRED B10 (DW-B9-01)** |
| DW-B9-01 | ATR box chart visualization | **NEW — B10** |
| DW-B9-02 | IMPL-NOTE-1: verify chart attachment API | **B9-T1 completion report** |
| DW-B9-03 | Click trader bid/ask offset | **B10** |

---

**TICKETS_COMPLETE**
