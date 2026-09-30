# PTT-COPIER-B12 Architecture Plan
# Block: PTT-COPIER-B12
# Date: 2026-07-11
# Author: ptt-architect (Phase 1)
# Status: PLAN_COMPLETE

---

## §1 Block Overview

| Property | Value |
|----------|-------|
| Block | PTT-COPIER-B12 |
| Tickets | 3 (T1, T2, T3) |
| Files touched | TradeCopierPanel.cs, CopyEngine.cs, AtrSizingEngine.cs |
| Backlog closed | DW-B11-DEFER-01 (Flatten/Trim limit orders) |
| Backlog shelved | DW-B9-01 (ATR box visualization), DW-B9-03 (click trader offset) — carry to B13 |
| Jane Street gate | PASS (all P0 rules checked in §5) |
| CYC gate | PASS (all methods <= 8, tallied in §6) |
| NT8 gate | PASS (volatile banned fields confirmed plain; Math.Clamp banned, using Math.Max/Min) |

### Shelved — NOT in B12

The following are explicitly out of scope:
- Buy Ask / Sell Bid quick-entry buttons (B13)
- Full-panel mode
- Auto-trail stop from BE level
- DW-B9-01 (ATR box on chart canvas) — carried to B13
- DW-B9-03 (click trader offset) — carried to B13
- B13-B16 forward roadmap items

---

## §2 Component List

| Component | File | Change Type |
|-----------|------|-------------|
| TradeCopierPanel | TradeCopierPanel.cs | Modify — major UI restructure |
| CopyEngine | CopyEngine.cs | Modify — 2 new overloads + 1 gate + 2 pass-through methods |
| AtrSizingEngine | AtrSizingEngine.cs | Modify — add _atrFraction field + SetAtrFraction() |

---

## §3 Class Names and Field Inventory

### 3.1 TradeCopierPanel — New Fields

All fields are UI-thread-only. No volatile on any field (NT8-003 ban; UI-thread-only contract).

```csharp
// T1 — Buffered buttons (plain int; UI-thread-only)
private int  _trimBuffer     = 1;
private int  _flattenBuffer  = 1;
private int  _beBuffer       = 1;

// T1 — BE 3-state FSM
private BeState _beState = BeState.Idle;

// T1 — Button references for buffered section
private Button _trimBtn2;          // replaces old _trimBtn (same semantic name OK)
private Button _flattenBtn2;       // replaces old _flattenBtn
private Button _beBtn2;            // replaces old _beBtn (3-state border applied here)
private Button _cancelBtn2;        // replaces old _cancelBtn
private Button _copyToggleBtn2;    // full-width toggle (replaces old _copyToggleBtn)

// T2 — Collapse state (plain bool; UI-thread-only)
private bool       _isCollapsed  = false;
private Button     _collapseToggleBtn;
private StackPanel _contentPanel;

// T3 — Risk/ATR spinners (plain double; UI-thread-only; no volatile per NT8-003)
private double  _maxRiskDollars = 200.0;
private double  _atrFraction    = 0.75;
private TextBox _riskDollarsBox;
private TextBox _atrFractionBox;

// T1 — Frozen semantic brush (blue for BE CONNECTED border)
private static readonly SolidColorBrush BrushConnected = MakeBrush(59, 130, 246);  // #3b82f6
```

### 3.2 TradeCopierPanel — Removed Fields (superseded by T1)

The following fields from B10 T2 are REMOVED in T1 because BuildBeArmRow and its 2-state
Arm BE button are replaced by the 3-state BE in the buffered buttons section:

```
_beArmBtn          (Button)    — removed
_beArmState        (bool)      — removed
_beArmBufferBox    (TextBox)   — removed
```

The existing _beBufferBox (TextBox for the old BE ticks input in the original actionGrid)
is also removed; the new _beBuffer (int) is driven by RepeatButtons.

The old `_trimBtn`, `_flattenBtn`, `_cancelBtn`, `_beBtn`, `_beBufferBox`, `_copyToggleBtn`
are reassigned to the new buttons so compiler references continue to bind. Engineers may
choose to rename to `_trimBtn2` etc. for clarity — the plan uses `_trimBtn2` to be explicit.

### 3.3 TradeCopierPanel — Nested Enum

