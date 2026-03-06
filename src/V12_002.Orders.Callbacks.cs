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

            lock (stateLock)
            {
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
                    return dict.TryRemove(kvp.Key, out _);
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

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
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
                    pos.EntryFilled = true;
                    pos.InitialTargetCount = activeTargetCount;

                    if (!pos.IsFollower)
                    {
                        int masterFillQty = filled > 0 ? filled : quantity;
                        SymmetryGuardOnMasterFill(kvp.Key, pos, averageFillPrice, masterFillQty, time.ToUniversalTime());
                    }

                    if (averageFillPrice <= 0)
                    {
                        Print(string.Format("[PRICE_GUARD] CRITICAL: averageFillPrice=0 for {0}. Keeping intended price {1:F2}. NOT re-anchoring.", kvp.Key, pos.EntryPrice));
                        SubmitBracketOrders(kvp.Key, pos);
                        return true;
                    }

                    pos.EntryPrice = averageFillPrice;
                    pos.ExtremePriceSinceEntry = averageFillPrice;

                    // Recalculate targets and stop
                    double stopDistance = pos.IsRMATrade ? currentATR * RMAStopATRMultiplier : Math.Abs(pos.InitialStopPrice - pos.EntryPrice);
                    pos.Target1Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 1);
                    pos.Target2Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 2);
                    pos.Target3Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 3);
                    pos.Target4Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 4);
                    pos.Target5Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 5);
                    ApplyTargetLadderGuard(pos);

                    stopDistance = Math.Min(stopDistance, 12.0);
                    pos.InitialStopPrice = pos.Direction == MarketPosition.Long ? averageFillPrice - stopDistance : averageFillPrice + stopDistance;
                    pos.CurrentStopPrice = pos.InitialStopPrice;

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

            if (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") || orderName.StartsWith("T4_") || orderName.StartsWith("T5_"))
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
            DeltaExpectedPositionLocked(ExpKey(acctName), delta);
            ClearDispatchSyncPending(ExpKey(acctName));
        }

        private bool HandleOrderCancelled(Order order)
        {
            string orderName = order.Name;
            bool handled = false;

            // Build 951: Bracket replace FSM -- each cancel confirm decrements the gate counter.
            // Resubmit fires only when ALL legs confirm (RemainingCancels == 0).
            foreach (var kvp in _bracketReplaceSpecs.ToArray())
            {
                BracketReplaceSpec spec = kvp.Value;
                bool matched;
                lock (spec.SyncRoot)  // Build 951.1: lock on SyncRoot, not the spec instance (DeepSource CA2002)
                {
                    matched = spec.PendingOrderIds.Remove(order.OrderId);
                }
                if (matched)
                {
                    int remaining = Interlocked.Decrement(ref spec.RemainingCancels);
                    Print(string.Format("[MOVE-SYNC] FSM: Cancel confirmed Bracket_{0} -- {1} legs remaining",
                        kvp.Key, remaining));
                    if (remaining <= 0)
                    {
                        // Build 951.1 Bug-A: TryRemove deferred to SubmitBracketReplacement after Account.Submit() succeeds.
                        // Keeping spec in dict ensures REAPER suppression covers the entire cancel-to-resubmit window.
                        BracketReplaceSpec capturedSpec = spec;
                        TriggerCustomEvent(o => SubmitBracketReplacement(capturedSpec), null);
                    }
                    return true;
                }
            }

            // Stop replacement check
            if (orderName.StartsWith("Stop_") || orderName.StartsWith("S_"))
            {
                foreach (var kvp in pendingStopReplacements.ToArray())
                {
                    if (kvp.Value.OldOrder == order && activePositions.TryGetValue(kvp.Key, out var pos))
                    {
                        if (pos.RemainingContracts > 0)
                        {
                            CreateNewStopOrder(kvp.Key, pos.RemainingContracts, kvp.Value.StopPrice, kvp.Value.Direction);
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
                        if (quantity > 0 && quantity != kvp.Value.TotalContracts)
                        {
                            // [937-FIX] Sync expectedPositions with broker-confirmed qty.
                            // Without this, RollbackExpectedPosition uses stale TotalContracts -> desync.
                            int qtyDiff = quantity - kvp.Value.TotalContracts;
                            string fixAcct = (kvp.Value.IsFollower && kvp.Value.ExecutingAccount != null)
                                ? kvp.Value.ExecutingAccount.Name : Account.Name;
                            int expDelta = (kvp.Value.Direction == MarketPosition.Long) ? qtyDiff : -qtyDiff;
                            DeltaExpectedPositionLocked(ExpKey(fixAcct), expDelta);
                            Print(string.Format("[937-FIX] expectedPositions adjusted on qty change: {0} delta={1}", fixAcct, expDelta));
                            lock (stateLock)
                            {
                                kvp.Value.TotalContracts = quantity;
                                kvp.Value.RemainingContracts = quantity;
                                GetTargetDistribution(quantity, out kvp.Value.T1Contracts, out kvp.Value.T2Contracts, out kvp.Value.T3Contracts, out kvp.Value.T4Contracts, out kvp.Value.T5Contracts);
                            }
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
        {
            if (e == null || e.Order == null) return;

            Order order = e.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                order.OrderState != OrderState.Unknown && order.OrderState != OrderState.Filled)
            {
                return;
            }

            // V12.1101E [TM-01]: Marshal broker-thread callback to strategy thread before mutating strategy state.
            _accountOrderQueue.Enqueue(new QueuedAccountOrderUpdate
            {
                Account = sender as Account,
                EventArgs = e
            });
            try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        // Build 935 [R-02]: Cap per-drain budget to prevent strategy-thread starvation
        // under high-velocity broker event bursts. Mirrors IpcMaxCommandsPerDrain pattern.
        private const int MaxAccountOrdersPerDrain = 8;

        private void ProcessAccountOrderQueue()
        {
            // V12.Phase7 [THREAD-01a]: Buffer-and-wait during flatten (symmetric with ProcessAccountExecutionQueue).
            if (isFlattenRunning)
            {
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                return;
            }

            int drainedCount = 0;
            QueuedAccountOrderUpdate item;
            while (drainedCount < MaxAccountOrdersPerDrain && _accountOrderQueue.TryDequeue(out item))
            {
                if (isFlattenRunning)
                {
                    _accountOrderQueue.Enqueue(item);
                    try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                    return;
                }
                drainedCount++;
                ProcessQueuedAccountOrder(item);
            }
            // If items remain after budget exhausted, reschedule for next strategy-thread slice.
            if (!_accountOrderQueue.IsEmpty)
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        // Build 935 [R-01]: Returns true if 'order' belongs to 'entryKey' position.
        // Encapsulates the 7-way compound OR so the outer search loop stays trivial.
        private bool TryFindOrderInPosition(Order order, string entryKey, out string matchedEntry)
        {
            matchedEntry = null;
            if ((entryOrders.TryGetValue(entryKey,   out var eOrder)  && (eOrder  == order || (eOrder  != null && eOrder.OrderId  == order.OrderId))) ||
                (stopOrders.TryGetValue(entryKey,    out var sOrder)  && (sOrder  == order || (sOrder  != null && sOrder.OrderId  == order.OrderId))) ||
                (target1Orders.TryGetValue(entryKey, out var t1Order) && (t1Order == order || (t1Order != null && t1Order.OrderId == order.OrderId))) ||
                (target2Orders.TryGetValue(entryKey, out var t2Order) && (t2Order != null && t2Order.OrderId == order.OrderId)) ||
                (target3Orders.TryGetValue(entryKey, out var t3Order) && (t3Order != null && t3Order.OrderId == order.OrderId)) ||
                (target4Orders.TryGetValue(entryKey, out var t4Order) && (t4Order != null && t4Order.OrderId == order.OrderId)) ||
                (target5Orders.TryGetValue(entryKey, out var t5Order) && (t5Order != null && t5Order.OrderId == order.OrderId)))
            {
                matchedEntry = entryKey;
                return true;
            }
            return false;
        }

        // Build 935 [R-01]: Handles a follower order positively matched to an active position.
        // Entry-not-filled -> rollback + desync label. Entry-filled or stop/target -> ghost log + cleanup.
        private void HandleMatchedFollowerOrder(string matchedEntry, PositionInfo matchedPos, Order order, string acctName, string reason)
        {
            bool isCancelLike = reason == "CANCELLED" || reason == "REJECTED";
            bool isEntryCancelLike = isCancelLike || reason == "UNKNOWN";
            bool isFilled = reason == "FILLED";

            if (isEntryCancelLike &&
                entryOrders.TryGetValue(matchedEntry, out var entryOrder) &&
                (entryOrder == order || (entryOrder != null && entryOrder.OrderId == order.OrderId)) &&
                !matchedPos.EntryFilled)
            {
                entryOrders.TryRemove(matchedEntry, out _);
                int gfExp = 0;
                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(acctName), out gfExp); }
                if (gfExp == 0)
                {
                    // Build 947: clean up any in-flight FSM spec to avoid orphaned state
                    _followerReplaceSpecs.TryRemove(matchedEntry, out _);
                    return;
                }

                // Build 947 FSM: if this cancel was our PendingCancel, submit replacement instead of DESYNC
                FollowerReplaceSpec fsm;
                if (_followerReplaceSpecs.TryGetValue(matchedEntry, out fsm)
                    && fsm.State == FollowerReplaceState.PendingCancel
                    && fsm.CancellingOrderId == order.OrderId)
                {
                    // Fill-during-gap guard: if master already has a live filled position, let REAPER handle
                    PositionInfo masterPos;
                    bool masterFilled = !string.IsNullOrEmpty(fsm.MasterSignalName)
                        && activePositions.TryGetValue(fsm.MasterSignalName, out masterPos)
                        && masterPos != null
                        && masterPos.EntryFilled
                        && masterPos.RemainingContracts > 0;

                    if (masterFilled)
                    {
                        Print("[FSM] Master filled during cancel wait -- routing "
                            + fsm.SignalName + " to repair instead of replace.");
                        _followerReplaceSpecs.TryRemove(fsm.SignalName, out _);
                        return;
                    }

                    // Snapshot latest spec values, transition to Submitting, schedule on strategy thread
                    int    qty      = fsm.PendingQty;
                    double price    = fsm.PendingPrice;
                    string acctNameCapture = fsm.AccountName;
                    string sigName  = fsm.SignalName;
                    FollowerReplaceSpec fsmCapture = fsm;
                    fsm.State = FollowerReplaceState.Submitting;

                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }
                    return; // FSM-controlled cancel -- not a real desync
                }

                Print(string.Format("[SIMA] Follower entry cancelled: {0} on {1}. Reaper monitoring.", matchedEntry, acctName));
                Draw.TextFixed(this, "SIMA_DESYNC_" + acctName, "(!) FOLLOWER DESYNC: " + acctName, TextPosition.TopLeft, Brushes.Red, new SimpleFont("Arial", 11), Brushes.Transparent, Brushes.Transparent, 50);
            }
            else
            {
                // Build 951.1 Bug-B: Bracket replace FSM gate for follower bracket-leg cancel confirms.
                // Follower orders arrive via OnAccountOrderUpdate -> this method, NOT HandleOrderCancelled.
                // Without this block RemainingCancels never reaches zero and SubmitBracketReplacement never fires.
                // Note: TryRemove responsibility belongs exclusively to SubmitBracketReplacement.
                // Build 951.2: REJECTED is also terminal -- the leg is gone regardless of why.
                // Build 951.3: FILLED is also terminal -- cancel-gap fill must advance the FSM.
                if (isCancelLike || isFilled)
                {
                    foreach (var kvp in _bracketReplaceSpecs.ToArray())
                    {
                        BracketReplaceSpec bSpec = kvp.Value;
                        bool bMatched;
                        bool bStopFilled = false;
                        int bTargetFilledNum = 0;
                        int bFilledQty = 0;
                        lock (bSpec.SyncRoot)
                        {
                            bMatched = bSpec.PendingOrderIds.Remove(order.OrderId);
                            if (bMatched && isFilled)
                            {
                                bSpec.GapFillDetected = true;
                                bFilledQty = Math.Max(0, order.Filled > 0 ? order.Filled : order.Quantity);
                                if ((order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_")))
                                {
                                    bSpec.StopPrice = 0;
                                    bSpec.StopQty = 0;
                                    bStopFilled = true;
                                }
                                else if (order.Name.StartsWith("T1_") || order.Name.StartsWith("T2_") || order.Name.StartsWith("T3_") ||
                                         order.Name.StartsWith("T4_") || order.Name.StartsWith("T5_"))
                                {
                                    bTargetFilledNum = order.Name[1] - '0';
                                    if (bTargetFilledNum >= 1 && bTargetFilledNum <= 5)
                                    {
                                        bSpec.TargetPrices[bTargetFilledNum - 1] = 0;
                                        bSpec.TargetQtys[bTargetFilledNum - 1] = 0;
                                    }
                                }
                            }
                        }
                        if (bMatched)
                        {
                            if (isFilled && bTargetFilledNum >= 1 && bTargetFilledNum <= 5)
                            {
                                PositionInfo bPos;
                                if (activePositions.TryGetValue(kvp.Key, out bPos) && bPos != null)
                                {
                                    bool alreadyProcessed;
                                    int appliedQty;
                                    int remainingAfter;
                                    ApplyTargetFill(bPos, bTargetFilledNum, bFilledQty, true,
                                        out alreadyProcessed, out appliedQty, out remainingAfter);
                                    if (!alreadyProcessed)
                                    {
                                        Print(string.Format(
                                            "[MOVE-SYNC] FSM: Gap fill accounted Bracket_{0} T{1} ({2} contracts). Remaining={3}",
                                            kvp.Key, bTargetFilledNum, appliedQty, remainingAfter));
                                    }
                                }
                                RemoveTargetReferenceOnTerminalFill(order);
                            }
                            else if (isFilled && bStopFilled)
                            {
                                stopOrders.TryRemove(kvp.Key, out _);
                                lock (bSpec.SyncRoot) { bSpec.PositionClosed = true; }  // Build 951.5: stop filled = position flat
                                Print(string.Format("[MOVE-SYNC] FSM: Gap fill accounted Bracket_{0} stop leg.", kvp.Key));
                            }

                            int bRemaining = Interlocked.Decrement(ref bSpec.RemainingCancels);
                            Print(string.Format("[MOVE-SYNC] FSM: Follower terminal confirmed Bracket_{0} ({1}) -- {2} legs remaining",
                                kvp.Key, reason, bRemaining));
                            if (bRemaining <= 0)
                            {
                                BracketReplaceSpec capturedBSpec = bSpec;
                                TriggerCustomEvent(o => SubmitBracketReplacement(capturedBSpec), null);
                            }
                            return; // FSM-controlled cancel -- not a desync event
                        }
                    }
                }

                // Build 950: Follower stop replacement -- mirrors HandleOrderCancelled master path.
                // Follower stop cancels arrive via OnAccountOrderUpdate (not OnOrderUpdate), so
                // HandleOrderCancelled never fires for them. Match pendingStopReplacements here.
                // This block is in the else branch because stop orders are not in entryOrders.
                // Build 951.2: Restrict legacy stop rescue to CANCELLED only.
                // REJECTED stops may indicate a broker-side condition -- do not double-submit.
                // Also skip if a bracket FSM spec is in-flight for this entry (it owns the resubmit).
                if (reason == "CANCELLED"
                    && (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_")))
                {
                    foreach (var _psr in pendingStopReplacements.ToArray())
                    {
                        if (_psr.Value.OldOrder == order)
                        {
                            // Build 951.2: Skip if bracket FSM already handling this entry.
                            if (_bracketReplaceSpecs.ContainsKey(_psr.Key))
                            {
                                Print(string.Format("[MOVE-SYNC] Skipping legacy rescue for {0} -- bracket FSM in-flight.", _psr.Key));
                                if (pendingStopReplacements.TryRemove(_psr.Key, out _))
                                    Interlocked.Decrement(ref pendingReplacementCount);
                                return;
                            }
                            PositionInfo _rPos;
                            if (activePositions.TryGetValue(_psr.Key, out _rPos) && _rPos.RemainingContracts > 0)
                            {
                                int _rQty;
                                lock (stateLock) { _rQty = _rPos.RemainingContracts; }
                                CreateNewStopOrder(_psr.Key, _rQty, _psr.Value.StopPrice, _psr.Value.Direction);
                                if (_psr.Value.BracketRestorationNeeded && _psr.Value.CapturedTargets != null)
                                {
                                    TargetSnapshot[] _snap = _psr.Value.CapturedTargets;
                                    string _rKey = _psr.Key;
                                    TriggerCustomEvent(o => RestoreCascadedTargets(_rKey, _snap), null);
                                }
                            }
                            if (pendingStopReplacements.TryRemove(_psr.Key, out _))
                                Interlocked.Decrement(ref pendingReplacementCount);
                            return;
                        }
                    }
                }
                Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, order.OrderId));
                RemoveGhostOrderRef(order, reason);
            }
        }

        // Build 935 [R-01]: SIMA cascade cleanup for unmatched master-cancel events.
        // Receives pre-computed snapshot -- eliminates the second activePositions.ToArray() allocation.
        private void ExecuteFollowerCascadeCleanup(bool enableSima, Order order, string reason, KeyValuePair<string, PositionInfo>[] snapshot)
        {
            // V12.18 SIMA CASCADE: If a master-account order was cancelled,
            // check if any follower positions share the same base signal and tear them down.
            if (enableSima && order.OrderState == OrderState.Cancelled && order.Account == this.Account)
            {
                string orderSignal = order.Name;
                foreach (var kvp in snapshot)
                {
                    PositionInfo cascadePos = kvp.Value;
                    if (!cascadePos.IsFollower) continue;
                    if (kvp.Key.Contains(orderSignal) || orderSignal.Contains(kvp.Key))
                    {
                        string cascadeAcctName = cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL";
                        if (!cascadePos.EntryFilled)
                        {
                            Print(string.Format("[GHOST_FIX] SIMA CASCADE: Master cancel of {0} triggers follower teardown for {1} on {2}",
                                orderSignal, kvp.Key, cascadeAcctName));
                            CleanupPosition(kvp.Key);

                            if (cascadePos.ExecutingAccount != null)
                            {
                                int rollbackDelta = (cascadePos.Direction == MarketPosition.Long) ? -cascadePos.TotalContracts : cascadePos.TotalContracts;
                                int currentExp = 0;
                                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(cascadeAcctName), out currentExp); }
                                if (currentExp == 0)
                                {
                                    Print(string.Format("[GHOST_FIX] SKIP cascade delta for {0}: expectedPositions already 0 (purge-race guard). Delta suppressed.",
                                        cascadeAcctName));
                                }
                                else
                                {
                                    DeltaExpectedPositionLocked(ExpKey(cascadeAcctName), rollbackDelta);
                                }
                                ClearDispatchSyncPending(ExpKey(cascadeAcctName));
                                try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); } catch { }
                            }
                        }
                        else
                        {
                            Print(string.Format("[DEAD-01] CASCADE-FILLED: Master cancel {0} -- follower {1} on {2} is FILLED. Issuing emergency flatten.",
                                orderSignal, kvp.Key, cascadeAcctName));
                            if (cascadePos.ExecutingAccount != null)
                            {
                                Account filledFollowerAcct = cascadePos.ExecutingAccount;
                                TriggerCustomEvent(o => EmergencyFlattenSingleFleetAccount(filledFollowerAcct), null);
                            }
                        }
                    }
                }
            }
            RemoveGhostOrderRef(order, reason);
        }

        private void ProcessQueuedAccountOrder(QueuedAccountOrderUpdate item)
        {
            if (item.EventArgs == null || item.EventArgs.Order == null) return;
            Order order = item.EventArgs.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            string reason = order.OrderState.ToString().ToUpper();
            string acctName = item.Account != null ? item.Account.Name : "UNKNOWN";
            Print(string.Format("[GHOST-AUDIT] OnAccountOrderUpdate: {0} | State={1} | Acct={2}", order.Name, reason, acctName));

            // Build 935 [R-01]: Single snapshot -- reused by both identity search and cascade cleanup,
            // eliminating the second activePositions.ToArray() allocation in the cascade path.
            var snapshot = activePositions.ToArray();

            string matchedEntry = null;
            PositionInfo matchedPos = null;
            foreach (var kvp in snapshot)
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                PositionInfo pos = kvp.Value;
                if (!pos.IsFollower || pos.ExecutingAccount == null || pos.ExecutingAccount != item.Account) continue;
                if (TryFindOrderInPosition(order, kvp.Key, out matchedEntry))
                {
                    matchedPos = pos;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(matchedEntry) && matchedPos != null && activePositions.ContainsKey(matchedEntry))
                HandleMatchedFollowerOrder(matchedEntry, matchedPos, order, acctName, reason);
            else
                ExecuteFollowerCascadeCleanup(EnableSIMA, order, reason, snapshot);
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                if (marketPosition == MarketPosition.Flat)
                    HandleFlatPositionUpdate(position);

                BroadcastSyncTargetState();
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
            }
        }

        // Build 935 [CB-B935-001]: Flat-position cleanup extracted from OnPositionUpdate.
        private void HandleFlatPositionUpdate(Position position)
        {
            // [H-14]: Sync expectedPositions on flat. Build 931: guard against spurious flat.
            string flatAcctName = position?.Account?.Name;
            if (!string.IsNullOrEmpty(flatAcctName))
            {
                string flatExpKey = ExpKey(flatAcctName);
                bool hasSyncPending = IsDispatchSyncPending(flatExpKey);
                bool hasPendingEntry = false;
                foreach (var kvp in entryOrders.ToArray())
                {
                    var ord = kvp.Value;
                    if (ord != null
                        && !IsOrderTerminal(ord.OrderState)
                        && activePositions.TryGetValue(kvp.Key, out var pos)
                        && pos.ExecutingAccount != null
                        && pos.ExecutingAccount.Name == flatAcctName)
                    {
                        hasPendingEntry = true;
                        break;
                    }
                }

                bool hasActivePositionForAcct = false;
                if (!hasPendingEntry)
                {
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (kvp.Value.ExecutingAccount != null
                            && kvp.Value.ExecutingAccount.Name == flatAcctName
                            && !kvp.Value.EntryFilled)
                        {
                            hasActivePositionForAcct = true;
                            break;
                        }
                    }
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

            // V8.22: Scan for orphans even if activePositions is empty (strategy restart)
            if (activePositions.Count == 0)
            {
                Print("EXTERNAL CLOSE/RESTART DETECTED - Scanning for orphaned bracket orders...");
                ReconcileOrphanedOrders("Position went flat");
                return;
            }

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
                            CancelOrder(stopOrder);
                    }
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var tDict = GetTargetOrdersDictionary(tNum);
                        if (tDict != null && tDict.TryGetValue(kvp.Key, out var tOrder))
                        {
                            if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                                CancelOrder(tOrder);
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

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (execution == null || execution.Order == null) return;

                string orderName = execution.Order.Name;
                if (string.IsNullOrEmpty(orderName)) return;

                // V12.Phase7 [C-01]: Dedup guard -- prevent double-decrement if OnOrderUpdate + OnExecutionUpdate both fire for same fill.
                // CRITICAL FIX: Use stateLock (same lock as ApplyTargetFill/OnOrderUpdate) to ensure mutual exclusion.
                // Previously used separate executionDeduplicateLock which allowed both threads to proceed concurrently.
                if (!string.IsNullOrEmpty(executionId))
                {
                    lock (stateLock)
                    {
                        if (!processedExecutionIds.Add(executionId))
                        {
                            Print(string.Format("[DEDUP] Skipping duplicate execution {0} for {1}", executionId, orderName));
                            return;
                        }
                        // Bounded pruning: keep at most MaxProcessedExecutionIds entries
                        processedExecutionIdQueue.Enqueue(executionId);
                        while (processedExecutionIdQueue.Count > MaxProcessedExecutionIds)
                            processedExecutionIds.Remove(processedExecutionIdQueue.Dequeue());
                    }
                }
                else
                {
                    // V12.1101E [F-08]: Fallback dedup key when executionId is missing: (Order, FilledQuantity).
                    // Uses runtime order object identity + cumulative filled quantity.
                    int dedupOrderIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(execution.Order);
                    int dedupFilledQuantity = execution.Order.Filled > 0 ? execution.Order.Filled : Math.Max(0, quantity);
                    string fallbackKey = string.Format("{0}|{1}", dedupOrderIdentity, dedupFilledQuantity);

                    lock (stateLock)
                    {
                        if (!processedExecutionFallbackKeys.Add(fallbackKey))
                        {
                            Print(string.Format("[DEDUP] Skipping duplicate fallback execution {0} for {1}", fallbackKey, orderName));
                            return;
                        }
                        processedExecutionFallbackQueue.Enqueue(fallbackKey);
                        while (processedExecutionFallbackQueue.Count > MaxProcessedExecutionIds)
                            processedExecutionFallbackKeys.Remove(processedExecutionFallbackQueue.Dequeue());
                    }
                }

                // V12.12: Compliance tracking for single-account mode
                // [939-P0]: Marshal Account.Get() off broker thread via TriggerCustomEvent.
                if (EnableComplianceHub && !EnableSIMA)
                {
                    TrackTradeEntry(Account, execution);
                    TriggerCustomEvent(o => UpdateAccountMetricsFromAccount(Account), null);
                    LogApexPerformance();
                }

                // Helper: Extract entry name from order name (removes prefix and optional timestamp suffix)
                Func<string, string, string> extractEntryName = (name, prefix) =>
                {
                    if (!name.StartsWith(prefix)) return "";
                    string entryPart = name.Substring(prefix.Length);
                    // Strip timestamp suffix if present (format: _123456789012345)
                    int lastUnderscore = entryPart.LastIndexOf('_');
                    if (lastUnderscore > 0 && entryPart.Length - lastUnderscore > 10)
                        entryPart = entryPart.Substring(0, lastUnderscore);
                    return entryPart;
                };

                // ============================================================
                // 1. STOP LOSS FILL - Manual OCO: Cancel all remaining targets
                // ============================================================
                if (orderName.StartsWith("Stop_"))
                {
                    string entryName = extractEntryName(orderName, "Stop_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int remainingAfterStop;
                        lock (stateLock)
                        {
                            pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                            remainingAfterStop = pos.RemainingContracts;
                        }

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
                                    CancelOrder(tOrder);
                                    cancelledTargets++;
                                }
                            }
                        }

                        if (cancelledTargets > 0)
                        {
                            Print(string.Format("OCO: Cancelled {0} target orders for {1}", cancelledTargets, entryName));
                        }

                        // Remove stop order reference
                        stopOrders.TryRemove(entryName, out _);

                        // Clean up pending replacements if any
                        if (pendingStopReplacements.TryRemove(entryName, out _))
                        {
                            Interlocked.Decrement(ref pendingReplacementCount);
                        }

                        // If position is fully closed, remove from activePositions
                        if (remainingAfterStop <= 0)
                        {
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                            entryOrders.TryRemove(entryName, out _);
                            Print(string.Format("Position {0} fully closed by stop.", entryName));
                        }
                    }
                }

                // ============================================================
                // 2. TARGET 1-5 FILL - Reduce stop quantity (unified loop)
                // V12.1101E [SK-01/A-1]: First-Writer-Wins guard prevents double-decrement.
                // ============================================================
                else if (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") ||
                         orderName.StartsWith("T4_") || orderName.StartsWith("T5_"))
                {
                    // Extract target number from prefix (T1_, T2_, etc.)
                    int targetNum = orderName[1] - '0';
                    string targetPrefix = "T" + targetNum + "_";
                    string entryName = extractEntryName(orderName, targetPrefix);

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
                            RequestStopCancelLifecycleSafe(entryName);
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }

                        // V12.1101E [F-07]: Clear target ref only after broker confirms Filled.
                        if (terminalFill)
                        {
                            var tDict = GetTargetOrdersDictionary(targetNum);
                            if (tDict != null) tDict.TryRemove(entryName, out _);
                        }
                    }
                }

                // ============================================================
                // 5. TRIM EXECUTION - V10.3.1: Enhanced Stop Integrity
                // ============================================================
                // ??"? CRITICAL: When a TRIM executes, we MUST reduce the stop order quantity
                // to match the new position size. If we don't, hitting the stop after a trim
                // would close more contracts than we hold, creating an unintended REVERSE position.
                // Example: Long 4 contracts, stop at 4. Trim 2 (now Long 2). If stop stays at 4,
                // getting stopped out would SELL 4 (close 2 + go SHORT 2) = DISASTER.
                else if (orderName.StartsWith("Trim_"))
                {
                    string entryName = extractEntryName(orderName, "Trim_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int previousQty;
                        int remainingAfterTrim;
                        lock (stateLock)
                        {
                            previousQty = pos.RemainingContracts;
                            pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                            remainingAfterTrim = pos.RemainingContracts;
                        }

                        Print(string.Format("TRIM EXECUTION: {0} contracts closed for {1}. Position: {2} ??' {3}",
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
                            RequestStopCancelLifecycleSafe(entryName);

                            // Also clean up any pending replacements
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }

                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("Error OnExecutionUpdate: " + ex.Message);
            }
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
        private void PropagateMasterPriceMove(Order masterOrder, double newLimit, double newStop, int newMasterQty = 0)
        {
            if (!EnableSIMA || masterOrder == null || masterOrder.Account != this.Account) return;

            // [BUILD 924 -- Fix C] Raise propagation flag before dispatch; finally block clears it.
            _propagationActive = true;
            try
            {

            // --- Step 1: Identify master position and move type via object identity ---
            string masterEntryName = null;
            bool isEntryMove  = false;
            bool isStopMove   = false;
            bool isTargetMove = false;
            int  masterTargetNum = 0;

            foreach (var kvp in entryOrders)
            {
                if (kvp.Value == masterOrder &&
                    activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                {
                    masterEntryName = kvp.Key;
                    isEntryMove = true;
                    break;
                }
            }

            if (masterEntryName == null)
            {
                foreach (var kvp in stopOrders)
                {
                    if (kvp.Value == masterOrder &&
                        activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                    {
                        masterEntryName = kvp.Key;
                        isStopMove = true;
                        break;
                    }
                }
            }

            if (masterEntryName == null)
            {
                for (int t = 1; t <= 5 && masterEntryName == null; t++)
                {
                    var tDict = GetTargetOrdersDictionary(t);
                    if (tDict == null) continue;
                    foreach (var kvp in tDict)
                    {
                        if (kvp.Value == masterOrder &&
                            activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                        {
                            masterEntryName  = kvp.Key;
                            isTargetMove     = true;
                            masterTargetNum  = t;
                            break;
                        }
                    }
                }
            }

            if (masterEntryName == null) return; // Not a tracked master order

            // --- Step 2: Resolve follower entry names via Symmetry dispatch context ---

            // [BUILD 926 -- Codex P1 Fix]: Derive master TradeType from boolean flags.
            // Master boolean flags ARE accurate (master positions set IsTRENDTrade, IsRMATrade etc. correctly).
            // Only FOLLOWER flags are contaminated (IsRMATrade=true on ALL followers for trailing behavior).
            // Follower type discrimination uses SignalName parsing instead -- see fallback scan below.
            string masterTradeType = null;
            if (activePositions.TryGetValue(masterEntryName, out var masterPosForType))
            {
                // [BUILD 928 -- Codex P2 Fix]: IsRetestTrade MUST be checked before IsRMATrade.
                // RETEST positions set both IsRetestTrade=true AND IsRMATrade=true (uses RMA trailing).
                // Old order checked IsRMATrade first -> RETEST master classified as "RMA" -> fallback
                // propagation targets RMA followers and silently skips RETEST followers.
                if      (masterPosForType.IsTRENDTrade)  masterTradeType = "TREND";
                else if (masterPosForType.IsRetestTrade) masterTradeType = "RETEST"; // <- before RMA
                else if (masterPosForType.IsRMATrade)    masterTradeType = "RMA";
                else                                     masterTradeType = "RMA";
            }

            IEnumerable<string> followerEntryNames;
            if (symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                string[] snapshot;
                lock (ctx.Sync) { snapshot = ctx.FollowerEntries.ToArray(); }
                followerEntryNames = snapshot;
            }
            else
            {
                // [BUILD 926 -- Codex P1 Fix]: Fallback type match now uses SignalName parsing.
                //
                // ROOT CAUSE: IsRMATrade=true is stamped on ALL fleet followers (ExecuteSmartDispatchEntry
                // line 434) to enforce point-based trailing. Using IsRMATrade as a type discriminator
                // caused OR followers to fail the !IsRMATrade predicate and be excluded from OR
                // propagation, and incorrectly included in RMA propagation.
                //
                // FIX: Fleet entry names are stamped with the trade type at dispatch time:
                //   Format: "Fleet_<AccountName>_<TRADETYPE>_<Index>"
                //   Example: "Fleet_PA-APEX-422136-05_OR_0", "Fleet_APEX-09_RMA_1"
                //
                // [BUILD 927 -- Codex P2 Fix]: Do NOT use Contains("_TYPE_") -- if an account name
                // itself contains a trade-type substring (e.g. _RMA_, _OR_), Contains() misclassifies
                // the follower by matching the account name token instead of the TRADETYPE segment.
                //
                // SAFE APPROACH: Extract TRADETYPE by segment position.
                // TRADETYPE is always the second-to-last underscore-delimited segment:
                //   lastUnderscore      = before the numeric Index
                //   secondLastUnderscore = before the TRADETYPE token
                // Example: "Fleet_SimApexSim_02_OR_0"
                //   lastUs  -> before "0"    -> remaining = "Fleet_SimApexSim_02_OR"
                //   typeUs  -> before "OR"   -> extracted = "OR"  ?
                var fallback = new List<string>();
                foreach (var kvp in activePositions)
                {
                    if (!kvp.Value.IsFollower || kvp.Value.ExecutingAccount == null) continue;
                    if (masterTradeType == null)
                    {
                        fallback.Add(kvp.Key);
                        continue;
                    }

                    // --- Segment-position extraction ---
                    string sig = kvp.Value.SignalName ?? kvp.Key;
                    string followerType = null;
                    int lastUs = sig.LastIndexOf('_');
                    if (lastUs > 0)
                    {
                        int typeUs = sig.LastIndexOf('_', lastUs - 1);
                        if (typeUs >= 0)
                        {
                            string extracted = sig.Substring(typeUs + 1, lastUs - typeUs - 1);
                            // Validate against known set -- rejects garbage from unusual account names
                            if (extracted == "OR"     || extracted == "RMA"  ||
                                extracted == "TREND"  || extracted == "RETEST" ||
                                extracted == "MOMO"   || extracted == "FFMA"  ||
                                // Build 930 Fix P2: Suffix-marker support -- FFMA_MNL, FFMA_MNL_MKT, OR_RETEST etc.
                                extracted.StartsWith("FFMA_") || extracted.StartsWith("MOMO_") ||
                                extracted.StartsWith("OR_")   || extracted.StartsWith("RMA_")  ||
                                extracted.StartsWith("TREND_") || extracted.StartsWith("RETEST_"))
                                followerType = extracted.Split('_')[0]; // normalize to base type
                        }
                    }

                    // Fallback: segment parsing failed -- use boolean flags
                    if (followerType == null)
                    {
                        if      (kvp.Value.IsTRENDTrade)  followerType = "TREND";
                        else if (kvp.Value.IsRetestTrade)  followerType = "RETEST";
                        else                               followerType = "RMA";
                    }

                    if (followerType == masterTradeType)
                        fallback.Add(kvp.Key);
                }
                followerEntryNames = fallback;
            }

            // --- Step 3: Apply move to each linked follower ---
            foreach (string fleetEntryName in followerEntryNames)
            {
                if (!activePositions.TryGetValue(fleetEntryName, out var pos)) continue;
                if (!pos.IsFollower || pos.ExecutingAccount == null) continue;

                if (isEntryMove)
                {
                    // [FIX-PM-02]: For StopMarket/StopLimit entries limitPrice=0 always; price lives in stopPrice.
                    // Passing newLimit=0 to PropagateMasterEntryMove caused the tick guard to silently no-op
                    // on every user-drag, and historically resubmitted Limit followers at price 0.
                    double effectiveEntryPrice = newLimit > 0 ? newLimit : newStop;
                    if (effectiveEntryPrice <= 0) continue; // both zero -- NT8 callback race, skip safely
                    PropagateMasterEntryMove(fleetEntryName, pos, effectiveEntryPrice, newMasterQty);
                }
                else if (isStopMove)
                    PropagateMasterStopMove(fleetEntryName, pos, newStop);
                else if (isTargetMove)
                    PropagateMasterTargetMove(fleetEntryName, pos, masterTargetNum, newLimit);
            }
            } // end try
            finally
            {
                // [BUILD 924 -- Fix C] Always clear propagation flag, even on exception.
                _propagationActive = false;
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master stop price move to follower.
        /// Delegates to UpdateStopOrder which uses cancel/resubmit via follower Account API
        /// (per V12.10 pattern -- ChangeOrder is master-local only).
        /// </summary>
        private void PropagateMasterStopMove(string fleetEntryName, PositionInfo pos, double newStop)
        {
            if (newStop <= 0) return;
            // [FIX-PM-03]: Skip stop propagation for followers whose entry hasn't filled yet.
            if (!pos.EntryFilled) return;
            double roundedStop = Instrument.MasterInstrument.RoundToTickSize(newStop);
            if (Math.Abs(pos.CurrentStopPrice - roundedStop) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] Stop move: {0} on {1}: {2:F2} -> {3:F2}",
                fleetEntryName,
                pos.ExecutingAccount != null ? pos.ExecutingAccount.Name : "local",
                pos.CurrentStopPrice, roundedStop));

            // Build 951: SIMA followers use unified bracket replace (shared OCO ID across all legs).
            // Local/master path continues via UpdateStopOrder (unmanaged orders, different OCO rules).
            if (pos.IsFollower && pos.ExecutingAccount != null)
            {
                // Build 951.1 Bug-C1: Do NOT update pos.CurrentStopPrice here for followers.
                // If the FSM cancel or submit fails, CurrentStopPrice would be stuck at the new
                // price, causing the duplicate-move guard to silently drop all future stop moves.
                // pos.CurrentStopPrice is committed in SubmitBracketReplacement after Submit() succeeds.
                InitiateBracketReplace(fleetEntryName, pos, roundedStop, 0, 0);
            }
            else
            {
                // Non-follower (master/local): update immediately -- UpdateStopOrder is synchronous.
                pos.CurrentStopPrice = roundedStop;
                UpdateStopOrder(fleetEntryName, pos, roundedStop, pos.CurrentTrailLevel);
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master target price move to follower via cancel+resubmit.
        /// Mirrors SymmetryGuardReplaceExistingFollowerTarget (Symmetry.cs:504) pattern.
        /// </summary>
        private void PropagateMasterTargetMove(string fleetEntryName, PositionInfo pos, int targetNum, double newLimit)
        {
            if (newLimit <= 0) return;
            var targetDict = GetTargetOrdersDictionary(targetNum);
            if (targetDict == null) return;

            // Build 951.5 [P2]: Check FSM in-flight BEFORE order-state gate.
            // During the cancel-gap the order won't be Working/Accepted -- the spec owns the price.
            BracketReplaceSpec inFlightSpec;
            if (_bracketReplaceSpecs.TryGetValue(fleetEntryName, out inFlightSpec))
            {
                lock (inFlightSpec.SyncRoot)
                {
                    inFlightSpec.TargetPrices[targetNum - 1] =
                        Instrument.MasterInstrument.RoundToTickSize(newLimit);
                }
                Print(string.Format("[MOVE-SYNC] T{0} move absorbed into in-flight FSM for {1}",
                    targetNum, fleetEntryName));
                return;
            }

            if (!targetDict.TryGetValue(fleetEntryName, out var tOrder) || tOrder == null) return;
            if (tOrder.OrderState != OrderState.Working && tOrder.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            if (Math.Abs(tOrder.LimitPrice - roundedLimit) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] T{0} move: {1} on {2}: {3:F2} -> {4:F2}",
                targetNum, fleetEntryName, pos.ExecutingAccount.Name, tOrder.LimitPrice, roundedLimit));

            // Build 951: SIMA followers use unified bracket replace -- maintains shared OCO ID.
            if (pos.IsFollower && pos.ExecutingAccount != null)
            {
                InitiateBracketReplace(fleetEntryName, pos, 0, targetNum, roundedLimit);
                return;
            }

            // Non-follower (local) path: original direct cancel+submit (unmanaged, no shared OCO group).
            try
            {
                pos.ExecutingAccount.Cancel(new[] { tOrder });

                int qty = tOrder.Quantity;
                OrderAction exitAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Sell : OrderAction.BuyToCover;
                string signalName = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);

                Order replacement = pos.ExecutingAccount.CreateOrder(
                    Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    qty, roundedLimit, 0,
                    "MGT_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    signalName, null);

                pos.ExecutingAccount.Submit(new[] { replacement });
                targetDict[fleetEntryName] = replacement;

                Print(string.Format("[MOVE-SYNC] T{0} resubmitted (local): {1} @ {2:F2}",
                    targetNum, fleetEntryName, roundedLimit));
            }
            catch (Exception ex)
            {
                Print(string.Format("[MOVE-SYNC] ERROR PropagateMasterTargetMove T{0} {1}: {2}",
                    targetNum, fleetEntryName, ex.Message));
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC / 1102Z-D: Propagate master entry price move to follower (pre-fill orders).
        /// Account.Change() removed -- it completes silently on Apex/Tradovate but is a broker-side no-op.
        /// Cancel + CreateOrder + Submit is the sole path, consistent with PropagateMasterTargetMove
        /// and UpdateStopOrder throughout this codebase.
        /// StampReaperMoveGrace() is called before Cancel to open a 5-second REAPER suppression window
        /// covering the cancel gap. REAPER's ChangePending guard (AuditApexPositions line 193) provides
        /// a second layer of protection.
        /// </summary>
        private void PropagateMasterEntryMove(string fleetEntryName, PositionInfo pos, double newLimit, int newMasterQty = 0)
        {
            if (!entryOrders.TryGetValue(fleetEntryName, out var fEntry) || fEntry == null) return;
            if (fEntry.OrderState != OrderState.Working && fEntry.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            // [FIX-PM-02b]: For StopMarket/StopLimit orders price lives in StopPrice (LimitPrice is always 0).
            bool isStopTypeEntry = fEntry.OrderType == OrderType.StopMarket || fEntry.OrderType == OrderType.StopLimit;
            double fEffectivePrice = isStopTypeEntry ? fEntry.StopPrice : fEntry.LimitPrice;

            // [QTY-SYNC]: Scale master quantity for this follower.
            // Fallback to fEntry.Quantity if no quantity signal (pure price-change callback, or qty=0 noise).
            // [923A-P2a-OVF]: checked{} forces explicit OverflowException vs silent int truncation on extreme parity ratios
            // (e.g., 1 NQ -> 10 MES with very high master qty). Clamps to maxContracts on overflow.
            int scaledQty;
            try
            {
                scaledQty = (newMasterQty > 0 && FleetParityMultiplier > 0)
                    ? checked((int)Math.Max(1L, (long)newMasterQty * FleetParityMultiplier))  // [922Z-OVF+923A]: long cast + checked int
                    : fEntry.Quantity;
            }
            catch (OverflowException)
            {
                Print(string.Format("[923A-OVF] Parity scalar overflow for {0} -- clamping to maxContracts ({1})", fleetEntryName, maxContracts));
                scaledQty = maxContracts;
            }

            bool priceChanged    = Math.Abs(fEffectivePrice - roundedLimit) > tickSize / 2;
            bool quantityChanged = scaledQty != fEntry.Quantity;
            if (!priceChanged && !quantityChanged) return;

            Print(string.Format("[MOVE-SYNC] Entry move: {0} on {1}: {2:F2} -> {3:F2} x{4}",
                fleetEntryName, pos.ExecutingAccount.Name, fEffectivePrice, roundedLimit, scaledQty));

            // 1102Z-D: Stamp grace BEFORE cancel -- opens 5-second REAPER suppression window.
            StampReaperMoveGrace();

            // Build 947 FSM: derive master signal name for fill-during-gap detection.
            // Uses same key-contains pattern as cascade cleanup to find the master activePositions entry.
            string masterSignalName = string.Empty;
            foreach (var kvp in activePositions)
            {
                if (!kvp.Value.IsFollower &&
                    (fleetEntryName.Contains(kvp.Key) || kvp.Key.Contains(fleetEntryName)))
                {
                    masterSignalName = kvp.Key;
                    break;
                }
            }

            // Build 947 FSM: two-phase replace -- wait for broker cancel confirmation before resubmit.
            // [GHOST-FIX-1 Build 922Z]: identity chain (fleetEntryName = signal name) preserved in FSM.
            // [FIX-PM-02c]: order type + direction threaded through FSM spec for StopMarket and Short support.
            OrderAction entryAction = pos.Direction == MarketPosition.Long
                ? OrderAction.Buy : OrderAction.SellShort;

            PropagateFollowerEntryReplace(
                fleetEntryName, masterSignalName,
                pos.ExecutingAccount.Name, pos.ExecutingAccount,
                roundedLimit, scaledQty,
                entryAction, fEntry.OrderType, isStopTypeEntry);
        }

        // Build 947: PropagateFollowerEntryReplace -- FSM entry point for two-phase cancel+resubmit.
        // Called from PropagateMasterEntryMove instead of the old inline cancel+submit block.
        // If a replace is already in-flight (PendingCancel or Submitting), ATR ticks are absorbed
        // by updating PendingQty/PendingPrice without firing a second cancel.
        private void PropagateFollowerEntryReplace(
            string fleetEntryName, string masterSignalName,
            string accountName, Account acct,
            double newPrice, int newQty,
            OrderAction entryAction, OrderType entryOrderType, bool isStopType)
        {
            Order currentEntry = null;

            lock (stateLock)
            {
                FollowerReplaceSpec existing;
                if (_followerReplaceSpecs.TryGetValue(fleetEntryName, out existing))
                {
                    // Already in PendingCancel or Submitting -- absorb ATR tick into latest spec.
                    existing.PendingQty   = newQty;
                    existing.PendingPrice = newPrice;
                    Print("[FSM] Replace spec updated (in-flight): "
                        + fleetEntryName + " qty=" + newQty + " price=" + newPrice);
                    return;
                }

                if (!entryOrders.TryGetValue(fleetEntryName, out currentEntry) || currentEntry == null)
                {
                    Print("[FSM] SKIP replace: no tracked entry for " + fleetEntryName);
                    return;
                }

                var spec = new FollowerReplaceSpec
                {
                    State             = FollowerReplaceState.PendingCancel,
                    CancellingOrderId = currentEntry.OrderId,
                    PendingQty        = newQty,
                    PendingPrice      = newPrice,
                    AccountName       = accountName,
                    SignalName        = fleetEntryName,
                    MasterSignalName  = masterSignalName,
                    EntryAction       = entryAction,
                    EntryOrderType    = entryOrderType,
                    IsStopType        = isStopType
                };
                _followerReplaceSpecs[fleetEntryName] = spec;
            }

            // Cancel outside lock -- currentEntry captured inside lock above
            try
            {
                acct.Cancel(new[] { currentEntry });
                Print("[FSM] Cancel sent for " + fleetEntryName
                    + " OrderId=" + currentEntry.OrderId);
            }
            catch (Exception ex)
            {
                Print("[FSM] Cancel failed for " + fleetEntryName + ": " + ex.Message);
                _followerReplaceSpecs.TryRemove(fleetEntryName, out _);
            }
        }

        // Build 947: SubmitFollowerReplacement -- called on strategy thread via TriggerCustomEvent
        // after broker confirms the PendingCancel. Uses spec fields to preserve direction + order type.
        private void SubmitFollowerReplacement(
            string fleetSignalName, string accountName,
            double price, int qty, FollowerReplaceSpec spec)
        {
            Account acct = Account.All.FirstOrDefault(
                a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
            if (acct == null)
            {
                Print("[FSM] SUBMIT FAIL: account not found: " + accountName);
                return;
            }

            // [FIX-PM-02c]: preserve order type so StopMarket followers remain StopMarket.
            double limitPx = !spec.IsStopType ? price : 0;
            double stopPx  =  spec.IsStopType ? price : 0;

            // [923A-P1-GUID]: 8-char GUID fragment as ocoId; signal name = fleetSignalName (GHOST-FIX-1).
            Order newEntry = acct.CreateOrder(
                Instrument, spec.EntryAction, spec.EntryOrderType, TimeInForce.Gtc,
                qty, limitPx, stopPx,
                "MGE_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                fleetSignalName, null);
            acct.Submit(new[] { newEntry });

            lock (stateLock)
            {
                entryOrders[fleetSignalName] = newEntry;

                // [QTY-SYNC]: Sync PositionInfo to new size so SubmitBracketOrders sum-assertion passes.
                PositionInfo pos;
                if (activePositions.TryGetValue(fleetSignalName, out pos) && pos != null)
                {
                    pos.TotalContracts     = qty;
                    pos.RemainingContracts = qty;
                    int ft1, ft2, ft3, ft4, ft5;
                    GetTargetDistribution(qty, out ft1, out ft2, out ft3, out ft4, out ft5);
                    pos.T1Contracts = ft1;
                    pos.T2Contracts = ft2;
                    pos.T3Contracts = ft3;
                    pos.T4Contracts = ft4;
                    pos.T5Contracts = ft5;
                }
            }

            Print("[FSM] Replacement submitted: " + fleetSignalName
                + " @ " + price + " x" + qty);
        }

        // Build 951: Unified bracket replace FSM entry point.
        // Called by PropagateMasterStopMove and PropagateMasterTargetMove for SIMA followers.
        // newStopPrice > 0 = stop is the moved leg; movedTargetNum > 0 = a target is the moved leg.
        private void InitiateBracketReplace(string fleetEntryName, PositionInfo pos,
            double newStopPrice, int movedTargetNum, double newTargetPrice)
        {
            // ATR tick absorption: if already in-flight, update the affected leg price only.
            BracketReplaceSpec existing;
            if (_bracketReplaceSpecs.TryGetValue(fleetEntryName, out existing))
            {
                lock (existing.SyncRoot)  // Build 951.1: lock on SyncRoot, not the spec instance (DeepSource CA2002)
                {
                    if (newStopPrice > 0)
                        existing.StopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                    if (movedTargetNum > 0)
                        existing.TargetPrices[movedTargetNum - 1] =
                            Instrument.MasterInstrument.RoundToTickSize(newTargetPrice);
                }
                Print(string.Format("[MOVE-SYNC] FSM: Bracket spec updated in-flight for {0}", fleetEntryName));
                return;
            }

            // Generate ONE shared OCO ID for the entire bracket.
            string freshOcoId = "V12_SYNC_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            var spec = new BracketReplaceSpec
            {
                EntryName        = fleetEntryName,
                FreshOcoId       = freshOcoId,
                Direction        = pos.Direction,
                ExecutingAccount = pos.ExecutingAccount,
                CreatedTime      = DateTime.Now,
                // Build 951.1 Bug-C1: Store back-reference so SubmitBracketReplacement can commit
                // pos.CurrentStopPrice atomically with the successful broker submit.
                PosRef           = pos
            };

            var toCancel = new List<Order>();

            // Collect current stop
            Order curStop;
            if (stopOrders.TryGetValue(fleetEntryName, out curStop) && curStop != null
                && (curStop.OrderState == OrderState.Working || curStop.OrderState == OrderState.Accepted))
            {
                spec.StopPrice = (newStopPrice > 0)
                    ? Instrument.MasterInstrument.RoundToTickSize(newStopPrice)
                    : curStop.StopPrice;
                spec.StopQty = curStop.Quantity;
                spec.PendingOrderIds.Add(curStop.OrderId);
                spec.RemainingCancels++;
                toCancel.Add(curStop);
            }

            // Collect current targets
            for (int t = 1; t <= 5; t++)
            {
                var tDict = GetTargetOrdersDictionary(t);
                Order tOrder;
                if (tDict != null && tDict.TryGetValue(fleetEntryName, out tOrder) && tOrder != null
                    && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                {
                    spec.TargetPrices[t - 1] = (movedTargetNum == t && newTargetPrice > 0)
                        ? Instrument.MasterInstrument.RoundToTickSize(newTargetPrice)
                        : tOrder.LimitPrice;
                    spec.TargetQtys[t - 1] = tOrder.Quantity;
                    spec.PendingOrderIds.Add(tOrder.OrderId);
                    spec.RemainingCancels++;
                    toCancel.Add(tOrder);
                }
            }

            if (spec.RemainingCancels == 0)
            {
                Print(string.Format("[MOVE-SYNC] FSM: No working legs for {0} -- bracket replace skipped", fleetEntryName));
                return;
            }

            _bracketReplaceSpecs[fleetEntryName] = spec;
            Print(string.Format("[MOVE-SYNC] FSM: PendingCancel -> Bracket_{0} ({1} legs, OCO={2})",
                fleetEntryName, spec.RemainingCancels, freshOcoId));

            try
            {
                pos.ExecutingAccount.Cancel(toCancel.ToArray());
                lock (stateLock) { pos.OcoGroupId = freshOcoId; }
            }
            catch (Exception ex)
            {
                _bracketReplaceSpecs.TryRemove(fleetEntryName, out _);
                Print(string.Format("[MOVE-SYNC] ERROR InitiateBracketReplace {0}: {1}", fleetEntryName, ex.Message));
            }
        }

        // Build 951.5: Emergency account protection when FSM resubmission fails.
        // Called before TryRemove in CreateOrder-null and Submit() exception paths.
        // Mirrors ProcessReaperFlattenQueue pattern (REAPER.cs). ASCII-only strings.
        private void EmergencyProtectFollowerAccount(Account execAccount, string entryName, List<Order> ordersToCancel)
        {
            if (execAccount == null) return;
            Print(string.Format("[FSM-GUARD] Emergency protection for {0} on {1}", entryName, execAccount.Name));
            try
            {
                if (ordersToCancel != null && ordersToCancel.Count > 0)
                    execAccount.Cancel(ordersToCancel.ToArray());
            }
            catch (Exception ex)
            {
                Print(string.Format("[FSM-GUARD] Cancel failed for {0}: {1}", entryName, ex.Message));
            }
            try
            {
                foreach (Position brkPos in execAccount.Positions)
                {
                    if (brkPos.Instrument.FullName != Instrument.FullName) continue;
                    if (brkPos.MarketPosition == MarketPosition.Flat || brkPos.Quantity <= 0) continue;
                    int flatQty = brkPos.Quantity;
                    OrderAction flatAction = brkPos.MarketPosition == MarketPosition.Long
                        ? OrderAction.Sell : OrderAction.BuyToCover;
                    string sigName = "FSM_ERR_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    Order flatOrder = execAccount.CreateOrder(Instrument, flatAction,
                        OrderType.Market, TimeInForce.Gtc, flatQty, 0, 0, "", sigName, null);
                    if (flatOrder != null)
                        execAccount.Submit(new[] { flatOrder });
                    break;
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[FSM-GUARD] Flatten failed for {0}: {1}", entryName, ex.Message));
            }
        }

        // Build 951: Phase 2 of bracket replace FSM.
        // Runs on strategy thread via TriggerCustomEvent after ALL legs confirm cancellation.
        // All resubmissions share spec.FreshOcoId -- broker sees a coherent OCO group.
        private void SubmitBracketReplacement(BracketReplaceSpec spec)
        {
            if (spec.ExecutingAccount == null) return;

            Print(string.Format("[MOVE-SYNC] FSM: Submitting Replacement -> Bracket_{0} @ OCO={1}",
                spec.EntryName, spec.FreshOcoId));
            Print(string.Format("[RESCUE] Resubmitted with Fresh OCO: {0}", spec.FreshOcoId));

            OrderAction exitAction = spec.Direction == MarketPosition.Long
                ? OrderAction.Sell : OrderAction.BuyToCover;
            var toSubmit = new List<Order>();

            double stopPriceSnap;
            int stopQtySnap;
            bool gapFillDetected;
            var targetPricesSnap = new double[5];
            var targetQtysSnap = new int[5];
            lock (spec.SyncRoot)
            {
                stopPriceSnap = spec.StopPrice;
                stopQtySnap = spec.StopQty;
                gapFillDetected = spec.GapFillDetected;
                Array.Copy(spec.TargetPrices, targetPricesSnap, 5);
                Array.Copy(spec.TargetQtys, targetQtysSnap, 5);
            }

            PositionInfo livePos;
            int liveRemaining = 0;
            lock (stateLock)
            {
                if (activePositions.TryGetValue(spec.EntryName, out livePos)
                    && livePos != null
                    && livePos.EntryFilled
                    && livePos.RemainingContracts > 0)
                {
                    liveRemaining = livePos.RemainingContracts;
                }
            }

            if (liveRemaining <= 0)
            {
                Print(string.Format(
                    "[MOVE-SYNC] FSM: Position {0} closed during cancel-gap -- aborting bracket replacement.",
                    spec.EntryName));
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                return;
            }

            // Build 951.5 [P1]: Abort if stop filled during cancel-gap -- position is already flat.
            bool positionClosed;
            lock (spec.SyncRoot) { positionClosed = spec.PositionClosed; }
            if (positionClosed)
            {
                Print(string.Format(
                    "[MOVE-SYNC] FSM: Stop filled during cancel-gap for Bracket_{0} -- aborting target recreation.",
                    spec.EntryName));
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                return;
            }

            // Build 951.5 [P1]: Cross-check broker-live position before submitting -- activePositions may lag.
            bool brokerLiveExists = false;
            foreach (Position brkPos in spec.ExecutingAccount.Positions)
            {
                if (brkPos.Instrument.FullName == Instrument.FullName
                    && brkPos.MarketPosition != MarketPosition.Flat
                    && brkPos.Quantity > 0)
                {
                    brokerLiveExists = true;
                    break;
                }
            }
            if (!brokerLiveExists)
            {
                Print(string.Format(
                    "[MOVE-SYNC] FSM: Broker-live position absent for Bracket_{0} -- aborting replacement.",
                    spec.EntryName));
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                return;
            }

            int reconciledStopQty = stopPriceSnap > 0 ? liveRemaining : 0;
            var reconciledTargetQtys = new int[5];
            int targetBudget = liveRemaining;
            int targetSnapshotTotal = 0;
            int targetReconciledTotal = 0;
            for (int t = 0; t < 5; t++)
            {
                int snapQty = Math.Max(0, targetQtysSnap[t]);
                targetSnapshotTotal += snapQty;
                if (targetPricesSnap[t] <= 0 || snapQty <= 0 || targetBudget <= 0)
                    continue;

                int useQty = Math.Min(snapQty, targetBudget);
                reconciledTargetQtys[t] = useQty;
                targetBudget -= useQty;
                targetReconciledTotal += useQty;
            }

            bool qtyAdjusted = Math.Max(0, stopQtySnap) != reconciledStopQty;
            if (!qtyAdjusted)
            {
                for (int t = 0; t < 5; t++)
                {
                    if (Math.Max(0, targetQtysSnap[t]) != reconciledTargetQtys[t])
                    {
                        qtyAdjusted = true;
                        break;
                    }
                }
            }
            if (qtyAdjusted || gapFillDetected)
            {
                Print(string.Format(
                    "[MOVE-SYNC] FSM: Qty reconcile Bracket_{0} stop {1}->{2} targets {3}->{4} live={5} gapFill={6}",
                    spec.EntryName, Math.Max(0, stopQtySnap), reconciledStopQty,
                    targetSnapshotTotal, targetReconciledTotal, liveRemaining, gapFillDetected));
            }

            // Build 951.1 Bug-D: Hold created orders in locals; write to dicts ONLY after Submit() succeeds.
            Order pendingStop = null;
            var pendingTargets = new Order[5];

            // Resubmit stop
            if (stopPriceSnap > 0 && reconciledStopQty > 0)
            {
                string stopSig = SymmetryTrim("S_" + spec.EntryName, 50);
                Order newStop = spec.ExecutingAccount.CreateOrder(
                    Instrument, exitAction, OrderType.StopMarket, TimeInForce.Gtc,
                    reconciledStopQty, 0, stopPriceSnap, spec.FreshOcoId, stopSig, null);
                // Build 951.1 Bug-D: null guard -- CreateOrder returns null on broker rejection
                if (newStop == null)
                {
                    Print(string.Format("[MOVE-SYNC] ERROR SubmitBracketReplacement {0}: CreateOrder null (stop)", spec.EntryName));
                    EmergencyProtectFollowerAccount(spec.ExecutingAccount, spec.EntryName, null);  // Build 951.5
                    _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                    return;
                }
                pendingStop = newStop;
                toSubmit.Add(newStop);
            }

            // Resubmit all active targets
            for (int t = 0; t < 5; t++)
            {
                if (targetPricesSnap[t] > 0 && reconciledTargetQtys[t] > 0)
                {
                    string tSig = SymmetryTrim("T" + (t + 1) + "_" + spec.EntryName, 40);
                    Order newTarget = spec.ExecutingAccount.CreateOrder(
                        Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                        reconciledTargetQtys[t], targetPricesSnap[t], 0,
                        spec.FreshOcoId, tSig, null);
                    // Build 951.1 Bug-D: null guard -- CreateOrder returns null on broker rejection
                    if (newTarget == null)
                    {
                        Print(string.Format("[MOVE-SYNC] ERROR SubmitBracketReplacement {0}: CreateOrder null (T{1})", spec.EntryName, t + 1));
                        EmergencyProtectFollowerAccount(spec.ExecutingAccount, spec.EntryName, toSubmit);  // Build 951.5
                        _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                        return;
                    }
                    pendingTargets[t] = newTarget;
                    toSubmit.Add(newTarget);
                }
            }

            if (toSubmit.Count == 0)
            {
                Print(string.Format("[MOVE-SYNC] FSM: No legs to resubmit for Bracket_{0} -- skip", spec.EntryName));
                // Build 951.1 Bug-A: Remove FSM entry so REAPER suppression does not linger.
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                return;
            }

            try
            {
                spec.ExecutingAccount.Submit(toSubmit.ToArray());

                // Build 951.1 Bug-D: Update tracking dicts only after successful submit.
                if (pendingStop != null)
                    stopOrders[spec.EntryName] = pendingStop;
                for (int t = 0; t < 5; t++)
                {
                    if (pendingTargets[t] != null)
                    {
                        var tDict = GetTargetOrdersDictionary(t + 1);
                        if (tDict != null) tDict[spec.EntryName] = pendingTargets[t];
                    }
                }

                // Build 951.1 Bug-C1: Commit stop price only after broker confirms orders are live.
                if (spec.PosRef != null && stopPriceSnap > 0 && reconciledStopQty > 0)
                    spec.PosRef.CurrentStopPrice = stopPriceSnap;

                // Build 951.1 Bug-A: TryRemove HERE -- REAPER suppression window ends after successful submit.
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);

                Print(string.Format("[MOVE-SYNC] FSM: Bracket_{0} resubmitted: {1} legs",
                    spec.EntryName, toSubmit.Count));
            }
            catch (Exception ex)
            {
                // Build 951.5: Emergency protect before removing -- Submit() may have partially landed orders.
                EmergencyProtectFollowerAccount(spec.ExecutingAccount, spec.EntryName, toSubmit);
                // Build 951.1: TryRemove on exception to prevent orphaned REAPER suppression.
                _bracketReplaceSpecs.TryRemove(spec.EntryName, out _);
                Print(string.Format("[MOVE-SYNC] ERROR SubmitBracketReplacement {0}: {1}",
                    spec.EntryName, ex.Message));
            }
        }

        #endregion
    }
}
