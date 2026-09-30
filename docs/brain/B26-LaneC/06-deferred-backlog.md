# Deferred Backlog

This file accumulates deferred work items surfaced during final reviews. Items are appended per block.
Each block MUST be reviewed at the start of the subsequent block for P0/P1 promotion.

---

## B26-LaneC Block (2026-07-17)

**Reviewer**: ptt-plan-reviewer (Phase 5 Final)
**Source**: `docs/brain/B26-LaneC/05-final-review.md` Section K
**Block verdict**: FINAL_PASS

### Section K — Deferred Work Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-backlog-01 | `OnTrim` (~L1278), `OnFlatten` (~L1283), `OnCancel` (~L1288) in `TradeCopierPanel.cs` — confirmed dead (zero `Click+=` wiring; V1 handlers orphaned after B12 restructure). The V1 field counterparts (`_flattenBtn` L122, `_cancelBtn` L123, `_trimBtn` L124) were deleted in B26 Lane C T2. The V1 handlers that referenced those dead fields are therefore also dead but were excluded from B26 Lane C per spec DEAD-B26 scope (spec L10233 authorizes deletion of `OnToggle` and `OnBreakEven` only). Architecture plan B3 and ticket review TR3 explicitly record these as "Out of scope — not authorized by DEAD-B26." Require Director approval before deletion. | P2 | future | OPEN |

### Discrepancy Note (informational)

The T2 completion report labels `OnTrim`, `OnFlatten`, `OnCancel` as "Live — wires _trimBtn2.Click / _flattenBtn2.Click / _cancelBtn2.Click." This is factually inaccurate per the spec and architecture plan. These methods wire the V1 `_trimBtn` / `_flattenBtn` / `_cancelBtn` fields (which were just deleted), not the V2 `_trimBtn2` / `_flattenBtn2` / `_cancelBtn2` fields. The correct outcome (retaining them) was achieved. The "Live" characterization in the completion report is a labeling error, not a code error. Captured here for the next block reviewer's awareness.

---

*End of B26-LaneC block.*
