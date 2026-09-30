# PTT-COPIER-B1 Architecture Plan

**Status:** PLAN_COMPLETE
**Spec:** specs/002-trade-copier-spec.html
**Date:** 2026-07-06

---

## 1. Overview

- **Epic:** PTT-COPIER-B1
- **Spec:** specs/002-trade-copier-spec.html
- **Target:** 3 C# files, ~350 lines total
- **Wave workspace path:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

### Design Pillars

1. Parasitic infrastructure: we borrow NT's plumbing, we own one event hook and two injected UI surfaces.
2. Hot potato: receive signal, forward signal, get out of the way. Minimal ownership.
3. Zero-launch: the Add-On is registered at install. No startup moment visible to the user.
4. Correctness by construction: illegal states are structurally unrepresentable in the type system.
5. Lock-free throughout: volatile + ConcurrentDictionary only. No lock() anywhere.

---

## 2. File Map

| File | Path in Wave workspace | Lines | Role |
|------|------------------------|-------|------|
| `CopyEngine.cs` | `src\PropTraderTools\CopyEngine.cs` | ~170 | Pure logic. Singleton. Subscribes to Account.All.OrderUpdate once. Gate chain, copy dispatch, trim, flatten, cancel, dedup. Zero UI references. |
| `TradeCopierPanel.cs` | `src\PropTraderTools\TradeCopierPanel.cs` | ~100 | ChartTrader row extension. Copy ON/OFF toggle, Trim/Flatten/Cancel buttons, keyboard shortcuts. Calls CopyEngine.Instance. No reference to Window. |
| `TradeCopierWindow.cs` | `src\PropTraderTools\TradeCopierWindow.cs` | ~80 | NTWindow Add-On. Rule management (instrument + leader + followers), status log TextBlock, global on/off. Calls CopyEngine.Instance. No reference to Panel. |

**Dependency graph:** `CopyEngine <- TradeCopierPanel`, `CopyEngine <- TradeCopierWindow`. Panel and Window are mutually independent. T2 and T3 can be built in parallel once T1 (CopyEngine) is complete.

---

## 3. Data Structures

All three structs are `private readonly struct` in CopyEngine.cs. Private constructors enforce JS-010. No mutable fields (JS-008). Structs are nested within the CopyEngine class to prevent external instantiation.

### 3.1 CopyRule

Configuration record. One rule = one instrument + one master account + one follower set.

```
private readonly struct CopyRule
{
    public readonly string    Instrument;
    public readonly Account   MasterAccount;
    public readonly Account[] FollowerAccounts;

    private CopyRule(string instrument, Account master, Account[] followers)
    {
        Instrument       = instrument;
        MasterAccount    = master;
        FollowerAccounts = followers;
    }

    public static CopyRule Create(string instrument, Account master, Account[] followers)
        => new CopyRule(instrument, master, followers);
}
```

**JS rules applied:**
- JS-003: Illegal state (no instrument or no master) is unrepresentable via private ctor.
- JS-008: All fields are readonly -- struct cannot be mutated after creation.
- JS-010: Private constructor + static Create() factory.

### 3.2 CopySignal

Per-order copy payload. Created once per qualifying order event. Passed by `in` reference to SendCopy.

```
private readonly struct CopySignal
{
    public readonly OrderAction Action;
    public readonly OrderType   Type;
    public readonly int         Quantity;
    public readonly double      LimitPrice;
    public readonly string      OrderId;

    private CopySignal(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)
    {
        Action     = action;
        Type       = type;
        Quantity   = qty;
        LimitPrice = limitPrice;
        OrderId    = orderId;
    }

    public static CopySignal Create(OrderAction action, OrderType type, int qty,
                                    double limitPrice, string orderId)
        => new CopySignal(action, type, qty, limitPrice, orderId);
}
```

**JS rules applied:**
- JS-001: Struct is a plain data carrier -- no throw, returned bool from SendCopy handles errors.
- JS-008: Fully readonly. Immutable after creation.
- JS-010: Private constructor + static Create() factory.

### 3.3 TrimSignal

Trim command payload. Carries NO qty field by design. Each account reads its own live position
and independently computes ceil(qty/2). Quantity synchronization across accounts is structurally
impossible -- illegal state is unrepresentable (JS-003 / JS-016).

