# B191 Plan Review
# QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close

**Epic**: B191
**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Plan reviewed**: docs/brain/B191/02-architecture-plan.md
**Spec source**: Orchestrator prompt (docs/brain/B191/00-orchestrator-prompt.md)
**Reference backlog**: docs/brain/B190/06-deferred-backlog.md
**Rules catalog**: docs/standards/jane-street/RULES_CATALOG.md

---

## Verdict

**REVIEW_PASS**

All 6 checklist categories pass. Zero violations found. Plan is approved for Phase 3 ticket generation.

---

## Check 1 — LANE-SPLIT GATE Compliance

| Item | Result | Plan Location |
|------|--------|---------------|
| "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present with reason | **PASS** | Plan section header, line 31 |
| Reason correctly cites same .cs file | **PASS** | "Both T1 and T3 edit PttGlobalQuickExit.cs. Per Parallel Lane Protocol: 'Two lanes writing to the same .cs file = merge conflict.'" |
| LANES-APPROVED not used (would be FAIL) | **PASS** | SINGLE-PIPELINE used throughout |

**Evidence**: Plan lines 31–34:
> `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE (same .cs file -- merge conflict prevention)`
> `Both T1 and T3 edit PttGlobalQuickExit.cs.`

**PASS**

---

## Check 2 — T1 Accuracy

| Item | Result | Plan Location |
|------|--------|---------------|
| Code already committed at 365b8779 — no new .cs edit needed | **PASS** | Section 2, line 70: "Status: COMMITTED at 365b8779. No new code required for Ph4a." |
| `forcedTargets` identified as root cause (shared across accounts) | **PASS** | Section 1 root cause description: "This list is then passed verbatim as the targets argument to every account in the foreach loop." |
| `perAccTargets = Build2TargetList(pos.Quantity)` inside per-account loop described | **PASS** | Section 2, lines 86–88: code block with comment "rebuild 2-target list from this account's actual position qty" |

**Evidence**: Plan section 2 (lines 70–111) provides full before/after code blocks showing the
committed change at 365b8779, the role of `forcedTargets` post-T1 (guard + log only), and the
CYC impact (zero new branches — straight-line variable assignment).

**PASS**

---

## Check 3 — T2 Test Strategy

| Item | Result | Plan Location |
|------|--------|---------------|
| Tests are pure arithmetic — no NT8 runtime required | **PASS** | Section 3, line 118: "No NT8 runtime. No [Fact(Skip=...)] on any test." Lines 122–125 explain why: `Build2TargetList` is `internal static` pure integer arithmetic with no NT8 API calls. |
| No Skip attribute needed (pure unit tests) | **PASS** | Section 3, line 118 explicitly: "No [Fact(Skip=...)] on any test." |
| Oracle T2-1: `Build2TargetList(5)` → `{T1=3, T2=2}` | **PASS** | Table line T2-1 + arithmetic verification lines 142–143 |
| Oracle T2-5: `Build2TargetList(1)` → `{T1=1, T2=0}` | **PASS** | Table line T2-5 + arithmetic verification line 144 |
| Oracle T2-2: `Build2TargetList(20)` → `{T1=10, T2=10}` | **PASS** | Table line T2-2 + arithmetic verification line 143 |
| Oracle T2-3: sum invariant == 5 | **PASS** | Table line T2-3 + verification line 145 |
| Oracle T2-4: reflection overload existence check | **PASS** | Table line T2-4 + rationale section lines 147–152 |
| Exactly 5 [Fact] tests specified | **PASS** | Section 10 method signatures: 5 `[Fact]` methods listed in B191Tests class |

**Note on T2-4**: The plan uses reflection `typeof(PttGlobalQuickExit).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Instance, ...)` — no NT8 invocation. This is structurally correct for an AddOn project where NT8 runtime is absent in the test host.

**PASS**

---

## Check 4 — T3 Correctness

