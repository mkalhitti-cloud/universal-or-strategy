$continue | PTT-COPIER BWAVE-CYC | Wave Final Validation + PR Preparation

═══════════════════════════════════════════════════════
ROLE + CONSTRAINTS
═══════════════════════════════════════════════════════

ROLE: copier-spec mode. Discussion, validation, and PR preparation only.
SRC CODE BAN: No .cs edits in this session. All three lanes are closed.
              If validation finds a gap, report it to Director. Do not self-fix.
WORKSPACE: C:\WSGTA\universal-or-strategy\

═══════════════════════════════════════════════════════
SITUATION
═══════════════════════════════════════════════════════

All three BWAVE-CYC lanes have reported FINAL_PASS:
  Lane A: LANE_A_FINAL_PASS (commit 68a1c1c4)
  Lane B: LANE_B_FINAL_PASS (TB-T1 through TB-T7 all VERIFY_PASS)
  Lane C: LANE_C_FINAL_PASS (T1a through T7 all VERIFY_PASS)

IMPORTANT STATE NOTE:
  The lane work is in the working directory but NOT fully committed to git HEAD.
  git status shows these files modified/untracked:
    M  src/PropTraderTools/CopyEngine.cs
    M  src/PropTraderTools/PropTraderTools.csproj
    M  src/PropTraderTools/Tests/BwaveCycLaneCTests.cs
    M  src/PropTraderTools/TradeCopierAddOn.cs
    M  src/PropTraderTools/TradeCopierPanel.cs
    M  src/PropTraderTools/TradeCopierWindow.cs
    ?? src/PropTraderTools/Tests/BwaveCycLaneBTests.cs
  Last committed HEAD: d908f27b (Lane A final report doc only)
  The .cs changes from Lane B and Lane C are uncommitted.

═══════════════════════════════════════════════════════
YOUR TASKS -- IN ORDER, DO NOT SKIP ANY
═══════════════════════════════════════════════════════

TASK 1 -- GROUND TRUTH LIZARD SCAN
  Run lizard across ALL production .cs files RIGHT NOW from working directory:
    lizard src/PropTraderTools/CopyEngine.cs --CCN 8
    lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8
    lizard src/PropTraderTools/TradeCopierWindow.cs --CCN 8
    lizard src/PropTraderTools/TradeCopierAddOn.cs --CCN 8
  Report exact Warning cnt for each file.
  List every method still showing CCN > 8 with its current CCN value.
  Do NOT rely on the lane final reports -- run lizard fresh and report what you see.

TASK 2 -- BUILD AND TEST
  Run: dotnet build
  Run: dotnet test
  Report: build errors/warnings, test pass/fail counts.
  Expected: 0 errors, 0 warnings. 0 new test failures.
  Known accepted baseline: 22 pre-existing IL-reflection failures in archive linting DLL.

TASK 3 -- GAP ANALYSIS
  Compare TASK 1 lizard results against lane final reports:
    Lane A claimed: all Lane-A target methods at CCN <= 8
    Lane B claimed: all Lane-B target methods at CCN <= 8
    Lane C claimed: TradeCopierPanel = 0 warnings, TradeCopierWindow = 0 warnings
  For each method still showing CCN > 8:
    - Was it in a lane's scope?
    - Did the lane claim it was resolved?
    - Is it a regression (was lower before, now higher) or was it never in scope?
  Classify each remaining warning as:
    [REGRESSION]   -- lane claimed resolved but still over limit
    [NEVER_SCOPED] -- was not assigned to any lane (Director decision needed)
    [NEW_HELPER]   -- helper extracted by lane that itself is now over limit

TASK 4 -- UNCOMMITTED WORK COMMIT
  After TASK 1-3 are complete and reported to Director:
  If Director approves, stage and commit ALL .cs changes:
    git add src/PropTraderTools/
    git status --short   (confirm everything staged)
    git commit -m "feat(ptt): BWAVE-CYC all lanes complete -- CopyEngine/Panel/Window/AddOn CCN reduction, Jane Street standard"
  Then run TASK 1 lizard scan again against HEAD to confirm no drift.

TASK 5 -- PR STRATEGY DISCUSSION
  After commit is confirmed, discuss PR options with Director:

  Option A -- Single PR (all .cs changes together):
    Branch: feature/bwave-cyc (create from current main)
    Contains: all CopyEngine.cs + Panel + Window + AddOn changes
    Diff size: estimated large (Lane A alone was 62 files, 24511 insertions)
    Risk: PR diff may exceed 10k char limit. CodeRabbit/Codacy review on large diff.

  Option B -- Three PRs (one per lane):
    PR-LaneA: CopyEngine.cs Lane A extractions only (already committed at 68a1c1c4)
    PR-LaneB: CopyEngine.cs Lane B extractions + new test file
    PR-LaneC: TradeCopierPanel.cs + TradeCopierWindow.cs + TradeCopierAddOn.cs
    Smaller diffs, easier review, but more coordination.

  Option C -- Two PRs:
    PR-1: All CopyEngine.cs changes (Lane A + B combined)
    PR-2: All Panel/Window/AddOn changes (Lane C)
    Logical split: engine vs UI tier.

  For each option report:
    - Estimated diff size (run: git diff HEAD~N --stat -- src/PropTraderTools/*.cs)
    - Whether it exceeds the 10k char PR hygiene limit
    - Recommendation

TASK 6 -- PRE-PR HYGIENE
  Before any PR is opened:
    Run: powershell -File scripts\verify_links.ps1 -Fix
    Run: powershell -File scripts\verify_pr_hygiene.ps1
    Run: dotnet csharpier check src/
  Report results. Fix any formatting issues before opening PR.

═══════════════════════════════════════════════════════
CODESCENE CLI (for TASK 5 trend verification if needed)
═══════════════════════════════════════════════════════

$env:CS_ACCESS_TOKEN = "pat_eyJpZCI6ODg1OTIsInJhbmQiOiJXcXBwWEU4ZUdvcHJDTnNXWUdaM2FFbmtVUzJ6UjV4QSJ9"

Pre-wave CodeScene baselines (for comparison):
  CopyEngine.cs:        1.41
  TradeCopierPanel.cs:  3.45
  TradeCopierWindow.cs: 5.81
  TradeCopierAddOn.cs:  7.91

═══════════════════════════════════════════════════════
KNOWN BASELINE (state in every report)
═══════════════════════════════════════════════════════

22 IL-reflection test failures in archive/v12-reference linting DLL.
Pre-existing since B87. Accepted baseline. Not regressions.

═══════════════════════════════════════════════════════
STOP AFTER TASK 5 DISCUSSION
═══════════════════════════════════════════════════════

Do not open any PR without Director approval after the Task 5 discussion.
Report all findings clearly. Director decides the PR strategy.
