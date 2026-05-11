// Build 971: Orders.Callbacks.Execution -- OnPositionUpdate, ProcessOnPositionUpdate, HandleFlatPositionUpdate, BroadcastSyncTargetState, OnExecutionUpdate, ProcessOnExecutionUpdate
// V12 Orders.Callbacks Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Orders Callbacks Execution

        protected override void OnPositionUpdate(Position position, double averagePrice,
            int quantity, MarketPosition marketPosition)
        {
            // Capture only primitive/string fields -- no NT8 object in closure [B967-FIX-01]
            string _acctName967 = position?.Account?.Name ?? "UNKNOWN"; // [B967-FIX-01]
            var    _mp967       = marketPosition;
            Enqueue(ctx => ctx.ProcessOnPositionUpdate(_acctName967, _mp967)); // [B967-FIX-01]
        }

        private void ProcessOnPositionUpdate(string acctName, MarketPosition marketPosition) // [B967-FIX-01]
        {
            try
            {
                if (marketPosition == MarketPosition.Flat)
                    HandleFlatPositionUpdate(acctName); // [B967-FIX-01]
                BroadcastSyncTargetState();
            }
            catch (Exception ex) { Print("ERROR OnPositionUpdate: " + ex.Message); }
        }

        // Build 935 [CB-B935-001]: Flat-position cleanup extracted from OnPositionUpdate.
        private void HandleFlatPositionUpdate(string acctName) // [B967-FIX-01]
        {
            HandleFlatPosition_SyncExpected(acctName);
            if (HandleFlatPosition_ReconcileOrphans())
                return;
            HandleFlatPosition_CleanupActivePositions();
        }

        private void HandleFlatPosition_SyncExpected(string acctName)
        {
            // [H-14]: Sync expectedPositions on flat. Build 931: guard against spurious flat.
            string flatAcctName = acctName;
            if (!string.IsNullOrEmpty(flatAcctName))
            {
                string flatExpKey = ExpKey(flatAcctName);
                bool hasSyncPending = IsDispatchSyncPending(flatExpKey);
                bool hasPendingEntry = HasPendingEntryForAcct(flatAcctName);

                bool hasActivePositionForAcct = false;
                if (!hasPendingEntry)
                {
                    hasActivePositionForAcct = HasUnfilledActivePositionForAcct(flatAcctName);
                }

                if (hasPendingEntry || hasActivePositionForAcct || hasSyncPending)
                {
                    string skipReason = hasPendingEntry
                        ? "pending entry in flight"
                        : (hasActivePositionForAcct ? "activePositions metadata present" : "dispatch sync pending");
                    Print($"[OnPositionUpdate] H-14 SKIP: {flatExpKey} broker=Flat but {skipReason} -- not resetting expectedPositions");
                }
                else
                {
                    SetExpectedPositionLocked(flatExpKey, 0);
                    Print($"[OnPositionUpdate] expectedPositions cleared for {flatExpKey} (position flat)");
                }
            }
        }

        private bool HasPendingEntryForAcct(string flatAcctName)
        {
            foreach (var kvp in entryOrders.ToArray())
            {
                var ord = kvp.Value;
                if (ord != null
                    && !IsOrderTerminal(ord.OrderState)
                    && activePositions.TryGetValue(kvp.Key, out var pos)
                    && pos.ExecutingAccount != null
                    && pos.ExecutingAccount.Name == flatAcctName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUnfilledActivePositionForAcct(string flatAcctName)
        {
            foreach (var kvp in activePositions.ToArray())
            {
                if (kvp.Value.ExecutingAccount != null
                    && kvp.Value.ExecutingAccount.Name == flatAcctName
                    && !kvp.Value.EntryFilled)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HandleFlatPosition_ReconcileOrphans()
        {
            // V8.22: Scan for orphans even if activePositions is empty (strategy restart)
            if (activePositions.Count == 0)
            {
                Print("EXTERNAL CLOSE/RESTART DETECTED - Scanning for orphaned bracket orders...");
                ReconcileOrphanedOrders("Position went flat");
                return true;
            }

            return false;
            }

        private void HandleFlatPosition_CleanupActivePositions()
        {
            List<string> positionsToCleanup = new List<string>();
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && pos.RemainingContracts > 0)
                {
                    Print("EXTERNAL CLOSE DETECTED - Position went flat. Cancelling orphaned orders...");
                    if (stopOrders.TryGetValue(kvp.Key, out var stopOrder))
                    {
                        if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                            CancelOrderSafe(stopOrder, pos);
                    }
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var tDict = GetTargetOrdersDictionary(tNum);
                        if (tDict != null && tDict.TryGetValue(kvp.Key, out var tOrder))
                        {
                            if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                                CancelOrderSafe(tOrder, pos);
                        }
                    }
                    positionsToCleanup.Add(kvp.Key);
                }
            }

            foreach (string key in positionsToCleanup)
                CleanupPosition(key);

            if (positionsToCleanup.Count > 0)
                Print("Cleanup complete - Strategy still running, ready for new entries.");
        }

        // Build 935 [CB-B935-002]: Target count broadcast extracted from OnPositionUpdate.
        private void BroadcastSyncTargetState()
        {
            // V14 ADAPTIVE VISIBILITY: Broadcast current position size to panel
            if (State != State.Realtime) return;

            // Build 1102Y-V2 [U-04]: Use live InitialTargetCount when in trade; fallback to dashboard count when flat.
            int syncCount = activeTargetCount;
            if (Position != null && Position.MarketPosition != MarketPosition.Flat)
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    PositionInfo p = kvp.Value;
                    if (!p.IsFollower && p.EntryFilled && p.RemainingContracts > 0 && p.InitialTargetCount > 0)
                    {
                        syncCount = p.InitialTargetCount;
                        break;
                    }
                }
            }
            SendResponseToRemote($"SYNC_TARGET_STATE|{syncCount}");
        }

        // V12.962 INLINE ACTOR: Thin-shell for OnExecutionUpdate.
        // Captures Execution fields before Enqueue; ProcessOnExecutionUpdate runs lock-free inside the drain.
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution == null || execution.Order == null) return;
            // Capture all values from Execution -- NT8 may recycle the object after callback returns
            string     _on  = execution.Order.Name ?? string.Empty;
            string     _eid = executionId ?? string.Empty;
            string     _oid = execution.Order.OrderId ?? string.Empty;
            int        _of  = execution.Order.Filled;
            OrderState _ost = execution.Order.OrderState;
            double     _pr  = price;
            int        _qty = quantity;
            Execution  _ex  = execution; // Reference kept -- stable for compliance TrackTradeEntry path
            Enqueue(ctx => ctx.ProcessOnExecutionUpdate(_on, _eid, _oid, _of, _ost, _pr, _qty, _ex));
        }

        private void ProcessOnExecutionUpdate(
            string orderName, string executionId, string orderId,
            int orderFilled, OrderState orderState,
            double price, int quantity, Execution execution)
        {
            try
            {
                if (string.IsNullOrEmpty(orderName)) return;

                if (ProcessOnExecution_Dedup(orderName, executionId, quantity, execution))
                    return;

                ProcessOnExecution_TrackCompliance(execution);

                // ============================================================
                // 1. STOP LOSS FILL - Manual OCO: Cancel all remaining targets
                // ============================================================
                if (orderName.StartsWith("Stop_"))
                    ProcessOnExecution_HandleStopFill(orderName, price, quantity);

                // ============================================================
                // 2. TARGET 1-5 FILL - Reduce stop quantity (unified loop)
                // V12.1101E [SK-01/A-1]: First-Writer-Wins guard prevents double-decrement.
                // ============================================================
                else if (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") ||
                         orderName.StartsWith("T4_") || orderName.StartsWith("T5_"))
                    ProcessOnExecution_HandleTargetFill(orderName, price, quantity, execution);

                // ============================================================
                // 5. TRIM EXECUTION - V10.3.1: Enhanced Stop Integrity
                // ============================================================
                // (!) CRITICAL: When a TRIM executes, we MUST reduce the stop order quantity
                // to match the new position size. If we don't, hitting the stop after a trim
                // would close more contracts than we hold, creating an unintended REVERSE position.
                // Example: Long 4 contracts, stop at 4. Trim 2 (now Long 2). If stop stays at 4,
                // getting stopped out would SELL 4 (close 2 + go SHORT 2) = DISASTER.
                else if (orderName.StartsWith("Trim_"))
                    ProcessOnExecution_HandleTrimFill(orderName, price, quantity);

                // Build 1105: Shadow callback injection -- closes 100-500ms leader flatten gap.
                // ManageTrailingStops covers steady-state trailing. This covers immediate
                // execution events (stop fill, target fill) where next trailing cycle is too late.
                ProcessOnExecution_RunShadowCheck();
            }
            catch (Exception ex)
            {
                Print("Error OnExecutionUpdate: " + ex.Message);
            }
        }

        private bool ProcessOnExecution_Dedup(string orderName, string executionId, int quantity, Execution execution)
        {
                // V12.962 INLINE ACTOR: Dedup guard -- lock-free, serial execution guaranteed by _drainToken.
                // V12.Phase7 [C-01]: Prevent double-decrement if OnOrderUpdate + OnExecutionUpdate both fire.
                if (!string.IsNullOrEmpty(executionId))
                {
                    // V14.2 [ADR-011]: Zero-allocation dedup via FNV-1a hash ring
                    long _execHash = FnvHash64(executionId);
                    if (_executionIdRing.ContainsOrAdd(_execHash))
                    {
                        Print(string.Format("[DEDUP] Skipping duplicate execution {0} for {1}", executionId, orderName));
                    return true;
                    }
                }
                else
                {
                    // V14.2 [ADR-011]: Fallback dedup when executionId is missing
                    // Uses execution.Order properties -- FIX-D5: correct variable mapping
                    string uniqueOrderId = !string.IsNullOrEmpty(execution.Order.OrderId)
                        ? execution.Order.OrderId : execution.Order.Name;
                    int dedupFilledQty = execution.Order.Filled > 0
                        ? execution.Order.Filled : Math.Max(0, quantity);
                    string _fallbackKey = string.Format("{0}|{1}", uniqueOrderId, dedupFilledQty);
                    long _fallbackHash = FnvHash64(_fallbackKey);
                    if (_executionIdFallbackRing.ContainsOrAdd(_fallbackHash))
                    {
                        Print(string.Format("[DEDUP] Skipping duplicate execution (fallback) orderId={0}",
                            execution.Order.OrderId));
                    return true;
                    }
                }

            return false;
        }

        private void ProcessOnExecution_TrackCompliance(Execution execution)
        {
                // V12.12: Compliance tracking for single-account mode
                // [939-P0]: Marshal Account.Get() off broker thread via TriggerCustomEvent.
                if (EnableComplianceHub && !EnableSIMA)
                {
                    TrackTradeEntry(Account, execution);
                    TriggerCustomEvent(o => UpdateAccountMetricsFromAccount(Account), null);
                    LogApexPerformance();
                }
        }

        private string ProcessOnExecution_ExtractEntryName(string name, string prefix)
                {
                    if (!name.StartsWith(prefix)) return "";
                    string entryPart = name.Substring(prefix.Length);
                    // Strip timestamp suffix if present (format: _123456789012345)
                    int lastUnderscore = entryPart.LastIndexOf('_');
                    if (lastUnderscore > 0 && entryPart.Length - lastUnderscore > 10)
                        entryPart = entryPart.Substring(0, lastUnderscore);
                    return entryPart;
        }

        private void ProcessOnExecution_HandleStopFill(string orderName, double price, int quantity)
                {
            string entryName = ProcessOnExecution_ExtractEntryName(orderName, "Stop_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int remainingAfterStop;
                        pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                        remainingAfterStop = pos.RemainingContracts;

                        Print(string.Format("STOP FILLED: {0} @ {1:F2}. Cancelling targets.", quantity, price));

                        // Manual OCO: Cancel all remaining profit targets immediately
                        // V12.1101E [F-07]: Keep target dictionary refs until terminal broker confirmation.
                        int cancelledTargets = 0;
                        for (int tNum = 1; tNum <= 5; tNum++)
                        {
                            var tDict = GetTargetOrdersDictionary(tNum);
                            if (tDict != null && tDict.TryGetValue(entryName, out var tOrder))
                            {
                                if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrderSafe(tOrder, pos);
                                    cancelledTargets++;
                                }
                            }
                        }

                        if (cancelledTargets > 0)
                        {
                            Print(string.Format("OCO: Cancelled {0} target orders for {1}", cancelledTargets, entryName));
                        }

                        // B957/D1: Only remove stopOrders and pendingStopReplacements when position is fully closed.
                        // Do NOT remove on partial fills -- the stop may still be tracking residual contracts.
                        if (remainingAfterStop <= 0)
                        {
                            stopOrders.TryRemove(entryName, out _);
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                                Interlocked.Decrement(ref pendingReplacementCount);
                            activePositions.TryRemove(entryName, out _);
                            entryOrders.TryRemove(entryName, out _);
                        }
                        if (remainingAfterStop <= 0)
                        {
                            SymmetryGuardForgetEntry(entryName);
                            Print(string.Format("Position {0} fully closed by stop.", entryName));
                        }
                    }
                }

        private void ProcessOnExecution_HandleTargetFill(string orderName, double price, int quantity, Execution execution)
                {
                    // Extract target number from prefix (T1_, T2_, etc.)
                    int targetNum = orderName[1] - '0';
                    string targetPrefix = "T" + targetNum + "_";
            string entryName = ProcessOnExecution_ExtractEntryName(orderName, targetPrefix);

                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        bool terminalFill = execution.Order.OrderState == OrderState.Filled;
                        bool alreadyProcessed;
                        int appliedQty;
                        int remainingAfter;
                        ApplyTargetFill(pos, targetNum, quantity, terminalFill, out alreadyProcessed, out appliedQty, out remainingAfter);
                        if (alreadyProcessed)
                        {
                            Print(string.Format("[1101E GUARD] T{0} already processed for {1} -- skipping duplicate OnExecutionUpdate fill", targetNum, entryName));
                            if (terminalFill)
                            {
                                var tDict = GetTargetOrdersDictionary(targetNum);
                                if (tDict != null) tDict.TryRemove(entryName, out _);
                            }
                            return;
                        }

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            appliedQty, price, remainingAfter));

                        if (remainingAfter > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            // A2-2: Defer activePositions.TryRemove to broker-confirmed stop terminal state (Build 960)
                            ProcessOnExecution_FinalizeFullClose(entryName);
                        }

                        // V12.1101E [F-07]: Clear target ref only after broker confirms Filled.
                        if (terminalFill)
                        {
                            var tDict = GetTargetOrdersDictionary(targetNum);
                            if (tDict != null) tDict.TryRemove(entryName, out _);
                        }
                    }
                }

        private void ProcessOnExecution_HandleTrimFill(string orderName, double price, int quantity)
                {
            string entryName = ProcessOnExecution_ExtractEntryName(orderName, "Trim_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int previousQty;
                        int remainingAfterTrim;
                        previousQty = pos.RemainingContracts;
                        pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                        remainingAfterTrim = pos.RemainingContracts;

                        Print(string.Format("TRIM EXECUTION: {0} contracts closed for {1}. Position: {2} -> {3}",
                            quantity, entryName, previousQty, remainingAfterTrim));

                        // V10.3.1 FIX: MANDATORY stop quantity reduction to prevent reverse position
                        if (remainingAfterTrim > 0)
                        {
                            Print(string.Format("STOP INTEGRITY: Reducing stop quantity from {0} to {1} for {2}",
                                previousQty, remainingAfterTrim, entryName));
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed by trim, cancel stop
                            Print(string.Format("TRIM FLATTEN: Position {0} fully closed. Cancelling stop.", entryName));
                            ProcessOnExecution_FinalizeFullClose(entryName);
                        }
                    }
                }

        private void ProcessOnExecution_FinalizeFullClose(string entryName)
                {
                    // Phase 6 T2.A: deliberate Target/Trim full-close parity hardening.
                    RequestStopCancelLifecycleSafe(entryName);

                    if (pendingStopReplacements.TryRemove(entryName, out _))
                    {
                        Interlocked.Decrement(ref pendingReplacementCount);
                    }

                    if (activePositions.TryGetValue(entryName, out var localPos) && localPos != null)
                    {
                        localPos.PendingCleanup = true; // B957/A: stateLock guards PositionInfo field writes
                    }
                    else
                    {
                        SymmetryGuardForgetEntry(entryName); // already gone -- clean up now
                    }
                }

        private void ProcessOnExecution_RunShadowCheck()
            {
            ShadowEngineCheck();
        }

        /// <summary>
        /// V12.MOVE-SYNC [Build 1102U]: Propagates Master price changes (entry/stop/target) to all
        /// linked follower accounts. Triggered from Master's OnOrderUpdate.
        ///
        /// Root-cause fixes vs prior implementation:
        ///   1. Object-identity lookup replaces fragile signal-name substring matching.
        ///   2. Stop moves delegate to UpdateStopOrder (cancel/resubmit via follower Account API).
        ///   3. Target moves use pos.ExecutingAccount.Cancel + CreateOrder + Submit (not ChangeOrder).
        ///   4. Entry (Limit, pre-fill) moves implemented via cancel/resubmit.
        /// </summary>

        #endregion
    

        private string ProcessOnExecution_ExtractEntryName(string name, string prefix)
                {
                    if (!name.StartsWith(prefix)) return "";
                    string entryPart = name.Substring(prefix.Length);
                    // Strip timestamp suffix if present (format: _123456789012345)
                    int lastUnderscore = entryPart.LastIndexOf('_');
                    if (lastUnderscore > 0 && entryPart.Length - lastUnderscore > 10)
                        entryPart = entryPart.Substring(0, lastUnderscore);
                    return entryPart;
        }

        private void ProcessOnExecution_RunShadowCheck()
            {
            ShadowEngineCheck();
        }
}
}
