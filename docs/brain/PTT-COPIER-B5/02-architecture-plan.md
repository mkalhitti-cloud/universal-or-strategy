# PTT-COPIER-B5 — Architecture Plan
**Status**: REVIEW_PASS (re-issued after V-01 fix)
**Block**: B5 (additive on top of B4)
**Date**: 2026-07-06
**Inputs read**: specs/002-trade-copier-spec.html, docs/standards/jane-street/RULES_CATALOG.md,
docs/brain/PTT-COPIER-B4/06-deferred-backlog.md, CopyEngine.cs, TradeCopierPanel.cs,
TradeCopierWindow.cs (Wave workspace, read-only)

---

## A — Scope Decision (DW-B5-01 through DW-B5-04)

| ID | Item | Decision | Rationale |
|----|------|----------|-----------|
| DW-B5-01 | Follower multi-select ComboBox (both surfaces) | **IN SCOPE — B5** | P2, target B5. Engine already supports `Account[]`. Gap is UI-only. Full design below. |
| DW-B5-02 | Shift+B per Window rule row | **IN SCOPE — B5** | P2, target B5. Panel already has Shift+B. Window has [BE] button per row but no KeyBinding. Full design below. |
| DW-B5-03 | Rule persistence across sessions | **DEFER — future** | P3. Requires serialization infrastructure (JSON/XML round-trip, NT shutdown hook). Too large for additive B5. No code impact this block. |
| DW-B5-04 | Spec HTML update for B3+B4 changes | **DEFER — future** | P3. Doc-only change. No code impact. |
| DW-B3-03 | xUnit tests for BreakEven() | **IN SCOPE — B5** | Was OPEN from B3 backlog, merged into B5. Two test methods added to CopyEngineTests.cs. |
| DW-B2-01 | StatusUpdate unsubscribe hygiene in tests | **IN SCOPE — B5** | P3, minor. Add IDisposable teardown or explicit -= in test class to prevent event leak warnings. |

### DW-B5-01: Follower Multi-Select — Full Design

**Problem**: Both surfaces call `_engine.AddRule(instrument, leader, new[] { follower })` with exactly one follower. `CopyRule.FollowerAccounts` is already `Account[]` — the engine supports multiple followers; only the UI is limited.

**WPF control choice**: Replace the follower `ComboBox` with a `ListBox` with `SelectionMode="Extended"`. This gives the user Ctrl+click and Shift+click multi-select using NT's native WPF theme (no custom styles needed). Wrap in a `ScrollViewer` with `MaxHeight="80"` to contain the list.

**XAML pattern** (code-behind equivalent):
```csharp
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    ItemsSource   = Account.All,
    MaxHeight     = 80,
    Margin        = new Thickness(2)
};
// No custom style -- inherits NT WPF theme automatically
var followerScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    Content = followerLb
};
```

**Extraction from selection** (applies to both Panel and Window):
```csharp
var followers = new List<Account>();
foreach (var item in followerLb.SelectedItems)
    if (item is Account acc) followers.Add(acc);
if (followers.Count == 0) { /* log error */ return; }
_engine.AddRule(instrName, leader, followers.ToArray());
```

**Tag update for Window's applyBtn**: currently `Tag = new object[] { instrName, leaderCb, followerCb }`. Must become `Tag = new object[] { instrName, leaderCb, followerLb }`. Same slot index (2), type changes from `ComboBox` to `ListBox`.

### DW-B5-02: Shift+B per Window Rule Row — Full Design

**Problem**: `TradeCopierPanel` registers `Shift+B → OnBreakEven` at line 144. `TradeCopierWindow` has a [BE] button per row but no `Shift+B` `KeyBinding`. When Window has focus, Shift+B does nothing.

**Solution**: Add `InputBindings` to `TradeCopierWindow` exactly as done in Panel. The handler needs to know *which rule row* is active (what instrument to pass to BreakEven). Since the Window can display multiple rule rows, we track the last-moused-over instrument in a field `_activeRuleInstrument`.

**Tracking mechanism**: `Grid.MouseEnter` event on each rule row grid sets `_activeRuleInstrument`. No selection model needed — MouseEnter is sufficient for keyboard-then-act flow.

**Fixed row** (`BuildRuleRow(string instrumentName)`):
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrumentName);
```

**Dynamic row** (`BuildDynamicRuleRow()`): instrument name comes from `instrTextBox.Text`, which is mutable. Capture the TextBox reference:
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);
```

**KeyBinding** added in `BuildUI()` of TradeCopierWindow (alongside existing window-level setup):
```csharp
var beWinCmd = new RelayCommand(o => OnWindowBreakEven(null, null));
InputBindings.Add(new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift));
```

