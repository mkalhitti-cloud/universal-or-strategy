# B191 Pipeline — ptt-orchestrator Launch Prompt
# Paste this entire block into a ptt-orchestrator session to run the full pipeline.
# Written: post-B191-DIRECT commit 365b8779

$launch | PTT-COPIER B191 | QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close

## Context

B191-DIRECT repair was committed (365b8779) under Director approval.
Code is live. Build is clean. ptt-sync: 19 linked, 0 errors.

This pipeline runs the FULL 5-PHASE PTT PIPELINE retroactively to:
1. Produce formal architecture artefacts for the already-committed fix (T1)
2. Add test coverage for the per-account qty path (T2 -- new B191Tests.cs)
3. Close DW-B187-01 by restoring followerPosQty in ExecuteFollowers (T3 -- targeted .cs edit)

Brain dir: docs/brain/B191/
Baseline commit: 65c2b0b2 (B190-T1..T6)
Fix commit: 365b8779 (B191-DIRECT)

---

## The FULL 5-PHASE PIPELINE (all phases mandatory -- none skippable -- none combinable)

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md (FINAL_PASS gate)

---

## Tickets (3)

### T1 -- Formalize B191-DIRECT (code already committed)
- File: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- Change: lines 177-181 -- `perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity)`
  built inside per-account loop, replacing pass-through of leader-built `forcedTargets`.
- NO new code needed for T1. Ph4a writes ticket-1-completion.md confirming the
  committed change is correct and intentional. Ph4b verifies scans + build.
- DW closed by T1: DW-B191-QTY-01 (new -- the root cause item from the Oct 6 live incident)

### T2 -- B191Tests.cs (new test file)
- File: src/PropTraderTools/Tests/B191Tests.cs (NEW)
- Tests (all xUnit [Fact], no NT8 runtime, no Skip):
  T2-1: Build2TargetList(5)  -> T1=3, T2=2  (confirms 5-contract PA exit qty is correct)
  T2-2: Build2TargetList(20) -> T1=10, T2=10 (confirms 20-contract leader target is different)
  T2-3: Build2TargetList(5).Sum(t => t.Qty) == 5 (sum invariant -- no qty lost)
  T2-4: Execute(List, Instrument) overload exists as internal method (reflection structural check)
  T2-5: Build2TargetList(1)  -> T1=1, T2=0  (boundary: single-contract account)
- dotnet test must pass all 5 facts after T2.

### T3 -- Restore followerPosQty in ExecuteFollowers (closes DW-B187-01)
- File: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- Change: in `ExecuteFollowers`, replace hardcoded `0` at
  `ResolveFollowerTargets(followerTargets, targets, 0, pos.Quantity)`
  with:
  `int followerPosQty = CopyEngine.Instance?.FindPosition(follower, pos.Instrument)?.Quantity ?? 0;`
  then pass `followerPosQty` as arg 3.
- Comment: // B191-T3: restore followerPosQty from live position (DW-B187-01 fix).
  // With followerPosQty>0, ScaleLeaderTargets fires when follower has no snapshot.
  // Zero fallback preserved: flat follower -> ScaleLeaderTargets skipped -> empty list correct.
- CYC impact: local variable assignment = 0 McCabe branches. CYC of ExecuteFollowers unchanged at 4.
- DW closed by T3: DW-B187-01 (P3, carried since B187)

---

## What each phase must produce

### Ph1 (ptt-architect) -- 02-architecture-plan.md
Read files before writing:
- src/PropTraderTools/Features/PttGlobalQuickExit.cs (full -- especially Execute lines 114-185,
  ExecuteFollowers lines 194-243, ResolveFollowerTargets lines 537-559)
- src/PropTraderTools/TradeCopierPanel.cs (Build2TargetList method ~line 1241,
  TryResolve2TargetContext ~line 1685, OnInstrQAll2tClick ~line 1743)
- docs/brain/B191/scan-report.md
- docs/brain/B190/06-deferred-backlog.md (DW-B187-01 full text)

Write docs/brain/B191/02-architecture-plan.md containing:
1. Root cause description (forcedTargets shared across accounts)
2. Fix rationale for T1 (perAccTargets per-account loop)
3. Test coverage strategy for T2 (pure arithmetic, no NT8 runtime)
4. Fix rationale for T3 (restore followerPosQty, CYC unchanged)
5. DNA audit: lock() absent, async void absent, CYC impact analysis, ASCII-only
6. Risk: T3 changes behavior when follower has no snapshot AND has a non-flat position.
   Was: ScaleLeaderTargets skipped (dead). After T3: ScaleLeaderTargets fires with real qty.
   This is the CORRECT behavior. Regression risk: LOW (path was dead before T3).

### Ph2 (ptt-plan-reviewer) -- 02-plan-review.md
Gate: REVIEW_PASS or REVIEW_FAIL with blocking issues listed.
Check:
- T1 code already committed -- verify plan matches the committed change exactly
- T2 tests are pure arithmetic (no NT8 runtime required, no Skip needed)
- T3 CYC impact is 0 (variable assignment is not a branch)
- T3 null chain `?.` does not introduce throw (JS-001 compliant)
- No lock() introduced anywhere
- No async void introduced anywhere

### Ph3 (ptt-architect) -- 04-tickets.md
Write 3 tickets (T1, T2, T3) with:
- AC (acceptance criteria) per ticket
- Files affected
- Test oracle for T2 (exact expected values)
- CYC before/after for T3

### Ph3.5 (ptt-ticket-reviewer) -- 04-ticket-review.md
Gate: TICKET_REVIEW_PASS or TICKET_REVIEW_FAIL.
Check:
- T1 AC matches committed diff
- T2 test oracles are arithmetically correct (Build2TargetList(5): T1=ceil(5/2)=3, T2=5-3=2)
- T3 change is minimal (1 new line, 1 changed line)
- All 3 tickets have clear VERIFY criteria

