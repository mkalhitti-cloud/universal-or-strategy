# PTT-COPIER-B20-LANE-C -- Tickets
# Phase: 3 (Ticket Generation)
# Author: ptt-architect
# Plan source: docs/brain/PTT-COPIER-B20-LANE-C/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/PTT-COPIER-B20-LANE-C/02-plan-review.md (REVIEW_PASS 2026-07-14)
# Date: 2026-07-14

---

## T3 — Account Display Fix + Cross-Surface Toggle Sync

### Spec Requirements Satisfied

| Req ID | Description |
|--------|-------------|
| DW-B17-ACCOUNT-NAME-01 | Strip `!<suffix>` from account names at display layer in Panel (`FollowerItem.ToString`) and Window (`AccountDisplayConverter` + `DataTemplate` on leader `ComboBox` and follower `ListBox` in both `BuildRuleRow` and `BuildDynamicRuleRow`). Raw `Account.Name` must not change. |
| DW-B20-LANE-A-DEFER-01 | Wire `CopyEnabledChanged` subscribers in `TradeCopierPanel` and `TradeCopierWindow` so that toggling copy in one surface syncs the other. |

**Upstream dependency (already CLOSED in B20-LANE-A T2)**: `CopyEnabledChanged` event
declared in `CopyEngine.cs` at line 130 and fired at line 240. No change to `CopyEngine.cs`
or `CopyEngineTests.cs` in this ticket.

---

### File Scope

| File | Path (Wave workspace) |
|------|-----------------------|
| `TradeCopierPanel.cs` | `src/PropTraderTools/TradeCopierPanel.cs` |
| `TradeCopierWindow.cs` | `src/PropTraderTools/TradeCopierWindow.cs` |

**Files NOT modified**: `CopyEngine.cs`, `CopyEngineTests.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`.

---

### Method Signatures — All New or Modified Methods

#### TradeCopierPanel.cs

| Type | Signature | CYC | Change kind |
|------|-----------|-----|-------------|
| `private void` | `OnCopyEnabledChanged(bool enabled)` | 2 | NEW |
| `public override string` | `FollowerItem.ToString()` | 1 | MODIFIED |

#### TradeCopierWindow.cs

| Type | Signature | CYC | Change kind |
|------|-----------|-----|-------------|
| `private void` | `OnCopyEnabledChanged(bool enabled)` | 1 | NEW |
| `private static DataTemplate` | `BuildAccountDisplayTemplate()` | 1 | NEW |
| `public object` | `AccountDisplayConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)` | 1 | NEW (nested class) |
| `public object` | `AccountDisplayConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)` | 1 | NEW (nested class, interface stub) |

**Existing methods receiving line-level insertions** (no signature change):

| Method | File | Change |
|--------|------|--------|
| `OnLoaded` | `TradeCopierPanel.cs` | Add `+= OnCopyEnabledChanged` at end of body |
| `Detach()` | `TradeCopierPanel.cs` | Add `-= OnCopyEnabledChanged` after `DisarmTrailBe` |
| `OnLoaded` | `TradeCopierWindow.cs` | Add `+= OnCopyEnabledChanged` inside second `try` block |
| `OnWindowClosed` | `TradeCopierWindow.cs` | Add `-= OnCopyEnabledChanged` after `PositionStateChanged -=` |
| `BuildRuleRow` | `TradeCopierWindow.cs` | Set `ItemTemplate` on `leaderCb` and `followerLb` |
| `BuildDynamicRuleRow` | `TradeCopierWindow.cs` | Set `ItemTemplate` on `leaderCb` and `followerLb` |

---

### JS Rule Constraints

