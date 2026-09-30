# B51-LaneA Architecture Plan

**Block**: PTT-COPIER-B51  
**Lane**: A  
**Epic**: B51-LaneA  
**Date**: 2026-08-08  
**Status**: REVIEW_PASS  
**Author**: ptt-architect  

---

## 1. Summary

Block B51 Lane A fixes two UI-only bugs in `TradeCopierPanel.cs` and bumps the build tag in
`CopyEngine.cs`. No engine logic is changed. No new tests are written (WPF DataTemplate factory
and RoutedEventHandler — not exercisable via xUnit without an NT8 Dispatcher host).

| Bug ID     | Title                                          | File                  | Method                             |
|------------|------------------------------------------------|-----------------------|------------------------------------|
| DW-B51-01  | Multiplier TextBox visible in follower rows    | TradeCopierPanel.cs   | `BuildCheckItemTemplate()`         |
| DW-B51-02  | ATM dropdown reappears after checkbox in Clone | TradeCopierPanel.cs   | `OnFollowerAtmTemplateComboLoaded` |
| (housekeep)| Build tag bump B50 -> B51                      | CopyEngine.cs         | build-tag string literal (L41)     |

---

## 2. Root Cause Analysis

### DW-B51-01 — Multiplier TextBox column visible in follower rows

**Root cause**: `BuildCheckItemTemplate()` constructs a `FrameworkElementFactory` for the multiplier
`TextBox` (column index 2) and registers its `TextChanged` event handler, but never sets the initial
`VisibilityProperty` to `Collapsed`. The WPF `FrameworkElementFactory` default for any unset
`VisibilityProperty` is `Visibility.Visible`. Therefore, when the DataTemplate is applied to each
follower row, the multiplier TextBox renders visible from the moment the row appears — regardless of
whether the user has requested a multiplier UI.

**Fix**: One additional `SetValue` call on `multFactory` immediately after the `AddHandler` call
(~line 1890) sets the default to `Collapsed`. Runtime code that intentionally shows the multiplier
box can still override this via a local value (standard WPF property value precedence: local value >
template default).

**Change**:
```csharp
// Before (line ~1889-1890):
multFactory.AddHandler(TextBox.TextChangedEvent,
    new TextChangedEventHandler(OnFollowerMultiplierChanged));

// After:
multFactory.AddHandler(TextBox.TextChangedEvent,
    new TextChangedEventHandler(OnFollowerMultiplierChanged));
multFactory.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed); // DW-B51-01
```

**CYC delta**: 0 — no new branch added.

---

### DW-B51-02 — ATM dropdown reappears after checkbox tick in Clone mode

**Root cause**: `OnFollowerAtmTemplateComboLoaded` fires each time a follower row's ATM ComboBox
is loaded into the visual tree (e.g., when the user ticks or unticks a follower checkbox, causing
row virtualization to add/remove the element). The existing code registers the combo in
`_atmComboRefs` for future mode-change events, but does **not** query the *current* mode at load
time. If `CopyMode.Clone` is already active when the row loads, the new combo defaults to
`Visibility.Visible` and briefly (or persistently) shows until the next mode-change event fires.

**Fix**: Immediately after adding `cb` to `_atmComboRefs`, query the current copy mode via
`CopyEngine.Instance.GetCopyMode()`. If the mode is `CopyMode.Clone`, collapse the combo
immediately. This closes the timing gap without touching the existing mode-toggle pathway.

**Change** (lines ~1973-1976, inside `!_atmComboRefs.Contains(cb)` block):
```csharp
// Before:
if (!_atmComboRefs.Contains(cb))
    _atmComboRefs.Add(cb);   // B50: track combo for Clone visibility toggle

// After:
if (!_atmComboRefs.Contains(cb))
{
    _atmComboRefs.Add(cb);   // B50: track combo for Clone visibility toggle
    // B51: apply current mode to newly-loaded combo (timing fix)
    if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
        cb.Visibility = Visibility.Collapsed;
}
```

**CYC delta**: +1 (one new if-branch). Previous CYC = 4. New CYC = 5. Constraint CYC <= 8: PASS.

**Threading**: `OnFollowerAtmTemplateComboLoaded` is a `RoutedEventHandler` — fires on the WPF UI
thread. `cb.Visibility` is a WPF dependency property set on the UI thread. `GetCopyMode()` reads a
PTT-internal enum value. No `Dispatcher.InvokeAsync` required.

---

### Build Tag Bump (CopyEngine.cs line 41)

```csharp
// Before:
"PTT-COPIER B50 | clone-mode+be-color+test-fix | 2026-08-08"

// After:
"PTT-COPIER B51 | ui-fixes | 2026-08-08"
```

One string literal replacement. Zero logic change.

---

## 3. Component List

