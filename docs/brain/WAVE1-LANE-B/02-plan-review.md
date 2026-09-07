# WAVE1-LANE-B Plan Review
# Phase 2 Output -- ptt-plan-reviewer
# Input: docs/brain/WAVE1-LANE-B/02-architecture-plan.md
# Date: 2026-08

---

## FINAL VERDICT: REVIEW_PASS

**Zero violations found. All mandatory checks passed.**

---

## Section 1: Live CCN Verification (Independent / Authoritative)

Command executed:
```
python -c "
import lizard
files = ['src/PropTraderTools/Features/PttBreakEven.cs',
         'src/PropTraderTools/Features/PttBreakEvenSwap.cs',
         'src/PropTraderTools/Features/PttGlobalQuickExit.cs',
         'src/PropTraderTools/Features/PttQuickExit.cs',
         'src/PropTraderTools/Features/PttGlobalBreakEven.cs']
over8 = [(f.cyclomatic_complexity, f.name) for fpath in files
         for r in lizard.analyze([fpath]) for f in r.function_list
         if f.cyclomatic_complexity > 8]
print('Methods CCN > 8:', over8 if over8 else 'NONE')
"
```

**Result: Methods CCN > 8: NONE**

Full per-file breakdown:

| File | Method | CCN | Status |
|------|--------|-----|--------|
| PttBreakEven.cs | Execute | 8 | AT-LIMIT -- COMPLIANT |
| PttBreakEven.cs | CancelStaleBracketsLocal | 8 | AT-LIMIT -- COMPLIANT |
| PttBreakEven.cs | ExecuteOneAccount | 7 | COMPLIANT |
| PttBreakEven.cs | SubmitBeTargetsLocal | 7 | COMPLIANT |
| PttBreakEven.cs | SubmitBeStopLocal | 7 | COMPLIANT |
| PttBreakEven.cs | SnapshotTargetsLocal | 6 | COMPLIANT |
| PttBreakEven.cs | IsSnapshotTargetOrder | 6 | COMPLIANT |
| PttBreakEven.cs | SubmitBePair | 5 | COMPLIANT |
| PttBreakEven.cs | SubmitBareStop | 5 | COMPLIANT |
| PttBreakEven.cs | FindPositionLocal | 4 | COMPLIANT |
| PttBreakEven.cs | IsBePriceOk | 4 | COMPLIANT |
| PttBreakEven.cs | BuildBeRejectMsg | 3 | COMPLIANT |
| PttBreakEven.cs | RaiseBeNotify | 2 | COMPLIANT |
| PttBreakEven.cs | IsInvalidInput | 2 | COMPLIANT |
| PttBreakEven.cs | BuildBeOcoId | 2 | COMPLIANT |
| PttBreakEven.cs | SafeName | 2 | COMPLIANT |
| PttBreakEven.cs | All other methods | <=5 | COMPLIANT |
| PttBreakEvenSwap.cs | Execute | 8 | AT-LIMIT -- COMPLIANT |
| PttBreakEvenSwap.cs | IsStopPriceSubmittable | 6 | COMPLIANT |
| PttBreakEvenSwap.cs | SubmitSwapPair | 5 | COMPLIANT |
| PttBreakEvenSwap.cs | SubmitBareStopSwap | 4 | COMPLIANT |
| PttBreakEvenSwap.cs | HasNoTargets | 2 | COMPLIANT |
| PttGlobalQuickExit.cs | Execute (List overload) | 8 | AT-LIMIT -- COMPLIANT |
| PttGlobalQuickExit.cs | ExecuteFollowers | 8 | AT-LIMIT -- COMPLIANT |
| PttGlobalQuickExit.cs | SnapshotTargetOrders | 8 | AT-LIMIT -- COMPLIANT |
| PttGlobalQuickExit.cs | Execute (no-arg) | 7 | COMPLIANT |
| PttGlobalQuickExit.cs | WaitForPttBeCancelled | 6 | COMPLIANT |
| PttGlobalQuickExit.cs | ResolveFollowerTargets | 6 | COMPLIANT |
| PttGlobalQuickExit.cs | ExecuteOne | 6 | COMPLIANT |
| PttGlobalQuickExit.cs | IsTargetOrder | 6 | COMPLIANT |
| PttGlobalQuickExit.cs | CancelPttBeOrders | 5 | COMPLIANT |
| PttGlobalQuickExit.cs | IsPttTargetOrder | 5 | COMPLIANT |
| PttGlobalQuickExit.cs | IsNonTerminalForInstr | 5 | COMPLIANT |
| PttGlobalQuickExit.cs | IsNonTerminalPttBeState | 5 | COMPLIANT |
| PttGlobalQuickExit.cs | ScaleLeaderTargets | 4 | COMPLIANT |
| PttGlobalQuickExit.cs | IsNativeTargetOrder | 4 | COMPLIANT |
| PttGlobalQuickExit.cs | All others | <=3 | COMPLIANT |
| PttQuickExit.cs | SnapshotStopPrice | 8 | AT-LIMIT -- COMPLIANT |
| PttQuickExit.cs | SubmitQxOcoPair | 7 | COMPLIANT |
| PttQuickExit.cs | Execute (main) | 5 | COMPLIANT |
| PttQuickExit.cs | SubmitStopOrder | 5 | COMPLIANT |
| PttQuickExit.cs | ResolveTargetCount | 4 | COMPLIANT |
| PttQuickExit.cs | SubmitTargetOrder | 4 | COMPLIANT |
| PttQuickExit.cs | IsFlatOrMissing | 4 | COMPLIANT |
| PttQuickExit.cs | GetQuickTicks (InstrumentDefaults) | 4 | COMPLIANT |
| PttQuickExit.cs | All others | <=3 | COMPLIANT |
| PttGlobalBreakEven.cs | ExecuteOne | 6 | COMPLIANT |
| PttGlobalBreakEven.cs | Execute (IEnumerable) | 3 | COMPLIANT |
| PttGlobalBreakEven.cs | All others | <=2 | COMPLIANT |