```
private readonly struct TrimSignal
{
    // NO qty field -- by design. Each account reads account.Positions[Instrument].Quantity.
    public readonly DateTime UtcTime;
    public readonly string   Instrument;

    private TrimSignal(string instrument)
    {
        UtcTime    = DateTime.UtcNow;   // JS-006: never DateTime.Now (SCAN-06)
        Instrument = instrument;
    }

    public static TrimSignal Create(string instrument) => new TrimSignal(instrument);
}
```

**JS rules applied:**
- JS-003: Quantity sync across accounts is an illegal state. TrimSignal cannot carry qty -- unrepresentable by construction.
- JS-008: Fully readonly.
- JS-010: Private constructor + static Create() factory.

---

## 4. CopyEngine Public API

`CopyEngine` is a `sealed` singleton class in `CopyEngine.cs`. It owns the single `Account.All.OrderUpdate` subscription for the entire Add-On.

```
internal sealed class CopyEngine
```

### Singleton Access

```
public static CopyEngine Instance { get; } = new CopyEngine();
private CopyEngine() { }
```

### Initialization / Cleanup

```
public void Initialize(CopyRule rule)
```
Responsibility: Store the active rule, subscribe `Account.All.OrderUpdate += OnOrderUpdate`.
JS enforced: Called once at Add-On startup. Not re-entrant.

```
public void Shutdown()
```
Responsibility: Unsubscribe `Account.All.OrderUpdate -= OnOrderUpdate`. Engine stays inactive until re-initialized.
JS enforced: No lock -- unsubscription is atomic in NT's event infrastructure.

### Status Event

```
public event Action<string> StatusUpdate;
```
Fires from the NT strategy thread. Subscribers (TradeCopierWindow) must dispatch to UI thread via `Dispatcher.InvokeAsync`.

---

### 4.1 OnOrderUpdate

```
public void OnOrderUpdate(object sender, OrderEventArgs e)
```

**Responsibility:** 4-gate chain filtering all order events, then dispatching copy signals to each follower account that passes the daily cap check.

**JS enforced:**
- JS-001: No throw in this hot path. All exits are early returns.
- JS-021/SCAN-01: No lock(). _isCopyEnabled is volatile. _dedupCache is ConcurrentDictionary.
- SCAN-06: DateTime.UtcNow.Ticks used in IsDedup, never DateTime.Now.

---

### 4.2 SendCopy

```
public bool SendCopy(Account follower, in CopySignal signal)
```

**Responsibility:** Calls `follower.CreateOrder(...)` with name "PTT-Copy", then `.Submit()`. Returns `true` on success, `false` on any failure. Never throws (JS-001 hot potato).

**JS enforced:**
- JS-001: Returns bool, never throws.
- SCAN-05: Order name is "PTT-Copy" (PTT- prefix mandatory).
- SCAN-06: No DateTime.Now anywhere in this call.

---

### 4.3 Trim

```
public void Trim(Instrument instrument)
```

**Responsibility:** For every account in `AllAccounts(instrument)`, reads live position qty via `account.Positions[instrument].Quantity`, skips if flat (qty == 0), computes `(int)Math.Ceiling(Math.Abs(qty) / 2.0)`, determines exit direction (Sell if long, Buy if short), submits a market order named "PTT-Trim".

**JS enforced:**
- JS-003: TrimSignal has no qty -- each account computes its own half independently.
- SCAN-05: Order name is "PTT-Trim".

---

### 4.4 Flatten

```
public void Flatten(Instrument instrument)
```

**Responsibility:** For every account in `AllAccounts(instrument)`, reads full position qty, skips if flat, submits a market order for the entire quantity named "PTT-Flatten".

**JS enforced:**
- SCAN-05: Order name is "PTT-Flatten".

---

### 4.5 CancelPendingEntries

```
public void CancelPendingEntries(Instrument instrument)
```

**Responsibility:** For every account in `AllAccounts(instrument)`, iterates `account.Orders` and cancels any order where `OrderState == Working` AND `IsBracketLeg(order)` returns `false`. Never cancels bracket legs (stops, targets) or our own PTT- orders.

**JS enforced:**
- JS-003: IsBracketLeg provides structural safety -- bracket cancellation is unrepresentable by the 3-layer guard.
- SCAN-05: Only cancels orders that are NOT prefixed "PTT-".

