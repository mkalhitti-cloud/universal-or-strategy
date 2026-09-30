# W9-L4-003/004 Scan Report

**ID**: W9-L4-003, W9-L4-004
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs
**Lines**: 966-977 (single LINQ chain)
**Pattern**: .Where(kvp => ... .Select(kvp => kvp.Key).ToArray()

## Violation Confirmed

LINQ chain at lines 966-977 in method `ExecuteFollowerCascadeResolveFollowers`.
Both W9-L4-003 and W9-L4-004 are part of one statement — must be fixed together.

## Exact Code

```csharp
            return snapshot
                .Where(kvp =>
                    kvp.Value != null
                    && kvp.Value.IsFollower
                    && (
                        kvp.Key == orderSignal
                        || kvp.Key.Contains("_" + orderSignal + "_")
                        || kvp.Key.EndsWith("_" + orderSignal)
                    )
                )
                .Select(kvp => kvp.Key)
                .ToArray();
```

## Enclosing Method

`private IEnumerable<string> ExecuteFollowerCascadeResolveFollowers(...)` at line 952.

## Hot Path

YES — part of follower cascade resolution in OnAccountOrderUpdate chain.

## Collection Type

`KeyValuePair<string, PositionInfo>[]` snapshot (array parameter)
