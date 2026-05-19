# CODEX P4 AUDIT: V14.2 Sovereign Photon

## Audit Date: 2026-04-04

## Auditor: CODEX (P4 Engineer)

## Plan File: docs/brain/implementation_plan.md (Build 1109.003-v14.2)

## Verdict: REVISION REQUIRED

Seven issues identified. Three are blocking (B1-B3). Four are non-blocking observations (O1-O4).

---

### Component 1: SPSC Ring Buffer + CRC16

**Thread Safety: PASS WITH OBSERVATIONS**

The Volatile.Read/Write barriers in SPSCRing are correctly placed:

- `TryEnqueue`: Reads `_producerCursor` via Volatile.Read, reads `_consumerCursor` via Volatile.Read for fullness check, then writes the slot and checksum BEFORE publishing cursor via `Volatile.Write(ref _producerCursor, cursor + 1)`. This is the correct store-release pattern -- the consumer will never observe the new cursor value before the slot data is committed.

- `TryDequeue`: Reads `_consumerCursor` via Volatile.Read, reads `_producerCursor` via Volatile.Read for emptiness check, copies the slot, then advances via `Volatile.Write(ref _consumerCursor, cursor + 1)`. Correct acquire-release pattern.

**[O1] CRC16 is solving a phantom problem.** The plan states "Both are strategy-thread but on DIFFERENT TriggerCustomEvent cycles" (line 112). Since TriggerCustomEvent dispatches all serialize on the NT8 strategy thread, the producer and consumer NEVER execute concurrently. There is zero possibility of torn reads. In a true SPSC scenario across threads, CRC16 would add value. Here, both producer and consumer are the same thread at different logical times. The ring buffer is being used as a pre-allocated circular queue, not a cross-thread communication channel.

CRC16 adds ~200ns per slot (16 Crc16Byte calls) for zero protection benefit. It is harmless but misleading -- it implies a threading hazard that does not exist. The plan should document this as a defense-in-depth measure (guarding against future refactoring that might move producer/consumer to different threads), NOT as torn-read detection for the current architecture.

**[O2] Cache-line padding is unreliable on .NET Framework 4.8.** The plan uses 7 `long` pad fields between `_producerCursor` and `_consumerCursor` (lines 122-125) to achieve 64-byte separation. However:

1. The CLR does not guarantee field layout order for reference types (sealed class). `[StructLayout(LayoutKind.Sequential)]` only applies to structs and P/Invoke, not to `sealed class` instances.
2. The GC may relocate objects, invalidating any cache-line alignment.
3. The JIT compiler may reorder fields for alignment optimization.

Since both producer and consumer are the same thread (O1), false sharing is impossible anyway. The padding is dead weight. If this design is ever promoted to true cross-thread SPSC, the padding should use `[FieldOffset]` on a struct or a dedicated padding struct, not class fields.

---

### Component 2: PhotonOrderPool

**Thread Safety: PASS WITH OBSERVATIONS**

The Interlocked free-stack for Claim() is correctly structured:

```text
Claim: top = Interlocked.Decrement(ref _freeTop);
       if (top < 0) { Interlocked.Increment(ref _freeTop); return null; }
       slotIndex = _freeStack[top];
```

This is a standard lock-free stack pop. The Decrement atomically reserves a slot. The restore on underflow (Increment) is correct. Under concurrent contention, two threads could race to claim `_freeStack[top]` at the same index, but since both producer and consumer are the same strategy thread (see O1), this race cannot occur.

**[B1] CRITICAL: Release() uses O(N) linear scan -- breaks O(1) contract.** (Lines 316-325 of plan)

```csharp
for (int i = 0; i < _capacity; i++)
{
    if (ReferenceEquals(_orderArrays[i], arr))
    {
        slotIndex = i;
        break;
    }
}
```

With `PhotonPoolCapacity = 32`, this scans up to 32 array references per release. While tolerable at 32 slots, this violates the plan's "zero overhead" ethos and makes the pool O(N) on release. The fix is simple: the pool should embed the slot index INTO the array it hands out (e.g., store it at `_orderArrays[i][MaxOrdersPerSlot]` by allocating `MaxOrdersPerSlot + 1`), or maintain a reverse-lookup `Dictionary<Order[], int>`, or have the caller pass back the slot index. Since FleetDispatchSlot is a struct, add a `PoolSlotIndex` field to carry the index through the ring.

**Recommended fix**: Add `public int PoolSlotIndex;` to `FleetDispatchSlot`. Set it in Claim(). Use it in Release() instead of scanning. This is O(1) and zero-alloc.

---

### Component 3: ExecutionIdRing (ADR-011)

