# B62-LaneA Orchestrator Prompt

## Block: B62 — Live entry drag sync + price-keyed dedup fix

**Run AFTER B63 is merged.** B63 must be deployed (F5 green) before testing B62.

**DW item**: DW-B62-01 (live confirmed 2026-08-11, NinjaTrader Grid 07-25/07-26 PM):
When the leader drags a working limit entry order in Chart Trader or SuperDOM, NT8 fires
`OnOrderUpdate` with the SAME orderId but a new `LimitPrice` at `Accepted` then `Working`
states. PTT currently has NO path to call `acc.Change()` on the follower's working `PTT-Copy`
order. The follower's entry stays at the old price.

Additionally, the existing `IsDedup` uses a 10-second time-based expiry. If the leader drags
after 10s, the dedup cache has evicted that orderId and `DispatchCopy` fires again, spawning
a second `PTT-Copy` entry order on the follower instead of moving the existing one.

**Goal: live drag sync identical to "Affordable Indicators" behaviour** — drag the leader entry
in Chart Trader or SuperDOM, follower entry moves instantly to the same price via `acc.Change()`.

---

## NT8 API CONFIRMATION (from NT8_FULL_REFERENCE.md)

**Drag event sequence** (confirmed from live logs 2026-08-11):
```
Order='X/Sim101' Name='Entry' New state='Change submitted'  ← NT8 auto-chase fires
Order='X/Sim101' Name='Entry' New state='Accepted'  LimitPrice=NEW_PRICE
Order='X/Sim101' Name='Entry' New state='Working'   LimitPrice=NEW_PRICE
```
Same orderId `X` across all three. PTT must detect "same orderId, different LimitPrice".

**`Account.Change(Order[])`** (NT8_FULL_REFERENCE.md line 328–329):
> "Change() — Changes specified order(s) on the account"
The `acc.Change(new Order[] { fo })` pattern is ALREADY used in `SyncFollowerBracket` (line 865).
B62 replicates this exact call for entry orders.

**`Order.LimitPriceChanged`** (NT8_FULL_REFERENCE.md line 839–840):
> "LimitPriceChanged — new limit price of an order. Used with Account.Change()"
Pattern: `fo.LimitPrice = newPrice; acc.Change(new Order[] { fo });`

---

## PIPELINE CHAIN (all 7 phases mandatory — none skippable — none combinable)

```
Ph1  ptt-architect       -> docs/brain/B62-LaneA/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B62-LaneA/02-plan-review.md       (gate: REVIEW_PASS)
Ph3  ptt-architect       -> docs/brain/B62-LaneA/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B62-LaneA/04-ticket-review.md   (gate: TICKET_REVIEW_PASS)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B62-LaneA/ticket-1-completion.md
Ph4b ptt-verifier        -> docs/brain/B62-LaneA/ticket-1-verification.md (gate: VERIFY_PASS)
Ph5  ptt-plan-reviewer   -> docs/brain/B62-LaneA/05-final-review.md
                         -> docs/brain/B62-LaneA/06-deferred-backlog.md
```

---

## EXACT CHANGES REQUIRED (7 changes total)

### Change 1 — `_dedupCache` field type: `long` → `double` (stores LimitPrice, not timestamp)

**Current** (line 112):
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

### Change 2 — Replace `IsDedup` body (price-keyed, no time expiry)

**Current** `IsDedup` body (lines 1442–1459):
```csharp
private bool IsDedup(string orderId)
{
    long now = DateTime.UtcNow.Ticks;
    long expiry = TimeSpan.FromSeconds(10).Ticks;
    foreach (var key in _dedupCache.Keys)
    {
        if (_dedupCache.TryGetValue(key, out long storedTicks) && now - storedTicks > expiry)
            _dedupCache.TryRemove(key, out _);
    }
    if (!_dedupCache.TryAdd(orderId, now))
        return true;
    return false;
}
```

**Required replacement**:
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

### Change 3 — Update `IsDedup` call site in `DispatchCopy` Gate 5

**Current** (line 763):
```csharp
if (IsDedup(order.OrderId.ToString()))
    return;
```

**Required**:
```csharp
// B62: pass limitPrice as second arg (price-keyed dedup).
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```

### Change 4 — Add `EvictDedup` method (evict on terminal state)

Add immediately after the new `IsDedup` method:
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

### Change 5 — Wire `EvictDedup` in `OnOrderUpdate` pre-gate

**Current** (lines 602–607):
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

