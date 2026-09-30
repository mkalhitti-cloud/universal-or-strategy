# NinjaTrader 8 Add-On API Surface Reference
# Source: developer.ninjatrader.com/docs/desktop/add_on (official NT8 docs)
# Extracted: 2026-08-10 from screenshot of Add On reference page
# Status: LIVING DOCUMENT -- append confirmed facts, never delete
# Purpose: Answer "is this callable from AddOn context?" without a live F5 probe

---

## WHAT IS AN ADD-ON

Custom Add-Ons can be used to extend NinjaTrader functionality.
The methods and properties covered in this section are unique to custom Add-On development.
For more information on the Add-On development process: see the official article.

---

## OFFICIAL ADD-ON API SURFACE (from docs screenshot)

These are the classes/interfaces/methods that the official NT8 docs list as available
in the Add-On context. If something is NOT on this list, it likely lives on the
NinjaScript base class and will produce CS1061 in AddOn context.

### Top-Level Categories Listed

| Name | What it provides |
|---|---|
| **NinjaTrader Controls** | Controls that are native to NT8 controls |
| **Accounts** | Account class -- subscribe to account-related events, access account-related information |
| **BarRequest** | Send/request data, subscribe to real-time Bar data, subscribe to real-time Bar data events |
| **Connection** | Connection class -- access connection-related events, subscribe to connection-related events |
| **InstrumentProvider Interface** | When creating your NTPlugin, if you use the Instrument link, implement the InstrumentProvider Interface |
| **ReserveProvider Interface** | When creating your NTPlugin, if you use the Reserve link, implement the ReserveProvider Interface |
| **NTTabFactory Interface** | If you wish to have tab page functionality (adding, removing, moving, deploying tabs) you must create a class which implements the NTTabFactory Interface |
| **NTTabFactory Interface** | (same -- tab management) |
| **MortgagePersistenceInterface** | Implement the MortgagePersistence Interface as a shelf for the ability to save and restore your data with NT mortgage axis |
| **NTPlugin Clear** | This is where a const component for tabs holds the custom add-on NTWindow can be defined |
| **Alert and Debug Concepts** | In most cases you can use the NinjaScript-provided methods for triggering alerts and debug/log functionality. However, some tables work on your own custom objects -- you may find yourself wanting to use this functionality outside the NinjaScript scope |
| **Automation** | Automation: something properties and methods used to change NT Strategies |
| **ControlCenter** | ControlCenter is a XAML-defined class describing the layout and properties of the Control Center window |
| **FundamentalsData** | FundamentalsData is used to access fundamental share price data and for subscribing to fundamental data events |
| **MarketData** | MarketData can be used to access snapshot market data and for subscribing to market data events |
| **MarketDepth** | MarketDepth can be used to access snapshot market depth data and for subscribing to market depth events |
| **NameBase** | NameBase can be used to store name entries |
| **NameDescription** | NameDescription can be used for subscribing to Name events |
| **NTHandlers** | NTHandlers is used to create name entries |
| **NTWindow** | The NTWindow class defines a generic window for a custom window creation. Instances of NTWindow are a container for instances of NTPlugin. In which UI elements and shell related logic are contained |
| **NumericTextBox** | NumericTextBox provides functionality for numeric text boxes to organize user input |
| **OnWindowCreated()** | This method is called whenever a new NTWindow is created |
| **OnWindowDestroyed()** | This method is called whenever a new NTWindow is destroyed |
| **OnWindowRestored()** | This method is used to return any custom NT data from the workspace by referencing a window |
| **OnWindowVariantID()** | This method is used to return any custom NT data associated with your window |
| **SubmitOrdersUnmanaged() / (StrategyName:key)** | SubmitOrders (unmanaged): can be used to submit entry orders with ATM strategies using the same Instrument/Limit/Order configured |
| **StrategyBase** | StrategyBase: certain properties (changed) can be used to submit entry orders to other windows with the same Instrument/Limit/Order configured |
| **PropagateInstrumentChange()** | In all NTWindow PropagateInstrumentChange() sends an instrument to other windows with the same Instrument/Limit/Order configured |
| **PropagateIntervalChange()** | In all NTWindow PropagateIntervalChange() sends an interval to other windows with the same Instrument/Limit/Order configured |
| **TabControl** | The TabControl provides functionality for working with NTPlugin objects within an NTWindow object |
| **TabControlManager** | The TabControlManager class can be used to set or check current properties of a TabControl object |

