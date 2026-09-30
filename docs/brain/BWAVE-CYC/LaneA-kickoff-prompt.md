$start-lane | PTT-COPIER BWAVE-CYC | CopyEngine BE/ATM/Bracket Complexity Reduction | Lane-A

OBJECTIVE: Reduce 21 high-CCN methods in the BE/ATM/bracket/sync cluster of CopyEngine.cs to
CYC <= 8 by extracting private helper methods. Zero behaviour change. Zero new public surface.
Jane Street strict standard.

WAVE WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
SPEC: specs/002-trade-copier-spec.html
BRAIN: docs/brain/BWAVE-CYC\
BASELINE LIZARD: docs/brain/BWAVE-CYC\00-baseline.txt
BASELINE CODESCENE: docs/brain/BWAVE-CYC\00-cs-check-CopyEngine.txt
  CopyEngine.cs baseline Code Health: 1.41 / 10
  Target after Lane A completes: >= 4.0 (meaningful improvement toward final 7.0 target)

CODESCENE CLI (mandatory -- set before every cs command):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
  cs check src/PropTraderTools/CopyEngine.cs     (full file score)
  cs delta                                        (score delta vs HEAD -- run after each ticket)

PIPELINE -- 4 STAGES, SEQUENTIAL, EACH IS A HARD STOP:

  STAGE 1 -- ptt-orchestrator
    Read docs/brain/BWAVE-CYC/00-wave-plan.md.
    Read docs/brain/BWAVE-CYC/00-baseline.txt.
    Read docs/brain/BWAVE-CYC/00-cs-check-CopyEngine.txt.
    Produce mission brief covering all 8 tickets below.
    Write docs/brain/BWAVE-CYC/LaneA-01-mission-brief.md.
    Output: "STAGE 1 COMPLETE -- handing off to ptt-architect."
    *** STOP. Do not proceed to Stage 2 in the same response. ***

  STAGE 2 -- ptt-architect
    Read EACH of the 8 target method bodies in CopyEngine.cs (exact line ranges below).
    For each method, design the extraction:
      - Identify the decision branches / conditional clusters to extract
      - Name each helper semantically (what it does, not "helper1")
      - State CCN target for parent after extraction (MUST be <= 8)
      - State CCN target for each helper (MUST be <= 4, leave headroom for future features)
      - For methods with "Complex Conditional (N expressions)" in CodeScene: collapse
        the condition into a named private bool method (e.g. IsBeTargetStale())
      - For methods flagged "Bumpy Road": identify the nested loop/if depth to flatten
    Produce numbered tickets (T1 through T8) -- one ticket per row in TARGET METHODS below.
    Write docs/brain/BWAVE-CYC/LaneA-02-architect-plan.md.
    Output: "STAGE 2 COMPLETE -- handing off to ptt-engineer."
    *** STOP. Do not proceed to Stage 3 in the same response. ***

  STAGE 3 -- ptt-engineer
    Execute each architect ticket ONE AT A TIME. Sequence: T1, T2, T3, T4, T5, T6, T7, T8.
    For each ticket:
      1. Read the method(s) in full from CopyEngine.cs
      2. Apply the extraction per the architect plan
      3. Run: dotnet build  --> must be BUILD_PASS before next ticket
      4. Run: $env:CS_ACCESS_TOKEN = "pat_eyJ..."; cs delta  --> confirm no score regression
      5. Log: "T{N} PASS -- {method} CCN before={X} after={Y}, helpers: {names}"
    CYC EXTRACTION RULES (non-negotiable):
      1. Private helpers ONLY -- no new public or internal surface
      2. Helper names are semantic (describe the decision, not the position)
      3. Each extracted helper: CCN <= 4
      4. Parent method after extraction: CCN <= 8
      5. Behaviour IDENTICAL -- no logic changes, no reordering, no early returns added
      6. One new [Fact] test per extracted helper (add to CopyEngineTests.cs)
      7. Read docs/standards/NT8_COMPILER_RULES.md before any .cs edit
    Write docs/brain/BWAVE-CYC/LaneA-03-engineer-report.md.
    Output: "STAGE 3 COMPLETE -- BUILD_PASS -- handing off to ptt-verifier."
    *** STOP. Do not proceed to Stage 4 in the same response. ***

  STAGE 4 -- ptt-verifier
    Run ALL 7 scans independently. Do not trust engineer's self-report.
    Read every modified method and verify CCN <= 8 with lizard directly.
    Run cs delta and confirm Code Health score improved vs pre-Lane-A HEAD.
    Write docs/brain/BWAVE-CYC/LaneA-04-verify-report.md.
    Output: "VERIFY_PASS" or "VERIFY_FAIL: [exact blocker with method name and CCN]"
    Lane A is FINAL_PASS only when VERIFY_PASS is reported.
    *** STOP. Lane A complete. Report FINAL_PASS to Director. ***

MERGING ANY TWO STAGES INTO ONE RESPONSE IS A PROTOCOL VIOLATION.
ptt-orchestrator must not produce tickets.
ptt-architect must not edit .cs files.
ptt-engineer must not run scans independently.
ptt-verifier runs last, alone, and independently.

