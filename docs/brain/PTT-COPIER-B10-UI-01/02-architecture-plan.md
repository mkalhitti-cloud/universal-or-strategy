# PTT-COPIER-B10-UI-01 — Architecture Plan

**Status**: REVIEW_READY
**Epic**: PTT-COPIER-B10-UI-01
**Ticket**: DW-B10-UI-01 — Follower dropdown Grid column alignment
**Date**: 2026-07-07
**Architect**: PTT Architect

---

## 1. Scope Statement

This block addresses **DW-B10-UI-01 only**.

All other OPEN deferred items from PTT-COPIER-B9 backlog are deferred to PTT-COPIER-B10
(separate brain dir) and are **not planned here**:

- DW-B9-01 — ATR box visualization on chart
- DW-B9-02 — NT8 chart attachment API verification for AtrSizingEngine
- DW-B9-03 — Click trader Bid+1/Ask-1 auto-offset
- DW-B9-GAP-001a — Mode 2 HandleBracketChange trailing stop policy
- DW-B9-GAP-001b — BE button MoveStopToBreakEven cancel+replace for trailing stop
- DW-B9-GAP-001c — Tighten Stop button (one-shot)
- DW-B9-GAP-001d — Sim101 verification test (trailing stop PREREQ)
- DW-B10-GAP-002a — Pending BE price watcher
- DW-B10-GAP-002b — MoveStopToBreakEven trailing stop fix

**Carry-forward notice only. Zero plan items for those tickets in this block.**

---

## 2. Problem Statement

`BuildCheckItemTemplate()` in [`TradeCopierPanel.cs`](../../../../WSGTA/universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs)
currently constructs each follower row using a **horizontal StackPanel**:

```
[Account name (variable width)] [P&L] [Mult TB] [ATM CB] [Named TB] [CheckBox]
```

Because account names vary in length (e.g. `Sim101` vs `Apex-Evaluation-5021-B`), all
columns to the right of the name shift horizontally per row. P&L, multiplier, ATM, and
checkbox columns never vertically align across rows — the dropdown checklist is unreadable
at a glance.

**Fix**: Replace the StackPanel row factory with a Grid row factory that enforces fixed-width
columns for every non-name cell, and a star-width column for the name cell with ellipsis
trimming. ColumnDefinitions must be added at runtime (post-materialization) via a Loaded
event handler because WPF `FrameworkElementFactory` cannot add `ColumnDefinitions` at
template-definition time.

---

## 3. Technical Approach

### 3.1 WPF Grid via FrameworkElementFactory + Loaded Event

The standard WPF pattern for Grid-based DataTemplate rows when using `FrameworkElementFactory`:

1. Create a `FrameworkElementFactory(typeof(Grid))` as the visual tree root.
2. Register a `RoutedEventHandler` on `FrameworkElement.LoadedEvent` for the factory.
3. Each child element (TextBlock, TextBox, ComboBox, CheckBox) gets a separate
   `FrameworkElementFactory`, with `Grid.ColumnProperty` set as an attached property value.
4. All child factories are appended to the grid factory.
5. Return a `DataTemplate { VisualTree = gridFactory }`.

At runtime, when the `ItemsControl` materializes each row, WPF fires the `Loaded` event
on the instantiated `Grid`. The handler `OnRowGridLoaded` adds 6 `ColumnDefinition` objects
with the exact widths from the column spec. A `Tag = true` guard prevents re-entry on
re-layout.

### 3.2 Why Loaded, Not FrameworkElementFactory Directly

`FrameworkElementFactory` does not expose a method to add `ColumnDefinitions` before
the element is instantiated. `Grid.ColumnDefinitions` is a `UIElementCollection`-like
object that is only accessible after the `Grid` has been created. The canonical WPF
workaround is the `Loaded` event handler pattern — used throughout this codebase already
(e.g., `OnRowApply` handlers in `TradeCopierWindow.cs`).

---

## 4. Method Signatures

### 4.1 Modified Method: `BuildCheckItemTemplate`

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

```csharp
/// <summary>
/// Builds the DataTemplate for each follower row in the dropdown checklist.
/// Uses a Grid row factory with 6 fixed/star columns so all column cells
/// align vertically across rows regardless of account name length.
/// </summary>
private DataTemplate BuildCheckItemTemplate()
```

