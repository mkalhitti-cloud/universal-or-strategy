# PTT-COPIER-B5 — Tickets
**Block**: B5 (additive on top of B4)
**Plan status**: REVIEW_PASS
**Date**: 2026-07-06
**Tickets**: T1 (TradeCopierPanel.cs), T2 (TradeCopierWindow.cs), T3 (CopyEngineTests.cs)

---

## Ticket T1 — Panel: replace follower ComboBox with multi-select ListBox

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Type**: ADDITIVE (surgical field rename + two method modifications)

---

### Existing code snapshot

`TradeCopierPanel.cs` is the ChartTrader row extension surface (B4, 231 lines).

Key symbols that this ticket builds on:

| Line | Symbol | Role |
|------|--------|------|
| 29 | `private ComboBox _followersCombo;` | Field to be renamed to `ListBox _followersListBox` |
| 48–147 | `BuildUI()` | Constructs the WPF tree; followers section at lines 66–73 |
| 59–63 | `_leaderCombo` setup | Leader ComboBox — **untouched** |
| 66–73 | `_followersCombo` setup | Followers ComboBox — **replaced** with ListBox+ScrollViewer |
| 184–203 | `OnApplyRule()` | Reads selected follower and calls `_engine.AddRule()` |
| 187 | `var follower = _followersCombo?.SelectedItem as Account;` | Single-select extraction — **replaced** |
| 200 | `_engine.AddRule(_instrument.FullName, leader, new[] { follower });` | Single-follower array — **replaced** |

`CopyEngine.AddRule(string, Account, Account[])` at CopyEngine.cs line 118 already accepts `Account[]` of any length. **No engine change needed.**

---

### Changes required

#### 1. Add `using System.Collections.Generic;` (line 6 area)

The file currently imports only `System`, `System.Windows`, `System.Windows.Controls`, `System.Windows.Input`, and NT namespaces. Add this directive so `List<Account>` compiles.

Insert **after** `using System;` (line 5) — or after `using System.Windows.Input;` (line 7) — whichever comes last alphabetically to maintain import ordering:

```csharp
using System.Collections.Generic;
```

#### 2. Rename field `_followersCombo` → `_followersListBox`

**Remove** (line 29):
```csharp
private ComboBox _followersCombo;
```
**Add** in the same position (between `_leaderCombo` and the next field):
```csharp
private ListBox _followersListBox;
```

#### 3. `BuildUI()` — replace followers ComboBox block (lines 66–73)

**Remove** these 4 lines (lines 68–71):
```csharp
_followersCombo = new ComboBox();
_followersCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
_followersCombo.ItemsSource = Account.All;
followersPanel.Children.Add(_followersCombo);
```

**Add** in their place:
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

No style reference is set on the `ListBox` — it inherits the NT WPF theme automatically.

#### 4. `OnApplyRule()` — replace single-follower extraction (lines 186–201)

**Remove** the follower extraction and guard (lines 187–201):
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

**Add** in their place:
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

---

### ADDITIVE contract

**Do NOT remove or alter any existing method, field, or property** other than the four lines being replaced as specified above.

Symbols that MUST remain untouched:

| Symbol | Lines | Reason |
|--------|-------|--------|
| `_engine`, `_instrument`, `_copyToggleBtn`, `_trimBtn`, `_flattenBtn`, `_cancelBtn`, `_beBtn`, `_beBufferBox`, `_statusText`, `_copyEnabled`, `_leaderCombo` | 18–27 | All existing fields |
| `OnInitialize()` | 31–40 | Subscribes engine, builds UI |
| `OnDestroyed()` | 42–46 | Unsubscribes engine |
| `BuildUI()` — all lines except the 4 follower lines removed | 48–147 | All other UI construction |
| `OnToggle()` | 149–154 | Copy on/off |
| `OnTrim()` | 156–160 | Trim handler |
| `OnFlatten()` | 162–166 | Flatten handler |
| `OnCancel()` | 168–172 | Cancel handler |
| `OnBreakEven()` | 175–182 | Break even handler (B4) |
| `OnApplyRule()` — all lines except the 7 follower lines replaced | 184–203 | Leader null check, instrument null check, status text update |
| `OnStatusUpdate()` | 205–212 | Dispatcher.InvokeAsync status update |
| `RelayCommand` nested class | 214–229 | ICommand wrapper |

