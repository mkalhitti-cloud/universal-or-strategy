# PTT-COPIER-B3 Architecture Plan
<!-- Status: REVIEW_PASS -->
<!-- Author: PTT Architect -->
<!-- Date: 2026-06 -->

---

## §1 Summary

Block 3 closes three deferred items from the B1 final review and adds xUnit test coverage for
the CopyEngine singleton.

| Ticket | File | Concern |
|--------|------|---------|
| T1 | `CopyEngine.cs` | 8 engine changes: per-rule enable, daily cap floor, null guards |
| T2 | `TradeCopierWindow.cs` | 8 UI changes: addRule wiring, dynamic row, tag-cast fixes |
| T3 | `CopyEngineTests.cs` | New file — 17 `[Fact]` methods covering all engine surface |

---

## §2 Scope

**In-scope:**
- B3-T1: 8 changes to `CopyEngine.cs`
- B3-T2: 8 changes to `TradeCopierWindow.cs`
- B3-T3: New `CopyEngineTests.cs` with 17 `[Fact]` methods

**Out-of-scope:**
- Follower ComboBox multi-select
- Rule persistence across sessions
- `TradeCopierPanel.cs` changes

**Files touched:**

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Modify |
| `src/PropTraderTools/TradeCopierWindow.cs` | Modify |
| `src/PropTraderTools/CopyEngineTests.cs` | Create |

---

## §3 T1 — CopyEngine.cs Changes

### Change 3.1 — Remove `readonly` from `_rules`

**Before:**
```csharp
private readonly ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```
**After:**
```csharp
private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```
**Rationale:** `SetRuleEnabled` must reassign `_rules`; `readonly` prevents field reassignment.
`ConcurrentBag` is retained — JS-025 satisfied. SCAN-11 (`readonly ConcurrentBag`) → 0 results.
SCAN-12 (`_rules = new ConcurrentBag`) → 2 results (field init + SetRuleEnabled reassign).

---

### Change 3.2 — Add `Enabled` field to CopyRule + update Create factory

**Before:**
```csharp
private readonly struct CopyRule
{
    internal readonly string Instrument;
    internal readonly Account MasterAccount;
    internal readonly Account[] FollowerAccounts;

    private CopyRule(string instrument, Account master, Account[] followers)
    {
        Instrument = instrument;
        MasterAccount = master;
        FollowerAccounts = followers;
    }

    internal static CopyRule Create(string instrument, Account master, Account[] followers)
        => new CopyRule(instrument, master, followers);
}
```
**After:**
```csharp
private readonly struct CopyRule
{
    internal readonly string Instrument;
    internal readonly Account MasterAccount;
    internal readonly Account[] FollowerAccounts;
    internal readonly bool Enabled;

    private CopyRule(string instrument, Account master, Account[] followers, bool enabled)
    {
        Instrument = instrument;
        MasterAccount = master;
        FollowerAccounts = followers;
        Enabled = enabled;
    }

    internal static CopyRule Create(string instrument, Account master, Account[] followers, bool enabled = true)
        => new CopyRule(instrument, master, followers, enabled);
}
```
**Rationale:** `readonly struct` fields must be set in constructor. `default: true` means all
existing `AddRule` callers compile unchanged. Immutability preserved — JS-025.

---

### Change 3.3 — Gate 2.5 in OnOrderUpdate

**Location:** immediately after `if (matchedRule == null) return;`

**Insert:**
```csharp
// Gate 2.5: per-rule enable check
if (!matchedRule.Value.Enabled) return;
```
**Rationale:** Placed after null guard (safe to dereference `.Value`), before Gate 3 (exits
before any allocation). No `throw` — JS-001. No `lock` — JS-021.

---

### Change 3.4 — Add `_dailyCapFloor` field

**Location:** immediately after `_rules` field declaration.

```csharp
private double _dailyCapFloor = -500.0;
```
**Rationale:** Encapsulates default daily P&L floor of −$500. Non-volatile `double` accepted
(Deviation D1): written on UI thread only, read on order-update thread; torn read on x64 CLR
is bounded-width and not safety-critical for a floor comparison. Can be hardened in Block 4
with `Volatile.Read`/`Volatile.Write`.

---

### Change 3.5 — Add `SetDailyCapFloor` method

**Location:** immediately after `SetEnabled`.

