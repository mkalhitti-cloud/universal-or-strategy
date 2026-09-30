# B47-LaneB -- Architecture Plan
**Block**: PTT-COPIER-B47 -- Panel UX Redesign
**Epic**: B47-LaneB
**Date**: 2026-08-07
**Status**: PLAN_COMPLETE (Cycle 2 -- re-review after VIOLATION-1 + VIOLATION-2 fixes)
**Author**: ptt-architect (Phase 1 / re-arch)
**Wave Workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## §1. Block Summary

B47 Lane B delivers a 6-ticket Panel UX redesign of `TradeCopierPanel.cs`.
The redesign replaces the ComboBox follower picker with an always-visible inline
ScrollViewer, adds auto-apply on follower selection, adds a collapsible "Copier"
section header, sorts follower rows dynamically, collapses legacy buttons, and
reorders the panel top-to-bottom. One build-tag ticket targets `CopyEngine.cs`.

Lane C owns all B47 tests. This plan produces **no test file**.

**Ticket table**:

| Ticket | ID | File | Change summary |
|--------|----|------|---------------|
| T1-B | DW-B47-INLINE-FOLLOWERS-02 | `TradeCopierPanel.cs` | Replace ComboBox with inline ScrollViewer (MaxHeight=66) |
| T2-B | DW-B47-AUTO-RULE-01 | `TradeCopierPanel.cs` | Add TryAutoApply() + BuildAtmMap() + BuildMultipliers() |
| T3-B | DW-B47-COPIER-COLLAPSE-05 | `TradeCopierPanel.cs` | Add collapsible "Copier" header + toggle |
| T4-B | DW-B47-FOLLOWERS-SORT-06 | `TradeCopierPanel.cs` | Add SortFollowerRows() -- checked first, alpha secondary |
| T5-B | DW-B47-BUTTON-LAYOUT-03 | `TradeCopierPanel.cs` | Hide Trim/Flatten/ClickTrader/Tighten; restructure to BE\|BE ALL + Quick\|Quick ALL |
| T6-B | DW-B47-PANEL-ORDER-04 | `TradeCopierPanel.cs` | Reorder BuildUI() -- ModeRow top; Copier section below Position Tools; status + BE/Quick at bottom |
| T7-B | Build tag | `CopyEngine.cs` | Update PttBuild.Tag to B47 |

---

## §2. New Fields (all UI-thread-only; plain type; no volatile)

The following fields are **added** to `TradeCopierPanel` by this block:

```csharp
// B47 T1-B: Inline followers ScrollViewer (replaces _followersDropDown in visual tree)
private ScrollViewer _followerScrollViewer       = null;
private StackPanel   _followerScrollViewerPanel  = null;  // inner StackPanel inside ScrollViewer

// B47 T3-B: Collapsible Copier header
private Button _copierCollapseBtn   = null;
private bool   _copierCollapsed     = false;  // default: Copier section expanded

// B47 T5-B/T6-B: Root-level BE and Quick row panels (extracted from _contentPanel)
private UniformGrid _beRowPanel    = null;  // 2-col: BE cluster | BE ALL cluster
private UniformGrid _quickRowPanel = null;  // 2-col: Quick cluster | Quick ALL cluster

// B47 T5-B: Quick ALL tick value (separate from _quickT1 for independent spin)
private int _quickAllT1 = 4;
```

**No existing fields are removed.** `_followersDropDown` is kept but no longer added to
the root StackPanel's Children. `UpdateDropDownHeader()` may still call `_followersDropDown.Text`
on a non-visual ComboBox -- harmless in WPF.

---

## §3. Files In Scope

| Label | Full Path | Change type |
|-------|-----------|-------------|
| FILE A | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Multiple method adds + rewrites |
| FILE B | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | 1-line const update (build tag) |

**Files NOT in scope**: All other `.cs` files in `src/PropTraderTools/`.

---

## §4. Change Design -- T1-B: Inline Followers ScrollViewer

### §4.1 Requirement
DW-B47-INLINE-FOLLOWERS-02: Replace `_followersDropDown` ComboBox in the visual tree with
an always-visible `ScrollViewer` containing a `StackPanel` of per-follower rows.

### §4.2 New method: `LoadFollowers()`

**Signature**: `private void LoadFollowers()`
**CYC**: 2 (null guard [1] + foreach loop [2])
**Called from**: `OnLoaded()` -- after `_followerItems` is populated

**Purpose**: Iterate `_followerItems`, build one imperative WPF row per item into
`_followerScrollViewerPanel.Children`. Calls `SortFollowerRows()` at the end.

```csharp
// B47 T1-B: LoadFollowers -- build inline follower rows into _followerScrollViewerPanel.
// CYC=2: null guard [1] + foreach [2].
// Called from OnLoaded() after _followerItems is populated from Account.All.
// JS-021: no lock. UI-thread only (called on Loaded event).
// NT8-019: no async void. NT8-003: no volatile.
private void LoadFollowers()
{
    if (_followerScrollViewerPanel == null) return;    // guard [1]
    _followerScrollViewerPanel.Children.Clear();
    foreach (var item in _followerItems)               // loop [2]
        BuildInlineFollowerRow(item);
    SortFollowerRows();  // B47 T4-B: initial sort (checked first, alpha within group)
}
```

### §4.3 New method: `BuildInlineFollowerRow(FollowerItem item)`

**Signature**: `private void BuildInlineFollowerRow(FollowerItem item)`
**CYC**: 1 (straight-line; no branches)
**Called from**: `LoadFollowers()`

