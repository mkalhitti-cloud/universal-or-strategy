# PTT-COPIER-B16 Plan Review
# Reviewer: ptt-plan-reviewer
# Phase: 2 (Plan Review)
# Date: 2026-07-14
# Plan reviewed: docs/brain/PTT-COPIER-B16/02-architecture-plan.md

---

## VERDICT: REVIEW_PASS

No violations found. All 8 checklist sections pass. Plan is cleared for Phase 3 (ticket generation).

---

## Checklist Results

### 1. SPEC ALIGNMENT

| Sub-check | Result | Evidence |
|-----------|--------|----------|
| T1 uses `MessageBox.Show` (not `_statusText`) | ✅ PASS | Plan §C.3 line 162: `System.Windows.MessageBox.Show(sb.ToString(), "PTT B16 ChartPanel Subtree")` |
| T1 guard is `volatile bool _chartScaleDiagDone` | ✅ PASS | Plan §C.1: `private volatile bool _chartScaleDiagDone = false;` |
| T1 adds `WalkChartPanelChildren` + `BuildMethodReport` | ✅ PASS | Plan §C.3 and §C.4 define both methods with full code |
| T1 does NOT change `GetPriceAtY` | ✅ PASS | `GetPriceAtY` appears only in §D (T2); §C contains no reference to modifying it |
| T1 does NOT change `OnChartMouseDown` | ✅ PASS | Plan §D.6 explicitly lists `OnChartMouseDown` as UNCHANGED; T1 scope is limited to `SetChart` + new methods |
| T2 has BOTH Branch A and Branch B | ✅ PASS | Plan §D.1 (Branch A — native API) and §D.2 (Branch B — linear interpolation) |
| T2 removes all T1 diagnostic code | ✅ PASS | Plan §D.6 lists REMOVE for field, both diagnostic methods, `using System.Reflection`, and `using System.Text` |
| T2 adds 8 `[Fact]` tests | ✅ PASS | Plan §D.4 lists exactly T_B16_01 through T_B16_08 |
| Files-not-to-touch list respected | ✅ PASS | Plan §E explicitly lists `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs` as Must NOT touch |

**Section verdict: PASS**

---

### 2. JS Rules (RULES_CATALOG.md)

| Rule | Requirement | Result | Evidence |
|------|-------------|--------|----------|
| JS-021 | No `lock()` anywhere | ✅ PASS | Plan §G: "No `lock()` anywhere. ✅ JS-021, NT8-018." No lock pattern present in any code block |
| JS-023 | Volatile bool for cross-thread field | ✅ PASS | `_chartScaleDiagDone` declared `private volatile bool`; §H confirms JS-023 compliance |
| JS-033 | No `async void` | ✅ PASS | Plan §G: "No `async void` anywhere." All new methods are `void`, `static double`, or `static string` — none async |
| JS-002 | No `return null` where value expected | ✅ PASS | All guard returns use `return 0.0` (double-returning methods) or bare `return;` (void methods). No method returns null |

**Section verdict: PASS**

---

### 3. NT8 Rules (NT8_COMPILER_RULES.md)

| Rule | Finding | Result |
|------|---------|--------|
| NT8-017: Cross-thread bool is `volatile` | `_chartScaleDiagDone` is `private volatile bool` — correct for UI-thread write, read from any thread | ✅ PASS |
| NT8-018: No `lock()` | Explicitly confirmed in §G and §H table | ✅ PASS |
| NT8-019: No `async void` | Explicitly confirmed in §G and §H table | ✅ PASS |
| NT8-028: No hex color string literals | §H: "No color changes in B16" — no `"#RRGGBB"` patterns in any code block | ✅ PASS |
| NT8-029 / NT8-035: Tick alignment on limit prices | Both branches apply `RoundToTickSize` (NT8-native); Math.Round CS1061 fallback documented. NT8-035 price=0.0 stub replaced in T2 | ✅ PASS |
| NT8-032: `MarketData.Last.Price` correct | Fallback in Branch A (§D.1 line 281) uses `last.Price`; NT8-037 SAFE pattern cited correctly | ✅ PASS |
| NT8-037: `GetValueByY` not called on ChartPanel | §F explicitly confirms absence; plan investigates depth-2 children for alternate API | ✅ PASS |
| `System.Reflection` using directive | §C.3 documents `using System.Reflection;` as required in T1; §D.6 lists it for REMOVE in T2 | ✅ PASS |

**Section verdict: PASS**

---

### 4. CYC Compliance

| Method | Claimed CYC | ≤ 8? | ≤ 6 (GetPriceAtY only)? | Notes |
|--------|-------------|------|--------------------------|-------|
| `SetChart` (T1 modified) | 2 | ✅ | — | 1 base + 1 guard branch |
| `WalkChartPanelChildren` | 5 | ✅ | — | guard cc(1) + guard panel(2) + for-loop(3) + FrameworkElement check(4) + base = 5 |
| `BuildMethodReport` | 2 | ✅ | — | foreach(1) + if !match continue(2) |
| `GetPriceAtY` Branch A | 4 | ✅ | ✅ (4 ≤ 6) | cc null, panel null, scale null, raw ≤ 0 |
| `GetPriceAtY` Branch B | 5 | ✅ | ✅ (5 ≤ 6) | cc null, panel null, height ≤ 0, raw ≤ 0, instrument null |
| `LinearYToPrice` | 2 | ✅ | — | height guard, raw guard |
| `AlignToTick` | 1 (table) / 2 (body) | ✅ | — | Table says CYC=1 but code shows `if (tickSize <= 0.0) return raw;` → CYC=2. Plan's own parenthetical acknowledges this discrepancy. Both values ≤ 8. No violation. |
| `SetChart` (T2 restored) | 1 | ✅ | — | straight-line |
| `OnChartMouseDown` | 7 | ✅ | — | unchanged from B15 |
| Each `[Fact]` test | 1 | ✅ | — | straight-line assertions |