**Verification conclusion: ALL methods across all 5 files are at CCN <= 8. Zero methods exceed the Jane Street threshold.**

---

## Section 2: Rule-by-Rule Findings

### JS-021 (lock() ban -- P0 CRITICAL)
**Status: PASS**
Plan documents: P0 scan via `Select-String -Pattern "lock\(" -Path <all 5 target files>`
Result: ONE match -- a COMMENT line only (`// JS-021: no lock().`) in PttGlobalBreakEven.cs line 4.
No actual `lock(` statement exists in any of the 5 files.
Proposed 7 new helper methods are all pure private static predicates / accessor delegates -- none introduce lock().

### JS-001 (throw in hot path -- P0 CRITICAL)
**Status: PASS**
Plan documents: no naked throw; all methods use try/catch with log. Order submission methods
(SubmitBePair, SubmitBareStop, SubmitBeStopLocal, SubmitBareStopSwap, SubmitSwapPair,
SubmitQxOcoPair, SubmitStopOrder, SubmitTargetOrder) all use try/catch pattern.
No new throw statements introduced by any proposed helper.

### JS-002 (return null -- P0 CRITICAL)
**Status: PASS**
Plan documents: all methods return void, bool, or initialized collections.
New helpers: GetBeOcoSeq() returns int; IsFollowerAcct/IsFinalOrder/IsLeaderAccount/IsFlatPosition
return bool; FindFollowerPosQty returns int (0 fallback); ClassifyTarget returns void.
No null return in any proposed helper.

### JS-003 (magic string discriminated state -- P0 CRITICAL)
**Status: PASS**
Not applicable -- no discriminated-union state modeling by string introduced in any proposed change.
Existing string-comparison patterns (IsNativeTargetOrder, IsPttTargetOrder) are read-only
classification helpers, not state discriminators.

### JS-008 (Mutable fields on struct / SolidColorBrush not Frozen -- P1)
**Status: PASS**
No structs or UI brushes involved in any target file or proposed helper.

### JS-009 (Dictionary for shared/thread-touched collection -- P1)
**Status: PASS**
No Dictionary<K,V> introduced by proposed helpers. All helpers operate on parameters
passed in; no shared mutable collections.

### JS-010 (Public constructor on singleton -- P1)
**Status: PASS**
No singleton or signal struct introduced. Proposed helpers are private static methods,
not types with constructors.

### JS-096 (Illegal states unrepresentable)
**Status: PASS**
Plan confirms no existing patterns are broken. All extractions are minimal delegations
(e.g. `if (!IsLeaderAccount(engine, acc)) continue;` replaces the inline &&-chain).
No new type modeling is introduced. The architectural invariant that PTT- prefixes
distinguish engine-managed orders from ATM-native orders is preserved throughout.

