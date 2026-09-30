# PTT-COPIER-B16 Architecture Plan
# Status: AWAITING REVIEW (Phase 2 output)
# Author: ptt-architect
# Date: 2026-07-14
# Prev block: PTT-COPIER-B15
# Next phase: ptt-plan-reviewer → 02-plan-review.md

---

## A. Block Summary

**Block:** PTT-COPIER-B16
**Primary goal:** Reopen DW-B8-04 (now filed as DW-B16-01). The click trader must place a
Limit order at the EXACT price level the user clicked on the chart. B15 "closed" DW-B8-04
with a `MarketData.Last.Price` fallback that ignores the Y pixel entirely. This is incorrect.

**Philosophy:** NINJA-NATIVE MAXIMUM. If NT8 exposes any pixel-to-price API on any child of
`ChartPanel`, use it directly. No custom order management. No V12 order engine patterns.

**Two-ticket structure (pre-approved by Director):**
- T1: ChartScale investigation diagnostic (F5 required — `MessageBox.Show` output)
- T2: Implementation (GATED on T1 VERIFY_PASS — two branches, engineer picks based on T1 findings)

---

## B. Prior Block Status

### B15 Left This State

B15 T1 ran a VT dump diagnostic. The dump was written to `_statusText` which has a
width limit. The raw output was:

```
ChartBars=NO VT|ChartTimeAxis,ChartPanel/
```

The `/` at the end is a truncation marker. `ChartPanel`'s own children were **never
examined**. The B15 VT walk only reached depth=1 (direct children of `ChartControl`).
Depth=2 (children of `ChartPanel`) was never read.

B15 T2 replaced `GetPriceAtY` with the `MarketData.Last.Price` fallback:

```csharp
// TradeCopierPanel.cs lines 298-304 (B15 current -- INCORRECT)
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (instrument == null) return 0.0;          // guard (1)
    var last = instrument.MarketData.Last;
    if (last == null) return 0.0;                // guard (2)
    return last.Price;   // BUG: ignores y entirely
}
```

The `y` parameter is captured (line 1127 in `OnChartMouseDown`) but passed into
`GetPriceAtY` where it is ignored. The click trader always places at last-trade price,
regardless of where the user clicked.

### DW-B16-01 Definition (Reopened DW-B8-04)

| Field | Value |
|-------|-------|
| ID | DW-B16-01 |
| Reopens | DW-B8-04 (incorrectly closed in B15) |
| Description | Click trader must derive order price from the Y pixel the user clicked |
| Root cause | B15 MarketData fallback ignores Y; NT8 depth=1 VT walk was truncated |
| Blocking | Nothing (DW-B9-03 is shelved per Director) |
| Approach | T1: expand VT walk to depth=2 via reflection + MessageBox; T2: use confirmed API or approximation |

---

## C. T1 Design: ChartPanel Subtree Diagnostic

### Goal

Determine whether NT8 exposes a native Y-to-price conversion API on any child of
`ChartPanel`. Walk `ChartPanel`'s own children, probe their method signatures via
reflection, read `ActualHeight` for correction factor data.

### C.1 New Field

```csharp
// TradeCopierPanel.cs -- class-level field (add near other volatile fields)
// B16 T1: one-shot guard for ChartPanel subtree diagnostic.
// NT8-017: volatile bool for cross-lifetime guard (UI thread write).
// Removed in T2.
private volatile bool _chartScaleDiagDone = false;
```

**Placement:** Near other volatile fields (e.g. after `_chartDiagDone` removal reference
from B15). Removed entirely in T2.

### C.2 SetChart Modification

Current `SetChart` (lines 285-288, CYC=1):
```csharp
public void SetChart(Chart chart)
{
    _currentChart = chart;
}
```

T1 modified `SetChart` (CYC=2):
```csharp
public void SetChart(Chart chart)
{
    _currentChart = chart;
    if (!_chartScaleDiagDone)          // one-shot guard (branch 1)
        WalkChartPanelChildren(chart);
}
```

**Public signature unchanged.** `TradeCopierAddOn.cs` is NOT touched.

### C.3 WalkChartPanelChildren Method