```csharp
// Private enum for BE 3-state FSM (T1)
// No volatile; state transitions only on WPF UI thread.
private enum BeState
{
    Idle,      // BE button shows "BE +N" — inactive
    Armed,     // After first click; engine.ArmPendingBe called; amber border
    Connected  // After engine fires pending BE; blue border; live repricing active
}
```

### 3.4 CopyEngine — New/Modified Members

```csharp
// New overload — T1 (DW-B11-DEFER-01 close): Limit exit at bid+buffer (long) or ask-buffer (short)
// refPrice: last close from panel chart, passed by panel; engine uses for limit offset
internal void Trim(Instrument instrument, int exitBuffer, double refPrice) -> void

// New overload — T1: Limit exit full qty
internal void Flatten(Instrument instrument, int exitBuffer, double refPrice) -> void

// New delegating methods — T3: push risk/ATR changes through to owned AtrSizingEngine
internal void UpdateMaxRisk(double maxRiskDollars) -> void
internal void UpdateAtrFraction(double fraction) -> void

// Modified: DispatchCopy — add PTT-prefix gate (Gate 0.5)
private void DispatchCopy(Order order, CopyRule rule) -> void   // CYC: 7 -> 8
```

### 3.5 AtrSizingEngine — New Members

```csharp
// New field — T3 (plain double, single-writer UI thread, no volatile per NT8-003)
private double _atrFraction = 1.0;

// New method — T3 (CYC=1)
internal void SetAtrFraction(double fraction) -> void

// Modified: UpdateMaxRisk (new standalone setter, avoids full SetParameters call)
internal void UpdateMaxRisk(double maxRiskDollars) -> void   // CYC=1
```

---

## §4 Exact Method Signatures

### 4.1 TradeCopierPanel.cs — New Methods (T1)

```csharp
// Layout builder — pure UI construction, no branches. CYC=1.
private void BuildBufferedButtonsRow(StackPanel root)

// Format helper shared by all 3 buffer button pairs. CYC=1. Static, no state.
// Example output: "Trim +1", "BE +2"
private static string FormatBuffer(string name, int ticks)

// --- Trim pair ---
// CYC=1 (no instrument guard needed — buffer is panel-local)
private void OnTrimUp(object sender, RoutedEventArgs e)
private void OnTrimDown(object sender, RoutedEventArgs e)
// CYC=3: instrument null guard(1) + refPrice guard(2) + engine call(3)
private void OnTrimClick(object sender, RoutedEventArgs e)

// --- Flatten pair ---
private void OnFlattenUp(object sender, RoutedEventArgs e)    // CYC=1
private void OnFlattenDown(object sender, RoutedEventArgs e)  // CYC=1
// CYC=3: instrument null(1) + refPrice guard(2) + engine call(3)
private void OnFlattenClick(object sender, RoutedEventArgs e)

// --- BE pair (3-state FSM) ---
// CYC=2: buffer clamp(1) + live reprice if Connected(2)
private void OnBeUp(object sender, RoutedEventArgs e)
private void OnBeDown(object sender, RoutedEventArgs e)       // CYC=2 same
// CYC=5: instrument null(1) + leaderAccount null(2) + Idle->Armed(3) + Armed->Idle(4) + Connected->Idle(5)
private void OnBeClick(object sender, RoutedEventArgs e)

// CYC=1: sets _beBtn2 label via FormatBuffer
private void UpdateBeLabel()

// CYC=3: switch on BeState (Idle/Armed/Connected = 3 cases)
private void UpdateBeVisuals(BeState state)

// Called by Dispatcher.InvokeAsync chain (existing OnPendingBeFiredDispatch pathway).
// Replaces FlashBeFired from B10 T2. Transitions ARMED -> CONNECTED.
// async void: event handler invoked via Dispatcher.InvokeAsync (explicitly permitted).
// CYC=2: null guard(1) + state transition body(2).
private async void OnBeConnected(string instr)

// Chart reference price for limit order calculation.
// CYC=3: chart null(1) + barsArray null/empty(2) + return last close(3).
private double GetRefPrice()
```

### 4.2 TradeCopierPanel.cs — New Methods (T2)

```csharp
// Layout builder for collapse header. CYC=1.
private void BuildCollapsibleHeader(StackPanel root)

// CYC=2: toggle _isCollapsed(1) + set Visibility + update button label(2).
private void OnCollapseClick(object sender, RoutedEventArgs e)
```

