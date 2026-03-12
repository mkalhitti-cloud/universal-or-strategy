// V12.44 MODULAR: Order Callbacks Module (Split from Orders.cs)
// Contains: OnOrderUpdate, OnAccountOrderUpdate, OnPositionUpdate, OnExecutionUpdate
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
        #region Order Callbacks

        /// <summary>
        /// Applies a target fill in a partial-fill-safe way.
        /// - Uses cumulative filled quantity to avoid over/under-decrement when callbacks race.
        /// - Marks target as filled only when complete (or when caller forces completion from a terminal order event).
        /// </summary>
        private void ApplyTargetFill(
            PositionInfo pos,
            int targetNumber,
            int fillQty,
            bool forceComplete,
            out bool alreadyProcessed,
            out int appliedQty,
            out int remainingContractsAfter)
        {
            alreadyProcessed = false;
            appliedQty = 0;
            remainingContractsAfter = 0;

            alreadyProcessed = IsTargetFilled(pos, targetNumber);
            if (alreadyProcessed)
            {
                remainingContractsAfter = pos.RemainingContracts;
                return;
            }

            int targetContracts = Math.Max(0, GetTargetContracts(pos, targetNumber));
            int filledQty = Math.Max(0, GetTargetFilledQuantity(pos, targetNumber));
            int remainingTargetQty = Math.Max(0, targetContracts - filledQty);

            int requestedFillQty = Math.Max(0, fillQty);
            appliedQty = Math.Min(requestedFillQty, remainingTargetQty);

            if (appliedQty > 0)
            {
                filledQty += appliedQty;
                SetTargetFilledQuantity(pos, targetNumber, filledQty);
                pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - appliedQty);
            }

            bool isComplete = forceComplete || filledQty >= targetContracts;
            if (isComplete)
            {
                SetTargetFilledQuantity(pos, targetNumber, Math.Max(filledQty, targetContracts));
                MarkTargetFilled(pos, targetNumber);
            }

            remainingContractsAfter = pos.RemainingContracts;
        }

        // V12.1101E [F-07]: Request stop cancellation without dropping dictionary state early.
        // We only remove references after broker-confirmed terminal states.
        // [BUILD 925 - P1 Fix]: Route follower stop cancels through pos.ExecutingAccount.Cancel()
        // instead of the master-local CancelOrder() API. CancelOrder() is a NinjaScript-managed
        // call that only works for orders submitted via SubmitOrderUnmanaged(). Fleet follower
        // stops are submitted via acct.Submit(), so they require the broker-level Account.Cancel()
        // API -- identical to the pattern already proven correct in CleanupPosition() [BUG-2a].
        private void RequestStopCancelLifecycleSafe(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;
            if (!stopOrders.TryGetValue(entryName, out var stopOrder) || stopOrder == null) return;

            // V12.1101H [COLLIDE-01]: Include ChangePending/ChangeSubmitted -- stops in these transient
            // states were previously ignored by this function, leaving them live at the broker after FlattenAll.
            if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted
                || stopOrder.OrderState == OrderState.ChangePending || stopOrder.OrderState == OrderState.ChangeSubmitted)
            {
                // [BUILD 925 - P1 Fix]: Check if this is a fleet follower -- use its account context.
                bool isFollowerStop = activePositions.TryGetValue(entryName, out var posRef)
                    && posRef != null && posRef.IsFollower && posRef.ExecutingAccount != null;

                if (isFollowerStop)
                {
                    // Fleet follower stop: must use Account API -- CancelOrder() targets master account only.
                    Print(string.Format("[925-P1] Follower stop cancel routed via ExecutingAccount.Cancel() for {0} on {1}",
                        entryName, posRef.ExecutingAccount.Name));
                    posRef.ExecutingAccount.Cancel(new[] { stopOrder });
                }
                else
                {
                    // Master/local stop: use the standard NinjaScript managed cancel.
                    CancelOrder(stopOrder);
                }
                return;
            }

            if (stopOrder.OrderState == OrderState.Cancelled || stopOrder.OrderState == OrderState.Filled ||
                stopOrder.OrderState == OrderState.Rejected || stopOrder.OrderState == OrderState.Unknown)
            {
                stopOrders.TryRemove(entryName, out _);
            }
        }

        // V12.1101E [F-07]: Broker-confirmed target cleanup fallback when position state was already torn down.
        private bool TryRemoveTargetReferenceByOrder(ConcurrentDictionary<string, Order> dict, Order order)
        {
            if (dict == null || order == null) return false;
            foreach (var kvp in dict.ToArray())
            {
                if (kvp.Value == order)
                {
                    dict.TryRemove(kvp.Key, out _);
                    return true;
                }
            }
            return false;
        }

        // V12.1101E [F-07]: Removes terminal target refs using broker-confirmed order object identity.
        private void RemoveTargetReferenceOnTerminalFill(Order order)
        {
            if (order == null) return;
            if (TryRemoveTargetReferenceByOrder(target1Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target2Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target3Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target4Orders, order)) return;
            TryRemoveTargetReferenceByOrder(target5Orders, order);
        }

        // V12.962 INLINE ACTOR: Thin-shell entry point. Captures order-object reference and all
        // primitive args before Enqueue. ProcessOnOrderUpdate runs lock-free inside the drain.
        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
        {
            // Order reference is stable (NT8 managed object); capture primitives to avoid
            // any potential race between callback return and drain execution.
            Order      _o  = order;
            double     _lp = limitPrice;
            double     _sp = stopPrice;
            int        _q  = quantity;
            int        _f  = filled;
            double     _af = averageFillPrice;
            OrderState _os = orderState;
            DateTime   _t  = time;
            string     _ne = nativeError ?? string.Empty;
            Enqueue(ctx => ctx.ProcessOnOrderUpdate(_o, _lp, _sp, _q, _f, _af, _os, _t, _ne));
        }

        private void ProcessOnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, string nativeError)
        {
            try
            {
                if (order.Account == this.Account && 
                    (orderState == OrderState.Working || orderState == OrderState.Accepted || orderState == OrderState.ChangeSubmitted))
                {
                    PropagateMasterPriceMove(order, limitPrice, stopPrice, quantity);
                }

                bool handled = false;

                if (orderState == OrderState.Filled)
                {
                    if (entryOrders.Values.Contains(order))
                        handled = HandleEntryOrderFilled(order, quantity, filled, averageFillPrice, time);
                    else
                        handled = HandleSecondaryOrderFilled(order, averageFillPrice);
                }
                else if (orderState == OrderState.Rejected)
                {
                    handled = HandleOrderRejected(order, nativeError);
                }
                else if (orderState == OrderState.Cancelled)
                {
                    handled = HandleOrderCancelled(order);
                }
                else if (orderState == OrderState.Accepted || orderState == OrderState.Working)
                {
                    handled = HandleOrderPriceOrQuantityChanged(order, limitPrice, stopPrice, quantity);
                }

                // Terminal catch-all
                if (!handled && (orderState == OrderState.Cancelled || orderState == OrderState.Rejected || orderState == OrderState.Unknown))
                {
                    RemoveGhostOrderRef(order, orderState.ToString().ToUpper());
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        private bool HandleEntryOrderFilled(Order order, int quantity, int filled, double averageFillPrice, DateTime time)
        {
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                if (entryOrders.TryGetValue(kvp.Key, out var entryOrder) && entryOrder == order && !kvp.Value.EntryFilled)
                {
                    PositionInfo pos = kvp.Value;
                    if (!pos.IsFollower)
                    {
                        int masterFillQty = filled > 0 ? filled : quantity;
                        SymmetryGuardOnMasterFill(kvp.Key, pos, averageFillPrice, masterFillQty, time.ToUniversalTime());
                    }

                    if (averageFillPrice <= 0)
                    {
                        pos.EntryFilled = true; pos.InitialTargetCount = activeTargetCount;
                        Print(string.Format("[PRICE_GUARD] CRITICAL: averageFillPrice=0 for {0}. Keeping intended price {1:F2}. NOT re-anchoring.", kvp.Key, pos.EntryPrice));
                        SubmitBracketOrders(kvp.Key, pos);
                        return true;
                    }

                    pos.EntryFilled = true;
                    pos.InitialTargetCount = activeTargetCount;
                    pos.EntryPrice = averageFillPrice;
                    pos.ExtremePriceSinceEntry = averageFillPrice;
                    // Recalculate targets and stop
                    double stopDistance = pos.IsRMATrade ? currentATR * RMAStopATRMultiplier : Math.Abs(pos.InitialStopPrice - pos.EntryPrice);
                    pos.Target1Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 1);
                    pos.Target2Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 2);
                    pos.Target3Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 3);
                    pos.Target4Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 4);
                    pos.Target5Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 5);
                    stopDistance = Math.Min(stopDistance, 12.0);
                    pos.InitialStopPrice = pos.Direction == MarketPosition.Long ? averageFillPrice - stopDistance : averageFillPrice + stopDistance;
                    pos.CurrentStopPrice = pos.InitialStopPrice;
                    ApplyTargetLadderGuard(pos);

                    Print(string.Format("{0} ENTRY FILLED: {1} {2} @ {3:F2}", pos.IsRMATrade ? "RMA" : "OR", pos.Direction, pos.TotalContracts, averageFillPrice));
                    SubmitBracketOrders(kvp.Key, pos);
                    return true;
                }
            }
            return false;
        }

        private bool HandleSecondaryOrderFilled(Order order, double averageFillPrice)
        {
            string orderName = order.Name;

            // Targets 1-5
            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict != null && tDict.Values.Contains(order))
                {
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (tDict.TryGetValue(kvp.Key, out var tOrder) && tOrder == order)
                        {
                            PositionInfo pos = kvp.Value;
                            ApplyTargetFill(pos, tNum, GetTargetContracts(pos, tNum), true, out _, out int appQty, out int rem);
                            Print(string.Format("T{0} FILLED ({1}): {2} contracts @ {3:F2} | Remaining: {4}", tNum, kvp.Key, appQty, averageFillPrice, rem));
                            UpdateStopQuantity(kvp.Key, pos);
                            tDict.TryRemove(kvp.Key, out _);
                            return true;
                        }
                    }
                }
            }

            // Stop filled
            if (orderName.StartsWith("Stop_") || orderName.StartsWith("S_"))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (stopOrders.TryGetValue(kvp.Key, out var sOrder) && sOrder == order)
                    {
                        Print(string.Format("STOP FILLED: {0} contracts @ {1:F2}", kvp.Value.RemainingContracts, averageFillPrice));
                        CleanupPosition(kvp.Key);
                        return true;
                    }
                }
                // Fallback by name
                string entryName = ExtractEntryNameFromStop(orderName);
                if (activePositions.TryGetValue(entryName, out var pos))
                {
                    Print(string.Format("STOP FILLED (by name): {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                    CleanupPosition(entryName);
                    return true;
                }
            }

            if (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") || orderName.StartsWith("T4_") || orderName.StartsWith("T5_") || orderName.StartsWith("Runner_"))
            {
                RemoveTargetReferenceOnTerminalFill(order);
                return true;
            }

            return false;
        }

        private string ExtractEntryNameFromStop(string orderName)
        {
            string stopPrefix = orderName.StartsWith("Stop_") ? "Stop_" : "S_";
            string entryNameFromOrder = orderName.Substring(stopPrefix.Length);
            int lastUnderscore = entryNameFromOrder.LastIndexOf('_');
            if (lastUnderscore > 0 && entryNameFromOrder.Length - lastUnderscore > 10)
                entryNameFromOrder = entryNameFromOrder.Substring(0, lastUnderscore);
            return entryNameFromOrder;
        }

        private bool HandleOrderRejected(Order order, string nativeError)
        {
            string orderName = order.Name;
            Print(string.Format("ORDER REJECTED: {0} | Error: {1}", orderName, nativeError));

            if (stopOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (stopOrders.TryGetValue(kvp.Key, out var sOrder) && sOrder == order)
                    {
                        Print(string.Format("?? ?? CRITICAL: Stop REJECTED for {0}. Re-submitting...", kvp.Key));
                        stopOrders.TryRemove(kvp.Key, out _);
                        CreateNewStopOrder(kvp.Key, kvp.Value.RemainingContracts, kvp.Value.CurrentStopPrice, kvp.Value.Direction);
                        return true;
                    }
                }
            }

            if (entryOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        Print(string.Format("[ZOMBIE-FIX] Entry REJECTED: {0}. Tearing down.", orderName));
                        RollbackExpectedPosition(kvp.Key, kvp.Value);
                        CleanupPosition(kvp.Key);
                        return true;
                    }
                }
            }

            RemoveGhostOrderRef(order, "REJECTED");
            return true;
        }

        private void RollbackExpectedPosition(string entryName, PositionInfo pos)
        {
            string acctName = (pos.IsFollower && pos.ExecutingAccount != null) ? pos.ExecutingAccount.Name : Account.Name;
            int delta = (pos.Direction == MarketPosition.Long) ? -pos.TotalContracts : pos.TotalContracts;
            DeltaExpectedPosition(ExpKey(acctName), delta);
            ClearDispatchSyncPending(ExpKey(acctName));
        }

        private bool HandleOrderCancelled(Order order)
        {
            string orderName = order.Name;
            bool handled = false;

            // Stop replacement check
            if (orderName.StartsWith("Stop_") || orderName.StartsWith("S_"))
            {
                foreach (var kvp in pendingStopReplacements.ToArray())
                {
                    if (kvp.Value.OldOrder == order && activePositions.TryGetValue(kvp.Key, out var pos))
                    {
                        // Build 955: Snapshot qty under stateLock -- single atomic read for both check and use.
                        int _stopQty;
                        _stopQty = pos.RemainingContracts;
                        if (_stopQty > 0)
                        {
                            CreateNewStopOrder(kvp.Key, _stopQty, kvp.Value.StopPrice, kvp.Value.Direction);
                            // Build 950: Restore OCO-cascade-cancelled targets after stop replacement.
                            if (kvp.Value.BracketRestorationNeeded && kvp.Value.CapturedTargets != null)
                            {
                                TargetSnapshot[] _mSnap = kvp.Value.CapturedTargets;
                                string _mKey = kvp.Key;
                                TriggerCustomEvent(o => RestoreCascadedTargets(_mKey, _mSnap), null);
                            }
                        }
                        if (pendingStopReplacements.TryRemove(kvp.Key, out _)) Interlocked.Decrement(ref pendingReplacementCount);
                        handled = true;
                        break;
                    }
                }

                // A2-2: Deferred PendingCleanup purge -- master stop terminal (Build 960 audit fix).
                // If no pendingStopReplacement matched, check if this stop cancel completes a
                // final-target/trim close where activePositions was intentionally kept alive.
                if (!handled)
                {
                    foreach (var kvp in stopOrders.ToArray())
                    {
                        if (kvp.Value == order)
                        {
                            PositionInfo cleanupPos;
                            if (activePositions.TryGetValue(kvp.Key, out cleanupPos) && cleanupPos != null
                                && cleanupPos.PendingCleanup && cleanupPos.RemainingContracts <= 0)
                            {
                                stopOrders.TryRemove(kvp.Key, out _);
                                activePositions.TryRemove(kvp.Key, out _);
                                SymmetryGuardForgetEntry(kvp.Key);
                                Print("[A2-2] Deferred PendingCleanup purge (master stop cancel): " + kvp.Key);
                            }
                            break;
                        }
                    }
                }
            }

            if (!handled && entryOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        if (EnableSIMA && !kvp.Value.IsFollower) SymmetryGuardCascadeFollowerCleanup(kvp.Key);
                        RollbackExpectedPosition(kvp.Key, kvp.Value);
                        CleanupPosition(kvp.Key);
                        return true;
                    }
                }
            }

            RemoveGhostOrderRef(order, "CANCELLED");
            return true;
        }

        private bool HandleOrderPriceOrQuantityChanged(Order order, double limitPrice, double stopPrice, int quantity)
        {
            if (entryOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        double newPrice = limitPrice > 0 ? limitPrice : stopPrice;
                        if (newPrice > 0 && Math.Abs(newPrice - kvp.Value.EntryPrice) > tickSize * 0.5)
                        {
                            kvp.Value.EntryPrice = newPrice;
                            Print(string.Format("V12: Entry order MOVED: {0} to {1:F2}", kvp.Key, newPrice));
                        }
                        int _totalContracts;
                        _totalContracts = kvp.Value.TotalContracts;
                        if (quantity > 0 && quantity != _totalContracts)
                        {
                            // [937-FIX] Sync expectedPositions with broker-confirmed qty.
                            // Without this, RollbackExpectedPosition uses stale TotalContracts -> desync.
                            int qtyDiff = quantity - _totalContracts;
                            string fixAcct = (kvp.Value.IsFollower && kvp.Value.ExecutingAccount != null)
                                ? kvp.Value.ExecutingAccount.Name : Account.Name;
                            int expDelta = (kvp.Value.Direction == MarketPosition.Long) ? qtyDiff : -qtyDiff;
                            DeltaExpectedPosition(ExpKey(fixAcct), expDelta);
                            Print(string.Format("[937-FIX] expectedPositions adjusted on qty change: {0} delta={1}", fixAcct, expDelta));
                            kvp.Value.TotalContracts = quantity;
                            kvp.Value.RemainingContracts = quantity;
                            GetTargetDistribution(quantity, out kvp.Value.T1Contracts, out kvp.Value.T2Contracts, out kvp.Value.T3Contracts, out kvp.Value.T4Contracts, out kvp.Value.T5Contracts);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

    #endregion

    }
}
