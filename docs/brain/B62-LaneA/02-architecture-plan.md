# B62-LaneA — Architecture Plan
# Live Entry Drag Sync + Price-Keyed Dedup Fix

**Block**: B62-LaneA
**Phase**: 1 (Architecture)
**Epic DW**: DW-B62-01
**Written by**: ptt-architect
**Date**: 2026-08-11
**Status**: REVIEW_PENDING

---

## Section 1 — Block Summary

### Feature: DW-B62-01 — Live Entry Drag Sync

**Problem statement**

When a leader drags a working limit entry order in Chart Trader or SuperDOM, NT8 fires
`OnOrderUpdate` with the SAME `orderId` but a new `LimitPrice` at the `Accepted` then `Working`
states. PTT currently has no path to call `acc.Change()` on the follower's working `PTT-Copy`
order. The follower entry stays at the old price indefinitely.

**Second problem — dedup time-expiry bug**

`IsDedup` uses a 10-second time-based expiry. If the leader drags after 10 seconds, the dedup
cache has already evicted the `orderId`. When the drag fires `OnOrderUpdate` again, `DispatchCopy`
treats it as a fresh order, calls `IsDedup` (which calls `TryAdd`, which succeeds because the key
was evicted), then submits a second `PTT-Copy` entry order instead of moving the existing one.
This creates a phantom duplicate live entry on every follower account.

**Root cause (two-part)**

1. `_dedupCache` stores a `long` timestamp and expires entries after 10 s. There is no semantic
   link between the cached value and the order's price. The cache cannot answer "has the price
   changed for a known orderId?"
2. `OnOrderUpdate` has no "Gate C" for entry drag detection. After Gate B (bracket drag), the code
   falls straight to `DispatchCopy` for all non-bracket orders — including the repeated Working
   state events that NT8 fires when the leader drags a limit entry.

**Fix strategy**

Change `_dedupCache` to store `double` (last dispatched `LimitPrice`) instead of `long`
(timestamp). This transforms the cache from a time-based expiry store into a price-keyed map.

Insert "Gate C" in `OnOrderUpdate` between Gate B (bracket) and `DispatchCopy`. Gate C detects
"same orderId already in cache + price has moved by at least one tick" and diverts to a new
`HandleEntryChange` method that calls `acc.Change()` on each follower's working `PTT-Copy` entry.

Add `EvictDedup` to remove cache entries when an order reaches a terminal state (Filled /
Cancelled / Rejected). This replaces the time-expiry eviction that was the root of the phantom
duplicate bug. `EvictDedup` is called from the `OnOrderUpdate` pre-gate (before Gate 1), ensuring
it fires even when copy is disabled.

---

## Section 2 — NT8 API Confirmation

### Drag event sequence (confirmed live logs 2026-08-11)

```
Order='X/Sim101' Name='Entry' New state='Change submitted'
Order='X/Sim101' Name='Entry' New state='Accepted'  LimitPrice=NEW_PRICE
Order='X/Sim101' Name='Entry' New state='Working'   LimitPrice=NEW_PRICE
```

Key observations:
- The `orderId` (`X`) is identical across all three state events.
- `LimitPrice` is updated on the `Accepted` and `Working` states — both carry the new price.
- Gate C fires on `Accepted` OR `Working` (either is sufficient; the second will be a no-op
  because `HandleEntryChange` updates `_dedupCache[orderId]` to `newPrice` on first fire,
  so the second event will have `storedPrice == newPrice` and the delta guard skips it).

### `Account.Change(Order[])` — NT8_FULL_REFERENCE.md line 328-329

> "Change() — Changes specified order(s) on the account"

Pattern already used in `SyncFollowerBracket` (confirmed at `CopyEngine.cs` line 865):
```csharp
acc.Change(new Order[] { fo });
```

`HandleEntryChange` uses the identical calling convention.

### `Order.LimitPrice` write + `acc.Change()` pattern