**Handler**:
```csharp
private void OnWindowBreakEven(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_activeRuleInstrument)) return;
    var instrument = FindInstrument(_activeRuleInstrument);
    if (instrument == null) return;
    int ticks = 2; // default; active row's beBox is not read via keyboard (use button for custom ticks)
    _engine.BreakEven(instrument, ticks);
}
```

**Note on ticks**: The keyboard shortcut uses a hardcoded default of 2 ticks (matching the beBox default). If the user wants a custom buffer, they use the [BE] button in the row (which reads beBox.Text). This is acceptable — the keyboard shortcut is the fast path.

**RelayCommand in Window**: TradeCopierWindow does not currently have RelayCommand. Add a private nested class identical to Panel's:
```csharp
private sealed class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    internal RelayCommand(Action<object> execute) { _execute = execute; }
    public event EventHandler CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object parameter) => true;
    public void Execute(object parameter) => _execute(parameter);
}
```

---

## B — B5 Change List (additive only)

| File | Change Type | Description |
|------|-------------|-------------|
| `TradeCopierPanel.cs` | Field rename | `_followersCombo` (ComboBox) → `_followersListBox` (ListBox) |
| `TradeCopierPanel.cs` | Method modify | `BuildUI()` — replace follower ComboBox with ListBox+ScrollViewer |
| `TradeCopierPanel.cs` | Method modify | `OnApplyRule()` — extract followers from `SelectedItems` array |
| `TradeCopierWindow.cs` | Field add | `private string _activeRuleInstrument;` |
| `TradeCopierWindow.cs` | Method modify | `BuildUI()` — add Shift+B KeyBinding |
| `TradeCopierWindow.cs` | Method modify | `BuildRuleRow(string)` — replace follower ComboBox with ListBox; add MouseEnter; update applyBtn.Tag |
| `TradeCopierWindow.cs` | Method modify | `BuildDynamicRuleRow()` — same as above with instrTextBox capture |
| `TradeCopierWindow.cs` | Method modify | `OnRowApply()` — extract followers from `SelectedItems` array |
| `TradeCopierWindow.cs` | Method add | `private void OnWindowBreakEven(object sender, RoutedEventArgs e)` |
| `TradeCopierWindow.cs` | Method add | `private void SetActiveRule(string instrName)` |
| `TradeCopierWindow.cs` | Class add | `private sealed class RelayCommand : ICommand` (nested) |
| `CopyEngineTests.cs` | Method add | `BreakEven_FlatAccount_SkipsAndLogs()` |
| `CopyEngineTests.cs` | Method add | `BreakEven_LongPosition_MovesStop_IsLoggedCorrectly()` |
| `CopyEngineTests.cs` | Teardown fix | Add `_engine.StatusUpdate -= ...` in test teardown (DW-B2-01) |

**CopyEngine.cs: ZERO CHANGES.** The engine already supports `Account[]` in `CopyRule.FollowerAccounts`. All gates, dispatch, and BreakEven are unchanged.

---

## C — CopyEngine.cs additions

**NONE.** CopyEngine.cs is not modified in B5. The engine's `AddRule(string, Account, Account[])` at line 118 already accepts an array of any length. `FollowerAccounts` dispatch loop at line 179 already iterates all followers. `BreakEven(Instrument, int)` at line 418 already works correctly. No new public or internal methods needed.

---

## D — TradeCopierPanel.cs additions

### Field change
```csharp
// REMOVE:
private ComboBox _followersCombo;

// ADD:
private ListBox _followersListBox;
```

### BuildUI() modification (followers section only — ~line 66-73)

**Replace**:
```csharp
_followersCombo = new ComboBox();
_followersCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
_followersCombo.ItemsSource = Account.All;
followersPanel.Children.Add(_followersCombo);
```

**With**:
```csharp
_followersListBox = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    ItemsSource   = Account.All,
    MaxHeight     = 80,
    Margin        = new Thickness(0, 2, 0, 0)
};
var followersScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 80,
    Content   = _followersListBox
};
followersPanel.Children.Add(followersScroll);
```

### OnApplyRule() modification (followers extraction — ~line 184-202)

**Replace**:
```csharp
var follower = _followersCombo?.SelectedItem as Account;
if (leader == null || follower == null)
{
    if (_statusText != null)
        _statusText.Text = "Select leader and follower accounts.";
    return;
}
_engine.AddRule(_instrument.FullName, leader, new[] { follower });
```

**With**:
```csharp
var followers = new List<Account>();
if (_followersListBox != null)
    foreach (var item in _followersListBox.SelectedItems)
        if (item is Account acc) followers.Add(acc);
if (leader == null || followers.Count == 0)
{
    if (_statusText != null)
        _statusText.Text = "Select leader and at least one follower.";
    return;
}
_engine.AddRule(_instrument.FullName, leader, followers.ToArray());
```

**Required using**: `using System.Collections.Generic;` — already present in Panel file at line 6 area (standard, add if absent).

