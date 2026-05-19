# JULES P4 AUDIT: V14.2 Sovereign Photon

## Audit Date: 2026-04-04

## Verdict: REVISION REQUIRED

---

## Component 1: SPSC Ring Buffer + CRC16

### 1.1 .NET 4.8 Compatibility

**PASS with observations.**

- `Volatile.Read(ref long)` and `Volatile.Write(ref long, long)` -- available in `System.Threading` since .NET 4.5. Confirmed safe.
- `BitConverter.DoubleToInt64Bits(double)` -- available since .NET Framework 2.0. Confirmed safe.
- `unchecked` blocks -- standard C# feature, no version dependency.
- Generic constraint `where T : struct` -- supported since C# 2.0 / .NET 2.0.
- No nullable reference types, switch expressions, or default interface methods detected.
- No `Span<T>`, `ArrayPool<T>`, or `stackalloc` in expressions.

**Observation 1.1a**: The padding strategy uses 7 `long` fields (`_pad1` through `_pad7`) to separate producer and consumer cursors onto different cache lines. This is a reasonable heuristic for .NET 4.8 where `[StructLayout]` cache-line control is unavailable. However, the JIT may reorder fields in a sealed class. Since `SPSCRing<T>` is a `sealed class` (not a struct), the CLR is free to rearrange instance fields. The padding fields have no `LayoutKind.Sequential` attribute enforced on the class. This makes cache-line isolation a best-effort heuristic, not a guarantee.

**Severity**: LOW. False sharing would only degrade performance, not correctness. Both cursors are protected by Volatile barriers.

**Observation 1.1b**: The `_pad1` through `_pad7` fields are never read or written. The C# compiler will emit warnings for unused fields (CS0169). NinjaTrader's embedded compiler treats warnings as errors in some configurations.

**Action Required**: Add `#pragma warning disable 0169` / `#pragma warning restore 0169` around the padding fields, or add `[System.Diagnostics.CodeAnalysis.SuppressMessage]` attributes.

### 1.2 SPSC Correctness

**ISSUE 1.2a [MEDIUM]: TryDequeue sets `crcValid = true` unconditionally.**

The consumer method at plan line 178 sets `crcValid = true` and comments "Caller must verify CRC externally." However, the `TryDequeue` signature includes `out bool crcValid` which semantically implies the ring itself is validating. The consumer code in Component 4 (PumpFleetDispatch) does perform external CRC verification, so this is functionally correct, but the API is misleading. The `crcValid` out parameter should either be removed (since it is always true) or the ring should compute and compare internally.

**Severity**: LOW. Functional correctness is maintained. API clarity issue only.

**ISSUE 1.2b [LOW]: Count property is not linearizable.**

`Count` reads `_producerCursor` and `_consumerCursor` with separate `Volatile.Read` calls. Between the two reads, the producer could advance, making `Count` momentarily negative if the consumer overtakes the cached producer position. For a single-producer single-consumer pattern this cannot happen in practice (the consumer only advances after confirming `cursor < producerPos`), but the property could return a stale value. This is acceptable for diagnostics but should not be used for capacity decisions.

**Severity**: LOW. Used only in diagnostics.

### 1.3 CRC16 Implementation

**PASS.** The CRC16-CCITT implementation is standard bitwise computation. Zero-allocation via byte extraction from `int`/`long`/`double`. No issues.

**Observation 1.3a**: CRC16 provides only 65,536 distinct values. For integrity verification of a 7-field struct, this is adequate for torn-read detection (which would produce wildly different values), but is not a cryptographic guarantee. The plan correctly positions this as "torn cross-thread read detection" only.

### 1.4 NinjaScript Partial Class Requirements

**PASS.** Both new files declare `namespace NinjaTrader.NinjaScript.Strategies` and `public partial class V12_002 : Strategy`. This matches the existing codebase pattern (verified against `V12_002.cs`, `V12_002.SIMA.cs`, etc.).

---

## Component 2: PhotonOrderPool

### 2.1 .NET 4.8 Compatibility

**PASS.**

- `Interlocked.Decrement(ref int)`, `Interlocked.Increment(ref int)`, `Interlocked.Read(ref long)` -- all available since .NET 2.0.
- `ReferenceEquals` -- available since .NET 1.0.
- No C# 8+ features detected.

