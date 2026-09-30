# PTT-COPIER-B18 Architecture Plan
# Status: REVIEW_PASS (confirmed from Sim101 live testing session 2026-07-15)
# Block: PTT-COPIER-B18
# Date: 2026-07-15
# Author: ptt-orchestrator (Director-observed Sim101 testing session)
# Prior block: docs/brain/PTT-COPIER-B16/06-deferred-backlog.md
# Spec: 002-trade-copier-spec.html

---

## §A Mission & Root Cause Analysis

### Mission

Fix two P1 runtime blockers preventing copy trading from functioning:

1. **DW-B17-LEADER-01** — Panel "Apply Rule" always reports "No leader" even with account selected
2. **DW-B18-ACCOUNTS-01** — Window follower ListBox shows only 4 accounts, cannot select PA accounts

Both confirmed from Sim101 live testing session (screenshots + code analysis).
No architect investigation phase needed — root causes are fully confirmed.

---

## §B T1 — DW-B17-LEADER-01: WireLeaderAccount finds wrong ComboBox

### Confirmed Root Cause

[`WireLeaderAccount`](src/PropTraderTools/TradeCopierAddOn.cs:482) in `TradeCopierAddOn.cs`:

```csharp
var accountCombo = FindVisualChild<ComboBox>(chartTrader);  // BUG: finds Instrument ComboBox
```

`FindVisualChild<ComboBox>` is a depth-first search (DFS) that returns the **first** `ComboBox`
in the ChartTrader visual tree. NT8 ChartTrader layout has two ComboBoxes in this order:

| Row | Control | SelectedItem type |
|-----|---------|-------------------|
| Row 1 | **Instrument ComboBox** (`MES SEP26`) | `string` — NOT `Account` |
| Row 2 | **Account ComboBox** (`PA-APEX-422136-01`) | `NinjaTrader.Cbi.Account` ← want this |

DFS hits the Instrument ComboBox first. `accountCombo.SelectedItem as NinjaTrader.Cbi.Account`
on a string item = **null**. The `if (current != null)` guard skips `SetLeaderAccount`. The
`SelectionChanged` event is wired to the wrong ComboBox. `_leaderAccount` stays null forever.

Every click on "Apply Rule" hits [`OnApplyRule` L1343](src/PropTraderTools/TradeCopierPanel.cs:1343):
```csharp
if (_leaderAccount == null)
{
    _statusText.Text = "No leader -- select account in ChartTrader.";
    return;
}
```

**Confirmed by screenshot**: `PA-APEX-422136-01l...` visible in ChartTrader Account field.
Status bar shows `"No leader -- select account in ChartTrader."` simultaneously.

### Fix Design (T1)

Add `FindAccountComboBox` private static helper to `TradeCopierAddOn.cs`.
Replace the `FindVisualChild<ComboBox>` call in `WireLeaderAccount` with it.

```csharp
// NEW helper -- walks entire visual tree, returns first ComboBox whose SelectedItem is Account
private static ComboBox FindAccountComboBox(DependencyObject parent)
{
    if (parent == null) return null;
    int count = VisualTreeHelper.GetChildrenCount(parent);
    for (int i = 0; i < count; i++)
    {
        var child = VisualTreeHelper.GetChild(parent, i);
        if (child is ComboBox cb && cb.SelectedItem is NinjaTrader.Cbi.Account)
            return cb;
        var result = FindAccountComboBox(child);
        if (result != null) return result;
    }
    return null;
}
```

In `WireLeaderAccount` (L484), replace:
```csharp
var accountCombo = FindVisualChild<ComboBox>(chartTrader);   // OLD
```
with:
```csharp
var accountCombo = FindAccountComboBox(chartTrader);         // NEW
```

**Scope**: `TradeCopierAddOn.cs` only. One new method, one call changed.
**CYC of new helper**: 4 (null guard + count loop + type+cast check + recursive call).
**Jane Street compliance**: JS-021 (no lock), JS-002 (no return null except on null parent — guard pattern).
**NT8 compiler**: Standard WPF `VisualTreeHelper` — no NT8-specific rule applies.
**Banned files**: `TradeCopierPanel.cs` (B17 owns it), `CopyEngine.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs`.

### Edge Case: Account not yet selected when WireLeaderAccount fires

If NT8 fires `DoInject` before the user has selected an account (Account ComboBox
`SelectedItem` is null at wire time), `FindAccountComboBox` will not find any ComboBox
with a non-null Account item. It returns null. `WireLeaderAccount` returns at L485.
`_leaderAccount` stays null.

The `SelectionChanged` subscription at L492 must STILL be wired so that when the user
later selects an account, the panel updates. This edge case requires a fallback:

If `FindAccountComboBox` returns null, fall back to `FindVisualChild<ComboBox>` to at
least hook the `SelectionChanged` event. The initial `SetLeaderAccount` call is skipped
(no account yet) but future selections will fire correctly.

```csharp
private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
{
    // Try to find the Account ComboBox (SelectedItem is Account)
    var accountCombo = FindAccountComboBox(chartTrader);

    // Edge case: no account selected yet — fall back to second ComboBox by index
    // This ensures SelectionChanged is always wired even before first account pick
    if (accountCombo == null)
        accountCombo = FindVisualChildByIndex<ComboBox>(chartTrader, 1); // 0=Instrument, 1=Account

    if (accountCombo == null) return;

    var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
    if (current != null) panel.SetLeaderAccount(current);

    accountCombo.SelectionChanged += (s, e) =>
    {
        var acc = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
        panel.SetLeaderAccount(acc);
    };
}
```

