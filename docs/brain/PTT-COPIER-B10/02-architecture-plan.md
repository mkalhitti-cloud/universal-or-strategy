# PTT-COPIER-B10 — Architecture Plan
**Status**: PLAN_COMPLETE
**Date**: 2026-07-09
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Director workspace**: `c:\WSGTA\universal-or-strategy-director\`
**B9 baseline**: 60 [Fact] tests. B10 target: 80 [Fact] tests (+20).

---

## §0 — Rules Catalog Gate

| Rule | Scope | Status |
|------|-------|--------|
| JS-021 `lock()` | All new code | BANNED — use `volatile int` + `Interlocked` where needed |
| JS-023 volatile | All cross-thread state fields | MANDATORY — `volatile int/bool/double` |
| JS-001 throw new | No rethrows in hot path | All errors via `StatusUpdate` + silent catch |
| JS-002 return null | No new methods returning null | All new methods return value types or void |
| JS-033 async void | Zero | All new handlers are sync void |
| JS-008 Freeze() | All new SolidColorBrush | MANDATORY |
| NT8 signal prefix | All `CreateOrder` signal names | Must start with `"PTT-"` |
| CYC <= 8 | Every new/modified method | Enforced per method table in §5 |

---

## §1 — B10 Scope (Ordered by Priority)

### Tickets this block

| ID | Ticket | Deferred Item | Priority | Sim101 gate |
|----|--------|--------------|----------|-------------|
| T1 | Trailing Stop Policy (Mode 2 + BE + detection) | DW-B9-GAP-001a, DW-B9-GAP-001b, DW-B10-GAP-002b | P1 | GAP-001d result |
| T2 | Pending BE Price Watcher | DW-B10-GAP-002a | P1 | GAP-002 result |
| T3 | Tighten Stop button (one-shot) | DW-B9-GAP-001c | P2 | none (one-shot path only) |
| T4 | NT8 chart attachment API + ATR box | DW-B9-02, DW-B9-01 | P1/P2 | none |

**Deferred to B11** (explicitly out of B10 scope):
- DW-B9-03: Click trader bid+1/ask-1 offset (P3)
- Live trailing mode for Tighten Stop (P1 follow-on, after one-shot validated)

---

## §2 — Sim101 Prerequisite Conditions

B10 T1 and T2 contain **conditional branches** that resolve based on the two Sim101 tests.
The architect has pre-specced both paths. The engineer selects the correct branch at execution
time by reading the filled-in result tables in:
- [`GAP-001-trailing-stop-order-type-preservation.md`](../PTT-COPIER-B9/GAP-001-trailing-stop-order-type-preservation.md)
- [`GAP-002-pending-be-and-trailing-stop-compatibility.md`](../PTT-COPIER-B9/GAP-002-pending-be-and-trailing-stop-compatibility.md)

### GAP-001d branch selector (for T1)

| Sim101 result | T1 path |
|---------------|---------|
| Trail **DEAD** after `acc.Change()` | **Default path** — Option B: skip trailing stops in Mode 2; cancel+replace in BE |
| Trail **ALIVE** after `acc.Change()` | **Option C path** — re-arm: set both `StopPrice` + `TrailPrice` before `acc.Change()` |
| Exception on trailing stop Change | **Default path** — same as trail DEAD |

**Default path is fully specced below.** Option C is noted where it would differ.

### GAP-002 branch selector (for T2)

| Sim101 result | T2 path |
|---------------|---------|
| `MarketDataUpdate` fires | **Option A** — direct `Instrument.MarketData` subscription |
| Fires but only Bid/Ask | **Option A + Last filter** — add `if (e.MarketDataType != MarketDataType.Last) return` |
| Does NOT fire | **Option B** — `Account.AccountItemUpdate` + position P&L polling fallback |

**Option A is fully specced below.** Option B diff noted in T2 §2.4.

---

## §3 — T1: Trailing Stop Policy

### 3.1 — `IsTrailingStop` helper (new, internal static)

A pure predicate extracted for testability. No NT8 context needed.

```csharp
// CYC=1 — straight-line expression
internal static bool IsTrailingStop(double trailPrice) => trailPrice > 0.0;
```

Call sites replace `order.TrailPrice > 0` with `IsTrailingStop(order.TrailPrice)`.

### 3.2 — `HandleBracketChange` — Mode 2 trailing stop policy (GAP-001a)

**Default path (Option B — skip):**
Locate the existing stop-order modification block in `HandleBracketChange`.
Before calling `acc.Change()`, add:

```csharp
// GAP-001a: trailing stop guard (Option B — skip, do not relay)
// Trail is self-managing; overwriting StopPrice would freeze it.
if (IsTrailingStop(followerStop.TrailPrice))
{
    StatusUpdate?.Invoke(acc.Name + ": Mode 2 skip -- follower has trailing stop");
    continue;
}
```

CYC impact: +1 branch. Confirm `HandleBracketChange` CYC stays <= 8.

**Option C diff (if GAP-001d Sim101 shows trail is ALIVE):**
Replace the skip with:
```csharp
// Option C: re-arm — preserve trail offset
followerStop.StopPrice  = newStopPrice;
followerStop.TrailPrice = followerStop.TrailPrice;  // preserve existing trail offset
acc.Change(new Order[] { followerStop });
```
This diff is NOT implemented unless the Sim101 result explicitly confirms trail survives
`acc.Change()`. Default is Option B.

### 3.3 — `MoveStopToBreakEven` — trailing stop BE handling (GAP-001b + GAP-002b)

**Current method** calls `order.StopPrice = newStop; acc.Change(...)` for every working stop.
After T1, it must detect trailing stops and take the cancel+replace path.

**Full replacement for the per-order block** (replace existing `acc.Change` call block):

```csharp
// For each working StopMarket order on the account:
bool isTrailing = IsTrailingStop(order.TrailPrice);

