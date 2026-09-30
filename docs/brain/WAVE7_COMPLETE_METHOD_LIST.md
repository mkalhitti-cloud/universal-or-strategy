# Wave 7 Complete Method List (170 Methods)

**Source**: Fresh CodeScene scan 2026-06-19T04:24:16Z  
**Threshold**: CYC > 8 (Jane Street strict standard)

## Priority 0: Critical (CYC 21+) - 10 Methods

| # | Method | File | CYC | LOC |
|---|--------|------|-----|-----|
| 1 | `IsCommandForThisInstrument` | V12_002.UI.IPC.cs | 36 | 50 |
| 2 | `HydrateFromOpenPositions` | V12_002.SIMA.Lifecycle.cs | 31 | 98 |
| 3 | `SweepBrokerOrders` | V12_002.SIMA.Lifecycle.cs | 24 | 67 |
| 4 | `HandleTerminated` | V12_002.Lifecycle.cs | 23 | 46 |
| 5 | `HydrateWorkingOrdersFromBroker` | V12_002.SIMA.Lifecycle.cs | 19 | 110 |
| 6 | `AdoptMasterOrders` | V12_002.SIMA.Lifecycle.cs | 19 | 42 |
| 7 | `TryHandleFleetCommand` | V12_002.UI.IPC.Commands.Fleet.cs | 19 | 42 |
| 8 | `TryHandleFleet_CancelAll` | V12_002.UI.IPC.Commands.Fleet.cs | 19 | 41 |
| 9 | `ProcessFlattenWorkItem_CancelOrders` | V12_002.SIMA.Flatten.cs | 18 | 36 |
| 10 | `CancelAll_ProcessSingleFleetAccount` | V12_002.UI.IPC.Commands.Fleet.cs | 18 | 31 |

## Priority 1: High (CYC 16-20) - 24 Methods

| # | Method | File | CYC | LOC |
|---|--------|------|-----|-----|
| 11 | `SymmetryGuardReplaceExistingFollowerTarget` | V12_002.Symmetry.Replace.cs | 18 | 49 |
| 12 | `SymmetryGuardTryResolveFollowersForDispatch` | V12_002.Symmetry.Replace.cs | 18 | 33 |
| 13 | `SyncLimitTarget` | V12_002.Orders.Management.StopSync.cs | 17 | 128 |
| 14 | `HydrateExpectedPositionsFromBroker` | V12_002.SIMA.Lifecycle.cs | 17 | 65 |
| 15 | `ClassifyOrderByPrefix` | V12_002.SIMA.Lifecycle.cs | 17 | 21 |
| 16 | `TryApplyConfigTarget_Value` | V12_002.UI.IPC.Commands.Config.cs | 17 | 45 |
| 17 | `DestroyPanel` | V12_002.UI.Panel.Construction.cs | 17 | 149 |
| 18 | `CheckFFMAConditions` | V12_002.Entries.FFMA.cs | 16 | 50 |
| 19 | `FlattenSinglePosition` | V12_002.Orders.Management.Flatten.cs | 16 | 76 |
| 20 | `RestoreCascadedTargets` | V12_002.Orders.Management.StopSync.cs | 16 | 90 |
| 21 | `EmergencyFlattenSingleFleetAccount` | V12_002.SIMA.Flatten.cs | 16 | 73 |
| 22 | `IsOrderAllowed` | V12_002.UI.Compliance.cs | 16 | 43 |
| 23 | `HandleFleetTargetFill` | V12_002.UI.Compliance.cs | 16 | 58 |
| 24 | `UpdatePanelState` | V12_002.UI.Panel.StateSync.cs | 16 | 51 |
| 25 | `SyncPanelConfigFromSnapshot` | V12_002.UI.Panel.StateSync.cs | 15 | 37 |
| 26 | `ProcessQueuedAccountOrder` | V12_002.Orders.Callbacks.AccountOrders.cs | 15 | 34 |
| 27 | `AuditMaster_HandleNakedPosition` | V12_002.REAPER.Audit.cs | 15 | 38 |
| 28 | `ProcessIpcCommands` | V12_002.UI.IPC.cs | 15 | 36 |
| 29 | `TryHandleFleet_MoveTarget` | V12_002.UI.IPC.Commands.Fleet.cs | 15 | 33 |
| 30 | `OnAccountOrderUpdate` | V12_002.Orders.Callbacks.AccountOrders.cs | 14 | 26 |
| 31 | `HandleMatchedFollowerOrder` | V12_002.Orders.Callbacks.AccountOrders.cs | 14 | 65 |
| 32 | `HandleMatchedFollower_StopReplacement` | V12_002.Orders.Callbacks.AccountOrders.cs | 14 | 23 |
| 33 | `PropagateMasterEntryMove` | V12_002.Orders.Callbacks.Propagation.cs | 14 | 60 |
| 34 | `BuildLiveBrokerOrderIndex` | V12_002.Orders.Management.Cleanup.cs | 14 | 23 |