### 2.2 Pool Thread Safety

**ISSUE 2.2a [HIGH]: `Claim()` has an ABA race on the free stack.**

The `Claim()` method does:

```csharp
int top = Interlocked.Decrement(ref _freeTop);
int slotIndex = _freeStack[top];
```

Between the `Decrement` and the array read, another thread could `Release()` a slot, calling `Interlocked.Increment(ref _freeTop)` and writing to `_freeStack[top]`. Since `top` is a local snapshot, the caller reads the correct index. However, the `_freeStack` array itself is NOT protected by any memory barrier between the `Release()` write and the `Claim()` read. On x86 this is naturally ordered (TSO), but the CLR JIT for x64 could reorder the array read ahead of the `Interlocked` fence in edge cases.

**Mitigating factor**: The plan states both producer (dispatch) and consumer (pump) run on the strategy thread, separated only by `TriggerCustomEvent` cycles. If both `Claim()` and `Release()` are always called from the same strategy thread (even on different TriggerCustomEvent cycles), there is no true concurrent access and the race cannot manifest. The code must document this single-thread-only constraint explicitly.

**Severity**: HIGH if ever called from multiple threads. LOW if strategy-thread-only invariant is enforced and documented. The current code does NOT have a thread-safety comment on `Claim()`/`Release()`.

**Action Required**: Add an explicit comment: "MUST be called from strategy thread only. Not safe for concurrent access." Or add a `Thread.CurrentThread.ManagedThreadId` debug assertion.

**ISSUE 2.2b [HIGH]: `Release()` linear scan is O(n) in pool capacity.**

`Release()` iterates all `_capacity` slots to find which slot the returned `Order[]` belongs to via `ReferenceEquals`. For capacity 32 this is 32 iterations -- negligible in absolute terms. But this is an O(n) operation on the hot path (called after every `acct.Submit()` in PumpFleetDispatch). If pool capacity ever increases, this becomes a concern.

**More critically**: The plan does NOT specify WHERE `Release()` is called in the PumpFleetDispatch consumer. The pseudo-code at plan line 749 says `// _photonPool.Release(orderArray);` as a comment. This is not implemented code. The consumer section (Component 4) is incomplete -- see Issue 4.1.

**Severity**: MEDIUM. The linear scan itself is acceptable at capacity 32, but the missing Release call is a pool leak defect.

**ISSUE 2.2c [MEDIUM]: Pool exhaustion + ring full double fallback creates counter skew.**

When pool exhaustion triggers (line 676-678), a heap-allocated `Order[MaxOrdersPerSlot]` is used. This array is NOT from the pool, so `Release()` will fail the `ReferenceEquals` check (line 319-325 returns early). The pool's internal `_freeTop` counter was decremented during the failed `Claim()` but immediately restored via `Interlocked.Increment`. So far so good.

But if the ring is ALSO full (line 702-714), the heap-allocated `Order[]` is passed to the legacy `FleetDispatchRequest` instead. When PumpFleetDispatch processes this legacy request, it has `req.Orders` as the heap array. The consumer code MUST NOT attempt to call `_photonPool.Release(req.Orders)` on this legacy path. The plan does not specify how the consumer distinguishes pool-sourced vs heap-sourced `Order[]` arrays.

**Action Required**: The consumer must track whether the `Order[]` came from the pool. Options: (a) only release when processing ring slots, never when processing legacy queue; (b) accept that `Release()` silently ignores non-pool arrays (current behavior, line 325: `if (slotIndex < 0) return`). Option (b) works but leaks pool slots -- the ring path claimed a pool array but fell back to ConcurrentQueue, so the pool slot is orphaned.

Wait -- re-reading the flow: if the pool is exhausted, `_proxyOrders` is heap-allocated. Then if the ring is full, the `_proxyOrders` (heap-allocated) is passed to `FleetDispatchRequest.Orders`. The pool slot was never claimed (exhaustion returned null), so no pool slot is orphaned. This is correct.

But if the pool succeeds (returns a valid array) and THEN the ring is full, the pool-sourced `_proxyOrders` is passed to `FleetDispatchRequest.Orders`. When PumpFleetDispatch processes this legacy request, it uses `req.Orders` for `acct.Submit()`, then there is no code to release the pool slot. The pool slot is leaked until the next `State.Configure` reinitializes the pool.

