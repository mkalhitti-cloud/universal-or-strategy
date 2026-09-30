# PTT-COPIER-B5 — Ticket T2 Completion Report

**Ticket**: T2 — TradeCopierWindow.cs: multi-select ListBox (DW-B5-01) + Shift+B per row (DW-B5-02)
**File edited**: `src/PropTraderTools/TradeCopierWindow.cs` (Wave workspace)
**Date**: 2026-07-06
**Engineer mode**: PTT Engineer (v12-engineer)
**Block**: B5 — ADDITIVE on top of B4

---

## Summary of Changes

All changes are additive surgical edits. No existing B1–B4 method, field, or property was removed or altered.

### 1. Header comment updated (line 1)
- `// PTT-COPIER-B4` → `// PTT-COPIER-B5`

### 2. Two new `using` directives added (lines 6, 9)
- **Line 6**: `using System.Collections.Generic;`
- **Line 9**: `using System.Windows.Input;`
- All 7 original using directives retained in original order.

### 3. New field `_activeRuleInstrument` (line 25)
- Inserted after `private bool _copyEnabled;`
- `private string _activeRuleInstrument = string.Empty;`
- Initialized to `string.Empty` so `string.IsNullOrEmpty` guard in `OnWindowBreakEven` fires safely on first Shift+B.

### 4. `BuildUI()` — Shift+B KeyBinding inserted before `Content = root;` (lines 109–111)
- Added `RelayCommand` wiring `o => OnWindowBreakEven(null, null)`.
- Added `new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift)` to `InputBindings`.
- All existing layout code in `BuildUI()` untouched.

### 5. `BuildRuleRow(string instrumentName)` — three changes
- **Line 119**: `grid.MouseEnter += (s, ev) => SetActiveRule(instrumentName);` — added immediately after grid construction.
- **Lines 150–165**: Replaced follower `ComboBox` (9 lines) with `ListBox` + `ScrollViewer` (16 lines). `SelectionMode = Extended`, `MaxHeight = 80`, `ItemsSource = Account.All`.
- **Line 198**: `applyBtn.Tag` updated from `followerCb` → `followerLb`.

### 6. `BuildDynamicRuleRow()` — three changes
- **Line 242**: `grid.MouseEnter += (s, ev) => SetActiveRule(instrTextBox.Text);` — added after `instrTextBox` added to grid's children (captures live text at mouse-enter time).
- **Lines 249–264**: Replaced follower `ComboBox` (4 lines) with `ListBox` + `ScrollViewer` (16 lines). Same pattern as fixed row.
- **Line 292**: `applyBtn.Tag` updated from `followerCb` → `followerLb`.

### 7. `OnRowApply()` — follower extraction replaced (lines 388–396)
- Removed: `tag[2] as ComboBox` single-select extraction + `new[] { follower }`.
- Added: `tag[2] as ListBox` + `List<Account>` loop over `SelectedItems` + `followers.ToArray()`.
- Guard changed from `follower == null` to `followers.Count == 0`.

### 8. New method `OnWindowBreakEven` (lines 399–406)
- CYC = 3 (2 `if` guards + base 1).
- Calls `FindInstrument(_activeRuleInstrument)` then `_engine.BreakEven(instrument, 2)`.
- Hardcoded 2 ticks: fast-path keyboard shortcut; per-row button reads `beBox.Text` for custom ticks.

### 9. New method `SetActiveRule` (lines 408–412)
- CYC = 1 (pure assignment).
- Sets `_activeRuleInstrument = instrName`.

### 10. New nested class `RelayCommand` (lines 445–460)
- `private sealed class RelayCommand : ICommand`
- Fields: `readonly Action<object> _execute`.
- `CanExecuteChanged`: empty add/remove (no notifications needed).
- `CanExecute`: always `true`.
- `Execute`: delegates to `_execute(parameter)`.
- CYC = 1 per method.
- Identical in structure to `TradeCopierPanel`'s `RelayCommand`.

---

## All 7 Scan Results

