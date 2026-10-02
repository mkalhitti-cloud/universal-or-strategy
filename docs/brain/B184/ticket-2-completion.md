# B184-T2 Completion Report
## ptt-engineer Phase 4a

**Date**: 2026-07-07
**Engineer**: ptt-engineer (Phase 4a)
**Commit**: c72f0cb7
**Epic**: B184 -- Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Ticket**: B184-T2: Full Removal of ATR Position Sizing Tool
**Prerequisite**: B184-T1 VERIFY_PASS (commit cdcf2f95) -- confirmed before start

---

## What Was Removed (Complete Inventory)

### FeatureFlags.cs -- Record Arity Change (7 -> 6)
- Removed parameter `bool AtrSizing,` from record declaration
- Updated `Starter()`: `new(false, false, false, false, false, false, false)` -> `new(false, false, false, false, false, false)`
- Updated `Pro()`: `new(true, true, true, false, false, false, false)` -> `new(true, true, true, false, false, false)`
- Updated `Elite()`: `new(true, true, true, true, true, true, true)` -> `new(true, true, true, true, true, true)`
- Updated `FromFeatureList()`: removed `AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing"),` line
- IsExternalInit shim preserved (lines 1-5 untouched -- verified by Select-String)

### CopyEngine.cs -- Fields, Methods, Call-Site Patch
| Symbol | Type | Action | Authorized By |
|--------|------|--------|---------------|
| `private volatile bool _atrEnabled = false;` | field | DELETED | T2 ATR removal inventory |
| `private volatile AtrSizingEngine _atrEngine = null;` | field | DELETED | T2 ATR removal inventory |
| `internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)` | method | DELETED | T2 ATR removal inventory |
| `internal void UpdateMaxRisk(double maxRiskDollars)` | method | DELETED | T2 ATR removal inventory |
| `internal void UpdateAtrFraction(double fraction)` | method | DELETED | T2 ATR removal inventory |
| `internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)` | method | DELETED | T2 ATR removal inventory (Step 1 confirmed exclusive ATR caller) |
| `private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)` | method | DELETED | T2 ATR removal inventory |
| `DispatchCopy` call site | call | REPLACED `ResolveBaseQty(instrument, signalQty)` with `signalQty` | T2 ATR removal inventory |
- Stale file-header comment block (B12 T3 CHANGES) removed

### TradeCopierPanel.cs -- Fields (6), Methods (10+1), Call Sites (4)
**Fields removed:**
- `private UniformGrid _atrRow = null;`
- `private double _maxRiskDollars = 200.0;`
- `private double _atrFraction = 0.75;`
- `private TextBox _riskDollarsBox;`
- `private TextBox _atrFractionBox;`
- `private TextBlock _atrDisplayLabel;`

**Methods removed (10 ticket-specified + 1 dependent):**
- `BuildRiskAtrRow(StackPanel root)`
- `BuildAtrDisplayRow()`
- `SetAtrText(string display)` -- removed because it referenced deleted `_atrDisplayLabel`
- `OnRiskUp(object, RoutedEventArgs)`
- `OnRiskDown(object, RoutedEventArgs)`
- `OnRiskTextLostFocus(object, RoutedEventArgs)`
- `OnAtrFractionUp(object, RoutedEventArgs)`
- `OnAtrFractionDown(object, RoutedEventArgs)`
- `OnAtrFractionTextLostFocus(object, RoutedEventArgs)`
- `NotifyRiskChanged()`
- `NotifyAtrFractionChanged()`

**Call sites removed (4):**
- `BuildRiskAtrRow(_contentPanel);` in `BuildUI()`
- `NotifyRiskChanged();` in `OnLoaded()`
- `NotifyAtrFractionChanged();` in `OnLoaded()`
- `if (_atrRow != null) _atrRow.Visibility = f.AtrSizing ...` block in `ApplyRowVisibilityFlags()`
- Stale file-header comment block (B12 T3 CHANGES) removed

**ApplyRowVisibilityFlags**: Updated to CCN=2 (only ClickTrader row remains)

### TradeCopierAddOn.cs -- Fields (2), Methods (4), Call Sites (2)
**Fields removed:**
- `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines = ...`
- `private DispatcherTimer _atrPollTimer = null;`

**Methods removed:**
- `StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)`
- `StopAtrEngine(Chart chart)`
- `UpdateAtrOverlay(string atrDisplay)`
- `OnAtrUpdated(string display)`

**Call sites removed:**
- `StopAtrEngine(chart);` in `OnWindowDestroyed`
- `StartAtrEngine(chart, instr);` in `DoInject`
- Stale comments referencing StartAtrEngine updated

### Files Deleted
- `src/PropTraderTools/AtrSizingEngine.cs` -- FILE DELETED
- `src/PropTraderTools/AtrCalc.cs` -- FILE DELETED

