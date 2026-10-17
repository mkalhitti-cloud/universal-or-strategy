# B196 Plan Review — Phase 2

**Epic**: B196
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-10-11
**Plan under review**: docs/brain/B196/02-architecture-plan.md
**Reference files read**:
- docs/standards/jane-street/RULES_CATALOG.md (JS-001..JS-110)
- docs/brain/B169/02-architecture-plan.md (T3 source of truth)
- docs/brain/B161-B166/02-architecture-plan.md §3 (T4 source of truth)
- docs/brain/B161-B166/06-deferred-backlog.md (VIOLATION-01 design)
- docs/brain/B195/02-architecture-plan.md §8 (DW-B195-02 design)

---

## §1 Violations Found

**None.** All checklist items PASS. See detailed findings below.

---

## §2 LANE-SPLIT GATE COMPLIANCE

| Checklist Item | Result | Evidence |
|---------------|--------|---------|
| LANE-SPLIT GATE RESULT stated in plan | PASS | §1 line 14: `### LANE-SPLIT GATE RESULT: SEQUENTIAL-APPROVED` |
| Gate result is SEQUENTIAL-APPROVED | PASS | Confirmed: SEQUENTIAL-APPROVED with rationale table |
| No parallel execution — sequential mandate present | PASS | §1 line 23: "All 5 tickets execute sequentially (T1 → T2 → T3 → T4 in order). T5 is read-only and may run parallel with any ticket." |

---

## §3 Scope Completeness Matrix

| Requirement | Addressed in Plan? | Plan Section |
|-------------|-------------------|--------------|
| T1 (DW-B195-02): DrainThenDispatch fast-path ATM check | PASS | §3 T1 — full code sample, CYC 4→5 |
| T1 "now" variable placement note | PASS | §3 T1 line 149: "The existing `now = (long)(int)Environment.TickCount` at line 6613 is declared AFTER the fast-path early return. It is NOT accessible inside the fast-path block." — inlining rationale explicit |
| T2 (DW-B170-01): FindLeaderStopPrice + OrderState.Accepted | PASS | §3 T2 — predicate change and CYC 5→6 |
| T3 (DW-B169): _dragsInFlight HashSet | PASS | §3 T3 3a — field declaration, StringComparer.Ordinal, bounded-3-entry note |
| T3: TryTrackDragInFlight | PASS | §3 T3 3f — full body, ChangePending/ChangeRejected handling |
| T3: TryRemoveDragSuffix | PASS | §3 T3 3g — full body, CYC=2 |
| T3: Removal of IsLeaderLegBeingChanged + IsLeaderLegBeingChangedTestable | PASS | §2 "Absent (already deleted)" table: both confirmed ABSENT from source; §4 Removed Components table: "Already ABSENT from source. No removal needed." |
| T4: IsFollowerPositionBlocked CYC=8 (NOT CYC=9) | PASS | §3 T4: "must be added with CYC=8 compliance from day 1 (IsBeSettleOrRetrySignalActive pre-extracted). There is no CYC=9 intermediate state." |
| T4: VIOLATION-01 extraction (IsBeSettleOrRetrySignalActive) | PASS | §3 T4 — full code body; identified as "VIOLATION-01 extraction" explicitly |
| T4: B163 AFP guard (IsPttEntryOrderCancelTrigger AverageFillPrice) | PASS | §3 T4 RESTORE-1 — full guard code, CYC 3→4 |
| T4: B162-A (TryFireBePriceRetry atomic) already in source — no re-apply | PASS | Not in §2 ABSENT list; not in §4 component lists. Plan correctly omits it. |
| T4: B162-B subsumed by HasActivePttBeExits recreation | PASS | New HasActivePttBeExits body in §3 T4 uses `"PTT-BE-Stop"` (no trailing dash, correct per B162-B). |
| T4: B164 (HasActivePttBeTargets) must be re-added (ABSENT in source) | PASS | §2 ABSENT table; §3 T4 component order item 5; §4 New Components |
| T4: B165 (IsBeSlotNonTerminal) must be re-added (ABSENT in source) | PASS | §2 ABSENT table; §3 T4 component order item 1; §4 New Components |
| T4: B166 (RunFollowerEvictPath sentinel) already in source — no re-apply | PASS | Not in §2 ABSENT list; not in §4 component lists. Plan correctly omits it. |
| T5 (DW-B195-03): investigation-only, no .cs edits | PASS | §3 T5: "read-only"; §1 T5 row: "Investigation only" |
| T5 disposition stated | PASS | §3 T5: "No CYC>8 method found. DW-B195-03 is CLOSED." |