```csharp
// private void WalkChartPanelChildren(Chart chart)
// B16 T1 diagnostic -- find ChartPanel children and probe for Y-to-price methods.
// One-shot: _chartScaleDiagDone prevents re-entry.
// Output: MessageBox.Show -- never _statusText (no truncation).
// NT8-017: _chartScaleDiagDone is volatile bool.
// CYC: guard(1) + cc null(2) + panel null(3) + loop(4) + FrameworkElement check(5) = CYC=5.
// Removed in T2.
//
// Required using directives (T1 only):
//   using System.Reflection;
//   using System.Text;    (if not already present)
//
// Called from: SetChart (UI thread -- Dispatcher.InvokeAsync context from TradeCopierAddOn).
// MessageBox.Show is legal on UI thread.
private void WalkChartPanelChildren(Chart chart)
{
    _chartScaleDiagDone = true;                                   // set immediately (guard)

    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);
    if (cc == null) return;                                        // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null) return;                                     // guard (2)

    var sb = new StringBuilder();
    sb.AppendLine("PTT B16 -- ChartPanel Children Probe");
    sb.AppendLine("ChartPanel.ActualHeight = " + panel.ActualHeight.ToString("F2"));
    sb.AppendLine("ChartPanel.ActualWidth  = " + panel.ActualWidth.ToString("F2"));
    sb.AppendLine();

    int count = VisualTreeHelper.GetChildrenCount(panel);
    sb.AppendLine("ChildCount = " + count);

    for (int i = 0; i < count; i++)                               // loop (3)
    {
        var child = VisualTreeHelper.GetChild(panel, i);
        sb.AppendLine("  [" + i + "] " + child.GetType().FullName);

        if (child is System.Windows.FrameworkElement fe)           // check (4)
            sb.AppendLine("       ActualHeight=" + fe.ActualHeight.ToString("F2")
                        + " ActualWidth=" + fe.ActualWidth.ToString("F2"));

        sb.AppendLine(BuildMethodReport(child.GetType()));
    }

    System.Windows.MessageBox.Show(sb.ToString(), "PTT B16 ChartPanel Subtree");
}
```

### C.4 BuildMethodReport Helper

```csharp
// private static string BuildMethodReport(Type t)
// Returns a multi-line string listing methods on t whose name (case-insensitive)
// contains "value", "price", "gety", or "y".
// CYC: foreach(1) + if-name-filter(2) = CYC=2.
// Removed in T2.
private static string BuildMethodReport(Type t)
{
    var sb = new StringBuilder();
    var methods = t.GetMethods(
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    foreach (var m in methods)                                     // (1)
    {
        string nameLower = m.Name.ToLower(
            System.Globalization.CultureInfo.InvariantCulture);

        bool match = nameLower.Contains("value")
                  || nameLower.Contains("price")
                  || nameLower.Contains("gety")
                  || (nameLower == "y");                           // exact "y" only to avoid noise
        if (!match) continue;                                      // (2)

        sb.Append("       Method: " + m.Name + "(");
        var parms = m.GetParameters();
        for (int p = 0; p < parms.Length; p++)
        {
            if (p > 0) sb.Append(", ");
            sb.Append(parms[p].ParameterType.Name + " " + parms[p].Name);
        }
        sb.Append(") -> " + m.ReturnType.Name);
        sb.AppendLine();
    }
    return sb.ToString();
}
```

### C.5 What the Engineer Must Record

After F5 on Sim101:
1. Read the MessageBox output in full.
2. Document ALL type names at depth=2 (children of ChartPanel).
3. For each child with a matching method, record the EXACT signature.
4. Record each child's `ActualHeight` and `ActualWidth`.
5. Append findings to `docs/standards/NT8_ADDON_KNOWLEDGE.md` under `## B16 Discoveries`.
6. If `ChartPanel.ActualHeight` or `ChartPanel.MaxValue` / `ChartPanel.MinValue` are
   visible in the output -- record those too (they may appear as methods or properties
   in the reflection output).