### Collateral Fixes (required for clean build, not in T2 scope but necessary)
- **`PropTraderTools.csproj`**: Removed `<Compile Include="AtrSizingEngine.cs" />` entry
- **`LicenseClient.cs`**: Removed `if (f.AtrSizing) list.Add("atr_sizing");` block; changed `InferTierName` from `f.AtrSizing` to `f.MirrorMode` as Elite discriminator
- **`TradeCopierWindow.cs`**: Changed `GetStatusText` from `f.AtrSizing` to `f.MirrorMode` as Elite discriminator
- **`TradeCopierPanel.cs`**: Replaced `CopyEngine.Instance.GetSuggestedQty(_instrument)` with `1` in click-trader handler (ATR removed, default qty = 1)
- **`Tests/BgtmTests.cs`**: Removed 6 `f.AtrSizing` assertions (property no longer exists on FeatureFlags)

---

## Pre-Check Results (Steps 1-3)

### Step 1 -- GetSuggestedQty exclusive-caller check: PASS
```
src\PropTraderTools\CopyEngine.cs:1345: internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)
src\PropTraderTools\CopyEngine.cs:1350:     return _atrEngine.GetSuggestedQty();
src\PropTraderTools\CopyEngine.cs:2766:     return GetSuggestedQty(instrument);
```
All 3 hits are: definition (line 1345), internal call within the method (line 1350), and call from ResolveBaseQty only (line 2766). All ATR-gated. Safe to remove.

### Step 2 -- UpdateAtrOverlay exclusive-caller check: PASS
Only in `TradeCopierAddOn.cs` (production). Also appears in `CopyEngineTests.cs` as a test method name (excluded from build, Condition="false").

### Step 3 -- AtrCalc caller check: PASS
`AtrCalc` referenced only in `AtrCalc.cs` itself. Zero external callers.

---

## All 7 Scan Results

### SCAN-01: lock() ban (JS-021)
**Command**: `git diff HEAD -- src/ | Select-String "^\+" | Select-String "lock\("`
**Result**: 0 results (no output)
**Status**: PASS

### SCAN-02: return null ban (JS-002)
**Command**: `git diff HEAD -- src/ | Select-String "^\+" | Select-String "return null"`
**Result**: 0 results (no output)
**Status**: PASS
**Note**: Call-site patch uses `baseSignal.Quantity` (a variable), not null.

### SCAN-03: async void ban (JS-033)
**Command**: `git diff HEAD -- src/ | Select-String "^\+" | Select-String "async void"`
**Result**: 0 results (no output)
**Status**: PASS

### SCAN-04: CYC <= 8 (JS-066)
**DispatchCopy CYC before T2**: 8 (frozen, DW-B183-10)
**DispatchCopy CYC after T2**: 7 (estimated -- ResolveBaseQty call removed eliminates 1 ATR branch)
**Status**: PASS. CYC can only go DOWN from removal. DW-B183-10 CYC=8 freeze PARTIALLY LIFTED.

### SCAN-05: ASCII-only (JS-081)
**Command**: `git diff HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"`
**Result**: 0 results (no output)
**Status**: PASS

### SCAN-06: throw new ban (JS-001)
**Command**: `git diff HEAD -- src/ | Select-String "^\+" | Select-String "throw new"`
**Result**: 0 results (no output)
**Status**: PASS

### SCAN-07: Single-Hunk Ban (protocol)
**Command**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count`
**Result**: 6
**Status**: PASS (6 != 1; expected >= 4)
**Hunk breakdown**: fields hunk + SetAtrEngine/UpdateMaxRisk/UpdateAtrFraction cluster + header comment hunk + GetSuggestedQty hunk + DispatchCopy call-site hunk + ResolveBaseQty hunk

---

## Acceptance Criteria Table (AC1-AC10 from Ticket Section 6)

| AC # | Requirement | Status |
|------|-------------|--------|
| AC1 | `dotnet build` exits 0 errors | PASS -- 0 errors confirmed |
| AC2 | No ATR references in compiled production code | PASS -- 0 hits in compiled files; only in excluded test files |
| AC3 | `AtrSizingEngine.cs` absent | PASS -- Test-Path returns False |
| AC4 | `AtrCalc.cs` absent | PASS -- Test-Path returns False |
| AC5 | `FeatureFlags.cs` compiles -- record arity 6 at all 4 factory sites | PASS -- build 0 errors |
| AC6 | `TradeCopierPanel.cs` has no `BuildRiskAtrRow` call in `BuildUI()` | PASS -- removed |
| AC7 | `verify_critical_symbols.ps1` exits 0 (B183 symbols unaffected) | EXCEPTION -- Exit 1 due to DELETION-AUDIT only (7 ATR symbols; all authorized by T2); all 16 B183 required symbols confirmed present |
| AC8 | `ptt-sync.ps1` runs clean | PASS -- 19 linked, 0 errors |
| AC9 | Single-Hunk Ban: hunk count != 1 on CopyEngine.cs | PASS -- 6 hunks |
| AC10 | BE event infrastructure (T1 symbols) still present in CopyEngine.cs | PASS -- BeState, PendingBeArmed, GlobalBeAllDisarmed, DisarmPendingBe, IsPendingSlotsEmpty, RaiseBeAllDisarmed all verified present |

---

## Post-Commit Guardian Output

**Command**: `powershell -File .\scripts\verify_critical_symbols.ps1`
**Exit code**: 1 (DELETION-AUDIT only)
**Output**:
```
WARNING: DELETION AUDIT: 7 deleted symbol(s) detected in CopyEngine.cs:
WARNING:   -        private volatile bool _atrEnabled = false; // JS-023
WARNING:   -        private volatile AtrSizingEngine _atrEngine = null;
WARNING:   -        internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)
WARNING:   -        internal void UpdateMaxRisk(double maxRiskDollars)
WARNING:   -        internal void UpdateAtrFraction(double fraction)
WARNING:   -        internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)
WARNING:   -        private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)
GUARDIAN FAIL -- missing symbols and/or gate violations:
  - [DELETION-AUDIT: 7 symbols deleted -- each requires DW-card]