| Rule | Constraint | Evidence in this ticket |
|------|------------|------------------------|
| **JS-021** | No `lock()` anywhere | No `lock` keyword introduced. `_copyEnabled` is a `bool` accessed only on the UI thread. `Dispatcher.InvokeAsync` dispatches without blocking. |
| **JS-033** | No `async void` (non-event-handler) | Both `OnCopyEnabledChanged` methods are `private void`. No async state machine. |
| **NT8-003** | No `volatile double/int` | No `volatile` fields introduced. `_copyEnabled` is a plain `bool`. |
| **JS-001** | No `throw` in hot path | `ConvertBack` throws `NotImplementedException` but is unreachable at runtime (WPF never calls `ConvertBack` on a `OneWay` binding). Interface stub only, not a hot path. |
| **JS-002** | No `return null` | All methods return `""` or a valid object via null-coalescing. No bare `return null`. |

---

### xUnit Test Requirements

**No new `[Fact]` tests required for this ticket.**

Rationale (from plan §6 and plan review, REVIEW_PASS):
1. `CopyEnabledChanged` event logic was tested in B20-LANE-A (CopyEnabled test pair).
   The new subscribers (`OnCopyEnabledChanged`) assign WPF `Content`/`Background` — xUnit
   cannot instantiate WPF controls without STA + full WPF app context.
2. `FollowerItem` is `private sealed` inside `TradeCopierPanel`; inaccessible from xUnit
   without reflection, violating project "no test contortion" principle.
3. `AccountDisplayConverter.Convert` wraps `.NET Split` — testing `.NET Split` is not our logic.
4. Spec explicitly states no new tests required (UI-only string transform + event wiring).

**Expected `[Fact]` count after this ticket**: **120** (unchanged from B20-LANE-A baseline).

---

### Exact Line-Level Instructions

> **Engineering discipline**: Apply changes in the order listed. Run SCAN-05 (build) after
> every file before moving to the next file. Do NOT write or modify any `.cs` file not listed
> in the File Scope table above.

---

#### PART A — TradeCopierPanel.cs

---

##### Change A — `OnLoaded`: subscribe to `CopyEnabledChanged`

**Location**: End of `OnLoaded` method body, approximately line 437-458.

Find the block that contains:
```csharp
LoadAtmTemplates();
NotifyRiskChanged();
NotifyAtrFractionChanged();
```

Add the following as the final statement before the method's closing brace `}`:

```csharp
_engine.CopyEnabledChanged += OnCopyEnabledChanged;
```

**Insertion context** (the method body should end as):
```csharp
            LoadAtmTemplates();
            NotifyRiskChanged();
            NotifyAtrFractionChanged();
            _engine.CopyEnabledChanged += OnCopyEnabledChanged;
        }
```

---

##### Change B — `Detach()`: unsubscribe from `CopyEnabledChanged`

**Location**: Inside `Detach()`, approximately lines 398-413.

Find the line:
```csharp
_engine.DisarmTrailBe();
```

Add the following immediately after that line:
```csharp
_engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

**Insertion context** (surrounding lines should read):
```csharp
            _engine.DisarmTrailBe();
            _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

---

##### Change C — NEW private method: `OnCopyEnabledChanged`

**Location**: After the closing brace of `OnCopyToggle` method (approximately line 908).

Insert the following method in its entirety:

```csharp
        // B20-LANE-C T3 -- OnCopyEnabledChanged: syncs Panel copy state from engine event.
        // CYC=2: null guard (1) + Dispatcher.InvokeAsync UI update (2).
        // JS-021: no lock. JS-023: Dispatcher.InvokeAsync for UI thread marshaling.
        private void OnCopyEnabledChanged(bool enabled)
        {
            _copyEnabled = enabled;
            if (_copyToggleBtn2 == null) return;
            Dispatcher.InvokeAsync(() =>
            {
                _copyToggleBtn2.Content    = enabled ? "\u25CF COPY ON" : "\u25CF COPY OFF";
                _copyToggleBtn2.Background = enabled ? BrushActive : BrushInactive;
            });
        }
```

**Null guard rationale**: `_copyToggleBtn2` is set during `BuildUI`. The Panel is a
`UserControl`; `BuildUI` may not complete before `Detach()` is called on a partial-init
path. The null guard costs +1 CYC (CYC=2 total), still well within the CYC <= 8 limit,
and prevents a hard crash.

