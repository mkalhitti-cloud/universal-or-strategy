# B59-LaneA Post-Pipeline $continue Prompt
# Paste everything inside the triple-backtick block into a fresh copier-spec session
# after ptt-orchestrator B59-LaneA has finished.

---

```
$continue — PTT Trade Copier Director session.

Context: B59-LaneA pipeline has just completed (or I am pasting this to validate it).
Block: B59 — DW-B59-01 Gate 0.5 exit-name guard via IsExitSignalName.

IMMEDIATE TASK — Validate B59-LaneA pipeline output, then give me simple NT8 test instructions.

## STEP 1 — Validate all 7 pipeline completion criteria

Run each check in order. Report PASS or FAIL for each. Stop on first FAIL and tell me what to fix.

CHECK 1 — Brain artifacts exist:
  Read and confirm non-empty:
  - docs/brain/B59-LaneA/02-architecture-plan.md
  - docs/brain/B59-LaneA/02-plan-review.md        (must end with REVIEW_PASS)
  - docs/brain/B59-LaneA/04-tickets.md
  - docs/brain/B59-LaneA/04-ticket-review.md       (must end with TICKET_REVIEW_PASS)
  - docs/brain/B59-LaneA/ticket-1-completion.md    (must contain a git commit hash)
  - docs/brain/B59-LaneA/ticket-1-verification.md  (must end with VERIFY_PASS)
  - docs/brain/B59-LaneA/05-final-review.md
  - docs/brain/B59-LaneA/06-deferred-backlog.md

CHECK 2 — IsExitSignalName helper exists in CopyEngine.cs:
  grep src/PropTraderTools/CopyEngine.cs for "internal static bool IsExitSignalName"
  PASS: exactly 1 hit

CHECK 3 — Gate 0.5 calls the helper (old guard removed):
  grep src/PropTraderTools/CopyEngine.cs for "IsExitSignalName(order.Name)"
  PASS: exactly 1 hit
  grep src/PropTraderTools/CopyEngine.cs for "order.Name != null"
  PASS: 0 hits (old single-condition guard is gone)

CHECK 4 — All 5 exit name cases in IsExitSignalName body:
  Read the IsExitSignalName method body.
  PASS: contains "PTT-", "Close", "Flatten", "Rev", "Exit" — all 5 cases present.
  PASS: first branch is null guard returning false.

CHECK 5 — 7 new tests present in CopyEngineTests.cs:
  grep src/PropTraderTools/CopyEngineTests.cs for "T_B59_0"
  PASS: exactly 7 lines returned (T_B59_01 through T_B59_07)

CHECK 6 — No lock() or throw new introduced:
  grep src/PropTraderTools/CopyEngine.cs for "lock("   -> PASS: 0 hits in IsExitSignalName
  grep src/PropTraderTools/CopyEngine.cs for "throw new" -> PASS: 0 hits in IsExitSignalName

CHECK 7 — Git commit present:
  Run: git log --oneline -5
  PASS: a commit with message containing "B59" and "IsExitSignalName" is visible.
  Also confirm deploy-sync ran (hard links current):
  Run: powershell -File scripts\verify_links.ps1

## STEP 2 — After all 7 checks pass, give me the NT8 live test instructions

Format: plain numbered list, no jargon, copy-paste ready for NT8 platform.

State at session start:
- Workspace: C:\WSGTA\universal-or-strategy (main branch)
- B58 src is clean. B59 adds IsExitSignalName + 7 tests. F5 required to test live.
- Open deferred items: DW-B58-01/02/03 (low priority), DW-B54-01/02 (ATM, future block),
  PRE-EXISTING-01/02/03 (pre-existing debt, unchanged).
- DW-B57-01: CLOSED (CreateOrder+Submit fix confirmed working 2026-08-10).
- DW-B59-01: target of this block.

Rules:
- SRC CODE BAN: do not touch any .cs file
- Workspace: C:\WSGTA\universal-or-strategy (main branch only)
- After any file edits run: powershell -File scripts\verify_links.ps1 -Fix
- NT8 API reference: grep docs/standards/NT8_FULL_REFERENCE.md before any NT8 API question
```
