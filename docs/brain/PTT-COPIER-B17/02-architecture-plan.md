# PTT-COPIER-B17 Architecture Plan
# Status: REVIEW_PASS
# Block: PTT-COPIER-B17
# Date: 2026-07-15
# Author: ptt-architect
# Prior block: docs/brain/PTT-COPIER-B16/06-deferred-backlog.md
# Spec: 002-trade-copier-spec.html

---

## §A Mission & Root Cause Analysis

### Mission

Fix click trader: chart clicks produce `rawPrice=0` so no limit order is placed.

### Confirmed Root Cause

`GetPriceAtY()` in [`TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs:297) calls:

```csharp
var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
```

`FindVisualChild<T>` is a depth-first search (DFS) that returns the **first** matching node.
The first `ChartPanel` in the visual tree under `ChartControl` is the **ChartTrader sidebar panel**:

| Panel | ActualWidth | ActualHeight | MaxValue | MinValue |
|-------|-------------|--------------|----------|----------|
| ChartTrader sidebar | 139.33 | 452.00 | **0.00** | **0.00** |
| Price canvas (target) | ~1047.20 | 452.00 | ~5023.25 | ~4987.50 |

Because `MaxValue = MinValue = 0`:
- `rawPrice = 0 - ratio * (0 - 0) = 0`
- Guard `if (rawPrice <= 0.0) return 0.0;` fires
- `OnChartMouseDown` guard `if (rawPrice <= 0.0) return;` fires
- **No order is placed**

### Known Safe Facts (from B16)

- `ChartPanel.MaxValue` and `ChartPanel.MinValue` compile clean in NT8 (NT8-039/040 NOT added)
- `LinearYToPrice()` and `AlignToTick()` are correct pure-math helpers — no bug there
- `FindVisualChild<T>` in `TradeCopierAddOn.cs` is DFS first-match — do NOT modify it

### B17 Goal

- **T1**: Diagnose the visual tree to confirm the panel count and discover whether `ChartControl.Charts` is accessible. Add interim fallback so click trader fires at Last.Price while T2 is under investigation.
- **T2**: Replace the DFS-first-match call with a path that reliably reaches the price-canvas `ChartPanel`.

---

## §B Ticket 1 Design

### T1 Overview

**File:** `TradeCopierPanel.cs` ONLY (plus `NT8_ADDON_KNOWLEDGE.md`)
**Do NOT touch:** `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs`

### B.1 New Field

```csharp
// B17 T1 -- diagnostic fire-once flag (JS-023: volatile, UI-thread flag)
private volatile bool _b17DiagDone = false;
```

- **Placement:** in the class field block after existing B9 T2 click trader fields (near `_clickArmed`)
- **JS-023:** declared `volatile bool` — written once on UI thread, read once on UI thread; volatile is technically not required for thread-safety here but is mandated by the spec for compliance consistency

### B.2 New Methods: `EnumerateAllChartPanels` + `ProbeChartsProperty`

The reflection probe inner loop (branches for `countProp != null`, `count > 0`, `itemProp != null`, `el != null`) is extracted into a dedicated `ProbeChartsProperty` helper. This keeps both methods at CYC ≤ 6.

#### B.2.1 `EnumerateAllChartPanels`

```csharp
// B17 T1: Walk full visual tree under cc, collect ALL ChartPanel instances.
// Delegates Charts reflection probe to ProbeChartsProperty.
// Fire-once via _b17DiagDone. CYC=4.
private void EnumerateAllChartPanels(ChartControl cc)
```

**Algorithm (iterative DFS using `Stack<DependencyObject>`):**

```
1. if (cc == null) return;                              // guard (1)
2. if (_b17DiagDone) return;                            // guard (2): fire-once
3. _b17DiagDone = true;
4. var sb = new System.Text.StringBuilder();
5. Push cc onto stack.
6. while (stack.Count > 0):                             // branch (3)
     node = stack.Pop()
     if (node is ChartPanel cp):                        // branch (4)
         sb.AppendLine("B17 ChartPanel[N]: W=" + cp.ActualWidth.ToString("F2") +
                       " H=" + cp.ActualHeight.ToString("F2") +
                       " Max=" + cp.MaxValue.ToString("F2") +
                       " Min=" + cp.MinValue.ToString("F2"))
     push all VisualTreeHelper.GetChild(node, i) children
7. // Delegate reflection probe to helper (no branch added here)
   ProbeChartsProperty(cc, sb);
8. MessageBox.Show(sb.ToString(), "B17 Diag")
```

**CYC count:**
1. `cc == null` — branch 1
2. `_b17DiagDone` — branch 2
3. `while (stack.Count > 0)` — branch 3
4. `node is ChartPanel cp` — branch 4

**CYC = 4 ≤ 6. PASS.**

#### B.2.2 `ProbeChartsProperty` (extracted helper)

```csharp
// B17 T1: Probe ChartControl.Charts via Reflection; append results to sb.
// Called once by EnumerateAllChartPanels. CYC=6.
private static void ProbeChartsProperty(ChartControl cc, StringBuilder sb)
```

**Algorithm:**

```
1. var chartsProp = cc.GetType().GetProperty("Charts");
   if (chartsProp == null):                             // branch (1)
       sb.AppendLine("Charts property: NOT FOUND")
       return;
2. sb.AppendLine("Charts property: " + chartsProp.PropertyType.FullName)
   var charts = chartsProp.GetValue(cc);
   if (charts == null):                                 // branch (2)
       sb.AppendLine("  Charts value: null")
       return;
3. var countProp = charts.GetType().GetProperty("Count");
   int count = countProp != null                        // branch (3): ternary
               ? (int)countProp.GetValue(charts)
               : -1;
   sb.AppendLine("  Charts.Count=" + count)
4. if (count > 0):                                      // branch (4)
       var itemProp = charts.GetType().GetProperty("Item");
       if (itemProp != null):                           // branch (5)
           var el = itemProp.GetValue(charts, new object[] { 0 });
           if (el != null):                             // branch (6)
               sb.AppendLine("  Charts[0].GetType()=" + el.GetType().FullName)
```

**CYC count:**
1. `chartsProp == null` — branch 1
2. `charts == null` — branch 2
3. `countProp != null` (ternary) — branch 3
4. `count > 0` — branch 4
5. `itemProp != null` — branch 5
6. `el != null` — branch 6

**CYC = 6 ≤ 6. PASS.**

**Required using directive added to TradeCopierPanel.cs in T1:**
```csharp
using System.Reflection;
using System.Text;
```

(`System.Text` for `StringBuilder`; `System.Reflection` for `GetProperty/GetValue`.)

### B.3 EnumerateAllChartPanels Call Site in OnChartMouseDown

Insert after the four guard returns and before the `GetPriceAtY` call:

```csharp
// B17 T1: diagnostic -- shows all ChartPanels + Charts probe; fires once
EnumerateAllChartPanels(chartControl);
```

**Placement in [`OnChartMouseDown`](src/PropTraderTools/TradeCopierPanel.cs:1155):**

```
guard (1)  if (!_clickArmed) return;
guard (2)  if (_leaderAccount == null) return;
guard (3)  if (_instrument == null) return;
guard (4)  if (chartControl == null) return;
→ NEW:     EnumerateAllChartPanels(chartControl);   // B17 T1 diagnostic
           Point mousePos = e.GetPosition(chartControl);
           double rawPrice = GetPriceAtY(chartControl, mousePos.Y, _instrument);
→ NEW:     if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // B17 T1 interim fallback
           if (rawPrice <= 0.0) return;             // guard (5): still exits if no data at all
           ...rest unchanged...
```

### B.4 Interim Fallback in OnChartMouseDown

Insert ONE line immediately after the `GetPriceAtY` call (before the existing guard):

```csharp
double rawPrice = GetPriceAtY(chartControl, mousePos.Y, _instrument);
if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // B17 T1 interim: Last.Price while panel fix is in T2
if (rawPrice <= 0.0) return;                     // guard (5): no valid price (instrument has no data)
```

`GetRefPrice()` is already implemented in B13 T1 — returns `_instrument.MarketData.Last.Price`. No changes to `GetRefPrice()`.

**Modified OnChartMouseDown CYC analysis:**
- guard (1) !_clickArmed
- guard (2) leaderAccount null
- guard (3) instrument null
- guard (4) chartControl null
- rawPrice <= 0 → GetRefPrice branch
- rawPrice <= 0 → return
- try/catch
= **CYC = 7 ≤ 8. PASS.**

### B.5 NT8_ADDON_KNOWLEDGE.md Update (T1)

Add section at end of file:

```markdown
## B17 T1 Discoveries

### Visual Tree Dump (F5 Sim101 output)
[ENGINEER: paste MessageBox content here after F5 run]

### ChartControl.Charts Probe Result
[ENGINEER: record "Charts property: NOT FOUND" or full type name]

### Interim Fallback Confirmed
[ENGINEER: confirm limit order fires at Last.Price after armed click]

### T2 Recommendation
[ENGINEER: Based on Charts probe — select Option B (compile) / Option B (reflect) / Option A]
```

### B.6 T1 Success Criterion

After F5 in NT8 Sim101:
1. MessageBox appears showing ALL `ChartPanel` instances with `ActualWidth`, `ActualHeight`, `MaxValue`, `MinValue`
2. Engineer can identify which panel has `MaxValue > 0` (the price canvas)
3. `Charts property:` line shows NOT FOUND or a type name
4. After dismissing MessageBox: arm click trader, click chart → limit order fires at `Last.Price`
5. `NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries` section filled in by engineer

---

## §C Ticket 2 Design

### T2 Overview

**Files:** `TradeCopierPanel.cs`, `CopyEngineTests.cs`, `NT8_ADDON_KNOWLEDGE.md`
**Do NOT touch:** `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs`

**BLOCKED:** T2 must not begin until T1 F5 output is recorded in `NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries`.

### C.1 T1 Diagnostic Code Removal

Remove in T2 (undo all T1 additions):

1. `private volatile bool _b17DiagDone = false;` — field removed
2. `private void EnumerateAllChartPanels(ChartControl cc)` — method removed
3. `EnumerateAllChartPanels(chartControl);` call in `OnChartMouseDown` — line removed
4. `if (rawPrice <= 0.0) rawPrice = GetRefPrice();` fallback line in `OnChartMouseDown` — line removed
5. `using System.Reflection;` and `using System.Text;` — directives removed (if not needed by any remaining code)

### C.2 Branch Decision Tree (T2 engineer reads T1 F5 output)

```
T1 F5 output → Charts probe result?
├─ "Charts property: NOT FOUND"
│   └─ Use OPTION A: FindPriceCanvasPanel heuristic
│
├─ "Charts property: [some type name]"
│   └─ Charts value == null?
│       ├─ YES → Use OPTION A (Charts exists but empty/null at runtime)
│       └─ NO → Charts.Count > 0?
│           ├─ NO  → Use OPTION A
│           └─ YES → Try OPTION B-compile first
│               ├─ cc.Charts compiles (no CS1061) → Use OPTION B-compile
│               └─ CS1061 → Use OPTION A
```

**Default: OPTION A is always the safe fallback.** The engineer should attempt Option B-compile only if T1 confirmed Charts is accessible and has items.

### C.3 Option B-compile: Direct ChartControl.Charts Access

If `ChartControl.Charts` compiles as a typed property in NT8:

```csharp
// B17 T2 Option B: Use ChartControl.Charts to get price-canvas panel directly.
// T1 F5 confirmed Charts property accessible with Count > 0.
// Charts[0] is assumed to be the primary price-canvas chart.
private static ChartPanel FindPriceCanvasPanelViaCharts(ChartControl cc)
{
    if (cc == null) return null;                               // guard (1)
    var charts = cc.Charts;
    if (charts == null || charts.Count == 0) return null;     // guard (2)
    // Iterate charts: return first whose ChartPanel has MaxValue > 0
    foreach (var chart in charts)                              // branch (3)
    {
        var panel = chart.ChartPanel;                         // actual property confirmed in T1
        if (panel == null) continue;                          // branch (4)
        if (panel.MaxValue > 0) return panel;                 // branch (5)
    }
    return null;
}
```

**CYC = 5 ≤ 8. PASS.**

NOTE: The exact property name on each `chart` element for `ChartPanel` must be confirmed from T1 F5 output (engineer records `Charts[0].GetType()` and then uses LSP hover to inspect its properties).

### C.4 Option A: FindPriceCanvasPanel Heuristic (Fallback)

Iterative DFS, accumulate the ChartPanel with `MaxValue > 0` AND largest `ActualWidth`:

```csharp
// B17 T2 Option A: Walk full visual tree under root; return ChartPanel with
// MaxValue > 0 and largest ActualWidth. This reliably selects the price canvas
// panel rather than the narrow ChartTrader sidebar (Width~139) which has MaxValue=0.
// CYC=5.
private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
{
    if (root == null) return null;                             // guard (1)
    ChartPanel best     = null;
    double     bestW    = 0.0;
    var        stack    = new Stack<DependencyObject>();
    stack.Push(root);
    while (stack.Count > 0)                                    // branch (2)
    {
        var node = stack.Pop();
        var cp = node as ChartPanel;
        if (cp != null && cp.MaxValue > 0 && cp.ActualWidth > bestW)  // branch (3)
        {
            best  = cp;
            bestW = cp.ActualWidth;
        }
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++)                            // branch (4)
        {
            var child = VisualTreeHelper.GetChild(node, i) as DependencyObject;
            if (child != null) stack.Push(child);              // branch (5)
        }
    }
    return best;
}
```

**CYC = 5 ≤ 8. PASS.**

Predicate: `cp.MaxValue > 0 AND cp.ActualWidth > bestW` — eliminates the sidebar panel (`MaxValue=0`) and selects the widest qualifying panel (the price canvas).

### C.5 Modified GetPriceAtY in T2

Replace the first interior line of `GetPriceAtY`:

**Before (B16 / T1 unchanged):**
```csharp
var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
```

**After (T2 Option A):**
```csharp
var panel = FindPriceCanvasPanel(cc);    // B17 T2: heuristic returns widest panel with MaxValue>0
```

**After (T2 Option B-compile):**
```csharp
var panel = FindPriceCanvasPanelViaCharts(cc);    // B17 T2: native Charts collection path
```

All existing guards remain unchanged. CYC of `GetPriceAtY` stays at 5.

### C.6 xUnit Tests (≥4 [Fact] names in CopyEngineTests.cs)

All tests exercise `TradeCopierPanel.LinearYToPrice` and `TradeCopierPanel.AlignToTick` (internal static, already accessible from test project as established in B16).

Minimum 4 required; 7 recommended:

| Test Name | Asserts |
|-----------|---------|
| `T_B17_01_LinearYToPrice_TopOfPanel_ReturnsMaxVal` | y=0, panelH=452, max=5023.25, min=4987.50, cf=1.0 → 5023.25 |
| `T_B17_02_LinearYToPrice_MiddleOfPanel_ReturnsMidpointPrice` | y=226, same params → approx 5005.375 |
| `T_B17_03_LinearYToPrice_ZeroPanelHeight_ReturnsZero` | panelH=0 → 0.0 |
| `T_B17_04_LinearYToPrice_OverBoundary_ReturnsZero` | y > panelH → raw < minVal → raw <= 0 → 0.0 |
| `T_B17_05_AlignToTick_AlreadyAligned_Unchanged` | raw=5023.25, tick=0.25 → 5023.25 |
| `T_B17_06_AlignToTick_HalfTickRoundsAwayFromZero` | raw=5023.125, tick=0.25 → 5023.25 (rounds up) |
| `T_B17_07_AlignToTick_ZeroTickSize_ReturnsRaw` | tick=0.0 → raw unchanged |

**Contract: engineer must implement AT LEAST T_B17_01 through T_B17_04 (4 tests).** Tests T_B17_05 through T_B17_07 are recommended for full path coverage.

### C.7 NT8_ADDON_KNOWLEDGE.md Update (T2)

Add section:

```markdown
## B17 T2 Discoveries

