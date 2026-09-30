# PTT-COPIER-B18 Implementation Tickets
# Status: TICKETS_COMPLETE
# Block: PTT-COPIER-B18
# Date: 2026-07-15
# Author: ptt-orchestrator (root causes confirmed from Sim101 live testing)
# Plan: docs/brain/PTT-COPIER-B18/02-architecture-plan.md

---

## Ticket 1 — B18-T1: Fix WireLeaderAccount — FindAccountComboBox

### Overview

| Field | Value |
|-------|-------|
| **Title** | Fix WireLeaderAccount: replace FindVisualChild<ComboBox> with FindAccountComboBox |
| **Defect** | DW-B17-LEADER-01 (P1 blocker — copy trading completely blocked) |
| **Files to MODIFY** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs` |
| **Files to UPDATE** | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` |
| **BANNED FILES** | `TradeCopierPanel.cs` (B17 active), `CopyEngine.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs` |
| **Blocked by** | Nothing — zero file overlap with B17 |
| **Unblocks** | Copy engine test, Trim test, Tighten test, BE test — all require rule registration |
| **Parallel safe** | YES — B17 owns TradeCopierPanel.cs only |

---

### Root Cause (confirmed)

[`WireLeaderAccount`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs:482):

```csharp
var accountCombo = FindVisualChild<ComboBox>(chartTrader);  // BUG: DFS first-match = Instrument ComboBox
```

NT8 ChartTrader has two ComboBoxes. DFS hits Instrument (`MES SEP26`, type=string) first.
`SelectedItem as NinjaTrader.Cbi.Account` = null. `SetLeaderAccount` never called.
`_leaderAccount` = null forever. Every "Apply Rule" click exits with "No leader".

Confirmed live: screenshot shows `PA-APEX-422136-01` selected in ChartTrader, status bar
shows `"No leader -- select account in ChartTrader."` simultaneously.

---

### Step-by-Step Implementation

#### Step 1 — Add `FindAccountComboBox` helper

In [`TradeCopierAddOn.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs:499),
after the existing `FindVisualChild<T>` helper and before `FindVisualChildByName<T>`,
insert the new helper:

```csharp
// B18 T1 -- FindAccountComboBox: walks visual tree, returns first ComboBox whose
// SelectedItem is a NinjaTrader.Cbi.Account. Used by WireLeaderAccount to skip
// the Instrument ComboBox (DFS first-match) and reach the Account ComboBox.
// CYC=4: null guard(1) + count loop(2) + type+cast check(3) + recursive call(4).
// JS-021: no lock. JS-002: returns null only on null parent (guard pattern).
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

// B18 T1 -- FindVisualChildByIndex: returns the Nth ComboBox (0-based) found by DFS.
// Used as fallback in WireLeaderAccount when no account is yet selected (SelectedItem=null).
// NT8 ChartTrader: index 0 = Instrument ComboBox, index 1 = Account ComboBox.
// CYC=5: null guard(1) + count loop(2) + type match(3) + index check(4) + recursive call(5).
// JS-021: no lock. JS-002: returns null only on null parent or index not found.
private static T FindVisualChildByIndex<T>(DependencyObject parent, int targetIndex)
    where T : DependencyObject
{
    int found = 0;
    return FindVisualChildByIndexInternal<T>(parent, targetIndex, ref found);
}

private static T FindVisualChildByIndexInternal<T>(DependencyObject parent, int targetIndex, ref int found)
    where T : DependencyObject
{
    if (parent == null) return null;
    int count = VisualTreeHelper.GetChildrenCount(parent);
    for (int i = 0; i < count; i++)
    {
        var child = VisualTreeHelper.GetChild(parent, i);
        if (child is T match)
        {
            if (found == targetIndex) return match;
            found++;
        }
        var result = FindVisualChildByIndexInternal<T>(child, targetIndex, ref found);
        if (result != null) return result;
    }
    return null;
}
```

#### Step 2 — Update `WireLeaderAccount`

In [`TradeCopierAddOn.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs:482),
replace the body of `WireLeaderAccount`:

**BEFORE:**
```csharp
private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
{
    var accountCombo = FindVisualChild<ComboBox>(chartTrader);
    if (accountCombo == null) return;

    // Set immediately from current selection
    var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
    if (current != null) panel.SetLeaderAccount(current);

    // Keep live as user switches accounts
    accountCombo.SelectionChanged += (s, e) =>
    {
        var acc = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
        panel.SetLeaderAccount(acc);
    };
}
```

**AFTER:**
```csharp
// B18 T1: Fix DW-B17-LEADER-01 -- FindVisualChild<ComboBox> returned Instrument ComboBox
// (DFS first-match). Now: FindAccountComboBox picks first ComboBox whose SelectedItem is Account.
// Fallback: if no account selected yet (all SelectedItems null), use index=1 (Account ComboBox
// is always the second ComboBox in ChartTrader visual tree). NT8-023: lambda captures only
// accountCombo + panel (same visual tree lifetime -- safe).
// CYC=4: null guard(1) + primary find(2) + fallback find(3) + SelectionChanged sub(4).
private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
{
    // Primary: find by SelectedItem type (works when account already selected)
    var accountCombo = FindAccountComboBox(chartTrader);

    // Fallback: no account selected yet -- pick second ComboBox (index 1 = Account)
    if (accountCombo == null)
        accountCombo = FindVisualChildByIndex<ComboBox>(chartTrader, 1);

    if (accountCombo == null) return;

    // Set immediately from current selection
    var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
    if (current != null) panel.SetLeaderAccount(current);

    // Keep live as user switches accounts in ChartTrader
    accountCombo.SelectionChanged += (s, e) =>
    {
        var acc = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
        panel.SetLeaderAccount(acc);
    };
}
```

