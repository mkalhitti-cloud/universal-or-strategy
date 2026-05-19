# GEMINI CLI P4 AUDIT: V14.2 Sovereign Photon

## Audit Date: 2026-04-04

## Auditor: Claude Opus 4.6 (acting as Gemini P4 Engineer)

## Plan File: docs/brain/implementation_plan.md

## Verdict: REVISION REQUIRED

---

### Component 1: SPSC Ring Buffer + CRC16

**Status: PASS WITH OBSERVATIONS**

#### Correctness

1. **Power-of-2 enforcement**: Constructor validates `(capacityPowerOf2 & (capacityPowerOf2 - 1)) != 0`. Correct. Mask-based indexing is sound.

2. **TryEnqueue barrier ordering**: The producer writes `_buffer[index]` and `_checksums[index]` BEFORE `Volatile.Write(ref _producerCursor, cursor + 1)`. This is correct -- the Volatile.Write acts as a release fence, ensuring the data is visible before the cursor advances.

3. **TryDequeue barrier ordering**: Consumer reads `_buffer[index]` and `_checksums[index]` AFTER `Volatile.Read(ref _consumerCursor)` which acts as an acquire fence. The `Volatile.Write(ref _consumerCursor, cursor + 1)` at the end is a release fence for the producer's `Volatile.Read(ref _consumerCursor)` full-capacity check. Correct.

4. **CRC16-CCITT polynomial 0x1021**: Standard, well-known. Implementation is bit-at-a-time (no lookup table). Acceptable for the low call frequency (max 12 per signal). Zero-allocation byte extraction via shifts. Clean.

5. **Cache-line padding**: 7 longs (56 bytes) between `_producerCursor` and `_consumerCursor`. With `_producerCursor` itself (8 bytes), total is 64 bytes. However, the CLR does not guarantee field layout in classes or structs without `[StructLayout]`. The JIT may reorder fields. **OBSERVATION**: For .NET 4.8, `sealed class` field layout is typically sequential for reference types, but this is an implementation detail, not a contract. Functional impact: false sharing could reduce performance on multi-core, but since both producer and consumer are the strategy thread (different TriggerCustomEvent cycles on the SAME thread), false sharing is impossible. The padding is defensive but inert.

6. **Cursor overflow**: `long` cursors at 1000 enqueues/sec = 292 million years. Acceptable.

#### Observations

- **CRC is defensive-only, not safety-critical**: Both producer and consumer run on the strategy thread. There are no torn reads possible in single-threaded struct copies. CRC detects logical corruption (e.g., a programming bug writing to the wrong slot), not concurrency corruption. The plan correctly notes this in the architecture overview ("Both are strategy-thread but on DIFFERENT TriggerCustomEvent cycles"). This is adequate.

- **TryDequeue sets `crcValid = true` unconditionally**: The comment says "Caller must verify CRC externally", which is what PumpFleetDispatch does. However, the output parameter name `crcValid` is misleading -- it does not actually validate the CRC. It always returns true. The parameter is dead weight. **Minor**: Consider renaming to `reserved` or removing it entirely.

---

### Component 2: PhotonOrderPool

**Status: REVISION REQUIRED -- 3 issues**

#### Issue 2.1: CRITICAL -- Order[] Not Carried Through FleetDispatchSlot [SEVERITY: P0]

**The FleetDispatchSlot struct has no `Order[]` field.** The plan states the producer calls `_photonPool.Claim()` to get a pre-allocated `Order[]`, fills it with entry/stop/target orders, then publishes a `FleetDispatchSlot` to the SPSC ring. But `FleetDispatchSlot` contains only value-type dispatch metadata -- there is no reference to the claimed `Order[]`.

On the consumer side, `PumpFleetDispatch` dequeues a `FleetDispatchSlot` and needs to call `acct.Submit(Order[])`. The comment says:

```text
// Build Order[] from pool slot (already claimed in dispatch)
// ... (process _ringSlot same as existing FleetDispatchRequest logic) ...
```

This is underspecified. There is no mechanism to retrieve the `Order[]` from the slot. The existing `FleetDispatchRequest` struct HAS an `Order[] Orders` field. The new `FleetDispatchSlot` does NOT.

