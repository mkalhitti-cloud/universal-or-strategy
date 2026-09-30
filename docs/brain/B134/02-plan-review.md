# B134 Plan Review

**Epic**: B134 -- DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index)
**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Plan File**: docs/brain/B134/02-architecture-plan.md
**Standards**: docs/standards/jane-street/RULES_CATALOG.md (JS-001, JS-002, JS-021)
**NT8 Refs**: docs/standards/NT8_FULL_REFERENCE.md, docs/standards/NT8_ADDON_KNOWLEDGE.md

---

## VERDICT: REVIEW_PASS

Zero violations. All checklist sections pass. Plan is cleared for Phase 3 (ticket generation).

---

## Section A — Source Investigation Completeness

| Check | Result | Evidence in Plan |
|-------|--------|-----------------|
| A1: SyncFollowerBracket (L2179-2210) read | PASS | Plan §B.1: full call-site code shown; `fo == null` early-return identified as DW-B144 observable failure |
| A2: FindFollowerBracketOrder (L2538-2566) read | PASS | Plan §B.2: full method body with line refs, CYC=6 annotation, Working\|\|Accepted state filter identified as root cause |
| A3: SyncAtmFollowerTarget Phase C + DeriveLeaderBracketIndex (L2312-2423) read | PASS | Plan §B.4: DeriveLeaderBracketIndex trailing-digit parse documented; SyncAtmFollowerTarget Phase C role confirmed; explicitly states "No changes to these methods in B134" |
| A4: NT8 Cancel-on-Submitted safety ruling | PASS | Plan §B.5: cites NT8_FULL_REFERENCE.md Account.Cancel L2408-2452 (no state restriction), OrderState table L3357-3374 (Submitted = non-terminal), NT8_ADDON_KNOWLEDGE.md L222 (Submitted listed as live state), ErrorCode.UnableToCancelOrder identified; existing try/catch coverage confirmed |

**Section A: PASS**

---

## Section B — DW-B144 Architecture

| Check | Result | Evidence in Plan |
|-------|--------|-----------------|
| B1: Option A or B with NT8 rationale | PASS | Plan §C: Option A selected; rationale: TP4 evidence (Submitted at drag time), NT8 no documented Cancel restriction, existing try/catch absorbs UnableToCancelOrder, Option B is 5x code for zero safety gain |
| B2: Exact lines/predicate change specified | PASS | Plan §C: L2549 specified; before/after code shown precisely -- three-condition `!= Working && != Accepted && != Submitted` |
| B3: CYC impact Ticket 1 calculated | PASS | Plan §E: pre-B134=6, post-T1=7 -- explicit in CYC table |
| B4: Option B not chosen (ConcurrentQueue not needed) | N/A | Option A selected |
| B5: No throw introduced around Cancel (JS-001) | PASS | Plan §F: JS-001 PASS stated; FindFollowerBracketOrder is a predicate-only method with zero Cancel calls; Cancel calls are in SyncAtmFollowerTarget and SyncAtmFollowerBracket (both already wrapped in try/catch per §B.5) |

**Section B: PASS**

---

## Section C — DW-B145 Architecture

| Check | Result | Evidence in Plan |
|-------|--------|-----------------|
| C1: NO-OP vs real fix decision conclusive | PASS | Plan §D: "Not a NO-OP: DW-B145 persists after Ticket 1 alone" -- no ambiguity |
| C2: Code path traced for NO-OP | N/A | Decision is real fix |
| C3: Exact method + param + line for real fix | PASS | Plan §B.3 + §D: SignalOrNameMatches path (1) fires for all ATM brackets (shared FromEntrySignal); leaderName exact-match path (3) never reached; after T1 all three targets pass state filter; insert guard `if (leaderName != null && order.Name != leaderName) continue;` at line 2547 in FindFollowerBracketOrder list overload; complete post-B134 method body shown |
| C4: CYC impact Ticket 2 calculated | PASS | Plan §E: post-T1+T2=8; CYC increase: T1 adds 1 branch (7), T2 adds 1 branch (8) |
| C5: Combined CYC of affected method <= 8 | PASS | CYC=8 AT LIMIT; plan explicitly notes "AT LIMIT; PASS" and documents `foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8` |

**Section C: PASS**

---

## Section D — Test Plan

| Check | Result | Evidence in Plan |
|-------|--------|-----------------|
| D1: B134Ticket1Tests >= 5 [Fact] | PASS | Plan §G: 5 named facts -- T1_SubmittedState_StopOrder_Found, T1_SubmittedState_TargetOrder_Found, T1_WorkingState_StillFound_Regression, T1_AcceptedState_StillFound_Regression, T1_NullOrder_NotMatched_Guard |
| D2: B134Ticket2Tests >= 3 [Fact] (real fix) | PASS | Plan §G: 3 named facts -- T2_Target3_ReturnsTarget3_NotTarget1, T2_Target1_ReturnsTarget1_WhenRequested, T2_NullLeaderName_ReturnsFirstMatch |
| D3: Prior regression guard stated | PASS | Plan §G table: B133x10, B132x6 (spec "5+1"=6 -- matches), B131x7, B130x8, B129x13. All present. |
| D4: Tests/B134Tests.cs registration in .csproj | PASS | Plan §F (Constraint table, last row): `<Compile Include="Tests\B134Tests.cs" />` after B133Tests.cs entry at L161 |

