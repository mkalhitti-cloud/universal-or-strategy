# B173 -- AtrWidget Live ATR Fix
# Plan Review -- Phase 2
# Author: ptt-plan-reviewer
# Status: REVIEW_PASS

---

## Verdict: REVIEW_PASS

Zero violations. All 14 checklist items pass. Plan is cleared for Phase 3 (ticket generation).

---

## Evidence Read

| File | Purpose |
|------|---------|
| `docs/brain/B173/02-architecture-plan.md` | Plan under review |
| `src/PropTraderTools/AtrSizingEngine.cs` | Confirm root cause (new outside pipeline) |
| `src/PropTraderTools/TradeCopierAddOn.cs` lines 390-410, 485-510 | Confirm WireNewPanel lines 397-398 and FindVisualChild existence |
| `src/PropTraderTools/TradeCopierPanel.cs` (grep + lines 3077-3294) | Confirm AtrWidget current implementation |
| `src/PropTraderTools/CopyEngineTests.cs` lines 10166-10222 | Confirm B172AtrWidgetTests location and final brace |
| `docs/brain/B172/06-deferred-backlog.md` | Confirm DW-B172-01 status and B172 summary |

---

## Checklist Results

### 1. LANE-SPLIT GATE COMPLIANCE

**Result: PASS**

