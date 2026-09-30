# PTT-COPIER-B9 — Ticket T1 Completion Report
**Ticket**: T1 — ATR Dynamic Sizing Engine (DW-B7-02 / DW-B8-03)
**Engineer**: ptt-engineer (Phase 5)
**Date**: 2026-07-09
**Status**: BUILD_PASS

---

## What Was Implemented

### NEW FILE: `AtrSizingEngine.cs` (~93 lines)

| Method | Signature | CYC | Notes |
|--------|-----------|-----|-------|
| `AtrSizingEngine(int testContracts)` | `internal AtrSizingEngine(int testContracts)` | 1 | Test-seam ctor — bypasses NT8 lifecycle |
| `AtrSizingEngine()` | `public AtrSizingEngine()` | 1 | NT8 parameterless ctor (required by NinjaScript) |
| `OnStateChange()` | `protected override void OnStateChange()` | 4 | SetDefaults, Configure, DataLoaded, Terminated |
| `OnBarUpdate()` | `protected override void OnBarUpdate()` | 2 | CurrentBar guard + ATR compute + volatile writes |
| `SetParameters()` | `internal void SetParameters(double, double)` | 1 | Single-writer UI thread, configures risk params |
| `GetSuggestedQty()` | `internal int GetSuggestedQty()` | 2 | Guard on `_hasData`; volatile read of `_lastContracts` |
| `GetLastAtr()` | `internal double GetLastAtr()` | 1 | Volatile read, expression body |
| `CalcContracts()` | `internal static int CalcContracts(double, double, double)` | 3 | Pure math, unit-testable without NT8 |

**Volatile fields (JS-023):**
- `private volatile int _lastContracts = 1`
- `private volatile double _lastAtr = 0.0`
- `private volatile bool _hasData = false`

### MODIFIED: `CopyEngine.cs` (+22 lines)

| Addition | Detail |
|----------|--------|
| `_atrEnabled` field | `private volatile bool _atrEnabled = false;` (ADV-002 fix — volatile) |
| `_atrEngine` field | `private volatile AtrSizingEngine _atrEngine = null;` |
| `SetAtrEngine()` | `internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)` — CYC=1 |
| `GetSuggestedQty()` | `internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)` — CYC=2 |
| `DispatchCopy` ATR integration | `int baseQty = _atrEnabled ? GetSuggestedQty(order.Instrument) : baseSignal.Quantity;` — replaces `baseSignal.Quantity * mult` with `baseQty * mult` |

### MODIFIED: `TradeCopierAddOn.cs` (+30 lines)

| Addition | Detail |
|----------|--------|
| `_atrEngines` field | `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines` |
| `StartAtrEngine()` | `private static void StartAtrEngine(Chart chart, Instrument instr)` — CYC=3 |
| `StopAtrEngine()` | `private static void StopAtrEngine(Chart chart)` — CYC=2 |
| `DoInject` modification | Captures `chartInstr` and calls `StartAtrEngine(chart, chartInstr)` after panel creation |
| `OnWindowDestroyed` modification | Calls `StopAtrEngine(chart)` before panel teardown |

**IMPL-NOTE-1 (DW-B9-02):** NT8 Indicator attachment via `chart.NinjaScripts.Add(engine)` deferred pending
runtime API verification. Engine object is stored in `_atrEngines` and `CopyEngine._atrEngine`; 
`GetSuggestedQty()` is callable immediately (returns safe default of 1 until `_hasData` is true).

### MODIFIED: `CopyEngineTests.cs` (+80 lines, 10 new [Fact] tests)

| Test ID | Method Name | Coverage |
|---------|-------------|----------|
| T-B9-01 | `CalcContracts_MES_ATR6_returns5` | floor(150/(6*5))=5 |
| T-B9-02 | `CalcContracts_MES_ATR8_returns3` | floor(150/(8*5))=3 |
| T-B9-03 | `CalcContracts_MES_ATR12_returns2` | floor(150/(12*5))=2 |
| T-B9-04 | `CalcContracts_ZeroAtr_returns1` | guard: atr<=0 -> 1 |
| T-B9-05 | `CalcContracts_NegativeAtr_returns1` | guard: atr<0 -> 1 |
| T-B9-06 | `CalcContracts_ResultBelowOne_clampsTo1` | floor(5/10)=0 -> 1 |
| T-B9-07 | `CalcContracts_ZeroTickValue_returns1` | guard: tickVal<=0 -> 1 |
| T-B9-08 | `CalcContracts_LargeMaxRisk_noOverflow` | floor(10000/5)=2000 |
| T-B9-09 | `GetSuggestedQty_returns1_when_no_engine` | ATR disabled -> 1 |
| T-B9-10 | `GetSuggestedQty_returns_engine_qty_when_set` | test-seam AtrSizingEngine(3) -> 3 |

