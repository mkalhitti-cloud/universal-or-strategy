# Plan Review -- BWAVE-NEXT LaneBRepair-R2 (Round 2)

**Epic**: BWAVE-NEXT LaneBRepair-R2
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-05
**Input**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/02-architecture-plan.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## VERDICT: REVIEW_PASS

No violations found. All checklist items pass.

---

## Section A: Lane-Split Gate Verification

| Question | Answer | Evidence in plan (§1) | Gate status |
|----------|--------|----------------------|-------------|
| Q1 -- Same method or within 50 lines? | **NO** | R2-F1 in `OnOrderUpdate` (~line 1431); R2-F2 in `DrainThenDispatch` (~line 6534). Different methods, ~5,100 lines apart. | LANES allowed |
| Q2 -- Fix B design depends on Fix A final design? | **NO** | R2-F1 adds `AbortDrainOnFill` (fill-abort cleanup path). R2-F2 widens `entryCandidates` predicate (drain setup path). Zero design dependency stated and confirmed by §5 interaction analysis. | LANES allowed |
| Q3 -- Each fix has standalone value if the other is blocked? | **YES** | R2-F1 closes permanent `_drainOwnedOrderIds` leak independently. R2-F2 closes Clone-mode silent no-drain path independently. | LANES allowed |
| Q4 -- Each fix has independent SIM verification path? | **YES** | R2-F1: fill-abort scenario (leader entry fills while drain in progress). R2-F2: Clone-mode dispatch scenario (follower holds "Entry"-named working order). | LANES allowed |

**LANE-SPLIT GATE RESULT**: `LANES-APPROVED` is **VALID**. Q1=NO, Q2=NO, Q3=YES, Q4=YES. No gate violation.

---

## Section B: R2-F1 Design Correctness

| Check | Finding | Pass? |
|-------|---------|-------|
| `AbortDrainOnFill` captures payload from `TryRemove` (`out var payload`) | Plan §3 shows `if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))` -- payload captured, not discarded. | PASS |
| `foreach` iterates `payload.DrainedOrderIds` and calls `_drainOwnedOrderIds.TryRemove(id, out _)` | Plan §3 shows `foreach (var id in payload.DrainedOrderIds)` + `_drainOwnedOrderIds.TryRemove(id, out _)` inside the if body. | PASS |
| Called from `OnOrderUpdate` Filled branch | Plan §3 shows `AbortDrainOnFill(e.Order.Account.Name)` replacing the original `TryRemove(..., out _)` one-liner in the Filled branch. | PASS |
| `OnOrderUpdate` CYC unchanged at 8 | Plan §4 CYC table: Before=8, After=8. Correct -- a method call is a statement, not a decision branch. No cyclomatic increment. | PASS |
| `AbortDrainOnFill` CYC | Plan §4: CYC=2 (in the table), then §4 narrative clarifies the Lizard/standard count is 3 (Base=1 + TryRemove if +1 + foreach +1 = 3). Both representations acknowledged and reconciled. Either way, CYC=3 is well within the <=8 budget. | PASS |

---

## Section C: R2-F2 Design Correctness

| Check | Finding | Pass? |
|-------|---------|-------|
| Adds `\|\| o.Name == "Entry"` using exact equality | Plan §3 T2 shows `\|\| o.Name == "Entry"` (string equality, no `StartsWith`). | PASS |
| Matches `FindFollowerEntryOrder` line 3717 pattern | Plan §2 R2-F2 cross-reference: "`FindFollowerEntryOrder` (line 3717) uses `order.Name == "PTT-Copy" \|\| order.Name == "Entry"` (exact equality)". Fix mirrors this exactly. | PASS |
| `\|\|` inside lambda predicate -- no new branch in `DrainThenDispatch` method body | Plan §2 states: "The `\|\|` is added inside the inline Where lambda predicate. `DrainThenDispatch` body CYC = 3 (no new method-body branch added). CYC unchanged." Correct: LINQ lambda predicates do not add to the enclosing method's CYC score. | PASS |
| `DrainThenDispatch` CYC unchanged at 3 | Plan §4 CYC table: Before=3, After=3. | PASS |

---

## Section D: Interaction Analysis

| Check | Finding | Pass? |
|-------|---------|-------|
| F2 adds Entry orders to drain tracking; F1 removes them on fill-abort (lifecycle closed) | Plan §5: F2 widens `entryCandidates` so "Entry" orders enter `_drainOwnedOrderIds` via existing loop (lines 6562-6566). F1 removes those same IDs from `_drainOwnedOrderIds` on fill-abort via `AbortDrainOnFill`. Full lifecycle closed for Clone-mode orders. | PASS |
| No conflict between F1 and F2 on shared data structures | Plan §5 interaction table: F2=TryAdd (setup), F1=TryRemove (cleanup). Complementary, non-conflicting operations on `ConcurrentDictionary` -- atomically safe. | PASS |
| Apply-order constraint absent (either order valid) | Plan §5: "No apply-order constraint: F2 can be committed before or after F1." Correct -- F1 and F2 touch different lines. | PASS |

---

## Section E: Locked-Decision Compliance

| Locked decision | Status in plan |
|-----------------|---------------|
| No `TickCount64` (`.NET 4.8` not supported) | Listed as out-of-scope §8. Correct pattern documented. |
| No removal of `.ToList()` from `ActiveOrders` (DW-NEXT-A-07 thread-safety lock) | Listed as out-of-scope §8. |
| No `Account.Change()` (banned for AddOnBase) | Plan NT8 API section explicitly states NOT USED. |
| No `AtmStrategyCreate()` / `AtmStrategyChangeStopTarget()` (StrategyBase-only, banned) | Plan NT8 API section explicitly states NOT USED. |
| No `DateTime.Now` | Plan NT8 API section explicitly states NOT USED -- existing TickCount pattern unchanged. |
| DW-NEXT-B-01/B-02/B-03 remain open (P2 future) | Plan §8 and Deferred Backlog section confirms all three remain OPEN, not touched. |

