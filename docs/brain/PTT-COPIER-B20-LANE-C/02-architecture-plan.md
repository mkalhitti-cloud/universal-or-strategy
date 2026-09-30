# PTT-COPIER-B20-LANE-C -- Architecture Plan
# Phase: 1 (Architecture)
# Ticket scope: T3 (DW-B17-ACCOUNT-NAME-01 + DW-B20-LANE-A-DEFER-01)
# Author: ptt-architect
# Review status: PENDING (re-submitted after V-01 resolution)
# Date: 2026-07-14

---

## §1 Spec Requirements Addressed

| Req ID | Description | Source |
|--------|-------------|--------|
| DW-B20-LANE-A-DEFER-01 | Wire `CopyEnabledChanged` subscribers in `TradeCopierPanel` and `TradeCopierWindow` so that toggling copy in one surface syncs the other. | B20-LANE-A backlog §3, Decision 4 |
| DW-B17-ACCOUNT-NAME-01 (Panel) | Strip `!<suffix>` from account names at the display layer in Panel's `FollowerItem.ToString()`. Raw `Account.Name` must not change. | B17 deferred item, spec line 2547 |
| DW-B17-ACCOUNT-NAME-01 (Window) | Strip `!<suffix>` from account names in the Window's leader `ComboBox` and follower `ListBox` item template via `AccountDisplayConverter` (`IValueConverter`). Raw `Account.Name` must not change. | spec line 2547-2548 — "Window's account ComboBox item template" |

All items were explicitly carried forward as OPEN in the B20-LANE-A deferred backlog
(Section 4, Full Open Items Ledger). DW-B17-SYNC-01 (event declaration and fire site in
CopyEngine) was CLOSED in B20-LANE-A T2 and is the upstream dependency for DEFER-01.

**V-01 resolution (re-submission)**: Plan previously omitted the Window account display
surface. This revision adds §3.3 (Window Part C) which implements `AccountDisplayConverter`
(`IValueConverter`) and applies it via `DataTemplate` on the leader `ComboBox` and follower
`ListBox` in both `BuildRuleRow` and `BuildDynamicRuleRow`. See Decision D-09.

---

## §2 Files Modified

| File | Location | Change count |
|------|----------|-------------|
| `TradeCopierPanel.cs` | `src/PropTraderTools/TradeCopierPanel.cs` | 4 changes (OnLoaded, Detach, new method, FollowerItem.ToString) |
| `TradeCopierWindow.cs` | `src/PropTraderTools/TradeCopierWindow.cs` | 6 changes (OnLoaded, OnWindowClosed, new OnCopyEnabledChanged, new AccountDisplayConverter class, new BuildAccountDisplayTemplate method, ItemTemplate wiring in BuildRuleRow + BuildDynamicRuleRow) |

**Files NOT modified** (explicitly confirmed):
- `CopyEngine.cs` — event already declared at line 130, already fired at line 240. No change.
- `CopyEngineTests.cs` — no new tests required (see §6).
- `TradeCopierAddOn.cs` — unrelated.
- `AtrSizingEngine.cs` — unrelated.

---

## §3 Changes Per File

### 3.1 TradeCopierPanel.cs

#### Change A — OnLoaded: subscribe to CopyEnabledChanged
**Location**: End of `OnLoaded` body (after `NotifyAtrFractionChanged()` at line ~457).

```
_engine.CopyEnabledChanged += OnCopyEnabledChanged;
```

This is added as the final statement before the method's closing brace. It follows the
existing subscription pattern at lines 440-441 (`PositionStateChanged`, `PendingBeFired`).

---

#### Change B — Detach: unsubscribe from CopyEnabledChanged
**Location**: After `_engine.PendingBeFired -= OnPendingBeFiredDispatch;` at line ~405.

