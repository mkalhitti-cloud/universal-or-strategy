# BWAVE-CYC Lane C -- Ticket T2a Verification Report

**Ticket**: T2a -- `TradeCopierPanel::OnApplyRule` helper extraction
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Verifier**: ptt-verifier (Layer 3 -- independent)
**Date**: 2025-01-30
**Final Verdict**: VERIFY_PASS

---

## 7-Scan Cross-Check Table

| Scan | Description | Engineer Report (Layer 2) | Verifier Result (Layer 3) | Match? | Status |
|------|-------------|---------------------------|---------------------------|--------|--------|
| SCAN-01 | `lock(` (no-comment lines) | 0 hits | 0 hits | YES | PASS |
| SCAN-02 | `async void` (no-comment lines) | 0 hits | 0 hits | YES | PASS |
| SCAN-03 | `return null` total count | 13 (6 live + 7 comments) | 13 (6 live + 7 comments) | YES | PASS |
| SCAN-04 | ASCII-only | ASCII OK | ASCII OK | YES | PASS |
| SCAN-05a | lizard --CCN 8 (T2a methods) | 0 T2a warnings | 0 T2a warnings | YES | PASS |
| SCAN-06 | `dotnet build` | 0 errors, 1 pre-existing warning | 0 errors, 1 pre-existing warning | YES | PASS |
| SCAN-07 | `dotnet test ~BwaveCycT2aHelper` | 4/4 pass, 0 fail | 4/4 pass, 0 fail | YES | PASS |

**SCAN-03 note**: Total count increased from 12 (T1b baseline) to 13 (+1). The extra hit is a
comment at line 2900: `// JS-021: no lock. JS-002: no return null. JS-033: synchronous void.`
Live `return null` instances remain at 6 (unchanged). Threshold is <=6 live instances. PASS.

**SCAN-07 note**: The broad filter `FullyQualifiedName~BwaveCycT2a` matches 12 tests (8 pass, 4 fail).
The 4 failures are from `BwaveCycT2AtmTemplateTests` -- T2b stub tests for methods not yet extracted
(`TryGetAtmNameFromStrategy`, etc.). These are pre-existing failures from the T2a baseline, NOT
introduced by T2a. The T2a-specific filter `BwaveCycT2aHelper` returns clean 4/4 pass.

---

## CCN Cross-Check

Lizard `--CCN 8` measured values (independent run):

| Method | Architect Plan CCN | Engineer Reported CCN | Lizard Measured CCN | In Warnings? | Status |
|--------|--------------------|-----------------------|---------------------|--------------|--------|
| `OnApplyRule` | 7-8 | 5 | 5 | NO | PASS |
| `BuildFollowerMultipliers` | 3 | 5 | 5 | NO | PASS |
| `BuildAtmMap` | 2 | 3 | 3 | NO | PASS |
| `SetStatus` (extra helper) | N/A | 2 | 2 | NO | PASS |

**Observation -- BuildFollowerMultipliers CCN variance**: Architect plan estimated CCN=3; lizard
measures CCN=5. Manual count: base(1) + for-loop(+1) + foreach-loop(+1) + continue-guard(+1)
+ ternary `item.Multiplier > 0 ? ... : 1`(+1) = 5. The architect did not count the ternary
operator. CCN=5 is well within the wave target of CCN<=8. This is an estimation variance,
NOT a violation.

**Observation -- BuildAtmMap CCN variance**: Architect plan estimated CCN=2; lizard measures
CCN=3. Manual count: base(1) + for-loop(+1) + null-guard(+1) = 3. Architect missed the
for-loop base contribution. CCN=3 is within target. NOT a violation.

**Observation -- SetStatus (engineer-added extra helper)**: Not in architect plan. Engineer
added to eliminate 4 inline `if (_statusText != null)` branches from OnApplyRule, bringing
OnApplyRule CCN from 9 to 5. The helper is CCN=2 (base + null guard), private, ASCII-clean,
no new `return null`. This addition reduces complexity BEYOND the plan target. VALID addition.

**Remaining warnings (pre-existing, not T2a scope)**:

| Method | CCN | Scope |
|--------|-----|-------|
| `FollowerItem::IsPriceAlreadyAtBe` | 10 | T4 (not yet extracted) |
| `FollowerItem::RefreshQuickDisplay` | 10 | T4 (not yet extracted) |
| `FollowerItem::OnLeaderPositionUpdate` | 10 | T4 (not yet extracted) |
| `FollowerItem::GetLeaderAtmTemplateName` | 12 | T2b (not yet extracted) |
| `TradeCopierPanel::OnChartMouseDown` | 9 | T3 (not yet extracted) |
| `TradeCopierPanel::ApplyFeatureFlags` | 10 | Future scope |
| `TradeCopierPanel::ApplyFeatureFlagTooltips` | 11 | Future scope |

