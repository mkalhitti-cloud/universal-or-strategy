# B133 LaneB -- Ticket Review
# DW-B143 FindFollowerBracketOrder Accepted-State Fix

Reviewer: ptt-ticket-reviewer
Phase: 3.5 (Ticket Review)
Lane: B
Epic: B133
Input: docs/brain/B133/LaneB-04-tickets.md
Plan:  docs/brain/B133/LaneB-02-architecture-plan.md
Standards: docs/standards/jane-street/RULES_CATALOG.md

---

## Ticket 1 -- DW-B143 FindFollowerBracketOrder Accepted-state fix + B133LaneBTests

### TR-01 -- Spec Req IDs

PASS
Both required spec requirement IDs are present in the ticket header:
  "Spec Req IDs: DW-B143 (P1), B133-LANEB-TEST (required)"
Both are traceable to DW-B143 (the confirmed P1 defect) and the lane test mandate.

### TR-02 -- Files Modified

PASS
Exactly 2 files listed in the Files Modified table:
  1. src/PropTraderTools/CopyEngine.cs  -- MODIFY L2535 state filter
  2. src/PropTraderTools/Tests/B133Tests.cs  -- CREATE or MODIFY B133LaneBTests
Ticket line 26 explicitly states: "No other files are touched. Exactly 2 files are in scope."

### TR-03 -- Exact Diff Present

PASS
Section 2a contains the exact before/after diff:
  BEFORE: if (order.OrderState != OrderState.Working)\n    continue;
  AFTER:  if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)\n    continue;
The net change is correctly described as the addition of "&& order.OrderState != OrderState.Accepted".

### TR-04 -- 7-Scan Checklist Present

PASS
Section 6 contains a full table for SCAN-01 through SCAN-07 with exact shell commands and
expected results. All 7 scans are present and carry verbatim-executable commands.

### TR-05 -- CYC Analysis Correct

FAIL
The ticket's CYC analysis block (Section 2a) states only the after-fix figure:
  "CYC analysis after fix: foreach (1) + ... = 6 / Ceiling: 8. Result: PASS (JS-066)."
The before-fix CYC of 5 is NOT stated in the ticket. The check requires both values:
  current=5, after fix=6, ceiling=8, PASS stated.
Without the before value the engineer cannot independently verify the delta and the
verifier cannot confirm the before-state without consulting the arch plan.

Required fix: Add "CYC before fix: 5" to the CYC analysis block so it reads:
  "CYC before fix: 5
   CYC after fix: foreach (1) + SignalOrNameMatches guard (1) + state filter (2) + isStop (1) + OrderType match (1) = 6
   Ceiling: 8. Result: PASS (JS-066)."

### TR-06 -- 5 [Fact] Method Names Specified

PASS
All 5 exact [Fact] method names are listed in Section 2b and each covers exactly one
OrderState variant:
  1. FindFollowerBracketOrder_AcceptedState_IsFound        (Accepted -- primary DW-B143 test)
  2. FindFollowerBracketOrder_SubmittedState_IsNotFound    (Submitted -- excluded state)
  3. FindFollowerBracketOrder_FilledState_IsNotFound       (Filled -- terminal state)
  4. FindFollowerBracketOrder_WorkingState_IsFound         (Working -- regression guard)
  5. FindFollowerBracketOrder_CancelledState_IsNotFound    (Cancelled -- terminal state)

### TR-07 -- Test Seam Used Correctly

PASS
Section 2b states: "Test seam: FindFollowerBracketOrderTestable(account, fromEntrySignal,
isStop, leaderName) at L2559-2564 in CopyEngine.cs -- same seam used by all prior B-block
test classes."
Each of the 5 test specifications in Section 4 calls FindFollowerBracketOrderTestable,
not the private FindFollowerBracketOrder directly.

### TR-08 -- Regression Table Present

PASS
Section 5 regression table:
  B133LaneATests  5  (B133 LaneA)
  B132Tests       5  (B132 block)
  B131Tests       7  (B131 block)
  B130Tests       8  (B130 block)
  B129Tests      13  (B129 block)
  Total prior    38  (all must pass)
Count is >= 38. New B133LaneBTests (5) and total 43 are also stated.

### TR-09 -- JS Constraints Cited

PASS
Section 3 table cites all required constraints with rule IDs:
  JS-021 (P0) -- No lock() in src/
  JS-001 (P0) -- No throw new in hot paths
  JS-002 (P0) -- No return null introduced (pre-existing unchanged)
  JS-033 (P0) -- No async void
  JS-066     -- CYC <= 8 (FindFollowerBracketOrder after fix = 6)
  JS-066     -- ASCII-only identifiers and literals
  JS-051     -- xUnit [Fact] only, no NUnit or MSTest

### TR-10 -- Accepted-State Fix Scoped; Submitted Excluded with Rationale

PASS
Test 2 (FindFollowerBracketOrder_SubmittedState_IsNotFound) explicitly states:
  "Submitted is explicitly excluded from the fix because NT8 Account.Cancel() on Submitted
   is unreliable. The filter must continue to skip Submitted orders."
The arch plan Section 3 provides the detailed NT8 Cancel() reliability rationale.
The ticket references the rationale in the test purpose and in the acceptance criteria.

### TR-11 -- No Scope Creep

PASS
Files Modified table lists exactly 2 files. Ticket line 26 confirms no other files are
touched. No additional methods, classes, or files are introduced beyond the described fix
and the B133LaneBTests class.

### TR-12 -- Acceptance Criteria Present and Complete

