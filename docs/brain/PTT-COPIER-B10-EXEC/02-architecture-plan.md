# PTT-COPIER-B10-EXEC Architecture Plan
# Phase 2 output. Written by ptt-architect after 10 mandatory sequentialthinking thoughts.
# Status: REVIEW_PASS_PENDING
# CYCLE 3 — VIOLATION-4 corrected: T4 WPF overlay ATR box added (BuildAtrOverlayRow + UpdateAtrOverlay).

---

## 1. Deferred Item Disposition (Thought 1 Mandatory)

Every open deferred item from docs/brain/PTT-COPIER-B10-UI-01/06-deferred-backlog.md:

| ID | Disposition | Ticket |
|----|-------------|--------|
| DW-B9-GAP-001a | **ADDRESSED BY T1** -- HandleBracketChange now skips follower orders where `order.TrailPrice > 0` (Option B: skip). Calling `acc.Change()` on a trailing stop has undefined effect on the trail watermark (NT8-026). Skip is safer than any modify path. | T1 |
| DW-B9-GAP-001b | **ADDRESSED BY T1** -- MoveStopToBreakEven gets a `TrailPrice > 0` guard and uses `acc.Change()` directly. GAP-001d CONFIRMED 2026-07-09: acc.Change() does NOT kill the trail (log shows Stop1/Stop2 continued firing Auto trail price modification events after acc.Change()). IsStopAlreadyAtBe() helper guards against double-BE submissions. | T1 |
| DW-B9-GAP-001c | **ADDRESSED BY T3** -- TightenTicks field on CopyRule + TightenStop engine method + UI buttons on Panel and Window. T3 uses acc.Change() for fixed stops (no cancel+replace needed -- T3 spec confirms pure acc.Change() path). | T3 |
| DW-B9-GAP-001d | **ADDRESSED BY T1 -- confirmed result adopted** -- GAP-001d CONFIRMED 2026-07-09: acc.Change() does NOT kill the trail. MoveStopToBreakEven uses acc.Change() as the production path. IsStopAlreadyAtBe() helper guards idempotency. | T1 |
| DW-B9-01 | **SHELVED THIS BLOCK** -- ATR box visualization on chart. Explicitly listed in the shelved items list for B10-EXEC. | N/A |
| DW-B9-02 | **ADDRESSED BY T4** -- NT8 chart attachment API investigation (try `chart.NinjaScripts.Add(engine)`, then `chart.Indicators.Add(engine)`, then event-based fallback) PLUS WPF overlay ATR box in ChartTrader panel showing "ATR=N.NN pts -> stopTicks=T -> qty=Q" live via BuildAtrOverlayRow() + UpdateAtrOverlay(string). DW-B9-01 (ATR box on chart canvas, SHELVED) is a different item and remains shelved. | T4 |
| DW-B9-03 | **SHELVED THIS BLOCK** -- Click trader Bid+1/Ask-1 auto-offset. Explicitly listed in the shelved items list for B10-EXEC. | N/A |
| DW-B10-GAP-002a | **ADDRESSED BY T2** -- ArmPendingBe() + OnPendingBeAccountUpdate() subscribing acc.AccountItemUpdate (GAP-002 CONFIRMED 2026-07-09: AccountItemUpdate fires in AddOn context, 10 events observed). Panel three-state visual (inactive/armed/fired). volatile int _pendingBeState: 0=Inactive, 1=Armed. | T2 |
| DW-B10-GAP-002b | **ADDRESSED BY T1+T2** -- T1 implements acc.Change() for trailing stops in MoveStopToBreakEven (GAP-001d confirmed path). T2's ArmPendingBe fires BreakEven() when UnrealizedPnL crosses zero, using the same acc.Change() path. No cancel+replace in either T1 or T2. | T1+T2 |

---

## 2. Component List

### 2.1 CopyEngine.cs (modified)

New fields:
```
private volatile int _pendingBeState = 0;  // 0=Inactive, 1=Armed -- volatile int per spec
private volatile int _pendingBeBufferTicks = 2;
private          Account _pendingBeAccount = null;    // single-writer UI thread
private          Instrument _pendingBeInstrument = null; // single-writer UI thread
```

New methods:
```
internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)
internal void DisarmPendingBe()
private  void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
internal event Action<string> PendingBeFired
```

Modified methods:
```
private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)
  -- adds TrailPrice > 0 guard: uses IsStopAlreadyAtBe() check then acc.Change() for ALL stops
     (trailing and fixed alike -- GAP-001d confirmed trail survives acc.Change())
  -- extracts IsStopAlreadyAtBe helper for idempotency guard

private static bool IsStopAlreadyAtBe(Order order, double newStop, bool isLong)  [NEW -- CYC=2]

private static bool IsTrailingStop(Order order)  [NEW -- CYC=1]

private void HandleBracketChange(Order leaderOrder, CopyRule rule)
  -- adds TrailPrice skip for follower orders via SyncFollowerBracket extraction

private void SyncFollowerBracket(Account acc, Order leaderOrder,
    bool isStop, double newPrice, double tickSize)  [NEW -- private helper, CYC=5]

internal void TightenStop(Instrument instrument, int ticks)  [NEW -- CYC=5]

private static void TightenOneStop(Account acc, Instrument instr, Order order,
    double targetPrice, double tickSize)  [NEW -- private static helper, CYC=4]
```

