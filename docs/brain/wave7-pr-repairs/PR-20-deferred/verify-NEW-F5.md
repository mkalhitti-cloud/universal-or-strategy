# Verify NEW-F5 -- OrderId fallback in PurgeFollowerStopScanStopOrders
# Branch: wave7/pr20-deferred-repairs
# Finding: VALID-LOGIC-BUG (P2)

## Fix Description
Added OrderId-based fallback to reference equality check in PurgeFollowerStopScanStopOrders.
Reused existing `IsMatchingStopReplacement` helper (line 791) which already implements
the ref-then-OrderId pattern, keeping CYC within bound.

## Verification

### Code Change
File: src/V12_002.Orders.Callbacks.AccountOrders.cs line 826

BEFORE:
```
if (sc.Value == order)
```

AFTER:
```
if (IsMatchingStopReplacement(sc.Value, order))
```

IsMatchingStopReplacement (line 791-793):
```csharp
private bool IsMatchingStopReplacement(Order psrOldOrder, Order order) =>
    psrOldOrder == order || (psrOldOrder != null && psrOldOrder.OrderId == order.OrderId);
```

### Gates
- dotnet build Linting.csproj: 0 errors, 0 warnings -- PASS
- wave7_prepush_gate.py: GATE PASSED (6/6 checks)
- CYC check: PurgeFollowerStopScanStopOrders CYC=7 (was 8, threshold 8) -- PASS
- lock() check: none found -- PASS
- ASCII check: PASS
- DateTime.Now check: none introduced -- PASS

### Commit
SHA: 04a2c6c9
Message: fix(wave7/pr20-deferred): NEW-F5 -- OrderId fallback in PurgeFollowerStopScanStopOrders via IsMatchingStopReplacement helper (CYC 8->7)

### OKF Alignment
- Rule 5 (production safety): independent_tracking -- matching by OrderId prevents ghost
  follower stops after NT broker reconnect (new Order instance, same logical order)
- Rule 6 (complexity): CYC=7 <= 8 -- PASS

verification_verdict: PASS
fix_confirmed: true
build_passed: true
gate_passed: true
cyc_achieved: 7
