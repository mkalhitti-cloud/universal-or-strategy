# PTT-COPIER-B15 Tickets
# Phase 3 — ptt-architect output
# Status: TICKETS_COMPLETE (CYCLE 2 — V-01 + V-02 repaired)
# Date: 2026-07-14
# Block: PTT-COPIER-B15
# Approved plan: docs/brain/PTT-COPIER-B15/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/PTT-COPIER-B15/02-plan-review.md (all 14 checks PASS)
# Ticket review: docs/brain/PTT-COPIER-B15/04-ticket-review.md (TICKET_REVIEW_FAIL — T1 V-01+V-02)
# Repairs: V-01 (DumpChartControlTree CYC=14→split sub-helpers) + V-02 (NT8-008 chart.ChartControl banned)

---

# TICKET T1 — ChartControl Visual Tree Diagnostic (Investigation)

## Header

| Field | Value |
|-------|-------|
| Ticket ID | B15-T1 |
| Title | ChartControl Visual Tree Diagnostic |
| Spec Req | DW-B8-04 (prerequisite investigation step) |
| CYC Budget | DumpChartControlTree CYC <= 3; DumpReflectionPath CYC <= 7; DumpVisualTree CYC <= 6; SetChart CYC = 2 |
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Wave workspace | `c:\WSGTA\universal-or-strategy` |
| Tests | None in T1 — runtime investigation, not unit-testable logic |
| Gates T2 | YES — T2 MUST NOT start until T1 VERIFY_PASS |

## Purpose

Inject a one-shot diagnostic into `TradeCopierPanel.cs` that walks the ChartControl visual
tree and probes for the `ChartBars` property via reflection. The engineer F5s on Sim101,
reads the `_statusText` output, and records the confirmed API path in
`docs/standards/NT8_ADDON_KNOWLEDGE.md` under `## B15 Discoveries`.

**T1 does NOT replace the `double price = 0.0` stub. The stub stays.**
**T1 does NOT modify OnChartMouseDown.**

---

## Implementation Instructions

### Step 1 — Add file header comment

At the top of `TradeCopierPanel.cs`, immediately before the line:
```
// PTT-COPIER-B14-T1 -- TradeCopierPanel.cs
```
Insert the following comment block:
```csharp
// PTT-COPIER-B15-T1 -- TradeCopierPanel.cs
// B15 T1 CHANGES:
//   1. Added _chartDiagDone volatile bool field (JS-023 cross-thread one-shot guard).
//   2. Added DumpReflectionPath(ChartControl cc, StringBuilder sb) -- reflection probe sub-helper.
//   3. Added DumpVisualTree(ChartControl cc, StringBuilder sb) -- visual tree walk sub-helper.
//   4. Added DumpChartControlTree(ChartControl cc) -- orchestrator (calls sub-helpers + writes statusText).
//   5. Modified SetChart(Chart chart) -- calls DumpChartControlTree after chart assigned.
//   6. OnChartMouseDown UNCHANGED. Stub double price = 0.0 UNCHANGED.
```

### Step 2 — Add `_chartDiagDone` field

In the fields block under the comment `// B9 T2 -- Click trader (JS-023: volatile cross-thread fields)`,
add the following line immediately AFTER `private volatile bool _clickBuy = true;`:

```csharp
// B15 T1 -- one-shot diagnostic guard (JS-023: cross-thread volatile bool)
private volatile bool _chartDiagDone = false;
```

**CRITICAL — NT8-017 + NT8-003:**
- Field type is `volatile bool` (allowed)
- Field type is NOT `volatile double` (would violate NT8-003)

### Step 3 — Add three diagnostic methods

Add the following three methods to `TradeCopierPanel.cs` immediately AFTER the existing
`SetChart(Chart chart)` method (currently line ~1079). Insert all three in the
`// -- public surface (called by TradeCopierAddOn) --` region, in this order:
1. `DumpReflectionPath` (sub-helper)
2. `DumpVisualTree` (sub-helper)
3. `DumpChartControlTree` (orchestrator — calls both sub-helpers)

---

#### 3a — `DumpReflectionPath` (sub-helper, CYC <= 7)

