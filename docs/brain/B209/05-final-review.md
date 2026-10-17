# B209 Final Review
# PTT-COPIER-B209: BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-07
**Block**: B209
**Commits reviewed**: a93e222775c4dbd0f1e858238655ac46ad9ddae8 (Ticket-1), ff3c2d70 (Ticket-2)
**Artifacts read**: 02-architecture-plan.md, 04-ticket-review.md, ticket-1-completion.md,
  ticket-1-verification.md, ticket-2-completion.md, ticket-2-verification.md,
  B209-orchestrator-prompt.md, RULES_CATALOG.md, src/PropTraderTools/CopyEngine.cs

---

## Section A — Coherent System Check

### A.1 — Do both fixes form a coherent defense against the B208 OCO path bug?

**YES. The two fixes are complementary and together close the entire attack surface.**

**FIX-G-1** (`TryClearBeSwapOnStopWorking` — Ticket-1, commit a93e2227):
- Extended the terminal-state clear from `Working`-only to
  `Working | Cancelled | Rejected | Accepted`.
- Directly eliminates the OCO cancel path flag leak: when PTT-BE-Target-N fills
  and NT8 auto-cancels PTT-BE-Stop-N via OCO, the `Cancelled` state now triggers
  `SetBeSwapInProgress(accName, false)`. Flag cannot leak permanently.

**FIX-G-2** (`TryCancelSingleStaleGtcExit` — Ticket-2, commit ff3c2d70):
- Added an `IsBeSwapInProgress` guard before the `acc.Cancel()` call.
- Prevents the stale-GTC-sweep from cancelling a live `PTT-BE-Stop-N` during the
  active swap window (rapid re-entry race condition).

**Together**: FIX-G-1 ensures the flag is cleared on all terminal states so the swap
window guard in FIX-G-2 is time-bounded and does not permanently protect any order.
FIX-G-2 ensures that while the swap is in progress, a live stop cannot be destroyed
by a new-entry sweep. The two fixes have no design-level dependency but work in tandem
to close both the detection-suppression path (FIX-G-1) and the premature-cancellation
path (FIX-G-2) documented in the B208 SIM Round 6 diagnosis.

**Coherent system check**: PASS

---

### A.2 — Is the implementation consistent across both tickets?

| Check | Result |
|-------|--------|
| `IsBeClearStateTrigger` is `internal static` | PASS — CopyEngine.cs line 3186 confirmed |
| `IsBeSwapGtcSweepGuard` is `internal static` | PASS — CopyEngine.cs line 3200 confirmed |
| Both accessible via `InternalsVisibleTo("PropTraderTools.Tests")` | PASS — line 42 confirmed |
| `B209Tests.cs` tests both seams (10 [Fact]) | PASS — ticket-2-verification.md item 10 |
| Sequential execution order honored (T1 before T2) | PASS — Ticket-2 explicitly gated on Ticket-1 VERIFY_PASS |

**Consistency check**: PASS

---

### A.3 — No missing wiring?

Live source verification at CopyEngine.cs lines 3167–3181:
- `TryClearBeSwapOnStopWorking` body contains the `isClearState` bool with four states
  (Working | Cancelled | Rejected | Accepted) — PRESENT.
- `SetBeSwapInProgress(e.Order.Account.Name, false)` call at line 3180 — PRESENT.
- `IsBeClearStateTrigger` seam at lines 3186-3194 — PRESENT.

Live source verification at CopyEngine.cs lines 2179–2198:
- `IsStaleGtcExitOrder` early return at line 2181 — PRESENT (pre-existing, unchanged).
- FIX-G-2 guard block at lines 2182-2186 — PRESENT.
- `try/catch` block intact at lines 2187-2197 — PRESENT (unchanged).
- `IsBeSwapGtcSweepGuard` seam at lines 3200-3203 — PRESENT.

**No missing wiring detected.** Both sides of each fix are correctly wired.

---

## Section B — Cross-File JS Violation Scan (Final State)

### B.1 — SCAN-01: JS-021 lock() in modified/added scope

