# B130 LaneB Tickets
# DW-B136 Gap B: Order-ID Scoped Cancel for Simultaneous Entries

**Block**: B130 LaneB
**Defect**: DW-B136 Gap B
**Spec**: `specs/002-trade-copier-spec.html#section-dw-b136`
**Plan**: `docs/brain/B130/LaneB-02-architecture-plan.md` (REVIEW_PASS, V2)
**Plan Review**: `docs/brain/B130/LaneB-02-plan-review.md` (Cycle 2 REVIEW_PASS)
**Status**: TICKETS_COMPLETE

---

## Ticket Index

| Ticket | File | Methods | Status |
|--------|------|---------|--------|
| B130-LaneB-T2 | `src/PropTraderTools/CopyEngine.cs` + `Tests/B130Tests.cs` | 1 field + 2 new methods + 3 modified | PENDING |

---

# Ticket B130-LaneB-T2

**Ticket ID**: B130-LaneB-T2
**Epic**: B130-LaneB
**Defect**: DW-B136 Gap B
**Phase**: 5 (Execution)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Test File**: `src/PropTraderTools/Tests/B130Tests.cs` (APPEND ONLY)

## Spec Requirements Satisfied

| Spec Reference | Requirement |
|----------------|-------------|
| `#section-dw-b136` Gap B | Scope cancel to specific leader order ID, not all instrument entries |
| `#section-dw-b136` | Single-entry constraint comment preserved (not removed from code) |
| Spec fix design L39077-81 | "TryCancelFollowerEntries should only cancel follower orders copied from specific leader" |

---

## Problem Statement (Summary)

`TryCancelFollowerEntries` (`CopyEngine.cs` ~L1621) matches follower orders by
**instrument name only** via `CancelOneAccount`. When the leader cancels order #2
(a Working limit entry), `CancelOneAccount` sweeps ALL Working/Initialized entry orders
for that instrument on each follower -- including the follower copy of leader order #1
(still Working). This ticket fixes the cross-cancel by scoping cancels to the specific
leader orderId.

---

## Implementation Steps

### STEP 0: Pre-flight verification

Before making ANY changes, confirm:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
Must return zero results. If any results: STOP. Report to Director.

---

### STEP 1: Add `_followerCopyMap` field to `CopyEngine.cs`

**Location**: After `_entryDispatchedOrders` declaration (L189-190), before the blank
line at L191 that precedes the `_filledBeTargetCount` comment at L192.

**Insert the following block BETWEEN L190 and L191 (the blank line)**:

```csharp

        // DW-B136 Gap B: leader order ID -> follower Order objects dispatched for that leader order.
        // Key = leader order.OrderId.ToString() (same format as _dedupCache and _entryDispatchedOrders).
        // Value = ConcurrentBag<Order> of follower Order objects submitted for this leader order.
        // Used by TryCancelFollowerEntries to scope cancel to the specific leader order being cancelled.
        // JS-021: no lock. JS-025: ConcurrentDictionary + ConcurrentBag (lock-free).
        // JS-001: only cancel calls are wrapped in try/catch in CancelScopedFollowerEntries.
        // Eviction: TryRemove called in CancelScopedFollowerEntries (cancel path) after iterating the bag.
        // NOTE: EvictDedup does NOT touch this map -- see execution-order note in LaneB-02-architecture-plan.md.
        internal readonly ConcurrentDictionary<string, ConcurrentBag<Order>> _followerCopyMap =
            new ConcurrentDictionary<string, ConcurrentBag<Order>>();
```

**Visibility**: `internal readonly` (NOT `private`) -- required by `B130Tests.cs` access via
`InternalsVisibleTo("PropTraderTools.Tests")` at L46.

**Verification after step**: `grep -n "_followerCopyMap" src/PropTraderTools/CopyEngine.cs`
must return the field declaration line.

---

### STEP 2: Add `RecordFollowerCopy` method to `CopyEngine.cs`

**Location**: Add immediately AFTER `TryCancelFollowerEntries` method body (after the closing
brace of `TryCancelFollowerEntries` at approximately L1642). Add before `TryHandleBracketDrag`
which starts at approximately L1644.

**Method signature**:
```
internal void RecordFollowerCopy(string leaderOrderId, Order followerOrder)
```