**Section D: PASS**

---

## Section E — Constraint Compliance

| Constraint | Rule | Result | Evidence in Plan |
|------------|------|--------|-----------------|
| No lock() | JS-021 (P0-CRITICAL) | PASS | Plan §F: "Pure predicate changes; no state mutation; no lock() in new/modified code" |
| No throw in hot path | JS-001 (P0-CRITICAL) | PASS | Plan §F: "FindFollowerBracketOrder and SignalOrNameMatches contain zero Cancel calls; no throw risk." B134 changes are predicate-only guards inside FindFollowerBracketOrder; no new throw sites |
| Order? null contract preserved | JS-002 (P0-CRITICAL) | PASS | Plan §F: "`return null` at L2565 unchanged." Spec explicitly permits this NT8 interop pattern; null contract is the required interface |
| ASCII-only | NT8/DNA mandate | PASS | Plan §F: "No new string literals; 'Submitted' is ASCII; 'B134 DW-B144/DW-B145' in comments are ASCII" |
| _diagnosticMode stays true through B134 SIM | Spec constraint | PASS | Plan §F: "No changes to diagnostic mode fields or initialization" |

**Section E: PASS**

---

## Section F — Files Changed

| File | Type | Result | Evidence in Plan |
|------|------|--------|-----------------|
| src/PropTraderTools/CopyEngine.cs | MODIFY | PASS | Plan §H row 1: two surgical edits in FindFollowerBracketOrder list overload (L2538-2566) |
| src/PropTraderTools/Tests/B134Tests.cs | NEW | PASS | Plan §H row 2: 8 xUnit [Fact] tests, B134FindFollowerBracketOrderTests class |
| src/PropTraderTools/PropTraderTools.csproj | MODIFY | PASS | Plan §H row 3: Compile entry registration |

**Section F: PASS**

---

## NT8 API Validation

| Claim | Source | Verified? |
|-------|--------|-----------|
| Account.Cancel() has no documented state restriction | NT8_FULL_REFERENCE.md L2408-2452 | YES -- syntax is `Cancel(IEnumerable<Order> orders)`, no state precondition documented |
| OrderState.Submitted is non-terminal (live) | NT8_FULL_REFERENCE.md L3357-3374 | YES -- terminal states are Cancelled, Rejected, Filled, Unknown only |
| ErrorCode.UnableToCancelOrder exists | NT8_FULL_REFERENCE.md L3378 | YES -- listed in ErrorCode enum |
| NT8_ADDON_KNOWLEDGE.md lists Submitted as live state | NT8_ADDON_KNOWLEDGE.md L222 | YES -- "order.OrderState // Submitted / Working / Accepted / Filled / Cancelled" |

All NT8 API claims are verified against authoritative references. The NT8 ruling in plan §B.5 is sound.

---

## Violation Summary

| ID | Rule | Location in Plan | Status |
|----|------|-----------------|--------|
| (none) | -- | -- | -- |

**Total violations: 0**

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|-------------|
| DW-B144: FindFollowerBracketOrder state filter extends to Submitted | YES | §B.2, §C |
| DW-B145: fo=Target1 wrong selection -- determine if B144 fix alone resolves | YES | §B.3, §D -- conclusively NO-OP ruled out, real fix specified |
| DW-B145: DeriveLeaderBracketIndex result reaching FindFollowerBracketOrder | YES | §B.4 -- DeriveLeaderBracketIndex changes not needed; fix is at FindFollowerBracketOrder selection layer |
| JS-021: no lock() | YES | §F |
| JS-001: no throw in hot path | YES | §F |
| JS-002: FindFollowerBracketOrder returns Order? null contract unchanged | YES | §F |
| CYC <= 8 per method (combined both tickets) | YES | §E -- CYC=8 AT LIMIT; PASS |
| ASCII-only | YES | §F |
| _diagnosticMode stays true through B134 SIM | YES | §F |
| Tests/B134Tests.cs registered in PropTraderTools.csproj | YES | §F, §H |
| DO NOT modify existing test files | YES | §H: "Files NOT touched: ... any B129-B133 test file" |
| Minimum 5 [Fact] B134Ticket1Tests | YES | §G -- 5 named facts |
| If DW-B145 real fix: minimum 3 [Fact] B134Ticket2Tests | YES | §G -- 3 named facts |
| Prior regression guard (B133x10, B132x5+1, B131x7, B130x8, B129x13) | YES | §G, §I |

**All spec requirements addressed. Coverage: 14/14.**

---

## Gate Decision

**REVIEW_PASS**

The architecture plan is complete, internally consistent, and compliant with all Jane Street rules (JS-001, JS-002, JS-021), NT8 constraints, and spec requirements. CYC reaches exactly 8 on the modified method — at the ceiling but valid. NT8 Cancel-on-Submitted safety is established by documentary evidence from both NT8_FULL_REFERENCE.md and NT8_ADDON_KNOWLEDGE.md.

Phase 3 (ticket generation) is **UNLOCKED**.

---

*Reviewed by ptt-plan-reviewer, B134 Phase 2.*
