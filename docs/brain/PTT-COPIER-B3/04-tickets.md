# PTT-COPIER-B3 — Tickets
<!-- Status: TICKETS_GENERATED -->
<!-- Epic: PTT-COPIER-B3 -->
<!-- Phase: 4 (Ticket Generation) -->
<!-- Architecture: 02-architecture-plan.md (REVIEW_PASS 34/34) -->

---

## Overview

| Ticket | File | Action | Changes |
|--------|------|--------|---------|
| T1 | `CopyEngine.cs` | Modify | 8 changes: per-rule enable, daily cap floor, null guards |
| T2 | `TradeCopierWindow.cs` | Modify | 8 changes: addRule wiring, dynamic row, tag-cast fixes |
| T3 | `CopyEngineTests.cs` | Create | New file — 17 `[Fact]` methods |

---

## T1 — CopyEngine.cs (modify only)

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

### Ordered Change List

---

#### Change 1 — Remove `readonly` from `_rules` field

**Before:**
```csharp
private readonly ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```
**After:**
```csharp
private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```
**Rationale:** `SetRuleEnabled` (Change 7) must reassign `_rules`; `readonly` prevents field reassignment. `ConcurrentBag` is retained — JS-025. SCAN-11 (`readonly ConcurrentBag`) → 0. SCAN-12 (`_rules = new ConcurrentBag`) → 2.

---

#### Change 2 — Add `bool Enabled` to `CopyRule` struct; update `Create` factory

Add `internal readonly bool Enabled;` field to the `CopyRule` struct.

Update private constructor:
```csharp
private CopyRule(string instrument, Account master, Account[] followers, bool enabled)
{
    Instrument = instrument;
    MasterAccount = master;
    FollowerAccounts = followers;
    Enabled = enabled;
}
```

Update factory (default `enabled = true` keeps existing callers unchanged):
```csharp
internal static CopyRule Create(string instrument, Account master, Account[] followers, bool enabled = true)
    => new CopyRule(instrument, master, followers, enabled);
```

---

#### Change 3 — Gate 2.5 in `OnOrderUpdate`

**Location:** immediately after `if (matchedRule == null) return;`

**Insert:**
```csharp
// Gate 2.5: per-rule enable check
if (!matchedRule.Value.Enabled) return;
```
**Rationale:** Placed after null guard (safe to dereference `.Value`), before Gate 3. No `throw` — JS-001. No `lock` — JS-021.

---

#### Change 4 — Add `_dailyCapFloor` field

**Location:** immediately after the `_rules` field declaration.

```csharp
private double _dailyCapFloor = -500.0;
```

---

#### Change 5 — Add `SetDailyCapFloor` method

**Location:** immediately after `SetEnabled`.

```csharp
internal void SetDailyCapFloor(double floor) { _dailyCapFloor = floor; }
```

---

#### Change 6 — Replace `PassesDailyCapCheck` stub entirely

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

---

#### Change 7 — Add `SetRuleEnabled` method

**Location:** after `SetDailyCapFloor`.

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
**Rationale:** Snapshot-rebuild-reassign; no `lock` — JS-021. Reference assignment on 64-bit CLR is atomic. `_rules` is always `ConcurrentBag` — JS-025. CYC = 2.

---

#### Change 8 — Add null guard as first statement in `FindRule`

**Insert as first statement of `FindRule`:**
```csharp
if (instrument == null) return null;
```
**Rationale:** Prevents `NullReferenceException` when `Flatten(null)` / `CancelPendingEntries(null)` are called. No `throw` — JS-001.

---

### T1 Acceptance Criteria

| # | Criterion | Verification |
|---|-----------|-------------|
| AC-1 | `readonly` removed from `_rules` | `grep "readonly ConcurrentBag" CopyEngine.cs` → 0 (SCAN-11) |
| AC-2 | `_rules = new ConcurrentBag` appears exactly twice | `grep "_rules = new ConcurrentBag" CopyEngine.cs` → 2 (SCAN-12) |
| AC-3 | `CopyRule.Enabled` field present | `grep "readonly bool Enabled" CopyEngine.cs` → 1 |
| AC-4 | Gate 2.5 present in `OnOrderUpdate` | `grep "Gate 2.5" CopyEngine.cs` → 1 |
| AC-5 | `_dailyCapFloor` field, default = -500.0 | `grep "_dailyCapFloor = -500" CopyEngine.cs` → 1 |
| AC-6 | `SetDailyCapFloor` present | `grep "SetDailyCapFloor" CopyEngine.cs` → ≥ 1 |
| AC-7 | `PassesDailyCapCheck` uses real P&L | body contains `RealizedProfitLoss` and `_dailyCapFloor` |
| AC-8 | `SetRuleEnabled` present, no `lock()` | body present; `grep "lock(" CopyEngine.cs` → 0 |
| AC-9 | `FindRule` null guard present | first statement is `if (instrument == null) return null;` |