**Purpose**: Construct a single row panel (4-column horizontal StackPanel) and add
to `_followerScrollViewerPanel.Children`. Columns:
- Col 0: CheckBox (IsChecked bound to item.IsSelected; Checked/Unchecked handlers inline)
- Col 1: TextBlock (account display name, bound to item.ToString())
- Col 2: TextBlock (daily P&L display; bound to item.DailyPnlText; Foreground bound to item.DailyPnlColor)
- Col 3: ATM ComboBox (w=120; IsEnabled=item.IsSelected initially; Loaded/Changed handlers same as BuildCheckItemTemplate)

ATM ComboBox `IsEnabled` is managed by the CheckBox event handlers (code-behind, no WPF Binding class).
P&L TextBlock Foreground and Text are set via code-behind property assignments on the TextBlock, mirroring the
`DailyPnlText`/`DailyPnlColor` binding pattern used in the existing `BuildCheckItemTemplate()` DataTemplate.

```csharp
// B47 T1-B: BuildInlineFollowerRow -- imperative row construction, no DataTemplate.
// CYC=1: straight-line. JS-021: no lock. NT8-012: no FrameworkElementFactory.
// ATM ComboBox IsEnabled is set by CheckBox Checked/Unchecked handlers (code-behind).
// Row: [CheckBox][account TextBlock][P&L TextBlock][ATM ComboBox]  -- 4 columns per spec.
private void BuildInlineFollowerRow(FollowerItem item)
{
    var row = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Margin      = new Thickness(0, 1, 0, 1)
    };

    // Col 0: CheckBox -- tracks IsSelected
    var chk = new CheckBox
    {
        IsChecked         = item.IsSelected,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(0, 0, 4, 0)
    };

    // Col 1: Account name label
    var nameLabel = new TextBlock
    {
        Text              = item.ToString(),
        Width             = 90,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(0, 0, 4, 0)
    };
    nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");

    // Col 2: P&L TextBlock -- mirrors DailyPnlText/DailyPnlColor from BuildCheckItemTemplate().
    // item.DailyPnlText: formatted string e.g. "+$125.00" (set by FollowerItem on each update).
    // item.DailyPnlColor: SolidColorBrush -- green for profit, red for loss, neutral otherwise.
    // Code-behind assignment (no WPF Binding class -- matches imperative row pattern).
    var pnlLabel = new TextBlock
    {
        Text              = item.DailyPnlText,
        Width             = 64,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(0, 0, 4, 0),
        Foreground        = item.DailyPnlColor
    };

    // Col 3: ATM ComboBox (NT8-045: populated from filesystem on Loaded event)
    var atmCombo = new ComboBox
    {
        Width             = 120,
        IsEnabled         = item.IsSelected,   // disabled when unchecked
        VerticalAlignment = VerticalAlignment.Center
    };
    atmCombo.AddHandler(FrameworkElement.LoadedEvent,
        new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded));
    atmCombo.SelectionChanged += OnFollowerAtmTemplateComboChanged;
    atmCombo.DataContext = item;  // needed by OnFollowerAtmTemplateComboLoaded pattern

    // CheckBox event handlers: toggle IsSelected + ATM IsEnabled + sort + auto-apply
    chk.Checked += (s, e) =>
    {
        item.IsSelected  = true;
        atmCombo.IsEnabled = true;
        SortFollowerRows();   // B47 T4-B
        UpdateCopierHeader(); // B47 T3-B
        TryAutoApply();       // B47 T2-B
    };
    chk.Unchecked += (s, e) =>
    {
        item.IsSelected  = false;
        atmCombo.IsEnabled = false;
        SortFollowerRows();   // B47 T4-B
        UpdateCopierHeader(); // B47 T3-B
        TryAutoApply();       // B47 T2-B
    };

    row.Children.Add(chk);
    row.Children.Add(nameLabel);
    row.Children.Add(pnlLabel);   // Col 2: P&L -- added between name and ATM ComboBox
    row.Children.Add(atmCombo);
    _followerScrollViewerPanel.Children.Add(row);
}
```

### §4.4 BuildUI() change -- ComboBox removed, ScrollViewer added

**Before** (in `BuildUI()`, follower section):
```csharp
_followersDropDown = new ComboBox { ... };
_followersDropDown.ItemTemplate = BuildCheckItemTemplate();
root.Children.Add(_followersDropDown);

var applyBtn = new Button { Content = "Add Followers", ... };
applyBtn.Click += OnApplyRule;
root.Children.Add(applyBtn);
```

**After** (in `BuildUI()`, follower section -- T1-B):
```csharp
// _followersDropDown kept as field but NOT added to visual tree.
// B46 OnFollowerAtmTemplateComboLoaded uses _followersDropDown.ItemsSource via OnLoaded --
// that assignment is harmless on a non-visual ComboBox.
_followersDropDown = new ComboBox { IsEditable = false, Text = "0 selected" };
_followersDropDown.ItemTemplate = BuildCheckItemTemplate();

// B47 T1-B: Inline ScrollViewer replacing ComboBox.
// MaxHeight=66 (~3 rows visible at once; scrolls to show all 10 accounts).
_followerScrollViewerPanel = new StackPanel();
_followerScrollViewer = new ScrollViewer
{
    MaxHeight                   = 66,
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    Content                     = _followerScrollViewerPanel,
    Margin                      = new Thickness(0, 0, 0, 2)
};
// *** T1-B IMPLEMENTATION NOTE -- DO NOT ADD _followerScrollViewer TO root HERE ***
// _followerScrollViewer is constructed and populated by T1-B, but it is NOT added to
// any parent StackPanel in T1-B's BuildUI() block.
// _followerScrollViewer enters the visual tree ONLY via BuildCopierSection(root) called
// from T6-B's rebuilt BuildUI(). Adding root.Children.Add(_followerScrollViewer) here
// would cause a WPF InvalidOperationException ("Element is already the child of another
// element") when T6-B subsequently calls BuildCopierSection which adds it a second time.
// T1-B scope: construct + populate only. Visual tree insertion: T6-B exclusively.

// Apply button: HIDDEN (Visibility.Collapsed). Event handler OnApplyRule stays wired.
// B47 spec: HIDE NOT DELETE.
var applyBtn = new Button { Content = "Add Followers", Margin = new Thickness(0, 2, 0, 2),
                            Visibility = Visibility.Collapsed };
applyBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
applyBtn.Click += OnApplyRule;
root.Children.Add(applyBtn);  // in tree but invisible -- preserves OnApplyRule wiring
```

