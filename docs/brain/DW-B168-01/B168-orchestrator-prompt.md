# B168 Pipeline — ptt-orchestrator Prompt

Paste this entire prompt into a new ptt-orchestrator session.

---

You are the ptt-orchestrator for block B168.

## Mission Brief

Bug: DW-B168-01 COLLATERAL-STORM
Root cause: ResubmitCollateralLegs in CopyEngine.cs cancels+resubmits collateral
PTT-STP-Drag-N orders even when those legs have their own HandleAtmStopSync in
flight (leader Stop-N in ChangeSubmitted/ChangePending state), producing a
cancel storm that loses follower stops on simultaneous 3-stop drags.

Design spec: docs/brain/DW-B168-01/DW-B168-01-design.md
Source file: src/PropTraderTools/CopyEngine.cs
Test file: src/PropTraderTools/CopyEngineTests.cs

## Mandatory 7-Step Pipeline

Execute ALL phases in order. No phase may be skipped, abbreviated, or
combined with another. Gate failures require rework of that phase before
advancing.

PHASE 1: ptt-architect
  Input: docs/brain/DW-B168-01/DW-B168-01-design.md + CopyEngine.cs context
  Output: docs/brain/B168/02-architecture-plan.md
  Contract:
    - Re-state root cause in one sentence
    - Specify EXACT line numbers in CopyEngine.cs for each change
    - List ALL files touched and line-count delta
    - Include full C# for IsLeaderLegBeingChanged (private static, CYC=2)
    - Include full C# for modified ResubmitCollateralLegs (CYC=3)
    - Include test seam: IsLeaderLegBeingChangedTestable
    - Include 4 [Fact] test signatures
    - CYC audit: both methods < 8, total delta +3 McCabe
    - No other methods touched
  Status marker: PLAN_COMPLETE

PHASE 2: ptt-plan-reviewer
  Input: docs/brain/B168/02-architecture-plan.md
  Output: docs/brain/B168/02-plan-review.md
  Gate: REVIEW_PASS required to advance
  Checks:
    - Zero-regression proof for single-stop drag path
    - CYC numbers correct and within budget
    - IsLeaderLegBeingChanged returns false for Working/Accepted orders (confirmed)
    - Stop3 "last handler" case explicitly addressed (acceptable one clean resubmit)
    - No lock() anywhere
    - No Unicode in string literals
    - Hard-link sync (ptt-sync.ps1) listed as post-change step
  Failure action: return to Phase 1 with specific objections

PHASE 3: ptt-architect
  Input: REVIEW_PASS from Phase 2
  Output: docs/brain/B168/04-tickets.md
  Tickets (exactly 2):

  TICKET B168-1 (src change):
    File: src/PropTraderTools/CopyEngine.cs
    Changes:
      1. Add IsLeaderLegBeingChanged private static method
         (exact lines: after FindLeaderCollateralOrder ~line 3802)
      2. Add IsLeaderLegBeingChangedTestable internal static seam
         (exact lines: after IsLeaderLegBeingChanged)
      3. Modify ResubmitCollateralLegs: insert one if/continue guard
         (exact line: after FindLeaderCollateralOrder call, before
          ResubmitOneCollateralLeg call, ~line 3956-3957)
    Acceptance: no new lock(), CYC delta confirmed, ASCII-only, no Unicode
    Sync: run powershell -File .\scripts\ptt-sync.ps1 after edit

  TICKET B168-2 (test change):
    File: src/PropTraderTools/CopyEngineTests.cs
    Changes:
      Add 4 [Fact] tests in a new DwB168Tests class or appended to
      DwB16702SlateReadyTests (at end of file):
        - IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue
        - IsLeaderLegBeingChanged_ChangePending_ReturnsTrue
        - IsLeaderLegBeingChanged_Working_ReturnsFalse
        - IsLeaderLegBeingChanged_Null_ReturnsFalse
    All use IsLeaderLegBeingChangedTestable seam added in B168-1
    All use StubOrder(name, state) helper pattern from existing tests
    Acceptance: 4 tests PASS, 0 SKIP, 0 FAIL

