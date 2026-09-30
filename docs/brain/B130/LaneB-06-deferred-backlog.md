# B130 LaneB Deferred Backlog

**Block**: B130 LaneB
**Date**: 2026-09-01
**Lane**: LaneB — DW-B136 Gap B: Order-ID Scoped Cancel for Simultaneous Entries
**Status**: FINAL_PASS

Both B130 lanes are complete:
- LaneA: DW-B137 (Stop1/Target1 ATM bracket name routing — CLOSED)
- LaneB: DW-B136 Gap B (order-ID scoped cancel for simultaneous entries — CLOSED)

---

## Items CLOSED This Block (LaneB)

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B136 Gap B | Cross-cancel-by-instrument bug in `TryCancelFollowerEntries`. Old `CancelOneAccount` instrument-scope sweep replaced by `CancelScopedFollowerEntries` (`_followerCopyMap`-keyed cancel). Follower copies of order #1 are now preserved when leader cancels order #2 (same instrument). V-01 regression guard (`EvictDedup` isolation) anchored by third [Fact] test. | B130 LaneB T2 — BUILD_PASS, VERIFY_PASS, FINAL_PASS |

---

## New Deferred Items Identified This Block (LaneB)

### DW-B130-01 — HandleEntryChange Gap: Drag-Replaced Follower Entries Not Tracked

**Priority**: P1
**Status**: OPEN
**Discovered by**: ptt-plan-reviewer Phase 5 final review

**Context**: `HandleEntryChange` (`CopyEngine.cs` L2425) handles the leader-entry drag scenario
(Gate C in `DispatchCopy`): when a leader Working limit entry has its price changed, the old
follower entry is cancelled and a new one submitted at the new price (`acc.Submit` at L2476).
This new follower order is NOT recorded in `_followerCopyMap` under the leader orderId.

**Impact**: If the leader then cancels the dragged entry (leader orderId unchanged after drag),
`CancelScopedFollowerEntries(leader-id)` will find only the old (now-Cancelled) follower order
in the bag, skip it due to the `OrderState != Working && != Initialized` guard, and exit.
The drag-replaced follower entry (new NT8 orderId, not in the map) will NOT be cancelled.

**Pre-B130-LaneB behavior** (regression): `CancelOneAccount` swept by instrument name, so
drag-replaced follower entries were caught. B130 LaneB restored correctness for the primary
simultaneous-entries scenario but introduced a partial regression for the entry-drag+cancel
composite scenario.

**Fix**: In `HandleEntryChange` after `acc.Submit(new[] { order })` at L2476, add:
```csharp
RecordFollowerCopy(leaderOrder.OrderId.ToString(), order);
```
The old cancelled follower entry reference already in the bag will be skipped by the
`OrderState != Working` guard — no double-cancel risk. The new drag-replaced entry will
be correctly cancelled if the leader subsequently cancels.

**Also required**: Update `B130Tests.cs` with a new [Fact] test:
`B130_DW136_DragReplacedFollowerEntryIsCancelledWhenLeaderCancels` — seeds a bag with
a Cancelled order (simulating the old follower entry), calls `RecordFollowerCopy` with a
new mock Working order (simulating the drag-replaced entry), calls
`CancelScopedFollowerEntries`, and asserts the Working order was targeted for cancel.

**Prerequisite**: Architect plan review for `HandleEntryChange` modification (low blast radius;
single-line addition + one test).

**Deferred to**: B131 or next available LaneB follow-on block.

---

### DW-B130-02 — _followerCopyMap Dead-Entry Accumulation (Filled/Rejected Leader Orders)

**Priority**: P2
**Status**: OPEN
**Discovered by**: ptt-plan-reviewer Phase 5 final review

**Context**: `_followerCopyMap` entries for leader orders that terminate in Filled or Rejected
state are never evicted. `EvictDedup` intentionally does NOT touch `_followerCopyMap` (V-01
fix). `CancelScopedFollowerEntries` is only triggered from `TryCancelFollowerEntries` (Cancelled
path). Filled and Rejected leader orders leave dead entries in the map for the session lifetime.