**Thread Safety: PASS**

The single-threaded assumption IS valid. `ProcessOnExecutionUpdate` is called from the actor drain path (`Enqueue(ctx => ctx.ProcessOnExecutionUpdate(...))`), which is serialized by `_drainToken` (V12_002.cs line 300, 381). The `_drainToken` uses `Interlocked.CompareExchange` to guarantee only one thread enters `DrainActor()` at a time, and `ProcessOnExecutionUpdate` runs within that drain. No concurrent access to `ExecutionIdRing` is possible.

**Hash collision analysis**: FNV-1a 64-bit with 512 entries. Birthday paradox collision probability: ~512^2 / (2 * 2^64) = ~7.1 x 10^-15. Negligible. This is safe.

**Open-addressing eviction correctness**: The `TableRemove` method at lines 521-548 correctly implements Robin Hood deletion with rehashing of subsequent displaced entries. The while loop at line 534 (`while (_tableKeys[next] != EMPTY_KEY)`) correctly terminates because the load factor is capped at < 0.5 (1024-entry table for 512 ring), guaranteeing empty sentinel slots exist.

**[O3] EMPTY_KEY sentinel collision with FNV-1a output.** The plan sets `EMPTY_KEY = 0L` and guards it at line 468: `if (hash == EMPTY_KEY) hash = 1L;`. FNV-1a of an empty string returns the offset basis `0xcbf29ce484222325L`, not 0, so this guard only fires for null/empty input. However, there is a theoretical (astronomically unlikely) possibility that a valid executionId hashes to exactly 0. The guard correctly handles this by mapping 0 to 1. This is acceptable.

**Eviction index calculation**: Line 492: `int evictIndex = (_ringHead - _ringCount + _ringCapacity) % _ringCapacity;`. When the ring is full (`_ringCount == _ringCapacity`), this evaluates to `_ringHead % _ringCapacity`, which is the oldest entry (since `_ringHead` is the next write position, the slot AT `_ringHead` is the oldest). Wait -- that is the slot ABOUT TO BE overwritten. This is correct: it evicts the entry at the current `_ringHead` position BEFORE writing the new one. Verified correct.

---

### Component 4: Dispatch Pipeline Integration

**[B2] CRITICAL: FleetDispatchSlot has NO Order[] field. Consumer path is unspecified.**

This is the design gap identified in the audit prompt, and it is confirmed as a BLOCKING deficiency.

**The problem in detail:**

1. In the producer (SIMA.Dispatch.cs, plan lines 672-714), `_photonPool.Claim()` returns an `Order[]`. The orders are populated: `_proxyOrders[_orderIdx++] = entry;` etc. A `FleetDispatchSlot` struct is created with metadata (Account, Quantity, prices, etc.) but NO reference to the `_proxyOrders` array. The slot is enqueued into the SPSC ring. The `_proxyOrders` array... goes nowhere.

2. In the consumer (SIMA.Fleet.cs, plan lines 724-756), `_photonDispatchRing.TryDequeue(out _ringSlot, ...)` produces a `FleetDispatchSlot` that contains Account, FleetEntryName, metadata -- but no Order[]. The plan says at line 747: `// ... (process _ringSlot same as existing FleetDispatchRequest logic) ...` -- this is a placeholder comment, not implementation.

3. The existing `PumpFleetDispatch` consumer (SIMA.Fleet.cs lines 88-162 in source) requires `req.Orders` for:
   - `foreach (var ord in req.Orders)` -- FSM initialization (line 101-131)
   - `req.Account.Submit(req.Orders)` -- broker submission (line 134)
   - `foreach (var ord in req.Orders)` -- OrderId registration (line 152-158)

Without an Order[] reference in the ring slot, the consumer cannot:

- Submit orders to the broker
- Initialize the FollowerBracketFSM
- Register OrderIds for FSM lookup

**Required fix**: Either:
(a) Add `public Order[] Orders;` to `FleetDispatchSlot` (breaking the "value-type only" aspiration but solving the linkage), OR
(b) Add `public int PoolSlotIndex;` to `FleetDispatchSlot` and retrieve the Order[] from `_photonPool` by index in the consumer. This preserves the struct-copy semantics while maintaining linkage.

Option (b) is preferred: it keeps the struct small, enables O(1) Release (fixing B1), and the Order[] reference is retrieved from the pool's `_orderArrays[slotIndex]` in the consumer. The consumer calls `_photonPool.Release(slotIndex)` after submit.

**[B3] CRITICAL: Pool slot leak on ConcurrentQueue fallback path.**

Plan lines 702-714:

```csharp
if (!_photonDispatchRing.TryEnqueue(ref _slot, _slotCrc))
{
    // Ring full -- fallback to ConcurrentQueue
    _pendingFleetDispatches.Enqueue(new FleetDispatchRequest {
        Account = acct,
        Orders = _proxyOrders,  // <-- Pool-claimed Order[]
        ...
    });
}
```

When the ring is full, the Order[] claimed from `_photonPool.Claim()` at line 672 is passed into the `FleetDispatchRequest` via the legacy ConcurrentQueue. The existing `PumpFleetDispatch` consumer processes the ConcurrentQueue and calls `req.Account.Submit(req.Orders)`. After submit completes, there is NO call to `_photonPool.Release(req.Orders)`.

The pool-claimed array is consumed by the legacy path but never returned to the pool. Over time, if fallback paths are triggered (even once), pool slots leak permanently. Since `PhotonPoolCapacity = 32`, 32 fallback dispatches exhaust the pool entirely, forcing all subsequent dispatches to heap-allocate.

**Required fix**: After the existing ConcurrentQueue consumer completes submit, check if the Order[] belongs to the pool and release it:

```csharp
// After successful submit in legacy ConcurrentQueue path:
if (req.Orders != null && req.Orders.Length == MaxOrdersPerSlot)
    _photonPool.Release(req.Orders);
```

This must also be added to every error/abort path in PumpFleetDispatch that discards a ConcurrentQueue request (lines 51-57, 164-183 in source).

---

### Cross-Component Risks

**[O4] Fallback Dedup Key Mismatch -- Confirmed Regression Risk.**

The plan's NEW fallback dedup code (lines 631-640):

```csharp
string _fallbackKey = (order.OrderId ?? orderName) + "|" + cumulativeFilledQuantity;
long _fallbackHash = FnvHash64(_fallbackKey);
```

The EXISTING code (V12_002.Orders.Callbacks.Execution.cs lines 218-221):

```csharp
string uniqueOrderId = !string.IsNullOrEmpty(execution.Order.OrderId)
    ? execution.Order.OrderId : execution.Order.Name;
string dedupOrderIdentity = GetStableHash(uniqueOrderId);
int dedupFilledQuantity = execution.Order.Filled > 0
    ? execution.Order.Filled : Math.Max(0, quantity);
string fallbackKey = string.Format("{0}|{1}", dedupOrderIdentity, dedupFilledQuantity);
```

Three differences:

1. **Hash function change**: Existing uses `GetStableHash()` (FNV-1a 32-bit, output as 8-char hex string) on the orderId component, THEN concatenates with quantity. The new code hashes the entire composite string with FNV-1a 64-bit. While the final dedup mechanism changes from HashSet membership to ExecutionIdRing membership, the key CONSTRUCTION differs -- the existing code hashes the orderId FIRST, then appends quantity to the hash string. The new code concatenates raw orderId + quantity, then hashes. These produce different collision profiles.

2. **Variable name `cumulativeFilledQuantity` does not exist** in `ProcessOnExecutionUpdate`. The method signature has `int orderFilled` (from `execution.Order.Filled`) and `int quantity` (from the callback parameter). The existing code uses `execution.Order.Filled > 0 ? execution.Order.Filled : Math.Max(0, quantity)`. The plan's `cumulativeFilledQuantity` is an undefined variable -- the plan code will not compile.

3. **`order` variable**: The plan references `order.OrderId` but the `ProcessOnExecutionUpdate` method does not have a local variable named `order`. The method receives `string orderId` and `Execution execution`. The existing code accesses `execution.Order.OrderId`. The plan's reference to `order` is undefined.

**Impact**: The fallback dedup section as written in the plan will NOT COMPILE. Even after fixing variable names, the key construction differs from the existing implementation, which could cause:

- False negatives (failing to detect true duplicates) if the new hash has different collision characteristics
- False positives (blocking legitimate executions) -- unlikely but possible

**Required fix**: The new fallback code must use the exact same key construction as the existing code, just routed through FnvHash64 + ExecutionIdRing instead of GetStableHash + HashSet:

```csharp
// Corrected fallback dedup
string uniqueOrderId = !string.IsNullOrEmpty(orderId) ? orderId : orderName;
int dedupFilledQty = orderFilled > 0 ? orderFilled : Math.Max(0, quantity);
string _fallbackKey = string.Format("{0}|{1}", uniqueOrderId, dedupFilledQty);
long _fallbackHash = FnvHash64(_fallbackKey);
if (_executionIdFallbackRing.ContainsOrAdd(_fallbackHash))
{
    Print(string.Format("[DEDUP] Skipping duplicate execution (fallback) orderId={0}", orderId));
    return;
}
```

