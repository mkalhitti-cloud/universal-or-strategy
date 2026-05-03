# Type 2 Resource Leak Remediation: Phase 1 Findings

## Objective
Identify deepening opportunities to remediate "Type 2" resource leaks (cleanup bypasses) as documented in ADR-019 (`docs/brain/arena_forensics_synthesis.md`). This document serves as the forensic foundation and Logical Proof of Failure for the ARCHITECT.

## Architectural DNA Context
Per `docs/brain/video_insights_registry.md`, the system requires **Zero-Allocation** patterns and **RAII-style safety** (Resource Acquisition Is Initialization). "A system that leaks semaphores or flags is not Production Grade." Performance is a subset of correctness.

## Forensic Findings & Logical Proof of Failure

### 1. Site #5: `HandleMatchedFollowerOrder` (AccountOrders.cs:369)
*   **Context**: Execution of `TriggerCustomEvent` lambda for FSM follower replacement.
*   **Logical Proof of Failure**: The cleanup mechanism `_followerReplaceSpecs.TryRemove(sigName, out _)` resides sequentially at the end of the lambda body. If an `_isTerminating` guard short-circuits the lambda prior to this statement, the removal is never executed. The generic `catch` block on the outer scope only captures exceptions from the `TriggerCustomEvent` invocation itself, not from within the asynchronous lambda.
*   **Impact**: Permanent leak of the FSM specification in the dictionary, causing ghost state reservations.

### 2. Site #11: `AuditAccountState` -> `ProcessReaperRepairQueue` (REAPER.Audit.cs:136)
*   **Context**: Enqueueing a repair and triggering `ProcessReaperRepairQueue`.
*   **Logical Proof of Failure**: The system relies on `ExecuteReaperRepair` (called within the triggered queue processor) to clear the `_repairInFlight` flag via its top-level `finally` block. If the lambda passed to `TriggerCustomEvent` short-circuits via an early return before invoking the queue processor, the `finally` block in `ExecuteReaperRepair` is never reached. The outer `catch` block only catches trigger failures, not lambda short-circuits.
*   **Impact**: The `_repairInFlight` flag remains permanently latched for that account, locking out all future repairs.

### 3. Sites #12-15: `ProcessReaperFlattenQueue`, `ProcessReaperNakedStopQueue` (REAPER.Audit.cs & REAPER.NakedStop.cs)
*   **Context**: Enqueueing flatten and emergency naked stop requests.
*   **Logical Proof of Failure**: `_reaperNakedStopInFlight` is added before triggering the custom event. If the lambda short-circuits, the processor is never invoked. Furthermore, within `ProcessReaperNakedStopQueue`, if an early return occurs during item processing before the `TryRemove` cleanup (or if an exception occurs and the cleanup isn't protected by `finally`), the in-flight guard leaks.
*   **Impact**: Permanent lockout of the emergency Naked Stop and Flatten safety mechanisms.

### 4. Site #16: `ExecuteSmartDispatchEntry` (SIMA.Dispatch.cs:60)
*   **Context**: Semaphore acquisition for `_simaToggleSem`.
*   **Logical Proof of Failure**: The `_simaToggleSem` is acquired via `Wait(0)`. A `try...finally` block exists to release it. If an `_isTerminating` early-return guard is injected sequentially *after* the `Wait(0)` acquisition but *before* the initiation of the `try` block, the thread will return having acquired the semaphore but without registering the `Release()` in the `finally` block.
*   **Impact**: Permanent semaphore contention, permanently blocking all subsequent Smart Dispatch fleet executions.
