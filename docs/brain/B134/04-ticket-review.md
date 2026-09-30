# B134 Ticket Review

**Epic**: B134 -- Two-Ticket: DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index)
**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Review)
**Ticket File**: docs/brain/B134/04-tickets.md
**Plan File**: docs/brain/B134/02-architecture-plan.md
**Plan Review**: docs/brain/B134/02-plan-review.md (REVIEW_PASS)
**Standards**: docs/standards/jane-street/RULES_CATALOG.md (JS-001, JS-002, JS-021)

---

## T1 -- DW-B144: Submitted-state gap

### Traceability

| Item | Check | Result |
|------|-------|--------|
| Cites DW-B144 as spec req-ID | Ticket 1 §"Spec Requirements": "DW-B144: FindFollowerBracketOrder rejects OrderState.Submitted..." | PASS |
| Maps to architecture plan | Ticket 1 root cause and exact edit maps directly to plan §C and §B.2 | PASS |
| CopyEngine.cs L2538-2566 verified | Source read: method runs exactly L2538-2566; state filter at L2549 matches BEFORE block verbatim | PASS |
| No phantom work | All described changes (state filter + comment + B134Tests.cs + .csproj) present in plan §H | PASS |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Ticket Section | Check | Result |
|------|---------------|-------|--------|
| JS-021 (no lock()) | §7-Scan Checklist SCAN-01, §Combined Constraint Summary | "Pure predicate extension; no state mutation, no lock." Zero lock() in FindFollowerBracketOrder. | PASS |
| JS-001 (no throw in hot path) | §7-Scan Checklist SCAN-02, §Combined Constraint Summary | "FindFollowerBracketOrder is a predicate-only method; no throw introduced." acc.Cancel() lives in SyncAtmFollowerTarget/SyncAtmFollowerBracket (already wrapped in try/catch). | PASS |
| JS-002 (Order? null contract) | §7-Scan Checklist SCAN-05, §Combined Constraint Summary | "return null; at L2565 (or equivalent after line shift) is still present. Order? nullable return type is unchanged." Confirmed in source: return null at L2565 preserved. NT8 interop contract explicitly documented. | PASS |
| JS-033 (no async void) | N/A | Method is synchronous; no async/await present. N/A correctly implied. | PASS |
| JS-036 (no new byte[] in hot path) | N/A | Predicate-only change; no allocations. N/A correctly implied. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Stage | CYC | Formula | Result |
|-------|-----|---------|--------|
| Pre-B134 | 6 | foreach(1) + SignalOrNameMatches(1) + state-filter(2) + isStop(1) + type-match(1) = 6 | PASS |
| Post-T1 only (intermediate) | 7 | +1 branch for Submitted condition | PASS |
| Post-T1+T2 (committed form) | 8 | +1 branch for leaderName guard | AT LIMIT; PASS |

Ticket states CYC before=6, after T1=7, after T1+T2=8. All within JS ceiling of 8.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Check | Ticket Section | Result |
|-------|---------------|--------|
| Account.Cancel() safety | §Root Cause (NT8 Cancel-on-Submitted safety): cites NT8_FULL_REFERENCE.md L2408-2452, NT8_ADDON_KNOWLEDGE.md L222, ErrorCode.UnableToCancelOrder. Cancel not in FindFollowerBracketOrder itself. | PASS |
| OrderState.Submitted confirmed non-terminal | §Root Cause: "OrderState.Submitted is a non-terminal (live) state (L3357-3374)." Confirmed by plan §B.5. | PASS |
| No async/await in lifecycle | N/A -- pure predicate change | PASS |
| No FontFamily / hardcoded hex / DateTime.Now | N/A -- no UI or date code | PASS |
| CreateOrder name prefix "PTT-" | N/A -- no CreateOrder call in FindFollowerBracketOrder | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| Method / Class | [Fact] Name | Present? |
|---------------|-------------|---------|
| FindFollowerBracketOrder | T1_SubmittedState_StopOrder_Found_And_Returned | PASS |
| FindFollowerBracketOrder | T1_SubmittedState_TargetOrder_Found_And_Returned | PASS |
| FindFollowerBracketOrder | T1_WorkingState_StillFound_Regression | PASS |
| FindFollowerBracketOrder | T1_AcceptedState_StillFound_Regression | PASS |
| FindFollowerBracketOrder | T1_NullOrder_NotMatched_Guard | PASS |
| B134Ticket1Tests total | 5 [Fact] (minimum 5) | PASS |
| Regression guard B129x13, B130x8, B131x7, B132x6, B133x10 | §7-Scan SCAN-07 + §Prior Block Regression Guard | PASS |
| "DO NOT MODIFY existing test files" | NOT EXPLICITLY STATED in Ticket 1 text. §Files Changed table implies restriction by omission only. No verbal "DO NOT MODIFY existing test files" mandate present in ticket body. | **FAIL** |