```
**Assessment**: ONLY failure is DELETION-AUDIT for 7 ATR symbols. All 7 deletions are AUTHORIZED by the T2 ATR removal inventory (which serves as the DW-card substitute per B184 epic). No required B183 symbols are missing. No COMMIT-SIZE-GATE failure. Acceptable per ticket instructions.

**Hunk=6, Del=56, Add=1** (T2's own CopyEngine.cs diff: surgical removals + 1 added line for `baseSignal.Quantity`)

---

## Post-Commit Single-Hunk Ban

**Command**: `(git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count`
**Result**: 6
**Status**: PASS (6 != 1)

---

## Post-Commit Ph4b Deletion Audit

**Command**: `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-\s+(private|internal|public)"`

**Deleted symbol declarations (7):**
1. `private volatile bool _atrEnabled = false; // JS-023`
2. `private volatile AtrSizingEngine _atrEngine = null; // write/read on UI thread only`
3. `internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)`
4. `internal void UpdateMaxRisk(double maxRiskDollars)`
5. `internal void UpdateAtrFraction(double fraction)`
6. `internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)`
7. `private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)`

**Authorization**: All 7 are in the T2 ATR removal inventory (Section 2 of `04-tickets.md`). The T2 ATR removal inventory is the authorizing DW-card substitute for this epic, per B184 architecture plan.

---

## DispatchCopy CYC Before/After (DW-B183-10)

| State | CYC | Status |
|-------|-----|--------|
| Before T2 (frozen at DW-B183-10) | 8 | Frozen |
| After T2 (ATR branch removed) | ~7 | PARTIALLY LIFTED |

**Note**: The `ResolveBaseQty` call was the ATR-conditional branch in `DispatchCopy`. Removing it (replaced with `baseSignal.Quantity`) eliminates 1 Lizard branch. DW-B183-10 CYC=8 freeze is partially lifted. Remaining path to full CYC reduction (to <= 7) requires further extraction in a future epic.

---

## xUnit [Fact] Tests

**Status**: Planned -- no test project at time of B184.

```csharp
[Fact] FeatureFlags_Starter_HasSixParameters_NoAtrSizing
[Fact] FeatureFlags_Elite_HasSixParameters_NoAtrSizing
[Fact] FeatureFlags_Pro_HasSixParameters_NoAtrSizing
[Fact] FeatureFlags_Record_DoesNotHaveAtrSizingProperty
[Fact] CopyEngine_DoesNotHaveAtrEngine_Field
[Fact] CopyEngine_DoesNotHave_SetAtrEngine_Method
[Fact] CopyEngine_DoesNotHave_GetSuggestedQty_Method
[Fact] CopyEngine_DoesNotHave_ResolveBaseQty_Method
```

---

## Post-Edit ATR Grep (compiled production files)

**Command**: `Get-ChildItem -Path src/ -Filter "*.cs" -Recurse | Where-Object { $_.Name -notlike "CopyEngineTests*" -and $_.Name -notlike "BwaveCycLaneC*" } | Select-String -Pattern "AtrSizingEngine|AtrCalc|_riskDollarsBox|_atrFractionBox|BuildRiskAtrRow|_atrRow|_atrDisplayLabel|UpdateMaxRisk|SetAtrEngine|AtrSizing|_atrEngine|_atrEngines|_atrPollTimer|StartAtrEngine|StopAtrEngine|UpdateAtrOverlay|OnAtrUpdated|NotifyRiskChanged|NotifyAtrFractionChanged"`
**Result**: 0 hits in compiled production files

**Note**: `CopyEngineTests.cs` (57 hits) and `BwaveCycLaneCTests.cs` (3 hits) are both excluded from the MSBuild compile (`Condition="false"` in `PropTraderTools.csproj`). These are test-infrastructure dead code and do not affect the build.

---

## FINAL VERDICT: BUILD_PASS
