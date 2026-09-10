# PTT-REPAIRS-08-JS002 Plan Review
**Status**: REVIEW_PASS
**Phase**: 2 (Plan Review)
**Epic**: PTT-REPAIRS-08-JS002
**Reviewer**: ptt-plan-reviewer
**Review Date**: 2026-09-10
**Review Cycle**: 2 of 2 (Correction Cycle 2)
**Plan Author**: ptt-architect
**Plan File**: `docs/brain/PTT-REPAIRS-08-JS002/02-architecture-plan.md`
**Prior Cycle**: Cycle 1 — REVIEW_FAIL (V1-V6: missing caller lists and 7-scan checklists per ticket)

---

## Verdict: REVIEW_PASS

**Active blocking violations: 0**

All 6 violations from Cycle 1 (V1–V6) have been resolved. No new violations found.
Phase 3 (ticket generation) is UNLOCKED.

---

## Cycle 1 Violations — Resolved

| ID | Cycle 1 Description | Resolution in Cycle 2 |
|----|--------------------|-----------------------|
| V1 | Ticket T1 missing inline caller list | T1 `**Callers:**` subsection added (plan lines 483–495) — CLOSED |
| V2 | Ticket T1 missing embedded 7-scan checklist | T1 `7-Scan Checklist (T1 pre-assessment)` block added (plan lines 503–534) — CLOSED |
| V3 | Ticket T2 missing inline caller list | T2 `**Callers:**` subsection added (plan lines 575–602) — CLOSED |
| V4 | Ticket T2 missing embedded 7-scan checklist | T2 `7-Scan Checklist (T2 pre-assessment)` block added (plan lines 610–644) — CLOSED |
| V5 | Ticket T3 missing inline caller list | T3 `**Callers:**` subsection added (plan lines 664–670) — CLOSED |
| V6 | Ticket T3 missing embedded 7-scan checklist | T3 `7-Scan Checklist (T3 pre-assessment)` block added (plan lines 677–711) — CLOSED |

---

## Per-Item Findings

---

### 1. LANE-SPLIT GATE

**Result**: PASS

- Gate result explicitly stated: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` (Plan Section 1). ✓
- Q1=NO, Q2=NO, Q3=YES, Q4=YES — all four answers present with justification. ✓
- Default rule correctly invoked: SINGLE-PIPELINE selected. ✓
- Justification grounded: all 15 fixes are annotation-only or cosmetic `return null` → `return default`
  substitutions; no cross-site dependencies; parallel lane overhead not justified. ✓

**Finding**: PASS

---

### 2. PER-SITE COVERAGE (15 sites)

**Result**: PASS

All 15 required sites verified against `src/PropTraderTools/CopyEngine.cs`:

| Site | Spec Line | Method | Plan Section | Source Verified | Fix Strategy | Status |
|------|-----------|--------|--------------|-----------------|--------------|--------|
| 1 | L1271 | FindBePosition | Site 1 | L1271 `return null`; return type `NinjaTrader.Cbi.Position` (non-nullable) | Annotate → `Position?` | PASS |
| 2 | L1997 | FindMatchingRule | Site 2 | L1997 `return null`; return type already `CopyRule?` | `return default` | PASS |
| 3 | L3030 | CaptureLinkedTargetPrice | Site 3 | L3030 `return null`; return type already `double?` | `return default` | PASS |
| 4 | L3135 | FindLeaderCollateralOrder (path 1) | Site 4+5 | L3135 `return null`; return type `Order` (non-nullable) | Annotate → `Order?` | PASS |
| 5 | L3143 | FindLeaderCollateralOrder (path 2) | Site 4+5 | L3143 `return null` same method | Annotate → `Order?` | PASS |
| 6 | L3951 | FindFollowerBracketOrder | Site 6 | L3929 method signature already `Order?` | ALREADY COMPLIANT — no code change | PASS |
| 7 | L4150 | FindFollowerEntryOrder | Site 7 | L4132 method signature already `Order?` | ALREADY COMPLIANT — no code change | PASS |
| 8 | L4439 | FindFollowerRuleForOrder | Site 8 | L4439 `return null`; return type already `CopyRule?` | `return default` | PASS |
| 9 | L5955 | ResolveNullFollowerSlot (path 1) | Sites 9–10 | L5955 `return null; // NT8 pattern` | Annotate → `Account?`; preserve `return null` verbatim | PASS |
| 10 | L5977 | ResolveNullFollowerSlot (path 2) | Sites 9–10 | L5977 `return null; // NT8 pattern` | Annotate → `Account?`; preserve `return null` verbatim | PASS |
| 11 | L5990 | FindRule (path 1) | Sites 11–12 | L5990 `return null; // Change 8: null guard`; return type already `CopyRule?` | `return default` | PASS |
| 12 | L5996 | FindRule (path 2) | Sites 11–12 | L5996 `return null`; same method | `return default` | PASS |
| 13 | L6080 | FindPosition | Site 13 | L6080 `return null`; return type `Position` (non-nullable) | Annotate → `Position?` + propagation | PASS |
| 14 | L7355 | ResolveMultipliers | Site 14 | L7355 `return null`; return type `int[]` (non-nullable) | Annotate → `int[]?` + L7295 caller | PASS |
| 15 | L7370 | FindFollowerAccount | Site 15 | L7363 method signature already `Account?` | ALREADY COMPLIANT — no code change | PASS |