### 4.3 TradeCopierPanel.cs — New Methods (T3)

```csharp
// Layout builder for Risk $ + ATR % spinners. CYC=1.
private void BuildRiskAtrRow(StackPanel root)

// CYC=1: increment, clamp(Math.Max/Min), update TextBox, call NotifyRiskChanged
private void OnRiskUp(object sender, RoutedEventArgs e)
// CYC=1: decrement, clamp, update TextBox, call NotifyRiskChanged
private void OnRiskDown(object sender, RoutedEventArgs e)
// CYC=3: parse(1) + clamp(2) + push(3). Fires on TextBox.LostFocus.
private void OnRiskTextLostFocus(object sender, RoutedEventArgs e)

// CYC=1: increment, clamp, update TextBox, call NotifyAtrFractionChanged
private void OnAtrFractionUp(object sender, RoutedEventArgs e)
// CYC=1: decrement, clamp, update TextBox, call NotifyAtrFractionChanged
private void OnAtrFractionDown(object sender, RoutedEventArgs e)
// CYC=3: parse(1) + clamp(2) + push(3). Fires on TextBox.LostFocus.
private void OnAtrFractionTextLostFocus(object sender, RoutedEventArgs e)

// Delegates to CopyEngine.UpdateMaxRisk. CYC=2: instrument null guard + call.
private void NotifyRiskChanged()

// Delegates to CopyEngine.UpdateAtrFraction. CYC=2: instrument null guard + call.
private void NotifyAtrFractionChanged()
```

### 4.4 CopyEngine.cs — New/Modified Methods

```csharp
// New overload — T1 (DW-B11-DEFER-01 close)
// Sells ceiling(qty/2) as Limit order at (refPrice + exitBuffer*tickSize) for long;
// buys ceiling(qty/2) as Limit order at (refPrice - exitBuffer*tickSize) for short.
// Signal name: "PTT-TrimLimit" (PTT-prefix compliant).
// CYC=5: rule null(1) + foreach acc(2) + flat skip(3) + direction(4) + try/catch(5).
internal void Trim(Instrument instrument, int exitBuffer, double refPrice)

// New overload — T1
// Sells full qty as Limit at (refPrice + exitBuffer*tickSize) for long;
// buys full qty as Limit at (refPrice - exitBuffer*tickSize) for short.
// Signal name: "PTT-FlattenLimit" (PTT-prefix compliant).
// CYC=5: rule null(1) + foreach acc(2) + flat skip(3) + direction(4) + try/catch(5).
internal void Flatten(Instrument instrument, int exitBuffer, double refPrice)

// Modified: DispatchCopy — PTT-prefix Gate 0.5 (top of method, before Gate 3)
// CYC goes from 7 -> 8 (at limit; PASS).
// Gate text: if (order.Name != null && order.Name.StartsWith("PTT-")) return;
private void DispatchCopy(Order order, CopyRule rule)

// New delegating method — T3. Null-guards _atrEngine, then calls SetParameters with
// new maxRiskDollars and preserved existing tickDollarValue (held in _cachedTickDollarValue).
// CYC=2: null guard + call.
internal void UpdateMaxRisk(double maxRiskDollars)

// New delegating method — T3. Null-guards _atrEngine, then calls SetAtrFraction.
// CYC=2: null guard + call.
internal void UpdateAtrFraction(double fraction)
```

NOTE on `_cachedTickDollarValue`: CopyEngine already has _atrEngine (AtrSizingEngine).
The existing SetAtrEngine call sets both engine and enabled flag. To support `UpdateMaxRisk`
without requiring a full SetParameters roundtrip, add `private double _cachedTickDollarValue = 5.0`
to CopyEngine. SetAtrEngine updates this cache on initial SetParameters call. UpdateMaxRisk
calls _atrEngine.UpdateMaxRisk(maxRiskDollars) which is a standalone setter (see §3.5).

### 4.5 AtrSizingEngine.cs — New/Modified Methods

