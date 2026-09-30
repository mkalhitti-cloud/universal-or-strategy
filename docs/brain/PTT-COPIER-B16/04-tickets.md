# PTT-COPIER-B16 Implementation Tickets
# Author: ptt-architect
# Phase: 3 (Ticket Generation)
# Date: 2026-07-14
# Input plan: docs/brain/PTT-COPIER-B16/02-architecture-plan.md (REVIEW_PASS)
# Input review: docs/brain/PTT-COPIER-B16/02-plan-review.md (REVIEW_PASS)
# Output: docs/brain/PTT-COPIER-B16/04-tickets.md
# Return value: TICKETS_COMPLETE

---

## TICKET T1 — ChartPanel Subtree Diagnostic

### T1.1 Metadata

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | ChartPanel subtree diagnostic via MessageBox |
| Block | PTT-COPIER-B16 |
| Spec requirements | DW-B16-01 (partial — T1 is the investigation phase) |
| Depends on | PTT-COPIER-B15 VERIFY_PASS (complete) |
| Gates | T2 is BLOCKED until T1 VERIFY_PASS |
| Engineer | ptt-engineer |
| Verifier | ptt-verifier |

### T1.2 Goal

Walk `ChartPanel`'s own visual children (depth=2 from `ChartControl`) and probe each
child's method signatures via `System.Reflection`. Output all findings to
`System.Windows.MessageBox.Show` (never `_statusText` — no truncation). Record findings
in `docs/standards/NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries` before calling
TICKET_COMPLETE. T2 picks Branch A or Branch B based on findings.

### T1.3 Files to Modify (Wave Workspace)

| File | Change |
|------|--------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Add field, modify SetChart, add 2 methods, add 2 using directives |
| `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` | Append `## B16 Discoveries` section with T1 F5 output |
| `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md` | Add new rule(s) if T1 F5 reveals previously unknown API absence (next ID: NT8-038) |

### T1.4 Files MUST NOT Touch

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\AtrSizingEngine.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

### T1.5 Step-by-Step Implementation

#### Step 1 — Add `using` directives to TradeCopierPanel.cs

At the top of `TradeCopierPanel.cs`, after the existing `using` block (currently ending
at `using NinjaTrader.Gui.Chart;` — line 113), add the following two lines in alpha order:

```csharp
using System.Reflection;     // B16 T1 diagnostic -- removed in T2
using System.Text;            // B16 T1 diagnostic -- removed in T2 (not present before B16)
```

Current `using` block ends at line 113. Insert both new lines immediately before line 114
(the blank line before `namespace PropTraderTools`). Do not reorder existing usings.

**Verification:** `grep -n "using System.Reflection" TradeCopierPanel.cs` → 1 hit.
**Verification:** `grep -n "using System.Text" TradeCopierPanel.cs` → 1 hit (NOT present in B15).

---

#### Step 2 — Add field `_chartScaleDiagDone` in the volatile fields section

In TradeCopierPanel.cs, locate the B9 T2 volatile fields block (lines 135-141):

```csharp
// B9 T2 -- Click trader (JS-023: volatile cross-thread fields)
private volatile bool    _clickArmed  = false;
private volatile bool    _clickBuy    = true;    // true=Buy, false=SellShort
private          Chart   _currentChart = null;   // single-writer UI thread
```

Immediately **after** `_clickBuy` and **before** `_currentChart`, insert:

```csharp
// B16 T1: one-shot guard -- ChartPanel subtree diagnostic (UI thread write).
// NT8-017: volatile bool for cross-thread guard.
// Removed in T2.
private volatile bool _chartScaleDiagDone = false;
```

**Verification:** `grep -n "_chartScaleDiagDone" TradeCopierPanel.cs` → exactly 3 hits
(field declaration, guard in SetChart, assignment in WalkChartPanelChildren).

---

#### Step 3 — Modify `SetChart` (lines 285-288)

Replace the current `SetChart` body:

```csharp
// B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
public void SetChart(Chart chart)
{
    _currentChart = chart;
}
```

With the T1 modified version (CYC=2):

```csharp
// B9 T2: Store chart reference for click trader.
// B16 T1 modified: one-shot ChartPanel subtree diagnostic. CYC=2.
// Restored to CYC=1 in T2 (diagnostic call removed).
public void SetChart(Chart chart)
{
    _currentChart = chart;
    if (!_chartScaleDiagDone)          // B16 T1: one-shot guard (branch 1)
        WalkChartPanelChildren(chart);
}
```

**CYC target:** 2 (1 base + 1 `if` branch).

---

#### Step 4 — Add `WalkChartPanelChildren` method

Add the following method immediately **after** `SetChart` (currently line 289, before
`SetInstrument` at line 306). The method is `private void`, so it goes in the
"public surface" region but is private:

```csharp
// B16 T1 diagnostic: walk ChartPanel's visual children and probe for Y-to-price methods.
// One-shot: _chartScaleDiagDone prevents re-entry on subsequent SetChart calls.
// Output: MessageBox.Show -- NOT _statusText (no truncation risk).
// NT8-017: _chartScaleDiagDone is volatile bool (UI-thread write).
// Threading: called from SetChart on UI thread (Dispatcher.InvokeAsync context in TradeCopierAddOn).
//            MessageBox.Show is legal on UI thread.
// CYC=5: guard cc (1), guard panel (2), for-loop (3), FrameworkElement check (4),
//         _chartScaleDiagDone = true is not a branch = base (5 total).
// Removed entirely in T2.
//
// Required using directives (T1 only, added in Step 1):
//   using System.Reflection;
//   using System.Text;
private void WalkChartPanelChildren(Chart chart)
{
    _chartScaleDiagDone = true;                                        // set immediately -- re-entry guard

    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);
    if (cc == null) return;                                             // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null) return;                                          // guard (2)

    var sb = new StringBuilder();
    sb.AppendLine("PTT B16 -- ChartPanel Children Probe");
    sb.AppendLine("ChartPanel.ActualHeight = " + panel.ActualHeight.ToString("F2"));
    sb.AppendLine("ChartPanel.ActualWidth  = " + panel.ActualWidth.ToString("F2"));
    sb.AppendLine();

    int count = VisualTreeHelper.GetChildrenCount(panel);
    sb.AppendLine("ChildCount = " + count);

    for (int i = 0; i < count; i++)                                    // loop (3)
    {
        var child = VisualTreeHelper.GetChild(panel, i);
        sb.AppendLine("  [" + i + "] " + child.GetType().FullName);

        if (child is System.Windows.FrameworkElement fe)               // check (4)
            sb.AppendLine("       ActualHeight=" + fe.ActualHeight.ToString("F2")
                        + " ActualWidth=" + fe.ActualWidth.ToString("F2"));

        sb.AppendLine(BuildMethodReport(child.GetType()));
    }

    System.Windows.MessageBox.Show(sb.ToString(), "PTT B16 ChartPanel Subtree");
}
```