### C.6 T1 CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `SetChart` (T1 modified) | 2 | if (!_chartScaleDiagDone) |
| `WalkChartPanelChildren` | 5 | return(guard cc), return(guard panel), for-loop, FrameworkElement check, and + guard already counted in total |
| `BuildMethodReport` | 2 | foreach, if !match continue |

All ≤ 8. ✅

### C.7 T1 Tests

None. Diagnostic code has no testable pure-math logic. All logic depends on NT8 runtime
VT contents that are unavailable in unit tests.

---

## D. T2 Design: Implementation (Gated on T1 VERIFY_PASS)

T2 is written with two branches. The engineer picks the branch based on T1 F5 findings.
The plan specifies both. The chosen branch is documented in NT8_ADDON_KNOWLEDGE.md B16.

### D.1 Branch A — Preferred: Native NT8 API Found

**Trigger:** T1 MessageBox shows a child of `ChartPanel` (e.g. `ChartScale` or similar)
that exposes a method returning a `double` with a parameter of type `double` or `Single`
whose name matches "GetValue", "GetPrice", "GetY", "ValueFromY", or similar.

**GetPriceAtY replacement (Branch A):**

```csharp
// private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
// B16 T2 Branch A: Use confirmed NT8 native API on ChartPanel child.
// Replace confirmed type placeholder {ChildType} and method {ConfirmedMethod} with
// actual type and method name recorded in NT8_ADDON_KNOWLEDGE.md B16 Discoveries.
// NT8-008: cc arrives from FindVisualChild<ChartControl>(chart) in OnChartMouseDown.
// CYC: cc null(1), panel null(2), child null(3), raw ≤ 0 fallback(4) = CYC=4.
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (cc == null) return 0.0;                                       // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null)                                                 // guard (2)
        goto fallback;

    // Walk children of ChartPanel to find confirmed child type.
    // {ChildType} = exact type confirmed by T1 F5 (e.g. NinjaTrader.Gui.Chart.ChartScale).
    // {ConfirmedMethod} = exact method name confirmed by T1 F5.
    // Engineer fills in the confirmed type and method name here.
    var scale = TradeCopierAddOn.FindVisualChild<{ChildType}>(panel);
    if (scale == null)                                                 // guard (3)
        goto fallback;

    double rawPrice = scale.{ConfirmedMethod}(y);
    if (rawPrice <= 0.0)                                               // guard (4)
        goto fallback;

    return instrument != null
        ? instrument.MasterInstrument.RoundToTickSize(rawPrice)        // NT8-native tick align
        : rawPrice;

fallback:
    if (instrument == null) return 0.0;
    var last = instrument.MarketData.Last;
    return last != null ? last.Price : 0.0;                           // NT8-032
}
```

**Note on `goto`:** `goto` is used here to avoid nested if/else that would push CYC above
the target. The `goto fallback` pattern is equivalent to early return from nested guards.
CYC remains 4 regardless. If engineer prefers early returns + helper method, that is also
acceptable as long as CYC ≤ 6 per method.

**Note on `RoundToTickSize`:** `_instrument.MasterInstrument.RoundToTickSize(double price)` is
the NT8-native tick-alignment method (documented in NT8 MasterInstrument API). If this raises
CS1061 at T2 F5, the engineer:
  1. Falls back to: `Math.Round(rawPrice / tickSize, MidpointRounding.AwayFromZero) * tickSize`
  2. Adds NT8-038 rule to `NT8_COMPILER_RULES.md` documenting the absence.

### D.2 Branch B — Fallback: No Native API Found

**Trigger:** T1 MessageBox shows no child of `ChartPanel` with a Y-to-price method. All
children enumerated. No method matching the name filter found.

**Approach:** Linear interpolation using `ChartPanel` price range properties.

NT8's `ChartPanel` exposes `MaxValue` and `MinValue` properties (documented in NT8 API, not
yet compiled-confirmed in PTT). These define the visible price range on the chart. The pixel
coordinate system: Y=0 is the top of the panel (highest price), Y=ActualHeight is the bottom
(lowest price).

Formula:
```
rawPrice = MinValue + (MaxValue - MinValue) * (1.0 - y / panelHeight)
```
Equivalently:
```
rawPrice = MaxValue - (y / panelHeight) * (MaxValue - MinValue)
```

