# Plan Review -- BWAVE-NEXT LaneBRepair-R4

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Plan reviewed**: `docs/brain/BWAVE-NEXT/LaneBRepair-R4/02-architecture-plan.md`
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b

---

## VERDICT: REVIEW_PASS

Zero violations. All five review requirement categories pass.

---

## Check 1: Lane-Split Gate Compliance

| Question | Plan Answer | Assessment |
|----------|-------------|------------|
| Gate result stated? | YES -- Section 1: "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" | PASS |
| Plan uses lanes? | NO -- explicit single-pipeline with single ticket T1 | PASS |
| Q1 answered? | YES -- "N/A -- only one fix." | PASS |
| Q2 answered? | YES -- "N/A -- no Fix B." | PASS |
| Q3 answered? | YES -- "N/A -- no Fix B." | PASS |
| Q4 answered? | YES -- "N/A -- no Fix B." | PASS |

**Result: PASS.** Single-pipeline declaration is present and consistent. No lane attempt is made, so the Q3/Q4 YES/NO trip wires are not reachable.

---

## Check 2: Source Read Verification

| Requirement | Plan Evidence | Assessment |
|-------------|---------------|------------|
| Exact line for `_pendingDispatchDrains.TryRemove` | Line 6634 (Section 2, table row (a)) | PASS |
| Exact line for `SubmitEntryDirect` | Line 6641 (Section 2, table row (c)) | PASS |
| Exact line for `foreach DrainedOrderIds` cleanup | Lines 6650-6651 (Section 2, table row (d)) | PASS |
| CONFIRMED or STALE declared | "R4-F1 is STALE" (Section 3, first line) | PASS |
| STALE: reason no code change is needed | Section 3 explains that R3-F2 already fixed the order; submit is at 6641 (before), cleanup is at 6650 (after); IDs preserved on failure is the correct design intent | PASS |
| STALE: try/finally fix pattern shown? | Section 4 provides the hypothetical pattern clearly marked "HYPOTHETICAL ONLY -- NOT NEEDED" | PASS |

**Result: PASS.** All three line numbers are cited precisely. STALE is declared. The reason (R3-F2 prior fix, correct current ordering) is explicitly documented with quoted source. The try/finally pattern is shown for reference even though it is not needed -- this is acceptable (it demonstrates reviewer awareness of what would be required if the order were wrong).

---

## Check 3: Spec Completeness

| Requirement | Plan Evidence | Assessment |
|-------------|---------------|------------|
| Acceptance criteria matches R4 prompt | Section 9 lists 9 criteria covering: source read confirmation, STALE declaration with line evidence, zero production changes, one [Fact] regression test, test name, build pass, prior tests pass, SCAN checklist, no new DW- items | PASS |
| All 11 dismissed findings recorded | Section 5 table has exactly 11 rows (CR5-outside-1/2/3, CR5-dup-1/2/3/4, CR5-test-1/2, DW-lock-1, DW-net-1) | PASS |
| Locked architecture decisions preserved | Section 6 lists 12 locked decisions, all matching prior-round locks | PASS |
| Single ticket T1 present | Section 8: "T1 -- R4-F1 STALE Verification + Regression Guard" | PASS |

**Result: PASS.** All spec completeness requirements are satisfied.

---

## Check 4: RULES_CATALOG Compliance

### P0 Rules Scan (new code in T1)

The only new code introduced is one xUnit `[Fact]` test method. Evaluated against all P0 rules:

| Rule | Check | Result |
|------|-------|--------|
| JS-001 (throw in hot path) | Test uses `Assert.Contains`, not `throw` in a production hot path | PASS |
| JS-002 (null return) | No `return null` in new code | PASS |
| JS-010 (public constructor on singleton/signal struct) | No new types introduced | PASS |
| JS-015 (unvalidated string crossing boundary) | No new boundary-crossing parameters | PASS |
| JS-021 (lock() ban) | Plan Section 8 explicitly states "No lock() -- the test uses file I/O and string search only"; SCAN-01 checklist requires zero `lock(` | PASS |
| JS-033 (async void) | Test method is `void` on a `[Fact]` -- this is a synchronous test, not `async void`. No violation | PASS |
| JS-036 / JS-037 (Span/ArrayPool) | No new buffer allocations in hot path; test-only context | PASS |