---

### 7-scan checklist

- [ ] S1: No `lock()` in added code
- [ ] S2: No `DateTime.Now` in added code
- [ ] S3: No hex literals in added code
- [ ] S4: ASCII-only string literals (`"Select leader and at least one follower."` is ASCII)
- [ ] S5: All added code is field/property declarations + inline loops — no new methods; max CYC = 1 (foreach over SelectedItems)
- [ ] S6: No `using` directives removed — only one directive added (`System.Collections.Generic`)
- [ ] S7: Build passes — `dotnet build` in Wave workspace; no NinjaTrader API changes

---

## Ticket T2 — Window: multi-select ListBox + Shift+B KeyBinding

**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Type**: ADDITIVE (two new using directives, one new field, four method modifications, two new methods, one new nested class)

---

### Existing code snapshot

`TradeCopierWindow.cs` is the standalone NTWindow Add-On surface (B4, 399 lines).

Key symbols this ticket builds on:

| Line | Symbol | Role |
|------|--------|------|
| 5–11 | `using` block | Currently missing `System.Windows.Input` and `System.Collections.Generic` |
| 22 | `private bool _copyEnabled;` | Field — new field `_activeRuleInstrument` inserted after this |
| 39–107 | `BuildUI()` | Window layout; `Content = root;` at line 106 — Shift+B setup inserted before this |
| 109–204 | `BuildRuleRow(string instrumentName)` | Fixed rule row — follower ComboBox at lines 142–150; `applyBtn.Tag` at line 183; `grid.MouseEnter` to be added |
| 143–150 | `var followerCb = new ComboBox { ... }` block | Single-select ComboBox — **replaced** with ListBox+ScrollViewer |
| 183 | `applyBtn.Tag = new object[] { instrumentName, leaderCb, followerCb };` | Tag slot 2 — **updated** from `followerCb` to `followerLb` |
| 206–285 | `BuildDynamicRuleRow()` | Dynamic row — follower ComboBox at lines 233–236; `applyBtn.Tag` at line 264; `grid.MouseEnter` to be added |
| 233–236 | `var followerCb = new ComboBox { ... }` block | Single-select ComboBox — **replaced** with ListBox+ScrollViewer |
| 264 | `applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb };` | Tag slot 2 — **updated** from `followerCb` to `followerLb` |
| 354–366 | `OnRowApply()` | Reads tag, extracts follower, calls `_engine.AddRule()` |
| 360–365 | `tag[2] as ComboBox` / single-follower AddRule | **Replaced** with multi-select loop |

---

### Changes required

#### 1. Add two `using` directives (file header)

After `using System.Windows.Controls;` (line 7), insert:
```csharp
using System.Collections.Generic;
using System.Windows.Input;
```

Final import block order:
```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
```

#### 2. New field `_activeRuleInstrument`

**Insert** immediately after `private bool _copyEnabled;` (line 22):
```csharp
private string _activeRuleInstrument; // B5: tracks last-moused-over rule row for Shift+B
```

#### 3. `BuildUI()` — add Shift+B KeyBinding before `Content = root;`

**Insert** the following 3 lines immediately before `Content = root;` (line 106):
```csharp
// B5: Shift+B window-level hotkey -- fires BreakEven on the last-moused-over rule row
var beWinCmd = new RelayCommand(o => OnWindowBreakEven(null, null));
InputBindings.Add(new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift));
```

#### 4. `BuildRuleRow(string instrumentName)` — three changes

**4a. Add MouseEnter tracking** immediately after `var grid = new Grid { Margin = new Thickness(2) };` (line 111):
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrumentName);
```

**4b. Replace follower ComboBox block** (lines 142–150):

Remove:
```csharp
// Follower ComboBox
var followerCb = new ComboBox
{
    ItemsSource = Account.All,
    Margin = new Thickness(2)
};
followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
Grid.SetColumn(followerCb, 2);
grid.Children.Add(followerCb);
```

Add:
```csharp
// B5: Follower ListBox (multi-select)
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