TARGET METHODS -- Lane A (21 methods, 8 tickets):

  T1 -- Highest severity pair (CCN 32 + 27)
    OnPendingBeAccountUpdate  L5480-5520  CCN=32  CS: "Complex Method(cc=19)" [CodeScene uses different cc formula]
    ArmPendingBe              L5308-5364  CCN=27  CS: "Complex Method(cc=17)"

  T2 -- BE target snapshot + stop-to-BE (CCN 24 + 18, Bumpy Road)
    SnapshotBeTargets         L4938-4981  CCN=24  CS: "Complex Method(cc=28)" -- NOTE: CS cc is higher here
    MoveStopToBreakEven       L4993-5133  CCN=18  CS: "Bumpy Road(3 bumps) + Large Method(82 LoC)"

  T3 -- Collateral resubmit (CCN 25, Large Method)
    ResubmitOneCollateralLeg  L2701-2785  CCN=25  CS: "Complex Method(cc=15) + Large Method(79 LoC)"

  T4 -- ATM cleanup pair (CCN 23 + 18)
    TryCleanupReArmedAtmBracket   L3727-3793  CCN=23  CS: "Complex Conditional(10 expressions)"
    ReplaceFollowerCopyOnAtmCancel L3548-3601 CCN=18  CS: "Bumpy Road(2 bumps) + Complex Method(cc=16)"

  T5 -- ATM/bracket sync (CCN 21 + 20) -- DW-B143-POSSTATE-CYC8 P0 items
    SyncAtmFollowerTarget     L2869-2953  CCN=21
    SyncFollowerBracket       L2279-2373  CCN=20  CS: "Complex Method(cc=16)"

  T6 -- Flatten + BE replace + target count (CCN 19 + 14 + 13)
    FlattenOneAccount         L4303-4372  CCN=19  CS: "Complex Method(cc=16) + Code Duplication cluster"
    TryReplacePttBeBrackets   L3644-3715  CCN=14  CS: "Complex Method(cc=12)"
    CountLeaderTargets        L4904-4931  CCN=13  CS: "Complex Method(cc=16)"

  T7 -- HandleEntry + PositionState + ResubmitTarget (CCN 13 + 13 + 13) -- DW-B143-POSSTATE-CYC8 P0
    HandleEntryChange         L3366-3426  CCN=13  CS: "Complex Method(cc=15)"
    TryFirePositionState      L3451-3499  CCN=13
    ResubmitTargetAfterCascade L2588-2649 CCN=13

  T8 -- QX bracket cancel cluster + AllAccounts (CCN 16 + 14 + 12 + 11 + 9)
    CancelQxBrackets (overload 1)  L875-905   CCN=14  CS: "Complex Method(cc=16)"
    CancelQxBrackets (overload 2)  L955-1004  CCN=16  CS: "Complex Method(cc=19)"
    CancelAllAccountOrders         L1013-1043 CCN=12
    BuildQxSnapshot                L916-944   CCN=11
    AllAccounts                    L4705-4752 CCN=9   (only 1 over limit -- include in T8)

SCANS (all 7 mandatory -- ptt-verifier runs all before VERIFY_PASS):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-05a: lizard src/PropTraderTools/CopyEngine.cs --CCN 8                        --> 0 warnings for all T1-T8 methods
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta                             --> Code Health does NOT decrease; no new Complex/Large/Bumpy findings
  SCAN-06: dotnet build                                                              --> 0 errors 0 warnings
  SCAN-07: dotnet test                                                               --> 370 pass, 22 pre-existing IL-reflection failures (ACCEPT -- not new regressions), 0 new failures

KNOWN BASELINE FAILURES -- NOT REGRESSIONS (ptt-verifier must acknowledge each):
  22 IL-reflection test failures in archive/v12-reference linting DLL
  These predate this wave (since B87). Do NOT count as new failures.
  ptt-verifier states: "22 pre-existing IL-reflection failures -- accepted, baseline confirmed"

AFTER ALL 7 SCANS PASS:
  powershell -File scripts\verify_links.ps1 -Fix
  (NT8 hard-link sync -- mandatory after every .cs change, CS0246 without it)

JS RULES IN SCOPE:
  JS-021: no lock() anywhere -- 0 results required
  JS-002: no return null for missing values -- 0 new instances
  JS-033: no async void (non-event-handler) -- 0 results
  CYC <= 8 for ALL modified methods (Jane Street strict standard)

BUILD TAG: "PTT-COPIER BWAVE-CYC Lane-A | {today-date}"

FINAL_PASS criteria:
  ptt-verifier VERIFY_PASS on all 7 scans
  All 21 target methods: CCN <= 8 confirmed by lizard
  cs delta: Code Health score improved vs pre-Lane-A HEAD
  New [Fact] tests: minimum 1 per extracted helper, all passing
  No new lock(), no new async void, no new return null
  Hard-link sync complete (verify_links.ps1 -Fix run)
  docs/brain/BWAVE-CYC/LaneA-04-verify-report.md written
