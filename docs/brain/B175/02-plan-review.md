# Plan Review — Block B175
# Defects: DW-B175-ATR-FORMULA + DW-B175-ATR-TEXTBOX-FOCUS
# Phase: 2 — Plan Review (Retry 1)
# Reviewer: ptt-plan-reviewer
# Date: 2025-01-27
# Source plan: docs/brain/B175/02-architecture-plan.md (Retry 1 — revised)
# Prior review: REVIEW_FAIL (3 violations: V1 test absent, V2 test absent, V3 underspecified)

---

## VERDICT: REVIEW_PASS

**Violations: 0**

All three prior violations (V1, V2, V3) are CLOSED in the revised plan. All Jane Street DNA
rules pass. All NT8 constraints pass. All CYC limits pass. All spec requirements addressed.
The single-pipeline routing justification is present and consistent.

---

## Prior Violation Resolution

### V1 — CLOSED

**Was**: T_B175_T1_01 was a different test scenario ("WilderDiffersFromSimpleAvg") — constant-TR
test was absent.

**Revised plan (Section 7, B175-1)**:
- Test name: `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` ✓
- Arrays: length=29, period=14 ✓
- Construction: `highs[i]=i+2, lows[i]=i, closes[i]=i+1` → H-L=2.0 constant for all i ✓
- Call: `AtrCalc.Compute(highs, lows, closes, 14)` ✓
- Assert: `result == 2.0` ✓
- Rationale: seed=(2.0*14)/14=2.0; each Wilder step=(2.0*13+2.0)/14=2.0 ✓

**Status: CLOSED**

---

### V2 — CLOSED

**Was**: No plan test matched the volatility-spike / Wilder-vs-simple comparison scenario.

**Revised plan (Section 7, B175-1)**:
- Test name: `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` ✓
- Arrays: length=29, period=14 ✓
- Bars 0..14: TR=1.0 → H=i+1, L=i, C=i+0.5 ✓
- Bars 15..28: TR=3.0 → H=i+3, L=i, C=i+1 ✓
- Seed = (1.0*14)/14 = 1.0 ✓
- Fallback comparison: 15-bar arrays (length=15 < 29), all TR=1.0 → simple avg = 1.0 ✓
- Assert: `AtrCalc.Compute(29-bar-spike, 14) > AtrCalc.Compute(15-bar-fallback, 14)` ✓

**Mathematical verification**: seed=1.0; 14 Wilder steps with TR=3.0:
each step atr=(atr*13+3.0)/14; series converges toward 3.0 from below; all 14 steps
produce atr > 1.0. Final Wilder result > 1.0 = simple avg of 15-bar fallback (1.0).
Assert Wilder > simple is MATHEMATICALLY CORRECT. ✓

**Status: CLOSED**

---

### V3 — CLOSED

**Was**: T_B175_T1_04 was "GuardArrayExactlyPeriodPlusOne" with no specific H/L/C values,
no `7.0/3.0` expected value, and no B174 regression label.

**Revised plan (Section 7, B175-1)**:
- Test name: `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` ✓
- Exact inputs: `H=[10,12,11,13]`, `L=[8,9,10,10]`, `C=[9,10,11,12]`, `period=3` ✓
- TR derivations: TR[1]=3, TR[2]=1, TR[3]=3, sum=7 ✓
- Assert: `result == 7.0/3.0` ✓
- Labeled explicitly as "B174 regression guard" ✓
- Fallback path verification: length=4, 2*3+1=7, 4<7 → Wilder's branch NOT entered ✓

**Status: CLOSED**

---

## CYC Annotation Note (informational — non-FAIL, carried forward from prior review)

The revised plan now annotates `ReadBarsToArrays` as `// CYC=2` and the CYC table reports
CYC=2 for that method. This corrects the prior inaccurate annotation of CYC=1.
CYC=2 ≤ 8 — compliant. No action required.

---

## Passing Checks (full sweep — all carried forward from prior PASS items, re-verified)

### Lane-Split Gate — PASS

