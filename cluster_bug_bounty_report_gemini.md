# V12 Photon Kernel Bug Bounty - Consolidated Report
**Orchestrator:** Gemini CLI
**Date:** May 17, 2026

> **ORCHESTRATOR NOTE:** The Gemini CLI sweep utilized an updated set of instructions emphasizing "Pattern-First" root-cause synthesis, which Bob and Qwen did not have during their initial sweeps. This accounts for Gemini's aggressive deduplication of symptoms into systemic anti-patterns. Bob and Qwen will inherit these updated instructions on their next runs. Jules CLI and Codex CLI sweeps are queued next before any repairs begin.

## Systemic Anti-Patterns (Pattern-First Synthesis)
During the cross-cluster deduplication, several repeating structural flaws violating V12 DNA were identified:

1. **Build 981 Protocol Violations (Ghost Order Windows)**: Using `Enqueue` for `stopOrders` and active tracking structures. The protocol explicitly mandates synchronous direct writes for brackets during submission. (Found in S2, S6)
2. **Systemic FSM Tracking State Leaks**: Failure to fully clean up parallel FSM tracking dictionaries (e.g., `targetXOrders`, `_followerBrackets`) during aborts, target replace cancellations, and stop-outs. (Found in S1, S2, S3, S6, S7)
3. **Missing Snapshot on Broker Collections**: Direct enumeration of NinjaTrader `Orders` and `Positions` collections without `.ToArray()`, leading to `InvalidOperationException` crashes on concurrent broker updates. (Found in S4)
4. **ConcurrentDictionary TOC-TOU Races**: Checking `ContainsKey` followed sequentially by indexer access `[]` or `TryAdd` without verifying the atomic return value. (Found in S2, S4)
5. **Non-Atomic Mutations of Shared State**: Bypassing the Actor/Enqueue model to mutate dictionaries or shared flags directly from network/timer threads. (Found in S1, S3, S5, S6)
6. **O(N^2) Iteration Cliffs on Hot Paths**: Deeply nested loops over collections that scale linearly with the fleet size, resulting in exponential degradation. (Found in S1, S2, S3, S4)
7. **Lifecycle Teardown Use-After-Free & NullRefs**: Race conditions during shutdown where resources (IPC listeners, sidebands, queues) are cleared while background threads are still actively processing them. (Found in S1, S3, S5)

---

## 1. Metrics & Triage Summary

**Total Bugs Found:** 28
**Validated:** 25
**Filtered (Hallucinations):** 3 (BUG-S7-003, BUG-S7-004, BUG-S7-005 - inferred purely from grep, unverifiable)

**Severity Breakdown (Validated):**
- **Critical:** 7
- **High:** 12
- **Medium:** 5
- **Low:** 1

### Per-Cluster Breakdown Table
| Cluster | Validated | Filtered | Critical | High | Med | Low |
|:---|:---|:---|:---|:---|:---|:---|
| S1 (SIMA Core) | 6 | 0 | 1 | 2 | 2 | 1 |
| S2 (Execution) | 5 | 0 | 2 | 3 | 0 | 0 |
| S3 (UI & IO) | 6 | 0 | 1 | 2 | 2 | 1 |
| S4 (REAPER) | 7 | 0 | 0 | 6 | 1 | 0 |
| S5 (Kernel State) | 3 | 0 | 1 | 1 | 1 | 0 |
| S6 (Signals) | 4 | 0 | 1 | 2 | 1 | 0 |
| S7 (Infrastructure) | 2 | 3 | 0 | 1 | 1 | 0 |

---

## 2. Recommended Repair Sequence
1. **Critical V12 DNA Repairs**: Fix all Build 981 `Enqueue` violations (BUG-X-001) and FSM State Leaks (BUG-X-004) to prevent order corruption and ghost positions.
2. **Lifecycle & NullRef Hardening**: Address BUG-X-007 and isolated critical ghost windows (S3-003, S5-001, S1-001) to ensure safe startup/teardown.
3. **Concurrency & Thread Safety**: Fix the `ConcurrentDictionary` TOC-TOU bugs (BUG-X-005) and Missing Snapshots (BUG-X-003) to stabilize the multi-threaded execution engine.
4. **Performance & Optimization**: Resolve the O(N^2) scaling cliffs (BUG-X-002) for large fleet deployments.
5. **Standardization**: Clean up the silent catch blocks and non-ASCII literals.

---

## 3. /epic-tdd Ticket Blocks (Repair Ready)

