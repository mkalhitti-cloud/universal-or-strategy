# B62-LaneA — Tickets
# Live Entry Drag Sync + Price-Keyed Dedup Fix

**Block**: B62-LaneA
**Phase**: 3 (Ticket Generation)
**Written by**: ptt-architect
**Date**: 2026-08-11
**Input plan**: `docs/brain/B62-LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Input review**: `docs/brain/B62-LaneA/02-plan-review.md` (REVIEW_PASS, 36/36)
**Source reads**: `CopyEngine.cs` lines 108-120, 600-665, 750-775, 860-935, 1448-1466

---

## Ticket 1 of 1

---

## A. Ticket ID and Summary

**ID**: B62-T1
**Block**: B62-LaneA
**Title**: Live entry drag sync + price-keyed dedup fix
**File (primary)**: `src/PropTraderTools/CopyEngine.cs`
**Test file (new)**: `src/PropTraderTools/Tests/B62Tests.cs`

**Summary**: Replace the time-based dedup cache with a price-keyed cache, add terminal-state
eviction, and wire a new Gate C + `HandleEntryChange` path so that leader limit-entry drags
are propagated to all follower working `PTT-Copy` orders via `acc.Change()`.

---

## B. Spec Requirement IDs

- **DW-B62-01**: Live entry drag sync — leader limit-entry drag must propagate to follower
  working PTT-Copy orders within the same `OnOrderUpdate` event chain.

---

## C. Pre-condition Check

Before writing any code, the engineer MUST verify all of the following from the live source:

| Check | Location | Expected |
|-------|----------|----------|
| `_dedupCache` field type | `CopyEngine.cs` line 112 | `ConcurrentDictionary<string, long>` |
| `IsDedup` signature | `CopyEngine.cs` line 1448 | `private bool IsDedup(string orderId)` — single arg |
| Gate C present? | `CopyEngine.cs` lines 650-660 | MUST NOT EXIST (no Gate C comment) |
| `HandleEntryChange` present? | Search in file | MUST NOT EXIST |
| `FindFollowerEntryOrder` present? | Search in file | MUST NOT EXIST |

**Verified at ticket-write time** (plan reviewer source reads 2026-08-11):
- Line 112: `private readonly ConcurrentDictionary<string, long> _dedupCache = ...` — CONFIRMED
- Line 1448: `private bool IsDedup(string orderId)` — single-arg — CONFIRMED
- Lines 659-660: `// No bracket -- normal copy dispatch` / `DispatchCopy(...)` — no Gate C — CONFIRMED
- `HandleEntryChange`: absent — CONFIRMED
- `FindFollowerEntryOrder`: absent — CONFIRMED

---

## D. All 7 Changes (Exact — implement in this dependency order)

### Change 1 — `_dedupCache` field type: `long` → `double` (line 112)

**Why**: Stores last dispatched `LimitPrice` (double) instead of `DateTime.UtcNow.Ticks` (long).
This transforms the cache from a time-expiry store into a price-keyed map, enabling Gate C to
detect "same orderId + different price = leader dragged" without iterating the full cache.

**Before** (`CopyEngine.cs` line 112):
```csharp
private readonly ConcurrentDictionary<string, long> _dedupCache = new ConcurrentDictionary<string, long>(); // JS-025
```

**After**:
```csharp
// B62: value changed from long (timestamp) to double (last dispatched LimitPrice).
// Enables drag detection: same orderId + different price = leader dragged.
// JS-025: ConcurrentDictionary is lock-free.
private readonly ConcurrentDictionary<string, double> _dedupCache = new ConcurrentDictionary<string, double>(); // JS-025
```

---

### Change 2 — Replace `IsDedup` body (lines 1448–1465)

**Why**: Eliminates the 10-second time-based expiry loop (O(n) allocation on every hot-path
invocation) and switches to `TryAdd(orderId, limitPrice)` semantics. The foreach pruning loop
and `DateTime.UtcNow.Ticks` usage are deleted entirely. Terminal-state eviction is now handled
by `EvictDedup` (Change 4). CYC drops from 7 to 2.

**New signature** (signature changes — adds `double limitPrice` parameter):
```csharp
private bool IsDedup(string orderId, double limitPrice)
```