```
_engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

This mirrors the unsubscription pattern at lines 403-405. Placement after PendingBeFired
unsubscription maintains event cleanup grouping.

---

#### Change C — NEW private method: OnCopyEnabledChanged
**Location**: After `OnCopyToggle` method (currently line ~908).

Signature:
```csharp
private void OnCopyEnabledChanged(bool enabled)
```

Full body:
```csharp
// B20-LANE-C: DW-B20-LANE-A-DEFER-01 -- cross-surface copy toggle sync.
// CYC=2: null guard (1) + base path (1). Lambda excluded from CYC count.
// JS-021: no lock. JS-033: not async. Dispatcher.InvokeAsync required (CopyEngine
// is not WPF-aware; future callers may fire on non-UI thread).
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
`UserControl` whose `BuildUI` may not complete before `Detach()` is called (partial
initialization path is theoretically possible). The null guard costs 1 CYC but prevents
a hard crash in edge cases.

**Dispatcher.InvokeAsync rationale**: `CopyEnabledChanged` fires from `CopyEngine.SetEnabled`,
which is not WPF-aware. Current callers (`OnCopyToggle`, `OnGlobalToggle`) are on the UI
thread, but CopyEngine imposes no such requirement. Consistent use of `Dispatcher.InvokeAsync`
future-proofs this callback at zero cost.

**No `_copyEnabled` update inside the lambda**: `_copyEnabled` is a value type (`bool`) on the
UI thread. Updating it synchronously before the `InvokeAsync` dispatch is correct. The lambda
captures `enabled` (value from event argument), not `_copyEnabled`, so there is no race.

---

#### Change D — FollowerItem.ToString: strip `!<suffix>` at display layer
**Location**: Line 269, `FollowerItem` inner class.

Current:
```csharp
public override string ToString() => Account?.Name ?? "";
```

New:
```csharp
// B20-LANE-C: DW-B17-ACCOUNT-NAME-01 -- strip !<suffix> at display layer only.
// Raw Account.Name is never modified. Split returns at least [0]; ?[0] guards
// against null chain propagation when Account or Name is null. CYC=1.
public override string ToString() => Account?.Name?.Split('!')?[0] ?? "";
```

**Critical null-safety note**: The spec-supplied form `Account?.Name?.Split('!')[0]` is
**unsafe**. When `Account` is null or `Account.Name` is null, the null-conditional chain
(`?.`) short-circuits to null before `Split`. A non-null-conditional index `[0]` on a null
reference throws `NullReferenceException`. The correct form is `?[0]` (null-conditional index).

**Behavior for account names without `!`**:
`"My Account".Split('!')` returns `["My Account"]`; `?[0]` returns `"My Account"`.
No data loss, backward compatible.

**Scope note — Window account controls addressed in §3.3 (Part C)**:
The Window's leader `ComboBox` and follower `ListBox` bind to `Account.All` directly
via `ItemsSource` (lines 99-101 of `TradeCopierWindow.cs`). These use NT8's native
`Account.ToString()` which is not authored by this codebase. We cannot override
`Account.ToString()` (NT8 sealed type). However, a `DataTemplate` + `IValueConverter`
approach permits display-layer name stripping without touching `Account.ToString()`.
**Conclusion**: Window display fix is implemented via `AccountDisplayConverter` — see §3.3.

---

### 3.3 TradeCopierWindow.cs — Part C: Window Account Display Fix

This section addresses **DW-B17-ACCOUNT-NAME-01 (Window)** per spec line 2547-2548.

#### Change H — NEW private sealed class: AccountDisplayConverter

**Location**: Inside `TradeCopierWindow` class body, after the `OnCopyEnabledChanged` method
(i.e., after Change G at line ~579+).

**Rationale for placement**: Private nested classes are grouped at the bottom of the class body
in this codebase (consistent with `FollowerItem` in Panel). Being `private sealed` restricts
its scope to `TradeCopierWindow` only.

Signature:
```csharp
private sealed class AccountDisplayConverter : IValueConverter
```

