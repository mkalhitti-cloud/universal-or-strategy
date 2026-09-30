$start-lane | PTT-COPIER BWAVE-CYC | CopyEngine Dispatch/Entry/BE Complexity Reduction | Lane-B v3

PREREQUISITE GATE: Confirm before starting.
  docs/brain/BWAVE-CYC/LaneA-final-report.md must exist and contain LANE_A_FINAL_PASS.
  Confirmed: LANE_A_FINAL_PASS at commit 68a1c1c4.

SITUATION:
Lane A is FINAL_PASS. Lane C is complete.
Lane B owns the dispatch/entry/BE cluster of CopyEngine.cs.
All line numbers in this prompt are CURRENT as of commit 68a1c1c4 -- verified by lizard.

Current lizard state (post-Lane-A):
  Build: PASSING.
  CopyEngine.cs warnings remaining: 42 (all Lane B scope).

WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
BRAIN: docs/brain/BWAVE-CYC\

CODESCENE CLI (set before every cs command in every subtask):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"

GATE — COMPLEXITY RULE (read carefully, agent was previously confused on this):
  PASS/FAIL GATE  = lizard CCN <= 8 on every method. This is the ONLY hard gate.
  TREND CHECK     = CodeScene cs delta score must NOT decrease vs pre-ticket HEAD.
  There is NO minimum CodeScene score target. "8 or higher on CodeScene" is NOT a requirement.
  Do NOT over-extract chasing a CodeScene number. Stop when lizard CCN <= 8 is met.

==============================================================
PIPELINE -- 3 STAGES, SEQUENTIAL, EACH IS A HARD STOP
==============================================================

STAGE 1 -- ptt-orchestrator (this session)
  Read this full prompt.
  Record current CopyEngine.cs CodeScene score: run cs check and save as Lane B start score.
  Write docs/brain/BWAVE-CYC/LaneB-01-mission-brief.md.
  Output: "STAGE 1 COMPLETE -- start score: {X} -- proceeding to ptt-architect."
  *** STOP. Do not proceed to Stage 2 in the same response. ***

STAGE 2 -- ptt-architect
  Read ALL target methods in CopyEngine.cs at the EXACT line ranges listed in TICKETS below.
  Line numbers are current as of commit 68a1c1c4. Verify each range still matches before designing.
  If any line range has shifted by more than 5 lines, note the correction in the plan.
  Write docs/brain/BWAVE-CYC/LaneB-02-architect-plan.md with the confirmed/adjusted plan.
  Output: "STAGE 2 COMPLETE -- architect plan written."
  *** STOP. Do not proceed to Stage 3 in the same response. ***

STAGE 3 -- ptt-orchestrator coordinates per-ticket execution
  For EACH ticket TB-T1 through TB-T7 (in order):

    STEP A: Spawn ptt-engineer subtask using ENGINEER PROMPT TEMPLATE below.
            Pass the specific TICKET_ID and TARGET METHODS for that ticket.
            Engineer executes ONE ticket only. Stops after BUILD_PASS.
            Engineer writes: docs/brain/BWAVE-CYC/LaneB-{TICKET_ID}-engineer.md

    STEP B: Wait for engineer to report "BUILD_PASS -- {TICKET_ID} complete".
            If BUILD_FAIL: STOP. Report exact error to Director. Do not proceed.

    STEP C: Spawn ptt-verifier subtask using VERIFIER PROMPT TEMPLATE below.
            Pass the same TICKET_ID.
            Verifier runs ALL 7 scans independently. Stops after VERIFY_PASS or VERIFY_FAIL.
            Verifier writes: docs/brain/BWAVE-CYC/LaneB-{TICKET_ID}-verify.md

    STEP D: Wait for verifier to report.
            If VERIFY_FAIL: STOP. Report exact blocker to Director. Do not proceed.
            If VERIFY_PASS: proceed to next ticket.

  MERGING ENGINEER AND VERIFIER INTO ONE SUBTASK IS A PROTOCOL VIOLATION.
  RUNNING MULTIPLE TICKETS IN ONE ENGINEER SESSION IS A PROTOCOL VIOLATION.