```csharp
internal void SetDailyCapFloor(double floor) { _dailyCapFloor = floor; }
```
**Rationale:** UI thread sets the floor; single field write; no lock needed — JS-021.

---

### Change 3.6 — Replace `PassesDailyCapCheck` stub

**Before:**
```csharp
private bool PassesDailyCapCheck(Account acc)
{
    // Phase 1 stub -- full P&L check is Block 2
    return true;
}
```
**After:**
```csharp
private bool PassesDailyCapCheck(Account acc)
{
    double pnl = acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
    if (pnl == double.MinValue) return true;
    return pnl > _dailyCapFloor;
}
```
**Rationale:** NT8 `Account.Get` returns `double.MinValue` when unavailable; fail-open guard
prevents phantom blocking. No `throw` — JS-001. No `lock` — JS-021. CYC = 3.

---

### Change 3.7 — Add `SetRuleEnabled` method

```csharp
internal void SetRuleEnabled(string instrument, bool enabled)
{
    var snapshot = new List<CopyRule>(_rules);
    var newBag = new ConcurrentBag<CopyRule>();
    foreach (var r in snapshot)
    {
        var updated = r.Instrument == instrument
            ? CopyRule.Create(r.Instrument, r.MasterAccount, r.FollowerAccounts, enabled)
            : r;
        newBag.Add(updated);
    }
    _rules = newBag;
}
```
**Rationale:** `ConcurrentBag` has no in-place update API; snapshot-rebuild-reassign is the
only option. Single-writer (UI thread only). Reference assignment on 64-bit CLR is atomic —
`OnOrderUpdate` sees either old or new bag, both valid states. No `lock` — JS-021.
`_rules` is always `ConcurrentBag` — JS-025. CYC = 2.

---

### Change 3.8 — Add null guard at top of `FindRule`

**Insert as first statement:**
```csharp
if (instrument == null) return null;
```
**Rationale:** Prevents `NullReferenceException` when `Flatten(null)` /
`CancelPendingEntries(null)` are called in tests (Deviation D2). No `throw` — JS-001.

---

## §4 T2 — TradeCopierWindow.cs Changes

### Change 4.1 — Promote `rulesPanel` to class field `_rulesPanel`

**Add class field (after `_logScroll`):**
```csharp
private StackPanel _rulesPanel;
```
**In `BuildUI`:** replace `var rulesPanel = new StackPanel()` with `_rulesPanel = new StackPanel()`
and replace all `rulesPanel` references inside `BuildUI` with `_rulesPanel`.

**Rationale:** `OnAddRule` appends rows after `BuildUI` returns; the field must be accessible
from the instance.

---

### Change 4.2 — Wire `OnRuleToggle` to engine

**Replace entire `OnRuleToggle` body:**
```csharp
private void OnRuleToggle(object sender, RoutedEventArgs e)
{
    var btn = sender as Button;
    if (btn == null) return;
    string instrName = btn.Tag is TextBox tb ? tb.Text : btn.Tag as string;
    bool newState = (string)btn.Content == "[ON]" ? false : true;
    btn.Content = newState ? "[ON]" : "[OFF]";
    _engine.SetRuleEnabled(instrName, newState);
}
```
**Rationale:** `is`-pattern handles both `string` tags (`BuildRuleRow`) and `TextBox` tags
(`BuildDynamicRuleRow`). Calls `_engine.SetRuleEnabled` — previously deferred in B1. CYC = 3.

---

### Change 4.3 — Enable `addRuleBtn` and wire Click

```csharp
// Before:
IsEnabled = false,   // <-- disabled in B1
// No Click handler wired

// After:
IsEnabled = true,
// added:
addRuleBtn.Click += OnAddRule;
```

---

### Change 4.4 — Add `OnAddRule` handler

```csharp
private void OnAddRule(object sender, RoutedEventArgs e)
{
    _rulesPanel.Children.Add(BuildDynamicRuleRow());
}
```

---

### Change 4.5 — Add `BuildDynamicRuleRow` method

Identical layout to `BuildRuleRow` except column 0 is an editable `TextBox` instead of a
`TextBlock`. All action buttons set `Tag = instrTextBox` (live reference, read at click-time).
`applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb }`.