CopyRule struct changes:
```
internal readonly int TightenTicks;   // default 5
CopyRule.Create factory: new optional param tightenTicks = 5
```

Serialization additions:
```
CopyRuleDto: public int TightenTicks { get; set; } = 0;
RuleToDto: emits rule.TightenTicks
DtoToRule: reads dto.TightenTicks > 0 ? dto.TightenTicks : 5  (backward compat)
```

### 2.2 TradeCopierPanel.cs (modified)

New fields:
```
private Button  _beArmBtn       = null;
private bool    _beArmState     = false;
private TextBox _beArmBufferBox = null;
private Button  _tightenBtn     = null;
private TextBox _tightenTicksBox = null;
```

New methods:
```
private void BuildBeArmRow(StackPanel root)
private void OnBEArmClick(object sender, RoutedEventArgs e)
private void UpdateBEArmVisuals(bool armed)
private void OnPendingBeFiredDispatch(string instr)   [Dispatcher.InvokeAsync wrapper]
private async void FlashBeFired(string instr)         [async void: UI event handler, explicitly allowed]
private void OnTightenStop(object sender, RoutedEventArgs e)
```

Event subscriptions:
```
_engine.PendingBeFired += OnPendingBeFiredDispatch;  (in OnLoaded)
_engine.PendingBeFired -= OnPendingBeFiredDispatch;  (in Detach())
```

### 2.3 TradeCopierWindow.cs (modified)

BuildRuleRow and BuildDynamicRuleRow changes:
```
Col 10 (new ColumnDefinition): Tighten cluster
  -- Button "[~]" (tighten symbol)
  -- TextBox (5, width=28)
  -- TextBlock "tks"
```

New methods:
```
private void OnRuleTightenStop(object sender, RoutedEventArgs e)
```

List tracking:
```
private readonly List<Button> _tightenBtns = new List<Button>();
```
UpdateButtonColors: no change (tighten button is not position-state-colored).

### 2.4 TradeCopierAddOn.cs (modified)

StartAtrEngine: replace the IMPL-NOTE-1 comment stub with the actual chart attachment call.
The investigation order is:
1. `chart.NinjaScripts.Add(engine)` -- try first; if CS1061, go to 2
2. `chart.Indicators.Add(engine)` -- try second; if CS1061, go to 3
3. Event-based fallback: subscribe `chart.BarsArray[0].Bars.BarUpdate += engine.OnBarUpdate` -- always compiles; avoids chart attachment entirely; engine.OnBarUpdate is public in Indicator base class

AtrSizingEngine.cs: if path 3 is chosen, add a `void InvokeBarUpdate()` shim or confirm `OnBarUpdate()` is already accessible from outside via the base class.

WPF overlay (NEW -- VIOLATION-4 fix):

New field:
```
private TextBlock _atrOverlayLabel = null;  // ChartTrader panel overlay label
```

New methods:
```
private void BuildAtrOverlayRow(Panel chartTraderRoot)
   -- called once during StartAtrEngine, after successful chart attach
   -- creates Border + TextBlock, injects into ChartTrader panel Grid
   -- plain ASCII initial text: "ATR=-.-- pts -> stopTicks=-- -> qty=--"

internal void UpdateAtrOverlay(string atrDisplay)
   -- updates _atrOverlayLabel.Text via Dispatcher.InvokeAsync
   -- display format: "ATR=N.NN pts -> stopTicks=T -> qty=Q"
   -- called via OnAtrUpdated callback from AtrSizingEngine.AtrUpdated event
```

AtrSizingEngine.cs additions:
```
internal event Action<string> AtrUpdated;  // fires formatted display string after each bar update
```
OnBarUpdate / ManualOnBarUpdate: after computing ATR, format string and fire AtrUpdated:
```
string display = string.Format("ATR={0:F2} pts -> stopTicks={1} -> qty={2}", atrPts, stopTicks, qty);
AtrUpdated?.Invoke(display);
```

Data source for stopTicks and qty: AtrSizingEngine already holds _riskDollars, _pointValue; stopTicks = (int)Math.Round(atr * _pointValue / _tickValue / _riskDollars * ...) per existing logic. qty is derived from the same ATR sizing calc.

NOTE: DW-B9-01 (ATR box visualization drawn directly on chart canvas) remains SHELVED this block. The overlay added here is a ChartTrader PANEL text display -- a different item at a different UI layer.

---

## 3. Class Signatures

### 3.1 CopyEngine.cs -- new and modified signatures