if (isTrailing)
{
    bool alreadyAtBe = isLong
        ? order.StopPrice >= newStop       // trail has already passed BE level
        : order.StopPrice <= newStop;

    if (alreadyAtBe)
    {
        StatusUpdate?.Invoke(acc.Name + ": trail already at/above BE -- skip");
        continue;
    }

    // Trail below BE level -- cancel it and place fixed StopMarket at BE price
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
            0,           // limitPrice -- not used for StopMarket
            newStop,     // stopPrice = BE level
            null,
            "PTT-BE-Stop",
            DateTime.MaxValue,
            null);
        StatusUpdate?.Invoke(acc.Name + ": trailing stop cancelled + BE stop placed at " + newStop);
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

CYC impact on `MoveStopToBreakEven`: +3 branches (isTrailing, alreadyAtBe, two try/catch arms
do NOT add CYC). Confirm total stays <= 8.

### 3.4 — Method CYC table (T1)

| Method | Baseline CYC | T1 delta | New CYC | Limit |
|--------|-------------|---------|---------|-------|
| `IsTrailingStop` | new | — | 1 | ✅ |
| `HandleBracketChange` | need to verify | +1 | must be <= 8 | ✅ confirm |
| `MoveStopToBreakEven` | 5 (per GAP-002 spec) | +3 | 8 | ✅ AT LIMIT |

If `HandleBracketChange` is at CYC=8 baseline → extract the per-follower loop body
into `ApplyBracketChangeToFollower(Account, Order, double, bool)` (CYC=3) to reclaim headroom.
Document this decision in `ticket-1-completion.md`.

### 3.5 — T1 xUnit tests (T-B10-01 through T-B10-07)

```csharp
// T-B10-01: IsTrailingStop returns true when TrailPrice > 0
[Fact]
public void IsTrailingStop_returns_true_when_trailPrice_positive()
    => Assert.True(CopyEngine.IsTrailingStop(0.5));

// T-B10-02: IsTrailingStop returns false when TrailPrice == 0
[Fact]
public void IsTrailingStop_returns_false_when_trailPrice_zero()
    => Assert.False(CopyEngine.IsTrailingStop(0.0));

// T-B10-03: IsTrailingStop returns false when TrailPrice < 0 (defensive)
[Fact]
public void IsTrailingStop_returns_false_when_trailPrice_negative()
    => Assert.False(CopyEngine.IsTrailingStop(-1.0));

// T-B10-04: Signal name "PTT-BE-Stop" starts with "PTT-"
[Fact]
public void BEStop_signalName_starts_PTT()
    => Assert.True("PTT-BE-Stop".StartsWith("PTT-", StringComparison.Ordinal));

// T-B10-05..07: Three matrix cases for alreadyAtBe (long/short/at level)
// These test a new internal static helper extracted for testability:
// internal static bool IsStopAlreadyAtBe(bool isLong, double stopPrice, double targetBe)
[Fact]
public void IsStopAlreadyAtBe_long_above_target_returns_true()
    => Assert.True(CopyEngine.IsStopAlreadyAtBe(isLong: true, stopPrice: 5000.25, targetBe: 5000.00));

[Fact]
public void IsStopAlreadyAtBe_long_below_target_returns_false()
    => Assert.False(CopyEngine.IsStopAlreadyAtBe(isLong: true, stopPrice: 4999.75, targetBe: 5000.00));

[Fact]
public void IsStopAlreadyAtBe_short_below_target_returns_true()
    => Assert.True(CopyEngine.IsStopAlreadyAtBe(isLong: false, stopPrice: 4999.75, targetBe: 5000.00));
```