**No `_copyEnabled` inside the lambda**: `_copyEnabled` is assigned synchronously before
`InvokeAsync`. The lambda captures the `enabled` parameter (call-time value), not
`_copyEnabled`, so there is no stale-capture race condition.

---

##### Change D — `FollowerItem.ToString()`: strip `!<suffix>` at display layer

**Location**: `FollowerItem` inner class, approximately line 269.

Find:
```csharp
public override string ToString() => Account?.Name ?? "";
```

Replace with:
```csharp
// B20-LANE-C T3 -- DW-B17-ACCOUNT-NAME-01: strip !<suffix> at display layer only.
// Raw Account.Name is never modified. ?[0] guards null propagation when Account or Name
// is null. Split("!")[0] without ?[0] is UNSAFE (NullReferenceException). CYC=1.
public override string ToString() => Account?.Name?.Split('!')?[0] ?? "";
```

**Critical null-safety**: Use `?[0]` (null-conditional index), NOT `[0]`. When the
null-conditional chain `Account?.Name?.Split('!')` returns null (Account or Name is null),
a non-conditional index `[0]` on null throws `NullReferenceException`. `?[0]` propagates
null through the chain, falling through to `?? ""`.

**Behavior for names without `!`**: `"My Account".Split('!')` returns `["My Account"]`;
`?[0]` yields `"My Account"`. Backward compatible, no data loss.

---

#### PART B — TradeCopierWindow.cs

**Pre-flight**: Add the two `using` directives below at the top of `TradeCopierWindow.cs`
if not already present (verify before adding):

```csharp
using System.Globalization;
using System.Windows.Data;
```

**Verification**: The current file has exactly 7 `using` directives:
`System`, `System.Collections.Generic`, `System.Windows`, `System.Windows.Controls`,
`System.Windows.Media`, `NinjaTrader.Cbi`, `NinjaTrader.NinjaScript`.
Neither `System.Globalization` nor `System.Windows.Data` is currently imported.
Both are required by `AccountDisplayConverter` (`CultureInfo`) and `IValueConverter`/`Binding`/`BindingMode`.

---

##### Change E — `OnLoaded`: subscribe to `CopyEnabledChanged`

**Location**: Inside `OnLoaded`, inside the second `try` block (approximately lines 108-118).

Find the second `try` block. It contains engine initialization calls including
`CopyEngine.Instance.LoadRules()` (approximately line 113). Add the subscription as the
last statement before the second `try` block's closing brace `}`:

```csharp
_engine.CopyEnabledChanged += OnCopyEnabledChanged;
```

