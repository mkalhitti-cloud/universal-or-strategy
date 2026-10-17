# B194 Plan Review

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-10-07
**Plan reviewed**: docs/brain/B194/02-architecture-plan.md
**References consulted**:
- docs/standards/jane-street/RULES_CATALOG.md (JS-001, JS-021, JS-033, JS-066)
- docs/intel/jane-street/complexity-reduction.md
- docs/standards/NT8_FULL_REFERENCE.md
- docs/standards/NT8_API_SURFACE.md (OrderState cycle)
- src/PropTraderTools/CopyEngine.cs (lines 370-385, 4995-5074, 6440-6600)

---

## VERDICT: REVIEW_PASS

All 12 checklist items PASS. Zero rule violations found.

---

## 1. LANE-SPLIT GATE COMPLIANCE

| Item | Result |
|------|--------|
| Gate result explicitly stated | PASS -- "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present |
| Q1 (all in same method / 50 lines?) | PASS -- Architect answered NO (spans 4 methods ~1600 lines); single-pipeline justified |
| Q2 (Fix B depends on Fix A?) | PASS -- Architect answered NO; fixes are independent by root cause |
| Q3 (each fix has standalone value?) | PASS -- Architect answered YES with evidence for each fix |
| Q4 (each fix has independent SIM verification?) | PASS -- Architect answered YES with specific log signals |
| Single-pipeline selected | PASS -- sequential T1->T2->T3 is correct given single-file scope |

**Result: PASS**

---

## 2. CYC VERIFICATION (JS-066 -- hard limit <=8)

### TryNakedDetect (post-F1)

Code verified at lines 6450-6461 against plan analysis.

| Item | Actual | Plan | JS-066 Limit | Result |
|------|--------|------|--------------|--------|
| Pre-F1 CYC | 3 (base=1, compound-state-check=+1, IsFollowerAccount=+1) | 3 | 8 | PASS |
| Post-F1 CYC | 4 (+1 for PTT-Copy/Filled guard) | 4 | 8 | PASS |

### HasNakedPosition (post-F2)

Code verified at lines 6501-6527.

| Item | Actual | Plan | JS-066 Limit | Result |
|------|--------|------|--------------|--------|
| Pre-F2 CYC | 8 (base=1, foreach-pos=+1, qty-check=+1, no-pos-return=+1, foreach-ord=+1, state-compound=+1, StopMarket-or-StopLimit=+1, Limit=+1) | 8 | 8 | PASS (at ceiling) |
| Post-F2 CYC (with helper) | 7 (base=1, foreach-pos=+1, qty-check=+1, no-pos-return=+1, foreach-ord=+1, IsBracketStopOrder=+1, Limit-check=+1) | 7 | 8 | PASS |
| Helper extraction required? | YES -- adding F2 without extraction would push CYC to 9 | YES -- architect identified and planned IsBracketStopOrder extraction | 8 | PASS |

### IsBracketStopOrder (new method, post-F2)

| Item | Plan | JS-066 Limit | Result |
|------|------|--------------|--------|
| CYC | 3 (base=1, TriggerPending-branch=+1, WorkingStop-branch=+1) | 8 | PASS |

### DrainThenDispatch (post-F3)

Code verified at lines 6541-6600. F3 change is a LINQ Where() predicate extension only.

| Item | Plan | JS-066 Limit | Result |
|------|------|--------------|--------|
| McCabe CYC change | None -- LINQ lambda is not a McCabe branch | n/a | PASS |
| Existing method CYC | Unchanged (3 per code comment at line 6537) | 8 | PASS |

### FlattenOneAccount (post-F4)

Code verified at lines 5000-5074. Code comment at line 5000 confirms "CYC=6".

| Item | Actual | Plan | JS-066 Limit | Result |
|------|--------|------|--------------|--------|
| Pre-F4 CYC | 6 (per comment line 5000-5002, verified against code structure) | 6 | 8 | PASS |
| Post-F4 CYC | 7 (+1 for debounce if-check in Insertion 1) | 7 | 8 | PASS |

**Result: PASS -- all methods at CYC <= 7 post-fix; new helper CYC=3**

---

## 3. JS-021 COMPLIANCE (no lock())