```csharp
// New field: plain double, single-writer UI thread, no volatile (NT8-003).
private double _atrFraction = 1.0;

// New method — T3. Single-writer UI thread. CYC=1.
// Stores fraction for use in next OnBarUpdate.
internal void SetAtrFraction(double fraction)

// New method — T3. Standalone risk update without requiring tickDollarValue.
// CYC=1: _maxRiskDollars = maxRiskDollars (straight-line).
internal void UpdateMaxRisk(double maxRiskDollars)

// Modified: OnBarUpdate — scale ATR by _atrFraction before CalcContracts.
// Change: int qty = CalcContracts(atr * _atrFraction, _maxRiskDollars, _tickDollarValue);
// CYC unchanged (CurrentBar guard stays as-is).
protected override void OnBarUpdate()
```

---

## §5 Jane Street Rule Compliance

| Rule | Scope | Status |
|------|-------|--------|
| JS-021 (P0) no lock() | All new methods | PASS — no lock anywhere. All new panel methods are UI-thread-only. Engine uses existing ConcurrentBag (lock-free). |
| JS-001 (P0) no throw in hot path | Trim/Flatten overloads | PASS — try/catch wraps acc.CreateOrder, exception routed to StatusUpdate, no rethrow. |
| JS-002 (P0) no return null | All handlers | PASS — early returns use bare `return;` not `return null`. |
| JS-033 (P0) no async void except event handlers | OnBeConnected | PASS — async void is invoked via Dispatcher.InvokeAsync (same pattern as existing FlashBeFired). |
| NT8-003 no volatile double | _maxRiskDollars, _atrFraction (panel), _atrFraction (engine) | PASS — all plain double, UI-thread-only. |
| NT8-003 no volatile bool | _isCollapsed | PASS — plain bool, UI-thread-only. |
| NT8-003 no volatile int | _trimBuffer, _flattenBuffer, _beBuffer | PASS — plain int, UI-thread-only. |
| No Math.Clamp (.NET 4.8) | T3 spinners | PASS — use `Math.Max(Math.Min(v, max), min)` throughout. |
| ASCII-only in .cs literals | All new string literals | PASS — arrows = "\u25B2"/"\u25BC"; bullet = "\u25CF"; no literal Unicode chars. |
| PTT-prefix on CreateOrder names | Trim/Flatten overloads | PASS — "PTT-TrimLimit", "PTT-FlattenLimit". |
| No FontFamily overrides | T3 spinners | PASS — NTTextBoxStyle / NTButtonStyle used; no font overrides. |
| No hardcoded hex | BrushConnected | PASS — MakeBrush(59, 130, 246) via RGB args, not hex string. |
| RepeatButton.Click event | T1 and T3 ▲▼ buttons | PASS — Click event used, not PreviewMouseLeftButtonDown. |
| No abstract record, no ImmutableDictionary, no init; | Not applicable | PASS — none introduced. |

---

## §6 CYC Budget

| Method | File | CYC | Limit | Status |
|--------|------|-----|-------|--------|
| BuildBufferedButtonsRow | Panel | 1 | 8 | PASS |
| FormatBuffer | Panel | 1 | 8 | PASS |
| OnTrimUp | Panel | 1 | 8 | PASS |
| OnTrimDown | Panel | 1 | 8 | PASS |
| OnTrimClick | Panel | 3 | 8 | PASS |
| OnFlattenUp | Panel | 1 | 8 | PASS |
| OnFlattenDown | Panel | 1 | 8 | PASS |
| OnFlattenClick | Panel | 3 | 8 | PASS |
| OnBeUp | Panel | 2 | 8 | PASS |
| OnBeDown | Panel | 2 | 8 | PASS |
| OnBeClick | Panel | 5 | 8 | PASS |
| UpdateBeLabel | Panel | 1 | 8 | PASS |
| UpdateBeVisuals | Panel | 3 | 8 | PASS |
| OnBeConnected | Panel | 2 | 8 | PASS |
| GetRefPrice | Panel | 3 | 8 | PASS |
| BuildCollapsibleHeader | Panel | 1 | 8 | PASS |
| OnCollapseClick | Panel | 2 | 8 | PASS |
| BuildRiskAtrRow | Panel | 1 | 8 | PASS |
| OnRiskUp | Panel | 1 | 8 | PASS |
| OnRiskDown | Panel | 1 | 8 | PASS |
| OnRiskTextLostFocus | Panel | 3 | 8 | PASS |
| OnAtrFractionUp | Panel | 1 | 8 | PASS |
| OnAtrFractionDown | Panel | 1 | 8 | PASS |
| OnAtrFractionTextLostFocus | Panel | 3 | 8 | PASS |
| NotifyRiskChanged | Panel | 2 | 8 | PASS |
| NotifyAtrFractionChanged | Panel | 2 | 8 | PASS |
| DispatchShortcut (modified) | Panel | 6 | 8 | PASS (Key.T/F with refPrice guard) |
| Trim(Instrument,int,double) | Engine | 5 | 8 | PASS |
| Flatten(Instrument,int,double) | Engine | 5 | 8 | PASS |
| DispatchCopy (modified +gate) | Engine | 8 | 8 | PASS (AT LIMIT) |
| UpdateMaxRisk | Engine | 2 | 8 | PASS |
| UpdateAtrFraction | Engine | 2 | 8 | PASS |
| SetAtrFraction | AtrEngine | 1 | 8 | PASS |
| UpdateMaxRisk | AtrEngine | 1 | 8 | PASS |
| OnBarUpdate (modified) | AtrEngine | 2 | 8 | PASS (unchanged from B11) |

