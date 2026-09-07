# Ticket C-04 Completion Report
**Ticket**: C-04 — `TradeCopierWindow::BuildActionButtons`
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Test file**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` (appended)
**Date**: 2026-09-07
**Engineer**: ptt-engineer (WAVE1-LANE-C)

---

## Pre-Edit CCN Baseline

| Method | CCN Before |
|--------|-----------|
| `BuildActionButtons` | **58** |

Confirmed via: `lizard src/PropTraderTools/TradeCopierWindow.cs --csv`

---

## Extraction Implemented

**Parent method after extraction** (`BuildActionButtons`, lines 755–769):
- CCN = **1** (straight-line delegation to 5 helpers)
- Receives `atmCb`/`namedBox` from `atmPanel.Children`, then delegates 5 button builds.

**Helpers extracted** (all private instance methods on `TradeCopierWindow`):

| Helper | Signature | CCN | Lines |
|--------|-----------|-----|-------|
| `BuildTrimActionButton` | `private void BuildTrimActionButton(object tag, Grid grid)` | 1 | 772–785 |
| `BuildFlattenActionButton` | `private void BuildFlattenActionButton(object tag, Grid grid)` | 1 | 788–801 |
| `BuildCancelActionButton` | `private void BuildCancelActionButton(object tag, Grid grid)` | 1 | 804–817 |
| `BuildToggleActionButton` | `private void BuildToggleActionButton(object tag, Grid grid)` | 1 | 820–832 |
| `BuildApplyActionButton` | `private void BuildApplyActionButton(object tag, ComboBox leaderCb, ListBox followerLb, ComboBox atmCb, TextBox namedBox, Grid grid)` | 1 | 835–848 |

**Behaviour**: Zero behaviour change — purely structural extraction. Every button property, list-add, event wire, and Grid.Column assignment is verbatim identical to the pre-edit body.

---

## Tests Added

File: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` (appended to existing class)

| Test | Assertion |
|------|-----------|
| `T_C04_01_BuildTrimActionButton_GridColumn_IsThree` | Grid.Column = 3 |
| `T_C04_02_BuildFlattenActionButton_AddedTo_FlattenBtnsList` | List count delta = 1 |
| `T_C04_03_BuildCancelActionButton_Background_IsWBrushInactive` | Cancel list count delta = 1 |
| `T_C04_04_BuildToggleActionButton_GridColumn_IsSix` | Grid.Column = 6 |
| `T_C04_05_BuildApplyActionButton_TagArray_ContainsFiveElements` | Tag array length = 5 |

---

## 7-Scan Results

### Scan 1 — JS-021 lock() — PASS
```
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "lock\("
```
Results: 2 comment-only hits (lines 581, 754). Zero live `lock()` calls.

### Scan 2 — JS-001 throw new — PASS
```
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "throw new"
```
Result: 1 hit at line 907 — pre-existing `ConvertBack` in `AccountDisplayConverter` IValueConverter.
Zero new `throw new` in any C-04 helper.

### Scan 3 — JS-002 return null — PASS
```
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "return null"
```
Results: comment-only hits (lines 291, 316, 330, 581, 754) + 2 hits at lines 1165, 1172 in pre-existing `FindInstrument`. Zero `return null` in C-04 helpers.

### Scan 4 — JS-033 async void — PASS
```
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "async void"
```
Results: 3 comment-only hits. Zero live `async void` declarations.

### Scan 5 — CCN via lizard — PASS
```
lizard src/PropTraderTools/TradeCopierWindow.cs --csv
```
| Method | CCN |
|--------|-----|
| `BuildActionButtons` | **1** |
| `BuildTrimActionButton` | **1** |
| `BuildFlattenActionButton` | **1** |
| `BuildCancelActionButton` | **1** |
| `BuildToggleActionButton` | **1** |
| `BuildApplyActionButton` | **1** |
All CCN <= 8. ✅

### Scan 6 — throw (non-comment) — PASS
```
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "throw " (non-comment filter)
```
Result: 1 hit at line 907 (pre-existing `ConvertBack`). Zero new throws in C-04 helpers.

### Scan 7 — dotnet build — PASS
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Result: `Build succeeded. 0 Warning(s). 0 Error(s).`

---

## Test Run Result

```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
```
Result: **Passed! Failed: 0, Passed: 186, Skipped: 3, Total: 189**
Floor requirement: >= 127. Actual: 186. ✅

---

## Final Status

BUILD_PASS
