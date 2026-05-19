# V12 Photon Kernel Bug Bounty - S7 (Kernel Infrastructure)

## Summary
The investigation was interrupted due to the time limit, so this is a partial audit based on the files reviewed so far. The audit focused on finding race conditions, FSM leaks, semaphore leaks, and wildcard logic violations within the V12 Photon Kernel Infrastructure.

## Findings

BUG-S7-001
Title: Double-Decrement Vulnerability in HandleTargetFill
Severity: High
Location: V12_002.Orders.Callbacks.Execution.cs.ProcessOnExecution_HandleTargetFill
Root Cause: Race condition between `OnOrderUpdate` and `OnExecutionUpdate` callbacks on partial fills, despite the `ProcessOnExecution_Dedup` check. The FNV-1a hash ring dedup only checks the execution ID, but `OnOrderUpdate` does not have an execution ID to dedup against. This means if both events fire, the position contracts could be decremented twice.
Evidence: `ApplyTargetFill(pos, targetNum, quantity, terminalFill, out alreadyProcessed, out appliedQty, out remainingAfter);` relies on flags set during processing, but these flags aren't fully synchronized across both event paths under high contention.
Test Impact: Stress test with simulated overlapping `OnOrderUpdate` and `OnExecutionUpdate` callbacks.

BUG-S7-002
Title: FSM Leak in FollowerBracketFSM Removal
Severity: Med
Location: V12_002.Symmetry.BracketFSM.cs.RemoveFsmOrderIdMappings
Root Cause: When `TryTerminateFollowerBracket` is called, it removes the FSM from `_followerBrackets` and clears `_orderIdToFsmKey` mappings. However, if an order is in the `ChangePending` state and receives a new Order ID from the broker *after* this termination sequence starts, the new Order ID will be leaked in the map.
Evidence: `if (fsm.EntryOrder != null && !string.IsNullOrEmpty(fsm.EntryOrder.OrderId))` only removes the ID currently on the order object.
Test Impact: Cancel follower bracket immediately after issuing a modify/replace order.

BUG-S7-003
Title: Semaphore Release Potential Leak in SIMA Dispatch
Severity: Med
Location: V12_002.SIMA.Dispatch.cs (implied by grep results)
Root Cause: The `_photonPool.ReleaseByIndex()` is called in `try/finally` blocks in some places but seems to lack full guarantees if the thread is aborted or an asynchronous exception occurs before the finally block is entered, especially in complex sideband rollback scenarios.
Evidence: `_photonPool.ReleaseByIndex(_poolSlotIndex);` pattern identified in grep results shows manual index management instead of `IDisposable` pattern.
Test Impact: Fault injection throwing asynchronous exceptions during dispatch loop.

BUG-S7-004
Title: Legacy `lock(stateLock)` remnant causing potential deadlock
Severity: Med
Location: V12_002.SIMA.Fleet.cs (implied by grep results)
Root Cause: The `stateLock` was ostensibly removed per documentation ("Phase 10: lock(stateLock) removed"), but the field `private readonly object stateLock = new object();` still exists, and some partial files (e.g., `V12_002.Orders.Management.StopSync.cs`) reference locking it in comments, suggesting it might still be used implicitly or by legacy code not fully removed.
Evidence: `// Locks stateLock to prevent dirty reads of pos.RemainingContracts` in `StopSync.cs`.
Test Impact: Multi-threaded stress test on `UpdateStopQuantity` while `ApplyTargetFill` is active.

BUG-S7-005
Title: Non-ASCII Strings Check
Severity: Low
Location: Check Script Output
Root Cause: The automated `check_ascii.py` script verified that the primary files (`V12_002.cs`, `V12_002.SIMA.cs`, etc.) are 100% ASCII. However, it didn't explicitly check all the `.cs` files in the directory. A manual python scan did not output any non-ASCII characters, so this passes.
Evidence: `Python script scan output was empty.`
Test Impact: N/A

## Notes
The audit was terminated early. A full review of `Entries.cs` and the remaining `*Constants*.cs`, `*Atm*.cs` files is required to complete the S7 checklist.