PASS
Section 7 Acceptance Criteria is present and includes:
  - Build 0 errors, 0 warnings (SCAN-07 gate)
  - All 5 new [Fact] tests pass by name
  - All 38 regression tests pass
  - SCAN-01 through SCAN-07 all return 0 violations
All required acceptance gates are present.

### TR-13 -- No DW- Items Created

PASS
The ticket contains no DW- items. Section 8 Compliance Confirmation confirms N/A for
CreateOrder and DateTime.UtcNow. All 4 NT8 facts were pre-confirmed in the arch plan
(Accepted cancel safety, Submitted exclusion, seam at L2559, IsWorkingBracket precedent).

---

## Overall: TICKET_REVIEW_FAIL

**Failing check**: TR-05

**Violation summary**:

TR-05 -- CYC Analysis Correct: FAIL
  Location: LaneB-04-tickets.md, Section 2a "CYC analysis after fix" block
  Missing: The before-fix CYC value of 5 is not stated in the ticket.
  The ticket writes only "CYC analysis after fix: ... = 6 / Ceiling: 8. Result: PASS."
  Required text to add (exact wording):
    "CYC before fix: 5"
  Full corrected block:
    CYC before fix: 5
    CYC after fix: foreach (1) + SignalOrNameMatches guard (1) + state filter (2) + isStop (1) + OrderType match (1) = 6
    Ceiling: 8. Result: PASS (JS-066).
  Impact: Without the before value, the engineer cannot independently verify the delta
  and the verifier (Phase 4b) cannot confirm pre-state compliance without leaving the ticket.

**Action required**: Architect updates LaneB-04-tickets.md Section 2a CYC block to add
"CYC before fix: 5" before re-submitting to Phase 3.5 for re-review.

All other 12 checks (TR-01 through TR-04, TR-06 through TR-13) passed.

---

## REVIEW CYCLE 2

Reviewer: ptt-ticket-reviewer
Cycle: 2
Trigger: TR-05 failed in Cycle 1 -- architect added "CYC before fix: 5" to Section 2a
Input: docs/brain/B133/LaneB-04-tickets.md (updated)

### Re-check scope

Only TR-05 failed in Cycle 1. Cycle 2 re-checks TR-05 only, then confirms no regression
across all other checks (TR-01..TR-04, TR-06..TR-13) from the one-line addition.

### TR-05 -- CYC Analysis Correct (Cycle 2)

PASS
The updated LaneB-04-tickets.md Section 2a now contains:
  "CYC analysis:
   - CYC before fix: 5
   - CYC after fix: 6 (ceiling 8) -- PASS (JS-066)
   - foreach (1) + SignalOrNameMatches guard (1) + state filter (2) + isStop (1) + OrderType match (1) = 6"

Both required values are now present:
  CYC before fix: 5  -- PRESENT (was missing in Cycle 1)
  CYC after fix:  6, ceiling 8, PASS (JS-066)  -- PRESENT (unchanged)
The breakdown confirms 6 decision points, within the ceiling of 8.
The verifier now has an unambiguous before-state anchor without leaving the ticket.
TR-05 violation from Cycle 1 is resolved.

### Cycle 2 Full Check Table

| Check | Description | Cycle 1 | Cycle 2 | Delta |
|-------|-------------|---------|---------|-------|
| TR-01 | Spec Req IDs present | PASS | PASS | none |
| TR-02 | Files Modified (exactly 2) | PASS | PASS | none |
| TR-03 | Exact before/after diff present | PASS | PASS | none |
| TR-04 | 7-Scan Checklist present (SCAN-01..07) | PASS | PASS | none |
| TR-05 | CYC analysis: before=5, after=6, ceiling 8, PASS | FAIL | **PASS** | **FIXED** |
| TR-06 | 5 [Fact] method names specified | PASS | PASS | none |
| TR-07 | Test seam used correctly | PASS | PASS | none |
| TR-08 | Regression table present (38 prior + 5 new = 43) | PASS | PASS | none |
| TR-09 | JS constraints cited (JS-001/002/021/033/051/066) | PASS | PASS | none |
| TR-10 | Accepted-state fix scoped; Submitted excluded with rationale | PASS | PASS | none |
| TR-11 | No scope creep | PASS | PASS | none |
| TR-12 | Acceptance criteria present and complete | PASS | PASS | none |
| TR-13 | No DW- items created | PASS | PASS | none |

### Violations (Cycle 2)

None. The single Cycle 1 violation (TR-05 missing "CYC before fix: 5") has been resolved
by the architect's one-line addition. No new violations introduced.

### Regression Confirmation

The only change from Cycle 1 to Cycle 2 is the addition of the line
  "- CYC before fix: 5"
to the CYC analysis block in Section 2a of the ticket. This is a purely additive,
documentation-only change. No structural sections were altered:
  - Section 1 (Files Modified): unchanged
  - Section 2a (Exact Diff): diff block unchanged; CYC breakdown line count + 1 only
  - Section 2b (Test Signatures): unchanged
  - Section 3 (JS Rule Constraints): unchanged
  - Section 4 (xUnit Test Specifications): unchanged
  - Section 5 (Regression Suite): unchanged
  - Section 6 (7-Scan Checklist): unchanged
  - Section 7 (Acceptance Criteria): unchanged
  - Section 8 (Compliance Confirmation): unchanged
All 12 previously-passing checks (TR-01..TR-04, TR-06..TR-13) confirmed PASS. No regression.

## Overall: TICKET_REVIEW_PASS

All 13 checks pass in Cycle 2. No violations remain. The engineer may proceed.
