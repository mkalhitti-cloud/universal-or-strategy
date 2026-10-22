# B211 Architecture Plan
## Move-Target Buttons: Ask / Ask-ALL / Bid / Bid-ALL

**Block**: B211
**Status**: REVIEW_PASS (V1/V2/V3/V4 fixed — revision 1)
**Architect**: ptt-architect
**Date**: 2026-07-07

---

## LANE-SPLIT GATE

Q1. Same method or within 50 lines? YES (Ask row and Bid row added together in the same data-driven spec array in BuildBufferedButtonsRow; structurally symmetric and co-located).
Q2. Fix B design depends on Fix A final design? YES (both rows call PttMoveTargetOne.Execute with the same `useAsk` bool switch; Bid design is derived directly from Ask design).
Q3. Each fix has standalone value if the other is blocked? YES (Ask row works independently; Bid row works independently).
Q4. Each fix has an independent SIM verification path? YES (Ask: press Ask, verify ask-side cancel+resubmit; Bid: press Bid, verify bid-side cancel+resubmit).

Default rule: SINGLE-PIPELINE unless Q1+Q2=NO and Q3+Q4=YES.
Since Q1=YES (condition fails), the default applies.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## B209 Deferred Backlog Review (READ-ONLY)

Items carried forward from `docs/brain/B209/06-deferred-backlog.md`:
| ID | Item | Status in B211 |
|----|------|----------------|
| DW-B209-01 | Guardian script single-hunk false positive | OPEN -- not B211 scope |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal pre-existing gap | OPEN -- not B211 scope |
| DW-B209-03 | SIM Round 7 -- verify OCO path + GTC sweep guard | OPEN -- not B211 scope |
| DW-B209-04 | specs/002-trade-copier-spec.html three updates | OPEN -- not B211 scope |

None of the above are closed or resolved by B211. All remain open.

---

## Architect Decisions

### DW-B211-03: Canonical Target Order Identification

**Decision: LAST Working PTT-prefixed Limit exit in acc.Orders iteration.**

When multiple Working Limit exit orders exist simultaneously (e.g., PTT-BE-Target-N from an active BE swap plus a prior PTT-MA-Ask-N), `TryFindWorkingTarget` scans `acc.Orders` and:
1. Filters for: `OrderState.Working`, `OrderType.Limit`, instrument match, PTT-prefixed name (`o.Name.StartsWith("PTT-", StringComparison.Ordinal)`), and exit action (`OrderAction.Sell` or `OrderAction.BuyToCover`).
2. Takes the **last matching order** in iteration order (NT8 appends to `Account.Orders` in submission order; last = most recently submitted).
3. Returns `false` (bool) if no match found, assigning `default` to the out parameter (no-op path). JS-002 compliant.

**Rationale**: "Most recently submitted" is the intent from the spec. NT8 does not expose a submission timestamp or sequence on `Order`, but `Account.Orders` is an append-ordered collection, so the last match is the best available proxy. Filtering by PTT-prefix explicitly excludes ATM-managed brackets (Target1, Target2, Stop1, Stop2) to avoid disrupting ATM state. The LAST-in-iteration strategy means the most recent PTT-* exit is moved, which is the semantically correct target for a "move my exit" operation.

### DW-B211-05: Shared vs Independent Buffers for Ask/Ask-ALL and Bid/Bid-ALL

**Decision: CONFIRM SHARED BUFFERS.**

`_askBuffer`: single `volatile int`, shared between [Ask] and [Ask-ALL].
`_bidBuffer`: single `volatile int`, shared between [Bid] and [Bid-ALL].

**Rationale**: User sets desired offset once via the spinners, then applies to single account (Ask) or all accounts (Ask-ALL). Sharing the buffer is the natural UX: both buttons display the same current tick offset. One field per side. Note this differs from BE/BE-ALL which maintain independent buffers for historical reasons tied to the GlobalBe state machine. B211 has no such constraint.

---