| Check | Result |
|---|---|
| LANE-SPLIT GATE RESULT present | YES — Section 2: LANES-APPROVED |
| Q1=NO, Q2=NO, Q3=YES, Q4=YES | CONSISTENT with spec |
| SINGLE-PIPELINE routing with written justification | PRESENT — "Both fixes are small (<20 lines each), in non-overlapping files, and share a single sync/verify step." ACCEPTABLE |

---

### DW-B175-ATR-FORMULA Structural Coverage — PASS

| Check | Result |
|---|---|
| Wilder's seed (simple avg of first period TRs) | PRESENT — Section 4a: loop i=1..period; `atr /= period` |
| Wilder's smoothing loop (period additional steps) | PRESENT — Section 4a: `for (int i = period+1; i <= 2*period; i++)` |
| Fallback to simple avg when length < 2*period+1 but >= period+1 | PASS — Wilder's `if` guard is false; returns seed as-is, NOT 0.0 |
| NT8 overload reads up to 2*period+1 bars with ternary fallback | PRESENT — Section 4b |
| ReadBarsToArrays helper extracted | PRESENT — Section 4c |
| Array index safety in ReadBarsToArrays | PASS — barsAgo range: k=0 → barsAgo=readLen-1 (oldest), k=readLen-1 → barsAgo=0 (newest); guard bars.Count >= readLen by construction |

---

### DW-B175-ATR-TEXTBOX-FOCUS Structural Coverage — PASS

| Check | Result |
|---|---|
| `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown` in BuildAtrWidgetRows | PRESENT — Section 4e |
| `OnAtrBoxPreviewKeyDown` handler declared in AtrWidget inner class | PRESENT — Section 4d |
| `_atrFractionBox.IsKeyboardFocused` check in handler | PRESENT — `if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)` |
| `e.Handled = true` INSIDE the IsKeyboardFocused guard | PASS — inside `if` body; not executed unconditionally |
| CYC(OnAtrBoxPreviewKeyDown) = 2 | PASS — base(1) + if(+1) = 2 |
| CYC(BuildAtrWidgetRows) unchanged = 1 | PASS — subscription line is straight-line, no new branches |

---

### CYC Table — ALL COMPLIANT

| Method | File | CYC | Compliant (<=8) |
|---|---|---|---|
| `AtrCalc.Compute(double[],double[],double[],int)` | AtrCalc.cs | 8 | YES |
| `AtrCalc.Compute(ChartControl,int,string)` | AtrCalc.cs | 8 | YES |
| `AtrCalc.ReadBarsToArrays` (NEW) | AtrCalc.cs | 2 | YES |
| `AtrWidget.OnAtrBoxPreviewKeyDown` (NEW) | TradeCopierPanel.cs | 2 | YES |
| `AtrWidget.BuildAtrWidgetRows` | TradeCopierPanel.cs | 1 | YES |

CYC counting: Base=1. Each if/for/while/case/catch/&&/||/?: = +1. Manual McCabe, authoritative
per RULES_CATALOG.md JS-013.

---

### Jane Street DNA Rules — ALL PASS

| Rule | Finding |
|---|---|
| JS-001 (no throw in hot paths / event handlers) | PASS — no `throw` in any new/modified method; Compute overloads have try/catch returning 0.0; OnAtrBoxPreviewKeyDown has no throwable operations |
| JS-002 (no unexpected null return) | PASS — Compute returns double (0.0 default); ReadBarsToArrays is void with out params |
| JS-003 (no magic string for state) | N/A — no state discrimination in this block |
| JS-008 (no mutable struct fields; SolidColorBrush frozen) | N/A — no new structs or brushes |
| JS-009 (no plain Dictionary for shared state) | N/A — no new collections |
| JS-010 (no public constructor on singleton) | N/A — AtrCalc is a static class; AtrWidget is private inner class |
| JS-013 (CYC <= 8 per method) | PASS — all methods at or below limit; see CYC table above |
| JS-021 (no lock/Monitor/Mutex/SemaphoreSlim) | PASS — zero lock statements in new/modified code |
| JS-023 (no off-thread WPF update without Dispatcher.InvokeAsync) | PASS — all new code is UI-thread-only (DispatcherTimer.Tick, WPF PreviewKeyDown); Dispatcher not required |

---

