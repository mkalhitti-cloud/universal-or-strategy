# B172 Ticket 1 — Data Layer Cleanout — Completion Report

**Epic:** B172 — ATR Display Widget Greenfield Replacement  
**Ticket:** T1 — Data Layer Cleanout  
**Engineer:** ptt-engineer (Phase 4a)  
**Status:** BUILD_PASS  

---

## Changes Made

### src/PropTraderTools/AtrSizingEngine.cs

Stripped to pure ATR value provider. Deleted:

| Member | Kind | Action |
|---|---|---|
| File header comment block (lines 1–18) | comment | DELETED |
| `AtrSizingEngine(int testContracts)` | internal constructor | DELETED |
| `_lastContracts` | `private volatile int` | DELETED |
| `_maxRiskDollars` | `private double` | DELETED |
| `_tickDollarValue` | `private double` | DELETED |
| `_atrFraction` | `private double` | DELETED |
| `AtrUpdated` | `internal event Action<string>` | DELETED |
| `FireAtrUpdated(double, int)` | `private void` | DELETED |
| `SetAtrFraction(double)` | `internal void` | DELETED |
| `UpdateMaxRisk(double)` | `internal void` | DELETED |
| `SetParameters(double, double)` | `internal void` | DELETED |
| `GetSuggestedQty()` | `internal int` | DELETED |
| `CalcContracts(double, double, double)` | `internal static int` | DELETED |

Modified:

- `OnStateChange()` — removed `_lastContracts = 1;` from Terminated branch
- `OnBarUpdate()` — replaced body: `_lastAtr = ATR(Period)[0]; _hasData = true;` (CYC=2)
- `ManualOnBarUpdate()` — changed visibility `public` → `internal`

Added:

- `internal bool HasData => _hasData;` — new property (CYC=1)

---

### src/PropTraderTools/CopyEngine.cs

Removed ATR members:

| Member | Kind | Action |
|---|---|---|
| B12 T3 header comments referencing `_atrEngine` | comment block (lines 19–22) | DELETED |
| `_atrEnabled` | `private volatile bool` field (line 136) | DELETED |
| `_atrEngine` | `private volatile AtrSizingEngine` field (line 137) | DELETED |
| `SetAtrEngine(AtrSizingEngine, bool)` | `internal void` method (lines 692–698) | DELETED |
| `UpdateMaxRisk(double)` CopyEngine version | `internal void` method (lines 700–706) | DELETED |
| `UpdateAtrFraction(double)` | `internal void` method (lines 708–714) | DELETED |
| `GetSuggestedQty(NinjaTrader.Cbi.Instrument)` | `internal int` method (lines 1549–1556) | DELETED |

Modified:

- `ResolveBaseQty(Instrument, int)` — simplified to `return signalQty;` (CYC=1, single call site at line 2839)

---

### src/PropTraderTools/FeatureFlags.cs

Removed `AtrSizing` parameter:

- Record declaration: `bool AtrSizing` parameter DELETED (7-param → 6-param)
- `Starter()`: `new(false, false, false, false, false, false, false)` → `new(false, false, false, false, false, false)`
- `Pro()`: `new(true, true, true, false, false, false, false)` → `new(true, true, true, false, false, false)`
- `Elite()`: `new(true, true, true, true, true, true, true)` → `new(true, true, true, true, true, true)`
- `FromFeatureList()`: `AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing"),` DELETED

---

### Compile-Clean Fix Files (T2/T3 scope — required to satisfy T1 compile gate)

To achieve compile-clean state before T2, minimal fixes were applied to caller files:

**src/PropTraderTools/TradeCopierAddOn.cs**
- Removed `engine.SetParameters(200.0, pointValue)` and `engine.SetAtrFraction(0.75)` calls from `StartAtrEngine` (those methods deleted in T1)
- Removed `CopyEngine.Instance.SetAtrEngine(...)` and `engine.AtrUpdated += OnAtrUpdated` call-sites (those members deleted in T1)
- Removed `engine.AtrUpdated -= OnAtrUpdated` from `StopAtrEngine` (T2 will delete the whole method)
- Removed `CopyEngine.Instance.SetAtrEngine(null, enabled: false)` from `StopAtrEngine`

**src/PropTraderTools/TradeCopierWindow.cs**
- Removed `if (f.AtrSizing) return "ELITE";` branch from `GetStatusText()` (AtrSizing parameter deleted in T1)
- CYC reduced from 3 to 2

**src/PropTraderTools/LicenseClient.cs**
- Removed `if (f.AtrSizing) list.Add("atr_sizing");` from `GetFeatureList()` (AtrSizing deleted)
- Simplified `InferTierName()` — removed `if (f.AtrSizing) return "ELITE";` branch

**src/PropTraderTools/TradeCopierPanel.cs**
- `ApplyRowVisibilityFlags()` — removed `_atrRow` / `f.AtrSizing` visibility block (AtrSizing deleted)
- `NotifyRiskChanged()` — replaced body with no-op stub (CopyEngine.UpdateMaxRisk deleted)
- `NotifyAtrFractionChanged()` — replaced body with no-op stub (CopyEngine.UpdateAtrFraction deleted)
- Click-trader handler: `CopyEngine.Instance.GetSuggestedQty(_instrument)` → `int qty = 1;`

