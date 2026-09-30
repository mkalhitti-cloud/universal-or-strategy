# B172 Ticket 3 Completion — Test Cleanout + New Tests

**Epic:** B172 — ATR Display Widget Greenfield Replacement
**Ticket:** T3 — Test Cleanout + New Tests
**Engineer:** ptt-engineer (Phase 4a)
**Date:** 2025-01-28
**Status:** BUILD_PASS

---

## Prerequisites Verified

- T1 compile-clean: confirmed (AtrSizingEngine stripped, CopyEngine ATR members removed)
- T2 compile-clean: confirmed (AtrWidget built, TradeCopierPanel/AddOn ATR wiring removed)
- Prerequisite note: T1 engineer already deleted all 20 target test methods as part of compile-clean fix.

---

## Part A — Deletion Status

All 20 target test methods were confirmed absent from `CopyEngineTests.cs` before T3 work began.
The T1 engineer deleted all of them as part of the compile-clean fix.

### Confirmed absent (T1 already deleted):

| Method | Series | Status |
|---|---|---|
| `CalcContracts_MES_ATR6_returns5` | T-B9-01 | ALREADY DELETED (T1) |
| `CalcContracts_MES_ATR8_returns3` | T-B9-02 | ALREADY DELETED (T1) |
| `CalcContracts_MES_ATR12_returns2` | T-B9-03 | ALREADY DELETED (T1) |
| `CalcContracts_ZeroAtr_returns1` | T-B9-04 | ALREADY DELETED (T1) |
| `CalcContracts_NegativeAtr_returns1` | T-B9-05 | ALREADY DELETED (T1) |
| `CalcContracts_ResultBelowOne_clampsTo1` | T-B9-06 | ALREADY DELETED (T1) |
| `CalcContracts_ZeroTickValue_returns1` | T-B9-07 | ALREADY DELETED (T1) |
| `CalcContracts_LargeMaxRisk_noOverflow` | T-B9-08 | ALREADY DELETED (T1) |
| `GetSuggestedQty_returns1_when_no_engine` | T-B9-09 | ALREADY DELETED (T1) |
| `GetSuggestedQty_returns_engine_qty_when_set` | T-B9-10 | ALREADY DELETED (T1) |
| `ClickTrader_atr_disabled_fallback_qty_is_1` | T-B9-12 | ALREADY DELETED (T1) |
| `ClickTrader_atr_enabled_uses_engine_qty` | T-B9-13 | ALREADY DELETED (T1) |
| `StartAtrEngine_NullChart_DoesNotThrow` | T-B11-T2-01 | ALREADY DELETED (T1) |
| `StartAtrEngine_NullInstrument_DoesNotThrow` | T-B11-T2-02 | ALREADY DELETED (T1) |
| `UpdateAtrOverlay_FormatsDisplayString_CorrectText` | T-B11-T2-03 | ALREADY DELETED (T1) |
| `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1` | T-B12-T3-01 | ALREADY DELETED (T1) |
| `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` | T-B12-T3-02 | ALREADY DELETED (T1) |
| `BuildRiskAtrRow_ClampMin_RejectsSubMinValue` | T-B12-T3-03 | ALREADY DELETED (T1) |
| `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` | T-B12-T3 | ALREADY DELETED (T1) |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` | B21-LANE-A | ALREADY DELETED (T1) |

**Additional T3 deletions by this engineer:** 0 (all 20 were already gone).

---

## Part B — New Tests Added

Three new test methods were added in a standalone `B172AtrWidgetTests` class (no `CopyEngine.Instance`
dependency — pure arithmetic, no NT8 runtime required, no `[Skip]`).

| Test | Identifier | Class | Status |
|---|---|---|---|
| `Ceiling_AlwaysRoundsUp` | T-B172-T3-01 | `B172AtrWidgetTests` | ADDED + PASS |
| `AtrFractionBox_ParseAndClamp_ClampsToRange` | T-B172-T3-02 | `B172AtrWidgetTests` | ADDED + PASS |
| `FormatDisplay_CorrectFormat` | T-B172-T3-03 | `B172AtrWidgetTests` | ADDED + PASS |

**Placement note:** Added as a new top-level class `B172AtrWidgetTests` at the end of `CopyEngineTests.cs`
(after `DwB170Tests`), before the namespace closing brace. Standalone class required because all existing
test classes have `private readonly CopyEngine _engine = CopyEngine.Instance` in their constructors,
which requires the NT8 AgileDotNetRT host. The 3 new tests are pure arithmetic and must NOT fail
due to an NT8 initialization error.

---

## 7-Scan Results

### SCAN-01: lock() check
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\(" -> results are ALL in comments ("// no lock()", "// lock-free", etc.)
```
**Result: PASS — 0 actual lock() statements.**

