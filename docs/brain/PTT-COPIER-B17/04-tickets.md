# PTT-COPIER-B17 Implementation Tickets
# Status: TICKETS_COMPLETE
# Block: PTT-COPIER-B17
# Date: 2026-07-15
# Author: ptt-architect
# Plan: docs/brain/PTT-COPIER-B17/02-architecture-plan.md (REVIEW_PASS — Cycle 2 of 2)

---

## Spec Requirement

**DW-B17-01** — Click trader Y-pixel-to-price returns rawPrice=0 because
`GetPriceAtY` calls `TradeCopierAddOn.FindVisualChild<ChartPanel>(cc)` which
returns the first matching ChartPanel via DFS — the ChartTrader sidebar
(Width~139, MaxValue=0, MinValue=0) — not the price canvas
(Width~1047, MaxValue>0).

Root cause confirmed in §A of plan. Two-ticket fix:
- T1: diagnose the visual tree + interim fallback
- T2: permanent panel-selection fix + tests

---

## Ticket 1 — B17-T1 Diagnostic + Interim Fallback

### Overview

| Field | Value |
|-------|-------|
| **Title** | B17-T1 Diagnostic + Interim Fallback |
| **Spec Req** | DW-B17-01 (P1 blocker) |
| **Files to MODIFY** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| **Files to UPDATE** | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` |
| **BANNED FILES** (do NOT touch) | `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs` |
| **Blocked by** | Nothing — T1 is the first ticket |
| **Unblocks** | T2 (requires T1 F5 output recorded in NT8_ADDON_KNOWLEDGE.md) |

---

### Step-by-Step Implementation

#### Step 1 — Add `using` directives

At the top of [`TradeCopierPanel.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:101),
confirm or add (after the existing `using NinjaTrader.Gui.Chart;` line):

```csharp
using System.Reflection;
using System.Text;
```

Check first: if `using System.Text;` is already present from a prior block, do not
add a duplicate. `System.Reflection` is required for `GetProperty`/`GetValue` calls in
`ProbeChartsProperty`. `System.Text` is required for `StringBuilder` in
`EnumerateAllChartPanels`. Both assemblies are part of .NET Framework 4.8 (always present
in NT8 host process — no NuGet reference required).

#### Step 2 — Add `_b17DiagDone` field

