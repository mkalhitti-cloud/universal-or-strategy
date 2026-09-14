# Deferred Work Backlog -- Block B157

**This file covers Block B157 only.**
Prior block backlog at: `docs/brain/B156/06-deferred-backlog.md`

Each block maintains its own 06-deferred-backlog.md.
Items are carried forward explicitly rather than concatenated to preserve file independence.

---

## Block B157 -- CopyEngine Six-Defect Repair

**Block ID:** B157
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-08-10
**Final verdict:** FINAL_PASS

### Block Summary

Six deferred defect cards addressed across 3 tickets:

- **DW-B157-RG-01 (P0):** IsReversalToFlatFollower logic inversion fixed.
  `followerIsFlat` -> `!followerIsFlat` at CopyEngine.cs:6526. 4 tests
  (1 renamed+flipped, 3 new) covering all truth-table branches. VERIFY_PASS.

- **DW-B156-DEFER-1 (P1):** TryEvictFollowerBeSlot (CYC=14) decomposed into
  thin router (CYC=7) + IsLeaderFlatDrainAbort (CYC=8) + RunFollowerEvictPath (CYC=4).
  No logic change. Log marker `[DRAIN-ABORT-LEADER-FLAT]` preserved verbatim. VERIFY_PASS.

- **DW-B153-05 (P2):** CountLeaderTargets stale annotations corrected.
  CopyEngine.cs lines 6638+6645 now read CYC=6 and CYC<=6 (was CYC=4, CYC<=5). VERIFY_PASS.

- **DW-B153-06 (P2):** UpdateLegTargetPrice dead code removed (lines 3533-3551
  after T2 line shift). Zero callers confirmed. VERIFY_PASS.

- **DW-B156-DEFER-2 (P3):** TrySetSlateReadyOnDrainFill annotation at CopyEngine.cs:991
  corrected: CYC<=5 -> CYC<=8. VERIFY_PASS.

- **DW-B156-DEFER-3 (P3):** Exact-string assertion test
  `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` added to
  CopyEngineTests.cs (line 9481). `[Fact]` no Skip. `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`.
  Test PASSED [156 ms]. VERIFY_PASS.

All 7 scans PASS file-wide. Zero executable lock(), zero non-ASCII bytes, zero async void,
zero DateTime.Now in executable code. All new/modified methods CYC<=8.

---

## Carry-Forward from B156

The following items were OPEN at end of B156 and were NOT addressed by B157.

| ID | Item | Priority | Prior Status | B157 Disposition |
|----|------|----------|--------------|-----------------|
| DW-B154-05 | CYC improvement: `IsPttCopyEntryFilled` delegation refactor. P3 refactor. | P3 | OPEN | STILL OPEN -- B157 did not touch this region |
| DW-B154-06 | Monitor: `TryTriggerBeRecovery` post-deployment watch for BE recovery storms. | P2 | OPEN | STILL OPEN -- no new evidence this block |
| DW-B155-01 | SIM gate pending for B155 `[DRAIN-ABORT-FLAT]` fix -- manual SIM run required. | P2 | OPEN | STILL OPEN -- B157 provides complementary coverage but does not substitute for B155 SIM gate |
| DW-BUG-0911-A-01 | Mid-close race condition -- `OnOrderUpdate` non-deterministic fill order when BE stop and leader close coincide. | P2 | OPEN | STILL OPEN -- not addressed by B157 |

Note: DW-B156-DEFER-1, DW-B156-DEFER-2, DW-B156-DEFER-3 were OPEN from B156 and are
now CLOSED by B157. They do not appear in the carry-forward table.

---

## New B157 Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B157-COMMENT-01 | Stale method comment at CopyEngine.cs line 6517 (post-T2 line numbering; was at line 6518 per T1 verifier report, shifted +19 by T2 line insertions). Reads: `// Returns true iff the current dispatch reverses the last direction AND the follower is flat.` This describes PRE-FIX semantics. Post-fix, the method returns `true` when the follower is NOT flat (open position + reversal = skip). Annotation-only change; zero logic impact. Identified by T1 verifier (ticket-1-verification.md §1.2). | P3 | B158 T2 | OPEN -- carry to B158 |
| DW-B157-SIM-01 | SIM gate for DW-B157-RG-01. 7-step SIM run executed 2026-09-14. Steps 1-7 all PASS. Step 3 (Short reversal): followers copied Short, zero [PTT-COPY-GUARD] lines -- B157 fix confirmed. Step 7 (protection path): follower with open Short not given Long copy -- DW-B128 protection intact. No regressions on steps 1,2,4,5,6. Anomalies: first unfilled Short at step 3 (NT8 ATM cancel on second fill -- expected) + Sim103 stale Close:CancelPending (NT8 SIM cancel latency artifact -- not a copier defect). CSV: docs/NinjaTrader Grid 2026-09-14 04-33 PM.csv. Output: docs/output.md. | P1 | CLOSED 2026-09-14 | CLOSED -- SIM PASS |

---

## Standing Open Items Aggregate (All Blocks Through B157)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | Future CYC block | OPEN |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | Post-deployment | OPEN |
| DW-B155-01 | B155 SIM gate pending | P2 | Post-deployment | OPEN |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 | Future drain-path block | OPEN |
| DW-B157-COMMENT-01 | Stale comment at CopyEngine.cs:6517 (post-fix semantics wrong) | P3 | B158 T2 | OPEN |
| DW-B157-SIM-01 | SIM validation for IsReversalToFlatFollower post-fix | P1 | CLOSED 2026-09-14 | CLOSED -- SIM PASS |

---

_Deferred backlog authored by ptt-plan-reviewer (Phase 5). Block B157. 2026-08-10._
_Carry-forward verified against docs/brain/B156/06-deferred-backlog.md._
_All items from B156 OPEN at time of B157 review are carried forward unchanged or marked CLOSED._
_B156 items DW-B156-DEFER-1, DW-B156-DEFER-2, DW-B156-DEFER-3 are CLOSED this block._
_DW-B157-SIM-01 CLOSED 2026-09-14 post-pipeline SIM. Director ran 7-step gate. All steps PASS._
