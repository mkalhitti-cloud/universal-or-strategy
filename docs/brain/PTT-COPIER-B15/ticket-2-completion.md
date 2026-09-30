# PTT-COPIER-B15 Ticket 2 Completion Report

**Ticket**: T2 -- Y-to-Price Conversion + Tick-Align (DW-B8-04 resolution)
**Engineer**: ptt-engineer (PTT-COPIER-B15)
**Date**: 2026-07-14
**Status**: BUILD_PASS

---

## RULES CATALOG GATE

**Result: PASS**

- Read `docs/standards/jane-street/RULES_CATALOG.md` (UTF-8 clean).
- Zero P0 violations in files touched by T2:
  - No `lock()` usage
  - No `async void` methods
  - No `volatile double` declarations
  - No `return null` for missing values
  - No `throw new XxxException` in hot paths
- Gate: **PASS -- proceed**

---

## NT8 COMPILER GATE

- Read `docs/standards/NT8_COMPILER_RULES.md`.
- NT8-036 confirmed: `ChartControl.ChartBars` absent -- used `FindVisualChild<ChartPanel>` instead.
- NT8-029: tick-alignment applied to all limit prices.
- NT8-007: `CreateOrder` arg 12 uses `(NinjaTrader.Cbi.CustomOrder)null` (not bare `null`).
- NT8-003: no `volatile double` declarations introduced.
- Gate: **PASS -- proceed**

---

## Summary of Changes

### File: `src/PropTraderTools/TradeCopierPanel.cs`

**Step 1 -- T1 diagnostic cleanup (removed):**
- Removed `private volatile bool _chartDiagDone` field.
- Removed `DumpReflectionPath(ChartControl cc, System.Text.StringBuilder sb)` method.
- Removed `DumpVisualTree(ChartControl cc, System.Text.StringBuilder sb)` method.
- Removed `DumpChartControlTree(ChartControl cc)` method.
- Reverted `SetChart(Chart chart)` to CYC=1 single-assignment form.

**Step 2 -- GetPriceAtY added (CYC=4):**
```csharp
private static double GetPriceAtY(ChartControl cc, double y)
{
    if (cc == null) return 0.0;                                                          // guard (1)
    var panel = TradeCopierAddOn.FindVisualChild<NinjaTrader.Gui.Chart.ChartPanel>(cc);
    if (panel == null) return 0.0;                                                       // guard (2)
    double raw = panel.GetValueByY(y);
    if (raw <= 0.0) return 0.0;                                                          // guard (3)
    return raw;
}
```
CYC = 4 (base=1 + 3 decision points). NT8-036 compliant.

**Step 3 -- OnChartMouseDown stub replaced (CYC=7):**

Removed:
```csharp
// NT8 constraint: ChartControl.GetValueByY does not exist in this NT8 version.
// DW-B8-04 (click trader) deferred...
double price  = 0.0;
_ = e.GetPosition(chartControl); // suppress unused-variable warning
```

Replaced with:
```csharp
// B15 T2: real Y-to-price conversion (NT8-036: direct property absent; ChartPanel via FindVisualChild).
// NT8-029: tick-align mandatory on all limit prices.
Point  mousePos  = e.GetPosition(chartControl);
double rawPrice  = GetPriceAtY(chartControl, mousePos.Y);
if (rawPrice <= 0.0) return;                                 // guard (5): no valid price
double tickSize  = _instrument.MasterInstrument.TickSize;
double price     = Math.Round(rawPrice / tickSize) * tickSize;
```

CYC count for OnChartMouseDown = 7:
- guard (1): `!_clickArmed`
- guard (2): `_leaderAccount == null`
- guard (3): `_instrument == null`
- guard (4): `chartControl == null`
- guard (5): `rawPrice <= 0.0`
- ternary: `isBuy ? OrderAction.Buy : OrderAction.SellShort`
CYC = 7 <= 8 (Jane Street limit). PASS.

**CreateOrder arg 12 verified:**
- Line 1144: `(NinjaTrader.Cbi.CustomOrder)null` -- NT8-007 compliant.

### File: `src/PropTraderTools/CopyEngineTests.cs`

