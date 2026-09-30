$continue | PTT-COPIER BWAVE-CYC | CopyEngine Complexity Reduction | Lane-A CONTINUATION

SITUATION:
Lane A ran a single-engineer session (wrong structure) and was stopped.
The work produced is CORRECT and COMMITTED (checkpoint: 12ec4ea0).
21 of 59 violations were reduced. 38 remain, all in CopyEngine.cs.
Lane C is FULLY COMPLETE (0 violations in Panel/Window/AddOn).
You are restarting Lane A with the correct structure: 1 engineer + 1 verifier per ticket.

ORCHESTRATOR ROLE FROM HERE:
You are ptt-orchestrator. The architect plan already exists -- do NOT re-run ptt-architect.
Your job is to spawn one engineer subtask per ticket, wait for it to complete,
then spawn one independent verifier subtask for that same ticket, wait for VERIFY_PASS,
then move to the next ticket. Sequential. No batching. No skipping.

ARCHITECT PLAN (already written -- read this before spawning any subtask):
  docs/brain/BWAVE-CYC/LaneA-02-architect-plan.md

CURRENT STATE (lizard baseline post-checkpoint):
  CopyEngine.cs: 38 methods over CCN=8 -- all listed in REMAINING TICKETS below.
  Build: PASSING. Tests: 436 pass / 22 pre-existing IL-reflection failures / 15 skips.
  The 22 failures are PRE-EXISTING -- not regressions. Accept them as baseline.

WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
BRAIN: docs/brain/BWAVE-CYC\

CODESCENE CLI (set before every cs command in every subtask):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"

==============================================================
ORCHESTRATOR EXECUTION PROTOCOL (mandatory, non-negotiable):
==============================================================

For EACH ticket T1 through T10 (in order):

  STEP 1 -- Spawn ptt-engineer subtask with the TICKET PROMPT below.
            Engineer does ONE ticket only. Engineer stops after BUILD_PASS.
            Engineer writes: docs/brain/BWAVE-CYC/LaneA-T{N}-engineer.md

  STEP 2 -- Wait for engineer subtask to complete and report BUILD_PASS.
            If engineer reports BUILD_FAIL: stop, report to Director, do not proceed.

  STEP 3 -- Spawn ptt-verifier subtask with the VERIFIER PROMPT below.
            Verifier runs ALL 7 scans independently. Verifier stops after VERIFY_PASS or VERIFY_FAIL.
            Verifier writes: docs/brain/BWAVE-CYC/LaneA-T{N}-verify.md

  STEP 4 -- Wait for verifier subtask to complete.
            If VERIFY_FAIL: stop, report exact blocker to Director, do not proceed.
            If VERIFY_PASS: proceed to next ticket.

MERGING ENGINEER AND VERIFIER INTO ONE SUBTASK IS A PROTOCOL VIOLATION.
RUNNING MULTIPLE TICKETS IN ONE ENGINEER SESSION IS A PROTOCOL VIOLATION.

==============================================================
TICKET PROMPT TEMPLATE (give this to ptt-engineer for each ticket):
==============================================================

You are ptt-engineer executing ONE ticket for BWAVE-CYC Lane-A.
Read docs/brain/BWAVE-CYC/LaneA-02-architect-plan.md -- find the section for {TICKET_ID}.
Execute ONLY that ticket. Do not touch any other methods.

TICKET: {TICKET_ID}
TARGET METHODS: {METHOD_LIST}
FILE: src/PropTraderTools/CopyEngine.cs

EXTRACTION RULES (non-negotiable):
  1. Private helpers ONLY -- no new public or internal surface
  2. Helper names are semantic (what they decide, not their position)
  3. Each extracted helper: CCN <= 4
  4. Parent method after extraction: CCN <= 8
  5. Behaviour IDENTICAL -- no logic changes, no reordering, no early returns added
  6. One new [Fact] test per extracted helper -- add to src/PropTraderTools/Tests/CopyEngineTests.cs
  7. Read docs/standards/NT8_COMPILER_RULES.md before any .cs edit