### Ph4a (ptt-engineer) per ticket

T1:
- No .cs edit (code is committed). Write docs/brain/B191/ticket-1-completion.md.
- Completion note must cite commit 365b8779, diff lines 177-181, CYC=8 unchanged.

T2:
- Create src/PropTraderTools/Tests/B191Tests.cs with 5 [Fact] tests (see T2 spec above).
- Run: dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B191"
- Expected: 5 passed, 0 failed, 0 skipped.
- Write docs/brain/B191/ticket-2-completion.md.

T3:
- Edit src/PropTraderTools/Features/PttGlobalQuickExit.cs: ExecuteFollowers method.
  Add local: `int followerPosQty = CopyEngine.Instance?.FindPosition(follower, pos.Instrument)?.Quantity ?? 0;`
  Change: `ResolveFollowerTargets(followerTargets, targets, 0, pos.Quantity)`
       to `ResolveFollowerTargets(followerTargets, targets, followerPosQty, pos.Quantity)`
- Run: dotnet build -> 0 errors, 0 warnings.
- Run: dotnet test --filter B116 -> all existing ResolveFollowerTargets tests still pass.
- Run: powershell -File .\scripts\ptt-sync.ps1 -> 19 linked, 0 errors.
- Write docs/brain/B191/ticket-3-completion.md.

### Ph4b (ptt-verifier) per ticket

T1 VERIFY:
- Confirm git show 365b8779 -- src/PropTraderTools/Features/PttGlobalQuickExit.cs shows
  `var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity);` at line 180.
- Confirm `ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop);` at line 182.
- Confirm build: 0 errors, 0 warnings.
- SCAN-01: Select-String -Pattern "lock\(" PttGlobalQuickExit.cs -> 0 results.
- SCAN-02: Select-String -Pattern "async void " PttGlobalQuickExit.cs -> 0 results.
- Write docs/brain/B191/ticket-1-verification.md (VERIFY_PASS or VERIFY_FAIL).

T2 VERIFY:
- Confirm B191Tests.cs exists at src/PropTraderTools/Tests/B191Tests.cs.
- Confirm 5 [Fact] are present, none have Skip attribute.
- Run: dotnet test --filter B191 -> 5 passed.
- Write docs/brain/B191/ticket-2-verification.md (VERIFY_PASS or VERIFY_FAIL).

T3 VERIFY:
- Confirm ExecuteFollowers at PttGlobalQuickExit.cs has:
  `int followerPosQty = CopyEngine.Instance?.FindPosition(follower, pos.Instrument)?.Quantity ?? 0;`
  and passes `followerPosQty` (not literal 0) to ResolveFollowerTargets.
- CYC check: ExecuteFollowers CYC still <= 4 (no new branches added).
- Run: dotnet build -> 0 errors, 0 warnings.
- Run: dotnet test --filter B116 -> all pass.
- Run: powershell -File .\scripts\ptt-sync.ps1 -> 19 linked, 0 errors.
- SCAN-01: Select-String -Pattern "lock\(" PttGlobalQuickExit.cs -> 0 results.
- Write docs/brain/B191/ticket-3-verification.md (VERIFY_PASS or VERIFY_FAIL).

### Ph5 (ptt-plan-reviewer) -- 05-final-review.md + 06-deferred-backlog.md

Read all 3 ticket-N-verification.md files.
Run full DNA scan on touched files:
- SCAN-01: lock() in PttGlobalQuickExit.cs -> 0
- SCAN-02: async void in PttGlobalQuickExit.cs -> 0
- SCAN-03: Build2TargetList(pos.Quantity) present at Execute(List,Instrument):180
- SCAN-04: followerPosQty sourced from FindPosition at ExecuteFollowers
- SCAN-05: dotnet build 0E 0W
- SCAN-06: dotnet test --filter "B191|B116|B120|B123|B128" all pass
- SCAN-07: ptt-sync 19 linked 0 errors

DW items closed by B191:
- DW-B191-QTY-01 (new): QAll2t passed leader qty to all accounts. Closed by T1.
- DW-B187-01 (P3): followerPosQty=0 dead path in ResolveFollowerTargets. Closed by T3.

DW items carried forward (must appear in 06-deferred-backlog.md):
- DW-B183-03 (P1): IsLaneEEligible CYC=8 frozen. Blocked until Lane E is next modified.

Write docs/brain/B191/05-final-review.md (FINAL_PASS gate).
Write docs/brain/B191/06-deferred-backlog.md (supersedes B190/06-deferred-backlog.md).

---

## Post-pipeline steps (after FINAL_PASS)

1. git add -A
2. git commit -m "B191-T1..T3 -- formalize QAll2t qty fix + tests + DW-B187-01 close"
3. powershell -File .\scripts\ptt-sync.ps1  (confirm 19 linked, 0 errors)
4. Tell Director: "B191 pipeline complete. Compile in NinjaTrader: Tools -> Edit NinjaScript -> Compile (F5). Expected: 0 errors, 0 warnings."

---

## NT8 compile + test (after Director compiles)

T1/T3 fix verification in NinjaTrader:
Open a chart where PA accounts have different contract sizes than PAAPEX10.
Click QAll2t:
- PAAPEX10 (20c): should receive T1=10, T2=10
- PA-01 (5c): should receive T1=3, T2=2
- No account should receive targets sized for a different account.
Output tab: each account logs "[PTT-QX-2T-ALL] follower: <name> leaderTargets=2"
with the per-account perAccTargets quantities in the ExecuteOne call.

---

*Written: post B191-DIRECT commit 365b8779*
*Brain dir: docs/brain/B191/*