---

## §4 Source State Verification

| Grep Check | Result | Plan Location |
|-----------|--------|---------------|
| IsFollowerPositionBlocked ABSENT | PASS | §2 ABSENT table, row 1 |
| _dragsInFlight ABSENT | PASS | §2 ABSENT table, row 8 |
| AverageFillPrice guard ABSENT from IsPttEntryOrderCancelTrigger | PASS | §2 Present table: "Line 893: no AverageFillPrice guard — gap confirmed" |
| OrderState.Accepted ABSENT from FindLeaderStopPrice | PASS | §2 Present table: "Line 3714: OrderState.Working only — gap confirmed" |
| IsLeaderLegBeingChanged ABSENT (B169 dead version) | PASS | §2 "Absent (already deleted prior to B196)" table — both methods confirmed absent |
| ATM-mode check ABSENT from DrainThenDispatch fast-path | PASS | §2 Present table: "Fast-path at 6606: bare SubmitEntryDirect — gap confirmed" |
| DragsInFlightForTest seam ABSENT | PASS | §2 ABSENT table, row 11 |
| TryTrackDragInFlight ABSENT | PASS | §2 ABSENT table, row 9 |
| TryRemoveDragSuffix ABSENT | PASS | §2 ABSENT table, row 10 |

---

## §5 JS Rule Compliance

| Rule | Check | Result | Evidence |
|------|-------|--------|---------|
| JS-021: No lock() | All new/modified methods | PASS | §6: "No lock( anywhere." HashSet on single NT8 callback thread; ConcurrentDictionary ops. §3 T3 3a comment: "JS-021: no lock -- all NT8 order callbacks for the leader account execute on the same NT8 background thread; concurrent access is impossible." |
| JS-033: No async void | All new methods | PASS | §6: "All new methods are private void or private bool (non-async). No async void introduced." |
| JS-066: CYC ≤ 8 | All touched methods | PASS | §5 CYC Impact Table: maximum CYC across all methods = 8. No method exceeds ceiling. See §6 CYC Table Accuracy for detailed verification. |
| JS-001: No throw in hot paths | All new methods | PASS | §6: "All new methods use early return pattern. No throw statements." |
| JS-002: No null return where value expected | All bool helpers | PASS | §6: "All bool helpers return false on null/guard exits. No null returns." |
| ASCII-only (JS-081, JS-082) | All new string literals and identifiers | PASS | §6: New string literals: "-fast-atm-", "-be-settle-pending" log tags (ASCII). HashSet StringComparer.Ordinal. All identifiers ASCII. |
| DateTime.UtcNow — no DateTime.Now | All new code | PASS | §6: "No DateTime usage in B196 code. T1 uses Environment.TickCount (existing pattern)." |
| NT8 AddOnBase scope | SendCopyWithAtm, StartAtmStrategy | PASS | §6: "SendCopyWithAtm uses StartAtmStrategy (AddOnBase-confirmed)." |

---

## §6 CYC Table Accuracy

| Method | Checklist Expects | Plan States | Match? |
|--------|------------------|-------------|--------|
| DrainThenDispatch | 4→5 | 4→5 (§5) | PASS |
| FindLeaderStopPrice | 5→6 | 5→6 (§5) | PASS |
| HandleAtmStopSync | 2→3 | 2→3 (§5, §3 T3 3c) | PASS |
| TryHandleBracketDrag | no CYC increase | unchanged (§5) | PASS |
| TryCancelFollowerEntries | no CYC increase | unchanged (§5) | PASS |
| ResubmitCollateralLegs | no CYC increase | unchanged — guard replaces guard (§5, §3 T3 3e) | PASS |
| TryTrackDragInFlight (new) | CYC=5 | CYC=5 (§5, §3 T3 3f, §5 CYC note) | PASS |
| TryRemoveDragSuffix (new) | CYC=2 | CYC=2 (§5, §3 T3 3g) | PASS |
| IsPttEntryOrderCancelTrigger | 3→4 | 3→4 (§5) | PASS |
| TryReplaceOnAtmCancel | 4→5 | 4→5 (§5) | PASS |
| IsFollowerPositionBlocked (new) | CYC=8 | CYC=8 (§5, §3 T4, full branch count enumerated) | PASS |
| IsBeSettleOrRetrySignalActive (new) | CYC=4 | CYC=4 (§5) | PASS (Note A) |
| HasActivePttBeExits (new) | CYC≤8 | CYC=8 (§5) | PASS |
| HasActivePttBeTargets (new) | CYC≤8 | CYC=8 (§5) | PASS |
| HasSettlingBeTargets (new) | CYC≤8 | CYC≤8 (§5) | PASS |
| IsBeSlotNonTerminal (new) | CYC=1 | CYC=1 (§5) | PASS |
| IsBeRetryArmedFlat (new) | CYC≤3 | CYC=3 (§5) | PASS |
| DragsInFlightForTest (new) | CYC=1 | CYC=1 (§5) | PASS |