---

## §7 Threading Model

All new code in TradeCopierPanel.cs runs on the WPF UI thread:
- RepeatButton Click events: fired by WPF on UI thread. No Dispatcher needed.
- Buffer fields (_trimBuffer etc.): plain int/double/bool, single-writer UI thread.
- Engine calls from button handlers: synchronous CopyEngine/AtrSizingEngine calls from UI thread.

BeState transition to CONNECTED:
- OnPendingBeFiredDispatch (existing method, called from NT8 account background thread)
  already does: `Dispatcher.InvokeAsync(() => FlashBeFired(instr));`
- In B12, replace `FlashBeFired(instr)` with `OnBeConnected(instr)` in that dispatch.
- OnBeConnected runs on UI thread (via Dispatcher.InvokeAsync). SAFE.

AtrSizingEngine:
- _atrFraction is written on UI thread (SetAtrFraction call from panel).
- _atrFraction is read on bar-close thread (OnBarUpdate).
- This is the same "understood staleness tolerance" pattern as the existing _lastAtr field
  in AtrSizingEngine: non-volatile, sizing hint only, not order-safety critical.
- No volatile; NT8-003 bans volatile double anyway.

NT8 API calls from UI thread:
- acc.CreateOrder (Trim/Flatten overloads): called from RepeatButton/Click handlers on UI thread.
  NT8 ChartTrader AddOn runs on WPF UI thread; acc.CreateOrder is safe from that thread.
- acc.Change (existing BreakEven path via MoveStopToBreakEven): same reasoning.

No new Dispatcher.InvokeAsync placements required. Existing pathway in OnPendingBeFiredDispatch
covers the only cross-thread event (ARMED -> CONNECTED transition).

---

## §8 Data Flow

### 8.1 Trim/Flatten Limit Order Flow

```
User clicks Trim button
  → OnTrimClick
    → GetRefPrice() -- read last close from _currentChart
    → if (refPrice <= 0) _engine.Trim(_instrument)         -- fallback: market order
    → else              _engine.Trim(_instrument, _trimBuffer, refPrice)
      → CopyEngine.Trim(instrument, exitBuffer, refPrice)
        → rule = FindRule(instrument)
        → foreach acc in AllAccounts:
            pos = FindPosition(acc, instrument)
            if flat: skip
            trimQty = ceil(qty / 2)
            action = long ? Sell : BuyToCover
            tickSize = instrument.MasterInstrument.TickSize
            limitPrice = long ? (refPrice + exitBuffer * tickSize)
                               : (refPrice - exitBuffer * tickSize)
            acc.CreateOrder(instrument, action, OrderType.Limit, ..., trimQty,
                            limitPrice, 0, null, "PTT-TrimLimit", DateTime.MaxValue, null)
      → NT8 emits OrderUpdate event for "PTT-TrimLimit" order
      → CopyEngine.OnOrderUpdate → DispatchCopy
          → Gate 0.5: order.Name.StartsWith("PTT-") == true → RETURN  (no cascade copy)
```

### 8.2 BE 3-State FSM Flow