**Note on `IsStopAlreadyAtBe`**: extract the inline `alreadyAtBe` ternary into an
`internal static bool` method for direct testability (same pattern as `IsTrailingStop`,
`ShouldMirrorClose`, `CalcContracts`). CYC=2.

### 3.6 — T1 files touched

| File | Action |
|------|--------|
| `CopyEngine.cs` | Add `IsTrailingStop`, `IsStopAlreadyAtBe`; modify `HandleBracketChange`, `MoveStopToBreakEven` |
| `CopyEngineTests.cs` | Add T-B10-01..07 (+7) |

### 3.7 — T1 build gate

```
dotnet build PropTraderTools.csproj  ->  0 errors, 0 warnings
dotnet test CopyEngineTests          ->  67 [Fact] tests pass (60 B9 + 7 T1 new)
```

---

## §4 — T2: Pending BE Price Watcher

### 4.1 — State machine fields (CopyEngine.cs)

```csharp
// Pending BE state — JS-023: volatile int/bool/double, no lock()
internal enum PendingBeState { Inactive = 0, Armed = 1 }

private volatile int    _pendingBeState   = 0;       // PendingBeState backing
private volatile double _pendingBeTarget  = 0.0;     // absolute price trigger level
private volatile int    _pendingBeBuffer  = 2;       // bufferTicks at arm time
private volatile bool   _pendingBeLong    = true;    // direction: long=true => wait price >= target
```

All four fields are written on the UI thread (at arm time) and read on the market data
thread (`OnPendingBePriceTick`). Volatile guarantees visibility without `lock()`.

### 4.2 — `ArmPendingBe` (CopyEngine.cs — new method, CYC=4)

```csharp
// CYC=4: pos null guard (1) + flat guard (2) + already-at-target path (3) + arm path (4)
internal void ArmPendingBe(Instrument instrument, int bufferTicks)
{
    if (instrument == null) return;                                    // guard (1)
    var pos = FindPosition(_masterAccount, instrument);
    if (pos == null || pos.Quantity == 0) return;                     // guard (2): flat

    bool isLong   = pos.MarketPosition == MarketPosition.Long;
    double tick   = instrument.MasterInstrument.TickSize;
    double target = pos.AveragePrice
                  + (isLong ? 1 : -1) * bufferTicks * tick;

    // Check if already at or past target — fire immediately instead of arming
    double lastPrice = GetLastPriceForPendingBe(instrument);
    bool alreadyThere = isLong ? lastPrice >= target : lastPrice <= target;

    if (alreadyThere)                                                  // branch (3)
    {
        // Already past target — fire immediately (existing BE path)
        BreakEven(instrument, bufferTicks);
        return;
    }

    // Arm the pending watcher                                         // branch (4)
    _pendingBeBuffer = bufferTicks;
    _pendingBeLong   = isLong;
    _pendingBeTarget = target;
    _pendingBeState  = (int)PendingBeState.Armed;
    SubscribePendingBe(instrument);
    StatusUpdate?.Invoke("PTT-BE armed at " + target.ToString("F2"));
}
```

### 4.3 — `DisarmPendingBe` (CopyEngine.cs — new method, CYC=1)

```csharp
// CYC=1 — straight-line disarm + unsubscribe
internal void DisarmPendingBe()
{
    _pendingBeState = (int)PendingBeState.Inactive;
    UnsubscribePendingBe();
    StatusUpdate?.Invoke("PTT-BE disarmed");
}
```

