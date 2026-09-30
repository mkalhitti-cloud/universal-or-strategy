# PTT-COPIER-B13 Ticket 1 Completion

**Engineer**: ptt-engineer
**Ticket**: DW-B12-DEFER-01 -- Wire GetRefPrice()
**Date**: 2026-07-13
**Status**: BUILD_PASS

---

## Changes Made

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace: `c:\WSGTA\universal-or-strategy`)
**Location**: Lines 749-759 (replaced entire comment block + method body)

### BEFORE (lines 749-759):
```csharp
        // B12 T1 -- GetRefPrice: returns 0.0 as ref price placeholder.
        // NT8: Chart (NinjaTrader.Gui.Chart.Chart) has no BarsArray property -- that lives on
        // NinjaScriptBase (strategies/indicators). From an AddOnBase context, bar data is not
        // directly accessible via the Chart window reference. Callers (Trim/Flatten/BE limit
        // entry) receive 0.0 and must use the buffer-tick offset from current market price
        // rather than a historical close. DW-B12-DEFER-01: wire real price via MarketData.
        // CYC=1.
        private double GetRefPrice()
        {
            return 0.0;
        }
```

### AFTER (lines 749-763):
```csharp
        // B13 T1 -- GetRefPrice: returns last traded price via instrument.MarketData.Last.Price.
        // NT8-032: MarketData.Last is MarketDataEventArgs; .Price is the double value.
        // NT8-027: synchronous snapshot read -- no subscription needed; field is always populated
        //          once the instrument is active in a chart session.
        // Returns 0.0 on any null (instrument not set, or no data yet).
        // CYC=4: (1) _instrument null guard, (2) md null guard, (3) last null guard, (4) return price.
        private double GetRefPrice()
        {
            if (_instrument == null) return 0.0;                   // (1) guard
            var md = _instrument.MarketData;
            if (md == null)   return 0.0;                          // (2) guard
            var last = md.Last;
            if (last == null) return 0.0;                          // (3) guard
            return last.Price;                                     // (4) double
        }
```

**Change summary**: Replaced 1-line stub `return 0.0` with triple null guard pattern
reading `_instrument.MarketData.Last.Price`. CYC increased from 1 to 4.
No callers changed. No new fields. No new brushes. No new events.

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN 1 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "lock\("` | 2 comment-only hits in `CopyEngine.cs` lines 547/1182 ("try block(0)") -- no executable `lock(` calls. **0 violations** |
| SCAN 2 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "async void "` | **0 matches** |
| SCAN 3 | `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "return null;"` | **0 matches** in modified file. (Pre-existing `return null` in unmodified files: `CopyEngine.cs` x4, `TradeCopierAddOn.cs` x5, `TradeCopierWindow.cs` x2 -- all pre-B13, not introduced by T1) |
| SCAN 4 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "volatile double"` | 2 comment-only hits in `AtrSizingEngine.cs` lines 13/49 (comment text: "volatile double forbidden") -- no executable `volatile double` declaration. **0 violations** |
| SCAN 5 | `python archive\v12-reference\scripts\complexity_audit.py` | `CYC > 8 (BLOCKING): 0` -- all methods CYC <= 8 pass. `GetRefPrice()` CYC=4 (3 null-guard branches + 1 return). |
| SCAN 6 | `dotnet build Linting.csproj` (archive\v12-reference) | **Build succeeded. 0 warnings, 0 errors** |
| SCAN 7 | `dotnet test V12_Performance.Tests.csproj` (archive\v12-reference\tests\tests\V12_Performance.Tests) | **Passed! Failed: 0, Passed: 331, Skipped: 0, Total: 331** |

All 7 scans: **0 violations**.

---

## Acceptance Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `GetRefPrice()` body contains the three null guards and `return last.Price` as specified | ✅ PASS -- implementation matches ticket AFTER block exactly |
| 2 | `dotnet build` completes with 0 errors, 0 warnings | ✅ PASS -- Build succeeded. 0 errors, 0 warnings |
| 3 | SCAN 1-4 all return 0 matches on modified file | ✅ PASS -- all comment-only hits pre-exist; 0 executable violations |
| 4 | SCAN 5 shows `GetRefPrice` CYC = 4 | ✅ PASS -- 3 null-guard branches + 1 return = CYC 4 |
| 5 | Sim101 gate DW-B13-SIM-T1-01: `[Trim +N]` / `[Flatten +N]` issue OrderType.Limit when `Last.Price` non-zero | ⏳ PENDING -- requires live NinjaTrader Sim101 chart session (ptt-verifier confirms) |

---

## NT8 Compliance

| Rule | Check | Status |
|------|-------|--------|
| NT8-032 | `MarketData.Last` used as `MarketDataEventArgs`; `.Price` property accessed | ✅ PASS |
| NT8-027 | Synchronous snapshot read from `AddOnBase` -- no subscription required | ✅ PASS |
| NT8-033 | `Chart.BarsArray` NOT used (AddOn constraint) | ✅ PASS -- not using BarsArray |
| NT8-003 | No `volatile double` introduced | ✅ PASS -- `GetRefPrice()` returns value-type `double` |
| NT8-001 | No `{ get; init; }` | ✅ PASS -- no new properties |

## Jane Street DNA Compliance

| Rule | Check | Status |
|------|-------|--------|
| JS-021 | No `lock()` | ✅ PASS -- 0 lock() in executable code |
| JS-033 | No `async void` | ✅ PASS -- method is `private double` |
| JS-001 | No throw in hot path | ✅ PASS -- null guards return 0.0; no throw |
| JS-002 | No `return null` | ✅ PASS -- returns `double` value type (0.0 is not null) |

---

## Verdict
BUILD_PASS