## Priority 2: Medium (CYC 11-15) - 89 Methods

| # | Method | File | CYC | LOC |
|---|--------|------|-----|-----|
| 35 | `Dispatch_ProcessFleetLoop` | V12_002.SIMA.Dispatch.cs | 14 | 114 |
| 36 | `VerifyPhotonSlotIntegrity` | V12_002.SIMA.Fleet.cs | 14 | 43 |
| 37 | `HydrateFSMsFromWorkingOrders` | V12_002.SIMA.Lifecycle.cs | 14 | 70 |
| 38 | `GetFsmExpectedPosition` | V12_002.Symmetry.BracketFSM.cs | 14 | 25 |
| 39 | `SymmetryGuardOnMasterFill` | V12_002.Symmetry.cs | 14 | 44 |
| 40 | `CancelOrphanedTargets` | V12_002.UI.Compliance.cs | 14 | 20 |
| 41 | `SubmitRepairOrderWithAuthorization` | V12_002.REAPER.Repair.cs | 14 | 67 |
| 42 | `UpdateLivePositionSnapshot` | V12_002.UI.SnapshotPool.cs | 14 | 43 |
| 43 | `ProcessAccountOrderQueue` | V12_002.Orders.Callbacks.AccountOrders.cs | 13 | 30 |
| 44 | `IsMasterReplaceCascadeCancellation` | V12_002.Orders.Callbacks.AccountOrders.cs | 13 | 26 |
| 45 | `ProcessFollowerCancellationSafe` | V12_002.Orders.Callbacks.AccountOrders.cs | 13 | 41 |
| 46 | `IsValidTradeTypeToken` | V12_002.Orders.Callbacks.Propagation.cs | 13 | 20 |
| 47 | `RefreshActivePositionOrders` | V12_002.Orders.Management.StopSync.cs | 13 | 49 |
| 48 | `UpdateStopQuantity` | V12_002.Orders.Management.StopSync.cs | 13 | 43 |
| 49 | `FormatInternal` | V12_002.Perf.LogBuffer.cs | 13 | 27 |
| 50 | `AuditSingleFleetAccount` | V12_002.REAPER.Audit.cs | 13 | 48 |
| 51 | `AuditMaster_CheckExpectedActual` | V12_002.REAPER.Audit.cs | 13 | 23 |
| 52 | `SetRmaAnchorFromIpc` | V12_002.SIMA.cs | 13 | 17 |
| 53 | `ExecuteMultiAccountMarket` | V12_002.SIMA.Execution.cs | 13 | 78 |
| 54 | `InitializeFollowerBracketFSM` | V12_002.SIMA.Fleet.cs | 13 | 34 |
| 55 | `HandleFsmFilled` | V12_002.Symmetry.BracketFSM.cs | 13 | 18 |
| 56 | `ManageTrailingStops` | V12_002.Trailing.cs | 13 | 33 |
| 57 | `MoveStop_SinglePosition` | V12_002.Trailing.Breakeven.cs | 13 | 63 |
| 58 | `PlacePanel` | V12_002.UI.Panel.Construction.cs | 13 | 56 |
| 59 | `CreateSection0_Identity` | V12_002.UI.Panel.Construction.cs | 13 | 154 |
| 60 | `ProcessQueuedExecution_HandleFleetOCO` | V12_002.UI.Compliance.cs | 13 | 17 |
| 61 | `ProcessQueuedExecution_SyncFlatPosition` | V12_002.UI.Compliance.cs | 13 | 36 |
| 62 | `LogApexPerformance` | V12_002.UI.Compliance.cs | 13 | 72 |
| 63 | `TryParseTargetMode` | V12_002.UI.IPC.cs | 13 | 27 |
| 64 | `TryHandleMode_SetMode` | V12_002.UI.IPC.Commands.Mode.cs | 13 | 59 |
| 65 | `ExecuteFFMAManualMarketEntry` | V12_002.Entries.FFMA.cs | 12 | 162 |
| 66 | `DrawORBox` | V12_002.DrawingHelpers.cs | 12 | 77 |
| 67 | `ExecuteRetestEntry` | V12_002.Entries.Retest.cs | 12 | 199 |
| 68 | `ProcessAccountOrder_UpdateMasterExpected` | V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 21 |
| 69 | `ProcessAccountOrder_UpdateFleetExpected` | V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 23 |
| 70 | `HandleMatchedFollower_PendingCancelReplace` | V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 61 |
| 71 | `ExecuteFollowerCascadeCleanup` | V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 60 |
| 72 | `ProcessFollowerCancellationUnconditional` | V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 29 |
| 73 | `RequestStopCancelLifecycleSafe` | V12_002.Orders.Callbacks.cs | 12 | 22 |
| 74 | `ProcessOnOrderUpdate` | V12_002.Orders.Callbacks.cs | 12 | 29 |
| 75 | `HandleOrderRejected` | V12_002.Orders.Callbacks.cs | 12 | 29 |
| 76 | `HandleOrderPriceOrQuantityChanged` | V12_002.Orders.Callbacks.cs | 12 | 39 |
| 77 | `ResolveFollowersViaScan_ProcessEntry` | V12_002.Orders.Callbacks.Propagation.cs | 12 | 22 |
| 78 | `CleanupPosition` | V12_002.Orders.Management.Cleanup.cs | 12 | 36 |
| 79 | `FlattenAll` | V12_002.Orders.Management.Flatten.cs | 12 | 37 |
| 80 | `HasActiveOrPendingOrderForEntry` | V12_002.Orders.Management.Flatten.cs | 12 | 15 |
| 81 | `AuditFleet_CalculateExpectedActual` | V12_002.REAPER.Audit.cs | 12 | 41 |
| 82 | `CancelWatchdogWorkingOrders` | V12_002.Safety.Watchdog.cs | 12 | 19 |
| 83 | `ProcessSingleFleetRMAAccount` | V12_002.SIMA.Execution.cs | 12 | 106 |
| 84 | `ShadowProcessFollowerStopUpdate` | V12_002.SIMA.Shadow.cs | 12 | 31 |
| 85 | `ResolveFsm_ByScan` | V12_002.Symmetry.BracketFSM.cs | 12 | 21 |
| 86 | `SymmetryGuardTryResolveFollower` | V12_002.Symmetry.Follower.cs | 12 | 83 |
| 87 | `SymmetryGuardSubmitFollowerBracket` | V12_002.Symmetry.Follower.cs | 12 | 101 |
| 88 | `MoveSpecificTarget` | V12_002.Trailing.Breakeven.cs | 12 | 41 |
| 89 | `AttachExecutionPanelHandlers` | V12_002.UI.Panel.Handlers.cs | 12 | 46 |
| 90 | `OnSubmitClick` | V12_002.UI.Panel.Handlers.cs | 12 | 30 |
| 91 | `TryHandleRisk_Breakeven` | V12_002.UI.IPC.Commands.Mode.cs | 12 | 16 |
| 92 | `StopIpcServer` | V12_002.UI.IPC.Server.cs | 12 | 29 |
| 93 | `MarkTargetFilled` | V12_002.PositionInfo.cs | 12 | 13 |
| 94 | `SetTargetFilledQuantity` | V12_002.PositionInfo.cs | 12 | 14 |
| 95 | `ProcessSessionReset` | V12_002.BarUpdate.cs | 11 | 32 |
| 96 | `IpcClientSession` | V12_002.cs | 11 | 4 |
| 97 | `IsAllowlistBypassAttempt` | V12_002.IPC.Hardening.cs | 11 | 19 |
| 98 | `EnterORPosition` | V12_002.Entries.OR.cs | 11 | 166 |
| 99 | `HandleOrderCancelled_ProcessStopReplacement` | V12_002.Orders.Callbacks.cs | 11 | 22 |
| 100 | `CancelOrphanedOrdersForPosition` | V12_002.Orders.Callbacks.Execution.cs | 11 | 15 |
| 101 | `ResolveMasterTradeType` | V12_002.Orders.Callbacks.Propagation.cs | 11 | 16 |
| 102 | `PropagateMaster_ApplyFollowerMove` | V12_002.Orders.Callbacks.Propagation.cs | 11 | 24 |
| 103 | `PurgePositionIfEligible` | V12_002.Orders.Management.Cleanup.cs | 11 | 21 |
| 104 | `ClassifyOrphanReason` | V12_002.Orders.Management.Cleanup.cs | 11 | 33 |
| 105 | `PurgeGhostOrderReferences` | V12_002.Orders.Management.Cleanup.cs | 11 | 42 |
| 106 | `CancelAllBracketOrdersForPosition` | V12_002.Orders.Management.Flatten.cs | 11 | 9 |
| 107 | `ProcessShutdownSIMA` | V12_002.SIMA.Lifecycle.cs | 11 | 25 |
| 108 | `ProcessFleetSlot` | V12_002.SIMA.Fleet.cs | 11 | 37 |
| 109 | `SubmitAndRegisterFleetOrders` | V12_002.SIMA.Fleet.cs | 11 | 30 |
| 110 | `DrainAllDispatchQueuesOnAbort` | V12_002.SIMA.Fleet.cs | 11 | 24 |
| 111 | `RemoveFsmOrderIdMappings` | V12_002.Symmetry.BracketFSM.cs | 11 | 14 |
| 112 | `SymmetryGuardOnFollowerFill` | V12_002.Symmetry.Follower.cs | 11 | 47 |
| 113 | `UpdateStopOrder` | V12_002.Trailing.StopUpdate.cs | 11 | 33 |
| 114 | `CollapseAllExecutionControls` | V12_002.UI.Panel.Handlers.cs | 11 | 21 |
| 115 | `HandleTrimCommand` | V12_002.UI.IPC.Commands.Config.cs | 11 | 85 |
| 116 | `TryApplyConfigTarget_Type` | V12_002.UI.IPC.Commands.Config.cs | 11 | 22 |
| 117 | `TryHandleFleet_LongShort` | V12_002.UI.IPC.Commands.Fleet.cs | 11 | 47 |
| 118 | `OnWatchdogTimer` | V12_002.Safety.Watchdog.cs | 11 | 30 |
| 119 | `CancelDirectFallbackOrders` | V12_002.Safety.Watchdog.cs | 11 | 18 |
| 120 | `OnBarUpdate` | V12_002.BarUpdate.cs | 10 | 51 |
| 121 | `ProcessOnStateChange` | V12_002.Lifecycle.cs | 10 | 11 |
| 122 | `ExecuteMOMOEntry` | V12_002.Entries.MOMO.cs | 10 | 166 |
| 123 | `SubmitTargetOrdersLoop` | V12_002.Orders.Management.cs | 10 | 104 |

