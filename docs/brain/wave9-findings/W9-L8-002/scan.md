# W9-L8-002 Scan: ProcessBracketEvent

## File
`src/V12_002.Symmetry.BracketFSM.cs`

## Method Source
```csharp
private void ProcessBracketEvent(AccountEvent evt)
{
    if (!ValidateFsmEventPreconditions(evt, out FollowerBracketFSM fsm))
        return;

    FollowerBracketState oldState = fsm.State;

    switch (evt.NewState)
    {
        case OrderState.Accepted:
        case OrderState.Working:
            TransitionToAccepted(fsm);
            break;

        case OrderState.Filled:
        case OrderState.PartFilled:
            HandleFsmFilled(evt, fsm);
            break;

        case OrderState.Cancelled:
            TransitionToCancelled(evt, fsm);
            break;

        case OrderState.Rejected:
            TransitionToRejected(evt, fsm);
            break;

        default:
            // Unhandled order state - no FSM transition
            break;
    }

    LogFsmTransition(fsm, oldState, evt);
}
```
Lines 473–506 of `src/V12_002.Symmetry.BracketFSM.cs`.

## CYC

**CYC = 6**

Calculation breakdown (1 base + count of branch points):

| # | Branch point | Expression |
|---|--------------|------------|
| 1 | Base | method entry |
| 2 | if (guard) | `!ValidateFsmEventPreconditions(...)` early-return |
| 3 | switch case | `OrderState.Accepted` / `OrderState.Working` (fall-through pair, counts as 1 decision) |
| 4 | switch case | `OrderState.Filled` / `OrderState.PartFilled` (fall-through pair, counts as 1 decision) |
| 5 | switch case | `OrderState.Cancelled` |
| 6 | switch case | `OrderState.Rejected` |

`default` is a no-op catch-all and does not add a branch point.  
No `&&` / `||` compound expressions are present inside this method body itself.

**CYC = 1 (base) + 1 (if guard) + 4 (switch arms) = 6**

This is at the Jane Street limit (≤ 8); it is **within tolerance** but only just. The
switch can still be table-driven to bring it to CYC = 2 (guard + table dispatch).

## Key Type

`NinjaTrader.Cbi.OrderState` — a **NinjaTrader platform enum** (external assembly, not defined in this repo).

Relevant values used in the dispatch:
- `OrderState.Accepted`
- `OrderState.Working`
- `OrderState.Filled`
- `OrderState.PartFilled`
- `OrderState.Cancelled`
- `OrderState.Rejected`

The key is read from `evt.NewState` (field of type `OrderState` on the `AccountEvent` struct).

## Shared Context

Two objects are available to every handler:

| Object | Type | Description |
|--------|------|-------------|
| `evt` | `AccountEvent` (struct) | The inbound broker event — carries `AccountAlias`, `OrderId`, `NewState`, `FillPrice`, `FilledQty`, `TimestampTicks`, `SignalName`, `ErrorMessage` |
| `fsm` | `FollowerBracketFSM` (class) | The resolved FSM container for this bracket — carries `AccountName`, `EntryName`, `OcoGroupId`, `State`, `RemainingContracts`, `ReplacingCancelOrderId`, `LastUpdateUtc`, `EntryOrder`, `StopOrder`, `Targets[]`, `IsInSync`, `LastBrokerError`, `ExpectedEntryPrice`, `ExpectedStopPrice`, `ExpectedTargetPrices[]` |
| `this` | `V12_002 : Strategy` | The enclosing partial-class strategy instance — all strategy state, Print(), etc. |

`oldState` (local `FollowerBracketState`) is captured before dispatch and used only by
`LogFsmTransition` after the switch, not passed into handlers.

## Dispatch Catalog

| # | Key (condition) | Handler Code |
|---|-----------------|--------------|
| 1 | `evt.NewState == OrderState.Accepted` **OR** `evt.NewState == OrderState.Working` (fall-through) | `TransitionToAccepted(fsm)` — transitions `fsm.State` to `FollowerBracketState.Accepted` if currently `Submitted` or `PendingSubmit`; no-op otherwise |
| 2 | `evt.NewState == OrderState.Filled` **OR** `evt.NewState == OrderState.PartFilled` (fall-through) | `HandleFsmFilled(evt, fsm)` — classifies fill via `ClassifyFillSignalType(evt.SignalName)`; if Stop/Target fill calls `ApplyFillContracts(fsm, evt.FilledQty)`; if Entry fill (state Accepted/Submitted) sets `fsm.State = FollowerBracketState.Active` |
| 3 | `evt.NewState == OrderState.Cancelled` | `TransitionToCancelled(evt, fsm)` — if `fsm.State == Replacing` AND `fsm.ReplacingCancelOrderId == evt.OrderId` absorbs the cancel (FSM stays `Replacing`); otherwise sets `fsm.State = FollowerBracketState.Cancelled` |
| 4 | `evt.NewState == OrderState.Rejected` | `TransitionToRejected(evt, fsm)` — sets `fsm.State = FollowerBracketState.Rejected` and captures `fsm.LastBrokerError = evt.ErrorMessage` |
| 5 | `default` (all other `OrderState` values) | No-op comment: `// Unhandled order state - no FSM transition` |

**Pre-dispatch guard (executed before switch):**

| # | Condition | Effect |
|---|-----------|--------|
| G1 | `!ValidateFsmEventPreconditions(evt, out fsm)` — resolves FSM via `ResolveFsmFromEvent(evt)` and passes metadata guard `MetadataGuardFsmEvent(evt, fsm)` | Early `return` — method exits entirely if FSM cannot be resolved or metadata guard fails |

**Post-dispatch (always executed when guard passes):**

| # | Call | Effect |
|---|------|--------|
| P1 | `LogFsmTransition(fsm, oldState, evt)` | If `fsm.State != oldState`, updates `fsm.LastUpdateUtc = DateTime.UtcNow` and prints Shadow Mode transition log line |