### Panel Fix Applied
Option: [A / B-compile] (based on T1 F5 output)

### GetPriceAtY Corrected Panel Selection
[ENGINEER: describe which path was used and confirm orders place at correct Y price]

### NT8 Rules Update
[ENGINEER: state "nt8-rules B17-T2: no new rules" or list new rules]
```

### C.8 T2 Success Criterion

F5 in NT8 Sim101, arm click trader, click chart at a specific price level:
1. Limit order placed at the **exact tick-aligned price corresponding to the Y pixel clicked**
2. No diagnostic MessageBox
3. No interim fallback active (GetRefPrice not used in click trader path)
4. All ≥4 T_B17 [Fact] tests pass
5. `NT8_ADDON_KNOWLEDGE.md ## B17 T2 Discoveries` filled in

---

## §D Constraints & Scan Checklist

### 7-Scan Checklist (engineer contract per ticket)

| Scan | Rule | Pattern | Expected Result |
|------|------|---------|-----------------|
| SCAN-01 | JS-021 | `lock\s*\(` in new/modified code | Zero matches |
| SCAN-02 | JS-023 | `_b17DiagDone` declared without `volatile` | Must have `volatile` keyword (T1); field removed (T2) |
| SCAN-03 | NT8-003 | `volatile double` | Zero matches |
| SCAN-04 | NT8-034 | `Math\.Clamp` | Zero matches |
| SCAN-05 | CYC ≤ 8 | all new/modified methods | EnumerateAllChartPanels≤4, ProbeChartsProperty≤6, FindPriceCanvasPanel≤5, OnChartMouseDown≤7, GetPriceAtY≤5 |
| SCAN-06 | NT8-014 | `CreateOrder` 9th arg | Must start with "PTT-" |
| SCAN-07 | JS-033 | `async void` | Zero matches in new/modified methods |