**Impact**: Memory impact is negligible in a normal trading session (tens to low hundreds of
orders). Dead entries cannot cause correctness issues (they hold stale terminal-state Order
references; the only consumer `CancelScopedFollowerEntries` skips non-Working/Initialized
orders via the guard). No known correctness regression.

**Fix options**:
- Option A: In `EvictDedup`, add `_followerCopyMap.TryRemove(orderId, out _)` for
  `Filled` and `Rejected` states ONLY (NOT Cancelled — that would re-introduce V-01).
  This requires careful conditional to preserve V-01 safety.
- Option B: Add a `ClearFollowerCopyMap()` method called on session-end or LoadRules().
  Simpler; no per-order overhead; acceptable for the usage pattern.

**Prerequisite**: Architect plan review for Option A safety (the V-01 guard must be explicit
in the conditional — e.g., `if (state != OrderState.Cancelled) _followerCopyMap.TryRemove(orderId, out _)`).

**Deferred to**: B132 or future block as a memory hygiene improvement.

---

## Carry-Forward Items (from B129 LaneA backlog — unchanged)

All 20 items from `docs/brain/B129/LaneA-06-deferred-backlog.md` carry forward.
Status changes from this block:

| Item | Prior Status | This Block Status |
|------|-------------|-------------------|
| DW-B136 Gap B | OPEN (P1) | **CLOSED** — B130 LaneB FINAL_PASS |
| DW-B134-OCO | OPEN (P2) | OPEN (unchanged) |
| DW-B129-01 | OPEN (P1) | OPEN (unchanged) |
| DW-B133 | OPEN (P2) | OPEN (unchanged) |
| DW-B124-01 | OPEN (P2) | OPEN (unchanged) |
| DW-B124-02 | OPEN (P2) | OPEN (unchanged) |
| DW-B107 | OPEN (P2) | OPEN (unchanged) |
| B107-DEFER-01 | OPEN (P0) | OPEN (unchanged) |
| B107-DEFER-02 | OPEN (P1) | OPEN (unchanged) |
| DW-B42-01 | OPEN (Low) | OPEN (unchanged) |
| DW-B42-02 | OPEN (High) | OPEN (unchanged) |
| DW-B42-03 | OPEN (Conditional) | OPEN (unchanged) |
| DW-PTT-BE-FIX-01 | OPEN (Medium) | OPEN (unchanged) |
| DW-PTT-BE-FIX-02 | OPEN (High) | OPEN (unchanged) |
| DW-PTT-BE-FIX-03 | OPEN (High) | OPEN (unchanged) |
| DW-B89-DEFERRED-01 | OPEN (P0) | OPEN (unchanged) |
| DW-B89-DEFERRED-02 | OPEN (High) | OPEN (unchanged) |
| DW-B89-DEFERRED-03 | OPEN (High) | OPEN (unchanged) |
| DW-B89-DEFERRED-04 | OPEN (High) | OPEN (unchanged) |
| DW-B89-DEFERRED-05 | OPEN (High) | OPEN (unchanged) |
| DW-B89-DEFERRED-06 | OPEN (Medium) | OPEN (unchanged) |

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block (LaneB) | 1 | DW-B136 Gap B (simultaneous-entries cross-cancel — FINAL_PASS) |
| New deferred (LaneB) | 2 | DW-B130-01 (HandleEntryChange gap, P1), DW-B130-02 (dead-entry accumulation, P2) |
| Carry-forward from B129 LaneA (with 1 closed) | 20 items — 1 closed = 19 still open | See table above |

**Total open items**: 21 (19 carry-forward + 2 new B130 LaneB items)
**Total closed this block**: 1 (DW-B136 Gap B)

---

## Spec Update Note (Orchestrator Action Required)

After FINAL_PASS, the orchestrator/Director MUST apply these spec HTML updates to
`specs/002-trade-copier-spec.html`:

| # | Update | Action |
|---|--------|--------|
| 1 | DW-B136 Gap B | Mark CLOSED — B130 LaneB FINAL_PASS |
| 2 | DW-B130-01 | Add as OPEN deferred → B131 |
| 3 | DW-B130-02 | Add as OPEN deferred → B132 |
| 4 | B130 LaneB | Mark PIPELINE_COMPLETE |