Plan states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`. Gate result is present (satisfies "single-pipeline but no gate result is stated" rule).

Gate inputs verified:
- Q1 = YES (same method / within 50 lines): single-pipeline appropriate
- Q2 = YES (Fix B depends on Fix A final design): single-pipeline appropriate
- Q3 = NO (not standalone): single-pipeline appropriate
- Q4 = NO (not independent SIM path): single-pipeline appropriate

Plan does not use lanes, so lane-specific failure conditions do not apply. Gate decision is self-consistent.

---

### 2. ROOT CAUSE ADDRESSED

**Result: PASS**

Plan Section "Root Cause" correctly identifies: `AtrSizingEngine extends NinjaTrader.NinjaScript.Indicators.Indicator`, instantiated via `new AtrSizingEngine()` at `TradeCopierAddOn.WireNewPanel` line 397 (verified in source). An `Indicator` created with `new` outside the NinjaScript pipeline has no `BarsArray`, no `CurrentBar`, no data series. `ManualOnBarUpdate()` calls `OnBarUpdate()` which accesses `ATR(Period)[0]` -- silently throws or returns 0.0. `_hasData` never becomes true. Widget blank forever.

Fix correctly substitutes a pure static helper (`AtrCalc`) reading from the already-live `ChartControl` obtained in `WireNewPanel`.

Cross-verified against actual source:
- `AtrSizingEngine.cs` line 6: `public class AtrSizingEngine : Indicator` -- confirmed
- `TradeCopierAddOn.cs` line 397: `var engine = new AtrSizingEngine();` -- confirmed
- `TradeCopierAddOn.cs` line 398: `panel.SetAtrProvider(engine);` -- confirmed

---

### 3. AtrCalc.cs DUAL OVERLOAD DESIGN

**Result: PASS**

Section 2 defines both required overloads:
- Method 1: `internal static double Compute(double[] highs, double[] lows, double[] closes, int period)` -- pure math, no NT8 dependency, testable
- Method 2: `internal static double Compute(NinjaTrader.Gui.Chart.ChartControl cc, int period)` -- NT8 runtime, delegates to Method 1

Tests in Section 5 use Method 1 only (no NT8 runtime required). Correct.

---

### 4. JS-001: NO THROW

**Result: PASS**

Section 2 explicitly states for both overloads: "JS-001: Entire body wrapped in try/catch; return 0.0 on any exception". Section 6 Jane Street rules table confirms "JS-001: no throw | AtrCalc.Compute both overloads: try/catch -> return 0.0 | ENFORCED".

Consequence: `OnTimerTick` in Section 3 correctly omits try/catch around `AtrCalc.Compute(_chartControl, 14)` because the callee absorbs all exceptions.

---

### 5. NT8-003: VOLATILE DOUBLE BANNED

**Result: PASS**

No `volatile double` anywhere in the plan. Confirmed:
- `_lastAtr` is declared non-volatile: comment reads "non-volatile: sizing hint only (NT8-003 PASS)"
- `_chartControl` is non-volatile: written and read on UI thread only, no volatile needed
- `_hasData` is `volatile bool` -- bool is explicitly permitted by NT8-003

Section 6 table row: "NT8-003: volatile double BANNED | _lastAtr non-volatile. No new volatile double introduced. | ENFORCED"

---

### 6. JS-021: NO LOCK()

**Result: PASS**

No `lock(` appears in any plan code snippet. Section 6 table: "JS-021: no lock | No lock() anywhere. Static class has no shared state. | ENFORCED". Section 7 (Threading Model) explains why no locking is needed: all operations are on the UI thread.

---

### 7. JS-023: VOLATILE ONLY FOR BOOL/INT

**Result: PASS**

Only `_hasData volatile bool` is volatile. No `volatile double`, no `volatile ChartControl`, no `volatile` on reference types. Section 6 table: "JS-023: volatile only for bool/int | _hasData volatile bool kept. _lastAtr non-volatile double. _chartControl written and read on UI thread -- no volatile needed. | ENFORCED"

---

### 8. ASCII-ONLY: NO UNICODE IN PLAN CODE SNIPPETS

**Result: PASS**

All code snippets examined. String literals are ASCII. Comments use `//` with standard ASCII characters. The plan uses `--` (double hyphen) not em-dash in code comments. No Unicode characters detected in any code block.

---

### 9. SPEC REQUIREMENTS COVERAGE MATRIX

**Result: PASS**

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| `AtrSizingEngine.cs` deleted | YES | Section 1 |
| `AtrCalc.cs` created with `Compute(double[], double[], double[], int)` | YES | Section 2, Method 1 |
| `AtrCalc.cs` created with `Compute(ChartControl, int)` | YES | Section 2, Method 2 |
| `TradeCopierPanel.cs`: `SetAtrProvider` -> `SetAtrChartControl` | YES | Section 3b |
| `TradeCopierPanel.cs`: `_provider` -> `_chartControl` | YES | Section 3a |
| `TradeCopierAddOn.cs`: WireNewPanel removes engine lines | YES | Section 4 |
| `CopyEngineTests.cs`: 2 new `[Fact]` tests for pure math overload | YES | Section 5 (2 tests: `AtrCalc_Compute_ReturnsZero_WhenInsufficientBars`, `AtrCalc_Compute_CorrectValue_KnownBars`) |

All 7 spec requirements addressed.

---

### 10. CYC TARGETS REALISTIC

**Result: PASS**

Section 9 CYC Summary verified:

| Method | CYC | Limit | Pass? |
|--------|-----|-------|-------|
| `AtrCalc.Compute(double[],...)` | 5-6 | 8 | PASS |
| `AtrCalc.Compute(ChartControl,...)` | 4-5 | 8 | PASS |
| `AtrWidget.SetChartControl` | 2 | 8 | PASS |
| `AtrWidget.OnTimerTick` | 3 | 8 | PASS |
| `AtrWidget.Detach` | 2 | 8 | PASS |
| `TradeCopierPanel.SetAtrChartControl` | 1 | 8 | PASS |

All methods are at or below CYC=6 (well within the CYC=8 hard limit).

---

### 11. TICKET STRUCTURE: SEQUENTIAL T1->T2->T3, NO SHARED .CS IN PARALLEL

**Result: PASS**

Section 11 defines strictly sequential tickets:
- T1: `AtrCalc.cs` (new) + `CopyEngineTests.cs` (append)
- T2: `TradeCopierPanel.cs` (depends on T1)
- T3: `TradeCopierAddOn.cs` + delete `AtrSizingEngine.cs` (depends on T2)

No ticket runs in parallel. Each ticket's dependency is stated explicitly. No .cs file is shared across concurrent tickets.

---

### 12. NT8 API VERIFICATION INSTRUCTION

**Result: PASS**

Section 2 contains a dedicated "ARCHITECT NOTE -- NT8 API Verification Required at Implementation" block with 5 enumerated engineer instructions:
1. Confirm `ChartControl.BarsArray` property type and accessor syntax
2. Confirm `ChartBars.Bars` accessor form
3. Confirm `Bars.GetHigh(int barsAgo)` / `GetLow` / `GetClose` signatures
4. Update `docs/standards/NT8_FULL_REFERENCE.md` with confirmed entries before marking complete
5. Fallback: indexer access `bars[barsAgo].High` / `.Low` / `.Close`

Satisfies the requirement for engineer NT8 API verification before coding.

---

### 13. PERIOD=14 HARDCODED

**Result: PASS**

The call site in Section 3 (OnTimerTick) uses `AtrCalc.Compute(_chartControl, 14)` -- literal `14`, not a property, not a field, not user-configurable. The `period` parameter on the method signature is for testability only (Section 5 tests use `period=3` for the known-value test). No configurable period is exposed to the user. Spec requirement satisfied.

---

### 14. PTT-SYNC.PS1 IN FINAL TICKET (T3)

**Result: PASS**

Section 11 T3 states: "Run ptt-sync.ps1 after this ticket (hard link resync required after file delete)". This satisfies the requirement for sync in the final ticket. Correct placement -- file deletion requires resync of hard links.

---

## Violation List

None.

---

## Additional Observations (Non-Blocking)

The following are informational only and do not affect the verdict:

1. **DW-B172-01 closure path**: Plan Section 12 correctly marks DW-B172-01 as "OPEN -> CLOSED by B173" pending Director SIM verification. This is the correct procedure.

2. **`FindVisualChild<ChartControl>` confirmed**: Plan states this is an existing static method on `TradeCopierAddOn` at lines 492-503. Verified in source -- method exists at exactly those lines (lines 492-508).

3. **`cc` may be null**: Plan Section 4 notes `cc` may be null if the visual tree is not fully loaded, and that `SetChartControl(null)` safely stops the timer. This defensive null path is correct and complete.

4. **Test math verified**: Section 5 T-B173-T1-02 ATR(3) calculation:
   - TR[1] = max(3-2, |3-1.5|, |2-1.5|) = max(1.0, 1.5, 0.5) = 1.5 ✓
   - TR[2] = max(4-2, |4-2.5|, |2-2.5|) = max(2.0, 1.5, 0.5) = 2.0 ✓
   - TR[3] = max(3-1, |3-3.0|, |1-3.0|) = max(2.0, 0.0, 2.0) = 2.0 ✓
   - ATR(3) = 5.5/3 ✓
   Test math is correct.

---

*Review written: B173 Phase 2. Plan cleared for Phase 3 (ptt-architect generates tickets).*