Additional NT8 scan for T1:
| SCAN-08 | NT8-028 | hex string literals `"#[0-9A-Fa-f]{6}"` | Zero matches |
| SCAN-09 | NT8-013 | `DateTime\.Now` | Zero matches |

---

## §E Success Criteria

### T1 F5 Gate (engineer must record before starting T2)

```
[ ] F5 green (no compilation errors)
[ ] MessageBox fires on first armed chart click
[ ] MessageBox text contains at least 2 ChartPanel entries (sidebar W~139 + canvas W~1047)
[ ] MessageBox text contains "Charts property:" line
[ ] Limit order fires at a price ~= Last.Price (interim fallback working)
[ ] NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries filled in
```

### T2 F5 Gate

```
[ ] F5 green (no compilation errors)
[ ] No MessageBox (diagnostic removed)
[ ] Armed click at chart Y-pixel for price $5020.00 → order placed at $5020.00 (tick-aligned)
[ ] Armed click at chart Y-pixel for price $5015.25 → order placed at $5015.25
[ ] All T_B17_01 through T_B17_04 [Fact] tests pass
[ ] NT8_ADDON_KNOWLEDGE.md ## B17 T2 Discoveries filled in
[ ] "nt8-rules B17-T1: [summary]" stated in completion report
[ ] "nt8-rules B17-T2: [summary]" stated in completion report
```