```csharp
fo.LimitPrice = newPrice;
acc.Change(new Order[] { fo });
```

This is the same pattern as `SyncFollowerBracket`. No new NT8 API surface is introduced; B62
reuses the existing `acc.Change()` path already proven in bracket drag sync (B10).

---

## Section 3 — All 7 Changes (Exact)

### Change 1 — `_dedupCache` field type: `long` → `double`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 112

**Current**:
```csharp
private readonly ConcurrentDictionary<string, long> _dedupCache = new ConcurrentDictionary<string, long>(); // JS-025
```

**Required**:
```csharp
// B62: value changed from long (timestamp) to double (last dispatched LimitPrice).
// Enables drag detection: same orderId + different price = leader dragged.
// JS-025: ConcurrentDictionary is lock-free.
private readonly ConcurrentDictionary<string, double> _dedupCache = new ConcurrentDictionary<string, double>(); // JS-025
```

**Rationale**: See Section 5.

---

### Change 2 — Replace `IsDedup` body (price-keyed, no time expiry)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 1448–1465

**Current**:
```csharp
private bool IsDedup(string orderId)
{
    long now = DateTime.UtcNow.Ticks;
    long expiry = TimeSpan.FromSeconds(10).Ticks;

    // Prune expired entries
    foreach (var key in _dedupCache.Keys)
    {
        if (_dedupCache.TryGetValue(key, out long storedTicks) && now - storedTicks > expiry)
            _dedupCache.TryRemove(key, out _);
    }

    // Attempt add -- if TryAdd returns false, orderId already exists (duplicate)
    if (!_dedupCache.TryAdd(orderId, now))
        return true;

    return false;
}
```

**Required** (signature changes to add `double limitPrice` param; body simplified to CYC=2):
```csharp
// B62: price-keyed dedup. Stores LimitPrice (double) instead of timestamp (long).
// First call for orderId: TryAdd succeeds -> not a dup -> dispatch.
// Repeat call same orderId: TryAdd fails -> true dup -> skip.
// Drag detection is handled by Gate C BEFORE this is called -- drag events never reach IsDedup.
// Eviction is handled by EvictDedup on terminal states (Filled/Cancelled/Rejected).
// CYC=2: TryAdd false-path (1) + early return.
// JS-025: ConcurrentDictionary.TryAdd is lock-free.
private bool IsDedup(string orderId, double limitPrice)
{
    if (!_dedupCache.TryAdd(orderId, limitPrice))
        return true;

    return false;
}
```

**CYC improvement**: 7 (current, with foreach + two branches) → 2 (new). The 10-second pruning
loop is deleted entirely; terminal-state eviction via `EvictDedup` replaces it.

---

### Change 3 — Update `IsDedup` call site in `DispatchCopy` Gate 5

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 763

**Current**:
```csharp
if (IsDedup(order.OrderId.ToString()))
    return;
```

**Required**:
```csharp
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```

**Note**: For market orders, `order.LimitPrice` is 0.0. This is safe — the stored value for
market orders will always be 0.0, and Gate C only fires on `OrderType.Limit`, so market orders
will never be evaluated in Gate C's price-delta comparison.

---

### Change 4 — Add `EvictDedup` method (terminal state eviction)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert position**: Immediately after the new `IsDedup` method (after line 1465 post-edit)

```csharp
// B62: evict dedup entry when order reaches terminal state (Filled/Cancelled/Rejected).
// Called unconditionally from OnOrderUpdate pre-gate, after TryFirePositionState.
// Ensures evicted orderId can be detected as drag-free new order on next placement.
// CYC=2: terminal-state guard (1) + TryRemove (no branch).
// JS-025: ConcurrentDictionary.TryRemove is lock-free.
internal void EvictDedup(string orderId, OrderState state)
{
    if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected)
        return;

    _dedupCache.TryRemove(orderId, out _);
}
```

