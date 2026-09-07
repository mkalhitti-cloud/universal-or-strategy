# Ticket C-07 Verification Report (Cycle 2)
**Ticket**: C-07 — TradeCopierAddOn::DoInject
**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Test File**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Verifier**: ptt-verifier (independent re-run)
**Cycle**: 2 (prior VERIFY_FAIL fixed — NinjaTrader.Cbi.Instrument removed from test)
**Date**: 2026-09-07

---

## Prior Failure Resolution

**Prior VERIFY_FAIL Cause**: `T_C07_04` used `NinjaTrader.Cbi.Instrument instr = null` which
failed to compile in the net8.0 test project (no NT8 dependency available).

**Fix Verified**:
- `Select-String -Pattern "NinjaTrader" tests/PropTraderTools.Tests/Wave1LaneCTests.cs` → **0 hits**
- Line 556 now reads: `object instr = null;` (same semantics, no NT8 reference)
- Suite compiles and runs clean.

---

## Check 1 — JS-021: lock() Scan

```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "lock\("
```

**Result**: 0 hits.
**Status**: ✅ PASS

---

## Check 2 — CCN (lizard)

```
lizard src/PropTraderTools/TradeCopierAddOn.cs --csv -x "*/bin/*" -x "*/obj/*"
| grep -E "DoInject|PurgeStalePanel|WireNewPanel"
```

**Results**:

| Method | Lines | CCN | Threshold | Status |
|--------|-------|-----|-----------|--------|
| `PurgeStalePanel` | 478-483 | **2** | ≤8 | ✅ PASS |
| `WireNewPanel` | 487-521 | **4** | ≤8 | ✅ PASS |
| `DoInject` | 524-552 | **4** | ≤8 | ✅ PASS |

**Status**: ✅ PASS — all three methods ≤8.

---

## Check 3 — Helper Method Signatures (private instance, not static)

```
Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs"
  -Pattern "private\s+(void|bool)\s+(PurgeStalePanel|WireNewPanel)"
```

**Results**:
- Line 478: `private void PurgeStalePanel(System.Windows.Controls.Grid grid)`
- Line 487: `private void WireNewPanel(TradeCopierPanel panel, Chart chart, ChartTrader chartTrader, System.Windows.Controls.Grid grid)`

Static check: `static.*PurgeStalePanel|static.*WireNewPanel` → **0 hits**

**Status**: ✅ PASS — both helpers are private instance methods on `TradeCopierAddOn`.

---

## Check 4 — No New Dispatcher.InvokeAsync / Task.Run / throw

Scanned lines 478-552 (PurgeStalePanel, WireNewPanel, DoInject bodies):

- `Dispatcher.InvokeAsync` hits at lines 189, 204, 283, 338 — all pre-existing in `TryInject`/`InjectIntoChart` paths, outside C-07 scope.
- No `Dispatcher.InvokeAsync` or `Task.Run` inside `PurgeStalePanel`, `WireNewPanel`, or the new `DoInject` body.
- `throw new` → **0 hits** across entire file.
- `throw` (bare) → **0 hits** in C-07 scope.

**Status**: ✅ PASS — no new async dispatch or throw statements added.

---

## Check 5 — Scope: Only C-07 Files Modified

C-07 touches:
- `src/PropTraderTools/TradeCopierAddOn.cs` — helpers added at lines 478-521, DoInject refactored at lines 524-552. ✅
- `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` — T_C07_01 through T_C07_05 added. ✅

Note: `TradeCopierPanel.cs` and `TradeCopierWindow.cs` show as modified in git working tree
but those changes belong to other LANE-C tickets (C-08, C-09+). C-07 only touched
`TradeCopierAddOn.cs` lines 478-552 and the test file. Scope is clean for C-07.

**Status**: ✅ PASS

---

## Check 6 — dotnet test: T_C07_01 through T_C07_05 Present and Passing

```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build
```

**T_C07 tests confirmed present** (via Select-String):
- `T_C07_01_PurgeStalePanel_RemovesChildByTypeName_TradeCopierPanel` — line 582
- `T_C07_02_PurgeStalePanel_RemovesRowDefinitionAtStaleRowIndex` — line 592
- `T_C07_03_PurgeStalePanel_DoesNotRemoveRow0` — line 602
- `T_C07_04_WireNewPanel_NullInstrument_DoesNotThrow` — line 612
- `T_C07_05_DoInject_DuplicateChart_ReturnsFalseOnTryAdd` — line 622

**Test run result**:
```
Passed! - Failed: 0, Passed: 233, Skipped: 3, Total: 236
```

**Hard floor**: ≥127. Actual: 233. ✅
**Failures**: 0. ✅

**Status**: ✅ PASS

---

## Check 7 — JS DNA Rules

| Rule | Check | Result | Status |
|------|-------|--------|--------|
| JS-021 | `lock(` in file | 0 hits | ✅ PASS |
| JS-001 | `throw new \w+Exception` | 0 hits | ✅ PASS |
| JS-002 | `return null` in C-07 scope (lines 478-552) | 0 hits | ✅ PASS |
| JS-033 | `async void \w` | 0 hits | ✅ PASS |
| JS-080 | CYC ≤ 8 for all 3 methods | 2/4/4 | ✅ PASS |
| JS-096 | No magic string for mode/state | N/A — no state discriminators added | ✅ PASS |
| JS-066 | Pre-existing try/catch preserved verbatim in WireNewPanel | Confirmed | ✅ PASS |

**Status**: ✅ ALL PASS

---

## Check 8 — Behavioural Integrity

**Orchestration chain verified**:
- `InjectIntoChart` → `chart.Dispatcher.InvokeAsync(() => DoInject(chart))` (line 189) — unchanged
- `OnChartLoaded` → `chart.Dispatcher.InvokeAsync(() => DoInject(chart))` (line 204) — unchanged
- `DoInject` now calls `PurgeStalePanel(grid)` then `WireNewPanel(panel, chart, chartTrader, grid)` — same logical sequence as pre-extraction, only refactored into named helpers.

**Pre-existing catch block** (lines 544-551): `_panels.TryRemove + MessageBox.Show` preserved verbatim.
**Pre-existing `_panels.TryAdd` guard** (line 526): preserved verbatim.
**Pre-existing `MessageBox.Show` on null grid** (lines 516-520 inside `WireNewPanel`): preserved verbatim.

**Status**: ✅ PASS — DoInject orchestration identical pre/post extraction.

---

## Summary

| Check | Description | Result |
|-------|-------------|--------|
| Fix Verification | `NinjaTrader` reference removed from test | ✅ PASS |
| Check 1 | lock() scan | ✅ PASS (0 hits) |
| Check 2 | CCN: DoInject=4, PurgeStalePanel=2, WireNewPanel=4 | ✅ PASS |
| Check 3 | Helpers are private instance methods | ✅ PASS |
| Check 4 | No new Dispatcher.InvokeAsync / Task.Run / throw | ✅ PASS |
| Check 5 | Scope — only C-07 files touched | ✅ PASS |
| Check 6 | 233 passing, 0 failures; T_C07_01–05 present | ✅ PASS |
| Check 7 | JS-021/001/002/033/080/096/066 | ✅ ALL PASS |
| Check 8 | Behavioural integrity preserved | ✅ PASS |

---

## VERDICT: VERIFY_PASS