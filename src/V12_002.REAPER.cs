// V12.17 THREADING FIX: Reaper (Safety Hub) Module
// REAPER Module (Extracted)
// FIX: acct.Flatten() calls moved from background thread -> strategy thread via TriggerCustomEvent
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

        // V12.17: Queue for flatten requests marshaled from background thread -> strategy thread
        private ConcurrentQueue<string> _reaperFlattenQueue = new ConcurrentQueue<string>();

        // V12.Phase8.2: Queue for repair requests marshaled from background thread -> strategy thread
        private ConcurrentQueue<string> _reaperRepairQueue = new ConcurrentQueue<string>();
        // V12.Phase8.2: Prevents double-repair for the same account while an order is in-flight
        private readonly HashSet<string> _repairInFlight = new HashSet<string>();

        // Build 1102R: Queue for naked-position emergency stop requests (background -> strategy thread)
        private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
            = new ConcurrentQueue<(string, MarketPosition, int)>();
        // Build 1102R: Prevents duplicate emergency stops while broker confirmation is pending (mirrors _repairInFlight)
        private readonly HashSet<string> _reaperNakedStopInFlight = new HashSet<string>();

        // GHOST-FIX-2 [Build 922Z]: Tracks when an account first appeared as "naked" (position with no working stop).
        // REAPER only fires emergency stop after NakedPositionGraceSec have elapsed, preventing race-condition
        // triggers during the normal bracket-confirmation window immediately after a fill.
        private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // [922Z-THROTTLE]: Prevents "Repair BLOCKED" from printing every second during intentional long-sitting orders.
        // Key = blocking order name; Value = last time the message was printed.
        private ConcurrentDictionary<string, DateTime> _repairBlockedLastLogged
            = new ConcurrentDictionary<string, DateTime>();

        // Build 935 [REAPER-B935-002]: Per-account fill-grace timestamps.
        // Replaces single global _lastExpectedPositionSetTicks which incorrectly blocked ALL account repairs
        // whenever ANY account had a fill. Now each account tracks its own fill-grace window independently.
        private readonly ConcurrentDictionary<string, long> _accountFillGraceTicks
            = new ConcurrentDictionary<string, long>();

        /// <summary>Build 946: Track consecutive failed repair attempts per account where PositionInfo is missing.</summary>
        private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount = new ConcurrentDictionary<string, int>();

        // Stamps per-account fill grace. Call from SetExpectedPositionLocked when applying a non-zero delta.
        private void StampAccountFillGrace(string expKey)
        {
            _accountFillGraceTicks[expKey] = DateTime.UtcNow.Ticks;
        }

        private bool IsReaperFillGraceActive(string expKey)
        {
            if (_accountFillGraceTicks.TryGetValue(expKey, out long stampTicks))
                return stampTicks > 0 && (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;
            // Fallback: check legacy global stamp (covers master account path)
            long globalStamp = Interlocked.Read(ref _lastExpectedPositionSetTicks);
            return globalStamp > 0 && (DateTime.UtcNow.Ticks - globalStamp) < ReaperFillGraceTicks;
        }


        private bool TryGetRepairDistanceLimitPoints(out double limitPoints)
        {
            limitPoints = 0;
            double atrLimit = CalculateATRStopDistance(RMAStopATRMultiplier);
            if (atrLimit <= 0)
                atrLimit = MinimumStop;

            double fenceLimit = (RepairTickFence > 0 && tickSize > 0)
                ? RepairTickFence * tickSize
                : atrLimit;

            limitPoints = Math.Min(Math.Abs(atrLimit), Math.Abs(fenceLimit));
            return limitPoints > 0;
        }

        /// <summary>
        /// V12 SIMA: Start the Reaper audit background thread
        /// </summary>
        private void StartReaperAudit()
        {
            if (isReaperRunning) return;

            isReaperRunning = true;
            reaperThread = new Thread(ReaperLoop)
            {
                IsBackground = true,
                Name = "V12_Reaper_Audit"
            };
            reaperThread.Start();
            Print("[REAPER] Audit thread STARTED - interval: " + ReaperIntervalMs + "ms");
        }

        /// <summary>
        /// V12 SIMA: Stop the Reaper audit background thread
        /// </summary>
        private void StopReaperAudit()
        {
            if (!isReaperRunning) return;

            isReaperRunning = false;
            try
            {
                if (reaperThread != null && reaperThread.IsAlive)
                {
                    reaperThread.Join(2000); // Wait up to 2 seconds
                }
            }
            catch { }
            Print("[REAPER] Audit thread STOPPED");
        }

        /// <summary>
        /// V12 SIMA: Reaper main loop - audits positions every ReaperIntervalMs
        /// </summary>
        private void ReaperLoop()
        {
            Print("[REAPER] Loop started - monitoring account positions...");

            // V12.Phase8 [F-05]: On cold start or reload the isFlattenRunning guard resets to false
            // even if a broker-side flatten is still in flight from the previous strategy instance.
            // Skip the very first audit cycle to allow any in-flight flatten to settle before
            // Reaper evaluates position state. This prevents false CRITICAL DESYNC on reload.
            bool firstCycle = true;

            while (isReaperRunning)
            {
                try
                {
                    Thread.Sleep(ReaperIntervalMs);
                    if (!isReaperRunning) break;

                    // V12.Phase8 [F-05]: Skip first cycle after startup -- grace period for in-flight flattens.
                    if (firstCycle)
                    {
                        firstCycle = false;
                        Print("[REAPER] Startup grace: skipping first audit cycle to allow in-flight flattens to settle.");
                        continue;
                    }

                    // V12.8: Pause auditing while a flatten is actively running to prevent race conditions
                    if (isFlattenRunning) continue;

                    // [BUILD 948] Skip audit until working orders have been re-adopted after restart/reconnect.
                    // Prevents false CRITICAL DESYNC or naked-position alerts during the adoption window.
                    if (!_orderAdoptionComplete) continue;

                    // V12.Hardening: Only audit in live/realtime -- skip historical replay
                    if (State != State.Realtime) continue;

                    AuditApexPositions();
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Print("[REAPER] ERROR: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// V12 SIMA: Audit all Apex account positions for desync
        /// If any account has a position that doesn't match expected, log it
        /// </summary>
        private void AuditApexPositions()
        {
            // Build 951.2: Sweep stale bracket replace specs before audit.
            SweepStaleBracketReplaceSpecs();

            bool shouldLog = (DateTime.UtcNow - lastReaperLog).TotalSeconds >= 30;
            int auditedCount = 0;
            int activeCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    auditedCount++;
                    if (AuditSingleFleetAccount(acct, shouldLog)) activeCount++;
                }
            }

            // V12.12: Explicitly audit the Master account if not covered by the prefix filter.
            bool masterAudited = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
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
            lock (stateLock)
            {
                expectedPositions.TryGetValue(expectedKey, out expectedQty);
                syncPending = _dispatchSyncPendingExpKeys.Contains(expectedKey);
            }
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
                    lock (stateLock) { alreadyInFlight = _repairInFlight.Contains(repairKey); }

                    if (!alreadyInFlight)
                    {
                        bool hasWorkingEntry = false;
                        string blockingOrderName = null;
                        OrderState blockingState = OrderState.Unknown;
                        Dictionary<string, PositionInfo> activeSnapshot;
                        lock (stateLock) { activeSnapshot = new Dictionary<string, PositionInfo>(activePositions); }

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
                            _reaperRepairQueue.Enqueue(acct.Name);
                            try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); } catch { }
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
                        try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                    }
                }
                else if (shouldLog)
                    Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
            }

            // Build 951.5: Unified helper replaces inline duplicate FSM check.
            if (IsBracketMoveInFlight(acct.Name))
            {
                if (shouldLog)
                    Print(string.Format("[REAPER] FSM in-flight for {0} -- suppressing naked check.", acct.Name));
                _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
                return hasState;
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
                        lock (stateLock) { alreadyNakedInFlight = _reaperNakedStopInFlight.Contains(acct.Name); }
                        if (!alreadyNakedInFlight)
                        {
                            lock (stateLock) { _reaperNakedStopInFlight.Add(acct.Name); }
                            Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                            catch (Exception tcEx) { Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed: {0}", tcEx.Message)); }
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
            lock (stateLock) { expectedPositions.TryGetValue(ExpKey(Account.Name), out masterExpectedQty); }

            bool hasState = masterExpectedQty != 0 || masterActualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");

            if (masterExpectedQty != masterActualQty)
            {
                // Build 951.5: Unified guard suppresses desync repairs while FSM is active.
                if (IsBracketMoveInFlight(Account.Name))
                {
                    if (shouldLog) Print($"[REAPER] FSM in-flight for {Account.Name} (Master) -- suppressing desync check.");
                    return hasState;
                }

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
                            try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
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

                        // V12.1101E [F-06]: Serialize expectedPositions mutation under stateLock.
                        // Build 1102U [BUG-1]: Composite key for instrument-scoped clear.
                        SetExpectedPositionLocked(ExpKey(accountName), 0);
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

        /// <summary>
        /// V12.Phase8.2: Processes queued repair requests on the strategy thread.
        /// Re-issues the original entry order for a desynced follower account.
        /// Build 935: Per-repair logic extracted to ExecuteReaperRepair (CS-R1140 compliance).
        /// </summary>
        private void ProcessReaperRepairQueue()
        {
            string accountName;
            while (_reaperRepairQueue.TryDequeue(out accountName))
                ExecuteReaperRepair(accountName);
        }

        // Build 935 [REAPER-B935-005]: Single-repair body extracted from ProcessReaperRepairQueue.
        // Threading: runs on strategy thread (via TriggerCustomEvent). All stateLock usages unchanged.
        private void ExecuteReaperRepair(string accountName)
        {
            string repairKey = accountName + "_" + Instrument.FullName;
            try
            {
                // 1. Find the stored PositionInfo for this account in activePositions
                PositionInfo repairPos = null;
                string repairEntryName = null;
                foreach (var kvp in activePositions.ToArray())
                {
                    PositionInfo pi = kvp.Value;
                    if (pi.IsFollower && pi.ExecutingAccount != null
                        && pi.ExecutingAccount.Name == accountName)
                    {
                        repairPos = pi;
                        repairEntryName = kvp.Key;
                        break;
                    }
                }

                if (repairPos == null)
                {
                    int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
                    Print(string.Format("[REAPER REPAIR] x No PositionInfo found for {0} -- cannot repair. (orphan attempt {1}/3)",
                        accountName, orphanCount));

                    if (orphanCount >= 3)
                    {
                        Print(string.Format("[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
                            accountName));
                        // SetExpectedPositionLocked(..., 0) already removes from _dispatchSyncPendingExpKeys internally.
                        SetExpectedPositionLocked(ExpKey(accountName), 0);
                        _reaperOrphanRepairCount.TryRemove(accountName, out _);
                    }
                    return;
                }

                // Clear orphan counter on successful PositionInfo resolution
                _reaperOrphanRepairCount.TryRemove(accountName, out _);

                OrderType repairOrderType = repairPos.EntryOrderType;
                double repairEntryPrice = Instrument.MasterInstrument.RoundToTickSize(repairPos.EntryPrice);
                double repairLimitPrice = 0;
                double repairStopPrice = 0;

                if (repairOrderType == OrderType.Limit)
                {
                    repairLimitPrice = repairEntryPrice;
                }
                else if (repairOrderType == OrderType.StopMarket)
                {
                    repairStopPrice = repairEntryPrice;
                }
                else if (repairOrderType == OrderType.StopLimit)
                {
                    repairLimitPrice = repairEntryPrice;
                    repairStopPrice = repairEntryPrice;
                }

                // Build 935: hard risk gate for ALL repair order types.
                // Repairs must remain inside the tighter of ATR-derived distance and tick fence distance.
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (currentPrice <= 0)
                {
                    Print($"[REAPER] REPAIR BLOCKED: invalid currentPrice={currentPrice:F4} for {accountName}.");
                    return;
                }

                if (!TryGetRepairDistanceLimitPoints(out double repairLimitPoints))
                {
                    Print($"[REAPER] REPAIR BLOCKED: unable to derive repair distance bound for {accountName}.");
                    return;
                }

                double hardBoundDiff = Math.Abs(currentPrice - repairEntryPrice);
                if (hardBoundDiff > repairLimitPoints)
                {
                    Print($"[REAPER] REPAIR BLOCKED: {accountName} {repairOrderType} exceeds hard bound. " +
                          $"Current={currentPrice:F2}, Entry={repairEntryPrice:F2}, Diff={hardBoundDiff:F4} > Limit={repairLimitPoints:F4}.");
                    return;
                }

                // 2. Safety Fence: enforce only when repair submits a Market order.
                if (repairOrderType == OrderType.Market)
                {
                    // Legacy market-fence check retained as a secondary guard.
                    double priceDiff = Math.Abs(currentPrice - repairEntryPrice);
                    double fenceDistance = RepairTickFence * tickSize;

                    if (priceDiff > fenceDistance)
                    {
                        Print($"[REAPER] REPAIR BLOCKED: Price fence exceeded for {accountName}. " +
                              $"Current={currentPrice:F2}, Entry={repairEntryPrice:F2}, " +
                              $"Diff={priceDiff:F4} > Fence={fenceDistance:F4} ({RepairTickFence} ticks). " +
                              $"Adjust RepairTickFence if you want to force entry.");
                        return;
                    }
                }

                // 3. Resolve account object
                Account targetAcct = repairPos.ExecutingAccount;
                if (targetAcct == null)
                {
                    Print($"[REAPER REPAIR] \u2717 ExecutingAccount is null for {accountName}");
                    return;
                }

                // 4. Mark in-flight to prevent double-repair
                lock (stateLock) { _repairInFlight.Add(repairKey); }

                try  // Build 940 [FIX-2]: Inner try/finally guarantees _repairInFlight cleanup on all exit paths.
                {
                // 5. Re-issue entry order using the SIMA acct.CreateOrder + acct.Submit pattern
                OrderAction action = repairPos.Direction == MarketPosition.Long
                    ? OrderAction.Buy : OrderAction.SellShort;
                int quantity = repairPos.TotalContracts;
                string repairSignal = repairEntryName;

                Order repairEntry = targetAcct.CreateOrder(
                    Instrument,
                    action,
                    repairOrderType,
                    TimeInForce.Gtc,
                    quantity,
                    repairLimitPrice,
                    repairStopPrice,
                    "",
                    repairSignal,
                    null);

                if (repairEntry == null)
                {
                    Print($"[REAPER REPAIR] \u2717 CreateOrder returned null for {accountName}");
                    return;
                }

                // V12.Phase8.2 [RACE-GUARD]: Re-verify expectedPositions immediately before order submission.
                int currentExpected = 0;
                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(accountName), out currentExpected); }
                if (currentExpected == 0)
                {
                    Print($"[REAPER REPAIR] (!) RACE GUARD ABORT for {accountName}: " +
                          $"expectedPositions cleared to 0 while repair was in queue. Discarding repair order.");
                    return;
                }

                lock (stateLock)
                {
                    repairPos.BracketSubmitted = false;
                    entryOrders[repairEntryName] = repairEntry;
                }

                targetAcct.Submit(new[] { repairEntry });

                Print($"[REAPER REPAIR] \u2713 Repair order submitted for {accountName} under key={repairEntryName}: " +
                      $"{action} {quantity} {repairOrderType} " +
                      $"{(repairOrderType == OrderType.Market ? "@ Market" : "@ " + repairEntryPrice.ToString("F2"))} " +
                      $"(original entry={repairEntryPrice:F2})");

                // 6. Clear DESYNC chart label
                try
                {
                    string desyncTag = "SIMA_DESYNC_" + accountName;
                    RemoveDrawObject(desyncTag);
                }
                catch { }
                }
                finally
                {
                    // 7. Clear in-flight flag -- guaranteed on all exit paths (return, throw, or normal).
                    lock (stateLock) { _repairInFlight.Remove(repairKey); }
                }
            }
            catch (Exception ex)
            {
                Print($"[REAPER REPAIR] \u2717 FAILED for {accountName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Build 1102R: Processes queued naked-position emergency stop requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// Submits a StopMarket order at MaximumStop ticks from current close to protect the naked position.
        /// </summary>
        private void ProcessReaperNakedStopQueue()
        {
            while (_reaperNakedStopQueue.TryDequeue(out var item))
            {
                try
                {
                    Account acct = Account.All.FirstOrDefault(a => a.Name == item.AccountName);
                    if (acct == null)
                    {
                        Print(string.Format("[REAPER][NAKED_STOP] Account {0} not found -- skipping.", item.AccountName));
                        continue;
                    }

                    // Compute emergency stop price: MaximumStop ticks from current close.
                    // Close[0] is safe here -- ProcessReaperNakedStopQueue runs on strategy thread
                    // via TriggerCustomEvent.
                    double emergencyStopDist = MaximumStop;
                    double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
                    if (atrBound > 0)
                        emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
                    if (emergencyStopDist <= 0)
                        emergencyStopDist = Math.Max(tickSize, MinimumStop);

                    double stopPrice;
                    OrderAction closeAction;

                    if (item.Direction == MarketPosition.Long)
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] - emergencyStopDist);
                        closeAction = OrderAction.Sell;
                    }
                    else
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] + emergencyStopDist);
                        closeAction = OrderAction.BuyToCover;
                    }

                    string signalName = "EMERGENCY_STOP_" + item.AccountName;
                    Order emergencyStop = acct.CreateOrder(
                        Instrument, closeAction, OrderType.StopMarket,
                        TimeInForce.Gtc, item.Qty,
                        0, stopPrice, "", signalName, null);

                    acct.Submit(new[] { emergencyStop });

                    // BUG-M2: Clear in-flight guard after successful submission
                    lock (stateLock) { _reaperNakedStopInFlight.Remove(item.AccountName); }
                    Print(string.Format(
                        "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2} (Dist={4:F2})",
                        item.AccountName, closeAction, item.Qty, stopPrice, emergencyStopDist));
                }
                catch (Exception ex)
                {
                    // BUG-M2: Clear in-flight guard on failure so next cycle can retry
                    lock (stateLock) { _reaperNakedStopInFlight.Remove(item.AccountName); }
                    Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
                }
            }
        }

        // Build 951.2: Bailout for orphaned _bracketReplaceSpecs entries.
        // If a broker never confirms a cancellation (dropped ACK, reconnect), the spec stays
        // indefinitely, keeping REAPER suppression active. 15s is generous enough to survive
        // normal broker latency but short enough to unblock REAPER before a naked-position
        // alert fires (REAPER uses a 30s grace period via _nakedPositionFirstSeen).
        private void SweepStaleBracketReplaceSpecs()
        {
            DateTime now = DateTime.Now;
            foreach (var kvp in _bracketReplaceSpecs.ToArray())
            {
                if ((now - kvp.Value.CreatedTime).TotalSeconds > 15)
                {
                    if (_bracketReplaceSpecs.TryRemove(kvp.Key, out _))
                    {
                        Print(string.Format(
                            "[MOVE-SYNC] WARN: Bracket_{0} spec TIMEOUT (>15s) -- clearing stale FSM entry. REAPER suppression lifted.",
                            kvp.Key));
                    }
                }
            }
        }

        // Build 951.5: Unified FSM-in-flight guard. Checks both fleet bracket FSM and legacy stop replacement.
        // Replaces duplicate inline checks across REAPER audit paths.
        private bool IsBracketMoveInFlight(string accountName)
        {
            foreach (var kvp in _bracketReplaceSpecs)
            {
                if (kvp.Value.ExecutingAccount != null
                    && string.Equals(kvp.Value.ExecutingAccount.Name, accountName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (var kvp in pendingStopReplacements)
            {
                PositionInfo fsmPos;
                if (activePositions.TryGetValue(kvp.Key, out fsmPos) && fsmPos != null)
                {
                    if (fsmPos.ExecutingAccount != null
                        && string.Equals(fsmPos.ExecutingAccount.Name, accountName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    // Build 952.1: Master positions have ExecutingAccount = null; match by Account.Name directly.
                    if (!fsmPos.IsFollower
                        && string.Equals(Account.Name, accountName, StringComparison.OrdinalIgnoreCase))
                    {
                        Print(string.Format("[REAPER] FSM in-flight for {0} (Master) -- suppressing desync check.", accountName));
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
