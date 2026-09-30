# PTT-COPIER-B5 — Ticket T2 Verification Report

**Ticket**: T2 — TradeCopierWindow.cs: multi-select ListBox (DW-B5-01) + Shift+B per row (DW-B5-02)
**File verified**: `src/PropTraderTools/TradeCopierWindow.cs` (Wave workspace, READ-ONLY)
**Verifier mode**: PTT Verifier (v12-phase5-v-verify)
**Date**: 2026-07-06
**Block**: B5

---

## Independent Scan Results (Verifier re-ran all 7 — engineer results NOT trusted)

| Scan | Command | Actual Output | Result |
|------|---------|---------------|--------|
| S1 | `Select-String -Pattern "lock\("` | No output | **0 matches — PASS** |
| S2 | `Select-String -Pattern "DateTime\.Now"` | No output | **0 matches — PASS** |
| S3 | `Select-String -Pattern "0x[0-9A-Fa-f]"` | No output | **0 matches — PASS** |
| S4 (non-ASCII) | `if (Select-String ... '[^\x00-\x7F]') { "FAIL" } else { "PASS" }` | `PASS` | **0 non-ASCII chars — PASS** |
| S5 (brace balance) | Count `{` and `}` via regex | `Open braces: 86  Close braces: 86  Match: True` | **PASS** |
| S6 (FontFamily) | `Select-String -Pattern "FontFamily"` | No output | **0 matches — PASS** |
| S7 (hex colors) | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | No output | **0 matches — PASS** |

All 7 independent scans: **ZERO violations**.

---

## S5 — CYC Manual Count for New/Modified Methods

| Method | Lines | Branch Points | CYC | Limit | Status |
|--------|-------|---------------|-----|-------|--------|
| `OnWindowBreakEven` | 400–406 | 2 `if` guards | 3 | ≤ 8 | **PASS** |
| `SetActiveRule` | 409–412 | 0 (pure assignment) | 1 | ≤ 8 | **PASS** |
| `RelayCommand.CanExecute` | 457 | 0 (expression body) | 1 | ≤ 8 | **PASS** |
| `RelayCommand.Execute` | 459 | 0 (expression body) | 1 | ≤ 8 | **PASS** |
| `OnRowApply` (modified) | 382–397 | null-tag, is-TextBox ternary, IsNullOrEmpty, null-followerLb, is-Account, leader-null\|\|count==0 | 7 | ≤ 8 | **PASS** |

CYC: all new/modified methods ≤ 8. **PASS**

---

## S6 — Using Directives Audit

Verified by `Select-String -Pattern "using System"` (output confirmed lines 5–9):

| Line | Directive | Status |
|------|-----------|--------|
| 5 | `using System;` | Retained — untouched |
| 6 | `using System.Collections.Generic;` | **Added (B5 new)** |
| 7 | `using System.Windows;` | Retained — untouched |
| 8 | `using System.Windows.Controls;` | Retained — untouched |
| 9 | `using System.Windows.Input;` | **Added (B5 new)** |
| 10 | `using NinjaTrader.Cbi;` | Retained (verified in file) |
| 11 | `using NinjaTrader.Gui;` | Retained |
| 12 | `using NinjaTrader.Gui.Tools;` | Retained |
| 13 | `using NinjaTrader.NinjaScript;` | Retained |

Both B5 required using directives present. Zero original directives removed. **PASS**

---

## Additive Contract Verification (V-A through V-H)

### V-A: `_activeRuleInstrument` field present?
**Line 25**: `private string _activeRuleInstrument = string.Empty; // B5: tracks last-moused-over rule row for Shift+B`
**PASS** — field present, initialized to `string.Empty` (safe for IsNullOrEmpty guard).

### V-B: `RelayCommand` nested class with ICommand implementation?
**Lines 446–460**: `private sealed class RelayCommand : ICommand` — contains:
- `readonly Action<object> _execute` (line 448)
- `CanExecuteChanged { add { } remove { } }` (line 455)
- `CanExecute(object parameter) => true` (line 457)
- `Execute(object parameter) => _execute(parameter)` (line 459)
**PASS** — full ICommand contract implemented, no lock, no state mutation outside Execute.

### V-C: `KeyBinding(Key.B, ModifierKeys.Shift)` in `BuildUI()`?
**Lines 110–111**:
```csharp
var beWinCmd = new RelayCommand(o => OnWindowBreakEven(null, null));
InputBindings.Add(new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift));
```
Located before `Content = root;` (line 113) — exactly as specified.
**PASS**

### V-D: Both `BuildRuleRow()` and `BuildDynamicRuleRow()` have `ListBox (SelectionMode=Extended)` + `ScrollViewer`?
**BuildRuleRow** (lines 151–165):
```csharp
var followerLb = new ListBox { SelectionMode = SelectionMode.Extended, ItemsSource = Account.All, MaxHeight = 80, Margin = new Thickness(2) };
var followerScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 80, Content = followerLb };
```
**BuildDynamicRuleRow** (lines 250–264): identical pattern.
**PASS** — both rows updated consistently.

