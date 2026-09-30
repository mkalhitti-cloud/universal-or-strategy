# BWAVE-NEXT Lane A -- Deferred Backlog

**Block**: BWAVE-NEXT Lane A
**Closed**: 2026-09-04
**Reviewer**: ptt-plan-reviewer (Phase 5)

---

## Deferred Items

| ID | Description | Priority | Recommended Target |
|----|-------------|----------|--------------------|
| DW-NEW-08-D | **Layer 2 Option D: cancel-before-dispatch drain.** Before dispatching a new entry copy to a follower, cancel ALL existing Working/Accepted entry orders for that follower+instrument first. Park the dispatch intent in `_pendingDispatchDrains` (ConcurrentDictionary) and wait for cancel acknowledgment before submitting the new single entry. Eliminates the root cause of the ATM bracket race (DW-NEW-08) by ensuring only one live entry per follower at a time. New code: `PendingDispatchDrain` sealed class, `DrainThenDispatch` (CYC=4), `OnDrainCancelAck` (CYC=3), `SubmitDrainedEntry` (CYC=3), drain watchdog (2s timeout). Modified: `PropagateFollowerEntryReplace` (+1 branch), `OnOrderUpdate` (+1 branch). Both stay <=8 CYC. See `DW-NEW-08-naked-fill-race.md` Layer 2 spec for full design. | P1 | BWAVE-NEXT Lane B |
| DW-NEXT-A-01 | **T4 SIM gate: GraceMs calibration.** After first live or SIM session with `NakedPositionDetector` active, monitor `[NAKED-DETECT]` log lines during normal fill+bracket-arm sequence. If false fires observed (naked detect fires before ATM brackets confirm), increase `GraceMs` constant (currently 500ms) in `NakedPositionDetector`. If naked positions slip through before bracket lag resolves, decrease. Document final calibration value and rationale in a follow-up note to `ticket-4-completion.md`. | P1 | Director action (first post-lane SIM/live session) |
| DW-NEXT-A-02 | **T3 NT8 sync verbatim output gap.** `ticket-3-completion.md` documents the expected ptt-sync-and-verify.ps1 output format but omits the actual verbatim run output. Spec protocol requires verbatim recording. Defect is documentation-only (the `IsPendingBeSlotActive(string)` seam is a trivially correct 1-line ConcurrentDictionary.ContainsKey expression; no functional risk). Director to confirm sync was executed or re-run sync command against current CopyEngine.cs and record verbatim output in a follow-up note appended to `ticket-3-completion.md`. | P2 | Director review / BWAVE-NEXT housekeeping |

---

## Items Explicitly Out of Scope (Confirmed Excluded)

| ID | Description | Reason |
|----|-------------|--------|
| DW-C38-01 | `OnPendingBeArmedDispatch` unsubscribe in `TradeCopierPanel.Detach()` | **Already fixed** as HOTFIX-BEALL-SYNC-01. Line 586 confirmed present throughout this lane. Not touched. |
| DW-C38-02 | `_modules.Teardown()` Dispose verification | Analysis-only; no crash observed; requires module-by-module audit; separate ticket. Not assigned to BWAVE-NEXT Lane A. |
| DW-C39-09 | `SaveRules` on `OnAddRule` in TradeCopierWindow | `TradeCopierWindow.cs` scope -- different lane/block. |
| DW-C39-07/08 | Null-guards, rule-count cap in TradeCopierWindow | `TradeCopierWindow.cs` scope -- different lane/block. |
| DW-RepairLC-01/02 | SIM gates (live NT8 validation sessions) | Director action, not engineer ticket. Requires live NT8 session. |
| DW-NEW-07 live-trading observations | Director-provided live observations for global BE cleanup scenarios | Director will provide; separate backlog append, not a code ticket in this lane. |
| acc.Orders.ToList() call sites 3-25 | 23 of 25 call sites intentionally not converted to ActiveOrders | Each has its own state gate, diagnostic purpose, or scans for different intent. Confirmed unchanged (count=23). |

---

*Deferred backlog written: 2026-09-04 | ptt-plan-reviewer | BWAVE-NEXT Lane A*