**Severity**: MEDIUM. In the pool-success + ring-full scenario, pool slots leak permanently. With 32 slots and 12 accounts, 3 such leaks would bring the pool to 29, reducing headroom. Repeated rapid dispatches could exhaust the pool.

**Action Required**: Either (a) release the pool slot in the fallback path, or (b) store a flag in `FleetDispatchRequest` indicating pool-sourced arrays that need release after pump processing.

### 2.3 Fleet Scaling: 32 Slots for 12 Accounts

**ISSUE 2.3a [HIGH]: 32-slot ring capacity is insufficient for burst scenarios.**

The plan states: "Power-of-2 capacity (32 slots for 12-account fleet + headroom)." Each dispatch produces one slot per active fleet account. With 12 accounts (minus master = 11 followers), one signal consumes 11 slots.

The pump processes ONE slot per `TriggerCustomEvent` cycle. Each cycle is ~1-5ms based on NT8 scheduler latency. So 11 slots take 11-55ms to drain.

If a second signal fires before the first pump cycle completes (which is realistic: MOMO can fire on consecutive bars at 100ms tick update cadence), the ring receives 11 + 11 = 22 slots. With a third rapid signal: 33 > 32. The ring overflows and hits the ConcurrentQueue fallback.

Worse: since NT8's `TriggerCustomEvent` is not guaranteed to fire between two consecutive `OnBarUpdate` calls, it is possible for 3 signals to enqueue before any pump cycle runs.

The ConcurrentQueue fallback path prevents data loss, but it defeats the purpose of the zero-allocation design. More critically, it creates the pool-slot-leak described in Issue 2.2c.

**Severity**: HIGH. The plan's own "fleet scaling" analysis (lines 762-786) acknowledges 12 accounts but does not analyze burst scenarios. The claim "32 slots sufficient" is not rigorously justified.

**Recommendation**: Increase ring capacity to 64 (next power of 2). This handles 5 concurrent signals for 12 accounts (5 *11 = 55 < 64) with headroom. Pool capacity should match: 64 slots with 64 pre-allocated `Order[7]` arrays. Memory cost: 64* 7 * 8 bytes (reference pointers) = 3.5 KB. Negligible.

---

## Component 3: ExecutionIdRing (ADR-011)

### 3.1 .NET 4.8 Compatibility

**PASS.**

- `unchecked((long)0xcbf29ce484222325L)` -- standard unchecked cast, .NET 2.0+.
- No C# 8+ features.
- `DateTime.UtcNow.Ticks` -- .NET 1.0+.

### 3.2 Hash Collision Analysis

**ISSUE 3.2a [LOW]: FNV-1a 64-bit collision probability claim needs qualification.**

Plan states: "Collision probability for 500 entries: ~1.4 x 10^-14 (negligible)." This is the birthday-problem probability for 500 random 64-bit values: P = n*(n-1)/(2*2^64) ~ 6.8 x 10^-15. The claim is approximately correct.

However, execution IDs from NinjaTrader/broker APIs are NOT random strings -- they often follow patterns like "exec-12345-67890" with sequential numeric components. FNV-1a is designed for good distribution of structured inputs, but the collision probability for structured inputs is higher than for truly random strings. The practical risk remains negligible for 512 entries.

**Severity**: LOW. Theoretical concern only. A collision would cause one valid execution to be treated as a duplicate and skipped -- this matches the existing risk profile of the HashSet approach (which also has hash collisions, though at the .NET `GetHashCode` 32-bit level, which is WORSE).

### 3.3 EMPTY_KEY Sentinel Collision

**PASS.** The code guards `if (hash == EMPTY_KEY) hash = 1L;` at line 468. This prevents the FNV hash of an empty string (which returns 0, matching `EMPTY_KEY`) from being stored as a sentinel. Correct.

### 3.4 Robin Hood Deletion Correctness

**ISSUE 3.4a [MEDIUM]: `TableRemove` rehash loop may not terminate correctly for full tables.**

The `TableRemove` method (line 521-548) uses Robin Hood deletion: after removing an entry, it rehashes all subsequent entries until an empty slot is found. The loop `while (_tableKeys[next] != EMPTY_KEY)` iterates forward. If the table is nearly full (load factor approaching 1.0), this loop could theoretically wrap around the entire table.