**Signature**: unchanged (private, returns `DataTemplate`, no parameters).
**CYC after change**: 1 (no branches — pure factory construction).

**Body summary**:
- Create `FrameworkElementFactory(typeof(Grid))` — root.
- Register `OnRowGridLoaded` on `FrameworkElement.LoadedEvent`.
- Create 6 child `FrameworkElementFactory` instances (TextBlock, TextBlock, TextBox,
  ComboBox, TextBox, CheckBox). Set `Grid.ColumnProperty` on each (0 through 5).
- Preserve all existing `SetBinding` and `SetValue` calls from the prior StackPanel
  children — only the container type changes. Bindings are **unchanged**.
- Append all children to grid factory.
- Return `new DataTemplate { VisualTree = gridFactory }`.

### 4.2 New Method: `OnRowGridLoaded`

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

```csharp
/// <summary>
/// Loaded event handler for Grid rows materialized from BuildCheckItemTemplate.
/// Adds 6 ColumnDefinitions with exact widths from the column spec.
/// Guard: Tag=true prevents re-entry on re-layout.
/// CYC=2: null/type guard + already-configured guard.
/// </summary>
private void OnRowGridLoaded(object sender, RoutedEventArgs e)
```

**Signature**: standard `RoutedEventHandler` pattern (`object sender, RoutedEventArgs e`).
**Return type**: `void`.
**CYC**: 2 (two branches — see Section 5).
**Not async void**: This is a synchronous WPF event handler, not an async operation.

---

## 5. Column Definitions (Exact Widths)

| Col | Content          | Width              | Constraint      | Notes                        |
|-----|------------------|--------------------|-----------------|------------------------------|
| 0   | Account name     | `*` (GridUnitType.Star) | MinWidth=80 | TextTrimming=CharacterEllipsis |
| 1   | Daily P&L        | 62 (fixed px)      | TextAlignment=Right | Existing binding preserved |
| 2   | Multiplier TB    | 30 (fixed px)      | —               | Existing TwoWay binding     |
| 3   | ATM ComboBox     | 80 (fixed px)      | —               | Existing TwoWay binding     |
| 4   | Named TB         | 80 (fixed px)      | Visibility=Collapsed | Unchanged                |
| 5   | CheckBox         | 20 (fixed px)      | HorizontalAlignment=Center | Existing IsChecked binding |

`OnRowGridLoaded` body (CYC=2):

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

---

## 6. Data Flow

```
FollowerAccountViewModel
  (AccountName, DailyPnl, Multiplier, AtmName, IsChecked — UNCHANGED)
        |
        v
  ItemsControl / ListBox (ItemTemplate = BuildCheckItemTemplate())
        |
        v
  [Grid row — 6 ColumnDefinitions added by OnRowGridLoaded]
    Col 0: TextBlock  -- AccountName binding (Width=*, ellipsis, MinWidth=80)
    Col 1: TextBlock  -- DailyPnl binding   (Width=62, Right-aligned)
    Col 2: TextBox    -- Multiplier binding  (Width=30, TwoWay)
    Col 3: ComboBox   -- AtmName binding     (Width=80, TwoWay)
    Col 4: TextBox    -- Named binding       (Width=80, Collapsed)
    Col 5: CheckBox   -- IsChecked binding   (Width=20, Center)
```

**Nothing above the DataTemplate layer changes.** ViewModel, bindings, engine, and
gate chain are all untouched. This is a pure view-layer change.

---

## 7. Threading Model

- `BuildCheckItemTemplate()` is called at initialization time (UI thread or any thread —
  `FrameworkElementFactory` construction is thread-safe).
- `OnRowGridLoaded` fires on the **WPF UI thread** (WPF `Loaded` event invariant).
  All `ColumnDefinitions.Add()` and `grid.Tag` assignments are UI-thread operations.
- No `Dispatcher.InvokeAsync` required.
- No `lock()` — Tag guard is a DependencyProperty, UI-thread-affined.
- No `ConcurrentQueue` interaction.

---