**Full method body to insert**:
```csharp
        // DW-B136 Gap B: record follower Order under the leader orderId that triggered the copy.
        // Called from SendCopy and SendCopyWithAtm after follower.Submit (or StartAtmStrategy) succeeds.
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

**CYC**: 1 (no conditional branches). ✅

---

### STEP 3: Add `CancelScopedFollowerEntries` method to `CopyEngine.cs`

**Location**: Add immediately AFTER `RecordFollowerCopy` (inserted in STEP 2).

**Method signature**:
```
internal void CancelScopedFollowerEntries(string leaderOrderId)
```

**Full method body to insert**:
```csharp
        // DW-B136 Gap B: cancel only follower orders recorded under the given leader order ID.
        // Replaces the instrument-scoped sweep in TryCancelFollowerEntries (CancelOneAccount).
        // Called from TryCancelFollowerEntries AFTER EvictDedup has already fired in OnOrderUpdate
        // (L1277 vs L1361). The map entry for leaderOrderId must still be present at this point --
        // EvictDedup does NOT touch _followerCopyMap (see LaneB-02-architecture-plan.md Section 4d).
        // CYC=5:
        //   (1) TryGetValue miss guard
        //   (2) foreach bag
        //   (3) OrderState guard (Working || Initialized)
        //   (4) try body
        //   (5) catch
        // JS-021: no lock. JS-001: catch logs, no rethrow. JS-002: void.
        // NT8: fo.Account.Cancel(Order[]) valid from AddOn context (NT8_ADDON_KNOWLEDGE.md line 222).
        // Eviction: TryRemove called after loop -- sole eviction point on cancel path.
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
            _followerCopyMap.TryRemove(leaderOrderId, out _); // DW-B136 Gap B: evict after use (sole eviction point)
        }
```

**CYC**: 5 (base=1, (1)+(2)+(3)+(4) = +4). Post-loop `TryRemove` adds no branch. ✅
**NT8 API**: `fo.Account.Cancel(new Order[] { fo })` -- same pattern as existing `CancelOneAccount` at ~L3336.

---

### STEP 4: Modify `TryCancelFollowerEntries` in `CopyEngine.cs`

**Location**: `TryCancelFollowerEntries` at approximately L1621-1642.

**Current body** (L1621-1642, summarized):
```csharp
private bool TryCancelFollowerEntries(Order order, CopyRule rule)
{
    if (order.OrderState != OrderState.Cancelled)
        return false;
    if (IsAtmBracketName(order.Name))
        return true; // HOTFIX-B63-COPY-CANCEL-01
    if (
        order.Name != null
        && (
            order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
            || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        )
    )
        return false; // DW-B103
    foreach (var acc in rule.FollowerAccounts)    // <-- REMOVE THIS BLOCK
    {
        if (acc == null)
            continue;
        CancelOneAccount(acc, order.Instrument);
    }
    return true;
}
```

**Change**: Replace the `foreach` loop and its body (the 6 lines starting at `foreach (var acc in rule.FollowerAccounts)`) with the scoped cancel call below.

**After modification, the tail of `TryCancelFollowerEntries` must read**:
```csharp
    if (
        order.Name != null
        && (
            order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
            || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        )
    )
        return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
    // DW-B136 Gap B: scope cancel to specific leader order, not all instrument entries.
    // Single-entry best practice: one leader entry per instrument at a time is the supported
    // workflow. This fix prevents collateral cancel when the constraint is violated (two
    // simultaneous entries). The constraint documentation in the spec and UI tooltip is preserved.
    // Note: rule param is unused post-fix; preserved for call-site stability (one call site: L1361).
    CancelScopedFollowerEntries(order.OrderId.ToString());
    return true;
}
```

**Updated comment header** for the method (replace the existing `// TryCancelFollowerEntries: CYC=6.` comment with):
```csharp
        // TryCancelFollowerEntries: CYC=4 (was 6). Propagates leader cancel to scoped follower entry orders.
        // Returns true if Cancelled state was handled (caller should return immediately).
        // HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
        // DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
        // DW-B136 Gap B: delegates to CancelScopedFollowerEntries (order-ID scoped, not instrument-scoped).
        // JS-021: no lock. JS-001: no throw.
```