## Jane Street Compliance Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_askBuffer`/`_bidBuffer` declared `volatile int`. `AllAccounts` is lock-free ConcurrentDictionary. No `lock()` anywhere. | PASS |
| JS-001 (no throw) | All new methods use guard-return pattern. No `throw new ...`. NT8 API calls (Cancel/CreateOrder/Submit) wrapped in try/catch in Execute — log-and-swallow, no rethrow. | PASS |
| JS-002 (no return null) | All new methods return `void`. FindWorkingTarget renamed to `TryFindWorkingTarget` — TryGet pattern with `out Order target`, returns `bool`. No null returned from any method. | PASS |
| JS-033 (no async void) | All click handlers are synchronous `void`. No `async`. | PASS |
| ASCII-only | Order names: "PTT-MA-Ask-", "PTT-MA-Bid-". Log strings: "[PTT-MA]". Button labels: "Ask +0", "Bid +0", "Ask-ALL +0", "Bid-ALL +0". All ASCII. | PASS |
| CYC <= 8 | See per-method CYC table below. All new methods <= 8. | PASS |
| No DateTime.Now | Not applicable -- no timestamps. | PASS |
| No FontFamily | NTButtonStyle resource reference only. | PASS |
| No hardcoded hex | BrushTeal reused (existing field). No new brush definitions. | PASS |
| Partial fill P0 | `qty = existingTarget.Quantity - existingTarget.Filled` (MANDATORY). | PASS |

### CYC Budget Table

| Method | File | CYC | Limit | Branches |
|--------|------|-----|-------|----------|
| `PttMoveTargetOne.Execute` | PttMoveTargetOne.cs | 6 | 6 | (1)instr null, (2)TryGetExitPrice fail, (3)pos null\|\|qty==0, (4)TryFindWorkingTarget false, (5)prefix ternary |
| `PttMoveTargetOne.TryGetExitPrice` | PttMoveTargetOne.cs | 6 | 8 | (1)tickSize ??, (2)tickSize<=0, (3)md==null, (4)ask/bid null, (5)price<=0 |
| `PttMoveTargetOne.TryFindWorkingTarget` | PttMoveTargetOne.cs | 6 | 8 | (1)foreach, (2)instr+state compound, (3)OrderType.Limit, (4)PTT-prefix, (5)exit action check |
| `PttMoveTargetAll.Execute` | PttMoveTargetAll.cs | 3 | 4 | (1)instrument null, (2)foreach AllAccounts |
| `OnAskUp/OnAskDown/OnBidUp/OnBidDown` | TradeCopierPanel.cs | 1 each | 8 | none -- clamp expression |
| `OnAskClick/OnBidClick` | TradeCopierPanel.cs | 4 each | 8 | (1)instrument null, (2)?? null-coalescing operator, (3)leader null |
| `OnAskAllClick/OnBidAllClick` | TradeCopierPanel.cs | 2 each | 8 | (1)instrument null |

---

## NT8 API Surface (Grounded -- Read from src/PropTraderTools/CopyEngine.cs)

### Account.Cancel
```csharp
acc.Cancel(new Order[] { existingTarget });
```
Confirmed AddOnBase-available (CopyEngine.cs line 2189, 2253). Pattern used throughout.

### Account.CreateOrder (13-arg signature)
```csharp
var order = acc.CreateOrder(
    instr,                              // Instrument
    isLong ? OrderAction.Sell : OrderAction.BuyToCover,  // OrderAction
    OrderType.Limit,                    // OrderType
    OrderEntry.Manual,                  // OrderEntry (user-initiated)
    TimeInForce.Day,                    // TimeInForce (intraday exit)
    qty,                                // int quantity
    limitPrice,                         // double limitPrice
    0.0,                                // double stopPrice (0 for Limit)
    string.Empty,                       // string oco
    orderName,                          // string signal name "PTT-MA-Ask-00001"
    NinjaTrader.Core.Globals.MaxDate,   // DateTime expiry
    (NinjaTrader.Cbi.CustomOrder)null   // NT8-007: (CustomOrder)null -- not string
);
```
Confirmed AddOnBase-available (CopyEngine.cs line 3997-4009, 6128-6133).