```csharp
private Grid BuildDynamicRuleRow()
{
    var grid = new Grid { Margin = new Thickness(2) };
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

    var instrTextBox = new TextBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), MinWidth = 40 };
    Grid.SetColumn(instrTextBox, 0);
    grid.Children.Add(instrTextBox);

    var leaderCb = new ComboBox { ItemsSource = Account.All, Margin = new Thickness(2) };
    leaderCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
    Grid.SetColumn(leaderCb, 1);
    grid.Children.Add(leaderCb);

    var followerCb = new ComboBox { ItemsSource = Account.All, Margin = new Thickness(2) };
    followerCb.SetResourceReference(ComboBox.StyleProperty, "AccountComboBoxStyle");
    Grid.SetColumn(followerCb, 2);
    grid.Children.Add(followerCb);

    var trimBtn = new Button { Content = "[1/2]", Tag = instrTextBox, Margin = new Thickness(2) };
    trimBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
    trimBtn.Click += OnRuleTrim;
    Grid.SetColumn(trimBtn, 3);
    grid.Children.Add(trimBtn);

    var flattenBtn = new Button { Content = "[=]", Tag = instrTextBox, Margin = new Thickness(2) };
    flattenBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
    flattenBtn.Click += OnRuleFlatten;
    Grid.SetColumn(flattenBtn, 4);
    grid.Children.Add(flattenBtn);

    var cancelBtn = new Button { Content = "[x]", Tag = instrTextBox, Margin = new Thickness(2) };
    cancelBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
    cancelBtn.Click += OnRuleCancel;
    Grid.SetColumn(cancelBtn, 5);
    grid.Children.Add(cancelBtn);

    var toggleBtn = new Button { Content = "[ON]", Tag = instrTextBox, Margin = new Thickness(2) };
    toggleBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
    toggleBtn.Click += OnRuleToggle;
    Grid.SetColumn(toggleBtn, 6);
    grid.Children.Add(toggleBtn);

    var applyBtn = new Button { Content = "Apply", Margin = new Thickness(2) };
    applyBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
    applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb };
    applyBtn.Click += OnRowApply;
    Grid.SetColumn(applyBtn, 7);
    grid.Children.Add(applyBtn);

    return grid;
}
```
**Rationale:** Dynamic rows need editable instrument names. TextBox live-reference tag pattern
avoids string capture at row-creation time. CYC = 1 (no branching).

---

### Change 4.6 — Update `OnRowApply` — handle TextBox in tag[0]

```csharp
private void OnRowApply(object sender, RoutedEventArgs e)
{
    var tag = (sender as Button)?.Tag as object[];
    if (tag == null) return;
    string instrName = tag[0] is TextBox tb ? tb.Text : tag[0] as string;
    if (string.IsNullOrEmpty(instrName)) return;
    var leaderCb = tag[1] as ComboBox;
    var followerCb = tag[2] as ComboBox;
    var leader = leaderCb?.SelectedItem as Account;
    var follower = followerCb?.SelectedItem as Account;
    if (leader == null || follower == null) return;
    _engine.AddRule(instrName, leader, new[] { follower });
}
```
**Rationale:** `is`-pattern on `tag[0]` handles both static `BuildRuleRow` (string) and
dynamic `BuildDynamicRuleRow` (TextBox). Guard returns on empty/null — JS-001. CYC = 5.

---

### Change 4.7 — Update `OnRuleTrim`, `OnRuleFlatten`, `OnRuleCancel`

In **each** of the three handlers replace:
```csharp
var instrName = (sender as Button)?.Tag as string;
```
with:
```csharp
var btn = sender as Button;
string instrName = btn?.Tag is TextBox tb ? tb.Text : btn?.Tag as string;
```
**Rationale:** Dynamic row buttons carry `TextBox` as `Tag`; `is`-pattern needed for both
static and dynamic rows. CYC = 2 per handler.

---

### Change 4.8 — Field ordering note

`private StackPanel _rulesPanel;` is added alongside other class-level UI fields, after
`_logScroll`, to maintain the existing grouping convention.

---

## §5 T3 — CopyEngineTests.cs

