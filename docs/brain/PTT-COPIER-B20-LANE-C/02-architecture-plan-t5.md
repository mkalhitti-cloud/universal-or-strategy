# PTT-COPIER-B20-LANE-C — T5 Architecture Plan Addendum

**Ticket**: T5 (DW-B20-CHARTTRADER-01, P1)
**Epic**: PTT-COPIER-B20-LANE-C
**Author**: ptt-architect (Phase 1 addendum)
**Date**: 2026-07-09
**Depends On**: 02-architecture-plan.md (T1–T4, REVIEW_PASS)

---

## §1 Root Cause Analysis

### Symptom

Buy/Sell/Close buttons in the ChartTrader panel become unclickable after the
ATR overlay is injected.  Repeated F5 cycles make it progressively worse
(more buttons blocked with each reload).

### WPF Hit-Testing Model

WPF routes pointer events to the topmost visual element that passes
`UIElement.IsHitTestVisible` and occupies the hit-test region.  For a `Grid`,
children at the same logical row are stacked in Z-order (last added = topmost).
A `Border` with no explicit `Background` still intercepts hit-tests when its
`BorderThickness` or `Padding` creates a non-zero bounding box over sibling
elements.

### The Bug — Three Compounding Problems

**Problem 1 — Missing `Grid.SetRow` call**

`BuildAtrOverlayRow` (`TradeCopierAddOn.cs:267-282`) adds a `Border` to
`chartTraderRoot.Children` with **no `Grid.SetRow` call**.  The ChartTrader
`Content` panel is a `Grid` (confirmed in `DoInject` visual-tree walk).
A `Grid` child with no row assignment defaults to **row 0**, which is the row
occupied by the native Buy/Sell/Close buttons.  The `Border` therefore
completely overlaps those buttons.

```
chartTraderRoot.Children.Add(border);   // no Grid.SetRow -> row 0
```

**Problem 2 — Stale-purge does not remove the overlay `Border`**

`DoInject` purges stale children by matching the type name `TradeCopierPanel`.
The injected `Border` is a plain WPF `Border`, so the purge loop skips it.
Every F5 appends another `Border` to row 0 without removing the previous one.

**Problem 3 — Wrong ownership layer**

The overlay was placed in `TradeCopierAddOn` (AddOnBase subclass) instead of
`TradeCopierPanel` (the docked StackPanel that owns the Risk$/ATR% row).
`TradeCopierPanel` already has its own lifecycle: it is created once per
`DoInject`, removed by the purge loop, and re-created cleanly on each F5.
Any UI element owned by the panel is therefore purged and re-created atomically.

### Correct Architecture

The ATR display label belongs in `TradeCopierPanel.BuildRiskAtrRow`, appended
to the same `StackPanel root` that already contains the Risk$/ATR% spinner row.
`TradeCopierAddOn.UpdateAtrOverlay` must route the update through `_panels`
(the existing `ConcurrentDictionary<Chart, TradeCopierPanel>`) instead of
directly writing to `_atrOverlayLabel`.

---

## §2 Fix Approach — 7 Changes Across 2 Files

### File A: `src/PropTraderTools/TradeCopierAddOn.cs`

#### Change A1 — Remove `_atrOverlayLabel` field (line 60)

**Remove**:
```csharp
private TextBlock _atrOverlayLabel = null;
```

**Rationale**: The label is now owned by `TradeCopierPanel`.
`TradeCopierAddOn` has no business holding a direct reference to a UI widget
inside the panel.  Removing the field eliminates the stale-reference hazard
(the field would outlive the panel after purge).

#### Change A2 — Replace `UpdateAtrOverlay` body (lines 288-293)

**Remove current body**:
```csharp
internal void UpdateAtrOverlay(string atrDisplay)
{
    if (_atrOverlayLabel == null) return;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        _atrOverlayLabel.Text = atrDisplay);
}
```