**Required** (insert one line after TryFirePositionState):
```csharp
// Pre-gate: fire position state unconditionally (even when copy disabled)
TryFirePositionState(e);
// B62: evict dedup on terminal states so orderId is not permanently blocked.
EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);

// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

### Change 6 — Add `FindFollowerEntryOrder` (mirror of `FindFollowerBracketOrder`)

Add immediately after `FindFollowerBracketOrder` (currently ends at line 925):
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

### Change 7 — Add `HandleEntryChange` + Gate C in `OnOrderUpdate`

**Add `HandleEntryChange` method** immediately after `HandleBracketChange` (currently ends at line 900):
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

**Add Gate C in `OnOrderUpdate`** — replace the current `DispatchCopy` call block (lines 650–660):

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

## TESTS REQUIRED (5 new [Fact] tests, tag T_B62_01 through T_B62_05)

**T_B62_01** — `IsDedup_FirstCall_ReturnsFalse`
- Arrange: new `CopyEngine`; orderId "ord-001"; limitPrice 7751.0.
- Act: call `IsDedup` via reflection with ("ord-001", 7751.0).
- Assert: returns `false`.

**T_B62_02** — `IsDedup_SecondCallSamePrice_ReturnsTrue`
- Arrange: call `IsDedup("ord-002", 7751.0)` once.
- Act: call again with same args.
- Assert: returns `true`.

**T_B62_03** — `EvictDedup_FilledState_RemovesEntry`
- Arrange: `IsDedup("ord-003", 7751.0)` to seed cache.
- Act: `engine.EvictDedup("ord-003", OrderState.Filled)`.
- Assert: `IsDedup("ord-003", 7751.0)` returns `false` (evicted, fresh again).

**T_B62_04** — `EvictDedup_WorkingState_DoesNotRemove`
- Arrange: `IsDedup("ord-004", 7751.0)` to seed cache.
- Act: `engine.EvictDedup("ord-004", OrderState.Working)`.
- Assert: `IsDedup("ord-004", 7751.0)` returns `true` (still in cache).

**T_B62_05** — `EvictDedup_CancelledState_RemovesEntry`
- Arrange: `IsDedup("ord-005", 7751.0)` to seed cache.
- Act: `engine.EvictDedup("ord-005", OrderState.Cancelled)`.
- Assert: `IsDedup("ord-005", 7751.0)` returns `false` (evicted).

---

## JANE STREET / OKF COMPLIANCE

| Rule | Check |
|------|-------|
| JS-021 | No `lock()` — all state via `ConcurrentDictionary` |
| JS-001 | No `throw new` in `HandleEntryChange` — `try/catch` only |
| JS-002 | `FindFollowerEntryOrder` returns `Order?` (nullable), null-guarded at call site |
| CYC ≤ 8 | `HandleEntryChange` CYC=5, `IsDedup` CYC=2, `EvictDedup` CYC=2, Gate C CYC=2 |
| ASCII-only | No Unicode in new string literals |
| xUnit only | All 5 tests use `[Fact]` |

---

## BRAIN ARTIFACT CHECKLIST

```
docs/brain/B62-LaneA/02-architecture-plan.md      <- Ph1
docs/brain/B62-LaneA/02-plan-review.md            <- Ph2  (must end: REVIEW_PASS)
docs/brain/B62-LaneA/04-tickets.md                <- Ph3
docs/brain/B62-LaneA/04-ticket-review.md          <- Ph3.5 (must end: TICKET_REVIEW_PASS)
docs/brain/B62-LaneA/ticket-1-completion.md       <- Ph4a (must contain git commit hash)
docs/brain/B62-LaneA/ticket-1-verification.md     <- Ph4b (must end: VERIFY_PASS)
docs/brain/B62-LaneA/05-final-review.md           <- Ph5
docs/brain/B62-LaneA/06-deferred-backlog.md       <- Ph5
```

---

## WORKSPACE RULES

- SRC CODE BAN: ptt-architect and ptt-plan-reviewer MUST NOT edit any `.cs` file.
- ptt-engineer is the ONLY mode permitted to touch `.cs` files.
- Workspace: `C:\WSGTA\universal-or-strategy` (main branch only).
- After any `.cs` edit: run `powershell -File scripts\verify_links.ps1 -Fix`
  then commit: `git add src/PropTraderTools/ && git commit -m "fix(ptt): B62 -- ..."`
- NT8 API reference: grep `docs/standards/NT8_FULL_REFERENCE.md` before any NT8 API claim.
- Jane Street rules: JS-021 no lock(), JS-001 no throw in hot path, JS-002 no null return,
  CYC <= 8 per method, ASCII-only literals, xUnit [Fact] only.
