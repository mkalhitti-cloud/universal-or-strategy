# PTT-COPIER-B12 Ticket T1 Completion Report
## DW-B12-BUFFERED-BUTTONS-01 — Buffered Exit Buttons

**Status**: BUILD_PASS
**Date**: 2026-07-07
**Engineer**: ptt-engineer (PTT-Engineer mode)

---

## Summary

Ticket T1 implemented buffered limit-exit UI for the trade copier panel:
- `CopyEngine.cs`: Added `Trim` and `Flatten` 3-arg limit-exit overloads + PTT-prefix Gate 0.5 in `DispatchCopy`
- `TradeCopierPanel.cs`: Full UI restructure — 3-row buffered buttons section, `BeState` FSM enum, 22 new methods, removed obsolete B10 T2 arm fields
- `CopyEngineTests.cs`: Added 5 new `[Fact]` tests (T-B12-01 through T-B12-05)

---

## Changed Files (Wave workspace)

### `src/PropTraderTools/CopyEngine.cs`

| Change | Method | CYC | Notes |
|--------|--------|-----|-------|
| New 3-arg overload | `Trim(Instrument, int, double)` | 5 | PTT-TrimLimit; fallback to market when refPrice<=0 or exitBuffer==0 |
| New 3-arg overload | `Flatten(Instrument, int, double)` | 5 | PTT-FlattenLimit; fallback to market when refPrice<=0 or exitBuffer==0 |
| Gate 0.5 added | `DispatchCopy` | 8 (AT LIMIT) | `if (order.Name != null && order.Name.StartsWith("PTT-")) return;` — prevents cascade copy of PTT- signals |

**`Trim(Instrument, int, double)` signature:**
```csharp
internal void Trim(Instrument instrument, int exitBuffer, double refPrice)
```
- CYC=5: fallback(1) + foreach acc(2) + flat skip(3) + isLong direction(4) + try/catch(5)
- Long: Sell Limit @ `refPrice + exitBuffer * tickSize`
- Short: BuyToCover Limit @ `refPrice - exitBuffer * tickSize`
- Fallback: `if (refPrice <= 0 || exitBuffer == 0) { Trim(instrument); return; }`
- NT8-007: arg 12 = `(NinjaTrader.Cbi.CustomOrder)null`
- Signal name: `"PTT-TrimLimit"`

**`Flatten(Instrument, int, double)` signature:**
```csharp
internal void Flatten(Instrument instrument, int exitBuffer, double refPrice)
```
- CYC=5: fallback(1) + foreach acc(2) + flat skip(3) + isLong direction(4) + try/catch(5)
- Long: Sell Limit @ `refPrice + exitBuffer * tickSize`
- Short: BuyToCover Limit @ `refPrice - exitBuffer * tickSize`
- Fallback: `if (refPrice <= 0 || exitBuffer == 0) { Flatten(instrument); return; }`
- NT8-007: arg 12 = `(NinjaTrader.Cbi.CustomOrder)null`
- Signal name: `"PTT-FlattenLimit"`

**`DispatchCopy` Gate 0.5:**
```csharp
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```
- Added as first guard in `DispatchCopy` body
- CYC: 7 → 8 (at limit, compliant)

---

### `src/PropTraderTools/TradeCopierPanel.cs`

#### Removed (B10 T2 obsolete fields)
- `private Button _beArmBtn = null;`
- `private bool _beArmState = false;`
- `private TextBox _beArmBufferBox = null;`
- Comment block `// B10 T2 -- Pending BE arm fields`

#### Added fields (B12 T1)
```csharp
private int _trimBuffer     = 1;    // plain int, UI-thread-only, no volatile
private int _flattenBuffer  = 1;
private int _beBuffer       = 1;
private BeState _beState    = BeState.Idle;   // 3-state FSM, no volatile
private Button _trimBtn2      = null;
private Button _flattenBtn2   = null;
private Button _beBtn2        = null;
private Button _cancelBtn2    = null;
private Button _copyToggleBtn2 = null;
```

#### Added fields (B12 T2 — declared here for T2 use)
```csharp
private bool _isCollapsed             = false;
private Button _collapseToggleBtn     = null;
private StackPanel _contentPanel      = null;
```