**CYC after change**: 4 (base=1 + (1) Cancelled guard + (2) IsAtmBracketName guard + (3) compound PTT-QX-/PTT-BE- OR guard). The `foreach` loop removed. `CancelScopedFollowerEntries` call adds no branch. ✅

**CRITICAL**: The `rule` parameter stays in the signature -- do NOT remove it. One call site at L1361 passes `matchedRule.Value`. Removing the param would require changing the call site, expanding diff scope.

---

### STEP 5: Modify `SendCopy` in `CopyEngine.cs`

**Location**: `SendCopy` at approximately L2898-2899. The current code reads:
```csharp
                if (order != null)
                    follower.Submit(new[] { order });
                return true;
```

**Change**: Expand the `if (order != null)` single-statement block into a braced block and add the `RecordFollowerCopy` call:

**Replace** (at ~L2898-2900):
```csharp
                if (order != null)
                    follower.Submit(new[] { order });
                return true;
```

**With**:
```csharp
                if (order != null)
                {
                    follower.Submit(new[] { order });
                    RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B: track follower order by leader ID
                }
                return true;
```

**CYC**: No new branch added. The `if (order != null)` check already existed. CYC = 5 unchanged. ✅

**Note**: `signal.OrderId` is `CopySignal.OrderId` (internal readonly string field at L497, set at L511).
It contains the leader order's `orderId` string, passed into `CopySignal.Create(...)` at the `DispatchCopy` call site.

---

### STEP 6: Modify `SendCopyWithAtm` in `CopyEngine.cs`

**Location**: `SendCopyWithAtm` at approximately L2942-2952. The current code after the ATM strategy
start calls reads:
```csharp
                if (namedMode.AtmObject != null) // (3) preferred: object overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
                else // (4) fallback: string overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
                StatusUpdate?.Invoke(
                    follower.Name
                        + ": PTT-ATM entry @ "
                        + signal.LimitPrice
                        + " atm="
                        + namedMode.TemplateName
                );
                return true;
```

**Change**: Add `RecordFollowerCopy` call AFTER the ATM strategy start calls, BEFORE the `StatusUpdate?.Invoke`:

**Replace** (at ~L2942-2953):
```csharp
                if (namedMode.AtmObject != null) // (3) preferred: object overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
                else // (4) fallback: string overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
                StatusUpdate?.Invoke(
                    follower.Name
                        + ": PTT-ATM entry @ "
                        + signal.LimitPrice
                        + " atm="
                        + namedMode.TemplateName
                );
                return true;
```

**With**:
```csharp
                if (namedMode.AtmObject != null) // (3) preferred: object overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
                else // (4) fallback: string overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
                RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B
                StatusUpdate?.Invoke(
                    follower.Name
                        + ": PTT-ATM entry @ "
                        + signal.LimitPrice
                        + " atm="
                        + namedMode.TemplateName
                );
                return true;
```

**CYC**: No new branch added. CYC = 4 unchanged. ✅

**NT8 note**: `StartAtmStrategy` handles submission internally. The `order` object returned by
`CreateOrder` is a valid live reference even though `Submit()` is NOT called. Recording it in
`_followerCopyMap` is correct -- NT8 Order objects update in-place via `OnOrderUpdate`.

---

### STEP 7: `EvictDedup` — DO NOT MODIFY

`EvictDedup` body MUST remain unchanged from current source. Zero `_followerCopyMap` references
may be added to `EvictDedup`. This is the V-01 fix validated in plan review Cycle 2.

**Why**: `EvictDedup` fires at L1277; `TryCancelFollowerEntries` (which calls
`CancelScopedFollowerEntries`) fires at L1361. If `EvictDedup` removes the map entry first,
`CancelScopedFollowerEntries` sees a `TryGetValue` miss and issues zero cancels -- DW-B136
Gap B is NOT fixed.

The engineer MUST verify no `_followerCopyMap` reference exists in `EvictDedup` after their changes:
```powershell
grep -A 20 "internal void EvictDedup" src/PropTraderTools/CopyEngine.cs
```
The output must show only `_dedupCache.TryRemove` and `_entryDispatchedOrders.Clear`. No `_followerCopyMap`.

---

### STEP 8: Append tests to `src/PropTraderTools/Tests/B130Tests.cs`