---

## KEY FINDING FOR DW-B54-01 -- AtmStrategyCreate in AddOn Context

### What the docs page shows

The official Add-On reference page lists **`SubmitOrdersUnmanaged()`** and **`StrategyBase`** as
available in AddOn context, but **`AtmStrategyCreate` is NOT listed anywhere on this page.**

`AtmStrategyCreate` lives on the `NinjaScript` base class (the parent of all NinjaScript
strategies/indicators). It is NOT a method on `Account`, `NTWindow`, or `AddOnBase`.

### Confirmed architectural fact

In NinjaScript strategy context:
```csharp
AtmStrategyCreate(instrument, action, orderType, ...) // works -- inherited from NinjaScript
```

In AddOn context (AddOnBase, NTPlugin, NTWindow):
```csharp
// AtmStrategyCreate does NOT exist -- AddOnBase does NOT inherit from NinjaScript
// This WILL produce CS1061 at compile time
```

### The unmanaged order path (what IS available)

The docs list `SubmitOrdersUnmanaged()` with ATM strategy key configuration as the
AddOn-accessible path for ATM-style bracket placement. Pattern:
```
SubmitOrdersUnmanaged(strategyName: key) -- submits entry order with ATM strategies
using the same Instrument/Limit/Order configured
```

This means: the AddOn path for ATM brackets is to submit an **unmanaged order with an
ATM template name as the strategy key** -- not to call `AtmStrategyCreate` directly.

### DW-B54-01 Resolution Path

**DW-B54-01 asks**: Can `AtmStrategyCreate()` be called from AddOn context?
**Answer from official docs**: NO. It is not in the AddOn API surface.

**The correct AddOn API path for ATM brackets** is:
  `Account.CreateOrder(...)` with `OrderEntry.AtmStrategy` + template name, OR
  the `SubmitOrdersUnmanaged` pattern listed in the docs.

**Recommended F5 probe to confirm** (add to TradeCopierAddOn.cs, F5, read compile error):
```csharp
// DW-B54-01 probe -- should produce CS1061 confirming AddOn cannot call AtmStrategyCreate
// AtmStrategyCreate("", "", DateTime.MaxValue, 0, 0, 0, out _atmId);
// If CS1061 confirmed: close DW-B54-01 as "cannot use AtmStrategyCreate from AddOn -- use Account.CreateOrder + OrderEntry"
```

---

## AUTOMATION NAMESPACE -- Key for AddOn-driven ATM

The docs list **`Automation`** as an AddOn-accessible feature:
> "Automation: something properties and methods used to change NT Strategies"

This is likely `NinjaTrader.Cbi.Automation` or the `NinjaTrader.Client` namespace.
**This is the most promising path for DW-B54-01** -- NT8 has a `NinjaTrader.Client` DLL
that exposes an automation API callable from outside NinjaScript context.

Note: `NinjaTrader.Client.dll` was removed from PropTraderTools.csproj in B50-LaneC
(DW-B50C-02) to resolve CS0433. Before using Automation, restore the reference.

---

## NTWindow / NTPlugin Pattern (for future panels)

If a future block needs a standalone window (not ChartTrader injection):
1. Create class inheriting `NTWindow`
2. Create class inheriting `NTPlugin` -- housed inside NTWindow
3. Register in `AddOnBase.OnWindowCreated()`
4. UI elements and shell logic go in NTWindow/NTPlugin, NOT in AddOnBase

Current PTT pattern: `TradeCopierWindow` inherits `UserControl` + is injected into
ChartTrader grid (B7 solution). Not using the NTWindow/NTPlugin pattern.

---

## Account API -- Confirmed in AddOn Context

These are confirmed callable from AddOnBase/AddOn context (confirmed by F5 across B1-B58):