#### Added brush
```csharp
private static readonly Brush BrushConnected = MakeBrush(59, 130, 246); // frozen blue
```

#### Added enum
```csharp
private enum BeState { Idle, Armed, Connected }
```

#### New Methods

| Method | Signature | CYC | Description |
|--------|-----------|-----|-------------|
| `BuildBufferedButtonsRow` | `private void BuildBufferedButtonsRow(StackPanel root)` | 1 | 3-row button section in `_contentPanel` |
| `FormatBuffer` | `private static string FormatBuffer(string name, int ticks)` | 1 | Formats button label e.g. "Trim +3" |
| `OnTrimUp` | `private void OnTrimUp(object s, RoutedEventArgs e)` | 1 | Increment _trimBuffer, update label |
| `OnTrimDown` | `private void OnTrimDown(object s, RoutedEventArgs e)` | 1 | Decrement _trimBuffer, update label |
| `OnTrimClick` | `private void OnTrimClick(object s, RoutedEventArgs e)` | 3 | Calls `_engine.Trim(instrument, buffer, refPrice)` |
| `OnFlattenUp` | `private void OnFlattenUp(object s, RoutedEventArgs e)` | 1 | Increment _flattenBuffer, update label |
| `OnFlattenDown` | `private void OnFlattenDown(object s, RoutedEventArgs e)` | 1 | Decrement _flattenBuffer, update label |
| `OnFlattenClick` | `private void OnFlattenClick(object s, RoutedEventArgs e)` | 3 | Calls `_engine.Flatten(instrument, buffer, refPrice)` |
| `OnBeUp` | `private void OnBeUp(object s, RoutedEventArgs e)` | 2 | Increment _beBuffer; live reprices if Connected |
| `OnBeDown` | `private void OnBeDown(object s, RoutedEventArgs e)` | 2 | Decrement _beBuffer; live reprices if Connected |
| `OnBeClick` | `private void OnBeClick(object s, RoutedEventArgs e)` | 5 | 3-state FSM: Idle→Armed→(Dispatcher fires)→Connected |
| `UpdateBeLabel` | `private void UpdateBeLabel()` | 1 | Sets _beBtn2.Content |
| `UpdateBeVisuals` | `private void UpdateBeVisuals(BeState st)` | 3 | Idle=no border, Armed=yellow, Connected=blue |
| `OnBeConnected` | `private void OnBeConnected(string instr)` | 2 | ARMED→CONNECTED, calls BreakEven. Regular void (not async void) |
| `GetRefPrice` | `private double GetRefPrice()` | 3 | Reads chart last close; returns 0.0 on null |
| `OnCopyToggle` | `private void OnCopyToggle(object s, RoutedEventArgs e)` | 2 | Toggles `_engine.IsActive` |
| `OnCancel2` | `private void OnCancel2(object s, RoutedEventArgs e)` | 1 | Calls `_engine.CancelPendingEntries(...)` |
| `BuildCollapsibleHeader` | `private void BuildCollapsibleHeader(StackPanel root)` | 1 | T2 collapse header row |
| `OnCollapseClick` | `private void OnCollapseClick(object s, RoutedEventArgs e)` | 2 | Toggles `_contentPanel.Visibility` |

#### Modified `BuildUI()`
- Replaced old `actionGrid` with `_contentPanel` StackPanel wrapper
- Calls `BuildCollapsibleHeader(root)` at [3.0]
- Calls `BuildBufferedButtonsRow(root)` at [4.0]
- Wrapped all row content in `_contentPanel`

#### Modified `DispatchShortcut(Key key)`
- `Key.T`: now calls `_engine.Trim(_instrument, _trimBuffer, GetRefPrice())`
- `Key.F`: now calls `_engine.Flatten(_instrument, _flattenBuffer, GetRefPrice())`

#### Modified `UpdateButtonColors()`
- Updated to use `_trimBtn2`, `_flattenBtn2`, `_beBtn2`, `_cancelBtn2`, `_copyToggleBtn2` with null guards

