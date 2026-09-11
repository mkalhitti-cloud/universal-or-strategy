# PTT Deferred Backlog

This file accumulates deferred work items across all epics.
Each epic appends one block. Items are closed when implemented in a subsequent block.

---

## Block: BWAVE-CYC-LOGIC-01

**Phase:** 5 — Final Review
**Reviewer:** PTT Plan Reviewer (ptt-plan-reviewer mode)
**Date:** 2026-01-01
**Epic verdict:** FINAL_PASS

### Deferred Items

**None.** BWAVE-CYC-LOGIC-01 produced zero deferred items.

All 71 method implementations (70 stub-fills + 1 new insert A-14b) were completed within this
epic. The architecture plan explicitly scoped zero deferrals: all 70 stubs were confirmed
implementable using AddOnBase APIs without NT8 host requirements.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| — | No deferred items from BWAVE-CYC-LOGIC-01 | — | — | N/A |

### Carry-Forward Informational Notes (non-blocking, no action required)

The following items were noted by the ticket reviewer (Phase 3.5) as informational.
They require no corrective action but are recorded here for engineer awareness in
future epics that build on these methods:

1. **CYC undercount pattern:** Several methods have stated CYC values that are 1-2 below a
   strict per-`||`-operator McCabe count. All are ≤ 8 under any counting method. Plan-approved
   in Cycle 2. If a future epic modifies these methods, the engineer should recount CYC from
   scratch using the strict per-operator method to avoid creeping toward limit.

2. **A-17 `leaderName` parameter reserved:** `CancelStaleTgtDragOrders(acc, instr, leaderName)`
   — the `leaderName` parameter is unused, reserved for future STP/TGT disambiguation. A future
   epic that activates this parameter must not break the existing call sites that pass a
   placeholder value.

3. **FromEntrySignalName → FromEntrySignal:** Architecture plan pseudocode contained a
   documentation error using `FromEntrySignalName` (a PTT-internal struct property) instead of
   the NT8 `Order.FromEntrySignal` property. All implementations correctly use `FromEntrySignal`.
   The plan pseudocode should be updated before being referenced in future epics.

---
