$start-lane | PTT-COPIER BWAVE-CYC | CopyEngine Dispatch/Entry Complexity Reduction | Lane-B

OBJECTIVE: Reduce 13 high-CCN methods in the dispatch/entry/order cluster of CopyEngine.cs to
CYC <= 8. Zero behaviour change. Zero new public surface. Jane Street strict standard.

PREREQUISITE -- HARD GATE: Lane A must be FINAL_PASS before this lane starts.
  Confirm: docs/brain/BWAVE-CYC/LaneA-04-verify-report.md exists and contains VERIFY_PASS.
  If Lane A is not FINAL_PASS: STOP. Do not proceed. Report to Director.

WAVE WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
SPEC: specs/002-trade-copier-spec.html
BRAIN: docs/brain/BWAVE-CYC\
BASELINE LIZARD: docs/brain/BWAVE-CYC\00-baseline.txt
BASELINE CODESCENE: docs/brain/BWAVE-CYC\00-cs-check-CopyEngine.txt
  CopyEngine.cs baseline Code Health: 1.41 / 10 (pre-wave)
  Expected score entering Lane B: >= 4.0 (after Lane A extractions)
  Target after Lane B completes: >= 6.5

CODESCENE CLI (mandatory -- set before every cs command):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
  cs check src/PropTraderTools/CopyEngine.cs     (full file score)
  cs delta                                        (score delta vs HEAD -- run after each ticket)

PIPELINE -- 4 STAGES, SEQUENTIAL, EACH IS A HARD STOP:

  STAGE 1 -- ptt-orchestrator
    Confirm LaneA-04-verify-report.md contains VERIFY_PASS. If not: STOP immediately.
    Read docs/brain/BWAVE-CYC/00-wave-plan.md.
    Read docs/brain/BWAVE-CYC/00-cs-check-CopyEngine.txt.
    Run: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs check src/PropTraderTools/CopyEngine.cs
    Record the post-Lane-A score as the Lane B starting score.
    Produce mission brief covering all 5 tickets below.
    Write docs/brain/BWAVE-CYC/LaneB-01-mission-brief.md.
    Output: "STAGE 1 COMPLETE -- Lane A score confirmed: {X}. Handing off to ptt-architect."
    *** STOP. Do not proceed to Stage 2 in the same response. ***

  STAGE 2 -- ptt-architect
    Read EACH of the 5 target method bodies in CopyEngine.cs (exact line ranges below).
    For each method, design the extraction:
      - Identify the decision branches / conditional clusters to extract
      - Name each helper semantically (what it does, not "helper1")
      - State CCN target for parent (MUST be <= 8)
      - State CCN target for each helper (MUST be <= 4)
      - OnOrderUpdate (CCN=23): this is the core dispatch gate -- extract the per-follower
        dispatch block, the order-type filter block, and the dedup check as named helpers.
        DO NOT change gate ordering. Gates 1-4 must remain in the same sequence.
      - DispatchCopy (CCN=13): extract the follower-order-building logic and the
        lot-ratio application as separate named helpers.
    Produce numbered tickets T1 through T5.
    Write docs/brain/BWAVE-CYC/LaneB-02-architect-plan.md.
    Output: "STAGE 2 COMPLETE -- handing off to ptt-engineer."
    *** STOP. Do not proceed to Stage 3 in the same response. ***

  STAGE 3 -- ptt-engineer
    Execute each architect ticket ONE AT A TIME. Sequence: T1, T2, T3, T4, T5.
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
    SPECIAL RULE FOR OnOrderUpdate:
      Gate ordering (Gate1=copy-enabled, Gate2=rule-match, Gate3=order-state,
      Gate4=dedup) MUST remain identical. Extraction can only touch the
      per-follower loop body and the inner dispatch call -- never the gates.
    Write docs/brain/BWAVE-CYC/LaneB-03-engineer-report.md.
    Output: "STAGE 3 COMPLETE -- BUILD_PASS -- handing off to ptt-verifier."
    *** STOP. Do not proceed to Stage 4 in the same response. ***

  STAGE 4 -- ptt-verifier
    Run ALL 7 scans independently. Do not trust engineer self-report.
    Read every modified method and verify CCN <= 8 with lizard directly.
    Run cs check and confirm Code Health score improved vs Lane B start score.
    Write docs/brain/BWAVE-CYC/LaneB-04-verify-report.md.
    Output: "VERIFY_PASS" or "VERIFY_FAIL: [exact blocker with method name and CCN]"
    Lane B is FINAL_PASS only when VERIFY_PASS is reported.
    *** STOP. Lane B complete. Report FINAL_PASS to Director. ***

