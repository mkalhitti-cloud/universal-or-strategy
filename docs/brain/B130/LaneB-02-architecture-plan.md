# B130 LaneB Architecture Plan
# DW-B136 Gap B: Order-ID Scoped Cancel for Simultaneous Entries

**Block**: B130 LaneB
**Defect**: DW-B136 Gap B — cross-cancel on simultaneous leader entries
**Severity**: P1 OPEN (per spec section-dw-b136, updated post-B129)
**Spec Section**: `specs/002-trade-copier-spec.html#section-dw-b136`
**Status**: REVIEW_PASS
**Date**: 2026-09-01
**Revision**: V2 — fixes V-01 execution-order defect (removed _followerCopyMap.TryRemove from EvictDedup)

---

## 1. Problem Statement

`TryCancelFollowerEntries` (`CopyEngine.cs` ~L1621) matches follower orders by
**instrument name only**. When the leader cancels order #2 (a Working limit entry),
`CancelOneAccount` sweeps **all** Working/Initialized entry orders for that instrument
on each follower — including the follower copy of leader order #1 (still Working).

```
Leader: order1 (Working) + order2 (Working) — two separate entries, same instrument
Leader cancels order2
  -> TryCancelFollowerEntries fires for order2
  -> CancelOneAccount(Sim102, MES) -> cancels ALL Working entry orders on Sim102
  -> Sim102 copy of order1 CANCELLED (collateral cancel)
  -> Sim102 now flat even though leader still has order1 Working
```

**Single-entry constraint (MUST remain documented, NOT removed)**:
The copier design intent is one active entry per instrument per leader account at a time.
The fix prevents the collateral damage when that constraint is violated; it does **not**
endorse simultaneous entries as a supported workflow. The UI tooltip and spec comment
documenting the single-entry constraint must be preserved.

---

## 2. Option Selection: Option B — `_followerCopyMap` ConcurrentDictionary

### Options Evaluated

| Option | Summary | Rejection Reason |
|--------|---------|-----------------|
| A | Embed leaderOrderId in `PTT-Copy` name (`"PTT-Copy-12345"`) | Blast radius: breaks `FindFollowerEntryOrder`, `ReplaceFollowerCopyOnAtmCancel`, all name-equality predicates (3-4 sites). High regression risk. |
| B | `_followerCopyMap: ConcurrentDictionary<string, ConcurrentBag<Order>>` keyed by leader orderId | Minimal blast radius, lowest CYC impact, zero name-change risk. **SELECTED.** |
| C | Reuse `_dedupCache` or `_orderMap` | `_dedupCache` stores `double` price (wrong value type). `_orderMap` is for bracket binding (different semantic). Not a clean fit. |

### Why Option B

1. **Lowest CYC impact** — `TryCancelFollowerEntries` CYC drops from 6 → 4 (outer loop
   replaced by single helper call). Two new methods: `RecordFollowerCopy` (CYC=1) and
   `CancelScopedFollowerEntries` (CYC=5).
2. **Zero blast radius on existing name predicates** — `PTT-Copy` name unchanged;
   `FindFollowerEntryOrder`, `ReplaceFollowerCopyOnAtmCancel`, `IsWorkingBracket` all unaffected.
3. **Lock-free** — `ConcurrentDictionary` + `ConcurrentBag` satisfy JS-025 without lock().
4. **Live Order references** — NT8 Order objects update in-place via `OnOrderUpdate`.
   Storing the reference means `fo.OrderState` at cancel time reflects current state;
   no stale-state problem.
5. **Correct eviction order** — `CancelScopedFollowerEntries` consumes the bag THEN calls
   `TryRemove` after the loop. `EvictDedup` does NOT touch `_followerCopyMap` — this
   preserves the execution order required by `OnOrderUpdate` (EvictDedup fires at L1277,
   TryCancelFollowerEntries fires at L1361).

---

## 3. Data Structure Definition

### New Field in `CopyEngine.cs` (after `_entryDispatchedOrders` declaration)

