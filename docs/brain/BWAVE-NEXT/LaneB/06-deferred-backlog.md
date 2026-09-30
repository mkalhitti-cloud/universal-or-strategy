# BWAVE-NEXT Lane B -- Deferred Backlog

## Header

- **Block**: BWAVE-NEXT Lane B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
- **Closed**: 2026-09-04
- **Reviewer**: ptt-plan-reviewer (Phase 5)

---

## Items Closed This Block

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-NEW-08-D | Layer 2 Option D cancel-before-dispatch drain. Before dispatching a new entry copy to a follower, cancel ALL existing Working/Accepted entry orders for that follower+instrument first. Park dispatch intent in `_pendingDispatchDrains` (ConcurrentDictionary). Wait for cancel acknowledgment before submitting the new single entry. Eliminates root cause of ATM bracket race (DW-NEW-08) by ensuring only one live entry per follower at a time. Implemented as: `PendingDispatchDrain` sealed class (9 fields), `_pendingDispatchDrains` readonly ConcurrentDictionary, `DrainThenDispatch` (CYC=4), `OnDrainCancelAck` (CYC=3), `SubmitDrainedEntry` (CYC=3), `SubmitEntryDirect` (CYC=2), `TryDrainWatchdog` (CYC=3). Modified: `HandleEntryChange` (CYC 7->6), `OnOrderUpdate` (CYC 6->7). All <=8. Log markers [DRAIN], [DRAIN-SUBMIT], [DRAIN-TIMEOUT] present. 3 [Fact] structural tests pass. NT8 sync 18/18 OK. Build 0 errors. | T2 (VERIFY_PASS 2026-09-04) |

---

## Deferred Items (still open)

| ID | Description | Priority | Recommended Target |
|----|-------------|----------|--------------------|
| DW-NEXT-LANEB-01 (SIM gate) | **SIM gate for T2 cancel-before-dispatch drain.** Requires live NT8 SIM session. Cannot be verified by structural tests alone. Evidence to collect: (1) NT8 output log shows `[DRAIN] cancel-sent=N` BEFORE `[DRAIN-SUBMIT]` on every dispatch cycle during 14+ drag-reposition cycles; (2) NT8 output log shows `[DRAIN-TIMEOUT] acct=...` if a cancel is unacknowledged for >2000ms; (3) Under 14+ drag cycles, follower ends every cycle with either flat OR Entry:Filled + brackets (no naked position). Non-blocking for code VERIFY_PASS (structural VERIFY_PASS already achieved). Status: Pending Director-scheduled SIM session. | P1 | Director action / first post-lane SIM session |
| DW-NEXT-A-01 | **T4 SIM gate: GraceMs calibration.** After first live or SIM session with `NakedPositionDetector` active (T4, PR #42), monitor `[NAKED-DETECT]` log lines during normal fill+bracket-arm sequence. If false fires observed (naked detect fires before ATM brackets confirm): increase `GraceMs` constant (currently 500ms) in `NakedPositionDetector`. If naked positions slip through before bracket lag resolves: decrease. Document final calibration value and rationale in a follow-up note appended to `docs/brain/BWAVE-NEXT/LaneA/ticket-4-completion.md`. Observation-only: no code change unless calibration indicates adjustment needed. Carried from LaneA 06-deferred-backlog.md. | P1 | Director action / first post-lane SIM/live session |
| DW-NEXT-A-02 | **T3 NT8 sync verbatim output gap.** `docs/brain/BWAVE-NEXT/LaneA/ticket-3-completion.md` documents the expected ptt-sync-and-verify.ps1 output format but omits the actual verbatim run output. Spec protocol requires verbatim recording. Defect is documentation-only (the `IsPendingBeSlotActive(string)` seam is a trivially correct 1-line ConcurrentDictionary.ContainsKey expression; no functional risk). Director to re-run `powershell -File scripts\ptt-sync-and-verify.ps1` against current main and append verbatim output as a follow-up note to ticket-3-completion.md. Carried from LaneA 06-deferred-backlog.md. | P2 | Director review / BWAVE-NEXT housekeeping |

---

## Items Explicitly Out of Scope (Confirmed Excluded)

From LaneB-mission-brief.md exclusion list (all confirmed unmodified in this lane):

| ID | Description | Reason |
|----|-------------|--------|
| DW-NEXT-A-03 | Short position detection for NakedPositionDetector | No shorts in current operational pattern. Future backlog. |
| DW-NEXT-A-04 | Multi-instrument cross-contamination in drain | Single-instrument use only. Future backlog. |
| DW-NEXT-A-05 | Entry orders misclassified as protective within 500ms grace window | Edge case within grace window. Future backlog. |
| DW-RepairLC-01/02 | SIM gates (live NT8 validation sessions) | Director action, live NT8 required. Not engineer tickets. |
| DW-C39-09-LaneA | SaveRules on OnAddRule in TradeCopierWindow | TradeCopierWindow.cs scope -- separate lane/block. |
| All NEW-0x test quality gaps | Test quality improvements | Separate lane. |
| DW-C38-02 | `_modules.Teardown()` Dispose verification | Analysis-only; no crash observed; separate ticket. Not assigned to this lane. |

---

*Deferred backlog written: 2026-09-04 | ptt-plan-reviewer | BWAVE-NEXT Lane B Phase 5*
*DW-NEW-08-D closed by T2 VERIFY_PASS. 3 items carried forward (1 new SIM gate + 2 from LaneA).*

---

## F5 Compilation Gate

**Status**: PASS
**Date**: 2026-09-04
**Branch**: main (commits febddf12 + 13ffb926)
**Result**: NinjaTrader 8 F5 compile succeeded -- 0 new errors.
**Recorded by**: Director (post-pipeline)