**The plan must either:**

- (a) Add an `Order[]` field (or `int PoolSlotIndex`) to `FleetDispatchSlot`, or
- (b) Add an `Order[][] _photonSlotOrders` parallel array indexed by ring buffer position, or
- (c) Use a separate `ConcurrentDictionary<string, Order[]>` keyed by FleetEntryName.

Option (a) is simplest and preserves zero-alloc. Adding a managed reference to the struct does not cause allocation -- it's just a pointer copy.

**Without this fix, the entire dispatch pipeline is broken. No orders can be submitted.**

#### Issue 2.2: MEDIUM -- Pool Exhaustion Under 3 Concurrent Signals [SEVERITY: P1]

The pool has 32 slots. A single signal dispatching to 12 accounts claims 12 slots. If a second signal fires before PumpFleetDispatch has consumed and released the first batch, 24 slots are consumed. A third concurrent signal exhausts the pool at 36 > 32.

The plan has a fallback (`if (_proxyOrders == null) ... new Order[MaxOrdersPerSlot]`), but fallback-allocated arrays will fail `Release()` because `ReferenceEquals` won't find them in `_orderArrays[]`. This is handled (`if (slotIndex < 0) return`), so no crash, but:

1. Fallback arrays become GC pressure (defeating the purpose)
2. The diagnostic `free` count will desync (claims without matching releases)
3. `_exhaustedCount` will increment but `_releaseCount` won't for those slots

**Recommendation**: Increase pool capacity to 48 (next power-of-2 would be 64, but 48 with array-based stack works). Or, since the SPSC ring itself is 32, and the ring would also be full at 36, the fallback to ConcurrentQueue covers this. The real concern is that SPSC ring capacity of 32 means 3 concurrent signals ALSO overflow the ring. This is handled by the fallback to `_pendingFleetDispatches.Enqueue()`. **Document this explicitly as acceptable degradation, not a bug.**

#### Issue 2.3: MEDIUM -- PhotonOrderPool.Release() Linear Scan [SEVERITY: P2]

`Release()` does a linear scan through `_orderArrays[0..capacity]` using `ReferenceEquals` to find the slot index. With capacity 32, this is 32 reference comparisons per release. At 12 accounts, that's 384 comparisons per signal cycle. This is cheap but inelegant.

**Alternative**: Store the slot index in a parallel field in `FleetDispatchSlot` (e.g., `int PoolSlotIndex`). On Claim(), return the index alongside the array. On Release(), use the index directly. O(1) instead of O(n).

This is a performance nit, not a correctness issue. Accept or optimize at discretion.

#### Thread Safety of Claim/Release

`_freeTop` is `volatile int` with `Interlocked.Decrement/Increment`. However, there is a TOCTOU race:

```csharp
int top = Interlocked.Decrement(ref _freeTop);
// Between here...
int slotIndex = _freeStack[top];
// ...another thread could Increment and write a different index to _freeStack[top]
```

**However**: Both Claim() and Release() are called from the strategy thread only (Claim in ExecuteSmartDispatchEntry, Release in PumpFleetDispatch, both strategy thread). So this is single-threaded access despite the Interlocked usage. The Interlocked is defensive. No actual race exists. **PASS.**

---

### Component 3: ExecutionIdRing (ADR-011)

**Status: REVISION REQUIRED -- 2 issues**

#### Issue 3.1: HIGH -- Fallback Dedup Key Mismatch [SEVERITY: P1]

The existing fallback dedup code (V12_002.Orders.Callbacks.Execution.cs lines 216-221):

```csharp
string uniqueOrderId = !string.IsNullOrEmpty(execution.Order.OrderId)
    ? execution.Order.OrderId : execution.Order.Name;
string dedupOrderIdentity = GetStableHash(uniqueOrderId);
int dedupFilledQuantity = execution.Order.Filled > 0
    ? execution.Order.Filled : Math.Max(0, quantity);
string fallbackKey = string.Format("{0}|{1}", dedupOrderIdentity, dedupFilledQuantity);
```

The plan's replacement (line 633):