```
State: IDLE
  → OnBeClick
      → engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)
      → _beState = Armed
      → UpdateBeVisuals(Armed) -- amber border
  
State: ARMED
  → entry fills on master account (NT8 event on background thread)
  → CopyEngine.OnPendingBeAccountUpdate fires
  → OnPendingBeFiredDispatch (existing) dispatches to UI:
      Dispatcher.InvokeAsync(() => OnBeConnected(instr))
  → OnBeConnected:
      → _beState = Connected
      → UpdateBeVisuals(Connected) -- blue border
      → engine.BreakEven(_instrument, _beBuffer) -- initial move to BE+N
  
State: CONNECTED (live repricing active)
  → OnBeUp / OnBeDown
      → _beBuffer = Math.Max(Math.Min(_beBuffer +/- 1, 20), 0)
      → UpdateBeLabel()
      → engine.BreakEven(_instrument, _beBuffer) -- immediate reprice

  → OnBeClick again:
      → engine.DisarmPendingBe() [cleans up any residual arm state]
      → _beState = Idle
      → UpdateBeVisuals(Idle) -- no border
```

### 8.3 Risk/ATR Input Flow

```
User clicks Risk ▲
  → OnRiskUp
      → _maxRiskDollars = Math.Max(Math.Min(_maxRiskDollars + 25, 1000), 10)
      → _riskDollarsBox.Text = _maxRiskDollars.ToString("F0")
      → NotifyRiskChanged()
          → engine.UpdateMaxRisk(_maxRiskDollars)
              → _atrEngine != null: _atrEngine.UpdateMaxRisk(_maxRiskDollars)
                  → AtrSizingEngine._maxRiskDollars = value

User tabs out of Risk $ TextBox
  → OnRiskTextLostFocus
      → double.TryParse(text) → v
      → v = Math.Max(Math.Min(v, 1000), 10)
      → _maxRiskDollars = v
      → _riskDollarsBox.Text = _maxRiskDollars.ToString("F0")
      → NotifyRiskChanged()  -- same path as above
```

---

## §9 UI Layout Specification

### 9.1 Panel structure after B12

```
TradeCopierPanel (StackPanel root, Margin=2)
  [0] Followers ComboBox dropdown (unchanged)
  [1] Apply Rule button (unchanged)
  [2] Separator border (unchanged)
  [3] Collapse header row (NEW — T2)
        ["\u25BC PTT"] toggle button (NTButtonStyle)
  [4] _contentPanel (StackPanel — T2; hidden when _isCollapsed=true)
        [4.0] BuildBufferedButtonsRow (T1)
                Row 1: UniformGrid Columns=2
                  Col 0: Trim cluster  (Button "Trim +N" + RepeatButton "\u25B2" + RepeatButton "\u25BC")
                  Col 1: Flatten cluster (Button "Flatten +N" + "\u25B2" + "\u25BC")
                Row 2: UniformGrid Columns=2
                  Col 0: Cancel button (NTButtonStyle, plain)
                  Col 1: BE cluster (Button "BE +N" + "\u25B2" + "\u25BC")
                Row 3: Copy toggle (full-width Button "\u25CF COPY ON/OFF", NTButtonStyle removed for color coding)
        [4.1] Status TextBlock (unchanged, now inside contentPanel)
        [4.2] BuildClickTraderRow (unchanged)
        [4.3] BuildModeRow (unchanged)
        [4.4] Tighten stop cluster (unchanged)
        [4.5] BuildAtmTemplateRow (unchanged)
        [4.6] BuildRiskAtrRow (T3)
                UniformGrid Columns=2
                  Col 0: Risk $ spinner
                         Label "Risk $"
                         TextBox (NTTextBoxStyle, Width=55, Text="200")
                         Grid 2-row/1-col:
                           Row 0: RepeatButton "\u25B2" (NTButtonStyle, Height=12)
                           Row 1: RepeatButton "\u25BC" (NTButtonStyle, Height=12)
                  Col 1: ATR % spinner
                         Label "ATR %"
                         TextBox (NTTextBoxStyle, Width=55, Text="0.75")
                         Grid 2-row/1-col:
                           Row 0: RepeatButton "\u25B2" (NTButtonStyle, Height=12)
                           Row 1: RepeatButton "\u25BC" (NTButtonStyle, Height=12)
```