**Visibility**: `internal` — required for T_B62_03 and T_B62_05 test access without reflection.

---

### Change 5 — Wire `EvictDedup` in `OnOrderUpdate` pre-gate

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 602–607

**Current**:
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

**Required** (insert one line after `TryFirePositionState`):
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);
// B62: evict dedup on terminal states so orderId is not permanently blocked.
EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

**Why pre-gate (before Gate 1)**: If copy is disabled at the moment an order fills, `EvictDedup`
must still fire. Without pre-gate placement, a filled `orderId` would stay in `_dedupCache`
permanently when copy is toggled off at fill time. The next placement of a new order with a
recycled `orderId` would then be permanently deduped.

---

### Change 6 — Add `FindFollowerEntryOrder` method

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert position**: Immediately after `FindFollowerBracketOrder` (ends at line 931)

```csharp
// B62: find the follower's working PTT-Copy limit entry order for the instrument.
// Mirror of FindFollowerBracketOrder -- matches by Name=="PTT-Copy" + Limit + Working.
// Used by HandleEntryChange to locate the order to acc.Change().
// CYC=3: foreach(1), instrument guard(2), state+name+type guard(3).
// JS-002: returns null when not found -- callers must null-guard.
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
{
    foreach (var order in follower.Orders.ToList())                       // (1)
    {
        if (order.Instrument != instrument)                               // (2)
            continue;
        if (order.OrderState == OrderState.Working                        // (3)
            && order.OrderType == OrderType.Limit
            && order.Name == "PTT-Copy")
            return order;
    }
    return null;
}
```

**Note**: `static` because it takes all required context as parameters (no `this` access). The
`PTT-Copy` name constant matches `DispatchCopy`'s submission naming convention. If the follower
has no matching order (already filled, cancelled, or not yet Working), returns `null` — callers
in `HandleEntryChange` null-guard with `if (fo == null) continue;`.

---

### Change 7 — Add `HandleEntryChange` + Gate C in `OnOrderUpdate`

#### Part A — `HandleEntryChange` method

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert position**: Immediately after `HandleBracketChange` (which ends at line 906)

```csharp
// B62: sync a leader entry drag to all follower working PTT-Copy limit orders.
// Mirror of HandleBracketChange -- tick-rounds price, calls acc.Change() per follower.
// Triggered by Gate C when leader's entry orderId is already in dedup cache but price changed.
// CYC=5: instr null(1), tickSize zero(2), price delta guard(3), foreach acc(4), fo null(5).
// JS-001: try/catch around acc.Change() -- no throw in hot path.
// JS-021: no lock -- _dedupCache is ConcurrentDictionary (lock-free).
private void HandleEntryChange(Order leaderOrder, CopyRule rule)
{
    var instrument = leaderOrder.Instrument;
    if (instrument == null)                                                    // (1)
        return;

    double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0;           // (2)
    double rawPrice = leaderOrder.LimitPrice;
    double newPrice = tickSize > 0
        ? Math.Round(rawPrice / tickSize) * tickSize
        : rawPrice;

    // Update stored price in dedup cache to track latest leader price.
    _dedupCache[leaderOrder.OrderId.ToString()] = newPrice;

    foreach (var acc in rule.FollowerAccounts)                                // (4)
    {
        if (acc == null)                                                       // (5)
            continue;

        var fo = FindFollowerEntryOrder(acc, instrument);
        if (fo == null)
            continue;

        double currentPrice = fo.LimitPrice;
        if (tickSize > 0 && Math.Abs(newPrice - currentPrice) < tickSize)    // (3)
            continue;

        try
        {
            fo.LimitPrice = newPrice;
            acc.Change(new Order[] { fo });
            StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(acc.Name + ": entry drag error: " + ex.Message);
        }
    }
}
```

#### Part B — Gate C in `OnOrderUpdate`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 650–660 (replace)