Ticket-1 VERIFY_PASS (ticket-1-verification.md, SCAN-01):
`Select-String -Pattern "lock\("` → 9 hits, ALL in comments (describing "no lock()").
Zero actual `lock(` executable code in the entire file.

Ticket-2 VERIFY_PASS (ticket-2-verification.md, SCAN-01):
`Select-String -Pattern "lock\(" -SimpleMatch` → 0 matches.

Final state: **0 lock() violations. JS-021: PASS.**

---

### B.2 — SCAN-05: JS-001 throw new in modified/added scope

Ticket-1 VERIFY_PASS (SCAN-05): `Select-String -Pattern "throw new"` → 0 results file-wide.
Ticket-2 VERIFY_PASS (SCAN-05): `Select-String -Pattern "throw new"` → 0 results file-wide.

Final state: **0 throw new violations. JS-001: PASS.**

---

### B.3 — SCAN-07: ptt-sync (both tickets)

Ticket-1 VERIFY_PASS (SCAN-07): `ptt-sync complete: 19 linked, 0 errors`
Ticket-2 VERIFY_PASS (SCAN-07): `ptt-sync complete: 19 linked, 0 errors`

Final state: **ptt-sync 19/0 on both commits. Hard-link integrity: PASS.**

---

### B.4 — Additional cross-file DNA scans (verified by ticket verifiers)

| Scan | Ticket-1 VERIFY | Ticket-2 VERIFY | Final State |
|------|----------------|----------------|-------------|
| JS-002 return null | PASS — void/bool, 0 hits in scope | PASS — void/bool, 0 in scope | PASS |
| JS-033 async void | PASS — 0 actual declarations | PASS — 0 actual declarations | PASS |
| JS-015 StringComparison.Ordinal | PASS — all StartsWith calls | PASS — all StartsWith calls | PASS |
| ASCII-only (SCAN-06) | PASS — 0 non-ASCII bytes in scope | PASS — 0 non-ASCII bytes (full file) | PASS |
| FontFamily (NT8 SCAN-03) | PASS — 0 executable hits | PASS | PASS |
| Hardcoded hex (NT8 SCAN-04) | PASS — 0 results | PASS | PASS |
| DateTime.Now (NT8 SCAN-06) | PASS — 0 executable hits | PASS | PASS |

**All cross-file JS violation scans: PASS. No rule violations introduced by B209.**

---

## Section C — Spec Requirements Satisfied Matrix

| Spec Requirement | Addressed? | Evidence |
|-----------------|-----------|---------|
| FIX-G-1: `_beSwapInProgress` flag no longer leaks permanently after OCO cancel | YES | `isClearState` includes `Cancelled` at CopyEngine.cs line 3173. OCO path now triggers clear. |
| FIX-G-2: PTT-BE-* orders protected from GTC sweep during swap window | YES | Guard block at CopyEngine.cs lines 2182-2186. `IsBeSwapInProgress` read before any cancel. |
| 10 [Fact] tests in B209Tests.cs | YES | Ticket-2-verification.md item 10: all 10 named tests confirmed present, xUnit only. |
| `IsBeClearStateTrigger` internal static seam present | YES | CopyEngine.cs line 3186, `internal static bool`. |
| `IsBeSwapGtcSweepGuard` internal static seam present | YES | CopyEngine.cs line 3200, `internal static bool`. |
| `InternalsVisibleTo("PropTraderTools.Tests")` | YES | CopyEngine.cs line 42, confirmed in both verifications. |
| Sequential execution order (T1 VERIFY_PASS before T2) | YES | Ticket-2 started only after ticket-1-verification.md VERIFY_PASS. |
| ptt-sync N/0 errors after each ticket | YES | Both commits: 19 linked, 0 errors. |
| Ph4b deletion audit: zero method-signature deletions | YES | Both tickets: 0 deleted signatures. |
| Single-hunk ban: not a full-file replace | YES | T1: 1 hunk but 51 diff lines scoped to lines 3151-3192 (surgical). T2: 2 hunks. |
| ASCII-only string literals | YES | SCAN-06 PASS on both tickets. Entire CopyEngine.cs is 100% ASCII. |
| CYC <= 8 for all modified methods | YES | See Section D below. |

**All spec requirements: SATISFIED.**