### Account.Submit
```csharp
if (order != null) acc.Submit(new[] { order });
```
Confirmed at CopyEngine.cs line 4016, 1440.

### OrderAction for Exit
- Long position exit: `OrderAction.Sell` (confirmed CopyEngine.cs line 1384, 2661, 5826)
- Short position exit: `OrderAction.BuyToCover` (confirmed CopyEngine.cs line 1384, 2662, 5827)

### Instrument Market Data
```csharp
var md = instr.MarketData;                     // null if no subscription
var ask = md?.Ask;                             // MarketDataEventArgs, null if no data
double askPrice = ask?.Price ?? 0.0;           // double
```
Confirmed via TradeCopierPanel.cs GetAsk() at line 1414-1425 (existing helper in panel).

### BANNED APIs
- `AtmStrategyChangeStopTarget()` -- StrategyBase-ONLY. AddOnBase-banned. NOT USED in B211. ✓
- `AtmStrategyCreate()` -- StrategyBase-ONLY. NOT USED. ✓
- `Account.Change()` -- silent no-op on ATM-owned brackets. NOT USED. ✓

---

## Component List

### 1. `TradeCopierPanel.cs` (MODIFY)

**New fields** (add after existing `_beBuffer` at line 241):
```csharp
private volatile int _askBuffer = 0;  // B211: Ask/Ask-ALL buffer ticks. JS-023: volatile.
private volatile int _bidBuffer = 0;  // B211: Bid/Bid-ALL buffer ticks. JS-023: volatile.
```

**New UniformGrid panel fields** (add after `_quickRowPanel` at line 264):
```csharp
private UniformGrid _askRowPanel = null; // B211: 2-col: Ask cluster | Ask-ALL cluster
private UniformGrid _bidRowPanel = null; // B211: 2-col: Bid cluster | Bid-ALL cluster
```

**New Button ref fields** (add with existing button fields):
```csharp
private Button _askBtn = null;
private Button _askAllBtn = null;
private Button _bidBtn = null;
private Button _bidAllBtn = null;
```

**BuildBufferedButtonsRow modifications**:
Before the `foreach` loop, initialize the two new panels:
```csharp
_askRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
_bidRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
```

Add 4 new entries to the `specs` array (same structure as existing BE entries):
```csharp
(
    FormatBuffer("Ask", _askBuffer),
    BrushTeal,
    true,
    OnAskUp, OnAskDown, OnAskClick,
    b => _askBtn = b,
    _askRowPanel
),
(
    FormatBuffer("Ask-ALL", _askBuffer),
    BrushTeal,
    true,
    OnAskAllUp, OnAskAllDown, OnAskAllClick,
    b => _askAllBtn = b,
    _askRowPanel
),
(
    FormatBuffer("Bid", _bidBuffer),
    BrushTeal,
    true,
    OnBidUp, OnBidDown, OnBidClick,
    b => _bidBtn = b,
    _bidRowPanel
),
(
    FormatBuffer("Bid-ALL", _bidBuffer),
    BrushTeal,
    true,
    OnBidAllUp, OnBidAllDown, OnBidAllClick,
    b => _bidAllBtn = b,
    _bidRowPanel
),
```

**BuildUI (root assembly) modifications** (after existing `root.Children.Add(_beRowPanel)` at line 871):
```csharp
root.Children.Add(_askRowPanel);  // B211: Ask row below BE row
root.Children.Add(_bidRowPanel);  // B211: Bid row below Ask row
```

**New event handlers** (add after `OnBeDown` at line ~1388):