### §4.5 OnLoaded() change -- add LoadFollowers() call

**Before** (OnLoaded, after _followerItems populated):
```csharp
if (_followersDropDown != null)
    _followersDropDown.ItemsSource = _followerItems;
UpdateDropDownHeader();
```

**After**:
```csharp
if (_followersDropDown != null)
    _followersDropDown.ItemsSource = _followerItems;  // kept; harmless on non-visual ComboBox
UpdateDropDownHeader();
LoadFollowers();  // B47 T1-B: populate inline ScrollViewer rows
```

### §4.6 CYC Analysis -- T1-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `LoadFollowers()` | N/A (new) | 2 | ≤ 8 ✓ |
| `BuildInlineFollowerRow()` | N/A (new) | 1 | ≤ 8 ✓ |
| `BuildUI()` | 1 | 1 (no new branches) | ≤ 8 ✓ |
| `OnLoaded()` | 5 | 5 (no new branches) | ≤ 8 ✓ |

---

## §5. Change Design -- T2-B: Auto-Apply on Follower Selection

### §5.1 Requirement
DW-B47-AUTO-RULE-01: Wire rule application to follower checkbox toggle and ATM template
change. Extract `BuildAtmMap` and `BuildMultipliers` from `OnApplyRule` inline code.

### §5.2 New method: `TryAutoApply()`

**Signature**: `private void TryAutoApply()`
**CYC**: 3 (leader null [1] + instrument null [2] + followers empty [3])

```csharp
// B47 T2-B: TryAutoApply -- applies copy rule automatically when follower selection changes.
// CYC=3: leader null [1], instrument null [2], followers empty [3].
// Called from CheckBox Checked/Unchecked lambdas in BuildInlineFollowerRow
// and from OnFollowerAtmTemplateComboChanged.
// OnApplyRule remains wired to (hidden) applyBtn -- NOT removed.
// JS-001: no throw. JS-002: no return null. JS-021: no lock. NT8-019: no async void.
private void TryAutoApply()
{
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;                                    // guard [1]
    if (_instrument == null) return;                                       // guard [2]
    var followers = GetSelectedFollowers();
    if (followers.Length == 0)                                             // guard [3]
    {
        if (_statusText != null) _statusText.Text = "No followers selected.";
        return;
    }
    var atmMap      = BuildAtmMap(followers);
    var multipliers = BuildMultipliers(followers);
    _engine.AddRule(_instrument.FullName, _leaderAccount, followers, multipliers, atmMap);
    _engine.SaveRules();
    if (_statusText != null)
        _statusText.Text = "Rule: " + _instrument.FullName + " leader=" + _leaderAccount.Name;
}
```

### §5.3 New method: `BuildAtmMap(Account[] followers)`

**Signature**: `private Dictionary<string, FollowerAtmMode> BuildAtmMap(Account[] followers)`
**CYC**: 2 (foreach [1] + inner foreach match [2])

Extracted from `OnApplyRule` lines 1872-1877. Key = `account.Name`; value = `ParseAtmModeNameLocal(item.AtmModeName)`.

```csharp
// B47 T2-B: BuildAtmMap -- extracted from OnApplyRule inline block.
// CYC=2: outer foreach [1] + inner foreach match [2].
// Returns Dictionary<string,FollowerAtmMode> -- never null (pre-initialized).
// JS-002: returns initialized Dictionary, not null.
// JS-021: no lock.
private Dictionary<string, FollowerAtmMode> BuildAtmMap(Account[] followers)
{
    var map = new Dictionary<string, FollowerAtmMode>();
    foreach (var acc in followers)                       // [1]
    {
        foreach (var item in _followerItems)             // [2]
        {
            if (item.Account != acc) continue;
            map[acc.Name] = ParseAtmModeNameLocal(item.AtmModeName ?? "Inherit");
            break;
        }
    }
    return map;
}
```

### §5.4 New method: `BuildMultipliers(Account[] followers)`

**Signature**: `private int[] BuildMultipliers(Account[] followers)`
**CYC**: 3 (foreach outer [1] + foreach inner [2] + break match [3])

Extracted from `OnApplyRule` lines 1858-1869.

```csharp
// B47 T2-B: BuildMultipliers -- extracted from OnApplyRule inline block.
// CYC=3: outer foreach [1] + inner foreach [2] + item match break [3].
// Returns int[] same length as followers[]. Index i corresponds to followers[i].
// JS-002: returns initialized int[], not null.
// JS-021: no lock.
private int[] BuildMultipliers(Account[] followers)
{
    var mults = new int[followers.Length];
    for (int i = 0; i < followers.Length; i++)           // [1]
    {
        mults[i] = 1;  // default: 1x
        foreach (var item in _followerItems)             // [2]
        {
            if (item.Account != followers[i]) continue;
            mults[i] = item.Multiplier;
            break;                                       // [3]
        }
    }
    return mults;
}
```

### §5.5 Wire `TryAutoApply()` to `OnFollowerAtmTemplateComboChanged`