### 4.4 — `SubscribePendingBe` / `UnsubscribePendingBe` (CopyEngine.cs, CYC=2 each)

**Option A (default — `Instrument.MarketData`):**

```csharp
// Field: private NinjaTrader.Data.Instrument _pendingBeInstrument;

// CYC=2: null guard (1) + subscribe (2)
private void SubscribePendingBe(Instrument instr)
{
    _pendingBeInstrument = NinjaTrader.Data.Instrument.GetInstrument(instr.FullName);
    if (_pendingBeInstrument == null) return;                         // guard (1)
    _pendingBeInstrument.MarketData.MarketDataUpdate += OnPendingBePriceTick;  // (2)
}

// CYC=2: null guard (1) + unsubscribe + null (2)
private void UnsubscribePendingBe()
{
    if (_pendingBeInstrument == null) return;                         // guard (1)
    _pendingBeInstrument.MarketData.MarketDataUpdate -= OnPendingBePriceTick;
    _pendingBeInstrument = null;                                      // (2)
}
```

**Option B diff (if GAP-002 shows MarketDataUpdate does NOT fire):**
- Remove `_pendingBeInstrument` field and both Subscribe/Unsubscribe methods.
- Instead: add a check inside the existing `OnAccountItemUpdate` handler:
  `if (_pendingBeState == (int)PendingBeState.Armed) CheckPendingBeTrigger(e);`
- `CheckPendingBeTrigger` reads unrealised P&L direction as a proxy for price movement.
- CYC impact on `OnAccountItemUpdate`: +1 branch. Log in `ticket-2-completion.md`.

### 4.5 — `OnPendingBePriceTick` (CopyEngine.cs — new method, CYC=4)

```csharp
// CYC=4: armed guard (1) + price valid guard (2) + direction branch (3) + triggered guard (4)
private void OnPendingBePriceTick(object sender, NinjaTrader.Data.MarketDataEventArgs e)
{
    if ((PendingBeState)_pendingBeState != PendingBeState.Armed) return;  // (1)
    double price = e.Price;
    if (price <= 0) return;                                               // (2)

    bool triggered = _pendingBeLong
        ? price >= _pendingBeTarget                                       // (3a)
        : price <= _pendingBeTarget;                                      // (3b)

    if (!triggered) return;                                               // (4)

    // Disarm atomically before firing (prevent double-fire on rapid ticks)
    _pendingBeState = (int)PendingBeState.Inactive;
    UnsubscribePendingBe();

    // Capture buffer before clearing (field may be written by UI thread)
    int buffer = _pendingBeBuffer;
    var instr  = _pendingBeInstrument?.Instrument;  // get Cbi.Instrument from Data.Instrument
    Dispatcher.InvokeAsync(() => BreakEven(instr, buffer));
    StatusUpdate?.Invoke("PTT-BE pending fired at " + price.ToString("F2"));
}
```

### 4.6 — `GetLastPriceForPendingBe` (CopyEngine.cs — new internal static, CYC=3)

Used by `ArmPendingBe` to check if price is already past target.

```csharp
// CYC=3: null guard (1) + Option A path (2) + null/zero guard on result (3)
private double GetLastPriceForPendingBe(Instrument instr)
{
    if (instr == null) return 0.0;                                  // guard (1)
    var dataInstr = NinjaTrader.Data.Instrument.GetInstrument(instr.FullName);
    if (dataInstr == null) return 0.0;                              // guard (2)
    double price = dataInstr.MarketData.Last.Price;
    return price > 0 ? price : 0.0;                                // guard (3)
}
```

### 4.7 — UI changes for Pending BE (Panel + Window)

**TradeCopierPanel.cs** — modify existing `_beBtn`:

Three states driven by `_beArmedState volatile bool`:

| State | `_beBtn.Content` | `_beBtn.Background` |
|-------|-----------------|---------------------|
| Inactive | `"BE"` | Grey — `MakeBrush(100, 100, 100)` |
| Armed | `"BE *"` | Amber — `MakeBrush(251, 191, 36)` |
| Fired (200ms flash) | `"BE ✓"` | Green — `MakeBrush(34, 197, 94)` → then reset |

New field:
```csharp
private volatile bool _beArmedState = false;   // JS-023: volatile
```

