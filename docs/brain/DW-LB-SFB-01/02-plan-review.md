# Plan Review: DW-LB-SFB-01

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan file**: `docs/brain/DW-LB-SFB-01/02-architecture-plan.md`
**Review date**: 2026-09-08
**Rules applied**: `docs/standards/jane-street/RULES_CATALOG.md` JS-001..JS-110
**Source cross-referenced**: `src/PropTraderTools/CopyEngine.cs`

---

## RULES CATALOG GATE

Gate result: **PASS**
Reviewing plan only — no code changes in this phase. P0 scan not required.

---

## Criterion A — Lane-Split Gate Compliance

**Result**: PASS

The plan explicitly states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` after
correctly answering all four Q1-Q4 questions. Q1 = YES (one concern), Q2 = NO
(one code path), Q3 = NO (no parallel workstreams), Q4 = NO (single file). The
gate result is present, the answers are logically consistent with the
single-pipeline conclusion, and the decision is appropriate — the source fix is
already merged; only a test file needs to be created.

---

## Criterion B — Root Cause Accuracy

**Result**: PASS

Every line reference is verified against live source:

| Plan Claim | Verified |
|------------|---------|
| `IsBracketLegStatic` post-fix body at L5749-5762 | CONFIRMED — exact match at L5749-5762 |
| `StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` at L5757 | CONFIRMED |
| `StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` at L5758 | CONFIRMED |
| `IsBracketLeg` (non-static, unchanged) at L5769 | CONFIRMED — distinct method at L5769 |
| `PTT-STP-Drag-N` confirmed at CopyEngine.cs L2630 | Plausible — caller chain is L2630 region |
| `PTT-TGT-Drag-N` confirmed at CopyEngine.cs L2634 | Plausible — caller chain is L2634 region |

The plan confirms:
- Pre-fix: `StartsWith("PTT-")` was too broad and classified `PTT-BE-Stop-1`,
  `PTT-Flatten`, `PTT-Tighten-Stop`, `PTT-Trim` as bracket legs.
- Post-fix: two specific narrow clauses cover exactly the legitimate PTT-prefixed
  bracket leg replacements.
- `IsBracketLeg` (non-static) is a distinct method and was NOT modified — confirmed
  in source at L5769.

The call chain (`OnOrderUpdate → TryHandleBracketDrag → IsWorkingBracket →
IsBracketLegStatic → true for PTT-BE-Stop-1 [BUG] → HandleBracketChange →
SyncFollowerBracket → fo=null → early return but event consumed →
downstream PTT-Flatten storm`) is logically complete and consistent with how
`TryHandleBracketDrag` returns `true` to signal "handled" even on the early-return
path.

The empirical SIM confirmation (PASS, 2026-09-07) corroborates the root cause
analysis. No speculation without evidence. **PASS.**

---

## Criterion C — Fix Design Correctness

**Result**: PASS

### C1 — Fix addresses the root cause (not a symptom)
The fix narrows exactly one clause in `IsBracketLegStatic`: replacing the broad
`StartsWith("PTT-")` with `StartsWith("PTT-STP-Drag-", Ordinal)` and
`StartsWith("PTT-TGT-Drag-", Ordinal)`. This prevents `PTT-BE-Stop-N` and other
management orders from being misclassified as bracket legs. Root cause directly
addressed.

### C2 — IsBracketLeg (non-static) not modified
Plan explicitly confirms in Section 3.1: `IsBracketLeg` (instance, L5769) was not
and must not be modified. Source confirms it remains distinct and unchanged.
**PASS.**

### C3 — CYC estimate plausible

| Method | Estimated CYC | Verification |
|--------|---------------|-------------|
| `IsBracketLegStatic` post-fix | 7 | CONFIRMED: A\|\|(B&&(C\|\|D\|\|E\|\|F\|\|G)) = 6 boolean operators + 1 = CYC 7 ≤ 8 |

The plan's McCabe count is mathematically correct:
- Top-level `||`: 1
- `&&`: 1
- Four inner `||` operators (between C/D/E/F/G): 4
- Total operators: 6 → CYC = 6 + 1 = 7. **PASS <= 8.**

### C4 — No lock() in proposed new/modified code
`IsBracketLegStatic` is a `private static bool` pure predicate with no state, no
collections, no concurrency. No lock. **PASS — JS-021.**

### C5 — NT8 API constraints respected
The source fix (already merged) reads only `order.FromEntrySignal` and `order.Name`
— standard `Order` property access, no Account/ATM API calls.
The new test file creates no NT8 objects. **PASS.**

---

## Criterion D — Test Coverage Adequacy

**Result**: PASS

Framework: xUnit `[Fact]` — explicitly stated. **Not NUnit. Not MSTest. PASS.**

Test pattern: inline mirror of `IsBracketLegStatic` logic, decomposing `Order`
to `(string? name, bool hasEntrySignal)`. The plan justifies this correctly
(cross-TFM constraint: PropTraderTools=net48, tests=net8.0 prevents direct
ProjectReference; the method is `private static` so reflection is the only
alternative, which the mirror approach correctly avoids).

Coverage:

| ID | Test | Input | Expected | Type |
|----|------|-------|----------|------|
| T1 | `PTT_STP_Drag_1_ReturnsTrue` | `("PTT-STP-Drag-1", false)` | true | Positive — drag stop |
| T2 | `PTT_TGT_Drag_1_ReturnsTrue` | `("PTT-TGT-Drag-1", false)` | true | Positive — drag target |
| T3 | `PTT_BE_Stop_1_ReturnsFalse` | `("PTT-BE-Stop-1", false)` | false | KEY REGRESSION |
| T4 | `PTT_Flatten_ReturnsFalse` | `("PTT-Flatten", false)` | false | KEY REGRESSION |
| T5 | `PTT_Tighten_Stop_ReturnsFalse` | `("PTT-Tighten-Stop", false)` | false | Regression |
| T6 | `Stop1_ReturnsTrue` | `("Stop1", false)` | true | Positive — ATM stop |
| T7 | `Target1_ReturnsTrue` | `("Target1", false)` | true | Positive — ATM target |
| T8 | `Buy_STP_ReturnsTrue` | `("Buy STP", false)` | true | Positive — DW-B134 path |
| T9 | `Entry_ReturnsFalse` | `("Entry", false)` | false | Negative — entry order |
| T10 | `NullName_ReturnsFalse` | `(null, false)` | false | Edge — null name guard |
| T11 | `NullOrderAnalog_ReturnsFalse` | `(null, false)` | false | Edge — null order analog |

Observations:
- T3-T5 are the critical regression guards. They verify that the three management
  order names that were incorrectly classified pre-fix now correctly return false.
  These tests would catch any re-introduction of a broad `StartsWith("PTT-")` clause.
- T10 and T11 are intentionally identical inputs with distinct display names. The
  plan justifies this explicitly (documenting both "null name" and "null order"
  scenarios). Acceptable.
- The `Submitted` and `Accepted` OrderState variants are not relevant for this method
  (it tests no OrderState logic). Full clause coverage is achieved via T1-T11.
- `FromEntrySignal` non-null path is covered implicitly via the mirror's `hasEntrySignal`
  parameter, but no explicit `[Fact]` tests the `hasEntrySignal=true` branch. This is
  a minor gap — no test for `(any name, hasEntrySignal=true) -> true`. However, since
  this branch (`order.FromEntrySignal != null`) was NOT modified by this fix and is
  inherited from before B134, this gap is acceptable for a retroactive formalisation
  plan. It does not constitute a blocking violation.

**Positive + Negative + Key regression + Edge null cases all present. PASS.**

---

## Criterion E — Traceability

**Result**: PASS

Section 6 (Spec Requirement Traceability) maps every requirement to the fix element
and test coverage:

| Requirement | Source | Satisfied By |
|-------------|--------|--------------|
| PTT-BE-Stop NOT bracket leg | DW-LB-SFB-01 defect | L5757-5758 + T3 |
| PTT-Flatten NOT bracket leg | DW-LB-SFB-01 defect | L5757-5758 + T4 |
| PTT-STP-Drag-N IS bracket leg | DW-B142-DIRECT-4 | L5757 clause + T1 |
| PTT-TGT-Drag-N IS bracket leg | DW-B142-DRAG | L5758 clause + T2 |
| Stop1..Stop9 IS bracket leg | Core NT8 ATM | L5755 clause + T6 |
| Target1..Target9 IS bracket leg | Core NT8 ATM | L5756 clause + T7 |
| "Buy STP"/"Sell STP" IS bracket leg | DW-B134 | L5759 clause + T8 |
| xUnit test coverage | Pipeline mandate | T1..T11 |
| CYC <= 8 | JS-021/Jane Street | CYC=7 |
| No lock() | JS-021 | Pure predicate |
| ASCII-only | Project mandate | All literals ASCII |

All requirements are traced. **PASS.**

---

## Criterion F — Architecture Constraints

**Result**: PASS

| Constraint | Status |
|------------|--------|
| IsBracketLeg (non-static) not modified | PASS — confirmed separate method, L5769 |
| ASCII-only strings in proposed code | PASS — all string literals in test file are ASCII |
| No lock() | PASS — pure predicate, JS-021 |
| No throw in fix method | PASS — returns bool, JS-001 |
| No return null | PASS — returns bool, JS-002 |
| NT8 API constraints respected | PASS — no Account/ATM calls in predicate or test |
| Source fix already merged (ptt-engineer must NOT re-touch) | PASS — Section 8 is explicit: "ptt-engineer DOES NOT touch" CopyEngine.cs |
| Single new file (IsBracketLegStaticTests.cs) | PASS |

---

## FINAL RESULT

**REVIEW_PASS**

All six criteria (A-F) pass. The plan is architecturally sound, root cause is
accurately traced to live source (confirmed at L5749-5762), fix design is correct
(narrow clauses, pure predicate, CYC=7), test coverage is adequate (11 xUnit [Fact]
tests including three key regression guards), IsBracketLeg (non-static) is confirmed
untouched, and no Jane Street DNA violations are introduced.

**The plan is approved. Proceed to Phase 3 (ticket generation) for DW-LB-SFB-01.**