**Correction factor note:** T1 records `ActualHeight` of each ChartPanel child. If the price
scale does not span the full `ChartPanel.ActualHeight` (e.g. the top/bottom margins are
occupied by the time axis chrome), a correction factor is needed. The engineer derives this
from the T1 `ActualHeight` data:
```
correctionFactor = priceAreaHeight / panelActualHeight
y_corrected = y - topMargin
```
Document the measured correction factor in `NT8_ADDON_KNOWLEDGE.md B16 Discoveries`. B16 Branch B
uses the correction factor derived from T1 -- NOT a hardcoded value.

**GetPriceAtY replacement (Branch B):**

```csharp
// private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
// B16 T2 Branch B: Linear interpolation via ChartPanel.MaxValue / MinValue / ActualHeight.
// NOTE: This is an approximation. Pixel-to-price is linear only when the chart scale is
// linear (not log). NT8 uses linear scale by default. Accuracy depends on correction factor.
// Correction factor = priceAreaHeight / panelActualHeight (measured from T1 ActualHeight data).
// NT8-029 replacement: RoundToTickSize instead of Math.Round (NT8-native tick align).
// CYC: cc null(1), panel null(2), height ≤ 0(3), raw ≤ 0(4), instrument null(5) = CYC=5.
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (cc == null) return 0.0;                                       // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null) return 0.0;                                    // guard (2)

    double panelH = panel.ActualHeight;
    if (panelH <= 0.0) return 0.0;                                    // guard (3): no divide by zero

    // CORRECTION_FACTOR: float derived from T1 ActualHeight readings.
    // Engineer fills in the measured value. Example: 0.95 if price area is 95% of panel height.
    const double CORRECTION_FACTOR = 1.0;                             // placeholder -- set from T1

    double maxVal  = panel.MaxValue;
    double minVal  = panel.MinValue;
    double yRatio  = (y / (panelH * CORRECTION_FACTOR));
    double rawPrice = maxVal - yRatio * (maxVal - minVal);

    if (rawPrice <= 0.0) return 0.0;                                  // guard (4): sanity

    if (instrument == null) return 0.0;                               // guard (5)
    return instrument.MasterInstrument.RoundToTickSize(rawPrice);     // NT8-native tick align
}
```

**If `ChartPanel.MaxValue` or `ChartPanel.MinValue` cause CS1061 at T2 F5:**
  1. Add NT8-039 (MaxValue absent) and/or NT8-040 (MinValue absent) to `NT8_COMPILER_RULES.md`.
  2. Document in `NT8_ADDON_KNOWLEDGE.md B16 Discoveries`.
  3. Fall back to `instrument.MarketData.Last.Price` (same as B15 -- DW-B16-01 remains OPEN).

### D.3 Internal Test Helper (Both Branches)

To enable [Fact] tests for the pure price math, add an `internal static` helper:

```csharp
// TradeCopierPanel.cs
// B16 T2: Pure-math helper for linear Y-to-price interpolation (Branch B).
// Internal for xUnit test access via InternalsVisibleTo (established pattern in CopyEngine).
// CYC=2: height guard(1), raw guard(2).
internal static double LinearYToPrice(
    double y, double panelH, double maxVal, double minVal, double correctionFactor)
{
    if (panelH <= 0.0) return 0.0;                                    // guard (1): no divide by zero
    double yRatio   = y / (panelH * correctionFactor);
    double rawPrice = maxVal - yRatio * (maxVal - minVal);
    if (rawPrice <= 0.0) return 0.0;                                  // guard (2): sanity
    return rawPrice;
}

// B16 T2: Pure-math tick alignment helper (used in both branches for testability).
// Mirrors NT8-native RoundToTickSize semantics via Math.Round.
// CYC=1: straight-line.
internal static double AlignToTick(double raw, double tickSize)
{
    if (tickSize <= 0.0) return raw;
    return Math.Round(raw / tickSize, MidpointRounding.AwayFromZero) * tickSize;
}
```

### D.4 T2 [Fact] Tests