With ring capacity 512 and table capacity 1024, the maximum load factor is 512/1024 = 0.5. At this load factor, the expected probe length is ~1.5 and the probability of a probe chain spanning more than 10 slots is negligible. The Robin Hood rehash will terminate quickly.

**However**: The rehash loop calls `TableInsert(rehashKey, rehashVal)` which also probes for an empty slot. If the removed slot was in a long probe chain, the rehashed entries may probe into the gap left by removal, creating correct placement. This is standard Robin Hood deletion. The implementation appears correct.

**Severity**: LOW. Correctness is maintained. Performance is bounded by load factor < 0.5 guarantee.

### 3.5 Fallback Dedup Key Regression

**ISSUE 3.5a [MEDIUM]: Fallback dedup key format changed -- potential regression.**

The existing fallback dedup (V12_002.Orders.Callbacks.Execution.cs, lines 218-221) constructs:

```csharp
string dedupOrderIdentity = GetStableHash(uniqueOrderId);  // FNV-1a 32-bit, returns hex string
string fallbackKey = string.Format("{0}|{1}", dedupOrderIdentity, dedupFilledQuantity);
```

The plan's replacement (line 633) constructs:

```csharp
string _fallbackKey = (order.OrderId ?? orderName) + "|" + cumulativeFilledQuantity;
```

These produce DIFFERENT keys for the same execution event:

- Old: `GetStableHash("exec-123") + "|" + 5` = `"A1B2C3D4|5"` (32-bit hex hash of OrderId, then filled qty)
- New: `"exec-123" + "|" + 5` = `"exec-123|5"` (raw OrderId, then filled qty)

Both keys are then hashed through `FnvHash64()`, so the final hash values are completely different from each other AND from the old `HashSet<string>` entries.

**This is NOT a functional regression** because the dedup cache is ephemeral (cleared on restart, not persisted). After the upgrade, the new ring starts empty. But it does mean that during a hot-reload (NT8 F5 recompile without restart), any in-flight executions that were already processed by the old dedup but are replayed by the broker after recompile would NOT be caught by the new dedup. This is the same risk as any restart.

Additionally, the plan's fallback key uses `order.OrderId ?? orderName`. The existing code uses `execution.Order.OrderId ?? execution.Order.Name` (not `orderName`). In context, `orderName` is the method parameter which is `execution.Order.Name`. But `order` in the plan snippet is `execution.Order`. So these are semantically equivalent, but the plan uses different variable names than the actual method parameters (the method signature has `string orderName` and `Execution execution`).

**Severity**: MEDIUM. The variable name mismatch between the plan's replacement code and the actual method parameters could cause a compile error. The plan must use the exact parameter names from the existing method signature.

**Action Required**: Verify the replacement code uses the correct variable names from the `ProcessOnExecutionUpdate` method signature: `execution.Order.OrderId`, `orderName`, `execution.Order.Filled` or `quantity`.

### 3.6 String Allocation in Fallback Path

**Observation**: The plan acknowledges (line 643) that the fallback path still allocates a string for hashing. The string `(order.OrderId ?? orderName) + "|" + cumulativeFilledQuantity` creates a temporary string on every fallback dedup check. This is unavoidable. The improvement is that the string is NOT stored in a `HashSet` -- only its 8-byte hash is retained. This is a genuine improvement over the old approach.

---

## Component 4: Dispatch Pipeline Integration

### 4.1 CRITICAL: Consumer-Side Integration is Incomplete

**ISSUE 4.1a [BLOCKER]: PumpFleetDispatch ring consumer does not have the Order[] array.**

The `FleetDispatchSlot` struct (plan lines 236-253) does NOT contain an `Order[]` field. The orders are claimed separately from `PhotonOrderPool` and populated in the dispatch loop (plan line 672-683). But the `FleetDispatchSlot` that is enqueued into the ring does NOT carry a reference to the `Order[]` array.

The existing `PumpFleetDispatch` consumer (SIMA.Fleet.cs:101-134) requires `req.Orders` to:

1. Call `req.Account.Submit(req.Orders)` at line 134
2. Iterate `req.Orders` to populate `FollowerBracketFSM` fields (entry, stop, targets) at lines 101-131
3. Register `ord.OrderId` for O(1) FSM lookup at lines 152-158

The plan's consumer section (lines 724-756) says:

```text
// Build Order[] from pool slot (already claimed in dispatch)
// ... (process _ringSlot same as existing FleetDispatchRequest logic) ...
```

This is a pseudo-code placeholder, not implementation. There is NO mechanism to carry the `Order[]` from the producer to the consumer via the ring. The `FleetDispatchSlot` struct has value-type metadata (prices, quantities) but the actual NinjaTrader `Order` objects are not referenced.

**Root Cause**: The `FleetDispatchSlot` was designed for CRC16 verification over value-type fields. Reference types (Account, Order[]) are excluded from CRC. But the slot still NEEDS the Order references for the consumer to function. The slot has `Account` (reference type) but is missing the `Order[]` or a pool-slot index to retrieve it.

**Fix Required**: Either:
(a) Add `public Order[] Orders;` to `FleetDispatchSlot` (simplest, matches FleetDispatchRequest pattern). This is a reference, not a value copy -- no GC pressure beyond the existing pool-managed array.
(b) Add `public int PoolSlotIndex;` to `FleetDispatchSlot` and retrieve via `_photonPool.GetSlotByIndex(index)`. This requires a new accessor method on PhotonOrderPool and an index-based claim API.

Option (a) is strongly recommended. The Orders field is already a managed reference (unavoidable per NT8 API), and the slot already contains `Account` (also a managed reference). Adding one more reference field does not change the allocation profile.

**Severity**: BLOCKER. The plan cannot be implemented as written. The consumer will not compile -- there is no way to get the Order objects from a consumed ring slot.

### 4.2 Missing Consumer FSM Population Logic

**ISSUE 4.2a [HIGH]: Full consumer body is placeholder.**

Plan lines 745-748:

```text
// Valid slot -- submit to broker
// Build Order[] from pool slot (already claimed in dispatch)
// ... (process _ringSlot same as existing FleetDispatchRequest logic) ...
// After successful submit, release Order[] back to pool
```

The existing PumpFleetDispatch consumer (SIMA.Fleet.cs:62-191) is 130 lines of critical logic:

- Stale dispatch rejection via MetadataGuardTimestamp (line 69)
- FSM creation with Entry/Stop/Target order assignment (lines 89-131)
- `acct.Submit(req.Orders)` (line 134)
- DispatchSyncPending clear (line 135)
- FSM promotion from PendingSubmit to Submitted (lines 138-146)
- OrderId-to-FSM key registration (lines 148-158)
- Full error rollback in catch block (lines 164-183)
- `_pendingFleetDispatchCount` decrement in finally (line 187)
- Chain pump via TriggerCustomEvent (line 189)

The plan provides NONE of this for the ring path. An engineer implementing this plan would need to duplicate or factor out the entire 130-line consumer body. This is a significant implementation gap that should be specified.

**Recommendation**: The plan should either (a) specify the complete consumer body for the ring path, or (b) refactor the existing consumer into a shared method `ProcessDispatchSlot(Account, Order[], string fleetEntryName, string expectedKey, int reservedDelta, long signalTicks)` that both the ring path and the legacy ConcurrentQueue path call. Option (b) is cleaner.

**Severity**: HIGH. Without this specification, the engineer must reverse-engineer the consumer contract, risking missed error-handling branches.

### 4.3 _pendingFleetDispatchCount Tracking on Ring Path

**ISSUE 4.3a [MEDIUM]: Double-counting on ring-full fallback.**

The dispatch code (plan line 701) increments `_pendingFleetDispatchCount` before attempting ring enqueue. If the ring is full (line 702), the code falls through to enqueue in `_pendingFleetDispatches` ConcurrentQueue (line 706). The count was already incremented once. The ConcurrentQueue path in the existing code (SIMA.Dispatch.cs:379) also increments the count.

Wait -- re-reading plan line 701: `Interlocked.Increment(ref _pendingFleetDispatchCount)` happens ONCE, before the ring attempt. If ring succeeds, count is correct (+1). If ring fails, the fallback enqueues to ConcurrentQueue WITHOUT a second increment (plan lines 706-714 do not re-increment). This is correct -- the single increment covers the fallback path.