```csharp
// DW-B136 Gap B: leader order ID -> follower Order objects dispatched for that leader order.
// Key = leader order.OrderId.ToString() (same format as _dedupCache and _entryDispatchedOrders).
// Value = ConcurrentBag<Order> of follower Order objects submitted for this leader order.
// Used by TryCancelFollowerEntries to scope cancel to the specific leader order being cancelled.
// JS-021: no lock. JS-025: ConcurrentDictionary + ConcurrentBag (lock-free).
// JS-001: only cancel calls are wrapped in try/catch in CancelScopedFollowerEntries.
// Eviction: TryRemove called in CancelScopedFollowerEntries (cancel path) after iterating the bag.
// NOTE: EvictDedup does NOT touch this map -- see execution-order note in Section 7.
internal readonly ConcurrentDictionary<string, ConcurrentBag<Order>> _followerCopyMap =
    new ConcurrentDictionary<string, ConcurrentBag<Order>>();
```

**Visibility**: `internal readonly` (not `private`) so `B130Tests.cs` can access the map
directly via `InternalsVisibleTo("PropTraderTools.Tests")` (already set at L46).

---

## 4. Changed Methods in `CopyEngine.cs`

### 4a. `SendCopy` — add `RecordFollowerCopy` call (CYC unchanged at 5)

**Location**: `SendCopy` (~L2783), after `follower.Submit(new[] { order })` at ~L2829.
**Change**: After confirming `order != null` and calling `Submit`, call
`RecordFollowerCopy(signal.OrderId, order)`.

```csharp
if (order != null)
{
    follower.Submit(new[] { order });
    RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B: track follower order by leader ID
}
```

**CYC check**: No new branch added. `signal.OrderId` is the leader's orderId (already carried
by `CopySignal.OrderId` — see `CopySignal.Create` call at L1904, `orderId` parameter at L497/511).
CYC = 5 (unchanged). ✅

### 4b. `SendCopyWithAtm` — add `RecordFollowerCopy` call (CYC unchanged at 4)

**Location**: `SendCopyWithAtm` (~L2847), after `order != null` guard at ~L2870.
**Change**: After the `StartAtmStrategy` call, call `RecordFollowerCopy(signal.OrderId, order)`.

```csharp
if (order == null)
    return false; // (2)
if (namedMode.AtmObject != null) // (3)
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
else // (4)
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B
```

**CYC check**: No new branch. CYC = 4 (unchanged). ✅

### 4c. `TryCancelFollowerEntries` — replace inner loop with scoped helper (CYC 6→4)

**Location**: `TryCancelFollowerEntries` (~L1621).
**Change**: Remove the `foreach (var acc in rule.FollowerAccounts)` loop and
`CancelOneAccount(acc, order.Instrument)` call. Replace with single call to
`CancelScopedFollowerEntries(order.OrderId.ToString())`.

Current (branches 4, 5, 6 = foreach + acc null + CancelOneAccount loop):
```csharp
foreach (var acc in rule.FollowerAccounts)       // (4)
{
    if (acc == null)                              // (5)
        continue;
    CancelOneAccount(acc, order.Instrument);      // (6 — CancelOneAccount is CYC=4 internally)
}
return true;
```

Replacement:
```csharp
// DW-B136 Gap B: scope cancel to specific leader order, not all instrument entries.
// Single-entry best practice: one leader entry per instrument at a time is the supported
// workflow. This fix prevents collateral cancel when the constraint is violated (two
// simultaneous entries). The constraint documentation in the spec and UI tooltip is preserved.
CancelScopedFollowerEntries(order.OrderId.ToString());
return true;
```

**Updated full method signature and CYC**:
```
TryCancelFollowerEntries: CYC=4
  (1) Cancelled state guard
  (2) IsAtmBracketName guard
  (3) PTT-QX-/PTT-BE- prefix guard (compound OR = 1 McCabe branch)
  (implicit) CancelScopedFollowerEntries call — no branch
  Base = 1
  CYC = 1 + 3 = 4
```
✅