Note: Plan correctly identifies that Sites 11–12 (spec label "ResolveNullFollowerSlot") are
actually in `FindRule`. This is a mission-brief labeling error; the plan's fix target is correct.

**Finding**: PASS

---

### 3. CONSTRAINT COMPLIANCE

**Result**: PASS

| Constraint | Plan Evidence | Source Verification | Finding |
|------------|--------------|---------------------|---------|
| No lock() introduced (JS-021) | Section 8 declares no threading changes; all changes are annotation-only | No `lock(` in any proposed code snippet; SCAN-1 per ticket verifies zero hits | PASS |
| No new throw (JS-001) | Section 8 declares no throw added | No `throw` keyword in any proposed change | PASS |
| CYC ≤ 8 all methods (JS-013) | Section 5 CYC table; all annotation-only (no branches added) | Highest pre/post CYC is `FindFollowerBracketOrder` at 8 (unchanged); all others ≤ 5 | PASS |
| ASCII-only | All code snippets use ASCII; NT8-pattern comments are ASCII | Verified in plan code blocks and SCAN-2 per ticket | PASS |
| NT8-pattern behavior unchanged (REQ-NT8-CONTRACT) | Sites 9–10: both `return null` preserved verbatim; strategy = annotate only | Source L5955, L5977 confirmed with `// NT8 pattern: null = slot could not be resolved` | PASS |
| All callers addressed (REQ-CALLER-PROPAGATION) | Section 3 per-site + per-ticket caller lists | L3301 `Order leaderLeg` → `Order?`; L7295 `int[] multipliers` → `int[]?`; IsFlat/SubmitMarketFlattenOrder param annotations; FindPositionPublic external callers already null-guard | PASS |

**Finding**: PASS

---

### 4. 7-SCAN CHECKLIST EMBEDDED PER TICKET

**Result**: PASS (all Cycle 1 V2/V4/V6 violations resolved)

All three tickets now embed a complete per-ticket 7-scan checklist with pre-assessment:

| Ticket | Block Title | Scans Present | Pre-assessments Present |
|--------|-------------|---------------|------------------------|
| T1 | `7-Scan Checklist (T1 pre-assessment)` | SCAN-1 through SCAN-7 | Yes — all 7 | PASS |
| T2 | `7-Scan Checklist (T2 pre-assessment)` | SCAN-1 through SCAN-7 | Yes — all 7 | PASS |
| T3 | `7-Scan Checklist (T3 pre-assessment)` | SCAN-1 through SCAN-7 | Yes — all 7 | PASS |

The global 7-scan template in Section 10 is also preserved as reference. Each ticket is
self-contained for engineer use without requiring cross-reference to Section 10.

**Finding**: PASS

---

### 5. PER-TICKET COMPLETENESS: SPEC REQ IDs, SIGNATURES, CALLER LIST, 7-SCAN

**Result**: PASS (all Cycle 1 V1/V3/V5 violations resolved)