```csharp
// New event
internal event Action<string> PendingBeFired;

// New: arm pending break-even price watcher using acc.AccountItemUpdate
// CYC=4: instr null(1), acc null(2), pos flat(3), AccountItemUpdate subscribe(4)
internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)

// New: disarm pending break-even
// CYC=3: armed check(1), acc null(2), unsubscribe(3)
internal void DisarmPendingBe()

// New: AccountItemUpdate callback -- fires on NT8 account background thread
// CYC=5: state check(1), item type filter(2), pnl threshold(3), CAS disarm(4), fire(5)
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)

// New: trailing stop detection predicate
// CYC=1: single return
private static bool IsTrailingStop(Order order)

// New: idempotency guard -- stop already at or past BE level
// CYC=2: long branch(1), short branch(2)
private static bool IsStopAlreadyAtBe(Order order, double newStop, bool isLong)

// New: sync one follower bracket order (extracted from HandleBracketChange inner loop)
// CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop(4), try block(0)
private void SyncFollowerBracket(Account acc, Order leaderOrder,
    bool isStop, double newPrice, double tickSize)

// New: move all working stops to currentPrice +/- N ticks
// CYC=5: rule null(1), foreach acc(2), pos flat(3), foreach orders(4), stop type(5)
internal void TightenStop(Instrument instrument, int ticks)

// New: apply tighten to one stop order
// CYC=4: null guard(1), alreadyTighter(2), TrailPrice>0(3), try block(0)
private static void TightenOneStop(Account acc, Instrument instr,
    Order order, double targetPrice, double tickSize)

// Modified: MoveStopToBreakEven -- adds IsStopAlreadyAtBe() guard; uses acc.Change() for ALL
//   stop types (trailing + fixed). GAP-001d confirmed trail survives acc.Change().
// CYC=6: IsFlat(1), tickSize guard(2), foreach(3), working(4), stop type(5), isStopLeg(6)
private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)

// Modified: HandleBracketChange -- delegates inner loop body to SyncFollowerBracket
// CYC=6: isStop(1), instr null(2), tickSize(3), rawPrice(4), foreach acc(5), acc null(6)
private void HandleBracketChange(Order leaderOrder, CopyRule rule)
```

### 3.2 TradeCopierPanel.cs -- new and modified signatures

```csharp
// New: builds "Arm BE" row -- arm/disarm button + buffer ticks textbox
// CYC=1: straight-line widget construction
private void BuildBeArmRow(StackPanel root)

// New: toggles pending BE arm state, calls ArmPendingBe or DisarmPendingBe
// CYC=3: instrument null(1), account null(2), armed toggle(3)
private void OnBEArmClick(object sender, RoutedEventArgs e)

// New: updates _beArmBtn background for 3 states (inactive/armed/fired)
// CYC=2: null guard(1), state ternary(2)
private void UpdateBEArmVisuals(bool armed)

// New: Dispatcher.InvokeAsync wrapper -- called on account background thread from engine PendingBeFired
// CYC=1: straight-line InvokeAsync
private void OnPendingBeFiredDispatch(string instr)

// New: flash green on BE fire, then revert -- async void: UI event handler (explicitly allowed)
// CYC=2: instrument filter(1), await Task.Delay(0, just for scheduling)
private async void FlashBeFired(string instr)

// New: tighten stop button click handler
// CYC=3: instrument null(1), parse error(2), engine call(3)
private void OnTightenStop(object sender, RoutedEventArgs e)
```

### 3.3 TradeCopierWindow.cs -- new and modified signatures

```csharp
// New: tighten stop click handler for rule rows
// CYC=4: tag null(1), name empty(2), parse guard(3), engine call(4)
private void OnRuleTightenStop(object sender, RoutedEventArgs e)
```

### 3.4 TradeCopierAddOn.cs -- modified and new signatures

```csharp
// Modified: StartAtrEngine -- replaces IMPL-NOTE-1 comment stub with actual attachment logic
//   + calls BuildAtrOverlayRow() after successful attach + subscribes AtrUpdated
// CYC=4: chart null(1), instr null(2), attachment try(3), fallback(4)
// NOTE: instance method (not static) -- accesses _atrOverlayLabel field and calls
//       BuildAtrOverlayRow() and ResolveChartTraderPanel() which are instance methods.
private void StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)

// New: builds WPF overlay row and injects into ChartTrader panel -- called once during attach
// CYC=1: straight-line widget construction; no branches
private void BuildAtrOverlayRow(Panel chartTraderRoot)

// New: updates _atrOverlayLabel.Text via Dispatcher.InvokeAsync from AtrUpdated callback
// CYC=2: null guard on _atrOverlayLabel(1), Dispatcher.InvokeAsync update(2)
internal void UpdateAtrOverlay(string atrDisplay)
```

CYC delta accounted per spec (+3 total): BuildAtrOverlayRow(1) + UpdateAtrOverlay(2) + attach call (+0 incremental, already in StartAtrEngine CYC=4). Both new methods are CYC <= 8. ✅

---

## 4. Data Flow

### 4.1 T1 -- Break Even with Trailing Stop Support

