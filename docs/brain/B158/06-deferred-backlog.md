# Deferred Work Backlog -- Block B158

**This file covers Block B158 only.**
Prior block backlog at: `docs/brain/B157/06-deferred-backlog.md`

Each block maintains its own 06-deferred-backlog.md.
Items are carried forward explicitly rather than concatenated to preserve file independence.

---

## Block B158 -- Leader-Flat Drain Abort SIM Gate + Stale Comment Fix

**Block ID:** B158
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-15
**Final verdict:** FINAL_PASS

### Block Summary

Two tickets addressed in parallel (LANES-APPROVED):

- **DW-B156-01 (P0):** T1 — Leader-flat drain abort SIM verification gate.
  IsLeaderFlatDrainAbort (CopyEngine.cs:1950) confirmed present with all 8 structural items.
  Called unconditionally from TryEvictFollowerBeSlot at L1938 (pre-Gate-1).
  Unit test `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` (CopyEngineTests.cs:9481)
  PASSED (1/1, [Fact], no [Skip]).
  Zero code changes. Empty git diff confirmed independently by engineer and verifier.
  All 7 scans PASS (file-wide). VERIFY_PASS.
  SIM gate (5-step scenario, marker [DRAIN-ABORT-LEADER-FLAT]) documented for Director execution.
  Disposition: **IMPLEMENTATION_CONFIRMED / SIM_PENDING_DIRECTOR**.

- **DW-B157-COMMENT-01 (P3):** T2 — Stale comment at IsReversalToFlatFollower corrected.
  CopyEngine.cs L6517 updated from pre-fix semantics ("follower is flat") to correct semantics
  ("follower is NOT flat — open position + reversal = protection path").
  git diff confirmed 1 line changed (L6517 only). No executable code modified. CYC=2 unchanged.
  ptt-sync.ps1: 18 linked, 0 errors. All 7 scans PASS. VERIFY_PASS.
  Disposition: **CLOSED**.

All 7 scans PASS file-wide. Zero executable lock(), zero non-ASCII bytes, zero async void,
zero DateTime.Now in executable code. All methods CYC<=8 (IsLeaderFlatDrainAbort AT LIMIT CYC=8,
no changes introduced). Lane independence confirmed — T1 region L1929-1974 and T2 region L6517
are entirely independent, verified by both engineer and verifier sessions.

---

## Carry-Forward from B157

The following items were OPEN at end of B157 and were NOT addressed by B158 (except where noted).

| ID | Item | Priority | Prior Status | B158 Disposition |
|----|------|----------|--------------|-----------------|
| DW-B154-05 | CYC improvement: `IsPttCopyEntryFilled` delegation refactor. P3 refactor. | P3 | OPEN | STILL OPEN -- B158 did not touch this region |
| DW-B154-06 | Monitor: `TryTriggerBeRecovery` post-deployment watch for BE recovery storms. | P2 | OPEN | STILL OPEN -- no new evidence this block |
| DW-B155-01 | SIM gate pending for B155 `[DRAIN-ABORT-FLAT]` fix -- manual SIM run required. | P2 | OPEN | STILL OPEN -- B158 T1 is a DIFFERENT SIM gate ([DRAIN-ABORT-LEADER-FLAT] vs [DRAIN-ABORT-FLAT]). Do not conflate. |
| DW-BUG-0911-A-01 | Mid-close race condition -- `OnOrderUpdate` non-deterministic fill order when BE stop and leader close coincide. | P2 | OPEN | STILL OPEN -- not addressed by B158 |
| DW-B157-COMMENT-01 | Stale comment at CopyEngine.cs:6517 (post-fix semantics wrong). | P3 | OPEN | **CLOSED** -- B158 T2 corrected the comment. New text confirmed in source. VERIFY_PASS. |

---