```csharp
// B211: OnAskUp -- increment _askBuffer, clamp [-10, +20]. CYC=1. JS-021: volatile int. ASCII-only.
// V4 rationale: lower clamp is -10 per spec WPF template (HTML line 43463). Upper clamp +20 per
// orchestrator prompt direction. Director approved.
private void OnAskUp(object sender, RoutedEventArgs e)
{
    _askBuffer = Math.Max(Math.Min(_askBuffer + 1, 20), -10);
    UpdateAskLabels();
}

// B211: OnAskDown -- decrement _askBuffer, clamp [-10, +20]. CYC=1. JS-021: volatile int. ASCII-only.
// V4 rationale: lower clamp is -10 per spec WPF template (HTML line 43463). Upper clamp +20 per
// orchestrator prompt direction. Director approved.
private void OnAskDown(object sender, RoutedEventArgs e)
{
    _askBuffer = Math.Max(Math.Min(_askBuffer - 1, 20), -10);
    UpdateAskLabels();
}

// B211: OnAskClick -- cancel Working target + resubmit Limit at Ask + buffer. CYC=4. JS-021: no lock.
private void OnAskClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
}

// B211: OnAskAllClick -- cancel Working target + resubmit Limit at Ask + buffer for all accounts. CYC=2.
private void OnAskAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    PttMoveTargetAll.Execute(_instrument, useAsk: true, _askBuffer);
}

// B211: OnAskAllUp -- increment _askBuffer (shared). CYC=1.
private void OnAskAllUp(object sender, RoutedEventArgs e) => OnAskUp(sender, e);

// B211: OnAskAllDown -- decrement _askBuffer (shared). CYC=1.
private void OnAskAllDown(object sender, RoutedEventArgs e) => OnAskDown(sender, e);

// B211: UpdateAskLabels -- refresh both Ask and Ask-ALL button labels. CYC=1.
private void UpdateAskLabels()
{
    if (_askBtn != null)    _askBtn.Content    = FormatBuffer("Ask",     _askBuffer);
    if (_askAllBtn != null) _askAllBtn.Content = FormatBuffer("Ask-ALL", _askBuffer);
}

// B211: OnBidUp -- increment _bidBuffer, clamp [-10, +20]. CYC=1.
// V4 rationale: lower clamp is -10 per spec WPF template (HTML line 43463). Upper clamp +20 per
// orchestrator prompt direction. Director approved.
private void OnBidUp(object sender, RoutedEventArgs e)
{
    _bidBuffer = Math.Max(Math.Min(_bidBuffer + 1, 20), -10);
    UpdateBidLabels();
}

// B211: OnBidDown -- decrement _bidBuffer, clamp [-10, +20]. CYC=1.
// V4 rationale: lower clamp is -10 per spec WPF template (HTML line 43463). Upper clamp +20 per
// orchestrator prompt direction. Director approved.
private void OnBidDown(object sender, RoutedEventArgs e)
{
    _bidBuffer = Math.Max(Math.Min(_bidBuffer - 1, 20), -10);
    UpdateBidLabels();
}

// B211: OnBidClick -- cancel Working target + resubmit Limit at Bid + buffer. CYC=4.
private void OnBidClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: false, _bidBuffer);
}

// B211: OnBidAllClick -- cancel Working target + resubmit Limit at Bid + buffer for all accounts. CYC=2.
private void OnBidAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    PttMoveTargetAll.Execute(_instrument, useAsk: false, _bidBuffer);
}

// B211: OnBidAllUp -- increment _bidBuffer (shared). CYC=1.
private void OnBidAllUp(object sender, RoutedEventArgs e) => OnBidUp(sender, e);

// B211: OnBidAllDown -- decrement _bidBuffer (shared). CYC=1.
private void OnBidAllDown(object sender, RoutedEventArgs e) => OnBidDown(sender, e);

// B211: UpdateBidLabels -- refresh both Bid and Bid-ALL button labels. CYC=1.
private void UpdateBidLabels()
{
    if (_bidBtn != null)    _bidBtn.Content    = FormatBuffer("Bid",     _bidBuffer);
    if (_bidAllBtn != null) _bidAllBtn.Content = FormatBuffer("Bid-ALL", _bidBuffer);
}
```

**Note on RepeatButton**: The existing BE-row spinners in `BuildBufferedButtonsRow` (line 1132-1145) construct RepeatButton with Content="^"/"v", Width=18, Height=12, no explicit Delay/Interval. The new Ask/Bid spinners use the same construction -- no explicit Delay/Interval, matching the existing pattern exactly.