---

## §F Method Signature Contracts

All signatures are exact. No deviation permitted.

### T1 New Methods

```csharp
// TradeCopierPanel.cs — T1 additions

// Field:
private volatile bool _b17DiagDone = false;

// Methods:
private void EnumerateAllChartPanels(ChartControl cc)
// Visibility: private (instance method, accesses _b17DiagDone instance field)
// Thread: UI thread only (called from OnChartMouseDown)
// Side effects: sets _b17DiagDone = true, calls ProbeChartsProperty, shows MessageBox once
// CYC = 4

private static void ProbeChartsProperty(ChartControl cc, StringBuilder sb)
// Visibility: private static (no instance state accessed)
// Thread: UI thread only (called by EnumerateAllChartPanels)
// Side effects: appends reflection probe results to sb
// CYC = 6
```

### T1 Modified Methods

```csharp
// OnChartMouseDown — modified call site (T1)
// Add EnumerateAllChartPanels(chartControl) call after guard (4), before GetPriceAtY
// Add: if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // after GetPriceAtY line
// Modified CYC = 7
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
```

### T2 New Methods (Option A — expected path)

```csharp
// TradeCopierPanel.cs — T2 Option A addition
private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
// Returns: ChartPanel with MaxValue > 0 and largest ActualWidth, or null if none qualifies
// Thread: UI thread only
// CYC = 5

// T2 Option B-compile alternative (only if T1 confirms cc.Charts compiles):
private static ChartPanel FindPriceCanvasPanelViaCharts(ChartControl cc)
// Returns: first ChartPanel in cc.Charts where panel.MaxValue > 0, or null
// Thread: UI thread only
// CYC = 5
```