**Current**:
```csharp
// Gate B: bracket drag detection -- divert to HandleBracketChange path
if (IsWorkingBracket(e.Order))
{
    if (e.Order.FromEntrySignal != null)
        PopulateOrderMap(e.Order.FromEntrySignal, e.Order.Account);
    HandleBracketChange(e.Order, matchedRule.Value);
    return;
}

// No bracket -- normal copy dispatch
DispatchCopy(e.Order, matchedRule.Value);
```

**Required**:
```csharp
// Gate B: bracket drag detection -- divert to HandleBracketChange path
if (IsWorkingBracket(e.Order))
{
    if (e.Order.FromEntrySignal != null)
        PopulateOrderMap(e.Order.FromEntrySignal, e.Order.Account);
    HandleBracketChange(e.Order, matchedRule.Value);
    return;
}

// Gate C (B62): entry drag detection -- same orderId + new LimitPrice = leader dragged.
// Fires when state is Accepted or Working (the two states that carry updated price post-drag).
// Only for Limit orders (Market orders have no LimitPrice to track).
// _dedupCache.TryGetValue: orderId was previously dispatched; compare stored price.
if (e.Order.OrderType == OrderType.Limit
    && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
{
    if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
        && Math.Abs(e.Order.LimitPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01))
    {
        HandleEntryChange(e.Order, matchedRule.Value);
        return;
    }
}

// No bracket, no drag -- normal copy dispatch
DispatchCopy(e.Order, matchedRule.Value);
```

---

## Section 4 — Data Flow Diagram

```
OnOrderUpdate(e)
  |
  +-- Pre-gate: TryFirePositionState(e)
  |             EvictDedup(orderId, state)    <-- B62 NEW (Change 5)
  |
  +-- Gate 1:  _isCopyEnabled?               -- false -> return
  +-- Gate 2:  rule matched?                 -- no    -> return
  +-- Gate 2.5: rule.Enabled?                -- false -> return
  |
  +-- Mirror relay (if Mirror mode)
  |
  +-- Cancelled state: CancelOneAccount      -> return
  +-- Leader-flat: TryDispatchLeaderFlat     -> return (if flat)
  |
  +-- Gate B: IsWorkingBracket?
  |           YES -> HandleBracketChange     -> return
  |
  +-- Gate C (B62 NEW):
  |     e.Order.OrderType == Limit?
  |     state == Accepted or Working?
  |     _dedupCache has orderId?
  |     |price delta| >= tickSize?
  |           YES -> HandleEntryChange       -> return
  |
  +-- DispatchCopy(order, rule)
        |
        +-- Gate 0.5: IsExitSignalName?      -- true -> return
        +-- Gate 3:   IsDispatchTriggerState? -- false -> return
        +-- Gate 4:   Market or Limit?        -- false -> return
        +-- Gate 5:   IsDedup(orderId, limitPrice)?  -- true -> return (B62: price param added)
        |
        +-- Build signal, scale per follower, SubmitOrder
```

---

## Section 5 — `_dedupCache` Semantic Change: `long` → `double`

### Before B62 (timestamp semantics)

```
_dedupCache: ConcurrentDictionary<string, long>
             key   = orderId
             value = DateTime.UtcNow.Ticks at dispatch time
```

Purpose: prevent the same orderId from being dispatched twice. Entries expire after 10 seconds
via a foreach pruning loop inside `IsDedup`. After expiry, a repeat `Working` state event (e.g.
from a leader drag) would re-enter `TryAdd`, succeed (key was removed), and dispatch a second
follower order — the phantom duplicate bug.

### After B62 (price semantics)

```
_dedupCache: ConcurrentDictionary<string, double>
             key   = orderId
             value = last dispatched (or updated) LimitPrice
```

Purpose (dual):
1. **Dedup** — same orderId + same price = `TryAdd` fails → true duplicate, skip dispatch.
2. **Drag detection** — same orderId + different price = Gate C fires `HandleEntryChange`.