Note: `rule` parameter is still required by the method signature (unchanged). It is no longer
used inside the method body after this change, but removing it would require updating all call
sites. The engineer should keep the parameter and add `// DW-B136 Gap B: rule param unused
post-fix; preserved for call-site stability` comment, OR verify there is exactly one call site
and update it. If only called from `OnOrderUpdate` (check L1361 context), dropping `rule` from
the signature is acceptable but changes the call site. **Architect decision: keep `rule`
parameter to minimize diff scope.**

### 4d. `EvictDedup` — NO CHANGE

**`EvictDedup` body is unchanged from the current source.** No `_followerCopyMap.TryRemove`
is added to `EvictDedup`.

**Reason**: `EvictDedup` fires at `OnOrderUpdate` L1277 — unconditionally, before the gate
chain. `TryCancelFollowerEntries` fires at L1361 (84 lines later, after multiple pre-gate
checks). If `EvictDedup` removed the `_followerCopyMap` entry for a Cancelled leader order,
`CancelScopedFollowerEntries` would see a `TryGetValue` miss and return immediately without
cancelling any follower copies — defeating the fix entirely.

The correct and only eviction point on the cancel path is inside `CancelScopedFollowerEntries`,
after the loop (see Section 5b).

For Filled and Rejected leader orders, the `_followerCopyMap` entry is harmless once the
leader order reaches a terminal state (the follower copies are already terminal or irrelevant).
These entries are naturally evicted when `CancelScopedFollowerEntries` fires for any future
cancel of the same orderId (which will simply miss and return), or they remain as dead entries
in the map. Memory impact is negligible (one ConcurrentBag per leader order, GC-eligible once
the order and follower Order references are released by NT8). No separate cleanup is required.

Current `EvictDedup` body (for reference — engineer must not modify this method):
```csharp
internal void EvictDedup(string orderId, OrderState state)
{
    if (
        state != OrderState.Filled
        && state != OrderState.Cancelled
        && state != OrderState.Rejected
    )
        return;

    _dedupCache.TryRemove(orderId, out _);
    if (state == OrderState.Cancelled)
        _entryDispatchedOrders.Clear(); // DW-B101: evict on Cancelled
    // _followerCopyMap NOT touched here -- eviction done in CancelScopedFollowerEntries after use.
    // See execution-order note in Section 7: EvictDedup fires at L1277 BEFORE
    // TryCancelFollowerEntries at L1361. Removing the map entry here would cause
    // CancelScopedFollowerEntries to see a TryGetValue miss and issue no cancels.
}
```

**CYC = 2 (unchanged, no modification required).** ✅

---

## 5. New Methods in `CopyEngine.cs`

### 5a. `RecordFollowerCopy` (new, CYC=1)

```csharp
// DW-B136 Gap B: record follower Order under the leader orderId that triggered the copy.
// Called from SendCopy and SendCopyWithAtm after follower.Submit succeeds.
// Key: leaderOrderId (same string as in _dedupCache and _entryDispatchedOrders).
// Value: ConcurrentBag<Order> -- thread-safe add, no lock().
// CYC=1: no branches. JS-021: lock-free (ConcurrentDictionary.GetOrAdd + ConcurrentBag.Add).
// JS-001: no throw. JS-002: void.
internal void RecordFollowerCopy(string leaderOrderId, Order followerOrder)
{
    var bag = _followerCopyMap.GetOrAdd(leaderOrderId, _ => new ConcurrentBag<Order>());
    bag.Add(followerOrder);
}
```

**Visibility**: `internal` (for test access via InternalsVisibleTo).

### 5b. `CancelScopedFollowerEntries` (new, CYC=5)

