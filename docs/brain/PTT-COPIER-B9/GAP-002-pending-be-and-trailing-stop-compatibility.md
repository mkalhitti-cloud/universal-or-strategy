# GAP-002 — Pending BE + Trailing Stop Compatibility
**Raised**: 2026-07-09
**Status**: OPEN — specced, ready for B10 architect
**Affects**: BE button (Panel + Window), planned Tighten Stop
**Priority**: P1
**Depends on**: GAP-001d (Sim101 trailing stop verification) — informs cancel+replace path

---

## Two Features, One Document

These two features interact and must be specced together:

| Feature | Summary |
|---|---|
| **Pending BE** | Click BE before price reaches BE level — arms a price watcher — fires automatically when price gets there |
| **Trailing stop compatibility** | When BE fires (immediate or pending), if the account has a trailing stop, handle it correctly rather than silently breaking it |

---

## Feature 1: Pending BE

### User problem today

The user clicks BE. `MoveStopToBreakEven` fires immediately. If price has not yet
reached `entry + bufferTicks`, the stop is moved to a level that is currently
a losing stop — the trade would close at a loss if price retraces to that level
before moving further in favour.

The user wants to **arm** BE early — "I want this to fire the moment price gets there,
not manually watch for it."

### State machine

```
[INACTIVE] --click BE--> [ARMED] --price reaches target--> fire --> [INACTIVE]
                          |
                          +--click BE again--> [INACTIVE]  (disarm/cancel)
```

Three states on the engine, represented as a volatile int (JS-023):

```csharp
// CopyEngine.cs new members (B10 T-pending-be)
internal enum PendingBeState { Inactive = 0, Armed = 1 }
private volatile int   _pendingBeState   = 0;      // JS-023
private volatile double _pendingBeTarget  = 0.0;    // absolute price level to watch (JS-023 — double write is not atomic on 32-bit, but NT8 is 64-bit; use Interlocked if needed)
private volatile int   _pendingBeBuffer  = 2;       // bufferTicks saved at arm time
private volatile bool  _pendingBeLong    = true;    // direction: long=true waits for price >= target
```

### Arming logic (called from `BreakEven()` when position already open but price not at target)

```
Arm:
  pos = FindPosition(masterAccount, instrument)
  if flat → skip (no position to protect)
  targetPrice = pos.AveragePrice + direction * bufferTicks * tickSize
  currentPrice = GetLastPrice(instrument)   // see price source below
  if price already at/past target → fire immediately (current behaviour, no change)
  else → set _pendingBeTarget = targetPrice
          set _pendingBeBuffer = bufferTicks
          set _pendingBeLong   = (pos.MarketPosition == Long)
          set _pendingBeState  = Armed
          SubscribeToPrice(instrument)      // arm price watcher
          StatusUpdate("BE armed at " + targetPrice)
```

### Price source for pending BE

`AddOnBase` has no `MarketData` or `OnBarUpdate`. Options:

| Option | How | Frequency | Availability |
|---|---|---|---|
| **A — `Instrument.MarketData` subscription** | `NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.MarketDataUpdate += handler` | Tick-by-tick | Available in AddOn context |
| **B — `Account.AccountItemUpdate`** | Already wired (B7 live P&L). `AccountItem.LastPrice` or position UnrealizedPnL | Per price change for open positions | Already subscribed |
| **C — `AtrSizingEngine.OnBarUpdate` heartbeat** | Reuse bar-close event as a "check price" heartbeat | Bar-close (~1 min) | Too slow — BE should fire within seconds of price reaching level |

**Recommendation: Option A** — `Instrument.MarketData` subscription.

Subscribe when armed, unsubscribe when fired or disarmed. One subscription per
instrument per pending BE arm. Clean lifecycle, tick-precision.

```csharp
// Subscribe (called at arm time)
private NinjaTrader.Data.Instrument _pendingBeInstrument;
private void SubscribePendingBe(NinjaTrader.Cbi.Instrument instr)
{
    _pendingBeInstrument = NinjaTrader.Data.Instrument.GetInstrument(instr.FullName);
    if (_pendingBeInstrument != null)
        _pendingBeInstrument.MarketData.MarketDataUpdate += OnPendingBePriceTick;
}

private void UnsubscribePendingBe()
{
    if (_pendingBeInstrument != null)
    {
        _pendingBeInstrument.MarketData.MarketDataUpdate -= OnPendingBePriceTick;
        _pendingBeInstrument = null;
    }
}
```

### Price tick handler