All locked decisions: **PASS**.

---

## Section F: Jane Street DNA Rule Scan

| Rule ID | Rule | Check | Finding | Pass? |
|---------|------|-------|---------|-------|
| JS-021 | `lock()` ban (P0) | Any `lock(` in new/modified code? | Plan threading section: "No `lock()` anywhere." Both fixes use `ConcurrentDictionary.TryRemove` (atomic, lock-free). | PASS |
| JS-033 | `async void` ban (P0) | Any `async void` in new methods? | `AbortDrainOnFill` is `private void` (synchronous), not `async void`. R2-F2 is a predicate expression change. No async methods introduced. | PASS |
| JS-002 | `return null` ban (P0) | Any `return null;` in new/modified code? | `AbortDrainOnFill` returns `void`. R2-F2 is a LINQ predicate (no return statement). No `return null` anywhere. | PASS |
| JS-001 | `throw` in hot paths (P0) | Any `throw new XxxException(...)` in new/modified code? | No exception throwing in either fix. | PASS |
| ASCII-only | Non-ASCII characters | Any Unicode/emoji/curly-quotes in new code? | Plan code blocks use plain ASCII only. Verified visually in §3 code snippets. | PASS |
| NT8 AddOnBase API | Banned NT8 APIs | `Account.Change`, `AtmStrategyCreate`, `AtmStrategyChangeStopTarget` in new code? | Plan NT8 section explicitly: none used. SCAN-05 in 7-scan checklist targets these. | PASS |
| NT8 `DateTime.Now` | `DateTime.Now` (should be `UtcNow` or TickCount) | Any `DateTime.Now` introduced? | Not used. Plan confirms existing TickCount pattern unchanged. | PASS |
| JS-021 (Dispatcher) | UI update off-thread without `Dispatcher.InvokeAsync` | Any UI updates in new code? | No UI updates in either fix. Both operate on NT8 order-update callback thread with no UI side effects. | PASS |
| CYC <=8 | Complexity budget | All methods within budget? | `OnOrderUpdate`=8 (unchanged), `AbortDrainOnFill`=3 (new), `DrainThenDispatch`=3 (unchanged). All <=8. | PASS |

---

## Section G: 7-Scan Checklist Template

| Check | Finding | Pass? |
|-------|---------|-------|
| 7-scan checklist template present in plan | Plan §6 contains the complete 7-scan table with SCAN-01 through SCAN-07, exact grep/python commands, pass conditions, and JS rule citations. Post-build `ptt-sync-and-verify.ps1` + F5 gate also present. | PASS |

---

## Section H: Spec Coverage Matrix

| Requirement | Addressed? | Plan section |
|-------------|-----------|--------------|
| R2-F1: fix `_drainOwnedOrderIds` permanent leak on fill-abort path | YES | §2 problem statement, §3 T1 fix, §4 CYC, §7 T1 acceptance criteria |
| R2-F2: fix `entryCandidates` missing Clone-mode "Entry" orders | YES | §2 problem statement, §3 T2 fix, §4 CYC, §7 T2 acceptance criteria |
| CYC budget: all modified/new methods <=8 | YES | §4 CYC table -- all pass |
| No new fields, classes, or files | YES | Component list: "New fields: none. New classes: none. New files: none." |
| Thread-safety of new operations | YES | §Threading model table -- all operations on `ConcurrentDictionary` (atomic) |
| Out-of-scope items documented | YES | §8 -- 9 items listed with rationale |
| Interaction between F1 and F2 analyzed | YES | §5 interaction analysis |
| Deferred backlog status | YES | Deferred Backlog section -- DW-NEXT-B-01/02/03 remain OPEN, no new items opened |

All spec requirements addressed: **PASS**.

---

## Section I: NT8 API Validation

The plan declares no NT8 API calls are added or modified by either fix. Both fixes are pure C# logic changes:
- R2-F1: `ConcurrentDictionary.TryRemove` (BCL, not NT8)
- R2-F2: LINQ `Where` predicate string comparison (BCL, not NT8)

The NT8 API table in the plan explicitly states `Account.Change()`, `AtmStrategyCreate()`, and `AtmStrategyChangeStopTarget()` are NOT USED. SCAN-05 in the 7-scan checklist enforces this at engineer time. **PASS**.

---

## Section J: Violation Log

**No violations found.**

| # | Rule ID | Description | Location in plan | Severity |
|---|---------|-------------|-----------------|----------|
| — | — | No violations | — | — |

---

## Final Decision

| Gate | Result |
|------|--------|
| LANE-SPLIT GATE | LANES-APPROVED VALID |
| R2-F1 design correctness | PASS |
| R2-F2 design correctness | PASS |
| Interaction analysis | PASS |
| Locked-decision compliance | PASS |
| JS-021 (lock ban) | PASS |
| JS-033 (async void ban) | PASS |
| JS-002 (return null ban) | PASS |
| JS-001 (throw in hot paths) | PASS |
| ASCII-only | PASS |
| NT8 API compliance | PASS |
| CYC <=8 (all methods) | PASS |
| 7-scan checklist present | PASS |
| Spec coverage matrix | PASS |

## REVIEW_PASS

Plan is approved. Phase 3 (ticket generation) is unlocked.

---

*Review written: ptt-plan-reviewer | BWAVE-NEXT LaneBRepair-R2 Round 2 | Phase 2*