### T2 Modified Methods

```csharp
// GetPriceAtY — modified panel-finding line only; all guards and math unchanged
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
// CYC = 5 (unchanged)

// LinearYToPrice — UNCHANGED (no modification in T2)
internal static double LinearYToPrice(
    double y, double panelH, double maxVal, double minVal, double correctionFactor)

// AlignToTick — UNCHANGED (no modification in T2)
internal static double AlignToTick(double raw, double tickSize)

// OnChartMouseDown — T1 additions removed; method restored to clean state
// CYC = 6 (restored; EnumerateAllChartPanels call gone, fallback line gone)
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
```

---

## §G CYC Analysis

| Method | Ticket | Branches | CYC | Bound |
|--------|--------|----------|-----|-------|
| `EnumerateAllChartPanels` | T1 new | cc null(1), _b17DiagDone(2), stack loop(3), type check(4) | 4 | ≤6 OK |
| `ProbeChartsProperty` | T1 new (extracted) | chartsProp null(1), charts null(2), countProp ternary(3), count>0(4), itemProp null(5), el null(6) | 6 | ≤6 OK |
| `OnChartMouseDown` | T1 modified | !_clickArmed(1), leaderAccount null(2), instrument null(3), chartControl null(4), rawPrice<=0 fallback(5), rawPrice<=0 return(6), try/catch(7) | 7 | ≤8 OK |
| `GetPriceAtY` | T2 modified (single-line change) | cc null(1), panel null(2), height<=0(3), raw<=0(4), instrument null(5) | 5 | ≤8 OK |
| `FindPriceCanvasPanel` | T2 Option A new | root null(1), stack loop(2), type+predicate check(3), child loop(4), child null check(5) | 5 | ≤8 OK |
| `FindPriceCanvasPanelViaCharts` | T2 Option B new | cc null(1), charts null/empty(2), foreach(3), panel null(4), MaxValue>0(5) | 5 | ≤8 OK |
| `OnChartMouseDown` | T2 (T1 removed) | !_clickArmed(1), leaderAccount null(2), instrument null(3), chartControl null(4), rawPrice<=0 return(5), try/catch(6) | 6 | ≤8 OK |