```
[UI thread] User clicks [BE] button
  Panel.OnBreakEven / Window.OnRuleBreakEven
  CopyEngine.BreakEven(instrument, bufferTicks)
    foreach acc in AllAccounts(instrument):
      MoveStopToBreakEven(acc, instrument, bufferTicks)
        FindPosition(acc, instrument) -> null guard (IsFlat)
        calc tickSize, direction, newStop
        foreach order in acc.Orders:
          instrument / working / StopMarket / isStopLeg checks
          [NEW] if IsStopAlreadyAtBe(order, newStop, isLong) -> skip (idempotency guard)
          order.StopPrice = newStop
          acc.Change(new Order[] { order })          // single path: works for both trailing
                                                     // and fixed stops (GAP-001d confirmed)

[UI thread] HandleBracketChange inner loop (Mode 2 / bracket sync)
  [NEW] SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize)
    FindFollowerBracketOrder(acc, ...)  -> fo
    if fo == null -> skip
    if price delta < tickSize -> skip
    [NEW] if (IsTrailingStop(fo)) -> StatusUpdate "trailing skip" -> skip
    isStop ? fo.StopPrice = newPrice : fo.LimitPrice = newPrice
    acc.Change(new Order[] { fo })
```

### 4.2 T2 -- Pending Break Even Price Watcher

```
[UI thread] User clicks [Arm BE] button
  Panel.OnBEArmClick
    CopyEngine.ArmPendingBe(instrument, masterAcc, bufferTicks)
      FindPosition(masterAcc, instrument) -> null guard (IsFlat check)
      _pendingBeBufferTicks = bufferTicks  [volatile int write]
      _pendingBeInstrument = instrument    [plain ref write, UI thread]
      _pendingBeAccount    = masterAcc     [plain ref write, UI thread]
      subscribe masterAcc.AccountItemUpdate += OnPendingBeAccountUpdate
      _pendingBeState = 1                  [volatile int write: Armed]
    Panel.UpdateBEArmVisuals(armed=true)   -> _beArmBtn.Background = BrushCaution (amber)

[NT8 account background thread] NT8 fires AccountItemUpdate
  CopyEngine.OnPendingBeAccountUpdate(sender, e)
    if (_pendingBeState != 1) return           [volatile int read -- fast exit if not Armed]
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss) return  [filter: PnL events only]
    bool triggered = (e.Value >= 0)            [UnrealizedPnL >= 0: position at or past breakeven]
    if (!triggered) return
    // CAS disarm: Interlocked.CompareExchange ensures only one callback wins the disarm race
    if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1) return  [DISARM FIRST]
    var acc  = _pendingBeAccount
    var instr = _pendingBeInstrument
    var buf  = _pendingBeBufferTicks
    if (acc != null)
      acc.AccountItemUpdate -= OnPendingBeAccountUpdate  [unsubscribe]
    _pendingBeAccount     = null
    _pendingBeInstrument  = null
    BreakEven(instr, buf)                                [fire BE -- acc.Change() path]
    PendingBeFired?.Invoke(instr?.FullName ?? string.Empty)  [notify UI]

[Panel UI thread -- via Dispatcher.InvokeAsync from OnPendingBeFiredDispatch]
  FlashBeFired(instr)  [async void -- UI event handler, explicitly allowed]
    if instrument mismatch -> return
    _beArmBtn.Background = BrushActive    [green]
    await Task.Delay(800)
    _beArmBtn.Background = BrushInactive  [grey]
    _beArmState = false
```

### 4.3 T3 -- Tighten Stop

```
[UI thread] User clicks [~] (tighten) button
  Panel.OnTightenStop / Window.OnRuleTightenStop
    read ticks from TextBox (int, default 5)
    CopyEngine.TightenStop(instrument, ticks)
      rule = FindRule(instrument) -> null guard
      currentPrice = instrument.MarketData?.Bid or Ask   [see Section 5.3 for details]
      tickSize = instrument.MasterInstrument.TickSize
      foreach acc in AllAccounts(instrument):
        pos = FindPosition(acc, instrument)
        if IsFlat(pos) -> skip
        isLong = (pos.MarketPosition == Long)
        targetPrice = isLong
            ? currentPrice - ticks * tickSize  [move stop up toward price, long]
            : currentPrice + ticks * tickSize  [move stop down toward price, short]
        foreach order in acc.Orders:
          if (order.OrderState != Working) continue
          if (order.OrderType != StopMarket && order.OrderType != StopLimit) continue
          if (!IsStopLeg(order)) continue
          TightenOneStop(acc, instrument, order, targetPrice, tickSize)
            alreadyTighter = isLong ? order.StopPrice >= targetPrice
                                    : order.StopPrice <= targetPrice
            if (alreadyTighter) continue
            if (order.TrailPrice > 0):
              acc.Cancel(new Order[] { order })
              acc.CreateOrder(instr, orderAction, StopMarket, OrderEntry.Manual,
                  TimeInForce.Day, order.Quantity, 0, targetPrice, null,
                  "PTT-Tighten-Stop", DateTime.MaxValue, (CustomOrder)null)
            else:
              order.StopPrice = targetPrice
              acc.Change(new Order[] { order })
```

### 4.4 T4 -- AtrSizingEngine Chart Attachment + WPF Overlay