However, the decrement happens in `PumpFleetDispatch.finally` (SIMA.Fleet.cs:187). If the ring path early-returns (plan line 756), the decrement MUST still happen. The plan's ring path (lines 726-756) does not include a `finally` block with `Interlocked.Decrement`. On the CRC failure path (line 738), the plan does call `Interlocked.Decrement` explicitly. But on the success path (line 756 `return`), no decrement is shown.

**Severity**: MEDIUM. If the ring path returns without decrementing `_pendingFleetDispatchCount`, the count will monotonically increase, eventually blocking flatten operations or other guards that check `_pendingFleetDispatchCount > 0`.

**Action Required**: The ring consumer path MUST decrement `_pendingFleetDispatchCount` in a `finally` block, mirroring the existing ConcurrentQueue consumer pattern.

### 4.4 isFlattenRunning / EnableSIMA Guard for Ring

**ISSUE 4.4a [MEDIUM]: Abort-and-drain logic only drains ConcurrentQueue, not ring.**

The existing PumpFleetDispatch abort path (SIMA.Fleet.cs:48-59) drains `_pendingFleetDispatches` when `isFlattenRunning || !EnableSIMA`. The plan does not add ring buffer draining to this abort path.

If a flatten is triggered while the ring has pending slots, those slots will never be consumed (the pump aborts before reaching the ring drain). The slots remain in the ring, occupying pool capacity. Worse: the `_pendingFleetDispatchCount` was incremented for each ring slot, but never decremented (the abort path only decrements for ConcurrentQueue items).

**Action Required**: The abort-and-drain path must also drain the ring buffer:

```csharp
FleetDispatchSlot abortSlot;
ushort abortCrc;
bool abortCrcValid;
while (_photonDispatchRing.TryDequeue(out abortSlot, out abortCrc, out abortCrcValid))
{
    Interlocked.Decrement(ref _pendingFleetDispatchCount);
    // Release pool slot if applicable
    // Rollback expectedPositions delta
    if (abortSlot.ReservedDelta != 0)
        AddExpectedPositionDeltaLocked(abortSlot.ExpectedKey, -abortSlot.ReservedDelta);
    ClearDispatchSyncPending(abortSlot.ExpectedKey);
}
```

**Severity**: MEDIUM. Without this, flatten during active dispatch leaves orphaned ring slots and leaked `_pendingFleetDispatchCount`.

### 4.5 State.Terminated Ring Drain

**ISSUE 4.5a [MEDIUM]: `DrainQueuesForShutdown()` does not drain the Photon ring.**

The existing `DrainQueuesForShutdown()` (Lifecycle.cs:431-451) drains `ipcCommandQueue` and `_cmdQueue` (actor commands). It does NOT drain `_pendingFleetDispatches` (which has its own drain in the abort path). The plan does not specify adding ring buffer drain to either `DrainQueuesForShutdown()` or the `State.Terminated` sequence.

If the strategy is terminated while the ring has pending slots, the ring's internal arrays hold references to `Account` and (if Issue 4.1a is fixed) `Order` objects. These references prevent GC collection of the Account/Order objects until the `V12_002` instance itself is collected.

**Severity**: LOW. The pool and ring are instance fields -- they will be collected when the strategy instance is collected. No resource leak beyond normal GC lifecycle. But `_pendingFleetDispatchCount` will be non-zero at termination, which may trigger debug assertions if any are added.

**Recommendation**: Add ring drain to `DrainQueuesForShutdown()` for completeness and diagnostic cleanliness.

### 4.6 TriggerCustomEvent Pump Initiation

**ISSUE 4.6a [MEDIUM]: Dispatch code must prime the pump for ring path.**

The existing dispatch code (SIMA.Dispatch.cs:485) primes the pump:

```csharp
if (!_pendingFleetDispatches.IsEmpty)
    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
```

With the ring buffer, the pump must also be primed when ring items are enqueued. The plan's dispatch code (line 702) enqueues to the ring but does NOT add a `TriggerCustomEvent` pump prime for the ring path. The existing pump prime only checks `_pendingFleetDispatches.IsEmpty`.

**Action Required**: After the fleet loop, the pump prime condition must check BOTH the ring and the legacy queue:

```csharp
if (!_photonDispatchRing.IsEmpty || !_pendingFleetDispatches.IsEmpty)
    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
```

**Severity**: MEDIUM. Without this fix, ring-enqueued slots will never be pumped until a legacy queue item also happens to be enqueued and triggers the pump. In the ideal case where the ring handles all dispatches, the pump would never fire.

Wait -- the plan shows the pump prime at plan line 753-754 inside the PumpFleetDispatch consumer's self-reschedule. But the INITIAL prime (after the fleet loop in Dispatch.cs) must also check the ring. Checking the plan... this is not specified. The plan only shows modifications to the per-account loop body, not to the pump prime after the loop.

### 4.7 Line Number References

**ISSUE 4.7a [LOW]: Several plan line references are approximate or inaccurate.**

- Plan says "Replace ordersToSubmit pattern (approximate lines 259-390)" -- actual ordersToSubmit creation is at SIMA.Dispatch.cs:259. The range is approximately correct but the end boundary depends on the target loop exit at ~line 397. Minor inaccuracy.
- Plan says "OLD (lines 202-212 in ProcessOnExecutionUpdate)" -- actual lines in the source are 202-212 (verified). Correct.
- Plan says "NEW fields after line 323" in V12_002.cs -- actual line 323 is the end of `FlattenWorkItem` struct (verified at line 323 `}`). Correct.

**Severity**: LOW. Line numbers are approximate but close enough for surgical implementation.

---

## Cross-Component Risks

### R1: Pool Slot Leak on Ring-Full Path (MEDIUM)

