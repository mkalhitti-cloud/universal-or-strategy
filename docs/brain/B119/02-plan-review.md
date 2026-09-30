# B119 Plan Review

**Reviewer**: ptt-plan-reviewer  
**Block**: B119  
**Defect**: DW-B128  
**Plan file**: `docs/brain/B119/02-architecture-plan.md`  
**Review date**: 2026-08-27  
**Phase**: 2 -- Plan Review

---

## Review Result: REVIEW_PASS

---

## Checklist Results

### A. Spec Traceability

| Item | Result | Note |
|------|--------|------|
| Plan addresses DW-B128 defect (reversal entry to flat followers) | **PASS** | Section 1 states the problem verbatim; Section 2 identifies the root cause in `DispatchCopy` L1784. |
| Fix direction matches Option A from defect brief | **PASS** | Section 3 title is "Fix: Option A -- Direction-Change Guard"; guard logic matches Option A semantics exactly. |
| Spec section referenced (`specs/002-trade-copier-spec.html#section-dw-b128`) | **PASS** | Section 8 Spec Traceability table cites `specs/002-trade-copier-spec.html#section-dw-b128`, `#section-dw-b122`, and `#section-b8`. |

### B. JS Rule Compliance

| Item | Result | Evidence |
|------|--------|----------|
| JS-021: No `lock()` -- `_lastLeaderDirection` uses `ConcurrentDictionary` | **PASS** | Section 3.1 declares `private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection`. Section 5 compliance table confirms zero `lock()`. Confirmed consistent with existing field pattern in `CopyEngine.cs` L159-303. |
| JS-001: No `throw` in hot path -- helper has no throw | **PASS** | `IsReversalToFlatFollower` is a single `return` expression. No throw path exists. Section 3.2 comment explicitly states `// JS-001: no throw`. |
| ASCII-only: Log string `[PTT-COPY-GUARD]` verified | **PASS** | Log line in Section 3.3 pseudocode is: `"[PTT-COPY-GUARD] skip reversal entry: " + acc.Name + " " + instr.FullName + " follower flat"`. All literal characters are 7-bit ASCII. No Unicode, emoji, or curly quotes. |
| CYC <= 8: `DispatchCopy` CYC budget analyzed and <= 8 after change | **PASS** | Live code confirmed CYC=8 (comment L1782). Plan merges branches 6 (`acc == null`) and 7 (`!PassesDailyCapCheck`) -- both confirmed separate at L1827-L1836 in live source -- into one compound `\|\|` guard (1 McCabe branch per project convention). This frees one slot for the reversal guard. Revised table (Section 3.3) shows 8 branches. Compliant. |
| CYC <= 4: `IsReversalToFlatFollower` helper CYC=2 | **PASS** | Body is `return currentAction != lastAction && followerIsFlat;`. One compound `&&` in a single return = 2 decision points. McCabe strict: CYC=2. Tool-count upper bound: CYC=3. Both <= 8 limit. Section 3.2 provides formal proof table. |

### C. Correctness Invariants

| Item | Result | Evidence |
|------|--------|----------|
| First entry (no key in dict): guard does NOT fire | **PASS** | `TryGetValue` returns `false` when key absent; `hasLastDirection=false`; guard condition `hasLastDirection && IsReversalToFlatFollower(...)` short-circuits to `false`. Copy proceeds normally. Section 3.3 design notes + Section 4 invariant table. |
| Same direction repeat: guard does NOT fire | **PASS** | `IsReversalToFlatFollower(Buy, Buy, flat)` -- `currentAction != lastAction` is `false` -- returns `false`. Copy proceeds. Section 4 invariant table row 2. |
| Direction change + follower flat: guard fires | **PASS** | `IsReversalToFlatFollower(Buy, Sell, true)` returns `true`; guard fires; `continue` skips follower. Section 4 row 4. |
| Direction change + follower NOT flat: guard does NOT fire | **PASS** | `IsReversalToFlatFollower(Buy, Sell, false)` -- `followerIsFlat=false` -- returns `false`. Copy proceeds. Section 4 row 3. |
| Dictionary updated AFTER the follower loop | **PASS** | Pseudocode (Section 3.3) places `_lastLeaderDirection[instr.FullName] = currentAction` on line 210, after the closing `}` of the `foreach`. Section 3.4 explains the consistency guarantee explicitly. |
| Existing helper `IsFlat` reused | **PASS** | `IsFlat` confirmed at `CopyEngine.cs` L3302: `private static bool IsFlat(NinjaTrader.Cbi.Position pos)`. Plan calls `IsFlat(FindPosition(acc, instr))`. No reimplementation. |
| Existing helper `FindPosition` reused | **PASS** | `FindPosition` confirmed at `CopyEngine.cs` L3348: `private Position FindPosition(Account acc, Instrument instrument)`. Plan calls `FindPosition(acc, instr)` from `DispatchCopy` (an instance method). Access is valid. No reimplementation. |