**Ticket T1:**
- Spec req IDs: `JS-002 (4 methods, 6 return null sites)` ✓
- Method signatures: All 4 signatures explicit (FindMatchingRule, CaptureLinkedTargetPrice, FindFollowerRuleForOrder, FindRule) ✓
- Caller list (`**Callers:**`): All 4 methods have call sites with line numbers and null-guard status ✓
  - FindMatchingRule: L1522, HasValue pattern, no change needed
  - CaptureLinkedTargetPrice: L2838/L2842, HasValue pattern, no change needed
  - FindFollowerRuleForOrder: L4390/L4391, HasValue pattern, no change needed
  - FindRule: struct-nullable, no propagation needed
- 7-scan checklist: Embedded with PASS pre-assessments ✓

**Ticket T2:**
- Spec req IDs: `JS-002 (8 sites across 7 methods)` ✓
- Method signatures: All 7 post-fix signatures explicit ✓
- Caller list (`**Callers:**`): All methods with call sites and null-guard status ✓
  - FindBePosition: L1248 already null-guards
  - FindLeaderCollateralOrder: L3301 **requires** `Order? leaderLeg =` annotation
  - ResolveNullFollowerSlot: L5938 already null-guards
  - FindPosition: 20+ callers — all enumerated by type (IsFlat pattern, explicit null check, SubmitMarketFlattenOrder)
  - IsFlat: parameter annotation only
  - SubmitMarketFlattenOrder: parameter annotation only
  - FindPositionPublic: 3 external callers (TradeCopierPanel.cs x2, PttBreakEvenSwap.cs x1) all null-guard
- 7-scan checklist: Embedded with PASS pre-assessments ✓

**Ticket T3:**
- Spec req IDs: `JS-002 (1 site: ResolveMultipliers)` ✓
- Method signature: `internal static int[]? ResolveMultipliers(CopyRuleDto dto)` ✓
- Caller list (`**Callers:**`): L7295 `int[] multipliers =` → `int[]? multipliers =`; downstream CopyRule.Create accepts null ✓
- 7-scan checklist: Embedded with PASS pre-assessments ✓

**Finding**: PASS

---

### 6. CALLER PROPAGATION CORRECTNESS

**Result**: PASS

All caller annotations verified against source:

| Method Changed | Caller Site | Source Confirmed | Action Required | Status |
|----------------|-------------|-----------------|-----------------|--------|
| FindBePosition → `Position?` | L1248: `if (pos == null || pos.Quantity == 0)` | ✓ | None (already null-guards) | PASS |
| FindMatchingRule (`return default`) | L1522: `if (matchedRule == null)` | ✓ | None (struct-nullable; semantically identical) | PASS |
| CaptureLinkedTargetPrice (`return default`) | L2838/L2842: `if (capturedTargetPrice.HasValue)` | ✓ | None | PASS |
| FindLeaderCollateralOrder → `Order?` | L3301: `Order leaderLeg =` | ✓ — confirmed non-nullable variable | Annotate: `Order? leaderLeg =` | PASS |
| FindFollowerRuleForOrder (`return default`) | L4390/L4391: `if (!matchedRule.HasValue)` | ✓ | None | PASS |
| ResolveNullFollowerSlot → `Account?` | L5938: `if (resolved != null) yield return resolved` | ✓ | None (already null-guards) | PASS |
| FindRule (`return default` x2) | Struct-nullable callers | ✓ | None | PASS |
| FindPosition → `Position?` | 20+ callers: IsFlat() or explicit null check | ✓ | IsFlat param + SubmitMarketFlattenOrder param annotations | PASS |
| IsFlat param → `Position? pos` | All callers pass FindPosition() result or explicit var | ✓ | None additional | PASS |
| SubmitMarketFlattenOrder param → `Position? pos` | Callers already pass nullable-compatible values | ✓ | None additional | PASS |
| FindPositionPublic → `Position?` | TradeCopierPanel.cs L1566, L2037; PttBreakEvenSwap.cs L77 | ✓ (all null-guard) | None (var inference + NRT disabled) | PASS |
| ResolveMultipliers → `int[]?` | L7295: `int[] multipliers =` | ✓ — confirmed non-nullable variable | Annotate: `int[]? multipliers =` | PASS |

