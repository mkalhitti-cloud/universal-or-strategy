# B210 Orchestrator Prompt
# PTT-COPIER-B210: SIM Round 7 — FIX-G-1 + FIX-G-2 Validation

**Block**: B210
**Mode**: copier-spec (SIM validation + spec update only — NO pipeline, no .cs edits)
**Spec file**: `file:///C:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html`
**Date**: 2026-07-07

---

## Confirmed State (do not re-investigate)

### B209 Pipeline — COMPLETE (FINAL_PASS 2026-07-07)

| Ticket | Change | Commit | Status |
|--------|--------|--------|--------|
| T1 FIX-G-1 | `TryClearBeSwapOnStopWorking` clears on `Working \| Cancelled \| Rejected \| Accepted` + `IsBeClearStateTrigger` seam | `a93e2227` | VERIFY_PASS |
| T2 FIX-G-2 | `TryCancelSingleStaleGtcExit` guards `PTT-BE-*` during active swap + `IsBeSwapGtcSweepGuard` seam + `B209Tests.cs` (10 [Fact]) | `ff3c2d70` | VERIFY_PASS |
| Ph5 | 05-final-review.md — FINAL_PASS | — | COMPLETE |
| Artifacts | docs/brain/B209/ (10 files) | `a10cee4e` | COMMITTED |
| Spec HTML | B208 section added (FIX-G APPLIED — AWAITING SIM ROUND 7) | — | UPDATED |

ptt-sync: 19/0 on both commits. Hard-link integrity confirmed.

### B208 Root Cause Summary (grounded from source, session 2026-07-07)

**Root cause 1 (OCO path flag leak)**: Pre-B209, when PTT-BE-Target-N filled (scratch exit), NT8 OCO
auto-cancelled PTT-BE-Stop-N directly to `Cancelled` state. `TryClearBeSwapOnStopWorking` only cleared
on `Working` — so `_beSwapInProgress` was permanently locked `true` for any account whose target fills
immediately. This permanently suppressed `NakedPositionDetector` for that account.
→ Fixed by FIX-G-1 (commit `a93e2227`).

**Root cause 2 (GTC sweep race)**: `TryCancelStaleGtcExitOnEntry` fires on every new `PTT-Copy`/`Entry`
fill. A rapid re-entry (copier dispatching new entry while BE swap is still in progress) triggered a GTC
sweep that swept the live PTT-BE-Stop-N (10–50ms after submission). `IsStaleGtcExitOrder` returns true
for any Working PTT-BE-* order. Follower left unprotected.
→ Fixed by FIX-G-2 (commit `ff3c2d70`).

Both fixes confirmed in source: CopyEngine.cs lines 3167–3203 (FIX-G-1), lines 2179–2198 (FIX-G-2).
PttBreakEvenSwap.cs lines 95–107 (finally block — safety escape only, never clears in <3s normal path).

---

## B210 Task: SIM Round 7

### Primary goal
Run SIM Round 7 to validate FIX-G-1 + FIX-G-2 in a live NT8 SIM environment.
This is the only gate before PROD deployment of commits `a93e2227` + `ff3c2d70`.

### SIM Round 7 verification scenarios

**Scenario A — OCO path (FIX-G-1)**
1. Copier ON. Enter position on leader + all followers (1 contract each).
2. Click BE-ALL. Wait for leader to close via scratch exit (PTT-BE-Target-N fills).
3. Confirm: PTT-BE-Stop-N on leader goes `Cancelled` via OCO.
4. Confirm in NT8 Output: `TryClearBeSwapOnStopWorking` fires on `Cancelled` (check `IsBeSwapInProgress` log).
5. Wait 500ms. Enter a NEW position on leader + followers.
6. Confirm: followers do NOT get spuriously flattened (NakedPositionDetector fires normally, not suppressed).
7. Log line to check: no `[PTT-Flatten]` or `[NAKED-DETECT]` immediately after re-entry (would indicate false trigger).
   PASS: `IsBeSwapInProgress` returns false after OCO cancel; NakedPositionDetector runs freely.