**Location:** `src/PropTraderTools/CopyEngineTests.cs`
**Framework:** xUnit ONLY. NEVER NUnit. NEVER MSTest.
**Namespace:** `PropTraderTools`
**Singleton access:** `CopyEngine.Instance` only — never `new CopyEngine()`
**Reset pattern:** each test begins with `_engine.SetEnabled(false)`
**Subscribe rule:** `Subscribe()` is NOT called in any test
**Private member access:** `FieldInfo` / `MethodInfo` via `BindingFlags.NonPublic | BindingFlags.Instance`

### 17 [Fact] Method Signatures

| # | Method | Group | Key assertion |
|---|--------|-------|---------------|
| 1 | `SetEnabled_True_EnablesGate1` | Gate-1 | `StatusUpdate` fires with non-null message |
| 2 | `SetEnabled_False_BlocksGate1` | Gate-1 | `StatusUpdate` fires with non-null message |
| 3 | `SetDailyCapFloor_SetsFloor` | DailyCapFloor | `_dailyCapFloor` via `FieldInfo` equals set value |
| 4 | `SetDailyCapFloor_DefaultIsNegative500` | DailyCapFloor | `_dailyCapFloor` via `FieldInfo` equals `-500.0` |
| 5 | `SetRuleEnabled_False_MarksRuleDisabled` | SetRuleEnabled | Matching rule in `_rules` bag has `Enabled == false` |
| 6 | `SetRuleEnabled_True_ReenablesRule` | SetRuleEnabled | Matching rule in `_rules` bag has `Enabled == true` |
| 7 | `SetRuleEnabled_UnknownInstrument_NoException` | SetRuleEnabled | `Record.Exception` returns `null` |
| 8 | `AddRule_AddsRuleToEngine` | AddRule | `_rules` count increments by 1 |
| 9 | `AddRule_StringOverload_NoException` | AddRule | `Record.Exception` returns `null` |
| 10 | `StatusUpdate_FiresOnSetEnabled` | StatusUpdate | `fired == true` |
| 11 | `StatusUpdate_MessageContainsON_WhenEnabled` | StatusUpdate | message contains `"ON"` |
| 12 | `StatusUpdate_MessageContainsOFF_WhenDisabled` | StatusUpdate | message contains `"OFF"` |
| 13 | `SetRuleEnabled_WithNullAccounts_NoException` | Null-safety | `Record.Exception` returns `null` |
| 14 | `Flatten_EngineAPI_Callable` | Smoke | `Record.Exception` returns `null` |
| 15 | `CancelPendingEntries_EngineAPI_Callable` | Smoke | `Record.Exception` returns `null` |
| 16 | `IsDedup_SameOrderId_ReturnsTrueOnSecondCall` | IsDedup | first=`false`, second=`true` |
| 17 | `IsDedup_DifferentOrderIds_BothAccepted` | IsDedup | both=`false` |

### Implementation Notes Per Group

**Tests 3–4** (`DailyCapFloor`):
Access `_dailyCapFloor` via:
```csharp
var fi = typeof(CopyEngine).GetField("_dailyCapFloor", BindingFlags.NonPublic | BindingFlags.Instance);
double val = (double)fi.GetValue(_engine);
```

**Tests 5–7** (`SetRuleEnabled`):
Access `_rules` via `FieldInfo`; enumerate the `ConcurrentBag<CopyRule>` to inspect `Enabled`
on the matching entry.

**Tests 8–9** (`AddRule`):
Count `_rules` before/after `AddRule` via `FieldInfo` enumeration.

**Tests 10–12** (`StatusUpdate`):
Subscribe a local `Action<string>` delegate to `_engine.StatusUpdate` before calling
`SetEnabled`.

**Test 13** (`Null-safety`):
Create a `CopyRule` with null `FollowerAccounts` (or empty array) via `AddRule("X", null, null)`.
Assert `Record.Exception` is `null` for subsequent `SetRuleEnabled("X", false)`.

**Tests 14–15** (`Smoke`):
Call `_engine.Flatten("UNKNOWN_INSTR")` and `_engine.CancelPendingEntries("UNKNOWN_INSTR")`.
`FindRule` returns `null` safely (Change 3.8 null guard). Assert `Record.Exception == null`.

**Tests 16–17** (`IsDedup`):
```csharp
MethodInfo mi = typeof(CopyEngine).GetMethod("IsDedup",
    BindingFlags.NonPublic | BindingFlags.Instance);
bool first  = (bool)mi.Invoke(_engine, new object[] { "order-1" });
bool second = (bool)mi.Invoke(_engine, new object[] { "order-1" });
// Assert.False(first); Assert.True(second);
```

