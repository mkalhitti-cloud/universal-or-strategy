# Plan Review: DW-LB-GR-01 BE Retry Logic Bug Fix

**Block**: DW-LB-GR-01  
**Epic**: DW-LB-GR-01 BE Retry Logic Bug Fix  
**Review Phase**: 2 -- Plan Review (cycle 2)  
**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-09-07  
**Input**: `docs/brain/DW-LB-GR-01/02-architecture-plan.md` (revised)  
**Source verified**: `src/PropTraderTools/CopyEngine.cs` L6099-L6165  

---

## Verdict

**REVIEW_PASS**

All three cycle-1 violations are resolved. All original checks pass. No new violations introduced.

---

## Cycle-1 Violation Re-Check

| ID | Violation | Status | Evidence |
|----|-----------|--------|---------|
| V-01 | SCAN-3 (async void grep) absent from § 5 | **FIXED** | § 5 row SCAN-3: `grep -rn "async\s*void" src/PropTraderTools/CopyEngine.cs` → 0 async void. Present and correctly specified. |
| V-02 | SCAN-5 (dotnet build) absent from § 5 | **FIXED** | § 5 row SCAN-5: `dotnet build` → 0 errors. Present and correctly specified. |
| V-03 | SCAN-6 (dotnet test) absent from § 5 | **FIXED** | § 5 row SCAN-6: `dotnet test` → All prior tests pass; 3 new [Fact] tests pass. Present and correctly specified. |

---

## Original Check Results

### LANE-SPLIT GATE

| Check | Result |
|-------|--------|
| § 7 present and reads `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` | PASS |

### Fix Correctness

| Check | Result | Evidence |
|-------|--------|---------|
| Defect at L6118 confirmed in source | PASS | Source L6118: `if (leaderCount == 0) // (2) targets==0 path` -- matches plan § 2 exactly |
| Fix is single-token: `leaderCount` → `targetsCount` | PASS | Plan § 2 specifies exactly one predicate token change. No logic structure changes, no new branches, no signature change |
| Comment update at L6104 also planned | PASS | Plan § 2 secondary: `leaderCount==0 branch(3)` → `targetsCount==0 branch(3)` in CYC annotation |
| Caller site 1 behavioral equivalence documented | PASS | § 1 caller analysis: both `targetsCount` and `leaderCount` are 0 at L6026-6035; fix is no-op there |
| Caller site 2 defect path documented | PASS | § 1 caller analysis: `leaderCount==0` + `targetsCount>0` is the production defect path |

### Test Plan

| Check | Result | Evidence |
|-------|--------|---------|
| Exactly 3 xUnit [Fact] test cases | PASS | § 4 lists 3 named [Fact] methods |
| Test 1 is the bug scenario (fails pre-fix, passes post-fix) | PASS | `RegisterBeRetrySlotIfNeeded_LeaderZero_TargetsNonZero_DoesNotArmRetry`: `leaderCount=0`, `targetsCount=2`, asserts retry NOT armed |
| Test 2 covers correct arm (zero follower targets) | PASS | `RegisterBeRetrySlotIfNeeded_TargetsZero_LeaderNonZero_ArmsRetry` |
| Test 3 covers partial-targets arm path | PASS | `RegisterBeRetrySlotIfNeeded_PartialTargets_LeaderHasMore_ArmsRetry`: exercises L6138-6143 partial path, confirms unaffected by fix |
| Test framework: xUnit only | PASS | All tests use `[Fact]` attribute -- NUnit/MSTest absent |
| Test seam: reflection pattern consistent with existing infra | PASS | Plan references `BwaveRefactorLaneBTests.cs` pattern |

### P0 Violation Scan (DNA Block)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` introduced. `_pendingFollowerBeSlots` is `ConcurrentDictionary` (lock-free). | PASS |
| JS-001 | No `throw` in `RegisterBeRetrySlotIfNeeded` or any changed path. | PASS |
| JS-002 | Method is `void`. No `return null` applicable. | PASS |
| JS-003 | No magic string for discriminated state. | PASS |
| JS-008 | No new mutable struct fields or unfrozen brushes. | PASS |
| JS-009 | No `Dictionary<K,V>` for shared state. `ConcurrentDictionary` preserved. | PASS |
| JS-010 | No new singleton or signal struct constructors. | PASS |
| JS-033 | No `async void` introduced. | PASS |
| SCAN-03 | No FontFamily override. | PASS |
| SCAN-04 | No hardcoded `#RRGGBB` hex. | PASS |
| SCAN-05 | No `CreateOrder` call. NT8 API surface not touched. | PASS |
| SCAN-06 | No `DateTime.Now`. | PASS |
| NT8: async/await in lifecycle | No `async/await` in any NT8 lifecycle method. | PASS |
| NT8: `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` | Correctly listed as StrategyBase-only; not used. | PASS |
| NT8: `Account.Change()` | Correctly noted as silent no-op on ATM-owned brackets; not used. | PASS |
| CYC | Fix does not add branches. `RegisterBeRetrySlotIfNeeded` remains CYC=6. No method exceeds 8. | PASS |

### Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|-------------|
| Identify defect root cause (wrong variable in predicate) | YES | § 1 |
| Document both caller sites and their behavioral impact | YES | § 1 Caller Analysis |
| Specify exact file, method, and line for fix | YES | § 2 |
| Fix is single-token, no structural change | YES | § 2 |
| Update CYC comment to match corrected code | YES | § 2 secondary |
| Scope limited to 1 file, 1 method, 1 token + 1 comment | YES | § 3 |
| 3 xUnit [Fact] tests including the defect scenario | YES | § 4 |
| SCAN-1 (lizard CYC<=8) | YES | § 5 SCAN-1 |
| SCAN-2 (lock grep) | YES | § 5 SCAN-2 |
| SCAN-3 (async void grep) | YES | § 5 SCAN-3 |
| SCAN-4 (ASCII-only) | YES | § 5 SCAN-4 |
| SCAN-5 (dotnet build) | YES | § 5 SCAN-5 |
| SCAN-6 (dotnet test) | YES | § 5 SCAN-6 |
| LANE-SPLIT GATE documented | YES | § 7 |
| Out-of-scope items enumerated | YES | § 6 |
| Architecture locks acknowledged | YES | § 8 |

---

## Findings This Cycle

**Violations found**: 0  
**Warnings**: 0  

---

## Gate Decision

All cycle-1 violations resolved. All spec requirements addressed. No P0 or P1 violations in the plan.  
The engineer may proceed to ticket generation (Phase 3) and implementation (Phase 4).

**REVIEW_PASS**
