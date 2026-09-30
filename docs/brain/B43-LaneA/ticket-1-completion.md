# B43-LaneA — Ticket T1 Completion Report
**Block:** PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
**Ticket:** T1 — TradeCopierPanel.cs: Replace ATM mode cluster with template ComboBox
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Engineer:** ptt-engineer
**Date:** 2026-08-05
**Plan Review:** TICKET_REVIEW_PASS (confirmed in 04-ticket-review.md)

---

## Status: BUILD_PASS

---

## What Was Implemented

### T1.1 — BuildCheckItemTemplate() REMOVALS

Removed from FrameworkElementFactory wiring:

- `atmFactory` (FrameworkElementFactory for ComboBox) — col 3, items Inherit/Market/Named,
  wired to `OnFollowerAtmComboLoaded` (LoadedEvent) and `OnFollowerAtmModeChanged_WithNamedBox`
  (SelectionChangedEvent). `gridFactory.AppendChild(atmFactory)` removed.
- `namedBoxFactory` (FrameworkElementFactory for TextBox) — col 4, Visibility.Collapsed,
  ToolTip "ATM template name". `gridFactory.AppendChild(namedBoxFactory)` removed.
- `chkFactory` column updated from **5 → 4** (namedBox col removed; checkmark shifts left).

### T1.2 — BuildCheckItemTemplate() ADDITIONS

Added `atmTemplateFactory` in place of the removed elements at col 3:

```csharp
var atmTemplateFactory = new FrameworkElementFactory(typeof(ComboBox));
atmTemplateFactory.SetValue(Grid.ColumnProperty,      3);
atmTemplateFactory.SetValue(ComboBox.WidthProperty,   120.0);
atmTemplateFactory.SetValue(ComboBox.MarginProperty,  new Thickness(2));
atmTemplateFactory.SetValue(ComboBox.ToolTipProperty, "ATM template for this follower");
atmTemplateFactory.AddHandler(FrameworkElement.LoadedEvent,
    new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded));
atmTemplateFactory.AddHandler(Selector.SelectionChangedEvent,
    new SelectionChangedEventHandler(OnFollowerAtmTemplateComboChanged));
```

AppendChild order updated to 5 children: nameFactory, pnlFactory, multFactory,
atmTemplateFactory, chkFactory.

### T1.3 — OnRowGridLoaded() MODIFIED

Column count changed from **6 → 5**. New column layout:
- Col 0: Star, MinWidth 80 — account name
- Col 1: 62px fixed — daily P&L
- Col 2: 30px fixed — multiplier TextBox
- Col 3: **120px fixed** — ATM template ComboBox (was 80px; wider for template names)
- Col 4: 20px fixed — checkbox

Idempotency guard `if (grid.Tag is bool) return;` preserved unchanged.

### T1.4 — OnFollowerAtmTemplateComboLoaded (NEW)

`private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)` — CYC=4

Populates ATM template ComboBox on Loaded event. Adds "(none)" sentinel, enumerates
`NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates` in try/catch, sets default to
leader's current ChartTrader ATM template (via `GetLeaderAtmTemplateName(_currentChart)`).
Idempotency guard prevents double-population on re-layout.

### T1.5 — OnFollowerAtmTemplateComboChanged (NEW)

`private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)` — CYC=3

Writes `item.AtmModeName` as `"Inherit"` (when "(none)" or empty) or `"Named:templateName"`.
Serialization format unchanged — `CopyEngine.ParseAtmModeName` parses both unchanged.
Uses `FindAncestorDataContext<FollowerItem>(cb)` fallback if `cb.DataContext` is null.

### T1.6 — GetLeaderAtmTemplateName (NEW, internal static)

`internal static string GetLeaderAtmTemplateName(Chart currentChart)` — CYC=4

Returns the ATM template name currently selected in ChartTrader for the given chart.
`internal static` for testability (T_B43_04 calls with null; no WPF instantiation required).
Uses `TradeCopierAddOn.FindVisualChild<ChartTrader>(currentChart)` and
`TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2)`. Returns `string.Empty` on all
null/exception paths. Never throws. NT8-008 and NT8-041 compliant.