**Scenario B — GTC sweep guard (FIX-G-2)**
1. Copier ON. Enter position on leader + all followers.
2. Click BE-ALL. Immediately (within 100ms) enter a NEW position on the leader (triggers copy to followers).
3. Confirm: followers receive new PTT-Copy fill AND still have their PTT-BE-Stop-N Working (not cancelled by GTC sweep).
4. Log line to check: no `[GTC-SWEEP] cancelled PTT-BE-Stop-*` while `_beSwapInProgress` is true.
   PASS: PTT-BE-Stop-N survives the GTC sweep window.

**Scenario C — Regression: PTT-QX-* still swept**
1. Copier ON. Enter position. Let it sit with a stale PTT-QX-* exit order Working.
2. Enter a new position (triggers `TryCancelStaleGtcExitOnEntry`).
3. Confirm: stale PTT-QX-* order IS cancelled by GTC sweep (FIX-G-2 guard does NOT protect non-PTT-BE-* orders).
4. Log line to check: `[GTC-SWEEP] cancelled PTT-QX-*` appears.
   PASS: regression-free.

### Spec update on SIM PASS
If all 3 scenarios PASS, update spec HTML B208 block:
- Card status pill: `FIX-G APPLIED — AWAITING SIM ROUND 7` → `FIX-G APPLIED — SIM ROUND 7 CONFIRMED ✓`
- SIM Round 7 warning block: change amber warning to green confirmation.
- Close DW-B209-03: status `P0 BLOCKS PROD` → `CLOSED — SIM R7 PASS {date}`.
- Add a SIM Round 7 results section documenting output log snippets confirming each scenario.
- Build tag: `PTT-COPIER B210 | sim-r7-be-all-validation | {date}`

### On SIM FAIL
If any scenario fails:
- Document the failure in spec HTML as `SIM ROUND 7 FAIL — {scenario} — {date}`.
- Add the failure log to a new B210 diagnosis block.
- Create a new orchestrator prompt for B211 (additional fix).
- SRC CODE BAN: any required fix goes through the FULL 5-phase PTT pipeline.

---

## Open Deferred Work (pre-B210)

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B209-01 | Guardian script single-hunk false positive | Low | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal pre-existing gap | Low | OPEN |
| DW-B209-03 | SIM Round 7 — blocks PROD | P0 | OPEN — B210 |
| DW-B186-DIAG-CLEANUP-01 | Remove [DW-B115-DIAG] log blocks from PttGlobalQuickExit | Low | OPEN |
| DW-B186-WARN-01 | CS0067 CopyEngine.PendingBeArmed event declared but never raised | Low | OPEN |

---

## SRC CODE BAN
No .cs edits in this session unless a SIM failure requires a fix, which must go through
the FULL 5-phase PTT pipeline (Ph1 ptt-architect → Ph2 ptt-plan-reviewer → Ph3 ptt-architect →
Ph3.5 ptt-ticket-reviewer → Ph4a ptt-engineer → Ph4b ptt-verifier → Ph5 ptt-plan-reviewer).
No shortcuts. No direct edits.

---

## Files

```
src/PropTraderTools/
  CopyEngine.cs                — FIX-G-1 (L3167-3203), FIX-G-2 (L2179-2198)
  Tests/B209Tests.cs           — 10 [Fact] covering IsBeClearStateTrigger + IsBeSwapGtcSweepGuard
  Features/PttBreakEvenSwap.cs — finally block (L95-107)

docs/brain/B209/               — full pipeline (10 files), committed a10cee4e
docs/brain/B210-orchestrator-prompt.md — this file

specs/002-trade-copier-spec.html  — B208 block added (FIX-G APPLIED — AWAITING SIM R7)
```