**Before** (`OnFollowerAtmTemplateComboChanged`, existing method body end):
```csharp
// ... existing logic sets item.AtmModeName
```

**After** (add at end of method):
```csharp
// ... existing logic sets item.AtmModeName
TryAutoApply();  // B47 T2-B: re-apply rule when ATM template selection changes
```

### §5.6 CYC Analysis -- T2-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `TryAutoApply()` | N/A (new) | 3 | ≤ 8 ✓ |
| `BuildAtmMap()` | N/A (new) | 2 | ≤ 8 ✓ |
| `BuildMultipliers()` | N/A (new) | 3 | ≤ 8 ✓ |
| `OnFollowerAtmTemplateComboChanged` | existing | +0 branches | ≤ 8 ✓ |

---

## §6. Change Design -- T3-B: Collapsible Copier Header

### §6.1 Requirement
DW-B47-COPIER-COLLAPSE-05: Add "▼ Copier" / "▶ Copier  (N active)" collapse button
that toggles `_followerScrollViewer` visibility. Default: expanded.

### §6.2 New method: `BuildCopierSection(StackPanel root)`

**Signature**: `private void BuildCopierSection(StackPanel root)`
**CYC**: 1 (straight-line)

```csharp
// B47 T3-B: BuildCopierSection -- adds Copier header button + ScrollViewer to root.
// CYC=1: straight-line construction.
// _copierCollapseBtn text: "v Copier" (expanded) / "> Copier  (N active)" (collapsed).
// Uses \u25BC / \u25B6 (down/right triangles) -- already present in codebase.
// JS-021: no lock. NT8-019: no async void.
private void BuildCopierSection(StackPanel root)
{
    _copierCollapseBtn = new Button
    {
        Content    = "\u25BC Copier",  // down arrow = expanded
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Margin     = new Thickness(0, 4, 0, 1)
    };
    _copierCollapseBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    _copierCollapseBtn.Click += OnCopierCollapseClick;
    root.Children.Add(_copierCollapseBtn);
    root.Children.Add(_followerScrollViewer);  // already constructed in BuildUI()
}
```

### §6.3 New method: `OnCopierCollapseClick(object sender, RoutedEventArgs e)`

**Signature**: `private void OnCopierCollapseClick(object sender, RoutedEventArgs e)`
**CYC**: 2 (if _copierCollapsed [1] + null guard [2])

```csharp
// B47 T3-B: OnCopierCollapseClick -- toggles _followerScrollViewer Visibility.
// CYC=2: _copierCollapsed branch [1] + null guard [2].
// JS-021: no lock. NT8-019: no async void.
private void OnCopierCollapseClick(object sender, RoutedEventArgs e)
{
    if (_followerScrollViewer == null) return;           // null guard [1]
    _copierCollapsed = !_copierCollapsed;
    _followerScrollViewer.Visibility =
        _copierCollapsed ? Visibility.Collapsed : Visibility.Visible;
    UpdateCopierHeader();                                // [2]
}
```

### §6.4 New method: `UpdateCopierHeader()`

**Signature**: `private void UpdateCopierHeader()`
**CYC**: 2 (null guard [1] + collapsed branch [2])

```csharp
// B47 T3-B: UpdateCopierHeader -- updates collapse button text to reflect current state.
// Expanded: "v Copier"  |  Collapsed: "> Copier  (N active)" where N = checked count.
// CYC=2: null guard [1] + _copierCollapsed branch [2].
private void UpdateCopierHeader()
{
    if (_copierCollapseBtn == null) return;              // guard [1]
    if (_copierCollapsed)                                // [2]
        _copierCollapseBtn.Content = "\u25B6 Copier  (" + CountActiveFollowers() + " active)";
    else
        _copierCollapseBtn.Content = "\u25BC Copier";
}
```

### §6.5 New method: `CountActiveFollowers()`

**Signature**: `private int CountActiveFollowers()`
**CYC**: 1 (foreach loop only -- LINQ avoided per NT8-003 style)

```csharp
// B47 T3-B: CountActiveFollowers -- count of items with IsSelected == true.
// CYC=1: foreach loop.
private int CountActiveFollowers()
{
    int n = 0;
    foreach (var item in _followerItems)
        if (item.IsSelected) n++;
    return n;
}
```

### §6.6 CYC Analysis -- T3-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `BuildCopierSection()` | N/A (new) | 1 | ≤ 8 ✓ |
| `OnCopierCollapseClick()` | N/A (new) | 2 | ≤ 8 ✓ |
| `UpdateCopierHeader()` | N/A (new) | 2 | ≤ 8 ✓ |
| `CountActiveFollowers()` | N/A (new) | 1 | ≤ 8 ✓ |

---

## §7. Change Design -- T4-B: SortFollowerRows()

### §7.1 Requirement
DW-B47-FOLLOWERS-SORT-06: Dynamically reorder follower rows: checked items first, then
alpha by display name within each group. Rebuild `_followerScrollViewerPanel` children.

### §7.2 New method: `SortFollowerRows()`

**Signature**: `private void SortFollowerRows()`
**CYC**: 3 (null guard [1] + List.Sort comparison lambda [2-counted as 1] + foreach rebuild [3])

**Note on CYC of lambda**: The sort comparison lambda contains 2 conditional branches (checked vs unchecked, then alpha). The Comparison<T> delegate is a separate scope. CYC of `SortFollowerRows()` body itself is 3 (guard, sort call, rebuild loop). The lambda is a separate symbol with CYC=3 (IsSelected comparison [1] + nested alpha comparison [2] + ternary result [3]) -- well within ≤ 8.