---

### 2. `src/PropTraderTools/Features/PttMoveTargetOne.cs` (NEW)

**Full class structure**:
```
namespace PropTraderTools
internal static class PttMoveTargetOne
```

**Method signatures** (all internal static, no lock, no throw, ASCII-only):

```csharp
// CYC=6. Spec: cancel Working Limit exit, submit Limit at Ask/Bid + bufferTicks.
// DW-B211-03: target = last Working PTT-* Limit exit in acc.Orders (most recently submitted).
// JS-021: no lock. JS-001: no throw (NT8 calls wrapped in try/catch). JS-033: synchronous void. ASCII-only.
internal static void Execute(Account acc, Instrument instr, bool useAsk, int bufferTicks)

// CYC=6. Computes Ask+buffer or Bid+buffer price. Returns false on no market data.
// Guard: tickSize ??, tickSize<=0, md null, ask/bid null, price<=0.
// JS-021: no lock. JS-001: no throw. ASCII-only.
private static bool TryGetExitPrice(Instrument instr, bool useAsk, int bufferTicks, out double price)

// CYC=6. Scans acc.Orders for last Working Limit exit with PTT- prefix on instrument.
// DW-B211-03: sets target to last match (most recently submitted PTT-* exit). Returns false if none.
// JS-002: TryGet pattern -- no null return. JS-021: acc.Orders.ToList() -- lock-free NT8 read.
// JS-001: no throw. ASCII-only.
private static bool TryFindWorkingTarget(Account acc, Instrument instr, out NinjaTrader.Cbi.Order target)
```

**Atomic sequence counter** (static field):
```csharp
private static int _maSeq = 0; // Interlocked.Increment for PTT-MA-Ask-N / PTT-MA-Bid-N names.
```

**Execute full logic** (CYC=6):
```
(1) if (instr == null) return;
(2) if (!TryGetExitPrice(instr, useAsk, bufferTicks, out double exitPrice)) return;
(3) pos = CopyEngine.Instance.FindPositionPublic(acc, instr); if (pos == null || pos.Quantity == 0) { log "[PTT-MA] no position"; return; }
(4) if (!TryFindWorkingTarget(acc, instr, out var target)) { log "[PTT-MA] no target found for " + acc.Name; return; }
    qty = target.Quantity - target.Filled;   // partial-fill safe (MANDATORY P0)
(5) string prefix = useAsk ? PttOrderNames.MaAskPrefix : PttOrderNames.MaBidPrefix;  // ternary branch
    string orderName = prefix + System.Threading.Interlocked.Increment(ref _maSeq).ToString("D5");
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    OrderAction action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    log "[PTT-MA] " + (useAsk ? "Ask" : "Bid") + ": " + acc.Name + " price=" + exitPrice + " qty=" + qty;
    try
    {
        acc.Cancel(new NinjaTrader.Cbi.Order[] { target });
        var newOrder = acc.CreateOrder(instr, action, OrderType.Limit, OrderEntry.Manual, TimeInForce.Day,
                       qty, exitPrice, 0.0, string.Empty, orderName,
                       NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
        if (newOrder != null) acc.Submit(new[] { newOrder });
    }
    catch (Exception ex)
    {
        Output.Process("[PTT-MA] Execute exception: " + ex.Message, PrintTo.OutputTab1);
    }
```

**TryGetExitPrice full logic** (CYC=6):
```
(1) double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;  // ?? branch
(2) if (tickSize <= 0.0) { price = 0.0; return false; }
(3) var md = instr.MarketData; if (md == null) { price = 0.0; return false; }
(4) double basePrice = useAsk ? (md.Ask?.Price ?? 0.0) : (md.Bid?.Price ?? 0.0);
    // Note: useAsk ternary NOT separately counted -- it is inside the assignment, same branch level
    // The md.Ask?.Price ?? 0.0 adds 1 branch (the ??)
(5) if (basePrice <= 0.0) { price = 0.0; return false; }
    double raw = basePrice + bufferTicks * tickSize;
    price = Math.Round(raw / tickSize) * tickSize;
    return true;
```