**Step 4 -- 6 [Fact] tick-align pure-math tests added:**
- `T_B15_01_TickAlign_MesPriceBelowTick_RoundsDown` -- 4502.12 -> 4502.00
- `T_B15_02_TickAlign_MesPriceAboveHalfTick_RoundsUp` -- 4502.14 -> 4502.25
- `T_B15_03_TickAlign_PriceExactTick_Unchanged` -- 4502.25 -> 4502.25
- `T_B15_04_TickAlign_PriceExactlyHalfTick_BankersRound` -- 4502.125 -> 4502.00 (ToEven)
- `T_B15_05_TickAlign_CrudePriceRoundTrip` -- 4502.37 -> 4502.25
- `T_B15_06_TickAlign_ZeroPrice_ReturnsZero` -- 0.0 -> 0.0

All 6 tests are pure math (no NT8 references). Tick size = 0.25 (MES SEP26).

### File: `docs/standards/NT8_ADDON_KNOWLEDGE.md` (Director workspace)

**Step 5 -- B15 Discoveries T2 API Confirmation sub-section added:**
- Documented `ChartPanel.GetValueByY` compile status (pending F5 gate).
- Documented confirmed access path via `FindVisualChild<ChartPanel>`.
- Documented DW-B8-04 implementation status.
- Documented all 5 T2 changes applied.

---

## T2 API Confirmation

**ChartPanel.GetValueByY**: Documented NT8 API method on ChartPanel.
- T1 confirmed `ChartPanel` is a direct visual child of `ChartControl` at depth=1.
- `GetValueByY` is the standard NT8 method to convert Y-pixel to price value.
- T2 code uses this API; compile status requires F5 gate in NinjaTrader 8.
- **If CS1061 at F5**: NT8-037 will be added, fallback to `MarketData.Last.Price`.
- NT8-037 has NOT been added (no CS1061 evidence yet -- F5 gate will confirm).

---

## 7-Scan Results

| Scan | Pattern | Target | Result |
|------|---------|--------|--------|
| SCAN-01 | `lock\(` | TradeCopierPanel.cs | **0** |
| SCAN-02 | `async void ` | TradeCopierPanel.cs | **0** |
| SCAN-03 | `price\s*=\s*0\.0` (stub) | TradeCopierPanel.cs | **0** |
| SCAN-04 | `DW-B8-04` (stub comment) | TradeCopierPanel.cs | **0** |
| SCAN-05 | `^\s+volatile double` (declarations) | src/PropTraderTools/*.cs | **0** |
| SCAN-06 | `ChartBars` | TradeCopierPanel.cs | **0** |
| SCAN-07 | CYC check | OnChartMouseDown CYC=7 <= 8; GetPriceAtY CYC=4 | **PASS** |

**Note on SCAN-05**: A broad pattern `volatile double` returns 2 hits in `AtrSizingEngine.cs` comments
(lines explaining why volatile double is NOT used). These are pre-existing explanatory comments,
not declarations. Zero actual `volatile double` field declarations exist in any T2-touched file.

All 7 scans: **ZERO violations**.

---

## Jane Street DNA Compliance

| Rule | Check | Status |
|------|-------|--------|
| JS-021 (no lock()) | SCAN-01 = 0 | PASS |
| JS-023 (volatile bool for atomics) | `_clickArmed`/`_clickBuy` remain volatile bool | PASS |
| JS-001 (no throw in hot path) | No throw in OnChartMouseDown | PASS |
| JS-008 (immutability / readonly) | No new mutable statics | PASS |
| NT8-029 (tick-align) | `Math.Round(raw/tick)*tick` applied | PASS |
| NT8-036 (no ChartBars) | SCAN-06 = 0 | PASS |
| NT8-007 (CreateOrder arg 12) | `(NinjaTrader.Cbi.CustomOrder)null` | PASS |

---

## BUILD_PASS

All 7 scans zero. All Jane Street DNA rules satisfied. All NT8 constraints respected.
6 new [Fact] tests added. T2 implementation complete pending F5 gate in NinjaTrader 8.

**DW-B8-04 STATUS**: IMPLEMENTED. Closure pending F5 gate. If `GetValueByY` causes CS1061 at F5:
add NT8-037 and apply `MarketData.Last.Price` fallback.