File: `src/PropTraderTools/CopyEngineTests.cs`

| Test Name | What It Asserts |
|-----------|----------------|
| `T_B16_01_LinearPriceInterp_TopOfChart_ReturnsMaxValue` | y=0, panelH=400, max=5000, min=4900, cf=1.0 → raw=5000.0 |
| `T_B16_02_LinearPriceInterp_BottomOfChart_ReturnsMinValue` | y=400, panelH=400, max=5000, min=4900, cf=1.0 → raw=4900.0 |
| `T_B16_03_LinearPriceInterp_MiddleOfChart_ReturnsMidpoint` | y=200, panelH=400, max=5000, min=4900, cf=1.0 → raw=4950.0 |
| `T_B16_04_LinearPriceInterp_QuarterFromTop_ReturnsThreeQuarterRange` | y=100, panelH=400, max=5000, min=4900, cf=1.0 → raw=4975.0 |
| `T_B16_05_LinearPriceInterp_ZeroHeight_ReturnsZero` | panelH=0 → 0.0 (no divide by zero) |
| `T_B16_06_AlignToTick_ValueBelowMidTick_RoundsDown` | raw=4975.10, tick=0.25 → 4975.00 |
| `T_B16_07_AlignToTick_ValueAboveMidTick_RoundsUp` | raw=4975.15, tick=0.25 → 4975.25 |
| `T_B16_08_AlignToTick_ExactTickBoundary_Unchanged` | raw=4975.25, tick=0.25 → 4975.25 |

**Note:** Tests call `TradeCopierPanel.LinearYToPrice(...)` and `TradeCopierPanel.AlignToTick(...)`
directly. Both methods are `internal static` accessible via `InternalsVisibleTo` (same pattern as
existing CopyEngine tests).

### D.5 T2 CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `GetPriceAtY` (Branch A) | 4 | cc null, panel null, scale null, raw ≤ 0 |
| `GetPriceAtY` (Branch B) | 5 | cc null, panel null, height ≤ 0, raw ≤ 0, instrument null |
| `LinearYToPrice` | 2 | height guard, raw guard |
| `AlignToTick` | 1 | straight-line (tickSize ≤ 0 guard is CYC=2 but acceptable) |
| `SetChart` (T2 restored) | 1 | straight-line (T1 diagnostic call removed) |
| `OnChartMouseDown` | 7 | unchanged from B15 |
| Each `[Fact]` test | 1 | straight-line |

All ≤ 8. ✅

### D.6 T2 Changes to TradeCopierPanel.cs Summary

| Action | What |
|--------|------|
| REMOVE | `private volatile bool _chartScaleDiagDone = false;` field |
| REMOVE | `WalkChartPanelChildren(Chart chart)` method |
| REMOVE | `BuildMethodReport(Type t)` helper method |
| REMOVE | `if (!_chartScaleDiagDone) WalkChartPanelChildren(chart);` from `SetChart` |
| REMOVE | `using System.Reflection;` (if added only for T1) |
| REMOVE | `using System.Text;` (if added only for T1) |
| REPLACE | `GetPriceAtY` body (lines 299-303): stub replaced with Branch A or Branch B |
| ADD | `LinearYToPrice` internal static helper |
| ADD | `AlignToTick` internal static helper |
| UNCHANGED | `OnChartMouseDown` body (lines 1116-1156) |
| UNCHANGED | `SetInstrument`, `SetLeaderAccount`, `Detach`, all other methods |

---

## E. Files Touched Map

| File | Tickets | What Changes |
|------|---------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | T1 + T2 | T1: add field + 2 methods + modify SetChart. T2: remove T1 code, replace GetPriceAtY, add 2 helpers |
| `src/PropTraderTools/CopyEngineTests.cs` | T2 only | Add T_B16_01 through T_B16_08 [Fact] tests |
| `docs/standards/NT8_COMPILER_RULES.md` | T1/T2 | Add any new rules from T1 F5 or T2 F5 findings |
| `docs/standards/NT8_ADDON_KNOWLEDGE.md` | T1/T2 | ## B16 Discoveries section (T1 F5 output + T2 confirmation) |

