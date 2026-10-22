# B215 Orchestrator Prompt
## Block: B215 -- Test-Debt Remediation + Spec Update + DW-B212-01 Close
## Status: READY FOR DIRECTOR APPROVAL

---

## DIRECTOR PRE-WORK (before sending this prompt to ptt-orchestrator)

These three items are gitignored -- agent tools cannot write to them.
Open each file DIRECTLY in the NinjaTrader project folder and apply manually:

### PRE-1: B74LaneCTests.cs (DW-B214-LB-01)
File: `src/PropTraderTools/Tests/B74LaneCTests.cs`
Change: Update two `Assert.Equal` expected values for the BE buffer cap tests:
- `GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen` -> change `Assert.Equal(10, ...)` to `Assert.Equal(50, ...)`
- `GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen` -> change `Assert.Equal(-10, ...)` to `Assert.Equal(-50, ...)`

### PRE-2: B73Tests.cs (DW-B214-LB-03)
File: `src/PropTraderTools/Tests/B73Tests.cs`
Change: Update two assertions to include "t" suffix:
- `T_BUF_BE_01_FormatGlobalBeBuffer_WithPositiveTicks_ReturnsCorrectLabel` -> append "t" to expected string
- `T_LABEL_02_FormatGlobalBeBuffer_WithFiveTicks_ReturnsCorrectLabel` -> append "t" to expected string

### PRE-3: B214Tests.cs T2 class (DW-B214-LB-02)
File: `src/PropTraderTools/Tests/B214Tests.cs`
Change: Append the `B214T2Tests` class with 3 `[Fact]` tests:
  - `T_B214_T2_01_IncrementBuffer_AtNewCeiling_DoesNotExceed50`
  - `T_B214_T2_02_DecrementBuffer_AtNewFloor_DoesNotGoBelow50`
  - `T_B214_T2_03_IncrementBuffer_From49_Reaches50`
Exact test code is in `docs/brain/B214/04-tickets-lane-b.md` Ticket T2 section "New xUnit Tests to Add".

After applying PRE-1, PRE-2, PRE-3: run `dotnet test` to confirm the former failures in
B74LaneCTests and B73Tests now pass. Then proceed to send the prompt below.

---

## INVESTIGATION NOTE FOR ARCHITECT (DW-B212-01 PRE-RESOLVED)

Before this pipeline runs, the Architect MUST read `src/PropTraderTools/CopyEngine.cs` lines 5977-6015
to confirm: `TrimOneAccount` calls `acc.CreateOrder(... "PTT-Trim" ...)` -- a partial market sell --
and does NOT call `CancelAllAccountOrders`. This means DW-B212-01's hypothesised ATM re-arm risk
does NOT apply to `Trim(Instrument)` because `TrimOneAccount` never cancels ATM brackets.
DW-B212-01 should be CLOSED-NO-ACTION in the Ph1 architecture plan with this evidence noted.
The Ph1 plan should include a one-line comment update at `Trim(Instrument)` (line 5818) confirming
the leader-skip investigation is resolved and no fix is needed -- this counts as Lane C T1.

---

## PIPELINE BLOCK: B215

### BLOCK OVERVIEW

Block: B215
Prior block: B214 (FINAL_PASS -- Lane A + Lane B)
Deferred items addressed:
- DW-B214-02: 24 pre-existing test failures (Lane A -- test-debt remediation)
- DW-B212-01: Trim(Instrument) leader-skip investigation -> CLOSED-NO-ACTION (Lane B -- comment only)
- DW-B209-04: specs/002-trade-copier-spec.html B209 OCO + GTC guard features (Lane C -- spec HTML only)
- DW-B214-01: CYC annotation inconsistency comment fix on TryResolveLeaderAccount (folds into Lane B T1)

NOT in this block (carry forward):
- DW-B209-01: Guardian script false positive fix (P2 -- separate block)
- DW-B209-02: BrushTeal gap (P2 -- cosmetic, separate block)
- DW-B211-01/02: Direction-aware Ask/Bid + nudge buttons (P2 -- future block)
- DW-B212-02: PttMoveTargetAll.cs future role decision (P2 -- future block)