After all 7 tickets VERIFY_PASS:
  Run: powershell -File scripts\verify_links.ps1 -Fix
  Run: lizard src/PropTraderTools/CopyEngine.cs --CCN 8
       Confirm: 0 warnings remain for ALL Lane B methods.
  Run: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs check src/PropTraderTools/CopyEngine.cs
       Confirm: score HIGHER than Lane B start score.
  Write docs/brain/BWAVE-CYC/LaneB-final-report.md
  Output exactly: "LANE_B_FINAL_PASS"

==============================================================
ENGINEER PROMPT TEMPLATE
(use this verbatim for each ticket -- substitute {TICKET_ID} and {METHOD_LIST})
==============================================================

You are ptt-engineer executing ONE ticket for BWAVE-CYC Lane-B.
Read docs/brain/BWAVE-CYC/LaneB-02-architect-plan.md -- find the section for {TICKET_ID}.
Execute ONLY that ticket. Do not read or touch any other methods.

TICKET: {TICKET_ID}
TARGET METHODS: {METHOD_LIST}
FILE: src/PropTraderTools/CopyEngine.cs

EXTRACTION RULES (non-negotiable):
  1. Private helpers ONLY -- no new public or internal surface
  2. Helper names are semantic (what they decide, not their position)
  3. Each extracted helper: CCN <= 4
  4. Parent method after extraction: CCN <= 8
  5. Behaviour IDENTICAL -- no logic changes, no reordering, no early returns added or removed
  6. One new [Fact] test per extracted helper -- add to src/PropTraderTools/Tests/CopyEngineTests.cs
     or create a new test file src/PropTraderTools/Tests/BwaveCycLaneBTests.cs if needed
  7. Read docs/standards/NT8_COMPILER_RULES.md before any .cs edit

COMPLEXITY GATE (read carefully):
  PASS/FAIL GATE = lizard CCN <= 8 on every modified method.
  TREND CHECK    = CodeScene cs delta score must NOT decrease.
  Do NOT over-extract chasing a CodeScene number. Stop when lizard CCN <= 8 is met.

SPECIAL RULE FOR TB-T1 (OnPendingBeAccountUpdate) -- HIGHEST PRIORITY:
  CCN=32. This is the highest CCN in the file after Lane A.
  The method manages pending BE slot trigger logic across multiple followers.
  Extract by responsibility slice -- each helper must have a clear single purpose.
  Do NOT merge helper responsibilities to hit a CCN target faster.
  The 4-gate pattern (null check, eligibility, trigger, execution) must remain in order.

SPECIAL RULE FOR TB-T2 (OnOrderUpdate):
  The 4-gate sequence (Gate1=copy-enabled, Gate2=rule-match, Gate3=order-state, Gate4=dedup)
  MUST remain in identical order. Do NOT reorder, merge, or remove any gate.
  Only extract the inner per-follower dispatch body and the dedup-check logic.

AFTER EXTRACTION:
  Run: dotnet build
  If build fails: fix immediately before proceeding. Report BUILD_FAIL if unfixable.
  Run: $env:CS_ACCESS_TOKEN="pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
       cs delta
  Write docs/brain/BWAVE-CYC/LaneB-{TICKET_ID}-engineer.md with:
    - Each method: name, CCN before, CCN after
    - Each helper extracted: name, CCN
    - BUILD_PASS or BUILD_FAIL
    - cs delta output (score delta line)
  Output exactly: "BUILD_PASS -- {TICKET_ID} complete"
  Then STOP. Do not run scans. Do not proceed to next ticket.

==============================================================
VERIFIER PROMPT TEMPLATE
(use this verbatim for each ticket -- substitute {TICKET_ID})
==============================================================

You are ptt-verifier for BWAVE-CYC Lane-B {TICKET_ID}.
Run ALL 7 scans independently. Do NOT trust the engineer report.
Read the modified methods yourself from CopyEngine.cs.

READ FIRST:
  docs/brain/BWAVE-CYC/LaneB-{TICKET_ID}-engineer.md (which methods were changed)
  docs/brain/BWAVE-CYC/LaneB-02-architect-plan.md ({TICKET_ID} section -- CCN targets)

FILE: src/PropTraderTools/CopyEngine.cs