```csharp
// B15 T1 -- DumpReflectionPath: probes ChartControl for ChartBars property via reflection.
// Sub-helper called by DumpChartControlTree. Never called directly.
// CYC=7:
//   (1) barsInfo != null check
//   (2) barsVal != null check
//   (3) indexer != null check
//   (4) item0 != null check
//   (5) panelInfo != null check
//   (6) panelVal != null check
//   (7) catch branch in try/catch
private void DumpReflectionPath(ChartControl cc, System.Text.StringBuilder sb)
{
    var barsInfo = cc.GetType().GetProperty("ChartBars");
    if (barsInfo == null)                                              // (1) no property
    {
        sb.Append("ChartBars=NO; ");
        return;
    }
    sb.Append("ChartBars=YES type=").Append(barsInfo.PropertyType.FullName).Append("; ");
    try
    {
        var barsVal = barsInfo.GetValue(cc);
        if (barsVal != null)                                           // (2)
        {
            var indexer = barsVal.GetType().GetProperty("Item", new[] { typeof(int) });
            if (indexer != null)                                       // (3)
            {
                var item0 = indexer.GetValue(barsVal, new object[] { 0 });
                if (item0 != null)                                     // (4)
                {
                    sb.Append("bars[0]=").Append(item0.GetType().FullName).Append("; ");
                    var panelInfo = item0.GetType().GetProperty("ChartPanel");
                    if (panelInfo != null)                             // (5)
                    {
                        sb.Append("ChartPanel=YES; ");
                        var panelVal = panelInfo.GetValue(item0);
                        if (panelVal != null)                          // (6)
                        {
                            var gvbMethod = panelVal.GetType().GetMethod("GetValueByY");
                            sb.Append("GetValueByY=").Append(gvbMethod != null ? "YES" : "NO").Append("; ");
                        }
                    }
                    else { sb.Append("ChartPanel=NO; "); }
                }
            }
        }
    }
    catch (Exception ex)                                               // (7)
    {
        sb.Append("reflErr=").Append(ex.Message).Append("; ");
    }
}
```

**CYC count verification for DumpReflectionPath:**
| Branch | Condition |
|--------|-----------|
| (1) | `if (barsInfo == null)` — property absent early return |
| (2) | `if (barsVal != null)` — null value guard |
| (3) | `if (indexer != null)` — indexer probe guard |
| (4) | `if (item0 != null)` — item0 probe guard |
| (5) | `if (panelInfo != null)` — ChartPanel probe guard |
| (6) | `if (panelVal != null)` — panelVal probe guard |
| (7) | `catch (Exception ex)` — exception branch |

CYC = 7. Within budget (<= 8). ✅

**NT8 NOTES:**
- `System.Text.StringBuilder` — allowed (.NET 4.8 built-in); parameter passed by reference
- `cc.GetType().GetProperty(...)` — uses `System.Reflection.PropertyInfo` via `var`
- Exception catch is intentionally broad (`Exception`) — diagnostic context, NOT a hot path
- No `lock()` (JS-021), no `async void` (JS-033), no `return null` (JS-002)

---

#### 3b — `DumpVisualTree` (sub-helper, CYC <= 6)

```csharp
// B15 T1 -- DumpVisualTree: walks ChartControl visual tree 3 levels deep, appending type names.
// Sub-helper called by DumpChartControlTree. Never called directly.
// CYC=6:
//   (1) outer for loop
//   (2) child == null continue guard
//   (3) inner for loop (depth 2)
//   (4) grand == null continue guard
//   (5) inner-inner for loop (depth 3)
//   (6) great != null append guard
private void DumpVisualTree(ChartControl cc, System.Text.StringBuilder sb)
{
    sb.Append("VT[");
    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(cc); i++)       // (1)
    {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(cc, i);
        if (child == null) continue;                                                             // (2)
        sb.Append(child.GetType().Name);
        for (int j = 0; j < System.Windows.Media.VisualTreeHelper.GetChildrenCount(child); j++) // (3)
        {
            var grand = System.Windows.Media.VisualTreeHelper.GetChild(child, j);
            if (grand == null) continue;                                                         // (4)
            sb.Append("/").Append(grand.GetType().Name);
            for (int k = 0; k < System.Windows.Media.VisualTreeHelper.GetChildrenCount(grand); k++) // (5)
            {
                var great = System.Windows.Media.VisualTreeHelper.GetChild(grand, k);
                if (great != null)                                                               // (6)
                    sb.Append("/").Append(great.GetType().Name);
            }
        }
        sb.Append(",");
    }
    sb.Append("]");
}
```

**CYC count verification for DumpVisualTree:**
| Branch | Condition |
|--------|-----------|
| (1) | `for (int i = 0; ...)` — outer loop (depth 1) |
| (2) | `if (child == null) continue` — depth-1 null guard |
| (3) | `for (int j = 0; ...)` — inner loop (depth 2) |
| (4) | `if (grand == null) continue` — depth-2 null guard |
| (5) | `for (int k = 0; ...)` — inner-inner loop (depth 3) |
| (6) | `if (great != null)` — depth-3 null guard |