**Test Coverage: FAIL**

Violation: The STEP 3 test-coverage checklist item `"DO NOT MODIFY existing test files" explicitly stated in both tickets` is not satisfied. Ticket 1 §Files Changed table lists only `B134Tests.cs` (NEW) -- the existing test files B129-B133 do not appear -- but the explicit prohibition phrase is absent from the ticket body. Without this explicit engineer directive, the engineer may add helpers or fixtures to existing test files during implementation and not recognize the violation. The plan §H states it ("Files NOT touched: ... any B129-B133 test file"), but the ticket must reproduce the constraint as an engineer-facing directive.

---

### Scan Checklist Presence

| Scan | Rule | Present? | Pass Criterion Present? |
|------|------|----------|------------------------|
| SCAN-01 | JS-021: no lock() | YES | YES -- "0 matches in new/modified lines; pure predicate extension; no lock." |
| SCAN-02 | JS-001: no throw | YES | YES -- "0 throw statements introduced; predicate-only method." |
| SCAN-03 | ASCII-only | YES | YES -- "0 non-ASCII characters in new/modified lines." |
| SCAN-04 | CYC <= 8 | YES | YES -- "Post-T1+T2 (committed form) = 8. AT LIMIT; <= 8 PASS." |
| SCAN-05 | JS-002: null contract | YES | YES -- "return null; at L2565 still present; Order? return type unchanged." |
| SCAN-06 | Build 0 errors | YES | YES -- "Exit code 0. 0 build errors. 0 new warnings." |
| SCAN-07 | Prior tests pass | YES | YES -- "0 regressions in prior block tests. B134Ticket1Tests: 5 PASS." |

**Scan Checklist: PASS** (all 7 scans present with pass criteria)

---

### File Routing

| File Path | Correct Wave Workspace? |
|-----------|------------------------|
| `src/PropTraderTools/CopyEngine.cs` | YES -- c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |
| `src/PropTraderTools/Tests/B134Tests.cs` | YES -- c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |
| `src/PropTraderTools/PropTraderTools.csproj` | YES -- c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |

**File Routing: PASS**

---

### T1 VERDICT: TICKET_REVIEW_FAIL

**Violation**: "DO NOT MODIFY existing test files" not explicitly stated in Ticket 1 body (§Files Changed or §New Tests section). Ticket reference: `## TICKET 1 -- DW-B144 > ### New Tests -- Ticket 1` and `### Files Changed -- Ticket 1`.

---

## T2 -- DW-B145: Wrong bracket index returned

### Traceability

| Item | Check | Result |
|------|-------|--------|
| Cites DW-B145 as spec req-ID | Ticket 2 §"Spec Requirements": "DW-B145: After Ticket 1 fixes..." | PASS |
| Maps to architecture plan | Ticket 2 root cause, exact edit, and CYC analysis maps to plan §D and §B.3 | PASS |
| CopyEngine.cs L2547 insertion point verified | Source read: L2547 is SignalOrNameMatches guard continue line; T2 guard is inserted after it | PASS |
| No phantom work | All described changes (leaderName guard + B134Tests.cs MODIFY) present in plan §H | PASS |
| DeriveLeaderBracketIndex NOT modified | Ticket 2 §Root Cause: "SignalOrNameMatches is NOT modified." §Files Changed: only CopyEngine.cs and B134Tests.cs. Plan §H: "Files NOT touched: ...DeriveLeaderBracketIndex..." | PASS |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Ticket Section | Check | Result |
|------|---------------|-------|--------|
| JS-021 (no lock()) | §7-Scan Checklist SCAN-01 | "Pure boolean predicate; no state mutation; no lock." | PASS |
| JS-001 (no throw in hot path) | §7-Scan Checklist SCAN-02 | "New line is a continue guard; 0 throw statements introduced." | PASS |
| JS-002 (Order? null contract) | §7-Scan Checklist SCAN-05 | "return null; final statement; not removed or altered by T2 guard insertion." | PASS |
| JS-033 (no async void) | N/A | Synchronous method; no async/await. N/A. | PASS |
| JS-036 (no new byte[] in hot path) | N/A | Predicate-only; no allocation. N/A. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Stage | CYC | Formula | Result |
|-------|-----|---------|--------|
| Post-T1 only | 7 | foreach(1) + SignalOrNameMatches(1) + state-filter(3) + isStop(1) + type-match(1) = 7 | PASS |
| Post-T1+T2 (committed) | 8 | +1 leaderName exact guard = 8 | AT LIMIT; PASS |
| SignalOrNameMatches (unchanged) | 3 | unchanged | PASS |