7 MANDATORY SCANS (run all 7, report each result explicitly):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances vs baseline
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances vs baseline
  SCAN-05a: lizard src/PropTraderTools/CopyEngine.cs --CCN 8
            --> CCN <= 8 for EVERY method modified in {TICKET_ID} (read lizard output explicitly)
            --> This is the ONLY hard pass/fail gate for complexity
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
            cs delta
            --> Code Health score does NOT decrease vs pre-ticket HEAD (trend check only -- no minimum target)
  SCAN-06: dotnet build                 --> 0 errors, 0 warnings
  SCAN-07: dotnet test
            --> 0 NEW failures
            --> 22 pre-existing IL-reflection failures are ACCEPTED BASELINE -- do not count as new

Write docs/brain/BWAVE-CYC/LaneB-{TICKET_ID}-verify.md with all 7 scan results and findings.
Output exactly: "VERIFY_PASS -- {TICKET_ID}" or "VERIFY_FAIL -- {TICKET_ID}: [exact blocker]"
Then STOP.

==============================================================
TICKETS -- Lane B (7 tickets) -- LINE NUMBERS CURRENT AS OF COMMIT 68a1c1c4
==============================================================

TB-T1 -- OnPendingBeAccountUpdate (CCN=32, HIGHEST PRIORITY -- REGRESSION FROM LANE A)
  Method: OnPendingBeAccountUpdate  L5480-5520  CCN=32
  Note: CCN increased from 18 to 32 after Lane A extracted ArmPendingBe -- the body that
        remained is now denser. Must be extracted before any other ticket.
  Extraction design:
    Parent CCN target: <= 7
    Helper 1: private bool IsPendingBeTriggerConditionMet(PendingBeSlot slot, double price)
              Absorbs the compound trigger-condition check:
              slot null guard, price vs trigger level comparison, slot armed state check.
              CCN target: <= 4
    Helper 2: private void ExecutePendingBeTrigger(PendingBeSlot slot, Account acc,
                Instrument instr)
              Absorbs the trigger execution body: MoveStopToBreakEven call,
              DisarmPendingBe call, StatusUpdate call.
              CCN target: <= 3
    Helper 3: private bool IsPendingBeSlotActive(PendingBeSlot slot)
              Absorbs the slot-active compound check: slot != null, slot.IsArmed,
              slot.Account name-match guard.
              CCN target: <= 3
    [Fact] tests:
      IsPendingBeTriggerConditionMet_ReturnsFalse_WhenSlotIsNull()
      IsPendingBeTriggerConditionMet_ReturnsFalse_WhenSlotNotArmed()
      IsPendingBeTriggerConditionMet_ReturnsTrue_WhenConditionMet()
      IsPendingBeSlotActive_ReturnsFalse_WhenSlotIsNull()
      IsPendingBeSlotActive_ReturnsFalse_WhenNotArmed()
      ExecutePendingBeTrigger_CallsMoveStop()

TB-T2 -- OnOrderUpdate (CCN=23, DW-B143-POSSTATE-CYC8 P0)
  Method: OnOrderUpdate  L1316-1431  CCN=23
  Extraction design:
    Parent CCN target: <= 7
    Helper 1: private bool IsDispatchTriggerState(OrderState s)
              Absorbs the OrderState compound in Gate 3:
              (Submitted || PartFilled || ChangeSubmitted).
              CCN target: <= 2
    Helper 2: private void DispatchCopyToFollowers(CopyRule rule, CopySignal signal)
              Absorbs the per-follower foreach body (lines ~1393-1427):
              the null-check continue, PassesDailyCapCheck call, and SendCopy call.
              CCN target: <= 3
    Parent after extraction:
      Gate 1: if (!_isCopyEnabled) return;
      Gate 2: foreach + rule-match + break; if (matchedRule == null) return;
      Gate 3: if (!IsDispatchTriggerState(e.OrderState)) return;
              if (!e.Order.IsMarket && !e.Order.IsLimit) return;
      Gate 4: if (IsDedup(e.Order.OrderId)) return;
      build signal, call DispatchCopyToFollowers
      Total CCN: <= 7
    [Fact] tests:
      IsDispatchTriggerState_ReturnsTrue_WhenSubmitted()
      IsDispatchTriggerState_ReturnsTrue_WhenPartFilled()
      IsDispatchTriggerState_ReturnsFalse_WhenFilled()
      DispatchCopyToFollowers_SkipsNullFollower()
      DispatchCopyToFollowers_SkipsWhenDailyCapExceeded()

