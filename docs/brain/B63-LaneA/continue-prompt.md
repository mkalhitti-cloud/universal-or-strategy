# $continue Prompt — B63 + B62 Post-Pipeline Validation

Paste this into a new PTT Trade Copier Director session AFTER both B63-LaneA and B62-LaneA
pipelines have completed (all 8 brain artifacts present for each, FINAL_PASS on both).

---

$continue — PTT Trade Copier Director session.

Context: B63-LaneA and B62-LaneA pipelines have just completed.
B63 = Gate B bracket state gap fix (IsWorkingBracket adds Accepted state).
B62 = Live entry drag sync + price-keyed dedup (HandleEntryChange + EvictDedup + Gate C).

IMMEDIATE TASK — Validate both pipelines, then give me live NT8 test steps.

## STEP 1 — Validate B63 pipeline (7 checks)

CHECK B63-1 — Brain artifacts exist and are complete:
  Read and confirm non-empty:
  - docs/brain/B63-LaneA/02-architecture-plan.md
  - docs/brain/B63-LaneA/02-plan-review.md        (must end with REVIEW_PASS)
  - docs/brain/B63-LaneA/04-tickets.md
  - docs/brain/B63-LaneA/04-ticket-review.md       (must end with TICKET_REVIEW_PASS)
  - docs/brain/B63-LaneA/ticket-1-completion.md    (must contain a git commit hash)
  - docs/brain/B63-LaneA/ticket-1-verification.md  (must end with VERIFY_PASS)
  - docs/brain/B63-LaneA/05-final-review.md
  - docs/brain/B63-LaneA/06-deferred-backlog.md

CHECK B63-2 — IsWorkingBracket now accepts Accepted state:
  grep src/PropTraderTools/CopyEngine.cs for "OrderState.Accepted"
  PASS: at least 1 hit inside the IsWorkingBracket method body.

CHECK B63-3 — IsWorkingBracket is internal (testable):
  grep src/PropTraderTools/CopyEngine.cs for "internal static bool IsWorkingBracket"
  PASS: exactly 1 hit.

CHECK B63-4 — 4 new tests present:
  grep src/PropTraderTools/CopyEngineTests.cs for "T_B63_0"
  PASS: exactly 4 lines (T_B63_01 through T_B63_04).

CHECK B63-5 — No lock() or throw new in modified code:
  grep src/PropTraderTools/CopyEngine.cs for "lock("    -> PASS: 0 hits in IsWorkingBracket area
  grep src/PropTraderTools/CopyEngine.cs for "throw new" -> PASS: 0 hits in IsWorkingBracket area

CHECK B63-6 — Git commit present:
  Run: git log --oneline -8
  PASS: a commit with message containing "B63" and "IsWorkingBracket" or "bracket" is visible.

CHECK B63-7 — verify_links.ps1 ran (hard links current):
  Run: powershell -File scripts\verify_links.ps1

## STEP 2 — Validate B62 pipeline (8 checks)

CHECK B62-1 — Brain artifacts exist and are complete:
  Read and confirm non-empty:
  - docs/brain/B62-LaneA/02-architecture-plan.md
  - docs/brain/B62-LaneA/02-plan-review.md        (must end with REVIEW_PASS)
  - docs/brain/B62-LaneA/04-tickets.md
  - docs/brain/B62-LaneA/04-ticket-review.md       (must end with TICKET_REVIEW_PASS)
  - docs/brain/B62-LaneA/ticket-1-completion.md    (must contain a git commit hash)
  - docs/brain/B62-LaneA/ticket-1-verification.md  (must end with VERIFY_PASS)
  - docs/brain/B62-LaneA/05-final-review.md
  - docs/brain/B62-LaneA/06-deferred-backlog.md

CHECK B62-2 — _dedupCache is now ConcurrentDictionary<string, double>:
  grep src/PropTraderTools/CopyEngine.cs for "ConcurrentDictionary<string, double>"
  PASS: exactly 1 hit (_dedupCache field).
  grep src/PropTraderTools/CopyEngine.cs for "ConcurrentDictionary<string, long>"
  PASS: 0 hits (old long type removed).

CHECK B62-3 — IsDedup now takes 2 params (orderId + limitPrice):
  grep src/PropTraderTools/CopyEngine.cs for "private bool IsDedup(string orderId, double limitPrice)"
  PASS: exactly 1 hit.

CHECK B62-4 — EvictDedup exists as internal method:
  grep src/PropTraderTools/CopyEngine.cs for "internal void EvictDedup"
  PASS: exactly 1 hit.

CHECK B62-5 — Gate C and HandleEntryChange exist:
  grep src/PropTraderTools/CopyEngine.cs for "Gate C"
  PASS: exactly 1 hit.
  grep src/PropTraderTools/CopyEngine.cs for "private void HandleEntryChange"
  PASS: exactly 1 hit.
  grep src/PropTraderTools/CopyEngine.cs for "FindFollowerEntryOrder"
  PASS: exactly 2 hits (definition + call in HandleEntryChange).

CHECK B62-6 — 5 new tests present:
  grep src/PropTraderTools/CopyEngineTests.cs for "T_B62_0"
  PASS: exactly 5 lines (T_B62_01 through T_B62_05).

CHECK B62-7 — No lock() or throw new introduced:
  grep src/PropTraderTools/CopyEngine.cs for "lock("    -> PASS: 0 hits in B62 methods
  grep src/PropTraderTools/CopyEngine.cs for "throw new" -> PASS: 0 hits in B62 methods

CHECK B62-8 — Git commit present:
  Run: git log --oneline -8
  PASS: a commit with message containing "B62" and "drag" or "dedup" is visible.

## STEP 3 — After all checks PASS, give me NT8 live test instructions

Format: plain numbered steps, no jargon, easy to follow in NT8.

Cover these 4 scenarios in order:
1. BRACKET LEAK FIX (B63): Place a leader limit order with ATM "MES $200 SL4".
   Expected: follower gets exactly 1 PTT-Copy entry order. NO extra sell limit orders appear.
2. DRAG SYNC (B62): With a leader working limit entry, drag it to a new price in Chart Trader.
   Expected: follower PTT-Copy entry order moves to the same price instantly.
3. CANCEL PROPAGATION (regression): Cancel the leader entry before fill.
   Expected: follower PTT-Copy also cancels.
4. MARKET ORDER (regression): Place a leader market order.
   Expected: follower PTT-Copy market order fills. No spurious bracket copies.

State at session start:
- Workspace: C:\WSGTA\universal-or-strategy (main branch)
- B59/B60/B61/B63/B62 all complete and F5 deployed.
- Open deferred items: DW-B54-01 (ATM inject, blocked), DW-B58-01/02/03 (P2).
- Total tests added B59-B63+B62: 19+ new [Fact] tests.

Rules:
- SRC CODE BAN: do not touch any .cs file
- Workspace: C:\WSGTA\universal-or-strategy (main branch only)
- After any file edits run: powershell -File scripts\verify_links.ps1 -Fix
- NT8 API reference: grep docs/standards/NT8_FULL_REFERENCE.md before any NT8 API question