**Note A — IsBeSettleOrRetrySignalActive CYC overcounting** (non-blocking):
The plan states CYC=4 for `IsBeSettleOrRetrySignalActive` (base + if-slot + if-retry + "short-circuit"). The code body has exactly two sequential `if` statements with no compound `&&`/`||` operator visible; standard McCabe would yield CYC=3. The plan overcounts by 1, reflecting the B161-B166 deferred backlog formula. An overcounted CYC is conservative — the method is safer than stated and remains well under the ≤8 ceiling. No JS-066 violation. The engineer may use CYC=3 in the inline comment if they measure independently; both values are compliant.

**Note B — TryTrackDragInFlight CYC discrepancy with B169** (non-blocking):
B169 plan §8 stated CYC=3. B196 plan §5 correctly recomputes CYC=5 (base + !IsStopLeg + IsNullOrEmpty + ChangePending + ChangeRejected). The B196 correction is accurate and documented at §5 note. No violation.

---

## §7 Test Plan Verification

| Checklist Item | Result | Plan Section |
|---------------|--------|-------------|
| T1: DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm [Fact] | PASS | §7 T1 Tests — present |
| T2: FindLeaderStopPrice_AcceptedStop_ReturnsPrice [Fact] | PASS | §7 T2 Tests — present |
| T2: FindLeaderStopPrice_WorkingStop_StillReturnsPrice [Fact] | PASS | §7 T2 Tests — present |
| T4: IsPttEntryOrderCancelTrigger_FilledEntry_ReturnsFalse [Fact] | PASS | §7 T4 Tests — present |
| T4: IsFollowerPositionBlocked_HasActivePttBeExits_ReturnsTrue [Fact] | PASS | §7 T4 Tests — present |
| T3: DwB168_02Tests — 4 [Fact] tests using DragsInFlightForTest seam | PASS | §7 T3 Tests — all 4 test names present: DragsInFlight_SingleStop_CollateralResubmitted, DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg, DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs, DragsInFlight_ClearOnCancelled_PreventsPermamentSkip |

---

## §8 Deferred Work Register Verification

| Checklist Item | Result | Plan Location |
|---------------|--------|---------------|
| §8 closes DW-B195-02 | PASS | §8 Items Closed table |
| §8 closes DW-B170-01 | PASS | §8 Items Closed table |
| §8 closes DW-B169 COLLATERAL-STORM | PASS | §8 Items Closed table |
| §8 closes B161-B166 Ghost-Entry + B163 AFP | PASS | §8 Items Closed table |
| §8 closes DW-B195-03 | PASS | §8 Items Closed table |
| Carries forward DW-B183-03 | PASS | §8 Items Carried Forward |
| Carries forward DW-B192-SINST-01 | PASS | §8 Items Carried Forward |
| Carries forward DW-B193-01 | PASS | §8 Items Carried Forward |
| Carries forward DW-B159-K2 | PASS | §8 Items Carried Forward |
| Carries forward DW-B159-K3 | PASS | §8 Items Carried Forward |
| Carries forward DW-B159-K4 | PASS | §8 Items Carried Forward |
| Carries forward DW-B167-DEFER-01 | PASS | §8 Items Carried Forward |
| Carries forward DW-B167-DEFER-03 | PASS | §8 Items Carried Forward |
| Carries forward DW-B167-DEFER-04 | PASS | §8 Items Carried Forward |

**Additional**: `DW-B170-SIM` (P0 SIM gate) appears as a new first-appearance item in §8. This is appropriate — it was identified as open in B170/05-final-review.md but not carried through subsequent backlog files. Reactivation is correct.

---

## §9 Cross-Reference Consistency

