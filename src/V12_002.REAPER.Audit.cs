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
                    if (AuditSingleFleetAccount(acct, shouldLog))
                    {
                        activeCount++;
                    }
                }
            }

            // V12.12: Explicitly audit the Master account if not covered by the prefix filter.
            bool masterAudited = IsFleetAccount(Account);
            if (!masterAudited)
            {
                auditedCount++;
                if (AuditMasterAccountIfNeeded(shouldLog))
                {
                    activeCount++;
                }
            }

            if (shouldLog)
            {
                if (activeCount == 0)
                {
                    Print($"[REAPER] Heartbeat: All {auditedCount} accounts flat.");
                }
                else
                {
                    Print($"[REAPER] Heartbeat: {activeCount}/{auditedCount} accounts with positions.");
                }
                lastReaperLog = DateTime.UtcNow;
            }
        }

        // Build 935 [REAPER-B935-003]: Per-account audit logic extracted from AuditApexPositions.
        // Returns true if the account has non-zero state (for heartbeat counter).
        private bool AuditSingleFleetAccount(Account acct, bool shouldLog)
        {
            Position pos;
            int actualQty;
            int expectedQty;
            string expectedKey;
            bool syncPending;
            bool inFillGrace;
            bool hasState;
            List<FollowerBracketFSM> accountFsms;

            AuditFleet_CalculateExpectedActual(
                acct,
                shouldLog,
                out actualQty,
                out expectedQty,
                out expectedKey,
                out syncPending,
                out inFillGrace,
                out hasState,
                out accountFsms,
                out pos);

            if (expectedQty != actualQty)
            {
                if (actualQty == 0 && expectedQty != 0)
                {
                    // GHOST-FIX-3: Skip repair for Master -- it uses no FollowerBracketFSM -- repair path not applicable.
                    if (acct.Name == Account.Name)
                    {
                        if (shouldLog)
                        {
                            Print($"[REAPER] {acct.Name} is the Master account -- skipping follower repair.");
                        }
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

                    string repairKey;
                    if (EnqueueReaperRepairCandidate(acct, shouldLog, expectedQty, accountFsms, out repairKey))
                    {
                        // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                        try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
                        catch (Exception repairTriggerEx)
                        {
                            _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                            Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                        }
                    }

                    return hasState;
                }

                bool isCriticalDesync = (actualQty != 0 && expectedQty == 0)
                    || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);

                if (isCriticalDesync)
                {
                    // Build 999: Position Pass grace -- defer critical desync when account failed Phase 5 Position Pass.
                    // Applies only to the case where actualQty!=0 and expectedQty==0 (no FSM created on reconnect).
                    // Does NOT apply when sign mismatch (that is a genuine live desync -- fire immediately).
                    if (actualQty != 0 && expectedQty == 0)
                    {
                        DateTime ppFailedTime;
                        if (_positionPassFailedFirstSeen.TryGetValue(acct.Name, out ppFailedTime))
                        {
                            double graceElapsed = (DateTime.UtcNow - ppFailedTime).TotalSeconds;
                            if (graceElapsed < 10.0)
                            {
                                if (shouldLog)
                                {
                                    Print(string.Format("[REAPER] {0}: Position Pass grace ({1:F1}s/10s) -- deferring critical desync. Stop replace in progress.",
                                        acct.Name, graceElapsed));
                                }
                                return hasState; // Defer -- check again next audit cycle
                            }
                            // Grace expired -- clear entry and fall through to critical desync
                            _positionPassFailedFirstSeen.TryRemove(acct.Name, out _);
                            Print(string.Format("[REAPER] {0}: Position Pass grace expired ({1:F1}s) -- firing critical desync.",
                                acct.Name, graceElapsed));
                        }
                    }

                    if (shouldLog)
                    {
                        Print($"[REAPER] * CRITICAL DESYNC on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                    }
                    if (AutoFlattenDesync)
                    {
                        if (shouldLog)
                        {
                            Print($"[REAPER] * QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                        }
                        if (EnqueueReaperFlattenCandidate(acct))
                        {
                            try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }
                            catch (Exception _flatTriggerEx)
                            {
                                _reaperFlattenInFlight.TryRemove(acct.Name + "_" + Instrument.FullName, out _);
                                Print("[REAPER] TriggerCustomEvent failed for flatten of "
                                    + acct.Name + ": " + _flatTriggerEx.Message
                                    + " -- in-flight cleared, will re-detect next cycle");
                            }
                        }
                    }
                }
                else if (shouldLog)
                {
                    Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                }
            }

            // --- NAKED POSITION AUDIT (Build 1102R) ---------------------------------
            if (actualQty != 0)
            {
                bool hasWorkingStop = AuditFleet_CheckWorkingStop(acct);

                if (!hasWorkingStop)
                {
                    if (EnqueueReaperNakedStopCandidate(acct, pos, actualQty, expectedKey, shouldLog))
                    {
                        try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                        catch (Exception tcEx)
                        {
                            _reaperNakedStopInFlight.TryRemove(expectedKey, out _); // [Build 969]
                            Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.", acct.Name, tcEx.Message));
                        }
                    }
                }
                else
                {
                    _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
                }
            }

            return hasState;
        }

        private void AuditFleet_CalculateExpectedActual(
            Account acct, bool shouldLog,
            out int actualQty, out int expectedQty, out string expectedKey,
            out bool syncPending, out bool inFillGrace, out bool hasState,
            out List<FollowerBracketFSM> accountFsms, out Position pos)
        {
            pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            actualQty = 0;
            if (pos != null && pos.MarketPosition != MarketPosition.Flat)
            {
                actualQty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
            }

            // Build 1105: FSM is the SOLE authority for follower expected position.
            accountFsms = _followerBrackets.Values.Where(f => f.AccountName == acct.Name).ToList();
            int fsmExpectedQty = GetFsmExpectedPosition(acct.Name);

            // Handle hydrated Active FSMs with no order reference (restart edge case)
            foreach (var f in accountFsms)
            {
                if (f.State == FollowerBracketState.Active && f.EntryOrder == null)
                {
                    if (actualQty != 0)
                    {
                        fsmExpectedQty += actualQty;
                    }
                    else
                    {
                        FollowerBracketFSM staleFsm;
                        if (TryTerminateFollowerBracket(f.EntryName, out staleFsm))
                        {
                            Print(string.Format("[REAPER-C7] Stale Active FSM for {0} on {1} (broker flat) -- auto-terminating",
                                f.EntryName, acct.Name));
                        }
                    }
                }
            }

            // Build 999: If Position Pass failed on reconnect but FSM has since been created (replace cycle completed), clear grace.
            if (fsmExpectedQty != 0)
            {
                _positionPassFailedFirstSeen.TryRemove(acct.Name, out _);
            }

            // AUTHORITY: Use FSM state from now on
            expectedKey = ExpKey(acct.Name);
            expectedQty = fsmExpectedQty;

            syncPending = _dispatchSyncPendingExpKeys.ContainsKey(expectedKey); // [B967-FIX-02]
            // Build 935 [REAPER-B935-002]: Per-account grace prevents Account A fill blocking Account B repair.
            inFillGrace = IsReaperFillGraceActive(expectedKey);

            hasState = expectedQty != 0 || actualQty != 0;
            if (shouldLog && hasState)
            {
                Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
            }
        }

        private bool EnqueueReaperRepairCandidate(Account acct, bool shouldLog, int expectedQty, List<FollowerBracketFSM> accountFsms, out string repairKey)
        {
            repairKey = acct.Name + "_" + Instrument.FullName;
            bool alreadyInFlight;
            alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]

            if (!alreadyInFlight)
            {
                // Phase 4: Use FSM to identify working entry
                bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

                if (!hasWorkingEntry)
                {
                    if (shouldLog)
                    {
                        Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
                    }
                    // A3-2: Mark in-flight BEFORE TriggerCustomEvent to block double-enqueue in next audit cycle (Build 960 audit fix)
                    _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
                    _reaperRepairQueue.Enqueue(acct.Name);
                    return true;
                }
            }
            else if (shouldLog)
            {
                Print($"[REAPER] {acct.Name} repair already in-flight -- skipping.");
            }

            return false;
        }

        private bool EnqueueReaperFlattenCandidate(Account acct)
        {
            string flattenKey = acct.Name + "_" + Instrument.FullName;
            if (!_reaperFlattenInFlight.TryAdd(flattenKey, 0))
            {
                return false;
            }
            _reaperFlattenQueue.Enqueue(acct.Name);
            return true;
        }

        private bool AuditFleet_CheckWorkingStop(Account acct)
        {
            // Build 1108.003 [D3]: Snapshot broker orders before iteration. orderSnapshot
            var orders = acct.Orders.ToArray();
            return orders.Any(o =>
                o.Instrument?.FullName == Instrument?.FullName &&
                (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
        }

        private bool EnqueueReaperNakedStopCandidate(Account acct, Position pos, int actualQty, string expectedKey, bool shouldLog)
        {
            bool hasPendingStopReplace = false;
            foreach (var psr in pendingStopReplacements.Values)
            {
                PositionInfo psrPos;
                if (activePositions.TryGetValue(psr.EntryName, out psrPos)
                    && psrPos != null && psrPos.ExecutingAccount != null
                    && psrPos.ExecutingAccount.Name == acct.Name)
                {
                    hasPendingStopReplace = true;
                    break;
                }
            }

            if (hasPendingStopReplace)
            {
                _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
                if (shouldLog)
                    Print(string.Format("[REAPER] {0}: Stop replace in flight -- suppressing naked audit.", acct.Name));
            }
            else
            {
                DateTime firstSeen;
                int graceSeconds = (NakedPositionGraceSec >= 5) ? NakedPositionGraceSec : 5;
                if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
                {
                    _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;
                    Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
                        acct.Name, actualQty, graceSeconds));
                }
                else if ((DateTime.UtcNow - firstSeen).TotalSeconds >= graceSeconds)
                {
                    bool alreadyNakedInFlight;
                    alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(expectedKey); // [Build 968]
                    if (!alreadyNakedInFlight)
                    {
                        _reaperNakedStopInFlight.TryAdd(expectedKey, 0); // [Build 968]
                        Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                            acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                        _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                        return true;
                    }
                }
            }

            return false;
        }

        private void TerminateFsmsForAccount(string accountName)
        {
            foreach (var kvp in _followerBrackets.ToArray())
            {
                FollowerBracketFSM fsm = kvp.Value;
                if (fsm == null || fsm.AccountName != accountName)
                {
                    continue;
                }

                FollowerBracketFSM removedFsm;
                if (TryTerminateFollowerBracket(kvp.Key, out removedFsm))
                {
                    Print(string.Format("[FSM-C3] Terminated FSM {0} for {1} (flatten)", kvp.Key, accountName));
                }
            }
        }

        // Build 935 [REAPER-B935-004]: Audit the Master account when it isn't covered by AccountPrefix.
        // Returns true if the master account has non-zero state.
        private bool AuditMasterAccountIfNeeded(bool shouldLog)
        {
            Position masterPos = Account.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            int masterActualQty = 0;
            if (masterPos != null && masterPos.MarketPosition != MarketPosition.Flat)
            {
                masterActualQty = masterPos.MarketPosition == MarketPosition.Long ? masterPos.Quantity : -masterPos.Quantity;
            }

            int masterExpectedQty = 0;
            string masterExpectedKey = ExpKey(Account.Name);
            // Build 1102U [BUG-1]: Composite key + stateLock guard.
            expectedPositions.TryGetValue(masterExpectedKey, out masterExpectedQty);

            bool hasState = masterExpectedQty != 0 || masterActualQty != 0;
            if (shouldLog && hasState)
            {
                Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
            }

            if (masterExpectedQty != masterActualQty)
            {
                if (masterActualQty == 0 && masterExpectedQty != 0)
                {
                    if (shouldLog)
                    {
                        Print($"[REAPER] {Account.Name} (Master) is Flat (Target/Stop hit). Expected was {masterExpectedQty}.");
                    }
                }
                else if (AuditMaster_CheckExpectedActual(shouldLog, masterActualQty, masterExpectedQty))
                {
                    if (shouldLog)
                    {
                        Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                    }
                    if (EnqueueReaperMasterFlatten())
                    {
                        try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }
                        catch (Exception _mFlatTriggerEx)
                        {
                            _reaperFlattenInFlight.TryRemove(Account.Name + "_" + Instrument.FullName, out _);
                            Print("[REAPER] TriggerCustomEvent failed for master flatten: "
                                + _mFlatTriggerEx.Message + " -- in-flight cleared, will re-detect next cycle");
                        }
                    }
                }
            }

            // Build 998: Master naked-position audit -- mirrors AuditSingleFleetAccount lines 160-200.
            // AuditMasterAccountIfNeeded previously only checked expectedPositions vs actual.
            // A naked master position (no working stop) was never detected or recovered.
            if (masterActualQty != 0)
            {
                bool masterHasWorkingStop = Account.Orders.Any(o =>
                    o.Instrument?.FullName == Instrument?.FullName &&
                    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
                if (!masterHasWorkingStop)
                {
                    DateTime masterFirstSeen;
                    int graceSeconds = (NakedPositionGraceSec >= 5) ? NakedPositionGraceSec : 5;
                    if (!_nakedPositionFirstSeen.TryGetValue(Account.Name, out masterFirstSeen))
                    {
                        _nakedPositionFirstSeen[Account.Name] = DateTime.UtcNow;
                        Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct naked -- starting {2}s grace window.",
                            Account.Name, masterActualQty, graceSeconds));
                    }
                    else if (EnqueueReaperMasterNakedStop(masterPos, masterActualQty, masterExpectedKey, masterFirstSeen))
                    {
                        try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                        catch (Exception tcEx)
                        {
                            _reaperNakedStopInFlight.TryRemove(masterExpectedKey, out _);
                            Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0} (Master): {1} -- in-flight cleared.",
                                Account.Name, tcEx.Message));
                        }
                    }
                }
                else
                {
                    _nakedPositionFirstSeen.TryRemove(Account.Name, out _);
                }
            }

            return hasState;
        }

        private bool AuditMaster_CheckExpectedActual(bool shouldLog, int masterActualQty, int masterExpectedQty)
        {
            // REAP-01: Suppress critical-desync within ReaperFillGraceTicks of a fresh reservation.
            long stampTicks = Interlocked.Read(ref _lastExpectedPositionSetTicks);
            bool inFillGrace = stampTicks > 0 &&
                (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;

            bool isCriticalDesync = !inFillGrace &&
                ((masterActualQty != 0 && masterExpectedQty == 0) ||
                 (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0));

            if (inFillGrace && shouldLog)
            {
                Print($"[REAPER] {Account.Name} (Master): Fill grace active -- desync check suppressed.");
            }

            if (isCriticalDesync)
            {
                if (shouldLog)
                    Print($"[REAPER] CRITICAL DESYNC on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                if (AutoFlattenDesync)
                {
                    return true;
                }
            }
            else if (shouldLog)
            {
                Print($"[REAPER] Minor Desync on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
            }

            return false;
        }

        private bool EnqueueReaperMasterFlatten()
        {
            string flattenKey = Account.Name + "_" + Instrument.FullName;
            if (!_reaperFlattenInFlight.TryAdd(flattenKey, 0))
            {
                return false;
            }
            _reaperFlattenQueue.Enqueue(Account.Name);
            return true;
        }

        private bool EnqueueReaperMasterNakedStop(Position masterPos, int masterActualQty, string masterExpectedKey, DateTime masterFirstSeen)
        {
            if ((DateTime.UtcNow - masterFirstSeen).TotalSeconds >= ((NakedPositionGraceSec >= 5) ? NakedPositionGraceSec : 5))
            {
                bool alreadyNakedInFlight;
                alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(masterExpectedKey);
                if (!alreadyNakedInFlight)
                {
                    _reaperNakedStopInFlight.TryAdd(masterExpectedKey, 0);
                    Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                        Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
                    _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// V12.17 FIX: Processes queued flatten requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// This is the SAFE way to call Account.Flatten() -- same pattern as IPC.
        /// </summary>
        private void ProcessReaperFlattenQueue()
        {
            string accountName;
            while (_reaperFlattenQueue.TryDequeue(out accountName))
            {
                try
                {
                    Account targetAcct = ProcessReaperFlatten_FindAccount(accountName);

                    if (targetAcct != null)
                    {
                        ProcessReaperFlatten_CancelWorkingOrders(targetAcct, accountName);
                        ProcessReaperFlatten_ClosePositions(targetAcct, accountName);
                        ProcessReaperFlatten_TerminateFsms(accountName);
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
                finally
                {
                    _reaperFlattenInFlight.TryRemove(accountName + "_" + Instrument.FullName, out _);
                }
            }
        }

        private Account ProcessReaperFlatten_FindAccount(string accountName)
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

            return targetAcct;
        }

        private void ProcessReaperFlatten_CancelWorkingOrders(Account targetAcct, string accountName)
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
                foreach (Order orderToCancel in ordersToCancel)
                {
                    CancelOrderOnAccount(orderToCancel, targetAcct);
                }
                Print($"[REAPER] Emergency Cancel: {ordersToCancel.Count} orders on {accountName}");
            }
        }

        private void ProcessReaperFlatten_ClosePositions(Account targetAcct, string accountName)
        {
            // 2. Proactively close positions via unmanaged market orders
            foreach (Position position in targetAcct.Positions)
            {
                if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
                {
                    continue;
                }

                int qty = position.Quantity;
                string signalName = "ReaperFlatten_" + position.MarketPosition.ToString();

                if (targetAcct == this.Account)
                {
                    // Master Account
                    if (position.MarketPosition == MarketPosition.Long)
                    {
                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", signalName);
                    }
                    else
                    {
                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", signalName);
                    }
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
        }

        private void ProcessReaperFlatten_TerminateFsms(string accountName)
        {
            // Build 1004: SetExpectedPositionLocked(0) removed -- FSM termination is the sole teardown.
            // expectedPositions write is vestigial once FSM is the authority source.
            TerminateFsmsForAccount(accountName);
        }

        #endregion
    }
}