**CYC target:** 5.

---

#### Step 5 — Add `BuildMethodReport` method

Add the following static method immediately **after** `WalkChartPanelChildren`:

```csharp
// B16 T1 diagnostic helper: returns a multi-line string of method signatures on type t
// whose names (case-insensitive) contain "value", "price", "gety", or equal "y".
// Removed in T2.
// CYC=2: foreach (1), if !match continue (2).
private static string BuildMethodReport(Type t)
{
    var sb = new StringBuilder();
    var methods = t.GetMethods(
        BindingFlags.Public | BindingFlags.Instance);

    foreach (var m in methods)                                         // (1)
    {
        string nameLower = m.Name.ToLower(
            System.Globalization.CultureInfo.InvariantCulture);

        bool match = nameLower.Contains("value")
                  || nameLower.Contains("price")
                  || nameLower.Contains("gety")
                  || (nameLower == "y");                               // exact "y" only
        if (!match) continue;                                          // (2)

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

**CYC target:** 2.

---

#### Step 6 — F5 Gate

1. Open NinjaTrader on Sim101.
2. Load `PropTraderTools.csproj` via the NinjaTrader Tools > Edit NinjaScript > Add-On menu
   (or `dotnet build` then F5 in the IDE). **Zero compiler errors required.**
3. Open any chart instrument (e.g. NQ, ES). The `SetChart` call fires from
   `TradeCopierAddOn.DoInject()` on the UI thread.
4. `MessageBox.Show` must appear exactly once with title `"PTT B16 ChartPanel Subtree"`.
5. Read and photograph/copy the full MessageBox text.
6. If the MessageBox does NOT appear: verify `_chartScaleDiagDone` volatile guard,
   `SetChart` modification, and `WalkChartPanelChildren` are all present.
7. If a compiler error appears: record the CS code and update `NT8_COMPILER_RULES.md`
   with new rule (next ID: NT8-038 or higher as appropriate).

---

#### Step 7 — Record findings in NT8_ADDON_KNOWLEDGE.md

Append the following section to
`c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md`:

```
## B16 Discoveries

### T1 F5 Output (run date: YYYY-MM-DD)

ChartPanel.ActualHeight = <ACTUAL>
ChartPanel.ActualWidth  = <ACTUAL>
ChildCount = <N>

[0] <TypeFullName>
    ActualHeight=<H>  ActualWidth=<W>
    Method: <Name>(<params>) -> <ReturnType>   [repeat for each matched method]

[1] <TypeFullName>
    ...

### T1 Branch Decision

Based on T1 F5 output:
- Branch A selected: YES / NO
  Reason: [e.g. "ChartScale.ValueFromY(Double y) -> Double found at index 0"]
     OR
  Reason: [e.g. "No child with matching Y-to-price method found"]

- Branch B selected: YES / NO

### T1 Correction Factor Data

ChartPanel.ActualHeight  = <ACTUAL>
ChartScale.ActualHeight  = <ACTUAL>  (or "not found")
Correction factor        = <ChartScale.ActualHeight / ChartPanel.ActualHeight>
                           (used in Branch B CORRECTION_FACTOR constant)
```

Replace all `<ACTUAL>`, `<N>`, `<TypeFullName>` etc. with actual values from the
MessageBox output. Do not leave angle-bracket placeholders in the committed document.

---

#### Step 8 — Update NT8_COMPILER_RULES.md (conditional)

If T1 F5 raises a new CS compiler error not covered by NT8-001 through NT8-037:
1. Assign the next available rule ID (currently NT8-038).
2. Fill in all fields: SEVERITY, CONFIRMED, ERROR, CAUSE, BANNED, SAFE, SCAN.
3. Append to `docs/standards/NT8_COMPILER_RULES.md` in the correct category section.
4. State `nt8-rules(B16-T1): no new rules` in the completion report if nothing new is found.

If `System.Reflection` usage inside NT8's Roslyn causes any CS error at F5, add a rule
documenting the constraint (e.g. "BindingFlags.Instance requires full namespace qualification").

---

### T1.6 Method Signatures

| Method | Access | Return | Parameters | CYC |
|--------|--------|--------|-----------|-----|
| `SetChart` (T1 modified) | `public` | `void` | `Chart chart` | 2 |
| `WalkChartPanelChildren` | `private` | `void` | `Chart chart` | 5 |
| `BuildMethodReport` | `private static` | `string` | `Type t` | 2 |

**Exact signatures (C# declaration form):**

```csharp
public void SetChart(Chart chart)
private void WalkChartPanelChildren(Chart chart)
private static string BuildMethodReport(Type t)
```

---

### T1.7 JS Rule Constraints

| Rule | Constraint | Applied Where |
|------|-----------|--------------|
| JS-021 | No `lock()` | No lock anywhere in new code |
| JS-023 | `volatile bool` for cross-thread field | `_chartScaleDiagDone` is `private volatile bool` |
| JS-033 | No `async void` | All new methods are sync `void` or `static string` |
| JS-002 | No `return null` | `BuildMethodReport` returns `sb.ToString()` (never null); void methods use bare `return;` |
| NT8-017 | `volatile` mandatory for cross-thread bool | `_chartScaleDiagDone = true` on UI thread; `if (!_chartScaleDiagDone)` on any thread |
| NT8-018 | No `lock()` | Confirmed absent |
| NT8-019 | No `async void` | Confirmed absent |
| NT8-028 | No hex color literals | No color changes in T1 |
| NT8-031 | `System.Reflection` requires explicit `using` | `using System.Reflection;` added in Step 1 |

---

### T1.8 Tests

**None.** T1 adds diagnostic code that depends on NT8 runtime visual tree state. This
state is unavailable in xUnit. `BuildMethodReport` takes a `Type` argument but its output
is non-deterministic (NT8 type methods change across builds). No pure-math test surface
exists in T1. Test coverage begins in T2.

---

### T1.9 NT8_ADDON_KNOWLEDGE.md Update

The engineer **must** update `docs/standards/NT8_ADDON_KNOWLEDGE.md` as specified in
Step 7 before filing `ticket-1-completion.md`. A completion without the B16 Discoveries
section filled in = INCOMPLETE.

---

### T1.10 7-Scan Checklist (T1)

Run each scan command in the Wave workspace root
(`c:\WSGTA\universal-or-strategy\`). Record actual results in brackets.

```
SCAN-01: lock() in TradeCopierPanel.cs
  Command:  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-02: async void in TradeCopierPanel.cs
  Command:  grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-03: DateTime.Now (non-UTC) in TradeCopierPanel.cs
  Command:  grep -n "DateTime\.Now[^U]" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-04: hex color string literals in TradeCopierPanel.cs
  Command:  grep -n '"#[0-9A-Fa-f]' src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-05: GetValueByY call in TradeCopierPanel.cs (source lines only)
  Command:  grep -n "\.GetValueByY(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results (comment-only hits are acceptable)
  Actual:   [ ]