```markdown
### /epic-tdd BUG-X-001: Build 981 Protocol Violation (Ghost Order Windows via Enqueue)
**Severity:** Critical
**Locations:** 
- V12_002.Symmetry.Follower.cs (SymmetryGuardSubmitFollowerBracket)
- V12_002.Trailing.StopUpdate.cs (CreateDirectStopOrder)
- V12_002.Entries.Retest.cs (ExecuteRetestEntry - async add vs sync remove)
**Objective:** Refactor order map registrations to write synchronously without `Enqueue` for brackets to eliminate ghost order shutdown races.
```

```markdown
### /epic-tdd BUG-X-004: Systemic FSM Tracking State Leaks
**Severity:** Critical
**Locations:**
- V12_002.Orders.Callbacks.AccountOrders.cs (HandleMatchedFollower_TargetReplaceCancel logic unreachable)
- V12_002.SIMA.Fleet.cs (DrainAllDispatchQueuesOnAbort missing dictionary cleanup)
- V12_002.UI.Compliance.cs (FinalizeStopFilledPosition leaves targetXOrders entries)
- V12_002.Symmetry.BracketFSM.cs (TryTerminateFollowerBracket OrderID map leak)
**Objective:** Audit and repair all state teardown functions to completely scrub `targetXOrders`, `_followerBrackets`, and ID maps.
```

```markdown
### /epic-tdd BUG-X-007: Lifecycle Teardown Use-After-Free & Null References
**Severity:** Critical
**Locations:**
- V12_002.SIMA.Fleet.cs (ProcessValidPhotonSlot sideband clearance race)
- V12_002.UI.IPC.Server.cs (ListenForRemote vs StopIpcServer null assignment)
- V12_002.Lifecycle.cs (DrainQueuesForShutdown missing `_cmdQueue` null check)
**Objective:** Add thread-safe lifecycle guards and null checks to background service polling loops and resource pools.
```

```markdown
### /epic-tdd BUG-X-003: Missing Snapshot on Broker Collections (InvalidOperationException)
**Severity:** High
**Locations:**
- V12_002.REAPER.Audit.cs (AuditMaster_HandleNakedPosition, ProcessReaperFlatten_CancelWorkingOrders, ProcessReaperFlatten_ClosePositions)
- V12_002.Safety.Watchdog.cs (FlattenWatchdogPositions, etc.)
**Objective:** Append `.ToArray()` to all iterations of NinjaTrader `Account.Orders` and `Account.Positions` collections to prevent concurrent modification exceptions.
```

```markdown
### /epic-tdd BUG-X-005: ConcurrentDictionary TOC-TOU Race Conditions
**Severity:** High
**Locations:**
- V12_002.Orders.Management.StopSync.cs & Flatten.cs (ContainsKey followed by `[]`)
- V12_002.REAPER.Audit.cs (EnqueueReaperRepairCandidate - ContainsKey followed by TryAdd)
**Objective:** Refactor to use atomic `TryGetValue` and evaluate the boolean return of `TryAdd`.
```

```markdown
### /epic-tdd BUG-X-006: Non-Atomic Mutations of Shared State (Bypassing Enqueue)
**Severity:** High
**Locations:**
- V12_002.StickyState.cs (SerializeStickyState background mutation)
- V12_002.Entries.Trend.cs & RMA.cs (linkedTRENDEntries direct mutation)
- V12_002.Entries.OR.cs (isLongArmed/lastArmedTime direct mutation)
- V12_002.SIMA.Lifecycle.cs (ProcessShutdownSIMA using AddExpectedPositionDelta instead of Locked)
**Objective:** Wrap shared state mutations in the Actor `Enqueue` block or apply `lock` / `Interlocked` atomic primitives where appropriate.
```

```markdown
### /epic-tdd BUG-X-002: Systemic O(N^2) Iteration Cliffs on Hot Paths
**Severity:** High
**Locations:**
- V12_002.SIMA.Fleet.cs (ShouldSkipFleet_RunHealthCheck)
- V12_002.Trailing.Breakeven.cs (FindTargetOrderForPosition)
- V12_002.UI.Panel.Construction.cs (FindAllButtonsByText allocating recursive Lists)
**Objective:** Optimize loops by utilizing existing O(1) dictionary lookups or refactoring the visual tree search to avoid exponential `List.AddRange()` allocations.
```

```markdown
### /epic-tdd BUG-X-MISC: Remaining High & Critical Isolated Flaws
**Severity:** Critical/High
**Locations:**
- V12_002.SIMA.Execution.cs (SubmitLocalRMAEntry order registration race - High)
- V12_002.UI.IPC.Commands.Fleet.cs (CancelAll_CleanupUnfilledPositions async fill race - Critical)
- V12_002.Lifecycle.cs (DrainQueuesForShutdown executes commands after GTC cancel - Critical)
- V12_002.Orders.Callbacks.Execution.cs (HandleTargetFill double-decrement race - High)
**Objective:** Address these specific critical synchronization and order tracking sequence bugs.
```