```csharp
// DW-B136 Gap B: cancel only follower orders recorded under the given leader order ID.
// Replaces the instrument-scoped sweep in TryCancelFollowerEntries.
// Called from TryCancelFollowerEntries AFTER EvictDedup has already fired in OnOrderUpdate
// (L1277 vs L1361). The map entry for leaderOrderId must still be present at this point --
// do NOT call _followerCopyMap.TryRemove anywhere before this method runs on a cancel path.
// CYC=5:
//   (1) TryGetValue miss guard
//   (2) foreach bag
//   (3) OrderState guard (Working || Initialized -- same as CancelOneAccount DW-B18-CANCEL-01)
//   (4) try body
//   (5) catch
// JS-021: no lock. JS-001: catch logs, no rethrow. JS-002: void.
// NT8: fo.Account.Cancel(Order[]) valid from AddOn context (NT8_ADDON_KNOWLEDGE.md line 222).
// Eviction: TryRemove called after loop -- belt-and-suspenders; safe if already missing.
internal void CancelScopedFollowerEntries(string leaderOrderId)
{
    if (!_followerCopyMap.TryGetValue(leaderOrderId, out var bag)) // (1)
        return;
    foreach (var fo in bag) // (2)
    {
        if (                                                        // (3)
            fo.OrderState != OrderState.Working
            && fo.OrderState != OrderState.Initialized
        )
            continue;
        try                                                         // (4)
        {
            fo.Account.Cancel(new Order[] { fo });
            StatusUpdate?.Invoke(fo.Account.Name + ": scoped cancel orderId=" + leaderOrderId);
        }
        catch (Exception ex)                                        // (5)
        {
            StatusUpdate?.Invoke("PTT-ScopedCancel error: " + ex.Message);
        }
    }
    _followerCopyMap.TryRemove(leaderOrderId, out _); // DW-B136 Gap B: evict after use
}
```

**Visibility**: `internal` (for test access via InternalsVisibleTo).

**CYC verification**:
- Base path: 1
- (1) TryGetValue miss = 1
- (2) foreach = 1
- (3) compound OrderState guard (OR of 2 states) = 1 McCabe branch (compound OR)
- (4) try = 1 (exception path branches from normal path)
Total: 1 + 4 = CYC=5 ✅

**Post-loop `TryRemove` adds no branch** — it is a single unconditional statement. CYC=5 confirmed.

---

## 6. CYC Analysis Per Method (Summary)

| Method | Before | After | Status |
|--------|--------|-------|--------|
| `TryCancelFollowerEntries` | 6 | 4 | ✅ Reduced |
| `SendCopy` | 5 | 5 | ✅ Unchanged |
| `SendCopyWithAtm` | 4 | 4 | ✅ Unchanged |
| `EvictDedup` | 2 | 2 | ✅ Unchanged (NOT modified — see Section 4d) |
| `RecordFollowerCopy` | N/A | 1 | ✅ New |
| `CancelScopedFollowerEntries` | N/A | 5 | ✅ New |
| `CancelOneAccount` | 4 | 4 | ✅ Unchanged (no longer called from TryCancelFollowerEntries but still called from other sites) |

All methods ≤ 8. ✅

---

## 7. Data Flow (Fixed) — Execution Order Verified

### Correct OnOrderUpdate Execution Order

```
OnOrderUpdate (Cancelled, id2):
  L1277: EvictDedup("id2", Cancelled)
           -> _dedupCache.TryRemove("id2")
           -> _entryDispatchedOrders.Clear()
           -> [NO _followerCopyMap access -- correct]
  ... (L1281-1360: TryFireFollowerBeDisarm, IsPttEntryOrderCancelTrigger, Gate 1, Gate 2)
  L1361: TryCancelFollowerEntries(order2, rule)
           -> Cancelled? YES
           -> IsAtmBracketName? NO
           -> PTT-QX-/PTT-BE- prefix? NO
           -> CancelScopedFollowerEntries("id2")
              -> _followerCopyMap.TryGetValue("id2") -> HIT (bag still present)
              -> foreach fo in bag:
                   fo.OrderState == Working -> fo.Account.Cancel([fo])  <- follower copy cancelled
              -> _followerCopyMap.TryRemove("id2")  <- evict after use
           -> return true
```

### Full Scenario: Two Simultaneous Leader Orders