---

## §6 Jane Street Compliance Table

| Rule | Description | B3 Compliance |
|------|-------------|---------------|
| JS-001 | No throw in hot path | Gate 2.5 is plain `return`. `PassesDailyCapCheck` has no `throw`. `FindRule` null guard is `return null`. `OnRowApply` uses guard returns. ✅ |
| JS-010 | Private CopyEngine constructor unchanged | `private CopyEngine() { }` untouched ✅ |
| JS-021 | ZERO `lock()` calls | `SetRuleEnabled` uses snapshot-rebuild-reassign, no lock. `SetDailyCapFloor` is a single field write. Zero `lock` in test file. ✅ |
| JS-023 | `volatile bool _isCopyEnabled` unchanged | Field declaration unchanged ✅ |
| JS-025 | ConcurrentBag maintained — never replaced with List | `_rules` is always `ConcurrentBag`. `SetRuleEnabled` rebuilds into `new ConcurrentBag<CopyRule>()`, never a `List`. ✅ |

---

## §7 SCAN-01..12 Assertions

| ID | Pattern | Target scope | Expected result |
|----|---------|--------------|-----------------|
| SCAN-01 | `lock(` | All 3 `.cs` files + `CopyEngineTests.cs` | 0 results |
| SCAN-02 | `DateTime.Now[^U]` | All 4 `.cs` files | 0 results |
| SCAN-03 | `new CopyEngine` | All 4 `.cs` files | 1 result — `CopyEngine.cs` singleton field only |
| SCAN-04 | `CreateOrder` in `TradeCopierWindow.cs` | `TradeCopierWindow.cs` | 0 results |
| SCAN-05 | `CreateOrder` in `CopyEngineTests.cs` | `CopyEngineTests.cs` | 0 results |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | All 4 `.cs` files | 0 results |
| SCAN-07 | `FontFamily` | All 4 `.cs` files | 0 results |
| SCAN-08 | `NUnit` | `CopyEngineTests.cs` | 0 results |
| SCAN-09 | `MSTest` or `TestClass` | `CopyEngineTests.cs` | 0 results |
| SCAN-10 | `Subscribe()` | `CopyEngineTests.cs` | 0 results |
| SCAN-11 | `readonly ConcurrentBag` | `CopyEngine.cs` | 0 results |
| SCAN-12 | `_rules = new ConcurrentBag` | `CopyEngine.cs` | 2 results |

---

## §8 Accepted Deviations

| # | Item | Justification |
|---|------|---------------|
| D1 | `_dailyCapFloor` is non-volatile `double` | Written only on UI thread; read on order-update thread. Torn `double` read on x64 CLR is bounded-width and not safety-critical for a P&L floor comparison. Can be hardened in Block 4 with `Volatile.Read`/`Volatile.Write`. |
| D2 | Null instrument in smoke tests | Tests 14–15 call `Flatten`/`CancelPendingEntries` with a valid but unknown instrument name string (`"UNKNOWN_INSTR"`). `FindRule` returns `null` safely via the null guard added in Change 3.8. |
| D3 | `TradeCopierPanel.cs` not modified | No per-rule toggle UI in panel. Panel's `OnToggle` calls `SetEnabled` (global). No panel-side wiring needed. |
| D4 | `CopyEngineTests.cs` in same assembly | xUnit supports same-assembly tests. `InternalsVisibleTo` not needed. |

---

## §9 Block 4 Backlog

| Priority | Item | File | Notes |
|----------|------|------|-------|
| P1 | Follower multi-select | `TradeCopierWindow.cs`, `TradeCopierPanel.cs` | Replace single `ComboBox` with `ListBox` + `SelectionMode.Multiple` |
| P2 | Rule persistence across sessions | `CopyEngine.cs`, new settings file | Serialize `_rules` bag to NT8 `UserDataDir` |
| P3 | `Volatile.Read`/`Write` for `_dailyCapFloor` | `CopyEngine.cs` | Harden D1 |
| P3 | `TradeCopierPanel` per-rule toggle | `TradeCopierPanel.cs` | Add if use-case demands chart-level rule pausing |
| P4 | Integration tests with NT8 sim accounts | New test file | B3 tests are unit-only |