AFTER EXTRACTION:
  Run: dotnet build
  Run: $env:CS_ACCESS_TOKEN="pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"; cs delta
  Write docs/brain/BWAVE-CYC/LaneA-{TICKET_ID}-engineer.md with:
    - Methods modified + CCN before/after for each
    - Helpers extracted + their names
    - BUILD_PASS confirmation
    - cs delta score result
  Output exactly: "BUILD_PASS -- {TICKET_ID} complete"
  Then STOP. Do not run any scans. Do not proceed to next ticket.

==============================================================
VERIFIER PROMPT TEMPLATE (give this to ptt-verifier for each ticket):
==============================================================

You are ptt-verifier for BWAVE-CYC Lane-A {TICKET_ID}. Run ALL 7 scans independently.
Do NOT trust the engineer report. Read the modified methods yourself.

READ: docs/brain/BWAVE-CYC/LaneA-{TICKET_ID}-engineer.md (to know which methods were changed)
FILE: src/PropTraderTools/CopyEngine.cs

7 MANDATORY SCANS (run all, report each result):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> must be 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> must be 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new vs pre-wave baseline
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new vs pre-wave baseline
  SCAN-05a: lizard src/PropTraderTools/CopyEngine.cs --CCN 8                        --> CCN <= 8 for ALL methods modified in {TICKET_ID}
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta                             --> Code Health score does NOT decrease
  SCAN-06: dotnet build                                                              --> 0 errors, 0 warnings
  SCAN-07: dotnet test                                                               --> 0 NEW failures (22 pre-existing IL-reflection failures are ACCEPTED baseline)

Write docs/brain/BWAVE-CYC/LaneA-{TICKET_ID}-verify.md with scan results.
Output exactly: "VERIFY_PASS -- {TICKET_ID}" or "VERIFY_FAIL -- {TICKET_ID}: [exact blocker]"
Then STOP.

==============================================================
REMAINING TICKETS (38 violations -- 10 tickets):
==============================================================

T1-CONT -- OnOrderUpdate (CCN=23, L1314-1429) [LANE B ticket -- SKIP -- belongs to Lane B]
  NOTE: OnOrderUpdate is assigned to Lane B per wave plan. Do NOT execute here.

LANE A REMAINING TICKETS:

TA-R1 -- ArmPendingBe + TryFireImmediateBeIfAlreadyAtLevel + IsPendingBeTriggerMet + OnPendingBeAccountUpdate
  OnPendingBeAccountUpdate   L5629-5654  CCN=18
  ArmPendingBe               L5445-5480  CCN=13
  TryFireImmediateBeIfAlreadyAtLevel L5485-5513 CCN=19 (new helper from T1 extraction -- needs further reduction)
  IsPendingBeTriggerMet      L5660-5673  CCN=18 (new helper from T1 extraction -- needs further reduction)
  See LaneA-02-architect-plan.md T1 section for exact extraction design.

TA-R2 -- SnapshotBeTargets + IsEligibleBeTargetOrder + IsLeaderTargetOrder + OnTrailBeAccountUpdate
  SnapshotBeTargets          L5043-5066  CCN=9
  IsEligibleBeTargetOrder    L5070-5085  CCN=10  (new helper from T2 -- needs further reduction)
  IsLeaderTargetOrder        L5022-5036  CCN=9   (new helper from T6 -- needs further reduction)
  OnTrailBeAccountUpdate     L5594-5621  CCN=9
  See LaneA-02-architect-plan.md T2 and T6 sections.

TA-R3 -- SyncFollowerBracket + CaptureLinkedTargetPrice + CaptureOtherLegTargetPrices
  SyncFollowerBracket        L2277-2345  CCN=16
  CaptureLinkedTargetPrice   L2460-2478  CCN=9
  CaptureOtherLegTargetPrices L2494-2514 CCN=9
  See LaneA-02-architect-plan.md T5 section.

TA-R4 -- TryFireFollowerBeRetry + TryEvictFollowerBeSlot + CancelPttDragOrphansForAccount
  TryFireFollowerBeRetry     L1481-1515  CCN=15
  TryEvictFollowerBeSlot     L1540-1572  CCN=13
  CancelPttDragOrphansForAccount L1604-1624 CCN=10
  See LaneA-02-architect-plan.md T3 and T8 sections.