```
Leader order1 dispatched (orderId="id1"):
  DispatchCopy -> SendCopy -> follower.CreateOrder -> order1Copy
                           -> follower.Submit([order1Copy])
                           -> RecordFollowerCopy("id1", order1Copy)
                              _followerCopyMap["id1"] = {order1Copy}

Leader order2 dispatched (orderId="id2"):
  DispatchCopy -> SendCopy -> follower.CreateOrder -> order2Copy
                           -> follower.Submit([order2Copy])
                           -> RecordFollowerCopy("id2", order2Copy)
                              _followerCopyMap["id2"] = {order2Copy}

Leader cancels order2 (NOT order1):
  OnOrderUpdate(Cancelled, id2)
    -> EvictDedup("id2", Cancelled)    [L1277 -- only touches _dedupCache]
    -> CancelScopedFollowerEntries("id2")  [via TryCancelFollowerEntries at L1361]
       -> TryGetValue("id2") -> HIT
       -> order2Copy.OrderState == Working -> cancel(order2Copy)
       -> TryRemove("id2")
       _followerCopyMap["id1"] = {order1Copy}  <- INTACT, order1Copy still Working

Result:
  - Follower copy of order2: CANCELLED (correct)
  - Follower copy of order1: UNTOUCHED, still Working (fix applied -- collateral cancel prevented)
```

### V-01 Defect (Fixed in This Revision)

The previous plan revision (V1) erroneously added `_followerCopyMap.TryRemove(orderId, out _)`
to `EvictDedup` (Section 4d) as well as to `CancelScopedFollowerEntries`. Because `EvictDedup`
fires at L1277 — before `TryCancelFollowerEntries` at L1361 — the map entry for `"id2"` was
removed before `CancelScopedFollowerEntries` could use it. The result was a `TryGetValue` miss
and zero cancels: DW-B136 Gap B was **not** fixed.

**V2 Resolution**: `_followerCopyMap.TryRemove` is removed from `EvictDedup` entirely.
`CancelScopedFollowerEntries` is the **sole** eviction point on the cancel path (post-loop).
`EvictDedup` body is unchanged from the current source.

---

## 8. Test Design

**File**: `src/PropTraderTools/Tests/B130Tests.cs`
**Framework**: xUnit [Fact] only (RULES_CATALOG.md mandate)
**Access**: Via `InternalsVisibleTo("PropTraderTools.Tests")` at CopyEngine.cs L46

### Test 1: `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag`

**Assertion goal**: After recording two different leader order IDs in `_followerCopyMap`,
`EvictDedup` for id2 does NOT remove id1's bag from the map.

```csharp
[Fact]
public void B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag()
{
    // Arrange
    var engine = CopyEngine.Instance;
    var bag1 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    var bag2 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    engine._followerCopyMap.TryAdd("leader-id-1", bag1);
    engine._followerCopyMap.TryAdd("leader-id-2", bag2);

    // Act: EvictDedup for leader-id-2 (simulate leader order2 reaching Cancelled)
    engine.EvictDedup("leader-id-2", NinjaTrader.Cbi.OrderState.Cancelled);

    // Assert: leader-id-1 bag is untouched (EvictDedup must NOT sweep _followerCopyMap)
    Assert.True(engine._followerCopyMap.ContainsKey("leader-id-1"),
        "leader-id-1 bag must survive EvictDedup for leader-id-2");
    // Assert: leader-id-2 bag is also still present (EvictDedup must NOT touch _followerCopyMap at all)
    Assert.True(engine._followerCopyMap.ContainsKey("leader-id-2"),
        "leader-id-2 bag must NOT be removed by EvictDedup -- only CancelScopedFollowerEntries evicts");
}
```

**What this verifies**:
- `EvictDedup` does NOT call `_followerCopyMap.TryRemove` for any state (Cancelled or otherwise)
- Regression guard: if a future engineer re-adds `TryRemove` to `EvictDedup`, this test fails
- Map isolation: one leader's EvictDedup path cannot affect another leader's bag

### Test 2: `B130_DW136_CancelScopedFollowerEntriesEvictsMapEntryAfterLoop`

**Assertion goal**: After `CancelScopedFollowerEntries` runs, the map entry is removed.