```csharp
// CYC=4
private void OnPendingBePriceTick(object sender, NinjaTrader.Data.MarketDataEventArgs e)
{
    if ((PendingBeState)_pendingBeState != PendingBeState.Armed) return;  // guard (1)
    double price = e.Price;
    if (price <= 0) return;                                                // guard (2)

    bool triggered = _pendingBeLong
        ? price >= _pendingBeTarget                                        // branch (3)
        : price <= _pendingBeTarget;

    if (!triggered) return;                                                // guard (4)

    // Disarm first (atomic — prevent double-fire on rapid ticks)
    _pendingBeState = (int)PendingBeState.Inactive;
    UnsubscribePendingBe();

    // Fire BE on all accounts
    // Must dispatch to UI thread for acc.Change() calls
    var instrument = _pendingBeInstrument;  // captured before null
    Dispatcher.InvokeAsync(() =>
        BreakEven(instrument?.Instrument, _pendingBeBuffer));
    StatusUpdate?.Invoke("PTT-BE pending fired at " + price);
}
```

### `BreakEven()` modification

Current: always fires immediately.
New: check if price is already at/past target. If not → arm pending BE instead of firing.

```csharp
internal void BreakEven(Instrument instrument, int bufferTicks)
{
    // First: check if any account has an open position
    // If price already at/past target for all accounts → fire immediately (existing path)
    // If price not yet at target → arm pending BE (new path)
    // Decision is per-instrument (one pending BE per instrument at a time)
    ...
}
```

**Simpler alternative**: keep `BreakEven()` unchanged (always fires immediately).
Add a NEW separate `ArmPendingBe(instrument, bufferTicks)` method called by a
separate `[BE Arm]` button or by a long-press on the BE button.

**Recommendation: separate Arm path** — two distinct actions, two distinct buttons:
- `[BE]` — fire immediately (current behaviour, unchanged)
- `[BE ●]` — arm pending BE (new button, or toggle on `[BE]` button)

This avoids changing the immediate BE path and keeps each code path simple (CYC unchanged).

UI: the `[BE]` button becomes a toggle:
- First click → if price already at BE level: fire immediately
- First click → if price below BE level: arm pending (button turns amber `[BE ●]`)
- Second click while armed → disarm (button returns to inactive grey)

---

## Feature 2: Trailing Stop Compatibility

### The problem

`MoveStopToBreakEven` calls `acc.Change(new Order[] { order })` after setting
`order.StopPrice`. If `order` is a trailing stop (`TrailPrice > 0`), calling
`acc.Change()` with only `StopPrice` written has **undefined behaviour** — the
trailing mechanism may freeze at the new price. (See GAP-001.)

### Correct behaviour matrix