Full body:
```csharp
// B20-LANE-C: DW-B17-ACCOUNT-NAME-01 -- strip !<suffix> at display layer only.
// Implements IValueConverter for use in DataTemplate on leader ComboBox and
// follower ListBox in BuildRuleRow and BuildDynamicRuleRow.
// CYC=1: no branches. ConvertBack CYC=1 (single throw, unreachable in practice).
// JS-021: no lock. NT8 constraint: IValueConverter is standard WPF (.NET 4.8).
private sealed class AccountDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string)?.Split('!')?[0] ?? value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

**Null safety**:
- `value as string` returns null if value is not a string (safe cast, no exception).
- `?.Split('!')?[0]` — null-conditional chain; if the string is null, result is null.
- `?? value?.ToString() ?? ""` — fallback to raw object ToString, then empty string.
- `ConvertBack` is a one-way binding; it is never called by WPF for `ItemTemplate` display.
  The `NotImplementedException` satisfies the interface contract and documents intent.

**`using` requirements** (must be present at file top):
- `System.Globalization` (for `CultureInfo`) — verify present; add if absent.
- `System.Windows.Data` (for `IValueConverter`) — verify present; add if absent.
- `System` (for `NotImplementedException`) — standard, always present.

**NT8 constraint check**: `IValueConverter` is part of `System.Windows.Data` (WPF, .NET 4.8).
This is a standard WPF interface fully available in the NT8 AddOn context. No special NT8
restriction applies. `CultureInfo` is part of `System.Globalization`, also standard.

---

#### Change I — NEW private static method: BuildAccountDisplayTemplate

**Location**: After `AccountDisplayConverter` class, inside `TradeCopierWindow`.

Signature:
```csharp
private static DataTemplate BuildAccountDisplayTemplate()
```

Full body:
```csharp
// B20-LANE-C: DW-B17-ACCOUNT-NAME-01 -- builds the shared DataTemplate that
// strips !<suffix> from Account.Name for display in ComboBox and ListBox items.
// Uses FrameworkElementFactory (code-only WPF; no XAML in this codebase).
// CYC=1: straight-line, no branches.
// JS-021: no lock. JS-033: not async.
private static DataTemplate BuildAccountDisplayTemplate()
{
    var converter = new AccountDisplayConverter();
    var factory = new FrameworkElementFactory(typeof(TextBlock));
    var binding = new Binding("Name")
    {
        Converter = converter,
        Mode = BindingMode.OneWay
    };
    factory.SetBinding(TextBlock.TextProperty, binding);
    var template = new DataTemplate();
    template.VisualTree = factory;
    return template;
}
```

**Design notes**:
- `Binding("Name")` binds to `Account.Name` (the `Name` property on the NT8 `Account` type).
  This is the canonical property name; `Account.All` contains `Account` objects with a `Name` property.
- `BindingMode.OneWay` is the correct mode for display-only item templates. No write-back needed.
- `AccountDisplayConverter` is instantiated per-call. This is called once per `BuildRuleRow`
  invocation, which is infrequent (UI construction only). No performance concern.
- `FrameworkElementFactory` is the correct code-only WPF pattern for `DataTemplate` construction
  when XAML is not used. This is the established pattern in `TradeCopierWindow.cs`.

**`using` requirements**:
- `System.Windows` (for `FrameworkElementFactory`, `DataTemplate`) — verify present; add if absent.
- `System.Windows.Controls` (for `TextBlock`) — verify present; add if absent.
- `System.Windows.Data` (for `Binding`, `BindingMode`) — verify present; add if absent.

---

#### Change J — BuildRuleRow: set ItemTemplate on leaderCb and followerLb

**Location**: Inside `BuildRuleRow()`, after `leaderCb` and `followerLb` are constructed
and before the method returns its result.

**Exact insertion point**: After `leaderCb.ItemsSource = Account.All;` (line ~99) and after
`followerLb.ItemsSource = Account.All;` (line ~101). The `ItemTemplate` must be set after
`ItemsSource` to respect WPF control initialization order.

```csharp
// B20-LANE-C: DW-B17-ACCOUNT-NAME-01 -- strip !<suffix> from displayed account names.
leaderCb.ItemTemplate  = BuildAccountDisplayTemplate();
followerLb.ItemTemplate = BuildAccountDisplayTemplate();
```

**CYC impact**: These are two assignment statements. No branching. CYC of `BuildRuleRow` is
unchanged (the method's own CYC is not increased by these statements).

---

#### Change K — BuildDynamicRuleRow: set ItemTemplate on leaderCb and followerLb

**Location**: Inside `BuildDynamicRuleRow()`, same pattern as Change J — after
`leaderCb.ItemsSource = Account.All;` and `followerLb.ItemsSource = Account.All;`.

```csharp
// B20-LANE-C: DW-B17-ACCOUNT-NAME-01 -- strip !<suffix> from displayed account names.
leaderCb.ItemTemplate  = BuildAccountDisplayTemplate();
followerLb.ItemTemplate = BuildAccountDisplayTemplate();
```

**Note**: `BuildDynamicRuleRow` uses the same `leaderCb`/`followerLb` variable names as
`BuildRuleRow` per the source reading. Both require the same `ItemTemplate` assignment.

---

### 3.2 TradeCopierWindow.cs

#### Change E — OnLoaded: subscribe to CopyEnabledChanged
**Location**: Inside the second `try` block, after `CopyEngine.Instance.LoadRules();`
(line ~113).

```
_engine.CopyEnabledChanged += OnCopyEnabledChanged;
```

The second try block (lines 108-118) already subscribes `StatusUpdate` and
`PositionStateChanged`. This subscription follows the same pattern.

---

#### Change F — OnWindowClosed: unsubscribe from CopyEnabledChanged
**Location**: After `_engine.PositionStateChanged -= OnPositionStateChanged;` at line ~124.

```
_engine.CopyEnabledChanged -= OnCopyEnabledChanged;
```

`OnWindowClosed` is the symmetric counterpart of `OnLoaded` for lifecycle cleanup.
`OnClosed` (line 127-133) handles `StatusUpdate` and `Unsubscribe()`; `OnWindowClosed`
handles per-surface event teardown. This is the existing pattern: `PositionStateChanged`
is unsubscribed in `OnWindowClosed`, so `CopyEnabledChanged` goes here too.

---

#### Change G — NEW private method: OnCopyEnabledChanged
**Location**: After `OnGlobalToggle` method (currently line ~579).

Signature:
```csharp
private void OnCopyEnabledChanged(bool enabled)
```

Full body:
```csharp
// B20-LANE-C: DW-B20-LANE-A-DEFER-01 -- cross-surface copy toggle sync.
// CYC=1: no control flow. Lambda excluded from CYC count.
// JS-021: no lock. JS-033: not async. Dispatcher.InvokeAsync required.
private void OnCopyEnabledChanged(bool enabled)
{
    _copyEnabled = enabled;
    Dispatcher.InvokeAsync(() =>
    {
        _globalToggleBtn.Content    = enabled ? "Copy All ON" : "Copy All OFF";
        _globalToggleBtn.Background = enabled ? WBrushActive : WBrushInactive;
    });
}
```

**No null guard rationale**: `_globalToggleBtn` is assigned during `BuildUI` (constructor).
If `BuildUI` throws, the constructor executes `return;` before `Loaded += OnLoaded` is
attached (lines 82-91 of `TradeCopierWindow.cs`). Therefore `OnLoaded` never fires, the
subscription to `CopyEnabledChanged` never occurs, and `OnCopyEnabledChanged` is never
called in the partial-construction case. `_globalToggleBtn` is guaranteed non-null at the
call site. CYC=1 (no control flow).

---

## §4 CYC Analysis Per New/Modified Method

| Method | File | CYC | Branches |
|--------|------|-----|----------|
| `OnCopyEnabledChanged(bool)` | `TradeCopierPanel.cs` | 2 | null guard on `_copyToggleBtn2` (1) + base path (1) |
| `FollowerItem.ToString()` (modified) | `TradeCopierPanel.cs` | 1 | null-conditional chain, no new decision point added vs prior version |
| `OnCopyEnabledChanged(bool)` | `TradeCopierWindow.cs` | 1 | base path only |
| `AccountDisplayConverter.Convert` | `TradeCopierWindow.cs` | 1 | straight-line: null-conditional chain, no branch |
| `AccountDisplayConverter.ConvertBack` | `TradeCopierWindow.cs` | 1 | straight-line: single throw (never called in practice) |
| `BuildAccountDisplayTemplate()` | `TradeCopierWindow.cs` | 1 | straight-line: object construction, no branches |

**Counting convention** (established in codebase, see line 417 Panel comment):
- `if`/`else`/`foreach`/`switch case` each add +1 to CYC.
- Ternary operators (`? :`) add +1 to CYC.
- Lambdas passed to `Dispatcher.InvokeAsync` are treated as separate CYC units; their
  internal ternaries are NOT counted toward the enclosing method's CYC.
- Null-conditional `?.` operators do not add to CYC (they are part of a single expression).

All new methods satisfy CYC <= 8. No existing methods are modified in a way that raises CYC.

---

## §5 JS Rule Compliance

| Rule | Description | Status | Evidence |
|------|-------------|--------|---------|
| **JS-021** | No `lock()` anywhere | PASS | No `lock` keyword introduced. `_copyEnabled` is a `bool` accessed exclusively on the UI thread (WPF single-threaded model). `Dispatcher.InvokeAsync` dispatches to UI thread without blocking. |
| **JS-033** | No `async void` | PASS | `OnCopyEnabledChanged` in both files is `private void`, not `async void`. No asynchronous state machine generated. |
| **NT8-003** | No `volatile double/int` | PASS | No `volatile` fields introduced. `_copyEnabled` is a plain `bool` (value type, not `double` or `int`). |

**Additional rules verified (no violation):**
- **JS-001** (no throw in hot path): No exceptions thrown in new code.
- **JS-002** (no `return null`): `ToString()` returns `""` not `null` when Account is null.
- CYC <= 8: Confirmed for all new/modified methods (§4).

---

## §6 No New Tests Rationale

### Part A — CopyEnabledChanged Wiring (DW-B20-LANE-A-DEFER-01)

**What the change does**: Attaches a `bool`-valued event callback to a WPF UI element
state mutation behind `Dispatcher.InvokeAsync`.

**Why no test**:
1. The underlying state machine — `CopyEngine._isCopyEnabled` toggled by `SetEnabled` and
   broadcast via `CopyEnabledChanged` — was tested in B20-LANE-A (tests at lines ~1031+
   in `CopyEngineTests.cs`, CopyEnabled test pair).
2. The subscriber callbacks (`OnCopyEnabledChanged`) are pure visual state assignments:
   `Content` and `Background` on a WPF `Button`. xUnit cannot instantiate WPF controls
   without an STA thread and a full WPF application context (NT8 restriction: no WPF
   test harness available).
3. The `_copyEnabled = enabled` assignment is trivially correct: a single bool assignment
   from a trusted event argument. No branching logic to test.
4. Per the task spec: "No new [Fact] tests required (display-only + event wiring)."

**Testability boundary**: CopyEngine (business logic) is fully tested. The UI binding
layer (WPF controls) is explicitly out of xUnit test scope in this codebase. This follows
the established pattern: `OnPositionStateChanged` in both Panel and Window has no direct
unit test; `CopyEngine.TryFirePositionState` is tested instead.

### Part B — FollowerItem.ToString() (DW-B17-ACCOUNT-NAME-01)

**What the change does**: Changes the string returned by a WPF data-binding helper method
from `Account.Name` to `Account.Name` with `!<suffix>` stripped.

**Why no test**:
1. `FollowerItem` is a `private sealed class` inside `TradeCopierPanel`. It is not
   accessible from xUnit without reflection, which violates the project's "no test
   contortion" principle.
2. The change has zero effect on business logic. `Account.Name` raw value is unchanged.
   No order routing, copy rule evaluation, or account selection logic reads `ToString()`.
3. The expression `Account?.Name?.Split('!')?[0] ?? ""` is a language-primitive operation.
   A test would assert `"Apex!Foo".Split('!')[0] == "Apex"` — testing .NET's `Split`, not
   our logic.
4. Per the task spec: "No new [Fact] tests required (display-only + event wiring)."

**Current test baseline**: 120 [Fact] tests (B20-LANE-A final count from backlog §6).
This ticket leaves the count unchanged.

---

## §7 Decision Log

| # | Decision | Rationale |
|---|----------|-----------|
| D-01 | Use `?[0]` (null-conditional index) in `FollowerItem.ToString()`, not `[0]` | Spec supplied `[0]` which is **unsafe**: when the null-conditional chain `Account?.Name?.Split('!')` returns null (Account or Name is null), a non-conditional index `[0]` on null throws `NullReferenceException`. The corrected form `?[0]` propagates null through the chain, falling through to `?? ""`. |
| D-02 | Window `OnCopyEnabledChanged` has CYC=1 (no null guard) | `_globalToggleBtn` cannot be null at call site. If `BuildUI` throws, `return;` exits the constructor before `Loaded += OnLoaded` is registered (lines 82-91). Therefore `CopyEnabledChanged` is never subscribed, and `OnCopyEnabledChanged` is never called. No null guard needed. |
| D-03 | Panel `OnCopyEnabledChanged` has CYC=2 (null guard on `_copyToggleBtn2`) | Panel's initialization path does not have the same early-return guarantee as Window's constructor. A null guard is cheap insurance (+1 CYC, still CYC=2 <= 8). |
| D-04 | `Dispatcher.InvokeAsync` required even though `CopyEnabledChanged` currently fires on the UI thread | `CopyEngine` is not WPF-aware. It imposes no threading contract on `SetEnabled` callers. Future callers (e.g., a background strategy) may call `SetEnabled` from a non-UI thread. Using `Dispatcher.InvokeAsync` unconditionally is the defensive pattern established at Panel line 433 and Window line 153. |
| D-05 | ~~No change to Window account controls~~ — SUPERSEDED by D-09 | Previously excluded; now addressed via `AccountDisplayConverter`. See D-09. |
| D-06 | Subscribe `CopyEnabledChanged` inside the existing `try` block in Window `OnLoaded` | The second `try` block (lines 108-118) covers engine initialization. `CopyEnabledChanged` subscription is an engine-level operation and belongs there. This is consistent with `StatusUpdate` and `PositionStateChanged` subscriptions at lines 110-111. |
| D-07 | Unsubscribe in `OnWindowClosed`, not `OnClosed` | `PositionStateChanged` is unsubscribed in `OnWindowClosed` (line 124). `StatusUpdate` is unsubscribed in `OnClosed` (line 130). `CopyEnabledChanged` is a UI-sync event; it should be torn down at the same lifecycle point as `PositionStateChanged` (i.e., when the Window is closed, before `OnClosed` finalizes). |
| D-08 | `_copyEnabled = enabled` set synchronously before `Dispatcher.InvokeAsync` | The bool assignment captures the new value before the lambda runs. The lambda references `enabled` (the parameter) directly, not `_copyEnabled`, so there is no stale-capture issue. |
| D-09 | `IValueConverter` in code (`AccountDisplayConverter`) is the cleanest approach for Window display-layer suffix stripping without XAML | We build WPF in code only (no XAML files in this codebase). Options evaluated: (a) `DisplayMemberPath = "Name"` — shows full name, no stripping; (b) static string field — can't bind to a method; (c) `IValueConverter` + `DataTemplate` via `FrameworkElementFactory` — standard WPF pattern, works in .NET 4.8 AddOn context, no XAML required, `CYC=1` for all new methods, and the converter is `private sealed` so scope is contained. Option (c) is the correct choice. This closes V-01 from the plan review. The converter is applied to **both** `BuildRuleRow` and `BuildDynamicRuleRow` to ensure consistent display across static and dynamic rule rows. |

---

## Appendix A — Cross-Surface Toggle Data Flow

```
[Panel: user clicks COPY ON button]
  --> OnCopyToggle (Panel, UI thread)
      _copyEnabled = true
      _engine.SetEnabled(true)
          CopyEnabledChanged?.Invoke(true)
              --> Panel.OnCopyEnabledChanged(true)   [redundant, but idempotent]
                  _copyEnabled = true (already true)
                  Dispatcher.InvokeAsync: update _copyToggleBtn2 (queued, idempotent)
              --> Window.OnCopyEnabledChanged(true)  [** NEW SYNC *]
                  _copyEnabled = true (was false)
                  Dispatcher.InvokeAsync: update _globalToggleBtn to "Copy All ON"
      (Panel button updated synchronously by OnCopyToggle lines 905-907)