SCAN-06: price = 0.0 stub still present (T1 must NOT have removed it)
  Command:  grep -n "price\s*=\s*0\.0" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 1 result (stub remains; GetPriceAtY not yet replaced in T1)
  Actual:   [ ]

SCAN-07: T_B16_ test names in CopyEngineTests.cs
  Command:  grep -n "T_B16_" src/PropTraderTools/CopyEngineTests.cs
  Expected: 0 results (no T1 tests)
  Actual:   [ ]
```

All 7 scans must be attested before T1 is COMPLETE. Any unexpected result = stop and
report to ptt-ticket-reviewer before filing completion.

---

### T1.11 BUILD_PASS Criteria

T1 is BUILD_PASS when ALL of the following are true:

1. ☐ `dotnet build` (or F5 in NT8 NinjaScript editor) returns zero errors.
2. ☐ `MessageBox.Show` fires exactly once with title `"PTT B16 ChartPanel Subtree"` on
       first chart open. Subsequent chart opens do NOT trigger a second MessageBox
       (`_chartScaleDiagDone = true` guard confirmed working).
3. ☐ MessageBox text contains at least one child type line (`[0] NinjaTrader...`).
4. ☐ `docs/standards/NT8_ADDON_KNOWLEDGE.md` `## B16 Discoveries` section written with
       actual MessageBox output (no angle-bracket placeholders).
5. ☐ `docs/standards/NT8_COMPILER_RULES.md` updated if new rule found; otherwise states
       `nt8-rules(B16-T1): no new rules` in completion report.
6. ☐ All 7 scan results recorded in T1.10 with actual values.

---

### T1.12 Gate Statement for T2

**T2 is BLOCKED until T1 VERIFY_PASS.**

The verifier (ptt-verifier) must confirm:
- MessageBox evidence present (screenshot or pasted text in `ticket-1-completion.md`)
- `NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries` populated with actual F5 values
- Branch A or Branch B decision stated explicitly
- Correction factor value stated (for Branch B) or N/A stated (for Branch A)

Only after `ticket-1-verification.md` is written with VERIFY_PASS status may T2 begin.

---
---

## TICKET T2 — GetPriceAtY Implementation (Gated on T1 VERIFY_PASS)

### T2.1 Metadata

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Title | Replace GetPriceAtY stub with real Y-pixel-to-price conversion |
| Block | PTT-COPIER-B16 |
| Spec requirements | DW-B16-01 (close or document conditional failure); DW-B16-02 (TightenOneStop fix) |
| Depends on | T1 VERIFY_PASS (HARD GATE — do not start T2 without T1 VERIFY_PASS) |
| Engineer | ptt-engineer |
| Verifier | ptt-verifier |

### T2.2 Goal

Replace the `GetPriceAtY` B15 stub (which returns `MarketData.Last.Price` ignoring Y) with
a real Y-pixel-to-price conversion. Remove all T1 diagnostic code. Add `LinearYToPrice`
and `AlignToTick` internal static helpers. Add 10 xUnit `[Fact]` tests. Close DW-B16-01
(or document conditional failure if all API paths fail). Also fix DW-B16-02:
remove the `IsTrailingStop` cancel+replace branch from `TightenOneStop()` in `CopyEngine.cs`,
and rename the `"~"` button to `"Tighten"` in `TradeCopierPanel.cs`.

**The engineer picks Branch A or Branch B before writing any code**, based on the
`ticket-1-verification.md` VERIFY_PASS finding stated by ptt-verifier.

---

### T2.3 Branch Decision Rules

Read `docs/brain/PTT-COPIER-B16/ticket-1-verification.md` before writing any code.

**Branch A applies when:** T1 MessageBox output shows a child of `ChartPanel` that exposes
a method returning a `double` (or `Single`) and accepting a `double` (or `Single`) parameter
whose name matches any of: `GetValue`, `GetPrice`, `GetY`, `ValueFromY`, `YToValue`,
`ValueAtY`, or any name containing "price", "value", or "gety" (case-insensitive).

**Branch B applies when:** T1 MessageBox output enumerates all children of `ChartPanel`
and none has a method matching the name filter above.

Record the branch chosen at the top of `ticket-2-completion.md`:
```
BRANCH_CHOSEN: A  (or B)
REASON: [one sentence citing the T1 MessageBox evidence]
```

---

### T2.4 Files to Modify (Wave Workspace)

| File | Change |
|------|--------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Remove T1 code; replace GetPriceAtY; add 2 helpers; restore SetChart CYC=1; remove 2 usings; rename "~" button to "Tighten" (DW-B16-02) |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Remove IsTrailingStop cancel+replace branch from TightenOneStop() (DW-B16-02); CYC 4→3 |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Add 10 [Fact] tests: T_B16_01 through T_B16_08 (price math) + T_B16_09, T_B16_10 (TightenOneStop) |
| `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` | Append T2 result to `## B16 Discoveries` |
| `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md` | Add new rule(s) if T2 F5 reveals CS1061 on RoundToTickSize / MaxValue / MinValue |

### T2.5 Files MUST NOT Touch

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\AtrSizingEngine.cs`

---

### T2.6 Step-by-Step Implementation

#### Step 0 — Fix TightenOneStop in CopyEngine.cs (DW-B16-02)

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

Locate `TightenOneStop()` (lines ~1196-1243). The current method has a header comment
`// CYC=4` and an `if (IsTrailingStop(order)) { ... } else { ... }` block inside the
`try { }`. Remove the entire if/else and replace with the single `acc.Change()` path:

**REMOVE** (lines ~1214-1237 — the full if/else):
```csharp
                if (IsTrailingStop(order))                                              // (3) cancel+replace for trailing
                {
                    acc.Cancel(new Order[] { order });
                    acc.CreateOrder(
                        instr,
                        order.OrderAction,
                        OrderType.StopMarket,
                        OrderEntry.Manual,
                        TimeInForce.Day,
                        order.Quantity,
                        0,
                        targetPrice,
                        null,
                        "PTT-Tighten-Stop",
                        DateTime.MaxValue,
                        (NinjaTrader.Cbi.CustomOrder)null);    // NT8-007: arg 12 = (CustomOrder)null
                    StatusUpdate?.Invoke(acc.Name + ": tighten trail cancel+replace -> " + targetPrice);
                }
                else                                                                   // fixed stop: acc.Change()
                {
                    order.StopPrice = targetPrice;
                    acc.Change(new Order[] { order });
                    StatusUpdate?.Invoke(acc.Name + ": tighten stop -> " + targetPrice);
                }
```