---

## E — TradeCopierWindow.cs additions

### Add using directive (file header)

- **Add using directive: `using System.Windows.Input;`**

  `TradeCopierWindow.cs` currently (B4) carries only:
  ```
  using System;
  using System.Windows;
  using System.Windows.Controls;
  using NinjaTrader.Cbi;
  using NinjaTrader.Gui;
  using NinjaTrader.Gui.Tools;
  using NinjaTrader.NinjaScript;
  ```
  `System.Windows.Input` must be added as the 8th using directive (after `System.Windows.Controls`)
  to resolve `Key`, `ModifierKeys`, `KeyBinding`, and `ICommand` -- all of which are introduced
  by the B5 additions below.

### New field (after `private bool _copyEnabled;`)
```csharp
private string _activeRuleInstrument; // tracks last-moused-over rule row for Shift+B
```

### BuildUI() modification — add Shift+B after existing setup

Locate the region where `Content = root;` is set (line 106). **Before** that line, add:
```csharp
// B5: Shift+B global hotkey for Window -- fires BreakEven on active rule
var beWinCmd = new RelayCommand(o => OnWindowBreakEven(null, null));
InputBindings.Add(new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift));
```

### BuildRuleRow(string instrumentName) modifications

**Replace** follower ComboBox block (~lines 142-150):
```csharp
// REMOVE:
var followerCb = new ComboBox
{
    ItemsSource = Account.All,
    Margin = new Thickness(2)
};
followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
Grid.SetColumn(followerCb, 2);
grid.Children.Add(followerCb);
```
**With**:
```csharp
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    ItemsSource   = Account.All,
    MaxHeight     = 80,
    Margin        = new Thickness(2)
};
var followerScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 80,
    Content   = followerLb
};
Grid.SetColumn(followerScroll, 2);
grid.Children.Add(followerScroll);
```

**Update** applyBtn.Tag (~line 183):
```csharp
// REMOVE:
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerCb };

// ADD:
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb };
```

**Add** MouseEnter tracking after grid creation (after `var grid = new Grid ...`):
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrumentName);
```

### BuildDynamicRuleRow() modifications

**Replace** follower ComboBox block (~lines 233-236):
```csharp
// REMOVE:
var followerCb = new ComboBox { ItemsSource = Account.All, Margin = new Thickness(2) };
followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
Grid.SetColumn(followerCb, 2);
grid.Children.Add(followerCb);
```
**With**:
```csharp
var followerLb = new ListBox
{
    SelectionMode = SelectionMode.Extended,
    ItemsSource   = Account.All,
    MaxHeight     = 80,
    Margin        = new Thickness(2)
};
var followerScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 80,
    Content   = followerLb
};
Grid.SetColumn(followerScroll, 2);
grid.Children.Add(followerScroll);
```

**Update** applyBtn.Tag (~line 264):
```csharp
// REMOVE:
applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb };

// ADD:
applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb };
```

**Add** MouseEnter tracking:
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);
```

### OnRowApply() modification (~lines 354-365)

**Replace** follower extraction:
```csharp
// REMOVE:
var followerCb = tag[2] as ComboBox;
var follower = followerCb?.SelectedItem as Account;
if (leader == null || follower == null) return;
_engine.AddRule(instrName, leader, new[] { follower });
```
**With**:
```csharp
var followerLb = tag[2] as ListBox;
var followers = new List<Account>();
if (followerLb != null)
    foreach (var item in followerLb.SelectedItems)
        if (item is Account acc) followers.Add(acc);
if (leader == null || followers.Count == 0) return;
_engine.AddRule(instrName, leader, followers.ToArray());
```

**Required using**: `using System.Collections.Generic;` — add if absent.

### New methods

```csharp
// B5: handler for Shift+B KeyBinding on the Window
private void OnWindowBreakEven(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_activeRuleInstrument)) return;
    var instrument = FindInstrument(_activeRuleInstrument);
    if (instrument != null)
        _engine.BreakEven(instrument, 2);
}

// B5: called from MouseEnter on rule row grids
private void SetActiveRule(string instrName)
{
    _activeRuleInstrument = instrName;
}
```

### New nested class (identical to Panel's RelayCommand)

```csharp
// Minimal ICommand wrapper -- no lock, no state mutation outside Execute
private sealed class RelayCommand : ICommand
{
    private readonly Action<object> _execute;

    internal RelayCommand(Action<object> execute)
    {
        _execute = execute;
    }

    public event EventHandler CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object parameter) => true;

    public void Execute(object parameter) => _execute(parameter);
}
```

---

## F — CopyEngineTests.cs additions

Assumed path: `src/PropTraderTools.Tests/CopyEngineTests.cs` (Wave workspace).

### DW-B3-03: BreakEven tests