---

### 4.6 IsDedup

```
public bool IsDedup(string orderId)
```

**Responsibility:** Returns `true` if `orderId` was already seen within the last 10 seconds (duplicate event). Uses `_dedupCache: ConcurrentDictionary<string, long>` keyed on orderId, value = `DateTime.UtcNow.Ticks`. On each call: prune entries older than 10 seconds (10_000_000 * 10 ticks), then attempt `TryAdd`. If `TryAdd` fails (key exists and is fresh), return true (is a dup). If TryAdd succeeds, return false (new event).

**JS enforced:**
- JS-021/JS-025/SCAN-01: ConcurrentDictionary -- no lock() needed.
- SCAN-06: DateTime.UtcNow.Ticks only.

---

### 4.7 SetEnabled

```
public void SetEnabled(bool enabled)
```

**Responsibility:** Sets `_isCopyEnabled = enabled`. Fires `StatusUpdate("Copy " + (enabled ? "ON" : "OFF"))`. Both UI surfaces (Panel toggle and Window on/off) call this method. The volatile write is immediately visible to OnOrderUpdate on any thread.

**JS enforced:**
- JS-023/SCAN-01: `_isCopyEnabled` is `volatile bool`. Write is atomic.

---

### 4.8 AllAccounts

```
private IEnumerable<Account> AllAccounts(Instrument instrument)
```

**Responsibility:** Returns the master account plus all follower accounts for the rule that matches the given instrument. This is the instrument fence -- accounts belonging to a different instrument's rule are never returned. MES trim never sees MNQ accounts. Returns empty if no matching rule.

**JS enforced:**
- JS-003: Instrument fence is structural -- wrong-instrument accounts cannot be returned.

---

### 4.9 IsBracketLeg

```
private bool IsBracketLeg(Order order)
```

**Responsibility:** Returns `true` if the order is a bracket leg that must never be cancelled. Uses 3-layer guard (see Section 6).

**JS enforced:**
- JS-003: 3-layer guard makes accidental bracket cancellation unrepresentable.

---

### 4.10 PassesDailyCapCheck

```
private bool PassesDailyCapCheck(Account account)
```

**Responsibility:** Returns `true` if the account's realized P&L for today is above the configured loss floor, or if daily cap enforcement is disabled. Reads `account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar)`. Returns `false` (skip this follower) if below threshold.

---

## 5. Gate Chain

The following pseudocode shows the exact 4-gate chain in `OnOrderUpdate`. All gates use early return -- no nesting, linear flow, CYC stays low.

```
OnOrderUpdate(object sender, OrderEventArgs e):

    Order order = e.Order;

    // Gate 1 -- enabled check
    if (!_isCopyEnabled)
        return;

    // Gate 2 -- source match: master account AND instrument must match active rule
    if (order.Account != _rule.MasterAccount)
        return;
    if (order.Instrument.FullName != _rule.Instrument)
        return;

    // Gate 3 -- state and type: must be Submitted, must be Market or Limit
    if (order.OrderState != OrderState.Submitted)
        return;
    bool isMarket = order.OrderType == OrderType.Market;
    bool isLimit  = order.OrderType == OrderType.Limit;
    if (!isMarket && !isLimit)
        return;

    // Gate 4 -- dedup: reject duplicate event for same orderId
    if (IsDedup(order.Id.ToString()))
        return;

    // All gates passed -- build signal and dispatch
    CopySignal signal = CopySignal.Create(
        order.OrderAction, order.OrderType, order.Quantity,
        order.LimitPrice, order.Id.ToString());

    foreach (Account follower in _rule.FollowerAccounts)
    {
        if (!PassesDailyCapCheck(follower))
            continue;
        bool sent = SendCopy(follower, in signal);
        if (sent)
            StatusUpdate?.Invoke("Copied " + order.OrderAction + " " + order.Quantity
                + " " + _rule.Instrument + " -> " + follower.Name);
    }
```

---

## 6. IsBracketLeg -- 3-Layer Guard

`IsBracketLeg(Order order)` returns `true` on the FIRST layer that matches. Cancellation is skipped for any order where this returns `true`.