#### Modified `OnPendingBeFiredDispatch()`
- Replaced call to `FlashBeFired` with `OnBeConnected`

#### `BuildBufferedButtonsRow` layout:
```
Row 1: [Trim   \u25B2\u25BC] [Flatten \u25B2\u25BC]
Row 2: [Cancel]              [BE      \u25B2\u25BC]
Row 3: [COPY ON/OFF ---- full width ----]
```
- All increment/decrement controls: `System.Windows.Controls.Primitives.RepeatButton` (FQN)
- Arrow chars: `"\u25B2"` / `"\u25BC"` — NEVER literal ▲▼

---

### `src/PropTraderTools/CopyEngineTests.cs`

5 new `[Fact]` tests added in section `// B12 T1: Buffered exit overload tests`:

| Test Name | What it verifies |
|-----------|-----------------|
| `Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer` | 3-arg `Flatten` overload exists with correct signature; signal name starts with "PTT-"; null instrument exits cleanly |
| `Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer` | Short-direction contract; null instrument guard |
| `Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer` | 3-arg `Trim` overload exists; "PTT-TrimLimit" signal name; null instrument guard |
| `PttPrefixGate_SkipsDispatchForPttOrders` | `DispatchCopy` still has 2 params; PTT- sentinel string contract |
| `Flatten_ZeroBuffer_FallsBackToMarketOrder` | `exitBuffer==0` routes to market overload; `refPrice<=0` also falls back; no exceptions |

---

## 7-Scan Results

| Scan | Pattern | Result | Notes |
|------|---------|--------|-------|
| SCAN-01 | `lock(` in code | **0** | All hits are in comments ("no lock") only |
| SCAN-02 | `async void` (new) | **0** | `OnBeConnected` is regular `void`; existing `async void` in comments only |
| SCAN-03 | `FontFamily` | **0** | None |
| SCAN-04 | `#RRGGBB` hex in code | **0** | Comment-only hits (`// green #22c55e`) — not in code strings |
| SCAN-05 | `CreateOrder` PTT- prefix | **0 violations** | All: `"PTT-TrimLimit"`, `"PTT-FlattenLimit"`, `"PTT-Copy"`, `"PTT-Trim"`, `"PTT-Flatten"`, `"PTT-Tighten-Stop"` |
| SCAN-06 | `DateTime.Now` | **0** | None |
| SCAN-07 | `Math.Clamp` in code | **0** | All uses are `Math.Max(Math.Min(...))` pattern; Clamp mentions only in comments |

Additional checks:
- `volatile` on new B12 fields: 0 — all new fields are plain `int` / `BeState` / `bool`
- Literal `▲▼` characters: 0 — all arrows use `"\u25B2"` / `"\u25BC"` escape strings
- Non-ASCII characters in modified files: 0 — all 3 files are ASCII-clean
- `return null` (new B12 code only): 0

---

## NT8 Compiler Rules Compliance

| Rule | Check | Status |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | ✅ — no init setters |
| NT8-002 | No `abstract/sealed record` | ✅ — `BeState` is `enum`, not record |
| NT8-003 | No `volatile double` | ✅ — new B12 fields: plain `int`/`BeState` |
| NT8-004 | No `ImmutableDictionary` in new code | ✅ — not used in B12 T1 |
| NT8-007 | `CreateOrder` arg 12 = `(CustomOrder)null` | ✅ — both `PTT-TrimLimit` and `PTT-FlattenLimit` use `(NinjaTrader.Cbi.CustomOrder)null` |

---

## Jane Street DNA Compliance

| Rule | Check | Status |
|------|-------|--------|
| JS-001 | No throw in hot path | ✅ — all `CreateOrder` calls wrapped in try/catch, no rethrow |
| JS-008 | `BrushConnected` frozen | ✅ — `MakeBrush(59,130,246)` calls `Freeze()` |
| JS-021 | No `lock()` | ✅ — 0 lock calls |
| JS-023 | Volatile only on permitted types | ✅ — new B12 fields are non-volatile |
| CYC≤8 | All new methods | ✅ — max is `OnBeClick`=5, `DispatchCopy`=8 (at limit) |

---

## BUILD_PASS