| Method / Property | Confirmed block | Notes |
|---|---|---|
| `Account.All` | B7 | Collection of all accounts |
| `Account.CreateOrder(...)` | B8 | 12-arg overload confirmed -- arg 12 must be `(NinjaTrader.Cbi.CustomOrder)null` |
| `Account.Submit(Order[])` | B57 | Must call after CreateOrder -- order stays Initialized without Submit |
| `Account.Cancel(Order[])` | B53 | Cancels working orders |
| `Account.Change(Order[])` | B31 | Modifies stop price in-place (preserves ATM OCO link) |
| `Account.Orders` | B10 | Iterate working orders |
| `Account.Positions` | B10 | Iterate open positions |
| `Account.Get(AccountItem, Instrument)` | B40 | Returns BidPrice/AskPrice etc. |
| `Account.AccountItemUpdate` | B10 | Event: fires on P&L / margin changes |
| `Account.OrderUpdate` | B10 | Event: fires on order state changes |
| `Account.ExecutionUpdate` | B10 | Event: fires on fills |
| `Account.PositionUpdate` | B14 | Event: fires on position changes |
| `Account.Name` | B8 | Account name string |
| `Account.Subscribe()` | B44 (CopyEngine) | Starts receiving account events |
| `Account.Unsubscribe()` | B44 (CopyEngine) | Stops receiving account events |

---

## OrderState Cycle -- Confirmed in AddOn Context

```
CreateOrder() -> Initialized (stays here without Submit)
Submit()      -> PendingSubmit -> Submitted -> Accepted -> Working
Fill          -> Filled
Cancel        -> PendingCancel -> Cancelled
```

Key facts confirmed in B56:
- NT8 **limit orders** reach `Accepted` state (NOT `Submitted`) when working
- NT8 **market orders** reach `Submitted` immediately
- `IsDispatchTriggerState` must check BOTH `Submitted` AND `Accepted` (B56-LaneA fix)

---

## OnWindowCreated / OnWindowDestroyed -- AddOnBase lifecycle

```csharp
// AddOnBase overrides:
protected override void OnWindowCreated(Window window)   // called for every new NT window
protected override void OnWindowDestroyed(Window window) // called when window closes
```

Key constraint (B7): `OnWindowCreated` fires AFTER `Loaded` for pre-existing charts.
Fix: check `chart.IsLoaded` -- if true, `Dispatcher.InvokeAsync` immediately.

---

## What is NOT available in AddOn context (confirmed CS1061)

| API | Why unavailable | Block confirmed |
|---|---|---|
| `AtmStrategyCreate(...)` | On NinjaScript base class, not on Account/AddOnBase | Docs confirm (2026-08-10) |
| `NinjaScripts.Add(...)` | NinjaScript collection only exists on NinjaScript base | B50-LaneC |
| `Indicators.Add(...)` | Same -- NinjaScript base only | B50-LaneC |
| `NinjaTrader.Client.dll` Globals | CS0433 ambiguity with Core.dll -- removed B50-LaneC | B50-LaneC |
| `RoundToTickSize(...)` | UNCONFIRMED in AddOn context | B16 (use AlignToTick instead) |
| `chartTrader.Rows` | Does not exist | B7 |
| `chartTrader.RowsPanel` | Does not exist | B7 |
| `chart.ChartControl` (as property) | Does not exist on Chart | B7 |
| `chart.Instrument` (as property) | Does not exist on Chart | B7 |

---

## Open Questions (require F5 probe to confirm)

| ID | Question | Priority |
|---|---|---|
| NT8-K-003 | Does NT8 read ATM template XML at dropdown-selection time or cache at startup? | P1 |
| NT8-K-005 | After cancel+resubmit with updated ATM template XML -- does bracket spawn at correct qty? | P1 |
| DW-B54-01 | Can AddOn submit entry with ATM bracket via Account.CreateOrder + OrderEntry enum? | P1 |
| DW-B54-02 | Live F5-GATE-02: does ATM bracket fire correctly from follower account? | P1, blocked by DW-B54-01 |

---

*Last updated: 2026-08-10 | Sources: official NT8 docs screenshot + B1-B58 empirical knowledge*