```
private bool IsBracketLeg(Order order)
{
    // Layer 1 -- structural: ATM-stamped orders carry a FromEntrySignal reference.
    // This is set by NT's ATM engine -- if non-null, the order is part of a bracket.
    if (order.FromEntrySignal != null)
        return true;

    // Layer 2 -- name prefix: our own PTT- orders must never self-cancel.
    // A PTT-Flatten or PTT-Trim order in Working state must not be cancelled by
    // a subsequent CancelPendingEntries call. The prefix is the safety net.
    if (order.Name.StartsWith("PTT-"))
        return true;

    // Layer 3 -- name convention: NT ATM names stops and targets with these prefixes.
    // Belt-and-suspenders for ATM orders that may not have FromEntrySignal set.
    if (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target"))
        return true;

    return false;
}
```

**Layer rationale:**
- Layer 1 is structural -- catches all properly ATM-stamped brackets.
- Layer 2 is self-protection -- prevents our own pending orders from being cancelled.
- Layer 3 is belt-and-suspenders -- catches NT-named brackets that may slip through Layer 1.

---

## 7. TradeCopierPanel API

`TradeCopierPanel` is a ChartTrader row extension. One instance per chart. All instances share the same `CopyEngine.Instance` singleton.

```
public class TradeCopierPanel : NTWindow
```

### 7.1 OnInitialize

```
protected override void OnInitialize()
```

Responsibility: Get `CopyEngine.Instance`, bind to the chart's current instrument (via `ChartControl.Instrument`). No engine startup here -- engine is already live.

### 7.2 OnDestroyed

```
protected override void OnDestroyed()
```

Responsibility: Unbind instrument reference only. Engine keeps running. Other panel instances and the Window are unaffected.

### 7.3 BuildUI

```
private void BuildUI()
```

Responsibility: Construct two rows inside the ChartTrader row slot using NT-native WPF controls.

```
Row 1: [  COPY ON/OFF  ]         -- Toggle button, full width
Row 2: [Trim 1/2  S+T] [Flatten  S+F] [Cancel  S+C]
```

All controls use:
- `Style="{DynamicResource NTButtonStyle}"` for buttons (SCAN-03/04)
- `Foreground` and `Background` via `NTBrushes.*` resource keys only (SCAN-04)
- No FontFamily override -- inherit NT WPF theme (SCAN-03)

### 7.4 Button Handlers

```
private void OnToggle()   -- calls CopyEngine.Instance.SetEnabled(!currentState)
private void OnTrim()     -- calls CopyEngine.Instance.Trim(chart instrument)
private void OnFlatten()  -- calls CopyEngine.Instance.Flatten(chart instrument)
private void OnCancel()   -- calls CopyEngine.Instance.CancelPendingEntries(chart instrument)
```

Each handler is a single call to CopyEngine. No logic in the handler itself.

### 7.5 Keyboard Shortcuts

Registered as WPF `KeyBinding` entries on the panel's `InputBindings` collection:

| Shortcut | Action | Handler |
|----------|--------|---------|
| Shift+T | Trim half position | OnTrim() |
| Shift+F | Flatten full position | OnFlatten() |
| Shift+C | Cancel pending entries | OnCancel() |

```csharp
InputBindings.Add(new KeyBinding(trimCommand,    Key.T, ModifierKeys.Shift));
InputBindings.Add(new KeyBinding(flattenCommand, Key.F, ModifierKeys.Shift));
InputBindings.Add(new KeyBinding(cancelCommand,  Key.C, ModifierKeys.Shift));
```

### 7.6 NT-Native UI Compliance

- All buttons: `Style="{DynamicResource NTButtonStyle}"` (SCAN-04)
- All color references: `NTBrushes.*` dynamic resource keys (SCAN-04)
- No `FontFamily` property set anywhere (SCAN-03)
- No hardcoded `#RRGGBB` hex values anywhere (SCAN-04)

---

## 8. TradeCopierWindow API

`TradeCopierWindow` subclasses `NinjaTrader.Gui.NTWindow`. NT provides the window chrome, docking, menu registration. We own the content area.

```
public class TradeCopierWindow : NTWindow
```

### 8.1 OnInitialize

```
protected override void OnInitialize()
```

Responsibility: Get `CopyEngine.Instance`. Subscribe `CopyEngine.Instance.StatusUpdate += OnStatusUpdate`. No new engine init -- singleton is already live.