```
[UI thread] TradeCopierAddOn.StartAtrEngine(chart, instr)
  engine = new AtrSizingEngine()
  engine.SetParameters(150.0, pointValue)
  _atrEngines[chart] = engine

  [INVESTIGATION STEP 1] try: chart.NinjaScripts.Add(engine)
    -> if compiles and works at runtime: attachment DONE; proceed to overlay creation
  [INVESTIGATION STEP 2] try: chart.Indicators.Add(engine)
    -> if compiles and works at runtime: attachment DONE; proceed to overlay creation
  [INVESTIGATION STEP 3 -- ALWAYS COMPILES] event-based fallback:
    chart.BarsArray[0].Bars.BarUpdate += (_, _) => engine.ManualOnBarUpdate()
    where ManualOnBarUpdate() calls ATR(Period)[0] internally
    -> attachment DONE via event subscription; proceed to overlay creation

  CopyEngine.Instance.SetAtrEngine(engine, enabled: false)

  [WPF OVERLAY CREATION -- called once after any successful attachment path]
  chartTraderRoot = ResolveChartTraderPanel(chart)  // locate ChartTrader Grid/StackPanel
  if (chartTraderRoot != null):
    BuildAtrOverlayRow(chartTraderRoot)             // inject Border+TextBlock into panel
    engine.AtrUpdated += OnAtrUpdated               // subscribe to live updates

  [Update NT8_ADDON_KNOWLEDGE.md with which attachment path worked]

[AtrSizingEngine background / bar-close thread] engine.OnBarUpdate / engine.ManualOnBarUpdate
  compute ATR, stopTicks, qty
  display = string.Format("ATR={0:F2} pts -> stopTicks={1} -> qty={2}", atrPts, stopTicks, qty)
  AtrUpdated?.Invoke(display)                       // fires formatted string

[UI thread via Dispatcher.InvokeAsync]
  TradeCopierAddOn.OnAtrUpdated(string display)     // subscribed handler
    UpdateAtrOverlay(display)
      if (_atrOverlayLabel == null) return           // null guard
      Dispatcher.InvokeAsync(() => _atrOverlayLabel.Text = display)
```

Note: `ResolveChartTraderPanel(chart)` is a helper that traverses the chart's visual tree to
locate the ChartTrader control's root Panel. If not found (null), overlay creation is skipped
gracefully -- the attachment still proceeds and the engine fires bar updates normally.

---

## 5. Design Decisions

### 5.1 GAP-001d Verdict Adopted: acc.Change() is the Production Path

GAP-001d CONFIRMED 2026-07-09: `acc.Change()` does NOT kill the trail. The Sim101 log showed
Stop1/Stop2 continued firing `Auto trail price modification` events after `acc.Change()` was
called (price sequence: 7589.25 → 7586.75 → 7585.75). The trail watermark was preserved.

MoveStopToBreakEven uses `acc.Change()` as the production path for ALL stop types (trailing
and fixed). This is the simpler, lower-latency path. No cancel+replace needed.

`IsStopAlreadyAtBe(order, newStop, isLong)` guards idempotency: if the stop is already at or
past the breakeven level, skip the `acc.Change()` call. This helper replaces the inline guard
that was previously part of `ApplyBreakEvenToOrder`. `ApplyBreakEvenToOrder` is removed from
scope -- it was designed for the (now superseded) cancel+replace path.

The "PTT-BE-Stop" CreateOrder signal is removed from T1 scope entirely.

### 5.2 GAP-002 Verdict Adopted: AccountItemUpdate is the Production Path

GAP-002 CONFIRMED 2026-07-09: `acc.AccountItemUpdate` (UnrealizedPnL) fired 10 times in AddOn
context. This is the confirmed price-proxy event for the pending BE watcher.

The trigger condition is `e.Value >= 0` (UnrealizedPnL >= 0) meaning the position has reached
or crossed breakeven. This is the spec-described comparison (spec line 2643:
"compare current price vs avg entry price, trigger BE when entry price crosses").

No `_pendingBePrice` / `_pendingBeLong` / `_pendingBeInstrument-for-price-comparison` fields
are needed. The AccountItemUpdate event already encodes whether price has moved past entry:
a non-negative UnrealizedPnL means the position is at or above breakeven.

**Handler signature** (confirmed from existing AddOn code at TradeCopierAddOn.cs:483):
```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
```

**Threading**: `AccountItemUpdate` fires on the NT8 account background thread -- NOT the UI
thread. `Dispatcher.InvokeAsync` is still required for any UI update (same as the existing
`OnAccountItemUpdate` in TradeCopierPanel.cs:253 which calls `Dispatcher.InvokeAsync`).
`Interlocked.CompareExchange` is used to ensure only one concurrent callback wins the disarm
race (eliminates the double-trigger risk on rapid PnL events).

Section 5.2 Option A / Option B discussion is removed -- GAP-002 confirmed Option B
(AccountItemUpdate). There is no Option A fallback needed.

### 5.3 TightenStop Current Price Source

Preferred: `instrument.MarketData.Bid` (short) / `instrument.MarketData.Ask` (long)
- Available via the NT8 instrument object
- Read on UI thread (called from button handler)

Fallback: use position AveragePrice as a conservative reference if MarketData is null/0.