In the field declarations block of [`TradeCopierPanel.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:135),
after the B9 T2 click trader volatile fields block (near `_clickArmed`, `_clickBuy` at
lines 136–137), add:

```csharp
// B17 T1 -- diagnostic gate: fires EnumerateAllChartPanels once only per session (JS-023: volatile)
private volatile bool _b17DiagDone = false;
```

**Placement constraint:** This field must be `volatile bool`. `volatile double` is banned
(NT8-003). The field is written once on the UI thread and read once on the UI thread;
`volatile` is added for spec compliance consistency with JS-023 (cross-thread flag pattern).

#### Step 3 — Add `ProbeChartsProperty` method

Add the following `private static void` method inside the `TradeCopierPanel` class,
after the `GetPriceAtY` / `LinearYToPrice` / `AlignToTick` method block
(around line 340 of the current file):

```csharp
// B17 T1: Probe ChartControl.Charts via Reflection; append results to sb.
// Called once by EnumerateAllChartPanels. CYC=6.
// No lock(), no async void, no return null (all paths append to sb then return void).
private static void ProbeChartsProperty(ChartControl cc, StringBuilder sb)
{
    var chartsProp = cc.GetType().GetProperty("Charts");
    if (chartsProp == null)                                        // branch (1)
    {
        sb.AppendLine("Charts property: NOT FOUND");
        return;
    }
    sb.AppendLine("Charts property: " + chartsProp.PropertyType.FullName);
    object charts = null;
    try { charts = chartsProp.GetValue(cc); } catch { /* reflection may throw */ }
    if (charts == null)                                            // branch (2)
    {
        sb.AppendLine("  Charts value: null");
        return;
    }
    var countProp = charts.GetType().GetProperty("Count");
    int count = countProp != null                                  // branch (3): ternary
        ? (int)countProp.GetValue(charts)
        : -1;
    sb.AppendLine("  Charts.Count=" + count);
    if (count > 0)                                                 // branch (4)
    {
        var itemProp = charts.GetType().GetProperty("Item");
        if (itemProp != null)                                      // branch (5)
        {
            object el = null;
            try { el = itemProp.GetValue(charts, new object[] { 0 }); } catch { /* may throw */ }
            if (el != null)                                        // branch (6)
                sb.AppendLine("  Charts[0].GetType()=" + el.GetType().FullName);
        }
    }
}
```

**CYC analysis:** branches (1)...(6) = CYC 6 ≤ 6. PASS.
**NT8 constraints:** no `Math.Clamp` (NT8-034), no `volatile double` (NT8-003),
no `DateTime.Now` (NT8-013), no `lock()` (JS-021), no `async void` (JS-033).
All exceptions from `GetValue` are swallowed silently — Reflection may throw in NT8
host; no re-throw in this diagnostic helper.

#### Step 4 — Add `EnumerateAllChartPanels` method

Add the following `private void` method in the same region, immediately after
`ProbeChartsProperty`:

```csharp
// B17 T1: Walk full visual tree under cc; collect ALL ChartPanel instances.
// Delegates Charts reflection probe to ProbeChartsProperty (keeps this method CYC=4).
// Fire-once via _b17DiagDone. Called from OnChartMouseDown on UI thread.
// CYC=4: cc null(1), _b17DiagDone(2), while loop(3), type check(4).
private void EnumerateAllChartPanels(ChartControl cc)
{
    if (cc == null)         return;                                // guard (1)
    if (_b17DiagDone)       return;                                // guard (2): fire-once
    _b17DiagDone = true;

    var sb    = new StringBuilder();
    var stack = new Stack<DependencyObject>();
    stack.Push(cc);
    int panelIndex = 0;

    while (stack.Count > 0)                                        // branch (3): loop
    {
        var node = stack.Pop();
        if (node is ChartPanel cp)                                 // branch (4): type check
        {
            sb.AppendLine(
                "B17 ChartPanel[" + panelIndex + "]:"
                + " W=" + cp.ActualWidth.ToString("F2")
                + " H=" + cp.ActualHeight.ToString("F2")
                + " Max=" + cp.MaxValue.ToString("F2")
                + " Min=" + cp.MinValue.ToString("F2"));
            panelIndex++;
        }
        int childCount = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i) as DependencyObject;
            if (child != null) stack.Push(child);
        }
    }

    ProbeChartsProperty(cc, sb);
    MessageBox.Show(sb.ToString(), "B17 Diag");
}
```

**CYC analysis:** branches (1) cc null, (2) _b17DiagDone, (3) while loop, (4) type check
= CYC 4 ≤ 6. PASS. (The inner `for` loop and `child != null` check are within the while
body; they do not exceed the bound. Plan reviewer confirmed CYC 4–5 either way: PASS.)
**Placement:** `private void` (instance method, must access `_b17DiagDone` field).

#### Step 5 — Wire diagnostic + interim fallback into `OnChartMouseDown`

Locate [`OnChartMouseDown`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1155).
The current method reads (lines 1155–1195):

```
guard (1)  if (!_clickArmed) return;
guard (2)  if (_leaderAccount == null) return;
guard (3)  if (_instrument    == null) return;
var chartControl = sender as ChartControl;
guard (4)  if (chartControl   == null) return;
// existing: capture mousePos, GetPriceAtY, guard rawPrice <= 0, then order logic
```

**Insert ONE line after guard (4)** (after the `if (chartControl == null) return;` line),
before the existing `Point mousePos = e.GetPosition(chartControl);` line:

```csharp
// B17 T1: diagnostic -- enumerate all ChartPanels + Charts probe; fires once via _b17DiagDone
EnumerateAllChartPanels(chartControl);
```

**Insert ONE line after `double rawPrice = GetPriceAtY(...)` call**, before the
existing `if (rawPrice <= 0.0) return;` guard:

```csharp
if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // B17 T1 interim: Last.Price while T2 panel fix is pending
```

The final call sequence in `OnChartMouseDown` becomes:

```csharp
// guard (1)
if (!_clickArmed)           return;
// guard (2)
if (_leaderAccount == null) return;
// guard (3)
if (_instrument    == null) return;
var chartControl = sender as ChartControl;
// guard (4)
if (chartControl   == null) return;

// B17 T1: diagnostic -- enumerate all ChartPanels + Charts probe; fires once via _b17DiagDone
EnumerateAllChartPanels(chartControl);

Point  mousePos  = e.GetPosition(chartControl);
double rawPrice  = GetPriceAtY(chartControl, mousePos.Y, _instrument);
if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // B17 T1 interim: Last.Price while T2 panel fix is pending
if (rawPrice <= 0.0) return;                     // guard (5): no valid price (instrument has no data at all)
// ... rest of method unchanged ...
```

**Modified OnChartMouseDown CYC analysis:**
- (1) `!_clickArmed`
- (2) `_leaderAccount == null`
- (3) `_instrument == null`
- (4) `chartControl == null`
- (5) `rawPrice <= 0.0` → `GetRefPrice()` fallback
- (6) `rawPrice <= 0.0` → `return`
- (7) `try/catch` (catch does not add CYC in most tools but counts as 1 branch in Lizard)

**CYC = 7 ≤ 8. PASS.**

`GetRefPrice()` is already implemented (B13 T1); it returns
`_instrument.MarketData.Last.Price`. No changes required to `GetRefPrice()`.

#### Step 6 — Update `NT8_ADDON_KNOWLEDGE.md`

Append the following section at the **end** of
`c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md`:

```markdown
## B17 T1 Discoveries