```csharp
string _fallbackKey = (order.OrderId ?? orderName) + "|" + cumulativeFilledQuantity;
```

**Three mismatches:**

1. **GetStableHash() removed**: The existing code hashes the OrderId via `GetStableHash()` (FNV-1a 32-bit, returns 8-char hex string). The plan uses the raw OrderId string. Since the result is then hashed again by `FnvHash64()`, this is functionally OK (double-hash vs single-hash), but the key SPACE is different. During a rollback scenario where old and new code coexist (e.g., partial deployment), the same execution could produce different keys and bypass dedup. Since this is a full replacement (not incremental), this is acceptable but should be noted.

2. **`cumulativeFilledQuantity` is undefined**: The existing code uses `execution.Order.Filled` (an int property on the Order object). The plan uses `cumulativeFilledQuantity` which is NOT a parameter of `ProcessOnExecutionUpdate()`. The method signature is: `ProcessOnExecutionUpdate(string orderName, string executionId, string orderId, int orderFilled, OrderState orderState, double price, int quantity, Execution execution)`. The plan must use `orderFilled` or `execution.Order.Filled`, not an undefined variable.

3. **`order` is undefined**: The plan references `order.OrderId` but the method receives `Execution execution`, not an `Order order`. The correct reference is `execution.Order.OrderId`. The plan must use the correct variable names from the method signature.

**This will not compile as written. The engineer implementing this MUST map to the correct variable names: `execution.Order.OrderId`, `execution.Order.Filled` (or `orderFilled` parameter).**

#### Issue 3.2: MEDIUM -- Robin Hood Deletion Correctness

The `TableRemove` method implements deletion with rehashing of subsequent entries:

```csharp
int next = (bucket + 1) & _tableMask;
while (_tableKeys[next] != EMPTY_KEY)
{
    long rehashKey = _tableKeys[next];
    int rehashVal = _tableValues[next];
    _tableKeys[next] = EMPTY_KEY;
    _tableValues[next] = -1;
    _tableCount--;
    TableInsert(rehashKey, rehashVal);
    next = (next + 1) & _tableMask;
}
```

This is standard backward-shift deletion for open-addressing hash tables. **Correctness analysis:**

- It removes the target slot, then rehashes all consecutive non-empty slots until hitting an empty slot. This is correct for linear probing.
- `_tableCount` is decremented for both the deleted entry AND each rehashed entry, but `TableInsert` increments `_tableCount` for each re-inserted entry. Net effect: count decreases by exactly 1. Correct.
- Edge case: If the hash table is completely full (load factor = 1.0), the while loop would scan every slot. But load factor is bounded at < 0.5 (ring capacity 512, table capacity 1024), so maximum probe length is statistically bounded. Acceptable.

**One subtle issue**: If two entries hash to the same value (FNV-1a collision), `TableRemove` will remove the FIRST one it finds at that hash, not necessarily the one associated with the evicted ring entry. Since both entries have the same hash key, removing either is functionally equivalent for dedup purposes (the dedup only cares about the hash, not the ring index). **PASS with note.**

#### Issue 3.3: LOW -- EMPTY_KEY Sentinel Collision

`EMPTY_KEY = 0L`. `FnvHash64("")` returns 0. The code remaps: `if (hash == EMPTY_KEY) hash = 1L`. Can a real non-empty string hash to exactly 1L? Yes, but the probability is 1/2^64 which is negligible. **Acceptable.**

However, two distinct strings that hash to 0L and 1L respectively would collide after remapping. The first (empty string) maps to 1L; the second (naturally hashing to 1L) also maps to 1L. This means `FnvHash64("")` and any string naturally hashing to 1L would be treated as duplicates. Since execution IDs are never empty strings (the code checks `!string.IsNullOrEmpty(executionId)` before calling the ring), the empty-string case never arises. **Non-issue.**

#### FNV-1a Implementation

The FNV-1a offset basis `0xcbf29ce484222325` and prime `0x100000001b3` are correct for FNV-1a 64-bit. The implementation iterates over `char` (UTF-16, 2 bytes each), not bytes. This means the hash is NOT standard FNV-1a over the UTF-8 encoding. For dedup purposes within a single process session, this is irrelevant -- consistency is all that matters. **PASS.**

