# PTT-COPIER-B2 — Tickets

**Epic:** PTT-COPIER-B2  
**Status:** TICKETS_COMPLETE  
**Date:** 2026-07-06  
**Source plan:** 02-architecture-plan.md (PLAN_COMPLETE)

---

## Dependency Order

```
T1 (CopyEngine.cs)
    └── T2 (TradeCopierWindow.cs)   ─┐
    └── T3 (TradeCopierPanel.cs)    ─┤  parallel after T1
T4 (spec HTML)                      ─┘  fully independent, run any time
```

T1 must complete before T2 and T3 because the new
`AddRule(string, Account, Account[])` overload added in T1 is called
by both T2 (`OnRowApply`) and T3 (`OnApplyRule`).

---

## T1 — CopyEngine.cs repairs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`  
**Defects addressed:** D2a (thread-safe collection), D2b (string-based AddRule overload)  
**Dependency:** None  
**CYC budget:** AddRule overload = 1

### Verified pre-conditions (from file read)

| Check | Finding |
|-------|---------|
| `using System.Collections.Concurrent;` at line 5 | ✅ Already present — no using to add |
| `private readonly List<CopyRule> _rules` at line 21 | ✅ Must be replaced with ConcurrentBag |
| `internal void AddRule(CopyRule rule)` at lines 93-96 | ✅ Existing overload — body unchanged, just changes to ConcurrentBag.Add transparently |
| No second `AddRule` overload exists | ✅ New overload must be inserted after line 96 |

### Change 1 — Line 21: Replace List with ConcurrentBag

**BEFORE (line 21):**
```csharp
        private readonly List<CopyRule> _rules = new List<CopyRule>();
```

**AFTER (line 21):**
```csharp
        private readonly ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```

Note: `using System.Collections.Concurrent;` is already at line 5 — the short
form `ConcurrentBag<CopyRule>` is valid without the fully-qualified name.

Note: `ConcurrentBag.Add()` has the same signature as `List.Add()` — the
existing `AddRule(CopyRule rule)` body at line 95 (`_rules.Add(rule)`) compiles
unchanged.

### Change 2 — After line 96: Insert new AddRule overload

Insert the following block **after** the closing brace of `AddRule(CopyRule rule)`
(currently line 96), as a new method in the Public API section:

```csharp
        internal void AddRule(string instrument, Account master, Account[] followers)
        {
            _rules.Add(CopyRule.Create(instrument, master, followers));
        }
```

This overload is the only external entry point for constructing rules.
`CopyRule` remains a private nested struct — `CopyRule.Create` is called
internally. CYC = 1 (no branches).

### xUnit tests to write

```csharp
[Fact]
public void AddRule_StringOverload_AddsRuleToCollection()
// Arrange: fresh CopyEngine via reflection or test seam
// Act: engine.AddRule("MES", masterAccount, new[] { followerAccount })
// Assert: engine has 1 rule (verify via reflection or via Gate 2 matching in OnOrderUpdate)

[Fact]
public void AddRule_StringOverload_RuleIsFoundByGate2()
// Arrange: engine with one rule for "MES", _isCopyEnabled = true
// Act: simulate OrderUpdate event matching instrument + master
// Assert: StatusUpdate fires (copy dispatched) -- proves rule is reachable

[Fact]
public void Rules_ConcurrentBag_ThreadSafeAddAndEnumerate()
// Arrange: Task A: 100x engine.AddRule(); Task B: 100x foreach(_rules)
// Act: Task.WaitAll(taskA, taskB)
// Assert: no exception thrown, no data corruption
```