```csharp
[Fact]
public void BreakEven_FlatAccount_SkipsAndLogs()
{
    // Arrange: engine with a rule, account has no open position
    var log = new List<string>();
    _engine.StatusUpdate += msg => log.Add(msg);
    // (account.Positions returns empty / zero qty for test instrument)

    // Act
    _engine.BreakEven(_testInstrument, 2);

    // Assert: log contains "flat skip" for the test account
    Assert.Contains(log, l => l.Contains("flat skip"));
}

[Fact]
public void BreakEven_LongPosition_LogsBeMove()
{
    // Arrange: account with long position and a working stop order
    var log = new List<string>();
    _engine.StatusUpdate += msg => log.Add(msg);

    // Act
    _engine.BreakEven(_testInstrument, 2);

    // Assert: log contains "BE moved to" for the account
    Assert.Contains(log, l => l.Contains("BE moved to"));
}
```

### DW-B2-01: StatusUpdate unsubscribe teardown

Add to test class teardown (IDisposable.Dispose or [XUnit.AfterTest]):
```csharp
public void Dispose()
{
    _engine.StatusUpdate -= OnTestStatusUpdate;
    // (where OnTestStatusUpdate is the local capture handler used in tests)
}
```

---

## G — RULES_CATALOG.md compliance checklist

| Rule | Description | B5 Status |
|------|-------------|-----------|
| JS-001 | Result<T,E> instead of exceptions | N/A — no new error-returning methods |
| JS-003 | Sealed record hierarchies | N/A — no new discriminated unions |
| JS-008 | Readonly structs for immutable data | N/A — no new structs; existing TrimSignal/CopySignal unchanged |
| JS-010 | Private constructors / smart constructors | PASS — no new public constructors |
| JS-021 | No lock() anywhere | PASS — zero lock() in all new code |
| JS-023 | Atomic/volatile for simple state | PASS — `_activeRuleInstrument` is UI-thread only; no volatile needed |
| JS-025 | Lock-free data structures | PASS — ConcurrentBag.Add used by engine; no new collections |
| ASCII-only | No Unicode in identifiers or strings | PASS — all new strings/identifiers ASCII |
| No hex colors | All colors via NTBrushes | PASS — no hardcoded hex; ListBox inherits NT theme |
| No FontFamily | No FontFamily usage | PASS — none |
| DateTime.UtcNow | No DateTime.Now | PASS — Window already uses DateTime.UtcNow; no new DateTime usage |
| CYC <= 8 | All methods <= 8 branches | PASS — see Thought 3 analysis; max new method CYC = 2 |
| Dispatcher.InvokeAsync | All UI updates from off-thread via InvokeAsync | PASS — no new off-thread UI updates; existing StatusUpdate path unchanged |
| PTT- prefix | All CreateOrder calls use PTT- prefixed names | PASS — no new CreateOrder calls |
| Additive only | No rewrites | PASS — CopyEngine.cs untouched; Panel/Window are surgical modifications |

---

## H — Risk / Regression notes

| Risk | Severity | Mitigation |
|------|----------|------------|
| ListBox height in cramped ChartTrader Panel | LOW | `MaxHeight = 80` + `ScrollViewer` contains overflow. Minimum 3 accounts visible. Acceptable for the panel's available height. |
| WPF `SelectedItems` is not Observable — `Account.All` changes post-open | LOW | NT accounts are stable during a session. No dynamic refresh needed. Same risk existed with ComboBox. |
| `_activeRuleInstrument` is null on first Shift+B before mousing over any row | LOW | Handler guards with `string.IsNullOrEmpty` check. Fires nothing silently — no error state. |
| Dynamic rule row's `instrTextBox.Text` is empty at MouseEnter time | LOW | `SetActiveRule("")` sets `_activeRuleInstrument = ""`. Subsequent Shift+B sees empty string, guards early-return. User must type instrument name before using keyboard shortcut. Acceptable. |
| `OnRowApply` tag slot 2 changes type (ComboBox → ListBox) | MEDIUM | Both `BuildRuleRow` and `BuildDynamicRuleRow` must be updated in the same ticket. If one is missed, the `tag[2] as ListBox` cast returns null and no rule is applied. Guard: `if (followers.Count == 0) return` prevents silent bad state. Add to ticket acceptance criteria. |
| Test isolation — CopyEngine is a singleton | LOW | Tests must reset engine state between runs. Existing tests handle this; new BreakEven tests follow same pattern. |
| B5 does not add multi-follower display in status log | INFO | Engine already logs per-account. No change needed. Each follower account logs its own status line. |
| Regression: existing single-follower usage | LOW | `followers.ToArray()` with 1 item is equivalent to `new[] { follower }`. Regression risk is zero at the engine level. |

---

*End of PTT-COPIER-B5 Architecture Plan*
