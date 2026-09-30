# BWAVE-CYC Lane C -- Ticket T3 Completion

**Engineer**: ptt-engineer
**Date**: 2025-01-30
**Wave**: BWAVE-CYC Complexity Reduction
**Lane**: C -- Panel / Window / AddOn
**Ticket**: T3 -- Panel: Feature Flag Visibility Switches
**Status**: BUILD_PASS

---

## What Was Implemented

### File Modified
`src/PropTraderTools/TradeCopierPanel.cs`

### Method 1: `TradeCopierPanel::ApplyFeatureFlags` (CCN 10 -> 1)

Extracted 3 private void helpers and refactored `ApplyFeatureFlags` to call them unconditionally:

**`ApplyTrimFlattenFlags(FeatureFlags f)`** -- CCN=4 (lizard)
- Sets `IsEnabled` on `_trimBtn2`, `_flattenBtn2`, `_cancelBtn2` with 3 null guards.
- Private instance method.

**`ApplyPositionControlFlags(FeatureFlags f)`** -- CCN=3 (lizard)
- Sets `IsEnabled` on `_beBtn2`, `_mirrorModeBtn` with 2 null guards.
- Private instance method.

**`ApplyRowVisibilityFlags(FeatureFlags f)`** -- CCN=5 (lizard)
- Sets `Visibility` on `_clickTraderRow` and `_atrRow` with 2 null guards + 2 ternary visibility assignments.
- Private instance method.

**Parent `ApplyFeatureFlags` after extraction**: CCN=1 (4 unconditional helper calls, no branches).

### Method 2: `TradeCopierPanel::ApplyFeatureFlagTooltips` (CCN 11 -> 1)

Extracted 1 private static helper and refactored `ApplyFeatureFlagTooltips` to use it:

**`SetButtonTooltip(System.Windows.Controls.Control btn, bool featureEnabled, string upgradeMessage)`** -- CCN=3 (lizard)
- Parameter type: `System.Windows.Controls.Control` (base class -- handles both `Button` and `RadioButton` fields).
- Body: `if (btn != null) btn.ToolTip = featureEnabled ? null : upgradeMessage;`
- Private static method. JS-002 compliant (no return null -- void).

**Parent `ApplyFeatureFlagTooltips` after extraction**: CCN=1 (5 unconditional `SetButtonTooltip(...)` calls, no branches).

### Test File Modified
`src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`

Added new class `BwaveCycT3HelperTests` with 7 [Fact] tests. The pre-existing stubs in `BwaveCycT3FeatureFlagTests` also now pass (7 stubs).

---

## DNA Rules Compliance

| Rule | Status |
|------|--------|
| JS-021 | No `lock()` -- PASS |
| JS-002 | No `return null` in new helpers (all void or use ternary) -- PASS |
| JS-033 | No `async void` -- PASS |
| CYC parent | `ApplyFeatureFlags`: CCN=1, `ApplyFeatureFlagTooltips`: CCN=1 -- PASS |
| CYC helpers | All 4 helpers <= 8 (max CCN=5 for `ApplyRowVisibilityFlags`) -- PASS |
| NT8 UI thread | Pure WPF property sets only; no Dispatcher, no Account/Order/Position -- PASS |
| ASCII-only | All identifiers and string literals ASCII -- PASS |
| Private only | Zero new public or internal surface (all helpers private) -- PASS |

---

## 7 Scans

### SCAN-01 -- lock check
Command: `Select-String "lock\(" C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs | Where-Object { $_.Line.Trim() -notmatch "^//" }`
**Result: 0 hits. PASS.**

### SCAN-02 -- async void check
Command: `Select-String "async void " C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs | Where-Object { $_.Line.Trim() -notmatch "^//" }`
**Result: 0 hits. PASS.**

### SCAN-03 -- return null check
Command: `(Select-String "return null" C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs).Count`
**Result: 14 pre-existing `return null` instances. No new instances added by T3. PASS.**

### SCAN-04 -- ASCII check
Command: `$f = Get-Content ...\TradeCopierPanel.cs -Raw; if ($f -match '[^\x00-\x7F]') { "NON-ASCII FOUND" } else { "ASCII OK" }`
**Result: ASCII OK. PASS.**

### SCAN-05a -- lizard CCN check (Panel file)
Command: `lizard C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs --CCN 8`
**T3 method results (CCN column = 2nd column):**
- `ApplyFeatureFlags`: NLOC=7, CCN=1 -- not in warnings. PASS.
- `ApplyTrimFlattenFlags`: NLOC=9, CCN=4 -- not in warnings. PASS.
- `ApplyPositionControlFlags`: NLOC=7, CCN=3 -- not in warnings. PASS.
- `ApplyRowVisibilityFlags`: NLOC=11, CCN=5 -- not in warnings. PASS.
- `ApplyFeatureFlagTooltips`: NLOC=8, CCN=1 -- not in warnings. PASS.
- `SetButtonTooltip`: NLOC=5, CCN=3 -- not in warnings. PASS.
**Result: All T3 methods absent from CCN > 8 warnings section. PASS.**

### SCAN-06 -- build
Command: `dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj -o bin\LaneC-T3`
**Result: Build succeeded. 0 Error(s). 1 pre-existing warning (xUnit2004 in B131Tests.cs -- unchanged). PASS.**

### SCAN-07 -- tests
Command: `dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build --filter "FullyQualifiedName~BwaveCycT3"`
**Result: Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: ~419 ms. PASS.**
- 7 new tests in `BwaveCycT3HelperTests`: all pass.
- 7 pre-existing stubs in `BwaveCycT3FeatureFlagTests`: all now pass (methods exist).
- 0 new failures introduced.

---

## Test Class and [Fact] Names

**Class**: `BwaveCycT3HelperTests` in `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`

| # | Test Name | Status |
|---|-----------|--------|
| 1 | `ApplyTrimFlattenFlags_SetsIsEnabled_PerTrimFlattenFlag` | PASS |
| 2 | `ApplyPositionControlFlags_SetsBeEnabled_PerBreakEvenFlag` | PASS |
| 3 | `ApplyRowVisibilityFlags_SetsCollapsed_WhenClickTraderFlagFalse` | PASS |
| 4 | `ApplyRowVisibilityFlags_SetsVisible_WhenAtrSizingFlagTrue` | PASS |
| 5 | `SetButtonTooltip_SetsUpgradeMessage_WhenFeatureDisabled` | PASS |
| 6 | `SetButtonTooltip_SetsNullTooltip_WhenFeatureEnabled` | PASS |
| 7 | `SetButtonTooltip_NoOp_WhenButtonNull` | PASS |

---

## Summary

- 4 helpers extracted (3 from `ApplyFeatureFlags`, 1 from `ApplyFeatureFlagTooltips`)
- `ApplyFeatureFlags` CCN: 10 -> 1
- `ApplyFeatureFlagTooltips` CCN: 11 -> 1
- All 7 scans: PASS
- 7 new [Fact] tests: 7/7 pass
- 0 new failures introduced
- All DNA rules satisfied

**BUILD_PASS**
