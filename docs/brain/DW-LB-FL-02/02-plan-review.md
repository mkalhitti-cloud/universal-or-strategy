# DW-LB-FL-02 Plan Review
# Phase 2 -- PTT Plan Reviewer

**Epic**: DW-LB-FL-02
**Plan file**: `docs/brain/DW-LB-FL-02/02-architecture-plan.md` (Revision 2)
**Reviewer**: ptt-plan-reviewer
**Review cycle**: 2 of 2 (first cycle: REVIEW_FAIL V-01 CYC; second cycle: this document)
**Date**: 2026-08-22
**Source reads**: CopyEngine.cs 4650-4730 (live source, TryDispatchLeaderFlat)

---

## RETURN: REVIEW_PASS

**Blocking violation count: 0**

---

## V-01 RESOLUTION VERIFICATION

V-01 from the prior review (REVIEW_FAIL): `TryDispatchLeaderFlat` was already CYC=8 from
live source (not CYC=6 as the stale comment claimed). Adding guard 3.5 would have pushed it
to CYC=9, exceeding the JS-021/Complexity P1 limit. The architect was required to free 1 DP
before adding the new guard.

**Revision 2 resolution**: Extract guard (1)'s compound `&&` comparison into the new helper
`IsDispatchableState(state)`. The extraction moves 1 decision point from
`TryDispatchLeaderFlat` into `IsDispatchableState`, freeing exactly 1 DP budget.

**CYC re-measurement from live source (CopyEngine.cs:4679-4691):**

```
Guard (1): state != Filled && state != Cancelled   ->  if=1 + &&=1 = 2 DPs  (pre-fix)
Guard (2): isFollower(account)                      ->  if=1       = 1 DP
Guard (2.5): IsNonFlatDispatchName(orderName)       ->  if=1       = 1 DP
Guard (3): !IsNativeExitName && hasOpenPosition     ->  if=1 + &&=1 = 2 DPs
foreach (4):                                        ->             = 1 DP
Pre-fix total DPs = 7  ->  CYC_before = 1+7 = 8  (live source confirms this exactly)
```

**Post-fix after IsDispatchableState extraction + guard 3.5 insertion:**

```
Guard (1): !IsDispatchableState(state)              ->  if=1       = 1 DP  (&&moved to helper)
Guard (2): isFollower(account)                      ->  if=1       = 1 DP
Guard (2.5): IsNonFlatDispatchName(orderName)       ->  if=1       = 1 DP
Guard (3.5): IsNativeExitOnFlatLeader(...)          ->  if=1       = 1 DP  (NEW; && lives in helper)
Guard (3): !IsNativeExitName && hasOpenPosition     ->  if=1 + &&=1 = 2 DPs
foreach (4):                                        ->             = 1 DP
Post-fix total DPs = 7  ->  CYC_after = 1+7 = 8  (at limit, <= 8 PASS)
```

