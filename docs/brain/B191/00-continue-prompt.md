# B191 $continue Prompt
# Paste this entire block into a new session AFTER the B191 pipeline is complete.
# Written: post B191-DIRECT commit 365b8779

$continue | PTT-COPIER B191 | Post-Pipeline Session

## Pipeline State

COMPLETE -- B191 T1..T3 all VERIFY_PASS. Ph5 FINAL_PASS. Pipeline committed.

- B190 commit: 65c2b0b2 (B190-T1..T6)
- B191-DIRECT commit: 365b8779 (QAll2t per-account qty fix + scan report)
- B191 pipeline commit: (written during the pipeline session -- confirm SHA here)
- Brain dir: docs/brain/B191/

## What was done in B191

### Root cause (Oct 6 live incident)
PA-01 (5 contracts) received T1=10, T2=10 when QAll2t was clicked because
`PttGlobalQuickExit.Execute(forcedTargets, instrument)` built `forcedTargets`
once from the PAAPEX10 leader panel (20 contracts -> T1=10, T2=10) and passed
that same fixed list to every account in Account.All.
PA-01 got reversed: short 5 -> net long 15 -> flatten exited at market.

### Fixes applied

| Ticket | File | Change |
|--------|------|--------|
| T1 (B191-DIRECT) | PttGlobalQuickExit.cs | perAccTargets = Build2TargetList(pos.Quantity) inside per-account loop |
| T2 | Tests/B191Tests.cs | 5 xUnit [Fact] tests: Build2TargetList arithmetic for 5c, 20c, 1c accounts |
| T3 | PttGlobalQuickExit.cs | Restore followerPosQty from FindPosition in ExecuteFollowers (DW-B187-01) |

### DW items closed by B191
- DW-B191-QTY-01: QAll2t passed leader qty to all accounts (root cause)
- DW-B187-01 (P3): followerPosQty=0 dead path in ResolveFollowerTargets

### DW items carried forward after B191
- DW-B183-03 (P1): IsLaneEEligible CYC=8 frozen -- blocked until Lane E is next modified

## What to do in the new session

### Option A -- Routine continuation
No urgent items are open. B191 is the clean state.
Next work should be Director-directed (new feature, next deferred item, or review).

### Option B -- If DW-B183-03 is in scope (Lane E modification)
Before any Lane E change: extract a helper method from IsLaneEEligible first.
Current CYC=8 is at the JS-066 ceiling. Any new branch without extraction = P0 violation.
Read: src/PropTraderTools/CopyEngine.cs (IsLaneEEligible ~line 2614)
Read: docs/intel/jane-street/complexity-reduction.md

### Option C -- If a new live incident requires direct repair
Follow the Director approval process for direct .cs repairs.
Document the incident, get Director approval, make the minimal fix,
then run the full pipeline to formalize it (as was done for B191-DIRECT).

## Open deferred items (authoritative list after B191)

| DW ID | Priority | Description | When |
|-------|----------|-------------|------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 -- any future Lane E branch needs helper extraction first | When Lane E is next modified |

## Full scan report
docs/brain/B191/scan-report.md -- all 11 Features/ files, all qty-critical
CopyEngine.cs methods, all TradeCopierPanel.cs exit handlers.
Verdict: no other per-account sizing risks. Root cause isolated to QAll2t.

## SRC CODE BAN reminder
All .cs edits go through the FULL 5-PHASE PTT PIPELINE.
Ph1 ptt-architect -> Ph2 ptt-plan-reviewer -> Ph3 ptt-architect ->
Ph3.5 ptt-ticket-reviewer -> Ph4a ptt-engineer ->
Ph4b ptt-verifier -> Ph5 ptt-plan-reviewer.
No shortcuts. Director approval required for any direct .cs edit.
"add it" = spec HTML only. Never src code.

---
*Handoff written: post B191 pipeline complete*
*Brain: docs/brain/B191/*
*Spec: specs/002-trade-copier-spec.html*