```csharp
// In TightenStop:
double bid = instrument.MarketData?.Bid ?? 0;
double ask = instrument.MarketData?.Ask ?? 0;
double currentPrice = bid > 0 && ask > 0
    ? (isLong ? ask : bid)
    : pos.AveragePrice;  // fallback: use entry price if market data unavailable
```

### 5.4 _pendingBeState Threading (volatile int)

`_pendingBeState` is `volatile int` per spec (line 2640): 0=Inactive, 1=Armed.

Write on UI thread in `ArmPendingBe()`. Read on account background thread in
`OnPendingBeAccountUpdate()`. The `volatile` keyword ensures visibility across threads
without a full memory barrier.

Disarm uses `Interlocked.CompareExchange(ref _pendingBeState, 0, 1)` rather than a plain
volatile write. This ensures exactly one concurrent callback wins the Armed→Inactive
transition -- safe even if NT8 dispatches multiple `AccountItemUpdate` callbacks
concurrently from its thread pool.

Fields `_pendingBeAccount` and `_pendingBeInstrument` are plain references written once on
the UI thread before `_pendingBeState` is set to Armed. The volatile store on `_pendingBeState`
provides the release fence -- any thread reading `_pendingBeState == 1` will also observe the
writes to `_pendingBeAccount` and `_pendingBeInstrument` (x64 TSO + volatile release semantics).

Pattern precedent: `AtrSizingEngine._lastAtr` (plain double, non-volatile, same release-fence
rationale). Same model applied here for the reference fields.

### 5.5 TightenTicks Serialization Backward Compat

Old XML files (B9 and earlier) have no `TightenTicks` element. XmlSerializer sets the
property to 0 on deserialization when the element is absent. `DtoToRule` converts:
```csharp
tightenTicks = dto.TightenTicks > 0 ? dto.TightenTicks : 5
```
Default 5 ticks is a reasonable conservative default.

### 5.6 FlashBeFired (async void)

`FlashBeFired` is declared `async void` because:
1. It is a UI event handler invoked via `Dispatcher.InvokeAsync` (not a library method)
2. The task mandate explicitly states: "No async void except FlashBeFired (UI event handler -- explicitly allowed)"
3. Pattern: set background, await Task.Delay(800ms), revert background

The 800ms flash duration is intentionally short. No cancellation token is needed -- the
flash is fire-and-forget; if the window closes during the flash, the GC handles cleanup.

---

## 6. NinjaTrader 8 API Usage

| API | Method | Notes |
|-----|--------|-------|
| `acc.Change(Order[])` | T1, T2, T3 | Move stop price. Set `order.StopPrice` first. Used for ALL stop types in T1/T2 (GAP-001d confirmed trail survives). |
| `acc.Cancel(Order[])` | T3 only | Cancel trailing stop before replacement in TightenStop. NOT used in T1 or T2. |
| `acc.CreateOrder(...)` | T3 only | 12-arg form. Arg 12 = `(NinjaTrader.Cbi.CustomOrder)null` (NT8-007). Signal name: "PTT-Tighten-Stop". |
| `order.TrailPrice` | T1, T2, T3 | > 0 = trailing stop. Confirmed fact (NT8-026). |
| `order.TrailPrice` | T1 HandleBracketChange | Skip follower orders where `order.TrailPrice > 0`. |
| `acc.AccountItemUpdate` | T2 | Subscribe/unsubscribe on masterAcc. Callback fires on NT8 account background thread. `AccountItemEventArgs.AccountItem == AccountItem.UnrealizedProfitLoss` filter. |
| `instrument.MarketData.Bid/Ask` | T3 | Read on UI thread for current price in TightenStop. |
| `chart.NinjaScripts.Add(engine)` | T4 | First candidate for AtrSizingEngine attachment. Try first. |
| `chart.Indicators.Add(engine)` | T4 | Second candidate. Try if NinjaScripts fails. |
| `Account.All` | T1, T2, T3 | Existing pattern -- only in Loaded handlers, never constructors. |
| `Interlocked.CompareExchange` | T2 | CAS disarm of `_pendingBeState` in OnPendingBeAccountUpdate to prevent double-trigger. |

---

## 7. Threading Model

| Field | Type | Thread written | Thread read | Mechanism |
|-------|------|---------------|-------------|-----------|
| `_pendingBeState` | `volatile int` | UI (ArmPendingBe) | Account bg (AccountItemUpdate cb) | volatile read/write + Interlocked CAS for disarm |
| `_pendingBeBufferTicks` | `volatile int` | UI | Account bg | volatile |
| `_pendingBeAccount` | `Account` (ref) | UI | Account bg | single-writer UI; volatile int fence on _pendingBeState |
| `_pendingBeInstrument` | `Instrument` (ref) | UI | Account bg | single-writer UI; volatile int fence on _pendingBeState |
| `_beArmState` | `bool` | UI | UI only | plain bool -- UI thread only |
| `_beArmBtn` | `Button` | UI | UI only | plain field -- UI thread only |
| `CopyRule.TightenTicks` | `int` (readonly struct field) | ConcurrentBag rebuild | ConcurrentBag iterate | ConcurrentBag rebuild pattern -- no lock |