### 8.2 BuildUI

```
private void BuildUI()
```

Responsibility: Construct the window content area:

```
[Global ON/OFF toggle]                             -- top row, full width

Per-rule rows (one row per CopyRule):
  [Instrument label] [Leader ComboBox] [Followers ComboBox] [1/2] [=] [X] [ON]
  -- Instrument: static label
  -- Leader: AccountComboBoxStyle ComboBox (NT-native account selector)
  -- Followers: CheckList-style ComboBox showing all accounts with checkboxes
  -- [1/2]: Trim button -> Trim(rule.Instrument)
  -- [=]: Flatten button -> Flatten(rule.Instrument)
  -- [X]: Cancel button -> CancelPendingEntries(rule.Instrument)
  -- [ON]: Per-rule enable toggle

Status log:
  [TextBlock _statusLog, scrollable, read-only, multi-line]
  -- Appended by OnStatusUpdate via Dispatcher.InvokeAsync
```

All controls use `NTButtonStyle`, `AccountComboBoxStyle`, and `NTBrushes.*` resource keys. No FontFamily override. No hex colors.

### 8.3 OnStatusUpdate

```
private void OnStatusUpdate(string line)
```

Responsibility: Engine fires `StatusUpdate` event on the NT strategy thread. This handler dispatches to the WPF UI thread using `Dispatcher.InvokeAsync` and appends the line to `_statusLog`. This is the ONLY method in the entire Add-On that requires a Dispatcher call -- all other UI interactions originate on the UI thread.

```
private void OnStatusUpdate(string line)
    => Dispatcher.InvokeAsync(() => _statusLog.AppendText(line + "\n"));
```

---

## 9. Concurrency Model

### 9.1 _isCopyEnabled

```
private volatile bool _isCopyEnabled;
```

- Declared `volatile` so reads and writes are always from/to main memory -- no CPU cache staleness.
- `SetEnabled(bool)` writes from the WPF UI thread (button click).
- `OnOrderUpdate` reads from the NT strategy thread (Gate 1).
- `volatile` guarantees the NT thread always sees the latest value without any lock.
- JS-023 compliant. SCAN-01/07 compliant.

### 9.2 _dedupCache

```
private readonly ConcurrentDictionary<string, long> _dedupCache = new();
```

- Keyed on `orderId` (string). Value is `DateTime.UtcNow.Ticks` at time of first seen.
- `ConcurrentDictionary` is lock-free internally (uses CAS operations).
- **Dedup expiry:** On every `IsDedup` call, entries older than 10 seconds are pruned.
  Threshold: `DateTime.UtcNow.Ticks - 10_000_000L * 10L` (10 seconds in 100-nanosecond ticks).
  Pruning uses `TryRemove` per stale key -- no lock needed.
- JS-025 compliant. SCAN-01/07 compliant. SCAN-06 compliant (DateTime.UtcNow only).

### 9.3 Thread Ownership

| Thread | Code running on it | Synchronization needed |
|--------|-------------------|------------------------|
| NT strategy thread | OnOrderUpdate, IsDedup, SendCopy, Trim, Flatten, CancelPendingEntries | None -- single consumer of gate chain |
| WPF UI thread | SetEnabled, OnToggle, OnTrim, OnFlatten, OnCancel, all BuildUI | None -- WPF handles UI thread safety |
| Cross-thread | StatusUpdate event: fired from NT thread, handled in Window | Dispatcher.InvokeAsync in OnStatusUpdate |

### 9.4 No lock() Contract

There is no `lock()` statement anywhere in any of the three files.

- `_isCopyEnabled`: `volatile bool` -- atomic reads/writes without lock.
- `_dedupCache`: `ConcurrentDictionary` -- built-in lock-free operations.
- `Account.All.OrderUpdate`: NT's event infrastructure is thread-safe -- subscribe/unsubscribe without lock.
- `StatusUpdate` event: `?.Invoke` is safe (delegate reference read is atomic).

SCAN-01 and SCAN-07 will return 0 results.

---

## 10. 7-Scan Compliance

For each scan, the specific design decision that guarantees it passes:

| Scan | Pattern | Design Decision | Guarantee |
|------|---------|-----------------|-----------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | No lock() anywhere. State uses volatile bool + ConcurrentDictionary only. | 0 results |
| SCAN-02 | Non-ASCII characters | All identifiers, string literals, comments are ASCII-only. No Unicode, no emoji, no curly quotes. | 0 results |
| SCAN-03 | `FontFamily` | No FontFamily property is set. All controls inherit NT's WPF theme dictionary automatically. | 0 results |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | All color references use `NTBrushes.*` dynamic resource keys. No hex literals anywhere. | 0 results |
| SCAN-05 | CreateOrder name param not starting with "PTT-" | All CreateOrder calls use: "PTT-Copy" (SendCopy), "PTT-Trim" (Trim), "PTT-Flatten" (Flatten). IsBracketLeg Layer 2 protects these names from self-cancellation. | 0 violations |
| SCAN-06 | `DateTime\.Now[^U]` | All timestamps use `DateTime.UtcNow`. Dedup TTL uses `DateTime.UtcNow.Ticks`. TrimSignal.UtcTime is `DateTime.UtcNow`. | 0 results |
| SCAN-07 | `\block\s*\(` | Belt-and-suspenders duplicate of SCAN-01. Same guarantee: no lock() anywhere. | 0 results |

---

## 11. Ticket Decomposition

Three tickets. T2 and T3 depend on T1. T2 and T3 are independent of each other and can be built in parallel once T1 is complete.

---

### T1: CopyEngine.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Lines:** ~170
**Dependencies:** None (pure logic, NT Cbi API only)

**Implement:**
- `private readonly struct CopyRule` with private ctor + static Create()
- `private readonly struct CopySignal` with private ctor + static Create()
- `private readonly struct TrimSignal` (NO qty field) with private ctor + static Create()
- `public sealed class CopyEngine` singleton
- `private volatile bool _isCopyEnabled`
- `private readonly ConcurrentDictionary<string, long> _dedupCache`
- `public static CopyEngine Instance`
- `public event Action<string> StatusUpdate`
- `public void Initialize(CopyRule rule)`
- `public void Shutdown()`
- `public void OnOrderUpdate(object sender, OrderEventArgs e)` -- 4-gate chain
- `public bool SendCopy(Account follower, in CopySignal signal)` -- "PTT-Copy"
- `public void Trim(Instrument instrument)` -- ceil(qty/2), "PTT-Trim"
- `public void Flatten(Instrument instrument)` -- full qty, "PTT-Flatten"
- `public void CancelPendingEntries(Instrument instrument)` -- "PTT-Cancel" (cancel name irrelevant, but cancels working non-bracket orders)
- `public bool IsDedup(string orderId)` -- 10-second TTL, ConcurrentDictionary
- `public void SetEnabled(bool enabled)` -- volatile bool write
- `private IEnumerable<Account> AllAccounts(Instrument instrument)` -- instrument fence
- `private bool IsBracketLeg(Order order)` -- 3-layer guard
- `private bool PassesDailyCapCheck(Account account)` -- P&L floor read

**xUnit tests to write:**
- `[Fact] Gate1_DisabledEngine_ReturnsBeforeCopy()`
- `[Fact] Gate2_WrongAccount_ReturnsBeforeCopy()`
- `[Fact] Gate2_WrongInstrument_ReturnsBeforeCopy()`
- `[Fact] Gate3_NotSubmitted_ReturnsBeforeCopy()`
- `[Fact] Gate3_StopOrder_ReturnsBeforeCopy()`
- `[Fact] Gate4_DuplicateOrderId_ReturnsBeforeCopy()`
- `[Fact] IsDedup_FreshEntry_ReturnsFalse()`
- `[Fact] IsDedup_SameIdWithinTtl_ReturnsTrue()`
- `[Fact] IsDedup_SameIdAfterTtlExpiry_ReturnsFalse()`
- `[Fact] IsBracketLeg_FromEntrySignalNotNull_ReturnsTrue()`
- `[Fact] IsBracketLeg_PttPrefix_ReturnsTrue()`
- `[Fact] IsBracketLeg_StopPrefix_ReturnsTrue()`
- `[Fact] IsBracketLeg_TargetPrefix_ReturnsTrue()`
- `[Fact] IsBracketLeg_RegularOrder_ReturnsFalse()`
- `[Fact] TrimSignal_HasNoQtyField_StructuralVerification()`
- `[Fact] CopySignal_Create_AllFieldsAssigned()`
- `[Fact] SetEnabled_VolatileWrite_ReflectedImmediately()`

