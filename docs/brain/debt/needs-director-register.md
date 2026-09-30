# Needs-Director Register

**Purpose**: Architectural findings that lane orchestrators correctly classified as
NEEDS_DIRECTOR -- decisions requiring Director judgment that cannot be auto-applied.
These are NOT bugs with a clear fix; they require design decisions about trade-offs.

**Written by**: wave-orch-phase7-lane instances (STEP 5a, NEEDS_DIRECTOR sub-section).
**Format**: Append-only. One row per finding.

---

## Register

| ID | Wave | PR | File | Method | Finding | Rationale for NEEDS_DIRECTOR | Priority |
|----|------|----|------|--------|---------|------------------------------|----------|
| ND-001 | 7 | PR-20 | src/V12_002.Orders.Callbacks.Propagation.cs | PropagateMasterTargetMove / ResubmitTargetOrder | Route through FSM FollowerTargetReplaceSpec instead of raw Cancel+Submit | Requires building FollowerTargetReplaceSpec, storing CancellingOrderId, and relying on Phase 2 for resubmit. Non-trivial -- exposes positions if done incorrectly. Intentional design per L551-555 comment. | P1 |
| ND-002 | 7 | PR-20 | src/V12_002.Orders.Callbacks.AccountOrders.cs | PurgeFollowerStopScanStopOrders | OrderId fallback: match by object reference falls back to OrderId when reference equality fails | Depends on NinjaTrader Order identity semantics -- unclear if OrderId is stable across session replay. Requires production testing. | P2 |
| ND-003 | 7 | PR-20 | src/V12_002.Orders.Callbacks.AccountOrders.cs | (cascade suppression path) | Restrict cascade suppression to active FSM states only | FSM state enumeration for cascade gate requires auditing all callers. Risk of orphaned cancellations if FSM state enum is wrong. | P2 |
| ND-004 | 7 | PR-20 | src/V12_002.Orders.Management.StopSync.cs | stopOrders Enqueue path | Ghost-order window: synchronous tracking vs deferred tracking trade-off in StopSync | Architectural: synchronous path eliminates ghost window but changes concurrency model. Director must approve model change. | P1 |
| ND-005 | 7 | PR-20 | src/V12_002.Orders.Callbacks.AccountOrders.cs | CaptureTargetSnapshot / RefreshTargetSnapshot | Deduplication of snapshot logic between two methods | Consolidation requires verifying all callers use the unified snapshot contract. Risk of subtle timing regression. | P3 |

---

## Resolution Workflow

When Director decides on a NEEDS_DIRECTOR item:

1. Update the row with `resolution: approved | rejected | deferred` and `decision_note`.
2. If approved: create a new epic in `epic_roadmap.json` referencing ND-NNN.
3. If rejected: add `rejection_reason` and mark `status: closed`.
4. If deferred: add target wave.

---

## Priority Key

| Level | Meaning |
|-------|---------|
| P0 | Blocking -- cannot merge without resolution |
| P1 | High -- architectural risk, fix in next wave |
| P2 | Medium -- functional gap, schedule within 2 waves |
| P3 | Low -- quality improvement, backlog |