Wait -- re-counting TryGetExitPrice CYC:
base=1, ??(1), <=0(1), md==null(1), ternary useAsk(1), ask?.Price??(1), basePrice<=0(1) = CYC=7. Over budget for this helper, but the limit for helpers is 8. Checking spec: CYC target for PttMoveTargetOne.Execute is <= 6. Helpers don't have individual spec CYC limits -- the global limit is <= 8. TryGetExitPrice at CYC=7 is under global limit. ✓

Actually re-examining: the `useAsk ? ... : ...` ternary in TryGetExitPrice is counted as 1 branch. The `md.Ask?.Price ?? 0.0` contains `?.` (1) and `??` (1) = 2 branches. So it may be CYC=8 (at limit). To stay safe, simplify:
```csharp
var side = useAsk ? md.Ask : md.Bid;  // ternary = 1
if (side == null) { price = 0.0; return false; }  // +1
double basePrice = side.Price;
if (basePrice <= 0.0) { price = 0.0; return false; }  // +1
```
This replaces ternary+null-coalescing with ternary+explicit null check. CYC: base(1) + ??(1) + <=0(1) + md==null(1) + useAsk-ternary(1) + side==null(1) + basePrice<=0(1) = CYC=7 (one less than before since removed ?. and ?? on ask access). Under limit. ✓

**TryFindWorkingTarget full logic** (CYC=6) — JS-002 compliant TryGet pattern, no null return:
```
target = default(NinjaTrader.Cbi.Order);
foreach (var o in acc.Orders.ToList())                                                   // (1) foreach
{
    if (o.Instrument?.FullName != instr.FullName || o.OrderState != OrderState.Working)
        continue;                                                                         // (2) instr+state compound
    if (o.OrderType != OrderType.Limit) continue;                                        // (3) type
    if (o.Name == null || !o.Name.StartsWith("PTT-", StringComparison.Ordinal)) continue; // (4) PTT prefix
    if (o.OrderAction != OrderAction.Sell
        && o.OrderAction != OrderAction.BuyToCover) continue;                            // (5) exit action
    target = o;  // take the last matching -- most recently submitted (DW-B211-03)
}
return target != null;
```
CYC = base(1) + foreach(1) + instrAndState compound(1) + type(1) + name/prefix(1) + action(1) = CYC=6. ✓
No null returned from the method itself. Caller checks the bool return.

---

### 3. `src/PropTraderTools/Features/PttMoveTargetAll.cs` (NEW)

**Full class structure**:
```
namespace PropTraderTools
internal static class PttMoveTargetAll
```

**Method signatures**:
```csharp
// CYC=3. Iterates CopyEngine.Instance.AllAccounts(instr) -- DO NOT re-implement.
// Calls PttMoveTargetOne.Execute per account.
// JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.
internal static void Execute(Instrument instrument, bool useAsk, int bufferTicks)
```

**Execute full logic** (CYC=3):
```
(1) if (instrument == null)
    {
        log "[PTT-MA-ALL] instrument null";
        return;
    }
    string side = useAsk ? "Ask" : "Bid";   // no CYC -- this is a label for logging, assigned once
    log "[PTT-MA-ALL] " + side + "-ALL: instr=" + instrument.FullName + " buf=" + bufferTicks + "t";
(2) foreach (Account acc in CopyEngine.Instance.AllAccounts(instrument))
    {
        PttMoveTargetOne.Execute(acc, instrument, useAsk, bufferTicks);
    }
```

Wait -- `useAsk ? "Ask" : "Bid"` is a ternary branch = +1. That makes CYC=4.

To stay at CYC=3: omit the side label in the log (log just "MA-ALL"), or move the ternary into Execute which is called per-account anyway.

Decision: Log the useAsk flag directly as a boolean:
```csharp
log "[PTT-MA-ALL] useAsk=" + useAsk + " instr=" + instrument.FullName + " buf=" + bufferTicks + "t";
```
No ternary in Execute. CYC=3. ✓

