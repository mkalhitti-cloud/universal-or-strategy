# B209 Deferred Backlog

## Block: B209 — BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)
## Date: 2026-07-07
## Status: PIPELINE_COMPLETE pending SIM Round 7

### Completed Items (closed by B209)

- CLOSED: Permanent `_beSwapInProgress` flag lock on OCO cancel path (FIX-G-1).
  `TryClearBeSwapOnStopWorking` now clears on Working | Cancelled | Rejected | Accepted.
  Commits: a93e222775c4dbd0f1e858238655ac46ad9ddae8 (Ticket-1).

- CLOSED: Premature GTC sweep of live PTT-BE-Stop-N during active BE-swap window (FIX-G-2).
  `TryCancelSingleStaleGtcExit` now guards PTT-BE-* orders when `IsBeSwapInProgress` is true.
  Commits: ff3c2d70 (Ticket-2).

No open B208/B209 deferred items remain related to BE-swap flag management.

---

### Deferred Items

#### DW-B209-01: Guardian script improvement — single-hunk false positive
- **Issue**: `verify_critical_symbols.ps1` exits non-zero on Ticket-1 commit (a93e2227)
  because the hunk count equals 1. The script check `$hunkCount -eq 1` fires the SINGLE-HUNK
  BAN error and concludes "full-file replace" — but the Ticket-1 diff is 51 total lines scoped
  to `@@ -3151,23 +3151,41 @@` in a 9000+ line file. The two adjacent surgical changes
  (TryClearBeSwapOnStopWorking body replacement + IsBeClearStateTrigger insertion) were merged
  into one hunk by git's 3-line context window. This is not a full-file replace.
- **Root cause**: Script cannot distinguish hunk-count=1 due to full-file replace (9000+ diff
  lines, hunk starts at line 1) from hunk-count=1 due to adjacent surgical changes merged by
  git context (< 300 diff lines, hunk starts mid-file).
- **Recommended fix**: Add diff-line-count check (e.g., if total diff lines < 300, not a
  full-file replace) AND/OR hunk-start-line check (if `@@ -1,` pattern absent, not full-file)
  before firing the ban.
- **Priority**: Low (false positive only — not a safety issue; actual full-file replaces still
  detected correctly because they produce thousands of diff lines)
- **Blocks**: Nothing immediate. Guardian still correctly enforces symbol presence and deletion audit.
- **Target Block**: B210 or future maintenance cycle

---

#### DW-B209-02: TradeCopierPanel.cs BrushTeal pre-existing gap
- **Issue**: `verify_critical_symbols.ps1` reports a persistent failure:
  `TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`
- **Root cause**: The Quick2t visual fix (`_instr2tBtn.Background = BrushTeal`) is missing from
  `TradeCopierPanel.cs`. This gap pre-existed before B209. Confirmed: both B209 commits
  (a93e2227, ff3c2d70) show 0 lines changed in TradeCopierPanel.cs per `git diff` analysis
  in ticket-1-verification.md and ticket-2-verification.md.
- **Action**: Director to investigate and resolve in a future block. Likely requires a dedicated
  ticket to restore or implement the Quick2t visual fix in TradeCopierPanel.cs.
- **Priority**: Low (pre-existing, not a regression, not introduced by B209)
- **Blocks**: Guardian script exits non-zero on every CopyEngine.cs commit until resolved.
  This is a persistent noise source in the guardian output.
- **Target Block**: Future block (investigate Quick2t visual fix history)

---

#### DW-B209-03: SIM Round 7 required (HIGH PRIORITY — blocks PROD)
- **Issue**: FIX-G-1 and FIX-G-2 are applied and committed (ptt-sync: 19 linked, 0 errors on
  both commits). The code changes are correct per architectural review, ticket review, engineer
  build, and independent verifier review. However, SIM validation has not yet been run with the
  new code in a live NT8 SIM environment.
- **Action**: Run SIM Round 7 with the following verification scenarios:
  1. **OCO path (FIX-G-1)**: Execute a BE swap. Fill the PTT-BE-Target-N (scratch exit).
     Verify PTT-BE-Stop-N is cancelled by NT8 via OCO. Verify `IsBeSwapInProgress(accName)`
     returns `false` after the Cancelled transition. Verify NakedPositionDetector fires PTT-Flatten
     normally on the subsequent trade (flag is not permanently stuck).
  2. **GTC sweep guard (FIX-G-2)**: Execute a BE swap. While PTT-BE-Stop-N is Working and
     `_beSwapInProgress` is true, trigger a rapid PTT-Copy/Entry fill on the same follower account.
     Verify that `TryCancelSingleStaleGtcExit` does NOT cancel PTT-BE-Stop-N. Verify the stop
     survives the GTC sweep.
  3. **Non-BE order sweep (regression guard)**: Verify PTT-QX-* orders are still swept normally
     (the FIX-G-2 guard only protects PTT-BE-* orders). Stale PTT-QX-* must still be cancelled.
- **Priority**: HIGH — required before PROD deployment of B209 changes.
- **Blocks**: PROD deployment of B209 commits (a93e2227, ff3c2d70).
- **Target Block**: B210 / SIM Round 7 session

---

#### DW-B209-04: specs/002-trade-copier-spec.html updates deferred
- **Issue**: The orchestrator prompt (B209-orchestrator-prompt.md, lines 251-257) and the
  architecture plan (section "Spec Update Requirements (Ph5)") require three updates to
  `specs/002-trade-copier-spec.html`:
  (a) In the B208 block, mark FIX-G-1 + FIX-G-2 as APPLIED, including the ptt-sync result
      (19 linked, 0 errors).
  (b) Update the B208 card title from "FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL" to
      "FIX-G APPLIED — AWAITING SIM ROUND 7".
  (c) Update the B208 bug table row status column to reflect both fixes applied.
- **Root cause**: `specs/002-trade-copier-spec.html` does not exist in this workspace
  (`c:\WSGTA\universal-or-strategy`). The `specs/` directory is absent. The file likely
  resides in the NinjaTrader AddOn workspace or a separate spec repository.
  `glob("specs/**")` returned no files.
- **Action**: Director or a dedicated spec-update agent must apply all three updates in the
  correct workspace before marking B209 COMPLETE for external tracking purposes.
- **Priority**: Medium (tracking artifact — does not affect code correctness or SIM validation)
- **Blocks**: External tracking accuracy. SIM Round 7 prerequisite for PROD.
- **Target Block**: Before or during SIM Round 7 session

---

### Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B210/future | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal pre-existing gap | P2 | Future | OPEN |
| DW-B209-03 | SIM Round 7 — verify OCO path + GTC sweep guard in live SIM | P0 | B210/SIM R7 | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html three updates (APPLIED, title, bug row) | P1 | Before SIM R7 | OPEN |
