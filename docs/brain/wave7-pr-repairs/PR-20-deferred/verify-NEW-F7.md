# Verify NEW-F7 -- Ghost-order window in stopOrders Enqueue path
# Branch: wave7/pr20-deferred-repairs
# Finding: VALID-LOGIC-BUG (P2)

## Fix Description
Added `ResolveStopReference` helper and injected it into `UpdateStopQuantity_Execute`
after the `stopOrders[entryName]` lookup. After a broker reconnect NT may return a new
Order object with the same OrderId; the dict retains the stale reference. The helper
walks Account.Orders to find a live order by OrderId and atomically updates the dict
via ConcurrentDictionary.TryUpdate, returning the fresh reference for the cancel call.

## Verification

### Code Changes
File: src/V12_002.Orders.Management.StopSync.cs

ADDED helper (line 502):
```csharp
// [PR-20-deferred NEW-F7] Reconcile stale stop reference after broker reconnect.
// NT may return a new Order object for the same logical stop; the dict retains the old reference.
// If Account.Orders contains an order with the same OrderId but a different reference, update dict.
private Order ResolveStopReference(string entryName, Order tracked)
{
    if (tracked == null || string.IsNullOrEmpty(tracked.OrderId))
        return tracked;
    foreach (Order liveOrder in Account.Orders)
    {
        if (liveOrder != tracked && liveOrder.OrderId == tracked.OrderId)
        {
            stopOrders.TryUpdate(entryName, liveOrder, tracked);
            return liveOrder;
        }
    }
    return tracked;
}
```

CHANGED UpdateStopQuantity_Execute (line 621 -- one assignment added after dict lookup):
```csharp
Order currentStop = stopOrders[entryName];
currentStop = ResolveStopReference(entryName, currentStop); // [NEW-F7] Reconcile stale ref post-reconnect
```

### Gates
- dotnet build Linting.csproj: 0 errors, 0 warnings -- PASS
- wave7_prepush_gate.py: GATE PASSED (6/6 checks)
- CYC check: UpdateStopQuantity_Execute CYC=8 (unchanged, threshold 8) -- PASS
- CYC check: ResolveStopReference CYC=6 (threshold 8) -- PASS
- lock() check: none found, TryUpdate is lock-free atomic ConcurrentDictionary op -- PASS
- ASCII check: PASS
- DateTime.Now check: none introduced -- PASS

### Commit
SHA: 956c5e08
Message: fix(wave7/pr20-deferred): NEW-F7 -- reconcile stale stop reference post-reconnect via ResolveStopReference

### OKF Alignment
- Rule 5 (production safety): staleness_guard -- stop reference reconciled before cancel call,
  preventing cancel-on-stale-ghost that blocks future replacements
- Rule 1 (lock-free): TryUpdate is ConcurrentDictionary atomic op (no lock() used)
- Rule 6 (complexity): both methods CYC <= 8 -- PASS

verification_verdict: PASS
fix_confirmed: true
build_passed: true
gate_passed: true
cyc_execute: 8
cyc_resolve: 6