Entries are no longer time-expired. They are evicted by `EvictDedup` when the order reaches a
terminal state (`Filled`, `Cancelled`, `Rejected`). This is semantically correct: an orderId
should remain in cache for exactly as long as the order is live, regardless of wall-clock time.

**Compatibility with market orders**: `order.LimitPrice` is `0.0` for market orders (NT8 default
for non-limit types). Gate C only fires for `OrderType.Limit`, so market orders are never
evaluated in the price-delta comparison. Storing `0.0` for market orders in `_dedupCache` is
correct and harmless.

---

## Section 6 — `EvictDedup` Lifecycle

### What `EvictDedup` guards against

Without eviction, a `ConcurrentDictionary` entry for an `orderId` would persist indefinitely
after the order is done. NT8 does reuse `orderId` integers across sessions (or under certain
conditions). A stale cache entry with an old price could:
- Block a legitimate new order (same `orderId` integer, `TryAdd` fails) — phantom dedup.
- Cause Gate C to fire `HandleEntryChange` on a brand new order whose `orderId` collides with
  an old evicted one whose price happened to differ — phantom drag sync.

### When `EvictDedup` is called

```
OnOrderUpdate (pre-gate, before Gate 1)
  -> EvictDedup(orderId, state)
       if state == Filled   -> _dedupCache.TryRemove(orderId)
       if state == Cancelled -> _dedupCache.TryRemove(orderId)
       if state == Rejected  -> _dedupCache.TryRemove(orderId)
       else                  -> no-op (fast return)
```

Pre-gate placement (before `_isCopyEnabled` check) ensures eviction fires even when the user
toggles copy off at fill time. This closes the permanent-blocking edge case.

### Why `EvictDedup` replaces the 10-second pruning loop

The old pruning loop in `IsDedup` had two problems:
1. **Wrong semantics**: it expired by time, not by order lifecycle.
2. **Performance**: iterating `_dedupCache.Keys` on every `OnOrderUpdate` call is O(n) and
   allocates a snapshot of the keys on each hot-path invocation.

`EvictDedup` is O(1) (`TryRemove` on known key) and fires exactly once per order terminal event.

---

## Section 7 — Test Specifications (T_B62_01 through T_B62_05)

**File**: `src/PropTraderTools/CopyEngineTests.cs` (new file `B62Tests.cs` acceptable)
**Framework**: xUnit [Fact] only. No NUnit, no MSTest.
**Access note**: `IsDedup` is `private` — access via reflection. `EvictDedup` is `internal` —
accessible from test assembly via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`
(already expected to be configured for prior test blocks).

---

### T_B62_01 — `IsDedup_FirstCall_ReturnsFalse`

```
Arrange: Construct CopyEngine (or testable wrapper).
         orderId = "ord-001", limitPrice = 7751.0
Act:     Call IsDedup("ord-001", 7751.0) via reflection.
Assert:  Returns false.
         (_dedupCache now contains "ord-001" -> 7751.0)
```

**What it verifies**: `TryAdd` on a fresh orderId succeeds; method returns false (not a dup).

---

### T_B62_02 — `IsDedup_SecondCallSamePrice_ReturnsTrue`

```
Arrange: Call IsDedup("ord-002", 7751.0) once (seeds cache).
Act:     Call IsDedup("ord-002", 7751.0) a second time.
Assert:  Returns true.
```

**What it verifies**: `TryAdd` on an existing orderId fails; method returns true (dup blocked).

---

### T_B62_03 — `EvictDedup_FilledState_RemovesEntry`

```
Arrange: Call IsDedup("ord-003", 7751.0) to seed cache.
         Verify IsDedup returns false (entry added).
Act:     engine.EvictDedup("ord-003", OrderState.Filled)
Assert:  IsDedup("ord-003", 7751.0) returns false.
         (Entry was removed; TryAdd succeeds again.)