**4c. Update `applyBtn.Tag`** (line 183):

Remove:
```csharp
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerCb };
```
Add:
```csharp
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb };
```

#### 5. `BuildDynamicRuleRow()` — three changes

**5a. Add MouseEnter tracking** immediately after `var grid = new Grid { Margin = new Thickness(2) };` (line 208):
```csharp
grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);
```

> Note: `instrTextBox` is declared at line 219. The lambda captures the reference, not the value, so it reads the current text at the time the mouse enters the row. Place this line **after** `instrTextBox` is declared and assigned (i.e., after line 226 `grid.Children.Add(instrTextBox);`), or capture after the TextBox local is created.
>
> **Precise insertion point**: After `Grid.SetColumn(instrTextBox, 0);` and `grid.Children.Add(instrTextBox);` (lines 225–226), add:
> ```csharp
> grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);
> ```

**5b. Replace follower ComboBox block** (lines 233–236):

Remove:
```csharp
var followerCb = new ComboBox { ItemsSource = Account.All, Margin = new Thickness(2) };
followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
Grid.SetColumn(followerCb, 2);
grid.Children.Add(followerCb);
```

Add:
```csharp
// B5: Follower ListBox (multi-select)
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

**5c. Update `applyBtn.Tag`** (line 264):

Remove:
```csharp
applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb };
```
Add:
```csharp
applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb };
```

#### 6. `OnRowApply()` — replace follower extraction (lines 360–365)

Remove:
```csharp
var leaderCb = tag[1] as ComboBox;
var followerCb = tag[2] as ComboBox;
var leader = leaderCb?.SelectedItem as Account;
var follower = followerCb?.SelectedItem as Account;
if (leader == null || follower == null) return;
_engine.AddRule(instrName, leader, new[] { follower });
```

Add:
```csharp
var leaderCb = tag[1] as ComboBox;
var leader = leaderCb?.SelectedItem as Account;
var followerLb = tag[2] as ListBox;
var followers = new List<Account>();
if (followerLb != null)
    foreach (var item in followerLb.SelectedItems)
        if (item is Account acc) followers.Add(acc);
