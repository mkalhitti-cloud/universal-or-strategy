// V12.44 MODULAR: Order Management Module (Split from Orders.cs)
// Contains: Bracket orders, stop management, position sync, flatten, cleanup, reconciliation
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
        #region Order Submission & Stop Management

        private void SubmitBracketOrders(string entryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;

            try
            {
                // Validate stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);

                // [BUILD 924 - Fix B] Route bracket submission to follower account when applicable.
                bool isFollowerSubmit = pos.IsFollower && pos.ExecutingAccount != null;
                OrderAction bracketExitAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Sell : OrderAction.BuyToCover;

                // Build 936 [FIX-2]: Shared OCO group ID for all stop + target orders in this bracket.
                // Non-empty value triggers broker-native OCO protection (stop auto-cancelled when a target fills).
                // Survives NT8 restart because the broker maintains the group association independently.
                string bracketOcoId = pos.OcoGroupId ?? string.Empty;

                // Submit initial stop for all contracts
                Order stopOrder;
                if (isFollowerSubmit)
                {
                    // [BUILD 924 - Fix B] Follower stop: use ExecutingAccount API (not SubmitOrderUnmanaged which is master-local)
                    string stopSig = SymmetryTrim("Stop_" + entryName, 40);
                    Order sOrd = pos.ExecutingAccount.CreateOrder(
                        Instrument, bracketExitAction, OrderType.StopMarket, TimeInForce.Gtc,
                        pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, stopSig, null);
                    // [BUILD 924 - Fix B / Director's Note] Null-guard after CreateOrder matches S-001 pattern.
                    if (sOrd == null)
                    {
                        Print(string.Format("[BRACKET_FATAL] Follower stop CreateOrder returned null for {0}. Flattening.", entryName));
                        FlattenPositionByName(entryName);
                        return;
                    }
                    // Build 929 Fix2 [P1]: Wrap Submit in local try/catch.
                    // If Submit() throws (broker disconnect, margin, reject), the outer catch only logs
                    // and returns -- leaving this follower with a filled position and NO stop loss.
                    // We must flatten immediately to prevent a naked position.
                    try
                    {
                        pos.ExecutingAccount.Submit(new[] { sOrd });
                    }
                    catch (Exception submitEx)
                    {
                        Print(string.Format("[BRACKET_FATAL] Follower stop Submit THREW for {0}: {1}. Emergency flattening.", entryName, submitEx.Message));
                        EmergencyFlattenSingleFleetAccount(pos.ExecutingAccount);
                        return;
                    }
                    stopOrder = sOrd;
                }
                else
                {
                    stopOrder = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, "Stop_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, "Stop_" + entryName);
                }

                // V12.Audit [S-001]: Null-guard stop submission result. If broker rejects or drops
                // the stop, flatten immediately -- never leave a position with a false "protected" state.
                if (stopOrder == null)
                {
                    Print(string.Format("[BRACKET_FATAL] Stop order submission returned null for {0}. Flattening.", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }
                stopOrders[entryName] = stopOrder;

                int nonRunnerLimitQty = 0;
                int runnerQty = 0;

                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue; // skip orphan/zero fills

                    // Universal Ladder: runner detection is slot-based only -- T(n)Type == Runner.
                    if (IsRunnerTarget(targetNum))
                    {
                        runnerQty += targetQty;
                        Print(string.Format("[FORENSIC] T{0} {1}: Runner qty={2} -- limit SKIPPED",
                            targetNum, entryName, targetQty));
                        continue;
                    }

                    double targetPrice = GetTargetPrice(pos, targetNum);
                    if (targetPrice <= 0)
                    {
                        Print(string.Format("[TARGET_SKIP] T{0} for {1} has qty={2} but invalid price={3:F2}; skipped",
                            targetNum, entryName, targetQty, targetPrice));
                        continue;
                    }

                    // V12.Phase7 [C-04]: Round target price to valid tick boundary before submission.
                    targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice);

                    Print(string.Format("[FORENSIC] T{0} {1}: qty={2} price={3:F2} submitting limit",
                        targetNum, entryName, targetQty, targetPrice));

                    Order limitOrder;
                    if (isFollowerSubmit)
                    {
                        // [BUILD 924 - Fix B] Follower target: use ExecutingAccount API
                        string targetSig = SymmetryTrim("T" + targetNum + "_" + entryName, 40);
                        Order tOrd = pos.ExecutingAccount.CreateOrder(
                            Instrument, bracketExitAction, OrderType.Limit, TimeInForce.Gtc,
                            targetQty, targetPrice, 0, bracketOcoId, targetSig, null);
                        // [BUILD 924 - Fix B / Director's Note] Null-guard after CreateOrder matches S-015 pattern.
                        if (tOrd != null)
                            pos.ExecutingAccount.Submit(new[] { tOrd });
                        else
                            Print(string.Format("[TARGET_WARN] Follower target T{0} CreateOrder returned null for {1}.", targetNum, entryName));
                        limitOrder = tOrd;
                    }
                    else
                    {
                        limitOrder = pos.Direction == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, targetQty, targetPrice, 0, bracketOcoId, "T" + targetNum + "_" + entryName)
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, targetQty, targetPrice, 0, bracketOcoId, "T" + targetNum + "_" + entryName);
                    }

                    var targetDict = GetTargetOrdersDictionary(targetNum);
                    // V12.Audit [S-015]: Only store non-null target orders. A null result means
                    // broker rejected the target -- skip storage so the slot stays empty rather
                    // than tracking a null reference. Stop is still present; no flatten needed.
                    if (targetDict != null)
                    {
                        if (limitOrder == null)
                        {
                            Print(string.Format("[TARGET_WARN] Target {0} order submission returned null for {1}. Target tracking disabled.", targetNum, entryName));
                        }
                        else
                        {
                            targetDict[entryName] = limitOrder;
                        }
                    }

                    nonRunnerLimitQty += targetQty;
                }

                pos.CurrentStopPrice = validatedStopPrice;

                // Zero-trust stop audit: stop quantity must always cover full position.
                if (stopOrder != null && stopOrder.Quantity != pos.TotalContracts)
                {
                    Print(string.Format("[STOP_AUDIT] MISMATCH {0}: StopQty={1} Total={2}",
                        entryName, stopOrder.Quantity, pos.TotalContracts));
                }
                else
                {
                    Print(string.Format("[STOP_AUDIT] OK {0}: StopQty={1} NonRunnerLimits={2} RunnerQty={3}",
                        entryName, pos.TotalContracts, nonRunnerLimitQty, runnerQty));
                }

                // V12.Audit [S-003]: BracketSubmitted is set AFTER the stop quantity audit so that
                // a mismatch detected above does not leave the position flagged as fully protected.
                pos.BracketSubmitted = true;

                // [938-BRACKET] Confirm full bracket submitted for follower accounts.
                if (isFollowerSubmit)
                    Print(string.Format("[938-BRACKET] Follower bracket submitted: {0} T1={1:F2} Stop={2:F2}",
                        entryName, pos.Target1Price, validatedStopPrice));

                StringBuilder bracketMsg = new StringBuilder();
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET V12.1101E: Stop@{1:F2}", tradeType, validatedStopPrice);
                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue;

                    bool isRunnerSlot = IsRunnerTarget(targetNum);

                    if (isRunnerSlot)
                        bracketMsg.AppendFormat(" | T{0}:{1}@trail", targetNum, targetQty);
                    else
                        bracketMsg.AppendFormat(" | T{0}:{1}@{2:F2}", targetNum, targetQty, GetTargetPrice(pos, targetNum));
                }

                Print(bracketMsg.ToString());

                // V12.Audit [D-007]: Verify target contract sum matches total position size.
                int _targetSum = nonRunnerLimitQty + runnerQty;
                if (_targetSum != pos.TotalContracts)
                {
                    Print(string.Format("[BRACKET_WARN] Target sum mismatch for {0}: targets={1} totalContracts={2}. Distribution may have lost contracts.",
                        entryName, _targetSum, pos.TotalContracts));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR SubmitBracketOrders: " + ex.Message);
            }
        }

        /// <summary>
        /// Phase 9.1 [SYNC_ALL]: Recalculates and re-submits or cancels limit target orders for
        /// all active positions based on current TnType settings and live ATR.
        /// Runs on the strategy thread. Called directly from ProcessIpcCommands() SYNC_ALL handler.
        /// </summary>
        private void RefreshActivePositionOrders()
        {
            if (activePositions == null || activePositions.IsEmpty)
            {
                Print("[SYNC_ALL] No active positions to refresh.");
                return;
            }

            // Snapshot under stateLock -- satisfies stateLock invariant for dict reads
            List<KeyValuePair<string, PositionInfo>> snapshot;
            lock (stateLock)
            {
                snapshot = activePositions.ToList();
            }

            int refreshed = 0;
            foreach (var kvp in snapshot)
            {
                string entryName = kvp.Key;
                PositionInfo pos = kvp.Value;

                // Guard: entry must be filled and position open
                if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                // Guard: skip SIMA followers -- fleet dispatch is out of scope for Phase 9.1
                if (pos.IsFollower)
                {
                    Print(string.Format("[SYNC_ALL] Skipping follower position {0}", entryName));
                    continue;
                }

                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    // Skip already-filled targets
                    if (IsTargetFilled(pos, targetNum)) continue;

                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue;

                    var targetDict = GetTargetOrdersDictionary(targetNum);
                    if (targetDict == null) continue;

                    // Check if a live limit order exists for this target slot
                    Order existingOrder = null;
                    bool hasWorkingOrder = targetDict.TryGetValue(entryName, out existingOrder) &&
                                           existingOrder != null &&
                                           (existingOrder.OrderState == OrderState.Working ||
                                            existingOrder.OrderState == OrderState.Accepted);

                    // [C-06 parity]: Skip ChangePending orders to avoid broker race
                    if (existingOrder != null && existingOrder.OrderState == OrderState.ChangePending)
                    {
                        Print(string.Format("[SYNC_ALL] T{0} {1}: ChangePending -- skipping", targetNum, entryName));
                        continue;
                    }

                    bool isNowRunner = IsRunnerTarget(targetNum);

                    if (isNowRunner)
                    {
                        // Runner targets must have NO limit order -- cancel any existing one
                        if (hasWorkingOrder)
                        {
                            try
                            {
                                CancelOrder(existingOrder);
                                targetDict.TryRemove(entryName, out _);
                                Print(string.Format("[SYNC_ALL] T{0} {1}: Limit cancelled -> now Runner", targetNum, entryName));
                                refreshed++;
                            }
                            catch (Exception ex)
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: CancelOrder failed -- {2}", targetNum, entryName, ex.Message));
                            }
                        }
                        continue;
                    }

                    // Limit/ATR/Ticks/Points: recalculate price from live ATR and entry
                    // Build 1102Y [P-06]: Role-aware reprice -- RMA/SIMA positions use stamped role; others use slot-based.
                    double newPrice = CalculateTargetPriceFromPos(pos.Direction, pos.EntryPrice, pos, targetNum);
                    if (newPrice <= 0)
                    {
                        Print(string.Format("[SYNC_ALL] T{0} {1}: Calculated price invalid ({2:F2}) -- skipped", targetNum, entryName, newPrice));
                        continue;
                    }

                    if (hasWorkingOrder)
                    {
                        // Shift existing limit if it moved by >= 1 tick
                        if (Math.Abs(existingOrder.LimitPrice - newPrice) >= tickSize)
                        {
                            try
                            {
                                ChangeOrder(existingOrder, existingOrder.Quantity, newPrice, 0);
                                switch (targetNum)
                                {
                                    case 1: pos.Target1Price = newPrice; break;
                                    case 2: pos.Target2Price = newPrice; break;
                                    case 3: pos.Target3Price = newPrice; break;
                                    case 4: pos.Target4Price = newPrice; break;
                                    case 5: pos.Target5Price = newPrice; break;
                                }
                                Print(string.Format("[SYNC_ALL] T{0} {1}: Repriced -> {2:F2}", targetNum, entryName, newPrice));
                                refreshed++;
                            }
                            catch (Exception ex)
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: ChangeOrder failed -- {2}", targetNum, entryName, ex.Message));
                            }
                        }
                        else
                        {
                            Print(string.Format("[SYNC_ALL] T{0} {1}: Price unchanged at {2:F2} -- no action", targetNum, entryName, newPrice));
                        }
                    }
                    else
                    {
                        // No working order (e.g. Runner->Limit swap): submit a fresh limit order
                        try
                        {
                            Order newLimit = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, targetQty, newPrice, 0, "", "T" + targetNum + "_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, targetQty, newPrice, 0, "", "T" + targetNum + "_" + entryName);

                            if (newLimit != null)
                            {
                                targetDict[entryName] = newLimit;
                                switch (targetNum)
                                {
                                    case 1: pos.Target1Price = newPrice; break;
                                    case 2: pos.Target2Price = newPrice; break;
                                    case 3: pos.Target3Price = newPrice; break;
                                    case 4: pos.Target4Price = newPrice; break;
                                    case 5: pos.Target5Price = newPrice; break;
                                }
                                Print(string.Format("[SYNC_ALL] T{0} {1}: New limit submitted @ {2:F2} qty={3}", targetNum, entryName, newPrice, targetQty));
                                refreshed++;
                            }
                            else
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: SubmitOrderUnmanaged returned null @ {2:F2}", targetNum, entryName, newPrice));
                            }
                        }
                        catch (Exception ex)
                        {
                            Print(string.Format("[SYNC_ALL] T{0} {1}: Submit failed -- {2}", targetNum, entryName, ex.Message));
                        }
                    }
                }
            }

            Print(string.Format("[SYNC_ALL] Complete. Positions scanned: {0} | Actions taken: {1}", snapshot.Count, refreshed));
        }

        /// <summary>
        /// Updates the stop order quantity after a partial target fill.
        /// </summary>
        /// <remarks>
        /// V12.Audit [C-08]: Callers MUST ensure the <paramref name="pos"/> reference is
        /// read under <c>stateLock</c> or from within a callback that is already serialized
        /// by the NinjaTrader dispatch thread. Passing a stale <paramref name="pos"/> can
        /// result in the stop being undersized relative to actual remaining contracts.
        /// </remarks>
        private void UpdateStopQuantity(string entryName, PositionInfo pos)
        {
            // V12.Hardening [RISK-01]: Atomic update guard
            // Locks stateLock to prevent dirty reads of pos.RemainingContracts while ApplyTargetFill is modifying it
            lock (stateLock)
            {
                if (!stopOrders.ContainsKey(entryName)) return;
                if (pos.RemainingContracts <= 0) return;
                // V12.41: No trailing/updates before entry fill is confirmed
                if (!pos.EntryFilled) return;

                try
                {
                    Order currentStop = stopOrders[entryName];

                    // V8.11 FIX: Store pending replacement BEFORE cancelling
                    // This ensures we only create a new stop when the old one is confirmed cancelled
                    if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                    {
                        // V8.31: Check if there's already a pending replacement to prevent duplicates
                        if (pendingStopReplacements.ContainsKey(entryName))
                        {
                            // Just update the quantity, don't create a new pending
                            if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                            {
                                existingPending.Quantity = pos.RemainingContracts;
                                Print(string.Format("V8.31: Updated existing pending replacement for {0} to {1} contracts", entryName, pos.RemainingContracts));
                            }
                            return;
                        }

                        // Store the replacement info
                        var newPending = new PendingStopReplacement
                        {
                            EntryName = entryName,
                            Quantity = pos.RemainingContracts,
                            StopPrice = pos.CurrentStopPrice,
                            Direction = pos.Direction,
                            OldOrder = currentStop,
                            CreatedTime = DateTime.Now  // V8.31: Added for timeout support
                        };

                        // V8.31: Thread-safe add
                        if (pendingStopReplacements.TryAdd(entryName, newPending))
                        {
                            Interlocked.Increment(ref pendingReplacementCount);
                        }

                        // Cancel old stop - replacement will be created in OnOrderUpdate when confirmed
                        CancelOrder(currentStop);
                        Print(string.Format("STOP CANCEL PENDING: {0} | Will replace with {1} contracts @ {2:F2}",
                            entryName, pos.RemainingContracts, pos.CurrentStopPrice));
                    }
                    else
                    {
                        // No existing stop to cancel, create new one directly
                        // V12.41: Pass the entry name for stricter validation
                        CreateNewStopOrder(entryName, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                    }
                }
                catch (Exception ex)
                {
                    Print(string.Format("(!) ERROR UpdateStopQuantity for {0}: {1}", entryName, ex.Message));
                    Print(string.Format("(!) POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
                }
            }
        }

        // V8.11: Helper method to create a new stop order
        // V8.31: Added guard to prevent duplicate stop creation
        private void CreateNewStopOrder(string entryName, int quantity, double stopPrice, MarketPosition direction)
        {
            try
            {
                // V12.41 ZOMBIE GUARD: Block stop creation if position is flat or entry not filled
                if (activePositions.TryGetValue(entryName, out var targetPos))
                {
                    if (targetPos.RemainingContracts <= 0)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED zombie stop for {0} - Position is FLAT (Remaining=0)", entryName));
                        return;
                    }
                    if (!targetPos.EntryFilled)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED early stop for {0} - Fill not yet confirmed", entryName));
                        return;
                    }
                }
                else
                {
                    Print(string.Format("[STOP_GUARD] BLOCKED orphan stop for {0} - No tracking record found", entryName));
                    return;
                }

                // V12.Phase7 [C-06]: Check if any live stop already exists for this entry (Working, Accepted,
                // ChangePending, or ChangeSubmitted). Without ChangePending guard, a ChangeOrder in flight
                // causes a second stop to be created -- leading to stacked stops that can reverse the position.
                if (stopOrders.TryGetValue(entryName, out var existingStop))
                {
                    if (existingStop != null && (
                        existingStop.OrderState == OrderState.Working ||
                        existingStop.OrderState == OrderState.Accepted ||
                        existingStop.OrderState == OrderState.ChangePending ||
                        existingStop.OrderState == OrderState.ChangeSubmitted))
                    {
                        Print(string.Format("V12.Phase7: SKIPPING duplicate stop for {0} -- existing stop state={1}", entryName, existingStop.OrderState));
                        return;
                    }
                }

                // V12.Phase7 [C-04]: Round stop price to valid tick boundary.
                // CreateNewStopOrder receives raw prices that may not be tick-aligned.
                // Off-tick prices are rejected by the broker, leaving the position unprotected.
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                Order newStop = null;
                OrderAction exitAction = direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route to correct account (fleet follower vs local)
                if (activePositions.TryGetValue(entryName, out var pos) && pos.IsFollower && pos.ExecutingAccount != null)
                {
                    // Build 951: Fresh OCO per replacement -- broker rejects reuse of cancelled OCO group.
                    string freshStopOco = "S_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    lock (stateLock) { pos.OcoGroupId = freshStopOco; }
                    string _b950OcoId = freshStopOco;
                    Print(string.Format("[RESCUE] Resubmitted with Fresh OCO: {0}", freshStopOco));
                    // Fleet follower: use Account API
                    string sigName = "S_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, exitAction,
                        OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, _b950OcoId, sigName, null);
                    if (newStop == null)
                    {
                        Print(string.Format("[BRACKET_FATAL] Follower stop CreateOrder returned null for {0}. Flattening.", entryName));
                        FlattenPositionByName(entryName);
                        return;
                    }
                    try
                    {
                        pos.ExecutingAccount.Submit(new[] { newStop });
                    }
                    catch (Exception submitEx)
                    {
                        Print(string.Format("[BRACKET_FATAL] Follower stop Submit THREW for {0}: {1}. Emergency flattening.", entryName, submitEx.Message));
                        EmergencyFlattenSingleFleetAccount(pos.ExecutingAccount);
                        return;
                    }
                }
                else
                {
                    // Build 951: Fresh OCO per replacement -- broker rejects reuse of cancelled OCO group.
                    string freshStopOco = "S_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    if (pos != null) { lock (stateLock) { pos.OcoGroupId = freshStopOco; } }
                    string _b950OcoId = freshStopOco;
                    Print(string.Format("[RESCUE] Resubmitted with Fresh OCO: {0}", freshStopOco));
                    // Local: use SubmitOrderUnmanaged with truncated signal name
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string sigName = "S_" + entryName + "_" + suffix;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = SubmitOrderUnmanaged(0, exitAction, OrderType.StopMarket, quantity, 0, stopPrice, _b950OcoId, sigName);
                }

                if (newStop == null)
                {
                    Print(string.Format("?? ?? CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("?? ?? POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        direction == MarketPosition.Long ? "LONG" : "SHORT", quantity, stopPrice));

                    // Attempt to flatten position immediately
                    Print(string.Format("?? ?? Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;

                // [LATENCY_AUDIT] Measure OCO turnaround: CreatedTime was stamped in UpdateStopQuantity() when
                // the target fill triggered the pending stop replacement. The delta = Target Fill -> Stop Cancel
                // confirmed -> new stop submitted -- the full OCO lifecycle round-trip.
                if (pendingStopReplacements.TryGetValue(entryName, out var pendingForLatency))
                {
                    double ocoLatencyMs = (DateTime.Now - pendingForLatency.CreatedTime).TotalMilliseconds;
                    Print(string.Format("[LATENCY_AUDIT] Target Fill -> Stop Cancel Delta: {0:F1}ms (Entry: {1})",
                        ocoLatencyMs, entryName));
                }

                Print(string.Format("STOP QTY UPDATED: {0} contracts @ {1:F2} (Order: {2})",
                    quantity, stopPrice, newStop.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("?? ?? ERROR CreateNewStopOrder for {0}: {1}", entryName, ex.Message));
            }
        }

        // Build 950: Re-submit profit targets that were OCO-cascade-cancelled during stop replacement.
        // Runs on strategy thread via TriggerCustomEvent. Checks Order.OrderState directly on the
        // captured Order object -- avoids dict-timing races with RemoveGhostOrderRef.
        private void RestoreCascadedTargets(string entryName, TargetSnapshot[] capturedTargets)
        {
            if (capturedTargets == null || capturedTargets.Length == 0) return;

            PositionInfo pos;
            if (!activePositions.TryGetValue(entryName, out pos)) return;

            bool entryFilled;
            int remainingContracts;
            MarketPosition direction;
            bool isFollower;
            Account executingAccount;
            string ocoGroupId;

            lock (stateLock)
            {
                entryFilled        = pos.EntryFilled;
                remainingContracts = pos.RemainingContracts;
                direction          = pos.Direction;
                isFollower         = pos.IsFollower;
                executingAccount   = pos.ExecutingAccount;
                ocoGroupId         = pos.OcoGroupId;
            }

            if (!entryFilled || remainingContracts <= 0) return;

            OrderAction exitAction = direction == MarketPosition.Long
                ? OrderAction.Sell : OrderAction.BuyToCover;
            string bracketOcoId = ocoGroupId ?? string.Empty;

            foreach (TargetSnapshot snap in capturedTargets)
            {
                if (snap == null || snap.CapturedOrder == null) continue;

                // Only restore targets the broker OCO cascade-cancelled.
                // Filled targets have OrderState.Filled -- skip them.
                if (snap.CapturedOrder.OrderState != OrderState.Cancelled
                    && snap.CapturedOrder.OrderState != OrderState.Rejected)
                    continue;

                double restoredPrice = Instrument.MasterInstrument.RoundToTickSize(snap.Price);
                Order newTarget = null;

                if (isFollower && executingAccount != null)
                {
                    string tSig = SymmetryTrim("T" + snap.TargetNum + "_" + entryName, 40);
                    Order tOrd = executingAccount.CreateOrder(
                        Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                        snap.Qty, restoredPrice, 0, bracketOcoId, tSig, null);
                    if (tOrd != null)
                    {
                        // Build 951.2: Guard follower submit -- broker exceptions must not crash the strategy loop.
                        try
                        {
                            executingAccount.Submit(new[] { tOrd });
                            newTarget = tOrd;
                        }
                        catch (Exception submitEx)
                        {
                            Print(string.Format(
                                "[B950] ERROR: RestoreCascadedTargets Submit threw for {0} T{1}: {2}",
                                entryName, snap.TargetNum, submitEx.Message));
                        }
                    }
                }
                else
                {
                    string tSig = "T" + snap.TargetNum + "_" + entryName;
                    newTarget = direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit,
                            snap.Qty, restoredPrice, 0, bracketOcoId, tSig)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit,
                            snap.Qty, restoredPrice, 0, bracketOcoId, tSig);
                }

                var tDict = GetTargetOrdersDictionary(snap.TargetNum);
                if (tDict != null)
                {
                    if (newTarget != null)
                    {
                        tDict[entryName] = newTarget;
                        Print(string.Format("[B950] Target T{0} restored for {1} @ {2:F2} qty={3}",
                            snap.TargetNum, entryName, restoredPrice, snap.Qty));
                    }
                    else
                    {
                        Print(string.Format("[B950] WARN: Target T{0} restore NULL for {1}",
                            snap.TargetNum, entryName));
                    }
                }
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice, int level = 0, double entryPrice = 0)
        {
            // V12.41: Use real-time price instead of stale bar Close[0]
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
            double tickSize = Instrument.MasterInstrument.TickSize;
            
            // Build 951: 2-tick buffer for all levels including BE -- prevents immediate-fill on BE stops.
            // Replaces the prior Level 1 zero-tick exception (V12.1102E).
            double minDistance = 2 * tickSize;

            double resultStop = desiredStopPrice;

            if (direction == MarketPosition.Long)
            {
                // For BE (Level 1), only adjust if stop is STRICTLY above market (illegal).
                // Equality is allowed for BE to prevent safety pull-back on the threshold cross.
                bool isIllegal = (level == 1) ? (desiredStopPrice > currentPrice) : (desiredStopPrice >= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        // Build 951.2: Apply minDistance for all levels including BE when no entryPrice floor is available.
                        resultStop = currentPrice - minDistance;
                        Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "above" : "at/above")));
                    }
                }
            }
            else
            {
                bool isIllegal = (level == 1) ? (desiredStopPrice < currentPrice) : (desiredStopPrice <= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        // Build 951.2: Apply minDistance for all levels including BE when no entryPrice floor is available.
                        resultStop = currentPrice + minDistance;
                        Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "below" : "at/below")));
                    }
                }
            }

            // [Build 1102H] Profit Floor: secondary backstop -- ensures resultStop never crosses
            // below entry for Long (or above entry for Short) regardless of how resultStop was set.
            if (level == 1 && entryPrice > 0)
            {
                if (direction == MarketPosition.Long && resultStop < entryPrice)
                    resultStop = entryPrice;
                else if (direction == MarketPosition.Short && resultStop > entryPrice)
                    resultStop = entryPrice;
            }

            // V12.Phase7 [C-04]: Always round to valid tick boundary before returning.
            return Instrument.MasterInstrument.RoundToTickSize(resultStop);
        }

        #endregion


        // V12.46: Trailing Stops region moved to Trailing.cs


        #region Position Sync & Flatten

        private void SyncPositionState()
        {
            List<string> toRemove = new List<string>();

            // V8.30: Thread-safe snapshot iteration
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && pos.RemainingContracts <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                CleanupPosition(key);
            }
        }

        /// <summary>
        /// V12 SIMA: Chase If Touch - iterates the unified entryOrders dictionary which contains
        /// BOTH local and fleet follower limit orders. When price touches a working limit entry
        /// that was not filled, the limit is nudged N ticks toward market (citOffset * TickSize)
        /// exactly once per order lifetime. Local orders: ChangeOrder() to new limit price.
        /// Follower orders: cancel + resubmit as OrderType.Limit at new price via ExecutingAccount.
        /// Re-nudging is prevented by _citNudgedKeys one-shot guard, cleared on fill or cancel.
        /// </summary>
        private void ManageCIT()
        {
            if (activePositions.Count == 0 && entryOrders.Count == 0) return;
            if (string.IsNullOrEmpty(ChaseIfTouchPoints) || ChaseIfTouchPoints == "0") return;

            // [BUILD 924 -- Fix C] Suppress CIT during price-move propagation to prevent
            // race-fire on freshly resubmitted follower limit orders before sync cycle completes.
            if (_propagationActive)
            {
                Print("[CIT] Suppressed during price-move propagation (Build 924 Fix C)");
                return;
            }

            double citOffset = 0;
            if (!double.TryParse(ChaseIfTouchPoints, out citOffset)) return;

            // Iterate ALL entry orders in the unified dictionary (local + every fleet account)
            foreach (var kvp in entryOrders.ToArray())
            {
                string key = kvp.Key;
                Order order = kvp.Value;
                if (order == null || order.OrderState != OrderState.Working) continue;
                if (order.OrderType != OrderType.Limit) continue; // only chase limit entries
                if (_citNudgedKeys.ContainsKey(key)) continue;    // [BUILD 949] one-shot: already nudged

                // [BUILD 948 CIT FIX] Correct directional bar-price logic:
                // - LONG entry (Buy): price must DROP DOWN to the limit -> compare Low[0] <= limitPrice
                // - SHORT entry (Sell): price must RISE UP to the limit -> compare High[0] >= limitPrice
                // Previous bug: Short used Low[0] <= limitPrice which is ALWAYS true when clicking
                // far above the current market, causing instant market conversion on every click.
                double currentPrice = (order.OrderAction == OrderAction.Buy) ? Low[0] : High[0];
                double limitPrice = order.LimitPrice;

                bool triggerChase = (order.OrderAction == OrderAction.Buy)
                    ? (currentPrice <= limitPrice)   // Long: bar low touched or pierced the limit
                    : (currentPrice >= limitPrice);  // Short: bar high touched or pierced the limit


                if (!triggerChase) continue;

                // Determine local vs follower
                PositionInfo pos = null;
                activePositions.TryGetValue(key, out pos);
                bool isFollower = pos != null && pos.IsFollower && pos.ExecutingAccount != null;

                try
                {
                    double tickSize      = Instrument.MasterInstrument.TickSize;
                    double nudgeDistance = citOffset * tickSize;
                    double newLimitPrice = (order.OrderAction == OrderAction.Buy)
                        ? Instrument.MasterInstrument.RoundToTickSize(limitPrice + nudgeDistance)
                        : Instrument.MasterInstrument.RoundToTickSize(limitPrice - nudgeDistance);

                    if (isFollower)
                    {
                        // Fleet follower: cancel limit, resubmit as nudged limit via account API
                        Account followerAcct = pos.ExecutingAccount;
                        Print($"[CIT] FLEET nudge: {key} on {followerAcct.Name} | {limitPrice:F2} -> {newLimitPrice:F2} ({citOffset} ticks toward mkt)");

                        followerAcct.Cancel(new[] { order });

                        Order nudgedOrder = followerAcct.CreateOrder(Instrument, order.OrderAction, OrderType.Limit,
                            TimeInForce.Gtc, order.Quantity, newLimitPrice, 0, "", "CIT_" + key, null);
                        if (nudgedOrder == null)
                        {
                            Print($"[CIT] ERROR: CreateOrder returned null for {key} on {followerAcct.Name} -- nudge aborted");
                            continue;
                        }
                        followerAcct.Submit(new[] { nudgedOrder });

                        entryOrders[key] = nudgedOrder;
                    }
                    else
                    {
                        // Local account: ChangeOrder moves limit N ticks toward market
                        Print($"[CIT] LOCAL nudge: {key} | {limitPrice:F2} -> {newLimitPrice:F2} ({citOffset} ticks toward mkt)");
                        ChangeOrder(order, order.Quantity, newLimitPrice, 0);
                    }
                    _citNudgedKeys.TryAdd(key, true); // [BUILD 949] one-shot: mark as nudged
                }
                catch (Exception ex)
                {
                    Print($"[CIT] ERROR chasing {key}: {ex.Message}");
                }
            }
        }

        private void FlattenAll()
        {
            // V1101E HOT-PATCH: Serialize entire flatten pipeline to prevent overlap with Reaper/order callbacks.
            lock (stateLock)
            {
                isFlattenRunning = true; // V12.13b: Suppress stop re-submit during flatten
                try
                {
                    // V10 GHOST FIX: Scan for actual live position even if activePositions is empty
                    int liveQty = 0;
                    MarketPosition liveDir = MarketPosition.Flat;
                    if (Position != null)
                    {
                        liveQty = Position.Quantity;
                        liveDir = Position.MarketPosition;
                    }

                    if (activePositions.Count == 0 && liveQty > 0)
                    {
                         Print(string.Format("FLATTEN GHOST: Closing ORPHANED position of {0} contracts", liveQty));
                         if (liveDir == MarketPosition.Long)
                             SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");
                         else
                             SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");

                         return;
                    }

                    if (activePositions.Count == 0 && Position.MarketPosition == MarketPosition.Flat)
                    {
                        Print("FLATTEN: No active positions to close");
                        // Still run SIMA flatten just in case of desync
                        if (EnableSIMA)
                        {
                            // V1101E HOT-PATCH: Keep flatten guard asserted across nested SIMA flatten call.
                            isFlattenRunning = true;
                            FlattenAllApexAccounts();
                            isFlattenRunning = true;
                        }
                        return;
                    }

                    Print("FLATTEN: Closing all positions...");

                    // V12.13b: Removed ExitLong/ExitShort block (managed-mode methods incompatible with IsUnmanaged=true)
                    // Unmanaged flatten via SubmitOrderUnmanaged is handled below at the per-position level

                    // 2. Clear all pending entry orders on Master
                    foreach (var entryOrder in entryOrders.Values)
                    {
                        if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                            CancelOrder(entryOrder);
                    }

                    // 3. Flatten SIMA Fleet
                    if (EnableSIMA)
                    {
                        // V1101E HOT-PATCH: Keep flatten guard asserted across nested SIMA flatten call.
                        isFlattenRunning = true;
                        FlattenAllApexAccounts();
                        isFlattenRunning = true;
                    }

                    // V12.2: Reset Sync State
                    isLongArmed = false;
                    isShortArmed = false;

                    // V1102Q [RUNNER-LEAK]: Explicit follower sweep. 
                    // Purge all follower metadata from memory to prevent ghost entries.
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (kvp.Value.IsFollower)
                        {
                            activePositions.TryRemove(kvp.Key, out _);
                            entryOrders.TryRemove(kvp.Key, out _);
                            Print($"[V1102Q] Follower Sweep: Purged {kvp.Key} from memory");
                        }
                    }

                    // V8.30: Thread-safe snapshot iteration (Master/Main entries)
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        PositionInfo pos = kvp.Value;
                        string entryName = kvp.Key;

                        if (pos.EntryFilled)
                        {
                            Print(string.Format("FLATTEN: Closing filled {0} position",
                                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT"));

                            // V12.1101E [PH5-COLLIDE-01]: Lifecycle-safe stop cancellation.
                            // Keep stop dictionary refs until broker-confirmed terminal state.
                            RequestStopCancelLifecycleSafe(entryName);
                            Print(string.Format("FLATTEN: Requested stop lifecycle cancel for {0}", entryName));

                            // V8.31: Also clear any pending stop replacements to prevent orphaned stops
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                                Print(string.Format("V8.31: Cleared pending stop replacement for {0}", entryName));
                            }

                            // Cancel all target orders (T1-T5)
                            for (int tNum = 1; tNum <= 5; tNum++)
                            {
                                var tDict = GetTargetOrdersDictionary(tNum);
                                if (tDict != null && tDict.TryGetValue(entryName, out var tOrder))
                                {
                                    if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted || tOrder.OrderState == OrderState.Submitted))
                                        CancelOrder(tOrder);
                                }
                            }

                            // V8.28 FIX: Use LIVE position quantity instead of cached RemainingContracts
                            int livePositionQty = 0;
                            try
                            {
                                if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                                    livePositionQty = Position.Quantity;
                            }
                            catch (Exception pEx) { Print("Flatten Error reading Position: " + pEx.Message); }

                            // Use the smaller of cached and live to avoid overselling
                            // V10 DIAGNOSTIC: Print values
                            Print(string.Format("FLATTEN DIAGNOSTIC: Entry={0} Cached={1} Live={2}", entryName, pos.RemainingContracts, livePositionQty));

                            // V10 FLATTEN FIX: Trust cached contracts if live is 0 (latency protection)
                            // If cached says we have contracts, we close them.
                            int flattenQty = pos.RemainingContracts;

                            if (livePositionQty > 0)
                            {
                                 // If NinjaTrader agrees we have a position, use the smaller to act safe?
                                 // No, if real position is smaller, we might be over-closing.
                                 // But if real is larger, we under-close.
                                 // Let's stick to closing what we know we opened.
                                 flattenQty = pos.RemainingContracts;
                            }

                            // Submit market order to close position
                            if (flattenQty > 0)
                            {
                                Order flattenOrder = pos.Direction == MarketPosition.Long
                                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName)
                                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName);

                                if (flattenOrder == null) Print("FLATTEN ERROR: SubmitOrderUnmanaged returned NULL");
                                else Print(string.Format("FLATTEN SENT: {0} {1} contracts", pos.Direction == MarketPosition.Long ? "SELL" : "BUY", flattenQty));
                            }
                            else
                            {
                                 Print("FLATTEN SKIPPED: Qty is 0");
                            }

                        }
                        else
                        {
                            // Cancel pending entry order
                            if (entryOrders.ContainsKey(entryName))
                            {
                                Order entryOrder = entryOrders[entryName];
                                if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(entryOrder);
                                    Print(string.Format("FLATTEN: Cancelled pending {0} entry order @ {1:F2}",
                                        pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", pos.EntryPrice));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print("ERROR FlattenAll: " + ex.Message);
                }
                finally
                {
                    // V1101E HOT-PATCH: Release flatten guard only after serialized flatten pipeline exits.
                    isFlattenRunning = false; // V12.13b: Always release guard
                }
            }
        }

        private void FlattenPositionByName(string entryName)
        {
            if (!activePositions.TryGetValue(entryName, out var pos)) return;

            if (pos.EntryFilled && pos.RemainingContracts > 0)
            {
                Print(string.Format("?? ?? EMERGENCY FLATTEN: Closing {0} position due to stop order failure", entryName));

                // V12.3: Determine if this is a fleet follower or local position
                bool isFleetFollower = pos.IsFollower && pos.ExecutingAccount != null;

                // V8.31: Cancel ALL bracket orders first to prevent race conditions
                // V12.3: Use Account.Cancel for fleet followers, CancelOrder for local
                if (stopOrders.TryGetValue(entryName, out var stopOrder) && stopOrder != null)
                {
                    if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { stopOrder });
                        else CancelOrder(stopOrder);
                    }
                }
                // Cancel all target orders (T1-T5)
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var tDict = GetTargetOrdersDictionary(tNum);
                    if (tDict != null && tDict.TryGetValue(entryName, out var tOrder) && tOrder != null)
                    {
                        if (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted)
                        {
                            if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { tOrder });
                            else CancelOrder(tOrder);
                        }
                    }
                }

                // V8.31: Clear pending replacements
                if (pendingStopReplacements.TryRemove(entryName, out _))
                {
                    Interlocked.Decrement(ref pendingReplacementCount);
                }

                int flattenQty = pos.RemainingContracts;
                OrderAction flattenAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route flatten order to correct account
                Order flattenOrder = null;
                if (isFleetFollower)
                {
                    // Fleet follower: flatten on the follower's own account
                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = pos.ExecutingAccount.CreateOrder(Instrument, flattenAction,
                        OrderType.Market, TimeInForce.Gtc, flattenQty, 0, 0, "", sigName, null);
                    pos.ExecutingAccount.Submit(new[] { flattenOrder });
                }
                else
                {
                    // Local: use SubmitOrderUnmanaged (use live position qty for accuracy)
                    try
                    {
                        if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                            flattenQty = Math.Max(flattenQty, Position.Quantity);
                    }
                    catch { }

                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = SubmitOrderUnmanaged(0, flattenAction, OrderType.Market, flattenQty, 0, 0, "", sigName);
                }

                if (flattenOrder != null)
                {
                    Print(string.Format("Emergency flatten order submitted on {0}: {1} {2} contracts at MARKET",
                        isFleetFollower ? pos.ExecutingAccount.Name : "LOCAL",
                        pos.Direction == MarketPosition.Long ? "SELL" : "BUY",
                        flattenQty));
                }
                else
                {
                    Print(string.Format("?? ???? ???? ?? CRITICAL: Emergency flatten order FAILED for {0}!", entryName));
                    Print("?? ???? ???? ?? MANUAL INTERVENTION REQUIRED - Close position manually in NinjaTrader!");
                }
            }
        }


        // V12.1101E [DESYNC-01]: Terminal-only removal. Returns true if order is Filled, Cancelled, Rejected, or Unknown.
        private static bool IsOrderTerminal(OrderState state)
        {
            return state == OrderState.Filled || state == OrderState.Cancelled
                || state == OrderState.Rejected || state == OrderState.Unknown;
        }

        // V12.1101E [DESYNC-01]: True if any stop/target/entry dict still holds a non-terminal order for this entry.
        private bool HasActiveOrPendingOrderForEntry(string entryName)
        {
            if (stopOrders.TryGetValue(entryName, out var stop) && stop != null && !IsOrderTerminal(stop.OrderState))
                return true;

            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict != null && tDict.TryGetValue(entryName, out var tOrder) && tOrder != null && !IsOrderTerminal(tOrder.OrderState))
                    return true;
            }

            if (entryOrders.TryGetValue(entryName, out var e) && e != null && !IsOrderTerminal(e.OrderState)) return true;
            return false;
        }

        /// <summary>
        /// V12.1101E [DESYNC-01]: Terminal-only cleanup. Only TryRemove when order is Filled/Cancelled/Rejected/Unknown;
        /// if Working/Accepted/Pending, call CancelOrder but do NOT remove -- OnOrderUpdate will remove on terminal state.
        /// activePositions is removed only at the end and only when no dict still holds an active/pending order.
        /// </summary>
        private void CleanupPosition(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;
            if (!activePositions.ContainsKey(entryName)) return;

            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // Build 1102U [BUG-2a]: Fleet followers must use Account.Cancel() -- not CancelOrder() which only
            // works for orders submitted through this strategy instance's NinjaScript order management.
            // Follower orders are submitted via acct.Submit(), so they require the broker-level cancel API.
            bool isFollowerForCleanup = activePositions.TryGetValue(entryName, out var cleanupPosRef)
                && cleanupPosRef.IsFollower && cleanupPosRef.ExecutingAccount != null;

            // Stop: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            if (stopOrders.TryGetValue(entryName, out var stopOrder))
            {
                if (stopOrder != null)
                {
                    if (IsOrderTerminal(stopOrder.OrderState))
                        stopOrders.TryRemove(entryName, out _);
                    else
                    {
                        if (isFollowerForCleanup)
                            cleanupPosRef.ExecutingAccount.Cancel(new[] { stopOrder });
                        else
                            CancelOrder(stopOrder);
                        cancelledStops++;
                    }
                }
                else
                    stopOrders.TryRemove(entryName, out _);
            }

            // T1-T5: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict == null) continue;

                if (tDict.TryGetValue(entryName, out var tOrder))
                {
                    if (tOrder != null)
                    {
                        if (IsOrderTerminal(tOrder.OrderState))
                            tDict.TryRemove(entryName, out _);
                        else
                        {
                            if (isFollowerForCleanup)
                                cleanupPosRef.ExecutingAccount.Cancel(new[] { tOrder });
                            else
                                CancelOrder(tOrder);
                            cancelledTargets++;
                        }
                    }
                    else
                    {
                        tDict.TryRemove(entryName, out _);
                    }
                }
            }

            // Entry: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            if (entryOrders.TryGetValue(entryName, out var eOrder))
            {
                if (eOrder != null)
                {
                    if (IsOrderTerminal(eOrder.OrderState))
                    {
                        entryOrders.TryRemove(entryName, out _);
                        _citNudgedKeys.TryRemove(entryName, out _);
                    }
                    else
                    {
                        if (isFollowerForCleanup)
                            cleanupPosRef.ExecutingAccount.Cancel(new[] { eOrder });
                        else
                            CancelOrder(eOrder);
                        cancelledEntries++;
                    }
                }
                else
                {
                    entryOrders.TryRemove(entryName, out _);
                    _citNudgedKeys.TryRemove(entryName, out _);
                }
            }

            if (pendingStopReplacements.TryRemove(entryName, out _))
                Interlocked.Decrement(ref pendingReplacementCount);

            if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
                Print(string.Format("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}",
                    entryName, cancelledStops, cancelledTargets, cancelledEntries));

            // V12.Phase8.2 [META-GUARD]: Pre-compute followerExpected before any purge decision.
            // If the Reaper has a non-zero expectedPositions for this account, a Repair Hook is planning
            // to re-issue the entry. Purging now would destroy the PositionInfo metadata
            // (price/qty/direction) that the Repair Hook reads to reconstruct the order.
            int followerExpected = 0;
            if (activePositions.TryGetValue(entryName, out var metaGuardCheck)
                && metaGuardCheck.IsFollower
                && metaGuardCheck.ExecutingAccount != null)
            {
                string followerAcctName = metaGuardCheck.ExecutingAccount.Name;
                lock (stateLock)
                {
                    // Build 1102U [BUG-1]: Must use composite key to match new ExpKey scheme.
                    expectedPositions.TryGetValue(ExpKey(followerAcctName), out followerExpected);
                }
                if (followerExpected != 0)
                {
                    Print(string.Format("[META-GUARD] {0}: Broker is flat but expectedPositions={1}. " +
                        "Retaining activePositions metadata for Repair Hook. Will purge after repair completes.",
                        entryName, followerExpected));
                    // [Phase 8.2 Part 3] Explicit early-return: prevent fall-through into FIX-ZP-02
                    // which would forcibly purge activePositions even when the Repair Hook is pending.
                    return;
                }
            }

            // V12.1101E [DESYNC-01]: Defer activePositions removal until no dict holds an active/pending order.
            // V12.Phase8.2 [META-GUARD]: Skip purge if Reaper Repair Hook is active (followerExpected != 0).
            if (followerExpected == 0 && !HasActiveOrPendingOrderForEntry(entryName))
            {
                if (activePositions.TryRemove(entryName, out _))
                    SymmetryGuardForgetEntry(entryName);
            }

            // [FIX-ZP-02]: Secondary safety net for SIMA followers -- force purge if broker confirms flat.
            // Guards against lingering non-terminal dict entries preventing HasActiveOrPendingOrderForEntry
            // from returning false even though the actual broker position is already flat.
            if (followerExpected == 0
                && activePositions.TryGetValue(entryName, out var followerCheck)
                && followerCheck.IsFollower
                && followerCheck.ExecutingAccount != null)
            {
                var brokerPos = followerCheck.ExecutingAccount.Positions
                    .FirstOrDefault(p => p.Instrument == Instrument);
                if (brokerPos != null && brokerPos.MarketPosition == MarketPosition.Flat)
                {
                    if (activePositions.TryRemove(entryName, out _))
                    {
                        SymmetryGuardForgetEntry(entryName);
                        Print(string.Format("[FIXED_G] Purging {0} - confirmed flat by broker.", entryName));
                    }
                }
            }
        }

        /// <summary>
        /// V12.12: Remove any ghost order reference (targets, stops, entries) when it reaches a terminal state.
        /// This only clears stale references; it does not alter stop quantities or position state.
        /// </summary>
        private void RemoveGhostOrderRef(Order order, string reason)
        {
            if (order == null) return;

            var orderDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
            {
                (target1Orders, "T1"),
                (target2Orders, "T2"),
                (target3Orders, "T3"),
                (target4Orders, "T4"),
                (target5Orders, "T5"),
                (stopOrders, "STOP"),
                (entryOrders, "ENTRY"),
            };

            bool foundInDict = false;
            string removedLabel = null;
            string removedKey = null;
            foreach (var (dict, label) in orderDicts)
            {
                // V12.17: Dual match - reference equality OR OrderId string match
                foreach (var kvp in dict.ToArray())
                {
                    if (kvp.Value == order ||
                        (kvp.Value != null && order != null && kvp.Value.OrderId == order.OrderId))
                    {
                        if (dict.TryRemove(kvp.Key, out _))
                        {
                            string matchType = (kvp.Value == order) ? "REF" : "ORDERID";
                            Print(string.Format("[GHOST_FIX] Order {0}_{1} terminated ({2}). Nullifying reference. (match={3}, OrderId={4})",
                                label, kvp.Key, reason, matchType, order.OrderId ?? "NULL"));
                            foundInDict = true;
                            removedLabel = label;
                            removedKey = kvp.Key;
                        }
                    }
                }
            }

            // V12.17: Position protection audit - if we just removed a STOP, check if position is now unprotected
            if (foundInDict && removedLabel == "STOP" && !string.IsNullOrEmpty(removedKey))
            {
                if (activePositions.TryGetValue(removedKey, out var auditPos) && auditPos.EntryFilled && auditPos.RemainingContracts > 0)
                {
                    if (!stopOrders.ContainsKey(removedKey))
                    {
                        Print(string.Format("V12.17: WARNING UNPROTECTED POSITION: {0} has {1} contracts with NO STOP after {2}. Manual intervention may be required.",
                            removedKey, auditPos.RemainingContracts, reason));
                    }
                }
            }

            // [FIX-ZP-01]: After any terminal order ref is removed, re-evaluate position purge eligibility.
            // Deliberately NOT calling CleanupPosition here to avoid cancelling live remaining orders
            // (e.g. T2-T5 still working after T1 fills). HasActiveOrPendingOrderForEntry is the safe gate.
            if (foundInDict && !string.IsNullOrEmpty(removedKey))
            {
                if (!HasActiveOrPendingOrderForEntry(removedKey))
                {
                    // [1102G] Guard: Never purge a position that still holds open contracts.
                    if (activePositions.TryGetValue(removedKey, out var purgeCheck) && purgeCheck.RemainingContracts > 0)
                        return;

                    // V12.Phase8.2 [META-GUARD]: If this is a follower with a pending repair,
                    // preserve activePositions metadata so the Repair Hook can reconstruct the order.
                    if (activePositions.TryGetValue(removedKey, out var ghostMetaCheck)
                        && ghostMetaCheck.IsFollower
                        && ghostMetaCheck.ExecutingAccount != null)
                    {
                        string ghostAcctName = ghostMetaCheck.ExecutingAccount.Name;
                        int ghostExpected = 0;
                        lock (stateLock)
                        {
                            // Build 1102U [BUG-1]: Composite key parity -- must match ExpKey scheme.
                            expectedPositions.TryGetValue(ExpKey(ghostAcctName), out ghostExpected);
                        }
                        if (ghostExpected != 0)
                        {
                            Print(string.Format("[META-GUARD] {0}: ZOMBIE_PURGE suppressed -- expectedPositions={1} on {2}. " +
                                "Retaining metadata for Repair Hook.",
                                removedKey, ghostExpected, ghostAcctName));
                            return;
                        }
                    }

                    if (activePositions.TryRemove(removedKey, out _))
                    {
                        SymmetryGuardForgetEntry(removedKey);
                        Print(string.Format("[ZOMBIE_PURGE] {0}: all order refs terminal. Purging activePositions.", removedKey));
                    }
                }
            }

            // V12.17: If it was not in our dictionaries, classify why
            if (!foundInDict)
            {
                // Only log if it is one of our orders (matching prefix) to avoid noise from other strategies
                if (order.Name.Contains("RMA") || order.Name.Contains("OR") || order.Name.Contains("MOMO") || order.Name.Contains("TREND") ||
                    order.Name.Contains("Stop_") || order.Name.Contains("Tgt_") || order.Name.Contains("Fleet_"))
                {
                    // V12.17: Distinguish expected cascade from suspicious orphan
                    bool positionStillActive = false;
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (order.Name.Contains(kvp.Key))
                        {
                            positionStillActive = true;
                            Print(string.Format("V12.17: WARNING {0} {1} - dict ref gone but position {2} still active (orphan risk, OrderId={3})",
                                order.Name, reason, kvp.Key, order.OrderId ?? "NULL"));
                            break;
                        }
                    }
                    if (!positionStillActive)
                    {
                        Print(string.Format("V12.17: {0} {1} - cleaned by upstream handler (expected cascade, OrderId={2})", order.Name, reason, order.OrderId ?? "NULL"));
                    }
                }
            }
        }

        private void ReconcileOrphanedOrders(string reason)
        {
            try
            {
                if (Account == null) return;

                bool foundOrphans = false;
                foreach (Order order in Account.Orders)
                {
                    if (order == null) continue;

                    // Only look at working orders
                    if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
                        continue;

                    // V8.27 CRITICAL FIX: Only process orders for THIS instrument
                    // This prevents cross-instrument cancellation when running multiple strategy instances
                    if (order.Instrument.FullName != Instrument.FullName)
                        continue;

                    // Check if this order has one of our prefix signatures
                    string name = order.Name;
                    if (name.StartsWith("Stop_") || name.StartsWith("T1_") || name.StartsWith("T2_") ||
                        name.StartsWith("T3_") || name.StartsWith("T4_") || name.StartsWith("T5_") ||
                        name.StartsWith("Flatten_") || name.StartsWith("Trim_"))
                    {
                        // Check if we actually have an active position for this
                        string entryName = "";
                        if (name.Contains("_"))
                        {
                            int firstUnderscore = name.IndexOf('_');
                            entryName = name.Substring(firstUnderscore + 1);
                            // Strip timestamp if present
                            int lastUnderscore = entryName.LastIndexOf('_');
                            if (lastUnderscore > 0 && entryName.Length - lastUnderscore > 10)
                                entryName = entryName.Substring(0, lastUnderscore);
                        }

                        // V10 FIX: Handle TRIM execution state update - MOVED TO OnExecutionUpdate

                        if (string.IsNullOrEmpty(entryName) || !activePositions.ContainsKey(entryName))
                        {
                            Print(string.Format("ORPHANED ORDER DETECTED ({0}): {1} | Cancelling...", reason, name));
                            CancelOrder(order);
                            foundOrphans = true;
                        }
                    }
                }

                // === V12.18 REVERSE AUDIT: Strategy -> Broker ===
                // For each tracked order ref, verify it still exists as Working/Accepted
                // in the broker's order collection. If it doesn't, it's a ghost -- purge it.
                Print(string.Format("[GHOST_FIX] REVERSE AUDIT START ({0})", reason));
                int reverseGhosts = 0;

                // Build a HashSet of live broker OrderIds for O(1) lookup
                HashSet<string> liveBrokerOrderIds = new HashSet<string>();
                foreach (Order brokerOrder in Account.Orders)
                {
                    if (brokerOrder != null && !string.IsNullOrEmpty(brokerOrder.OrderId) &&
                        (brokerOrder.OrderState == OrderState.Working || brokerOrder.OrderState == OrderState.Accepted))
                    {
                        liveBrokerOrderIds.Add(brokerOrder.OrderId);
                    }
                }

                // Also scan fleet accounts if SIMA is enabled
                if (EnableSIMA)
                {
                    foreach (Account acct in Account.All)
                    {
                        if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foreach (Order fleetOrder in acct.Orders)
                            {
                                if (fleetOrder != null && !string.IsNullOrEmpty(fleetOrder.OrderId) &&
                                    (fleetOrder.OrderState == OrderState.Working || fleetOrder.OrderState == OrderState.Accepted))
                                {
                                    liveBrokerOrderIds.Add(fleetOrder.OrderId);
                                }
                            }
                        }
                    }
                }

                // Check all strategy order dictionaries against live broker orders
                var reverseCheckDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
                {
                    (stopOrders, "STOP"), (target1Orders, "T1"), (target2Orders, "T2"),
                    (target3Orders, "T3"), (target4Orders, "T4"), (target5Orders, "T5"), (entryOrders, "ENTRY"),
                };

                foreach (var (dict, label) in reverseCheckDicts)
                {
                    foreach (var kvp in dict.ToArray())
                    {
                        Order trackedOrder = kvp.Value;
                        if (trackedOrder == null) continue;

                        // Only audit orders that SHOULD be alive (Working/Accepted)
                        // Terminal orders are cleaned by OnOrderUpdate; this catches leaks
                        bool isTerminal = (trackedOrder.OrderState == OrderState.Cancelled ||
                                           trackedOrder.OrderState == OrderState.Rejected ||
                                           trackedOrder.OrderState == OrderState.Filled ||
                                           trackedOrder.OrderState == OrderState.Unknown);

                        bool notInBroker = !string.IsNullOrEmpty(trackedOrder.OrderId) &&
                                           !liveBrokerOrderIds.Contains(trackedOrder.OrderId);

                        if (isTerminal || notInBroker)
                        {
                            if (dict.TryRemove(kvp.Key, out _))
                            {
                                string state = trackedOrder.OrderState.ToString();
                                Print(string.Format("[GHOST_FIX] REVERSE AUDIT: {0} ghost for {1} purged (State={2}, InBroker={3}, OrderId={4})",
                                    label, kvp.Key, state, !notInBroker, trackedOrder.OrderId ?? "NULL"));
                                reverseGhosts++;
                            }
                        }
                    }
                }

                Print(string.Format("[GHOST_FIX] REVERSE AUDIT COMPLETE: {0} ghosts purged", reverseGhosts));

                if (foundOrphans || reverseGhosts > 0)
                    Print("Orphaned order reconciliation complete.");
            }
            catch (Exception ex)
            {
                Print("ERROR ReconcileOrphanedOrders: " + ex.Message);
            }
        }

        #endregion
    }
}