## 8. File Scope

| File | Change |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Modify `BuildCheckItemTemplate()` + add `OnRowGridLoaded()` |
| `src/PropTraderTools/CopyEngine.cs` | **No change** |
| `src/PropTraderTools/TradeCopierWindow.cs` | **No change** |
| `src/PropTraderTools/AtrSizingEngine.cs` | **No change** |
| `tests/CopyEngineTests.cs` | **No change** — no tests required (pure UI layout) |

**Single-file change. Zero cross-contamination.**

---

## 9. NT8 API Usage

| API | Used? | Notes |
|-----|-------|-------|
| `Account.All` | No | Not needed — layout only |
| `CreateOrder` | No | Not needed |
| `account.Positions` | No | Not needed |
| `NTBrushes` | No | No color changes |
| `FontFamily` | **BANNED** — not used | ASCII-only styling |
| `FrameworkElementFactory` | Yes | Standard WPF, available in NT8 host |
| `Grid`, `ColumnDefinition`, `GridLength` | Yes | Standard WPF, available in NT8 host |
| `RoutedEventHandler` | Yes | Standard WPF |
| `FrameworkElement.LoadedEvent` | Yes | Standard WPF routed event |

All types are standard `System.Windows` / `System.Windows.Controls` — no additional
NuGet references or NT8-specific imports required beyond what `TradeCopierPanel.cs`
already uses.

---

## 10. 7-Scan Checklist

| Scan | Rule | Check | Result |
|------|------|-------|--------|
| SCAN-01 | JS-021 — No `lock()` | No lock() in new or modified code | **PASS** |
| SCAN-02 | JS-033 — No `async void` (non-EventHandler) | `OnRowGridLoaded` is `void` (sync), not `async void` | **PASS** |
| SCAN-03 | JS-001 — No `throw` in business logic | Null guard uses `return`, not `throw` | **PASS** |
| SCAN-04 | JS-002 — No `return null` | `BuildCheckItemTemplate` returns non-null `DataTemplate`; `OnRowGridLoaded` is `void` | **PASS** |
| SCAN-05 | JS-036/037 — No `byte[]` heap alloc in hot path | No buffers — pure WPF layout | **PASS** |
| SCAN-06 | ASCII-only | All identifiers and strings are ASCII; no FontFamily; no hex color literals | **PASS** |
| SCAN-07 | CYC check (all methods ≤ 8) | `BuildCheckItemTemplate` CYC=1; `OnRowGridLoaded` CYC=2 | **PASS** |

---

## 11. Build Gate

| Gate | Command | Expected result |
|------|---------|-----------------|
| NT8 compile | F5 in NinjaTrader (or indirect: `dotnet build` of strategy project) | 0 errors |
| Linting | `dotnet build Linting.csproj` | 0 errors |

No new `using` directives needed — `Grid`, `ColumnDefinition`, `FrameworkElementFactory`,
and `RoutedEventHandler` are already imported in `TradeCopierPanel.cs` as a WPF control file.

---

## 12. Carry-Forward Notice — OPEN B9 Deferred Items

The following items remain **OPEN** from PTT-COPIER-B9 and are deferred to
PTT-COPIER-B10 (separate brain directory). They are listed here for traceability only.
**None of these items are in scope for PTT-COPIER-B10-UI-01.**

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B9-01 | ATR box visualization on chart | P2 | OPEN |
| DW-B9-02 | NT8 chart attachment API verification for AtrSizingEngine | P1 | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 | OPEN |
| DW-B9-GAP-001a | Mode 2 trailing stop policy (HandleBracketChange) | P1 | OPEN |
| DW-B9-GAP-001b | BE MoveStopToBreakEven cancel+replace for trailing stop | P1 | OPEN |
| DW-B9-GAP-001c | Tighten Stop button (one-shot) | P2 | OPEN |
| DW-B9-GAP-001d | Sim101 verification test (trailing stop PREREQ) | P1 (prereq) | OPEN |
| DW-B10-GAP-002a | Pending BE price watcher | P1 | OPEN |
| DW-B10-GAP-002b | MoveStopToBreakEven trailing stop fix (post GAP-001d) | P1 | OPEN |
