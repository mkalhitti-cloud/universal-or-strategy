# B60-LaneA — ptt-orchestrator Prompt

**Block**: B60
**Lane**: A (single lane — 2 tickets, both in CopyEngine.cs / CopyEngineTests.cs)
**Defects closed**: DW-B60-01 (leader-close propagation), DW-B59-02 (Rev prefix widening)
**Priority**: P1 + P1
**Date**: 2026-08-10
**Workspace**: C:\WSGTA\universal-or-strategy (main branch)

---

## MISSION BRIEF

B60 closes two P1 defects discovered during B59 live testing:

### DW-B60-01 — Leader manual close does not close follower position (P1)

When the leader closes their position via the Positions tab Close button, Gate 0.5 (B59) correctly
blocks the `"Close"` order from being forwarded as a phantom copy. However, the follower position
remains open. The copier needs to detect leader-flat and issue `PTT-Flatten` to all followers.

**Infrastructure already present — wire-up only:**
- `PositionStateChanged` event fires from `TryFirePositionState` in `OnOrderUpdate` at
  `CopyEngine.cs:938` on every Filled/PartFilled/Cancelled event.
- `Flatten(Account leader, Instrument instrument)` at `CopyEngine.cs:1135` fans out
  `PTT-Flatten` market orders to all follower accounts for an instrument.
- `IsFollowerAccount(Account)` at `CopyEngine.cs:400` guards against follower-triggered recursion.
- `HasOpenPosition(Account, Instrument)` at `CopyEngine.cs:958` checks if account is now flat.

**What needs to be added**: After Gate 0.5 (and before or inside the copy-enabled path), detect
when `e.Order.Account` is a leader for the matched rule instrument AND the leader just went Flat
(`HasOpenPosition` returns false). Then call `Flatten(leaderAccount, instrument)` to close all
followers. Must NOT fire if copy is disabled or if the account is a follower.

### DW-B59-02 — `IsExitSignalName` uses exact `"Rev"` match instead of prefix (P1)

`IsExitSignalName` at `CopyEngine.cs:724` uses `name == "Rev"` (exact equality). The architecture
plan specified `name.StartsWith("Rev", StringComparison.Ordinal)` to catch all NT8 reversal order
names (`"Reversal"`, `"RevLong"`, `"RevShort"`, etc.). Only the exact string `"Rev"` is currently
blocked. Live NT8 reversal orders may use longer names.

**Change**: Replace `if (name == "Rev") return true;` with
`if (name.StartsWith("Rev", StringComparison.Ordinal)) return true;`
Add 3 additional test cases to T_B59_05 (or a new T_B60_XX fact).

---

## SRC CODE BAN — MANDATORY