---

### 4. `src/PropTraderTools/Core/PttContracts.cs` (MODIFY -- constants only)

Add to `PttOrderNames` static class (after existing `PttBeTargetPrefix`):
```csharp
/// <summary>Move-target Ask order name prefix. "PTT-MA-Ask-"
/// B211: used by PttMoveTargetOne.Execute for Ask-side cancel+resubmit.</summary>
internal const string MaAskPrefix = "PTT-MA-Ask-";

/// <summary>Move-target Bid order name prefix. "PTT-MA-Bid-"
/// B211: used by PttMoveTargetOne.Execute for Bid-side cancel+resubmit.</summary>
internal const string MaBidPrefix = "PTT-MA-Bid-";
```

---

### 5. `src/PropTraderTools/Tests/B211Tests.cs` (NEW)

**Namespace**: `PropTraderTools.Tests`  
**xUnit [Fact] only.** No NUnit, no MSTest.

8 required tests:

```
T1: PttMoveTargetOne_NoTarget_ReturnsNoOp
    Setup: acc with no Working Limit orders, instrument with position.
    Assert: acc.CreateOrder never called (spy/fake or verify via output).

T2: PttMoveTargetOne_NoPosition_ReturnsNoOp
    Setup: acc with Working Limit order, instrument with no position (Quantity==0).
    Assert: acc.Cancel never called.

T3: PttMoveTargetOne_PartialFill_QtyIsQuantityMinusFilled
    Setup: target order with Quantity=5, Filled=2.
    Assert: computed qty == 3 (5 - 2).

T4: PttMoveTargetOne_ZeroFill_QtyIsQuantity
    Setup: target order with Quantity=4, Filled=0.
    Assert: computed qty == 4 (4 - 0).

T5: TryGetExitPrice_AskPlusZeroBuffer_ReturnsAskExactly
    Setup: Ask=100.0, tickSize=0.25, bufferTicks=0, useAsk=true.
    Assert: price == 100.0.

T6: TryGetExitPrice_AskPlusTwoBuffer_ReturnsAskPlusTwoTicks
    Setup: Ask=100.0, tickSize=0.25, bufferTicks=2, useAsk=true.
    Assert: price == 100.5 (100.0 + 2*0.25).

T7: TryGetExitPrice_BidMinusOneBuffer_ReturnsBidMinusOneTick
    Setup: Bid=99.75, tickSize=0.25, bufferTicks=-1, useAsk=false.
    Assert: price == 99.5 (99.75 + (-1)*0.25).

T8: IsBeSwapGtcSweepGuard_ReturnsFalse_ForPttMaAskOrder
    Assert: CopyEngine.IsBeSwapGtcSweepGuard("PTT-MA-Ask-1", true) == false.
    Rationale: PTT-MA-* does NOT start with "PTT-BE-", so FIX-G-2 guard does not protect it.
    This confirms PTT-MA-* orders ARE swept by TryCancelSingleStaleGtcExit when beSwapInProgress.
    (This is correct behaviour -- PTT-MA-* exits should be moveable even during BE swap.)
```

**Note on T1/T2 test structure**: Because PttMoveTargetOne is a static class that directly calls `CopyEngine.Instance.FindPositionPublic()` and `acc.Cancel()/CreateOrder()`, these tests will require either a stub Account/Instrument pattern consistent with the existing B211Tests stub infrastructure OR pure unit tests exercising the logic through extractable pure functions. The engineer must follow the stub pattern established in B209Tests.cs and other test files. If full Account stubbing is impractical, T1/T2 test the no-op log message emission pattern using `Output.Process` capture.

T3 and T4 are pure arithmetic tests: `qty = target.Quantity - target.Filled` can be tested with a simple fake Order stub.

T5-T7 test `TryGetExitPrice` which is a private static method. The engineer should either:
(a) Make it `internal` for testability (preferred), or
(b) Test it indirectly through Execute with a captured CreateOrder call.
The spec mandates these tests exist; engineering must expose the price logic appropriately.