---

## Section D — All 7 Scans Zero Confirmation

### Ticket-1 (commit a93e2227)

| Scan | Command | Result | Layer 2/3 Match |
|------|---------|--------|----------------|
| SCAN-01 lock() | `Select-String "lock\("` in scope | PASS — 0 executable | MATCH |
| SCAN-02 async void | `Select-String "async void "` in scope | PASS — 0 actual | MATCH |
| SCAN-03 return null | `Select-String "return null"` in scope | PASS — 0 in scope | MATCH |
| SCAN-04 CYC | Manual McCabe count | PASS — TryClear=8, IsBeClear=6 | MATCH (both <=8) |
| SCAN-05 throw new | `Select-String "throw new"` file-wide | PASS — 0 | MATCH |
| SCAN-06 ASCII | Byte-level scan lines 3154-3187 | PASS — 0 non-ASCII | MATCH |
| SCAN-07 ptt-sync | `ptt-sync.ps1` | PASS — 19/0 | MATCH |

**Ticket-1: all 7 scans PASS.**

### Ticket-2 (commit ff3c2d70)

| Scan | Command | Result | Layer 2/3 Match |
|------|---------|--------|----------------|
| SCAN-01 lock() | `Select-String "lock\(" -SimpleMatch` | PASS — 0 | MATCH |
| SCAN-02 async void | `Select-String "async void "` | PASS — 0 actual | MATCH |
| SCAN-03 return null | `Select-String "return null"` in scope | PASS — 0 in scope | MATCH |
| SCAN-04 CYC | Manual strict McCabe count | PASS — TryCancelSingle=8, IsBeSwapGuard=4 | MATCH (both <=8) |
| SCAN-05 throw new | `Select-String "throw new"` file-wide | PASS — 0 | MATCH |
| SCAN-06 ASCII | Full-file byte scan | PASS — 0 non-ASCII (entire file) | MATCH |
| SCAN-07 ptt-sync | `ptt-sync.ps1` | PASS — 19/0 | MATCH |

**Ticket-2: all 7 scans PASS.**

### CYC Summary

| Method | Strict CYC | Max Allowed | Result |
|--------|-----------|-------------|--------|
| `TryClearBeSwapOnStopWorking` | 8 | 8 | PASS (at limit) |
| `IsBeClearStateTrigger` | 6 | 8 | PASS |
| `TryCancelSingleStaleGtcExit` | 8 | 8 | PASS (at limit) |
| `IsBeSwapGtcSweepGuard` | 4 | 8 | PASS |

Note: Architecture plan stated lower CYC targets (5 and 2 respectively). Independent strict
Lizard counting (each `&&` and `||` = +1) yields higher but still within-spec values. This is
a documented methodological difference — both verifiers independently confirmed the higher values.
No JS CYC-8 violation exists.

**All 7 scans zero across src/PropTraderTools/ for B209 scope: CONFIRMED.**

---

## Section E — Pre-existing Issues Disposition

### E.1 — Guardian Script: BrushTeal Pre-existing Gap

`verify_critical_symbols.ps1` reports:
```
GUARDIAN FAIL: TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
```

**Disposition**: Pre-existing. Documented in ticket-1-verification.md (lines 340-342, 362-368)
and ticket-2-verification.md (lines 376-382). Confirmed by `git diff HEAD~1 HEAD -- TradeCopierPanel.cs`
returning 0 lines for both B209 commits. B209 does not touch `TradeCopierPanel.cs`.

**Action**: Deferred to future block. Recorded in 06-deferred-backlog.md as DW-B209-02.

---

### E.2 — Guardian Script: Single-Hunk False Positive (Ticket-1)

`verify_critical_symbols.ps1` fires `SINGLE-HUNK BAN` for Ticket-1 commit (a93e2227).
Script check: `$hunkCount -eq 1` → true. Script conclusion: "full-file replace".

**Disposition**: False positive. Ticket-1 diff is 51 total lines, scoped to `@@ -3151,23 +3151,41 @@`
in a 9000+ line file. Two adjacent surgical changes (TryClearBeSwapOnStopWorking body replacement
+ IsBeClearStateTrigger insertion) were merged into one hunk by git's 3-line context window.
Full-file replace would produce ~9000 diff lines starting at line 1. This is definitively not
a full-file replace. Verifier independently confirmed ruling.