No lock() anywhere. All new state follows existing project patterns. ✅

NT8 order calls (`acc.Change`, `acc.Cancel`, `acc.CreateOrder`) are safe from background
threads -- confirmed by existing usage in `OnOrderUpdate` which fires on the NT8 order
event background thread and calls `SendCopy` --> `acc.CreateOrder()` directly.

AccountItemUpdate fires on the NT8 account background thread (same as existing
`OnAccountItemUpdate` in TradeCopierPanel.cs). `Dispatcher.InvokeAsync` is used for all
UI updates triggered by this callback (via `OnPendingBeFiredDispatch`).

---

## 8. File Split (Zero Cross-Contamination)

| Ticket | Files Modified | Purpose |
|--------|---------------|---------|
| T1 | `CopyEngine.cs` only | Trailing stop detection + acc.Change() BE/bracket paths + IsStopAlreadyAtBe + IsTrailingStop helpers |
| T2 | `CopyEngine.cs` + `TradeCopierPanel.cs` | Pending BE arm/watcher (AccountItemUpdate) + Panel 3-state visual |
| T3 | `CopyEngine.cs` + `TradeCopierPanel.cs` + `TradeCopierWindow.cs` | TightenStop feature across all surfaces |
| T4 | `TradeCopierAddOn.cs` + `AtrSizingEngine.cs` | Chart attachment investigation + WPF overlay ATR box (BuildAtrOverlayRow + UpdateAtrOverlay) |