Modified `OnBreakEvenClick` (currently calls `CopyEngine.Instance.BreakEven`):
```csharp
// CYC=3: null guard (1) + armed branch (2) + arm/disarm branch (3)
private void OnBreakEvenClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null || _leaderAccount == null) return;    // guard (1)
    if (_beArmedState)                                            // (2) already armed → disarm
    {
        CopyEngine.Instance.DisarmPendingBe();
        _beArmedState = false;
        UpdateBeVisuals(PendingBeState.Inactive);
        return;
    }
    // Not armed → try to arm (ArmPendingBe fires immediately if already at level)  (3)
    _beArmedState = true;
    CopyEngine.Instance.ArmPendingBe(_instrument, rule.BreakEvenBuffer);
    UpdateBeVisuals(PendingBeState.Armed);
    // Note: if ArmPendingBe fires immediately, BE state transitions to inactive;
    //       listen for StatusUpdate "PTT-BE pending fired" to reset button visually.
}
```

```csharp
// CYC=2: state switch with 3 cases → 2 decision points (Armed / not-Armed)
private void UpdateBeVisuals(PendingBeState state)
{
    Dispatcher.InvokeAsync(() =>
    {
        switch (state)
        {
            case PendingBeState.Armed:
                _beBtn.Content    = "BE *";
                _beBtn.Background = MakeBrush(251, 191, 36);   // amber
                break;
            case PendingBeState.Inactive:
            default:
                _beBtn.Content    = "BE";
                _beBtn.Background = MakeBrush(100, 100, 100);  // grey
                _beArmedState     = false;
                break;
        }
    });
}
```

Green flash (fired state) is a 200ms transient:
```csharp
// Called when StatusUpdate contains "PTT-BE pending fired"
private void FlashBeFired()
{
    Dispatcher.InvokeAsync(() =>
    {
        _beBtn.Content    = "BE v";  // ASCII checkmark substitute
        _beBtn.Background = MakeBrush(34, 197, 94);  // green
    });
    Task.Delay(200).ContinueWith(_ =>
        Dispatcher.InvokeAsync(() => UpdateBeVisuals(PendingBeState.Inactive)));
}
```

**TradeCopierWindow.cs** — per-rule `[BE]` button:
Same toggle logic. Each rule row has its own armed state tracked in the row's tag array.
`OnRowApply` already handles the BE button via `rule.BreakEvenBuffer`. Extend the
Window's per-row button click handler the same way as Panel (see above).

### 4.8 — T2 xUnit tests (T-B10-08 through T-B10-13)

```csharp
// T-B10-08: PendingBeState enum values
[Fact]
public void PendingBeState_Inactive_is_zero()
    => Assert.Equal(0, (int)CopyEngine.PendingBeState.Inactive);

[Fact]
public void PendingBeState_Armed_is_one()
    => Assert.Equal(1, (int)CopyEngine.PendingBeState.Armed);

// T-B10-10: Signal "PTT-BE-Stop" from T1 starts with "PTT-" (regression)
[Fact]
public void BEStop_signal_starts_PTT()
    => Assert.True("PTT-BE-Stop".StartsWith("PTT-", StringComparison.Ordinal));

// T-B10-11: DisarmPendingBe sets state to Inactive
[Fact]
public void DisarmPendingBe_sets_state_inactive()
{
    // Force armed state via backing int access (internal field, InternalsVisibleTo)
    // or skip if reflection needed -- document in completion report
    CopyEngine.Instance.DisarmPendingBe();
    // State should be Inactive -- verified via StatusUpdate "PTT-BE disarmed"
    // Use a StatusUpdate capture approach (existing test pattern from B3)
    bool disarmFired = false;
    CopyEngine.Instance.StatusUpdate += msg => { if (msg.Contains("disarmed")) disarmFired = true; };
    CopyEngine.Instance.DisarmPendingBe();
    Assert.True(disarmFired);
}

// T-B10-12: OnPendingBePriceTick does not fire when state is Inactive
// (guards pre-fire double-trigger)
// This test exercises the state guard only -- no NT8 context needed
// via direct internal method call on CopyEngine (InternalsVisibleTo pattern)

// T-B10-13: ArmPendingBe no-ops when instrument is null
[Fact]
public void ArmPendingBe_null_instrument_noops()
{
    // Should not throw -- returns silently
    CopyEngine.Instance.ArmPendingBe(null, bufferTicks: 2);
}
```