TA-R5 -- IsReArmedAtmBracketCleanupRequired + ReplaceFollowerCopyOnAtmCancel + TryFindRuleAndFollowerIndex + TryReplacePttBeBrackets
  IsReArmedAtmBracketCleanupRequired L3831-3851 CCN=14  (new helper from T4 -- needs further reduction)
  ReplaceFollowerCopyOnAtmCancel     L3600-3635 CCN=9
  TryFindRuleAndFollowerIndex        L3639-3663 CCN=9   (new helper from T4)
  TryReplacePttBeBrackets            L3706-3756 CCN=10
  See LaneA-02-architect-plan.md T4 section.

TA-R6 -- TryFirePositionState + FindFollowerBracketOrder + MatchesLeaderName + HandleBracketChange + CreateFollowerReplacementStop
  TryFirePositionState       L3503-3539  CCN=11
  FindFollowerBracketOrder   L3198-3231  CCN=11
  MatchesLeaderName          L3253-3270  CCN=11
  HandleBracketChange        L3114-3147  CCN=9
  CreateFollowerReplacementStop L3048-3093 CCN=9
  See LaneA-02-architect-plan.md T7 section.

TA-R7 -- FlattenOneAccount + MirrorClose + BuildUpdatedMultipliers
  FlattenOneAccount          L4375-4429  CCN=11
  MirrorClose                L1936-1975  CCN=9
  BuildUpdatedMultipliers    L1283-1299  CCN=9
  See LaneA-02-architect-plan.md T6 section.

TA-R8 -- DispatchCopy + SyncAtmFollowerBracket + IsExitSignalName
  DispatchCopy               L2080-2197  CCN=13  [OVERLAPS Lane B T2 -- orchestrator must coordinate]
  SyncAtmFollowerBracket     L2395-2445  CCN=11  [OVERLAPS Lane B T4]
  IsExitSignalName           L2006-2031  CCN=10  [OVERLAPS Lane B T4]
  *** HOLD TA-R8 -- these three methods are assigned to Lane B. Do NOT execute here.
  *** Notify Director that TA-R8 is deferred to Lane B.

TA-R9 -- CancelQxBrackets (overload 2) + SubmitBeStop + IsFollowerAccount
  CancelQxBrackets           L956-997    CCN=11
  SubmitBeStop               L1085-1140  CCN=10
  IsFollowerAccount          L758-777    CCN=9
  See LaneA-02-architect-plan.md T8 section.

TA-R10 -- DtoToRule + RuleToTo
  DtoToRule                  L5762-5825  CCN=11
  RuleToDto                  L5724-5759  CCN=9
  See LaneA-02-architect-plan.md T8 section.

==============================================================
COORDINATION NOTE FOR ORCHESTRATOR:
==============================================================
Lane B (not yet started) owns: OnOrderUpdate, DispatchCopy, SyncAtmFollowerBracket, IsExitSignalName,
TryFireFollowerBeRetry, TryEvictFollowerBeSlot, TryHandleEntryDrag, DtoToRule.
Do NOT execute TA-R8 -- those methods are Lane B's. Execute TA-R1 through TA-R7, TA-R9, TA-R10 only.
After all Lane A tickets have VERIFY_PASS, report LANE_A_FINAL_PASS to Director.
Director will then paste the Lane B prompt.

FINAL_PASS CRITERIA FOR LANE A:
  All TA-R1 through TA-R7, TA-R9, TA-R10 tickets: VERIFY_PASS
  lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -- 0 warnings for all Lane A methods
  (Lane B methods will still show warnings until Lane B runs -- that is expected)
  dotnet build: 0 errors, 0 warnings
  dotnet test: 0 new failures vs checkpoint baseline (436 pass, 22 pre-existing accepted)
  cs delta: Code Health score improved vs checkpoint
  powershell -File scripts\verify_links.ps1 -Fix (run once after all tickets complete)
  Write docs/brain/BWAVE-CYC/LaneA-final-report.md
  Output: "LANE_A_FINAL_PASS"