### 7-scan checklist for T1

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 | `grep "lock(" CopyEngine.cs` | 0 results |
| SCAN-02 | Non-ASCII chars | 0 results |
| SCAN-03 | `grep "FontFamily" CopyEngine.cs` | 0 results (N/A — no UI) |
| SCAN-04 | `grep "#[0-9A-Fa-f]\{6\}" CopyEngine.cs` | 0 results (N/A — no UI) |
| SCAN-05 | CreateOrder names | "PTT-Copy", "PTT-Trim", "PTT-Flatten" unchanged |
| SCAN-06 | `grep "DateTime\.Now[^U]" CopyEngine.cs` | 0 results |
| SCAN-07 | `grep "lock\s*(" CopyEngine.cs` | 0 results |
| B2-SCAN-04 | `grep "new List<CopyRule>" CopyEngine.cs` | **0 results** |
| B2-SCAN-05 | `grep "ConcurrentBag" CopyEngine.cs` | **exactly 1 result** (field declaration) |
| B2-SCAN-AddRule | `grep "AddRule" CopyEngine.cs` | **exactly 2 results** (both overloads) |

---

## T2 — TradeCopierWindow.cs repairs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`  
**Defects addressed:** D1 (Subscribe lifecycle), D2d (followerCb ItemsSource + Apply button + OnRowApply), D4 (bare catch), D5 (border brush keys)  
**Dependency:** T1 must be complete (OnRowApply calls `_engine.AddRule(string, Account, Account[])`)  
**CYC budget:** OnRowApply = 3

### Verified pre-conditions (from file read)

| Check | Finding |
|-------|---------|
| `OnInitialize()` at lines 24-29 | `_engine.Subscribe()` call is **absent** after line 27 — must add |
| `OnDestroyed()` at lines 31-34 | `_engine.Unsubscribe()` call is **absent** after line 33 — must add |
| `sep1.SetResourceReference(Border.BorderBrushProperty, "BorderBrush")` at line 63 | ✅ Wrong key — must fix |
| `sep2.SetResourceReference(Border.BorderBrushProperty, "BorderBrush")` at line 87 | ✅ Wrong key — must fix |
| `followerCb` at lines 137-143 has no `ItemsSource` | ✅ Must add `followerCb.ItemsSource = Account.All;` |
| Grid has 7 column definitions (cols 0-6) at lines 108-114 | ✅ Must add col 7 for Apply button |
| `catch` at line 241 is bare `catch` with no type | ✅ Must add `(Exception)` |
| `OnRowApply` method does **not** exist | ✅ Must add |

### Change 1 — Lines 27-28: Add Subscribe() in OnInitialize

**BEFORE:**
```csharp
            _engine = CopyEngine.Instance;
            _engine.StatusUpdate += OnStatusUpdate;
            BuildUI();
```

**AFTER:**
```csharp
            _engine = CopyEngine.Instance;
            _engine.StatusUpdate += OnStatusUpdate;
            _engine.Subscribe();
            BuildUI();
```

`_engine.Subscribe()` goes after the StatusUpdate hook and before `BuildUI()`.

### Change 2 — Lines 32-34: Add Unsubscribe() in OnDestroyed

**BEFORE:**
```csharp
        protected override void OnDestroyed()
        {
            _engine.StatusUpdate -= OnStatusUpdate;
        }
```

**AFTER:**
```csharp
        protected override void OnDestroyed()
        {
            _engine.StatusUpdate -= OnStatusUpdate;
            _engine.Unsubscribe();
        }
```

### Change 3 — Line 63: Fix sep1 border brush key

**BEFORE (line 63):**
```csharp
            sep1.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
```

**AFTER (line 63):**
```csharp
            sep1.SetResourceReference(Border.BorderBrushProperty, "NTBrushes.BorderBrush");
```

### Change 4 — Line 87: Fix sep2 border brush key

**BEFORE (line 87):**
```csharp
            sep2.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
```

**AFTER (line 87):**
```csharp
            sep2.SetResourceReference(Border.BorderBrushProperty, "NTBrushes.BorderBrush");
```

### Change 5 — Line 140-143: Add ItemsSource to followerCb

The `followerCb` initializer block (lines 137-143) currently has no `ItemsSource`.

**BEFORE (lines 137-143):**
```csharp
            // Follower ComboBox
            var followerCb = new ComboBox
            {
                Margin = new Thickness(2)
            };
            followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
            Grid.SetColumn(followerCb, 2);
            grid.Children.Add(followerCb);
```