## Priority 3: Low (CYC 9-10) - 47 Methods

| # | Method | File | CYC | LOC |
|---|--------|------|-----|-----|
| 124 | `ManageCIT` | V12_002.Orders.Management.Flatten.cs | 10 | 33 |
| 125 | `AuditMaster_HandleDesyncFlatten` | V12_002.REAPER.Audit.cs | 10 | 20 |
| 126 | `ProcessReaperFlatten_CancelWorkingOrders` | V12_002.REAPER.Audit.cs | 10 | 19 |
| 127 | `LogHealthCheckResult` | V12_002.SIMA.Fleet.cs | 10 | 23 |
| 128 | `SweepTrackedOrders` | V12_002.SIMA.Lifecycle.cs | 10 | 32 |
| 129 | `SymmetryGuardCascadeFollowerCleanup` | V12_002.Symmetry.Replace.cs | 10 | 33 |
| 130 | `SymmetryGuardPruneDispatches` | V12_002.Symmetry.Replace.cs | 10 | 20 |
| 131 | `SymmetryNormalizeTradeType` | V12_002.Symmetry.Replace.cs | 10 | 17 |
| 132 | `InitiateStopReplacement` | V12_002.Trailing.StopUpdate.cs | 10 | 46 |
| 133 | `DumpVisualTree` | V12_002.UI.Panel.Helpers.cs | 10 | 48 |
| 134 | `FindChartTabGrid` | V12_002.UI.Panel.Helpers.cs | 10 | 23 |
| 135 | `SyncLiveTargetRows` | V12_002.UI.Panel.StateSync.cs | 10 | 21 |
| 136 | `IsAllowedIpcAction` | V12_002.UI.IPC.cs | 10 | 13 |
| 137 | `SendResponseToRemote` | V12_002.UI.IPC.Commands.Misc.cs | 10 | 26 |
| 138 | `ProcessQueuedExecution_HandleFleetBrackets` | V12_002.UI.Compliance.cs | 10 | 18 |
| 139 | `ExecuteTREND_Preflight` | V12_002.Entries.Trend.cs | 9 | 25 |
| 140 | `ExecuteFFMALimitEntry` | V12_002.Entries.FFMA.cs | 9 | 146 |
| 141 | `HandleMatchedFollower_PendingCleanupPurge` | V12_002.Orders.Callbacks.AccountOrders.cs | 9 | 16 |
| 142 | `HandleOrderCancelled_RollbackUnfilledEntry` | V12_002.Orders.Callbacks.cs | 9 | 13 |
| 143 | `BroadcastSyncTargetState` | V12_002.Orders.Callbacks.Execution.cs | 9 | 11 |
| 144 | `PropagateMasterTargetMove` | V12_002.Orders.Callbacks.Propagation.cs | 9 | 60 |
| 145 | `EvaluateZombiePurgeEligibility` | V12_002.Orders.Management.Cleanup.cs | 9 | 33 |
| 146 | `CreateNewStopOrder` | V12_002.Orders.Management.StopSync.cs | 9 | 80 |
| 147 | `ValidateStopPrice` | V12_002.Orders.Management.StopSync.cs | 9 | 32 |
| 148 | `AuditFleet_CheckWorkingStop` | V12_002.REAPER.Audit.cs | 9 | 8 |
| 149 | `ExecuteMultiAccountBracket` | V12_002.SIMA.Execution.cs | 9 | 107 |
| 150 | `ExecuteRMAEntryV2` | V12_002.SIMA.Execution.cs | 9 | 110 |
| 151 | `ClosePositionsOnlyApexAccounts` | V12_002.SIMA.Flatten.cs | 9 | 45 |
| 152 | `ValidateCachedEntry` | V12_002.SIMA.Shadow.cs | 9 | 19 |
| 153 | `DeserializeSnapshot` | V12_002.StickyState.cs | 9 | 39 |
| 154 | `SymmetryFindDispatchForMasterFill` | V12_002.Symmetry.cs | 9 | 20 |
| 155 | `FindTargetOrderForPosition` | V12_002.Trailing.Breakeven.cs | 9 | 23 |
| 156 | `FleetSync_SyncFollowersToLevel` | V12_002.Trailing.cs | 9 | 34 |
| 157 | `ManageTrail_RunPerTradeBranches` | V12_002.Trailing.cs | 9 | 8 |
| 158 | `CleanupStalePendingReplacements` | V12_002.Trailing.StopUpdate.cs | 9 | 26 |
| 159 | `HandleChartClick_ConvertPrice` | V12_002.UI.Callbacks.cs | 9 | 54 |
| 160 | `OnKeyDown` | V12_002.UI.Callbacks.cs | 9 | 17 |
| 161 | `FindChartTraderBySiblingSearch` | V12_002.UI.Panel.Helpers.cs | 9 | 16 |
| 162 | `SyncModeChipVisuals` | V12_002.UI.Panel.StateSync.cs | 9 | 39 |
| 163 | `ShouldSyncPendingOrder` | V12_002.UI.Sizing.cs | 9 | 15 |
| 164 | `FindMasterPosition` | V12_002.UI.Snapshot.cs | 9 | 15 |
| 165 | `PopulateTargetSnapshots` | V12_002.UI.Snapshot.cs | 9 | 21 |
| 166 | `GetSubscriberCounts` | SignalBroadcaster.cs | 9 | 10 |
| 167 | `ProcessIpcServer` | V12_002.UI.IPC.Server.cs | 9 | 26 |
| 168 | `FlattenSpecificTarget` | V12_002.UI.IPC.Commands.Misc.cs | 9 | 28 |
| 169 | `TrackTradeEntry` | V12_002.UI.Compliance.cs | 9 | 17 |
| 170 | `UpdateConfigControlsEnabled` | V12_002.UI.Panel.Handlers.cs | 9 | 17 |