| Item | Result |
|------|--------|
| F1 (TryNakedDetect guard) | PASS -- read-only property access only; no lock() |
| F2 (IsBracketStopOrder) | PASS -- pure predicate; no state, no lock() |
| F3 (DrainThenDispatch LINQ) | PASS -- LINQ predicate only; no state mutation |
| F4 (FlattenOneAccount debounce) | PASS -- ConcurrentDictionary.GetOrAdd/AddOrUpdate; lock-free (approved primitive per lock-free-patterns.md) |
| New field _flattenFilledTicks | PASS -- ConcurrentDictionary<string,long> with StringComparer.Ordinal; matches existing _nakedDetectLastQueuedTicks pattern at lines 373-374 |

**Result: PASS -- zero lock() in any planned code**

---

## 4. JS-033 COMPLIANCE (no async void)

| Item | Result |
|------|--------|
| F1 -- TryNakedDetect | PASS -- synchronous void; no async keyword |
| F2 -- IsBracketStopOrder | PASS -- synchronous static bool; no async keyword |
| F3 -- DrainThenDispatch | PASS -- synchronous void (unchanged); no async keyword |
| F4 -- FlattenOneAccount | PASS -- synchronous void (unchanged); no async keyword |
| Existing Dispatcher.InvokeAsync() | PASS -- this is a fire-and-forget event-handler dispatch pattern already in use at line 6491; not a new async void |

**Result: PASS -- no new async void methods**

---

## 5. F1 CORRECTNESS: TryNakedDetect PTT-Copy Fill Guard

Guard as planned:
```csharp
if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled)
    return;
```

| Check | Analysis | Result |
|-------|----------|--------|
| Guard fires ONLY on PTT-Copy + Filled | YES -- requires BOTH Name=="PTT-Copy" AND State==Filled | PASS |
| Does NOT suppress NakedDetect for non-PTT-Copy orders | YES -- Name check fails for all other order names (PTT-Flatten, Entry, ATM bracket names) | PASS |
| Does NOT suppress PTT-Copy Cancelled/Rejected | YES -- OrderState.Cancelled and .Rejected fail the Filled check; guard does not fire | PASS |
| Guard placement (before all existing checks) | YES -- plan specifies insertion "before the first if (e.Order.OrderState...)" which means before the existing terminal-state guard | PASS |

**Result: PASS**

---

## 6. F2 CORRECTNESS: HasNakedPosition TriggerPending Stop Recognition

IsBracketStopOrder as planned:
```csharp
if (o.OrderState == OrderState.TriggerPending
    && o.OrderType == OrderType.Market
    && o.StopPrice > 0)
    return true;
if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
    && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit))
    return true;
return false;
```

| Check | Analysis | Result |
|-------|----------|--------|
| Matches DW-B194-STOP-01 (SnapshotStopPrice fix) pattern | YES -- plan confirms OrderState.TriggerPending, OrderType.Market, Order.StopPrice were all confirmed via commit a434679a | PASS |
| isTriggerPendingStop requires StopPrice > 0 | YES -- `&& o.StopPrice > 0` is explicitly present in the planned code | PASS |
| isWorkingStop preserves existing Working+Submitted+StopMarket+StopLimit path | YES -- second if-clause matches existing HasNakedPosition behavior exactly | PASS |
| TriggerPending+Market without StopPrice>0 (real market entry) would be misclassified? | NO -- the `&& o.StopPrice > 0` guard prevents real market entry orders (StopPrice=0) from returning true | PASS |

**Result: PASS -- StopPrice>0 guard correctly discriminates bracket stops from market entries**

---

## 7. F3 CORRECTNESS: DrainThenDispatch Submitted Inclusion

| Check | Analysis | Result |
|-------|----------|--------|
| NT8_FULL_REFERENCE.md confirms account.Cancel() is AddOnBase available | YES -- line 80: "account.Cancel / account.CreateOrder / Submit -- AddOnBase available" | PASS |
| NT8_API_SURFACE.md confirms Submitted is a valid pre-Working order state | YES -- line 169: "Submit() -> PendingSubmit -> Submitted -> Accepted -> Working"; Cancel -> PendingCancel -> Cancelled applies to any live state | PASS |
| Existing drain logic uses follower.Cancel(new Order[] { e }) at line 6596 | YES -- confirmed at line 6596 | PASS |
| follower.Cancel() works on Submitted state orders | PASS -- NT8 Cancel is available at AddOnBase level without documented state restriction; the order state cycle confirms Submitted is a cancellable live state |
| Adding Submitted to entryCandidates is safe | YES -- a Submitted PTT-Copy represents a drain-2 order that has not yet reached Working; it occupies a slot and must be cancelled before drag-3 can dispatch | PASS |

