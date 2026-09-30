$start-lane | PTT-COPIER BWAVE-CYC | Panel/Window/AddOn Complexity Reduction | Lane-C

OBJECTIVE: Reduce 25 high-CCN methods across TradeCopierPanel.cs, TradeCopierWindow.cs, and
TradeCopierAddOn.cs to CYC <= 8. Zero behaviour change. Zero new public surface.
Jane Street strict standard.

PARALLELISM: This lane runs in parallel with Lane A from t=0. Lane C touches only
TradeCopierPanel.cs, TradeCopierWindow.cs, and TradeCopierAddOn.cs. Lane A/B only touch
CopyEngine.cs. There is NO file overlap. Proceed independently.

WAVE WORKSPACE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
SPEC: specs/002-trade-copier-spec.html
BRAIN: docs/brain/BWAVE-CYC\
BASELINE LIZARD: docs/brain/BWAVE-CYC\00-baseline.txt
BASELINE CODESCENE:
  docs/brain/BWAVE-CYC\00-cs-check-TradeCopierPanel.txt  (baseline: 3.45 / 10)
  docs/brain/BWAVE-CYC\00-cs-check-TradeCopierWindow.txt (baseline: 5.81 / 10)
  docs/brain/BWAVE-CYC\00-cs-check-TradeCopierAddOn.txt  (baseline: 7.91 / 10)
  Targets after Lane C: Panel >= 7.0, Window >= 8.0, AddOn >= 9.0

CODESCENE CLI (mandatory -- set before every cs command):
  $env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"
  cs check src/PropTraderTools/TradeCopierPanel.cs
  cs check src/PropTraderTools/TradeCopierWindow.cs
  cs check src/PropTraderTools/TradeCopierAddOn.cs
  cs delta   (score delta vs HEAD -- run after each ticket)

NT8 UI THREAD CONTRACT (critical -- read before any extraction in this lane):
  TradeCopierPanel.cs and TradeCopierWindow.cs contain WPF event handlers and
  NT8 Dispatcher callbacks. Extraction rules for this lane:

  SAFE to extract:
    - Pure decision logic (if/else trees, guard clauses, value computation)
    - Named predicate helpers (bool methods that answer a single question)
    - Value-building helpers that return a computed value

  FORBIDDEN to extract (would violate NT8 UI thread affinity):
    - Do NOT move any code that calls Dispatcher.InvokeAsync / Dispatcher.Invoke
      into a helper -- keep the Dispatcher call in the original method
    - Do NOT extract code that accesses NT8 Account, Order, or Position objects
      into a helper that could be called from a non-UI thread
    - Do NOT change the invocation site of any NT8 lifecycle callbacks
      (OnStateChange, OnWindowCreated, OnWindowDestroyed, etc.)
    - AccountDisplayConverter callbacks (OnRowApply, OnRuleBreakEven, etc.) are
      NT8 dispatcher callbacks -- extraction must keep the callback signature
      and entry point intact; only extract the internal decision logic

  Read docs/standards/NT8_ADDON_KNOWLEDGE.md before any edit to TradeCopierAddOn.cs.
  Read docs/standards/NT8_COMPILER_RULES.md before any .cs edit.