```csharp
// B47 T4-B: SortFollowerRows -- sort _followerItems and rebuild ScrollViewer panel children.
// Sort order: checked items first; within each group, alpha by account DisplayName.
// Rebuilds _followerScrollViewerPanel.Children to match sorted _followerItems.
// CYC=3: null guard [1] + List.Sort call [2] + foreach rebuild [3].
// JS-021: no lock. UI-thread only.
private void SortFollowerRows()
{
    if (_followerScrollViewerPanel == null) return;  // guard [1]

    _followerItems.Sort((a, b) =>                    // [2]
    {
        if (a.IsSelected != b.IsSelected)
            return a.IsSelected ? -1 : 1;  // checked first
        return string.Compare(a.ToString(), b.ToString(),
                              StringComparison.OrdinalIgnoreCase);
    });

    _followerScrollViewerPanel.Children.Clear();
    foreach (var item in _followerItems)             // [3]
        BuildInlineFollowerRow(item);
}
```

### §7.3 CYC Analysis -- T4-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `SortFollowerRows()` | N/A (new) | 3 | ≤ 8 ✓ |
| Comparison lambda | N/A (new) | 3 | ≤ 8 ✓ |

---

## §8. Change Design -- T5-B: Button Layout Restructure

### §8.1 Requirement
DW-B47-BUTTON-LAYOUT-03: Hide Trim/Flatten row, _quickT3Row, ClickTrader row, tightenRow.
Restructure `BuildBufferedButtonsRow()` to produce:
- Row A (UniformGrid 2-col): BE cluster | BE ALL cluster → stored in `_beRowPanel`
- Row B (UniformGrid 2-col): Quick cluster | Quick ALL cluster (with ▲▼ spinner) → stored in `_quickRowPanel`

### §8.2 Modified method: `BuildBufferedButtonsRow(StackPanel root)`

**Before layout** (current):
```
Row 1 (UniformGrid 2-col): Trim cluster | Flatten cluster        → added to root
Row 2 (UniformGrid 3-col): BE cluster | BE ALL cluster | Quick cluster → added to root
Row 3 (full-width Button): Quick ALL                              → added to root
T3 row (StackPanel, Collapsed): Quick T3                          → added to root
```

**After layout** (B47 T5-B):
```
Row 1 (UniformGrid 2-col, Visibility.Collapsed): Trim | Flatten  → added to root (HIDDEN)
_quickT3Row (StackPanel, Visibility.Collapsed): unchanged         → added to root (HIDDEN)
_beRowPanel (UniformGrid 2-col): BE cluster | BE ALL cluster      → stored in field, NOT added to root here
_quickRowPanel (UniformGrid 2-col): Quick cluster | Quick ALL cluster → stored in field, NOT added to root here
```

`_beRowPanel` and `_quickRowPanel` are added to root StackPanel by `BuildUI()` (T6-B) after the Copier section.

**New method signature**: same -- `private void BuildBufferedButtonsRow(StackPanel root)`
**CYC**: 1 (straight-line; no new branches; hides are Visibility property assignments)

**Key construction change**: Quick ALL cluster gets a DockPanel (matching Quick cluster):

```csharp
// B47 T5-B: Quick ALL cluster -- DockPanel with ▲▼ spinner, same pattern as Quick cluster.
// _quickAllT1 field (default 4) drives the display. OnQuickAllUp/Down handlers added.
var quickAllCluster = new DockPanel { LastChildFill = true };
var quickAllArrows  = new Grid();
quickAllArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
quickAllArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
var quickAllUp = new System.Windows.Controls.Primitives.RepeatButton
    { Content = "\u25B2", Width = 18, Height = 12 };
var quickAllDn = new System.Windows.Controls.Primitives.RepeatButton
    { Content = "\u25BC", Width = 18, Height = 12 };
quickAllUp.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
quickAllDn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
quickAllUp.Click += OnQuickAllUp;
quickAllDn.Click += OnQuickAllDown;
Grid.SetRow(quickAllUp, 0);
Grid.SetRow(quickAllDn, 1);
quickAllArrows.Children.Add(quickAllUp);
quickAllArrows.Children.Add(quickAllDn);
DockPanel.SetDock(quickAllArrows, Dock.Right);
// Reuse existing _quickAllBtn -- replace its parent from standalone to cluster.
// Reconstruct: _quickAllBtn now lives inside quickAllCluster.
_quickAllBtn = new Button
{
    Content         = FormatBuffer("Quick ALL", _quickAllT1),
    BorderBrush     = MakeBrush(13, 148, 136),
    Foreground      = MakeBrush(13, 148, 136),
    BorderThickness = new Thickness(2)
};
_quickAllBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_quickAllBtn.Click += OnQuickAllClick;
quickAllCluster.Children.Add(quickAllArrows);
quickAllCluster.Children.Add(_quickAllBtn);
```

### §8.3 New handlers: `OnQuickAllUp` and `OnQuickAllDown`

**CYC**: 1 each (straight-line)

```csharp
// B47 T5-B: OnQuickAllUp/Down -- spin _quickAllT1 for Quick ALL cluster.
// CYC=1: straight-line. Mirrors OnQuickUp/OnQuickDown pattern.
private void OnQuickAllUp(object sender, RoutedEventArgs e)
{
    _quickAllT1 = Math.Min(_quickAllT1 + 1, 99);
    if (_quickAllBtn != null) _quickAllBtn.Content = FormatBuffer("Quick ALL", _quickAllT1);
}

private void OnQuickAllDown(object sender, RoutedEventArgs e)
{
    _quickAllT1 = Math.Max(_quickAllT1 - 1, 1);
    if (_quickAllBtn != null) _quickAllBtn.Content = FormatBuffer("Quick ALL", _quickAllT1);
}
```

### §8.4 Hiding rows in `BuildUI()` -- T5-B