**Result: PASS**

---

## 8. F4 CORRECTNESS: FlattenOneAccount Debounce

| Check | Analysis | Result |
|-------|----------|--------|
| ConcurrentDictionary pattern is lock-free | YES -- GetOrAdd (value-type literal, no closure) and AddOrUpdate are lock-free atomic ops | PASS |
| No hot-path allocation | YES -- GetOrAdd(acc.Name, 0L) passes a value literal not a delegate; no closure allocation | PASS |
| Timestamp recorded at submission, not fill | YES -- Insertion 2 is placed before acc.Submit(new[] { order }), not in an OnOrderUpdate callback | PASS |
| 500ms window correctly computed | YES -- Environment.TickCount is milliseconds; `< 500L` enforces <500ms window | PASS |
| DW-B194-04 explicitly documented | YES -- Section 11 table shows DW-B194-04: CLOSED (implemented), assigned to T3 | PASS |
| TickCount cast pattern matches existing code | YES -- `(long)(int)Environment.TickCount` matches line 6479 exactly; existing comment at 6470 notes wrap safety | PASS |

Note: The planned _flattenFilledTicks field comment does not include the TickCount wrap-period note present at line 6470 ("500ms grace window is far shorter than wrap period -- safe"). This is a documentation gap but NOT a rule violation. Engineer should add the wrap note when implementing.

**Result: PASS**

---

## 9. TEST COVERAGE

| Check | Result |
|-------|--------|
| All 4 bugs have corresponding [Fact] tests | PASS -- 7 tests total covering all 4 fixes (T1=2, T2=4, T3=1) |
| xUnit [Fact] only | PASS -- plan specifies xUnit throughout; no NUnit, no MSTest |
| Tests do NOT require NT8 runtime | PASS -- all tests use stub Account/Order objects; plan explicitly states "No NT8 runtime required" |
| F1 test: TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled | PASS -- covers PTT-Copy Filled suppression |
| F2 test: HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists | PASS -- covers BUG-2 scenario |
| F3 test: DrainThenDispatch_IncludesSubmittedInEntryCandidates | PASS -- covers Submitted inclusion |
| F4 test: FlattenOneAccount_SkipsWhenFlattenWithin500ms | PASS -- covers debounce logic |
| IsBracketStopOrder helper has 3 dedicated [Fact] tests | PASS -- true/true/false coverage |

**Visibility note** (not a violation -- advisory): Plan correctly identifies that `IsBracketStopOrder` must be declared `internal static` (not `private static`) for test-project accessibility. Section 4 uses `private static` as intent, Section 10/13 override to `internal static` for implementation. Engineer contract is clear. No rule violation; this is correctly handled.

**Result: PASS**

---

## 10. DW CARD COMPLETENESS

| DW Card | Status | Ticket | Result |
|---------|--------|--------|--------|
| DW-B194-01 (TryNakedDetect false positive) | CLOSED | T1 (F1) | PASS |
| DW-B194-02 (HasNakedPosition TriggerPending blind spot) | CLOSED | T2 (F2) | PASS |
| DW-B194-03 (DrainThenDispatch Submitted exclusion) | CLOSED | T1 (F3) | PASS |
| DW-B194-04 (FlattenOneAccount loop debounce) | CLOSED (implemented) | T3 (F4) | PASS |
| DW-B183-03 (IsLaneEEligible CYC=8 frozen) | OPEN carry-forward | -- | PASS |
| DW-B192-SINST-01 (SetInstrument CYC ceiling) | OPEN carry-forward | -- | PASS |
| DW-B193-01 (OnQuickAllBufferChanged label strings) | OPEN carry-forward | -- | PASS |

**Result: PASS -- all 4 B194 DW cards present; all 3 carry-forward cards present as OPEN**

---

## 11. TICKET STRUCTURE

### T1 -- Fix TryNakedDetect (F1) + Fix DrainThenDispatch (F3)

| Check | Result |
|-------|--------|
| Spec requirement IDs | PASS -- BUG-1 (P0), BUG-4 (P0) |
| Method signatures specified | PASS -- both existing signatures stated |
| JS rule constraints listed | PASS -- SCAN-01 through SCAN-07 present |
| xUnit [Fact] names | PASS -- 2 tests named |
| 7-scan checklist | PASS -- complete |

