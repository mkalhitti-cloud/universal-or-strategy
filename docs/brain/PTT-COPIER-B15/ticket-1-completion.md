# PTT-COPIER-B15 Ticket 1 Completion Report
# Ticket: B15-T1 -- ChartControl Visual Tree Diagnostic
# Engineer: ptt-engineer (Phase 4a)
# Date: 2026-07-14
# Wave workspace: c:\WSGTA\universal-or-strategy
# Target file: src/PropTraderTools/TradeCopierPanel.cs

---

## RULES CATALOG GATE RESULT

**GATE: PASS**

Checked docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean, 1560L).

P0 rules verified against T1 new code:

| Rule | Pattern | Result |
|------|---------|--------|
| JS-021 | `lock(` | 0 occurrences in new code |
| JS-033 | `async void` | 0 occurrences in new code |
| JS-001 | `throw new ...Exception(` in business logic | 0 occurrences |
| JS-002 | `return null;` | 0 occurrences (all methods return void or early-return) |

NT8 rules verified:
- NT8-008: `chart.ChartControl` absent — `TradeCopierAddOn.FindVisualChild<ChartControl>(chart)` used exclusively
- NT8-003: no `volatile double` — new field is `volatile bool _chartDiagDone`
- NT8-017: `volatile bool` used correctly for cross-thread one-shot guard

**RULES CATALOG GATE: PASS — no P0 violations found**

---

## Code Summary

### 1. File Header Comment Added (Step 1)

Inserted at line 1 of TradeCopierPanel.cs, before the existing B14-T1 header:

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

### 2. `_chartDiagDone` Field Added (Step 2)

Added at line 136, after `_clickBuy`:

```csharp
// B15 T1 -- one-shot diagnostic guard (JS-023: cross-thread volatile bool)
private volatile bool    _chartDiagDone = false;
```

NT8-017 compliance: `volatile bool` (allowed). NOT `volatile double` (NT8-003 ban).

### 3. Three Diagnostic Methods Added (Step 3)

All three methods inserted after `SetChart`, before `SetInstrument`.

#### DumpReflectionPath (CYC=8 actual, budget <=8)

Private sub-helper. Probes `ChartControl` via reflection for `ChartBars` property, then
walks to `ChartBars[0].ChartPanel` and checks for `GetValueByY` method. Output appended
to StringBuilder passed by caller. CYC=8 (WARN-C2-01: ternary on `gvbMethod != null`
counts as branch; reviewer noted this in ticket review — still within budget).

CYC decision points:
1. `if (barsInfo == null)` early return
2. `if (barsVal != null)` null guard
3. `if (indexer != null)` indexer guard
4. `if (item0 != null)` item guard
5. `if (panelInfo != null)` ChartPanel guard
6. `if (panelVal != null)` panelVal guard
7. `catch (Exception ex)` exception branch
8. `gvbMethod != null ? "YES" : "NO"` ternary

CYC = 8. At budget limit. PASS (budget <= 8).

#### DumpVisualTree (CYC=6, budget <=8)

Private sub-helper. Walks ChartControl visual tree 3 levels deep using
`System.Windows.Media.VisualTreeHelper.GetChildrenCount/GetChild`. Appends type names
to StringBuilder.

CYC decision points:
1. `for (int i = 0; ...)` depth-1 loop
2. `if (child == null) continue` depth-1 null guard
3. `for (int j = 0; ...)` depth-2 loop
4. `if (grand == null) continue` depth-2 null guard
5. `for (int k = 0; ...)` depth-3 loop
6. `if (great != null)` depth-3 null guard

CYC = 6. Within budget. PASS.

#### DumpChartControlTree (CYC=3 conservative, actual 2, budget <=3)

Orchestrator. One-shot guard via `_chartDiagDone || cc == null`. Calls
`DumpReflectionPath` and `DumpVisualTree`. Writes output to `_statusText.Text`
via `Dispatcher.InvokeAsync` (thread-safe UI update).

CYC decision points:
1. `if (_chartDiagDone || cc == null) return` combined one-shot + null guard
2. `if (_statusText != null)` inside Dispatcher lambda

CYC = 2 (conservative 3 per ticket spec). Within budget. PASS.

### 4. SetChart Modified (Step 4)

```csharp
// B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
// B15 T1: Call DumpChartControlTree after chart assigned (one-shot diagnostic).
public void SetChart(Chart chart)
{
    _currentChart = chart;
    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);   // NT8-008: chart.ChartControl absent
    if (cc != null)
        DumpChartControlTree(cc);
}
```

SetChart CYC after T1 = 2 (one `if (cc != null)` guard added). Within budget. PASS.
NT8-008 compliance: `chart.ChartControl` is ABSENT. Only `FindVisualChild<ChartControl>` used.

### 5. OnChartMouseDown Unchanged (Step 5)

`double price = 0.0` stub at line 1228 remains. `_ = e.GetPosition(chartControl)` suppression
remains. All DW-B8-04 comments remain. **UNCHANGED as required by T1 spec.**

---

## CYC Summary Table

| Method | CYC (actual) | Budget | Status |
|--------|-------------|--------|--------|
| `DumpReflectionPath(ChartControl cc, StringBuilder sb)` | 8 | <= 8 | PASS (at limit) |
| `DumpVisualTree(ChartControl cc, StringBuilder sb)` | 6 | <= 8 | PASS |
| `DumpChartControlTree(ChartControl cc)` | 2 (conservative 3) | <= 3 | PASS |
| `SetChart(Chart chart)` after T1 | 2 | <= 8 | PASS |

---