### LANE MAP

| Lane | Work | Files | Pipeline Type |
|------|------|-------|---------------|
| Lane A | 24 test failure remediation -- diagnose + fix each failing test class | CopyEngine.cs + test files (gitignored -- PowerShell only) | STANDARD Ph1-Ph5 |
| Lane B | DW-B212-01 close (comment only) + DW-B214-01 CYC annotation fix | CopyEngine.cs (1 comment line) + TradeCopierPanel.cs (1 comment line) | MINIMAL Ph1-Ph5 |
| Lane C | DW-B209-04 spec HTML update | specs/002-trade-copier-spec.html ONLY -- NO .cs files | SPEC-ONLY Ph1-Ph5 |

Lanes A and C are independent. Lane B is independent of both.
Recommended execution order: Lane B first (minimal, low risk), then Lane A and Lane C in parallel.

---

## PIPELINE CHAIN (MANDATORY -- ALL PHASES -- NONE SKIPPABLE -- NONE COMBINABLE)

Ph1  ptt-architect       -> docs/brain/B215/02-architecture-plan-{lane}.md
Ph2  ptt-plan-reviewer   -> docs/brain/B215/02-plan-review-{lane}.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B215/04-tickets-{lane}.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B215/04-ticket-review-{lane}.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src edits + docs/brain/B215/ticket-{N}-completion.md
Ph4b ptt-verifier        -> docs/brain/B215/ticket-{N}-verification.md     (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B215/05-final-review-{lane}.md + 06-deferred-backlog.md

DO NOT SKIP ANY PHASE. DO NOT COMBINE PHASES. DO NOT RUN PIPELINE UNTIL DIRECTOR SAYS "run pipeline".

---

## LANE A ORCHESTRATOR PROMPT
### (paste into ptt-orchestrator session for Lane A)

---

You are the ptt-orchestrator for B215 Lane A.

BLOCK: B215-LaneA
EPIC: Test-Debt Remediation -- 24 Pre-Existing Test Failures
SOURCE: DW-B214-02 in docs/brain/B214/06-deferred-backlog.md
PRIOR BLOCK: B214 (FINAL_PASS confirmed in docs/brain/NO-PIPELINE-REPAIRS.md line 4326)

CONTEXT:
24 test failures have been pre-existing across B211/B212/B213/B214 without being introduced by
any of those blocks. They exist in 8 test classes. Before this pipeline runs, the Director
has already manually applied the gitignored manual updates (PRE-1, PRE-2, PRE-3 above), so the
B74LaneCTests and B73Tests failures are already resolved. The remaining 24 (or fewer after PRE fixes)
are structural failures in these 8 classes:
  - B153IsPttQxTargetTests (6 failures) -- QX targeting logic
  - B155ContractTests (2 failures) -- contract validation
  - DwB15701SlateReadyTests (2 failures) -- slate-ready conditions
  - DwB168_02Tests (4 failures) -- DW-B168 conditions
  - LaneFTests / B188 (3 failures) -- Lane F eligibility
  - B189T4SpinnerTests (2 failures) -- spinner integration
  - B195Tests (1 failure) -- SubmitDrainedEntry
  - B196Tests (4 failures) -- FindLeaderStopPrice

GOAL:
Diagnose each failing test class. For each: identify whether the failure is (a) a stale
assertion that no longer matches the current implementation, (b) a genuine regression introduced
by prior blocks that was not caught, or (c) a test that tests removed/renamed functionality.
Produce a remediation plan covering all 24 failures. Implement fixes.

SCOPE CONSTRAINTS (HARD LIMITS):
- Touch ONLY the test files and the minimum CopyEngine.cs / feature files required to fix genuine regressions.
- If a test is stale (asserts old behaviour no longer spec'd), update the test to match current spec.
- If a test covers a genuine regression, fix the regression in src/. Create a DW-card for every
  src/ regression fix -- treat each as a mini-hotfix with full JS-DNA audit.
- If a test covers removed/renamed functionality, delete the test with a documented rationale.
- Do NOT attempt to fix the gitignored test files directly (B74LaneCTests.cs, B73Tests.cs,
  B214Tests.cs T2 class) -- those are handled by Director PRE-work above.
- CYC <= 8 on any modified src/ method.
- Zero new lock() usages. Zero new async void. ASCII-only.

PIPELINE CHAIN (MANDATORY -- ALL PHASES -- NONE SKIPPABLE):
Ph1  ptt-architect       -> docs/brain/B215/02-architecture-plan-lane-a.md
Ph2  ptt-plan-reviewer   -> docs/brain/B215/02-plan-review-lane-a.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B215/04-tickets-lane-a.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B215/04-ticket-review-lane-a.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src edits + docs/brain/B215/ticket-{N}-completion.md  (one ticket per failing class)
Ph4b ptt-verifier        -> docs/brain/B215/ticket-{N}-verification.md     (VERIFY_PASS per ticket)
Ph5  ptt-plan-reviewer   -> docs/brain/B215/05-final-review-lane-a.md + 06-deferred-backlog.md (FINAL_PASS gate)

SUCCESS CRITERIA:
- dotnet test: Failed count = 0 (or documented as intentional deletions with DW-cards).
- Passed count >= 200 (must not drop -- no tests deleted without authorised DW-card).
- All 7 scans PASS in every ticket verification report.
- ptt-sync 21/0 after every .cs edit.
- 05-final-review-lane-a.md and 06-deferred-backlog.md produced.

KEY FILES TO READ (Ph1 Architect -- read before designing):
- docs/brain/B214/06-deferred-backlog.md (DW-B214-02 section -- full failure list)
- docs/brain/B214/ticket-t1-verification.md (SCAN-2 -- exact 24 failure names)
- docs/standards/jane-street/RULES_CATALOG.md (enforcement gate)
- src/PropTraderTools/CopyEngine.cs (for regression investigation)
- Run: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v detailed 2>&1 > test-output.txt
  Then read test-output.txt to see exact failure messages for each of the 24 tests.

---

## LANE B ORCHESTRATOR PROMPT
### (paste into ptt-orchestrator session for Lane B)

---

You are the ptt-orchestrator for B215 Lane B.

BLOCK: B215-LaneB
EPIC: DW-B212-01 Close (comment) + DW-B214-01 CYC annotation fix
SOURCE: DW-B212-01 and DW-B214-01 in docs/brain/B214/06-deferred-backlog.md
PRIOR BLOCK: B214 (FINAL_PASS confirmed in docs/brain/NO-PIPELINE-REPAIRS.md line 4326)

CONTEXT:
Two documentation/comment-only fixes. No logic changes. No new tests.

DW-B212-01: CopyEngine.cs `Trim(Instrument)` at line 5818 was flagged as potentially needing
a leader-skip guard identical to `Flatten(Instrument)` (HF-08). Investigation confirmed:
`TrimOneAccount` (lines 5977-6015) calls `acc.CreateOrder(... "PTT-Trim" ...)` -- a partial
market sell -- and does NOT call `CancelAllAccountOrders`. Therefore no ATM re-arm risk exists.
DW-B212-01 is CLOSED-NO-ACTION. The only code change is a one-line comment at `Trim(Instrument)`
(line 5818) documenting the investigation result.

DW-B214-01: `TryResolveLeaderAccount` has a CYC comment annotation mismatch: Ph4a counted
CYC=6 (project path-counting convention), Ph4b independent count is CYC=8 (strict McCabe
decision points). CYC=8 still PASSES JS-066. The fix is a comment-only update to align the
annotation to strict McCabe: update the existing CYC comment on `TryResolveLeaderAccount`
to read `CYC=8 (strict McCabe: 8 decision points)` with a note on the count methodology.
File: src/PropTraderTools/TradeCopierPanel.cs -- comment only, no logic.

SCOPE CONSTRAINTS (HARD LIMITS):
- Comment-only edits. Zero logic changes. Zero new tests required.
- Exactly 2 files touched: CopyEngine.cs (1 comment line) and TradeCopierPanel.cs (1 comment line).
- CYC unchanged on all methods -- comment edits do not affect CYC.
- ptt-sync after each .cs edit. Guardian after each .cs edit. DIFF GUARD must PASS.

PIPELINE CHAIN (MANDATORY -- ALL PHASES -- NONE SKIPPABLE):
Ph1  ptt-architect       -> docs/brain/B215/02-architecture-plan-lane-b.md
Ph2  ptt-plan-reviewer   -> docs/brain/B215/02-plan-review-lane-b.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B215/04-tickets-lane-b.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B215/04-ticket-review-lane-b.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src edits + docs/brain/B215/ticket-lb1-completion.md
Ph4b ptt-verifier        -> docs/brain/B215/ticket-lb1-verification.md     (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B215/05-final-review-lane-b.md + 06-deferred-backlog.md (FINAL_PASS gate)

SUCCESS CRITERIA:
- Exactly 2 comment lines changed across 2 files (no logic diff).
- SCAN-7 hunk count: CopyEngine.cs = 1 hunk (1 line changed), TradeCopierPanel.cs = 1 hunk (1 line changed).
- dotnet test: Failed/Passed counts unchanged from B214 baseline (24/200/547/771).
- ptt-sync 21/0. Guardian PASS (pre-existing DW-B209-01/02 false positive documented).
- DW-B212-01 marked CLOSED-NO-ACTION in NO-PIPELINE-REPAIRS.md.
- DW-B214-01 marked CLOSED in 06-deferred-backlog.md.

KEY FILES TO READ (Ph1 Architect -- read before designing):
- docs/brain/B214/06-deferred-backlog.md (DW-B212-01 and DW-B214-01 sections)
- src/PropTraderTools/CopyEngine.cs lines 5818-5827 (Trim(Instrument) -- the method with no leader guard)
- src/PropTraderTools/CopyEngine.cs lines 5977-6015 (TrimOneAccount -- confirm NO CancelAllAccountOrders)
- src/PropTraderTools/CopyEngine.cs lines 5829-5848 (Flatten(Instrument) -- the fixed comparator)
- src/PropTraderTools/TradeCopierPanel.cs -- search for TryResolveLeaderAccount to find the CYC comment
- docs/standards/jane-street/RULES_CATALOG.md

---

## LANE C ORCHESTRATOR PROMPT
### (paste into ptt-orchestrator session for Lane C)

---

You are the ptt-orchestrator for B215 Lane C.

BLOCK: B215-LaneC
EPIC: DW-B209-04 -- Spec HTML Update (B209 OCO + GTC guard features)
SOURCE: DW-B209-04 in docs/brain/B214/06-deferred-backlog.md
PRIOR BLOCK: B214 (FINAL_PASS confirmed in docs/brain/NO-PIPELINE-REPAIRS.md line 4326)

CONTEXT:
specs/002-trade-copier-spec.html has not been updated to document the B209 OCO + GTC guard
features, or the B211/B212/B213 fixes, or the B214 Lane B UI changes. This spec must be
updated before SIM R8. The spec is a standalone HTML file -- NO .cs files are touched in this lane.

FEATURES TO DOCUMENT (read NO-PIPELINE-REPAIRS.md for each before writing):

1. B209 OCO guard (HOTFIX-BUG-BE-OCO-REUSE): BeSwapAll now reuses OCO bracket slots correctly.
   Read: docs/brain/NO-PIPELINE-REPAIRS.md section HOTFIX-BUG-BE-OCO-REUSE.

2. B209 GTC guard (HOTFIX-ENTRY-DRAG-DEDUP): Entry drag dedup guard prevents double-entry.
   Read: docs/brain/NO-PIPELINE-REPAIRS.md section HOTFIX-ENTRY-DRAG-DEDUP.

3. B211 Ask/Bid buttons (HOTFIX-B211-ASK-01, HOTFIX-B211-ASKALL-01, HOTFIX-B211-BIDALL-01):
   Ask and Bid buttons now move targets to ask/bid price. Ask-All and Bid-All operate across all
   accounts using direct _leaderAccount + _followerItems loop (not AllAccounts/FindRule).
   Read: docs/brain/NO-PIPELINE-REPAIRS.md sections HOTFIX-B211-ASK-01, HOTFIX-B211-ASKALL-01,
   HOTFIX-B211-BIDALL-01.

4. B212 re-entry guards (HOTFIX-B212-REENTRY-V3, HOTFIX-B212-REENTRY-BETARGET V4,
   HOTFIX-B212-REENTRY-FOLLOWER-V1, HOTFIX-B212-REENTRY-FOLLOWER-V2): Multi-layer follower
   re-entry suppression after BE-All and Flatten-All operations.
   Read: relevant sections in docs/brain/NO-PIPELINE-REPAIRS.md.

5. B213 multi-chart instrument fix (HOTFIX-B213-MULTI-CHART-INSTRUMENT-01):
   TryResolveLeaderAccount now matches by instrument FullName across charts.
   Read: docs/brain/NO-PIPELINE-REPAIRS.md section HOTFIX-B213-MULTI-CHART-INSTRUMENT-01.

6. B214 Lane B UI changes:
   - Button labels: "Be All", "Ask-All", "Bid-All" (title case, not ALL-CAPS).
   - Buffer numbers show "t" tick suffix (e.g. "+5t", "-3t"); zero buffer shows label only.
   - BE All buffer cap raised from 10 to 50 ticks.
   Read: docs/brain/B214/05-final-review-lane-b.md.

SCOPE CONSTRAINTS (HARD LIMITS):
- Touch ONLY specs/002-trade-copier-spec.html. Zero .cs files. Zero test files.
- Preserve all existing spec sections. Add new blocks; do not remove or rewrite existing ones.
- Use the same HTML structure/style already present in the spec file.
- ASCII-only content. No emoji. No curly quotes.

PIPELINE CHAIN (MANDATORY -- ALL PHASES -- NONE SKIPPABLE):
Ph1  ptt-architect       -> docs/brain/B215/02-architecture-plan-lane-c.md
Ph2  ptt-plan-reviewer   -> docs/brain/B215/02-plan-review-lane-c.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B215/04-tickets-lane-c.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B215/04-ticket-review-lane-c.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> spec edits + docs/brain/B215/ticket-lc1-completion.md
Ph4b ptt-verifier        -> docs/brain/B215/ticket-lc1-verification.md     (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B215/05-final-review-lane-c.md + 06-deferred-backlog.md (FINAL_PASS gate)

SUCCESS CRITERIA:
- All 6 feature groups documented in spec with correct section headings and descriptions.
- HTML validates (no unclosed tags, no broken structure).
- Zero .cs files touched (SCAN-1: confirm git diff shows only .html changes).
- DW-B209-04 marked CLOSED in 06-deferred-backlog.md.
- 05-final-review-lane-c.md and 06-deferred-backlog.md produced.

KEY FILES TO READ (Ph1 Architect -- read before designing):
- specs/002-trade-copier-spec.html (map the existing structure -- where to insert new blocks)
- docs/brain/NO-PIPELINE-REPAIRS.md (read each feature section listed above)
- docs/brain/B214/05-final-review-lane-b.md (Lane B UI changes summary)
- docs/brain/B214/06-deferred-backlog.md (DW-B209-04 item)

---

## SEQUENCING RECOMMENDATION

1. Director applies PRE-1, PRE-2, PRE-3 manually in NT8 project folder.
2. Director runs `dotnet test` to confirm manual changes pass.
3. Director runs all three lanes in order: B first (minimal, ~2 phases), then A and C concurrently.
4. Each lane is independent -- no inter-lane dependencies.
5. B215 FINAL_PASS requires all three lanes at FINAL_PASS.

DO NOT RUN PIPELINE UNTIL DIRECTOR SAYS "run pipeline".
