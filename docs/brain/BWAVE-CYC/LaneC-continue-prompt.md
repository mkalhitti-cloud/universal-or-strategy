$continue | PTT-COPIER BWAVE-CYC | Panel/Window/AddOn Complexity Reduction | Lane-C CONTINUATION

SITUATION:
Lane C ran a single-engineer session (wrong structure) and was stopped.
THE GOOD NEWS: Lane C is FULLY COMPLETE.
lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8  --> Warning cnt = 0
lizard src/PropTraderTools/TradeCopierWindow.cs --CCN 8 --> Warning cnt = 0
lizard src/PropTraderTools/TradeCopierAddOn.cs --CCN 8  --> Warning cnt = 0
All 25 target methods are at CCN <= 8. Build passes. 0 new test failures.

Your ONLY job now is to run the independent verification pass and close the lane.

ORCHESTRATOR ROLE FROM HERE:
You are ptt-orchestrator. There is no more engineering work to do for Lane C.
Spawn ONE ptt-verifier subtask (below) to independently confirm the full lane is clean.
When VERIFY_PASS is returned, write the final report and declare LANE_C_FINAL_PASS.

WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
BRAIN: docs/brain/BWAVE-CYC\
ARCHITECT PLAN: docs/brain/BWAVE-CYC/LaneC-02-architect-plan.md
CHECKPOINT COMMIT: 12ec4ea0

CODESCENE CLI (set before every cs command):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"

==============================================================
STEP 1 -- Spawn this verifier subtask now:
==============================================================

You are ptt-verifier for BWAVE-CYC Lane-C FULL LANE VERIFICATION.
The engineer completed all 17 methods across TradeCopierPanel.cs, TradeCopierWindow.cs,
and TradeCopierAddOn.cs in a single session. Your job is to independently verify
the ENTIRE lane is clean before it is declared FINAL_PASS.

READ FIRST:
  docs/brain/BWAVE-CYC/LaneC-02-architect-plan.md  (17 methods, 8 tickets, exact CCN targets)
  docs/brain/BWAVE-CYC/LaneC-01-mission-brief.md

VERIFY ALL 17 METHODS (check each one individually against the architect plan CCN targets):

  T1: UpdateButtonColors (target CCN <= 5), OnLoaded (target CCN <= 7)
  T2: OnApplyRule (target CCN <= 8), GetLeaderAtmTemplateName (target CCN <= 5)
  T3: ApplyFeatureFlags/Panel (target CCN <= 4), ApplyFeatureFlagTooltips (target CCN <= 2)
  T4: IsPriceAlreadyAtBe (target CCN <= 5), RefreshQuickDisplay (target CCN <= 6),
      OnLeaderPositionUpdate (target CCN <= 6), OnChartMouseDown (target CCN <= 7)
  T5: AccountDisplayConverter::OnRowApply (target CCN <= 7)
  T6: OnRuleBreakEven (target CCN <= 5), OnRuleArmBe (target CCN <= 7), OnRuleTightenStop (target CCN <= 5)
  T7: TradeCopierWindow::ApplyFeatureFlags (target CCN <= 5)
  T8: DoInject (target CCN <= 7), WireControlCenterMenu (target CCN <= 5)

NT8 UI THREAD CONTRACT CHECK (verify these were NOT violated):
  - No Dispatcher.InvokeAsync calls were moved into extracted helpers
  - AccountDisplayConverter callback signatures unchanged
  - DoInject visual tree walk helpers are private, only called from DoInject
  - All AddOn lifecycle callbacks (OnStateChange, OnWindowCreated, OnWindowDestroyed) unchanged

7 MANDATORY SCANS (run all, report each result):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> must be 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> must be 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new vs pre-wave baseline
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new vs pre-wave baseline
  SCAN-05a: lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8                  --> Warning cnt = 0
            lizard src/PropTraderTools/TradeCopierWindow.cs --CCN 8                 --> Warning cnt = 0
            lizard src/PropTraderTools/TradeCopierAddOn.cs --CCN 8                  --> Warning cnt = 0
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs check src/PropTraderTools/TradeCopierPanel.cs
            cs check src/PropTraderTools/TradeCopierWindow.cs
            cs check src/PropTraderTools/TradeCopierAddOn.cs
            --> All three scores must be HIGHER than baselines (Panel: 3.45, Window: 5.81, AddOn: 7.91)
  SCAN-06: dotnet build                                                              --> 0 errors, 0 warnings
  SCAN-07: dotnet test                                                               --> 0 NEW failures
            (22 pre-existing IL-reflection failures are ACCEPTED -- not new regressions)

Write docs/brain/BWAVE-CYC/LaneC-final-verify.md with:
  - Each of the 17 methods: confirmed CCN after extraction (read with lizard)
  - NT8 UI thread contract: PASS or FAIL with specifics
  - All 7 scan results
  - CodeScene scores for all 3 files

Output exactly: "VERIFY_PASS -- Lane-C FINAL" or "VERIFY_FAIL -- Lane-C: [exact blocker]"
Then STOP.

==============================================================
STEP 2 -- After verifier returns VERIFY_PASS:
==============================================================

Run once:
  powershell -File scripts\verify_links.ps1 -Fix

Write docs/brain/BWAVE-CYC/LaneC-final-report.md with:
  - Summary: 17 methods reduced, 25 violations eliminated
  - CodeScene scores before/after for Panel, Window, AddOn
  - Test count: {N} pass / 22 pre-existing accepted / 15 skips
  - Verify links: PASS

Output exactly: "LANE_C_FINAL_PASS"
Report to Director.