**REPLACE WITH** (single path, no if/else):
```csharp
                // DW-B16-02: all stop types use acc.Change() -- GAP-001d CONFIRMED safe.
                // cancel+replace branch removed (was nuking ATM bracket + trail watermark).
                order.StopPrice = targetPrice;
                acc.Change(new Order[] { order });
                StatusUpdate?.Invoke(acc.Name + ": tighten stop -> " + targetPrice);
```

**Update the method header comment** (lines ~1196-1200):
- Change `// CYC=4: null guard(1), alreadyTighter(2), TrailPrice>0 cancel+replace(3), try block(0).`
  to `// CYC=3: null guard(1), alreadyTighter(2), try block(0). DW-B16-02: cancel+replace removed.`
- Remove the line `// NT8-007: arg 12 of CreateOrder = (NinjaTrader.Cbi.CustomOrder)null.`
  (CreateOrder call no longer present)
- Remove the line `// PTT- prefix: "PTT-Tighten-Stop" (SCAN-05 compliant).`
  (signal name no longer used)

**Verification after Step 0:**
- `grep -n "PTT-Tighten-Stop" src/PropTraderTools/CopyEngine.cs` → 0 results
- `grep -n "IsTrailingStop" src/PropTraderTools/CopyEngine.cs` → matches only the
  method definition of `IsTrailingStop` itself, NOT a call inside `TightenOneStop`
- CYC of `TightenOneStop` = 3 (null guard, alreadyTighter guard, try block does not add CYC)

---

#### Step 0b — Rename "~" button to "Tighten" in TradeCopierPanel.cs BuildUI (DW-B16-02)

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

Search for the `"~"` button content string in `BuildUI` (the Tighten Stop button added in B10 T3).

```
grep -n '"~"' src/PropTraderTools/TradeCopierPanel.cs
```

Change `Content = "~"` (or `Content="~"` or `.Content = "~"`) to `Content = "Tighten"`.

**Verification after Step 0b:**
- `grep -n '"~"' src/PropTraderTools/TradeCopierPanel.cs` → 0 results
- `grep -n '"Tighten"' src/PropTraderTools/TradeCopierPanel.cs` → 1 result in BuildUI

---

#### Step 1 — Remove all T1 diagnostic code from TradeCopierPanel.cs

Make ALL of the following removals in a single edit pass:

| Item to Remove | Location |
|---------------|---------|
| `using System.Reflection;` | Top of file (added in T1 Step 1) |
| `using System.Text;` | Top of file (added in T1 Step 1; was absent before B16) |
| `private volatile bool _chartScaleDiagDone = false;` field and its 3-line comment block | Volatile fields section |
| `if (!_chartScaleDiagDone) WalkChartPanelChildren(chart);` line and its preceding comment | Body of `SetChart` |
| Entire `WalkChartPanelChildren(Chart chart)` method (approx 25 lines including comment header) | After SetChart |
| Entire `BuildMethodReport(Type t)` method (approx 20 lines including comment header) | After WalkChartPanelChildren |

After removals:
- `SetChart` must be back to CYC=1, body is one line: `_currentChart = chart;`
- The comment on SetChart must read `// B9 T2: Store chart reference. CYC=1 (straight-line).`
  (update the "B16 T1 modified" annotation — restore the original single-line comment)
- `grep -n "_chartScaleDiagDone" TradeCopierPanel.cs` → 0 results
- `grep -n "WalkChartPanelChildren" TradeCopierPanel.cs` → 0 results
- `grep -n "BuildMethodReport" TradeCopierPanel.cs` → 0 results
- `grep -n "using System.Reflection" TradeCopierPanel.cs` → 0 results
- `grep -n "using System.Text" TradeCopierPanel.cs` → 0 results

---

#### Step 2 — Replace `GetPriceAtY` (lines ~298-304 after T1 edits)

**If Branch A chosen (native NT8 API found):**

Replace the entire `GetPriceAtY` body with the Branch A implementation.
Substitute `{ChildType}` with the exact type full name found in T1 F5 (e.g.
`NinjaTrader.Gui.Chart.ChartScale`). Substitute `{ConfirmedMethod}` with the exact method
name found in T1 F5 (e.g. `ValueFromY`). Engineer MUST fill in both placeholders — leaving
`{ChildType}` or `{ConfirmedMethod}` as literal text is an error.

```csharp
// B16 T2 Branch A: Use confirmed NT8 native API on ChartPanel child.
// {ChildType} = [FILL IN from T1 F5, e.g. NinjaTrader.Gui.Chart.ChartScale]
// {ConfirmedMethod} = [FILL IN from T1 F5, e.g. ValueFromY]
// NT8-008: cc arrives from FindVisualChild<ChartControl>(chart) in OnChartMouseDown.
// NT8-029: tick alignment via RoundToTickSize (UNCONFIRMED -- add NT8-038 if CS1061).
// NT8-032: MarketData.Last.Price fallback (confirmed B12).
// CYC=4: cc null(1), panel null(2), scale null(3), raw <= 0(4).
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (cc == null) return 0.0;                                        // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null)                                                  // guard (2)
        goto fallback;

    var scale = TradeCopierAddOn.FindVisualChild<{ChildType}>(panel);
    if (scale == null)                                                  // guard (3)
        goto fallback;

    double rawPrice = scale.{ConfirmedMethod}(y);
    if (rawPrice <= 0.0)                                                // guard (4)
        goto fallback;

    return instrument != null
        ? instrument.MasterInstrument.RoundToTickSize(rawPrice)         // NT8-native tick align
        : rawPrice;

fallback:
    if (instrument == null) return 0.0;
    var last = instrument.MarketData.Last;
    return last != null ? last.Price : 0.0;                            // NT8-032
}
```

**If `RoundToTickSize` raises CS1061 at F5:**
1. Replace `instrument.MasterInstrument.RoundToTickSize(rawPrice)` with:
   ```csharp
   AlignToTick(rawPrice, instrument.MasterInstrument.TickSize)
   ```
2. Add NT8-038 rule to `NT8_COMPILER_RULES.md` (see Step 6).

---

**If Branch B chosen (linear interpolation):**

Replace the entire `GetPriceAtY` body with the Branch B implementation.
Set `CORRECTION_FACTOR` to the float derived from T1 `ActualHeight` readings
(correction factor = ChartScale.ActualHeight / ChartPanel.ActualHeight, recorded in
`NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries`). Engineer MUST replace the placeholder
`1.0` with the measured value — leaving `1.0` is acceptable ONLY IF T1 confirmed that
the price scale spans the full ChartPanel height (i.e. correction factor = 1.0 exactly).
Document the chosen value in `ticket-2-completion.md`.