### T2 -- Fix HasNakedPosition (F2) + Extract IsBracketStopOrder

| Check | Result |
|-------|--------|
| Spec requirement IDs | PASS -- BUG-2 (P1) |
| Method signatures specified | PASS -- new IsBracketStopOrder and modified HasNakedPosition |
| JS rule constraints listed | PASS -- SCAN-01 through SCAN-07 present |
| xUnit [Fact] names | PASS -- 4 tests named |
| 7-scan checklist | PASS -- complete |

### T3 -- FlattenOneAccount Belt-and-Suspenders Debounce (F4)

| Check | Result |
|-------|--------|
| Spec requirement IDs | PASS -- BUG-3 (P0) residual path |
| Method signatures specified | PASS -- existing FlattenOneAccount + new field declaration |
| JS rule constraints listed | PASS -- SCAN-01 through SCAN-07 present |
| xUnit [Fact] names | PASS -- 1 test named |
| 7-scan checklist | PASS -- complete |

Sequential order T1->T2->T3: PASS

**Result: PASS**

---

## 12. ASCII-ONLY COMPLIANCE

| String/Identifier | ASCII? | Result |
|-------------------|--------|--------|
| "PTT-Copy" | YES | PASS |
| "PTT-Flatten" | YES | PASS |
| "flat-debounce skip" | YES | PASS |
| "_flattenFilledTicks" | YES | PASS |
| "IsBracketStopOrder" | YES | PASS |
| All other new identifiers (TryNakedDetect guard, LINQ predicate) | YES | PASS |

**Result: PASS**

---

## NT8 API Validation

| API | Source | Result |
|-----|--------|--------|
| OrderState.TriggerPending | AddOnBase confirmed per B194-DIRECT commit a434679a | PASS |
| OrderType.Market | Standard NT8 enum, in current use | PASS |
| Order.StopPrice | AddOnBase confirmed per B194-DIRECT commit a434679a | PASS |
| Account.Cancel(Order[]) | NT8_FULL_REFERENCE.md line 80 confirmed | PASS |
| Account.Submit() | NT8_FULL_REFERENCE.md line 80 confirmed | PASS |
| Dispatcher.InvokeAsync() | In current use at line 6491 | PASS |
| ConcurrentDictionary<string,long> | In current use at lines 373-374 | PASS |
| Environment.TickCount | In current use at line 6479 | PASS |
| No StrategyBase-only methods | PASS -- no AtmStrategyCreate, no AtmStrategyChangeStopTarget | PASS |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| BUG-1: TryNakedDetect PTT-Copy fill false positive (P0) | YES | Section 4, F1; T1 |
| BUG-2: HasNakedPosition TriggerPending stop recognition (P1) | YES | Section 4, F2; T2 |
| BUG-3: FlattenOneAccount infinite loop residual path (P0) | YES | Section 4, F4; T3 |
| BUG-4: DrainThenDispatch Submitted order exclusion (P0) | YES | Section 4, F3; T1 |
| All fixes in single file (CopyEngine.cs only) | YES | Section 6 |
| No new public API surface | YES | All fixes are private/internal |
| Unit tests for all bugs | YES | Section 13 (7 tests) |
| Carry-forward DW cards documented | YES | Section 11 |

All 4 confirmed bugs addressed. No spec requirement left unaddressed.

---

## Summary

| Check | Items | Violations | Result |
|-------|-------|------------|--------|
| 1. Lane-split gate | 5 | 0 | PASS |
| 2. CYC verification | 5 methods | 0 | PASS |
| 3. JS-021 no lock() | 4 fixes + 1 field | 0 | PASS |
| 4. JS-033 no async void | 4 fixes | 0 | PASS |
| 5. F1 correctness | 4 items | 0 | PASS |
| 6. F2 correctness | 4 items | 0 | PASS |
| 7. F3 correctness | 5 items | 0 | PASS |
| 8. F4 correctness | 6 items | 0 | PASS |
| 9. Test coverage | 7 tests | 0 | PASS |
| 10. DW card completeness | 7 cards | 0 | PASS |
| 11. Ticket structure | 3 tickets | 0 | PASS |
| 12. ASCII-only | 6 identifiers | 0 | PASS |

**Total violations: 0**

---

*Written by: ptt-plan-reviewer*
*Epic: B194*
*Phase: 2 (Plan Review)*
*Gate result: REVIEW_PASS -- Phase 3 (ticket generation) is UNLOCKED*
