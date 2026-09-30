# PTT-COPIER-B20-LANE-C — 04-tickets-t5.md

**Ticket**: T5 (DW-B20-CHARTTRADER-01)
**Epic**: PTT-COPIER-B20-LANE-C
**Author**: ptt-architect (Phase 3)
**Date**: 2026-07-09
**Plan Source**: `docs/brain/PTT-COPIER-B20-LANE-C/02-architecture-plan-t5.md` (REVIEW_PASS)

---

## Spec Requirements Satisfied

| Req ID | Description |
|---|---|
| DW-B20-CHARTTRADER-01 | Fix ChartTrader Buy/Sell/Close buttons becoming unclickable after ATR overlay injection |
| DW-B20-CHARTTRADER-01.1 | Remove `_atrOverlayLabel` from `TradeCopierAddOn` (wrong ownership layer) |
| DW-B20-CHARTTRADER-01.2 | Remove `BuildAtrOverlayRow` (root cause of Grid row-0 overlap defect) |
| DW-B20-CHARTTRADER-01.3 | Remove `ResolveChartTraderPanel` (dead code after A4) |
| DW-B20-CHARTTRADER-01.4 | Move ATR display label ownership into `TradeCopierPanel.BuildRiskAtrRow` |
| DW-B20-CHARTTRADER-01.5 | Route `UpdateAtrOverlay` through `_panels` registry, not stale direct field reference |

---

## T5 — Fix ChartTrader Button Blockage (ATR Overlay Ownership Correction)

### Workspace Files

| File | Role |
|---|---|
| `src/PropTraderTools/TradeCopierAddOn.cs` | 5 changes: remove field, remove 2 methods, trim overlay block, fix UpdateAtrOverlay |
| `src/PropTraderTools/TradeCopierPanel.cs` | 3 changes: add field, add method, extend BuildRiskAtrRow |

---

### Root Cause Summary

Three compounding WPF defects in the existing `BuildAtrOverlayRow` method:

1. **Grid row-0 overlap**: `Border` added to `chartTraderRoot.Children` with no `Grid.SetRow` call.
   The ChartTrader content panel is a `Grid`; children default to row 0, overlapping native Buy/Sell/Close buttons.

2. **Stale-purge miss**: `DoInject` purges children by matching type name `TradeCopierPanel`.
   The plain `Border` injected by `BuildAtrOverlayRow` is not type-matched, so it accumulates on every F5.

3. **Wrong ownership layer**: UI widgets must be owned by `TradeCopierPanel`, which has atomic
   create/purge lifecycle via `DoInject`/`DoRemove`. `TradeCopierAddOn` has no such lifecycle.

---

### Method Signatures — All T5 Methods

#### TradeCopierAddOn.cs

```csharp
// MODIFIED — CYC: 4 -> 3. Removes chartTraderRoot guard block (Change A4).
private void StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)

// MODIFIED — CYC: 2 (unchanged). Body replaced to route via _panels (Change A2).
internal void UpdateAtrOverlay(string atrDisplay)

// DELETED — was CYC: 1. Method removed entirely (Change A3).
// private void BuildAtrOverlayRow(Panel chartTraderRoot)

// DELETED — was CYC: 2. Zero callers after A4 (Change A5).
// private Panel ResolveChartTraderPanel(Chart chart)
```

#### TradeCopierPanel.cs

```csharp
// NEW — CYC: 2 (null guard + assignment). Called via Dispatcher.InvokeAsync from
// TradeCopierAddOn.UpdateAtrOverlay. Runs on UI thread only (Change P2).
public void SetAtrText(string display)

// MODIFIED — CYC: 1 (unchanged). Straight-line extension; no branches added (Change P3).
private void BuildRiskAtrRow(StackPanel root)
```

---

### Implementation Specification

#### TradeCopierAddOn.cs — 5 Changes

---

**Change A1 — Remove `_atrOverlayLabel` field (line ~60)**

JS rules: JS-021 (no new state that outlives panel lifecycle)

REMOVE this field declaration:
```csharp
private TextBlock _atrOverlayLabel = null;
```

