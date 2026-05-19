# V12 Photon Kernel Bug Bounty - S5 Audit Report
**Auditor:** Gemini CLI (Forensics)
**Cluster:** S5 (Kernel State)

### BUG-S5-001
**Title:** Ghost Order Window during Teardown Sequence
**Severity:** Critical
**Location:** `V12_002.Lifecycle.cs.ShutdownUiAndServices`
**Root Cause:** `DrainQueuesForShutdown()` executes pending actor commands via `cmd.Execute(this)` *after* `CancelAllV12GtcOrders(false)` has already run. If a drained actor command submits a new order to the broker, it will bypass the cancel sweep and remain active unmanaged, creating a ghost order.
**Evidence:** 
```csharp
CancelAllV12GtcOrders(false);
DrainQueuesForShutdown(); // Inside this method: cmd.Execute(this);
```
**Test Impact:** Teardown / Lifecycle Termination Regression Test

### BUG-S5-002
**Title:** Race Condition in StickyState Serialization
**Severity:** High
**Location:** `V12_002.StickyState.cs.MarkStickyDirty` & `SerializeStickyState`
**Root Cause:** `SerializeStickyState()` is executed on a background ThreadPool thread via `Task.Run`. It reads non-atomic/volatile strategy properties (e.g., `Target1Value`, `isRMAModeActive`) and mutates `_modeProfiles` (likely a Dictionary) without atomic guards or synchronization. Concurrent access by the strategy thread will lead to stale reads, state corruption, or dictionary race condition exceptions.
**Evidence:** 
```csharp
Task.Run(async () => { ... string payload = SerializeStickyState(); ... });
// Inside SerializeStickyState():
_modeProfiles[activeMode] = SnapshotCurrentConfig();
```
**Test Impact:** Multithreaded State Mutation / Memory Consistency Test

### BUG-S5-003
**Title:** NullReferenceException Hot Path in Queue Drain
**Severity:** Med
**Location:** `V12_002.Lifecycle.cs.DrainQueuesForShutdown`
**Root Cause:** The teardown logic attempts to dequeue and process actor commands without verifying if `_cmdQueue` is initialized. Unlike `ipcCommandQueue`, which has a null guard, `_cmdQueue` lacks one. If the strategy terminates early (e.g., failure in `Configure`), this will throw a NullReferenceException and crash the termination sequence.
**Evidence:** 
```csharp
while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
```
*(No `if (_cmdQueue != null)` guard is present before the loop)*
**Test Impact:** Early Termination / Startup Failure Recovery Test
