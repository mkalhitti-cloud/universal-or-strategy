# ProcessAccountOrderQueue — CYC Reduction Completion

## CYC Gate Output
CYC_GATE: PASS  EPIC-W7-OVERRUN-ProcessAccountOrderQueue  ProcessAccountOrderQueue  CYC=7

## Summary
| Field            | Value                                                  |
|------------------|--------------------------------------------------------|
| Epic             | EPIC-W7-OVERRUN-ProcessAccountOrderQueue               |
| File             | src/V12_002.Orders.Callbacks.AccountOrders.cs          |
| Method           | ProcessAccountOrderQueue                               |
| CYC Before       | 13                                                     |
| CYC After        | 7                                                      |
| Build            | 0 errors                                               |
| Gate             | PASS (exit 0)                                          |

## Extraction

### New Helper Method
- **`TriggerQueueReprocessSafe(string errorTag)`** — Private helper in the same class/file.
  Wraps the repeated `try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { if (_diagFleet) Print(...) }` pattern.
  Removed 3 identical try/catch/if-_diagFleet blocks from `ProcessAccountOrderQueue`, each contributing 2 CYC (1 catch + 1 if).
  Total CYC removed from caller: 6.

## Branch Count (after extraction)
| Branch                                         | +CYC |
|------------------------------------------------|------|
| Base                                           | 1    |
| if (_oqDepth > 50)                             | 1    |
| if (isFlattenRunning) [flatten gate]           | 1    |
| while (drainedCount < Max && TryDequeue)       | 2    |
| if (isFlattenRunning) [drain loop]             | 1    |
| if (!_accountOrderQueue.IsEmpty)               | 1    |
| **Total**                                      | **7**|

## Constraints Verified
- No `lock()` used — Actor/Enqueue pattern preserved
- ASCII-only strings throughout
- Helpers in same class, same file
- Zero logic drift — pure structural extraction
- Build: 0 errors, 0 warnings
