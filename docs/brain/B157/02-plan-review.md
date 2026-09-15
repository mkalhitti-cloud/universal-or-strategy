# Plan Review -- Block B157
# ptt-plan-reviewer Phase 2 output
# Reviewed: 2026-08-10 (Cycle 1) / 2026-08-10 (Cycle 2)
# Status: REVIEW_PASS

---

## Cycle 2 Verdict

**REVIEW_PASS**

V-01 is resolved. No new violations found. Plan is approved for Phase 3 (ticket generation).

---

## Cycle 2 V-01 Resolution Confirmation

| Check | Required | Found in Plan | Result |
|---|---|---|---|
| Removal range in Section 4.2 | `3514-3532` | `3514-3532` | RESOLVED ✓ |
| Quoted first comment text in Section 4.2 | `"extracted from CaptureOtherLegTargetPrices for-i body"` | `"extracted from CaptureOtherLegTargetPrices for-i body"` | RESOLVED ✓ |
| Removal range in Section 7 (B157-T2) | `3514-3532` | `3514-3532` | CONSISTENT ✓ |
| Removal range in plan footer source-lines note | `3514-3532` | `3514-3532` | CONSISTENT ✓ |

All three locations that reference the removal range (Section 4.2, Section 7, footer) now agree on `3514-3532` and the quoted comment text matches the actual source. The orphaned-comment risk identified in Cycle 1 is eliminated.

---

## Cycle 2 Full Re-Scan (New Violations Search)

No new violations found against the corrected plan. All DNA checks below return PASS.

| Rule | Category | Check | Result |
|---|---|---|---|
| JS-021 | CONCURRENCY P0 | No `lock()` introduced; ConcurrentDictionary used throughout | PASS |
| JS-021 | CONCURRENCY P0 | No Monitor/Mutex/SemaphoreSlim for state | PASS |
| JS-023 | CONCURRENCY P0 | No UI update from off-thread; no WPF mutations in any change | PASS |
| JS-001 | TYPE SAFETY P0 | No `throw` in OnOrderUpdate / SendCopy / gate chain | PASS |
| JS-002 | TYPE SAFETY P0 | All new methods are `void`; `IsReversalToFlatFollower` returns `bool` -- no null where value expected | PASS |
| JS-003 | TYPE SAFETY P0 | No magic string for discriminated state introduced | PASS |
| JS-009 | IMMUTABILITY P1 | No `Dictionary<K,V>` for shared state; all shared collections are ConcurrentDictionary | PASS |
| JS-008 | IMMUTABILITY P1 | No mutable fields on struct; no SolidColorBrush introduced | PASS |
| JS-010 | CONSTRUCTION P1 | No new class/struct constructors | PASS |
| NT8 | async/await | Not introduced in any lifecycle or hot-path method | PASS |
| NT8 | Account.All | Not introduced | PASS |
| NT8 | FontFamily override | No WPF changes | PASS |
| NT8 | Hardcoded #RRGGBB | No color strings introduced | PASS |
| NT8 | CreateOrder prefix | No CreateOrder calls | PASS |
| NT8 | DateTime.Now | Not introduced | PASS |
| CYC | COMPLEXITY P1 | IsReversalToFlatFollower=2, TryEvictFollowerBeSlot=7, IsLeaderFlatDrainAbort=8, RunFollowerEvictPath=4; all <=8 | PASS |
| SPEC | COMPLETENESS P0 | All 6 defect cards addressed; V-01 corrected; no requirement gap remains | PASS |

---

## Cycle 1 Record (Preserved)

> **Status at Cycle 1:** REVIEW_FAIL
> **Violations found:** 1 (V-01)

### Violation Table (Cycle 1)

| # | Rule / Constraint | Violation Description | Plan Location |
|---|---|---|---|
| V-01 | SPEC COMPLETENESS (P0) -- plan correctness | Section 4.2 stated removal range `3515-3532` but the first comment line for `UpdateLegTargetPrice` is **line 3514** (`// BWAVE-REFACTOR-LaneB-T5: extracted from CaptureOtherLegTargetPrices for-i body.`). Following the plan literally would leave line 3514 as an orphaned dangling comment after the method body is deleted. Additionally, the plan's quoted first comment text read `"extracted from CaptureLinkedTargetPrice return logic"` but the actual source reads `"extracted from CaptureOtherLegTargetPrices for-i body"` -- a mismatch that prevents the engineer from independently confirming the removal target by text match. | Section 4.2 (`DW-B153-06 Exact Lines to Remove`) |

---

## Lane-Split Gate Compliance