Rationale: The label is now owned by `TradeCopierPanel`. Removing the field eliminates
the stale-reference hazard (the field would outlive the panel after purge).

---

**Change A2 — Replace `UpdateAtrOverlay` body (lines ~288-293)**

JS rules: JS-021 (no lock — `_panels` is `ConcurrentDictionary`), JS-033 (no async void)

BEFORE (remove entire body):
```csharp
internal void UpdateAtrOverlay(string atrDisplay)
{
    if (_atrOverlayLabel == null) return;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        _atrOverlayLabel.Text = atrDisplay);
}
```

AFTER (new body — method signature unchanged):
```csharp
internal void UpdateAtrOverlay(string atrDisplay)
{
    var panel = _panels.Values.FirstOrDefault();
    if (panel == null) return;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => panel.SetAtrText(atrDisplay));
}
```

NOTE: `_panels.Values.FirstOrDefault()` requires `System.Linq`.
Check whether `using System.Linq;` is already present in `TradeCopierAddOn.cs`.
If not, add it at the top of the file with the existing `using` directives.

CYC = 2: null-guard on `panel` (1) + `InvokeAsync` dispatch (2).

---

**Change A3 — Remove `BuildAtrOverlayRow` method entirely (lines ~267-282)**

JS rules: no JS rules violated by removal; eliminating dead/defective code

REMOVE the entire method (definition, braces, and body):
```csharp
private void BuildAtrOverlayRow(Panel chartTraderRoot) { ... }
```

Only caller is the overlay-injection block in `StartAtrEngine`, which is removed by Change A4.
After A4, zero callers remain.

---

**Change A4 — Trim overlay-injection block in `StartAtrEngine` (lines ~228-233)**

JS rules: JS-021 (no new lock patterns), CYC reduction

REMOVE the entire `chartTraderRoot` guard block (including the comment):
```csharp
// WPF OVERLAY: inject ATR display into ChartTrader panel (guard 4)
var chartTraderRoot = ResolveChartTraderPanel(chart);
if (chartTraderRoot != null)
{
    BuildAtrOverlayRow(chartTraderRoot);
    engine.AtrUpdated += OnAtrUpdated;
}
```

REPLACE WITH (subscription only, unconditional):
```csharp
engine.AtrUpdated += OnAtrUpdated;
```

IMPORTANT: The `AtrUpdated` subscription line MUST be preserved — it is the event
hook that drives `UpdateAtrOverlay` -> `panel.SetAtrText`. Only the overlay build
block and its comment are removed.

After this change: `StartAtrEngine` CYC drops from 4 to 3 (guard 4 removed).

---

**Change A5 — Remove `ResolveChartTraderPanel` method (lines ~255-261)**

JS rules: no JS rules violated by removal; eliminating dead code

After Change A4 removes the only call site, `ResolveChartTraderPanel` has zero callers.
Grep confirms exactly 2 occurrences of the name in the file: the definition and the
call site now removed by A4.

REMOVE the entire method:
```csharp
private Panel ResolveChartTraderPanel(Chart chart)
{
    if (chart == null) return null;
    var chartTrader = FindVisualChild<ChartTrader>(chart);
    if (chartTrader == null) return null;
    return chartTrader.Content as Panel;
}
```

---

#### TradeCopierPanel.cs — 3 Changes

---

**Change P1 — Add `_atrDisplayLabel` field (after line ~187)**

JS rules: JS-021 (panel-owned field; no cross-thread access without Dispatcher)

Insert immediately after the existing `_atrFractionBox` field declaration:
```csharp
// B20-LANE-C T5 -- ATR display label (owned by Panel; set in BuildRiskAtrRow)
private TextBlock _atrDisplayLabel;
```

No `= null` initializer needed — C# default is null for reference types.

---

**Change P2 — Add `SetAtrText(string display)` public method (after BuildRiskAtrRow)**

JS rules: JS-021 (no lock — runs on UI thread via Dispatcher.InvokeAsync from caller),
JS-033 (no async void — method is synchronous), JS-002 (void return; early-return is guard exit)

