# PTT-COPIER-B10-UI-01 — Ticket File

**Status**: TICKETS_COMPLETE
**Epic**: PTT-COPIER-B10-UI-01
**Architecture Plan**: 02-architecture-plan.md (REVIEW_PASS)
**Date**: 2026-07-07
**Tickets in this file**: 1 (T1 only)

---

## T1 — DW-B10-UI-01: Follower dropdown Grid column alignment

### Summary

Replace the horizontal `StackPanel` row factory inside
[`BuildCheckItemTemplate()`](../../../../WSGTA/universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs)
with a `Grid` row factory. The fix makes all six columns (name, P&L, multiplier,
ATM, named-TB, checkbox) align vertically across every follower row regardless of
account name length.

---

### File

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

No other file changes. Zero cross-contamination.

---

### Spec Requirement References (002-trade-copier-spec.html)

| Ref | Line | Requirement |
|-----|------|-------------|
| B7-UI-PANEL-FOLLOWER | 1302 | Follower ComboBox rows: `[account name][daily P&L][checkmark]` — horizontal layout via `StackPanel ItemTemplate` (B7 baseline this ticket upgrades). |
| B7-UI-PANEL-FOLLOWER-DETAIL | 1303 | `FollowerItem` INPC bindings, live P&L color, `GetSelectedFollowers()` pattern — all preserved unchanged. |
| B7-EVOLUTION-TABLE | 1544–1562 | B7 row layout spec: "horizontal StackPanel — left to right: account name / daily P&L / checkmark." Grid alignment upgrade is an additive layout fix; all bindings and INPC wiring from B7 are retained. |
| B8-PANEL-ROW | 1975 | B8 added per-follower mult TextBox + ATM ComboBox to each row. Those bindings are preserved in this ticket — only the container type changes. |

---

### Problem Statement

`BuildCheckItemTemplate()` currently constructs each follower row using a horizontal
`StackPanel`:

```
[Account name (variable width)] [P&L] [Mult TB] [ATM CB] [Named TB] [CheckBox]
```

Because account names vary in length (e.g., `Sim101` vs `Apex-Evaluation-5021-B`),
all columns to the right of the name shift horizontally per row. P&L, multiplier,
ATM, and checkbox columns never vertically align across rows — the dropdown checklist
is unreadable at a glance.

**Fix**: Replace the StackPanel row factory with a Grid row factory that enforces
fixed-width columns for every non-name cell, and a star-width column for the name
cell with ellipsis trimming. `ColumnDefinitions` must be added at runtime
(post-materialization) via a `Loaded` event handler because WPF
`FrameworkElementFactory` cannot add `ColumnDefinitions` at template-definition time.

---

### Method Signatures

```csharp
/// <summary>
/// Builds the DataTemplate for each follower row in the dropdown checklist.
/// Uses a Grid row factory with 6 fixed/star columns so all column cells
/// align vertically across rows regardless of account name length.
/// </summary>
private DataTemplate BuildCheckItemTemplate()

/// <summary>
/// Loaded event handler for Grid rows materialized from BuildCheckItemTemplate.
/// Adds 6 ColumnDefinitions with exact widths from the column spec.
/// Guard: Tag=true prevents re-entry on re-layout.
/// CYC=2: null/type guard + already-configured guard.
/// </summary>
private void OnRowGridLoaded(object sender, RoutedEventArgs e)
```

---

### Column Definitions

| Col | Content       | Width                      | Notes                          |
|-----|---------------|----------------------------|--------------------------------|
| 0   | Account name  | `*` (Star), MinWidth=80    | `TextTrimming=CharacterEllipsis` |
| 1   | Daily P&L     | 62 px (fixed)              | `TextAlignment=Right`; existing binding preserved |
| 2   | Multiplier TB | 30 px (fixed)              | Existing TwoWay binding preserved |
| 3   | ATM ComboBox  | 80 px (fixed)              | Existing TwoWay binding preserved |
| 4   | Named TB      | 80 px (fixed)              | `Visibility=Collapsed`; unchanged |
| 5   | CheckBox      | 20 px (fixed)              | `HorizontalAlignment=Center`; existing IsChecked binding preserved |

---

### Implementation Steps

1. **Open** `src/PropTraderTools/TradeCopierPanel.cs`.

2. **Locate** `BuildCheckItemTemplate()`. Note the existing root factory is
   `FrameworkElementFactory(typeof(StackPanel))`.

3. **Replace** the root factory with `FrameworkElementFactory(typeof(Grid))`:
   ```csharp
   var gridFactory = new FrameworkElementFactory(typeof(Grid));
   ```

4. **Register** `OnRowGridLoaded` on the factory's `LoadedEvent`:
   ```csharp
   gridFactory.AddHandler(FrameworkElement.LoadedEvent,
       new RoutedEventHandler(OnRowGridLoaded));
   ```