| Item | Result | Plan Location |
|------|--------|---------------|
| CYC impact is 0 new branches (variable assignment only) | **PASS** | Section 4, lines 221–225: "The `?.` null-conditional operator and `??` 0 null-coalescing operator are NOT McCabe branches — they are expressions, not conditional statements. CYC of ExecuteFollowers() = 4, unchanged after T3." |
| Null chain `?.` does not introduce throw (JS-001 compliant — `?? 0` fallback present) | **PASS** | Section 5 DNA audit, JS-001 block (lines 255–259): "No NullReferenceException possible. No throw introduced." Null-safe chain confirmed: `CopyEngine.Instance?.FindPositionPublic(...)?.Quantity ?? 0` |
| Behavioral change described: `ScaleLeaderTargets` now fires for non-flat followers with no snapshot | **PASS** | Section 6 T3 behavior change table (lines 332–336): explicitly shows "follower has OPEN POSITION" row: Pre-T3 = "ScaleLeaderTargets SKIPPED (dead path)"; Post-T3 = "ScaleLeaderTargets FIRES" |
| Change classified as CORRECT (dead path restored, not a regression) | **PASS** | Same table: "YES — CORRECT (dead path now active)". Section 6 verdict: "The newly activated path (ScaleLeaderTargets) was dead code confirmed in B187 — not a previously-passing path that could break." |

**Note on `FindPositionPublic` vs `FindPosition`**: The orchestrator spec references `FindPosition`
(private). The plan correctly substitutes `FindPositionPublic` (`internal`) and documents the
reason (Section 4, lines 215–219): `FindPosition` is `private` to `CopyEngine` and inaccessible
from `PttGlobalQuickExit`. `FindPositionPublic` is the established `internal` wrapper used by
`PttBreakEvenSwap.cs:53` and `TradeCopierPanel.cs:1788`. This is an architectural correction, not
a spec deviation.

**PASS**

---

## Check 5 — DNA Audit

| Rule | Check | Result | Plan Location |
|------|-------|--------|---------------|
| JS-021 | `lock()` absent | **PASS** | Section 5, lines 235–240: "zero lock() occurrences" confirmed for PttGlobalQuickExit.cs. T1 and T3 each add a straight-line variable assignment — no lock introduced. |
| JS-033 | `async void` absent | **PASS** | Section 5, lines 242–250: "zero async void occurrences" confirmed. ExecuteFollowers is "synchronous void (not an event handler, not async)". |
| JS-001 | No throw in hot path (T3 null chain) | **PASS** | Section 5, JS-001 block: `?.` propagates null safely through all three hops; `?? 0` unwraps to 0. No `throw` introduced. |
| JS-002 | No `return null` at new sites | **PASS** | Section 5, JS-002 block: T1 and T3 add zero new return sites. FindPositionPublic null return is pre-existing contract. |
| JS-066 | CYC <= 8 for all modified methods | **PASS** | CYC table in Section 5: Execute(List, Instrument)=8, ExecuteFollowers=4, Build2TargetList=1. All at or below 8. T1 delta=0, T3 delta=0. |
| JS-081/JS-082 | ASCII-only identifiers and string literals | **PASS** | Section 5, lines 282–289: `perAccTargets` and `followerPosQty` are ASCII identifiers. No new string literals introduced in either T1 or T3. |
| NT8: `DateTime.Now` ban | **PASS** | Section 5: "No DateTime usage introduced in T1 or T3." |
| NT8: `Account.All` in constructor ban | **PASS** | Section 11: `Account.All` is in pre-existing `Execute()` method, not in any constructor. Not modified by B191. |
| NT8: `CreateOrder` PTT-prefix | **PASS** | Section 5: "Neither T1 nor T3 creates orders. Not applicable." |
| NT8: `async/await` in lifecycle methods | **PASS** | No lifecycle methods modified. No async patterns introduced. |
| NT8: `AtmStrategyCreate` (StrategyBase-only) | **PASS** | Section 11: explicitly confirmed NOT used anywhere in B191. |