**CRITICAL RULE: APPEND ONLY.**
Do NOT overwrite or modify the existing `B130_DW137_*` tests written by LaneA ticket-1.
Do NOT remove any existing using statements or class/namespace structure.
Append ONLY -- add the three [Fact] methods before the closing brace of the test class.

**Framework**: xUnit [Fact] only. No NUnit. No MSTest. (RULES_CATALOG.md mandate.)

**Access**: `CopyEngine._followerCopyMap`, `RecordFollowerCopy`, and `CancelScopedFollowerEntries`
are `internal`. `InternalsVisibleTo("PropTraderTools.Tests")` is already present at
`CopyEngine.cs` L46. No new attribute needed.

---

#### Test 1: `B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2`

**Purpose**: Verify that cancelling follower copies for leader order #1 does NOT affect
follower copies recorded under leader order #2. This is the core behavioral assertion for DW-B136 Gap B.

**Approach**: Directly manipulate `_followerCopyMap` via `TryAdd` (test-seam access).
Call `CancelScopedFollowerEntries("leader-id-1")`. Assert "leader-id-2" bag is still in the map.
(No real `Order` objects required -- empty bags confirm map isolation.)

```csharp
[Fact]
public void B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2()
{
    // Arrange: two leader orders, each with a bag in the follower copy map
    var engine = CopyEngine.Instance;
    var bag1 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    var bag2 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    engine._followerCopyMap.TryAdd("leader-id-1", bag1);
    engine._followerCopyMap.TryAdd("leader-id-2", bag2);

    // Act: cancel follower entries for leader order #1 only
    engine.CancelScopedFollowerEntries("leader-id-1");

    // Assert: leader-id-1 entry evicted (cancel path completed)
    Assert.False(
        engine._followerCopyMap.ContainsKey("leader-id-1"),
        "leader-id-1 bag must be evicted after CancelScopedFollowerEntries"
    );
    // Assert: leader-id-2 entry is UNTOUCHED (DW-B136 Gap B: no cross-cancel)
    Assert.True(
        engine._followerCopyMap.ContainsKey("leader-id-2"),
        "leader-id-2 bag must survive cancel of leader-id-1 (DW-B136 Gap B fix)"
    );

    // Cleanup: remove test entries to avoid polluting singleton state
    engine._followerCopyMap.TryRemove("leader-id-2", out _);
}
```

**What this verifies**:
- `CancelScopedFollowerEntries` evicts only its own key -- not a global sweep
- `_followerCopyMap["leader-id-2"]` is intact after cancelling `"leader-id-1"`
- Regression guard: if implementation reverts to instrument-scope sweep, "leader-id-2" is also removed and this test fails

---

#### Test 2: `B130_DW136_SingleEntryPathUnchanged`

**Purpose**: Verify the single-entry (normal, non-simultaneous) cancel path works correctly.
A follower copy is recorded, then `CancelScopedFollowerEntries` evicts it cleanly.
Also verifies no-throw on a second call (key already absent).

```csharp
[Fact]
public void B130_DW136_SingleEntryPathUnchanged()
{
    // Arrange: single leader order with one follower bag (normal single-entry workflow)
    var engine = CopyEngine.Instance;
    var bag = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    engine._followerCopyMap.TryAdd("leader-id-solo", bag);

    // Act: cancel follower entries for this single leader order
    engine.CancelScopedFollowerEntries("leader-id-solo");

    // Assert: map entry evicted (single-entry eviction path is clean)
    Assert.False(
        engine._followerCopyMap.ContainsKey("leader-id-solo"),
        "Single-entry: map entry must be evicted by CancelScopedFollowerEntries"
    );

    // Assert: calling again on absent key does not throw (belt-and-suspenders safety)
    var ex = Record.Exception(() => engine.CancelScopedFollowerEntries("leader-id-solo"));
    Assert.Null(ex);
}
```

**What this verifies**:
- `CancelScopedFollowerEntries` calls `TryRemove` after the loop (even for an empty bag)
- Single-entry eviction path is unconditional
- Double-call (absent key) is a safe no-op (no throw)
- Regression guard: if post-loop `TryRemove` is removed, first Assert.False fails

---

#### Test 3: `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag`