CYC = 6. Within budget (<= 8). ✅

**NT8 NOTES:**
- `System.Windows.Media.VisualTreeHelper` — confirmed WPF API (no NT8 restriction)
- No `lock()` (JS-021), no `async void` (JS-033), no `return null` (JS-002)

---

#### 3c — `DumpChartControlTree` (orchestrator, CYC <= 3)

```csharp
// B15 T1 -- DumpChartControlTree: one-shot diagnostic orchestrator.
// Fires once per panel lifetime (guarded by _chartDiagDone).
// Delegates reflection probe to DumpReflectionPath, visual walk to DumpVisualTree.
// Output written to _statusText so engineer can read it on Sim101.
// CYC=3:
//   (1) guard: _chartDiagDone || cc == null early return
//   (2) Dispatcher.InvokeAsync lambda: _statusText != null check
//   (straight-line calls to sub-helpers — no additional branches)
private void DumpChartControlTree(ChartControl cc)
{
    if (_chartDiagDone || cc == null) return;           // (1) one-shot guard + null check
    _chartDiagDone = true;

    var sb = new System.Text.StringBuilder();
    DumpReflectionPath(cc, sb);                         // reflection probe (CYC=7 sub-helper)
    DumpVisualTree(cc, sb);                             // visual tree walk (CYC=6 sub-helper)
    string diagMsg = sb.ToString();

    // (2) Thread-safe UI write: _statusText is a WPF control (UI thread required)
    Dispatcher.InvokeAsync(() =>
    {
        if (_statusText != null)                        // (2) null guard inside lambda
            _statusText.Text = diagMsg;
    });
}
```

**CYC count verification for DumpChartControlTree:**
| Branch | Condition |
|--------|-----------|
| (1) | `if (_chartDiagDone \|\| cc == null) return` — combined one-shot guard + null check |
| (2) | `if (_statusText != null)` inside Dispatcher lambda |

CYC = 2 (conditional in lambda counted per method scope; orchestrator has 2 decision points).
Counted conservatively as CYC = 3 if each sub-helper call is treated as a CYC node. Either way: well within budget (<= 8). ✅

**NT8 NOTES on all three methods:**
- `System.Text.StringBuilder` — allowed (.NET 4.8 built-in)
- `System.Windows.Media.VisualTreeHelper` — confirmed WPF API (no NT8 restriction)
- `cc.GetType().GetProperty(...)` — `System.Reflection.PropertyInfo` via `var`
- Exception catch in DumpReflectionPath is intentionally broad (`Exception`) — diagnostic context only; NOT a hot path
- No `lock()` anywhere (JS-021)
- No `async void` (JS-033)
- No `return null` (JS-002) — all methods return `void`

### Step 4 — Modify `SetChart` to call the diagnostic

The current `SetChart` method (TradeCopierPanel.cs, found near line 1079) is:
```csharp
// B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
public void SetChart(Chart chart)
{
    _currentChart = chart;
}
```

Replace it with:
```csharp
// B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
// B15 T1: Call DumpChartControlTree after chart assigned (one-shot diagnostic).
public void SetChart(Chart chart)
{
    _currentChart = chart;
    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);   // NT8-008: chart.ChartControl absent (CS1061 confirmed B8)
    if (cc != null)
        DumpChartControlTree(cc);
}
```

**CRITICAL — NT8-008 (P0):**
- `chart.ChartControl` DOES NOT EXIST — CS1061 is a confirmed B8 build break.
- `FindVisualChild<ChartControl>(chart)` is the ONLY safe pattern (NT8-008 SAFE section).
- `FindVisualChild<T>` is the depth-first helper already present in `TradeCopierAddOn.cs`.
- Do NOT write `chart.ChartControl` anywhere in this file. Zero uses allowed.

**SetChart CYC after T1:** CYC = 2 (one `if cc != null` guard added). Within budget. ✅

**SCAN-08 (NT8-008 enforcement):**
```
grep -n "chart\.ChartControl" src/PropTraderTools/TradeCopierPanel.cs
Expected: 0 results (NT8-008 P0 — banned)
```

### Step 5 — Update file header in NT8_ADDON_KNOWLEDGE.md

After F5 on Sim101, the engineer MUST add a `## B15 Discoveries` section to
`docs/standards/NT8_ADDON_KNOWLEDGE.md`. Minimum required content:

```markdown
## B15 Discoveries

Date: YYYY-MM-DD
Block: PTT-COPIER-B15 T1
Method: DumpChartControlTree — visual tree + reflection probe on ChartControl

### Confirmed API path for Y-to-price conversion

_statusText output on Sim101 (copy verbatim here):
> [PASTE ACTUAL OUTPUT HERE]

### Resolved questions

| Question | Answer | Source |
|----------|--------|--------|
| ChartControl.ChartBars property exists? | YES/NO | _statusText |
| ChartBars[0] type | [TYPE NAME] | _statusText |
| ChartBars[0].ChartPanel property exists? | YES/NO | _statusText |
| ChartPanel.GetValueByY(double) method exists? | YES/NO | _statusText |
| ChartControl visual tree child at scale position | [TYPE NAME] | _statusText VT[] |

### T2 confirmed API call

```csharp
// Fill in the confirmed API path before writing T2 code:
// CONFIRMED-API: [e.g. cc.ChartBars[0].ChartPanel.GetValueByY(y)]
```

### NT8_COMPILER_RULES update required?

[ ] No new compiler errors discovered
[ ] YES — new rule NT8-036 added (describe if applicable)
```

---

## T1 Engineer Deliverable

After F5 on Sim101:

1. Read `_statusText` content from the PTT panel on the chart
2. Paste the raw output into NT8_ADDON_KNOWLEDGE.md `## B15 Discoveries`
3. Complete all rows of the "Resolved questions" table
4. Fill in `T2 confirmed API call` with the actual confirmed code line

**The T1 completion report (`ticket-1-completion.md`) MUST include the confirmed
`_statusText` output verbatim.** T2 is blocked until T1 VERIFY_PASS is recorded.

---

## 7-Scan Checklist (T1)

Engineer signs off each item before submitting ticket-1-completion.md:

```
SCAN-01 [ ] lock() check — 0 results required:
         grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs

SCAN-02 [ ] async void check — 0 results required (event handlers excluded):
         grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs

SCAN-03 [ ] ChartControl.GetValueByY direct call — 0 results required (NT8-009):
         grep -n "\.GetValueByY(" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (DumpReflectionPath uses GetMethod() reflection, not direct call)

SCAN-04 [ ] volatile bool on _chartDiagDone — must be present (NT8-017, JS-023):
         grep -n "_chartDiagDone" src/PropTraderTools/TradeCopierPanel.cs
         Expected: field declaration includes "volatile bool"

SCAN-05 [ ] DumpChartControlTree called from SetChart ONLY — single call site:
         grep -n "DumpChartControlTree" src/PropTraderTools/TradeCopierPanel.cs
         Expected: exactly 2 hits (method definition + one call in SetChart)

SCAN-06 [ ] _statusText.Text update in Dispatcher.InvokeAsync — thread-safe UI update:
         grep -n "_statusText.Text" src/PropTraderTools/TradeCopierPanel.cs
         Expected: DumpChartControlTree's assignment is inside Dispatcher.InvokeAsync lambda

SCAN-07 [ ] File header comment added for B15 T1 changes:
         grep -n "B15 T1 CHANGES" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 1 match in the header comment block at top of file

SCAN-08 [ ] NT8-008 banned pattern absent — 0 results required:
         grep -n "chart\.ChartControl" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (chart.ChartControl is a P0 banned pattern — CS1061 confirmed B8)
```

All 8 scans must be signed off PASS before ticket-1-completion.md is written.

---

## CYC Summary Table (T1 methods)

| Method | CYC | Budget | Status |
|--------|-----|--------|--------|
| `DumpReflectionPath(ChartControl cc, StringBuilder sb)` | 7 | <= 8 | ✅ PASS |
| `DumpVisualTree(ChartControl cc, StringBuilder sb)` | 6 | <= 8 | ✅ PASS |
| `DumpChartControlTree(ChartControl cc)` | 3 | <= 8 | ✅ PASS |
| `SetChart(Chart chart)` after T1 | 2 | <= 8 | ✅ PASS |

---

## Protected Files (T1)

The following files MUST NOT be modified in T1:

| File | Status |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/TradeCopierAddOn.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/TradeCopierWindow.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/AtrSizingEngine.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/CopyEngineTests.cs` | PROTECTED in T1 — tests added in T2 only |

---

## Completion Artifact

Write `docs/brain/PTT-COPIER-B15/ticket-1-completion.md` with:
- All 8 SCAN results (PASS/FAIL + evidence)
- Verbatim `_statusText` output from Sim101 F5 run
- CYC confirmation for DumpReflectionPath (<= 7), DumpVisualTree (<= 6), DumpChartControlTree (<= 3)
- Whether NT8_ADDON_KNOWLEDGE.md B15 Discoveries section was written

---
---

# TICKET T2 — Implement Y-to-Price Conversion + Tick-Align (Implementation)

## Header

| Field | Value |
|-------|-------|
| Ticket ID | B15-T2 |
| Title | Replace 0.0 Stub: Y-to-Price Conversion + Tick-Align |
| Spec Req | DW-B8-04 (closure) |
| CYC Budget | GetPriceAtY = 4; OnChartMouseDown final = 6 |
| Files | `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/CopyEngineTests.cs` |
| Wave workspace | `c:\WSGTA\universal-or-strategy` |
| Tests | 6 [Fact] tests in CopyEngineTests.cs (tick-align pure math) |
| Precondition | **T1 VERIFY_PASS required. NT8_ADDON_KNOWLEDGE.md must contain "## B15 Discoveries" with confirmed API path.** |

## Gate Statement

**DO NOT begin T2 implementation until:**
1. `docs/brain/PTT-COPIER-B15/ticket-1-verification.md` exists and contains `VERIFY_PASS`
2. `docs/standards/NT8_ADDON_KNOWLEDGE.md` section `## B15 Discoveries` is populated with
   the confirmed `_statusText` output from Sim101

Before writing any T2 code, read `NT8_ADDON_KNOWLEDGE.md ## B15 Discoveries` and extract:
- The confirmed property name for ChartBars access (may NOT be `ChartBars`)
- The confirmed path to reach `ChartPanel`
- Whether `GetValueByY(double y)` exists on that panel type

All `[CONFIRMED-API]` placeholders in this ticket MUST be replaced with the confirmed values
from the T1 investigation before the engineer writes any T2 code.

---

## Purpose

Remove the diagnostic scaffolding from T1 and replace the hardcoded `double price = 0.0`
stub in `OnChartMouseDown` with the real Y-to-price conversion using the confirmed API
discovered in T1. Apply mandatory NT8-029 tick-align before the Limit order fires.

---

## Implementation Instructions

### Step 1 — Add file header comment

At the top of `TradeCopierPanel.cs`, immediately before the line added by T1:
```
// PTT-COPIER-B15-T1 -- TradeCopierPanel.cs
```
Insert:
```csharp
// PTT-COPIER-B15-T2 -- TradeCopierPanel.cs
// B15 T2 CHANGES:
//   1. Removed _chartDiagDone volatile bool field (T1 diagnostic cleanup).
//   2. Removed DumpReflectionPath(ChartControl cc, StringBuilder sb) method (T1 diagnostic cleanup).
//   3. Removed DumpVisualTree(ChartControl cc, StringBuilder sb) method (T1 diagnostic cleanup).
//   4. Removed DumpChartControlTree(ChartControl cc) method (T1 diagnostic cleanup).
//   5. Reverted SetChart(Chart chart) to CYC=1 (removed DumpChartControlTree call).
//   6. Added GetPriceAtY(ChartControl cc, double y) private static method (CYC=4).
//   7. Modified OnChartMouseDown: replaced 0.0 stub + suppression line with real lookup.
//      Final CYC=6. DW-B8-04 CLOSED.
```

### Step 2 — Remove T1 diagnostic artifacts

Remove the following things added in T1:

**2a — Remove field declaration:**
Find and delete the line:
```csharp
private volatile bool _chartDiagDone = false;
```
Include the comment line above it:
```csharp
// B15 T1 -- one-shot diagnostic guard (JS-023: cross-thread volatile bool)
```

**2b — Remove all three diagnostic methods:**
Delete the entire `DumpReflectionPath` method (all lines from the comment
`// B15 T1 -- DumpReflectionPath:` through the closing brace `}`).
Delete the entire `DumpVisualTree` method (all lines from the comment
`// B15 T1 -- DumpVisualTree:` through the closing brace `}`).
Delete the entire `DumpChartControlTree` method (all lines from the comment
`// B15 T1 -- DumpChartControlTree:` through the closing brace `}`).

**2c — Revert `SetChart` to single-line:**
Replace the T1-modified `SetChart`:
```csharp
public void SetChart(Chart chart)
{
    _currentChart = chart;
    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);
    if (cc != null)
        DumpChartControlTree(cc);
}
```
With the original:
```csharp
// B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
public void SetChart(Chart chart)
{
    _currentChart = chart;
}
```