### V-E: Both rows have `MouseEnter → SetActiveRule` call?
**BuildRuleRow**: Line 119 — `grid.MouseEnter += (s, ev) => SetActiveRule(instrumentName);`
**BuildDynamicRuleRow**: Line 242 — `grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);`
**PASS** — dynamic row captures `instrTextBox.Text` (live reference) as required by architecture plan.

### V-F: `OnRowApply()` uses `foreach` over `SelectedItems`?
**Lines 391–394**:
```csharp
var followers = new List<Account>();
if (followerLb != null)
    foreach (var item in followerLb.SelectedItems)
        if (item is Account acc) followers.Add(acc);
```
**PASS** — multi-select pattern correctly iterates `SelectedItems`; `followers.Count == 0` guard at line 395.

### V-G: `OnWindowBreakEven()` calls `OnRuleBreakEven(_activeRuleInstrument)` or equivalent?
**Lines 400–406**:
```csharp
private void OnWindowBreakEven(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_activeRuleInstrument)) return;
    var instrument = FindInstrument(_activeRuleInstrument);
    if (instrument != null)
        _engine.BreakEven(instrument, 2);
}
```
Note: Architecture plan specified calling `_engine.BreakEven` directly (not via `OnRuleBreakEven`). Implementation matches architecture plan exactly — direct engine call with hardcoded 2 ticks.
**PASS**

### V-H: All B1-B4 methods untouched?

Spot-check of key B1-B4 methods against the file:

| Method | Lines | Verdict |
|--------|-------|---------|
| `OnRuleBreakEven()` | 365–380 | Untouched — tag-array pattern, `FindInstrument`, `_engine.BreakEven(instrument, ticks)` intact |
| `OnGlobalToggle()` | 315–320 | Untouched — `_copyEnabled` toggle + `_engine.SetEnabled` + button content update intact |
| `OnAddRule()` | 322–325 | Untouched — `_rulesPanel.Children.Add(BuildDynamicRuleRow())` single line intact |
| `OnInitialize()` | 28–34 | Untouched — engine wire-up, Subscribe, BuildUI |
| `OnDestroyed()` | 36–40 | Untouched — event unsub, Unsubscribe |
| `OnStatusUpdate()` | 414–429 | Untouched — `Dispatcher.InvokeAsync`, `DateTime.UtcNow`, trim-to-50 loop |
| `FindInstrument()` | 431–443 | Untouched — null guard + `Instrument.GetInstrument` + catch |

**Note on `OnRemoveRule`**: Not present in architecture plan for Window (only `OnAddRule` is defined). No regression.  
**Note on `OnToggleCopyEngine`**: Template alias for `OnGlobalToggle` — actual method name in this codebase is `OnGlobalToggle` (line 315). Confirmed untouched.

**PASS** — zero B1-B4 mutations detected.

---

## Architecture Plan Compliance

| Plan Item (Section B) | Status in File |
|----------------------|----------------|
| `_activeRuleInstrument` field add | Line 25 — PRESENT |
| `BuildUI()` — Shift+B KeyBinding | Lines 110–111 — PRESENT |
| `BuildRuleRow()` — MouseEnter + ListBox + Tag update | Lines 119, 151–165, 198 — PRESENT |
| `BuildDynamicRuleRow()` — MouseEnter + ListBox + Tag update | Lines 242, 250–264, 292 — PRESENT |
| `OnRowApply()` — SelectedItems multi-select | Lines 391–396 — PRESENT |
| `OnWindowBreakEven()` method add | Lines 400–406 — PRESENT |
| `SetActiveRule()` method add | Lines 409–412 — PRESENT |
| `RelayCommand` nested class add | Lines 446–460 — PRESENT |

All 8 change-list items for `TradeCopierWindow.cs` implemented. **PASS**

---

## Final Verdict

| Category | Result |
|----------|--------|
| S1 — No lock() | PASS |
| S2 — No DateTime.Now | PASS |
| S3 — No hex literals | PASS |
| S4 — ASCII-only | PASS |
| S5 — Brace balance | PASS |
| S6 — No FontFamily | PASS |
| S7 — No hex colors | PASS |
| CYC ≤ 8 (all new methods) | PASS |
| Using directives (2 new, 7 retained) | PASS |
| V-A: _activeRuleInstrument present | PASS |
| V-B: RelayCommand : ICommand present | PASS |
| V-C: KeyBinding Shift+B in BuildUI | PASS |
| V-D: ListBox+ScrollViewer in both rows | PASS |
| V-E: MouseEnter→SetActiveRule in both rows | PASS |
| V-F: foreach over SelectedItems in OnRowApply | PASS |
| V-G: OnWindowBreakEven → _engine.BreakEven | PASS |
| V-H: B1-B4 methods untouched | PASS |

---

## VERIFY_PASS