| Stop type | Current stop vs BE level | Correct action |
|---|---|---|
| **Fixed stop** | Below BE | Move to BE (current behaviour — correct) |
| **Trailing stop** | Already at or above BE level | **Skip** — trail has already done its job, no action needed |
| **Trailing stop** | Below BE level (trail hasn't caught up yet) | **Cancel + replace** with fixed StopMarket at BE price |

"At or above" means:
- Long: `order.StopPrice >= targetPrice`
- Short: `order.StopPrice <= targetPrice`

### Implementation

Replace the `acc.Change()` block in `MoveStopToBreakEven` with:

```csharp
// For each working stop order:
bool isTrailing = order.TrailPrice > 0;    // NT8 trailing stop has TrailPrice set

if (isTrailing)
{
    // Check if trail has already passed BE level
    bool alreadyAtBe = isLong
        ? order.StopPrice >= newStop
        : order.StopPrice <= newStop;

    if (alreadyAtBe)
    {
        StatusUpdate?.Invoke(acc.Name + ": trail already at/above BE -- skip");
        continue;
    }

    // Trail is below BE -- cancel it and place fixed stop at BE price
    try
    {
        acc.Cancel(new Order[] { order });
        acc.CreateOrder(
            instrument,
            isLong ? OrderAction.Sell : OrderAction.BuyToCover,
            OrderType.StopMarket,
            OrderEntry.Manual,
            TimeInForce.Day,
            order.Quantity,
            0,          // limitPrice -- not used for StopMarket
            newStop,    // stopPrice = BE level
            null,
            "PTT-BE-Stop",   // starts with PTT- (SCAN-05)
            DateTime.MaxValue,
            null
        );
        StatusUpdate?.Invoke(acc.Name + ": trailing stop cancelled + fixed BE stop placed at " + newStop);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-BE trail-replace error: " + ex.Message);
    }
}
else
{
    // Fixed stop -- existing acc.Change() path (unchanged)
    try
    {
        order.StopPrice = newStop;
        acc.Change(new Order[] { order });
        StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-BE error: " + ex.Message);
    }
}
```

### What happens to the trailing stop AFTER BE fires

After cancel+replace:
- The old trailing stop order is cancelled
- A new **fixed** StopMarket sits at BE price
- Price continues in the trader's favour → stop does NOT trail further
- This is **intentional and correct** — the user pressed BE, they set a hard floor

If the user wants the stop to trail again from BE level onwards, they must set a
new trailing stop ATM manually. This is out of scope for the BE button — the BE
button is a "set floor and forget" action.

The trailing stop is gone but the position is protected. This is the right trade-off.

---

## Interaction: Pending BE + Trailing Stop

When pending BE fires (price watcher triggers):
1. `BreakEven(instrument, bufferTicks)` is called from `OnPendingBePriceTick`
2. `MoveStopToBreakEven` runs on each account with the trailing-stop-aware logic above
3. If the trailing stop has already moved past BE by the time the watcher fires
   (possible — the trail might have run ahead) → skip (already protected)
4. If not → cancel+replace with fixed stop at BE price

This means pending BE is safe for trailing stop accounts — it either does the right
thing or does nothing if the trail already handled it.

---

## NT8 API Note: `order.TrailPrice`

In NT8, working trailing stop orders have:
- `order.OrderType == OrderType.StopMarket`
- `order.TrailPrice > 0` (the trailing offset in price units, not ticks)

To convert ticks to price units: `trailPrice = nTicks * instrument.MasterInstrument.TickSize`

The `TrailPrice` field is readable from working orders. It is 0 for fixed stop orders.
**This must be verified on Sim101 before implementation** (GAP-001d).

---

## CYC Impact

| Method | Before | After | Change |
|---|---|---|---|
| `MoveStopToBreakEven` | 5 | 7 | +2 (isTrailing branch + alreadyAtBe branch) |
| `OnPendingBePriceTick` | new | 4 | new method |
| `SubscribePendingBe` | new | 2 | new method |
| `UnsubscribePendingBe` | new | 2 | new method |
| `BreakEven` | 1 | 1 | unchanged (arm logic in new `ArmPendingBe`) |
| `ArmPendingBe` | new | 4 | new method |

All methods stay at CYC <= 8. ✅

---

## UI Changes Required

### Panel (`TradeCopierPanel.cs`)

BE button becomes a toggle with three visual states:

| State | Button appearance | What it shows |
|---|---|---|
| Inactive | `[BE]` grey | Ready to arm or fire |
| Armed (pending) | `[BE ●]` amber | Watching price — click to cancel |
| Just fired | Brief `[BE ✓]` green flash (200ms) → returns to grey | Confirmation |

Implementation: existing `_beBtn` + `_beArmedState volatile bool` (JS-023).
`OnBreakEven` checks current price vs target; if below → call `ArmPendingBe`; if at/past → call `BreakEven` (immediate).

### Window (`TradeCopierWindow.cs`)

Same toggle logic on per-rule `[BE]` button. Armed state shown per-rule (each rule
has its own `_beArmedState` since rules can have different instruments).

---

## Deferred Backlog Entries

| ID | Item | Priority | Target |
|---|---|---|---|
| DW-B10-GAP-002a | Pending BE price watcher (`ArmPendingBe` + `OnPendingBePriceTick` + `Instrument.MarketData` subscription) + Panel/Window toggle UI | P1 | B10 |
| DW-B10-GAP-002b | `MoveStopToBreakEven` trailing stop fix: `order.TrailPrice > 0` → cancel+replace path | P1 | B10 (after GAP-001d Sim101 verify) |

---

## Prerequisites Before Implementation

1. **GAP-001d** — Sim101 test: confirm `order.TrailPrice` is readable and > 0 on a working trailing stop order. Confirm `acc.Cancel()` successfully removes a trailing stop order.
2. **Sim101 test for pending BE** — verify `NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.MarketDataUpdate` fires correctly in AddOn context (no NinjaScriptBase lifecycle).

---

*Full context: docs/brain/PTT-COPIER-B9/GAP-001-trailing-stop-order-type-preservation.md*

---

## Sim101 Verification Test — GAP-002 Price Watcher (REQUIRED before B10 DW-B10-GAP-002a)

**Status**: PENDING — must be run manually in NinjaTrader against Sim101 before B10 starts.
**Blocks**: DW-B10-GAP-002a (Pending BE price watcher implementation)

### Purpose

Confirm that `NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.MarketDataUpdate`
fires correctly when subscribed from an `AddOnBase` subclass — i.e. with no
`NinjaScriptBase` lifecycle, no `OnStateChange`, no `OnBarUpdate`.

If it does NOT fire → the Pending BE implementation must use the fallback
`Account.AccountItemUpdate` path (Option B in GAP-002 §Price source).

### How to run

Add a temporary test block to `TradeCopierAddOn.OnWindowCreated` (or attach it to a
hidden button in `TradeCopierPanel`). Remove it after the test — this is diagnostic only.

```csharp
// ── Inside TradeCopierAddOn — temporary test code ───────────────────────────
// Add these two members to TradeCopierAddOn temporarily:

private int _gap002TickCount = 0;

private void OnGap002TestTick(object sender, NinjaTrader.Data.MarketDataEventArgs e)
{
    _gap002TickCount++;
    Print($"GAP-002 tick #{_gap002TickCount}: "
        + $"price={e.Price}  "
        + $"type={e.MarketDataType}  "
        + $"time={e.Time:HH:mm:ss.fff}");

    // Auto-unsubscribe after 10 ticks to avoid log flood
    if (_gap002TickCount >= 10)
    {
        var instr = sender as NinjaTrader.Data.Instrument;
        if (instr != null)
            instr.MarketData.MarketDataUpdate -= OnGap002TestTick;
        Print("GAP-002: unsubscribed after 10 ticks");
    }
}

// ── Subscribe from OnWindowCreated (or a test button handler) ───────────────
// Replace "MES 09-26" with your active front-month contract name:
private void SubscribeGap002Test()
{
    const string instrName = "MES 09-26";   // CHANGE to your active contract
    var instr = NinjaTrader.Data.Instrument.GetInstrument(instrName);
    if (instr == null)
    {
        Print($"GAP-002: GetInstrument('{instrName}') returned null -- check instrument name");
        return;
    }
    _gap002TickCount = 0;
    instr.MarketData.MarketDataUpdate += OnGap002TestTick;
    Print($"GAP-002: subscribed to {instr.FullName} MarketDataUpdate from AddOn context");
}
```

Call `SubscribeGap002Test()` from `OnWindowCreated` (or wire it to a temp button).
Watch the NinjaTrader Output window while MES/ES is trading.

### What to observe and log

| Field | Observed value |
|-------|---------------|
| Did `MarketDataUpdate` fire in AddOn context? | YES / NO |
| How many ticks received before auto-unsub? | *(count)* |
| `e.MarketDataType` values seen (Last, Bid, Ask, ...) | *(list)* |
| `e.Price` plausible for current market? | YES / NO |
| `e.Time` is current time (not stale)? | YES / NO |
| `GetInstrument()` returned non-null? | YES / NO |
| Any exception or NT8 error logged? | *(fill in)* |
| Fires during market hours only, or also pre/post? | *(fill in)* |

### Secondary check — `AccountItemUpdate` fallback (Option B)

If `MarketDataUpdate` does NOT fire, test Option B:

```csharp
// Subscribe to AccountItemUpdate (already wired for P&L tracking)
// Check if LastPrice is available and current:
private void OnAccountItemUpdateGap002(object sender, AccountItemEventArgs e)
{
    if (e.AccountItem == AccountItem.RealizedProfitLoss
     || e.AccountItem == AccountItem.UnrealizedProfitLoss)
    {
        // Check if LastPrice accessible from position:
        var acc = (Account)sender;
        foreach (var pos in acc.Positions)
        {
            if (pos.Instrument?.FullName == "MES 09-26")
                Print($"GAP-002 fallback: LastPrice via Position.LastPrice="
                    + $"{pos.AveragePrice}  Qty={pos.Quantity}");
        }
    }
}
```

Note: `AccountItemUpdate` fires per account on P&L changes (driven by price), not on
every tick. This may be too coarse for a BE price watcher that must fire within seconds.

### Decision matrix

| Observed result | Decision for DW-B10-GAP-002a |
|-----------------|------------------------------|
| `MarketDataUpdate` fires reliably, `e.Price` is Last price | **Option A** confirmed — use `Instrument.MarketData` subscription |
| `MarketDataUpdate` fires but only Bid/Ask, not Last | **Option A with filter**: add `if (e.MarketDataType != MarketDataType.Last) return` |
| `MarketDataUpdate` does NOT fire | **Option B** — use `Account.AccountItemUpdate` + position polling |
| Exception on subscribe | Investigate NT8 version; may require dispatch to UI thread first |

### Record result here

```
DATE: _______________
INSTRUMENT: _______________
NT8 VERSION: _______________
MARKET CONDITIONS (RTH / ETH / closed): _______________

RESULT: [ ] MarketDataUpdate fires correctly — Option A confirmed
         [ ] MarketDataUpdate fires but only Bid/Ask — Option A with Last filter
         [ ] MarketDataUpdate does NOT fire — use Option B fallback
         [ ] Exception thrown: _______________

MarketDataType values observed: _______________
Tick count received: _______________

DECISION for DW-B10-GAP-002a:
  Price source: [ ] Option A (Instrument.MarketData)
                [ ] Option A with MarketDataType.Last filter
                [ ] Option B (Account.AccountItemUpdate fallback)

NOTES:
_______________
```