```

**What it verifies**: Filled state triggers eviction; orderId is unlocked for re-use.

---

### T_B62_04 — `EvictDedup_WorkingState_DoesNotRemove`

```
Arrange: Call IsDedup("ord-004", 7751.0) to seed cache.
Act:     engine.EvictDedup("ord-004", OrderState.Working)
Assert:  IsDedup("ord-004", 7751.0) returns true.
         (Entry is still present; Working is not a terminal state.)
```

**What it verifies**: Non-terminal states are ignored; the early-return guard works.

---

### T_B62_05 — `EvictDedup_CancelledState_RemovesEntry`

```
Arrange: Call IsDedup("ord-005", 7751.0) to seed cache.
Act:     engine.EvictDedup("ord-005", OrderState.Cancelled)
Assert:  IsDedup("ord-005", 7751.0) returns false.
         (Entry was removed; TryAdd succeeds again.)
```

**What it verifies**: Cancelled state triggers eviction; mirrors T_B62_03 for the Cancelled path.

---

## Section 8 — Jane Street Compliance Table

| Rule | Requirement | B62 Status |
|------|-------------|------------|
| JS-021 | No `lock()` — all shared state via lock-free primitives | PASS — `_dedupCache` is `ConcurrentDictionary`. No `lock()` added in any new method. |
| JS-001 | No `throw new XxxException` in hot-path methods | PASS — `HandleEntryChange` wraps `acc.Change()` in `try/catch`. No `throw` propagates. |
| JS-002 | No `return null` contract violations — nullable return types must be null-guarded at call site | PASS — `FindFollowerEntryOrder` returns `Order?`. Call site in `HandleEntryChange` null-guards with `if (fo == null) continue;`. |
| CYC <= 8 | All new methods must have cyclomatic complexity <= 8 | PASS — `HandleEntryChange` CYC=5, `IsDedup` CYC=2, `EvictDedup` CYC=2, Gate C inline CYC=2. All within limit. |
| ASCII-only | No Unicode characters in new string literals | PASS — All new string literals use ASCII only. `"->"` uses hyphen-minus (0x2D) and `">"` (0x3E), not Unicode arrows. |
| xUnit only | All tests use `[Fact]` (no NUnit, no MSTest) | PASS — All 5 tests T_B62_01 through T_B62_05 use xUnit `[Fact]`. |
| JS-025 | ConcurrentDictionary for shared mutable state | PASS — `_dedupCache` type change preserves `ConcurrentDictionary`. `TryAdd`, `TryRemove`, `TryGetValue` are all lock-free. |
| NT8-003 | No `volatile double` fields | PASS — `_dedupCache` value type `double` is inside `ConcurrentDictionary`, not a standalone `volatile double` field. No violation. |
| DateTime.Now ban | Use `DateTime.UtcNow` (or avoid entirely) | PASS — `IsDedup` no longer uses `DateTime.UtcNow.Ticks` (timestamp eviction removed entirely). No new `DateTime` usage. |

---

## Section 9 — Deferred Items (Carry-Forward from B59-LaneA)

The following items from `docs/brain/B59-LaneA/06-deferred-backlog.md` are carried forward.
None are closed by B62.

### DW-B60-01 — Leader manual close does not close follower position

**Priority**: P1
**Original target**: B60
**Current status**: OPEN — not addressed in B62

**Description**: When the leader closes via the Positions tab Close button (NT8 order
`Name="Close"`), Gate 0.5 correctly blocks phantom copy. However, after the leader goes Flat,
the follower position remains open. The `Flatten(Account leader, Instrument instrument)` method
at `CopyEngine.cs:1135` already exists and fans out `PTT-Flatten` market orders. The wire-up
(detect `leader hasPos -> false` inside the copy-enabled, rule-matched path and call `Flatten`)
was deferred from B59 review. B60-LaneA confirmed it was implemented (commit fac65246 area) via
`TryDispatchLeaderFlat`. Verify status in B62 review: if the method is confirmed live and tested,
this item can be closed.

### DW-B59-02 — `IsExitSignalName` uses exact `"Rev"` match instead of prefix

**Priority**: P1
**Original target**: B60
**Current status**: OPEN — not addressed in B62

**Description**: As-built uses `name == "Rev"` (exact equality). Orders named `"Reversal"`,
`"RevLong"`, or `"RevShort"` pass Gate 0.5. Action: confirm actual NT8 reversal order names
against `NT8_FULL_REFERENCE.md` and live NT8 test logs. Widen to `name.StartsWith("Rev",
StringComparison.Ordinal)` if NT8 uses longer names. Add test cases for each variant.

### DW-B58-01 — `SnapshotTargetsPublic` hardcoded order-name prefixes

**Priority**: P2
**Status**: OPEN — future. B62 does not touch `SnapshotTargetsPublic`.

### DW-B58-02 — `GlobalBe` non-atomic lazy init

**Priority**: P2
**Status**: OPEN — future. No non-UI-thread caller introduced in B62.

### DW-B58-03 — `RelayBe` does not forward `OcoGroup`

**Priority**: P2
**Status**: OPEN — future. No BE changes in B62.

### DW-B54-01 — ATM auto-inject

**Priority**: P1
**Status**: OPEN — blocked. `AtmStrategyCreate()` is `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. No change in B62.