YOU ARE BANNED FROM EDITING ANY .cs FILE DIRECTLY.
All src/PropTraderTools/*.cs edits MUST go through the FULL 5-PHASE PTT PIPELINE.
THE PIPELINE IS (all phases mandatory — none skippable — none combinable):
  Ph1  ptt-architect       -> docs/brain/B60-LaneA/02-architecture-plan.md
  Ph2  ptt-plan-reviewer   -> docs/brain/B60-LaneA/02-plan-review.md       (REVIEW_PASS gate)
  Ph3  ptt-architect       -> docs/brain/B60-LaneA/04-tickets.md
  Ph3.5 ptt-ticket-reviewer -> docs/brain/B60-LaneA/04-ticket-review.md   (TICKET_REVIEW_PASS gate)
  Ph4a ptt-engineer        -> src .cs edits + docs/brain/B60-LaneA/ticket-1-completion.md
  Ph4b ptt-verifier        -> docs/brain/B60-LaneA/ticket-1-verification.md (VERIFY_PASS gate)
  Ph5  ptt-plan-reviewer   -> docs/brain/B60-LaneA/05-final-review.md + 06-deferred-backlog.md

---

## STATE AT SESSION START

- Workspace: C:\WSGTA\universal-or-strategy (main branch)
- Last commit: fac65246 (B59 — IsExitSignalName + Gate 0.5)
- B59 src is clean and deployed. B60 adds follower-close propagation + Rev prefix fix.
- Open deferred items carried into B60:
  - DW-B60-01: P1 — leader-close does not propagate to follower (THIS BLOCK)
  - DW-B59-02: P1 — Rev exact-match too narrow (THIS BLOCK)
  - DW-B58-01/02/03: P2 — carry-forward, not in B60 scope
  - DW-B54-01: P1 — ATM auto-inject, blocked on StrategyBase, future block
  - PRE-EXISTING-01/02/03: P2 — unchanged

---

## PIPELINE EXECUTION (start here, follow in order)

### PHASE 1 — ptt-architect

Mode: plan
Output file: docs/brain/B60-LaneA/02-architecture-plan.md

Read first (mandatory before writing anything):
1. `read_file("docs/standards/jane-street/RULES_CATALOG.md")` — run JS rules gate
2. `read_file("docs/standards/NT8_FULL_REFERENCE.md")` — check Order.Name, CreateOrder, Account.Flatten
3. `read_file("docs/brain/B60-LaneA/orchestrator-prompt.md")` — this file
4. `read_file("docs/brain/B59-LaneA/06-deferred-backlog.md")` — DW-B60-01 + DW-B59-02 full descriptions
5. `read_file("src/PropTraderTools/CopyEngine.cs", range="600-660")` — OnOrderUpdate + TryFirePositionState
6. `read_file("src/PropTraderTools/CopyEngine.cs", range="720-755")` — IsExitSignalName + Gate 0.5
7. `read_file("src/PropTraderTools/CopyEngine.cs", range="930-970")` — TryFirePositionState + HasOpenPosition
8. `read_file("src/PropTraderTools/CopyEngine.cs", range="1110-1160")` — Flatten(Account, Instrument)
9. `read_file("src/PropTraderTools/CopyEngine.cs", range="395-410")` — IsFollowerAccount

Deliverables in 02-architecture-plan.md:
- Rules Catalog Gate (JS-001, JS-002, JS-021, ASCII, CYC) — PASS/FAIL
- Problem statement for DW-B60-01 and DW-B59-02 separately
- Exact insertion/replacement for each change (file, line, old text, new text)
- CYC analysis for any modified method
- Test plan: test IDs T_B60_01..T_B60_NN for DW-B60-01 tests; T_B60_Rev_01..NN for DW-B59-02 fix
- Diff size estimate (must be < 10,000 chars)
- NT8 API notes (must cite NT8_FULL_REFERENCE.md lines)
- Deferred items section (carry-forward from B59)

---

### PHASE 2 — ptt-plan-reviewer

Mode: plan
Input: docs/brain/B60-LaneA/02-architecture-plan.md
Output file: docs/brain/B60-LaneA/02-plan-review.md

Read live source before reviewing:
- `read_file("src/PropTraderTools/CopyEngine.cs", range="600-660")` — OnOrderUpdate
- `read_file("src/PropTraderTools/CopyEngine.cs", range="720-755")` — IsExitSignalName
- `read_file("src/PropTraderTools/CopyEngine.cs", range="930-970")` — TryFirePositionState

Checklist (all must PASS before REVIEW_PASS):
1. DW-B60-01 wire-up does NOT fire when copy is disabled (Gate 1 respected)
2. DW-B60-01 wire-up does NOT fire when the flattening account is a follower (IsFollowerAccount guard)
3. DW-B60-01 does NOT introduce a second path that could trigger for the same event (dedup)
4. DW-B59-02 replacement is exactly `name.StartsWith("Rev", StringComparison.Ordinal)`
5. All new methods are `internal static` or use only `internal` members (testable without reflection)
6. No `lock()` introduced (JS-021)
7. No `throw new` in hot path (JS-001)
8. All CYC values <= 8
9. ASCII-only in all new string literals
10. 7-scan checklist present in plan
11. deploy-sync / verify_links.ps1 in commit steps
12. Diff estimate <= 10,000 chars

End with: REVIEW_PASS or REVIEW_FAIL (list violations as V-01, V-02...)

---

### PHASE 3 — ptt-architect (ticket generation)

Mode: plan
Input: docs/brain/B60-LaneA/02-architecture-plan.md (must be REVIEW_PASS)
Output file: docs/brain/B60-LaneA/04-tickets.md

Generate tickets. Combine both fixes into one ticket if the diff is small (< 50 lines total);
split into two tickets only if they touch different methods with no interaction.

Each ticket must contain:
- Spec requirement ID (DW-B60-01 or DW-B59-02)
- File + line range
- OLD text (exact, copy from live source)
- NEW text (exact, copy verbatim from plan)
- JS rule constraints
- CYC constraint
- xUnit test bodies (copy verbatim from plan — all IDs T_B60_xx)
- 7-scan checklist
- Commit message: `fix(ptt): B60 -- leader-close propagation + Rev prefix fix [N tests]`
- Verification steps including `powershell -File .\scripts\verify_links.ps1 -Fix`

---

### PHASE 3.5 — ptt-ticket-reviewer

Mode: plan
Input: docs/brain/B60-LaneA/04-tickets.md + live source
Output file: docs/brain/B60-LaneA/04-ticket-review.md

Check every ticket against:
- Traceability to DW-B60-01 or DW-B59-02 (no phantom work)
- OLD text matches live source exactly (read the file, do not trust the plan)
- NEW text is correct and complete
- No lock(), throw new, return null in new code
- xUnit [Fact] only (no NUnit, no MSTest)
- All test method names contain T_B60_
- verify_links.ps1 and correct commit message present
- File routing correct (src/PropTraderTools/ only)

End each ticket verdict with: TICKET_REVIEW_PASS or TICKET_REVIEW_FAIL

---

### PHASE 4a — ptt-engineer

Mode: agent (v12-engineer Bob CLI)
Input: docs/brain/B60-LaneA/04-tickets.md (must be TICKET_REVIEW_PASS)
Output file: docs/brain/B60-LaneA/ticket-1-completion.md

Execute each ticket in order. For each:
1. Read the exact OLD text from live source to confirm it matches the ticket
2. Apply the change using apply_diff (never write_file on .cs files)
3. Run `dotnet build src/PropTraderTools` — must exit 0
4. Run `dotnet test src/PropTraderTools` — must exit 0, all T_B60_ tests pass
5. Run 7 mandatory scans (grep lock(, throw new, order.Name != null at Gate 0.5, T_B60_ count)
6. Copy CopyEngine.cs to NT8 path manually (deploy-sync.ps1 is archived):
   `Copy-Item src\PropTraderTools\CopyEngine.cs "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Force`
7. Verify with `powershell -File .\scripts\verify_links.ps1 -Fix`
8. git add src/PropTraderTools/CopyEngine.cs src/PropTraderTools/CopyEngineTests.cs
9. git commit -m "fix(ptt): B60 -- leader-close propagation + Rev prefix fix [N tests]"
10. Record commit hash in ticket-1-completion.md

---

### PHASE 4b — ptt-verifier

Mode: agent
Input: docs/brain/B60-LaneA/ticket-1-completion.md
Output file: docs/brain/B60-LaneA/ticket-1-verification.md

Run ALL scans independently (never trust Phase 4a self-report):
- SCAN-01: grep `LeaderFlatDispatch\|OnLeaderFlat\|PTT-Flatten` in CopyEngine.cs — confirm new path present
- SCAN-02: grep `IsFollowerAccount` near new flat-detection code — confirm guard present
- SCAN-03: grep `StartsWith.*"Rev"` in IsExitSignalName body — confirm prefix match (not exact)
- SCAN-04: grep `name == "Rev"` — confirm old exact match GONE (0 hits)
- SCAN-05: grep `lock(` in CopyEngine.cs executable code — 0 hits
- SCAN-06: grep `throw new` in CopyEngine.cs — 0 hits
- SCAN-07: grep `T_B60_` in CopyEngineTests.cs — confirm all expected test methods present
- SCAN-08: read new method body — CYC <= 8 verified by counting
- SCAN-09: verify_links.ps1 result — OK, DESYNC=0

End with: VERIFY_PASS or VERIFY_FAIL (list failures)

---

### PHASE 5 — ptt-plan-reviewer (final review)

Mode: plan
Inputs: all prior phase outputs
Output files:
  docs/brain/B60-LaneA/05-final-review.md
  docs/brain/B60-LaneA/06-deferred-backlog.md

Final review checklist:
- DW-B60-01 CLOSED: leader-flat propagates PTT-Flatten to followers
- DW-B59-02 CLOSED: Rev prefix widened to StartsWith
- No new P0/P1 violations introduced
- All verifier scans PASS
- Commit hash confirmed in completion report
- NT8 API discoveries documented (if any)
- Carry-forward items from B59 correctly populated in 06-deferred-backlog.md
  (DW-B58-01, DW-B58-02, DW-B58-03, DW-B54-01, PRE-EXISTING-01/02/03)
- Any new deferred items from B60 documented with IDs DW-B60-XX

End 05-final-review.md with: FINAL_PASS or FINAL_FAIL

---

## RULES (enforced at every phase)

- SRC CODE BAN: no .cs edits outside Phase 4a
- JS-001: no throw in hot path
- JS-002: no return null (bool methods only)
- JS-021: no lock()
- CYC <= 8 for every method touched
- ASCII-only in all new string literals
- xUnit [Fact] only — never NUnit, never MSTest
- internal static for all new testable helpers
- deploy-sync.ps1 is ARCHIVED — use manual copy + verify_links.ps1 -Fix
- NT8 API reference: grep docs/standards/NT8_FULL_REFERENCE.md before any NT8 API claim
- Workspace: C:\WSGTA\universal-or-strategy (main branch only)