**PASS**

---

## Check 6 — DW Item Closures

| DW Item | Priority | Action in Plan | Result |
|---------|----------|----------------|--------|
| DW-B191-QTY-01 (NEW) | P0 | Closed by T1 (commit 365b8779) | **PASS** — Section 7, lines 350–359 |
| DW-B187-01 | P3 (from B190 backlog) | Closed by T3 | **PASS** — Section 7, lines 361–371 |
| DW-B183-03 | P1 (from B190 backlog) | Carried forward: OPEN | **PASS** — Section 8, lines 373–390 |

**DW-B187-01 closure integrity check**: The B190 deferred backlog (DW-B187-01) described three
open questions: (A) Is `ScaleLeaderTargets` dead code to remove? (B) Should `followerPosQty` come
from a proper data path? (C) Should the parameter be removed if scaling is confirmed dead?

The plan's T3 answers (B): `followerPosQty` is now sourced from `CopyEngine.FindPositionPublic`
— the same data path used elsewhere in the codebase. Questions (A) and (C) are rendered moot by
(B): the path is now live, so `ScaleLeaderTargets` is no longer dead code and the parameter
correctly carries live data. The closure is architecturally complete.

**PASS**

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|------------|--------------|
| T1: code at 365b8779, lines 177-181, no new edit needed | YES | Section 2 |
| T1: `perAccTargets = Build2TargetList(pos.Quantity)` inside per-account loop | YES | Section 2, lines 86–88 |
| T1: `forcedTargets` is root cause (shared leader qty) | YES | Section 1 root cause |
| T2: 5 pure arithmetic xUnit [Fact] tests, no Skip | YES | Section 3, Section 10 |
| T2: Oracle Build2TargetList(5)→{3,2} | YES | Section 3, T2-1 |
| T2: Oracle Build2TargetList(20)→{10,10} | YES | Section 3, T2-2 |
| T2: Oracle sum==5 | YES | Section 3, T2-3 |
| T2: Execute overload reflection check | YES | Section 3, T2-4 |
| T2: Oracle Build2TargetList(1)→{1,0} | YES | Section 3, T2-5 |
| T3: add `int followerPosQty` local, change arg 3 from 0 | YES | Section 4, lines 192–202 |
| T3: CYC of ExecuteFollowers remains <=4 | YES | Section 4, lines 221–225; CYC table |
| LANE-SPLIT GATE: SINGLE-PIPELINE with .cs file reason | YES | Gate block, lines 31–34 |
| DW-B191-QTY-01 closed by T1 | YES | Section 7 |
| DW-B187-01 closed by T3 | YES | Section 7 |
| DW-B183-03 carried forward | YES | Section 8 |

**All spec requirements addressed. Zero gaps.**

---

## Summary

| Checklist Category | Status |
|-------------------|--------|
| 1. LANE-SPLIT GATE | PASS |
| 2. T1 accuracy | PASS |
| 3. T2 test strategy | PASS |
| 4. T3 correctness | PASS |
| 5. DNA audit | PASS |
| 6. DW closures | PASS |

**Total violations**: 0
**Blocking issues**: None

---

## Phase 3 Authorization

Plan is approved for ticket generation.

Engineer notes for Phase 3 (ticket drafting):
- T1 ticket: `ticket-1-completion.md` only (code committed). No `.cs` edit. Build + SCAN-01/02 only.
- T2 ticket: Create `B191Tests.cs` with 5 `[Fact]` methods. `dotnet test` must report 5/0/0.
- T3 ticket: One new `int followerPosQty` line + one argument change in `ExecuteFollowers`. Build + B116 filter + ptt-sync 19 linked.

---

*Written by: ptt-plan-reviewer*
*Epic: B191*
*Phase: 2 (Plan Review)*
*Verdict: REVIEW_PASS*