---

## §H xUnit Test Names for T2

**File:** `CopyEngineTests.cs`
**Minimum:** 4 tests (T_B17_01 through T_B17_04)
**Recommended:** 7 tests (T_B17_01 through T_B17_07)

```csharp
[Fact]
public void T_B17_01_LinearYToPrice_TopOfPanel_ReturnsMaxVal()
// y=0, panelH=452, max=5023.25, min=4987.50, cf=1.0
// Expected: 5023.25 (top of panel = maxValue)

[Fact]
public void T_B17_02_LinearYToPrice_MiddleOfPanel_ReturnsMidpointPrice()
// y=226, panelH=452, max=5023.25, min=4987.50, cf=1.0
// Expected: 5005.375 (midpoint of price range)

[Fact]
public void T_B17_03_LinearYToPrice_ZeroPanelHeight_ReturnsZero()
// panelH=0, any y/max/min/cf
// Expected: 0.0 (panelH guard fires)

[Fact]
public void T_B17_04_LinearYToPrice_OverBoundary_ReturnsZero()
// y=1000, panelH=452, max=5023.25, min=4987.50, cf=1.0
// rawPrice = 5023.25 - (1000/452)*(35.75) = 5023.25 - 79.09 = 4944.16... 
// Wait: rawPrice=4944 > 0. Need y large enough that raw <= 0.
// Use y=panelH * (max / (max - min)) + epsilon -- simpler: max=10, min=5, panelH=100, y=300
// rawPrice = 10 - (300/100)*(5) = 10 - 15 = -5 <= 0 → returns 0.0
// Expected: 0.0 (raw <= 0 guard fires)

[Fact]
public void T_B17_05_AlignToTick_AlreadyAligned_Unchanged()
// raw=5023.25, tick=0.25 → Expected: 5023.25

[Fact]
public void T_B17_06_AlignToTick_HalfTickRoundsAwayFromZero()
// raw=5023.125, tick=0.25 → Math.Round(5023.125/0.25, AwayFromZero)*0.25
// = Math.Round(20092.5, AwayFromZero)*0.25 = 20093 * 0.25 = 5023.25
// Expected: 5023.25

[Fact]
public void T_B17_07_AlignToTick_ZeroTickSize_ReturnsRaw()
// tick=0.0, raw=5023.25 → tickSize guard fires → Expected: 5023.25 unchanged
```

---

## §I NT8 Rules Applied

| Rule | Applies To | Constraint |
|------|-----------|------------|
| NT8-003 | All new fields | No `volatile double` — all new fields are `volatile bool` or plain types |
| NT8-034 | All new methods | No `Math.Clamp` — use `Math.Max(Math.Min(...))` if clamping needed (not needed here) |
| NT8-009 | GetPriceAtY | `ChartControl.GetValueByY()` absent — confirmed. Not used. |
| NT8-037 | GetPriceAtY | `ChartPanel.GetValueByY()` absent — confirmed. Not used. |
| NT8-036 | TradeCopierPanel | `ChartControl.ChartBars` absent. Not used. |
| NT8-008 | TradeCopierAddOn | `Chart.ChartControl` absent. Not used. |
| NT8-013 | OnChartMouseDown | `DateTime.MaxValue` used for GTC. Not changed. |
| NT8-014 | OnChartMouseDown | `"PTT-Click"` signal name preserved. Not changed. |
| NT8-007 | OnChartMouseDown | `(NinjaTrader.Cbi.CustomOrder)null` arg 11 preserved. Not changed. |
| NT8-017/18 | Field declarations | `volatile bool _b17DiagDone`: field written on UI thread; `volatile` added per spec. `lock()` absent. |