```csharp
// B16 T2 Branch B: Linear interpolation via ChartPanel.MaxValue / MinValue / ActualHeight.
// Approximation: pixel-to-price is linear on NT8 default linear scale.
// Correction factor derived from T1 ActualHeight readings (see NT8_ADDON_KNOWLEDGE.md B16).
// NT8-029 replacement: RoundToTickSize (UNCONFIRMED -- add NT8-039 if CS1061 on MaxValue).
// NT8-032: Last.Price fallback if ChartPanel.MaxValue/MinValue absent (CS1061).
// CYC=5: cc null(1), panel null(2), height <= 0(3), raw <= 0(4), instrument null(5).
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
{
    if (cc == null) return 0.0;                                        // guard (1)

    var panel = TradeCopierAddOn.FindVisualChild<ChartPanel>(cc);
    if (panel == null) return 0.0;                                     // guard (2)

    double panelH = panel.ActualHeight;
    if (panelH <= 0.0) return 0.0;                                     // guard (3): no divide by zero

    // CORRECTION_FACTOR: set from T1 ActualHeight measurement.
    // 1.0 = price scale spans full ChartPanel height (no margin correction).
    // Example: 0.95 = price area is 95% of ChartPanel.ActualHeight.
    const double CORRECTION_FACTOR = 1.0;                              // FILL IN from T1 data

    double maxVal   = panel.MaxValue;
    double minVal   = panel.MinValue;
    double yRatio   = y / (panelH * CORRECTION_FACTOR);
    double rawPrice = maxVal - yRatio * (maxVal - minVal);

    if (rawPrice <= 0.0) return 0.0;                                   // guard (4): sanity

    if (instrument == null) return 0.0;                                // guard (5)
    return instrument.MasterInstrument.RoundToTickSize(rawPrice);      // NT8-native tick align
}
```

**If `ChartPanel.MaxValue` or `ChartPanel.MinValue` raises CS1061 at F5:**
1. Add NT8-039 (MaxValue absent) to `NT8_COMPILER_RULES.md`.
2. Add NT8-040 (MinValue absent) if also absent.
3. Fall back to `instrument.MarketData.Last.Price` (same as B15).
4. DW-B16-01 remains OPEN. Document in `ticket-2-completion.md`.

**If `RoundToTickSize` raises CS1061 at F5 (in either branch):**
1. Replace with `AlignToTick(rawPrice, instrument.MasterInstrument.TickSize)`.
2. Add NT8-038 rule to `NT8_COMPILER_RULES.md`.

---

#### Step 3 — Add `LinearYToPrice` internal static helper

Add the following method to TradeCopierPanel.cs, immediately **after** the `GetPriceAtY`
method (in both branches — the helper is added regardless of which branch is chosen):

```csharp
// B16 T2: Pure-math linear Y-to-price interpolation helper.
// Internal static for xUnit test access via Reflection (CopyEngineTests.cs pattern).
// Formula: rawPrice = maxVal - (y / (panelH * correctionFactor)) * (maxVal - minVal)
// CYC=2: height guard(1), raw guard(2).
internal static double LinearYToPrice(
    double y, double panelH, double maxVal, double minVal, double correctionFactor)
{
    if (panelH <= 0.0) return 0.0;                                     // guard (1): no divide by zero
    double yRatio   = y / (panelH * correctionFactor);
    double rawPrice = maxVal - yRatio * (maxVal - minVal);
    if (rawPrice <= 0.0) return 0.0;                                   // guard (2): sanity
    return rawPrice;
}
```

**CYC target:** 2.

---

#### Step 4 — Add `AlignToTick` internal static helper

Add the following method immediately **after** `LinearYToPrice`:

```csharp
// B16 T2: Pure-math tick alignment helper.
// Mirrors NT8-native RoundToTickSize semantics via Math.Round AwayFromZero.
// Internal static for xUnit test access via Reflection (CopyEngineTests.cs pattern).
// NT8-029: replaces raw price with nearest tick boundary.
// CYC=2: tickSize guard(1), straight-line (2).
internal static double AlignToTick(double raw, double tickSize)
{
    if (tickSize <= 0.0) return raw;                                    // guard (1)
    return Math.Round(raw / tickSize, MidpointRounding.AwayFromZero) * tickSize;
}
```

**CYC target:** 2 (the guard is a branch; the comment header says CYC=1 but correctly
counts as CYC=2 with the guard. Both values ≤ 8. Plan review confirmed this is acceptable.)

---

#### Step 5 — Add 8 [Fact] tests to CopyEngineTests.cs

Append the following 8 test methods to the `CopyEngineTests` class body, immediately
**before** the closing `}` of the class (currently the last `}` before the namespace
closing `}`). Each test is a flat `[Fact]` method — CYC=1 each.

The tests call `TradeCopierPanel.LinearYToPrice(...)` and `TradeCopierPanel.AlignToTick(...)`
via `System.Reflection.MethodInfo` (following the established pattern in `CopyEngineTests`:
see `GetField`/`GetMethod` helpers — they use `BindingFlags.NonPublic | BindingFlags.Instance`).
Because `LinearYToPrice` and `AlignToTick` are `internal static`, use:
```csharp
typeof(TradeCopierPanel).GetMethod("LinearYToPrice",
    BindingFlags.NonPublic | BindingFlags.Static)
    .Invoke(null, new object[] { y, panelH, maxVal, minVal, cf })
```

**However**, for readability and to avoid per-test Reflection boilerplate, add two private
static helper methods to `CopyEngineTests` (before the 8 facts):

```csharp
private static double CallLinearYToPrice(
    double y, double panelH, double maxVal, double minVal, double cf)
{
    return (double)typeof(TradeCopierPanel)
        .GetMethod("LinearYToPrice",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        .Invoke(null, new object[] { y, panelH, maxVal, minVal, cf });
}

private static double CallAlignToTick(double raw, double tickSize)
{
    return (double)typeof(TradeCopierPanel)
        .GetMethod("AlignToTick",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        .Invoke(null, new object[] { raw, tickSize });
}
```

Then add the 8 `[Fact]` methods:

```csharp
[Fact]
public void T_B16_01_LinearPriceInterp_TopOfChart_ReturnsMaxValue()
{
    // y=0 (top of panel) must return maxVal unchanged.
    double result = CallLinearYToPrice(0.0, 400.0, 5000.0, 4900.0, 1.0);
    Assert.Equal(5000.0, result, 5);
}

[Fact]
public void T_B16_02_LinearPriceInterp_BottomOfChart_ReturnsMinValue()
{
    // y=panelH (bottom of panel) must return minVal.
    double result = CallLinearYToPrice(400.0, 400.0, 5000.0, 4900.0, 1.0);
    Assert.Equal(4900.0, result, 5);
}

[Fact]
public void T_B16_03_LinearPriceInterp_MiddleOfChart_ReturnsMidpoint()
{
    // y=200 (mid panel) must return (5000+4900)/2 = 4950.
    double result = CallLinearYToPrice(200.0, 400.0, 5000.0, 4900.0, 1.0);
    Assert.Equal(4950.0, result, 5);
}

[Fact]
public void T_B16_04_LinearPriceInterp_QuarterFromTop_ReturnsThreeQuarterRange()
{
    // y=100 (quarter from top) must return maxVal - 0.25*(maxVal-minVal) = 5000-25 = 4975.
    double result = CallLinearYToPrice(100.0, 400.0, 5000.0, 4900.0, 1.0);
    Assert.Equal(4975.0, result, 5);
}

[Fact]
public void T_B16_05_LinearPriceInterp_ZeroHeight_ReturnsZero()
{
    // panelH=0 triggers the divide-by-zero guard -- must return 0.0 not throw.
    double result = CallLinearYToPrice(100.0, 0.0, 5000.0, 4900.0, 1.0);
    Assert.Equal(0.0, result, 5);
}

[Fact]
public void T_B16_06_AlignToTick_ValueBelowMidTick_RoundsDown()
{
    // raw=4975.10, tick=0.25: 4975.10/0.25=19900.4 -> AwayFromZero rounds to 19900 -> 4975.00
    double result = CallAlignToTick(4975.10, 0.25);
    Assert.Equal(4975.00, result, 5);
}

[Fact]
public void T_B16_07_AlignToTick_ValueAboveMidTick_RoundsUp()
{
    // raw=4975.15, tick=0.25: 4975.15/0.25=19900.6 -> AwayFromZero rounds to 19901 -> 4975.25
    double result = CallAlignToTick(4975.15, 0.25);
    Assert.Equal(4975.25, result, 5);
}

[Fact]
public void T_B16_08_AlignToTick_ExactTickBoundary_Unchanged()
{
    // raw=4975.25, tick=0.25: 4975.25/0.25=19901.0 -> exact boundary -> 4975.25
    double result = CallAlignToTick(4975.25, 0.25);
    Assert.Equal(4975.25, result, 5);
}
```

---

#### Step 6 — Update NT8_COMPILER_RULES.md (conditional on F5 findings)

Run F5 in NT8. For each CS1061 encountered on unconfirmed APIs, add the following rules:

**If `instrument.MasterInstrument.RoundToTickSize(double)` → CS1061:**
```
### NT8-038 | P0 | `MasterInstrument.RoundToTickSize(double)` DOES NOT EXIST — CS1061
CONFIRMED: B16 T2 (CS1061)
ERROR: CS1061 "'MasterInstrument' does not contain definition for 'RoundToTickSize'"
CAUSE: ...
BANNED: instrument.MasterInstrument.RoundToTickSize(rawPrice)
SAFE:   AlignToTick(rawPrice, instrument.MasterInstrument.TickSize)
        where AlignToTick = Math.Round(raw/tick, MidpointRounding.AwayFromZero) * tick
SCAN:   grep -n "RoundToTickSize" src/ --include="*.cs"
```

**If `ChartPanel.MaxValue` → CS1061:**
```
### NT8-039 | P0 | `ChartPanel.MaxValue` DOES NOT EXIST IN THIS NT8 BUILD — CS1061
CONFIRMED: B16 T2 (CS1061)
ERROR: CS1061 "'ChartPanel' does not contain a definition for 'MaxValue'"
CAUSE: ...
BANNED: panel.MaxValue
SAFE:   instrument.MarketData.Last.Price (fallback -- DW-B16-01 remains OPEN)
SCAN:   grep -n "\.MaxValue\b" src/ --include="*.cs"
```

**If `ChartPanel.MinValue` → CS1061:**
```
### NT8-040 | P0 | `ChartPanel.MinValue` DOES NOT EXIST IN THIS NT8 BUILD — CS1061
CONFIRMED: B16 T2 (CS1061)
[same pattern as NT8-039 for MinValue]
SCAN:   grep -n "\.MinValue\b" src/ --include="*.cs"
```

If no new rules: state `nt8-rules(B16-T2): no new rules` in `ticket-2-completion.md`.

---

#### Step 7 — Update NT8_ADDON_KNOWLEDGE.md B16 Discoveries with T2 result

Append to the `## B16 Discoveries` section (already written in T1):

```
### T2 Branch Chosen and Result

BRANCH: A / B  [delete one]
API used: [exact method call or "linear interpolation"]
RoundToTickSize: confirmed present / CS1061 -- NT8-038 added  [delete one]
ChartPanel.MaxValue: confirmed present / CS1061 -- NT8-039 added / N/A (Branch A)  [delete one]
ChartPanel.MinValue: confirmed present / CS1061 -- NT8-040 added / N/A (Branch A)  [delete one]
CORRECTION_FACTOR used: [value, e.g. 0.95 or 1.0]
DW-B16-01 status: CLOSED (real Y price implemented) / OPEN (all API paths failed)  [delete one]
```

---

#### Step 6b — Add T_B16_09 and T_B16_10 tests to CopyEngineTests.cs (DW-B16-02)

Add two private static helper methods and two `[Fact]` tests to `CopyEngineTests.cs`
for `TightenOneStop` verification. Because `TightenOneStop` is `private` and uses `Account`
and `Order` NT8 types that are unavailable in unit tests, these tests verify the **pure
logic branches** (null guard, alreadyTighter guard) without exercising NT8 order submission.