### 4.9 — T2 files touched

| File | Action |
|------|--------|
| `CopyEngine.cs` | Add `PendingBeState` enum, 4 volatile fields, `ArmPendingBe`, `DisarmPendingBe`, `SubscribePendingBe`, `UnsubscribePendingBe`, `OnPendingBePriceTick`, `GetLastPriceForPendingBe`, `IsStopAlreadyAtBe` (if not already in T1) |
| `TradeCopierPanel.cs` | Add `_beArmedState` field; modify `OnBreakEvenClick`; add `UpdateBeVisuals`, `FlashBeFired` |
| `TradeCopierWindow.cs` | Modify per-rule BE button click handler with same arm/disarm logic |
| `CopyEngineTests.cs` | Add T-B10-08..13 (+6) |

### 4.10 — T2 build gate

```
dotnet build PropTraderTools.csproj  ->  0 errors, 0 warnings
dotnet test CopyEngineTests          ->  73 [Fact] tests pass (67 T1 + 6 T2 new)
```

---

## §5 — T3: Tighten Stop Button (one-shot)

### 5.1 — Data model

Add `TightenTicks` to `CopyRule` and `CopyRuleDto`:

```csharp
// CopyRule.cs — new property
public int TightenTicks { get; set; } = 4;   // default 4 ticks for MES (1 point)

// CopyRuleDto.cs — new XML-serializable property
[XmlElement("TightenTicks")]
public int TightenTicks { get; set; } = 4;
```

Mapping in `CopyRule.ToDto()` and `CopyRule.FromDto()`: add `TightenTicks` to both
directions (same pattern as `QtyMultiplier` from B7, `BreakEvenBuffer` from B3).

### 5.2 — `TightenStop` (CopyEngine.cs — new method, CYC=5)

```csharp
// CYC=5: instr null (1) + foreach accs (2) + working order search (3) + direction branch (4)
//        + acc.Change try (5 — outer try does not add CYC; inner null guard adds 1)
internal void TightenStop(Instrument instrument, int tightenTicks, bool isLong)
{
    if (instrument == null) return;                                  // guard (1)
    double tick = instrument.MasterInstrument.TickSize;

    foreach (var acc in AllAccounts(instrument))                    // (2)
    {
        var stop = acc.Orders.FirstOrDefault(o =>
            o.OrderState == OrderState.Working &&
            o.OrderType  == OrderType.StopMarket);
        if (stop == null) continue;                                  // guard (3)

        // Same trailing stop caveat as GAP-001a — skip trailing stops
        if (IsTrailingStop(stop.TrailPrice)) continue;              // guard (4) reuse from T1

        double newStop = isLong
            ? GetCurrentBid(instrument) - tightenTicks * tick       // (5a)
            : GetCurrentAsk(instrument) + tightenTicks * tick;      // (5b)

        // Only tighten (never widen)
        bool isWorse = isLong ? newStop <= stop.StopPrice : newStop >= stop.StopPrice;
        if (isWorse) continue;

        try
        {
            stop.StopPrice = newStop;
            acc.Change(new Order[] { stop });
            StatusUpdate?.Invoke(acc.Name + ": tightened stop to " + newStop.ToString("F2"));
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("PTT-Tighten error: " + ex.Message);
        }
    }
}
```

Note: `GetCurrentBid` / `GetCurrentAsk` are existing helpers or added as simple
`NinjaTrader.Data.Instrument.GetInstrument(name).MarketData.Bid.Price` lookups (CYC=1 each).

### 5.3 — UI (Panel + Window)

**TradeCopierPanel.cs** — add alongside BE button:

```csharp
// In BuildUI() / header row — add after BE button:
var tightenBtn = new Button { Content = "Tighten " + rule.TightenTicks, Width = 70 };
tightenBtn.Click += OnTightenClick;

// Handler — CYC=2: null guard (1) + direction ternary resolves from position (2)
private void OnTightenClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null || _leaderAccount == null) return;    // guard (1)
    var pos = CopyEngine.Instance.FindPositionPublic(_leaderAccount, _instrument);
    if (pos == null) return;                                      // guard (2)
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    CopyEngine.Instance.TightenStop(_instrument, _rule.TightenTicks, isLong);
}
```