#### Step 3 — Update comment on WireLeaderAccount

Update the CYC comment above `WireLeaderAccount` from `// CYC=3` to `// CYC=4` to
reflect the added fallback branch.

#### Step 4 — Verify build passes

Run `dotnet build` in `c:\WSGTA\universal-or-strategy`. Zero errors required.

#### Step 5 — Deploy and F5

Copy compiled DLL to NT8 AddOns folder, restart NinjaTrader, press F5, confirm
no compiler errors in NT8 Output window.

#### Step 6 — Live test

In ChartTrader with `PA-APEX-422136-01` selected:
1. Check followers dropdown — select a follower account
2. Click "Apply Rule"
3. Confirm status bar shows `"Rule: MES SEP26 leader=PA-APEX-422136-01..."` (not "No leader")

#### Step 7 — Update NT8_ADDON_KNOWLEDGE.md

Append to the Testing Session section:
```
### DW-B17-LEADER-01 — CLOSED (B18 T1)
Fixed in TradeCopierAddOn.cs. FindAccountComboBox replaces FindVisualChild<ComboBox>.
WireLeaderAccount now correctly wires the Account ComboBox. "Apply Rule" succeeds.
```

---

## Ticket 2 — B18-T2: Fix follower ListBox WPF virtualization trap

### Overview

| Field | Value |
|-------|-------|
| **Title** | TradeCopierWindow follower ListBox: remove outer ScrollViewer, set fixed Height |
| **Defect** | DW-B18-ACCOUNTS-01 (P1 — follower selection blocked in Window) |
| **Files to MODIFY** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs` |
| **Files to UPDATE** | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` |
| **BANNED FILES** | `TradeCopierPanel.cs` (B17 active), `TradeCopierAddOn.cs`, `CopyEngine.cs`, `AtrSizingEngine.cs` |
| **Blocked by** | Nothing — zero file overlap with B17 or B18 T1 |
| **Parallel safe** | YES |

---

### Root Cause (confirmed)

[`BuildRuleRow`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs:282) and
[`BuildDynamicRuleRow`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs:442):

```csharp
var followerLb = new ListBox { MaxHeight = 80, ... };
var followerScroll = new ScrollViewer { MaxHeight = 80, Content = followerLb };
```

`ListBox` inside `ScrollViewer` = WPF `VirtualizingStackPanel` sees infinite height =
renders only 4 items (80px / ~22px = 4). All 20+ accounts bound, none beyond 4 rendered.
Confirmed by: leader `ComboBox` in same row (same `Account.All`) shows all 20+ accounts.

---

### Step-by-Step Implementation

#### Step 1 — Fix `BuildRuleRow`

Locate the follower `ListBox` + `ScrollViewer` block in
[`BuildRuleRow`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs:282):

```csharp
// REMOVE MaxHeight from followerLb declaration:
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    MaxHeight     = 80,           // REMOVE this line
    Margin        = new Thickness(2)
};
_followerBoxes.Add(followerLb);
// REMOVE the ScrollViewer wrapper entirely:
var followerScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 80,
    Content   = followerLb
};
Grid.SetColumn(followerScroll, 2);      // REMOVE
grid.Children.Add(followerScroll);      // REMOVE
```

Replace with:
```csharp
// B18 T2: fix DW-B18-ACCOUNTS-01 -- remove outer ScrollViewer (caused VirtualizingStackPanel
// to see infinite height, rendering only 4 items). ListBox internal scroll now handles scrolling.
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    Height        = 100,          // fixed height -- ListBox internal ScrollViewer handles scroll
    Margin        = new Thickness(2)
};
_followerBoxes.Add(followerLb);
Grid.SetColumn(followerLb, 2);
grid.Children.Add(followerLb);
```

#### Step 2 — Fix `BuildDynamicRuleRow`

Apply identical change to
[`BuildDynamicRuleRow`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs:442):

```csharp
// REMOVE MaxHeight from followerLb, REMOVE followerScroll wrapper, direct placement
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    Height        = 100,
    ItemsSource   = Account.All,
    Margin        = new Thickness(2)
};
Grid.SetColumn(followerLb, 2);
grid.Children.Add(followerLb);
```

Note: `BuildDynamicRuleRow` also needs `_followerBoxes.Add(followerLb)` to be kept — verify
it is still present after the edit (it was not present in the original dynamic row — confirm
from source before editing).

#### Step 3 — Verify build passes

Run `dotnet build`. Zero errors required.

#### Step 4 — Deploy and F5

Copy DLL, restart NinjaTrader, F5, confirm no errors.

#### Step 5 — Live test

1. Open TradeCopierWindow (New → Trade Copier)
2. Row 1 follower area — confirm all 20+ accounts visible without expanding
3. Scroll inside the follower area — confirm works
4. Ctrl+click two accounts — confirm multi-select works
5. Dynamic row (+ Add Rule) — confirm same behavior

#### Step 6 — Update NT8_ADDON_KNOWLEDGE.md

Append to Testing Session section:
```
### DW-B18-ACCOUNTS-01 — CLOSED (B18 T2)
Fixed in TradeCopierWindow.cs. Outer ScrollViewer removed from BuildRuleRow and
BuildDynamicRuleRow. followerLb.Height = 100 (fixed). All accounts now render correctly.
```

---

## Deployment Order

1. **Deploy T1 first** (TradeCopierAddOn.cs) — unblocks copy trading via Panel
2. **F5 verify T1** — confirm "Apply Rule" works and rule registers
3. **Deploy T2** (TradeCopierWindow.cs) — unblocks follower selection in Window
4. **F5 verify T2** — confirm all accounts visible in Window follower ListBox
5. **Run copy engine test** — place Limit order on leader, confirm follower copy fires

---