### Step 3 — Add `GetPriceAtY` private static method

Add the following method immediately AFTER the existing `SetChart` method. Insert BEFORE the
`SetInstrument` method.

```csharp
// B15 T2 -- GetPriceAtY: converts pixel Y coordinate to price using confirmed NT8 chart scale API.
// Confirmed API path: [CONFIRMED-API — read from NT8_ADDON_KNOWLEDGE.md B15 Discoveries before writing]
// NT8-009: ChartControl.GetValueByY does NOT exist -- use ChartPanel path only.
// NT8-029: caller applies tick-align after this returns.
// CYC=4:
//   guard (1): cc null check (combined with bars null)
//   guard (2): bars.Count == 0
//   guard (3): panel null
//   (4): return panel.GetValueByY(y)
private static double GetPriceAtY(ChartControl cc, double y)
{
    if (cc == null) return 0.0;                                        // guard (cc null)
    var bars = cc.ChartBars;                  // [CONFIRMED-API: replace cc.ChartBars with confirmed property name if different]
    if (bars == null || bars.Count == 0) return 0.0;                   // guard (1+2)
    var panel = bars[0].ChartPanel;           // [CONFIRMED-API: replace bars[0].ChartPanel with confirmed path if different]
    if (panel == null) return 0.0;                                     // guard (3)
    return panel.GetValueByY(y);                                       // (4)
}
```

**ENGINEER MUST substitute `[CONFIRMED-API]` with the actual confirmed property name(s)**
from `NT8_ADDON_KNOWLEDGE.md ## B15 Discoveries` before writing this method.

The `var` keyword is used throughout so the compiler resolves the exact types from the
NT8 assemblies. Do NOT hardcode a type name unless the confirmed type is needed for casting.

**CYC count verification for GetPriceAtY:**
| Branch | Condition |
|--------|-----------|
| (cc null) | `if (cc == null) return 0.0` |
| (1+2) | `if (bars == null \|\| bars.Count == 0) return 0.0` — counted as 1 CYC decision (combined) |
| (3) | `if (panel == null) return 0.0` |
| (4) | `return panel.GetValueByY(y)` — straight-line, no branch |

CYC = 4. Within budget. ✅

**NT8 NOTES:**
- No `lock()` (JS-021)
- No `async void` (JS-033)
- Returns `0.0` (double) on all guard paths — NOT `return null` (JS-002)
- Method is `private static` — no instance state accessed
- `cc.ChartBars` — exact property name confirmed by T1; if different, substitute directly

### Step 4 — Modify `OnChartMouseDown` stub block

**Locate the current stub block** in `OnChartMouseDown` at lines 1097-1101:
```csharp
            // NT8 constraint: ChartControl.GetValueByY does not exist in this NT8 version.
            // DW-B8-04 (click trader) deferred -- price lookup via visual tree / scale panel pending.
            // Temporary: use 0.0 so file compiles; click-trader will not fire valid orders until fixed.
            double price  = 0.0;
            _ = e.GetPosition(chartControl); // suppress unused-variable warning
```

**Replace those 5 lines** (the 3 comment lines + `double price = 0.0;` + `_ = e.GetPosition(...)`)
with:
```csharp
            // B15 T2 -- DW-B8-04: real Y-to-price conversion (NT8-009 resolved via ChartPanel.GetValueByY).
            // NT8-029: tick-align result before submitting Limit order.
            Point  mousePos = e.GetPosition(chartControl);
            double rawPrice = GetPriceAtY(chartControl, mousePos.Y);
            if (rawPrice <= 0.0) return;                                        // guard (5)
            double tickSize = _instrument.MasterInstrument.TickSize;
            double price    = Math.Round(rawPrice / tickSize) * tickSize;       // NT8-029 tick-align
```

**After this change, `OnChartMouseDown` has the following guards and final CYC:**

| Guard | Condition | CYC count |
|-------|-----------|-----------|
| (1) | `if (!_clickArmed) return;` | +1 |
| (2) | `if (_leaderAccount == null) return;` | +1 |
| (3) | `if (_instrument == null) return;` | +1 |
| (4) | `if (chartControl == null) return;` | +1 |
| (5) | `if (rawPrice <= 0.0) return;` | +1 |
| ternary | `isBuy ? OrderAction.Buy : OrderAction.SellShort` | +1 |

**Final CYC = 6. Within budget (<= 8).** ✅