**AFTER:**
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

### Change 6 — BuildRuleRow: Add column 7 Apply button

After the per-rule on/off toggle button block (currently lines 167-171) and
**before** the `return grid;` at line 173, add:

```csharp
            // Apply button (column 7) -- wires leader/follower selection into CopyEngine
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var applyBtn = new Button
            {
                Content = "Apply",
                Tag = new object[] { instrumentName, leaderCb, followerCb },
                Margin = new Thickness(2)
            };
            applyBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
            applyBtn.Click += OnRowApply;
            Grid.SetColumn(applyBtn, 7);
            grid.Children.Add(applyBtn);
```

### Change 7 — Line 241: Fix bare catch

**BEFORE (lines 240-243):**
```csharp
            catch
            {
                return null;
            }
```

**AFTER:**
```csharp
            catch (Exception)
            {
                return null;
            }
```

### Change 8 — Add OnRowApply method

Add the following private method to the class, after the `FindInstrument` method
(after line 245, before the closing `}` of the class):

```csharp
        private void OnRowApply(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var tag = btn?.Tag as object[];
            if (tag == null)
                return;
            var instrName  = tag[0] as string;
            var leaderCb   = tag[1] as ComboBox;
            var followerCb = tag[2] as ComboBox;
            var leader     = leaderCb?.SelectedItem as Account;
            var follower   = followerCb?.SelectedItem as Account;
            if (leader == null || follower == null || instrName == null)
                return;
            _engine.AddRule(instrName, leader, new[] { follower });
        }
```