TB-T3 -- OnTrailBeAccountUpdate + SubmitBeStop (CCN=9 + CCN=10)
  Methods: OnTrailBeAccountUpdate  L5445-5472  CCN=9
           SubmitBeStop            L1087-1142  CCN=10
  Note: SubmitBeStop was in Lane A (TA-R9) but was NOT reduced there (still CCN=10).
        It must be completed here.
  Extraction design:
    OnTrailBeAccountUpdate -- Parent CCN target: <= 6
      Helper 1: private bool IsTrailBeTriggerMet(TrailBeSlot slot, double price)
                Absorbs the compound trail trigger check:
                slot null, price vs trail level, slot armed state.
                CCN target: <= 3
    SubmitBeStop -- Parent CCN target: <= 5
      Helper 1: private Order? BuildBeStopOrder(Account acc, Instrument instrument,
                  double stopPx, int bufferTicks)
                Absorbs the acc.CreateOrder(StopMarket) call, null check,
                and price-validation guard (stopPx <= 0 early return).
                CCN target: <= 3
      Helper 2: private void LinkBeStopToTargets(Order beStop, List<Order> targets,
                  Instrument instrument)
                Absorbs the foreach-targets OCO-link loop: null check on target,
                instrument match guard, OCO sequence assignment, StatusUpdate call.
                CCN target: <= 3
    [Fact] tests:
      IsTrailBeTriggerMet_ReturnsFalse_WhenSlotIsNull()
      IsTrailBeTriggerMet_ReturnsFalse_WhenNotArmed()
      IsTrailBeTriggerMet_ReturnsTrue_WhenLevelReached()
      BuildBeStopOrder_ReturnsNull_WhenStopPxIsZero()
      BuildBeStopOrder_ReturnsNull_WhenCreateOrderReturnsNull()
      LinkBeStopToTargets_SkipsNullTarget()
      LinkBeStopToTargets_SkipsTargetWithWrongInstrument()

TB-T4 -- DispatchCopy (CCN=13)
  Method: DispatchCopy  L2082-2199  CCN=13
  Extraction design:
    Parent CCN target: <= 6
    Helper 1: private double ComputeFollowerLimitPrice(double leaderPrice, double tickOffset)
              Absorbs the limit-price offset computation:
              positive/negative tickOffset branch + tickSize multiplication.
              CCN target: <= 2
    Helper 2: private bool ShouldSkipFollowerDispatch(Account follower, CopyRule rule)
              Absorbs the pre-dispatch guard block: follower null, account name match
              to source (prevent self-copy), and IsEnabled check.
              CCN target: <= 3
    Helper 3: private void SubmitFollowerCopyOrder(Account acc, CopySignal signal,
                double limitPx, CopyRule rule)
              Absorbs the acc.CreateOrder + null check + acc.Submit + dedup-preload block.
              CCN target: <= 3
    [Fact] tests:
      ComputeFollowerLimitPrice_AddsOffset_WhenPositive()
      ComputeFollowerLimitPrice_SubtractsOffset_WhenNegative()
      ShouldSkipFollowerDispatch_ReturnsTrue_WhenFollowerIsNull()
      ShouldSkipFollowerDispatch_ReturnsTrue_WhenFollowerIsSameAsSource()
      SubmitFollowerCopyOrder_SkipsSubmit_WhenCreateOrderReturnsNull()

