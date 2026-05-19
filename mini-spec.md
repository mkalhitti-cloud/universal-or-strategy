# Mini-Spec: Phase 6 SIMA Subgraph Extraction
**Mission**: Hot Path Execution Hardening (Photon Kernel)
**Epic**: SIMA Subgraph Extraction (Decoupling M3 Milestone)
**Version**: 1.0 (Metabolic Design)

## 1. OBJECTIVE
Surgically decouple the SIMA (Single-Instance Multi-Account) engine from the `V12_002` God Class. Move SIMA-specific state and hot-path execution logic into a dedicated `Morpheus.SIMA` module to enable unit testing, reduce memory footprint, and ensure lock-free atomicity.

## 2. ARCHITECTURAL COMPONENTS

### A. Interface: `ISIMAHost`
Defines the contract for the Strategy to interact with the SIMA Engine.
- `Print(string msg)`: Standard logging bridge.
- `TriggerCustomEvent(Action action)`: Threading bridge for host-side execution.
- `Account MainAccount { get; }`: Reference to the master trading account.
- `Instrument Instrument { get; }`: Reference to the strategy instrument.

### B. Core logic: `SIMAEngine`
The heart of the copy-trading logic. Zero inheritance from `NinjaScript`.
- `void ProcessFleetSlot(...)`: Evaluates a single account for order synchronization.
- `void PumpFleetDispatch(...)`: The main dispatch loop.
- `bool ShouldSkipFleetAccount(...)`: Evaluation logic for fleet health.

### C. Data Model: `SIMAData`
Plain Old Data (POD) structures for cross-module communication.
- `FleetDispatchRequest`: Metadata for a single dispatch task.
- `FollowerRank`: Priority weighting for account execution.

## 3. STATE MIGRATION MAP

| Member | Current Location | New Location | Access Pattern |
| :--- | :--- | :--- | :--- |
| `expectedPositions` | `V12_002.Data.cs` | `SIMAEngine` | `ConcurrentDictionary` |
| `_followerBrackets` | `V12_002.Data.cs` | `SIMAEngine` | `ConcurrentDictionary` |
| `_pendingFleetDispatches` | `V12_002.Data.cs` | `SIMAEngine` | `ConcurrentQueue` |
| `_lastExpectedPositionTicks`| `V12_002.Data.cs` | `SIMAEngine` | `Interlocked` |

## 4. EXECUTION FLOW (DECOUPLED)

1.  **Event**: Strategy detects a position change in the Master account.
2.  **Handoff**: Host calls `SIMAEngine.UpdateExpectedPosition(delta)`.
3.  **Scheduling**: `SIMAEngine` enqueues a `FleetDispatchRequest`.
4.  **Pumping**: `SIMAEngine` uses `ISIMAHost.TriggerCustomEvent` to schedule the dispatch pump on the strategy thread.
5.  **Execution**: `SIMAEngine` iterates the fleet, calling `acct.Submit()` via the host's account references.

## 5. SUCCESS CRITERIA
- [ ] `V12_002` God Class size reduced by >200 lines.
- [ ] `SIMAEngine` unit tests pass with a mock `ISIMAHost`.
- [ ] Zero `lock()` statements in the extracted logic.
- [ ] `deploy-sync.ps1` passes with ASCII gate 0.
