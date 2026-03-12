// V12 REAPER Audit Module -- Fleet position audit, desync detection, and emergency flatten
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Audit Logic

        private void AuditApexPositions()
        {
            bool shouldLog = (DateTime.UtcNow - lastReaperLog).TotalSeconds >= 30;
            int auditedCount = 0;
            int activeCount = 0;

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    auditedCount++;
                    if (AuditSingleFleetAccount(acct, shouldLog)) activeCount++;
                }
            }

            // V12.12: Explicitly audit the Master account if not covered by the prefix filter.
            bool masterAudited = IsFleetAccount(Account);
            if (!masterAudited)
            {
                auditedCount++;
                if (AuditMasterAccountIfNeeded(shouldLog)) activeCount++;
            }

            if (shouldLog)
            {
                if (activeCount == 0)
                    Print($"[REAPER] Heartbeat: All {auditedCount} accounts flat.");
                else
                    Print($"[REAPER] Heartbeat: {activeCount}/{auditedCount} accounts with positions.");
                lastReaperLog = DateTime.UtcNow;
            }
        }

        // Build 935 [REAPER-B935-003]: Per-account audit logic extracted from AuditApexPositions.
        // Returns true if the account has non-zero state (for heartbeat counter).
        private bool AuditSingleFleetAccount(Account acct, bool shouldLog)
        {
            Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            int actualQty = 0;
            if (pos != null && pos.MarketPosition != MarketPosition.Flat)
                actualQty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;

            // Build 1102U [BUG-1]: Composite key + stateLock guard.
            string expectedKey = ExpKey(acct.Name);
            int expectedQty = 0;
            bool syncPending = false;
            expectedPositions.TryGetValue(expectedKey, out expectedQty);
            syncPending = _dispatchSyncPendingExpKeys.ContainsKey(expectedKey); // [B967-FIX-02]
            // Build 935 [REAPER-B935-002]: Per-account grace prevents Account A fill blocking Account B repair.
            bool inFillGrace = IsReaperFillGraceActive(expectedKey);

            bool hasState = expectedQty != 0 || actualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");

            if (expectedQty != actualQty)
            {
                if (actualQty == 0 && expectedQty != 0)
                {
                    // GHOST-FIX-3: Skip repair for Master -- it uses SubmitOrderUnmanaged, not follower path.
                    if (acct.Name == Account.Name)
                    {
                        if (shouldLog) Print($"[REAPER] {acct.Name} is the Master account -- skipping follower repair.");
                        return hasState;
                    }

                    if (syncPending || inFillGrace)
                    {
                        if (shouldLog)
                        {
                            string reason = syncPending ? "dispatch sync pending" : "fill grace active";
                            Print($"[REAPER] {acct.Name}: repair deferred ({reason}) while expected={expectedQty}, actual=0.");
                        }
                        return hasState;
                    }

                    string repairKey = acct.Name + "_" + Instrument.FullName;
                    bool alreadyInFlight;
                    alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]

                    if (!alreadyInFlight)
                    {
                        bool hasWorkingEntry = false;
                        string blockingOrderName = null;
                        OrderState blockingState = OrderState.Unknown;
                        Dictionary<string, PositionInfo> activeSnapshot;
                        activeSnapshot = new Dictionary<string, PositionInfo>(activePositions);

                        foreach (var kvp in entryOrders.ToArray())
                        {
                            Order ord = kvp.Value;
                            if (ord == null) continue;
                            OrderState ordState = ord.OrderState;
                            if (IsOrderTerminal(ordState)) continue;
                            if (activeSnapshot.TryGetValue(kvp.Key, out var pi)
                                && pi.IsFollower && pi.ExecutingAccount != null
                                && pi.ExecutingAccount.Name == acct.Name
                                && (ordState == OrderState.Working || ordState == OrderState.Submitted
                                    || ordState == OrderState.Accepted || ordState == OrderState.ChangePending
                                    || ordState == OrderState.Unknown || ordState == OrderState.Initialized))
                            {
                                hasWorkingEntry = true;
                                blockingOrderName = string.IsNullOrEmpty(ord.Name) ? kvp.Key : ord.Name;
                                blockingState = ordState;
                                break;
                            }
                        }

                        if (!hasWorkingEntry)
                        {
                            if (shouldLog) Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
                            // A3-2: Mark in-flight BEFORE immediate queue processing to block double-enqueue in the next audit cycle (Build 960 audit fix)
                            _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if queue processing fails, preventing permanent lockout.
                            try { ProcessReaperRepairQueue(); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] ProcessReaperRepairQueue failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }
                        }
                        else
                        {
                            string throttleKey = blockingOrderName ?? acct.Name;
                            DateTime lastLogged;
                            bool shouldLogBlocked = !_repairBlockedLastLogged.TryGetValue(throttleKey, out lastLogged)
                                                    || (DateTime.UtcNow - lastLogged).TotalSeconds >= 30;
                            if (shouldLogBlocked)
                            {
                                _repairBlockedLastLogged[throttleKey] = DateTime.UtcNow;
                                Print($"[REAPER] Repair BLOCKED by {blockingOrderName} in state {blockingState} (throttled: next log in 30s)");
                            }
                        }
                    }
                    else if (shouldLog)
                        Print($"[REAPER] {acct.Name} repair already in-flight -- skipping.");

                    return hasState;
                }

                bool isCriticalDesync = (actualQty != 0 && expectedQty == 0)
                    || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);

                if (isCriticalDesync)
                {
                    if (shouldLog) Print($"[REAPER] * CRITICAL DESYNC on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                    if (AutoFlattenDesync)
                    {
                        if (shouldLog) Print($"[REAPER] * QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                        _reaperFlattenQueue.Enqueue(acct.Name);
                        try { ProcessReaperFlattenQueue(); } catch { }
                    }
                }
                else if (shouldLog)
                    Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
            }

            // ?? NAKED POSITION AUDIT (Build 1102R) ??????????????????????????????????
            if (actualQty != 0)
            {
                bool hasWorkingStop = acct.Orders.Any(o =>
                    o.Instrument?.FullName == Instrument?.FullName &&
                    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));

                if (!hasWorkingStop)
                {
                    DateTime firstSeen;
                    int graceSeconds = (NakedPositionGraceSec > 0) ? NakedPositionGraceSec : 3;
                    if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
                    {
                        _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;
                        Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
                            acct.Name, actualQty, graceSeconds));
                    }
                    else if ((DateTime.UtcNow - firstSeen).TotalSeconds >= graceSeconds)
                    {
                        bool alreadyNakedInFlight;
                        alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(ExpKey(acct.Name)); // [Build 968]
                        if (!alreadyNakedInFlight)
                        {
                            _reaperNakedStopInFlight.TryAdd(ExpKey(acct.Name), 0); // [Build 968]
                            Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                            try { ProcessReaperNakedStopQueue(); }
                            catch (Exception tcEx)
                            {
                                _reaperNakedStopInFlight.TryRemove(ExpKey(acct.Name), out _); // [Build 969]
                                Print(string.Format("[REAPER][NAKED_STOP] ProcessReaperNakedStopQueue failed for {0}: {1} -- in-flight cleared.", acct.Name, tcEx.Message));
                            }
                        }
                    }
                }
                else
                    _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
            }

            return hasState;
        }

        // Build 935 [REAPER-B935-004]: Audit the Master account when it isn't covered by AccountPrefix.
        // Returns true if the master account has non-zero state.
        private bool AuditMasterAccountIfNeeded(bool shouldLog)
        {
            Position masterPos = Account.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            int masterActualQty = 0;
            if (masterPos != null && masterPos.MarketPosition != MarketPosition.Flat)
                masterActualQty = masterPos.MarketPosition == MarketPosition.Long ? masterPos.Quantity : -masterPos.Quantity;

            int masterExpectedQty = 0;
            // Build 1102U [BUG-1]: Composite key + stateLock guard.
            expectedPositions.TryGetValue(ExpKey(Account.Name), out masterExpectedQty);

            bool hasState = masterExpectedQty != 0 || masterActualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");

            if (masterExpectedQty != masterActualQty)
            {
                if (masterActualQty == 0 && masterExpectedQty != 0)
                {
                    if (shouldLog) Print($"[REAPER] {Account.Name} (Master) is Flat (Target/Stop hit). Expected was {masterExpectedQty}.");
                }
                else
                {
                    // REAP-01: Suppress critical-desync within ReaperFillGraceTicks of a fresh reservation.
                    long stampTicks = Interlocked.Read(ref _lastExpectedPositionSetTicks);
                    bool inFillGrace = stampTicks > 0 &&
                        (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;

                    bool isCriticalDesync = !inFillGrace &&
                        ((masterActualQty != 0 && masterExpectedQty == 0) ||
                         (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0));

                    if (inFillGrace && shouldLog)
                        Print($"[REAPER] {Account.Name} (Master): Fill grace active -- desync check suppressed.");

                    if (isCriticalDesync)
                    {
                        if (shouldLog)
                            Print($"[REAPER] CRITICAL DESYNC on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                        if (AutoFlattenDesync)
                        {
                            if (shouldLog) Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                            _reaperFlattenQueue.Enqueue(Account.Name);
                            try { ProcessReaperFlattenQueue(); } catch { }
                        }
                    }
                    else if (shouldLog)
                        Print($"[REAPER] Minor Desync on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                }
            }

            return hasState;
        }

        /// <summary>
        /// V12.17 FIX: Processes queued flatten requests on the strategy thread.
        /// Called directly from the actor-enqueued REAPER audit flow.
        /// This is the SAFE way to call Account.Flatten() -- same pattern as IPC.
        /// </summary>
        private void ProcessReaperFlattenQueue()
        {
            string accountName;
            while (_reaperFlattenQueue.TryDequeue(out accountName))
            {
                try
                {
                    // Find the account by name
                    Account targetAcct = null;
                    foreach (Account acct in Account.All)
                    {
                        if (acct.Name == accountName)
                        {
                            targetAcct = acct;
                            break;
                        }
                    }

                    // Also check if it's the Master account
                    if (targetAcct == null && Account.Name == accountName)
                        targetAcct = Account;

                    if (targetAcct != null)
                    {
                        // [V12.Phase9] REAPER FIX: Use manual unmanaged close instead of broken targetAcct.Flatten().
                        // 1. Cancel all working orders for this instrument
                        List<Order> ordersToCancel = new List<Order>();
                        foreach (Order order in targetAcct.Orders)
                        {
                            if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                 order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
                            {
                                ordersToCancel.Add(order);
                            }
                        }
                        if (ordersToCancel.Count > 0)
                        {
                            targetAcct.Cancel(ordersToCancel);
                            Print($"[REAPER] Emergency Cancel: {ordersToCancel.Count} orders on {accountName}");
                        }

                        // 2. Proactively close positions via unmanaged market orders
                        foreach (Position position in targetAcct.Positions)
                        {
                            if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat) continue;
                            
                            int qty = position.Quantity;
                            string signalName = "ReaperFlatten_" + position.MarketPosition.ToString();
                            
                            if (targetAcct == this.Account)
                            {
                                // Master Account
                                if (position.MarketPosition == MarketPosition.Long)
                                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", signalName);
                                else
                                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", signalName);
                            }
                            else
                            {
                                // Fleet Account
                                OrderAction closeAction = position.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                                Order closeOrder = targetAcct.CreateOrder(Instrument, closeAction, OrderType.Market, TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                                targetAcct.Submit(new[] { closeOrder });
                            }
                            Print($"[REAPER] ? Emergency Market Close: {qty} contracts on {accountName}");
                        }

                        // V12.1101E [F-06]: REAPER audit already runs on the strategy thread.
                        // Build 1102U [BUG-1]: Composite key for instrument-scoped clear.
                        SetExpectedPosition(ExpKey(accountName), 0);
                        Print($"[REAPER] ? MARSHAL-FLATTEN (Unmanaged) executed on strategy thread for {accountName}");
                    }
                    else
                    {
                        Print($"[REAPER] [X] Could not find account '{accountName}' for marshal-flatten");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[REAPER] [X] MARSHAL-FLATTEN FAILED for {accountName}: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
