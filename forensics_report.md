# Forensic Report: Phase 6 SIMA Subgraph Extraction
**Status**: Stage 0 (Forensic Intake) Complete
**Target**: `V12_002.SIMA.*.cs` -> `Morpheus.SIMA` Module

## 1. Executive Summary
The SIMA (Single-Instance Multi-Account) copy trading engine is currently implemented as a distributed partial class extension of the `V12_002` God Class. While logically grouped into `.SIMA.cs`, `.SIMA.Fleet.cs`, etc., the implementation relies on direct access to private strategy state and the `Strategy` base class threading model (`TriggerCustomEvent`).

## 2. Decoupling Heatmap (God Class Entanglements)

### A. State Entanglement
The following members must be moved to the new `SIMAManager` or bridged via an interface:
- `ConcurrentDictionary<string, int> expectedPositions`
- `ConcurrentDictionary<string, FollowerBracketFSM> _followerBrackets`
- `ConcurrentDictionary<string, byte> _dispatchSyncPendingExpKeys`
- `ConcurrentQueue<FleetDispatchRequest> _pendingFleetDispatches`
- `long _lastExpectedPositionSetTicks` (Atomic)

### B. Logical Dependencies
- **Ordering**: `acct.Submit(orders)` is currently called directly in `ProcessFleetSlot`.
- **Instrument Context**: `ExpKey(string acctName)` relies on `Instrument.FullName`.
- **Strategy Pump**: `PumpFleetDispatch` recursively schedules itself via `TriggerCustomEvent`.

## 3. Structural Proof of Failure
- **Inability to Test**: The SIMA logic cannot be unit-tested without instantiating a full `V12_002` NinjaScript Strategy.
- **Memory Pressure**: The `V12_002` object header is bloated by SIMA state, even when `EnableSIMA` is false.
- **Threading Risk**: SIMA logic interleaves with REAPER audit logic in the same partial-class namespace, making lock-free verification difficult.

## 4. Proposed "Metabolic" Extraction
1. **Namespace**: `Morpheus.SIMA`
2. **Interface**: `ISIMAHost` (Strategy-side implementation)
3. **Core**: `SIMAEngine` (Logic-only, zero Strategy inheritance)
4. **Data**: `SIMAData` (POD structs for requests/ranks)

## 5. Risk Assessment
- **Order ID Mapping**: The `_orderIdToFsmKey` mapping must remain synchronized between the Host and the Engine.
- **Race Condition**: The transition from `AddExpectedPositionDeltaLocked` (private) to an external engine method must remain atomic across the strategy thread.

---
**Next Step**: Hand off this report to the ARCHITECT (Traycer) to generate the `mini-spec.md` and `implementation_plan.md`.
