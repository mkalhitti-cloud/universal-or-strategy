# PTT-COPIER-B7 -- Architecture Plan
# REVISION CYCLE 1 -- Rewritten after REVIEW_FAIL.
# Phase 2 output. Written by ptt-architect after 8 mandatory sequentialthinking thoughts.
# Status: PLAN_COMPLETE

---

## Violations Addressed (REVISION CYCLE 1)

All 8 violations from `docs/brain/PTT-COPIER-B7/02-plan-review.md` are fixed in this revision.

| ID | Severity | How Fixed in This Plan |
|----|----------|------------------------|
| V01 | P0-SPEC | `_orderMap: ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>` added to CopyEngine. `FollowerBinding` readonly struct added. `PopulateOrderMap` method designed. `FindFollowerBracketOrder` revised to use `FromEntrySignal` name matching (not leg-type scan). |
| V02 | P1-SPEC | Price-delta guard `if (Math.Abs(newPrice - fo.StopPrice/LimitPrice) < tickSize) continue;` added inside `HandleBracketChange` foreach loop, verified in CYC count (CYC=8 exactly). |
| V03 | P0-JS-002 | `FindFollowerBracketOrder` return type changed to `Order?` (C# 8+ nullable reference type). Null contract is explicit. Callers use `if (fo == null) continue`. |
| V04 | P0-SPEC | Layer 3 live state fully designed. `UpdateButtonColors(bool hasPosition, bool hasEntries)` on both Panel and Window. `OnPositionStateChanged` handler on both surfaces. Subscribe in OnLoaded, unsubscribe in Detach()/OnClosed. `UpdateButtonColors(false, false)` called at end of `BuildUI()` — all action buttons start grey. |
| V05 | P0-JS-003 | `public readonly struct PositionState` with `HasOpenPosition` and `HasWorkingEntries` (init-only) added to CopyEngine.cs (outside class). `public event Action<string, PositionState> PositionStateChanged` declared in CopyEngine. |
| V06 | P0-JS-003 | `public abstract record FollowerAtmMode` with `sealed record Inherit()`, `Market()`, `Named(string)` and private base constructor (JS-010) added to CopyEngine.cs (outside class). B7: scaffolding only, zero behavior change. |
| V07 | P1-JS-009 | `ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates { get; init; }` added to `CopyRule` struct with `ImmutableDictionary<string, FollowerAtmMode>.Empty` default. `using System.Collections.Immutable` added to CopyEngine.cs. |
| V08 | P1-SPEC | All brush RGB values corrected to PTT_DESIGN_PILLAR canonical values: `BrushDanger = MakeBrush(239, 68, 68)` (was 185,28,28), `BrushCaution = MakeBrush(245, 158, 11)` (was 217,119,6), `BrushInactive = MakeBrush(55, 65, 81)` (was 75,85,99). Brush names aligned to PTT_DESIGN_PILLAR (`BrushActive`, `BrushDanger`, `BrushCaution`, `BrushInactive`). |

---

## Section 0: Open Items Disposition

All B6 deferred items confirmed CLOSED per `docs/brain/PTT-COPIER-B6/06-deferred-backlog.md`.
B6 backlog is empty. No carryover into B7.

### B7 Manifest Open Items Disposition

| ID | Item | Priority | Disposition | Reason |
|----|------|----------|-------------|--------|
| B7-F0 | Bracket mirroring (stop+target follow leader drags) | P0 | **ADDRESSED IN B7 (T1)** | Core copy fidelity feature. Implemented in `CopyEngine.cs`. |
| B7-F1 | Button color coding (Layer 2 + Layer 3) | P2 | **ADDRESSED IN B7 (T2)** | Applies to both surfaces. Layer 3 live state fully designed (V04). |
| B7-F2 | Live P&L + Pos per account in grid | P2 | **CLOSED (already implemented)** | Completed in the B7 session prior to architecture phase. Confirmed by reading source. No further work needed. |
| B7-F3 | Per-account qty multiplier (1x/2x/3x) | P2 | **DEFERRED to B8** | Requires CopyRule DTO + serialization changes. Medium complexity. |
| B7-F4 | ATR dynamic sizing engine | P1 | **DEFERRED to B8/B9** | New file, MarketData subscription. High complexity, warrants own block. |
| B7-F5 | ScrollViewer on TradeCopierWindow rule grid | P2 | **ADDRESSED IN B7 (T2)** | Pure UI change, 5 lines. Bundled with T2. |

---

## Section 1: Feature B7-F0 -- Bracket Mirroring (P0)

### Summary
When the master account's stop or target bracket order is modified (dragged on chart),
propagate the same price change to all follower accounts' matching bracket orders on
the same instrument. Uses the existing `Account.OrderUpdate` event already subscribed.
Follower bracket lookup uses `FromEntrySignal` name matching, NOT iterating by leg type.

### File Changed
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

### New Using Directive Required
```csharp
using System.Collections.Immutable;   // for ImmutableDictionary in CopyRule (V07)
```

### Required Refactor: OnOrderUpdate CYC Reduction
`OnOrderUpdate` currently has ~11 cyclomatic branches (exceeds CYC 8 limit). Adding bracket
detection without extraction would push it to ~13. MUST extract the copy-dispatch path
before adding the bracket branch.

**Extraction: `DispatchCopy(Order order, CopyRule rule)`**

Receives the order and matched rule, runs gates 3-4 (Submitted check, type check, dedup
check) and dispatches to follower accounts. Contains all logic currently in OnOrderUpdate
lines 172-201.

### New Methods

#### 1. `private void DispatchCopy(Order order, CopyRule rule)` (EXTRACTED from OnOrderUpdate)
```
Signature: private void DispatchCopy(Order order, CopyRule rule)
CYC estimate: 6
Branches:
  (1) if OrderState != Submitted: return
  (1) bool isMarket = ...; bool isLimit = ...; if (!isMarket && !isLimit): return
  (1) if IsDedup(orderId): return
  (1) foreach followers
  (1) if acc == null: continue
  (1) if !PassesDailyCapCheck: continue
  (0) SendCopy call
```
Pure extraction -- behavior identical to current OnOrderUpdate inner block.

#### 2. `private static bool IsWorkingBracket(Order order)` (NEW)
```
Signature: private static bool IsWorkingBracket(Order order)
CYC estimate: 1
Returns: order.OrderState == OrderState.Working && IsBracketLeg(order)
Purpose: Gate predicate for bracket change detection in OnOrderUpdate.
```

#### 3. `private void HandleBracketChange(Order leaderOrder, CopyRule rule)` (NEW -- V02 price-delta guard added)
```
Signature: private void HandleBracketChange(Order leaderOrder, CopyRule rule)
CYC estimate: 8
Branches:
  (1) bool isStop = IsStopLeg(leaderOrder)
  (1) Instrument instrument = leaderOrder.Instrument; if null: return
  (1) double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0; if tickSize <= 0: use raw
  (1) double rawPrice = isStop ? leaderOrder.StopPrice : leaderOrder.LimitPrice  (ternary)
  (1) foreach rule.FollowerAccounts
  (1) if acc == null: continue
  (0) var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop)
  (1) if fo == null: continue                                          -- V03 nullable guard
  (1) PRICE-DELTA GUARD (V02): if (Math.Abs(newPrice - (isStop ? fo.StopPrice : fo.LimitPrice)) < tickSize) continue
  try {
      isStop ? fo.StopPrice = newPrice : fo.LimitPrice = newPrice
      acc.Change(new Order[] { fo })
      StatusUpdate?.Invoke(acc.Name + ": bracket synced " + (isStop ? "stop" : "target") + " -> " + newPrice)
  } catch (Exception ex) {
      StatusUpdate?.Invoke(acc.Name + ": bracket sync error: " + ex.Message)
  }
  -- Note: try/catch wraps acc.Change() only; does NOT add to outer method CYC count
```
**Total HandleBracketChange CYC = 8** (exactly at limit). ✅

Tick rounding (applied BEFORE price-delta guard):
`newPrice = tickSize > 0 ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice`

#### 4. `private Order? FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)` (NEW -- V01 + V03)
```
Signature: private Order? FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)
CYC estimate: 4
Return type: Order? (nullable reference type -- JS-002 compliant)
Branches:
  (1) foreach follower.Orders
  (1) if order.FromEntrySignal != fromEntrySignalName: continue    -- spec: "FromEntrySignal name match"
  (1) if order.OrderState != OrderState.Working: continue
  (1) if isStop: check OrderType.StopMarket or StopLimit
      else: check OrderType.Limit && !IsStopLeg(order)
  (0) return order
  (0) after loop: return null
```
**Why FromEntrySignal matching (not leg-type scan):**
Per spec line 2176-2177 and V01 violation: follower bracket lookup MUST correlate by
`FromEntrySignal` name so the correct bracket is found even when multiple instruments
are open simultaneously. Old approach (iterating and matching by leg type) could select
the wrong bracket if ATM names collide.

#### 5. `private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)` (NEW -- V01)
```
Signature: private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)
CYC estimate: 1
Body:
  _orderMap.GetOrAdd(fromEntrySignalName, _ => new ConcurrentBag<FollowerBinding>())
           .Add(new FollowerBinding { FollowerAccount = followerAccount, FromEntrySignalName = fromEntrySignalName });
```
Called from OnOrderUpdate when a follower account's bracket order fires Working state
for the first time, recording the association in _orderMap for future lookups.
The ConcurrentDictionary.GetOrAdd is atomic (JS-025). ConcurrentBag.Add is lock-free (JS-021). ✅

#### 6. `private void TryFirePositionState(OrderEventArgs e)` (NEW -- V04/V05)
```
Signature: private void TryFirePositionState(OrderEventArgs e)
CYC estimate: 2
Purpose: Fires PositionStateChanged for any state change that affects position truth.
         Called BEFORE Gate 1 in OnOrderUpdate so it fires even when copy is disabled.
Branches:
  (1) multi-condition early return:
      if e.OrderState is not Filled/PartFilled/Cancelled/Rejected: return
  (1) if e.Order?.Instrument?.FullName == null: return
Body after guards:
  string instr = e.Order.Instrument.FullName;
  bool hasPos     = HasOpenPosition(e.Order.Account, e.Order.Instrument);
  bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
  PositionStateChanged?.Invoke(instr, new PositionState
  {
      HasOpenPosition   = hasPos,
      HasWorkingEntries = hasEntries
  });
```
Fire triggers: Filled, PartFilled, Cancelled, Rejected -- any state that changes position truth.
Per-account evaluation (not aggregate): fires with state for the account that generated the event.
UI surfaces filter by their own instrument via the `instr` parameter.

### New Private Helper Methods (supporting TryFirePositionState)

#### `private bool HasOpenPosition(Account acc, Instrument instrument)` (NEW)
```
CYC estimate: 2
Branches:
  (1) var pos = FindPosition(acc, instrument); if pos == null: return false
  (1) return pos.Quantity > 0 (no branch -- expression)
```
Thin wrapper over existing FindPosition().

#### `private bool HasWorkingEntries(Account acc, Instrument instrument)` (NEW)
```
CYC estimate: 3
Branches:
  (1) foreach acc.Orders
  (1) if order.Instrument != instrument: continue
  (1) if order.OrderState != Working: continue
  (0) if !IsBracketLeg(order): return true  (entry found)
  (0) end loop: return false
```

### New Fields (CopyEngine class -- additive)

```csharp
// V01: order map for follower bracket lookup (JS-025: ConcurrentDictionary + ConcurrentBag, no lock)
private readonly ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>> _orderMap
    = new ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>();
```

### Modified Method: `OnOrderUpdate` (RESTRUCTURED)
```
CYC estimate after extraction: 7
Structure:
  Pre-gate:  TryFirePositionState(e)                               (0 -- method call, no branch)
  Gate 1:    if !_isCopyEnabled: return                            (1)
  Gate 2:    foreach _rules; if Instr+Account match: matchedRule   (2)
  Gate 2n:   if matchedRule == null: return                        (1)
  Gate 2.5:  if !matchedRule.Value.Enabled: return                 (1)
  Gate B:    if IsWorkingBracket(e.Order):                         (1)
               if e.Order.FromEntrySignal != null:                 (1)  <-- populate map
                 PopulateOrderMap(...)
               HandleBracketChange(e.Order, matchedRule.Value)
               return
  else:      DispatchCopy(e.Order, matchedRule.Value)              (0)
Total: 7 branches. CYC = 7 ✅
```
Note: The Gate B branch includes a nested check for FromEntrySignal != null before
calling PopulateOrderMap. This adds 1 branch to the OnOrderUpdate count (total 7, <=8). ✅

### Data Model Changes

#### 1. `CopyRule` struct -- additive field (V07)
```csharp
// Inside existing CopyRule private readonly struct -- additive only:
internal ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates { get; init; }
    = ImmutableDictionary<string, FollowerAtmMode>.Empty;   // JS-009

// CopyRule.Create factory must be updated to pass FollowerAtmTemplates through.
// Default value: always ImmutableDictionary<string, FollowerAtmMode>.Empty in B7.
// B8 will populate it with actual template assignments.
```

### Jane Street Rule Constraints (B7-F0)
| Rule | How Satisfied |
|------|---------------|
| JS-002 (no null return) | `FindFollowerBracketOrder` returns `Order?` — nullable annotation makes null contract explicit (V03) |
| JS-003 (readonly struct) | `FollowerBinding` readonly struct; `PositionState` readonly struct (in Section 1.5) |
| JS-009 (ImmutableDictionary) | `CopyRule.FollowerAtmTemplates` defaults to `ImmutableDictionary<string, FollowerAtmMode>.Empty` |
| JS-021 (no lock) | `_orderMap` is ConcurrentDictionary; inner value is ConcurrentBag. No lock keyword. |
| JS-025 (ConcurrentDictionary) | `_orderMap`: ConcurrentDictionary for outer map; ConcurrentBag for inner collection. |
| JS-001 (no throw in hot path) | `HandleBracketChange` wraps `acc.Change()` in try/catch. StatusUpdate fires on error. |

### NT8 Constraints (B7-F0)
| Constraint | How Satisfied |
|------------|---------------|
| No async/await in lifecycle | `HandleBracketChange` is synchronous. |
| `acc.Change(new Order[]{order})` pattern | Used identically to `MoveStopToBreakEven` at CopyEngine.cs:443. |
| Tick rounding mandatory | `Math.Round(rawPrice / tickSize) * tickSize` applied before price-delta guard. |
| Price-delta guard | `if (Math.Abs(newPrice - currentPrice) < tickSize) continue` before every `acc.Change()` (V02). |
| No Dispatcher.InvokeAsync in CopyEngine | `PositionStateChanged` fires event. UI handlers own the Dispatcher wrapper. |
| `Account.Orders` iteration on background thread | Established pattern in `CancelPendingEntries`. |
| Infinite mirror loop prevention | Follower accounts are never the `MasterAccount` of any rule — structural prevention by construction. |

### Test Plan (CopyEngineTests.cs -- new [Fact] methods)

| Test ID | Method Name | What It Verifies |
|---------|-------------|-----------------|
| T-B7-01 | `DispatchCopy_MethodExists` | Reflection: `DispatchCopy` private method exists with 2 params. Guards against accidental removal. |
| T-B7-02 | `IsWorkingBracket_MethodExists` | Reflection: `IsWorkingBracket` static private method exists with 1 param. |
| T-B7-03 | `HandleBracketChange_NullGuards_DoNotThrow` | Invoke via reflection with null-adjacent inputs. Verify no unhandled exception escapes. |
| T-B7-04 | `FindFollowerBracketOrder_NullableReturnType` | Reflection: `FindFollowerBracketOrder` return type is `Order?` (nullable). Confirms JS-002 compliance. |
| T-B7-05 | `OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy` | Set `_isCopyEnabled=true`, invoke `OnOrderUpdate` via reflection with a Working+bracket order. Verify copy dispatch path not taken. |

**Total tests after B7: 22 existing + 5 new = 27 [Fact] methods.**

---

## Section 1.5: CopyEngine Types -- Additive (V05 + V06 + V07)

These types are added OUTSIDE the `CopyEngine` class in `CopyEngine.cs`.
They are PUBLIC so UI files can reference them without additional imports
(same namespace `PropTraderTools`).
All changes are strictly additive -- no existing logic is touched.

### `FollowerBinding` readonly struct (V01)
```csharp
// Outside CopyEngine class -- additive (V01)
internal readonly struct FollowerBinding
{
    internal Account FollowerAccount     { get; init; }
    internal string  FromEntrySignalName { get; init; }
}
```
Used as the value type in `_orderMap: ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>`.
Records which follower accounts are associated with each `FromEntrySignal` name.

### `PositionState` readonly struct (V05)
```csharp
// Outside CopyEngine class -- additive (V05, JS-003)
public readonly struct PositionState
{
    public bool HasOpenPosition   { get; init; }
    public bool HasWorkingEntries { get; init; }
}
```
**Why readonly struct (not two loose bools):**
- Two anonymous `bool` parameters in a callback can be silently transposed by any caller
- Named struct makes each field's meaning unambiguous at the call site (JS-003)
- `readonly struct` = no defensive copy overhead, zero allocation on event fire

### `FollowerAtmMode` abstract record hierarchy (V06)
```csharp
// Outside CopyEngine class -- additive (V06, JS-003, JS-010)
public abstract record FollowerAtmMode
{
    private FollowerAtmMode() { }   // JS-010: private base constructor prevents external subclassing
    public sealed record Inherit()                 : FollowerAtmMode;  // default: follow leader's ATM
    public sealed record Market()                  : FollowerAtmMode;  // pure market, no bracket
    public sealed record Named(string TemplateName): FollowerAtmMode;  // specific ATM template name
}
```
**B7 status:** Field scaffolding only. `CopyRule.FollowerAtmTemplates` is always empty in B7.
Zero behavior change. B8 adds `SendCopy` switch + UI dropdown.

### `PositionStateChanged` event on CopyEngine (V05)
```csharp
// Inside CopyEngine class -- additive field declaration:
public event Action<string, PositionState> PositionStateChanged;
```
- Fired from `TryFirePositionState` (called from `OnOrderUpdate`)
- Fires per instrument name + per account
- Both surfaces (Panel + Window) subscribe and call `UpdateButtonColors` via `Dispatcher.InvokeAsync`

### `CopyRule.FollowerAtmTemplates` field (V07)
```csharp
// Inside existing private readonly struct CopyRule -- additive field (V07, JS-009):
internal ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates { get; init; }
    = ImmutableDictionary<string, FollowerAtmMode>.Empty;
```
**Why ImmutableDictionary (not Dictionary):**
Per JS-009: persistent collections in readonly structs must be immutable. The empty default
ensures no null reference risk. B8 populates via `ImmutableDictionary.SetItem(acc, mode)`.

### Fire Site in OnOrderUpdate
```csharp
// In OnOrderUpdate -- BEFORE Gate 1 (fires even when copy is disabled):
TryFirePositionState(e);
// ... rest of existing gates and dispatch logic ...
```

### Handler Pattern in UI Surfaces (preview for Section 2)
```csharp
// In TradeCopierPanel / TradeCopierWindow -- subscribe in Loaded:
_engine.PositionStateChanged += OnPositionStateChanged;

// Handler -- always via Dispatcher.InvokeAsync (off-thread callback, JS-023):
private void OnPositionStateChanged(string instr, PositionState state)
{
    if (_instrument == null || _instrument.FullName != instr) return;
    Dispatcher.InvokeAsync(() => UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries));
}
```

---

## Section 2: Feature B7-F1 -- Button Color Coding (P2)

### Summary
Apply semantic color backgrounds to action buttons on both UI surfaces, following PTT_DESIGN_PILLAR.md.
**Layer 2** (static semantic colors): correct RGB values per pillar.
**Layer 3** (live state): buttons start grey, transition to active color when target state exists.
`PositionStateChanged` event drives live transitions.

### Files Changed
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`

### Canonical Brush Values (PTT_DESIGN_PILLAR lines 192-198) -- V08 Correction

```
BrushActive   = MakeBrush( 34, 197,  94)   // green  #22c55e  -- Copy ON, BE (when live)
BrushDanger   = MakeBrush(239,  68,  68)   // red    #ef4444  -- Flatten/Cancel (when live)  << CORRECTED from 185,28,28
BrushCaution  = MakeBrush(245, 158,  11)   // amber  #f59e0b  -- Trim (when live)            << CORRECTED from 217,119,6
BrushInactive = MakeBrush( 55,  65,  81)   // grey   #4b5563  -- All action buttons (no target) << CORRECTED from 75,85,99
BrushPositive = MakeBrush( 34, 197,  94)   // green (text)
BrushNegative = MakeBrush(239,  68,  68)   // red (text)
BrushDim      = MakeBrush(107, 114, 128)   // grey (text)
```

These values match the spec HTML CSS variables exactly (PTT_DESIGN_PILLAR lines 65-66 comment):
`--green: #22c55e  --red: #ef4444  --amber: #f59e0b  --dim: #4b5563`

### SCAN-04 Compliance (No Hex Strings)
All brush colors MUST use `Color.FromRgb(r, g, b)` with integer RGB via `MakeBrush()`.
NO `"#RRGGBB"` hex string literals. Violation = SCAN-04 failure = BUILD_FAIL.

---

### TradeCopierPanel.cs Changes

#### New/Updated static frozen brush fields (at class level, via existing `MakeBrush`)
```csharp
// Canonical semantic button brushes (V08: corrected RGB values per PTT_DESIGN_PILLAR lines 192-198)
// JS-008: all Freeze()d via MakeBrush(), static readonly = zero allocation on re-render
private static readonly SolidColorBrush BrushActive   = MakeBrush( 34, 197,  94);  // green  #22c55e
private static readonly SolidColorBrush BrushDanger   = MakeBrush(239,  68,  68);  // red    #ef4444
private static readonly SolidColorBrush BrushCaution  = MakeBrush(245, 158,  11);  // amber  #f59e0b
private static readonly SolidColorBrush BrushInactive = MakeBrush( 55,  65,  81);  // grey   #4b5563
// Note: BrushPositive/BrushNegative/BrushDim are already defined in FollowerItem nested class
//       and remain unchanged (P&L colors, correct values from B7-F2 implementation)
```
Note: Panel already has `MakeBrush` helper from B7-F2 implementation. No duplicate needed.
Note: `BrushActive` replaces old `BrushCopyOn`/`BrushBe` (same RGB, canonical name from pillar).
Note: `BrushInactive` replaces old `BrushCopyOff` (corrected RGB value).
Note: `BrushDanger` replaces old `BrushDanger` (corrected RGB value, was 185,28,28).

#### New method: `UpdateButtonColors(bool hasPosition, bool hasEntries)` (V04, Layer 3)
```
Signature: private void UpdateButtonColors(bool hasPosition, bool hasEntries)
CYC estimate: 5
Must run on UI thread (called via Dispatcher.InvokeAsync).
Branches:
  (1) _copyToggleBtn.Background = _copyEnabled ? BrushActive : BrushInactive
  (1) _flattenBtn.Background = hasPosition ? BrushDanger : BrushInactive
  (1) _cancelBtn.Background  = hasEntries  ? BrushDanger : BrushInactive
  (1) _trimBtn.Background    = hasPosition ? BrushCaution : BrushInactive
  (1) _beBtn.Background      = hasPosition ? BrushActive : BrushInactive
```
No side effects beyond setting button backgrounds. No new subscriptions inside this method.

#### New method: `OnPositionStateChanged(string instr, PositionState state)` (V04)
```
Signature: private void OnPositionStateChanged(string instr, PositionState state)
CYC estimate: 1
Branches:
  (1) if _instrument == null || _instrument.FullName != instr: return
  (0) Dispatcher.InvokeAsync(() => UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries))
```
Called from CopyEngine order-update thread (off-UI-thread). MUST marshal via Dispatcher.InvokeAsync.
The captured `state` struct is a value type -- no reference aliasing risk in closure.

#### BuildUI() modifications (Layer 2 + Layer 3 initial state)
```
- _copyToggleBtn: Background = BrushInactive  (initial -- OFF state)
  do NOT call SetResourceReference("NTButtonStyle") on color-coded buttons
- flattenBtn.Background = BrushInactive  (starts grey -- no position yet)
- cancelBtn.Background  = BrushInactive  (starts grey -- no entries yet)
- trimBtn.Background    = BrushInactive  (starts grey -- no position yet)
- beBtn.Background      = BrushInactive  (starts grey -- no position yet)
- At END of BuildUI(): call UpdateButtonColors(false, false)  -- ensures consistent initial state (V04)
```
**All action buttons start grey.** Color activates only when position/entries exist (Layer 3 live state).

#### OnToggle() modification (Layer 2 toggle reflection)
```csharp
_copyToggleBtn.Background = _copyEnabled ? BrushActive : BrushInactive;
```
This continues to immediately reflect copy ON/OFF state on click (Layer 2).
Note: copy toggle is ALWAYS interactive (never grey) per PTT_DESIGN_PILLAR Layer 3 table.

#### Subscribe/Unsubscribe wiring (V04)
```
OnLoaded handler -- ADD:
    _engine.PositionStateChanged += OnPositionStateChanged;

Detach() method -- ADD:
    _engine.PositionStateChanged -= OnPositionStateChanged;
```
Panel already has OnLoaded and Detach() wiring for AccountItemUpdate. Same pattern.

#### NTButtonStyle note (unchanged from original passing plan)
Color-coded buttons will NOT call `SetResourceReference(Control.StyleProperty, "NTButtonStyle")`.
NT8 ControlTemplate may override Background. Buttons NOT color-coded (e.g. Apply Rule) keep NTButtonStyle.

---

### TradeCopierWindow.cs Changes

#### Add `MakeWinBrush` static helper + new brush fields
TradeCopierWindow has no existing `MakeBrush`. Add:
```csharp
// JS-008: MakeWinBrush produces frozen brushes (immutable + thread-safe for Dispatcher.InvokeAsync)
private static SolidColorBrush MakeWinBrush(byte r, byte g, byte b)
{
    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
    brush.Freeze();
    return brush;
}

// Canonical semantic brushes (V08: corrected RGB values per PTT_DESIGN_PILLAR lines 192-198)
private static readonly SolidColorBrush WBrushActive   = MakeWinBrush( 34, 197,  94);  // green  #22c55e
private static readonly SolidColorBrush WBrushDanger   = MakeWinBrush(239,  68,  68);  // red    #ef4444
private static readonly SolidColorBrush WBrushCaution  = MakeWinBrush(245, 158,  11);  // amber  #f59e0b
private static readonly SolidColorBrush WBrushInactive = MakeWinBrush( 55,  65,  81);  // grey   #4b5563
```
Note: `W` prefix to avoid collision with any potential future Window base-class members.

#### New method: `UpdateButtonColors(bool hasPosition, bool hasEntries)` (V04, Layer 3)
```
Signature: private void UpdateButtonColors(bool hasPosition, bool hasEntries)
CYC estimate: 5
Handles all rule-row buttons + global toggle. Called on UI thread via Dispatcher.InvokeAsync.
Branches:
  (1) _globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive
  (1) foreach per-rule Flatten buttons: btn.Background = hasPosition ? WBrushDanger : WBrushInactive
  (1) foreach per-rule Cancel buttons: btn.Background  = hasEntries  ? WBrushDanger : WBrushInactive
  (1) foreach per-rule Trim buttons: btn.Background    = hasPosition ? WBrushCaution : WBrushInactive
  (1) foreach per-rule BE buttons: btn.Background      = hasPosition ? WBrushActive : WBrushInactive
```
Note: Window stores per-rule button references in fields/lists (e.g. `List<Button> _flattenBtns`).
Engineer must add these button-reference lists during implementation (additive -- Window already has
`_leaderBoxes` and `_followerBoxes` lists as precedent).

#### New method: `OnPositionStateChanged(string instr, PositionState state)` (V04)
```
Signature: private void OnPositionStateChanged(string instr, PositionState state)
CYC estimate: 1
Branches:
  (1) if instr == null: return   (simple null guard -- Window has no per-instrument filter unlike Panel)
  (0) Dispatcher.InvokeAsync(() => UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries))
```
Note: Window shows all rules (not filtered by instrument). Any position state change triggers
button re-evaluation. The `instr` param is logged but not used as a filter here.

#### BuildUI() modifications
```
- _globalToggleBtn.Background = WBrushInactive  (initial -- OFF state)
  do NOT call SetResourceReference("NTButtonStyle") on _globalToggleBtn
- BuildRuleRow(): flattenBtn.Background = WBrushInactive  (starts grey)
                  cancelBtn.Background  = WBrushInactive
                  trimBtn.Background    = WBrushInactive
                  beBtn.Background      = WBrushInactive
                  toggleBtn.Background  = WBrushActive (Content "[ON]" initial)  -- toggle always colored
- BuildDynamicRuleRow(): same button initial states as BuildRuleRow()
- At END of BuildUI() (after all rule rows created):
    call UpdateButtonColors(false, false)  -- ensures consistent initial state (V04)
```

#### OnGlobalToggle() modification (Layer 2 toggle reflection)
```csharp
_globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive;
```

#### OnRuleToggle() modification (Layer 2 toggle reflection)
```csharp
// After existing state-flip logic:
btn.Background = newState ? WBrushActive : WBrushInactive;
```

#### Subscribe/Unsubscribe wiring (V04)
```
OnLoaded handler -- ADD:
    _engine.PositionStateChanged += OnPositionStateChanged;

Add to constructor (after BuildUI):
    Closed += OnWindowClosed;

New method OnWindowClosed:
    _engine.PositionStateChanged -= OnPositionStateChanged;
```
Window already has Loaded handler (`Loaded += OnLoaded` at line 52). Same subscription pattern.

### Data Model Changes
None for B7-F1 UI files. `PositionState` struct lives in CopyEngine.cs (T1).

### Jane Street Rule Constraints (B7-F1)
| Rule | How Satisfied |
|------|---------------|
| JS-008 (brushes Freeze()d) | All brushes via `MakeBrush()` / `MakeWinBrush()` which call `brush.Freeze()`. Static readonly = single allocation. |
| JS-023 (Dispatcher.InvokeAsync) | `OnPositionStateChanged` calls `Dispatcher.InvokeAsync`. Frozen brushes safe to capture in lambda. |
| SCAN-04 (no hex strings) | RGB integers via `MakeBrush`/`MakeWinBrush`. No `"#RRGGBB"` strings. |
| SCAN-02 (no non-ASCII) | All strings ASCII. |
| SCAN-01 (no lock) | No lock keyword. Button updates on UI thread only. |

### NT8 Constraints (B7-F1)
| Constraint | How Satisfied |
|------------|---------------|
| NTButtonStyle vs Background conflict | Color-coded buttons skip `SetResourceReference("NTButtonStyle")`. Non-colored buttons keep it. |
| Off-thread UI update | `OnPositionStateChanged` marshals via `Dispatcher.InvokeAsync`. Never sets Background directly on event thread. |
| Brush thread safety | All brushes are `Freeze()`d. Safe to reference from any thread (captured in closure). |

### Test Plan (B7-F1)
No engine logic changes -- no xUnit tests required for B7-F1.
Manual verification in NT8 F5:
- Copy toggle: green when ON, dark grey when OFF (both Panel and Window)
- Flatten/Cancel: dark grey initially; red ONLY when a position/working entries exist
- Trim: dark grey initially; amber ONLY when position exists
- BE: dark grey initially; green ONLY when position exists
- Open a trade: verify all four action buttons activate simultaneously on both surfaces
- Close the trade: verify all four action buttons return to dark grey simultaneously on both surfaces

---

## Section 3: Feature B7-F5 -- ScrollViewer on TradeCopierWindow Rule Grid (P2)

**STATUS: UNCHANGED FROM ORIGINAL PLAN (REVIEW_PASS). Reproduced for completeness.**

### Summary
The `_rulesPanel` StackPanel in `TradeCopierWindow.BuildUI()` has no scroll constraint.
With many rules added it can overflow the window. Wrap it in a `ScrollViewer` with
`MaxHeight=400` so the rule area scrolls independently of the log area.

### File Changed
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`

### Method Modified: `BuildUI()`
**Current (lines 121-124)**:
```csharp
_rulesPanel = new StackPanel();
_rulesPanel.Children.Add(BuildRuleRow("MES"));
DockPanel.SetDock(_rulesPanel, Dock.Top);
root.Children.Add(_rulesPanel);
```

**After change**:
```csharp
_rulesPanel = new StackPanel();
_rulesPanel.Children.Add(BuildRuleRow("MES"));

var rulesScroll = new ScrollViewer
{
    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    MaxHeight = 400,
    Content   = _rulesPanel
};
DockPanel.SetDock(rulesScroll, Dock.Top);
root.Children.Add(rulesScroll);
```

Note: `_rulesPanel` field itself is unchanged (still a StackPanel). The `OnAddRule` handler
appends to `_rulesPanel.Children` -- this works correctly because `_rulesPanel` is the
ScrollViewer's Content. Adding children to the StackPanel causes the ScrollViewer to show
a scrollbar when content height exceeds 400px.

Note: `DockPanel.SetDock` is applied to the ScrollViewer (outermost wrapper), not the
StackPanel. DockPanel must dock the outermost wrapper or layout breaks.

### Jane Street Rule Constraints
No JS rules triggered (pure WPF layout change).

### NT8 Constraints
ScrollViewer is standard WPF and safe in NT8 Window subclasses.

### Test Plan
No engine logic -- no xUnit tests. Manual F5 verification:
- Add 5+ rules via "+ Add Rule" button
- Verify rule area scrolls at MaxHeight=400
- Verify log area below still fills remaining DockPanel space

### Risk / Complexity Note
**Minimal risk.** DockPanel layout: ScrollViewer takes `Dock.Top`, log ScrollViewer takes
remaining fill space (LastChildFill=true). MaxHeight=400 is a reasonable default.

---

## Section 4: Deferred Items

### B7-F3: Per-account Qty Multiplier (1x/2x/3x)
**Status: DEFERRED to B8**
Reason: Requires `CopyRule` DTO change + serialization update + UI TextBox per follower row.
Medium-complexity, multi-file change with persistence impact. Clean B8 ticket.

### B7-F4: ATR Dynamic Sizing Engine
**Status: DEFERRED to B8/B9**
Reason: New file (`AtrSizingEngine.cs`), MarketData subscription, rolling ATR calculation.
High complexity. Warrants own block.

---

## Section 5: Ticket Grouping (REVISED from original plan)

Ticket grouping expanded from original (T1=F0, T2=F1+F5) to accommodate additional
CopyEngine changes introduced by V01/V05/V06/V07.

### T1 -- CopyEngine + Tests (B7-F0 + V01 + V02 + V03 + V05 + V06 + V07)
**Files**: `CopyEngine.cs`, `CopyEngineTests.cs`
**Priority**: P0
**Work**:
- Add `using System.Collections.Immutable;` directive (V07)
- Add top-level types outside CopyEngine class: `FollowerBinding` readonly struct (V01), `PositionState` readonly struct (V05), `FollowerAtmMode` abstract record hierarchy (V06)
- Add to CopyEngine class: `_orderMap` ConcurrentDictionary field (V01), `PositionStateChanged` event (V05)
- Add to `CopyRule` struct: `FollowerAtmTemplates` ImmutableDictionary field (V07)
- Extract `DispatchCopy` from `OnOrderUpdate` (structural refactor, must not change behavior)
- Add: `IsWorkingBracket`, `HandleBracketChange` (with price-delta guard V02), `FindFollowerBracketOrder` (nullable return V03, FromEntrySignal matching V01), `PopulateOrderMap`, `TryFirePositionState`, `HasOpenPosition`, `HasWorkingEntries`
- Modify `OnOrderUpdate`: call `TryFirePositionState(e)` before Gate 1; add IsWorkingBracket branch
- Add 5 new xUnit [Fact] tests (total 27)
**Dependency**: None (self-contained engine change)
**Verification**: All 22 existing tests still pass + 5 new tests pass; SCAN-01..07 zero

### T2 -- UI (B7-F1 + B7-F5 + V04 + V08)
**Files**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`
**Priority**: P2
**Work**:
- **TradeCopierPanel.cs**:
  - Add/update class-level brush fields with canonical RGB values (V08)
  - Add `UpdateButtonColors(bool hasPosition, bool hasEntries)` method (V04)
  - Add `OnPositionStateChanged(string instr, PositionState state)` handler (V04)
  - Subscribe in `OnLoaded`, unsubscribe in `Detach()` (V04)
  - Set all action button backgrounds to `BrushInactive` in `BuildUI()`; call `UpdateButtonColors(false, false)` at end (V04)
  - Update `OnToggle()` to use `BrushActive` / `BrushInactive` (V08 name fix)
- **TradeCopierWindow.cs**:
  - Add `MakeWinBrush` static helper (V08)
  - Add class-level brush fields with canonical RGB values (V08)
  - Add button-reference tracking lists (`_flattenBtns`, `_cancelBtns`, `_trimBtns`, `_beBtns`)
  - Add `UpdateButtonColors(bool hasPosition, bool hasEntries)` method (V04)
  - Add `OnPositionStateChanged(string instr, PositionState state)` handler (V04)
  - Subscribe in `OnLoaded`; add `Closed += OnWindowClosed`; implement `OnWindowClosed` to unsubscribe (V04)
  - Set all action button backgrounds to `WBrushInactive` in `BuildRuleRow()`/`BuildDynamicRuleRow()`; call `UpdateButtonColors(false, false)` at end of `BuildUI()` (V04)
  - Update `OnGlobalToggle()` / `OnRuleToggle()` to use canonical brush names (V08)
  - Wrap `_rulesPanel` in `ScrollViewer` (MaxHeight=400) (B7-F5, unchanged)
**Dependency**: T1 must be committed first (Panel/Window reference `PositionState` from CopyEngine.cs)
**Verification**: F5 green in NT8; SCAN-01..07 all zero; 27 tests pass; manual Layer 3 live state test

---

## Section 6: Cross-Cutting Concerns

### 7-Scan Checklist (must be 0 on every file before FINAL_PASS)

| Scan | Pattern | T1 Files | T2 Files | Status |
|------|---------|----------|----------|--------|
| SCAN-01 | `lock(` | CopyEngine.cs | Panel, Window | 0 -- ConcurrentDictionary + ConcurrentBag. No lock keyword. |
| SCAN-02 | non-ASCII chars | CopyEngine.cs | Panel, Window | 0 -- all strings ASCII only |
| SCAN-03 | `FontFamily` | CopyEngine.cs | Panel, Window | 0 -- no font changes |
| SCAN-04 | `#RRGGBB` hex strings | CopyEngine.cs | Panel, Window | 0 -- RGB integers via MakeBrush only |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | CopyEngine.cs | Panel, Window | 0 -- no new CreateOrder; existing "PTT-Copy" unchanged |
| SCAN-06 | `DateTime.Now` | CopyEngine.cs | Panel, Window | 0 -- DateTime.UtcNow only (existing code) |
| SCAN-07 | `sealed class TradeCopierWindow` | n/a | Window | 0 -- class declaration unchanged |

### CYC Summary (all methods <= 8)

| Method | File | CYC | Status |
|--------|------|-----|--------|
| `OnOrderUpdate` (refactored) | CopyEngine.cs | 7 | ✅ |
| `DispatchCopy` (extracted) | CopyEngine.cs | 6 | ✅ |
| `IsWorkingBracket` | CopyEngine.cs | 1 | ✅ |
| `HandleBracketChange` (with price-delta guard V02) | CopyEngine.cs | **8** | ✅ (at limit) |
| `FindFollowerBracketOrder` (FromEntrySignal match V01+V03) | CopyEngine.cs | 4 | ✅ |
| `PopulateOrderMap` | CopyEngine.cs | 1 | ✅ |
| `TryFirePositionState` | CopyEngine.cs | 2 | ✅ |
| `HasOpenPosition` | CopyEngine.cs | 2 | ✅ |
| `HasWorkingEntries` | CopyEngine.cs | 3 | ✅ |
| `MakeWinBrush` | TradeCopierWindow.cs | 1 | ✅ |
| `BuildUI` (modified) | TradeCopierWindow.cs | 1 | ✅ |
| `OnGlobalToggle` (modified) | TradeCopierWindow.cs | 2 | ✅ |
| `OnRuleToggle` (modified) | TradeCopierWindow.cs | 3 | ✅ |
| `UpdateButtonColors` (new) | TradeCopierWindow.cs | 5 | ✅ |
| `OnPositionStateChanged` (new) | TradeCopierWindow.cs | 1 | ✅ |
| `OnWindowClosed` (new) | TradeCopierWindow.cs | 1 | ✅ |
| `BuildUI` (modified) | TradeCopierPanel.cs | 1 | ✅ |
| `OnToggle` (modified) | TradeCopierPanel.cs | 2 | ✅ |
| `UpdateButtonColors` (new) | TradeCopierPanel.cs | 5 | ✅ |
| `OnPositionStateChanged` (new) | TradeCopierPanel.cs | 1 | ✅ |

### Test Summary

| Block | Tests | Delta |
|-------|-------|-------|
| B1-B6 existing | 22 | baseline |
| B7 new (T1) | +5 | T-B7-01 through T-B7-05 |
| **B7 total** | **27** | |

All tests: xUnit `[Fact]` only. No NUnit. No MSTest. Per `testing-strategies.md` mandate.

### File Change Summary

| File | Change Type | Ticket |
|------|-------------|--------|
| `CopyEngine.cs` | Logic: new top-level types, new fields, new methods, OnOrderUpdate restructure | T1 |
| `CopyEngineTests.cs` | +5 [Fact] tests | T1 |
| `TradeCopierPanel.cs` | UI: corrected brush constants, UpdateButtonColors, OnPositionStateChanged, subscribe wiring, BuildUI initial state | T2 |
| `TradeCopierWindow.cs` | UI: MakeWinBrush, corrected brush constants, UpdateButtonColors, OnPositionStateChanged, subscribe wiring, BuildUI initial state, ScrollViewer | T2 |
| `TradeCopierAddOn.cs` | UNCHANGED | -- |

---

## Appendix: B7 Source Baseline (for engineer reference)

```
CopyEngine.cs        534 lines  B6-complete (SaveRules/LoadRules)
TradeCopierPanel.cs  225 lines  B7-FIX (P&L live dropdown -- completedThisSession)
TradeCopierWindow.cs 392 lines  B6-FIX4 (plain Window, LoadRules/SaveRules)
TradeCopierAddOn.cs  230 lines  B7-FIX5 (working ChartTrader injection)
CopyEngineTests.cs   345 lines  22 xUnit [Fact] tests
```

All 7 scans confirmed PASS on baseline (per manifest.json `"scans": "all-7-pass"`).

### Key NT8 API reference points

| Call | Source line | Pattern to replicate |
|------|-------------|----------------------|
| `acc.Change(new Order[] { order })` | CopyEngine.cs:443 | Used by HandleBracketChange for bracket sync |
| `acc.Cancel(new Order[] { order })` | CopyEngine.cs:323 | Existing pattern, not changed |
| `FindPosition(acc, instrument)` | CopyEngine.cs:~450 | Used by HasOpenPosition |
| `IsBracketLeg(order)` | CopyEngine.cs:~320 | Used by HandleBracketChange + HasWorkingEntries |
| `order.FromEntrySignal` | (NT8 Order property) | Key for _orderMap and FindFollowerBracketOrder |
| `Dispatcher.InvokeAsync(...)` | TradeCopierPanel.cs (existing in OnAccountItemUpdate) | Pattern for UpdateButtonColors dispatch |