### SCAN-02: Deleted symbol grep (CopyEngineTests.cs)
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "CalcContracts|SetAtrFraction|UpdateMaxRisk|UpdateAtrFraction|SetAtrEngine|ManualOnBarUpdate|GetSuggestedQty|FireAtrUpdated|AtrUpdated|BuildRiskAtrRow|NotifyAtrFractionChanged|NotifyRiskChanged" -> 0 matches
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "AtrSizingEngine" -> 0 matches
```
**Result: PASS — 0 matches for all deleted symbol names.**

### SCAN-03: volatile double check
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "volatile double" -> 2 matches, both in COMMENTS:
  CopyEngine.cs:461      -- "// volatile double banned"
  TradeCopierPanel.cs:3243 -- "// no volatile double"
```
**Result: PASS — 0 actual `volatile double` field declarations.**

### SCAN-04: Non-ASCII string literals in new tests
```
New string literals in B172AtrWidgetTests: " pts", "abc", "F2" (not in tests), "2 pts", "1 pts"
All pure ASCII. Manual review confirmed.
```
**Result: PASS — 0 non-ASCII violations.**

### SCAN-05: CYC check on new test methods
```
Ceiling_AlwaysRoundsUp      -> straight-line assertions, no branches. CYC=1.
AtrFractionBox_ParseAndClamp_ClampsToRange -> straight-line assertions, no branches. CYC=1.
FormatDisplay_CorrectFormat -> straight-line assertions, no branches. CYC=1.
```
**Result: PASS — all 3 new tests CYC=1 (well within CYC<=8 gate).**

### SCAN-06: WPF brush code in tests
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "new Solid|new Linear|new Radial" -> 0 matches
```
**Result: N/A PASS — no brush construction in test file.**

### SCAN-07: AtrSizingEngine in CopyEngineTests.cs
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "AtrSizingEngine" -> 0 matches
```
**Result: PASS — 0 matches.**

---

## Build Result

```
dotnet build src/PropTraderTools/
  Errors:   0
  Warnings: 1140 (all pre-existing xUnit1004 [Fact(Skip=...)] warnings -- unchanged count)
  Time:     ~2.4s
```

**BUILD: PASS (0 errors)**

---

## Test Result

```
dotnet test src/PropTraderTools/ --no-build
  Failed:  8   (pre-existing NT8-runtime TypeInitializationException failures -- unchanged)
  Passed:  222 (219 pre-existing + 3 new B172AtrWidgetTests)
  Skipped: 358
  Total:   588
  Duration: ~960ms
```

**New tests specifically:**
```
dotnet test --filter "FullyQualifiedName~B172AtrWidgetTests"
  Failed:  0
  Passed:  3
  Skipped: 0
```

**TEST: PASS — 3 new tests all PASS. Pre-existing failure count unchanged (8).**

---

## ptt-sync.ps1 Result

```
powershell -File .\scripts\ptt-sync.ps1
  ptt-sync complete: 18 linked, 0 errors
```

**SYNC: PASS — 18 files linked.**

---

## Status: BUILD_PASS