### Visual Tree Dump (F5 Sim101 output)
[ENGINEER: paste the exact MessageBox content here after F5 run]
Expected format:
B17 ChartPanel[0]: W=139.33 H=452.00 Max=0.00 Min=0.00
B17 ChartPanel[1]: W=1047.20 H=452.00 Max=5023.25 Min=4987.50
Charts property: ...

### ChartControl.Charts Probe Result
[ENGINEER: record "Charts property: NOT FOUND" or the full type name shown]

### Interim Fallback Confirmed
[ENGINEER: confirm limit order fires at Last.Price after armed click (rawPrice = GetRefPrice() path)]

### T2 Recommendation
[ENGINEER: Based on Charts probe -- select Option B-compile (if cc.Charts accessible) / Option A (heuristic fallback)]
```

---

### Method Signatures (exact — no deviation)

```csharp
// New field (TradeCopierPanel.cs):
private volatile bool _b17DiagDone = false;

// New method 1 (TradeCopierPanel.cs):
private void EnumerateAllChartPanels(ChartControl cc)
// Visibility: private instance (accesses _b17DiagDone instance field)
// Thread: UI thread only (called from OnChartMouseDown)
// Side effects: sets _b17DiagDone=true, calls ProbeChartsProperty, shows MessageBox once
// CYC = 4

// New method 2 (TradeCopierPanel.cs):
private static void ProbeChartsProperty(ChartControl cc, StringBuilder sb)
// Visibility: private static (no instance state)
// Thread: UI thread only (called by EnumerateAllChartPanels)
// Side effects: appends reflection probe results to sb
// CYC = 6

// Modified method (TradeCopierPanel.cs):
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
// Added: EnumerateAllChartPanels(chartControl) call after guard (4), before GetPriceAtY
// Added: if (rawPrice <= 0.0) rawPrice = GetRefPrice(); after GetPriceAtY call
// CYC = 7 (was 6 before T1 additions)
```

---

### CYC Bounds (T1)

| Method | Branches | CYC | Bound | Result |
|--------|----------|-----|-------|--------|
| `EnumerateAllChartPanels` | cc null(1), _b17DiagDone(2), while(3), type check(4) | 4 | ≤ 6 | PASS |
| `ProbeChartsProperty` | chartsProp null(1), charts null(2), countProp ternary(3), count>0(4), itemProp null(5), el null(6) | 6 | ≤ 6 | PASS |
| `OnChartMouseDown` | !_clickArmed(1), leaderAccount null(2), instrument null(3), chartControl null(4), rawPrice<=0 GetRefPrice(5), rawPrice<=0 return(6), try/catch(7) | 7 | ≤ 8 | PASS |

---

### 7-Scan Checklist — T1 (MANDATORY, run before marking complete)

```
Scan 1 — JS-021 lock():
  grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 2 — JS-033 async void:
  grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 3 — JS-002 return null:
  grep -rn "return null;" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  0 NEW instances (existing return 0.0 guards are unchanged; new methods use void/void return)

Scan 4 — NT8-003 volatile double:
  grep -rn "volatile double" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results  (new field is volatile bool, not volatile double)

Scan 5 — NT8-034 Math.Clamp:
  grep -rn "Math.Clamp" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 6 — CYC audit:
  python scripts/complexity_audit.py src/PropTraderTools/TradeCopierPanel.cs
  All methods must report ≤ 8
  Key targets: EnumerateAllChartPanels=4, ProbeChartsProperty=6, OnChartMouseDown=7

Scan 7 — Build:
  dotnet build (Wave workspace: c:\WSGTA\universal-or-strategy)
  MUST return: 0 errors, 0 warnings
```

Additional T1 scans:
```
Scan 8 — NT8-028 hex string literals:
  grep -rn "#[0-9A-Fa-f]\{6\}" src/PropTraderTools/TradeCopierPanel.cs
  MUST return 0 results in new code (no hardcoded hex color strings)

Scan 9 — NT8-013 DateTime.Now:
  grep -rn "DateTime\.Now" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results in new code (DateTime.MaxValue for GTC is unchanged and allowed)