---

### Component 4: Dispatch Pipeline Integration

**Status: REVISION REQUIRED -- 2 critical issues**

#### Issue 4.1: CRITICAL -- Consumer Path Entirely Underspecified [SEVERITY: P0]

The plan's PumpFleetDispatch ring consumption section contains:

```csharp
// Valid slot -- submit to broker
// Build Order[] from pool slot (already claimed in dispatch)
// ... (process _ringSlot same as existing FleetDispatchRequest logic) ...
// After successful submit, release Order[] back to pool
// _photonPool.Release(orderArray);
```

This is a placeholder comment, not implementation code. The existing PumpFleetDispatch (V12_002.SIMA.Fleet.cs lines 45-191) performs approximately 100 lines of critical logic:

1. **isFlattenRunning / EnableSIMA abort guard** with full queue drain and delta rollback (lines 48-59)
2. **MetadataGuard stale rejection** with dict cleanup (lines 68-84)
3. **FollowerBracketFSM initialization** from Order[] -- iterating orders to classify entry/stop/targets (lines 89-131)
4. **acct.Submit(req.Orders)** (line 134)
5. **ClearDispatchSyncPending** (line 135)
6. **FSM PendingSubmit -> Submitted promotion** (lines 139-146)
7. **OrderId -> FSM key registration** for O(1) lookup (lines 149-159)
8. **Error catch**: delta rollback + dict cleanup + FSM cleanup (lines 164-184)
9. **Finally**: decrement pending count + chain next pump (lines 186-191)

The plan must specify how ALL of these steps are performed for the ring-buffer path. The current "process same as existing logic" is insufficient for an engineer to implement without architectural decisions. Specifically:

- **How does the ring slot map to the FSM population?** The existing code iterates `req.Orders` to find entry/stop/target orders. If `FleetDispatchSlot` has no `Order[]` (see Issue 2.1), this cannot work.
- **Where does the error rollback occur?** The try/catch must wrap the ring path identically.
- **Where is the Order[] released?** Must be in the `finally` block, not just after success.

**The plan must provide the complete ring-buffer consumption path, including error handling, FSM init, and pool release.**

#### Issue 4.2: HIGH -- Shutdown Drain Missing for Ring Buffer [SEVERITY: P1]

`DrainQueuesForShutdown()` (V12_002.Lifecycle.cs lines 431-454) drains `ipcCommandQueue` and `_cmdQueue`. `ProcessShutdownSIMA()` (V12_002.SIMA.Lifecycle.cs lines 89-107) drains `_pendingFleetDispatches` with delta rollback.

Neither location drains `_photonDispatchRing`. If the strategy terminates while the ring contains pending dispatch slots:

1. Reserved deltas are never rolled back -- `expectedPositions` retains phantom quantities.
2. Claimed Order[] pool slots are never released -- pool leaks (not critical since strategy is terminating, but diagnostic counts will be wrong).
3. Dict entries (activePositions, entryOrders, stopOrders) registered in dispatch remain populated without matching orders.

**The plan must add ring drain logic to both `ProcessShutdownSIMA()` and `State.Terminated` (or `DrainQueuesForShutdown`).** Pattern:

```csharp
FleetDispatchSlot ringSlot;
ushort _crc;
bool _cv;
while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out ringSlot, out _crc, out _cv))
{
    if (ringSlot.ReservedDelta != 0)
        AddExpectedPositionDelta(ringSlot.ExpectedKey, -ringSlot.ReservedDelta);
    ClearDispatchSyncPending(ringSlot.ExpectedKey);
    // Release pool slot if applicable
}
```

---

### Protocol Compliance

#### ASCII-Only String Literals: PASS

All string literals in the plan use straight double quotes, ASCII dashes (`--`), ASCII arrows (`->`), and standard alphanumeric/punctuation characters. No emoji, curly quotes, em-dashes, or Unicode arrows detected.

Specific verification:

- `"[PHOTON] Pool exhausted -- fallback to heap alloc"` -- ASCII clean
- `"[PHOTON] Ring full -- fallback to ConcurrentQueue"` -- ASCII clean
- `"[PHOTON_CRC] INTEGRITY FAILURE on slot: expected=0x{0:X4} got=0x{1:X4} entry={2} -- SKIPPING"` -- ASCII clean
- `"[PHOTON_HEALTH] "` -- ASCII clean
- `"[DEDUP] Skipping duplicate execution {0}"` -- ASCII clean
- All format strings use `string.Format()` with numbered placeholders. **PASS.**

#### No lock() or Monitor.Enter: PASS

Grep of the full plan text confirms zero instances of `lock(`, `Monitor.Enter`, or `Monitor.TryEnter`. All thread coordination uses `Interlocked` and `Volatile`. **PASS.**

#### Ghost-Order Prevention (Signed Delta Rollback): PASS WITH OBSERVATION

The dispatch path preserves signed delta rollback:

- `reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;` (unchanged)
- `AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);` (unchanged)
- Error path: `AddExpectedPositionDeltaLocked(req.ExpectedKey, -req.ReservedDelta);` (unchanged)

The `FleetDispatchSlot.ReservedDelta` field carries the delta through the ring. **However**, the consumer path is underspecified (Issue 4.1), so we cannot verify that the ring consumer performs the rollback correctly. The CRC-failure path does NOT rollback the delta -- it decrements `_pendingFleetDispatchCount` but does not call `AddExpectedPositionDeltaLocked(ringSlot.ExpectedKey, -ringSlot.ReservedDelta)`.

**CRC failure path MUST include delta rollback and dict cleanup.** Currently it just logs and skips. This leaks expected position deltas.

#### MOVE-SYNC Replace FSM: NOT IMPACTED

The plan does not modify `_followerReplaceSpecs` or the two-phase Replace FSM. The FSM is populated from PumpFleetDispatch, which is modified but the ring consumer is underspecified. **Conditional PASS** -- depends on correct consumer implementation.

#### Build 981 Protocol (Direct stopOrders Writes): PASS

The plan does not modify the dict registration block in ExecuteSmartDispatchEntry (lines 330-338):

```csharp
stopOrders[fleetEntryName] = stop;  // Direct write, no enqueue
```

This is preserved. **PASS.**

---

### Regression Safety Matrix

| Component | Modified? | Assessment |
|-----------|-----------|------------|
| 3-phase Flatten-Scope FSM | NO | Plan does not touch flatten pipeline. `PumpFlattenOps` is separate from `PumpFleetDispatch`. **SAFE.** |
| REAPER audit cycle | NO | Only addition is diagnostic log line. No logic modification. **SAFE.** |
| FollowerBracketFSM state machine | NO | States and transitions unchanged. FSM population code in PumpFleetDispatch needs to be replicated in ring consumer (Issue 4.1). **CONDITIONAL.** |
| isFlattenRunning guard | NO | Guard at top of PumpFleetDispatch is unchanged. Ring consumption is inserted AFTER this guard (abort and drain runs first). **SAFE** -- but need to verify the ring is also drained in the abort path (currently only `_pendingFleetDispatches` is drained). |
| expectedPositions management | NO | `AddExpectedPositionDeltaLocked` is unchanged. Delta reservation in dispatch is unchanged. **SAFE** -- but ring consumer rollback is underspecified. |
| Watchdog timer | NO | Not referenced or modified. **SAFE.** |
| MarkDispatchSyncPending / ClearDispatchSyncPending | NO | Called in dispatch (unchanged). Must be called in ring consumer (underspecified). **CONDITIONAL.** |

**CRITICAL GAP in isFlattenRunning drain**: The abort path at the top of PumpFleetDispatch (lines 48-59) drains only `_pendingFleetDispatches`:

```csharp
while (_pendingFleetDispatches.TryDequeue(out stale))
```

The plan inserts ring consumption BEFORE the ConcurrentQueue drain but AFTER the abort check. However, the abort check itself does NOT drain the ring. If flatten starts while items are in the ring:

1. Ring items are never drained
2. PumpFleetDispatch returns after draining the queue
3. Ring items persist until next PumpFleetDispatch call (which may not come if flatten completed and no new signals fire)

**The abort/drain logic MUST also drain `_photonDispatchRing` with delta rollback.**