**src/PropTraderTools/CopyEngineTests.cs** (T3 scope — required for compile gate)
- Deleted 20 obsolete test methods:
  - `CalcContracts_MES_ATR6_returns5` through `CalcContracts_LargeMaxRisk_noOverflow` (8 tests)
  - `GetSuggestedQty_returns1_when_no_engine`
  - `GetSuggestedQty_returns_engine_qty_when_set`
  - `ClickTrader_atr_disabled_fallback_qty_is_1`
  - `ClickTrader_atr_enabled_uses_engine_qty`
  - `StartAtrEngine_NullChart_DoesNotThrow`
  - `StartAtrEngine_NullInstrument_DoesNotThrow`
  - `UpdateAtrOverlay_FormatsDisplayString_CorrectText`
  - `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1`
  - `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing`
  - `BuildRiskAtrRow_ClampMin_RejectsSubMinValue`
  - `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet`
  - `CalcContracts_DefaultValues_Use200Risk_075Fraction`
- Fixed `result.AtrSizing` assertion → `result.MultiRule`
- Added `_engine` field to `DwB170Tests` class (pre-existing bug fix for compile)

---

## 7-Scan Results

### SCAN-01: lock() check
```
Command: Get-ChildItem -Path "src/PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "^\s+lock\s*\("
Result: 0 matches
```
**PASS ✅**

### SCAN-02: Deleted symbol references
```
Command: Get-ChildItem -Path "src/PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "_maxRiskDollars|_tickDollarValue|CalcContracts|SetAtrFraction|GetSuggestedQty|FireAtrUpdated|AtrUpdated|SetAtrEngine|UpdateMaxRisk|UpdateAtrFraction|_atrEngine|_atrEnabled|SetParameters|AtrSizing"
```

Results in T1 files:
- `CopyEngine.cs`: 0 matches for target symbols ✅
- `FeatureFlags.cs`: 0 matches ✅
- `AtrSizingEngine.cs`: only class name `AtrSizingEngine` (allowed, class declaration kept) ✅

Remaining hits in T2-scope files (TradeCopierAddOn.cs, TradeCopierPanel.cs) — expected; will be cleaned in T2.

**PASS ✅ (T1 targets clean; T2-scope residuals expected)**

### SCAN-03: volatile double
```
Command: Get-ChildItem -Path "src/PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "volatile double"
Result: Comments only (no declarations)
```
**PASS ✅**

### SCAN-04: Non-ASCII characters in modified files
```
Command: Get-Content "src/PropTraderTools/AtrSizingEngine.cs" | Where-Object { $_ -match '[^\x00-\x7F]' }
         Get-Content "src/PropTraderTools/CopyEngine.cs" | Where-Object { $_ -match '[^\x00-\x7F]' }
         Get-Content "src/PropTraderTools/FeatureFlags.cs" | Where-Object { $_ -match '[^\x00-\x7F]' }
Result: 0 matches (all files)
```
**PASS ✅**

### SCAN-05: Cyclomatic complexity
All modified/new methods:

| Method | Actual CYC | Status |
|---|---|---|
| `AtrSizingEngine.OnBarUpdate()` | 2 (guard + set) | PASS ✅ |
| `AtrSizingEngine.OnStateChange()` | 4 (4 state branches; unchanged) | PASS ✅ |
| `AtrSizingEngine.ManualOnBarUpdate()` | 1 (straight-line) | PASS ✅ |
| `AtrSizingEngine.HasData` (new) | 1 (expression body) | PASS ✅ |
| `AtrSizingEngine.GetLastAtr()` | 1 (expression body; unchanged) | PASS ✅ |
| `CopyEngine.ResolveBaseQty()` | 1 (straight-line return) | PASS ✅ |

All ≤ CYC 8. **PASS ✅**

### SCAN-06: Raw brush constructors
```
Command: Select-String -Path "src/PropTraderTools/AtrSizingEngine.cs" -Pattern "new Solid|new Linear|new Radial"
Result: 0 matches
```
T1 adds no UI code. **N/A — PASS ✅**

### SCAN-07: AtrSizing in FeatureFlags.cs
```
Command: Select-String -Path "src/PropTraderTools/FeatureFlags.cs" -Pattern "AtrSizing"
Result: 0 matches
```
**PASS ✅**

---

## Build Result

```
dotnet build src/PropTraderTools/
Build succeeded.
0 Error(s)
Warnings: pre-existing CS0436 type-conflict warnings (unchanged from baseline)
```

**BUILD_PASS**

---

## ptt-sync.ps1 Result

```
powershell -File .\scripts\ptt-sync.ps1
ptt-sync complete: 18 linked, 0 errors
```

Files linked: AtrSizingEngine.cs, CopyEngine.cs, FeatureFlags.cs, LicenseClient.cs, TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, Core\PttContracts.cs, Features\PttBreakEven.cs, Features\PttBreakEvenSwap.cs, Features\PttCancel.cs, Features\PttCopier.cs, Features\PttFlatten.cs, Features\PttFollowerStrategy.cs, Features\PttGlobalBreakEven.cs, Features\PttGlobalQuickExit.cs, Features\PttQuickExit.cs, Features\PttTrim.cs

---

## Status: BUILD_PASS

All 7 scans pass. Build succeeds with 0 errors. Hard-link sync: 18 linked, 0 errors.
T1 compile gate satisfied. Ready for T2.