> **Note on C# version compatibility:** The architecture plan shows `if (btn?.Tag is not object[] tag)`
> (C# 9 negated pattern). The implementation above uses the `as`/null-check form for
> maximum NT8 build-target compatibility. Both are semantically identical. Use the `is not`
> form only if the NT8 solution file targets `<LangVersion>9.0</LangVersion>` or later.

CYC = 3 (1 base + null-check `tag == null` + compound null-check on leader/follower/instrName).

### xUnit tests to write

```csharp
[Fact]
public void OnInitialize_CallsSubscribeOnEngine()
// Arrange: mock CopyEngine with Subscribe recording
// Act: call window.OnInitialize()
// Assert: engine.Subscribe() was called exactly once

[Fact]
public void OnDestroyed_CallsUnsubscribeOnEngine()
// Arrange: initialized window
// Act: call window.OnDestroyed()
// Assert: engine.Unsubscribe() was called exactly once

[Fact]
public void OnRowApply_NullTag_DoesNotCallAddRule()
// Arrange: Button with Tag = null
// Act: OnRowApply(btn, e)
// Assert: engine.AddRule never called

[Fact]
public void OnRowApply_NullLeader_DoesNotCallAddRule()
// Arrange: tag with SelectedItem = null on leaderCb
// Act: OnRowApply(btn, e)
// Assert: engine.AddRule never called

[Fact]
public void OnRowApply_NullFollower_DoesNotCallAddRule()
// Arrange: tag with SelectedItem = null on followerCb
// Act: OnRowApply(btn, e)
// Assert: engine.AddRule never called

[Fact]
public void OnRowApply_ValidSelections_CallsAddRule()
// Arrange: tag with instrName="MES", leaderCb.SelectedItem=master, followerCb.SelectedItem=follower
// Act: OnRowApply(btn, e)
// Assert: engine.AddRule("MES", master, [follower]) called once

[Fact]
public void FindInstrument_ExceptionThrown_ReturnsNull()
// Arrange: instrument name that throws in GetInstrument
// Act: FindInstrument("BAD_NAME")
// Assert: returns null, no unhandled exception
```

### 7-scan checklist for T2

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 | `grep "lock(" TradeCopierWindow.cs` | 0 results |
| SCAN-02 | Non-ASCII chars | 0 results |
| SCAN-03 | `grep "FontFamily" TradeCopierWindow.cs` | 0 results |
| SCAN-04 | `grep "#[0-9A-Fa-f]\{6\}" TradeCopierWindow.cs` | 0 results |
| SCAN-05 | CreateOrder calls in this file | N/A — Window never creates orders |
| SCAN-06 | `grep "DateTime\.Now[^U]" TradeCopierWindow.cs` | 0 results |
| SCAN-07 | `grep "lock\s*(" TradeCopierWindow.cs` | 0 results |
| B2-SCAN-03 | `grep "_engine.Subscribe\|_engine.Unsubscribe" TradeCopierWindow.cs` | **exactly 2 results** |
| B2-SCAN-06 | `grep 'catch {' TradeCopierWindow.cs` | **0 results** (bare catch gone) |
| B2-SCAN-07 | `grep '"BorderBrush"' TradeCopierWindow.cs` (plain key, no NTBrushes prefix) | **0 results** |
| B2-AddRule | `grep "AddRule" TradeCopierWindow.cs` | **at least 1 result** (OnRowApply body) |

---

## T3 — TradeCopierPanel.cs repairs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`  
**Defects addressed:** D2c (combo field promotion + Apply button + OnApplyRule), D3 (button IsEnabled)  
**Dependency:** T1 must be complete (OnApplyRule calls `_engine.AddRule(string, Account, Account[])`)  
**CYC budget:** OnApplyRule = 6

### Verified pre-conditions (from file read)

| Check | Finding |
|-------|---------|
| `leaderCombo` is a local variable in `BuildUI()` at line 55 | ✅ Must promote to private field |
| `followersCombo` is a local variable in `BuildUI()` at line 65 | ✅ Must promote to private field |
| leaderCombo populated via `foreach ... Items.Add(acc.Name)` at lines 57-58 | ✅ Must replace with `ItemsSource = Account.All` |
| followersCombo populated via `foreach ... Items.Add(acc.Name)` at lines 67-68 | ✅ Must replace with `ItemsSource = Account.All` |
| No Apply button in `BuildUI()` | ✅ Must add after `accountGrid` is added to root |
| `_trimBtn` at line 89: `IsEnabled = false` | ✅ Must change to `IsEnabled = true` |
| `_flattenBtn` at line 94: `IsEnabled = false` | ✅ Must change to `IsEnabled = true` |
| `_cancelBtn` at line 99: `IsEnabled = false` | ✅ Must change to `IsEnabled = true` |
| `OnApplyRule` method does **not** exist | ✅ Must add |
| Panel does NOT call `Subscribe()` or `Unsubscribe()` | ✅ Confirmed — must stay that way |

### Change 1 — Class-level field declarations: Promote combos

Add two private fields to the class declaration block (after line 25,
`private bool _copyEnabled;`):

```csharp
        private ComboBox _leaderCombo;
        private ComboBox _followersCombo;
```

### Change 2 — Lines 55-59: Replace leaderCombo local with field + ItemsSource

**BEFORE (lines 55-59):**
```csharp
            var leaderCombo = new ComboBox();
            leaderCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
            foreach (Account acc in Account.All)
                leaderCombo.Items.Add(acc.Name);
            leaderPanel.Children.Add(leaderCombo);
```

**AFTER:**
```csharp
            _leaderCombo = new ComboBox();
            _leaderCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
            _leaderCombo.ItemsSource = Account.All;
            leaderPanel.Children.Add(_leaderCombo);
```

### Change 3 — Lines 65-69: Replace followersCombo local with field + ItemsSource

**BEFORE (lines 65-69):**
```csharp
            var followersCombo = new ComboBox();
            followersCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
            foreach (Account acc in Account.All)
                followersCombo.Items.Add(acc.Name);
            followersPanel.Children.Add(followersCombo);
```

**AFTER:**
```csharp
            _followersCombo = new ComboBox();
            _followersCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
            _followersCombo.ItemsSource = Account.All;
            followersPanel.Children.Add(_followersCombo);
```

### Change 4 — After line 73: Add Apply Rule button

After `root.Children.Add(accountGrid);` (line 73) and **before** the separator
comment (line 75), insert:

```csharp
            // Apply Rule button -- wires combo selections into CopyEngine
            var applyBtn = new Button { Content = "Apply Rule" };
            applyBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            applyBtn.Click += OnApplyRule;
            root.Children.Add(applyBtn);
```

### Change 5 — Line 89: trimBtn IsEnabled true

**BEFORE (line 89):**
```csharp
            _trimBtn = new Button { Content = "Trim 1/2  S+T", IsEnabled = false };
```

**AFTER (line 89):**
```csharp
            _trimBtn = new Button { Content = "Trim 1/2  S+T", IsEnabled = true };
```

### Change 6 — Line 94: flattenBtn IsEnabled true

**BEFORE (line 94):**
```csharp
            _flattenBtn = new Button { Content = "Flatten  S+F", IsEnabled = false };
```

**AFTER (line 94):**
```csharp
            _flattenBtn = new Button { Content = "Flatten  S+F", IsEnabled = true };
```

### Change 7 — Line 99: cancelBtn IsEnabled true

**BEFORE (line 99):**
```csharp
            _cancelBtn = new Button { Content = "Cancel  S+C", IsEnabled = false };
```

**AFTER (line 99):**
```csharp
            _cancelBtn = new Button { Content = "Cancel  S+C", IsEnabled = true };
```

### Change 8 — Add OnApplyRule method

Add the following private method after `OnCancel` (after line 146), before
`OnStatusUpdate`:

```csharp
        private void OnApplyRule(object sender, RoutedEventArgs e)
        {
            var leader   = _leaderCombo?.SelectedItem as Account;
            var follower = _followersCombo?.SelectedItem as Account;
            if (_instrument == null)
            {
                if (_statusText != null)
                    _statusText.Text = "No instrument -- open a chart first.";
                return;
            }
            if (leader == null || follower == null)
            {
                if (_statusText != null)
                    _statusText.Text = "Select leader and follower accounts.";
                return;
            }
            _engine.AddRule(_instrument.FullName, leader, new[] { follower });
            if (_statusText != null)
                _statusText.Text = "Rule applied: " + _instrument.Name;
        }
```

CYC = 6:
- 1 base
- `_instrument == null` check (+1)
- `_statusText != null` inside instrument-null branch (+1)
- `leader == null || follower == null` check (+1)
- `_statusText != null` inside leader/follower-null branch (+1)
- `_statusText != null` at end (+1)

All within Jane Street strict standard (CYC <= 8).

### xUnit tests to write

```csharp
[Fact]
public void OnApplyRule_NullInstrument_ShowsStatusText()
// Arrange: panel with _instrument = null, combos have selections
// Act: OnApplyRule(null, null)
// Assert: _statusText.Text == "No instrument -- open a chart first."

[Fact]
public void OnApplyRule_NullLeader_ShowsStatusText()
// Arrange: panel with instrument set, _leaderCombo.SelectedItem = null
// Act: OnApplyRule(null, null)
// Assert: _statusText.Text == "Select leader and follower accounts."

[Fact]
public void OnApplyRule_NullFollower_ShowsStatusText()
// Arrange: panel with instrument set, leader selected, follower SelectedItem = null
// Act: OnApplyRule(null, null)
// Assert: _statusText.Text == "Select leader and follower accounts."

[Fact]
public void OnApplyRule_ValidSelections_CallsAddRule()
// Arrange: panel with instrument "MES", leader, follower selected
// Act: OnApplyRule(null, null)
// Assert: engine.AddRule("MES", leader, [follower]) called once
//         _statusText.Text starts with "Rule applied:"

[Fact]
public void TrimButton_IsEnabled_True()
// Arrange: BuildUI() called
// Assert: _trimBtn.IsEnabled == true

[Fact]
public void FlattenButton_IsEnabled_True()
// Assert: _flattenBtn.IsEnabled == true

[Fact]
public void CancelButton_IsEnabled_True()
// Assert: _cancelBtn.IsEnabled == true
```

### 7-scan checklist for T3

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 | `grep "lock(" TradeCopierPanel.cs` | 0 results |
| SCAN-02 | Non-ASCII chars | 0 results |
| SCAN-03 | `grep "FontFamily" TradeCopierPanel.cs` | 0 results |
| SCAN-04 | `grep "#[0-9A-Fa-f]\{6\}" TradeCopierPanel.cs` | 0 results |
| SCAN-05 | CreateOrder calls in this file | N/A — Panel never creates orders |
| SCAN-06 | `grep "DateTime\.Now[^U]" TradeCopierPanel.cs` | 0 results |
| SCAN-07 | `grep "lock\s*(" TradeCopierPanel.cs` | 0 results |
| B2-SCAN-02 | `grep "_engine.Subscribe\|_engine.Unsubscribe" TradeCopierPanel.cs` | **0 results** — Panel must NOT own lifecycle |
| B2-SCAN-08 | `grep "IsEnabled = false" TradeCopierPanel.cs` | **0 results** for _trimBtn, _flattenBtn, _cancelBtn |
| B2-AddRule | `grep "AddRule" TradeCopierPanel.cs` | **at least 1 result** (OnApplyRule body) |
| B2-ItemsSource | `grep "Items.Add" TradeCopierPanel.cs` | **0 results** — foreach string-add loops removed |

---

## T4 — specs/002-trade-copier-spec.html surgical edits

**File:** `c:\WSGTA\universal-or-strategy-director\specs\002-trade-copier-spec.html`  
**Dependency:** None — fully independent, can run in parallel with T1/T2/T3  
**Method:** `search_and_replace` or `apply_diff` only. Do NOT rewrite the file.  
**Tests:** None required (HTML spec is documentation, not compiled code).

### SD-1 (~line 697): Update JS rule citation for _rules collection

**FIND:**
```
JS-021, JS-023 (Interlocked for dedup) enforced throughout
```

**REPLACE:**
```
JS-021 (no lock), JS-023 (volatile bool), JS-025 (ConcurrentDictionary dedup) enforced throughout
```

---

### SD-2 (~line 663): Correct dedup key description

**FIND:**
```
10-second composite fingerprint</code>. Prevents double-fire
```

**REPLACE:**
```
10-second TTL keyed on <code style="font-family:var(--mono); font-size:11px;">orderId</code> (NT8 order IDs are unique per order event). Prevents double-fire
```

---

### SD-3 (~line 997): Correct dedup mechanism description

**FIND:**
```
Dedup dictionary guarded by <code style="font-family:var(--mono);font-size:11px;">Interlocked.CompareExchange</code>, not lock()
```

**REPLACE:**
```
Dedup dictionary is <code style="font-family:var(--mono);font-size:11px;">ConcurrentDictionary&lt;string,long&gt;</code> — lock-free TryAdd/TryRemove. No Interlocked needed.
```

---

### SD-4 (~line 1070): Add sealed to TradeCopierPanel class snippet

**FIND (multiline):**
```
<span class="kw">public class</span>
<span class="fn">TradeCopierPanel</span>
```

**REPLACE (multiline):**
```
<span class="kw">public sealed class</span>
<span class="fn">TradeCopierPanel</span>
```

---

### SD-5a (~line 1051): Update CopyEngine line count

**FIND:**
```
// ── 1. CopyEngine.cs (~170 lines) ── pure logic, zero UI refs
```

**REPLACE:**
```
// ── 1. CopyEngine.cs (~350 lines) ── pure logic, zero UI refs
```

---

### SD-5b (~line 1068): Update TradeCopierPanel line count

**FIND:**
```
// ── 2. TradeCopierPanel.cs (~100 lines) ── ChartTrader row
```

**REPLACE:**
```
// ── 2. TradeCopierPanel.cs (~175 lines) ── ChartTrader row
```

---

### SD-5c (~line 1082): Update TradeCopierWindow line count

**FIND:**
```
// ── 3. TradeCopierWindow.cs (~80 lines) ── Add-On window
```

**REPLACE:**
```
// ── 3. TradeCopierWindow.cs (~250 lines) ── Add-On window
```

---

### SD-6 (~line 411): Update phase status pill

**FIND (multiline):**
```
<span class="pill pill-green">
<span class="status-dot dot-green">
</span>Phase 1 — Brainstorm</span>
```

**REPLACE (multiline):**
```
<span class="pill pill-green">
<span class="status-dot dot-green">
</span>Block 1 — COMPLETE · Block 2 active</span>
```

---

### SD-7 (~line 1694): Update spec status banner

**FIND:**
```
<span class="pill pill-green">Spec locked — ready to build Block 1</span>
```

**REPLACE:**
```
<span class="pill pill-green">Block 1 COMPLETE · Block 2 repairs in progress</span>
```

---

### SD-8 (~line 1163): Update button IsEnabled documentation

**FIND:**
```
Disabled (grayed) when position qty == 0. Market order exit, ceil(qty/2) per account independently.
```

**REPLACE:**
```
Block 1: always enabled (engine handles flat-skip internally, logs "flat skip"). Block 2: live position binding to disable when flat. Market order exit, ceil(qty/2) per account independently.
```

---

### SD-9 (~line 545): Update total line count

**FIND:**
```
Total: ~320 lines. Everything else is NT's.
```

**REPLACE:**
```
Total: ~770 lines (B1 actual). Everything else is NT's.
```

---

### SD-10 (~lines 1273-1275): Update Gate 2 code snippet to multi-rule scan

**FIND (multiline):**
```
          // Gate 2: is this the master account on this instrument?
          if (e.Order.Account != _masterAccount) return;
          if (e.Order.Instrument != Instrument)  return;
```

**REPLACE (multiline):**
```
          // Gate 2: find matching rule (instrument + master account)
          CopyRule? matchedRule = null;
          foreach (var rule in _rules)
          {
            if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)
            { matchedRule = rule; break; }
          }
          if (matchedRule == null) return;
```

---

### T4 verification after apply

After all 10 search-and-replace operations, grep the file for each new string to
confirm the replacement landed:

| SD | New text to search for | Expected |
|----|------------------------|---------|
| SD-1 | `JS-025 (ConcurrentDictionary dedup)` | 1 result |
| SD-2 | `10-second TTL keyed on` | 1 result |
| SD-3 | `ConcurrentDictionary&lt;string,long&gt;` | 1 result |
| SD-4 | `public sealed class</span>` (in span context) | 1 result |
| SD-5a | `CopyEngine.cs (~350 lines)` | 1 result |
| SD-5b | `TradeCopierPanel.cs (~175 lines)` | 1 result |
| SD-5c | `TradeCopierWindow.cs (~250 lines)` | 1 result |
| SD-6 | `Block 1 — COMPLETE · Block 2 active` | 1 result |
| SD-7 | `Block 1 COMPLETE · Block 2 repairs in progress` | 1 result |
| SD-8 | `Block 1: always enabled (engine handles flat-skip` | 1 result |
| SD-9 | `~770 lines (B1 actual)` | 1 result |
| SD-10 | `find matching rule (instrument + master account)` | 1 result |

---

## Summary

| Ticket | File | Defects fixed | New methods | xUnit tests | Parallel? |
|--------|------|---------------|-------------|-------------|-----------|
| T1 | CopyEngine.cs | D2a, D2b | `AddRule(string,Account,Account[])` | 3 | First (blocks T2, T3) |
| T2 | TradeCopierWindow.cs | D1, D2d, D4, D5 | `OnRowApply` | 7 | After T1 |
| T3 | TradeCopierPanel.cs | D2c, D3 | `OnApplyRule` | 7 | After T1 |
| T4 | 002-trade-copier-spec.html | SD-1 through SD-10 | N/A | None | Any time |