**New body**:
```csharp
internal void UpdateAtrOverlay(string atrDisplay)
{
    var panel = _panels.Values.FirstOrDefault();
    if (panel == null) return;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => panel.SetAtrText(atrDisplay));
}
```

**Rationale**:
- `_panels` is a `ConcurrentDictionary<Chart, TradeCopierPanel>` already used
  by `DoInject` / `DoRemove`.  One AddOn instance = one active panel at a time;
  `FirstOrDefault()` is safe and requires no lock.
- `Dispatcher.InvokeAsync` preserves the existing UI-thread marshal contract.
  `OnAtrUpdated` fires on the AtrSizingEngine bar-close background thread; this
  method is the only dispatch site.
- CYC = 2: null-guard on `panel` (1) + `InvokeAsync` dispatch (2).

#### Change A3 — Remove `BuildAtrOverlayRow` method (lines 267-282)

**Remove entire method**:
```csharp
private void BuildAtrOverlayRow(Panel chartTraderRoot) { ... }
```

**Rationale**: Functionality superseded by `TradeCopierPanel.BuildRiskAtrRow`
extension (Change P3 below).  Removing it eliminates the source of the
overlay-in-row-0 defect.

#### Change A4 — Trim overlay-injection block in `StartAtrEngine` (lines 228-233)

**Remove these two lines** (the `engine.AtrUpdated += OnAtrUpdated` line stays):
```csharp
var chartTraderRoot = ResolveChartTraderPanel(chart);
if (chartTraderRoot != null)
{
    BuildAtrOverlayRow(chartTraderRoot);
    engine.AtrUpdated += OnAtrUpdated;
}
```

**Replace with** (subscription only, no overlay build):
```csharp
engine.AtrUpdated += OnAtrUpdated;
```

**Rationale**: `StartAtrEngine` no longer needs to locate `chartTraderRoot`
for overlay purposes.  The `AtrUpdated` subscription is preserved so updates
still flow to `UpdateAtrOverlay` -> `panel.SetAtrText`.

> NOTE: After this change `StartAtrEngine` CYC drops from 4 to 3 (removes
> guard 4 — the `chartTraderRoot != null` branch).

#### Change A5 — Remove `ResolveChartTraderPanel` method (lines 255-261)

After Change A4, `StartAtrEngine` no longer calls `ResolveChartTraderPanel`.
Grep confirms exactly 2 occurrences: the definition and the single call site
in `StartAtrEngine` (now removed by A4). The method has zero callers after A4.

**Remove entire method**:
```csharp
private Panel ResolveChartTraderPanel(Chart chart)
{
    if (chart == null) return null;
    var chartTrader = FindVisualChild<ChartTrader>(chart);
    if (chartTrader == null) return null;
    return chartTrader.Content as Panel;
}
```

**Rationale**: Dead code with zero callers is a scan liability and a source of
confusion (future engineers may assume it is needed). Removing it is
minimal-correct and stays within T5 scope. CYC = 2 is eliminated.

---

### File B: `src/PropTraderTools/TradeCopierPanel.cs`

#### Change P1 — Add `_atrDisplayLabel` field

Add near the existing Risk/ATR field declarations (after `_atrFractionBox`):

```csharp
private TextBlock _atrDisplayLabel;
```