### PRE-EXISTING-01 — Non-ASCII at CopyEngine.cs lines 395, 496

**Priority**: P2
**Status**: OPEN — pre-existing. B62 does not touch these lines.

### PRE-EXISTING-02 — Non-ASCII at CopyEngine.cs lines 1256, 1257

**Priority**: P2
**Status**: OPEN — pre-existing. B62 does not touch these lines.

### PRE-EXISTING-03 — `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B62.

---

## Section 10 — Out of Scope

The following are explicitly NOT addressed by B62-LaneA:

1. **Bracket order drag for followers with no `FromEntrySignal`**: `FindFollowerBracketOrder`
   (B10) and `HandleBracketChange` (B10) handle bracket drag. B62 does not modify that path.

2. **Market order drag**: NT8 does not support dragging market orders. Gate C is limited to
   `OrderType.Limit`. No market order path is changed.

3. **Stop-limit entry drag**: `OrderType.StopLimit` is not handled by Gate C. `FindFollowerEntryOrder`
   matches `OrderType.Limit` only. If stop-limit entries are ever added, a separate deferred item
   will be needed.

4. **ATM strategy entry drag**: `AtmStrategyCreate()` is `StrategyBase`-only (DW-B54-01). B62
   has no ATM path.

5. **`IsExitSignalName` prefix fix** (DW-B59-02): Not in B62 scope. The exact `"Rev"` vs
   `StartsWith("Rev")` question is a separate B60/B63 item.

6. **Leader manual close propagation** (DW-B60-01): `TryDispatchLeaderFlat` already wired at
   line 646. B62 does not modify that block. Verification of its live status is a B63 review item.

7. **UI components (`TradeCopierPanel`, `TradeCopierWindow`)**: No UI changes in B62. Drag sync
   confirmation is reported via the existing `StatusUpdate` event.

8. **Test infrastructure changes**: B62 adds 5 new `[Fact]` tests. It does not add test projects,
   modify test runner config, or touch existing tests.

9. **Dedup cache serialization / persistence**: `_dedupCache` is in-memory only. No serialization
   is added. Cache is rebuilt from scratch on add-on restart.

10. **Multiple simultaneous drags**: B62 handles the single-leader, single-instrument case. If a
    leader has two open entries on the same instrument and drags both simultaneously, the
    `orderId` uniqueness in NT8 ensures each drag fires its own `OnOrderUpdate` sequence
    independently. No special multi-drag handling is required.

---

PLAN_COMPLETE
