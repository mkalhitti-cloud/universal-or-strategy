# B129 LaneA Plan Review — DW-B135

**Block**: B129 LaneA
**Defect**: DW-B135 — Reversal Guard False-Positive After Leader Flat
**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review — Pass 2, final)
**Date**: 2026-08-31
**Plan Reviewed**: `docs/brain/B129/LaneA-02-architecture-plan.md`

---

## Checklist Results

| Item | Description | Verdict | Evidence / Citation |
|------|-------------|---------|---------------------|
| R-01 | Root cause accuracy | **PASS** | Plan Section A correctly identifies `_lastLeaderDirection` never cleared on leader flat. All 3 false-positive conditions confirmed from code: L331 (field), L1914-1916 (`TryGetValue`/`hasLastDirection`), L1936 (guard check), L3593 (predicate `currentAction != lastAction && followerIsFlat`). |
| R-02 | Fix design completeness | **PASS** | Insertion point L2382-2386 confirmed from CopyEngine.cs. Predicate uses `foreach (_rules)` + `e.Order.Account.Name == r.MasterAccount?.Name`. Operation is `_lastLeaderDirection.TryRemove(instr, out _)`. Location is L2361-2387 — 200+ lines below LaneB range (~L2160). All 4 sub-requirements met. |
| R-03 | DW-B128 preservation proof | **PASS** | Plan Section B and Section F both state: during race window `hasPos=True`, `if (!hasPos)` block NOT entered, key NOT cleared. Code at L2372 confirms `HasOpenPosition` is the guard. DW-B128 protection provably preserved. |
| R-04 | Thread safety (JS-021) | **PASS** | Plan Section C: `TryRemove` is lock-free ConcurrentDictionary op. No `lock()` in new code. Called from NT8 UI thread (`OnOrderUpdate` L1353). `_rules` foreach safe (UI-thread-only mutation). No JS-021 violation. |
| R-05 | CYC analysis | **PASS** | Plan Section D: CYC BEFORE=3 confirmed (L2365 state check, L2368 null guard, L2382 CAS). Post-fix CYC=5 or 6 (both ≤ 8). JS-080 compliant. |
| R-06 | Partial close safety | **PASS** | Plan Section E: full proof with example (4→2 contracts). `HasOpenPosition` returns `true` while any contracts remain. `if (!hasPos)` block NOT entered. Direction key preserved during partial close. |
| R-07 | Test contract adequacy | **PASS** | Plan Section G: Test 1 covers (a) key cleared after flat event (`Assert.False(HasLeaderDirection)`). Test 2 covers (b) DW-B128 pure predicate (`Assert.True(IsReversalToFlatFollower(Sell,Buy,true))`). Test 3 covers (c) fresh engine — no key (`Assert.False(HasLeaderDirection)` on new instance). All 3 are `[Fact]` xUnit. |
| R-08 | Spec update plan | **PASS** | Revised Section H now contains all 5 required spec update actions: (1) Spec Update 1 — DW-B135 CLOSED (LaneA PIPELINE_COMPLETE); (2) Spec Update 2 — DW-B134 CLOSED (LaneB PIPELINE_COMPLETE); (3) Spec Update 3 — DW-B134-OCO OPEN deferred to B130; (4) Spec Update 4 — DW-B136 Gap A RESOLVED (root cause DW-B135); (5) Spec Update 5 — B129 fully PIPELINE_COMPLETE (both lanes). All 5 required items confirmed present. |
| R-09 | Carry-forward review | **PASS** | Plan Section F and Pre-flight checklist: DW-B134-OCO explicitly documented as UNAFFECTED/deferred to B130. Confirmed against LaneB-06-deferred-backlog.md which lists DW-B134-OCO as new OPEN item, not closed by LaneA. No intersection with LaneA scope. |
| R-10 | P0 rule compliance | **PASS** | No `lock()` (JS-021 PASS). No `throw new XxxException` (JS-001 PASS). `TryFirePositionState` is `void` — no `return null` (JS-002 PASS). No `async void` (JS-033 PASS). All 4 P0 rules clean. |

---

## Violation Detail

*Pass 1 violation (R-08) resolved in revised plan. No violations remain.*

---

## Pass 2 Re-Check

### R-08 re-check — Section H (revised plan)

**5-item verification**:

| Item | Required | Present | Location in plan |
|------|----------|---------|-----------------|
| (a) DW-B135: mark CLOSED (B129 LaneA PIPELINE_COMPLETE) | ✅ | **YES** | Spec Update 1, Section H L302-308 |
| (b) DW-B134: mark CLOSED (B129 LaneB PIPELINE_COMPLETE) | ✅ | **YES** | Spec Update 2, Section H L310-315 |
| (c) DW-B134-OCO: add as OPEN deferred → B130 | ✅ | **YES** | Spec Update 3, Section H L317-321 |
| (d) DW-B136 Gap A: mark RESOLVED (root cause DW-B135) | ✅ | **YES** | Spec Update 4, Section H L323-328 |
| (e) B129: mark fully PIPELINE_COMPLETE (both lanes) | ✅ | **YES** | Spec Update 5, Section H L330-337 |

All 5 items confirmed present. R-08: PASS.

**Regression check (R-01..R-07, R-09, R-10)**: The revision is purely additive to Section H. Sections A–G and Pre-flight Checklist are unchanged. All 9 previously passing items remain PASS. No regressions.

---

## Overall Verdict

**REVIEW_PASS**

**Violation count**: 0
**Pass 1 violation**: R-08 — resolved in revised plan (Section H now contains all 5 required spec update items).

All 10 items (R-01 through R-10) are **PASS**. The plan is approved to proceed to Phase 3 (ticket generation).
