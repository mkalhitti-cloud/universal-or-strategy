# Completion: ProcessAccountOrder_UpdateFleetExpected

## CYC Gate Output

CYC_GATE: PASS  EPIC-W7-OVERRUN-ProcessAccountOrder_UpdateFleetExpected  ProcessAccountOrder_UpdateFleetExpected  CYC=7

## Summary

- **File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs`
- **Method**: `ProcessAccountOrder_UpdateFleetExpected`
- **CYC Before**: 12
- **CYC After**: 7
- **Build**: 0 errors
- **Wave Ready**: true

## Refactoring

Extracted two private helpers into the same class/file, mirroring the existing
`HandleMasterStopFill` / `HandleMasterTargetFill` pattern already present at lines 109-133:

### New Helper Methods

1. **`HandleFleetStopFill(Account acct)`**
   - Removes `acct.Name` from `_nakedPositionFirstSeen`
   - Enqueues `SetExpectedPositionLocked(fExpKey, 0)`
   - CYC = 1

2. **`HandleFleetTargetFill(Order order, Account acct)`**
   - Delta-decrements `expectedPositions` via `Enqueue` lambda
   - CYC = 5 (lambda null-guard + && + if/else-if/else for sign)

### Resulting `ProcessAccountOrder_UpdateFleetExpected`

```csharp
private void ProcessAccountOrder_UpdateFleetExpected(Order order, Account acct)
{
    if (order.OrderState == OrderState.Filled || order.OrderState == OrderState.PartFilled)
    {
        if (order.Name.StartsWith("Stop_"))
            HandleFleetStopFill(acct);
        else if (order.Name.StartsWith("T") && order.Name.Contains("_"))
            HandleFleetTargetFill(order, acct);
    }
}
```

CYC = 6 (measured by gate as 7 inclusive of base path) — within target <= 8.

## Constraints

- No locks used (lock() BANNED)
- ASCII-only string literals
- Helpers in same class, same file
- Zero logic drift (pure structural extraction)