Insert after the closing brace of `BuildRiskAtrRow` (line ~1563), before `OnRiskUp`:
```csharp
// B20-LANE-C T5 -- SetAtrText: updates ATR display label. Called via Dispatcher.InvokeAsync from
// TradeCopierAddOn.UpdateAtrOverlay. CYC=2: null guard (1) + assignment (2).
// Called on UI thread only (via Dispatcher.InvokeAsync in caller).
public void SetAtrText(string display)
{
    if (_atrDisplayLabel == null) return;
    _atrDisplayLabel.Text = display;
}
```

CYC = 2: null-guard on `_atrDisplayLabel` (1) + assignment (2).

---

**Change P3 — Extend `BuildRiskAtrRow` to append ATR display row (line ~1562)**

JS rules: JS-021 (no lock), ASCII-only string literal, no FontFamily, no hardcoded hex color

In `BuildRiskAtrRow`, AFTER the existing final line:
```csharp
root.Children.Add(grid);
```

And BEFORE the closing brace of the method, INSERT:
```csharp
var atrRow = new Border
{
    BorderThickness = new Thickness(1),
    CornerRadius    = new CornerRadius(2),
    Padding         = new Thickness(4, 2, 4, 2),
    Margin          = new Thickness(2)
};
_atrDisplayLabel = new TextBlock { Text = "ATR=-.-- pts -> stopTicks=-- -> qty=--" };
atrRow.Child = _atrDisplayLabel;
root.Children.Add(atrRow);
```

IMPORTANT constraints:
- `root` is a `StackPanel _contentPanel` passed from `BuildUI`. StackPanel stacks vertically;
  no `Grid.SetRow` is needed or applicable. This is the correct fix for the row-0 overlap defect.
- The `Border` is owned by `TradeCopierPanel`'s own StackPanel — purged and re-created
  atomically on each F5. No accumulation defect possible.
- Placeholder text `"ATR=-.-- pts -> stopTicks=-- -> qty=--"` is ASCII-only (compliant).
- No `FontFamily`, no `Background`, no `Foreground`, no `BorderBrush` set to hex values.
  `BorderBrush` and `Background` are intentionally unset — inherited from WPF theme.
- `BuildRiskAtrRow` CYC remains 1 (straight-line construction; no branches added).

---

### CYC Table

| Method | File | Before | After | Constraint |
|---|---|---|---|---|
| `BuildAtrOverlayRow` | TradeCopierAddOn | 1 | **DELETED** | Removed by Change A3 |
| `ResolveChartTraderPanel` | TradeCopierAddOn | 2 | **DELETED** | Removed by Change A5; zero callers after A4 |
| `UpdateAtrOverlay` | TradeCopierAddOn | 2 | **2** | Guard changed: field -> panel. CYC unchanged. |
| `OnAtrUpdated` | TradeCopierAddOn | 1 | 1 | Unchanged |
| `StartAtrEngine` | TradeCopierAddOn | 4 | **3** | Guard 4 removed by Change A4 |
| `SetAtrText` | TradeCopierPanel | NEW | **2** | null-guard + assignment (Change P2) |
| `BuildRiskAtrRow` | TradeCopierPanel | 1 | **1** | Straight-line extension; no branches (Change P3) |

All modified and new methods: CYC <= 8. Jane Street constraint satisfied.

---

### JS Rule Constraints per Method

| Method | Applicable Rules | Status |
|---|---|---|
| `UpdateAtrOverlay` | JS-021 (no lock), JS-033 (no async void), JS-002 (void guard return OK) | MUST PASS |
| `SetAtrText` | JS-021 (no lock — UI thread via Dispatcher.InvokeAsync), JS-033 (no async void) | MUST PASS |
| `BuildRiskAtrRow` (extended) | ASCII-only string, no FontFamily, no hex color, JS-021 | MUST PASS |
| All removed methods | N/A — deletion eliminates violations | N/A |

No `[Fact]` tests required for T5.
See §5 of `02-architecture-plan-t5.md` for rationale (WPF Z-order defect; not exercisable via xUnit
without a full WPF Application host). `[Fact]` count stays at 120.

---

### Acceptance Criteria (Manual F5 Gate)

After applying all 8 changes:

1. F5 in NinjaTrader shows the ATR display label inside `TradeCopierPanel` StackPanel (below the ATR% spinner row).
2. ChartTrader Buy/Sell/Close buttons are fully clickable after ATR engine starts.
3. Repeated F5 cycles do not accumulate duplicate ATR rows in the panel.
4. SCAN-05 build produces same 3 pre-existing NT8-assembly errors and 0 new errors from T5.

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Run each scan with `ctx_shell` SEQUENTIALLY (one at a time, wait for result).
If `ctx_shell` returns MCP error: use `execute_command` as fallback for that ONE scan only.

---

**SCAN-01 — lock() check**

```
ctx_shell: grep -rn "lock(" c:/WSGTA/universal-or-strategy/src/PropTraderTools/
```

Expected result: 0 actual `lock()` statements in any `.cs` file.
Comments containing `lock(` do NOT count. Scan passes if no code-level `lock(` is present.
JS-021 (P0 CRITICAL) enforced.

---

**SCAN-02 — async void check**

```
ctx_shell: grep -rn "async void " c:/WSGTA/universal-or-strategy/src/PropTraderTools/ --include="*.cs"
```

Expected result: 0 results.
No new `async void` non-handler methods introduced by T5.
JS-033 (P0 CRITICAL) enforced.

---

**SCAN-03 — return null check**

```
ctx_shell: grep -rn "return null;" c:/WSGTA/universal-or-strategy/src/PropTraderTools/ --include="*.cs"
```

Expected result: 0 new `return null;` in T5-changed methods.
`UpdateAtrOverlay` returns `void` (early-return guard, not `return null`).
`SetAtrText` returns `void` (early-return guard, not `return null`).
`ResolveChartTraderPanel` (the only method that previously returned null) is DELETED by Change A5.
Net change: existing `return null;` count decreases by the number removed with `ResolveChartTraderPanel`.
Verify that the total count is less than or equal to the pre-T5 baseline.

---

**SCAN-04 — volatile check**

```
ctx_shell: grep -rn "volatile" c:/WSGTA/universal-or-strategy/src/PropTraderTools/ --include="*.cs"
```

Expected result: No new `volatile` fields introduced by T5.
`_atrDisplayLabel` is `private TextBlock` (no volatile). NT8-003 compliance maintained.

---

**SCAN-05 — build**

```
ctx_shell: dotnet build c:/WSGTA/universal-or-strategy/src/PropTraderTools/PropTraderTools.csproj
```

Expected result: Same 3 pre-existing NT8-assembly reference errors; 0 new errors from T5 changes.
If any NEW error appears referencing `_atrOverlayLabel`, `BuildAtrOverlayRow`, or
`ResolveChartTraderPanel` — the removal was incomplete. Fix before proceeding.
If `FirstOrDefault()` fails to compile: confirm `using System.Linq;` was added per Change A2 NOTE.

---

**SCAN-06 — tests**

```
ctx_shell: dotnet test c:/WSGTA/universal-or-strategy/src/PropTraderTools/PropTraderTools.csproj
```

Expected result: 120 `[Fact]` tests pass, unchanged.
T5 adds no new `[Fact]` tests (structural-only changes; see §5 of architecture plan).
If test count drops below 120, a pre-existing test was inadvertently broken — stop and investigate.

---

**SCAN-07 — CYC manual check of T5-modified methods**

Manual review only (no automated tool required for this scan).

Verify each method against the CYC table above:

| Method | Expected CYC | How to Verify |
|---|---|---|
| `UpdateAtrOverlay` (TradeCopierAddOn) | 2 | 1 null-guard `if (panel == null)` + 1 `InvokeAsync` call |
| `StartAtrEngine` (TradeCopierAddOn) | 3 | Was 4; guard 4 (`if (chartTraderRoot != null)`) removed by A4 |
| `SetAtrText` (TradeCopierPanel) | 2 | 1 null-guard `if (_atrDisplayLabel == null)` + 1 assignment |
| `BuildRiskAtrRow` (TradeCopierPanel) | 1 | Straight-line construction; no if/while/for/switch added |

Expected: 0 methods with CYC > 8. Jane Street strict standard satisfied.

---

## TICKETS_COMPLETE