**CYC table (plan's authoritative table -- verified against source):**

| Method | Pre-fix (live source) | Post-fix | Limit | Status |
|--------|-----------------------|----------|-------|--------|
| TryDispatchLeaderFlat | 8 | 8 | 8 | PASS (at limit) |
| IsDispatchableState | N/A (new) | 2 | 8 | PASS |
| IsNativeExitOnFlatLeader | N/A (new) | 2 | 8 | PASS |
| IsNativeExitName | 6 (unchanged) | 6 | 8 | PASS |
| FlattenFollower | 3 (unchanged) | 3 | 8 | PASS |

Table is single, authoritative, and consistent with both the live source measurement and all
other CYC claims in Revision 2. No internal inconsistency remains. V-01 is **RESOLVED**.

---

## CHECK-BY-CHECK RESULTS

### CHECK 1 -- LANE-SPLIT GATE: PASS

Gate result explicitly stated: **SINGLE-PIPELINE** (plan section "LANE-SPLIT GATE RESULT").
All 4 questions answered:
- Q1: Same method, all changes in one file (`CopyEngine.cs`). YES. ✓
- Q2: N/A -- single fix, no Fix B. Correctly stated as N/A with rationale. ✓
- Q3: N/A -- single fix. Correctly stated as N/A with rationale. ✓
- Q4: Single SIM verification path defined. YES. ✓

### CHECK 2 -- V-01 RESOLVED: CYC baseline from live source: PASS

- Pre-fix CYC measured from CopyEngine.cs:4679-4691 (live source): **8**. ✓
  (Stale comment at line 4659 says "CYC=6" -- plan acknowledges this is stale and measures
  from live guards directly. Correct.)
- `IsDispatchableState` declared `internal static bool`, CYC=2 (`||` = 1 DP). ✓
- Post-fix `TryDispatchLeaderFlat` CYC = 8 (extraction frees 1 DP, guard 3.5 uses it). ✓
- CYC table is single and authoritative; values are consistent throughout Revision 2. ✓

### CHECK 3 -- DW-B65-01 REGRESSION GUARD PRESERVED: PASS

Guard 3.5 logic when leader HAS open position (the DW-B65-01 case):
```
IsNativeExitOnFlatLeader("Close", LEADER, ES, hasOpenPosition)
= IsNativeExitName("Close") && !hasOpenPosition(LEADER, ES)
= true && !true          (leader has position -- NT8 lag case)
= true && false
= false                  --> guard 3.5 does NOT block
```
Dispatch continues to guard (3), which also does not block (native exit bypass), and
`FlattenFollower` is called for each follower. DW-B65-01 preserved. ✓

Verified at plan section "DATA FLOW: DW-B65-01 REGRESSION CHECK". Logic is correct.

### CHECK 4 -- OPTION A GUARD 3.5 LOGIC CORRECT: PASS

`IsNativeExitOnFlatLeader` returns true if and only if:
1. `IsNativeExitName(orderName)` is true (native NT8 exit: "Close", "Flatten", "Exit*", "Rev*"), AND
2. `!hasOpenPosition(account, instrument)` is true (leader IS flat)

Defect scenario (leader flat after PTT-BE-Stop fill):
```
= IsNativeExitName("Close") && !hasOpenPosition(LEADER, ES)
= true && !false     (leader is flat)
= true && true
= true               --> returns false (guard 3.5 BLOCKS dispatch) ✓
```

Normal exit scenario (leader has position -- DW-B65-01):
```
= true && !true      (leader has position)
= true && false
= false              --> guard 3.5 does NOT block ✓
```

Non-native-exit name (e.g. PTT-BE-Stop-*):
```
= IsNativeExitName("PTT-BE-Stop-12345") && ...
= false && ...
= false              --> guard 3.5 does NOT block (already blocked by guard 2.5) ✓
```

The guard is a correct minimal discriminator. Logic verified. ✓

### CHECK 5 -- 7-SCAN CHECKLIST COMPLETE: PASS

All 7 scans present with pass/fail predictions:

| Scan | Description | Present | Result |
|------|-------------|---------|--------|
| SCAN-01 | lock() grep | YES | PASS (no lock in new code) |
| SCAN-02 | async void grep | YES | PASS (no new async methods) |
| SCAN-03 | return null grep | YES | PASS (all new methods return bool) |
| SCAN-04 | CYC complexity | YES | PASS (manual McCabe, 8 at limit) |
| SCAN-05 | ASCII-only grep | YES | PASS (all identifiers ASCII) |
| SCAN-06 | NT8 API compliance | YES | PASS (no banned calls) |
| SCAN-07 | xUnit test coverage | YES | 10 [Fact] methods specified |

All 7 items present. ✓

### CHECK 6 -- xUnit [Fact] TESTS SPECIFIED (>= 7): PASS

10 named `[Fact]` methods specified (plan section "7-SCAN CHECKLIST", SCAN-07):

| Test | Covers |
|------|--------|
| IsDispatchableState_WhenFilled_ReturnsTrue | helper unit |
| IsDispatchableState_WhenCancelled_ReturnsTrue | helper unit |
| IsDispatchableState_WhenWorking_ReturnsFalse | helper unit |
| IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlat_ReturnsTrue | helper unit |
| IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse | helper unit (regression path) |
| IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse | helper unit |
| TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers | **defect case** |
| TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers | **DW-B65-01 regression** |
| TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers | edge case |
| TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers | edge case |

Count: 10 >= 7. Defect case covered. Regression case covered. `IsDispatchableState` helper
covered (3 facts). All use delegate injection (no NT8 runtime needed). ✓

Visibility note (carried from V-01 review, now resolved): `IsNativeExitOnFlatLeader` is
specified as `internal static` in the method signature block (plan line 183) and SCAN-07
confirms `internal static` for test access. The inconsistency from the first review cycle
is fully resolved. ✓

### CHECK 7 -- NT8 API COMPLIANCE: PASS

Verified from NT8 API GATE section (plan) and NT8_FULL_REFERENCE.md / NT8_ADDON_KNOWLEDGE.md:

| API | AddOnBase? | Used in fix? | Status |
|-----|-----------|--------------|--------|
| AtmStrategyCreate() | StrategyBase ONLY | No | PASS |
| AtmStrategyChangeStopTarget() | StrategyBase ONLY | No | PASS |
| Account.Change() | AddOnBase (no-op on ATM) | No | PASS |
| Account.Positions (via hasOpenPosition delegate) | AddOnBase readable | Yes (existing) | PASS |
| DateTime.Now | BANNED | No | PASS |
| FontFamily override | BANNED | No | PASS |
| CreateOrder without PTT- prefix | BANNED | No (not used) | PASS |

No banned NT8 API usage in new or modified code. ✓

### CHECK 8 -- NT8 SIM GATE (6 STEPS): PASS

6 steps present (plan section "NT8 SIM GATE STEPS"):

| Step | Description | Match defect spec? |
|------|-------------|-------------------|
| Step 1 | Enter long on leader, verify both accounts | YES |
| Step 2 | Trigger BE stop fill, verify leader+follower flat | YES |
| Step 3 | Click Close on already-flat leader | YES (exact defect trigger) |
| Step 4 | PASS criteria: zero PTT-Flatten, no loop, follower stays flat | YES |
| Step 5 | FAIL criteria: loop still present | YES |
| Step 6 | DW-B65-01 regression: fresh long + immediate Close -> follower flattens | YES |

All 6 steps match the defect scenario (Clone mode + BE ALL + "Close" on flat leader).
DW-B65-01 regression step explicitly included. ✓

---

## DNA RULE SWEEP (new/modified code only)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock ban) | No lock() in IsDispatchableState, IsNativeExitOnFlatLeader, TryDispatchLeaderFlat | PASS |
| JS-001 (no throw in hot path) | No throw in any new or modified guard | PASS |
| JS-002 (no return null) | All new methods return bool; no null return path | PASS |
| JS-033 (no async void) | No new async methods | PASS |
| JS-036/037 (no hot-path alloc) | No new allocations in any guard | PASS |
| ASCII-only | All new identifiers: IsDispatchableState, IsNativeExitOnFlatLeader, orderName, account, instrument, hasOpenPosition, state -- all ASCII | PASS |
| DateTime.Now ban (SCAN-06) | Not used | PASS |
| Hardcoded #RRGGBB hex (SCAN-04) | No UI code introduced | PASS |
| sealed TradeCopierWindow | Not applicable (no window code) | PASS |
| CreateOrder without PTT- prefix | Not applicable (no order creation) | PASS |
| CYC <= 8 | TryDispatchLeaderFlat=8, IsDispatchableState=2, IsNativeExitOnFlatLeader=2 | PASS |

**Zero violations across all DNA rules.** ✓

---

## SPEC COVERAGE MATRIX

| Requirement | Addressed? | Plan section |
|-------------|------------|--------------|
| Block PTT-Flatten when leader is already flat (RC-1) | YES | Component Design, guard 3.5 |
| Preserve DW-B65-01 native-exit bypass (position lag) | YES | DATA FLOW: DW-B65-01 Regression Check |
| RC-2: IsNonFlatDispatchName not modified for "Close" | YES | Confirmed Root Causes section |
| Fix in TryDispatchLeaderFlat only (minimal surface) | YES | Component Design, one file three changes |
| No Option B (FlattenFollower cancel layer) | YES | Rationale section, clearly rejected with 4 reasons |
| CYC <= 8 post-fix | YES | CYC Complexity Analysis (Revision 2, authoritative) |
| xUnit tests for defect + regression cases | YES | SCAN-07, 10 [Fact] methods |
| NT8 SIM verification path | YES | NT8 SIM GATE STEPS, 6 steps |

All spec requirements addressed. ✓

---

## SUMMARY

| Check | Result |
|-------|--------|
| 1. Lane-split gate | PASS |
| 2. V-01 resolved (CYC baseline from live source, pre=8, post=8) | PASS |
| 3. DW-B65-01 regression guard preserved | PASS |
| 4. Option A guard 3.5 logic correct | PASS |
| 5. 7-scan checklist complete (7/7) | PASS |
| 6. xUnit [Fact] tests >= 7 (10 specified) | PASS |
| 7. NT8 API compliance | PASS |
| 8. NT8 SIM gate (6 steps) | PASS |

**Violations: 0**

---

## RETURN: REVIEW_PASS

Revision 2 correctly resolves V-01. The `IsDispatchableState` extraction from guard (1)
frees exactly 1 decision-point budget, allowing guard (3.5) to be inserted while holding
`TryDispatchLeaderFlat` at CYC=8 (at the limit, not exceeding it). The pre-fix CYC baseline
is accurately measured from live source (not the stale comment). The single authoritative CYC
table is internally consistent throughout the plan. All other checks from the first review
cycle remain PASS. No new violations introduced.

**Phase 3 (ticket generation) is UNLOCKED.**