Source-verified propagation changes in CopyEngine.cs only: L3301, L7295, IsFlat param, SubmitMarketFlattenOrder param, FindPositionPublic return.
No code edits required in external files (TradeCopierPanel.cs, PttBreakEvenSwap.cs).

**Finding**: PASS

---

### 7. SPEC REQUIREMENT MAPPING

**Result**: PASS

| Req ID | Description | Plan Coverage | Finding |
|--------|-------------|--------------|---------|
| REQ-JS002 | Methods must not return null | All 15 sites addressed: 3 already compliant, 6 receive `return default`, 6 receive `T?` annotation | PASS |
| REQ-NT8-CONTRACT | ResolveNullFollowerSlot preserves runtime null; annotate only | Sites 9–10: `Account?` annotation + both `return null` preserved verbatim with NT8-pattern comments | PASS |
| REQ-CALLER-PROPAGATION | All callers of changed methods updated for nullable | L3301, L7295, IsFlat, SubmitMarketFlattenOrder, FindPositionPublic all addressed; external callers confirmed null-guarded | PASS |
| REQ-CYC-BUDGET | No method exceeds CYC=8 | Section 5 table: all methods ≤ 8; annotation-only changes = zero CYC delta | PASS |
| REQ-BUILD | dotnet build = 0 Error(s) | NRT disabled (no `<Nullable>enable</Nullable>`); `T?` annotations informational; all caller variable types updated; external callers use `var` inference | PASS (design level) |
| REQ-TEST | dotnet test ≥ 19 passed, no regressions | xUnit tests specified per ticket (T1: 4 facts, T2: 4 facts, T3: 3 facts); zero behavior change at all sites except annotation | PASS (design level) |

**Finding**: PASS

---

### 8. JANE STREET DNA COMPLIANCE TABLE

**Result**: PASS

| Rule | Status | Evidence |
|------|--------|----------|
| JS-001 (no throw in OnOrderUpdate/gate chain) | PASS | Annotation-only changes; no `throw` keyword introduced in any proposed code |
| JS-002 (no return null) | PASS | All 15 sites addressed: annotate `T?` or replace with `return default` |
| JS-003 (no magic string for discriminated state) | N/A | No discriminated state changes |
| JS-008 (mutable fields on struct / SolidColorBrush) | N/A | No new struct or brush usage |
| JS-009 (Dictionary for shared collection) | N/A | No new collection types |
| JS-010 (public constructor on singleton/struct) | N/A | No new types introduced |
| JS-013 (CYC ≤ 8) | PASS | Section 5 CYC table; no new branches; highest = 8 (unchanged) |
| JS-021 (no lock/Monitor/Mutex for state) | PASS | Annotation-only; no threading changes; Section 8 confirms |
| JS-023 (UI update off-thread) | N/A | No UI changes |
| NT8: no async/await in OnInitialize/OnDestroyed | N/A | No lifecycle method changes |
| NT8: no Account.All in constructor | N/A | FindFollowerAccount uses Account.All in a private static method (existing behavior) |
| NT8: no CreateOrder without PTT- prefix | N/A | No new CreateOrder calls |
| NT8: DateTime.UtcNow (not DateTime.Now) | N/A | No DateTime usage introduced |
| NT8: no sealed TradeCopierWindow | N/A | No window changes |
| NT8: no FontFamily override | N/A | No UI changes |
| NT8: no hardcoded #RRGGBB hex | N/A | No hardcoded colors |

**Finding**: PASS

---

### 9. NT8 API SURFACE

**Result**: PASS

Plan Section 7 confirms no new NT8 API calls. All changes are pure type annotation changes.
NT8 runtime contract unchanged for all affected methods:
- `Account.Positions` iteration: unchanged
- `Order` properties (Name, State, Type, Instrument, Quantity): unchanged
- `Account.All` usage in `FindFollowerAccount`: existing behavior, already `Account?`

**Finding**: PASS

---

### 10. NULLABLE CONTEXT ANALYSIS

**Result**: PASS