Described in Issue 2.2c. When `_photonPool.Claim()` succeeds but `_photonDispatchRing.TryEnqueue()` fails, the pool slot is passed to the legacy `FleetDispatchRequest.Orders`. After PumpFleetDispatch processes the legacy request, the pool slot is NOT released (PumpFleetDispatch's existing code does not call `_photonPool.Release()`). The pool slowly leaks slots.

### R2: Dual-Path Consumer Complexity (MEDIUM)

The consumer now has two paths: ring (zero-alloc) and ConcurrentQueue (legacy fallback). Both must maintain identical invariants:

- MetadataGuardTimestamp stale check
- FSM creation and order-to-FSM registration
- DispatchSyncPending clear
- expectedPositions delta rollback on error
- Full dict cleanup on error
- _pendingFleetDispatchCount decrement in finally
- Pool slot release (ring path only)
- Pump chain (check both ring and queue)

Any divergence between the two paths creates a behavior asymmetry bug. The plan should specify a shared `ProcessFleetSlot()` helper to guarantee identical behavior.

### R3: Diagnostic Observability Gap (LOW)

The plan adds `[PHOTON_HEALTH]` logging to the REAPER cycle. However, there is no logging for:

- How many dispatches used the ring path vs fallback path per session
- Whether pool exhaustion events correlate with ring-full events
- Ring high-water-mark (maximum concurrent occupancy)

These would be valuable for capacity tuning.

### R4: No Unit Test or Isolation Test Specified (LOW)

The plan specifies only SIM tests (manual). No automated test for:

- SPSCRing wraparound at capacity boundary
- PhotonOrderPool claim/release cycle balance
- ExecutionIdRing eviction correctness at boundary
- CRC16 torn-read simulation

These components are pure data structures with no NT8 dependency and could be unit-tested outside NinjaTrader.

---

## Recommendations

### MUST FIX (Blockers)

1. **Add `Order[]` field (or pool index) to `FleetDispatchSlot`** -- the struct is missing the essential data the consumer needs. Without this, the ring consumer cannot call `acct.Submit()` or populate FSMs. (Issue 4.1a)

2. **Specify complete ring consumer body in PumpFleetDispatch** -- either provide the full 130-line consumer body for the ring path, or refactor into a shared `ProcessFleetSlot()` method. The current placeholder is not implementable. (Issue 4.2a)

### SHOULD FIX (High/Medium)

1. **Increase ring + pool capacity from 32 to 64** -- protects against burst scenarios with 12 accounts. Memory cost is negligible (~7 KB total). (Issue 2.3a)

2. **Add ring drain to PumpFleetDispatch abort path** -- prevents orphaned slots and leaked `_pendingFleetDispatchCount` during flatten. (Issue 4.4a)

3. **Add `_pendingFleetDispatchCount` decrement to ring consumer path** -- must be in a `finally` block. (Issue 4.3a)

4. **Fix pump prime to check both ring and legacy queue** -- both in the initial prime (after fleet loop in Dispatch.cs) and in the self-reschedule. (Issue 4.6a)

5. **Add pool slot release to ring consumer success path** -- prevent pool leak. Currently only a comment placeholder. (Issue 2.2b)

6. **Handle pool-sourced Order[] in ring-full fallback** -- either release the pool slot immediately, or ensure the legacy consumer releases it after processing. (Issue 2.2c / R1)

7. **Document strategy-thread-only constraint on PhotonOrderPool** -- or add debug assertions. (Issue 2.2a)

8. **Verify fallback dedup replacement uses correct variable names** from the `ProcessOnExecutionUpdate` method signature. (Issue 3.5a)

### NICE TO HAVE (Low)

1. **Add `#pragma warning disable 0169`** for padding fields in SPSCRing. (Issue 1.1b)

2. **Add ring drain to `DrainQueuesForShutdown()`** for clean termination. (Issue 4.5a)

3. **Add ring/pool path-split diagnostics** (ring-hits vs fallback-hits counter). (R3)

---

## Summary Table

| ID | Severity | Component | Issue |
|----|----------|-----------|-------|
| 4.1a | BLOCKER | Dispatch Integration | FleetDispatchSlot missing Order[] -- consumer cannot function |
| 4.2a | HIGH | Dispatch Integration | Consumer body is placeholder, not implementation |
| 2.2a | HIGH* | PhotonOrderPool | Thread-safety not documented (mitigated if single-thread) |
| 2.3a | HIGH | Fleet Scaling | 32-slot capacity insufficient for burst (12 accts x 3 signals = 36) |
| 2.2b | MEDIUM | PhotonOrderPool | Release() call missing from consumer (pool leak) |
| 2.2c | MEDIUM | PhotonOrderPool | Ring-full path leaks pool slots |
| 3.5a | MEDIUM | ExecutionIdRing | Fallback key variable names may not match method signature |
| 3.4a | MEDIUM | ExecutionIdRing | Robin Hood deletion correctness (verified OK at load < 0.5) |
| 4.3a | MEDIUM | Dispatch Integration | _pendingFleetDispatchCount not decremented on ring success path |
| 4.4a | MEDIUM | Dispatch Integration | Abort-and-drain does not drain ring buffer |
| 4.5a | MEDIUM | Lifecycle | State.Terminated does not drain ring |
| 4.6a | MEDIUM | Dispatch Integration | Pump prime does not check ring emptiness |
| 1.1a | LOW | SPSCRing | Cache-line padding not guaranteed by CLR layout |
| 1.1b | LOW | SPSCRing | Unused field warnings from padding |
| 1.2a | LOW | SPSCRing | crcValid out param always true (misleading API) |
| 1.2b | LOW | SPSCRing | Count property not linearizable |
| 1.3a | LOW | CRC16 | 16-bit checksum adequate for torn-read only |
| 3.2a | LOW | ExecutionIdRing | Collision probability for structured inputs |

*Severity downgraded to LOW if strategy-thread-only invariant is documented.

---

## Verdict Rationale

**REVISION REQUIRED** due to:

1. One BLOCKER (FleetDispatchSlot missing Order[] field -- consumer cannot compile or function)
2. One HIGH gap (consumer body is a placeholder, not implementable specification)
3. One HIGH capacity concern (32-slot ring insufficient for 12-account burst)
4. Multiple MEDIUM issues around counter tracking, drain paths, and pump priming

The plan's architectural concept is sound -- SPSC ring buffer with CRC16 for zero-alloc dispatch, pre-allocated pool, and hash-ring dedup are all valid optimizations for the .NET 4.8 / NinjaTrader 8 environment. The .NET 4.8 API surface is correctly targeted. The partial class structure is correct. The CRC16 and FNV-1a implementations are correct.

However, the dispatch pipeline integration (Component 4) has critical specification gaps that would force the implementing engineer to make design decisions that belong in the architecture plan. The plan must be revised to close these gaps before engineering can proceed.