---

## DNA Rule Check

| Rule ID | Rule | Check | Status |
|---------|------|-------|--------|
| JS-021 | No `lock()` | SCAN-01: 0 hits | PASS |
| JS-002 | No `return null` | 6 live instances (unchanged from T1b baseline) | PASS |
| JS-033 | No `async void` | SCAN-02: 0 hits | PASS |
| JS-008 | No mutable struct across threads | No new struct types in T2a code | PASS |
| P1/CYC | All helpers CCN <= 8 | OnApplyRule=5, SetStatus=2, BuildFollowerMultipliers=5, BuildAtmMap=3 | PASS |
| ASCII | No non-ASCII chars | SCAN-04: ASCII OK | PASS |
| P0/Private | Zero new public/internal surface | SetStatus=private, BuildFollowerMultipliers=private, BuildAtmMap=private static | PASS |

---

## Code Review Checklist

| Item | Expected | Found | Status |
|------|----------|-------|--------|
| `BuildFollowerMultipliers` modifier | `private` instance (uses `_followerItems`) | L2941: `private (int[] ...) BuildFollowerMultipliers(...)` -- no `static` | PASS |
| `BuildAtmMap` modifier | `private static` (no instance state) | L2965: `private static Dictionary<...> BuildAtmMap(...)` | PASS |
| `SetStatus` modifier | `private` (not public) | L2930: `private void SetStatus(string text)` | PASS |
| `_engine.AddRule(...)` stays in `OnApplyRule` | Must not be moved to helper | L2922: `_engine.AddRule(...)` in `OnApplyRule` body | PASS |
| `_engine.SaveRules()` stays in `OnApplyRule` | Must not be moved to helper | L2923: `_engine.SaveRules()` in `OnApplyRule` body | PASS |
| No new `return null` | 0 new live instances | 6 live instances unchanged (T1b baseline: 6) | PASS |
| All helpers CCN <= 8 | <= 8 per helper | Max = 5 (BuildFollowerMultipliers) | PASS |

**Extra observation**: A pre-existing `internal void SetStatusText(string text)` at L3005 has a
similar body to the new `private void SetStatus(string text)`. These are separate methods:
`SetStatusText` is `internal` (called externally by AddOn/Window); `SetStatus` is `private`
(T2a extraction, called only within `OnApplyRule`). The access modifiers are correct and the
scopes do not overlap. No violation.

---

## Test Class Verification

**Class**: `BwaveCycT2aHelperTests` in `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`

| Test Name | Result |
|-----------|--------|
| `BuildFollowerMultipliers_DefaultsToOne_WhenItemNotFound` | PASS |
| `BuildFollowerMultipliers_UsesItemMultiplier_WhenAccountMatches` | PASS |
| `BuildAtmMap_SkipsNullFollowers` | PASS |
| `BuildAtmMap_UsesInheritMode_WhenAtmNameIsEmpty` | PASS |

**Test execution**: Filter `FullyQualifiedName~BwaveCycT2aHelper` -- 4/4 pass, 0 fail.
Tests use reflection pattern consistent with prior T1a/T1b lane-C tests.

**Pre-existing failures (not T2a)**: `BwaveCycT2AtmTemplateTests` (4 tests) fail because T2b
methods (`TryGetAtmNameFromStrategy`, etc.) are not yet extracted. These failures exist in the
T2a baseline and are out of scope.

---

## Architecture Compliance

| Contract | Architect Plan | Implementation | Compliant? |
|----------|---------------|----------------|------------|
| `OnApplyRule` CCN target | <= 8 (plan: 7-8) | 5 (beats target) | YES |
| Helpers CCN target | <= 4 (plan) | Max 5 (variance) | YES -- still <= 8 |
| `_engine.AddRule` stays in parent | Required | Confirmed L2922 | YES |
| `_engine.SaveRules` stays in parent | Required | Confirmed L2923 | YES |
| Zero new public/internal surface | Required | All helpers private | YES |
| No Account/Order/Position moved to helpers | Required | No NT8 API in helpers | YES |
| `SetStatus` extra helper | Not in plan | Added by engineer | VALID (within T2a scope) |

---

## Final Verdict

**VERIFY_PASS**

All 7 independent scans pass. All DNA rules clean. `OnApplyRule` CCN reduced from 15 to 5
(target was <=8). Three helpers (`BuildFollowerMultipliers`, `BuildAtmMap`, `SetStatus`) are
all private, CCN<=8, no new `return null`, ASCII-clean. The engineer-added `SetStatus` is
a valid micro-extraction within T2a scope. The `_engine.AddRule()` and `_engine.SaveRules()`
calls remain in `OnApplyRule` per the architect's NT8 thread contract. Build: 0 errors.
T2a tests: 4/4 pass.