| Check | Result |
|---|---|
| Gate result present in plan header | PASS -- "SINGLE LANE -- 3 tickets" |
| Q1 answered (more than one file?) | PASS -- "YES -- CopyEngine.cs and CopyEngineTests.cs" |
| Q2 answered (one ticket possible?) | PASS -- "YES -- single engineer, single PR" |
| Q3 answered (dependency ordering?) | PASS -- "T1 travels together; T2 and T3 fully independent" |
| Q4 answered (one lane or multiple?) | PASS -- "ONE LANE" |
| Gate result consistent with plan structure | PASS -- 3 tickets, single PR |

---

## Spec Coverage Matrix

| Requirement (Defect Card) | Addressed? | Plan Section |
|---|---|---|
| DW-B157-RG-01: `followerIsFlat` -> `!followerIsFlat` fix | YES | Section 1, B157-T1 |
| DW-B157-RG-01: 4 test changes (1 flip + 3 new) | YES | Section 8, B157-T1 test plan |
| DW-B156-DEFER-1: TryEvictFollowerBeSlot decomposition | YES | Section 2, B157-T2 |
| DW-B153-05: Stale CYC=4 annotation on CountLeaderTargets | YES | Section 3, B157-T2 |
| DW-B153-06: Remove dead code UpdateLegTargetPrice | YES -- corrected in Cycle 2 | Section 4, B157-T2 |
| DW-B156-DEFER-2: CYC<=5 -> CYC<=8 on TrySetSlateReadyOnDrainFill | YES | Section 5, B157-T2 |
| DW-B156-DEFER-3: Exact-string assertion for [DRAIN-ABORT-LEADER-FLAT] | YES | Section 6/7, B157-T3 |
| Carry-forward: DW-B154-05 | YES | Section 0, Section 12 |
| Carry-forward: DW-B154-06 | YES | Section 0, Section 12 |
| Carry-forward: DW-B155-01 | YES | Section 0, Section 12 |
| Carry-forward: DW-BUG-0911-A-01 | YES | Section 0, Section 12 |

---

## Detailed Check Results

### CHECK 1: DW-B157-RG-01 -- Logic Inversion

**Line verification:**
`src/PropTraderTools/CopyEngine.cs` line 6527 confirmed:
```
return currentAction != lastAction && followerIsFlat;
```
Plan states exact same content. Line number 6527 is accurate. ✓

**Fix correctness:**
`followerIsFlat` -> `!followerIsFlat`. Pre-fix: returns `true` (skip) when flat AND direction changed -- this is the bug: flat follower has no live brackets and is safe to copy. Post-fix: returns `true` (skip) only when NOT flat AND direction changed -- protection path. Logically correct. ✓

**Semantics table (plan Section 1.4):**
All 4 rows verified correct against `currentAction != lastAction && !followerIsFlat`:

| followerIsFlat | reversal | Pre-fix result | Post-fix result | Expected |
|---|---|---|---|---|
| true | true | true (BUG) | false | false (allow flat+reversal) |
| false | true | false | true | true (block open+reversal) |
| true | false | false | false | false (allow flat+same) |
| false | false | false | false | false (allow open+same) |

PASS ✓

**Test changes:**
- T1-1: `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`. Inputs: Buy, Sell, flat=false. Post-fix: `true && !false = true` -> `Assert.True` ✓
- T1-2: `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse`. Inputs: Buy, Sell, flat=true. Post-fix: `true && !true = false` -> `Assert.False` ✓ (regression proof)
- T1-3: `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue`. Inputs: Sell, Buy, flat=false. Post-fix: `true && !false = true` -> `Assert.True` ✓
- T1-4: `IsReversalToFlatFollower_SameDirection_ReturnsFalse`. Inputs: Buy, Buy, flat=false. Post-fix: `false && anything = false` -> `Assert.False` ✓

All 4 test changes logically correct. PASS ✓

---

### CHECK 2: DW-B156-DEFER-1 -- TryEvictFollowerBeSlot Decomposition

**CYC for each method:**

Parent `TryEvictFollowerBeSlot` (post-extraction):
`e?.Order`(+1) + `o==null`(+1) + `IsEvictTriggerState`(+1) + `o.Instrument?.FullName`(+1) + `??`(+1) + `IsFollowerAccount`(+1) = 6 + base(1) = CYC=7. <=8 PASS ✓

`IsLeaderFlatDrainAbort` (new helper):
`IsFollowerAccount`(+1) + `OrderState`(+1) + `IsFlat`(+1) + `foreach`(+1) + `if kv...`(+1) + `?.`(+1) + `abortCount>0`(+1) = 7 + base(1) = CYC=8. <=8 PASS ✓ (at limit)

`RunFollowerEvictPath` (new helper):
`if(isFilled &&...)`(+1) + `&&`(+1) + `if(slotEvicted)`(+1) = 3 + base(1) = CYC=4. <=8 PASS ✓