### NT8 API Constraints — ALL PASS

| Constraint | Finding |
|---|---|
| NT8-001: No async/await in lifecycle methods | N/A — no lifecycle changes |
| NT8-002: No Account.All in constructor | N/A — not modified |
| NT8-003: No CreateOrder without PTT- prefix | N/A — no CreateOrder calls |
| NT8-004: No FontFamily override | PASS — no FontFamily in new code |
| NT8-005: No #RRGGBB hex colors | PASS — no color literals |
| NT8-006: No DateTime.Now | PASS — no timestamp usage |
| NT8-007/008/009: No AtmStrategyCreate/AtmStrategyChangeStopTarget | N/A — not used |
| sealed TradeCopierWindow | N/A — not modified |
| Account.All in constructor | N/A — not modified |

---

### ASCII Compliance — PASS

All string literals and comments in new/modified code use only ASCII printable characters
(0x20–0x7E). "Wilder's" uses straight apostrophe U+0027 (standard ASCII). No Unicode, no
emoji, no curly quotes. Compliant with ASCII-001.

---

### Fallback Correctness — PASS

When `highs.Length >= period+1` but `highs.Length < 2*period+1`:
1. Early guard `if (highs.Length < period + 1 ...) return 0.0` — passes (does not return).
2. Seed loop computes simple average into `atr`.
3. Wilder's guard `if (highs.Length >= 2 * period + 1)` evaluates FALSE.
4. Smooth loop skipped.
5. `return atr` returns the simple average. NOT 0.0. NOT a partial Wilder result. CORRECT.

---

### B174 Regression Analysis — PASS (per Appendix A)

T_B173_T1_02 / T_B174_T1_01 fixture: period=3, array length=4.
Check: 4 < 2*3+1 = 7. TRUE → Wilder's branch not entered. Returns seed = 5.5/3.
Existing `Assert.Equal(5.5/3.0, result)` still passes. Analytically confirmed. ✓

---

### Test Plan Coverage — COMPLETE

| Mandate Test | Plan Test | Match |
|---|---|---|
| T-B175-T1-01: constant TR=2.0, 29 bars → 2.0 | `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` | MATCH — exact scenario, inputs, assertion ✓ |
| T-B175-T1-02: Wilder's spike result > simple avg | `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` | MATCH — 29-bar spike, 15-bar fallback comparison, math verified ✓ |
| T-B175-T1-03: 15-bar fallback → simple avg | `T_B175_T1_03_FallbackPathReturnsSimpleAvg` | MATCH — covers fallback path ✓ |
| T-B175-T1-04: B174 regression exact H/L/C → 7.0/3.0 | `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` | MATCH — exact inputs, exact expected value, B174 label ✓ |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|---|---|---|
| Wilder's ATR formula (seed + smoothing loop) | YES | §4a |
| CYC(Compute(double[])) <= 8 | YES | §4a, §5 |
| NT8 overload reads 2*period+1 bars with fallback | YES | §4b |
| ReadBarsToArrays helper extracted | YES | §4c |
| Fallback to simple avg when insufficient bars | YES | §4a |
| B174 regression: existing tests still pass | YES | Appendix A |
| PreviewKeyDown subscription in BuildAtrWidgetRows | YES | §4e |
| OnAtrBoxPreviewKeyDown handler with IsKeyboardFocused | YES | §4d |
| e.Handled=true inside IsKeyboardFocused guard | YES | §4d |
| CYC(OnAtrBoxPreviewKeyDown) = 2 | YES | §5 |
| T-B175-T1-01 (constant TR=2.0, 29 bars → 2.0) | YES | §7/B175-1 |
| T-B175-T1-02 (spike: Wilder's > simple avg) | YES | §7/B175-1 |
| T-B175-T1-03 (15-bar fallback → simple avg) | YES | §7/B175-1 |
| T-B175-T1-04 (B174 regression exact values → 7.0/3.0) | YES | §7/B175-1 |
| JS-001/021/023 compliance | YES | §6 |
| ASCII-only | YES | §6 |
| Lane-split gate with SINGLE-PIPELINE justification | YES | §2 |
| Deferred backlog carry-forward | YES | §8 |