T8 calls the public `CopyEngine.IsBeSwapGtcSweepGuard` static method directly (already internal-accessible per InternalsVisibleTo in CopyEngine.cs).

---

## Data Flow Diagram

```
User Click [Ask +0]
  |
  v
OnAskClick (TradeCopierPanel.cs, UI thread)
  | guard: _instrument null -> return
  | guard: _leaderAccount ?? TryResolveLeaderAccount()
  | guard: _leaderAccount null -> return
  v
PttMoveTargetOne.Execute(acc, instr, useAsk=true, _askBuffer)
  | (1) instr null -> return
  | (2) TryGetExitPrice -> false? return
  |       TryGetExitPrice: tickSize, md.Ask.Price + buffer*tick -> price
  | (3) FindPositionPublic -> null/qty==0? log+return
  | (4) TryFindWorkingTarget -> false? log+return
  |       TryFindWorkingTarget: last PTT-prefixed Working Limit exit on instr (TryGet, no null)
  | (5) qty = target.Quantity - target.Filled
  | (6) orderName = "PTT-MA-Ask-" + seq
  |     action = isLong ? Sell : BuyToCover
  |     try {
  |       acc.Cancel(new Order[]{ target })
  |       newOrder = acc.CreateOrder(instr, action, Limit, Manual, Day, qty, price, 0, "", orderName, ...)
  |       if (newOrder != null) acc.Submit(new[]{ newOrder })
  |     } catch (Exception ex) { Output.Process("[PTT-MA] Execute exception: " + ex.Message, ...); }
  v
NT8 processes cancel + new Limit at Ask price
```

```
User Click [Ask-ALL +0]
  |
  v
OnAskAllClick (TradeCopierPanel.cs, UI thread)
  | guard: _instrument null -> return
  v
PttMoveTargetAll.Execute(instr, useAsk=true, _askBuffer)
  | (1) instrument null -> log+return (already guarded above, defense-in-depth)
  | (2) foreach acc in CopyEngine.Instance.AllAccounts(instr)
  v
  PttMoveTargetOne.Execute(acc, instr, useAsk=true, bufferTicks) -- per account
```

---

## Threading Model

| Operation | Thread | Mechanism |
|-----------|--------|-----------|
| `_askBuffer`/`_bidBuffer` increment/decrement | UI (click handler) | `volatile int` -- no lock needed |
| Button label updates (`.Content = ...`) | UI (same click handler) | Direct -- already on UI thread |
| `acc.Cancel()` / `acc.CreateOrder()` / `acc.Submit()` | UI (click handler) | NT8 AddOnBase API -- UI-thread safe |
| `CopyEngine.Instance.AllAccounts()` | UI (click handler) | Lock-free ConcurrentDictionary iteration |
| `Interlocked.Increment(ref _maSeq)` | UI (atomic) | Lock-free -- JS-023 compliant |

No `Dispatcher.InvokeAsync` required. All operations are initiated from the UI thread click handler.

---

## File Positions Summary

| File | Action | Key Change |
|------|--------|------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFY | Add 2 rows, 4 entries in specs array, 14 handlers |
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | CREATE | Execute + TryGetExitPrice + TryFindWorkingTarget |
| `src/PropTraderTools/Features/PttMoveTargetAll.cs` | CREATE | Execute -- loops AllAccounts |
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFY | Add MaAskPrefix + MaBidPrefix constants |
| `src/PropTraderTools/Tests/B211Tests.cs` | CREATE | 8 xUnit [Fact] tests |

---

## ptt-sync Requirement (downstream engineers)

After Ph4a (.cs edits), engineer MUST run:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected output: N/0 (N linked files, 0 errors).
Result must be included in `ticket-N-completion.md`.

---

## Ph4b Deletion Audit Requirement (downstream verifiers)

Before VERIFY_PASS, run:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"
```
Every deleted symbol must have an authorising DW-card reference. Zero tolerance.

---

## B211 Deferred Items (none at plan time)

No deferred items identified at architecture phase. All design decisions resolved above (DW-B211-03, DW-B211-05).