| Component              | Class / Method                         | File                | Change Type  |
|------------------------|----------------------------------------|---------------------|--------------|
| Follower row template  | `BuildCheckItemTemplate()`             | TradeCopierPanel.cs | +1 line      |
| ATM combo load handler | `OnFollowerAtmTemplateComboLoaded()`   | TradeCopierPanel.cs | +4 lines     |
| Build tag              | string literal L41                     | CopyEngine.cs       | 1 line edit  |

---

## 4. NinjaTrader 8 API Usage

| API                                  | Usage                              | NT8 Safe? |
|--------------------------------------|------------------------------------|-----------|
| `FrameworkElement.VisibilityProperty`| DependencyProperty, WPF .NET 4.8   | YES       |
| `FrameworkElementFactory.SetValue()` | Template property default setter    | YES       |
| `Visibility.Collapsed`               | WPF enum, .NET 4.8                 | YES       |
| `CopyEngine.Instance.GetCopyMode()`  | PTT-internal singleton read        | YES       |
| `CopyMode.Clone`                     | PTT-internal enum value            | YES       |
| `UIElement.Visibility` (property set)| Standard WPF, UI thread            | YES       |

No NT8 banned APIs (NT8-001 through NT8-020) are introduced.

---

## 5. Jane Street / NT8 Rule Compliance

| Rule     | Severity | Check                                    | Result |
|----------|----------|------------------------------------------|--------|
| JS-021   | P0       | No `lock()` in modified regions          | PASS   |
| JS-001   | P0       | No `throw new XxxException` in hot paths | PASS   |
| JS-002   | P0       | No `return null`                         | PASS   |
| JS-033   | P0       | No `async void` (non-event-handler)      | PASS   |
| NT8-001  | P0       | No `{ get; init; }`                      | PASS   |
| NT8-002  | P0       | No `abstract record` / `sealed record`   | PASS   |
| NT8-007  | P0       | No `CreateOrder` with wrong arg 12 type  | PASS   |

---

## 6. CYC Impact Analysis

| Method                              | Before | After | Delta | Limit | Status |
|-------------------------------------|--------|-------|-------|-------|--------|
| `BuildCheckItemTemplate()`          | N/A    | N/A   |  +0   | <=8   | PASS   |
| `OnFollowerAtmTemplateComboLoaded`  |  4     |  5    |  +1   | <=8   | PASS   |

---

## 7. Threading Model

Both changes are confined to WPF UI-thread contexts:

- **`BuildCheckItemTemplate()`**: Template factory construction — single-threaded, called on the UI
  thread during panel initialization. `FrameworkElementFactory.SetValue` is not a live UI mutation;
  it sets a template default value before any element is instantiated. No dispatcher needed.

- **`OnFollowerAtmTemplateComboLoaded()`**: `RoutedEventHandler` — always fires on the WPF UI thread.
  `cb.Visibility` is set directly on the UI thread. `GetCopyMode()` is a read-only enum access.
  No dispatcher needed. No `lock()`. No `ConcurrentQueue` interaction.

---

## 8. Data Flow

```
DW-B51-01:
  BuildCheckItemTemplate()
    └─ multFactory.SetValue(VisibilityProperty, Collapsed)   [NEW]
         └─ DataTemplate applied to follower row
              └─ TextBox renders Collapsed by default
                   └─ Runtime code may set Visible via local value (no change needed)

DW-B51-02:
  User ticks follower checkbox
    └─ WPF adds ComboBox to visual tree
         └─ OnFollowerAtmTemplateComboLoaded fires (UI thread)
              └─ !_atmComboRefs.Contains(cb) → true
                   └─ _atmComboRefs.Add(cb)
                   └─ GetCopyMode() == CopyMode.Clone?       [NEW]
                        └─ YES → cb.Visibility = Collapsed    [NEW]
                        └─ NO  → (no action, remains Visible)
```

---

## 9. No New Tests Rationale

Both fixes mutate WPF `DependencyProperty` values — one via `FrameworkElementFactory` (template
default), one via direct property assignment inside a `RoutedEventHandler`. These paths require a
live WPF `Application` object, a `Dispatcher` pump, and NT8's NinjaScript hosting environment to
exercise. xUnit test runners (console hosts) do not instantiate a WPF `Application` or a
`DispatcherFrame`. Attempting to test these in isolation would require a heavyweight WPF test harness
that is out of scope for B51 and inconsistent with the existing PTT test approach.

Manual verification is performed via SCAN-03 (grep Visibility.Collapsed), SCAN-04 (grep GetCopyMode),
and runtime inspection inside NinjaTrader 8 (F5 load).

---

## 10. Files in Scope

| Action | File Path (Wave workspace)                                              |
|--------|-------------------------------------------------------------------------|
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`       |