**Accessibility note**: `IsDedup` remains `private`. Tests access it via reflection (see Section G).

**Before** (full current body, lines 1448–1465):
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

**After** (full replacement body, CYC=2):
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

---

### Change 3 — Update `IsDedup` call site in `DispatchCopy` Gate 5 (line 763)

**Why**: The call site must pass `order.LimitPrice` to match the new two-argument signature.
For market orders, `order.LimitPrice` is `0.0` — this is safe because Gate C only fires for
`OrderType.Limit`, so market orders are never evaluated in Gate C's price-delta comparison.

**Before** (`CopyEngine.cs` line 763):
```csharp
if (IsDedup(order.OrderId.ToString()))
    return;
```

**After**:
```csharp
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```

---

### Change 4 — Add `EvictDedup` method (insert after new `IsDedup`)

**Where to insert**: Immediately after the new `IsDedup` method (after line 1465 post-edit,
i.e., the closing `}` of the new `IsDedup`).

**Why**: Replaces the time-expiry loop. `EvictDedup` fires O(1) on each terminal state event
via `TryRemove`. This prevents permanently-blocked orderIds when copy is toggled off at fill
time (the pre-gate placement in Change 5 ensures this runs unconditionally).

```csharp
// B62: evict dedup entry when order reaches terminal state (Filled/Cancelled/Rejected).
// Called unconditionally from OnOrderUpdate pre-gate, after TryFirePositionState.
// Ensures evicted orderId can be re-used for the next fresh order on the same instrument.
// CYC=2: terminal-state guard (1) + TryRemove (no branch).
// JS-025: ConcurrentDictionary.TryRemove is lock-free.
internal void EvictDedup(string orderId, OrderState state)
{
    if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected)
        return;

    _dedupCache.TryRemove(orderId, out _);
}
```

**Accessibility**: `internal` — required for direct call from T_B62_03, T_B62_04, T_B62_05
without reflection. Assumes `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` is
already configured (established in prior blocks).

---

### Change 5 — Wire `EvictDedup` in `OnOrderUpdate` pre-gate (after line 603)

**Where to insert**: One line after `TryFirePositionState(e)` (line 603), BEFORE
`// Gate 1: enabled check` (line 605). Must be in the pre-gate block.

**Why pre-gate (before Gate 1)**: If copy is disabled when an order fills, `EvictDedup` must
still fire. Without pre-gate placement, a filled `orderId` would stay in `_dedupCache`
permanently when the user toggles copy off at fill time, permanently blocking the orderId.

**Before** (`CopyEngine.cs` lines 602–607):
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

**After**:
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);
// B62: evict dedup on terminal states so orderId is not permanently blocked.
EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

---

### Change 6 — Add `FindFollowerEntryOrder` method (after `FindFollowerBracketOrder`)

**Where to insert**: Immediately after `FindFollowerBracketOrder` (whose closing `}` is at
line 931). Insert the new method between lines 931 and 933.

**Why**: Mirror of `FindFollowerBracketOrder` — locates the follower's working `PTT-Copy` limit
entry order for an instrument. Required by `HandleEntryChange` (Change 7) to call `acc.Change()`.