MERGING ANY TWO STAGES INTO ONE RESPONSE IS A PROTOCOL VIOLATION.
ptt-orchestrator must not produce tickets.
ptt-architect must not edit .cs files.
ptt-engineer must not run scans independently.
ptt-verifier runs last, alone, and independently.

TARGET METHODS -- Lane B (13 methods, 5 tickets):

  T1 -- Core dispatch gate (CCN=23) -- DW-B143-POSSTATE-CYC8 P0
    OnOrderUpdate  L1316-1431  CCN=23  CS: "Complex Method(cc=20)"
    SPECIAL CONSTRAINT: Gate sequence (Gates 1-4) must not change.
    Extract: per-follower dispatch body, order-type guard, lot-ratio application.

  T2 -- Copy dispatch (CCN=13, Large Method 87 LoC)
    DispatchCopy  L2082-2199  CCN=13  CS: "Complex Method(cc=16) + Large Method(87 LoC)"
    Extract: follower-order-builder logic, limit-price calculation, order-action resolution.

  T3 -- BE retry + BE evict (CCN=15 + CCN=13)
    TryFireFollowerBeRetry   L1483-1517  CCN=15  CS: "Complex Conditional(2 expressions)"
    TryEvictFollowerBeSlot   L1542-1574  CCN=13

  T4 -- Entry drag + exit signal + ATM follower bracket (CCN=11 + CCN=10 + CCN=11)
    TryHandleEntryDrag       L1886-1909  CCN=11
    IsExitSignalName         L2008-2033  CCN=10  CS: "Complex Conditional(2 expressions)"
    SyncAtmFollowerBracket   L2395-2445  CCN=11

  T5 -- DTO deserialization (CCN=11)
    DtoToRule  L5609-5672  CCN=11  CS: "Bumpy Road(3 bumps) + Complex Method(cc=12)"
    Extract: per-field mapping blocks into named private helpers.

SCANS (all 7 mandatory -- ptt-verifier runs all before VERIFY_PASS):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-05a: lizard src/PropTraderTools/CopyEngine.cs --CCN 8                        --> 0 warnings for ALL methods (Lane A + Lane B combined)
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta                             --> Code Health does NOT decrease vs Lane B start; no new findings
  SCAN-06: dotnet build                                                              --> 0 errors 0 warnings
  SCAN-07: dotnet test                                                               --> 370 pass, 22 pre-existing IL-reflection (ACCEPT), 0 new failures

KNOWN BASELINE FAILURES -- NOT REGRESSIONS:
  22 IL-reflection test failures in archive/v12-reference linting DLL -- pre-existing.
  ptt-verifier states: "22 pre-existing IL-reflection failures -- accepted, baseline confirmed"

AFTER ALL 7 SCANS PASS:
  powershell -File scripts\verify_links.ps1 -Fix

JS RULES IN SCOPE:
  JS-021: no lock() -- 0 results
  JS-002: no return null -- 0 new
  JS-033: no async void -- 0 results
  CYC <= 8 ALL modified methods

BUILD TAG: "PTT-COPIER BWAVE-CYC Lane-B | {today-date}"

FINAL_PASS criteria:
  Lane A VERIFY_PASS confirmed before start
  ptt-verifier VERIFY_PASS on all 7 scans
  All 13 Lane-B target methods: CCN <= 8 confirmed by lizard
  lizard CopyEngine.cs --CCN 8: 0 warnings total (covers both Lane A + B targets)
  cs delta: Code Health score improved vs Lane B start score
  New [Fact] tests: minimum 1 per extracted helper, all passing
  No new lock(), no new async void, no new return null
  Hard-link sync complete (verify_links.ps1 -Fix run)
  docs/brain/BWAVE-CYC/LaneB-04-verify-report.md written