No method exceeds CYC = 8. Both `GetPriceAtY` variants are ≤ 6 as required.

**Section verdict: PASS**

---

### 5. Test Coverage

| Sub-check | Result | Evidence |
|-----------|--------|----------|
| 8 `[Fact]` tests planned for T2 | ✅ PASS | T_B16_01 through T_B16_08 listed in §D.4 |
| Top-of-chart interpolation | ✅ PASS | T_B16_01: y=0 → maxValue |
| Bottom-of-chart interpolation | ✅ PASS | T_B16_02: y=400 → minValue |
| Middle-of-chart interpolation | ✅ PASS | T_B16_03: y=200 → midpoint 4950.0 |
| Zero-height guard (no divide by zero) | ✅ PASS | T_B16_05: panelH=0 → 0.0 |
| Tick alignment — round down | ✅ PASS | T_B16_06: raw=4975.10, tick=0.25 → 4975.00 |
| Tick alignment — round up | ✅ PASS | T_B16_07: raw=4975.15, tick=0.25 → 4975.25 |
| Tick alignment — exact boundary | ✅ PASS | T_B16_08: raw=4975.25, tick=0.25 → 4975.25 |
| Test names follow `T_B16_NN` convention | ✅ PASS | All 8 tests use `T_B16_0N` format |

**Section verdict: PASS**

---

### 6. Section K (Deferred Work)

| Sub-check | Result | Evidence |
|-----------|--------|----------|
| DW-B16-01 defined | ✅ PASS | §K.1: DW-B16-01, P1, target B16 T1+T2, full description |
| DW-B16-02 (conditional fallback failure) documented | ✅ PASS | §K.1: DW-B16-02 (conditional), P2, B17+, describes NT8 Reflection cache investigation path if Branch B CS1061 fails |
| Prior carry-forwards listed | ✅ PASS | §K.2 lists DW-B9-01, DW-B9-03, DW-B12-DEFER-01 (orig) with priority and source block |

**Section verdict: PASS**

---

### 7. 7-Scan Checklist

| Scan | Present in plan? | Expected result specified? | Result |
|------|-----------------|---------------------------|--------|
| SCAN-01: `lock(` in TradeCopierPanel.cs | ✅ §L | 0 results | ✅ PASS |
| SCAN-02: `async void` in TradeCopierPanel.cs | ✅ §L | 0 results | ✅ PASS |
| SCAN-03: `DateTime.Now` (non-UTC) | ✅ §L | 0 results | ✅ PASS |
| SCAN-04: hex color `"#...` string literals | ✅ §L | 0 results | ✅ PASS |
| SCAN-05: `.GetValueByY(` call | ✅ §L | 0 code hits (comments only) | ✅ PASS |
| SCAN-06: `price = 0.0` stub in CreateOrder | ✅ §L | T1=1 (stub present), T2=0 (replaced) | ✅ PASS |
| SCAN-07: `T_B16_` test names in CopyEngineTests.cs | ✅ §L | T1=0, T2=8 | ✅ PASS |

All 7 scans present with correct expected results for both tickets.

**Section verdict: PASS**

---

### 8. Unconfirmed APIs

| API | Flagged UNCONFIRMED? | CS1061 fallback documented? | Result |
|-----|---------------------|-----------------------------|--------|
| `instrument.MasterInstrument.RoundToTickSize(double)` | ✅ §F: "UNCONFIRMED — NT8-029" | ✅ §D.1: `Math.Round(..., MidpointRounding.AwayFromZero) * tickSize` + new NT8-038 rule instruction | ✅ PASS |
| `ChartPanel.MaxValue` | ✅ §F: "UNCONFIRMED" | ✅ §D.2: Add NT8-039/NT8-040 + fallback to `instrument.MarketData.Last.Price` | ✅ PASS |
| `ChartPanel.MinValue` | ✅ §F: "UNCONFIRMED" | ✅ §D.2: same fallback as MaxValue | ✅ PASS |

**Section verdict: PASS**

---

## Violations Found

None.

---

## Summary

| Section | Result |
|---------|--------|
| 1. Spec Alignment | ✅ PASS |
| 2. JS Rules (JS-021, JS-023, JS-033, JS-002) | ✅ PASS |
| 3. NT8 Rules | ✅ PASS |
| 4. CYC Compliance | ✅ PASS |
| 5. Test Coverage | ✅ PASS |
| 6. Section K (Deferred Work) | ✅ PASS |
| 7. 7-Scan Checklist | ✅ PASS |
| 8. Unconfirmed APIs | ✅ PASS |

**Overall: REVIEW_PASS**

Phase 3 (ticket generation) is unblocked.

---

## Amendment: DW-B16-02 added (2026-07-15)

P1 bug DW-B16-02 injected by Director before T1. Added to T2 scope.
Plan review verdict unchanged: **REVIEW_PASS**.

DW-B16-02 fix is surgical (3 lines removed from TightenOneStop, 0 added net)
and consistent with the existing `MoveStopToBreakEven` pattern confirmed safe by GAP-001d.
No new architectural risk. The `"~"` → `"Tighten"` label rename is cosmetic and
complies with NT8 ASCII-only mandate.

CopyEngine.cs added to T2 files-to-modify. All JS and NT8 rule constraints remain
satisfied — the removed branch was the only use of `acc.Cancel` + `acc.CreateOrder`
in `TightenOneStop`; no lock(), no async void, no hex literals introduced.