Note: The intermediate `GetStableHash()` step is intentionally dropped -- it was producing a hex string representation of a 32-bit FNV hash, which was then stored as a string in the HashSet. With ExecutionIdRing, we hash the raw composite key directly with 64-bit FNV, which is strictly superior (larger hash space, no intermediate string allocation).

---

### Shutdown Drain Gap

The existing `ProcessShutdownSIMA()` (SIMA.Lifecycle.cs lines 89-107) drains only `_pendingFleetDispatches` (the ConcurrentQueue). After Photon integration, `_photonDispatchRing` also needs draining on shutdown. Any slots remaining in the ring at shutdown will have:

- ReservedDelta not rolled back (expectedPositions leak)
- DispatchSyncPending not cleared (sync barrier leak)
- Pool-claimed Order[] not released (pool slot leak)

**Required fix**: Add ring drain before or after the ConcurrentQueue drain:

```csharp
// Drain Photon ring on shutdown
FleetDispatchSlot ringSlot;
ushort storedCrc;
bool crcValid;
while (_photonDispatchRing != null
    && _photonDispatchRing.TryDequeue(out ringSlot, out storedCrc, out crcValid))
{
    if (ringSlot.ReservedDelta != 0)
        AddExpectedPositionDelta(ringSlot.ExpectedKey, -ringSlot.ReservedDelta);
    ClearDispatchSyncPending(ringSlot.ExpectedKey);
    // Release pool slot if PoolSlotIndex is added per B1/B2 fix
}
```

---

### Recommendations

1. **[B1] Add PoolSlotIndex to FleetDispatchSlot.** Eliminates O(N) Release scan and provides the linkage needed for B2.

2. **[B2] Carry Order[] through the ring via PoolSlotIndex.** Consumer retrieves `_photonPool.GetByIndex(slot.PoolSlotIndex)` and passes to `acct.Submit()`. Add a `GetByIndex(int)` method to PhotonOrderPool.

3. **[B3] Add pool Release calls to ALL consumer paths.** Both the Photon ring consumer and the legacy ConcurrentQueue consumer must release pool-claimed arrays after processing (or on error/abort).

4. **[O4] Fix fallback dedup key construction.** Use correct variable names from ProcessOnExecutionUpdate signature. Preserve existing key semantics.

5. **Add shutdown drain for _photonDispatchRing** in ProcessShutdownSIMA and the PumpFleetDispatch abort path (isFlattenRunning guard).

6. **[O1] Document CRC16 as defense-in-depth**, not torn-read detection. Current architecture has no torn-read risk.

7. **[O2] Remove or document cache-line padding as aspirational.** CLR does not honor field order in sealed classes.

---

### Summary Table

| ID  | Severity | Component | Issue |
|-----|----------|-----------|-------|
| B1  | BLOCKING | PhotonOrderPool | Release() O(N) scan -- add PoolSlotIndex for O(1) |
| B2  | BLOCKING | Dispatch Pipeline | FleetDispatchSlot missing Order[] linkage -- consumer cannot submit |
| B3  | BLOCKING | Dispatch Pipeline | Pool slot leak on ConcurrentQueue fallback path |
| O1  | OBSERVATION | SPSC Ring | CRC16 solves non-existent torn-read risk (same-thread producer/consumer) |
| O2  | OBSERVATION | SPSC Ring | Cache-line padding ineffective on sealed class (CLR field layout) |
| O3  | OBSERVATION | ExecutionIdRing | EMPTY_KEY sentinel guard correct but worth documenting |
| O4  | OBSERVATION | Dedup | Fallback key uses undefined variables -- will not compile |

**Note on O4**: While classified as observation severity, the undefined variable names (`cumulativeFilledQuantity`, `order`) make this section non-compilable. It is functionally blocking but categorized as observation because the fix is straightforward variable name correction, not an architectural redesign.

---

### Verdict Rationale

Three blocking issues (B1, B2, B3) require architectural revision before this plan can be handed to an engineer for implementation. B2 in particular is a fundamental design gap -- the plan's primary data structure (FleetDispatchSlot) cannot carry the payload that the consumer requires. The recommended PoolSlotIndex solution resolves B1 and B2 simultaneously with minimal structural change.

The plan is architecturally sound in concept. The SPSC ring, PhotonOrderPool, and ExecutionIdRing are well-designed components. The issues are in the INTEGRATION seams -- how data flows between components and how lifecycle events (shutdown, fallback, error) handle pool resources.

**REVISION REQUIRED. Resubmit after addressing B1, B2, B3, and O4.**