**Insertion context** (the second `try` block's tail should read):
```csharp
                CopyEngine.Instance.LoadRules();
                _engine.CopyEnabledChanged += OnCopyEnabledChanged;
            }
```

---

##### Change F — `OnWindowClosed`: unsubscribe from `CopyEnabledChanged`

**Location**: Inside `OnWindowClosed`, approximately lines 122-125.

Find the line:
```csharp
_engine.PositionStateChanged -= OnPositionStateChanged;
```

Add the following immediately after that line:
```csharp
_engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

**Insertion context** (surrounding lines should read):
```csharp
            _engine.PositionStateChanged -= OnPositionStateChanged;
            _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

---

##### Change G — NEW private method: `OnCopyEnabledChanged`

**Location**: After the closing brace of `OnGlobalToggle` method (approximately lines 573-579).

Insert the following method in its entirety:

```csharp
        // B20-LANE-C T3 -- OnCopyEnabledChanged: syncs Window copy state from engine event.
        // CYC=1: straight-line Dispatcher.InvokeAsync (constructor guarantee: _globalToggleBtn != null).
        // JS-021: no lock. JS-023: Dispatcher.InvokeAsync for UI thread marshaling.
        private void OnCopyEnabledChanged(bool enabled)
        {
            _copyEnabled = enabled;
            Dispatcher.InvokeAsync(() =>
            {
                _globalToggleBtn.Content    = enabled ? "Copy All ON" : "Copy All OFF";
                _globalToggleBtn.Background = enabled ? WBrushActive  : WBrushInactive;
            });
        }
```

**No null guard rationale**: `_globalToggleBtn` is assigned during `BuildUI` (constructor).
If `BuildUI` throws, the constructor executes `return;` before `Loaded += OnLoaded` is
registered (lines 82-91 of `TradeCopierWindow.cs`). Therefore `OnLoaded` never fires,
the `CopyEnabledChanged` subscription never occurs, and `OnCopyEnabledChanged` is never
called in the partial-construction path. `_globalToggleBtn` is guaranteed non-null at the
call site. CYC=1 (no control flow).

---

##### Change H — NEW private sealed class: `AccountDisplayConverter`

**Location**: Inside the `TradeCopierWindow` class body, after the `OnCopyEnabledChanged`
method (Change G above), before `BuildRuleRow`.

Insert the following class in its entirety:

```csharp
        // B20-LANE-C T3 -- AccountDisplayConverter: strips !<broker-suffix> for display.
        // IValueConverter.Convert: "Acct!Apex!Apex" -> "Acct". CYC=1.
        // IValueConverter.ConvertBack: one-way binding only; never called by WPF.
        private sealed class AccountDisplayConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as string)?.Split('!')?[0] ?? value?.ToString() ?? string.Empty;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException("AccountDisplayConverter is one-way only");
            }
        }
```

**`using` requirements** (must be present — see pre-flight above):
- `using System.Globalization;` — for `CultureInfo` parameter type
- `using System.Windows.Data;` — for `IValueConverter`
- `using System;` — for `NotImplementedException` (standard, always present)

**NT8 / .NET 4.8 compatibility**: `IValueConverter` is a standard WPF interface
(`System.Windows.Data`) available in .NET 4.8. NT8 AddOn context supports standard WPF.
No NT8 compiler restriction applies. `FrameworkElementFactory` + `DataTemplate` are
the established code-only WPF pattern used elsewhere in `TradeCopierWindow.cs`.

---

##### Change I — NEW private static method: `BuildAccountDisplayTemplate()`

**Location**: Inside `TradeCopierWindow`, immediately after the `AccountDisplayConverter`
class (Change H above), before `BuildRuleRow`.

Insert the following field and method in their entirety:

```csharp
        private static readonly AccountDisplayConverter _accountDisplayConverter = new AccountDisplayConverter();

        // B20-LANE-C T3 -- BuildAccountDisplayTemplate: builds the shared DataTemplate that
        // strips !<suffix> from Account.Name for display in ComboBox and ListBox items.
        // Uses FrameworkElementFactory (code-only WPF; no XAML in this codebase).
        // CYC=1: straight-line, no branches.
        // JS-021: no lock. JS-033: not async.
        private static DataTemplate BuildAccountDisplayTemplate()
        {
            var template    = new DataTemplate(typeof(Account));
            var tbFactory   = new FrameworkElementFactory(typeof(TextBlock));
            var binding     = new System.Windows.Data.Binding("Name")
            {
                Mode      = System.Windows.Data.BindingMode.OneWay,
                Converter = _accountDisplayConverter
            };
            tbFactory.SetBinding(TextBlock.TextProperty, binding);
            tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = tbFactory;
            return template;
        }
```

**Design notes**:
- `Binding("Name")` binds to `Account.Name` (the `Name` property on the NT8 `Account` type).
  `Account.All` contains `Account` objects; `Name` is the canonical property for display.
- `BindingMode.OneWay` is correct for display-only item templates. No write-back needed.
- The static readonly field `_accountDisplayConverter` is shared across calls. Converter
  is stateless; sharing is safe and avoids repeated allocation.
- `FrameworkElementFactory` + `DataTemplate` built in code is the established pattern in
  `TradeCopierWindow.cs`. No XAML files exist in this codebase.

**`using` requirements** (verify present):
- `System.Windows` — for `FrameworkElementFactory`, `DataTemplate`, `VerticalAlignment`
- `System.Windows.Controls` — for `TextBlock`
- `System.Windows.Data` — for `Binding`, `BindingMode`
- `NinjaTrader.Cbi` — for `Account` (already present)

---

##### Change J — `BuildRuleRow`: set `ItemTemplate` on `leaderCb` and `followerLb`

**Location**: Inside `BuildRuleRow()`, approximately lines 247-405.

Find the `leaderCb` ComboBox construction. It will contain:
```csharp
var leaderCb = new ComboBox { ... };
```

After the `leaderCb` construction block (and after any `ItemsSource = Account.All`
assignment), add:
```csharp
leaderCb.ItemTemplate = BuildAccountDisplayTemplate();
```

Find the `followerLb` ListBox construction. After the ListBox construction, after
`VirtualizingStackPanel.SetIsVirtualizing` and `ScrollViewer.SetVerticalScrollBarVisibility`
calls, add:
```csharp
followerLb.ItemTemplate = BuildAccountDisplayTemplate();
```

**ItemTemplate must be set after ItemsSource** to respect WPF control initialization order.

---

##### Change K — `BuildDynamicRuleRow`: set `ItemTemplate` on `leaderCb` and `followerLb`

**Location**: Inside `BuildDynamicRuleRow()`, approximately lines 408-562.

Same pattern as Change J. Apply the identical two `ItemTemplate` assignments:

After `leaderCb` ComboBox construction (and after `ItemsSource = Account.All`):
```csharp
leaderCb.ItemTemplate = BuildAccountDisplayTemplate();
```

After `followerLb` ListBox construction (after `VirtualizingStackPanel.SetIsVirtualizing`
and `ScrollViewer.SetVerticalScrollBarVisibility`):
```csharp
followerLb.ItemTemplate = BuildAccountDisplayTemplate();
```

**Both `BuildRuleRow` and `BuildDynamicRuleRow` must be wired**: Static rule rows use
`BuildRuleRow`; dynamically added rows use `BuildDynamicRuleRow`. Both must apply the
template to ensure consistent account name display across all rule rows.

---

### 7-Scan Checklist (Engineer Contract)

Run each scan with `ctx_shell` **sequentially**. Wait for each result before proceeding.
All scans must pass before marking T3 complete.

```
SCAN-01: JS-021 — No lock()
  ctx_shell("grep -rn \"lock(\" src/PropTraderTools/")
  Expected: 0 results

SCAN-02: JS-033 — No async void
  ctx_shell("grep -rn \"async void \" src/PropTraderTools/ --include=\"*.cs\"")
  Expected: 0 results

SCAN-03: JS-002 — No new return null violations
  ctx_shell("grep -rn \"return null\" src/PropTraderTools/ --include=\"*.cs\"")
  Action: Review each hit; confirm no new violations introduced by T3 changes.
  Note: Hits in code NOT changed by T3 are pre-existing and out of scope.

SCAN-04: NT8-003 — No new volatile fields
  ctx_shell("grep -rn \"volatile\" src/PropTraderTools/ --include=\"*.cs\"")
  Expected: 0 new volatile double/int fields introduced by T3.

SCAN-05: Build — Zero errors
  ctx_shell("dotnet build")   (run from Wave workspace root)
  Expected: 0 errors, 0 new warnings.
  Note: The two missing `using` directives (System.Globalization, System.Windows.Data)
  in TradeCopierWindow.cs MUST be added (pre-flight step) before this scan passes.

SCAN-06: Tests — All pass
  ctx_shell("dotnet test")   (run from Wave workspace root)
  Expected: 120 [Fact] pass, 0 fail.
  Note: T3 adds 0 new [Fact] tests; count must be exactly 120.

SCAN-07: CYC — No new CYC > 8
  ctx_shell("python scripts/complexity_audit.py")   (or equivalent)
  Expected: 0 new methods with CYC > 8.
  Reference CYC table:
    OnCopyEnabledChanged (Panel)          CYC=2
    FollowerItem.ToString (Panel)         CYC=1
    OnCopyEnabledChanged (Window)         CYC=1
    AccountDisplayConverter.Convert       CYC=1
    AccountDisplayConverter.ConvertBack   CYC=1
    BuildAccountDisplayTemplate           CYC=1
```

---

### Cross-Surface Toggle Data Flow (Reference)

```
[Panel: user clicks COPY ON]
  --> OnCopyToggle (Panel, UI thread)
      _copyEnabled = true
      _engine.SetEnabled(true)
          CopyEnabledChanged?.Invoke(true)
              --> Panel.OnCopyEnabledChanged(true)   [redundant, idempotent]
              --> Window.OnCopyEnabledChanged(true)  [*** NEW SYNC ***]
                  _copyEnabled = true
                  Dispatcher.InvokeAsync: _globalToggleBtn -> "Copy All ON"

[Window: user clicks "Copy All ON"]
  --> OnGlobalToggle (Window, UI thread)
      _copyEnabled = true
      _engine.SetEnabled(true)
          CopyEnabledChanged?.Invoke(true)
              --> Panel.OnCopyEnabledChanged(true)   [*** NEW SYNC ***]
                  _copyEnabled = true
                  Dispatcher.InvokeAsync: _copyToggleBtn2 -> "COPY ON"
              --> Window.OnCopyEnabledChanged(true)  [redundant, idempotent]
```

The redundant self-callback is harmless: bool assignment is idempotent, and the
`Dispatcher.InvokeAsync` queues a no-op UI update (same content/background already set).

---

### Subscribe/Unsubscribe Symmetry (Reference)

| Surface | Method | Subscribe | Unsubscribe |
|---------|--------|-----------|-------------|
| `TradeCopierPanel` | `OnCopyEnabledChanged` | `OnLoaded` (Change A) | `Detach()` (Change B) |
| `TradeCopierWindow` | `OnCopyEnabledChanged` | `OnLoaded` (Change E) | `OnWindowClosed` (Change F) |

---

### Completion Criteria

T3 is complete when ALL of the following are true:

- [ ] Change A — `TradeCopierPanel.OnLoaded`: `+= OnCopyEnabledChanged` added at end of method
- [ ] Change B — `TradeCopierPanel.Detach()`: `-= OnCopyEnabledChanged` added after `DisarmTrailBe`
- [ ] Change C — `TradeCopierPanel.OnCopyEnabledChanged(bool)`: new method added (CYC=2)
- [ ] Change D — `TradeCopierPanel.FollowerItem.ToString()`: modified to use `?[0]` (CYC=1)
- [ ] Pre-flight — `TradeCopierWindow.cs`: `using System.Globalization;` and `using System.Windows.Data;` added
- [ ] Change E — `TradeCopierWindow.OnLoaded`: `+= OnCopyEnabledChanged` added inside second `try` block
- [ ] Change F — `TradeCopierWindow.OnWindowClosed`: `-= OnCopyEnabledChanged` added after `PositionStateChanged -=`
- [ ] Change G — `TradeCopierWindow.OnCopyEnabledChanged(bool)`: new method added (CYC=1)
- [ ] Change H — `TradeCopierWindow.AccountDisplayConverter`: new `private sealed class` added
- [ ] Change I — `TradeCopierWindow.BuildAccountDisplayTemplate()`: new static method + static field added
- [ ] Change J — `TradeCopierWindow.BuildRuleRow`: `leaderCb.ItemTemplate` and `followerLb.ItemTemplate` assigned
- [ ] Change K — `TradeCopierWindow.BuildDynamicRuleRow`: `leaderCb.ItemTemplate` and `followerLb.ItemTemplate` assigned
- [ ] SCAN-01: 0 `lock(` in `src/PropTraderTools/`
- [ ] SCAN-02: 0 `async void` in `src/PropTraderTools/`
- [ ] SCAN-03: No new `return null` violations
- [ ] SCAN-04: No new `volatile` fields
- [ ] SCAN-05: `dotnet build` — 0 errors
- [ ] SCAN-06: `dotnet test` — 120 [Fact] pass
- [ ] SCAN-07: `complexity_audit.py` — 0 new CYC > 8

---

**TICKETS_COMPLETE**