**Rationale**: Instance field owned by `TradeCopierPanel`.  Created once in
`BuildRiskAtrRow`; nulled implicitly when the panel is garbage-collected after
purge.  No `= null` initializer needed (C# default).

#### Change P2 — Add `SetAtrText(string display)` public method

Add after `BuildRiskAtrRow` or in the ATR-methods region:

```csharp
public void SetAtrText(string display)
{
    if (_atrDisplayLabel == null) return;
    _atrDisplayLabel.Text = display;
}
```

**Rationale**:
- Called exclusively from `UpdateAtrOverlay` via `Dispatcher.InvokeAsync`,
  so it runs on the WPF UI thread.  No additional dispatch needed inside.
- CYC = 2: null-guard (1) + assignment (2).
- Naming follows the existing `Set*` pattern in this class.

#### Change P3 — Extend `BuildRiskAtrRow` to append ATR display row

After the existing final line `root.Children.Add(grid);` (line 1581) and
**before the closing brace** (line 1582), append:

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

**Rationale**:
- `root` is the `StackPanel _contentPanel` passed from `BuildUI`.  Items added
  to a `StackPanel` stack vertically; no `Grid.SetRow` is needed or applicable.
  This completely avoids the row-0 overlap defect.
- The `Border` is a child of `TradeCopierPanel`'s own `StackPanel`, so it is
  purged and re-created atomically with the panel on each F5.  No accumulation.
- Placeholder text `"ATR=-.-- pts -> stopTicks=-- -> qty=--"` uses ASCII-only
  characters (JS-compliant).
- No `FontFamily`, no hardcoded hex color.  `BorderBrush` and `Background` are
  intentionally unset — they inherit the panel's resource brush context, which
  is WPF-theme-safe and passes the NTBrushes audit.
- `BuildRiskAtrRow` CYC remains 1 (straight-line construction; no branches
  added).

---

## §3 CYC Table

| Method | File | Before | After | Note |
|---|---|---|---|---|
| `BuildAtrOverlayRow` | TradeCopierAddOn | 1 | **DELETED** | Method removed entirely (Change A3) |
| `ResolveChartTraderPanel` | TradeCopierAddOn | 2 | **DELETED** | Zero callers after A4 removes StartAtrEngine call site (Change A5) |
| `UpdateAtrOverlay` | TradeCopierAddOn | 2 | **2** | Guard changed: `_atrOverlayLabel` -> `_panels.FirstOrDefault()` (Change A2) |
| `OnAtrUpdated` | TradeCopierAddOn | 1 | 1 | Unchanged |
| `StartAtrEngine` | TradeCopierAddOn | 4 | **3** | Guard 4 (chartTraderRoot) removed (Change A4) |
| `SetAtrText` | TradeCopierPanel | NEW | **2** | null-guard + assignment (Change P2) |
| `BuildRiskAtrRow` | TradeCopierPanel | 1 | **1** | Straight-line extension; no branches added (Change P3) |

All modified and new methods: CYC <= 8. Constraint satisfied.

---

## §4 JS Rule Compliance

| Rule | ID | Status | Notes |
|---|---|---|---|
| No `lock()` anywhere | JS-021 | PASS | No lock introduced. `_panels` is `ConcurrentDictionary` (existing). `FirstOrDefault()` on snapshot is lock-free. |
| No `async void` (non-handler) | JS-033 | PASS | No async void. `Dispatcher.InvokeAsync` lambda is synchronous inside. |
| No `return null` on business path | JS-002 | PASS | `UpdateAtrOverlay` returns `void`; early-return on null panel is a guard, not a result. `SetAtrText` also `void`. |
| No `throw` in hot path | JS-001 | PASS | No exceptions thrown in new code. |
| Dispatcher.InvokeAsync on all UI writes | NT8 WPF | PASS | `SetAtrText` is called exclusively via `Dispatcher.InvokeAsync` from `UpdateAtrOverlay`. The label write is therefore always on the UI thread. |
| ASCII-only identifiers and string literals | JS-global | PASS | Placeholder text uses ASCII only: `"ATR=-.-- pts -> stopTicks=-- -> qty=--"`. Arrow `->` is ASCII hyphen-greater-than. |
| No FontFamily | NT8 WPF | PASS | No `FontFamily` property set on any new element. |
| No hardcoded hex colors | NT8 WPF | PASS | No `Background`, `Foreground`, or `BorderBrush` set to `#RRGGBB` values. Defaults inherited from WPF theme. |
| No `DateTime.Now` | JS-global | PASS | Not applicable to this ticket. |
| CYC <= 8 on all methods | Jane Street | PASS | See §3 table. Max CYC in any changed method = 3. |

---

## §5 No New [Fact] Rationale

T5 makes **structural** changes (removing a misplaced widget, adding the same
widget in the correct owner), not **behavioral** changes to CopyEngine logic or
order dispatch.  The CopyEngine already has `[Fact]` tests covering ATR routing
(`CopyEngineTests.cs`).

The specific bug (overlay blocking button clicks) is a **WPF visual-tree Z-order
defect** that cannot be exercised with xUnit facts — it requires a live
NinjaTrader process and WPF hit-test simulation.  An xUnit test cannot
instantiate `ChartTrader`, `Grid`, or perform WPF hit-testing without a
full WPF Application host, which is outside the NT8 AddOn test harness.

`SetAtrText` is a pure property setter with a null guard (CYC=2).  Its
correctness is self-evident from inspection; the overhead of a test fixture
that creates a `TradeCopierPanel` (which requires a `CopyEngine` singleton,
an `NTBrushes` resource dictionary, and a WPF dispatcher) exceeds the value
of the assertion.

**Acceptance criterion** (manual verification during F5 gate):
1. After T5 changes are applied, F5 in NinjaTrader should show the ATR display
   inside the `TradeCopierPanel` StackPanel.
2. Buy/Sell/Close buttons must remain fully clickable.
3. Repeated F5 cycles must not accumulate duplicate ATR rows.

---

## §6 Decision Log

| # | Decision | Rationale | Alternative Rejected |
|---|---|---|---|
| D1 | Delete `BuildAtrOverlayRow` entirely rather than fix `Grid.SetRow` | A Grid-row fix would resolve accumulation but not the lifecycle issue (purge still misses it). Full deletion + panel ownership is the minimal correct fix. | Keeping `BuildAtrOverlayRow` with `Grid.SetRow(border, N)` where N = last row — rejected because the stale-purge gap remains. |
| D2 | Route `UpdateAtrOverlay` through `_panels.Values.FirstOrDefault()` | `_panels` is already the canonical registry of active panels. No new state needed. | Adding a `_currentPanel` field to `TradeCopierAddOn` — rejected (new field = new ownership, new nullability surface, no benefit over existing `_panels`). |
| D3 | Extend `BuildRiskAtrRow` rather than add a new `BuildAtrDisplayRow` method | `BuildRiskAtrRow` already builds the ATR-fraction spinner; the display label is logically part of the same ATR context row group. One method, one region. | Adding a new `BuildAtrDisplayRow(StackPanel root)` called from `BuildUI` — rejected (more entry points to audit, no encapsulation benefit). |
| D4 | Use `Dispatcher.InvokeAsync` in `UpdateAtrOverlay`, NOT inside `SetAtrText` | The dispatch is at the `AddOn` layer (single dispatch site, clear ownership). `SetAtrText` stays synchronous and UI-thread-only — easier to reason about, CYC stays at 2. | Dispatching inside `SetAtrText` — rejected (double indirection, hides the dispatch site from callers). |
| D5 | Remove `ResolveChartTraderPanel` entirely (Change A5) | After Change A4 removes the only call site in `StartAtrEngine`, grep confirms exactly 2 occurrences of the name: the definition and that call site. Zero callers remain. Dead code with zero callers is a scan liability. Removal is minimal-correct within T5 scope. | Keeping the method as a utility stub — rejected; preserving dead code under a false "future callers" assumption violates the minimal-change principle and misleads future engineers. |
| D6 | No `BorderBrush` / `Background` on the new `Border` | Inheriting from WPF theme avoids NT8 compiler warnings about hardcoded color values and satisfies the no-hex-color rule. The `Border` border is still visible via `BorderThickness=1` using the default `BorderBrush` of the surrounding panel. | Setting explicit `NTBrushes` resource references — deferred; acceptable but not required for P1 correctness fix. |