Combined ceiling CYC=8 is AT LIMIT; <= 8 PASS.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Check | Ticket Section | Result |
|-------|---------------|--------|
| Account.Cancel() safety | N/A for T2; predicate guard in FindFollowerBracketOrder; no Cancel calls | PASS |
| OrderState.Submitted confirmed non-terminal | Covered by T1; T2 depends on T1 applied first. Dependency stated: "T1 must be applied before T2" | PASS |
| No async/await in lifecycle | N/A | PASS |
| SignalOrNameMatches not modified | §Exact Edit: "SignalOrNameMatches is NOT modified. CYC stays at 3." | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| Method / Class | [Fact] Name | Present? |
|---------------|-------------|---------|
| FindFollowerBracketOrder | T2_Target3_ReturnsTarget3_NotTarget1 | PASS |
| FindFollowerBracketOrder | T2_Target1_ReturnsTarget1_WhenRequested | PASS |
| FindFollowerBracketOrder | T2_NullLeaderName_ReturnsFirstMatch_BackwardCompat | PASS |
| B134Ticket2Tests total | 3 [Fact] (minimum 3) | PASS |
| Regression guard B129x13, B130x8, B131x7, B132x6, B133x10 | §7-Scan SCAN-07 | PASS |
| "DO NOT MODIFY existing test files" | NOT EXPLICITLY STATED in Ticket 2 text. §Files Changed lists B134Tests.cs MODIFY only, but the explicit prohibition phrase is absent from the ticket body. | **FAIL** |

**Test Coverage: FAIL**

Violation: Same as T1. The explicit directive "DO NOT MODIFY existing test files" (or equivalent prohibitory language) is absent from Ticket 2 body. The §Files Changed -- Ticket 2 table lists only B134Tests.cs (MODIFY) and CopyEngine.cs (MODIFY) -- the constraint is implied but not stated. Ticket reference: `## TICKET 2 -- DW-B145 > ### New Tests -- Ticket 2` and `### Files Changed -- Ticket 2`.

---

### Scan Checklist Presence

| Scan | Rule | Present? | Pass Criterion Present? |
|------|------|----------|------------------------|
| SCAN-01 | JS-021: no lock() | YES | YES -- "leaderName guard is a pure boolean predicate; no state mutation; no lock." |
| SCAN-02 | JS-001: no throw | YES | YES -- "new line is a continue guard; 0 throw statements." |
| SCAN-03 | ASCII-only | YES | YES -- "0 non-ASCII characters in new/modified lines." |
| SCAN-04 | CYC <= 8 | YES | YES -- "Combined post-T1+T2 CYC = 8. AT LIMIT; <= 8 PASS." |
| SCAN-05 | JS-002: null contract | YES | YES -- "return null; is the final statement; not removed or altered by T2." |
| SCAN-06 | Build 0 errors | YES | YES -- "Exit code 0. 0 build errors. 0 new warnings." |
| SCAN-07 | Prior tests pass | YES | YES -- "0 regressions. B134Ticket2Tests: 3 PASS." |

**Scan Checklist: PASS** (all 7 scans present with pass criteria)

---

### File Routing

| File Path | Correct Wave Workspace? |
|-----------|------------------------|
| `src/PropTraderTools/CopyEngine.cs` | YES -- c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |
| `src/PropTraderTools/Tests/B134Tests.cs` | YES -- c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |

**File Routing: PASS**

---

### T2 VERDICT: TICKET_REVIEW_FAIL

**Violation**: "DO NOT MODIFY existing test files" not explicitly stated in Ticket 2 body. Ticket reference: `## TICKET 2 -- DW-B145 > ### New Tests -- Ticket 2` and `### Files Changed -- Ticket 2`.

---

## Violation Summary