if (leader == null || followers.Count == 0) return;
_engine.AddRule(instrName, leader, followers.ToArray());
```

#### 7. New method `OnWindowBreakEven` (add after `OnRuleBreakEven`, around line 352)

```csharp
// B5: Shift+B KeyBinding handler -- uses last-moused-over rule row's instrument
private void OnWindowBreakEven(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_activeRuleInstrument)) return;
    var instrument = FindInstrument(_activeRuleInstrument);
    if (instrument != null)
        _engine.BreakEven(instrument, 2);
}
```

#### 8. New method `SetActiveRule` (add immediately after `OnWindowBreakEven`)

```csharp
// B5: called from MouseEnter on rule row grids; tracks active row for Shift+B
private void SetActiveRule(string instrName)
{
    _activeRuleInstrument = instrName;
}
```

#### 9. New nested class `RelayCommand` (add at end of class, before closing `}`)

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

### ADDITIVE contract

**Do NOT remove or alter any existing method, field, or property** other than the lines being replaced as specified above.

Symbols that MUST remain untouched:

| Symbol | Lines | Reason |
|--------|-------|--------|
| `_engine`, `_globalToggleBtn`, `_logPanel`, `_logScroll`, `_rulesPanel`, `_copyEnabled`, `MaxLogLines` | 17–23 | All existing fields |
| `OnInitialize()` | 25–31 | Subscribe, Build |
| `OnDestroyed()` | 33–37 | Unsubscribe |
| `BuildUI()` — all lines except the 3 Shift+B lines inserted | 39–107 | All existing layout |
| `BuildRuleRow()` — all lines except 3 changes (MouseEnter add, ComboBox swap, Tag update) | 109–204 | Leader ComboBox, trim/flatten/cancel/toggle/apply/BE buttons |
| `BuildDynamicRuleRow()` — all lines except 3 changes (MouseEnter add, ComboBox swap, Tag update) | 206–285 | Instrument TextBox, leader ComboBox, trim/flatten/cancel/toggle/apply/BE buttons |
| `OnGlobalToggle()` | 287–292 | Global copy toggle |
| `OnAddRule()` | 294–297 | Adds dynamic row |
| `OnRuleTrim()` | 299–306 | Trim via engine |
| `OnRuleFlatten()` | 308–315 | Flatten via engine |
| `OnRuleCancel()` | 317–324 | Cancel entries via engine |
| `OnRuleToggle()` | 326–334 | Per-rule enable/disable |
| `OnRuleBreakEven()` | 336–352 | [BE] button per row (B4) |
| `OnRowApply()` — lines 354–360 (tag extraction, instrName, leaderCb) | 354–366 | Instrument name extraction, null guard on instrName |
| `OnStatusUpdate()` | 368–383 | Dispatcher.InvokeAsync log append |
| `FindInstrument()` | 385–397 | NT instrument lookup with null guard |

---

### Acceptance criteria

Both `BuildRuleRow` (fixed row) and `BuildDynamicRuleRow` (dynamic row) **must** be updated in the same commit. If one is missed, `tag[2] as ListBox` in `OnRowApply()` returns null and no rule is applied. The `followers.Count == 0` guard prevents silent bad state, but the rule will silently fail to register. Verify both by applying a rule on both a fixed and a dynamic row.

---

### 7-scan checklist

- [ ] S1: No `lock()` in added code
- [ ] S2: No `DateTime.Now` in added code — only existing `DateTime.UtcNow` in `OnStatusUpdate` (untouched)
- [ ] S3: No hex literals in added code
- [ ] S4: ASCII-only string literals — `"// B5: ..."` comment strings and `""` are ASCII
- [ ] S5: All added methods CYC ≤ 8 — `OnWindowBreakEven`: CYC=2 (2 early-returns); `SetActiveRule`: CYC=1 (assignment only); `RelayCommand` methods: CYC=1 each
- [ ] S6: No `using` directives removed — two directives added (`System.Collections.Generic`, `System.Windows.Input`)
- [ ] S7: Build passes — `dotnet build` in Wave workspace

---

## Ticket T3 — Tests: BreakEven xUnit tests + StatusUpdate teardown

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Type**: ADDITIVE (two new `[Fact]` methods + `IDisposable` teardown)

---

### Existing code snapshot

`CopyEngineTests.cs` is the xUnit test file for `CopyEngine` (B3, 226 lines).

Key existing structure:

| Lines | Symbol | Role |
|-------|--------|------|
| 12–14 | `public class CopyEngineTests` / `_engine` field | Test class and singleton reference |
| 16–17 | `GetField()` helper | Reflection accessor for private fields |
| 19–20 | `GetMethod()` helper | Reflection accessor for private methods |
| 22–30 | `SetEnabled_True_EnablesGate1` | [Fact] — subscribes `StatusUpdate` inline, never unsubscribes |
| 31–40 | `SetEnabled_False_BlocksGate1` | [Fact] — same pattern |
| 140–146 | `StatusUpdate_FiresOnSetEnabled` | [Fact] — lambda capture, no unsubscribe |
| 149–157 | `StatusUpdate_MessageContainsON_WhenEnabled` | [Fact] — lambda capture, no unsubscribe |
| 159–168 | `StatusUpdate_MessageContainsOFF_WhenDisabled` | [Fact] — lambda capture, no unsubscribe |
| 226 | `}` (end of class) | Insertion point for Dispose + new [Fact] methods |

The tests use `CopyEngine.Instance` (singleton). `BreakEven(Instrument, int)` is a public internal method at CopyEngine.cs line 418. It calls `AllAccounts(instrument)` which calls `FindRule(instrument)`. `FindRule` returns null for `null` instrument (null guard added in B4 at line 351). When `FindRule` returns null, `AllAccounts` yields nothing → `BreakEven` iterates zero accounts → no StatusUpdate fires and no exception is thrown.

`Trim(null)` and `Flatten(null)` (existing tests at lines 182–186 and 187–193) validate this exact null-instrument guard pattern. The BreakEven tests follow the same model.

---

### Changes required

#### 1. Implement `IDisposable` — add teardown for StatusUpdate (DW-B2-01)

**Modify class declaration** (line 12) to implement `IDisposable`:

Remove:
```csharp
public class CopyEngineTests
```
Add:
```csharp
public class CopyEngineTests : IDisposable
```

**Add `_statusHandler` field** after `_engine` field (line 14):
```csharp
private Action<string> _statusHandler;
```

**Add `Dispose()` method** after the last `[Fact]` method (before closing `}` of class):
```csharp
public void Dispose()
{
    if (_statusHandler != null)
    {
        _engine.StatusUpdate -= _statusHandler;
        _statusHandler = null;
    }
}
```

**Migration note**: Existing test methods that subscribe inline lambdas (`_engine.StatusUpdate += msg => ...`) do not currently use `_statusHandler`. They remain untouched. The `Dispose()` teardown only cleans up subscriptions that use `_statusHandler`. Existing inline-lambda subscriptions are already functionally isolated because: (a) each test captures a local variable that is discarded after the test, and (b) xUnit creates a new class instance per test. The `_statusHandler` mechanism is provided for future tests (including T3's new [Fact] methods below) that want guaranteed cleanup.

#### 2. New `[Fact]`: `BreakEven_NullInstrument_NoException`

```csharp
[Fact]
public void BreakEven_NullInstrument_NoException()
{
    // Arrange
    _engine.SetEnabled(false);

    // Act: null instrument hits FindRule null guard (CopyEngine.cs line 351) -> no accounts iterated
    var ex = Record.Exception(() => _engine.BreakEven(null, 2));

    // Assert: no exception thrown, matching Flatten_EngineAPI_Callable pattern
    Assert.Null(ex);
}
```

#### 3. New `[Fact]`: `BreakEven_NoMatchingRule_FiresNoStatusUpdate`

```csharp
[Fact]
public void BreakEven_NoMatchingRule_FiresNoStatusUpdate()
{
    // Arrange: engine disabled; no rule registered for null instrument
    _engine.SetEnabled(false);
    bool fired = false;
    _statusHandler = _ => fired = true;
    _engine.StatusUpdate += _statusHandler;

    // Act
    _engine.BreakEven(null, 2);

    // Assert: zero accounts iterated -> StatusUpdate never fires
    Assert.False(fired);
}
```

---

### ADDITIVE contract

**Do NOT remove or alter any existing method, field, or property.**

Symbols that MUST remain untouched:

| Symbol | Lines | Reason |
|--------|-------|--------|
| `_engine` field | 14 | Singleton reference used by all tests |
| `GetField()` | 16–17 | Reflection helper |
| `GetMethod()` | 19–20 | Reflection helper |
| All 14 existing `[Fact]` methods | 22–224 | B1–B4 test coverage |
| All existing `using` directives | 4–8 | Required for existing tests |

---

### xUnit tests

| Method | What it asserts |
|--------|-----------------|
| `BreakEven_NullInstrument_NoException` | Calling `BreakEven(null, 2)` does not throw; confirms null-instrument guard at `FindRule` (CopyEngine.cs line 351). Mirrors the `Flatten_EngineAPI_Callable` guard-path pattern. |
| `BreakEven_NoMatchingRule_FiresNoStatusUpdate` | When no rule is registered for the given instrument, `BreakEven` iterates zero accounts and fires no `StatusUpdate` event. Uses `_statusHandler` so the subscription is cleaned up by `Dispose()`. |

---

### 7-scan checklist

- [ ] S1: No `lock()` in added code
- [ ] S2: No `DateTime.Now` in added code
- [ ] S3: No hex literals in added code
- [ ] S4: ASCII-only string literals
- [ ] S5: All added methods CYC ≤ 8 — `Dispose`: CYC=2 (null check + null assign); each `[Fact]`: CYC=1 (linear)
- [ ] S6: No `using` directives removed — existing `using System; System.Collections.Concurrent; System.Reflection; NinjaTrader.Cbi; Xunit;` all preserved
- [ ] S7: Build passes — `dotnet build` in Wave workspace; `dotnet test` passes all 14 existing + 2 new facts

---

*End of PTT-COPIER-B5 Tickets*