### CYC Analysis

| Item | CYC | Assessment |
|------|-----|------------|
| `SubmitDrainedEntry` (production, NO CHANGE) | Unchanged. Plan states CYC = 4 (base 1 + TryRemove guard 1 + null guard 1 + foreach 1). STALE path means zero CYC risk. | PASS |
| New `[Fact]` test method | Plan states CYC = 2 (base 1 + Assert branch 1). Within budget (<=8). | PASS |
| Hypothetical try/finally (not implemented) | Plan correctly notes try/finally adds ZERO McCabe branches; CYC would remain 4 even if applied. | INFORMATIONAL |

**Result: PASS.** No P0 violations introduced. CYC analysis is present. STALE path means no production CYC risk.

---

## Check 5: STALE Path Specific Checks

| Requirement | Plan Evidence | Assessment |
|-------------|---------------|------------|
| Line-number evidence that submit is BEFORE cleanup | Line 6641 (`SubmitEntryDirect`) < Line 6650 (`foreach DrainedOrderIds`). Both cited explicitly in Section 2 table and Section 3. | PASS |
| Reference to prior round that already fixed the order | Section 2 (line 6649 comment): "R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure." Section 3: "a prior repair round (R3-F2) already fixed the ordering." | PASS |
| Regression guard test appropriate and scoped | Test reads source file and asserts the R3-F2 comment is still present. This is a narrow, surgical regression guard -- it will fail if a future edit moves the comment or reorders the code. Scope is appropriate: one [Fact], one assertion, CYC=2, no production code change. | PASS |

**Result: PASS.** All three STALE-specific checks are satisfied with exact evidence.

---

## Violation Log

| # | Rule ID | Description | Location in Plan | Status |
|---|---------|-------------|------------------|--------|
| (none) | -- | No violations found | -- | -- |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Lane-split gate result stated | YES | Section 1 |
| Single-pipeline (one finding) | YES | Section 1 |
| TryRemove line number (6634) | YES | Section 2 |
| SubmitEntryDirect line number (6641) | YES | Section 2 |
| foreach DrainedOrderIds line number (6650) | YES | Section 2 |
| STALE / CONFIRMED declared | YES (STALE) | Section 3 |
| STALE: why no code change needed | YES | Sections 3-4 |
| try/finally pattern shown | YES (hypothetical) | Section 4 |
| All 11 dismissed findings recorded | YES | Section 5 |
| Locked architecture decisions preserved | YES | Section 6 |
| Deferred items carried forward | YES | Section 7 |
| Single ticket T1 | YES | Section 8 |
| No new DW- items | YES | Sections 7, 8 |
| SCAN-01 through SCAN-07 checklist | YES | Section 8 |
| CYC analysis for SubmitDrainedEntry | YES | Section 4 note + Section 8 |
| Acceptance criteria stated | YES | Section 9 |
| Threading model documented | YES | Section 10 |
| Component summary | YES | Section 11 |

---

## Summary

The plan is technically correct, complete, and compliant. The architect performed a direct source read, identified the exact line numbers for all three operations, correctly declared the finding STALE, traced the prior fix to R3-F2 with inline comment evidence, and proposed an appropriately scoped regression guard test as the sole deliverable. No P0 or P1 violations are present in the proposed code. CYC remains at budget for all methods. The lane-split gate is correctly declared SINGLE-PIPELINE with N/A answers. All 11 dismissed findings and all locked decisions are recorded. Zero new DW- items are generated.

**REVIEW_PASS**

---

*Review written: 2026-09-05 | ptt-plan-reviewer | Phase 2 | BWAVE-NEXT LaneBRepair-R4*