**Must NOT touch:**
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierAddOn.cs`
- `src/PropTraderTools/TradeCopierWindow.cs`
- `src/PropTraderTools/AtrSizingEngine.cs`

---

## F. NT8 API Usage Reference

| API | Confirmed | Rule | Used In |
|-----|-----------|------|---------|
| `FindVisualChild<ChartControl>(chart)` | ✅ B15 | NT8-008 | WalkChartPanelChildren, GetPriceAtY |
| `FindVisualChild<ChartPanel>(cc)` | ✅ B15 | NT8-036 | WalkChartPanelChildren, GetPriceAtY |
| `VisualTreeHelper.GetChildrenCount(panel)` | ✅ B7+ | — | WalkChartPanelChildren |
| `VisualTreeHelper.GetChild(panel, i)` | ✅ B7+ | — | WalkChartPanelChildren |
| `System.Reflection.BindingFlags` | NT8 compatible | NT8-031 pattern | BuildMethodReport (T1 only) |
| `System.Windows.MessageBox.Show` | ✅ B7 diagnostic | — | WalkChartPanelChildren (T1 only) |
| `instrument.MasterInstrument.RoundToTickSize(double)` | UNCONFIRMED | NT8-029 | GetPriceAtY T2 |
| `instrument.MarketData.Last.Price` | ✅ B12 | NT8-032 | GetPriceAtY fallback |
| `ChartPanel.MaxValue` | UNCONFIRMED | — | GetPriceAtY Branch B |
| `ChartPanel.MinValue` | UNCONFIRMED | — | GetPriceAtY Branch B |
| `ChartPanel.ActualHeight` | likely ✅ (FrameworkElement base) | — | GetPriceAtY Branch B |

**UNCONFIRMED items:** If `RoundToTickSize` or `ChartPanel.MaxValue/MinValue` raise CS1061 at F5:
  1. Document as new NT8-NNN rule.
  2. Fall back to established confirmed patterns (Math.Round for tick, Last.Price for price).
  3. DW-B16-01 status: remains OPEN if Branch B also fails; document in Section K.

---

## G. Threading Model

| Context | Thread | Operations |
|---------|--------|-----------|
| `SetChart()` called from `TradeCopierAddOn.DoInject()` | UI thread (Dispatcher.InvokeAsync) | `WalkChartPanelChildren` call safe |
| `WalkChartPanelChildren` | UI thread | `VisualTreeHelper`, `MessageBox.Show`, reflection — all legal |
| `_chartScaleDiagDone` writes | UI thread only | `volatile bool` per NT8-017 |
| `GetPriceAtY` called from `OnChartMouseDown` | UI thread (WPF mouse event) | VT walk safe |
| `LinearYToPrice` / `AlignToTick` | any thread (pure math) | no thread constraints |

No `lock()` anywhere. ✅ JS-021, NT8-018.
No `async void` anywhere. ✅ JS-033, NT8-019.

---

## H. JS Rule Compliance Table

| Rule | Requirement | B16 Status |
|------|-------------|-----------|
| JS-021 | No `lock()` | ✅ No lock anywhere in B16 code |
| JS-023 | Volatile for cross-thread state | ✅ `_chartScaleDiagDone` is `volatile bool` |
| JS-033 | No `async void` | ✅ All methods are sync void or static double |
| NT8-013 | `DateTime.MaxValue` in CreateOrder | ✅ Unchanged from B15 (line 1145) |
| NT8-014 | Signal name starts with "PTT-" | ✅ "PTT-Click" unchanged |
| NT8-017 | Cross-thread fields must be volatile | ✅ New field is volatile bool |
| NT8-018 | No lock() | ✅ |
| NT8-019 | No async void in NT8 callbacks | ✅ |
| NT8-028 | No hex color strings | ✅ No color changes in B16 |
| NT8-029 | Tick alignment on limit prices | ✅ RoundToTickSize (or Math.Round fallback) |
| NT8-032 | MarketData.Last.Price not .Last | ✅ Fallback uses .Last.Price correctly |
| NT8-035 | No 0.0 stub in CreateOrder price | ✅ T2 replaces stub with real lookup |
| NT8-036 | ChartControl.ChartBars absent | ✅ Not used; FindVisualChild<ChartPanel> instead |
| NT8-037 | ChartPanel.GetValueByY absent | ✅ Not used; plan investigates depth-2 children |

---

## I. Shelved Items (carry-forward, no change in B16)

| ID | Item | Reason |
|----|------|--------|
| DW-B9-01 | ATR box visualization on chart canvas | Shelved since B9; no work in B16 scope |
| DW-B9-03 | Click trader Bid+1/Ask-1 spread auto-offset | Shelved per Director mandate in B16 mission brief |
| DW-B12-DEFER-01 (orig) | Buy Ask / Sell Bid full-panel mode expansion | Shelved; no work in B16 scope |

---

## J. CYC Budget Table (All Methods in Scope)

| Method | File | CYC Target | CYC Actual |
|--------|------|-----------|-----------|
| `SetChart` (T1 modified) | TradeCopierPanel.cs | ≤ 8 | 2 |
| `WalkChartPanelChildren` | TradeCopierPanel.cs | ≤ 8 | 5 |
| `BuildMethodReport` | TradeCopierPanel.cs | ≤ 8 | 2 |
| `SetChart` (T2 restored) | TradeCopierPanel.cs | ≤ 8 | 1 |
| `GetPriceAtY` (Branch A) | TradeCopierPanel.cs | ≤ 6 | 4 |
| `GetPriceAtY` (Branch B) | TradeCopierPanel.cs | ≤ 6 | 5 |
| `LinearYToPrice` | TradeCopierPanel.cs | ≤ 8 | 2 |
| `AlignToTick` | TradeCopierPanel.cs | ≤ 8 | 1 |
| `OnChartMouseDown` | TradeCopierPanel.cs | ≤ 8 | 7 (unchanged) |
| Each `T_B16_XX` [Fact] | CopyEngineTests.cs | ≤ 8 | 1 |

---

## K. Deferred Work

### K.1 Items Opened in B16

| ID | Description | Priority | Target |
|----|-------------|----------|--------|
| DW-B16-01 | Click trader Y-pixel-to-price lookup — see this plan for T1+T2 | P1 | B16 T1+T2 |
| DW-B16-02 (conditional) | If T2 Branch B used AND ChartPanel.MaxValue/MinValue are absent (CS1061): pixel-to-price remains unresolved; investigate NT8 Reflection cache at `%USERPROFILE%\Documents\NinjaTrader 8\cache\NinjaTrader.Core-*.Reflection.dat` for ChartScale or custom coordinate transform methods | P2 | B17+ |

### K.2 Items Carried Forward Open

| ID | Description | Priority | Source |
|----|-------------|----------|--------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B9 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset (UNBLOCKED if B16 T2 succeeds with real Y price) | P3 | B9 |
| DW-B12-DEFER-01 (orig) | Buy Ask / Sell Bid full-panel mode expansion | P2 | B12 |

---

## L. 7-Scan Checklist Template

Engineers completing T1 and T2 must attest to each scan item.

```
SCAN-01: lock() search
  grep -r "lock(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  T1 result: [ ]   T2 result: [ ]