| # | Ticket | Category | Rule / Mandate | Ticket Section | Violation |
|---|--------|----------|----------------|----------------|-----------|
| 1 | T1 | Test Coverage | Explicit engineer directive (PTT protocol: test files protection) | `### New Tests -- Ticket 1` and `### Files Changed -- Ticket 1` | "DO NOT MODIFY existing test files" not present in ticket body. Constraint implied by omission from §Files Changed table but NOT stated as a directive the engineer can act on. |
| 2 | T2 | Test Coverage | Explicit engineer directive (PTT protocol: test files protection) | `### New Tests -- Ticket 2` and `### Files Changed -- Ticket 2` | Same as V1 -- absence of the explicit "DO NOT MODIFY existing test files" prohibitory statement in Ticket 2 body. |

---

## Checks That PASSED (Both Tickets)

| Check | T1 | T2 |
|-------|----|----|
| Traceability | PASS | PASS |
| JS-021 (no lock) | PASS | PASS |
| JS-001 (no throw) | PASS | PASS |
| JS-002 (null contract) | PASS | PASS |
| JS-033 N/A | PASS | PASS |
| JS-036 N/A | PASS | PASS |
| CYC Pre-Check | PASS | PASS |
| NT8 Constraints | PASS | PASS |
| SCAN-01 through SCAN-07 present | PASS | PASS |
| File Routing | PASS | PASS |
| Exact BEFORE/AFTER code blocks | PASS | PASS |
| .csproj insertion line + text | PASS | PASS |
| 8 named [Fact] total (5+3) | PASS | PASS |
| Regression guard counts stated | PASS | PASS |
| Source line refs verified against live source | PASS | PASS |
| Scope lock (FindFollowerBracketOrder only) | PASS | PASS |

---

## Required Architect Fixes (Minimal)

The following additions to 04-tickets.md will clear both violations without any logic changes:

**In `### New Tests -- Ticket 1`** (or `### Files Changed -- Ticket 1`), add a one-line explicit directive:

```
> **DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs). B134Tests.cs is the only new/modified test file in this block.**
```

**In `### New Tests -- Ticket 2`** (or `### Files Changed -- Ticket 2`), add the identical directive.

---

## Overall: TICKET_REVIEW_FAIL

**Reason**: Violations V1 and V2 -- "DO NOT MODIFY existing test files" explicit engineer directive absent from both Ticket 1 and Ticket 2 bodies.

**Scope of failure**: Test Coverage check only. All JS pre-checks, CYC pre-checks, NT8 constraints, scan checklists, file routing, traceability, and source line verification PASS.

**Fix required**: Architect adds one explicit prohibitory sentence to each ticket's `### New Tests` or `### Files Changed` section. No logic changes, no code block changes, no structural changes to tickets.

**Next action**: Return to ptt-architect for the two-line addition. Re-review after fix before spawning ptt-engineer.

---

*Reviewed by ptt-ticket-reviewer, B134 Phase 3.5.*

---

## Re-Review Cycle 2

**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Re-Review -- Repair Cycle 2)
**Input**: docs/brain/B134/04-tickets.md (architect-repaired version)
**Prior violations**: V1 (T1 "DO NOT MODIFY" absent), V2 (T2 "DO NOT MODIFY" absent)

---

### V1 Resolution Check -- Ticket 1

**Required**: Explicit sentence prohibiting modification of existing test files.
**Location confirmed**: `## TICKET 1 -- DW-B144 > ### New Tests -- Ticket 1` (line 170):
> **DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs). Only create new file Tests/B134Tests.cs.**

**V1: RESOLVED** ✅

---

### V2 Resolution Check -- Ticket 2

**Required**: Identical sentence in Ticket 2.
**Location confirmed**: `## TICKET 2 -- DW-B145 > ### New Tests -- Ticket 2` (line 402):
> **DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs). Only create new file Tests/B134Tests.cs.**

**V2: RESOLVED** ✅

---