```

---

### T1 Success Criterion

After F5 in NT8 Sim101:

```
[ ] F5 green — no compilation errors
[ ] MessageBox fires on FIRST armed chart click (only fires once — _b17DiagDone prevents re-fire)
[ ] MessageBox text contains at least 2 "B17 ChartPanel[N]:" lines
    (Expected: ChartPanel[0] W~139 Max=0, ChartPanel[1] W~1047 Max>0)
[ ] MessageBox text contains a "Charts property:" line
    (Either "Charts property: NOT FOUND" or a type name)
[ ] After dismissing MessageBox: arm click trader, click chart -> limit order fires
    (rawPrice from interim GetRefPrice() fallback gives a non-zero price -> order placed at Last.Price)
[ ] NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries section filled in verbatim by engineer
    (Paste exact MessageBox text; record Charts probe result; confirm order fired)
```

**IMPORTANT:** T2 is BLOCKED until the engineer records the T1 F5 output in
`NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries`. Director will read the output and
confirm whether Option B-compile or Option A is used for T2.

---

---

## Ticket 2 — B17-T2 Permanent Fix: GetPriceAtY Correct Panel Selection

### Overview

| Field | Value |
|-------|-------|
| **Title** | B17-T2 Permanent Fix — GetPriceAtY correct panel selection |
| **Spec Req** | DW-B17-01 (P1 blocker) |
| **Files to MODIFY** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| **Files to MODIFY** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |
| **Files to UPDATE** | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` |
| **BANNED FILES** (do NOT touch) | `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs` |
| **BLOCKED ON** | T1 F5 output recorded in `NT8_ADDON_KNOWLEDGE.md ## B17 T1 Discoveries` |

---

### Branch Decision (read T1 F5 output first)

```
T1 F5 output -> Charts probe result?
├─ "Charts property: NOT FOUND"
│   └─ Use OPTION A (FindPriceCanvasPanel heuristic) — CONCRETE SPEC BELOW
│
├─ "Charts property: [type name]" but Charts value == null OR Charts.Count == 0
│   └─ Use OPTION A
│
├─ "Charts property: [type name]" + Charts.Count > 0 + cc.Charts compiles (no CS1061)
│   └─ Use OPTION B-compile (FindPriceCanvasPanelViaCharts)
│   └─ NOTE: Director will provide updated ticket section if Option B is selected.
│      Default implementation in this ticket is OPTION A.
```

**This ticket is written with Option A as the concrete implementation.**
If T1 F5 shows `ChartControl.Charts` is accessible and compiles, the Director will
confirm and provide the Option B code before T2 begins. Option B replaces only
Step 2 (the new method) and Step 3 (the one-line change in GetPriceAtY).

---

### Step-by-Step Implementation

#### Step 1 — Remove ALL T1 diagnostic code