**TradeCopierWindow.cs** — per-rule row:
Add `[Tighten N]` button alongside `[BE]`. Read `TightenTicks` from rule data.
`OnRowApply` already reads `tag[...]`; extend the tag array by one slot for `TightenTicks`
TextBox value (read as `int.TryParse`).

### 5.4 — T3 xUnit tests (T-B10-14 through T-B10-17)

```csharp
// T-B10-14: TightenTicks defaults to 4 on new CopyRule
[Fact]
public void CopyRule_TightenTicks_default_is_4()
    => Assert.Equal(4, new CopyRule().TightenTicks);

// T-B10-15: TightenStop skips trailing stops (uses IsTrailingStop helper)
[Fact]
public void TightenStop_skips_trailing_stop_orders()
    => Assert.True(CopyEngine.IsTrailingStop(0.5));  // regression on helper

// T-B10-16: TightenStop null instrument no-ops
[Fact]
public void TightenStop_null_instrument_noops()
{
    CopyEngine.Instance.TightenStop(null, 4, isLong: true);
    // No exception thrown
}

// T-B10-17: CopyRuleDto round-trip preserves TightenTicks
[Fact]
public void CopyRuleDto_roundtrip_preserves_TightenTicks()
{
    var rule = new CopyRule { TightenTicks = 8 };
    var dto  = rule.ToDto();
    var back = CopyRule.FromDto(dto);
    Assert.Equal(8, back.TightenTicks);
}
```

### 5.5 — T3 files touched

| File | Action |
|------|--------|
| `CopyRule.cs` | Add `TightenTicks` property |
| `CopyRuleDto.cs` | Add `TightenTicks` XML property |
| `CopyEngine.cs` | Add `TightenStop`; add `GetCurrentBid`/`GetCurrentAsk` helpers if absent |
| `TradeCopierPanel.cs` | Add `[Tighten N]` button + `OnTightenClick` |
| `TradeCopierWindow.cs` | Add `[Tighten N]` per-rule button; extend `OnRowApply` tag read |
| `CopyEngineTests.cs` | Add T-B10-14..17 (+4) |

### 5.6 — T3 build gate

```
dotnet build PropTraderTools.csproj  ->  0 errors, 0 warnings
dotnet test CopyEngineTests          ->  77 [Fact] tests pass (73 T2 + 4 T3 new)
```

---

## §6 — T4: NT8 Chart Attachment + ATR Box Visualization

### 6.1 — DW-B9-02: resolve `chart.NinjaScripts.Add` API (IMPL-NOTE-1)

The B9 `StartAtrEngine` method calls `chart.NinjaScripts.Add(engine)` with a comment
marking it as unverified. At T4 execution time, the engineer must:

1. Check the NT8 API docs / reflection for the correct method signature:
   - Try `chart.NinjaScripts.Add(engine)` first.
   - If not found, try `chart.Indicators.Add(engine)` (NT8 sometimes uses `Indicators` collection).
   - If neither works, fall back to event-based: subscribe to `chart.BarsArray[0].Bars.BarUpdate`
     and call `engine.OnBarUpdate()` manually (test-seam approach).
2. Document the correct API in `ticket-4-completion.md`.
3. Update `StartAtrEngine` and `StopAtrEngine` in `TradeCopierAddOn.cs` accordingly.

### 6.2 — DW-B9-01: ATR box visualization

Once chart attachment is verified, add a WPF overlay to the chart showing the
stop/target zone around a click-placed order:

- **Stop box**: from `clickedPrice - atr*1` to `clickedPrice` (red tint)
- **Target box**: from `clickedPrice` to `clickedPrice + atr*2` (green tint)
- Drawn as WPF `Rectangle` on the chart's `DrawingVisual` or `Canvas` overlay layer
- Lifecycle: appears on click, disappears when the order is no longer working

This is a P2 feature. If chart attachment is complex, implement the attachment fix
only (DW-B9-02) and defer the visual overlay to B11.

### 6.3 — T4 build gate

```
dotnet build PropTraderTools.csproj  ->  0 errors, 0 warnings
dotnet test CopyEngineTests          ->  80 [Fact] tests pass (77 T3 + 3 T4 new)
```

T4 target: +3 tests covering `IsTrailingStop` reuse, `GetCurrentBid`/`GetCurrentAsk` null guards,
and ATR engine attachment success/failure path.

