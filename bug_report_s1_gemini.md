# V12 Photon Kernel Bug Bounty - S1 (SIMA Core)
Forensic Audit Report

BUG-S1-001
Title: Master local RMA entry race condition (Ghost Order Window)
Severity: High
Location: V12_002.SIMA.Execution.cs.SubmitLocalRMAEntry (lines 355-360)
Root Cause: `SubmitOrderUnmanaged` is called BEFORE registering the order in `entryOrders` and `activePositions`. If the order fills instantly or `OnOrderUpdate` fires before registration completes, the event handler will not find the position info and might ignore it, creating an untracked ghost position.
Evidence: `Order entryOrder = SubmitOrderUnmanaged(...); if (entryOrder != null) { entryOrders[localKey] = entryOrder; ... }`
Test Impact: Master account fast-fill race tests

BUG-S1-002
Title: Use-After-Free in Photon Sideband Clearance
Severity: Critical
Location: V12_002.SIMA.Fleet.cs.ProcessValidPhotonSlot (lines 235-236)
Root Cause: `_photonPool.ReleaseByIndex(_sbIdx)` is called inside `ProcessFleetSlot`. Immediately after it returns, `ProcessValidPhotonSlot` clears `_photonSideband[_sbIdx]`. A concurrent dispatch thread could claim the released slot and write to the sideband before it gets cleared, resulting in the new dispatch's sideband data being erroneously zeroed out.
Evidence: `ProcessFleetSlot` has `finally { if (poolSlotIndex >= 0) _photonPool.ReleaseByIndex(poolSlotIndex); }`. `ProcessValidPhotonSlot` calls `ProcessFleetSlot`, then does `_photonSideband[_sbIdx] = default(FleetDispatchSideband);`.
Test Impact: Concurrent multi-account dispatch load tests / Race condition fuzzing

BUG-S1-003
Title: FSM and Tracking State Leak on Abort
Severity: High
Location: V12_002.SIMA.Fleet.cs.DrainAllDispatchQueuesOnAbort (lines 154-180)
Root Cause: When `PumpFleetDispatch` aborts due to SIMA being inactive or flatten running, `DrainAllDispatchQueuesOnAbort` rolls back position deltas but fails to remove the proactively created entries in `_followerBrackets`, `activePositions`, `entryOrders`, `stopOrders`, and target dictionaries. This leaves orphaned state for orders that were never submitted.
Evidence: `VerifyPhotonSlotIntegrity` correctly performs full cleanup via `TryRemove` on all tracking dicts on failure, but `DrainAllDispatchQueuesOnAbort` only adjusts `ReservedDelta` and `ClearDispatchSyncPending`, omitting `TryRemove` cleanup.
Test Impact: State leak / Orphaned entry tests on toggling SIMA during active dispatch

BUG-S1-004
Title: O(N^2) nested loop in fleet health check
Severity: Med
Location: V12_002.SIMA.Fleet.cs.ShouldSkipFleet_RunHealthCheck (line 254)
Root Cause: Iterates over entire `_followerBrackets` and `activePositions` dictionaries for each account during the fleet dispatch loop (`Dispatch_ProcessFleetLoop` -> `ShouldSkipFleetAccount` -> `ShouldSkipFleet_RunHealthCheck`), creating O(N^2) complexity on the hot path.
Evidence: `foreach (var _fkvp in _followerBrackets)` and `foreach (var _pkvp in activePositions)` called per-account inside a fleet iteration loop.
Test Impact: High fleet-count latency profiling / Big-O performance tests

BUG-S1-005
Title: Shared state without atomic guards in Shutdown
Severity: Med
Location: V12_002.SIMA.Lifecycle.cs.ProcessShutdownSIMA (lines 120, 131)
Root Cause: Calls `AddExpectedPositionDelta` instead of `AddExpectedPositionDeltaLocked` when rolling back reserved deltas from the ring and legacy queue. This mutates shared state without the atomic guards used elsewhere, potentially causing delta corruption if REAPER or other threads are concurrently reading/modifying.
Evidence: `AddExpectedPositionDelta(ignored.ExpectedKey, -ignored.ReservedDelta);`
Test Impact: Concurrent shutdown/dispatch state fuzzing

BUG-S1-006
Title: Potential Null Reference in Shadow Follower List
Severity: Low
Location: V12_002.SIMA.Shadow.cs.ShadowBuildFollowerEntryList (line 82)
Root Cause: Directly accesses `ctx.Followers.Length` assuming it is non-null. If `SymmetryDispatchContext.Followers` can be null, it will throw a NullReferenceException on the hot path.
Evidence: `string[] followerSnapshot = ctx.Followers; var followerEntryNames = new System.Collections.Generic.List<string>(followerSnapshot.Length);`
Test Impact: Edge-case NullReference injection / Malformed Context testing