After `BuildBufferedButtonsRow(_contentPanel)` is called, set Visibility on the rows and the
ClickTrader row container. The ClickTrader row is appended to `_contentPanel` by
`BuildClickTraderRow(_contentPanel)`. tightenRow is constructed and added to `_contentPanel`
inline in `BuildUI()`.

The engineer stores local references to these rows and collapses them after construction:

```csharp
// B47 T5-B: HIDE NOT DELETE -- collapse rows per spec. Event handlers stay wired.
// clickTraderRow is the StackPanel returned by / containing BuildClickTraderRow output.
// tightenRow is the StackPanel built inline for Tighten + ticks.
// row1 (Trim|Flatten) is stored in BuildBufferedButtonsRow via field _trimFlatRow.
// _quickT3Row already starts Collapsed (B41).
// Pattern: store local ref before adding to _contentPanel, then set Visibility.Collapsed.
```

**Implementation note**: The engineer should store the local variables `tightenRow` and the
StackPanel produced by `BuildClickTraderRow` as fields (e.g., `_clickTraderRow`,
`_tightenRow`) OR use the `UIElement.Visibility = Visibility.Collapsed` immediately after
adding to `_contentPanel`. The simplest approach is to add to `_contentPanel` then immediately
collapse:

```csharp
BuildClickTraderRow(_contentPanel);
// B47 T5-B: hide ClickTrader row (Buy/Sell/Arm/Cancel)
// The last child just added is the ClickTrader row:
if (_contentPanel.Children.Count > 0)
{
    var ctRow = _contentPanel.Children[_contentPanel.Children.Count - 1] as FrameworkElement;
    if (ctRow != null) ctRow.Visibility = Visibility.Collapsed;
}

// tightenRow is added inline -- immediately collapse after adding:
_contentPanel.Children.Add(tightenRow);
tightenRow.Visibility = Visibility.Collapsed;  // B47 T5-B: hide Tighten row
```

**Better pattern** (engineer discretion): Add `_clickTraderRowPanel` and `_tightenRow` as
private fields so `Visibility` can be toggled later if needed. Since B47 spec says HIDE NOT
DELETE, storing the ref is cleaner than `Children[Count-1]` indexing.

### §8.5 CYC Analysis -- T5-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `BuildBufferedButtonsRow()` | 1 | 1 | ≤ 8 ✓ |
| `OnQuickAllUp()` | N/A (new) | 1 | ≤ 8 ✓ |
| `OnQuickAllDown()` | N/A (new) | 1 | ≤ 8 ✓ |

---

## §9. Change Design -- T6-B: Panel Vertical Order Restructure

### §9.1 Requirement
DW-B47-PANEL-ORDER-04: Restructure `BuildUI()` to produce final vertical order:
1. BuildModeRow (COPY ON + Signal/Mirror) -- moved to root, above Position Tools
2. Separator + "▼ Position Tools" collapsible header + `_contentPanel`
3. BuildCopierSection ("▼ Copier" header + `_followerScrollViewer`)
4. `_statusText` (moved below Copier section)
5. `_beRowPanel` (BE | BE ALL -- extracted from `BuildBufferedButtonsRow`)
6. `_quickRowPanel` (Quick | Quick ALL cluster)

### §9.2 Before (current `BuildUI()` order):
```
1. _followersDropDown (ComboBox)
2. applyBtn ("Add Followers")
3. separator Border
4. BuildCollapsibleHeader → "▼ Position Tools"
5. _contentPanel:
   a. BuildBufferedButtonsRow (Trim|Flatten / BE|BE ALL|Quick / Quick ALL / T3 row)
   b. _statusText
   c. BuildClickTraderRow (Buy/Sell/Arm/Cancel)
   d. BuildModeRow (Signal|Mirror|COPY ON)
   e. tightenRow
   f. BuildRiskAtrRow
6. root.Children.Add(_contentPanel)
```

### §9.3 After (B47 T6-B `BuildUI()` order):
```
1. _followerScrollViewer (T1-B; applyBtn hidden just above it)
2. BuildModeRow → added directly to root (not _contentPanel)
3. separator Border
4. BuildCollapsibleHeader → "▼ Position Tools"
5. _contentPanel:
   a. BuildBufferedButtonsRow (row1 hidden, row2 hidden, T3 row; _beRowPanel/_quickRowPanel stored but NOT added here)
   b. BuildClickTraderRow (hidden by T5-B)
   c. tightenRow (hidden by T5-B)
   d. BuildRiskAtrRow
6. root.Children.Add(_contentPanel)
7. BuildCopierSection(root)  → "▼ Copier" header + _followerScrollViewer injected here
8. _statusText added to root (NOT to _contentPanel)
9. root.Children.Add(_beRowPanel)
10. root.Children.Add(_quickRowPanel)
```

### §9.4 Key structural diff in `BuildUI()`:

**Before** (BuildModeRow call site):
```csharp
// B9 T3: Copy mode row (Signal / Mirror radio buttons)
BuildModeRow(_contentPanel);
```

**After** (T6-B):
```csharp
// B47 T6-B: Mode row moved to root (above Position Tools header).
BuildModeRow(root);
```

**Before** (_statusText):
```csharp
_statusText = new TextBlock { ... };
_contentPanel.Children.Add(_statusText);
```

**After** (T6-B):
```csharp
// _statusText NOT added to _contentPanel. Added to root after BuildCopierSection.
_statusText = new TextBlock { ... };
// ... (do NOT call _contentPanel.Children.Add(_statusText) here)
```