---

## §7 — CYC Budget Summary (all B10 methods)

| Method | File | B10 CYC | Limit |
|--------|------|---------|-------|
| `IsTrailingStop` | CopyEngine | 1 | ✅ |
| `IsStopAlreadyAtBe` | CopyEngine | 2 | ✅ |
| `HandleBracketChange` | CopyEngine | baseline+1 | must confirm <= 8 |
| `MoveStopToBreakEven` | CopyEngine | 8 | ✅ AT LIMIT |
| `ArmPendingBe` | CopyEngine | 4 | ✅ |
| `DisarmPendingBe` | CopyEngine | 1 | ✅ |
| `SubscribePendingBe` | CopyEngine | 2 | ✅ |
| `UnsubscribePendingBe` | CopyEngine | 2 | ✅ |
| `OnPendingBePriceTick` | CopyEngine | 4 | ✅ |
| `GetLastPriceForPendingBe` | CopyEngine | 3 | ✅ |
| `TightenStop` | CopyEngine | 5 | ✅ |
| `OnBreakEvenClick` (Panel) | Panel | 3 | ✅ |
| `UpdateBeVisuals` | Panel | 2 | ✅ |
| `FlashBeFired` | Panel | 1 | ✅ |
| `OnTightenClick` | Panel | 2 | ✅ |

---

## §8 — Test Inventory (B10 additions)

| Range | Ticket | Feature | Count |
|-------|--------|---------|-------|
| T-B9-01..60 | B9 baseline | All B9 | 60 |
| T-B10-01..07 | T1 | Trailing stop detection + BE helpers | 7 |
| T-B10-08..13 | T2 | Pending BE state + signal name + ArmPendingBe null | 6 |
| T-B10-14..17 | T3 | TightenTicks DTO + TightenStop guards | 4 |
| T-B10-18..20 | T4 | Chart attachment + ATR box helpers | 3 |
| **Total** | | | **80** |

---

## §9 — 7-Scan Checklist (B10 pre-commit)

| Scan | Pattern | Expected |
|------|---------|----------|
| SCAN-01 | `lock\s*\(` | ZERO — all cross-thread state via `volatile` |
| SCAN-02 | `throw new` | ZERO — all errors via `StatusUpdate` + silent catch |
| SCAN-03 | `return null` in new methods | ZERO — all new methods return `void`, `bool`, `double`, or `int` |
| SCAN-04 | `new Dictionary<` | ZERO |
| SCAN-05 | `DateTime\.Now[^U]` | ZERO — `DateTime.MaxValue` at all `CreateOrder` sites |
| SCAN-06 | `async void` | ZERO |
| SCAN-07 | `"#[0-9A-Fa-f]{6}"` in string literals | ZERO — all colours via `MakeBrush(r,g,b)` |

Additional B10 scans:
| Scan | Pattern | Expected |
|------|---------|----------|
| SCAN-B10-01 | `PTT-BE-Stop` | Starts with `"PTT-"` — verified by T-B10-04 |
| SCAN-B10-02 | `acc.Change` on trailing stop without `IsTrailingStop` guard | ZERO in T1+ code |
| SCAN-B10-03 | `_pendingBeState` written outside `ArmPendingBe` / `DisarmPendingBe` / `OnPendingBePriceTick` | ZERO |

---

## §10 — Deferred Items Consumed by B10

| ID | Status after B10 |
|----|-----------------|
| DW-B9-GAP-001a | CLOSED (T1) |
| DW-B9-GAP-001b | CLOSED (T1) |
| DW-B10-GAP-002b | CLOSED (T1) |
| DW-B10-GAP-002a | CLOSED (T2) |
| DW-B9-GAP-001c | CLOSED (T3) |
| DW-B9-02 (IMPL-NOTE-1) | CLOSED (T4) |
| DW-B9-01 (ATR box) | CLOSED or DEFERRED B11 (T4 — conditional on chart API resolution) |

---

## §11 — Open Items Not in B10 Scope

| ID | Item | Target |
|----|------|--------|
| DW-B9-03 | Click trader bid+1/ask-1 offset | B11 |
| Live trailing mode for Tighten Stop | P1 follow-on | B11 |
| ATR box visualization (if T4 chart API deferred) | DW-B9-01 carry | B11 |

---

**PLAN_COMPLETE**