PIPELINE -- 4 STAGES, SEQUENTIAL, EACH IS A HARD STOP:

  STAGE 1 -- ptt-orchestrator
    Read docs/brain/BWAVE-CYC/00-wave-plan.md.
    Read the 3 baseline CodeScene files listed above.
    Confirm: Lane C is independent of Lane A/B (different files -- no coordination needed).
    Produce mission brief covering all 8 tickets below.
    Write docs/brain/BWAVE-CYC/LaneC-01-mission-brief.md.
    Output: "STAGE 1 COMPLETE -- handing off to ptt-architect."
    *** STOP. Do not proceed to Stage 2 in the same response. ***

  STAGE 2 -- ptt-architect
    Read EACH of the 8 target method bodies across the 3 files (exact line ranges below).
    For each method, design the extraction, applying the NT8 UI THREAD CONTRACT rules above.
    Specifically for each method:
      - Identify pure decision logic safe to extract
      - Identify any Dispatcher or NT8 API calls that MUST stay in the original method
      - Name each helper semantically
      - State CCN target for parent (MUST be <= 8)
      - State CCN target for each helper (MUST be <= 4)
      - Flag any method where extraction is constrained by NT8 thread rules
    Produce numbered tickets T1 through T8.
    Write docs/brain/BWAVE-CYC/LaneC-02-architect-plan.md.
    Output: "STAGE 2 COMPLETE -- handing off to ptt-engineer."
    *** STOP. Do not proceed to Stage 3 in the same response. ***

  STAGE 3 -- ptt-engineer
    Execute each architect ticket ONE AT A TIME. Sequence: T1, T2, T3, T4, T5, T6, T7, T8.
    For each ticket:
      1. Read the method(s) in full from the relevant file
      2. Apply the extraction per the architect plan, respecting NT8 UI THREAD CONTRACT
      3. Run: dotnet build  --> must be BUILD_PASS before next ticket
      4. Run: $env:CS_ACCESS_TOKEN = "pat_eyJ..."; cs delta  --> confirm no score regression
      5. Log: "T{N} PASS -- {method} CCN before={X} after={Y}, helpers: {names}"
    CYC EXTRACTION RULES (non-negotiable):
      1. Private helpers ONLY -- no new public or internal surface
      2. Helper names are semantic (what they decide, not their position)
      3. Each extracted helper: CCN <= 4
      4. Parent method after extraction: CCN <= 8
      5. Behaviour IDENTICAL -- no logic changes, no reordering, no early returns added
      6. One new [Fact] test per extracted helper (add to relevant test file)
      7. NT8 UI THREAD CONTRACT rules above are mandatory -- read them again before editing
    Write docs/brain/BWAVE-CYC/LaneC-03-engineer-report.md.
    Output: "STAGE 3 COMPLETE -- BUILD_PASS -- handing off to ptt-verifier."
    *** STOP. Do not proceed to Stage 4 in the same response. ***

  STAGE 4 -- ptt-verifier
    Run ALL 7 scans independently. Do not trust engineer self-report.
    Read every modified method and verify CCN <= 8 with lizard directly.
    Run cs check for each of the 3 modified files and confirm scores improved.
    Confirm NT8 UI THREAD CONTRACT was not violated (no Dispatcher calls moved).
    Write docs/brain/BWAVE-CYC/LaneC-04-verify-report.md.
    Output: "VERIFY_PASS" or "VERIFY_FAIL: [exact blocker with method name and CCN]"
    Lane C is FINAL_PASS only when VERIFY_PASS is reported.
    *** STOP. Lane C complete. Report FINAL_PASS to Director. ***

MERGING ANY TWO STAGES INTO ONE RESPONSE IS A PROTOCOL VIOLATION.
ptt-orchestrator must not produce tickets.
ptt-architect must not edit .cs files.
ptt-engineer must not run scans independently.
ptt-verifier runs last, alone, and independently.