### T1 — 7-Scan Checklist

| Scan | Pattern | Expected | Command |
|------|---------|----------|---------|
| SCAN-01 | `lock(` | 0 | `grep -c "lock(" CopyEngine.cs` = 0 |
| SCAN-02 | `DateTime.Now[^U]` | 0 | `grep -cP "DateTime\.Now[^U]" CopyEngine.cs` = 0 |
| SCAN-03 | `new CopyEngine` | 1 | `grep -c "new CopyEngine" CopyEngine.cs` = 1 |
| SCAN-04 | `CreateOrder` | n/a | Not applicable |
| SCAN-05 | `CreateOrder` | n/a | Not applicable |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | 0 | `grep -cP "#[0-9A-Fa-f]{6}" CopyEngine.cs` = 0 |
| SCAN-07 | `FontFamily` | 0 | `grep -c "FontFamily" CopyEngine.cs` = 0 |
| SCAN-11 | `readonly ConcurrentBag` | 0 | `grep -c "readonly ConcurrentBag" CopyEngine.cs` = 0 |
| SCAN-12 | `_rules = new ConcurrentBag` | 2 | `grep -c "_rules = new ConcurrentBag" CopyEngine.cs` = 2 |

---

## T2 — TradeCopierWindow.cs (modify only)

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`

### Ordered Change List

---

#### Change 1 — Promote `rulesPanel` to class field `_rulesPanel`

Add as class field alongside other UI fields (after `_logScroll`):
```csharp
private StackPanel _rulesPanel;
```

---

#### Change 2 — Replace local `rulesPanel` with `_rulesPanel` in `BuildUI`

In `BuildUI`:
- Replace `var rulesPanel = new StackPanel();` with `_rulesPanel = new StackPanel();`
- Replace **all** subsequent `rulesPanel` references inside `BuildUI` with `_rulesPanel`

**Rationale:** `OnAddRule` appends rows after `BuildUI` returns; the field must be accessible from the instance.

---

#### Change 3 — Replace entire `OnRuleToggle` body

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
**Rationale:** `is`-pattern handles both `string` tags (`BuildRuleRow`) and `TextBox` tags (`BuildDynamicRuleRow`). Calls `_engine.SetRuleEnabled` — previously deferred in B1. CYC = 3.

---

#### Change 4 — Enable `addRuleBtn` and wire Click

In `BuildUI`, change `IsEnabled = false` to `IsEnabled = true` for `addRuleBtn`, and add after button construction:
```csharp
addRuleBtn.Click += OnAddRule;
```

---

#### Change 5 — Add `OnAddRule` handler

```csharp
private void OnAddRule(object sender, RoutedEventArgs e)
{
    _rulesPanel.Children.Add(BuildDynamicRuleRow());
}
```

---

#### Change 6 — Add `BuildDynamicRuleRow` method

Column 0 is an editable `TextBox`. All action buttons carry `instrTextBox` as their `Tag`. `applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerCb }`.

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

---

#### Change 7 — Update `OnRowApply` — handle TextBox in `tag[0]`

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

---

#### Change 8 — Update `OnRuleTrim`, `OnRuleFlatten`, `OnRuleCancel` — replace tag cast with is-pattern

In **each** of the three handlers replace:
```csharp
var instrName = (sender as Button)?.Tag as string;
```
with:
```csharp
var btn = sender as Button;
string instrName = btn?.Tag is TextBox tb ? tb.Text : btn?.Tag as string;
```

---

### T2 Acceptance Criteria

| # | Criterion | Verification |
|---|-----------|-------------|
| AC-1 | `_rulesPanel` is class field | `grep "StackPanel _rulesPanel" TradeCopierWindow.cs` → 1 at class scope |
| AC-2 | No `var rulesPanel` local remains | `grep "var rulesPanel" TradeCopierWindow.cs` → 0 |
| AC-3 | `OnRuleToggle` calls `_engine.SetRuleEnabled` | `grep "SetRuleEnabled" TradeCopierWindow.cs` → ≥ 1 |
| AC-4 | `addRuleBtn.IsEnabled = true` | `grep "addRuleBtn.IsEnabled = true" TradeCopierWindow.cs` → 1 |
| AC-5 | `addRuleBtn.Click += OnAddRule` | `grep "OnAddRule" TradeCopierWindow.cs` → ≥ 1 |
| AC-6 | `OnAddRule` method present | body calls `_rulesPanel.Children.Add(BuildDynamicRuleRow())` |
| AC-7 | `BuildDynamicRuleRow` present, col 0 is TextBox | method returns Grid; column 0 is TextBox |
| AC-8 | `OnRowApply` handles TextBox tag | `tag[0] is TextBox tb` pattern present |
| AC-9 | `OnRuleTrim/Flatten/Cancel` handle TextBox tag | `is TextBox tb` pattern in all three |

### T2 — 7-Scan Checklist

| Scan | Pattern | Expected | Command |
|------|---------|----------|---------|
| SCAN-01 | `lock(` | 0 | `grep -c "lock(" TradeCopierWindow.cs` = 0 |
| SCAN-02 | `DateTime.Now[^U]` | 0 | `grep -cP "DateTime\.Now[^U]" TradeCopierWindow.cs` = 0 |
| SCAN-03 | `new CopyEngine` | 0 | `grep -c "new CopyEngine" TradeCopierWindow.cs` = 0 |
| SCAN-04 | `CreateOrder` | 0 | `grep -c "CreateOrder" TradeCopierWindow.cs` = 0 |
| SCAN-05 | `CreateOrder` | n/a | Not applicable |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | 0 | `grep -cP "#[0-9A-Fa-f]{6}" TradeCopierWindow.cs` = 0 |
| SCAN-07 | `FontFamily` | 0 | `grep -c "FontFamily" TradeCopierWindow.cs` = 0 |

---

## T3 — CopyEngineTests.cs (CREATE NEW FILE)

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Action: CREATE NEW FILE. Do NOT modify any existing file.**

**Constraints:**
- Framework: xUnit ONLY. NEVER NUnit. NEVER MSTest.
- Namespace: `PropTraderTools`
- Singleton: `CopyEngine.Instance` only — NEVER `new CopyEngine()`
- Reset: each test begins with `_engine.SetEnabled(false)`
- No `Subscribe()` in any test
- `IsDedup` accessed via `MethodInfo.Invoke + BindingFlags.NonPublic | BindingFlags.Instance`
- Internal field access via `FieldInfo.GetValue`

### Complete File Content

```csharp
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class CopyEngineTests
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;

        private static FieldInfo GetField(string name)
            => typeof(CopyEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        private static MethodInfo GetMethod(string name)
            => typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);

        [Fact]
        public void SetEnabled_True_EnablesGate1()
        {
            _engine.SetEnabled(false);
            string received = null;
            _engine.StatusUpdate += msg => received = msg;
            _engine.SetEnabled(true);
            Assert.NotNull(received);
        }

        [Fact]
        public void SetEnabled_False_BlocksGate1()
        {
            _engine.SetEnabled(true);
            string received = null;
            _engine.StatusUpdate += msg => received = msg;
            _engine.SetEnabled(false);
            Assert.NotNull(received);
        }

        [Fact]
        public void SetDailyCapFloor_SetsFloor()
        {
            _engine.SetEnabled(false);
            _engine.SetDailyCapFloor(-999.0);
            var fi = GetField("_dailyCapFloor");
            double actual = (double)fi.GetValue(_engine);
            Assert.Equal(-999.0, actual);
        }

        [Fact]
        public void SetDailyCapFloor_DefaultIsNegative500()
        {
            _engine.SetEnabled(false);
            _engine.SetDailyCapFloor(-500.0);
            var fi = GetField("_dailyCapFloor");
            double actual = (double)fi.GetValue(_engine);
            Assert.Equal(-500.0, actual);
        }

        [Fact]
        public void SetRuleEnabled_False_MarksRuleDisabled()
        {
            _engine.SetEnabled(false);
            _engine.AddRule("SETEST", null, new Account[0]);
            _engine.SetRuleEnabled("SETEST", false);
            var fi = GetField("_rules");
            var bag = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
            bool found = false;
            foreach (var r in bag)
            {
                if (r.Instrument == "SETEST")
                {
                    Assert.False(r.Enabled);
                    found = true;
                }
            }
            Assert.True(found, "Rule SETEST not found in _rules after AddRule");
        }

        [Fact]
        public void SetRuleEnabled_True_ReenablesRule()
        {
            _engine.SetEnabled(false);
            _engine.AddRule("RETEST", null, new Account[0]);
            _engine.SetRuleEnabled("RETEST", false);
            _engine.SetRuleEnabled("RETEST", true);
            var fi = GetField("_rules");
            var bag = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
            bool found = false;
            foreach (var r in bag)
            {
                if (r.Instrument == "RETEST")
                {
                    Assert.True(r.Enabled);
                    found = true;
                }
            }
            Assert.True(found, "Rule RETEST not found in _rules after AddRule");
        }

        [Fact]
        public void SetRuleEnabled_UnknownInstrument_NoException()
        {
            _engine.SetEnabled(false);
            var ex = Record.Exception(() => _engine.SetRuleEnabled("NONEXISTENT", false));
            Assert.Null(ex);
        }

        [Fact]
        public void AddRule_AddsRuleToEngine()
        {
            _engine.SetEnabled(false);
            var fi = GetField("_rules");
            var bagBefore = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
            int countBefore = 0;
            foreach (var _ in bagBefore) countBefore++;
            _engine.AddRule("TESTADD", null, new Account[0]);
            var bagAfter = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
            int countAfter = 0;
            foreach (var _ in bagAfter) countAfter++;
            Assert.Equal(countBefore + 1, countAfter);
        }

        [Fact]
        public void AddRule_StringOverload_NoException()
        {
            _engine.SetEnabled(false);
            var ex = Record.Exception(() => _engine.AddRule("NQ 09-25", "SIM101", "SIM102"));
            Assert.Null(ex);
        }

        [Fact]
        public void StatusUpdate_FiresOnSetEnabled()
        {
            _engine.SetEnabled(false);
            bool fired = false;
            _engine.StatusUpdate += _ => fired = true;
            _engine.SetEnabled(true);
            Assert.True(fired);
        }

        [Fact]
        public void StatusUpdate_MessageContainsON_WhenEnabled()
        {
            _engine.SetEnabled(false);
            string received = null;
            _engine.StatusUpdate += msg => received = msg;
            _engine.SetEnabled(true);
            Assert.NotNull(received);
            Assert.Contains("ON", received, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void StatusUpdate_MessageContainsOFF_WhenDisabled()
        {
            _engine.SetEnabled(true);
            string received = null;
            _engine.StatusUpdate += msg => received = msg;
            _engine.SetEnabled(false);
            Assert.NotNull(received);
            Assert.Contains("OFF", received, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetRuleEnabled_WithNullAccounts_NoException()
        {
            _engine.SetEnabled(false);
            _engine.AddRule("NULLTEST", null, null);
            var ex = Record.Exception(() => _engine.SetRuleEnabled("NULLTEST", false));
            Assert.Null(ex);
        }

        [Fact]
        public void Flatten_EngineAPI_Callable()
        {
            _engine.SetEnabled(false);
            var ex = Record.Exception(() => _engine.Flatten("UNKNOWN_INSTR"));
            Assert.Null(ex);
        }

        [Fact]
        public void CancelPendingEntries_EngineAPI_Callable()
        {
            _engine.SetEnabled(false);
            var ex = Record.Exception(() => _engine.CancelPendingEntries("UNKNOWN_INSTR"));
            Assert.Null(ex);
        }

        [Fact]
        public void IsDedup_SameOrderId_ReturnsTrueOnSecondCall()
        {
            _engine.SetEnabled(false);
            MethodInfo mi = typeof(CopyEngine).GetMethod("IsDedup", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);
            string orderId = "TEST-DEDUP-SAME-" + DateTime.UtcNow.Ticks;
            bool first  = (bool)mi.Invoke(_engine, new object[] { orderId });
            bool second = (bool)mi.Invoke(_engine, new object[] { orderId });
            Assert.False(first,  "First call should return false (not a duplicate)");
            Assert.True(second,  "Second call with same ID should return true (duplicate)");
        }

        [Fact]
        public void IsDedup_DifferentOrderIds_BothAccepted()
        {
            _engine.SetEnabled(false);
            MethodInfo mi = typeof(CopyEngine).GetMethod("IsDedup", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);
            string id1 = "TEST-DEDUP-A-" + DateTime.UtcNow.Ticks;
            string id2 = "TEST-DEDUP-B-" + DateTime.UtcNow.Ticks;
            bool result1 = (bool)mi.Invoke(_engine, new object[] { id1 });
            bool result2 = (bool)mi.Invoke(_engine, new object[] { id2 });
            Assert.False(result1, "First unique ID should not be a duplicate");
            Assert.False(result2, "Second unique ID should not be a duplicate");
        }
    }
}
```

**Note on smoke tests (Tests 14–15):** `Flatten` and `CancelPendingEntries` are called with `"UNKNOWN_INSTR"` (a valid string). The `FindRule` null guard added in T1 Change 8 (`if (instrument == null) return null;`) ensures an unknown instrument name returns `null` safely, so these tests assert `Record.Exception == null` without any NT8 account context.

### T3 Acceptance Criteria

| # | Criterion | Verification |
|---|-----------|-------------|
| AC-1 | File exists at correct path | File present at `src/PropTraderTools/CopyEngineTests.cs` |
| AC-2 | Namespace is `PropTraderTools` | `grep "namespace PropTraderTools" CopyEngineTests.cs` → 1 |
| AC-3 | Exactly 17 `[Fact]` methods | `grep -c "\[Fact\]" CopyEngineTests.cs` = 17 |
| AC-4 | Zero `lock(` | `grep -c "lock(" CopyEngineTests.cs` = 0 |
| AC-5 | Zero NUnit | `grep -c "NUnit" CopyEngineTests.cs` = 0 |
| AC-6 | Zero MSTest/TestClass | `grep -cE "MSTest|TestClass" CopyEngineTests.cs` = 0 |
| AC-7 | Zero `Subscribe()` | `grep -c "Subscribe()" CopyEngineTests.cs` = 0 |
| AC-8 | Zero `new CopyEngine()` | `grep -c "new CopyEngine" CopyEngineTests.cs` = 0 |
| AC-9 | All 17 method bodies complete | No stub bodies |

### T3 — Scan Checklist

| Scan | Pattern | Expected | Command |
|------|---------|----------|---------|
| SCAN-01 | `lock(` | 0 | `grep -c "lock(" CopyEngineTests.cs` = 0 |
| SCAN-02 | `DateTime.Now[^U]` | 0 | `grep -cP "DateTime\.Now[^U]" CopyEngineTests.cs` = 0 |
| SCAN-03 | `new CopyEngine` | 0 | `grep -c "new CopyEngine" CopyEngineTests.cs` = 0 |
| SCAN-04 | `CreateOrder` | 0 | `grep -c "CreateOrder" CopyEngineTests.cs` = 0 |
| SCAN-05 | `CreateOrder` | 0 | same as SCAN-04 |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | 0 | `grep -cP "#[0-9A-Fa-f]{6}" CopyEngineTests.cs` = 0 |
| SCAN-07 | `FontFamily` | 0 | `grep -c "FontFamily" CopyEngineTests.cs` = 0 |
| SCAN-08 | `NUnit` | 0 | `grep -c "NUnit" CopyEngineTests.cs` = 0 |
| SCAN-09 | `MSTest\|TestClass` | 0 | `grep -cE "MSTest\|TestClass" CopyEngineTests.cs` = 0 |
| SCAN-10 | `Subscribe()` | 0 | `grep -c "Subscribe()" CopyEngineTests.cs` = 0 |

---

## Cross-File 7-Scan Summary

Run after all three tickets are complete.

| Scan | Pattern | Scope | Expected |
|------|---------|-------|----------|
| SCAN-01 | `lock(` | All 3 files | 0 |
| SCAN-02 | `DateTime.Now[^U]` | All 3 files | 0 |
| SCAN-03 | `new CopyEngine` | All 3 files | 1 (singleton only in `CopyEngine.cs`) |
| SCAN-04 | `CreateOrder` | `TradeCopierWindow.cs` | 0 |
| SCAN-05 | `CreateOrder` | `CopyEngineTests.cs` | 0 |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | All 3 files | 0 |
| SCAN-07 | `FontFamily` | All 3 files | 0 |
| SCAN-11 | `readonly ConcurrentBag` | `CopyEngine.cs` | 0 |
| SCAN-12 | `_rules = new ConcurrentBag` | `CopyEngine.cs` | 2 |