SCAN-02: async void search
  grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  T1 result: [ ]   T2 result: [ ]

SCAN-03: DateTime.Now search
  grep -n "DateTime\.Now[^U]" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  T1 result: [ ]   T2 result: [ ]

SCAN-04: hex color search
  grep -n '"#[0-9A-Fa-f]' src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  T1 result: [ ]   T2 result: [ ]

SCAN-05: GetValueByY search (must be absent from source lines, OK in comments)
  grep -n "\.GetValueByY(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results (comments only)
  T1 result: [ ]   T2 result: [ ]

SCAN-06: price 0.0 stub in CreateOrder (T2 must eliminate)
  grep -n "price\s*=\s*0\.0" src/PropTraderTools/TradeCopierPanel.cs
  T1 expected: 1 result (stub still present in T1)
  T2 expected: 0 results (Branch A or B replaces stub)
  T1 result: [ ]   T2 result: [ ]

SCAN-07: [Fact] test names present in CopyEngineTests.cs
  grep -n "T_B16_" src/PropTraderTools/CopyEngineTests.cs
  T1 expected: 0 results (no T1 tests)
  T2 expected: 8 results (T_B16_01 through T_B16_08)
  T1 result: [ ]   T2 result: [ ]
```

---

## M. Return Value

**PLAN_COMPLETE**

Awaiting ptt-plan-reviewer verdict in `docs/brain/PTT-COPIER-B16/02-plan-review.md`.

---

## §DW-B16-02 — TightenOneStop Bug Fix (P1) [AMENDMENT 2026-07-15]

**Source:** Director injection before T1 start. CopyEngine.cs:1214 confirmed.

### Problem

`TightenOneStop()` contains a branch that detects `IsTrailingStop(order)` and takes the
`acc.Cancel() + acc.CreateOrder()` (cancel+replace) path. `IsTrailingStop()` returns `true`
for ANY `OrderType.StopMarket` order (NT8-026: `Order.TrailPrice` absent, so StopMarket
is the proxy — this proxy is too broad). Result: any StopMarket stop placed by the click
trader or manually gets cancel+replaced by TightenOneStop, which:
1. Nukes the NT8 trail watermark on a trailing stop order.
2. Orphans the ATM bracket target (bracket loses its stop leg reference).

### Confirmed Safe Pattern

All other PTT stop-price modifications (`MoveStopToBreakEven`, `ArmBE`, `TrailBE`,
`Flatten`, `Trim`, `Cancel`) use `acc.Change()`. GAP-001d confirmed `acc.Change()` does
NOT kill the trail watermark. TightenOneStop is the only exception — and it should not be.

### Fix (surgical — 3 lines removed, 0 added net)

In [`CopyEngine.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs)
`TightenOneStop()` (lines ~1201-1243):