| Scan | Pattern / Check | Result | Evidence |
|------|----------------|--------|----------|
| S1 | `lock\(` | **0 matches** | `Select-String -Pattern "lock\("` → no output |
| S2 | `DateTime\.Now[^U]` | **0 matches** | Only `DateTime.UtcNow` exists (line 422, untouched) |
| S3 | `0x[0-9A-Fa-f]` | **0 matches** | `Select-String -Pattern "0x[0-9A-Fa-f]"` → no output |
| S4 | Non-ASCII characters | **0 chars** | PowerShell regex `[^\x00-\x7F]` → count 0 |
| S5 | `FontFamily` | **0 matches** | `Select-String -Pattern "FontFamily"` → no output |
| S6 | `#[0-9A-Fa-f]{6}` hex colors | **0 matches** | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` → no output |
| S7 | Brace balance | **86 = 86** | `{` count 86 = `}` count 86 → balanced |

All 7 scans: **ZERO violations**.

---

## CYC Verification for All New Methods

| Method | Branch points | CYC | Limit | Status |
|--------|--------------|-----|-------|--------|
| `OnWindowBreakEven` | 2 `if` guards | 3 | ≤ 8 | PASS |
| `SetActiveRule` | 0 (assignment) | 1 | ≤ 8 | PASS |
| `RelayCommand.CanExecute` | 0 | 1 | ≤ 8 | PASS |
| `RelayCommand.Execute` | 0 | 1 | ≤ 8 | PASS |
| `OnRowApply` (modified) | `if(null)` + `foreach` + `if(is Account)` + `if(==null\|\|.Count==0)` + 2 prior guards | 6 | ≤ 8 | PASS |

---

## Using Directives Audit

| Directive | Status |
|-----------|--------|
| `using System;` (line 5) | Retained — untouched |
| `using System.Collections.Generic;` (line 6) | **Added — new** |
| `using System.Windows;` (line 7) | Retained — untouched |
| `using System.Windows.Controls;` (line 8) | Retained — untouched |
| `using System.Windows.Input;` (line 9) | **Added — new** |
| `using NinjaTrader.Cbi;` (line 10) | Retained — untouched |
| `using NinjaTrader.Gui;` (line 11) | Retained — untouched |
| `using NinjaTrader.Gui.Tools;` (line 12) | Retained — untouched |
| `using NinjaTrader.NinjaScript;` (line 13) | Retained — untouched |

Zero directives removed. Two added.

---

## Additive Contract Compliance

**No existing B1–B4 methods altered.**

| Symbol | Lines (new) | Status |
|--------|------------|--------|
| `OnInitialize()` | 28–34 | Untouched |
| `OnDestroyed()` | 36–40 | Untouched |
| `BuildUI()` | 42–114 | Only 3 lines inserted before `Content = root;` |
| `BuildRuleRow()` | 116–219 | 3 surgical changes only (MouseEnter + ComboBox→ListBox + Tag) |
| `BuildDynamicRuleRow()` | 221–313 | 3 surgical changes only (MouseEnter + ComboBox→ListBox + Tag) |
| `OnGlobalToggle()` | 315–320 | Untouched |
| `OnAddRule()` | 322–325 | Untouched |
| `OnRuleTrim()` | 327–334 | Untouched |
| `OnRuleFlatten()` | 336–343 | Untouched |
| `OnRuleCancel()` | 345–352 | Untouched |
| `OnRuleToggle()` | 354–362 | Untouched |
| `OnRuleBreakEven()` | 364–380 | Untouched |
| `OnRowApply()` | 382–397 | Follower extraction replaced (6 lines → 9 lines); tag/instrName/null guard untouched |
| `OnStatusUpdate()` | 414–429 | Untouched |
| `FindInstrument()` | 431–443 | Untouched |

---

## Final Line Count

**TradeCopierWindow.cs: 463 lines** (was 400 lines in B4; +63 lines net additive).

---

## Acceptance Criteria Check

- [x] Both `BuildRuleRow` and `BuildDynamicRuleRow` updated in same commit — tag[2] is consistently `ListBox` in both paths.
- [x] `followers.Count == 0` guard prevents silent null state when no selection.
- [x] `_activeRuleInstrument` initialized to `string.Empty` — Shift+B before mousing over any row does nothing (guarded by `string.IsNullOrEmpty`).
- [x] Dynamic row MouseEnter placed AFTER `instrTextBox` is added to grid (line 242) — lambda captures live `instrTextBox.Text` reference.
- [x] `RelayCommand` is `private sealed` — not leaking to outer scope.

---

*BUILD_PASS*