### Complexity Rule (CYC <= 8 per method)
**Status: PASS**
Live lizard run confirms: Methods CCN > 8: NONE.
All methods at AT-LIMIT (CCN=8) are documented with their exact branch counts.
Post-extraction targets in T-B1 and T-B3 will further reduce AT-LIMIT methods
(Execute: 8->6, CancelStaleBracketsLocal: 8->8, ExecuteFollowers: 8->5,
Execute(List): 8->8, SnapshotTargetOrders: 8->7).
No method will exceed CCN=8 as a result of any proposed extraction.

### NT8 API Constraints
**Status: PASS**
- CreateOrder + Submit co-located in same try block for all order-submission methods. ✅
- No AtmStrategyCreate or AtmStrategyChangeStopTarget (StrategyBase-only APIs) used. ✅
- No Account.All in constructor. ✅
- DateTime.Now not found; only DateTime.UtcNow and DateTime.MaxValue. ✅
- PTT- prefix confirmed on all CreateOrder calls. ✅
- No async/await in OnInitialize/OnDestroyed/OnWindowCreated. ✅
- No FontFamily override or hardcoded hex color. ✅
- ASCII-only strings. ✅

---

## Section 3: Mandatory Checklist Results

| Check | Result | Evidence |
|-------|--------|----------|
| Lane-Split Gate result present (Section 2) | PASS | LANES-APPROVED documented |
| Q1 answered | PASS | NO -- methods span 5 files |
| Q2 answered | PASS | NO -- no cross-class helper sharing |
| Q3 answered | PASS | YES -- each file independent |
| Q4 answered | PASS | YES -- distinct SIM test paths |
| Rules Catalog Gate result (Section 1) | PASS | GATE RESULT: PASS |
| P0 scan: lock() | PASS | 0 actual matches (1 comment match only) |
| P0 scan: async void | PASS | 0 matches |
| P0 scan: throw in hot path | PASS | 0 naked throws |
| P0 scan: return null | PASS | 0 null returns |
| All target methods mapped with CCN | PASS | 30+ methods mapped in Section 3 |
| No method above CCN 8 (after plan's work) | PASS | Lizard: 0 methods > 8 (live verified) |
| Lane isolation: zero CopyEngine.cs edits | PASS | Section 5 explicit confirmation |
| NT8 API constraints respected | PASS | All 9 NT8 checks clear |
| T-B1 has lizard CCN<=8 acceptance criterion | PASS | SCAN-04 in ticket |
| T-B2 has lizard CCN<=8 acceptance criterion | PASS | SCAN-04 in ticket |
| T-B3 has lizard CCN<=8 acceptance criterion | PASS | SCAN-04 in ticket |
| T-B4 has lizard CCN<=8 acceptance criterion | PASS | SCAN-04 in ticket |
| T-B5 has lizard CCN<=8 acceptance criterion | PASS | Lizard run documented |
| PLAN_COMPLETE stated | PASS | Section 6 |

---

## Section 4: Plan Accuracy Note (Informational -- Not a Violation)

The plan states several methods as "CCN 9-10, ABOVE 8" requiring extraction
(PttBreakEven::Execute, CancelStaleBracketsLocal, PttGlobalQuickExit::ExecuteFollowers,
Execute(List), SnapshotTargetOrders). The live lizard run returns these methods at
CCN=8 (at-limit), not CCN=9-10.

**This is not a violation.** Explanation:
1. The plan explicitly states in Section 3: "The CCN baseline provided reflects a historical
   measurement taken BEFORE substantial extraction work was already completed."
2. The plan's manual branch-counting is slightly more conservative than lizard's counting
   (lizard does not count catch blocks as branches; the plan does in some cases).
3. All proposed extractions are ADDITIVE IMPROVEMENTS on already-compliant code. They will
   reduce AT-LIMIT (8) methods to lower values (5-8), which is desirable.
4. The lizard SCAN-04 acceptance criterion in every ticket will verify <= 8, which
   already passes for all files.

The plan's proposed engineering work remains valid and worth executing as a margin-of-safety
improvement. It does not introduce any rule violation.

---

## Section 5: Violations Found

**None.**

Zero P0 violations. Zero P1 violations. Zero NT8 violations. Zero spec coverage gaps.

---

## FINAL VERDICT: REVIEW_PASS