`FindVisualChildByIndex<T>` is a new helper that returns the Nth match (0-based) from
the DFS walk. Simpler fallback than name-based lookup.

**Engineer decision**: Implement primary `FindAccountComboBox` path. Add
`FindVisualChildByIndex<T>` as the fallback. Both helpers are self-contained, CYC <= 5 each.

---

## §C T2 — DW-B18-ACCOUNTS-01: Follower ListBox WPF Virtualization Trap

### Confirmed Root Cause

[`BuildRuleRow`](src/PropTraderTools/TradeCopierWindow.cs:249) and
[`BuildDynamicRuleRow`](src/PropTraderTools/TradeCopierWindow.cs:407) both wrap
`followerLb` in a `ScrollViewer`:

```csharp
var followerLb = new ListBox { MaxHeight = 80, ... };
var followerScroll = new ScrollViewer { MaxHeight = 80, Content = followerLb };
Grid.SetColumn(followerScroll, 2);
grid.Children.Add(followerScroll);
```

WPF `VirtualizingStackPanel` (default `ListBox` item panel) measures the `ListBox` against
**infinite** available height when the parent is a `ScrollViewer` — the ScrollViewer removes
the layout height constraint. The virtualizer generates item containers only for items that
fit in the **clip rect** (`MaxHeight=80` / ~22px per row = **4 rows**). The remaining 16+
accounts exist in `ItemsSource` but have no rendered containers.

The outer `ScrollViewer` has nothing to scroll: the `ListBox` reports measured height = 4
items tall. `ScrollViewer.ScrollableHeight` = 0. Scroll gestures do nothing.

**Confirmed by screenshot**: Leader `ComboBox` (same `Account.All` source, different rendering
path via WPF `Popup`) shows all 20+ accounts correctly. Follower `ListBox` in same row = 4.

### Fix Design (T2)

Remove the outer `ScrollViewer`. Set `followerLb.Height = 100` (fixed, not `MaxHeight`).
Place `followerLb` directly in the `Grid` column. The `ListBox` has its own internal
`ScrollViewer` that handles scrolling correctly when no outer scroll parent is present.

**In `BuildRuleRow`** — replace:
```csharp
// REMOVE this ScrollViewer wrapper
var followerScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 80,
    Content   = followerLb
};
Grid.SetColumn(followerScroll, 2);
grid.Children.Add(followerScroll);
```
with:
```csharp
// DIRECT placement — no outer ScrollViewer
followerLb.Height = 100;   // fixed height, ListBox internal scroll handles the rest
Grid.SetColumn(followerLb, 2);
grid.Children.Add(followerLb);
```

Also remove `MaxHeight = 80` from the `followerLb` declaration itself (replaced by `Height = 100`).

**Apply identically to `BuildDynamicRuleRow`.**

**Scope**: `TradeCopierWindow.cs` only. Two methods, identical change in each.
**CYC impact**: None — no logic change, layout only.
**NT8 compiler**: Standard WPF layout properties — no NT8 rule applies.
**Banned files**: `TradeCopierPanel.cs`, `TradeCopierAddOn.cs`, `CopyEngine.cs`, `AtrSizingEngine.cs`.

---

## §D Deferred Items (out of B18 scope)

| ID | Description | Why deferred | Target |
|----|-------------|-------------|--------|
| DW-B17-SYNC-01 | Copy ON/OFF sync via CopyEngine event | Touches TradeCopierPanel.cs — B17 owns it | B19 T1 |
| DW-B17-ACCOUNT-NAME-01 | Strip !Apex!Apex broker suffix display | Nice-to-have, no functional impact | B19 T2 |
| DW-B17-WINDOW-01 | Window follower column height (visual) | Superseded by T2 fix | Closed by T2 |

---

## §E Parallel Execution Safety

| File | B17 touches? | B18 T1 touches? | B18 T2 touches? | Conflict? |
|------|-------------|----------------|----------------|---------|
| `TradeCopierPanel.cs` | YES (active) | NO | NO | None |
| `TradeCopierAddOn.cs` | NO | YES | NO | None |
| `TradeCopierWindow.cs` | NO | NO | YES | None |
| `CopyEngine.cs` | NO | NO | NO | None |

**Zero file overlap. B18 T1 and T2 can run in parallel with B17 and with each other.**

---

## §F Success Criteria

After B18 T1 deployed and F5 verified:
- [ ] Click "Apply Rule" in Panel with `PA-APEX-422136-01` in ChartTrader Account field
- [ ] Status bar shows `"Rule: MES SEP26 leader=PA-APEX-422136-01..."` (not "No leader")
- [ ] `CopyEngine._rules` contains one rule entry for MES
- [ ] Copy ON → place Limit order on Sim101 → follower account receives a copied order

After B18 T2 deployed and F5 verified:
- [ ] Open TradeCopierWindow, Row 1 follower area shows all 20+ accounts
- [ ] Scroll works inside the follower ListBox
- [ ] Multi-select works (Ctrl+click selects multiple accounts)
- [ ] Dynamic rows (+ Add Rule) show same full account list

---