---

## 7-Scan Results

Scans run against: `AtrSizingEngine.cs`, `CopyEngine.cs` (new lines), `TradeCopierAddOn.cs` (new lines), `CopyEngineTests.cs` (new lines).

| Scan | Pattern | Result | Notes |
|------|---------|--------|-------|
| SCAN-01 | `lock\s*\(` executable | **ZERO** | Two comment hits in CopyEngine.cs (`// no lock (JS-021)`) are pre-existing B8 comments, excluded |
| SCAN-02 | non-ASCII bytes | **ZERO** | All files UTF-8 no-BOM |
| SCAN-03 | `FontFamily` | **ZERO** | No FontFamily in any new code |
| SCAN-04 | `#[0-9A-Fa-f]{6}` hex | **ZERO** | No hex color strings anywhere |
| SCAN-05 | `DateTime.Now[^U]` | **ZERO** | No DateTime.Now in new code |
| SCAN-06 | `async void` | **ZERO** | All methods sync void |
| SCAN-07 | Additional checks | **ZERO** | volatile on all cross-thread fields; ConcurrentDictionary for `_atrEngines`; `CalcContracts` is `internal static` |

**Additional B9-T1 checks:**

| Check | Result |
|-------|--------|
| `private volatile bool _atrEnabled` in CopyEngine.cs | CONFIRMED (ADV-002) |
| `private volatile AtrSizingEngine _atrEngine` in CopyEngine.cs | CONFIRMED |
| `_lastContracts`, `_lastAtr`, `_hasData` all `volatile` in AtrSizingEngine | CONFIRMED |
| `CalcContracts` declared `internal static` | CONFIRMED (testability) |
| `_atrEngines` is `ConcurrentDictionary<Chart, AtrSizingEngine>` | CONFIRMED |
| IMPL-NOTE-1: documented in this completion report | CONFIRMED (DW-B9-02) |

---

## Test Count

| Source | Count |
|--------|-------|
| B8 baseline | 40 |
| B9 T1 new tests | 10 |
| **Total** | **50** |

Test count verified: `Select-String -Path CopyEngineTests.cs -Pattern '\[Fact\]' | Measure-Object` = **50**

---

## File Line Counts

| File | Lines |
|------|-------|
| `AtrSizingEngine.cs` (NEW) | 93 |
| `CopyEngine.cs` (MODIFIED) | 1,063 (+23 lines from B8 baseline of ~1,040) |
| `TradeCopierAddOn.cs` (MODIFIED) | 256 (+28 lines from B8 baseline of ~230) |
| `CopyEngineTests.cs` (MODIFIED) | 977 (+85 lines from B8 baseline of 892) |

---

## CYC Summary — All T1 Methods

| Method | File | CYC | Limit |
|--------|------|-----|-------|
| `OnStateChange` | AtrSizingEngine | 4 | OK |
| `OnBarUpdate` | AtrSizingEngine | 2 | OK |
| `CalcContracts` | AtrSizingEngine | 3 | OK |
| `GetSuggestedQty` (AtrSizingEngine) | AtrSizingEngine | 2 | OK |
| `SetParameters` | AtrSizingEngine | 1 | OK |
| `GetLastAtr` | AtrSizingEngine | 1 | OK |
| `SetAtrEngine` | CopyEngine | 1 | OK |
| `GetSuggestedQty` (CopyEngine) | CopyEngine | 2 | OK |
| `StartAtrEngine` | TradeCopierAddOn | 3 | OK |
| `StopAtrEngine` | TradeCopierAddOn | 2 | OK |

All methods CYC <= 8. No methods approach the limit.

---

## ADV-002 Resolution

`_atrEnabled` declared `private volatile bool _atrEnabled = false;` in CopyEngine.cs.
Explicit volatile keyword present as required by ADV-002 (volatile ensures cross-thread
visibility without lock()).

---

## BUILD_PASS