Plan Section 2 correctly characterizes the project's NRT posture:
- LangVersion 9.0, no `<Nullable>enable</Nullable>` → annotations are informational only
- Struct-nullable (`CopyRule?`, `double?`) = `Nullable<T>` with `HasValue` — `return null` compiles but triggers JS-002 TEXT linter → correct fix: `return default`
- Reference-type nullable (`Position?`, `Order?`, `Account?`, `int[]?`) = informational annotation → no compiler enforcement, no new warnings
- Two-strategy approach (strategy a: annotate `T?`; strategy b: `return default`) is correctly assigned to each site

**Finding**: PASS

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|-------------|
| LANE-SPLIT GATE result stated | YES | Section 1 |
| Q1/Q2 answers with justification | YES | Section 1 |
| SINGLE-PIPELINE selection with rationale | YES | Section 1 |
| All 15 JS-002 sites listed | YES | Section 3 |
| Fix strategy per site (a or b) | YES | Section 3 |
| CYC projections for all changed methods | YES | Section 5 |
| No lock() constraint met | YES | Section 8 |
| No throw constraint met | YES | Section 8 |
| NT8 API surface documented | YES | Section 7 |
| NT8-pattern null behavior preserved (L5955, L5977) | YES | Sites 9–10, Section 6 T2 |
| Caller propagation for all changed methods | YES | Section 3 per-site |
| 7-scan checklist template in plan | YES | Section 10 |
| Ticket T1: spec req IDs | YES | Section 6 T1 |
| Ticket T1: method signatures | YES | Section 6 T1 |
| Ticket T1: caller list | YES | Section 6 T1 — RESOLVED (was V1) |
| Ticket T1: 7-scan checklist | YES | Section 6 T1 — RESOLVED (was V2) |
| Ticket T2: spec req IDs | YES | Section 6 T2 |
| Ticket T2: method signatures | YES | Section 6 T2 |
| Ticket T2: caller list | YES | Section 6 T2 — RESOLVED (was V3) |
| Ticket T2: 7-scan checklist | YES | Section 6 T2 — RESOLVED (was V4) |
| Ticket T3: spec req IDs | YES | Section 6 T3 |
| Ticket T3: method signatures | YES | Section 6 T3 |
| Ticket T3: caller list | YES | Section 6 T3 — RESOLVED (was V5) |
| Ticket T3: 7-scan checklist | YES | Section 6 T3 — RESOLVED (was V6) |
| REQ-JS002 satisfied | YES | Sections 3, 6 |
| REQ-NT8-CONTRACT satisfied | YES | Sites 9–10 |
| REQ-CALLER-PROPAGATION satisfied | YES | Sections 3, 6 T2, 6 T3 |
| REQ-CYC-BUDGET satisfied | YES | Section 5 |
| REQ-BUILD satisfied at design level | YES | Sections 4, 9 |
| REQ-TEST satisfied at design level | YES | Section 6 (xUnit tests per ticket) |

---

## Summary

The architect has fully resolved all 6 violations from Correction Cycle 1. The revised plan now
satisfies all mandatory checks:

1. **LANE-SPLIT GATE**: Explicitly stated SINGLE-PIPELINE with full Q1–Q4 justification.
2. **Per-site coverage**: All 15 sites covered with correct fix strategy per site.
3. **Constraint compliance**: No lock(), no throw, CYC ≤ 8, ASCII-only, NT8-pattern preserved.
4. **7-scan per ticket**: All three tickets embed the full 7-scan checklist with pre-assessments.
5. **Per-ticket completeness**: All three tickets have spec req IDs, method signatures, caller lists, and 7-scan checklists.
6. **Spec requirements**: REQ-JS002, REQ-NT8-CONTRACT, REQ-CALLER-PROPAGATION, REQ-CYC-BUDGET, REQ-BUILD, REQ-TEST all addressed.

No Jane Street DNA violations. No NT8 API violations. No new violations found in Cycle 2.

---

## Return

**REVIEW_PASS**

Phase 3 (ticket generation) is UNLOCKED. The architect may proceed to generate `04-tickets.md`.

---

*ptt-plan-reviewer · PTT-REPAIRS-08-JS002 · 2026-09-10 · Cycle 2*