**7-scan checklist:**
- SCAN-01: grep for lock( in CopyEngine.cs -- must return 0
- SCAN-02: non-ASCII scan -- must return 0
- SCAN-03: FontFamily scan -- must return 0
- SCAN-04: hex color scan -- must return 0
- SCAN-05: verify CreateOrder calls use "PTT-Copy", "PTT-Trim", "PTT-Flatten"
- SCAN-06: DateTime.Now scan -- must return 0
- SCAN-07: regex lock scan -- must return 0

---

### T2: TradeCopierPanel.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
**Lines:** ~100
**Dependencies:** T1 (CopyEngine.cs must be complete)

**Implement:**
- `public class TradeCopierPanel` (subclass of NTWindow -- NT8 NTWindow subclasses must not be sealed)
- `protected override void OnInitialize()` -- get CopyEngine.Instance, bind instrument
- `protected override void OnDestroyed()` -- unbind instrument only, engine keeps running
- `private void BuildUI()` -- two rows: toggle + action buttons
- `private void OnToggle()` -- SetEnabled toggle
- `private void OnTrim()` -- Trim(instrument)
- `private void OnFlatten()` -- Flatten(instrument)
- `private void OnCancel()` -- CancelPendingEntries(instrument)
- WPF KeyBinding registrations for Shift+T, Shift+F, Shift+C
- All buttons use `NTButtonStyle`
- All colors via `NTBrushes.*` -- no hex
- No FontFamily property anywhere

**xUnit tests to write:**
- `[Fact] OnToggle_CallsSetEnabled_WithFlippedState()`
- `[Fact] OnTrim_CallsTrimWithChartInstrument()`
- `[Fact] OnFlatten_CallsFlattenWithChartInstrument()`
- `[Fact] OnCancel_CallsCancelPendingEntriesWithChartInstrument()`
- `[Fact] OnDestroyed_DoesNotShutdownEngine()`

**7-scan checklist:**
- SCAN-01: grep for lock( in TradeCopierPanel.cs -- must return 0
- SCAN-02: non-ASCII scan -- must return 0
- SCAN-03: FontFamily scan -- must return 0
- SCAN-04: hex color scan -- must return 0
- SCAN-05: no CreateOrder calls in this file -- N/A (panel never creates orders)
- SCAN-06: DateTime.Now scan -- must return 0
- SCAN-07: regex lock scan -- must return 0

---

### T3: TradeCopierWindow.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
**Lines:** ~80
**Dependencies:** T1 (CopyEngine.cs must be complete)

**Implement:**
- `public class TradeCopierWindow : NTWindow`
- `protected override void OnInitialize()` -- get CopyEngine.Instance, subscribe StatusUpdate
- `private void BuildUI()` -- global toggle + per-rule rows + status log TextBlock
- Per-rule row controls: instrument label, leader AccountComboBoxStyle ComboBox, followers checklist ComboBox, Trim/Flatten/Cancel buttons, per-rule ON toggle
- `private void OnStatusUpdate(string line)` -- Dispatcher.InvokeAsync -> AppendText
- All buttons use `NTButtonStyle`
- Account ComboBoxes use `AccountComboBoxStyle`
- All colors via `NTBrushes.*` -- no hex
- No FontFamily property anywhere

**xUnit tests to write:**
- `[Fact] OnStatusUpdate_DispatchesToUiThread()`
- `[Fact] OnInitialize_SubscribesToStatusUpdateEvent()`
- `[Fact] OnStatusUpdate_AppendsLineToLog()`
- `[Fact] GlobalToggle_CallsSetEnabled()`

**7-scan checklist:**
- SCAN-01: grep for lock( in TradeCopierWindow.cs -- must return 0
- SCAN-02: non-ASCII scan -- must return 0
- SCAN-03: FontFamily scan -- must return 0
- SCAN-04: hex color scan -- must return 0
- SCAN-05: no CreateOrder calls in this file -- N/A (window never creates orders directly)
- SCAN-06: DateTime.Now scan -- must return 0
- SCAN-07: regex lock scan -- must return 0