---

### Recommendations

#### P0 -- Must Fix Before Implementation

1. **Add Order[] to FleetDispatchSlot** (or add a PoolSlotIndex that maps to a parallel Order[][] array). Without this, the dispatch pipeline cannot function.

2. **Specify complete ring consumer path in PumpFleetDispatch**. The "process same as existing logic" placeholder must be replaced with actual code covering: FSM init, acct.Submit, ClearDispatchSyncPending, OrderId registration, error catch with delta rollback + dict cleanup, finally with pool release + pending count decrement + chain pump.

3. **Add delta rollback to CRC failure path**. The current CRC failure handler skips the slot but does NOT rollback the reserved delta or clean up dicts registered during dispatch. This leaks phantom expected positions.

#### P1 -- Must Fix Before Merge

1. **Add ring buffer drain to shutdown paths**: ProcessShutdownSIMA() and DrainQueuesForShutdown() must drain `_photonDispatchRing` with delta rollback, just as they drain `_pendingFleetDispatches`.

2. **Add ring buffer drain to isFlattenRunning abort path** in PumpFleetDispatch (lines 48-59). Currently only drains the ConcurrentQueue.

3. **Fix undefined variable names in fallback dedup**: `cumulativeFilledQuantity` and `order` are not in scope in `ProcessOnExecutionUpdate`. Must use `execution.Order.Filled` (or `orderFilled` parameter) and `execution.Order`.

#### P2 -- Should Fix

1. **Document pool exhaustion as accepted degradation**: 3 concurrent signals with 12 accounts = 36 slots > 32 capacity. Fallback to heap allocation + ConcurrentQueue is the correct safety net, but this should be explicitly documented as a design decision, not a latent surprise.

2. **Consider increasing SPSC ring + pool to 64**: Power-of-2, supports 5 concurrent signals x 12 accounts (60 < 64). Minimal memory overhead (64 x ~120 bytes = 7.5KB).

3. **Remove or rename `crcValid` output parameter** from TryDequeue. It is always set to `true` and never used meaningfully.

4. **PhotonOrderPool.Release() O(n) scan**: Add a `PoolSlotIndex` to FleetDispatchSlot for O(1) release. Minor performance improvement.

---

### Summary of Findings

| # | Severity | Component | Issue |
|---|----------|-----------|-------|
| 2.1 | P0 | PhotonOrderPool | FleetDispatchSlot has no Order[] field -- dispatch pipeline broken |
| 4.1 | P0 | Dispatch Integration | Consumer path is placeholder comments, not implementation |
| 4.CRC | P0 | Dispatch Integration | CRC failure path missing delta rollback + dict cleanup |
| 3.1 | P1 | ExecutionIdRing | Fallback dedup uses undefined variables (won't compile) |
| 4.2 | P1 | Dispatch Integration | No shutdown drain for ring buffer |
| 4.Abort | P1 | Dispatch Integration | isFlattenRunning abort path doesn't drain ring |
| 2.2 | P2 | PhotonOrderPool | Pool exhaustion at 3 concurrent signals underdocumented |
| 2.3 | P2 | PhotonOrderPool | Release() O(n) scan, could be O(1) |
| 1.CRC | Info | SPSC Ring | CRC param naming misleading but functionally harmless |
| 3.3 | Info | ExecutionIdRing | EMPTY_KEY sentinel collision negligible |

**Total: 3 P0 blockers, 3 P1 issues, 2 P2 suggestions, 2 informational notes.**

---

### Verdict: REVISION REQUIRED

The plan introduces sound zero-allocation infrastructure (SPSC ring, PhotonPool, ExecutionIdRing) with correct lock-free semantics and CRC integrity verification. However, the integration layer has three blocking gaps: (1) the Order[] bridge between pool and ring slot is missing, (2) the consumer path is entirely placeholder text, and (3) the CRC failure path leaks deltas. Additionally, shutdown and flatten-abort drain paths must include the new ring buffer. The fallback dedup code contains undefined variables that will fail compilation.

The foundational data structures (Components 1-3) are architecturally sound. The integration (Component 4) needs a complete specification pass before an engineer can implement safely.