### T1 vs DW-B195-02 design (B195 §8)

B195 §8 DW-B195-02 specifies the fix direction:
> `var fastAtm = GetCloneAtmMode();`
> `if (fastAtm is FollowerAtmMode.Named namedFastAtm) { var sig = CopySignal.Create(action, orderType, qty, price, acctKey + "-fast-atm-" + now); SendCopyWithAtm(...); } else { SubmitEntryDirect(...); }`

B196 §3 T1 implements this with a minor deviation: the orderId uses `follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount` instead of `acctKey + "-fast-atm-" + now`. This is **correct** — the `now` variable is not accessible in the fast-path (documented in plan). `follower.Name` equals `acctKey` in this context (the account name). No functional difference. **CONSISTENT.**

### T3 vs B169 source of truth

B196 §3 T3 faithfully reproduces all B169 §§5a-5h design elements:
- Field declaration (3a) ✅ matches B169 §5a exactly
- TryHandleBracketDrag call (3b) ✅ matches B169 §5b exactly
- HandleAtmStopSync remove (3c) ✅ matches B169 §5c exactly
- TryCancelFollowerEntries (3d) ✅ matches B169 §5d exactly
- ResubmitCollateralLegs (3e) ✅ matches B169 §5g exactly (guard precedes FindLeaderCollateralOrder)
- TryTrackDragInFlight (3f) ✅ body matches B169 §5e; CYC correctly corrected to 5
- TryRemoveDragSuffix (3g) ✅ matches B169 §5f exactly
- DragsInFlightForTest (3h) ✅ matches B169 §5h exactly

Line number stale-warning at §3 T3 opening is appropriate — engineer re-grep mandate present. **CONSISTENT.**

### T4 vs B161-B166 source of truth

B196 §3 T4 correctly extracts from B161-B166 §3:
- IsBeSlotNonTerminal body matches B165 §3
- IsBeRetryArmedFlat body matches B166/B165 §3 IsBeRetryArmedFlat
- IsBeSettleOrRetrySignalActive body matches DW-B161B166-01 deferred backlog remedy exactly
- HasActivePttBeExits body matches B162-B corrected prefix (no trailing dash) ✅
- HasActivePttBeTargets body matches B164 §3 exactly (PTT-BE-Target- WITH dash) ✅
- IsFollowerPositionBlocked 6-signal parent matches B165 §4 signal map ✅
- RESTORE-1 AFP guard matches B163 §3 exactly ✅
- RESTORE-2 IsFollowerPositionBlocked guard matches B161 §3 exactly ✅

B161-B166/06-deferred-backlog.md DW-B161B166-01 status is CLOSED (applied by B196-T4). B196 plan acknowledges this: "DW-B161B166-01 was 'CLOSED' in the deferred backlog but the extraction was never applied to source — B196-T4 applies the compliant version." **CONSISTENT.**

---

## §10 Summary of Findings

| Category | Total Checks | PASS | FAIL | Violations |
|----------|-------------|------|------|-----------|
| LANE-SPLIT GATE | 3 | 3 | 0 | None |
| Scope Completeness | 15 | 15 | 0 | None |
| Source State Verification | 9 | 9 | 0 | None |
| JS Rule Compliance | 8 | 8 | 0 | None |
| CYC Table Accuracy | 18 | 18 | 0 | None (2 non-blocking notes) |
| Test Plan | 6 | 6 | 0 | None |
| Deferred Work Register | 14 | 14 | 0 | None |
| Cross-Reference Consistency | 3 | 3 | 0 | None |
| **TOTAL** | **76** | **76** | **0** | **0 violations** |

---

## Final Verdict

**REVIEW_PASS**

All 76 checklist items PASS. Zero JS rule violations found. Plan is architecturally sound, internally consistent, and consistent with all reference source plans (B169, B161-B166, B195).

**Non-blocking notes** (documentation imprecisions, no engineer action required):
1. `IsBeSettleOrRetrySignalActive` CYC stated as 4; actual standard McCabe count is 3. Conservative overcounting — compliant regardless.
2. `TryTrackDragInFlight` CYC corrected from B169's CYC=3 to CYC=5 — correction is accurate and documented in plan.

**Gate status**: REVIEW_PASS — Phase 3 (ticket generation) is UNLOCKED.

---

*Reviewer: ptt-plan-reviewer*
*Epic: B196*
*Phase: 2 (Plan Review)*
*Return: REVIEW_PASS*