**Remove** the entire `if (IsTrailingStop(order)) { ... } else { ... }` block.
**Replace** with the single `acc.Change()` path (was the `else` branch):

```csharp
// DW-B16-02: all stop types use acc.Change() -- GAP-001d CONFIRMED safe.
// cancel+replace branch removed (was nuking ATM bracket + trail watermark).
order.StopPrice = targetPrice;
acc.Change(new Order[] { order });
StatusUpdate?.Invoke(acc.Name + ": tighten stop -> " + targetPrice);
```

CYC drops from 4 to 3 (branch (3) removed).
Update header comment accordingly.

### Button Label Fix

In `TradeCopierPanel.cs` `BuildUI`: rename `"~"` button `Content` to `"Tighten"`.
- ASCII-safe (NT8 ASCII-only mandate).
- Eliminates ambiguous `~` symbol.

### Scope Assignment

**NOT a new ticket.** Appended to T2 scope (T2 already touches `TradeCopierPanel.cs`).
`CopyEngine.cs` added to T2 files-to-modify.

### Files

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Remove if/else in `TightenOneStop()` |
| `src/PropTraderTools/TradeCopierPanel.cs` | Rename `"~"` button to `"Tighten"` |
| `src/PropTraderTools/CopyEngineTests.cs` | Add T_B16_09 + T_B16_10 |

**Must NOT touch:** `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs`

### New [Fact] Tests

| Test Name | Assertion |
|-----------|-----------|
| `T_B16_09_TightenOneStop_StopMarketOrder_UsesAccChange_NotCancel` | acc.Change called; acc.Cancel NOT called |
| `T_B16_10_TightenOneStop_AlreadyTighter_NoAction` | no acc.Change, no acc.Cancel when alreadyTighter |

### Additional Scans (added to T2.13 BUILD_PASS)

```
SCAN-08: grep -n "PTT-Tighten-Stop" CopyEngine.cs → 0 results
         (CreateOrder call removed; signal name must be absent)
SCAN-09: grep -n '"~"' TradeCopierPanel.cs → 0 results
         (button label renamed to "Tighten")
```

### CYC Budget

| Method | Before | After |
|--------|--------|-------|
| `TightenOneStop` | 4 | 3 |