**After BuildCopierSection** (end of BuildUI()):
```csharp
root.Children.Add(_contentPanel);
BuildCopierSection(root);   // B47 T3-B: "▼ Copier" header + _followerScrollViewer
root.Children.Add(_statusText);   // B47 T6-B: status below Copier
root.Children.Add(_beRowPanel);   // B47 T5-B/T6-B: BE | BE ALL
root.Children.Add(_quickRowPanel); // B47 T5-B/T6-B: Quick | Quick ALL
Content = root;
```

### §9.5 CYC Analysis -- T6-B
| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `BuildUI()` | 1 | 1 (reorder only; no new conditional branches) | ≤ 8 ✓ |

---

## §10. Change Design -- T7-B: Build Tag

**File**: `CopyEngine.cs`
**Class**: `PttBuild` (internal static class)

| State | Value |
|-------|-------|
| Before | `"PTT-COPIER B46 \| atm-template-guard \| 2026-08-06"` |
| After | `"PTT-COPIER B47 \| panel-ux-redesign \| 2026-08-07"` |

Single const string replacement. No logic change. CYC: 1 → 1.

---

## §11. Execution Order and Dependencies

```
T7-B (build tag)     -- no dependencies; execute first or last; fast
T1-B (ScrollViewer)  -- no dependency on other B47 tickets; execute first among UX tickets
T4-B (SortFollowerRows) -- depends on T1-B (_followerScrollViewerPanel field must exist)
T3-B (Copier header) -- depends on T1-B (_followerScrollViewer must exist)
T2-B (TryAutoApply)  -- depends on T1-B (CheckBox lambdas reference TryAutoApply)
                       depends on T4-B (SortFollowerRows called in same lambdas)
T5-B (button layout) -- no dependency on T1-T4; independent restructure
T6-B (panel order)   -- depends on T1-B, T3-B, T5-B (references _followerScrollViewer,
                        BuildCopierSection, _beRowPanel/_quickRowPanel)
```

**Recommended order**: T7-B → T1-B → T4-B → T3-B → T2-B → T5-B → T6-B

**Link sync after all tickets**:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

---

## §12. Acceptance Criteria

| ID | Criterion | How B47 Lane B addresses it |
|----|-----------|----------------------------|
| AC-1 | Followers visible without opening a dropdown; each row shows [CheckBox][account name][P&L TextBlock][ATM ComboBox] | T1-B: ScrollViewer always visible (MaxHeight=66); BuildInlineFollowerRow() constructs 4-column row per spec |
| AC-2 | Checking a follower auto-applies the copy rule | T2-B: TryAutoApply() wired to CheckBox.Checked lambda |
| AC-3 | Unchecking a follower re-applies rule without that follower | T2-B: same TryAutoApply() with updated GetSelectedFollowers() |
| AC-4 | ATM ComboBox disabled when follower row unchecked | T1-B: atmCombo.IsEnabled toggled in Checked/Unchecked lambda |
| AC-5 | Checked follower rows appear at top of list | T4-B: SortFollowerRows() called on every check change |
| AC-6 | "▼ Copier" header click collapses follower section | T3-B: OnCopierCollapseClick toggles _followerScrollViewer.Visibility |
| AC-7 | Collapsed header shows "(N active)" count | T3-B: UpdateCopierHeader() formats count via CountActiveFollowers() |
| AC-8 | Trim/Flatten row hidden (not deleted) | T5-B: row1.Visibility = Collapsed; OnTrimClick/OnFlattenClick stay wired |
| AC-9 | ClickTrader row hidden (not deleted) | T5-B: clickTraderRow.Visibility = Collapsed; OnArmClick etc. stay wired |
| AC-10 | BE\|BE ALL and Quick\|Quick ALL in 2-col 50/50 layout | T5-B: two UniformGrid 2-col rows (_beRowPanel, _quickRowPanel) |
| AC-11 | Quick ALL has ▲▼ spinner | T5-B: quickAllCluster DockPanel with RepeatButtons |
| AC-12 | Mode row (COPY ON/OFF, Signal/Mirror) appears at top | T6-B: BuildModeRow(root) before separator |
| AC-13 | Status text appears below Copier section | T6-B: _statusText added to root after BuildCopierSection |
| AC-14 | OnApplyRule still callable (not deleted) | T1-B: applyBtn added to tree with Visibility.Collapsed |
| AC-15 | PttBuild.Tag updated to B47 | T7-B |

---

## §13. Deferred Items (Carry-Forward from B46 -- Read Only)

| ID | Priority | Status After B47 Lane B | Notes |
|----|----------|------------------------|-------|
| DW-B42-01 | P2 | OPEN | T3 test for IsPttQxTarget -- not in B47 scope |
| DW-B42-02 | P1 | OPEN | DW-B42-05 live acceptance test still unrun |
| DW-B42-03 | P2 | OPEN | T4/T5 slot extension -- future block |
| DW-B42-04 | P2 | OPEN | NT8-NEW comment cosmetic cleanup |
| DW-B42-05 | P1 | UNBLOCKED (B46) | Run live acceptance test D1-D7 after B47 ships |
| DW-B43-02 | P1 | PARTIALLY CLOSED | GetLeaderAtmTemplateName index accuracy (component a) remains open |
| DW-B43-03 | P2 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | OPEN | CopyEngineTests.cs 60 compile errors -- cleanup block |
| DW-B44-02 | P1 | OPEN | Live F5 (DW-B42-05) still pending |
| DW-B44-03 | P1 | PARTIALLY CLOSED | Same as DW-B43-02 |

---

## §14. Scope Exclusions