### 9.2 Buffered Button Cluster Widget (reused 3x)

Each buffer-control cluster is: `[MainButton][▲][▼]` laid out as a DockPanel or HorizontalStackPanel.
- MainButton: DockPanel.Dock=Left, grows to fill
- ▲ RepeatButton: 18px wide, 12px tall (half-height stack)
- ▼ RepeatButton: 18px wide, 12px tall

The main button label is set by FormatBuffer(name, buffer). Example: "Trim +1".
Both RepeatButtons use NTButtonStyle.

### 9.3 BE Visual States

| State | Button Content | Border Color | Border Thickness |
|-------|---------------|--------------|------------------|
| Idle | FormatBuffer("BE", _beBuffer) | none | 0 |
| Armed | "BE Armed" | BrushCaution (amber) | 2 |
| Connected | "BE Live" | BrushConnected (blue) | 2 |

Border is set on the _beBtn2 Button via `_beBtn2.BorderBrush = brush; _beBtn2.BorderThickness = new Thickness(2)`.

---

## §10 Backlog Ledger

| ID | Description | Action | Target |
|----|-------------|--------|--------|
| DW-B11-DEFER-01 | Flatten/Trim limit orders — new engine overloads | CLOSED by T1 | B12 T1 |
| DW-B9-01 | ATR box visualization on chart canvas | SHELVED | B13 |
| DW-B9-03 | Click trader Bid+1/Ask-1 offset | SHELVED | B13 |

---

## §11 xUnit Test Requirements (per ticket)

Tests live in `CopyEngineTests.cs` (existing test file).
All tests use `[Fact]` attribute (xUnit; never NUnit/MSTest per JS testing mandate).

### T1 Tests

```csharp
// CopyEngine.Trim limit overload — long position
[Fact] Trim_LimitOverload_LongPosition_EmitsSellLimitAtBidPlusBuffer()
  // arrange: mock Account with long pos qty=4, refPrice=100.0, exitBuffer=1, tickSize=0.25
  // assert: CreateOrder called with OrderType.Limit, limitPrice=100.25, qty=2, "PTT-TrimLimit"

// CopyEngine.Trim limit overload — short position
[Fact] Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtAskMinusBuffer()
  // arrange: mock Account with short pos qty=4, refPrice=100.0, exitBuffer=1, tickSize=0.25
  // assert: CreateOrder called with OrderType.Limit, limitPrice=99.75, qty=2, "PTT-TrimLimit"

// CopyEngine.Flatten limit overload — long
[Fact] Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty()
  // arrange: long pos qty=4
  // assert: qty=4, limitPrice=refPrice+buffer*tick, "PTT-FlattenLimit"

// CopyEngine.Flatten limit overload — short
[Fact] Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty()
  // arrange: short pos qty=4
  // assert: qty=4, limitPrice=refPrice-buffer*tick, "PTT-FlattenLimit"

// PTT-prefix gate in DispatchCopy
[Fact] DispatchCopy_PttPrefixGate_SkipsOrderNamed_PTT_TrimLimit()
  // arrange: order with Name="PTT-TrimLimit", valid rule, OrderState.Submitted, OrderType.Market
  // assert: SendCopy never called (followers not touched)
```

### T2 Tests

No unit tests required for T2. The collapse toggle is a 2-line WPF Visibility mutation
(CYC=2, pure UI) that is trivially verifiable by visual inspection during F5 run.

### T3 Tests

```csharp
// AtrSizingEngine fraction scaling
[Fact] AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1()
  // arrange: engine, atr=10 pts, tickDollarValue=5, maxRisk=500, fraction=0.5
  // assert: CalcContracts(atr*0.5=5, 500, 5) = floor(500/25)=20 contracts

// CopyEngine.UpdateMaxRisk delegation
[Fact] UpdateMaxRisk_SetsAtrEngineMaxRiskDollars()
  // arrange: CopyEngine with attached AtrSizingEngine, initial maxRisk=150
  // act: engine.UpdateMaxRisk(300)
  // assert: atrEngine.GetSuggestedQty() reflects new risk (will yield different qty on next bar)

// CopyEngine.UpdateAtrFraction delegation
[Fact] UpdateAtrFraction_SetsAtrEngineFraction()
  // arrange: CopyEngine with attached AtrSizingEngine
  // act: engine.UpdateAtrFraction(0.5)
  // assert: atrEngine's internal fraction = 0.5 (verify via CalcContracts behavior)
```