```csharp
[Fact]
public void B130_DW136_CancelScopedFollowerEntriesEvictsMapEntryAfterLoop()
{
    // Arrange
    var engine = CopyEngine.Instance;
    var bag = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    // Bag contains no real Order objects -- all iteration skips will TryRemove still fires
    engine._followerCopyMap.TryAdd("leader-id-solo", bag);

    // Act: simulate cancel path (no EvictDedup called first -- correct execution order)
    engine.CancelScopedFollowerEntries("leader-id-solo");

    // Assert: entry evicted by CancelScopedFollowerEntries post-loop TryRemove
    Assert.False(engine._followerCopyMap.ContainsKey("leader-id-solo"),
        "CancelScopedFollowerEntries must evict map entry after iterating the bag");
}
```

**What this verifies**:
- `CancelScopedFollowerEntries` calls `TryRemove` after the loop even if the bag is empty
- Single-entry eviction path is clean
- Regression guard: if post-loop TryRemove is removed, this test fails

### Test 3: `B130_DW136_CancelScopedFollowerEntriesMissesAfterEvictDedup`

**Assertion goal**: Validates that `CancelScopedFollowerEntries` correctly handles a missing
map entry (returns without error), AND that the map entry IS still present when
`CancelScopedFollowerEntries` runs before `EvictDedup` (correct production order).

```csharp
[Fact]
public void B130_DW136_CancelScopedFollowerEntriesMissesAfterEvictDedup()
{
    // This test documents the V-01 regression scenario.
    // If someone re-adds TryRemove to EvictDedup, CancelScopedFollowerEntries will miss.
    // This test verifies that (A) CancelScopedFollowerEntries does NOT throw on miss,
    // and (B) the map still has the entry when called in correct order (before EvictDedup).

    var engine = CopyEngine.Instance;
    var bag = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    engine._followerCopyMap.TryAdd("regression-id", bag);

    // Scenario A: call CancelScopedFollowerEntries FIRST (production order) -- entry present
    engine.CancelScopedFollowerEntries("regression-id");
    Assert.False(engine._followerCopyMap.ContainsKey("regression-id"),
        "After CancelScopedFollowerEntries: entry must be evicted");

    // Scenario B: call EvictDedup AFTER -- map miss is a safe no-op
    // (production OnOrderUpdate calls CancelScopedFollowerEntries before EvictDedup completes
    //  its gate chain, but even if EvictDedup ran first, CancelScopedFollowerEntries must not throw)
    var exception = Record.Exception(() =>
        engine.CancelScopedFollowerEntries("regression-id"));
    Assert.Null(exception);
    // Also verify EvictDedup on an already-evicted orderId is a safe no-op
    var exception2 = Record.Exception(() =>
        engine.EvictDedup("regression-id", NinjaTrader.Cbi.OrderState.Cancelled));
    Assert.Null(exception2);
}
```

**What this verifies**:
- `CancelScopedFollowerEntries` does not throw on TryGetValue miss (early return path safe)
- `EvictDedup` called with an already-removed orderId is a safe no-op
- Documents V-01 regression scenario to prevent future re-introduction

---

## 9. 7-Scan Checklist (Pre-Check — Engineer Contract)

The engineer MUST verify all 7 items to zero before marking the ticket BUILD_PASS.

| # | Scan | Command | Pass Criterion |
|---|------|---------|----------------|
| SCAN-01 | No lock() in new or modified code | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero results in new/modified lines |
| SCAN-02 | CYC <= 8 for all modified/new methods | Manual count (see section 6) | RecordFollowerCopy=1, CancelScopedFollowerEntries=5, TryCancelFollowerEntries=4, SendCopy=5, SendCopyWithAtm=4, EvictDedup=2 (unmodified) |
| SCAN-03 | ASCII-only string literals | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero non-ASCII characters in new lines |
| SCAN-04 | JS-001 no throw in hot path | Inspect CancelScopedFollowerEntries | try/catch present, no rethrow |
| SCAN-05 | PTT- prefix on new orders | N/A — no new orders created by this fix | Vacuously satisfied; existing "PTT-Copy" unchanged |
| SCAN-06 | DateTime.UtcNow (no DateTime.Now) | N/A — no DateTime usage in this fix | Vacuously satisfied |
| SCAN-07 | ConcurrentDictionary for new map | Inspect _followerCopyMap declaration | `ConcurrentDictionary<string, ConcurrentBag<Order>>` used; no lock() |

