# NT8 AddOnBase — Confirmed API Knowledge
# Empirically confirmed across PTT pipeline waves. Do not re-investigate items marked CONFIRMED.
# Last updated: 2026-09-11

---

## AddOnBase vs StrategyBase — Critical Distinction

PTT (TradeCopierAddOn) derives from **AddOnBase**, NOT StrategyBase.
Many NT8 API methods only exist on StrategyBase. Calling them from AddOnBase produces
a compile error or a silent no-op. Always check this table before using any NT8 API.

---

## Confirmed AddOnBase-Available APIs

| API | Status | Notes |
|-----|--------|-------|
| `account.PositionUpdate` event | CONFIRMED available | Subscribe in `Subscribe()`, unsubscribe in `Unsubscribe()` |
| `account.OrderUpdate` event | CONFIRMED available | Primary entry dispatch event |
| `account.Cancel(Order)` | CONFIRMED available | Use for bracket cancel + resubmit pattern |
| `account.CreateOrder(...)` | CONFIRMED available | Order name must carry `"PTT-"` prefix except `"Entry"` (ATM) |
| `Submit(Order)` | CONFIRMED available | Call after `CreateOrder` |
| `Account.All` | CONFIRMED available | Only access inside `Subscribe()`/`Unsubscribe()` (State.Loaded context) |
| `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(AtmStrategy, Order)` | CONFIRMED available | STATIC form. Confirmed lines 2245-2248 NT8_FULL_REFERENCE.md |
| `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(string, Order)` | CONFIRMED available | STATIC form, string template name fallback |
| `Account.Change(Order, ...)` | CONFIRMED available BUT | Silent no-op on ATM-owned brackets. Use Cancel+CreateOrder+Submit instead |
| `Dispatcher.InvokeAsync(...)` | CONFIRMED available | Required for all WPF updates from background threads |

---

## Confirmed StrategyBase-ONLY APIs (BANNED in AddOnBase)

| API | Status | Correct AddOnBase Alternative |
|-----|--------|-------------------------------|
| `AtmStrategyCreate(...)` | BANNED — StrategyBase only | Use `StartAtmStrategy` (static) |
| `AtmStrategyChangeStopTarget(...)` | BANNED — StrategyBase only | `account.Cancel()` + `account.CreateOrder()` + `Submit()` |
| `StartAtmStrategy(...)` instance form | BANNED — StrategyBase only | Use the STATIC `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` |

---

## StartAtmStrategy — Full Usage Pattern (AddOnBase)

```csharp
// Step 1: Create the entry order with name "Entry" (REQUIRED for ATM bracket arming)
Order entryOrder = account.CreateOrder(
    instrument,
    OrderAction.Buy,       // or Sell
    OrderType.Market,
    OrderTimeInForce.Day,
    qty,
    0, 0,
    "",
    "Entry");              // <-- MUST be "Entry"

// Step 2: Submit the order
Submit(entryOrder);        // or account.Submit(entryOrder) — verify which overload

// Step 3: Arm the ATM strategy (prefer object overload; fall back to string)
FollowerAtmMode namedMode = GetCloneAtmMode(instrument.FullName);
if (namedMode?.AtmObject != null)
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, entryOrder);
else if (!string.IsNullOrEmpty(namedMode?.AtmTemplateName))
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmTemplateName, entryOrder);
```

See `CopyEngine.cs` `SendCopyWithAtm` (~line 5117) for the established project implementation.

---

## NT8 Thread Model (AddOnBase)

| Event | Thread |
|-------|--------|
| `account.OrderUpdate` | NT8 background thread (account dispatch) |
| `account.PositionUpdate` | NT8 background thread (account dispatch) |
| `OnStateChange` / `OnInitialize` | NT8 UI thread |
| `OnWindowCreated` | NT8 UI thread |
| WPF event handlers (Button.Click, etc.) | WPF dispatcher thread |

**Rule**: Any state written from a background thread must use thread-safe primitives
(`ConcurrentDictionary`, `Interlocked`). Any WPF element mutation from a background
thread must be wrapped in `Dispatcher.InvokeAsync`.

---

## NT8 Compiler Rules (Roslyn quirks)

| Rule ID | Rule | Status |
|---------|------|--------|
| NT8-043 | `?.Event -=` null-conditional unsubscription causes silent runtime crash | WATCH — not yet confirmed; zero occurrences in current PTT code |
| ASCII-001 | NT8 Roslyn rejects non-ASCII bytes in `.cs` files | CONFIRMED — zero non-ASCII bytes required |

---

## Confirmed Closed Items

| ID | Item | Status |
|---|---|---|
| DW-B25-01 | Companion field race — `_pendingBeAccount` / `_pendingBeInstrument` fields | STALE — confirmed absent from `TradeCopierPanel.cs` 2026-09-11. No action required. |

*Source: empirically confirmed PTT-BUGS-LIVE-0911 Lanes A/B/C, 2026-09-11*
*Cross-reference: docs/standards/RULES_CATALOG.md Section 2*