[Window: user clicks "Copy All ON" button]
  --> OnGlobalToggle (Window, UI thread)
      _copyEnabled = true
      _engine.SetEnabled(true)
          CopyEnabledChanged?.Invoke(true)
              --> Panel.OnCopyEnabledChanged(true)   [** NEW SYNC *]
                  _copyEnabled = true (was false)
                  Dispatcher.InvokeAsync: update _copyToggleBtn2 to "COPY ON"
              --> Window.OnCopyEnabledChanged(true)  [redundant, but idempotent]
                  _copyEnabled = true (already true)
                  Dispatcher.InvokeAsync: update _globalToggleBtn (queued, idempotent)
      (Window button updated synchronously by OnGlobalToggle lines 577-578)
```

The redundant self-callback (a surface receives its own toggle change) is harmless:
the bool assignment is idempotent, and the `Dispatcher.InvokeAsync` queues a no-op
UI update (same content/background that was already set synchronously).

---

## Appendix B — FollowerItem.ToString Null Safety Chain

```
Account = null         -> Account?.Name      = null
                       -> null?.Split('!')   = null
                       -> null?[0]           = null
                       -> null ?? ""         = ""  [SAFE]

Account.Name = null    -> Account?.Name      = null
                       -> null?.Split('!')   = null
                       -> null?[0]           = null
                       -> null ?? ""         = ""  [SAFE]