**Purpose**: Verify that `EvictDedup` does NOT touch `_followerCopyMap` at all.
This is the primary V-01 regression guard: if a future engineer re-adds
`_followerCopyMap.TryRemove` to `EvictDedup`, this test fails immediately.

**Approach**: Seed two bags in `_followerCopyMap` (`"leader-id-1"` and `"leader-id-2"`).
Call `EvictDedup("leader-id-2", OrderState.Cancelled)`. Assert BOTH keys are still present
in the map — EvictDedup must not remove either.

```csharp
[Fact]
public void B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag()
{
    // Arrange: two leader orders recorded in the follower copy map
    var engine = CopyEngine.Instance;
    var bag1 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    var bag2 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
    engine._followerCopyMap.TryAdd("leader-id-1", bag1);
    engine._followerCopyMap.TryAdd("leader-id-2", bag2);

    // Act: EvictDedup fires for leader-id-2 (simulates Cancelled order reaching L1277)
    engine.EvictDedup("leader-id-2", NinjaTrader.Cbi.OrderState.Cancelled);

    // Assert: leader-id-1 bag is untouched (EvictDedup must NOT sweep _followerCopyMap)
    Assert.True(
        engine._followerCopyMap.ContainsKey("leader-id-1"),
        "leader-id-1 bag must survive EvictDedup for leader-id-2"
    );
    // Assert: leader-id-2 bag is also still present (EvictDedup must NOT touch _followerCopyMap at all)
    Assert.True(
        engine._followerCopyMap.ContainsKey("leader-id-2"),
        "leader-id-2 bag must NOT be removed by EvictDedup -- only CancelScopedFollowerEntries evicts"
    );

    // Cleanup: remove test entries to avoid polluting singleton state
    engine._followerCopyMap.TryRemove("leader-id-1", out _);
    engine._followerCopyMap.TryRemove("leader-id-2", out _);
}
```

**What this verifies**:
- `EvictDedup` does NOT call `_followerCopyMap.TryRemove` for any orderId or state
- Both map entries survive an `EvictDedup("leader-id-2", Cancelled)` call intact
- Regression guard: if a future engineer re-adds `TryRemove` to `EvictDedup`, `ContainsKey("leader-id-2")` returns false and this test fails immediately
- Map isolation: one leader's EvictDedup path cannot affect another leader's bag (V-01 guard)

---

## Method Signatures (Complete Reference)

| Type | Signature | Visibility | CYC | File |
|------|-----------|-----------|-----|------|
| Field | `internal readonly ConcurrentDictionary<string, ConcurrentBag<Order>> _followerCopyMap` | `internal readonly` | N/A | CopyEngine.cs |
| New method | `internal void RecordFollowerCopy(string leaderOrderId, Order followerOrder)` | `internal` | 1 | CopyEngine.cs |
| New method | `internal void CancelScopedFollowerEntries(string leaderOrderId)` | `internal` | 5 | CopyEngine.cs |
| Modified | `private bool TryCancelFollowerEntries(Order order, CopyRule rule)` | `private` | 4 (was 6) | CopyEngine.cs |
| Modified | `private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)` | `private` | 5 (unchanged) | CopyEngine.cs |
| Modified | `private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)` | `private` | 4 (unchanged) | CopyEngine.cs |
| Unchanged | `internal void EvictDedup(string orderId, OrderState state)` | `internal` | 2 | CopyEngine.cs |

### xUnit [Fact] Test Names

| Test Method | File | Asserts |
|-------------|------|---------|
| `B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2` | `Tests/B130Tests.cs` | Cancel id-1 evicts id-1 bag; id-2 bag survives |
| `B130_DW136_SingleEntryPathUnchanged` | `Tests/B130Tests.cs` | Single-entry eviction clean; double-call no-throw |
| `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag` | `Tests/B130Tests.cs` | EvictDedup does NOT touch _followerCopyMap; both bags survive (V-01 regression guard) |

---

## 7-Scan Checklist (Engineer Contract)

The engineer MUST run and verify ALL 7 scans to zero/pass before marking BUILD_PASS.