```csharp
// B62: find the follower's working PTT-Copy limit entry order for the instrument.
// Mirror of FindFollowerBracketOrder -- matches by Name=="PTT-Copy" + Limit + Working.
// Used by HandleEntryChange to locate the order to acc.Change().
// CYC=3: foreach (1), instrument guard (2), state+name+type compound guard (3).
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

**Notes**:
- `static` because it takes all required context as parameters (no `this` access).
- `"PTT-Copy"` matches the submission naming convention in `DispatchCopy`.
- Instrument comparison is object-reference equality (NT8 `Instrument` object identity).
- Returns `null` when not found — callers null-guard with `if (fo == null) continue;`.

---

### Change 7 — Add `HandleEntryChange` + Gate C in `OnOrderUpdate`

#### Part A — `HandleEntryChange` method

**Where to insert**: Immediately after `HandleBracketChange` (whose closing `}` is at line 906).
Insert between lines 906 and the comment starting at 908.

**Why**: Receives leader's updated `LimitPrice` from Gate C, tick-rounds it, updates the dedup
cache to the new price, then loops all follower accounts calling `acc.Change()` on each
follower's working `PTT-Copy` limit order.

**Reviewer correction (from 02-plan-review.md Section 7.5)**: The plan CYC comment says `CYC=5`
but the actual decision count is 6. The `if (fo == null) continue;` branch was unlabeled in
the plan annotation. Engineer MUST:
1. Set the CYC comment to `CYC=6`.
2. Number all six branch labels sequentially in code-flow order: (1)–(6).

```csharp
// B62: sync a leader entry drag to all follower working PTT-Copy limit orders.
// Mirror of HandleBracketChange -- tick-rounds price, calls acc.Change() per follower.
// Triggered by Gate C when leader's entry orderId is already in dedup cache but price changed.
// CYC=6: instr null (1), tickSize ternary (2), foreach acc (3), acc null (4), fo null (5), price delta guard (6).
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

    foreach (var acc in rule.FollowerAccounts)                                // (3)
    {
        if (acc == null)                                                       // (4)
            continue;

        var fo = FindFollowerEntryOrder(acc, instrument);
        if (fo == null)                                                        // (5)
            continue;

        double currentPrice = fo.LimitPrice;
        if (tickSize > 0 && Math.Abs(newPrice - currentPrice) < tickSize)    // (6)
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

**Where to replace**: Lines 650–660 (the Gate B block through `DispatchCopy` call).

**Before** (`CopyEngine.cs` lines 650–660):
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

**After**:
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

## E. Method Signatures (Engineer Contract)

```csharp
// Change 2 — replaces single-arg overload entirely
private bool IsDedup(string orderId, double limitPrice)           // CYC=2

// Change 4 — new method
internal void EvictDedup(string orderId, OrderState state)        // CYC=2

// Change 6 — new method
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)  // CYC=3

// Change 7A — new method
private void HandleEntryChange(Order leaderOrder, CopyRule rule)   // CYC=6
```

All method names, parameter names, return types, and access modifiers are EXACT. Do not alter.

---

## F. 7-Scan Checklist (MANDATORY — Engineer Contract)

Engineer MUST run each scan and report zero findings before closing the ticket.
Record scan output (pass/count) in `ticket-1-completion.md`.

| Scan | Command | Required Result |
|------|---------|-----------------|
| SCAN-01: ASCII | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 NEW occurrences in B62 changes (pre-existing lines 395, 496, 1256, 1257 are exempt) |
| SCAN-02: Build | `dotnet build src/PropTraderTools/ --no-restore` | 0 errors, 0 warnings |
| SCAN-03: Tests | `dotnet test src/PropTraderTools/ --no-build` | All pass — 5 new T_B62_xx tests + all prior |
| SCAN-04: Lock | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 results in new B62 code |
| SCAN-05: Complexity | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | All new methods ≤ 8 |
| SCAN-06: Throw | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new `throw new` in B62 changes |
| SCAN-07: Null return | Manual review of `FindFollowerEntryOrder` return type and all call sites | Return type is `Order?`; all callers null-guard with `if (fo == null) continue;` |

---

## G. 5 Test Specifications

**File**: `src/PropTraderTools/Tests/B62Tests.cs` (new file — create if absent)
**Framework**: xUnit `[Fact]` ONLY. No NUnit. No MSTest.
**Access**: `IsDedup` is `private` — use reflection (`BindingFlags.NonPublic | BindingFlags.Instance`).
`EvictDedup` is `internal` — direct call from test assembly (requires
`[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` already configured in production assembly).

---

### T_B62_01 — `IsDedup_FirstCall_ReturnsFalse`

```
[Fact]
Arrange:
  - Construct a CopyEngine instance (use minimal ctor or test-accessible factory).
  - orderId = "ord-001", limitPrice = 7751.0

Act:
  - Invoke IsDedup("ord-001", 7751.0) via reflection.

Assert:
  - Returns false.
  - (_dedupCache now contains "ord-001" -> 7751.0)
```

**What it verifies**: `TryAdd` on a fresh orderId succeeds; method correctly reports "not a dup".

---

### T_B62_02 — `IsDedup_SecondCallSamePrice_ReturnsTrue`

```
[Fact]
Arrange:
  - Construct CopyEngine.
  - Call IsDedup("ord-002", 7751.0) once to seed cache (returns false).

Act:
  - Call IsDedup("ord-002", 7751.0) a second time.

Assert:
  - Returns true.
```

**What it verifies**: `TryAdd` on an existing orderId fails; method correctly reports "dup — skip".

---

### T_B62_03 — `EvictDedup_FilledState_RemovesEntry`

```
[Fact]
Arrange:
  - Construct CopyEngine.
  - Call IsDedup("ord-003", 7751.0) to seed cache (verify returns false).

Act:
  - engine.EvictDedup("ord-003", OrderState.Filled)

Assert:
  - IsDedup("ord-003", 7751.0) returns false.
    (Entry was removed; TryAdd succeeds again — orderId is unlocked for re-use.)
```

**What it verifies**: `Filled` is a terminal state — triggers eviction; orderId can be re-placed.

---

### T_B62_04 — `EvictDedup_WorkingState_DoesNotRemove`

```
[Fact]
Arrange:
  - Construct CopyEngine.
  - Call IsDedup("ord-004", 7751.0) to seed cache.

Act:
  - engine.EvictDedup("ord-004", OrderState.Working)

Assert:
  - IsDedup("ord-004", 7751.0) returns true.
    (Entry is still present; Working is not a terminal state — early-return guard works.)
```

**What it verifies**: Non-terminal states are no-ops; the guard `!=Filled && !=Cancelled && !=Rejected` holds.

---

### T_B62_05 — `EvictDedup_CancelledState_RemovesEntry`

```
[Fact]
Arrange:
  - Construct CopyEngine.
  - Call IsDedup("ord-005", 7751.0) to seed cache.

Act:
  - engine.EvictDedup("ord-005", OrderState.Cancelled)

Assert:
  - IsDedup("ord-005", 7751.0) returns false.
    (Entry was removed; mirrors T_B62_03 for the Cancelled path.)
```

**What it verifies**: `Cancelled` terminal state triggers eviction; symmetric with `Filled` case.

---

## H. Acceptance Criteria

The ticket is complete when ALL of the following are true:

- [ ] All 7 changes implemented in dependency order (1 → 2 → 3 → 4 → 5 → 6 → 7)
- [ ] Change 2 CYC comment corrected to `CYC=6` in `HandleEntryChange` (reviewer note honored)
- [ ] Change 7A branch labels renumbered (1)–(6) sequentially in code-flow order (reviewer note honored)
- [ ] All 5 T_B62_xx tests written in `src/PropTraderTools/Tests/B62Tests.cs`
- [ ] All 5 T_B62_xx tests PASS
- [ ] Zero regressions in prior test suites
- [ ] All 7 scans (SCAN-01 through SCAN-07) pass with zero findings
- [ ] `git commit` hash recorded in `docs/brain/B62-LaneA/ticket-1-completion.md`
- [ ] Completion report format: `feat(ptt): B62 live entry drag sync [5 tests]`

---

## I. Out of Scope

The following are explicitly NOT part of this ticket:

1. **Market order drag**: NT8 does not support dragging market orders. Gate C is `OrderType.Limit` only.
2. **Stop-limit entry drag**: `OrderType.StopLimit` not handled by Gate C or `FindFollowerEntryOrder`.
3. **Bracket order drag**: Handled by Gate B / `HandleBracketChange` (B10). Not modified here.
4. **ATM strategy entry drag**: `AtmStrategyCreate()` is `StrategyBase`-only (DW-B54-01, blocked).
5. **`IsExitSignalName` prefix fix** (DW-B59-02): Not in B62 scope.
6. **Leader manual close propagation** (DW-B60-01): `TryDispatchLeaderFlat` already wired at line 646. Not modified.
7. **UI components** (`TradeCopierPanel.cs`, `TradeCopierWindow.cs`): No UI changes in B62.
8. **OCA/OCO group handling**: No group changes.
9. **Test infrastructure**: No new test projects, no test runner config changes.
10. **Dedup cache serialization/persistence**: In-memory only; rebuilt on add-on restart.

---

TICKETS_COMPLETE
