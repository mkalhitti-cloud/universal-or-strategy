# PR-20-deferred Triage
# Lane L7 -- Wave 7 Post-Merge Deferred Repairs
# Branch: wave7/pr20-deferred-repairs
# Date: 2026-07-04

---

## Lamport Gate
- wave_7_complete: CONFIRMED (multiple events, highest clock=165)
- docs/brain/wave7-pr-repairs/PR-20/lane-L1-complete.md: CONFIRMED

---

## Source Summary

fix_queue.md listed 4 deferred NEEDS_DIRECTOR findings from Lane L1 (PR #20):
  NEW-F3, NEW-F5, NEW-F6, NEW-F7

F6 (PropagateMasterTargetMove FSM) was EXCLUDED from this lane (promoted to Wave 8 epic)
and is not in this queue.

Live bot forensics (Greptile MCP, PR #20):
  - 8 unaddressed Greptile comments
  - P0: PR size warning (INFRA-NOISE)
  - P1: Circuit breaker DateTime mixing -- ActivateCircuitBreakerIfThreshold (Trailing.StopUpdate.cs)
  - P1: DateTime.Now latency mixing in StopSync vs UtcNow (Trailing path CreatedTime)
  - P1: LINQ .Values.Contains() in hot path -- pre-existing, DEFERRED
  - P2: Magic number 5 -- pre-existing, DEFERRED
  - P2: MarkTargetFilled bounds guard -- HALLUCINATION (switch expression without default
        is semantically identical to if-else chain without out-of-range handling)
  - P1: TryParseTargetMode silent diagnostic loss -- outside scope (IPC.cs, not in cluster)
  - P1: IsOrderForThisInstrument -- ALREADY FIXED (NEW-F2 in lane L1)

---

## Finding Classification

### NEW-F3: CaptureTargetSnapshot / RefreshTargetSnapshot deduplication
**Classification: HALLUCINATION**
**Reason**: Methods named `CaptureTargetSnapshot` and `RefreshTargetSnapshot` do not exist
in src/V12_002.Orders.Management.StopSync.cs. The actual snapshot logic uses
`ValidateAndSnapshotPositions()` (line 121) which calls `activePositions.ToList()` exactly
once. No redundant snapshot building exists. This was a CodeRabbit generic analysis based
on method names that were never in this codebase.
**Action: SKIP**

### NEW-F5: OrderId fallback in PurgeFollowerStopScanStopOrders
**Classification: VALID-LOGIC-BUG**
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs line 826
**OKF Rule**: Rule 5 -- production safety (independent_tracking, no ghost orders)
**Priority**: P2
**Confirmed**: `sc.Value == order` at line 826 is pure reference equality.
After NT reconnect, the broker returns a new Order instance for the same logical order.
Reference match fails silently -- follower stop is not purged, leaving a ghost.
Fix: Add `|| (sc.Value?.OrderId != null && sc.Value.OrderId == order?.OrderId)` fallback.
**Action: FIX**

### NEW-F6: Cascade suppression active-state guard
**Classification: VALID-LOGIC-BUG**
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs line 908
**OKF Rule**: Rule 5 -- production safety (defense in depth)
**Priority**: P2
**Confirmed**: `_followerReplaceSpecs.TryGetValue(followerKey, out b948FsmSpec)` suppresses
cascade for ANY spec, including SubmitFailed and Idle states. FollowerReplaceState enum
has: Idle, PendingCancel, Submitting, SubmitFailed. The guard should only suppress when
state is PendingCancel or Submitting (active replace states). A SubmitFailed spec should
NOT block cascade cleanup.
**Action: FIX**

### NEW-F7: Ghost-order window in stopOrders Enqueue path
**Classification: VALID-LOGIC-BUG**
**File**: src/V12_002.Orders.Management.StopSync.cs line 602
**OKF Rule**: Rule 5 -- production safety (staleness_guard, no ghost orders)
**Priority**: P2
**Confirmed**: `stopOrders` is keyed by entryName (string). Line 711 stores new Order
reference: `ctx.stopOrders[en966] = ns966`. On broker reconnect, NT returns a new Order
object for the same logical stop. `stopOrders[entryName]` still finds the key but returns
the old (stale) reference. `currentStop.OrderState` on a stale reference may not reflect
the live broker state, causing `CancelOrderForReplace` to be called on the wrong object.
Fix: After retrieving `stopOrders[entryName]`, reconcile with Account.Orders if the
retrieved order appears stale (OrderState is inconsistent). Simpler fix: normalize the
stop order reference by checking Account.Orders for a matching OrderId when the dict
contains a stale reference.

The simplest safe fix: In `CreateNewStopOrder`, when `Enqueue` stores the new stop, also
log a print confirming the OrderId so any stale reference is detectable. For the ghost
window itself: add OrderId-based reconciliation before cancel in `UpdateStopQuantity_Execute`
(line 602).
**Action: FIX**

---

## Additional Bot Findings from PR #20 (Greptile live comments, not in fix_queue)

These are NEW findings identified during triage. They are not in the fix_queue but
require assessment:

### G-01: DateTime mixing -- circuit breaker path
**File**: src/V12_002.Trailing.StopUpdate.cs ~L339-343 and src/V12_002.Trailing.cs ~L342
**Classification**: VALID-LOGIC-BUG (P1 -- circuit breaker permanently frozen)
**Description**: `ActivateCircuitBreakerIfThreshold` sets `circuitBreakerActivatedTime`
to `DateTime.UtcNow`, but reset check in `Trailing.cs` uses `DateTime.Now`. On machines
west of UTC, the circuit breaker can never reset after the first activation (difference
is hours, not seconds).
**Status**: NOT IN FIX_QUEUE -- but is a critical production bug. Escalating to triage
as additional finding G-01. Will be fixed in this lane as it is in scope (StopSync cluster).

### G-02: DateTime.Now in latency measurement (Trailing.StopUpdate.cs line 176, 316)
**File**: src/V12_002.Trailing.StopUpdate.cs
**Classification**: VALID-DNA (DateTime.Now in production logic)
**Description**: PendingStopReplacement.CreatedTime stamped with DateTime.Now while
StopSync latency check uses DateTime.UtcNow. Latency readings will be off by UTC offset.
**Status**: This was partially fixed as NEW-F1a (Trailing.StopUpdate.cs) in lane L1.
Need to verify if the specific lines 176 and 316 were actually fixed.

---

## Summary

| Finding | Classification | Action |
|---------|---------------|--------|
| NEW-F3 | HALLUCINATION | SKIP |
| NEW-F5 | VALID-LOGIC-BUG | FIX |
| NEW-F6 | VALID-LOGIC-BUG | FIX |
| NEW-F7 | VALID-LOGIC-BUG | FIX |
| G-01 | VALID-LOGIC-BUG | Assess (in-scope StopSync) |
| G-02 | VALID-DNA | Verify already fixed |
| Others | INFRA-NOISE/HALLUCINATION/DEFERRED | SKIP |

Actionable in this lane: NEW-F5, NEW-F6, NEW-F7
Additional assessment needed: G-01, G-02

TRIAGE_DONE PR#20-deferred logic=3 mech=0 dna=0 hall=1 noise=1 fixed=0