Account.Name = "Apex"  -> "Apex".Split('!')  = ["Apex"]
                       -> ["Apex"]?[0]       = "Apex"
                       -> "Apex" ?? ""       = "Apex"  [SAFE -- no suffix]

Account.Name = "Apex!Apex"
                       -> "Apex!Apex".Split('!') = ["Apex", "Apex"]
                       -> ["Apex","Apex"]?[0]    = "Apex"
                       -> "Apex" ?? ""           = "Apex"  [CORRECT]

Account.Name = "Rithmic Trader!SimAccount"
                       -> Split result [0] = "Rithmic Trader"
                       -> "Rithmic Trader"  [CORRECT]
```

---

## Appendix C — SCAN-01..07 Pre-flight (Engineer Contract)

| SCAN | Check | Expected Result |
|------|-------|----------------|
| SCAN-01 | `grep -n "lock(" TradeCopierPanel.cs TradeCopierWindow.cs` | 0 matches in new code |
| SCAN-02 | `grep -n "async void" TradeCopierPanel.cs TradeCopierWindow.cs` | 0 matches in new methods |
| SCAN-03 | `grep -n "volatile" TradeCopierPanel.cs TradeCopierWindow.cs` | 0 new volatile fields |
| SCAN-04 | `grep -n "Dispatcher.Invoke(" TradeCopierPanel.cs TradeCopierWindow.cs` | 0 matches (must use InvokeAsync, not Invoke) |
| SCAN-05 | `grep -n "CopyEnabledChanged" TradeCopierPanel.cs TradeCopierWindow.cs` | Exactly 2 lines Panel (+=, -=), 2 lines Window (+=, -=), 1 line each (method body) |
| SCAN-06 | `grep -n '\.Name\[0\]' TradeCopierPanel.cs` (non-null-conditional index) | 0 matches (must be `?[0]`) |
| SCAN-07 | `grep -n "AccountDisplayConverter" TradeCopierWindow.cs` | Exactly 1 class definition + 1 Convert method + 1 ConvertBack method + 2 references in BuildAccountDisplayTemplate + 2 references each in BuildRuleRow and BuildDynamicRuleRow |
| SCAN-08 | `grep -n "ItemTemplate" TradeCopierWindow.cs` | Exactly 4 assignment lines (2 in BuildRuleRow + 2 in BuildDynamicRuleRow) |
| SCAN-09 | Build: `dotnet build` | 0 errors, 0 new warnings |

---

**REVIEW_STATUS**: PENDING ptt-plan-reviewer (re-submission — V-01 resolved by adding §3.3 Part C)