### T1.7 — FindAncestorDataContext<T> (NEW, private static)

`private static T FindAncestorDataContext<T>(DependencyObject child) where T : class` — CYC=3

Walks the WPF visual tree upward from `child`, returning the DataContext of the first ancestor
whose DataContext is assignable to type T. Returns `default(T)` (not `return null`) at all exits.
Called only from UI-thread handlers (VisualTreeHelper requirement).

### T1.8 — Handlers REMOVED (3 methods deleted entirely)

| Method | Was Located At | Reason |
|--------|---------------|--------|
| `OnFollowerAtmComboLoaded` | ~L1600 | Wired to removed atmFactory; dead after B43 |
| `OnFollowerAtmModeChanged` | ~L1611 | B8 variant; dead after B43 |
| `OnFollowerAtmModeChanged_WithNamedBox` | ~L1625 | B9 variant; dead after B43 |

### T1.9 — OnApplyRule (NO CHANGE)

Confirmed at L1804: `atmNames[i] = item.AtmModeName ?? "Inherit"` — reads `item.AtmModeName`
and passes to `ParseAtmModeNameLocal(atmNames[i])`. Format "Inherit" / "Named:templateName"
is unchanged. Zero diff on this method.

---

## 7-Scan Results (all 7 scans zero — PASS)

| Scan | Pattern | Result | Command Used |
|------|---------|--------|-------------|
| SCAN-01 | `lock\s*\(` | **0 code hits** (1 comment-only at L1019) | `Select-String -Pattern "lock\s*\("` |
| SCAN-02 | `async void` | **0 code hits** (1 comment-only at L1019) | `Select-String -Pattern "async void"` |
| SCAN-03 | `return null` in new B43 code | **0 hits** in new methods (all exits use `return string.Empty`, `return default(T)`, or guard-returns) | manual code review L1599-L1687 |
| SCAN-04 | CYC audit (4 new methods) | **OnFollowerAtmTemplateComboLoaded=4, OnFollowerAtmTemplateComboChanged=3, GetLeaderAtmTemplateName=4, FindAncestorDataContext=3** — all ≤8 | manual branch count |
| SCAN-05 | `{ get; init; }` | **0 hits** | `Select-String -Pattern "\{\s*get;\s*init;\s*\}"` |
| SCAN-06 | `volatile double` | **0 hits** | `Select-String -Pattern "volatile double"` |
| SCAN-07 | `async\s+void\s+\w` (belt-and-suspenders) | **0 hits** | `Select-String -Pattern "async\s+void\s+\w"` |

### Additional Acceptance Checks

| Check | Result |
|-------|--------|
| `namedBoxFactory` grep | **0 hits** (removed) |
| `private void OnFollowerAtmComboLoaded` | **0 hits** (removed) |
| `private void OnFollowerAtmModeChanged` | **0 hits** (removed) |
| `private void OnFollowerAtmModeChanged_WithNamedBox` | **0 hits** (removed) |
| `ColumnDefinitions.Add` count in `OnRowGridLoaded` | **5 calls** (was 6) |
| Column widths | Star/80min, 62, 30, **120**, 20 (was 80, 80) |

---

## NT8 Surprises

None. `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates` was used inside a try/catch
per plan §6.1 (F5 VERIFY required). No fallback to filesystem was needed for build;
the try/catch gracefully handles unavailability at compile time. NT8-008 and NT8-041 noted
and respected: `Chart.ChartControl` not used; visual tree walk via `FindVisualChild<ChartTrader>`
used instead.

The `is not Grid` pattern in `OnRowGridLoaded` (L1571) was pre-existing from B10 — no change
needed, already compiling in NT8.

---

## Files Modified

- `src/PropTraderTools/TradeCopierPanel.cs` — net +35 lines (removed ~55, added ~90)

## Files NOT Modified (zero diff required per plan)

- `CopyEngine.cs`
- `PttContracts.cs`
- `PttBus.cs`
- `TradeCopierWindow.cs`
- `TradeCopierAddOn.cs`
- All other `src/PropTraderTools/*.cs` files

---

## Result: BUILD_PASS
