# W9-L8-004 Scan: DispatchRunnerAction

## File
`src/V12_002.UI.Callbacks.cs`

## Method Source

```csharp
private void DispatchRunnerAction(string action, string entryName, PositionInfo pos, int runnerContracts)
{
    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

    switch (action)
    {
        case "market":
            ExecuteRunner_Market(entryName, pos, runnerContracts);
            break;

        case "stop1pt":
            ExecuteRunner_StopOnePoint(entryName, pos);
            break;

        case "stop2pt":
            ExecuteRunner_StopTwoPoint(entryName, pos);
            break;

        case "stopbe":
            ExecuteRunner_Breakeven(entryName, pos, currentPrice);
            break;

        case "lock50":
            ExecuteRunner_Lock50(entryName, pos, currentPrice);
            break;

        case "disabletrail":
            ExecuteRunner_DisableTrail(entryName, pos);
            break;
    }
}
```

## CYC

**CYC = 8**

Calculation:
- Base: 1
- `lastKnownPrice > 0` (ternary in `currentPrice` assignment): +1
- `case "market"`: +1
- `case "stop1pt"`: +1
- `case "stop2pt"`: +1
- `case "stopbe"`: +1
- `case "lock50"`: +1
- `case "disabletrail"`: +1

**Total: 1 + 7 = 8**

## Key Type

`string` — the `action` parameter is a raw `string` literal compared via `switch (action)`.

No enum wraps the keys; callers pass plain string constants such as `"market"`, `"stop1pt"`, etc.

## Shared Context

All handlers receive some or all of the following, depending on handler:

| Variable | Source | Available to |
|----------|--------|--------------|
| `entryName` | parameter (`string`) | all 6 handlers |
| `pos` | parameter (`PositionInfo`) | all 6 handlers |
| `runnerContracts` | parameter (`int`) | `"market"` only |
| `currentPrice` | local (`double`, derived from `lastKnownPrice` or `Close[0]`) | `"stopbe"`, `"lock50"` |
| `this` (strategy instance) | implicit | all handlers (private methods on same class) |

## Dispatch Catalog

| # | Key (condition) | Handler Code |
|---|-----------------|--------------|
| 1 | `"market"` | `ExecuteRunner_Market(entryName, pos, runnerContracts)` — submits a market exit order for `runnerContracts` contracts; prints success or error. |
| 2 | `"stop1pt"` | `ExecuteRunner_StopOnePoint(entryName, pos)` — calculates `EntryPrice ± 1.0`, rounds to tick, calls `UpdateStopOrder`. |
| 3 | `"stop2pt"` | `ExecuteRunner_StopTwoPoint(entryName, pos)` — calculates `EntryPrice ± 2.0`, rounds to tick, calls `UpdateStopOrder`. |
| 4 | `"stopbe"` | `ExecuteRunner_Breakeven(entryName, pos, currentPrice)` — calculates `beStopTarget = EntryPrice ± (BreakEvenOffsetTicks × TickSize)`; arms deferred BE if price not yet at target; otherwise calls `UpdateStopOrder` and marks `pos.ManualBreakevenTriggered = true`. |
| 5 | `"lock50"` | `ExecuteRunner_Lock50(entryName, pos, currentPrice)` — calculates `lock50Stop = EntryPrice ± (unrealizedProfit × 0.5)`, rounds to tick, calls `UpdateStopOrder`. |
| 6 | `"disabletrail"` | `ExecuteRunner_DisableTrail(entryName, pos)` — sets `pos.CurrentTrailLevel = 999` to freeze trailing stop; prints confirmation. |

## Notes

- The switch has **no `default` branch** — unrecognised action strings silently no-op.
- All six delegate methods are private on the same partial class; each is already extracted (good decomposition).
- The method sits exactly at CYC = 8 (Jane Street threshold). The ternary on `currentPrice` is the one non-dispatch branch; inlining it into the two handlers that use it would drop the method to CYC = 7 and remove the only pre-switch computation.
- The string-keyed dispatch is a type-safety risk: renaming a key at the call-site has no compiler check. An enum would make illegal action strings unrepresentable (V12 DNA principle).