Remove the following from [`TradeCopierPanel.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs):

1. **Field** — remove entirely:
   ```csharp
   private volatile bool _b17DiagDone = false;
   ```

2. **Method** — remove entirely:
   ```csharp
   private void EnumerateAllChartPanels(ChartControl cc) { ... }
   ```

3. **Method** — remove entirely:
   ```csharp
   private static void ProbeChartsProperty(ChartControl cc, StringBuilder sb) { ... }
   ```

4. **In `OnChartMouseDown`** — remove the call site line:
   ```csharp
   EnumerateAllChartPanels(chartControl);
   ```

5. **In `OnChartMouseDown`** — remove the fallback line:
   ```csharp
   if (rawPrice <= 0.0) rawPrice = GetRefPrice();   // B17 T1 interim: ...
   ```

6. **`using System.Reflection;`** — remove ONLY IF this directive was added in T1
   AND is not used elsewhere in the file. Check with:
   ```
   grep -n "System.Reflection\|\.GetType()\|GetProperty\|GetValue\|GetMethod\|BindingFlags" TradeCopierPanel.cs
   ```
   If the grep returns hits outside of the T1 methods you just removed, keep the
   directive. If the only hits were the T1 methods, remove the directive.

7. **`using System.Text;`** — remove ONLY IF no remaining code references `StringBuilder`
   or other `System.Text` types. Check with:
   ```
   grep -n "StringBuilder\|System\.Text\." TradeCopierPanel.cs
   ```
   Remove only if no remaining hits.

**Verification:** After this step, run:
```
grep -rn "_b17DiagDone\|EnumerateAllChartPanels\|ProbeChartsProperty\|B17 interim" TradeCopierPanel.cs
```
Result MUST be: 0 matches.

#### Step 2 — Add `FindPriceCanvasPanel` (Option A — concrete implementation)

Add the following `private static` method inside `TradeCopierPanel` class,
in the same region as `GetPriceAtY` / `LinearYToPrice` / `AlignToTick`
(around line 340 in the pre-T2 file):

```csharp
// B17 T2 Option A: Walk full visual tree under root; return the ChartPanel with
// MaxValue > 0 and largest ActualWidth. Reliably selects the price canvas panel
// rather than the ChartTrader sidebar (Width~139, MaxValue=0 -- DFS first-match victim).
// CYC=5: root null(1), while loop(2), type+predicate(3), child loop(4), child null(5).
private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
{
    if (root == null) return null;                                 // guard (1)
    ChartPanel best  = null;
    double     bestW = 0.0;
    var        stack = new Stack<DependencyObject>();
    stack.Push(root);

    while (stack.Count > 0)                                        // branch (2): loop
    {
        var node = stack.Pop();
        var cp = node as ChartPanel;
        if (cp != null && cp.MaxValue > 0 && cp.ActualWidth > bestW)  // branch (3): predicate
        {
            best  = cp;
            bestW = cp.ActualWidth;
        }
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++)                                // branch (4): child loop
        {
            var child = VisualTreeHelper.GetChild(node, i) as DependencyObject;
            if (child != null) stack.Push(child);                  // branch (5): null guard
        }
    }
    return best;
}
```

**CYC analysis:** (1) root null, (2) while, (3) predicate, (4) for, (5) child null
= CYC 5 ≤ 8. PASS.

**Predicate rationale:** `cp.MaxValue > 0` eliminates the ChartTrader sidebar (MaxValue=0,
confirmed in §A of plan). `cp.ActualWidth > bestW` picks the widest qualifying panel
(the price canvas at ~1047px vs sidebar at ~139px).

**Option B-compile alternative** (only if Director confirms cc.Charts accessible):
```csharp
// B17 T2 Option B-compile: Use ChartControl.Charts to get price-canvas panel directly.
// Only use if T1 F5 confirmed Charts property accessible with Count > 0.
// CYC=5: cc null(1), charts null/empty(2), foreach(3), panel null(4), MaxValue>0(5).
private static ChartPanel FindPriceCanvasPanelViaCharts(ChartControl cc)
{
    if (cc == null) return null;                                   // guard (1)
    var charts = cc.Charts;
    if (charts == null || charts.Count == 0) return null;         // guard (2)
    foreach (var chart in charts)                                  // branch (3): loop
    {
        var panel = chart.ChartPanel;
        if (panel == null) continue;                               // branch (4)
        if (panel.MaxValue > 0) return panel;                      // branch (5)
    }
    return null;
}
```

NOTE: The exact property name `chart.ChartPanel` must be confirmed from T1 F5 output
(`Charts[0].GetType()` line). Use LSP hover on the confirmed type to inspect its
ChartPanel property name before writing Option B.

#### Step 3 — Replace the panel-finding line in `GetPriceAtY`

Locate [`GetPriceAtY`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:297).
The FIRST interior line (after `if (cc == null) return 0.0;`) currently reads:

```csharp
var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
```

Replace this single line with (Option A):

```csharp
var panel = FindPriceCanvasPanel(cc);    // B17 T2: heuristic selects widest ChartPanel with MaxValue>0
```

All other lines of `GetPriceAtY` remain UNCHANGED. The guard structure and math are
correct — only the panel-finding call changes.

**After change, `GetPriceAtY` reads:**

```csharp
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (cc == null) return 0.0;                                    // guard (1)

    var panel = FindPriceCanvasPanel(cc);    // B17 T2: heuristic selects widest ChartPanel with MaxValue>0
    if (panel == null) return 0.0;                                 // guard (2)

    double panelH = panel.ActualHeight;
    if (panelH <= 0.0) return 0.0;                                 // guard (3): no divide by zero

    const double CORRECTION_FACTOR = 1.0;

    double maxVal   = panel.MaxValue;
    double minVal   = panel.MinValue;
    double yRatio   = y / (panelH * CORRECTION_FACTOR);
    double rawPrice = maxVal - yRatio * (maxVal - minVal);

    if (rawPrice <= 0.0) return 0.0;                               // guard (4): sanity

    if (instrument == null) return 0.0;                            // guard (5)
    return AlignToTick(rawPrice, instrument.MasterInstrument.TickSize);
}
```

**CYC of GetPriceAtY = 5 (unchanged). PASS.**

#### Step 4 — Update the comment block at top of `GetPriceAtY`

Replace the existing multi-line comment block immediately before `GetPriceAtY`
(lines 290–296 in the current file) with:

```csharp
// B17 T2: Linear interpolation via ChartPanel.MaxValue / MinValue / ActualHeight.
// B17 fix: FindPriceCanvasPanel replaces FindVisualChild<ChartPanel> (DFS first-match
// returned ChartTrader sidebar: Width~139, MaxValue=0 -> rawPrice=0 -> no order placed).
// FindPriceCanvasPanel selects widest ChartPanel with MaxValue>0 = price canvas.
// CORRECTION_FACTOR = 1.0 (B16 T1 confirmed ContentPresenter.ActualHeight = ChartPanel.ActualHeight).
// NT8-029 replacement: RoundToTickSize absent -- AlignToTick via Math.Round AwayFromZero.
// CYC=5: cc null(1), panel null(2), height<=0(3), raw<=0(4), instrument null(5).
```

#### Step 5 — Add 4+ new `[Fact]` tests in `CopyEngineTests.cs`

Locate [`CopyEngineTests.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs).
The file currently ends with:

```csharp
        [Fact]
        public void T_B16_10_TightenOneStop_NotYetTighterLong_ProceedsToChange()
        { ... }


    }
}
```

Insert the following test region **before the final `}` `}` of the class/namespace**,
after the last existing B16 test (line 1820). The reflection helper methods
`CallLinearYToPrice` and `CallAlignToTick` already exist in the file (lines 1726–1741)
and are reused by all new tests — do NOT add duplicate helper declarations.

```csharp
        // =====================================================================
        // B17 T2: GetPriceAtY panel selection + pure-math coverage
        //         (T_B17_01 through T_B17_07)
        // All tests call CallLinearYToPrice / CallAlignToTick (declared above in B16 T2 region).
        // No WPF tree required -- pure-math helpers only.
        // =====================================================================

        // T_B17_01: y=0 (top of panel) must return maxVal regardless of range.
        // LinearYToPrice(0, 452, 5023.25, 4987.50, 1.0) = 5023.25 - 0*(35.75) = 5023.25
        [Fact]
        public void T_B17_01_LinearYToPrice_TopOfPanel_ReturnsMaxVal()
        {
            double result = CallLinearYToPrice(0.0, 452.0, 5023.25, 4987.50, 1.0);
            Assert.Equal(5023.25, result, 5);
        }

        // T_B17_02: y=226 (midpoint) must return midpoint of price range.
        // Linear interp: 5023.25 - (226/452)*(35.75) = 5023.25 - 17.875 = 5005.375
        [Fact]
        public void T_B17_02_LinearYToPrice_MiddleOfPanel_ReturnsMidpointPrice()
        {
            double result = CallLinearYToPrice(226.0, 452.0, 5023.25, 4987.50, 1.0);
            Assert.Equal(5005.375, result, 5);
        }

        // T_B17_03: panelH=0 triggers guard (1) in LinearYToPrice -> returns 0.0.
        // This was the B17 root cause: ChartTrader sidebar had MaxValue=MinValue=0 ->
        // linear interp returned 0 -> GetPriceAtY guard (4) fired -> no order placed.
        [Fact]
        public void T_B17_03_LinearYToPrice_ZeroPanelHeight_ReturnsZero()
        {
            double result = CallLinearYToPrice(100.0, 0.0, 5023.25, 4987.50, 1.0);
            Assert.Equal(0.0, result, 5);
        }

        // T_B17_04: y large enough that rawPrice <= 0 -> guard (2) fires -> returns 0.0.
        // max=10, min=5, panelH=100, y=300:
        // rawPrice = 10 - (300/100)*(5) = 10 - 15 = -5 <= 0 -> 0.0
        [Fact]
        public void T_B17_04_LinearYToPrice_OverBoundary_ReturnsZero()
        {
            double result = CallLinearYToPrice(300.0, 100.0, 10.0, 5.0, 1.0);
            Assert.Equal(0.0, result, 5);
        }

        // T_B17_05: AlignToTick -- already tick-aligned price must be unchanged.
        // 5023.25 / 0.25 = 20093.0 exactly -> Math.Round(20093.0) = 20093 -> * 0.25 = 5023.25
        [Fact]
        public void T_B17_05_AlignToTick_AlreadyAligned_Unchanged()
        {
            double result = CallAlignToTick(5023.25, 0.25);
            Assert.Equal(5023.25, result, 5);
        }

        // T_B17_06: AlignToTick -- 5023.125 / 0.25 = 20092.5 -> Math.Round(20092.5, AwayFromZero).
        // AlignToTick uses MidpointRounding.AwayFromZero -> rounds 20092.5 up to 20093 -> * 0.25 = 5023.25.
        [Fact]
        public void T_B17_06_AlignToTick_HalfTickRoundsAwayFromZero()
        {
            double result = CallAlignToTick(5023.125, 0.25);
            Assert.Equal(5023.25, result, 5);
        }

        // T_B17_07: AlignToTick tickSize guard -- zero tickSize must return raw unchanged.
        // CYC guard (1) in AlignToTick: if (tickSize <= 0.0) return raw;
        [Fact]
        public void T_B17_07_AlignToTick_ZeroTickSize_ReturnsRaw()
        {
            double result = CallAlignToTick(5023.25, 0.0);
            Assert.Equal(5023.25, result, 5);
        }
```

