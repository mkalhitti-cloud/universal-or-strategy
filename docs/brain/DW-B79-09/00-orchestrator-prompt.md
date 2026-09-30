# DW-B79-09 — ptt-orchestrator Prompt

**Paste the block below into a ptt-orchestrator session to execute DW-B79-09.**

---

```
PTT-COPIER DW-B79-09 — ptt-orchestrator

You are the ptt-orchestrator for pipeline DW-B79-09.
SRC CODE BAN: Do NOT edit any .cs file directly.
All src edits go through the full 5-phase PTT pipeline only.
No phases may be skipped, combined, or abbreviated.

════════════════════════════════════════════════════════════════
PIPELINE: DW-B79-09
Title:    RemoveAll race guard uniform application
          (CancelQxBrackets ×2 + CancelStaleBracketsLocal)
Priority: P3 — cosmetic uniformity
Ticket:   1 ticket (DW-B79-09-TICKET-1)
Brain:    docs/brain/DW-B79-09/
Spec:     specs/002-trade-copier-spec.html  section-b79 (DW-B79-09 card)
HEAD:     5925b618
════════════════════════════════════════════════════════════════

CONTEXT:
DW-B79-04 added a RemoveAll(Filled || Cancelled) race guard to CancelAllAccountOrders.
The same guard was not applied to three unguarded cancel methods.
This pipeline adds the identical one-line guard to each.

THREE FIX TARGETS:
  1. CopyEngine.cs ~L630:
     CancelQxBrackets (2-param) — insert before: try { acc.Cancel(stale.ToArray()); }
  2. CopyEngine.cs ~L702:
     CancelQxBrackets (3-param) — insert before: try { acc.Cancel(stale.ToArray()); }
  3. PttBreakEven.cs ~L193:
     CancelStaleBracketsLocal — insert before: acc.Cancel(stale.ToArray()); inside try

THE ONE-LINER (identical for all three):
  stale.RemoveAll(o => o.OrderState == OrderState.Filled
                    || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard

CYC IMPACT: +0 per method (RemoveAll is not a branch)
TEST DELTA: 292 → 295 (+3 [Fact])
FILES:      CopyEngine.cs (×2) + PttBreakEven.cs (×1) + CopyEngineTests.cs (+3 tests)

════════════════════════════════════════════════════════════════
MANDATORY PIPELINE — ALL 7 PHASES, NONE SKIPPABLE:

Ph1  ptt-architect
     → Read: docs/brain/DW-B79-09/02-architecture-plan.md
             src/PropTraderTools/CopyEngine.cs L605-704
             src/PropTraderTools/Features/PttBreakEven.cs L165-199
     → Output: docs/brain/DW-B79-09/02-architecture-plan.md (confirm/update)
     → Gate: plan written

Ph2  ptt-plan-reviewer
     → Input: docs/brain/DW-B79-09/02-architecture-plan.md
     → Output: docs/brain/DW-B79-09/02-plan-review.md
     → Gate: REVIEW_PASS required — DO NOT proceed to Ph3 until REVIEW_PASS

Ph3  ptt-architect
     → Input: docs/brain/DW-B79-09/02-plan-review.md
     → Output: docs/brain/DW-B79-09/04-tickets.md
     → Ticket content:
         TICKET-1 (DW-B79-09-TICKET-1):
           Edit CopyEngine.cs — insert RemoveAll before acc.Cancel in 2-param CancelQxBrackets
           Edit CopyEngine.cs — insert RemoveAll before acc.Cancel in 3-param CancelQxBrackets
           Edit PttBreakEven.cs — insert RemoveAll before acc.Cancel in CancelStaleBracketsLocal
           Edit CopyEngineTests.cs — add 3 [Fact] tests (T_DW_B79_09_01/02/03)
     → Gate: tickets written

Ph3.5  ptt-ticket-reviewer
     → Input: docs/brain/DW-B79-09/04-tickets.md
     → Output: docs/brain/DW-B79-09/04-ticket-review.md
     → Gate: TICKET_REVIEW_PASS required — DO NOT proceed to Ph4a until TICKET_REVIEW_PASS

Ph4a  ptt-engineer
     → Input: docs/brain/DW-B79-09/04-tickets.md (TICKET-1)
     → Execute ALL edits via apply_diff / search_and_replace — no write_file for existing files
     → Run after edits: dotnet build && dotnet test
     → Output: docs/brain/DW-B79-09/ticket-1-completion.md
     → Gate: BUILD_PASS + test count = 295

Ph4b  ptt-verifier
     → Input: docs/brain/DW-B79-09/ticket-1-completion.md
     → Run 7-scan independently:
         grep -r "lock(" src/ --include="*.cs"
         grep -rn "async void " src/ --include="*.cs"
         grep -rn "return null;" src/ --include="*.cs"
         python scripts/complexity_audit.py
         dotnet build
         dotnet test
         dotnet csharpier check src/
     → Output: docs/brain/DW-B79-09/ticket-1-verification.md
     → Gate: VERIFY_PASS required — all 7 scans zero, [Fact] = 295

Ph5  ptt-plan-reviewer
     → Input: docs/brain/DW-B79-09/ticket-1-verification.md
     → Output: docs/brain/DW-B79-09/05-final-review.md
              docs/brain/DW-B79-09/06-deferred-backlog.md
     → Gate: FINAL_PASS required
     → On FINAL_PASS: commit with message:
         "fix(ptt): DW-B79-09 RemoveAll race guard CancelQxBrackets×2+CancelStaleBracketsLocal [295 tests]"
     → Run: powershell -File .\deploy-sync.ps1

════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA (Ph4b verifier checks all):

  [ ] CancelQxBrackets 2-param: RemoveAll line present before acc.Cancel (CopyEngine.cs)
  [ ] CancelQxBrackets 3-param: RemoveAll line present before acc.Cancel (CopyEngine.cs)
  [ ] CancelStaleBracketsLocal: RemoveAll line present before acc.Cancel (PttBreakEven.cs)
  [ ] CYC unchanged: 6 / 7 / 6 for the three methods respectively
  [ ] [Fact] count = 295 (was 292, +3)
  [ ] dotnet build — 0 errors
  [ ] dotnet test — 295/295 PASS
  [ ] 7-scan — all zero (lock, async-void, return-null, new-array, CYC, JS P0, ASCII)
  [ ] deploy-sync.ps1 — PASS
  [ ] F5 NinjaTrader — GREEN (Director confirmation before close)

════════════════════════════════════════════════════════════════
DO NOT:
  - Edit any .cs file before Ph4a is reached
  - Skip or combine any phase
  - Treat this as a "small fix" exempt from the pipeline
  - Use write_file for existing .cs files (use apply_diff or search_and_replace)
════════════════════════════════════════════════════════════════
```