TARGET METHODS -- Lane C (25 methods, 8 tickets):

  T1 -- Panel: FollowerItem button colours + load (CCN=18 + CCN=17, WPF binding methods)
    FollowerItem::UpdateButtonColors  L633-671   CCN=18  CS: "Bumpy Road(2 bumps) + Complex Method"
    FollowerItem::OnLoaded            L696-802   CCN=17  CS: "Bumpy Road(3 bumps) + Large Method(86 LoC)"
    File: TradeCopierPanel.cs
    NT8 note: These are WPF RoutedEvent handlers. Extract decision logic only.
              Do NOT move any VisualTreeHelper or DependencyProperty calls.

  T2 -- Panel: apply-rule + ATM template name (CCN=15 + CCN=12)
    OnApplyRule                       L2843-2894 CCN=15  CS: "Bumpy Road(5 bumps) + Complex Method(cc=17)"
    FollowerItem::GetLeaderAtmTemplateName L2642-2678 CCN=12
    File: TradeCopierPanel.cs
    NT8 note: OnApplyRule modifies CopyRule and calls CopyEngine.Instance. Extract
              only the validation/guard logic. Keep the CopyEngine call in OnApplyRule.

  T3 -- Panel: feature flag visibility switches (CCN=10 + CCN=11)
    TradeCopierPanel::ApplyFeatureFlags         L3176-3202 CCN=10
    TradeCopierPanel::ApplyFeatureFlagTooltips  L3206-3218 CCN=11
    File: TradeCopierPanel.cs
    NT8 note: These switch on FeatureFlags enum values. Safe to extract per-flag
              visibility blocks into named helpers (e.g. ApplyDragFeatureFlags()).

  T4 -- Panel: position/price callbacks (CCN=10 + CCN=10 + CCN=10 + CCN=9)
    FollowerItem::IsPriceAlreadyAtBe       L1602-1616 CCN=10
    FollowerItem::RefreshQuickDisplay      L2027-2047 CCN=10
    FollowerItem::OnLeaderPositionUpdate   L2096-2122 CCN=10
    TradeCopierPanel::OnChartMouseDown     L2749-2796 CCN=9
    File: TradeCopierPanel.cs
    NT8 note: IsPriceAlreadyAtBe is a pure predicate -- fully safe to refactor.
              OnLeaderPositionUpdate fires on NT8 position event thread; extract
              only the pure decision logic, keep any UI dispatch calls in original.

  T5 -- Window: row apply handler (CCN=18, NT8 dispatcher callback)
    AccountDisplayConverter::OnRowApply  L1156-1199 CCN=18  CS: "Bumpy Road(2 bumps) + Complex Method(cc=18)"
    File: TradeCopierWindow.cs
    NT8 note: This is an AccountDisplayConverter callback fired by NT8 Dispatcher.
              Extract the per-rule-type decision blocks into private helpers on the
              AccountDisplayConverter class. Keep the outer switch/dispatch intact.

  T6 -- Window: BE/stop/arm rule callbacks (CCN=11 + CCN=10 + CCN=10)
    AccountDisplayConverter::OnRuleBreakEven    L1082-1097 CCN=11
    AccountDisplayConverter::OnRuleArmBe        L1104-1129 CCN=10
    AccountDisplayConverter::OnRuleTightenStop  L1135-1151 CCN=10
    File: TradeCopierWindow.cs
    NT8 note: Same dispatcher-callback constraints as T5.

  T7 -- Window: feature flags (CCN=9)
    TradeCopierWindow::ApplyFeatureFlags  L399-431 CCN=9
    File: TradeCopierWindow.cs
    NT8 note: Pure visibility/enable switches. Safe extraction.

  T8 -- AddOn: inject + menu wire (CCN=15 + CCN=9, NT8 visual tree walk)
    TradeCopierAddOn::DoInject           L384-491 CCN=15  CS: "Bumpy Road(2 bumps) + Large Method(77 LoC)"
    TradeCopierAddOn::WireControlCenterMenu L114-150 CCN=9 CS: "Bumpy Road(2 bumps)"
    File: TradeCopierAddOn.cs
    NT8 note: DoInject walks the NT8 WPF visual tree (VisualTreeHelper.GetChild, etc.)
              and attaches to NT8 Control Center UI elements. Extract the finding-of-
              specific-controls into named private helpers (e.g. FindChartTraderPanel())
              but keep the main injection sequence in DoInject. Read
              docs/standards/NT8_ADDON_KNOWLEDGE.md before editing this method.

SCANS (all 7 mandatory -- ptt-verifier runs all before VERIFY_PASS):
  SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  --> 0 results
  SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  --> 0 new instances
  SCAN-05a: lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8                  --> 0 warnings for T1-T4 methods
            lizard src/PropTraderTools/TradeCopierWindow.cs --CCN 8                 --> 0 warnings for T5-T7 methods
            lizard src/PropTraderTools/TradeCopierAddOn.cs --CCN 8                  --> 0 warnings for T8 methods
  SCAN-05b: $env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta                             --> Code Health does NOT decrease for any modified file
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

BUILD TAG: "PTT-COPIER BWAVE-CYC Lane-C | {today-date}"

FINAL_PASS criteria:
  ptt-verifier VERIFY_PASS on all 7 scans
  All 25 Lane-C target methods: CCN <= 8 confirmed by lizard
  cs check scores: Panel >= 7.0, Window >= 8.0, AddOn >= 9.0
  NT8 UI THREAD CONTRACT not violated (ptt-verifier explicitly confirms)
  New [Fact] tests: minimum 1 per extracted helper, all passing
  No new lock(), no new async void, no new return null
  Hard-link sync complete (verify_links.ps1 -Fix run)
  docs/brain/BWAVE-CYC/LaneC-04-verify-report.md written