## 8 Scan Results

### SCAN-01: lock() check — 0 results required

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "lock\("
```

**RESULT: 0 results. PASS.**
Note: CopyEngine.cs has `// no lock (JS-021)` comments — these are comment text, NOT `lock()` code.

### SCAN-02: async void check — 0 results required

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "async void "
```

**RESULT: 0 results. PASS.**

### SCAN-03: ChartControl.GetValueByY direct call — 0 results required (NT8-009)

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "\.GetValueByY\("
```

**RESULT: 0 results. PASS.**
DumpReflectionPath uses `GetType().GetMethod("GetValueByY")` reflection probe — it does NOT
call `GetValueByY()` directly. No NT8-009 violation.

### SCAN-04: volatile bool _chartDiagDone present (NT8-017, JS-023)

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "_chartDiagDone"
```

**RESULT: Field declaration at line 136 includes `volatile bool`. PASS.**

Exact match lines (relevant):
- Line 3: header comment (declaration mentioned)
- Line 136: `private volatile bool    _chartDiagDone = false;`
- Line 391: `if (_chartDiagDone || cc == null) return;` (guard usage)
- Line 392: `_chartDiagDone = true;` (write usage)

### SCAN-05: DumpChartControlTree called from SetChart ONLY

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "DumpChartControlTree"
```

**RESULT: Exactly 1 method definition (line 389) + 1 call site (line 290 in SetChart). PASS.**

### SCAN-06: _statusText.Text update in Dispatcher.InvokeAsync

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "_statusText\.Text"
```

**RESULT: DumpChartControlTree's `_statusText.Text = diagMsg` is at line 403, inside
`Dispatcher.InvokeAsync(() => { if (_statusText != null) _statusText.Text = diagMsg; })`. PASS.**

### SCAN-07: File header comment for B15 T1 changes present

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "B15 T1 CHANGES"
```

**RESULT: 1 match at line 2. PASS.**

### SCAN-08: chart.ChartControl banned pattern absent (NT8-008 P0)

```
Select-String -Path "...TradeCopierPanel.cs" -Pattern "chart\.ChartControl"
```

**RESULT: 1 match at line 288 — in a COMMENT only (`// NT8-008: chart.ChartControl absent...`).
Zero actual code uses. PASS.**

---

## All 8 Scans Sign-Off

| Scan | Pattern | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `lock\(` | 0 code matches | PASS |
| SCAN-02 | `async void ` | 0 matches | PASS |
| SCAN-03 | `\.GetValueByY\(` | 0 matches | PASS |
| SCAN-04 | `_chartDiagDone` + `volatile bool` | field present line 136 | PASS |
| SCAN-05 | `DumpChartControlTree` call sites | 1 call in SetChart only | PASS |
| SCAN-06 | `_statusText\.Text` in Dispatcher | line 403 inside InvokeAsync | PASS |
| SCAN-07 | `B15 T1 CHANGES` header | line 2 | PASS |
| SCAN-08 | `chart\.ChartControl` actual code | 0 code matches (1 comment) | PASS |

---

## F5 Sim101 Result

**F5 PENDING — engineer must run on Sim101 and update NT8_ADDON_KNOWLEDGE.md before T2 begins.**

### Instructions for engineer to complete T1:

1. Run hard-link sync:
   ```powershell
   powershell -File c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1
   ```
   If any STALE links:
   ```powershell
   powershell -File c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1 -Fix
   ```

2. F5 compile in NinjaTrader on Sim101. Confirm green compilation.

3. Open a chart with the PTT panel visible.

4. The `_statusText` field will show the diagnostic dump output immediately on chart load.

5. Read the output — it will look like:
   ```
   ChartBars=YES type=... bars[0]=... ChartPanel=YES/NO GetValueByY=YES/NO VT[ChildType/.../...,...]
   ```
   OR if ChartBars is absent:
   ```
   ChartBars=NO; VT[...]
   ```

6. Record the FULL output verbatim below AND in `docs/standards/NT8_ADDON_KNOWLEDGE.md`
   under `## B15 Discoveries`.

### _statusText output from Sim101 (FILL IN AFTER F5):

```
[PASTE ACTUAL _statusText OUTPUT HERE AFTER F5 RUN]
```

### NT8_ADDON_KNOWLEDGE.md update status:

`## B15 Discoveries` section: **PENDING** (template ready in 04-tickets.md; engineer must paste F5 output)

**T2 is blocked until T1 VERIFY_PASS is recorded and the `## B15 Discoveries` section is complete.**

---

## Protected Files Compliance

No protected files were modified in T1:

| File | Status |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | UNTOUCHED |
| `src/PropTraderTools/TradeCopierAddOn.cs` | UNTOUCHED |
| `src/PropTraderTools/TradeCopierWindow.cs` | UNTOUCHED |
| `src/PropTraderTools/AtrSizingEngine.cs` | UNTOUCHED |
| `src/PropTraderTools/CopyEngineTests.cs` | UNTOUCHED |

---

## BUILD_PASS

All 8 scans: PASS
CYC compliance: PASS (all methods within budget)
P0 rules: PASS (no lock, no async void, no return null, no throw in hot path)
NT8 compliance: PASS (NT8-003, NT8-008, NT8-017 all satisfied)
Protected files: PASS (none modified)
OnChartMouseDown stub: PASS (double price = 0.0 preserved, DW-B8-04 comments intact)

**BUILD_PASS**

F5 PENDING — run on Sim101 to complete T1 investigation and populate NT8_ADDON_KNOWLEDGE.md ## B15 Discoveries before T2 begins.