## New B158 Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B158-SIM-01 | B158 T1 SIM gate for DW-B156-01 leader-flat drain abort. EXECUTED 2026-09-15. OUTCOME: **FAIL**. `[DRAIN-ABORT-LEADER-FLAT]` absent from entire 1764-line output. Ghost re-entries confirmed on Sim102+Sim103 at same timestamp as PTT-BE-Stop-1/2/3 fills (CSV: 6:16:28 PM). Sim104 retained 3 contracts. DW-B156-01 remains OPEN. DW-B158-DRAIN-RACE-01 formally opened as DW-B159. Evidence: docs/output.md + docs/NinjaTrader Grid 2026-09-14 06-18 PM.csv | P0 | CLOSED -- FAIL outcome recorded | **FAIL -- DW-B159 OPENED** |
| DW-B158-CYC6-STALE-01 | Stale CYC=6 comment at ~L1904 for TryEvictFollowerBeSlot. Pre-dates DW-B156-DEFER-1 decomposition (which produced the correct CYC=7 comment at L1926). Annotation-only cleanup with zero logic impact. Flagged by both B158 engineer and verifier. | P3 | Future annotation block | OPEN |
| DW-B158-DRAIN-RACE-01 | ~~CONDITIONAL~~ **NOW OPEN AS DW-B159.** DW-B158-SIM-01 FAIL confirmed timing race. Leader-flat drain abort fires too late -- followers' Entry orders reach Accepted state before PTT-BE-Stop-3 fills. Two-level root cause: (1) per-leg fill ordering: drain-abort checks flatness on each PTT-BE-Stop-N fill, leader not flat until Stop-N all filled, (2) PositionStateChanged race: TryEnqueueFollowerEntry fires on hasPos=True before drain-abort can intercept. Fix: _beStopDrainActive latch armed on BE-All, cleared when leader flat + all stops terminal. Gate TryEnqueueFollowerEntry on latch. See docs/brain/B159/00-intake.md. | P0 | B159 pipeline | **OPEN AS DW-B159** |

---

## Standing Open Items Aggregate (All Blocks Through B158)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | Future CYC block | OPEN |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | Post-deployment | OPEN |
| DW-B155-01 | B155 [DRAIN-ABORT-FLAT] SIM gate pending | P2 | Post-deployment | OPEN |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 | Future drain-path block | OPEN |
| DW-B156-01 | Leader-flat drain abort -- SIM gate FAIL (DW-B158-SIM-01), drain-abort never fires, ghost re-entry reproduced | P0 | B159 pipeline | OPEN -- B159 REQUIRED |
| DW-B157-COMMENT-01 | Stale comment at CopyEngine.cs:6517 | P3 | B158 T2 | **CLOSED** -- B158 T2 |
| DW-B157-SIM-01 | SIM validation for IsReversalToFlatFollower post-fix | P1 | CLOSED 2026-09-14 | CLOSED -- SIM PASS |
| DW-B158-SIM-01 | B158 T1 SIM gate for DW-B156-01 | P0 | CLOSED (FAIL outcome) | **FAIL** -- DW-B159 opened |
| DW-B158-CYC6-STALE-01 | Stale CYC=6 comment at ~L1904 for TryEvictFollowerBeSlot | P3 | Future annotation block | OPEN |
| DW-B158-DRAIN-RACE-01 | Multi-leg PTT-BE-Stop fill timing race (ghost re-entry) | P0 | B159 pipeline | **OPEN AS DW-B159** |
| DW-B159 | BE drain latch fix: _beStopDrainActive + TryEnqueueFollowerEntry gate | P0 | B159 pipeline | OPEN -- intake at docs/brain/B159/00-intake.md |

---

## Director Action Required

**DW-B158-SIM-01 EXECUTED 2026-09-15. OUTCOME: FAIL.**

- `[DRAIN-ABORT-LEADER-FLAT]` absent from entire 1764-line output.
- Ghost re-entries confirmed on Sim102 + Sim103. Sim104 retained 3 contracts.
- DW-B156-01 remains P0 OPEN.
- DW-B158-DRAIN-RACE-01 formally opened as **DW-B159**.
- B159 pipeline intake at: docs/brain/B159/00-intake.md
- Spec card at: section-dw-b159 in 002-trade-copier-spec.html

**Next action:** Start B159 pipeline via ptt-orchestrator.

---

_Deferred backlog authored by ptt-plan-reviewer (Phase 5). Block B158. 2026-09-15._
_Carry-forward verified against docs/brain/B157/06-deferred-backlog.md._
_DW-B157-COMMENT-01 CLOSED this block (B158 T2)._
_DW-B156-01 IMPLEMENTATION_CONFIRMED / SIM_PENDING_DIRECTOR._
_New items added: DW-B158-SIM-01 (OPEN), DW-B158-CYC6-STALE-01 (OPEN), DW-B158-DRAIN-RACE-01 (CONDITIONAL)._