TB-T5 -- TryFireFollowerBeRetry + TryEvictFollowerBeSlot (CCN=15 + CCN=13)
  Methods: TryFireFollowerBeRetry  L1483-1517  CCN=15
           TryEvictFollowerBeSlot  L1542-1574  CCN=13
  Extraction design:
    TryFireFollowerBeRetry -- Parent CCN target: <= 6
      Helper 1: private bool IsBeRetryEligible(PendingFollowerBeSlot slot, Account acc)
                Absorbs the compound eligibility guard: slot null, acc null,
                slot.Account == acc name-match, slot.RetryCount under limit, IsFlat check.
                CCN target: <= 4
      Helper 2: private void ExecuteBeRetryAndRearm(PendingFollowerBeSlot slot,
                  Account acc, Instrument instr, int bufferTicks)
                Absorbs the retry execution block: BreakEven call, IncrementRetry,
                QueueBeRetryFallback, StatusUpdate.
                CCN target: <= 2
    TryEvictFollowerBeSlot -- Parent CCN target: <= 6
      Helper 1: private bool IsBeSlotEvictable(PendingFollowerBeSlot slot, Account acc)
                Absorbs the compound eviction-eligibility check: slot null, acc null,
                position-flat check, timeout-elapsed check.
                CCN target: <= 4
    [Fact] tests:
      IsBeRetryEligible_ReturnsFalse_WhenSlotIsNull()
      IsBeRetryEligible_ReturnsFalse_WhenRetryCountAtMax()
      IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat()
      ExecuteBeRetryAndRearm_CallsBreakEven()
      IsBeSlotEvictable_ReturnsFalse_WhenSlotIsNull()
      IsBeSlotEvictable_ReturnsTrue_WhenPositionFlatAndTimeoutElapsed()

TB-T6 -- TryHandleEntryDrag + IsExitSignalName + SyncAtmFollowerBracket + CancelPttDragOrphansForAccount
         (CCN=11 + CCN=10 + CCN=11 + CCN=10)
  Methods: TryHandleEntryDrag             L1886-1909  CCN=11
           IsExitSignalName               L2008-2033  CCN=10
           SyncAtmFollowerBracket         L2395-2445  CCN=11
           CancelPttDragOrphansForAccount L1606-1626  CCN=10
  Extraction design:
    TryHandleEntryDrag -- Parent CCN target: <= 6
      Helper 1: private bool IsEntryDragEligible(OrderEventArgs e)
                Absorbs the compound entry-drag eligibility guard:
                e.Order.Name contains "Entry", order state working/submitted compound,
                instrument match, IsFollowerAccount check.
                CCN target: <= 4
    IsExitSignalName -- Parent CCN target: <= 5
      Helper 1: private bool IsNonFlatDispatchName(string name)
                StartsWith("PTT-Copy") || StartsWith("Entry") || name == "Close"
                CCN target: <= 3
      Helper 2: private bool IsNativeExitName(string name)
                name == "Exit" || StartsWith("Target") || name == "Stop"
                CCN target: <= 3
    SyncAtmFollowerBracket -- Parent CCN target: <= 6
      Helper 1: private bool IsSyncAtmBracketEligible(Order fo, Order leaderOrder)
                Absorbs the pre-sync compound guard:
                fo null, leaderOrder null, IsAtmSTPOrder check,
                price-no-change early-exit check.
                CCN target: <= 4
    CancelPttDragOrphansForAccount -- Parent CCN target: <= 5
      Helper 1: private bool IsPttDragOrphanCancellable(Order o, Instrument instrument)
                Absorbs the compound cancel-eligibility check:
                o.Instrument?.FullName == instrument.FullName,
                (StartsWith("PTT-STP-Drag-") || StartsWith("PTT-TGT-Drag-")),
                order state working/submitted compound.
                CCN target: <= 4
    [Fact] tests:
      IsEntryDragEligible_ReturnsFalse_WhenOrderNameNotEntry()
      IsEntryDragEligible_ReturnsFalse_WhenOrderStateNotWorking()
      IsNonFlatDispatchName_ReturnsTrue_WhenNameIsPttCopy()
      IsNonFlatDispatchName_ReturnsFalse_WhenNameIsEmpty()
      IsNativeExitName_ReturnsTrue_WhenNameIsTarget()
      IsNativeExitName_ReturnsFalse_WhenNameIsPttCopy()
      IsSyncAtmBracketEligible_ReturnsFalse_WhenFollowerOrderNull()
      IsSyncAtmBracketEligible_ReturnsFalse_WhenPriceUnchanged()
      IsPttDragOrphanCancellable_ReturnsFalse_WhenInstrumentDoesNotMatch()
      IsPttDragOrphanCancellable_ReturnsFalse_WhenOrderStateIsFilled()