**Logic completeness:** All original logic paths in lines 1934-1972 accounted for. No logic dropped. ✓
**Signature compatibility:** Both helpers receive parameters available at the call site in the parent. ✓
**Placement:** Both new helpers immediately after `TryEvictFollowerBeSlot` (after line 1973), before `LogBeSlotEviction` at line 1975. ✓

PASS ✓

---

### CHECK 3: DW-B153-05 -- Stale CYC Annotation for CountLeaderTargets

Lines 6639 and 6646 "Before" text confirmed. Strict McCabe CYC=6 arithmetic:
`base(1) + if rule==null(1) + if leader==null(1) + foreach(1) + if o==null(1) + if IsNativeLeaderTarget(1) = 6`
Corrections to `CYC=6` and `CYC<=6` are accurate. PASS ✓

---

### CHECK 4: DW-B153-06 -- Remove Dead Code UpdateLegTargetPrice

**Cycle 2 corrected state:**
- Removal range: `3514-3532` inclusive. ✓
- Quoted first comment: `"// BWAVE-REFACTOR-LaneB-T5: extracted from CaptureOtherLegTargetPrices for-i body."` ✓
- Section 7 and footer consistent: both read `3514-3532`. ✓
- Dead code confirmation: 1 match (definition only, 0 callers) per plan. ✓

PASS ✓ (V-01 RESOLVED)

---

### CHECK 5: DW-B156-DEFER-2 -- TrySetSlateReadyOnDrainFill CYC Annotation

Line 991 "Before" text confirmed. Strict McCabe CYC=8:
`base(1)+if order==null(1)+if OrderState!=Filled(1)+if !StartsWith(1)+if !IsFollowerAccount(1)+if IsFlat(1)+?. in log(1)+?? in log(1)=8`
Correction `CYC<=5` -> `CYC<=8` is accurate. 8<=8 remains compliant. PASS ✓

---

### CHECK 6: DW-B156-DEFER-3 -- Exact-String Assertion Test

Actual source line 1952: `"[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name` -- exact string present. ✓
Existing test at line 9364 asserts `"DRAIN-ABORT-FLAT"` -- distinct substring, no redundancy. ✓
New test `[Fact]` (no Skip). ✓

PASS ✓

---

## Constraint Compliance Summary

| Constraint | Plan Status | Reviewer Verdict |
|---|---|---|
| ASCII-only strings and comments | All proposed literals verified ASCII | PASS |
| No lock() introduced | Confirmed. ConcurrentDictionary throughout | PASS |
| No async/await introduced | Not present in any new/modified method | PASS |
| No DateTime.Now in executable code | Not introduced | PASS |
| CYC<=8 per method (strict McCabe) | IsReversalToFlatFollower=2, TryEvictFollowerBeSlot=7, IsLeaderFlatDrainAbort=8, RunFollowerEvictPath=4 | PASS |
| No throw in OnOrderUpdate / hot path | Not introduced | PASS |
| No null return where value expected | Not introduced | PASS |
| No Dictionary<K,V> for shared state | Not introduced | PASS |
| No Dispatcher.InvokeAsync omission | No WPF mutations; not required | PASS |
| Minimal change scope | T1: 1-char + 4 tests. T2: extract + 3 annotation fixes + dead code. T3: 1 test | PASS |
| No reformatting surrounding code | Explicitly prohibited in plan | PASS |

---

## Ticket Structure Assessment

| Ticket | Grouping Logic | Standalone Value | 7-Scan Template |
|---|---|---|---|
| B157-T1 | P0 bug fix + its tests travel together (correct dependency constraint) | YES -- P0 fix is independently shippable | YES -- Section 9 template |
| B157-T2 | Pure CopyEngine.cs structural work (decompose + annotations + dead code) | YES -- structural improvement independent of T1 | YES -- Section 9 template |
| B157-T3 | Test-only CopyEngineTests.cs enhancement | YES -- test coverage improvement independent of T1/T2 | YES -- Section 9 covers T3 items in SCAN-07 |

---

## Carry-Forward Verification

| ID | Item | In Plan? |
|---|---|---|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | YES ✓ |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | YES ✓ |
| DW-B155-01 | B155 SIM gate pending | YES ✓ |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | YES ✓ |

All required carry-forward items identified. ✓

---

_Plan review authored by ptt-plan-reviewer (Phase 2). Block B157._
_Cycle 1: REVIEW_FAIL (1 violation -- V-01)._
_Cycle 2: REVIEW_PASS (V-01 resolved, no new violations)._
_Source verified: CopyEngine.cs lines 6521-6528, 1920-1973, 985-1010, 6630-6665, 3510-3532._
_Test verified: CopyEngineTests.cs lines 8095-8135, 9350-9385._
_B156 deferred backlog read at: docs/brain/B156/06-deferred-backlog.md._