**Action**: Guardian script logic improvement deferred. Recorded in 06-deferred-backlog.md as DW-B209-01.

---

### E.3 — CYC Methodological Discrepancy

Architecture plan stated `TryClearBeSwapOnStopWorking` CYC=5 and `TryCancelSingleStaleGtcExit`
CYC delta=+1. Engineer and verifier independently measured CYC=8 for both methods using strict
Lizard methodology (each `&&` and `||` counted as +1 independently).

**Disposition**: Both values pass JS CYC <= 8 ceiling. No violation. The discrepancy is purely
methodological — architect used compound-predicate counting (if A || B = +1), Lizard strict uses
+1 per boolean operator. No action required. Within spec.

---

## Section F — Spec Update Status

### F.1 — `specs/002-trade-copier-spec.html`

The orchestrator prompt (`B209-orchestrator-prompt.md`, lines 251-257) requires Ph5 to update
`specs/002-trade-copier-spec.html` with:
- (a) Mark FIX-G-1 + FIX-G-2 as APPLIED with ptt-sync result
- (b) Update B208 card title: "FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL" →
      "FIX-G APPLIED — AWAITING SIM ROUND 7"
- (c) Update B208 bug table row status

**Search result**: `glob("specs/**")` → no files found. The `specs/` directory does not exist
in this workspace (`c:\WSGTA\universal-or-strategy`). The spec file is not accessible to this
reviewer in the current workspace.

**Disposition**: DEFERRED. Recorded in 06-deferred-backlog.md as DW-B209-04.
The Director must apply these three spec updates either in the workspace where the spec resides
or in a dedicated spec-update ticket before marking B209 fully COMPLETE for external tracking.

---

## Section K — Deferred Work (MANDATORY)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script: `verify_critical_symbols.ps1` fires single-hunk ban for adjacent surgical hunks merged by git context. Script cannot distinguish hunk-count=1 due to full-file replace vs. adjacent changes. Recommendation: add diff-line-count or hunk-start-line check before firing the ban. | P2 (Low — false positive only, not a safety issue) | B210 or future | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal pre-existing gap: `_instr2tBtn.Background = BrushTeal` (Quick2t visual fix) missing. Pre-existed before B209. Not caused by B209. Requires separate investigation and ticket. | P2 (Low) | Future block | OPEN |
| DW-B209-03 | SIM Round 7 required: FIX-G-1 and FIX-G-2 are committed and ptt-synced. SIM validation required before PROD deployment. Verify OCO path (PTT-BE-Target fills → PTT-BE-Stop cancels via OCO → `IsBeSwapInProgress` returns false → NakedPositionDetector fires normally). Verify GTC sweep does NOT cancel PTT-BE-Stop-N during active swap window under rapid re-entry. | P0 (HIGH — blocks PROD) | B210 / SIM Round 7 | OPEN |
| DW-B209-04 | `specs/002-trade-copier-spec.html` spec updates deferred: (a) mark FIX-G-1 + FIX-G-2 APPLIED with ptt-sync result; (b) update B208 card title to "FIX-G APPLIED — AWAITING SIM ROUND 7"; (c) update B208 bug table row status. File not accessible in this workspace (specs/ directory absent). Director must apply in correct workspace. | P1 (Medium — tracking artifact) | Before SIM Round 7 | OPEN |

---

## Final Verdict

### Pre-existing items NOT caused by B209 (do not affect verdict)
- TradeCopierPanel.cs BrushTeal guardian failure — pre-existing, documented, deferred
- Guardian script single-hunk false positive — script limitation, documented, deferred

### B209 scope: clean
- All spec requirements satisfied
- All 7 scans zero for B209 modified/added scope (both tickets)
- No JS rule violations in B209 code
- Both fixes coherently close the B208 OCO path bug
- 10 xUnit [Fact] tests covering both seams
- ptt-sync: 19/0 on both commits
- Ph4b deletion audit: 0 method-signature deletions on both commits

---

## FINAL_PASS
