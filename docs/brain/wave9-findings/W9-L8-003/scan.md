# W9-L8-003 Scan: RouteTargetActionToHandler

## File
`src/V12_002.UI.Callbacks.cs`

## Method Source
```csharp
private void RouteTargetActionToHandler(
    string action,
    string entryName,
    PositionInfo pos,
    string targetType,
    int targetNumber,
    ConcurrentDictionary<string, Order> targetOrders,
    int targetContracts,
    double currentPrice
)
{
    switch (action)
    {
        case "market":
            ExecuteTarget_Market(entryName, pos, targetType, targetOrders, targetContracts);
            break;

        case "1point":
            ExecuteTarget_OnePoint(entryName, pos, targetType, targetContracts);
            break;

        case "2point":
            ExecuteTarget_TwoPoint(entryName, pos, targetType, targetContracts);
            break;

        case "marketprice":
            ExecuteTarget_MarketPrice(entryName, pos, targetType, targetContracts, currentPrice);
            break;

        case "breakeven":
            ExecuteTarget_Breakeven(entryName, pos, targetType, targetContracts);
            break;

        case "cancel":
            ExecuteTarget_Cancel(entryName, pos, targetType, targetOrders, targetContracts);
            break;

        default:
            Print(string.Format("[UI] Unknown target action: {0}", action));
            break;
    }
}
```

## CYC
**7**

Calculation:
- Base: 1
- `case "market"`: +1
- `case "1point"`: +1
- `case "2point"`: +1
- `case "marketprice"`: +1
- `case "breakeven"`: +1
- `case "cancel"`: +1

Total: 1 + 6 = **7**

> Note: `default` does not add a branch (it is the fall-through path). No `&&` / `||` operators present.

## Key Type
**`string`** — the parameter `action` is a plain `string`, dispatched via a `switch (action)` statement.

Possible values (from dispatch catalog): `"market"`, `"1point"`, `"2point"`, `"marketprice"`, `"breakeven"`, `"cancel"`, and any unrecognized string (default).

## Shared Context
All handler calls receive a consistent set of arguments derived from the method's parameters:

| Parameter | Type | Description |
|-----------|------|-------------|
| `entryName` | `string` | Identifies the specific position entry being targeted |
| `pos` | `PositionInfo` | Full position context (direction, entry price, etc.) |
| `targetType` | `string` | Label for the target ("T1", "T2", etc.) |
| `targetOrders` | `ConcurrentDictionary<string, Order>` | Live order dictionary for this target (only used by "market" and "cancel") |
| `targetContracts` | `int` | Number of contracts assigned to this target |
| `currentPrice` | `double` | Last known price (only used by "marketprice") |
| `targetNumber` | `int` | Numeric index of the target (received but not forwarded to any handler) |

The enclosing class (`this`) provides access to instrument, order submission, and Print utilities used inside each handler.

## Dispatch Catalog

| # | Key (condition) | Handler Code |
|---|-----------------|--------------|
| 1 | `"market"` | `ExecuteTarget_Market(entryName, pos, targetType, targetOrders, targetContracts);` |
| 2 | `"1point"` | `ExecuteTarget_OnePoint(entryName, pos, targetType, targetContracts);` |
| 3 | `"2point"` | `ExecuteTarget_TwoPoint(entryName, pos, targetType, targetContracts);` |
| 4 | `"marketprice"` | `ExecuteTarget_MarketPrice(entryName, pos, targetType, targetContracts, currentPrice);` |
| 5 | `"breakeven"` | `ExecuteTarget_Breakeven(entryName, pos, targetType, targetContracts);` |
| 6 | `"cancel"` | `ExecuteTarget_Cancel(entryName, pos, targetType, targetOrders, targetContracts);` |
| 7 | `default` (unrecognized) | `Print(string.Format("[UI] Unknown target action: {0}", action));` |

---

## Refactor Recommendation (for W9-L8-003 Plan phase)

CYC = 7 is **within** the Jane Street ≤8 threshold. However the string-keyed switch is a code-smell:
- No compile-time safety on action strings
- `targetNumber` is received but silently dropped (unused in all branches)
- Introducing a 7th real action would push CYC to 8 (one change away from violation)

Recommended approach: replace `string action` with a typed enum (`TargetAction`) and convert the
switch to an interface dispatch or enum-keyed `Dictionary<TargetAction, Action<...>>`.
This would reduce CYC to 1 (O(1) lookup) while preserving all existing behavior.
