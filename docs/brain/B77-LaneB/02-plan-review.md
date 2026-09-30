# B77-LaneB Plan Review

**Epic**: B77-LaneB — QX Race Guard
**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Input**: docs/brain/B77-LaneB/02-architecture-plan.md

---

## Review Result: APPROVED

---

## Mandatory Reads Completed

| Read | File | Key Finding |
|------|------|-------------|
| STEP 1 | `docs/brain/B77-LaneB/02-architecture-plan.md` | Full plan read. Approach C chosen. |
| STEP 2 | `docs/standards/NT8_FULL_REFERENCE.md` lines 775–920 | `Order.SubmittedTime` confirmed absent. `Order.Time` = last state change. `OrderId` confirmed non-unique. Object reference equality confirmed (line 773). |
| STEP 3 | `src/PropTraderTools/CopyEngine.cs` | All callers of `CancelQxBrackets` identified: lines 419, 649 (internal), 597 in TradeCopierPanel.cs (external). All 3 use 2-param overload. |
| STEP 3b | `src/PropTraderTools/Features/PttQuickExit.cs` | Call site at line 67 confirmed: `CopyEngine.Instance?.CancelQxBrackets(leader, instr)` — 2-param. Execute() is synchronous, CYC=8 (documented in source comment). |

---

## Checklist Results

| ID | Item | Result | Evidence |
|----|------|--------|----------|
| R1 | No `lock()` in chosen approach — JS-021 | **PASS** | Plan §4 and §3 Approach C: `HashSet<Order>` is a local on the calling thread, passed synchronously, never shared. No `lock()`, no `Monitor`, no `SemaphoreSlim`. |
| R2 | No `async void` — JS-033 | **PASS** | All proposed methods (`BuildQxSnapshot`, new overload, Execute() modification) are synchronous. Source confirms Execute() has no `async` keyword (PttQuickExit.cs line 36). |
| R3 | NT8 API claims cited from NT8_FULL_REFERENCE.md | **PASS** | All four NT8 claims carry explicit line citations: `SubmittedTime` absent (lines 775–920 full table), `OrderId` non-unique (line 771), reference equality (line 773), `Order.Time` definition (line 903–904). All confirmed correct against the reference file. |
| R4 | Approach B infeasibility documented | **PASS** | Plan §3 Approach B documents `Order.SubmittedTime` does not exist (grep = 0 matches, full property table reviewed). Three independent failure modes for `Order.Time` proxy enumerated. Verdict: INFEASIBLE. Requirement satisfied. |
| R5 | Blast radius for CancelQxBrackets signature change is correct | **PASS** | Source scan confirms exactly 4 call sites: `CopyEngine.cs:419` (RelayBe), `CopyEngine.cs:649` (CancelQxBracketsForFollowers), `TradeCopierPanel.cs:597` (panel OnPositionStateChanged), `PttQuickExit.cs:67` (Execute). All 3 existing callers use 2-param overload and are unaffected. Only PttQuickExit.cs:67 is updated. Plan blast-radius table matches source exactly. |
| R6 | CancelQxBrackets new overload CYC ≤ 8 | **PASS** | Plan §5 claims CYC=7 for the new overload. Source structure of the 2-param overload (lines 586–605) plus one additional `snapshot` branch. Both plan CYC=7 and the 1-branch-higher interpretation (CYC=8) are at or within the ≤ 8 budget. No violation possible. Engineer must recount at implementation. |
| R7 | IsQxCancelCandidate CYC ≤ 8 (unchanged) | **PASS** | Source lines 568–576 confirm `IsQxCancelCandidate` is not modified. CYC=6 per source comment (line 566). Plan marks it 6 (unchanged). |
| R8 | Guard has no new race — snapshot built on one thread before Submit loop | **PASS** | Plan §3 correctly identifies NT8 dispatcher serial execution guarantee. `Execute()` is synchronous; `BuildQxSnapshot` reads `acc.Orders` and returns a local `HashSet<Order>` on the same thread. The snapshot is consumed and exhausted before the Submit loop begins. No concurrent mutation path exists. |
| R9 | BuildQxSnapshot uses correct NT8 API to enumerate QX orders | **PASS** | Plan §4 describes iterating `acc.Orders` — the same collection already used and tested in `CancelQxBrackets` at source line 590. No new unverified NT8 API introduced. |
| R10 | T1/T2/T3 ticket split coherent; T3 test IDs match chosen approach | **PASS** | T1 (CopyEngine.cs additions), T2 (PttQuickExit.cs call site), T3 (8 xUnit `[Fact]` tests) all target Approach C exclusively. Test IDs T_B77_01–T_B77_08 cover null guards, exclusion logic, snapshot filter behaviour, and IL ordering contract. No reference to Approach A or B in tests. |

---

## Violations

None. Zero violations found across all Jane Street DNA rules (JS-001, JS-002, JS-003, JS-008, JS-009, JS-010, JS-021, JS-023, JS-033), NT8 hard constraints, and complexity budget.

---

## Advisory (non-blocking)

**CYC count note (R6)**: The plan states the existing 2-param `CancelQxBrackets` is CYC=6; counting the source body at lines 586–605 yields 7 branches (null guard, foreach, stateOk compound, stateOk continue, instrument filter, `IsQxCancelCandidate` if, `stale.Count` guard). The existing source comment at line 584 also says CYC=6, suggesting a consistent off-by-one in both plan and source annotation. The new 3-param overload may therefore be CYC=8 (not 7), which is still exactly at budget. **This is not a violation** — the budget is ≤ 8 and the worst case is 8. Engineer must perform a Roslyn-accurate branch count at implementation time and confirm CYC ≤ 8 before marking T1 complete.

---

## Summary

The plan is architecturally sound, fully NT8-grounded, and Jane Street compliant. Approaches A and B are correctly eliminated with evidence from `NT8_FULL_REFERENCE.md`. Approach C is the only feasible design: a pre-submit snapshot `HashSet<Order>` passed to a new 3-param overload of `CancelQxBrackets`, with zero blast radius on the three existing callers. All CYC budgets are within the ≤ 8 limit. Pipeline may proceed to Phase 3 (ticket generation).

---

## Decision

**REVIEW_PASS** — pipeline proceeds to Ph3.
