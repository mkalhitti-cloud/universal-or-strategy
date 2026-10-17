# B209 Plan Review — PTT-COPIER-B209 BE-ALL Permanent Lock Fix (RETRY)

**Block**: B209
**Phase**: 2 — Plan Review (RETRY after architect corrections)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-10-16
**Plan under review**: `docs/brain/B209/02-architecture-plan.md` (revised)
**Prior review result**: REVIEW_FAIL (2 violations — now re-reviewed from scratch)
**Result**: REVIEW_PASS

---

## VIOLATIONS

None. All prior violations corrected. All 18 checklist items PASS.

---

## PER-ITEM CHECKLIST RESULTS

| # | Item | Result | Evidence |
|---|------|--------|----------|
| 1 | LANE-SPLIT GATE: present + Q1-Q4 correct + SEQUENTIAL mandate | PASS | Gate Q1=NO, Q2=NO, Q3=YES, Q4=YES → LANES-APPROVED (lines 13-33). Sequential mandate: "Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS is confirmed" (line 314-316). Prior VIOLATION-1 corrected. |
| 2 | ROOT CAUSE: B208 OCO flag leak correctly described | PASS | Lines 39-55. Nine-step sequence correctly traces PTT-BE-Stop-N CancelSubmitted→Cancelled (never Working) path, permanent flag lock, NakedPositionDetector suppression. |
| 3 | FIX-G-1 DESIGN: body matches spec (Working\|Cancelled\|Rejected\|Accepted) | PASS | Plan lines 99-113 match orchestrator spec lines 105-109 exactly. Four-state isClearState boolean correct. |
| 4 | FIX-G-1 CYC: spec-declared=5, strict=7, both <=8 | PASS | CYC analysis lines 116-126. Plan states strict Lizard CYC=7 (conservative), spec-declared=5. Both <=8 per JS complexity rule. |
| 5 | FIX-G-2 DESIGN: body matches spec (PTT-BE-* + IsBeSwapInProgress guard) | PASS | Plan lines 155-162 match orchestrator spec lines 155-158 exactly. Guard placement after IsStaleGtcExitOrder early-return correct. |
| 6 | FIX-G-2 CYC: spec-declared=2, strict<=7, both <=8 | PASS | CYC analysis lines 177-187. Plan states strict Lizard CYC<=7, spec-declared=2. Both <=8. |
| 7 | Testable seams: both present with correct signatures | PASS | IsBeClearStateTrigger(string orderName, OrderState state): internal static bool (line 205). IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress): internal static bool (line 222). Both match orchestrator spec exactly. |
| 8 | TEST COVERAGE: 10 named [Fact] tests, all states + edge cases | PASS | 10 tests listed lines 244-261 (table) and 275-304 (skeleton). Tests 1-6 cover IsBeClearStateTrigger (Working/Cancelled/Rejected/Accepted true + Submitted false + wrong name false). Tests 7-10 cover IsBeSwapGtcSweepGuard (swap active/inactive, non-PTT-BE name, null). Minimum 6 from spec exceeded. xUnit [Fact] only — no NUnit/MSTest. |
| 9 | JS-021: no lock() in new/modified code | PASS | All state access via ConcurrentDictionary<string,bool> _beSwapInProgress (plan line 407). No lock() in any proposed code block. Compliance matrix line 375 confirms. |
| 10 | JS-001: no throw in hot paths | PASS | FIX-G-1: void, no throw. FIX-G-2: no new throw; existing try/catch preserved unchanged. Both seams: bool return, no throw. Compliance matrix line 376 confirms. |
| 11 | JS-033: no async void | PASS | All new methods synchronous void or bool. No async keyword anywhere in proposed code. Compliance matrix line 378 confirms. |
| 12 | ASCII-only string literals | PASS | "PTT-BE-Stop", "PTT-BE-", "[GTC-SWEEP]", "PTT-BE-Stop-1", "PTT-QX-Stop-1", "B209-FIX-G-1", "B209-FIX-G-2" — all ASCII. Compliance matrix line 381 confirms. |
| 13 | TICKET DECOMPOSITION: Ticket-1=FIX-G-1+seam, Ticket-2=FIX-G-2+seam+tests | PASS | Ticket-1 (lines 319-340): MODIFY TryClearBeSwapOnStopWorking + ADD IsBeClearStateTrigger. Ticket-2 (lines 345-372): MODIFY TryCancelSingleStaleGtcExit + ADD IsBeSwapGtcSweepGuard + CREATE B209Tests.cs. Scope boundaries correct. |
| 14 | ptt-sync requirement documented | PASS | Dedicated section lines 449-453 + per-ticket post-edit mandatories (lines 339, 370). Command: powershell -File .\scripts\ptt-sync.ps1. |
| 15 | Guardian symbol check requirement documented | PASS | Dedicated section lines 455-458 + per-ticket (lines 340, 371). Command: powershell -File .\scripts\verify_critical_symbols.ps1. |
| 16 | Ph4b deletion audit requirement documented | PASS | Dedicated section lines 462-475 with exact PowerShell command. Both Ticket-1 and Ticket-2 deletion expectations stated. |
| 17 | Single-hunk ban documented | PASS | Lines 476-477: apply_diff/search_and_replace mandate stated. `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` = false requirement documented. |
| 18 | SPEC UPDATE OBLIGATIONS: all three specs/002-trade-copier-spec.html updates present | PASS | (A) Lines 489-492: mark FIX-G-1+FIX-G-2 APPLIED with ptt-sync result. (B) Lines 494-497: change card title to "FIX-G APPLIED — AWAITING SIM ROUND 7". (C) Lines 499-501: update bug table row status column. Prior VIOLATION-2 corrected. |

---

## SPEC COVERAGE MATRIX