Add a private helper for the alreadyTighter check (mirrors the method's internal logic):

```csharp
private static bool IsAlreadyTighter(bool isLong, double stopPrice, double targetPrice)
{
    return isLong ? stopPrice >= targetPrice : stopPrice <= targetPrice;
}
```

Then add the two `[Fact]` tests:

```csharp
[Fact]
public void T_B16_09_TightenOneStop_AlreadyTighterLong_ReturnsEarly()
{
    // Long position: stopPrice >= targetPrice means already as tight or tighter.
    // alreadyTighter should be true -- no further action needed.
    bool result = IsAlreadyTighter(isLong: true, stopPrice: 4975.00, targetPrice: 4970.00);
    Assert.True(result);
}

[Fact]
public void T_B16_10_TightenOneStop_NotYetTighterLong_ProceedsToChange()
{
    // Long position: stopPrice < targetPrice means stop can move closer.
    // alreadyTighter should be false -- acc.Change() path will execute.
    bool result = IsAlreadyTighter(isLong: true, stopPrice: 4960.00, targetPrice: 4970.00);
    Assert.False(result);
}
```

These tests confirm the guard logic is correct after the DW-B16-02 if/else removal.
They are pure C# — no NT8 types required. CYC=1 each.

---

#### Step 8 — F5 Gate (T2)

1. Ensure all T1 diagnostic code is removed (Step 1 verification commands pass).
2. `dotnet build` or F5 in NT8 → zero compiler errors.
3. Open a chart instrument in Sim101. Click on the chart while click trader is armed.
4. Verify in the NT8 Order Entry audit log or positions that the limit order was placed at
   or near the Y position clicked (not at 0.0, not at last-trade only when click is different).
5. If Branch B and the price is off: adjust `CORRECTION_FACTOR` and repeat.
6. Run `dotnet test` → all 10 T_B16_ tests green.

---

### T2.7 Method Signatures

| Method | Access | Return | Parameters | CYC |
|--------|--------|--------|-----------|-----|
| `SetChart` (T2 restored) | `public` | `void` | `Chart chart` | 1 |
| `GetPriceAtY` (Branch A) | `private static` | `double` | `ChartControl cc, double y, Instrument instrument` | 4 |
| `GetPriceAtY` (Branch B) | `private static` | `double` | `ChartControl cc, double y, Instrument instrument` | 5 |
| `LinearYToPrice` | `internal static` | `double` | `double y, double panelH, double maxVal, double minVal, double correctionFactor` | 2 |
| `AlignToTick` | `internal static` | `double` | `double raw, double tickSize` | 2 |

**Exact signatures (C# declaration form):**

```csharp
public void SetChart(Chart chart)
private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
internal static double LinearYToPrice(double y, double panelH, double maxVal, double minVal, double correctionFactor)
internal static double AlignToTick(double raw, double tickSize)
```

**UNCHANGED method (must not be touched):**

```csharp
internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)   // CYC=7, unchanged
```

---

### T2.8 JS Rule Constraints

| Rule | Constraint | Applied Where |
|------|-----------|--------------|
| JS-021 | No `lock()` | No lock in any new or modified method |
| JS-023 | Volatile for cross-thread bool | `_clickArmed`, `_clickBuy` unchanged and remain `volatile` |
| JS-033 | No `async void` | All new methods are `static double` or `void`; no async |
| JS-002 | No `return null` | All returns are `0.0` (double) or `raw` (double); no null returns |
| NT8-013 | `DateTime.MaxValue` in CreateOrder | Line 1145 unchanged |
| NT8-014 | Signal name starts with "PTT-" | `"PTT-Click"` on line 1144 unchanged |
| NT8-017 | Volatile for cross-thread | No new cross-thread fields in T2 |
| NT8-018 | No lock() | Confirmed absent |
| NT8-019 | No async void | Confirmed absent |
| NT8-028 | No hex color | No color changes in T2 |
| NT8-029 | Tick alignment | `RoundToTickSize` or `AlignToTick` fallback |
| NT8-032 | MarketData.Last.Price | Fallback uses `.Last.Price` correctly |
| NT8-035 | No 0.0 stub in price | GetPriceAtY T2 replaces stub — DW-B16-01 closes |
| NT8-036 | ChartControl.ChartBars absent | Not used; FindVisualChild<ChartPanel> only |
| NT8-037 | ChartPanel.GetValueByY absent | Not called; replacement strategy used |

---

### T2.9 [Fact] Test Specifications

File: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

Access pattern: `System.Reflection.BindingFlags.NonPublic | BindingFlags.Static` via
`CallLinearYToPrice` and `CallAlignToTick` private helpers (added to `CopyEngineTests`).

| Test Name | Inputs | Expected Output | Assertion |
|-----------|--------|----------------|-----------|
| `T_B16_01_LinearPriceInterp_TopOfChart_ReturnsMaxValue` | y=0, panelH=400, max=5000, min=4900, cf=1.0 | 5000.0 | `Assert.Equal(5000.0, result, 5)` |
| `T_B16_02_LinearPriceInterp_BottomOfChart_ReturnsMinValue` | y=400, panelH=400, max=5000, min=4900, cf=1.0 | 4900.0 | `Assert.Equal(4900.0, result, 5)` |
| `T_B16_03_LinearPriceInterp_MiddleOfChart_ReturnsMidpoint` | y=200, panelH=400, max=5000, min=4900, cf=1.0 | 4950.0 | `Assert.Equal(4950.0, result, 5)` |
| `T_B16_04_LinearPriceInterp_QuarterFromTop_ReturnsThreeQuarterRange` | y=100, panelH=400, max=5000, min=4900, cf=1.0 | 4975.0 | `Assert.Equal(4975.0, result, 5)` |
| `T_B16_05_LinearPriceInterp_ZeroHeight_ReturnsZero` | y=100, panelH=0, max=5000, min=4900, cf=1.0 | 0.0 | `Assert.Equal(0.0, result, 5)` |
| `T_B16_06_AlignToTick_ValueBelowMidTick_RoundsDown` | raw=4975.10, tick=0.25 | 4975.00 | `Assert.Equal(4975.00, result, 5)` |
| `T_B16_07_AlignToTick_ValueAboveMidTick_RoundsUp` | raw=4975.15, tick=0.25 | 4975.25 | `Assert.Equal(4975.25, result, 5)` |
| `T_B16_08_AlignToTick_ExactTickBoundary_Unchanged` | raw=4975.25, tick=0.25 | 4975.25 | `Assert.Equal(4975.25, result, 5)` |
| `T_B16_09_TightenOneStop_AlreadyTighterLong_ReturnsEarly` | isLong=true, stop=4975.00, target=4970.00 | true | `Assert.True(result)` |
| `T_B16_10_TightenOneStop_NotYetTighterLong_ProceedsToChange` | isLong=true, stop=4960.00, target=4970.00 | false | `Assert.False(result)` |

All 10 tests are `[Fact]`, CYC=1. `dotnet test` must return 10/10 pass.

---

### T2.10 NT8_ADDON_KNOWLEDGE.md Update

Append to `## B16 Discoveries` as specified in Step 7 of T2.6. The section must include:
- Branch chosen and one-line justification
- Whether `RoundToTickSize` compiled or raised CS1061
- Whether `MaxValue`/`MinValue` compiled or raised CS1061 (Branch B only)
- `CORRECTION_FACTOR` value used (Branch B) or N/A (Branch A)
- Final DW-B16-01 status: CLOSED or OPEN with reason

---

### T2.11 NT8_COMPILER_RULES.md Update

Add NT8-038, NT8-039, NT8-040 rules as applicable per T2 F5 findings (Step 6).
If no new rules: state `nt8-rules(B16-T2): no new rules` in `ticket-2-completion.md`.

---

### T2.12 7-Scan Checklist (T2)

Run each scan command in the Wave workspace root
(`c:\WSGTA\universal-or-strategy\`). Record actual results in brackets.

```
SCAN-01: lock() in TradeCopierPanel.cs
  Command:  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-02: async void in TradeCopierPanel.cs
  Command:  grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-03: DateTime.Now (non-UTC) in TradeCopierPanel.cs
  Command:  grep -n "DateTime\.Now[^U]" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-04: hex color string literals in TradeCopierPanel.cs
  Command:  grep -n '"#[0-9A-Fa-f]' src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results
  Actual:   [ ]

SCAN-05: GetValueByY call in TradeCopierPanel.cs (source lines only)
  Command:  grep -n "\.GetValueByY(" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results (comment-only hits are acceptable; code hits are a violation)
  Actual:   [ ]

SCAN-06: price = 0.0 stub eliminated (T2 must replace the B15 stub)
  Command:  grep -n "price\s*=\s*0\.0" src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results (Branch A or B must replace stub with real lookup)
  Note:     If Branch B fails with CS1061 on MaxValue/MinValue AND RoundToTickSize,
            and engineer falls back to Last.Price, the stub pattern is absent but the
            variable `rawPrice` from Last.Price is non-zero -- SCAN-06 still passes.
            Document the fallback in ticket-2-completion.md.
  Actual:   [ ]

SCAN-07: T_B16_ test names in CopyEngineTests.cs (T2 must add exactly 10)
  Command:  grep -n "T_B16_" src/PropTraderTools/CopyEngineTests.cs
  Expected: 10 results (T_B16_01 through T_B16_10)
  Actual:   [ ]

SCAN-08: PTT-Tighten-Stop signal name removed from CopyEngine.cs
  Command:  grep -n "PTT-Tighten-Stop" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results (CreateOrder call removed in DW-B16-02 fix)
  Actual:   [ ]

SCAN-09: "~" button label removed from TradeCopierPanel.cs
  Command:  grep -n '"~"' src/PropTraderTools/TradeCopierPanel.cs
  Expected: 0 results (renamed to "Tighten")
  Actual:   [ ]
```

All 9 scans must be attested before T2 is COMPLETE. Any unexpected result = stop and
report to ptt-ticket-reviewer.

---

### T2.13 BUILD_PASS Criteria

T2 is BUILD_PASS when ALL of the following are true:

1.  ☐ `dotnet build` (or F5 in NT8 NinjaScript editor) returns zero errors.
2.  ☐ `dotnet test` returns 10/10 pass for T_B16_01 through T_B16_10.
3.  ☐ Branch A or B documented in `ticket-2-completion.md` with one-line justification.
4.  ☐ SCAN-06: `grep -n "price\s*=\s*0\.0" TradeCopierPanel.cs` returns 0 results.
5.  ☐ SCAN-07: `grep -n "T_B16_" CopyEngineTests.cs` returns exactly 10 results.
6.  ☐ All T1 diagnostic code removed — 5 grep checks above return 0 results each.
7.  ☐ `docs/standards/NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries` T2 section appended.
8.  ☐ `docs/standards/NT8_COMPILER_RULES.md` updated for any new rules found; or
        `nt8-rules(B16-T2): no new rules` stated in completion report.
9.  ☐ All 9 scan results recorded in T2.12 with actual values.
10. ☐ F5 gate: Sim101 click trader places limit order at a price reflecting the Y pixel
        (Branch A), or at an approximation based on ChartPanel geometry (Branch B), or
        at last-trade price with DW-B16-01 OPEN documented (full Branch B CS1061 failure).
        In any case, `rawPrice` is NOT a literal `0.0` anywhere in the code path.
11. ☐ SCAN-08: `grep -n "PTT-Tighten-Stop" CopyEngine.cs` returns 0 results.
        (cancel+replace branch removed by DW-B16-02)
12. ☐ SCAN-09: `grep -n '"~"' TradeCopierPanel.cs` returns 0 results.
        (button label renamed to "Tighten" by DW-B16-02)

---

### T2.14 Gate Statement

**T2 is the final ticket for PTT-COPIER-B16.**

After T2 VERIFY_PASS, ptt-plan-reviewer performs Phase 5 cross-file coherence review.
No further tickets exist in B16 scope.

DW-B16-01 closure status (one of three outcomes):
- **CLOSED:** Branch A found native API — click trader places at exact clicked Y price.
- **PARTIAL-CLOSED:** Branch B linear interpolation works — click trader places near clicked Y price.
- **OPEN:** Branch B CS1061 on MaxValue/MinValue — DW-B16-01 carries forward to B17+.

DW-B16-02 closure status: **CLOSED** (cancel+replace branch removed; acc.Change() used for all stop types).

The outcome must be recorded explicitly in `ticket-2-completion.md`.

---

## Appendix: Cross-Ticket Summary

| Item | T1 State | T2 State |
|------|---------|---------|
| `using System.Reflection` | ADDED | REMOVED |
| `using System.Text` | ADDED | REMOVED |
| `_chartScaleDiagDone` field | ADDED | REMOVED |
| `WalkChartPanelChildren` method | ADDED | REMOVED |
| `BuildMethodReport` method | ADDED | REMOVED |
| `SetChart` CYC | 2 | 1 (restored) |
| `GetPriceAtY` | B15 stub (Last.Price) | Branch A or B (real Y price) |
| `LinearYToPrice` | absent | ADDED (internal static) |
| `AlignToTick` | absent | ADDED (internal static) |
| `OnChartMouseDown` | UNCHANGED | UNCHANGED |
| `TightenOneStop` CYC | UNCHANGED (4) | 3 (DW-B16-02: if/else removed) |
| `"~"` button label | UNCHANGED | `"Tighten"` (DW-B16-02) |
| Tests (T_B16_xx) | 0 | 10 [Fact] (01-08 price math, 09-10 TightenOneStop) |
| NT8_ADDON_KNOWLEDGE.md | B16 T1 section | B16 T2 appended |
| DW-B16-01 | Investigation (T1) | CLOSED / PARTIAL / OPEN |
| DW-B16-02 | N/A | CLOSED (cancel+replace branch removed) |

---

## Amendment Log

| Date | Change | Authority |
|------|--------|-----------|
| 2026-07-15 | DW-B16-02 injected: TightenOneStop + button rename added to T2 scope | Director (pre-T1 injection) |
| DW-B16-01 | INVESTIGATION | CLOSED / PARTIAL / OPEN |

---

## Return Value

**TICKETS_COMPLETE**