PHASE 3.5: ptt-ticket-reviewer
  Input: docs/brain/B168/04-tickets.md
  Output: docs/brain/B168/04-ticket-review.md
  Gate: TICKET_REVIEW_PASS required to advance
  Checks:
    - Ticket B168-1 touches ONLY the 3 listed change sites
    - No whitespace mutations outside changed lines
    - Test seam pattern matches IsPttStpDragCancellableTestable (line 4141)
    - Ticket B168-2 tests compile without NT8 runtime (stub pattern)
  Failure action: return to Phase 3 with specific objections

PHASE 4a: ptt-engineer
  Input: TICKET_REVIEW_PASS + 04-tickets.md
  Output: src .cs edits + docs/brain/B168/ticket-1-completion.md
         + docs/brain/B168/ticket-2-completion.md
  Actions:
    1. Read CopyEngine.cs lines 3790-3810 (FindLeaderCollateralOrder context)
    2. Read CopyEngine.cs lines 3939-3960 (ResubmitCollateralLegs body)
    3. Read CopyEngine.cs lines 4139-4177 (IsPttStpDragCancellableTestable pattern)
    4. Apply Ticket B168-1 changes (surgical apply_diff, not write_file)
    5. Run: powershell -File .\scripts\ptt-sync.ps1
    6. Apply Ticket B168-2 test additions
    7. Write ticket-1-completion.md and ticket-2-completion.md
  BUILD gate: no compile errors (NT8 hot-compile in NinjaTrader 8)
  Status: BUILD_PASS required in completion docs

PHASE 4b: ptt-verifier
  Input: Phase 4a completions + CopyEngine.cs (post-edit)
  Output: docs/brain/B168/ticket-1-verification.md
         + docs/brain/B168/ticket-2-verification.md
  Verifications:
    Ticket 1:
      - grep "IsLeaderLegBeingChanged" CopyEngine.cs -> exactly 3 occurrences
        (definition + testable seam + call site)
      - grep "lock(" src/ -> 0 matches
      - Confirm no whitespace-only lines changed outside the 3 edit sites
      - Confirm CYC comment on ResubmitCollateralLegs updated to CYC=3
    Ticket 2:
      - 4 test methods present in CopyEngineTests.cs
      - All use IsLeaderLegBeingChangedTestable
      - No NT8 runtime references in test bodies
    Run: dotnet test (or NT8 hot-compile) -> 4 new PASS, 0 FAIL
  Gate: VERIFY_PASS required to advance

PHASE 5: ptt-plan-reviewer
  Input: all VERIFY_PASS artifacts
  Output: docs/brain/B168/05-final-review.md
         + docs/brain/B168/06-deferred-backlog.md
  Final review checks:
    - Diff character count < 10,000 (DIFF GUARD)
    - No new lock() anywhere in diff
    - Hard-link sync confirmed (ptt-sync.ps1 ran)
    - Test results: 4 new PASS, existing suite green
    - DW-B168-01 marked CLOSED in 06-deferred-backlog.md
    - Any newly discovered issues listed as new DW items
    - SIM gate note: B168 requires Director SIM test
      (drag all 3 stops simultaneously, confirm 0 missing stops on followers)
  Status: FINAL_PASS

## Important Context

- CopyEngine.cs is 499,694 bytes. Use apply_diff for surgical edits.
  NEVER use write_file on CopyEngine.cs.
- Hard-link is LIVE: src/ == NT8 AddOns path. ptt-sync.ps1 re-validates.
- IsLeaderLegBeingChanged is private static: testable only via seam.
  Pattern to follow: IsPttStpDragCancellableTestable at line 4141.
- ResubmitCollateralLegs CYC comment is on its header block.
  Update "CYC=2" to "CYC=3" in that comment. Update the DW-B168-01
  annotation inline.
- The ONLY 3 edit sites in CopyEngine.cs are:
    A) After FindLeaderCollateralOrder (~line 3802): add IsLeaderLegBeingChanged
    B) After IsLeaderLegBeingChanged: add IsLeaderLegBeingChangedTestable seam
    C) Inside ResubmitCollateralLegs loop (~line 3956): add if/continue guard
  No other lines may be modified.

## Stop. Do not begin Phase 1 until Director confirms "go".