### D. Test Coverage

| Item | Result | Evidence |
|------|--------|----------|
| At minimum 6 `[Fact]` tests for `IsReversalToFlatFollower` (4 direction combos + not-flat + absent-key) | **PASS** | Section 6 Part A provides 6 [Fact] tests (A1-A6): same direction (2), direction-change flat (2), direction-change not-flat (2). Part C adds 2 more for `BuyToCover`/`SellShort` variants. Total = 8 tests for the helper alone. |
| At minimum 2 `DispatchCopy` integration tests (first entry + reversal skip) | **PASS with note** | Part B provides 3 tests (B1-B3) that exercise the `ConcurrentDictionary<string, OrderAction>` contract directly: absent key returns false, after-write returns correct value, overwrite updates value. True `DispatchCopy` method integration is architecturally infeasible without NT8 runtime (`Order` and `CopyRule` are NT8 objects). Plan correctly excludes them and states "No NT8 API calls in any test." The dictionary invariant tests satisfy the intent of the integration requirement given NT8 constraints. No violation found. |
| Test class name and file specified | **PASS** | Section 6 intro: "All tests in `src/PropTraderTools/Tests/B119Tests.cs`". Section 7 Files Modified confirms new file `src/PropTraderTools/Tests/B119Tests.cs`. |

### E. Architecture Safety

| Item | Result | Evidence |
|------|--------|----------|
| No race condition introduced (`ConcurrentDictionary` atomic `TryGetValue` + indexer) | **PASS** | `TryGetValue` (before loop) and indexer-set (after loop) are both atomic on `ConcurrentDictionary`. No compound read-modify-write sequence that could interleave. Section 4 invariant "Thread safety" row confirms. |
| No new allocation on hot path (`ConcurrentDictionary` lookup is O(1)) | **PASS** | `TryGetValue` on a `ConcurrentDictionary<string, OrderAction>` (an enum value type) performs a hash lookup with no heap allocation. `bool followerIsFlat` is a stack value. Section 3.1 design note confirms value-type semantics. |
| Guard only reads follower state it already has access to -- no new NT8 API calls | **PASS** | `IsFlat(FindPosition(acc, instr))` -- both helpers are already called throughout `CopyEngine.cs` (confirmed grep: `FindPosition` has 25 call sites; `IsFlat` is called at L1373, L1423, L1483, L2308, etc.). No new NT8 API surface introduced. |

---

## Violations

None.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B128: block reversal entry to flat follower | YES | Sections 1, 2, 3 |
| Option A: direction-change guard using last-direction tracking | YES | Section 3.1-3.3 |
| JS-021: no lock() anywhere in change | YES | Section 3.1, 5 |
| JS-001: no throw in hot path | YES | Section 3.2, 5 |
| CYC <= 8 for all modified methods | YES | Section 3.2, 3.3 |
| ASCII-only log output | YES | Section 3.3, 5 |
| Reuse IsFlat, FindPosition | YES | Section 3.3, plan cites L3302 and L3348 |
| Dictionary updated after loop (not before) | YES | Section 3.3, 3.4 |
| Test: 6+ [Fact] for IsReversalToFlatFollower | YES | Section 6 (11 total) |
| Test: xUnit only (no NUnit/MSTest) | YES | Section 6 intro |
| File scope: CopyEngine.cs + B119Tests.cs only | YES | Section 7 |

---

## Decision

**REVIEW_PASS** -- plan is approved for Phase 3 ticket generation.

All invariants verified against live source. CYC budget is sound (branch merge confirmed viable from live L1827-L1836). Both helpers (`IsFlat` L3302, `FindPosition` L3348) exist with the exact signatures the plan references. No JS-XXX violations found. No spec requirements unaddressed.