### Full Re-Check -- Ticket 1 (DW-B144)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-B144 cited; maps to plan §C + §B.2; no phantom work; all changes in plan §H |
| JS-021 (no lock) | PASS | Pure predicate extension; no state mutation; no lock() |
| JS-001 (no throw) | PASS | Predicate-only method; 0 throw statements introduced |
| JS-002 (null contract) | PASS | `return null;` at closing line preserved; `Order?` type unchanged |
| JS-033 N/A | PASS | Synchronous method; no async/await |
| JS-036 N/A | PASS | No heap allocation |
| CYC Pre-Check | PASS | Pre=6, post-T1=7, post-T1+T2=8; AT JS ceiling (≤8); formula stated |
| NT8 Constraints | PASS | Cancel() safety cited from NT8_FULL_REFERENCE.md L2408-2452; Submitted confirmed non-terminal (L3357-3374) |
| Test Coverage | PASS | 5 [Fact] present; "DO NOT MODIFY" directive present (V1 resolved); xUnit only; regression guard stated |
| SCAN-01 (lock) | PASS | Present with pass criterion |
| SCAN-02 (throw) | PASS | Present with pass criterion |
| SCAN-03 (ASCII) | PASS | Present with pass criterion |
| SCAN-04 (CYC) | PASS | Present with pass criterion |
| SCAN-05 (null) | PASS | Present with pass criterion |
| SCAN-06 (build) | PASS | Present with pass criterion |
| SCAN-07 (prior tests) | PASS | Present with pass criterion |
| File Routing | PASS | All 3 paths under c:\WSGTA\universal-or-strategy\src\PropTraderTools\ |
| Spec Coverage | PASS | DW-B144 covered exactly once |
| Scope Lock | PASS | Edit bounded to FindFollowerBracketOrder list overload; no other methods or files touched |

**T1 VERDICT: TICKET_REVIEW_PASS**

---

### Full Re-Check -- Ticket 2 (DW-B145)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-B145 cited; maps to plan §D + §B.3; SignalOrNameMatches not modified confirmed; no phantom work |
| JS-021 (no lock) | PASS | Pure boolean predicate `leaderName != null && order.Name != leaderName`; no state mutation |
| JS-001 (no throw) | PASS | New line is a `continue` guard; 0 throw statements introduced |
| JS-002 (null contract) | PASS | `return null;` final statement; not affected by T2 guard insertion |
| JS-033 N/A | PASS | Synchronous method |
| JS-036 N/A | PASS | No allocation |
| CYC Pre-Check | PASS | Post-T1=7, post-T1+T2=8 AT LIMIT; SignalOrNameMatches=3 (unchanged); all ≤8 |
| NT8 Constraints | PASS | No Cancel() in FindFollowerBracketOrder from T2; SignalOrNameMatches not modified |
| Test Coverage | PASS | 3 [Fact] present; "DO NOT MODIFY" directive present (V2 resolved); xUnit only; regression guard in SCAN-07 |
| SCAN-01 (lock) | PASS | Present with pass criterion |
| SCAN-02 (throw) | PASS | Present with pass criterion |
| SCAN-03 (ASCII) | PASS | Present with pass criterion |
| SCAN-04 (CYC) | PASS | Present with pass criterion |
| SCAN-05 (null) | PASS | Present with pass criterion |
| SCAN-06 (build) | PASS | Present with pass criterion |
| SCAN-07 (prior tests) | PASS | Present with pass criterion |
| File Routing | PASS | Both paths under c:\WSGTA\universal-or-strategy\src\PropTraderTools\; .csproj correctly omitted (registered in T1) |
| Spec Coverage | PASS | DW-B145 covered exactly once |
| Scope Lock | PASS | Edit bounded to FindFollowerBracketOrder list overload; SignalOrNameMatches/SyncFollowerBracket/DeriveLeaderBracketIndex/all B129-B133 test files untouched |

**T2 VERDICT: TICKET_REVIEW_PASS**

---

### Aggregate Checks

| Check | Result |
|-------|--------|
| DW-B144 + DW-B145 both covered (no gaps) | PASS |
| No spec requirement covered twice | PASS |
| Combined test count: 8 [Fact] (5 T1 + 3 T2) | PASS |
| CYC ceiling across all modified methods ≤ 8 | PASS |
| Engineer execution order stated (steps 1-7) | PASS |
| Prior block regression guard table present | PASS |
| ptt-sync-and-verify.ps1 step included | PASS |
| F5 NinjaTrader compile step included | PASS |
| No new violations introduced | PASS |

---

## Overall Re-Review Cycle 2: TICKET_REVIEW_PASS

**Both V1 and V2 violations resolved. No new violations found. All checks pass across both tickets.**

*Re-reviewed by ptt-ticket-reviewer, B134 Phase 3.5 Repair Cycle 2.*

TICKET_REVIEW_PASS
