# Ticket C-01 Completion Report (RETRY — Tests Added)
**Ticket**: C-01 -- FollowerItem::BuildBufferedButtonsRow
**File modified**: `src/PropTraderTools/TradeCopierPanel.cs`
**Test file created**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Engineer**: ptt-engineer (PTT Pipeline Phase 4a)
**Date**: 2026-09-07 (retry -- tests missing from prior session)
**Epic**: WAVE1-LANE-C

---

## Scope

**Ticket C-01 ONLY.** No other tickets touched.

---

## What Was Implemented

### Extraction (completed in prior session -- NOT re-done)

The following helpers were already extracted in the prior session and remain intact:

| Helper | Lines | CCN | Visibility |
|--------|-------|-----|-----------|
| `BuildBufferedButtonsRow` (parent, refactored) | 1131-1167 | 1 | private instance |
| `BuildSingleButtonCluster` (extracted helper) | 1171-1219 | 3 | private instance |
| `BuildQuickT3HiddenRow` (extracted helper) | 1223-1240 | 1 | private instance |

### Tests Added (this session)

**File**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`

The test project targets `net8.0`; the main project targets `net48` (NT8 requirement). Direct
`ProjectReference` is impossible across TFMs. Tests follow the established inline-mirror pattern
(see `B140Tests.cs`, `B143Tests.cs`). WPF types are replaced by inline state-tracking logic that
mirrors the production conditional logic exactly.

| Test ID | Method Name | What It Verifies |
|---------|-------------|-----------------|
| T_C01_01 | `T_C01_01_BuildSingleButtonCluster_TealTrue_SetsBorderBrushToTeal` | `isTeal=true` branch sets BorderBrush (lines 1207-1211) |
| T_C01_02 | `T_C01_02_BuildSingleButtonCluster_TealFalse_DoesNotSetTealBorderBrush` | `isTeal=false` skips BorderBrush assignment |
| T_C01_03 | `T_C01_03_BuildQuickT3HiddenRow_AddsCollapsedRowToRoot` | `_quickT3Row` constructed with `Visibility=Collapsed` (line 1229) |
| T_C01_04 | `T_C01_04_BuildSingleButtonCluster_StoreAction_AssignsButtonReference` | `storeAction(btn)` receives non-null reference (line 1217) |

---

## 7-Scan Results

### Scan 1 -- JS-021 lock() (TradeCopierPanel.cs)
```
Command: Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\("
Result:  3 hits -- ALL comments only (lines 1170, 1222, 1315)
         Zero live lock() calls in C-01 helpers or anywhere in file.
```
**PASS** -- 0 live hits

### Scan 2 -- JS-001 throw new (TradeCopierPanel.cs)
```
Command: Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "throw new"
Result:  (no output) -- zero hits
```
**PASS** -- 0 hits

### Scan 3 -- JS-002 return null (TradeCopierPanel.cs)
```
Command: Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "return null"
Result:  Hits at lines 505, 565, 570, 574, 1977, 1987 -- all pre-existing code in other methods.
         C-01 helpers (lines 1131-1240) are void return -- no return null possible.
         Comment-only hits at 1170, 1222 (documentation guards for C-01 helpers).
         Zero new return null in C-01 helpers.
```
**PASS** -- 0 new hits in C-01 scope

### Scan 4 -- JS-033 async void (TradeCopierPanel.cs)
```
Command: Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "async void"
Result:  9 hits -- ALL comments only (lines 1315, 1375, 1613, 1759, 2045, 2175, 2197, 2239, 2785)
         Zero live async void declarations anywhere.
```
**PASS** -- 0 live hits

### Scan 5 -- CYC lizard (TradeCopierPanel.cs)
```
Command: lizard src/PropTraderTools/TradeCopierPanel.cs --csv -x "*/bin/*" -x "*/obj/*"
         (filtered to C-01 methods)
Result:
  BuildBufferedButtonsRow  -- NLOC=33, CCN=1  (lines 1131-1167)
  BuildSingleButtonCluster -- NLOC=49, CCN=3  (lines 1171-1219)
  BuildQuickT3HiddenRow    -- NLOC=18, CCN=1  (lines 1223-1240)
  All CCN <= 8.
```
**PASS** -- all <= 8

### Scan 6 -- JS-001 throw (non-comment) (TradeCopierPanel.cs)
```
Command: Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "throw " |
         Where-Object { $_.Line -notmatch "^\s*//" }
Result:  (no output) -- zero live throw statements in entire file
```
**PASS** -- 0 hits

### Scan 7 -- dotnet build
```
Command: dotnet build src/PropTraderTools/PropTraderTools.csproj --nologo
Result:
  All projects are up-to-date for restore.
  PropTraderTools -> ...\bin\Debug\PropTraderTools.dll
  Build succeeded.
  0 Warning(s)
  0 Error(s)
```
**PASS** -- zero errors

---

## Test Run Result

```
Command: dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --nologo
Result:
  Passed!  - Failed: 0, Passed: 131, Skipped: 3, Total: 134, Duration: 44 ms
```

**Baseline**: 127 passing (pre-C-01 tests).
**After C-01 tests added**: 131 passing (+4 new C-01 tests), 0 failing.

---

## Scan Summary Table

| Scan | Description | Result |
|------|-------------|--------|
| Scan 1 | lock() -- JS-021 | PASS (3 comment-only hits, 0 live) |
| Scan 2 | throw new -- JS-001 | PASS (0 hits) |
| Scan 3 | return null -- JS-002 | PASS (0 new in C-01 helpers) |
| Scan 4 | async void -- JS-033 | PASS (0 live hits) |
| Scan 5 | lizard CCN <= 8 | PASS (max CCN=3 across all 3 methods) |
| Scan 6 | throw (non-comment) | PASS (0 hits) |
| Scan 7 | dotnet build | PASS (0 errors, 0 warnings) |

---

BUILD_PASS