1. **`PttFollowerStrategy.cs`** -- B46 fix; not touched in B47 Lane B.
2. **Multiplier TextBox in row** -- `BuildInlineFollowerRow` does NOT include a multiplier spinner per-row. The multiplier is preserved via `FollowerItem.Multiplier` and `BuildMultipliers()`. Inline multiplier UI is not in B47 spec.
3. **`CopyEngineTests.cs`** -- 60 pre-existing compile errors remain deferred (DW-B44-01).
4. **Live F5 acceptance test** -- not executed in B47 Lane B. Deferred.
5. **`OnApplyRule` logic** -- no changes to `ParseAtmModeNameLocal`, `GetSelectedFollowers()`, or `OnApplyRule` body. Extraction of `BuildAtmMap`/`BuildMultipliers` into helpers does not change the logic; `OnApplyRule` still calls the inline versions it already has OR can be refactored to call the new helpers (engineer's choice for DRY, not required by spec).
6. **B47 Lane C tests** -- test file is Lane C scope. Lane B writes no test file.
7. **`TradeCopierAddOn.cs`** -- no changes.
8. **`PttContracts.cs`** -- no changes.

---

## §15. Jane Street Alignment

| Rule | P | Scope | Check |
|------|---|-------|-------|
| JS-001 (no throw in hot path) | P0 | TryAutoApply: early `return;` (void), not throw. BuildAtmMap/BuildMultipliers: no throw. | PASS |
| JS-002 (no return null) | P0 | BuildAtmMap returns initialized Dictionary. BuildMultipliers returns initialized int[]. No return null. | PASS |
| JS-021 (no lock) | P0 | All new methods are UI-thread-only. No lock anywhere in new code. | PASS |
| JS-033 (no async void) | P0 | All new methods are `private void` (sync event handlers) or value-returning. No `async void`. | PASS |
| JS-004 (exhaustive matching) | P1 | No new switch/match over DU types. N/A. | N/A |
| JS-008 (readonly struct / frozen brush) | P1 | No new brushes added in B47. Existing frozen brushes reused for Quick ALL cluster. | PASS |
| JS-023 (volatile for cross-thread) | P1 | No new cross-thread fields. All new fields are UI-thread-only (plain bool/int). | PASS |

---

## §16. NT8 Compiler Alignment

| Rule | Severity | Applicable | Status |
|------|----------|-----------|--------|
| NT8-001 (`init` setter banned) | P0 | No new `{ get; init; }` properties | PASS |
| NT8-002 (`abstract/sealed record` banned) | P0 | No records | PASS |
| NT8-003 (`volatile double/bool` banned for UI fields) | P0 | New fields are plain `bool`, `int`, not volatile -- correct | PASS |
| NT8-004 (`System.Collections.Immutable` banned) | P0 | `BuildAtmMap` returns `Dictionary<K,V>` (mutable), not ImmutableDictionary | PASS |
| NT8-007 (`CreateOrder` arg 12 string) | P0 | No `CreateOrder` calls in B47 | N/A |
| NT8-012 (`FrameworkElementFactory` Loaded pattern) | P1 | `BuildInlineFollowerRow` uses `atmCombo.AddHandler(FrameworkElement.LoadedEvent, ...)` matching existing NT8-045 pattern (not FrameworkElementFactory since rows are imperative) | PASS |
| NT8-013 (`DateTime.Now` banned) | P0 | No DateTime usage | PASS |
| NT8-014 (PTT- prefix on CreateOrder) | P1 | No CreateOrder calls | N/A |
| NT8-018 / NT8-019 (`lock()` / `async void` banned) | P0 | Neither present in new code | PASS |
| NT8-020 (SolidColorBrush must Freeze) | P1 | No new brushes instantiated. Existing `MakeBrush()` calls `.Freeze()` internally | PASS |
| NT8-042 (`Dispatcher.InvokeAsync` unavailable from AddOn) | P0 | No new Dispatcher calls. New methods are on UI thread. | PASS |
| NT8-043 (null-conditional compound assignment banned) | P0 | No `-=/?. ` patterns in new code | PASS |
| NT8-044 (`StringComparison` needs `using System;`) | P0 | `string.Compare(..., StringComparison.OrdinalIgnoreCase)` in `SortFollowerRows` -- `using System;` already present at line 2 of file | PASS |
| NT8-045 (AtmStrategy filesystem workaround) | P1 | `OnFollowerAtmTemplateComboLoaded` unchanged; `BuildInlineFollowerRow` wires same handler | PASS |

**No new NT8 compiler rules discovered.** Post-session audit: `nt8-rules(B47): no new rules`.

---

## §17. 7-Scan Pre-Commit Checklist (SCAN-01 through SCAN-07)

All scans target `src/PropTraderTools/TradeCopierPanel.cs` and `src/PropTraderTools/CopyEngine.cs`:

| Scan | Pattern | Command | Expected Result |
|------|---------|---------|----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches in new code |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches in new code |
| SCAN-03 | No `return null` | `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches in new methods |
| SCAN-04 | No `DateTime.Now` | `grep -n "DateTime.Now" src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-05 | No hex color literals | `grep -n '"#[0-9A-Fa-f]' src/PropTraderTools/TradeCopierPanel.cs` | Zero matches |
| SCAN-06 | No `FontFamily` | `grep -rn "FontFamily" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches |
| SCAN-07 | Visibility.Collapsed on hidden rows | `grep -n "Visibility.Collapsed" src/PropTraderTools/TradeCopierPanel.cs` | Matches present for row1, _quickT3Row, clickTraderRow, tightenRow (at minimum 4 sites) |

---

*Architecture plan complete. 10 sequential thoughts executed. Cycle-2 re-arch fixes: VIOLATION-1 (P&L TextBlock added to BuildInlineFollowerRow -- 4 columns), VIOLATION-2 (_followerScrollViewer double-add eliminated -- T1-B construct only; visual tree insertion via T6-B BuildCopierSection exclusively). All 6+1 tickets fully specified.*