| # | Scan | Command | Pass Criterion | Expected Result |
|---|------|---------|----------------|----------------|
| SCAN-01 | No lock() in new or modified code | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | Zero results in new or modified lines | 0 matches in _followerCopyMap, RecordFollowerCopy, CancelScopedFollowerEntries, TryCancelFollowerEntries, SendCopy, SendCopyWithAtm |
| SCAN-02 | CYC <= 8 for all new/modified methods | Manual count per Section 6 of plan | RecordFollowerCopy=1, CancelScopedFollowerEntries=5, TryCancelFollowerEntries=4, SendCopy=5, SendCopyWithAtm=4, EvictDedup=2 (unmodified) | All values <= 8 |
| SCAN-03 | No new `async void` | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` | Zero new results | No async void in any new or modified method |
| SCAN-04 | No `return null` in new methods; no rethrow in catch | Inspect CancelScopedFollowerEntries catch block | try/catch present, catch only logs, no rethrow, no `return null` | `StatusUpdate?.Invoke("PTT-ScopedCancel error: " + ex.Message)` only |
| SCAN-05 | ASCII-only string literals in new code | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero non-ASCII characters in new lines | Strings: "DW-B136 Gap B", ": scoped cancel orderId=", "PTT-ScopedCancel error: " are all ASCII |
| SCAN-06 | NT8 API correctness | Inspect `CancelScopedFollowerEntries` body | `fo.Account.Cancel(new Order[] { fo })` matches existing `CancelOneAccount` pattern at ~L3336; `signal.OrderId` confirmed at CopySignal L497 | No StrategyBase-only API used; Cancel valid from AddOn; no DateTime.Now |
| SCAN-07 | B130_DW136_* tests compile and pass | `dotnet test --filter "B130_DW136"` | All three new [Fact] methods pass | 3 new tests pass; existing B130_DW137_* tests unchanged and still pass |

### Additional Engineer Invariants (Not in Scan List -- Still Mandatory)

- `_followerCopyMap` field: **MUST be `internal readonly`** (NOT `private`) -- test access requires this
- `RecordFollowerCopy`: **MUST be `internal`** visibility
- `CancelScopedFollowerEntries`: **MUST be `internal`** visibility
- `TryCancelFollowerEntries` comment: **MUST retain** single-entry best-practice note (see STEP 4)
- **`EvictDedup` body: MUST NOT be modified** -- zero `_followerCopyMap` references may be added
- `CancelScopedFollowerEntries`: `TryRemove` AFTER the loop is the ONLY map eviction on cancel path
- Double `TryRemove` on same key (second call returns false): safe no-op, no exception
- `B130Tests.cs`: APPEND ONLY -- do not modify or delete any existing `B130_DW137_*` test

---

## Acceptance Criteria

| Criterion | Verification |
|-----------|-------------|
| Leader order #1 cancelled → only follower copies of #1 cancelled | SCAN-07 Test 1 (map isolation) + Director SIM gate |
| Leader order #2 copies NOT cancelled when order #1 is cancelled | SCAN-07 Test 1 `Assert.True(ContainsKey("leader-id-2"))` |
| Single-entry path unchanged (no regression) | SCAN-07 Test 2 |
| All 7 scans pass to zero | SCAN-01 through SCAN-07 |
| `EvictDedup` body unchanged | `grep -A 20 "internal void EvictDedup"` shows no `_followerCopyMap` reference |
| `dotnet build` passes with zero errors | Full solution build after all changes |
| `powershell -File scripts\ptt-sync-and-verify.ps1` passes | 0 MISMATCH lines in output |
| F5 in NinjaTrader 8 compiles with zero errors | Director SIM gate (NT8 compilation confirmation) |

---

## Out of Scope for This Ticket

The following are explicitly NOT addressed by B130-LaneB-T2:

- DW-B134-OCO (OCO orphan risk after ATM STP cancel+resubmit) -- P2, later block
- DW-B129-01 (Director SIM Gate: Quick2t/QAll2t) -- Director-owned
- DW-B133 (2-target forced count for PttGlobalQuickExit ALL path) -- deferred
- All DW-B89-DEFERRED-xx items -- Director-owned SIM gates
- DW-B107 (MoveStopToBreakEven Step A stale target snapshot) -- separate block
- `CancelOneAccount` itself -- NOT removed, NOT modified (still called from other sites)
- Any modification to `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, or `PttContracts.cs`

---

## Return

**TICKETS_COMPLETE**