**Minimum contract:** T_B17_01 through T_B17_04 (4 tests) are required.
T_B17_05 through T_B17_07 (3 additional tests) are recommended for full path coverage.
All 7 are provided here — implement all 7.

**Note on `AlignToTick` in `TradeCopierPanel.cs`:** The current implementation (B16)
uses `Math.Round(raw / tickSize) * tickSize` with the default
`MidpointRounding.ToEven` (banker's rounding). T_B17_06 asserts AwayFromZero behaviour.
**Before writing this test, check the current `AlignToTick` implementation** for whether
it uses `MidpointRounding.AwayFromZero`. If the current implementation uses
`MidpointRounding.ToEven`, the expected value for T_B17_06 must be adjusted:

- `MidpointRounding.ToEven` (banker's): `Math.Round(20092.5) = 20092` → result = 5023.00
- `MidpointRounding.AwayFromZero`: `Math.Round(20092.5, AwayFromZero) = 20093` → result = 5023.25

Read the current `AlignToTick` body at
[`TradeCopierPanel.cs:340`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:340)
and adjust T_B17_06's expected value to match whichever rounding mode is currently used.
Do not change the `AlignToTick` implementation itself — it is correct and tested by B16.

#### Step 6 — Update `NT8_ADDON_KNOWLEDGE.md`

Append the following section to
`c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md`
**after** the `## B17 T1 Discoveries` section filled in during T1:

```markdown
## B17 T2 Discoveries

### Panel Fix Applied
Option: [A / B-compile]
(Based on T1 F5 output: record which option was used and why)

### GetPriceAtY Corrected Panel Selection
[ENGINEER: describe confirmed path. Example:
"Option A applied. FindPriceCanvasPanel selected ChartPanel[1] (W=1047.20, Max=5023.25).
Armed click at Y=200 on 452px panel placed limit order at $5010.25 (tick-aligned). Correct."]

### Test Count Delta
Prior [Fact] count (before T2): 104
Added [Fact] tests (T_B17_01 through T_B17_07): 7
New total [Fact] count: 111

### NT8 Rules Update
[ENGINEER: state "nt8-rules B17-T2: no new rules" OR list any new NT8 compiler rules discovered
and confirm they have been added to docs/standards/NT8_COMPILER_RULES.md]
```

---

### Method Signatures (exact — no deviation)

```csharp
// New method (TradeCopierPanel.cs — Option A):
private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
// Returns: ChartPanel with MaxValue > 0 and largest ActualWidth, or null if none qualifies
// Thread: UI thread only (called by GetPriceAtY which is called from OnChartMouseDown)
// CYC = 5

// New method (TradeCopierPanel.cs — Option B-compile, only if Director confirms):
private static ChartPanel FindPriceCanvasPanelViaCharts(ChartControl cc)
// Returns: first ChartPanel in cc.Charts where panel.MaxValue > 0, or null
// Thread: UI thread only
// CYC = 5

// Modified method — single-line change only; all guards and math unchanged:
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
// CYC = 5 (unchanged)

// Unchanged methods (T2 adds tests but does NOT modify these):
internal static double LinearYToPrice(
    double y, double panelH, double maxVal, double minVal, double correctionFactor)
// CYC = 2 (unchanged)

internal static double AlignToTick(double raw, double tickSize)
// CYC = 2 (unchanged)

// Restored method (T1 diagnostic additions fully removed):
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
// CYC = 6 (restored to pre-T1 value: 5 guards + try/catch; no fallback line, no diagnostic call)
```

---

### CYC Bounds (T2)

| Method | Branches | CYC | Bound | Result |
|--------|----------|-----|-------|--------|
| `FindPriceCanvasPanel` | root null(1), while(2), type+predicate(3), for(4), child null(5) | 5 | ≤ 8 | PASS |
| `FindPriceCanvasPanelViaCharts` | cc null(1), charts null/empty(2), foreach(3), panel null(4), MaxValue>0(5) | 5 | ≤ 8 | PASS |
| `GetPriceAtY` | cc null(1), panel null(2), height<=0(3), raw<=0(4), instrument null(5) | 5 | ≤ 8 | PASS |
| `OnChartMouseDown` | !_clickArmed(1), leaderAccount null(2), instrument null(3), chartControl null(4), rawPrice<=0 return(5), try/catch(6) | 6 | ≤ 8 | PASS |

---

### 7-Scan Checklist — T2 (MANDATORY, run before marking complete)

```
Scan 1 — JS-021 lock():
  grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 2 — JS-033 async void:
  grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 3 — JS-002 return null:
  grep -rn "return null;" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  0 NEW instances
  (FindPriceCanvasPanel uses "return null" as a structural visual-tree guard — not a
  business-logic hot path. Plan reviewer confirmed this is compliant per §4 of plan review.)

Scan 4 — NT8-003 volatile double:
  grep -rn "volatile double" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 5 — NT8-034 Math.Clamp:
  grep -rn "Math.Clamp" src/PropTraderTools/TradeCopierPanel.cs --include="*.cs"
  MUST return 0 results

Scan 6 — CYC audit:
  python scripts/complexity_audit.py src/PropTraderTools/TradeCopierPanel.cs
  All methods must report ≤ 8
  Key targets: FindPriceCanvasPanel=5, GetPriceAtY=5, OnChartMouseDown=6

Scan 7 — Build:
  dotnet build (Wave workspace: c:\WSGTA\universal-or-strategy)
  MUST return: 0 errors, 0 warnings
```

Additional T2 scans:
```
Scan 8 — Diagnostic cleanup:
  grep -rn "_b17DiagDone\|EnumerateAllChartPanels\|ProbeChartsProperty\|B17 interim" \
    src/PropTraderTools/TradeCopierPanel.cs
  MUST return 0 results (all T1 code fully removed)

Scan 9 — xUnit test count:
  Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "\[Fact\]" |
    Measure-Object | Select-Object -ExpandProperty Count
  MUST be >= 108  (prior count=104 + 4 minimum new tests)
  Target with all 7 recommended tests: >= 111
```

---

### T2 Success Criterion

F5 in NT8 Sim101, arm click trader, click chart at a specific price level:

```
[ ] F5 green — no compilation errors
[ ] No diagnostic MessageBox appears (T1 code fully removed; _b17DiagDone field gone)
[ ] No interim fallback active (GetRefPrice not called in click trader path)
[ ] Armed click at chart Y-pixel corresponding to price $5020.00
    -> limit order placed at $5020.00 (or nearest tick: $5020.00 for 0.25 tick)
[ ] Armed click at chart Y-pixel corresponding to price $5015.25
    -> limit order placed at $5015.25 (exact tick-aligned value)
[ ] All T_B17_01 through T_B17_04 [Fact] tests pass (minimum)
[ ] All T_B17_01 through T_B17_07 [Fact] tests pass (recommended)
[ ] Scan 8 returns 0 matches (all T1 code removed)
[ ] Scan 9 count >= 108 (prior 104 + 4 minimum)
[ ] NT8_ADDON_KNOWLEDGE.md ## B17 T2 Discoveries section filled in
[ ] "nt8-rules B17-T2: [summary or 'no new rules']" stated in completion report
```

---

## Files Touched Summary

### T1

| File | Change |
|------|--------|
| `TradeCopierPanel.cs` | Add `_b17DiagDone` field; add `using System.Reflection` + `using System.Text`; add `EnumerateAllChartPanels()` method; add `ProbeChartsProperty()` method; modify `OnChartMouseDown` (2 lines added) |
| `NT8_ADDON_KNOWLEDGE.md` | Append `## B17 T1 Discoveries` section with placeholder text (engineer fills in after F5) |

### T2

| File | Change |
|------|--------|
| `TradeCopierPanel.cs` | Remove T1 diagnostic (field + 2 methods + 2 OnChartMouseDown lines + using directives if unused); add `FindPriceCanvasPanel` (Option A) or `FindPriceCanvasPanelViaCharts` (Option B); replace one line in `GetPriceAtY`; update comment block above `GetPriceAtY` |
| `CopyEngineTests.cs` | Add 7 `[Fact]` tests (T_B17_01 through T_B17_07); minimum 4 required |
| `NT8_ADDON_KNOWLEDGE.md` | Append `## B17 T2 Discoveries` section after T1 discoveries are filled in |

### Files NOT Touched (both tickets)

- `CopyEngine.cs` — no changes
- `TradeCopierAddOn.cs` — `FindVisualChild<T>` left as-is; not called in T2 click trader path
- `TradeCopierWindow.cs` — no changes
- `AtrSizingEngine.cs` — no changes

---

*End of B17 Tickets — ptt-architect*