| Requirement (orchestrator-prompt.md) | Addressed in Plan? | Plan Section |
|--------------------------------------|-------------------|--------------|
| Root cause — OCO path permanent flag lock | YES | Root Cause Analysis §1, lines 39-55 |
| Root cause — premature GTC sweep of live BE stop | YES | Root Cause Analysis §2, lines 57-70 |
| FIX-G-1: TryClearBeSwapOnStopWorking Working\|Cancelled\|Rejected\|Accepted | YES | Fix-G-1 Design, lines 86-114 |
| FIX-G-1 CYC=5 (spec-declared), strict<=7, both <=8 | YES | CYC Analysis, lines 116-126 |
| FIX-G-2: TryCancelSingleStaleGtcExit PTT-BE-* guard | YES | Fix-G-2 Design, lines 143-174 |
| FIX-G-2 CYC=2 (spec-declared), strict<=7, both <=8 | YES | CYC Analysis, lines 177-187 |
| IsBeClearStateTrigger seam with correct signature | YES | Testable Seam Designs §1, lines 200-214 |
| IsBeSwapGtcSweepGuard seam with correct signature | YES | Testable Seam Designs §2, lines 217-225 |
| InternalsVisibleTo verification required | YES | Lines 229-233 |
| 10 [Fact] xUnit tests (minimum 6 from spec) | YES | Test Coverage Plan, lines 238-305 |
| JS-021 no lock() | YES | Compliance Matrix line 375; ConcurrentDictionary throughout |
| JS-001 no throw in hot paths | YES | Compliance Matrix line 376; existing catch preserved |
| JS-002 no return null | YES | Compliance Matrix line 377; all new methods void or bool |
| JS-033 no async void | YES | Compliance Matrix line 378; synchronous only |
| ASCII-only string literals | YES | Compliance Matrix line 381 |
| ptt-sync after every CopyEngine.cs edit | YES | Lines 449-453 + per-ticket post-edit mandatories |
| Guardian symbol check after CopyEngine.cs edit | YES | Lines 455-458 + per-ticket post-edit mandatories |
| Ph4b deletion audit before VERIFY_PASS | YES | Lines 462-475 (exact command) |
| Single-hunk ban | YES | Lines 476-477 |
| Ticket-1 = FIX-G-1 + IsBeClearStateTrigger seam | YES | Ticket-1, lines 319-340 |
| Ticket-2 = FIX-G-2 + IsBeSwapGtcSweepGuard seam + B209Tests.cs | YES | Ticket-2, lines 345-372 |
| Sequential execution mandate (same write-set file) | YES | Lines 314-316: Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS |
| specs/002-trade-copier-spec.html — B208 block APPLIED | YES | Lines 489-492 |
| specs/002-trade-copier-spec.html — card title update | YES | Lines 494-497 |
| specs/002-trade-copier-spec.html — bug table row status | YES | Lines 499-501 |
| 06-deferred-backlog.md update | YES | Lines 503-505 |
| NakedPositionDetector cross-check | YES | Lines 507-510 |

---

## PRIOR VIOLATIONS — DISPOSITION

### VIOLATION-1 (Prior): PARALLEL execution declared for shared write-set

**Status**: CORRECTED

Prior plan (line 312): `LANES-APPROVED — two parallel tickets.`

Revised plan (lines 312-316):
> **LANE-SPLIT GATE RESULT: LANES-APPROVED**
> **Sequential execution required** — Ticket-1 and Ticket-2 share the `CopyEngine.cs` write-set.
> Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS is confirmed.
> Execution order: Ticket-1 → VERIFY_PASS → Ticket-2.

PARALLEL LANE PROTOCOL violation is resolved. Sequential execution mandate is unambiguous.

---

### VIOLATION-2 (Prior): specs/002-trade-copier-spec.html update obligations absent

**Status**: CORRECTED

Revised plan Section "Spec Update Requirements (Ph5)" lines 486-511 now explicitly addresses all three obligations from the orchestrator spec (lines 252-256):

- (A) Mark FIX-G-1+FIX-G-2 APPLIED with ptt-sync result: present at plan lines 489-492.
- (B) Card title from "FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL" to "FIX-G APPLIED — AWAITING SIM ROUND 7": present at plan lines 494-497.
- (C) Bug table row status column update: present at plan lines 499-501.

SPEC COMPLETENESS violation is resolved.

---

## NT8 API SURFACE NOTE

Plan NT8 API verification table (lines 395-412) confirms:
- `Account.Cancel(Order[])` is AddOnBase-available (line 2182, confirmed).
- `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` correctly identified as StrategyBase-only and marked N/A.
- All claimed `OrderState.*` enum values (Working, Cancelled, Rejected, Accepted) confirmed as valid NT8 enum members.

No NT8 API violations found in the plan.

---

## THREADING MODEL NOTE

Plan threading section (lines 418-425) correctly identifies both methods as NT8 `OnOrderUpdate` event-thread callers. `_beSwapInProgress` ConcurrentDictionary access is lock-free. No UI updates. No `Dispatcher.InvokeAsync` required. No new `ConcurrentQueue` ownership. Threading model is clean per JS-021 and JS-023.

---

## SUMMARY

The architect made exactly the two targeted corrections required:
1. Replaced "two parallel tickets" with an explicit sequential execution mandate (Ticket-2 blocked on Ticket-1 VERIFY_PASS).
2. Added all three `specs/002-trade-copier-spec.html` update obligations to the Ph5 spec section.

Re-review from scratch confirms all 18 items PASS. No new violations introduced. Plan is architecturally sound, JS-compliant, and spec-complete.

Ph3 (ticket generation) is now unblocked.

---

## RETURN VALUE

**REVIEW_PASS**