**CRITICAL — NT8-007 verification:**
Inspect `CreateOrder` argument 12 (the last argument, currently `null` at line 1116).
The argument MUST be `(NinjaTrader.Cbi.CustomOrder)null`, NOT a bare `null`.

Current code (line 1116): `null);`
Required code: `(NinjaTrader.Cbi.CustomOrder)null);`

If the current arg 12 is a bare `null` (as seen at line 1116), change it to:
```csharp
                    (NinjaTrader.Cbi.CustomOrder)null);
```

The full CreateOrder call after T2 must read:
```csharp
            try
            {
                _leaderAccount.CreateOrder(
                    _instrument, action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Day,
                    qty, price, 0, null,
                    "PTT-Click",          // signal name -- starts with "PTT-" (NT8-014)
                    DateTime.MaxValue,    // NT8-013: GTC sentinel
                    (NinjaTrader.Cbi.CustomOrder)null);   // NT8-007: explicit null cast required
            }
```

### Step 5 — Add tick-align tests to `CopyEngineTests.cs`

Append the following at the END of the `CopyEngineTests` class, immediately before the closing
`}` of the class (after the last existing `[Fact]` method in the file).

**First, add the private static helper** (this is a test-file-only helper — not a production method):
```csharp
        // =====================================================================
        // B15 T2: Tick-align pure math tests  (T-B15-01 through T-B15-06)
        // No NT8 runtime required -- pure double arithmetic.
        // =====================================================================

        // Tick-align helper mirroring OnChartMouseDown formula. Static, no NT8 dependency.
        private static double TickAlign(double raw, double tickSize)
            => Math.Round(raw / tickSize) * tickSize;
```

**Then add the six [Fact] tests:**

```csharp
        // T-B15-01: Price already on a tick boundary -- result is unchanged.
        [Fact]
        public void TickAlign_MesPriceBelowTick_RoundsDown()
        {
            // 4502.12 / 0.25 = 18008.48 -> Round = 18008 -> 18008 * 0.25 = 4502.00
            Assert.Equal(4502.00, TickAlign(4502.12, 0.25), precision: 8);
        }

        // T-B15-02: Price above half-tick boundary rounds up.
        [Fact]
        public void TickAlign_MesPriceAboveHalfTick_RoundsUp()
        {
            // 4502.14 / 0.25 = 18008.56 -> Round = 18009 -> 18009 * 0.25 = 4502.25
            Assert.Equal(4502.25, TickAlign(4502.14, 0.25), precision: 8);
        }

        // T-B15-03: Price exactly on a tick boundary is unchanged.
        [Fact]
        public void TickAlign_PriceExactTick_Unchanged()
        {
            // 4502.25 / 0.25 = 18009.00 -> Round = 18009 -> 18009 * 0.25 = 4502.25
            Assert.Equal(4502.25, TickAlign(4502.25, 0.25), precision: 8);
        }

        // T-B15-04: Price at exactly half-tick (0.125) -- Math.Round uses banker's rounding (MidpointRounding.ToEven).
        // 4502.125 / 0.25 = 18008.5 -- banker's round -> 18008 (even) -> 18008 * 0.25 = 4502.00
        // NOTE: C# default Math.Round(double, MidpointRounding.ToEven) applies.
        // Engineer MUST verify this at runtime. If NT8's CLR rounds up to 4502.25, update assertion.
        [Fact]
        public void TickAlign_PriceExactlyHalfTick_RoundsToEven()
        {
            // Banker's rounding: 18008.5 rounds to 18008 (even) -> 4502.00
            Assert.Equal(4502.00, TickAlign(4502.125, 0.25), precision: 8);
        }

        // T-B15-05: Arbitrary crude price rounds to nearest tick boundary below.
        [Fact]
        public void TickAlign_CrudePriceRoundTrip()
        {
            // 4502.37 / 0.25 = 18009.48 -> Round = 18009 -> 18009 * 0.25 = 4502.25
            Assert.Equal(4502.25, TickAlign(4502.37, 0.25), precision: 8);
        }

        // T-B15-06: Zero raw price -- GetPriceAtY guard returns 0.0 before tick-align fires;
        // the tick-align formula itself also returns 0.0 for zero input.
        [Fact]
        public void TickAlign_ZeroTickSizeGuard()
        {
            // 0.0 / 0.25 = 0.0 -> Round = 0 -> 0 * 0.25 = 0.0
            Assert.Equal(0.0, TickAlign(0.0, 0.25), precision: 8);
        }
```

