# Ticket C-07 Completion Report
**Ticket**: C-07 -- TradeCopierAddOn::DoInject
**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Test File**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Date**: 2026-09-07

---

## What Was Implemented

Extracted two private instance helpers from `DoInject` per ticket C-07 spec:

### Helpers Extracted

| Helper | Signature | Lines | CCN | Status |
|--------|-----------|-------|-----|--------|
| `PurgeStalePanel` | `private void PurgeStalePanel(System.Windows.Controls.Grid grid)` | 478-483 | 2 | PASS (<=8) |
| `WireNewPanel` | `private void WireNewPanel(TradeCopierPanel panel, Chart chart, ChartTrader chartTrader, System.Windows.Controls.Grid grid)` | 487-521 | 4 | PASS (<=8) |

### Parent Method After Extraction

| Method | Lines | CCN Before | CCN After | Status |
|--------|-------|-----------|----------|--------|
| `DoInject` | 524-552 | 43 | 4 | PASS (<=8) |

### Extraction Details

**`PurgeStalePanel`** (CCN=2):
- Private instance method on `TradeCopierAddOn`
- Takes `System.Windows.Controls.Grid grid`
- Null-guards grid, then delegates to existing static `TryDetachAndRemoveStalePanels(grid)`
- Preserves all stale panel removal logic (collect + descending sort + per-child remove + RowDefinition guard)

**`WireNewPanel`** (CCN=4):
- Private instance method on `TradeCopierAddOn`
- Takes `TradeCopierPanel panel, Chart chart, ChartTrader chartTrader, System.Windows.Controls.Grid grid`
- Contains: `TrySetPanelInstrument` call, `StartAtrEngine`, `SetChart`, `WireLeaderAccount`, SIM101 handler wire, `RemoveSim101`, `HookKeyShortcut`, `InjectPanelIntoGrid`
- Pre-existing `MessageBox.Show` on grid-null path preserved verbatim
- No new try/catch added; outer try/catch in DoInject retained

**Parent `DoInject`** (CCN=4):
- Retains `_panels.TryAdd` guard + outer try/catch
- Inside try: FindVisualChild + null check, cast to Grid, `PurgeStalePanel(grid)`, `new TradeCopierPanel()`, `WireNewPanel(panel, chart, chartTrader, grid)`
- Catch: TryRemove + MessageBox preserved verbatim

---

## Tests Added

**File**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Tests added**: 5 [Fact] tests

| Test Name | Description |
|-----------|-------------|
| `T_C07_01_PurgeStalePanel_RemovesChildByTypeName_TradeCopierPanel` | Verifies stale children matching type name "TradeCopierPanel" are counted/removed (2 removed) |
| `T_C07_02_PurgeStalePanel_RemovesRowDefinitionAtStaleRowIndex` | Verifies row at index 2 (index > 0) is removed |
| `T_C07_03_PurgeStalePanel_DoesNotRemoveRow0` | Verifies row at index 0 is NOT removed (guard: index > 0) |
| `T_C07_04_WireNewPanel_NullInstrument_DoesNotThrow` | Verifies null instrument path completes without exception (pre-existing try/catch) |
| `T_C07_05_DoInject_DuplicateChart_ReturnsFalseOnTryAdd` | Verifies duplicate chart key causes TryAdd to return false (early return path) |

---

## 7-Scan Results

### Scan 1 -- JS-021 lock()
```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "lock\("
```
**Result**: 0 hits (non-comment). PASS.

### Scan 2 -- JS-001 throw new
```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "throw new"
```
**Result**: 0 hits. PASS. (No throw new in PurgeStalePanel or WireNewPanel.)

### Scan 3 -- JS-002 return null
```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "return null"
```
**Result**: 8 hits -- ALL pre-existing in static visual tree helpers (FindVisualChild, FindAccountComboBox, FindVisualChildByIndex). Zero new return null in PurgeStalePanel or WireNewPanel. PASS.

### Scan 4 -- JS-033 async void
```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "async void"
```
**Result**: 0 hits. PASS.

### Scan 5 -- CCN (lizard)
```
lizard src/PropTraderTools/TradeCopierAddOn.cs --csv
```
**Results**:
- `DoInject`: CCN = 4 (was 43). PASS (<=8).
- `PurgeStalePanel`: CCN = 2. PASS (<=8).
- `WireNewPanel`: CCN = 4. PASS (<=8).

### Scan 6 -- throw (any, in new helpers)
```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "\bthrow\b"
```
**Result**: 0 hits (no throw keyword outside comments). PASS.

### Scan 7 -- Build
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Result**: Build succeeded. 0 errors, 0 warnings. PASS.

---

## Test Results

```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build
```
**Result**: Passed! Failed: 0, Passed: 228, Skipped: 3, Total: 231
**Hard floor**: >= 127. PASS (228 >= 127).

---

## BUILD_PASS