5. **Update each child factory**: For every existing child
   (`TextBlock`/`TextBox`/`ComboBox`/`CheckBox`), replace the `HorizontalAlignment`
   or `Margin` StackPanel positioning with `Grid.ColumnProperty` as an attached
   property value. Assign columns 0–5 in document order (name=0, P&L=1, mult=2,
   ATM=3, named=4, checkbox=5):
   ```csharp
   nameFactory.SetValue(Grid.ColumnProperty, 0);
   pnlFactory.SetValue(Grid.ColumnProperty, 1);
   multFactory.SetValue(Grid.ColumnProperty, 2);
   atmFactory.SetValue(Grid.ColumnProperty, 3);
   namedFactory.SetValue(Grid.ColumnProperty, 4);
   checkFactory.SetValue(Grid.ColumnProperty, 5);
   ```

6. **Preserve all existing `SetBinding` and `SetValue` calls** on every child
   factory verbatim. Bindings do not change — only the container type changes.

7. **Append all children** to `gridFactory` (same order as current StackPanel):
   ```csharp
   gridFactory.AppendChild(nameFactory);
   gridFactory.AppendChild(pnlFactory);
   gridFactory.AppendChild(multFactory);
   gridFactory.AppendChild(atmFactory);
   gridFactory.AppendChild(namedFactory);
   gridFactory.AppendChild(checkFactory);
   ```

8. **Return** the template:
   ```csharp
   return new DataTemplate { VisualTree = gridFactory };
   ```

9. **Add** `OnRowGridLoaded` as a new private method immediately after
   `BuildCheckItemTemplate()`:
   ```csharp
   private void OnRowGridLoaded(object sender, RoutedEventArgs e)
   {
       if (sender is not Grid grid) return;   // Branch 1: type guard
       if (grid.Tag is bool) return;          // Branch 2: already-configured guard
       grid.Tag = true;

       grid.ColumnDefinitions.Add(new ColumnDefinition
           { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
       grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
       grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
       grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
       grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
       grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
   }
   ```

10. **Verify no new `using` directives are needed** — `Grid`, `ColumnDefinition`,
    `GridLength`, `FrameworkElementFactory`, `RoutedEventHandler`, and
    `FrameworkElement` are already imported in `TradeCopierPanel.cs`.

---

### 7-Scan Checklist (Inline)

| Scan | Rule | Requirement | Verification |
|------|------|-------------|--------------|
| SCAN-01 | **JS-021** — No `lock()` | Zero `lock(` in new or modified code. `Tag` guard is UI-thread-affined DependencyProperty. | `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` → 0 new hits |
| SCAN-02 | **JS-033** — No `async void` (non-EventHandler) | `OnRowGridLoaded` is synchronous `void`, NOT `async void`. No async operation is present. | `grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs` → 0 new hits |
| SCAN-03 | **JS-001** — No `throw` in business logic | Null/type guard in `OnRowGridLoaded` uses `return`, not `throw`. No exception thrown. | `grep -n "throw new " src/PropTraderTools/TradeCopierPanel.cs` → 0 new hits |
| SCAN-04 | **JS-002** — No `return null` | `BuildCheckItemTemplate()` returns `new DataTemplate { ... }` — never null. `OnRowGridLoaded` is `void`. | `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` → 0 new hits |
| SCAN-05 | **JS-036/037** — No `byte[]`/`T[]` heap alloc in hot path | No byte buffers. `ColumnDefinition` objects are WPF layout objects, not hot-path allocations. | Visual inspection — no `new byte[...]` or `new T[N]` present |
| SCAN-06 | **ASCII-only** | All new identifiers (`gridFactory`, `OnRowGridLoaded`, `grid`, `ColumnDefinitions`) are ASCII. No `FontFamily`. No hex color literals. | Visual inspection of changed lines |
| SCAN-07 | **CYC ≤ 8** | `BuildCheckItemTemplate()` CYC=1 (no branches). `OnRowGridLoaded()` CYC=2 (two `if` guards). Both well within limit. | `python scripts/complexity_audit.py` — confirm both methods ≤ 8 |

---

### Tests

**None required.** This is a pure view-layer layout change. No business logic,
no engine behavior, no data model mutations. The `tests/CopyEngineTests.cs` file
is **unchanged**.

---

### Build Gate

```powershell
dotnet build Linting.csproj /p:Configuration=Release /clp:ErrorsOnly
```

**Expected**: 0 errors, 0 warnings for modified file.

---

### Acceptance Criteria

- [ ] All six columns in the follower dropdown checklist align vertically across
      every row regardless of account name length.
- [ ] Account names longer than the column width are trimmed with ellipsis (no
      overflow, no horizontal scrollbar).
- [ ] Column widths match spec: name=*(min 80), P&L=62, Mult=30, ATM=80,
      Named=80, Checkbox=20.
- [ ] All existing bindings (AccountName, DailyPnlText, DailyPnlColor, Multiplier,
      AtmName, IsChecked) remain functional — no regression.
- [ ] `FollowerItem.INotifyPropertyChanged` live P&L updates still render correctly
      (green/red/dim).
- [ ] Header `"N selected"` count remains accurate after check/uncheck.
- [ ] `OnRowGridLoaded` does not re-apply `ColumnDefinitions` on re-layout
      (Tag guard fires on second pass).
- [ ] `GetSelectedFollowers()` behavior is unchanged.
- [ ] Build gate passes: `dotnet build Linting.csproj /p:Configuration=Release /clp:ErrorsOnly` → 0 errors.
- [ ] All 7 scans PASS (SCAN-01 through SCAN-07).
- [ ] F5 in NinjaTrader compiles green.