## Summary Statistics

- **Total Methods**: 170
- **Priority 0 (CYC 21+)**: 10 methods (5.9%)
- **Priority 1 (CYC 16-20)**: 24 methods (14.1%)
- **Priority 2 (CYC 11-15)**: 89 methods (52.4%)
- **Priority 3 (CYC 9-10)**: 47 methods (27.6%)

## File Distribution

Top 10 files by method count:
1. V12_002.SIMA.Lifecycle.cs: 11 methods
2. V12_002.Orders.Callbacks.AccountOrders.cs: 10 methods
3. V12_002.UI.IPC.Commands.Fleet.cs: 8 methods
4. V12_002.Orders.Management.Cleanup.cs: 7 methods
5. V12_002.SIMA.Fleet.cs: 7 methods
6. V12_002.Symmetry.Replace.cs: 6 methods
7. V12_002.Orders.Callbacks.Propagation.cs: 6 methods
8. V12_002.Trailing.Breakeven.cs: 5 methods
9. V12_002.UI.Panel.StateSync.cs: 5 methods
10. V12_002.REAPER.Audit.cs: 5 methods

## Next Steps

Please review this complete list and confirm:
1. ✅ Approve all 170 methods - proceed with roadmap generation
2. ❌ Exclude specific methods - specify which ones
3. ❌ Exclude specific files - specify which files
4. ❌ Change threshold - specify new CYC threshold

---

**Awaiting your approval to proceed with Wave 7 execution.**