No ticket modifies a file that another ticket has exclusive ownership of at the same time.
Recommended execution order: T1 first (no UI dependencies), then T2 (builds on T1's BE path),
then T3 (independent), then T4 (independent).

---

## 9. Jane Street / NT8 Rules Pre-flight

| Rule | Check | Status |
|------|-------|--------|
| JS-021 no lock() | Zero lock() in all new/modified methods. ConcurrentBag rebuild pattern (existing). Interlocked.CompareExchange used in T2 (lock-free). | PASS |
| JS-033 no async void | Only FlashBeFired -- explicitly allowed by task mandate as UI event handler. | PASS |
| JS-002 no return null | All new helpers use void or return bool/int. Nullable Order? contract unchanged. | PASS |
| JS-001 no throw in hot path | All acc.Change/Cancel/CreateOrder calls wrapped in try/catch that logs via StatusUpdate. | PASS |
| NT8-003 no volatile double | No volatile double anywhere. _pendingBeAccount and _pendingBeInstrument are plain refs (not volatile). _pendingBeState is volatile int (correct). | PASS |
| NT8-007 arg 12 (CustomOrder)null | All new CreateOrder calls use (NinjaTrader.Cbi.CustomOrder)null. T3 only (PTT-Tighten-Stop). | PASS |
| CYC <= 8 all methods | MoveStopToBreakEven(6), HandleBracketChange(6), SyncFollowerBracket(5), IsStopAlreadyAtBe(2), IsTrailingStop(1), ArmPendingBe(4), DisarmPendingBe(3), OnPendingBeAccountUpdate(5), TightenStop(5), TightenOneStop(4), FlashBeFired(2), OnBEArmClick(3), OnTightenStop(3), OnRuleTightenStop(4), BuildAtrOverlayRow(1), UpdateAtrOverlay(2). All <= 8. | PASS |
| ASCII-only strings | New literals: "PTT-Tighten-Stop", "Arm BE", "Tighten", "tks", "0 selected", "armed", "trailing skip", etc. All ASCII. "PTT-BE-Stop" removed (T1 no longer uses cancel+replace). | PASS |
| PTT- prefix on signal names | "PTT-Tighten-Stop" (T3 trailing cancel+replace). T1 and T2 use acc.Change() -- no CreateOrder signal needed. | PASS |
| Math.Clamp ban (.NET 4.8) | TightenTicks clamp: `Math.Max(1, Math.Min(500, ticks))`. | PASS |
| no { get; init; } | TightenTicks = readonly field on readonly struct. | PASS |
| NTButtonStyle policy | Color-coded buttons skip NTButtonStyle. Non-color utility buttons use NTButtonStyle. | PASS |
| No abstract record | No new record types. | PASS |
| No ImmutableDictionary | Not used anywhere. | PASS |
| No FontFamily | No new FontFamily. | PASS |
| No hex colors | New buttons use existing MakeBrush statics (BrushActive/BrushCaution/BrushInactive). | PASS |

---

## 10. xUnit Test Inventory (21 tests)

### T1 Tests (CopyEngineTests.cs)

```
[Fact] MoveStopToBreakEven_TrailingStop_ChangesStopViaChange
  -- order.TrailPrice = 2.0: verify acc.Change called with updated StopPrice
     (GAP-001d confirmed: no cancel+replace needed; trail survives acc.Change())

[Fact] MoveStopToBreakEven_FixedStop_ChangesPrice
  -- order.TrailPrice = 0: verify acc.Change called with updated StopPrice (existing path preserved)

[Fact] MoveStopToBreakEven_StopAlreadyAtBe_Skips
  -- long position, stop already >= newStop: acc.Change NOT called (IsStopAlreadyAtBe guard)

[Fact] HandleBracketChange_FollowerTrailingStop_Skips
  -- FindFollowerBracketOrder returns order with TrailPrice > 0: acc.Change NOT called

[Fact] IsTrailingStop_PositiveTrailPrice_ReturnsTrue

[Fact] IsTrailingStop_ZeroTrailPrice_ReturnsFalse
```

### T2 Tests (CopyEngineTests.cs)

```
[Fact] ArmPendingBe_SetsStateArmed
  -- after ArmPendingBe, _pendingBeState (via reflection seam or testable property) equals 1

[Fact] DisarmPendingBe_ClearsArmedState
  -- after DisarmPendingBe, _pendingBeState equals 0

[Fact] OnPendingBeAccountUpdate_NotArmed_NoEvent
  -- _pendingBeState = 0: PendingBeFired not invoked

[Fact] OnPendingBeAccountUpdate_UnrealizedPnlPositive_FiresPendingBeFired
  -- simulate AccountItemEventArgs with AccountItem.UnrealizedProfitLoss, Value=+10.0: event fires once

[Fact] OnPendingBeAccountUpdate_UnrealizedPnlNegative_DoesNotFire
  -- simulate AccountItemEventArgs with Value=-5.0: event does NOT fire

[Fact] OnPendingBeAccountUpdate_TriggeredOnce_DisarmsBeforeFiring
  -- verify _pendingBeState == 0 BEFORE PendingBeFired fires (CAS ordering)
```

### T3 Tests (CopyEngineTests.cs)

```
[Fact] TightenStop_LongPosition_MovesStopToTargetPrice
  -- fixed stop, long position: acc.Change called with stop = targetPrice

[Fact] TightenStop_ShortPosition_MovesStopToTargetPrice

[Fact] TightenStop_TrailingStop_CancelsAndReplaces
  -- order.TrailPrice > 0: Cancel + CreateOrder("PTT-Tighten-Stop")

[Fact] TightenStop_StopAlreadyTighter_Skips
  -- long: stop >= targetPrice already -> no Change, no Cancel

[Fact] TightenStop_FlatPosition_Skips
  -- no position -> no action taken

[Fact] CopyRule_TightenTicks_SerializesAndDeserializes
  -- round-trip: SaveRules writes TightenTicks=10, LoadRules reads 10

[Fact] CopyRule_TightenTicks_OldXmlBackwardCompat
  -- XML without TightenTicks element: DtoToRule returns rule.TightenTicks = 5 (default)
```

### T4 Tests (CopyEngineTests.cs or TradeCopierAddOnTests.cs)

```
[Fact] StartAtrEngine_NullChart_DoesNotThrow
  -- StartAtrEngine(null, ...) returns safely

[Fact] StartAtrEngine_NullInstrument_DoesNotThrow
  -- StartAtrEngine(chart, null) returns safely

[Fact] UpdateAtrOverlay_FormatsDisplayString_CorrectText
  -- Arrange: create TradeCopierAddOn instance, call BuildAtrOverlayRow to init _atrOverlayLabel
     (use test seam: expose _atrOverlayLabel via internal or constructor injection)
  -- Act: call UpdateAtrOverlay("ATR=1.25 pts -> stopTicks=5 -> qty=2") synchronously
     (or verify the string passed to Dispatcher matches format)
  -- Assert: _atrOverlayLabel.Text == "ATR=1.25 pts -> stopTicks=5 -> qty=2"
  -- Verifies format: "ATR=N.NN pts -> stopTicks=T -> qty=Q"
```

**Total: 22 [Fact] tests.**
Note: T4 attachment-API tests cannot exercise NT8 chart runtime without a real NT8 instance.
The 3 T4 tests cover null-guard safety (2 facts) and overlay display string formatting (1 fact).

---

## 11. Diag Row Disposition

The `BuildDiagRow` / `OnDiagGap001d` / `OnDiagGap002` code in `TradeCopierPanel.cs` and
`TradeCopierAddOn.cs` (RunGap001dTest, RunGap002Test) was introduced in B9 as temporary
test scaffolding.

**This block**: Do NOT remove. The diag row remains useful for T4 verification and for
any future GAP investigation. The comment in the source already says "REMOVE AFTER TESTS"
but since T4 is still actively using RunGap002Test, removal is deferred to B11.

---

## 12. SCAN-01 through SCAN-07 Checklist Template (for 04-tickets.md)

Applies to all 4 tickets:

- SCAN-01: No lock() in any new/modified method.
- SCAN-02: CYC count verified <= 8 for every new/modified method.
- SCAN-03: ASCII-only in all new string literals.
- SCAN-04: No DateTime.Now -- use DateTime.UtcNow if time logging needed.
- SCAN-05: All CreateOrder signal names start with "PTT-".
- SCAN-06: No FontFamily, no hardcoded hex colors, no Math.Clamp.
- SCAN-07: Dispatcher.InvokeAsync used for all UI updates from background threads.