TB-T7 -- DtoToRule + GetRefPrice (CCN=11 + CCN=10)
  Methods: DtoToRule   L5609-5672  CCN=11
           GetRefPrice L5241-5248  CCN=10
  Extraction design:
    DtoToRule -- Parent CCN target: <= 5
      Helper 1: private string[] ResolveFollowerNames(CopyRuleDto dto)
                FollowerAccountNames null guard + ToArray() conversion.
                Returns empty string[] when null (JS-002 compliant).
                CCN target: <= 2
      Helper 2: private Dictionary<string,FollowerAtmMode> ResolveAtmMap(CopyRuleDto dto)
                AtmModes null guard + foreach construction of the atmMap Dictionary.
                Returns empty Dictionary when null (JS-002 compliant).
                CCN target: <= 3
      Helper 3: private int[] ResolveMultipliers(CopyRuleDto dto, int followerCount)
                Multipliers null/length guard + ToArray() with length-mismatch fallback
                to all-ones array. Returns valid int[] always (JS-002 compliant).
                CCN target: <= 3
    GetRefPrice -- Parent CCN target: <= 5
      Helper 1: private double SelectRefPriceByDirection(bool isLong, double bid,
                  double ask, double last)
                isLong: bid > 0 ? bid : last; short: ask > 0 ? ask : last.
                CCN target: <= 3
    [Fact] tests:
      ResolveFollowerNames_ReturnsEmptyArray_WhenDtoFollowersNull()
      ResolveFollowerNames_ReturnsArray_WhenFollowersPresent()
      ResolveAtmMap_ReturnsEmptyDict_WhenDtoAtmModesNull()
      ResolveMultipliers_ReturnsAllOnes_WhenLengthMismatch()
      ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull()
      SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive()
      SelectRefPriceByDirection_ReturnsLast_WhenLongAndBidZero()
      SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive()

==============================================================
SCANS (all 7 mandatory -- ptt-verifier runs these for EVERY ticket)
==============================================================

SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> 0 results
SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> 0 results
SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
SCAN-05a: lizard src/PropTraderTools/CopyEngine.cs --CCN 8
          --> CCN <= 8 for ALL methods modified in this ticket (ONLY hard gate)
SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
          cs delta
          --> Code Health score does NOT decrease vs pre-ticket HEAD (trend check only)
SCAN-06: dotnet build                 --> 0 errors, 0 warnings
SCAN-07: dotnet test
          --> 0 NEW failures
          --> 22 pre-existing IL-reflection failures = ACCEPTED BASELINE (pre-existing since B87)
          --> Test count must be >= prior ticket baseline (new tests added by engineer raise this number)

==============================================================
JS RULES IN SCOPE (all tickets)
==============================================================

JS-021: no lock() -- 0 results required (SCAN-01)
JS-002: no return null for missing values -- 0 new instances (SCAN-03)
         Helpers that signal absence must return empty collection, 0.0, false, or string.Empty.
JS-033: no async void (non-event-handler) -- 0 results (SCAN-02)
CYC: all modified methods <= 8, all extracted helpers <= 4

==============================================================
KNOWN BASELINE FAILURES (not regressions -- state in every verify report)
==============================================================

22 IL-reflection test failures in archive/v12-reference linting DLL.
Pre-existing since B87. Not caused by this wave.
ptt-verifier states in every report: "22 pre-existing IL-reflection failures -- accepted, not new"

==============================================================
FINAL_PASS CRITERIA FOR LANE B
==============================================================

  All 7 tickets (TB-T1 through TB-T7): VERIFY_PASS confirmed
  lizard src/PropTraderTools/CopyEngine.cs --CCN 8: 0 warnings for ALL Lane B methods
  dotnet build: 0 errors, 0 warnings
  dotnet test: 0 new failures
  cs check CopyEngine.cs: score higher than Lane B start score
  powershell -File scripts\verify_links.ps1 -Fix: run and confirm PASS
  docs/brain/BWAVE-CYC/LaneB-final-report.md: written
  Output: "LANE_B_FINAL_PASS"

BUILD TAG: "PTT-COPIER BWAVE-CYC Lane-B | {today-date}"