Additional engineer invariants:
- `_followerCopyMap` field: `internal readonly` (NOT private — required for B130Tests.cs access)
- `RecordFollowerCopy` method: `internal` visibility
- `CancelScopedFollowerEntries` method: `internal` visibility
- `TryCancelFollowerEntries` comment: must retain single-entry best-practice note (see section 4c)
- **`EvictDedup` body: MUST NOT be modified** — no `_followerCopyMap.TryRemove` is added
- `CancelScopedFollowerEntries` calls `TryRemove` after loop — this is the ONLY map eviction on the cancel path
- Double TryRemove (if CancelScopedFollowerEntries called twice for same id) is safe — returns false on miss, no-op

---

## 10. DW Items

**None.** All NT8 API questions were resolved by existing docs and existing code patterns:

- `Order.Account` usage confirmed: `CopyEngine.cs` L1609 (`order.Account.Name`)
- `acc.Cancel(Order[])` from AddOn confirmed: `NT8_ADDON_KNOWLEDGE.md` line 222
- `signal.OrderId` available in `SendCopy`: `CopySignal` struct field at L497, set at L511
- `Order.OrderId.ToString()` key format confirmed: existing pattern at L1894, L1684, L3516
- `ConcurrentBag<Order>`: standard .NET 4.8, no NT8 API dependency
- `fo.Account.Cancel(fo)` pattern: structurally identical to existing `CancelOneAccount`
  which calls `acc.Cancel(new Order[] { order })` at L3336
- `EvictDedup` execution order: confirmed at `OnOrderUpdate` L1277 vs L1361 (CopyEngine.cs)

---

## 11. Component Summary

| Component | Type | File | CYC | Visibility | Modified? |
|-----------|------|------|-----|------------|-----------|
| `_followerCopyMap` | Field (new) | `CopyEngine.cs` | N/A | `internal readonly` | New |
| `RecordFollowerCopy` | Method (new) | `CopyEngine.cs` | 1 | `internal` | New |
| `CancelScopedFollowerEntries` | Method (new) | `CopyEngine.cs` | 5 | `internal` | New |
| `TryCancelFollowerEntries` | Method (modified) | `CopyEngine.cs` | 4 (was 6) | `private` | Modified |
| `SendCopy` | Method (modified) | `CopyEngine.cs` | 5 (unchanged) | `private` | Modified |
| `SendCopyWithAtm` | Method (modified) | `CopyEngine.cs` | 4 (unchanged) | `private` | Modified |
| `EvictDedup` | Method (unchanged) | `CopyEngine.cs` | 2 (unchanged) | `internal` | **NOT modified** |
| `B130Tests.cs` | Test file (new) | `Tests/B130Tests.cs` | N/A | N/A | New |

---

## 12. Spec Requirement Satisfied

| Spec Reference | Requirement | Covered By |
|----------------|------------|-----------|
| `#section-dw-b136` Gap B | Scope cancel to specific leader order ID | `CancelScopedFollowerEntries` + `_followerCopyMap` |
| `#section-dw-b136` | Single-entry constraint documented (not removed) | Comment in `TryCancelFollowerEntries` |
| Spec fix design (L39077-81) | "TryCancelFollowerEntries should only cancel follower orders copied from specific leader" | `RecordFollowerCopy` + `CancelScopedFollowerEntries` |

---

## 13. Out of Scope for This Lane

The following defects are documented in the deferred backlog and are **NOT** addressed by B130 LaneB:

- DW-B134-OCO (OCO orphan risk after ATM STP cancel+resubmit) — P2, B130 LaneA or later
- DW-B129-01 (Director SIM Gate: Quick2t/QAll2t) — Director-owned
- DW-B133 (2-target forced count for PttGlobalQuickExit ALL path) — deferred
- All DW-B89-DEFERRED-xx items — Director-owned SIM gates
- DW-B107 (MoveStopToBreakEven Step A stale target snapshot) — separate block

---

## Return

**PLAN_COMPLETE**