**NT8 Rules Gate Result: PASS — no P0 violations planned.**

---

## §J Files Touched

### Ticket 1

| File | Location | Change Type |
|------|----------|-------------|
| `TradeCopierPanel.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Add field `_b17DiagDone`, add `using System.Reflection`, add `using System.Text`, add `EnumerateAllChartPanels()` method, add `ProbeChartsProperty()` method, modify `OnChartMouseDown` (2 lines added) |
| `NT8_ADDON_KNOWLEDGE.md` | `docs/standards/` | Add `## B17 T1 Discoveries` section with placeholder text |

### Ticket 2

| File | Location | Change Type |
|------|----------|-------------|
| `TradeCopierPanel.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Remove T1 diagnostic (field + method + 2 OnChartMouseDown lines + 2 using directives if unused), add `FindPriceCanvasPanel` (Option A) or `FindPriceCanvasPanelViaCharts` (Option B), modify `GetPriceAtY` (one line changed) |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Add ≥4 [Fact] test methods (T_B17_01 through T_B17_04 minimum) |
| `NT8_ADDON_KNOWLEDGE.md` | `docs/standards/` | Update `## B17 T1 Discoveries` with actual F5 values, add `## B17 T2 Discoveries` section |

### Files NOT Touched (confirmed)

- `CopyEngine.cs` — no changes
- `TradeCopierAddOn.cs` — `FindVisualChild<T>` left as-is; not called in T2 click trader path
- `TradeCopierWindow.cs` — no changes
- `AtrSizingEngine.cs` — no changes

---

## §K Deferred Items

### §K.1 Items Opened in B17

| ID | Title | Status | Ticket |
|----|-------|--------|--------|
| DW-B17-01 | Click trader wrong panel selection — ChartTrader sidebar DFS-first-match bug | OPEN (T1+T2) | T1 diagnostic + T2 fix |

### §K.2 Items Carried Forward Open (READ-ONLY from B16)

| ID | Title | Priority | Source | Notes |
|----|-------|----------|--------|-------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B9 | Shelved continuously since B9. No work performed in B10-B17. |
| DW-B9-03 | Click trader Bid+1/Ask-1 spread auto-offset | P3 | B9 | SHELVED per Director decision (B17 brief). Fully unblocked by DW-B16-01 CLOSE. Eligible for scheduling. |
| DW-B12-DEFER-01 (orig) | Buy Ask/Sell Bid full-panel mode expansion | P2 | B12 | Shelved continuously since B12. No blocking dependency. Requires UX spec before implementation. |

### §K.3 Items Closed in B16 (for reference)

| ID | Title | Closed In |
|----|-------|-----------|
| DW-B16-01 | Click trader Y-pixel-to-price lookup (B16 Branch B) | B16 T2 VERIFY_PASS |
| DW-B16-02 | TightenOneStop cancel+replace kills native ATM bracket | B16 T2 VERIFY_PASS |

---

## Appendix: B17 Diagnostic Format Reference

The MessageBox produced by `EnumerateAllChartPanels` should look like:

```
B17 ChartPanel[0]: W=139.33 H=452.00 Max=0.00 Min=0.00
B17 ChartPanel[1]: W=1047.20 H=452.00 Max=5023.25 Min=4987.50
Charts property: System.Collections.ObjectModel.ObservableCollection`1[...]
  Charts.Count=1
  Charts[0].GetType()=NinjaTrader.Gui.Chart.NinjaChart
```

OR (if Charts absent):

```
B17 ChartPanel[0]: W=139.33 H=452.00 Max=0.00 Min=0.00
B17 ChartPanel[1]: W=1047.20 H=452.00 Max=5023.25 Min=4987.50
Charts property: NOT FOUND
```

Engineer records the actual text verbatim in `NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries`.

---

*End of B17 Architecture Plan — ptt-architect*
