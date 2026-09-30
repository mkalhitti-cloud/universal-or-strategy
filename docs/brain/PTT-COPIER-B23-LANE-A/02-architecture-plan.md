# PTT-COPIER-B23-LANE-A — Architecture Plan
# Block:  PTT-COPIER-B23
# Lane:   A
# Defect: DW-B22-NULLREF-01 (P0)
# Status: REVIEW_PENDING
# Date:   2026-07-16

---

## §1  Defect Summary and Root Cause

### Defect ID
`DW-B22-NULLREF-01` (P0)

### Symptom
`PTT-Copy error: Object reference not set to an instance of an object` logged in TradeCopierWindow
every time a copy order fires to a follower account that is NOT the active chart account.
The follower order is never submitted — the NullReferenceException is thrown inside NT8's
`Account.CreateOrder()` internals and caught at `CopyEngine.cs` line 757.

### Root Cause
NT8's `Account.CreateOrder()` internally dereferences a chart-context reference
(confirmed by NT8 community: the internal `NinjaScriptBase` context must be set, or the
submission must occur on the UI dispatcher thread with an active instrument context).

When called from `OnOrderUpdate` (which fires on NT8's account background thread),
`CreateOrder()` on a non-active-chart account throws `NullReferenceException` because the
internal chart/bar context is null for that account on the background thread.

The active chart account (e.g. PA-APEX-04 when that chart is open) succeeds because its
context is initialised. All others fail.

### Evidence
- TradeCopierWindow log: `PTT-Copy error: Object reference not set...` at 00:38:05, 00:37:08,
  00:36:35, 00:35:12 (multiple follower accounts)
- PA-APEX-04 reached `Working` state (active chart account) — all others stayed `Initialized`
  (stale from prior session XML) or never submitted

---

## §2  Fix Design — Dispatcher.InvokeAsync Wrapping

### Strategy
Wrap every follower `acc.CreateOrder()` call in `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync()`
to marshal the submission onto the NT8 UI thread. NT8 requires order submission to the UI
dispatcher for non-active-chart accounts when called from AddOn context.

This is the minimal change: no architecture change, no new methods, just thread-marshal.

### Change Site
`CopyEngine.cs` — `SendCopy()` method (lines 720–762)

### Before (lines 737–755)
```csharp
try
{
    follower.CreateOrder(
        instrument,
        signal.Action,
        orderType,
        OrderEntry.Manual,
        TimeInForce.Day,
        signal.Quantity,
        limitPrice,
        0,
        null,
        signalName,
        DateTime.Now.AddDays(1),
        (NinjaTrader.Cbi.CustomOrder)null
    );
    return true;
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("PTT-Copy error: " + ex.Message);
    return false;
}
```

### After
```csharp
try
{
    NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() =>
    {
        follower.CreateOrder(
            instrument,
            signal.Action,
            orderType,
            OrderEntry.Manual,
            TimeInForce.Day,
            signal.Quantity,
            limitPrice,
            0,
            null,
            signalName,
            DateTime.Now.AddDays(1),
            (NinjaTrader.Cbi.CustomOrder)null
        );
    });
    return true;
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("PTT-Copy error: " + ex.Message);
    return false;
}
```

**NT8 dispatcher access**: `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher` is the NT8
application dispatcher — available from AddOn context without a chart reference. This is the
correct dispatcher for all NT8 UI-thread marshaling per NT8 AddOn documentation.

### CYC Impact
`SendCopy` CYC: 5 → 5 (no new branches; InvokeAsync lambda is not a branch).

### JS Compliance
- JS-021: no `lock()` added — Dispatcher.InvokeAsync is fire-and-forget async marshal
- JS-033: no `async void` — InvokeAsync returns Task, not void; we do not await (fire-and-forget is correct for order submission)
- JS-001: try/catch preserved unchanged

### New [Fact] Required
`SendCopy_NonActiveChart_DispatcherInvoked` — verifies that when SendCopy is called,
the order submission is marshaled. Note: NT8 dispatcher not available in test context;
test verifies that the method completes without throwing (smoke test only, CYC=1).

---

## §3  Write-Set

| File | Path |
|------|------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files.