---

## §12 Spinner Parameter Table (T3)

| Spinner | Field | Default | Step | Min | Max | Engine Push |
|---------|-------|---------|------|-----|-----|-------------|
| Risk $ | _maxRiskDollars | 200.0 | 25 | 10 | 1000 | CopyEngine.UpdateMaxRisk |
| ATR % | _atrFraction | 0.75 | 0.05 | 0.25 | 3.00 | CopyEngine.UpdateAtrFraction |

Clamp formula (no Math.Clamp per NT8 .NET 4.8 ban):
```csharp
_maxRiskDollars = Math.Max(Math.Min(_maxRiskDollars + step, 1000.0), 10.0);
_atrFraction    = Math.Max(Math.Min(_atrFraction + step, 3.00), 0.25);
```

TextBox display formats: Risk $ → "F0" (no decimals), ATR % → "F2" (2 decimals).

---

## §13 File Change Summary per Ticket

### T1 — DW-B12-BUFFERED-BUTTONS-01

**TradeCopierPanel.cs**
- Add fields: _trimBuffer, _flattenBuffer, _beBuffer (plain int), _beState (BeState), new Button refs, BrushConnected
- Add nested enum: BeState { Idle, Armed, Connected }
- Remove fields: _beArmBtn, _beArmState, _beArmBufferBox
- Modify BuildUI(): wrap rows in _contentPanel; call BuildBufferedButtonsRow; wire new toggle btn
- Remove BuildBeArmRow() call; remove OnBEArmClick, UpdateBEArmVisuals, FlashBeFired
- Add methods: BuildBufferedButtonsRow, FormatBuffer, OnTrimUp/Down/Click, OnFlattenUp/Down/Click, OnBeUp/Down/Click, UpdateBeLabel, UpdateBeVisuals, OnBeConnected, GetRefPrice
- Modify OnPendingBeFiredDispatch: `Dispatcher.InvokeAsync(() => OnBeConnected(instr))`
- Modify DispatchShortcut Key.T/Key.F: call new overload with GetRefPrice() + fallback

**CopyEngine.cs**
- Add Trim(Instrument, int, double) overload
- Add Flatten(Instrument, int, double) overload
- Add _cachedTickDollarValue field (set in SetAtrEngine)
- Modify DispatchCopy: add PTT-prefix gate (Gate 0.5) at top

### T2 — DW-B12-COLLAPSE-01

**TradeCopierPanel.cs**
- Add fields: _isCollapsed (bool), _collapseToggleBtn (Button), _contentPanel (StackPanel)
- Add methods: BuildCollapsibleHeader, OnCollapseClick
- Modify BuildUI(): call BuildCollapsibleHeader before _contentPanel; wrap all rows in _contentPanel

### T3 — DW-B12-RISK-ATR-INPUTS-01

**TradeCopierPanel.cs**
- Add fields: _maxRiskDollars (double), _atrFraction (double), _riskDollarsBox (TextBox), _atrFractionBox (TextBox)
- Add methods: BuildRiskAtrRow, OnRiskUp/Down/TextLostFocus, OnAtrFractionUp/Down/TextLostFocus, NotifyRiskChanged, NotifyAtrFractionChanged
- Modify BuildUI(): call BuildRiskAtrRow at end of _contentPanel

**CopyEngine.cs**
- Add UpdateMaxRisk(double) delegating method
- Add UpdateAtrFraction(double) delegating method

**AtrSizingEngine.cs**
- Add _atrFraction field (double, plain, no volatile)
- Add SetAtrFraction(double) method
- Add UpdateMaxRisk(double) method (standalone setter)
- Modify OnBarUpdate: pass `atr * _atrFraction` to CalcContracts

---

## §14 Forward Roadmap (do NOT implement in B12)

| ID | Item | Block |
|----|------|-------|
| DW-B12-DEFER-PLACEHOLDER | No new deferred items anticipated; review post-B12 | B13 |
| DW-B9-01 | ATR box visualization on chart canvas | B13 |
| DW-B9-03 | Click trader Bid+1/Ask-1 offset | B13 |

---

**PLAN_COMPLETE**