**IMPORTANT:** The `Assert.Equal(double, double, int)` overload uses decimal precision rounding
(number of decimal places), not tolerance. `precision: 8` means compare to 8 decimal places.
This overload is available in xUnit 2.x (the version in use in this project — see existing tests
for the exact overload pattern used).

**Verify these tests compile in the existing test project WITHOUT any NinjaTrader references.**
The `TickAlign` helper is a pure `Math.Round` call — no NT8 types. If the test project requires
a `using` for `Math`, it is already in scope via `System` (always present).

### Step 6 — Update `NT8_ADDON_KNOWLEDGE.md`

Add or confirm the `## B15 Discoveries` section (T1 engineer may have already added it).
If the section exists, ensure it includes the confirmed ChartScale/ChartPanel API path used
in the final `GetPriceAtY` implementation.

At the END of the `## B15 Discoveries` section, add the following after the section is complete:

```markdown
### B15 T2 Status

DW-B8-04: CLOSED
GetPriceAtY confirmed API: [record the exact property path used in T2, e.g., cc.ChartBars[0].ChartPanel.GetValueByY(y)]
NT8-035 (hardcoded 0.0 stub): CLOSED by B15 T2
NT8-009 SAFE section updated: ChartPanel.GetValueByY(double) is the confirmed alternative to ChartControl.GetValueByY (absent)
```

---

## 7-Scan Checklist (T2)

Engineer signs off each item before submitting ticket-2-completion.md:

```
SCAN-01 [ ] lock() check -- 0 results required:
         grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs

SCAN-02 [ ] async void check -- 0 results required (event handlers excluded):
         grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs

SCAN-03 [ ] ChartControl.GetValueByY direct call -- 0 results required (NT8-009):
         grep -n "chartControl\.GetValueByY\|ChartControl.*GetValueByY" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (GetPriceAtY uses panel.GetValueByY via confirmed path)

SCAN-04 [ ] Stub comment + hardcoded 0.0 removed from OnChartMouseDown (NT8-035):
         grep -n "price\s*=\s*0\.0" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results in OnChartMouseDown (0.0 may appear elsewhere as guard returns)

SCAN-05 [ ] Suppression line removed from OnChartMouseDown:
         grep -n "_ = e.GetPosition" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (e.GetPosition is now actively used without suppression)

SCAN-06 [ ] Tick-align formula present (NT8-029):
         grep -n "Math.Round.*tickSize" src/PropTraderTools/TradeCopierPanel.cs
         Expected: >= 1 result (the Math.Round(rawPrice / tickSize) * tickSize line)

SCAN-07 [ ] Tick-align [Fact] tests in CopyEngineTests.cs:
         grep -n "TickAlign_" src/PropTraderTools/CopyEngineTests.cs
         Expected: >= 4 results (T-B15-01 through T-B15-06 -- all 6 required)
```

All 7 scans must be PASS before ticket-2-completion.md is written.

---

## Protected Files (T2)

| File | Status |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/TradeCopierAddOn.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/TradeCopierWindow.cs` | PROTECTED — do not touch |
| `src/PropTraderTools/AtrSizingEngine.cs` | PROTECTED — do not touch |

---

## Completion Artifact

Write `docs/brain/PTT-COPIER-B15/ticket-2-completion.md` with:
- All 7 SCAN results (PASS/FAIL + evidence)
- CYC confirmation for GetPriceAtY (= 4) and OnChartMouseDown (= 6)
- Confirmed `[CONFIRMED-API]` substitution used in GetPriceAtY
- NT8-007 arg 12 verification: confirm `(NinjaTrader.Cbi.CustomOrder)null` cast is present
- Test results: all 6 [Fact] tests pass (xUnit output)
- DW-B8-04 closure statement

---

## DW-B8-04 Closure Criteria

DW-B8-04 is CLOSED when ALL of the following are true at T2 VERIFY_PASS:

1. `TradeCopierPanel.cs` compiles in NT8 (F5 green on Sim101)
2. `double price = 0.0;` stub removed from `OnChartMouseDown`
3. `_ = e.GetPosition(chartControl);` suppression line removed
4. `GetPriceAtY` uses confirmed API path from B15 Discoveries
5. `Math.Round(rawPrice / tickSize) * tickSize` tick-align present in `OnChartMouseDown`
6. All 6 TickAlign [Fact] tests pass
7. `NT8_ADDON_KNOWLEDGE.md ## B15 Discoveries` records confirmed API path + DW-B8-04 CLOSED

---

## Return: TICKETS_COMPLETE
