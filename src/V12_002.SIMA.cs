// V12.12 FLEET SYMMETRY & SAFETY HARDENING - Single-Instance Multi-Account Copy Trading Engine
// SIMA Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;
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
        /// <summary>
        /// V12 SIMA: Helper struct to rank accounts by Daily P/L
        /// </summary>
        private struct AccountRankInfo
        {
            public Account Account;
            public double DailyPL;
            public string Name;
        }

        /// <summary>
        /// V12.Phase8 [F-01/F-02]: Staging struct for target orders -- committed to tracking dicts only after Submit succeeds.
        /// </summary>
        private struct StagedTarget
        {
            public int Num;
            public double Price;
            public Order Order;
        }

        /// <summary>
        /// Build 936 [FIX-1]: Self-contained unit for deferred acct.Submit() via TriggerCustomEvent pump.
        /// Created in ExecuteSmartDispatchEntry setup phase (fast path); consumed by PumpFleetDispatch
        /// on the strategy thread one-at-a-time, breaking the 7-second monolithic blocking window into
        /// N x (next-tick-cycle) slices.
        /// </summary>
        private struct FleetDispatchRequest
        {
            public Account Account;
            public Order[] Orders;
            public string FleetEntryName;
            public string ExpectedKey;
            public int ReservedDelta;
        }

        /// <summary>
        /// [STRESS_TEST Phase 9.0] When true, OnAccountExecutionUpdate injects duplicate execution events
        /// into _accountExecutionQueue to validate the EntryFilled dedup guard under high-message density.
        /// Default: false -- must be manually enabled for stress testing only. Never enable in production.
        /// </summary>
        private bool isStressTestEnabled = false;

        // V12.1101E [F-06]: Serialize expectedPositions mutations so Reaper never observes partial state.
        private void AddExpectedPositionDeltaLocked(string accountName, int delta)
        {
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            lock (stateLock)
            {
                int oldVal = 0;
                expectedPositions.TryGetValue(accountName, out oldVal);
                int newVal = oldVal + delta;
                expectedPositions[accountName] = newVal;
                // [Phase 8.2 Part 3 - ACCOUNT_SYNC] Trace every mutation for desync audits.
                Print($"[ACCOUNT_SYNC] {accountName} expected: {oldVal} -> {newVal}");
            }
            if (delta != 0)
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
        }

        // V12.1101E [F-06]: Shared AddOrUpdate wrapper with stateLock serialization.
        private void AddOrUpdateExpectedPositionLocked(string accountName, int addValue, Func<int, int> updateExisting)
        {
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null || updateExisting == null) return;
            lock (stateLock)
            {
                expectedPositions.AddOrUpdate(accountName, addValue, (k, v) => updateExisting(v));
            }
        }

        // V12.1101E [F-06]: Serialized set for expectedPositions.
        private void SetExpectedPositionLocked(string accountName, int value)
        {
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            lock (stateLock)
            {
                expectedPositions[accountName] = value;
                if (value == 0)
                    _dispatchSyncPendingExpKeys.Remove(accountName);
            }
            // REAP-01: Stamp timestamp when a position is reserved so REAPER can apply
            // a grace window and avoid false "Critical Desync" during the broker-confirm lag.
            // Build 935 [REAPER-B935-002]: Also stamp per-account dictionary for scoped grace.
            if (value != 0)
            {
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
                StampAccountFillGrace(accountName);
            }
        }

        // Build 930.1 [P1]: Delta rollback for cascade cancellations.
        // Subtracts or adds the cancelled entry's quantity to the signed total.
        // Preserves expected position for other active entries on the same account.
        private void DeltaExpectedPositionLocked(string accountName, int delta)
        {
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            lock (stateLock)
            {
                int current;
                expectedPositions.TryGetValue(accountName, out current);
                int updated = current + delta;
                expectedPositions[accountName] = updated;
                Print($"[ACCOUNT_SYNC] {accountName} expected delta: {current} + ({delta}) = {updated}");
            }
            if (delta != 0)
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
        }

        private void MarkDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return;
            lock (stateLock) { _dispatchSyncPendingExpKeys.Add(expectedKey); }
        }

        private void ClearDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return;
            lock (stateLock) { _dispatchSyncPendingExpKeys.Remove(expectedKey); }
        }

        private bool IsDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return false;
            lock (stateLock) { return _dispatchSyncPendingExpKeys.Contains(expectedKey); }
        }

        /// <summary>
        /// 1102Z-C [RR-2b]: Stamp _lastExpectedPositionSetTicks to open a fresh 5-second REAPER grace window.
        /// Call before any follower entry order mutation (Change or Cancel) during a price-move propagation.
        /// Does NOT mutate expectedPositions ??" position is already reserved; only the price is moving.
        /// Thread-safe: Interlocked.Exchange is lock-free.
        /// </summary>
        private void StampReaperMoveGrace()
        {
            Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
        }

        // Build 1102U [BUG-1]: Composite key for expectedPositions.
        // Prevents cross-instrument state collision when multiple chart instances trade different
        // instruments (e.g. MES + MCL) on the same account fleet. REAPER was reading MCL's expected
        // quantity on the MES chart, triggering an infinite repair loop of rejected orders.
        // ALL expectedPositions reads and writes MUST use this helper instead of bare acct.Name.
        private string ExpKey(string acctName)
        {
            return acctName + "_" + Instrument.FullName;
        }

        /// <summary>
        /// V12 SIMA: Returns the list of Apex accounts sorted by Daily P/L (Lowest to Highest)
        /// </summary>
        private List<AccountRankInfo> GetSortedAccountFleet()
        {
            List<AccountRankInfo> fleet = new List<AccountRankInfo>();

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                    fleet.Add(new AccountRankInfo { Account = acct, DailyPL = dailyPL, Name = acct.Name });
                }
            }

            // Sort by P/L ascending (Lowest P/L first)
            return fleet.OrderBy(a => a.DailyPL).ToList();
        }

        #region V12 SIMA Multi-Account Execution Engine

        /// <summary>
        /// V12 SIMA: Execute a Smart Dispatched trade across the fleet.
        /// Logic:
        ///   - Signal = TREND: Lowest P/L account gets TREND targets, others get RMA targets.
        ///   - Signal = RMA/OR/MOMO: All accounts get RMA targets.
        /// Accounts use FIXED brackets (Path B) for zero trail lag.
        /// </summary>
        private void ExecuteSmartDispatchEntry(string tradeType, OrderAction action, int quantity, double entryPrice, OrderType entryOrderType = OrderType.Market, params string[] masterEntryNames)
        {
            // V12.Phase8 [F-03]: Semaphore guard to prevent racing with SIMA lifecycle changes (ApplySimaState).
            if (!_simaToggleSem.Wait(200))
            {
                Print("[DISPATCH] (!) Semaphore timeout -- skipping dispatch to avoid SIMA lifecycle race");
                return;
            }

            // [Phase 7.2 LATENCY] T0: Start immediately after semaphore acquired, before any work.
            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;

            try
            {
                // V12.2: Diagnostic logging for copy trading troubleshooting
                Print($"[DISPATCH] ExecuteSmartDispatchEntry called: {tradeType} | EnableSIMA={EnableSIMA} | OrderType={entryOrderType}");

                if (!EnableSIMA)
                {
                    Print("[DISPATCH] ?????? SIMA DISABLED - Enable in strategy parameters to copy trade");
                    return;
                }

                // EMERGENCY FIX [H-12]: Abort dispatch if flatten is in progress to prevent re-entry race.
                if (isFlattenRunning)
                {
                    Print("[DISPATCH] (!) Aborting dispatch -- flatten in progress (isFlattenRunning=true)");
                    return; // finally block at line 414 releases _simaToggleSem
                }

                List<AccountRankInfo> fleet = GetSortedAccountFleet();

                // V12.Audit [Q3-002]: Snapshot fleet active state under stateLock to prevent UI race.
                // The UI/IPC thread can toggle activeFleetAccounts between TryGetValue and Submit,
                // so we capture a consistent set of active account names once before the dispatch loop.
                HashSet<string> activeAccountSnapshot;
                // FIX-B [Build 1102Z]: Snapshot activeTargetCount atomically with the fleet snapshot.
                // The IPC SET_TARGET_COUNT command writes activeTargetCount on the TCP listener thread,
                // so a live read inside the fleet loop (line below) can produce a different bound for
                // different accounts. Capturing once here ensures all fleet accounts submit identical
                // target counts for this dispatch.
                int dispatchTargetCount;
                lock (stateLock)
                {
                    activeAccountSnapshot = new HashSet<string>(
                        activeFleetAccounts
                            .Where(kvp => kvp.Value)
                            .Select(kvp => kvp.Key));
                    dispatchTargetCount = Math.Max(1, Math.Min(5, activeTargetCount));
                }

                // V12.2: Log fleet state for diagnostics
                int activeCount = activeAccountSnapshot.Count;
                Print($"[DISPATCH] Fleet: {fleet.Count} total accounts | {activeCount} ACTIVE in Fleet Manager");

                if (fleet.Count == 0)
                {
                    Print("[DISPATCH] ?????? NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");
                    return;
                }

                if (activeCount == 0)
                {
                    Print("[DISPATCH] ?????? NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");
                }

                int rmaCount = 0;
                string symmetryDispatchId = SymmetryGuardBeginDispatch(tradeType, action, quantity, entryPrice);
                if (masterEntryNames != null)
                {
                    foreach (string masterEntryName in masterEntryNames)
                    {
                        if (!string.IsNullOrEmpty(masterEntryName))
                            SymmetryGuardRegisterMasterEntry(symmetryDispatchId, masterEntryName);
                    }
                }

                // [Phase 7.2 LATENCY] T_LoopStart + batch log buffer (flushed once after loop).
                long tLoopStartTicks = sw.ElapsedTicks;
                var dispatchLog = new StringBuilder(512);
                dispatchLog.AppendLine($"[LATENCY] Loop start at {(tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency:F3} ms from entry");

                for (int i = 0; i < fleet.Count; i++)
                {
                    Account acct = fleet[i].Account;

                    // V12.1: Skip Master account if its order was already placed by the caller
                    if (acct == this.Account) continue;

                    // Build 935 [SIMA-B935-001]: Inactive + H-13 + consistency lock delegated to ShouldSkipFleetAccount.
                    if (ShouldSkipFleetAccount(acct, fleet[i], activeAccountSnapshot, dispatchLog)) continue;

                    // V12: Followers ALWAYS use RMA multipliers for point-based trails (User Req)
                    bool useRmaForFollower = true;
                    MarketPosition followerDirection = action == OrderAction.Buy ? MarketPosition.Long : MarketPosition.Short;

                    // [LEAK-01]: Use centralized ATR calculator (ceiling + min/max guards, fleet-ready).
                    double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);

                    double stopPrice = (action == OrderAction.Buy) ? entryPrice - stopDist : entryPrice + stopDist;
                    // Universal Ladder: T(n)Type dropdown drives all target pricing.
                    double t1TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 1);
                    double t2TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 2);
                    double t3TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 3);
                    double t4TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 4);
                    double t5TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 5);

                    // Rounding
                    stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                    // V1102Q [PARITY-01]: Scale quantity for Micro accounts (e.g. ES->MES 10x parity)
                    // [923A-P2c-OVF]: checked{} prevents silent int overflow on parity multiply (cf. Callbacks.cs same pattern)
                    int followerQty;
                    try
                    {
                        followerQty = checked((int)Math.Max(1L, (long)quantity * FleetParityMultiplier));
                    }
                    catch (OverflowException)
                    {
                        Print($"[923A-OVF] SIMA parity overflow qty={quantity} x mult={FleetParityMultiplier} -- clamping to maxContracts ({maxContracts})");
                        followerQty = maxContracts;
                    }

                    // V12.40 FLEET PARITY: Use same distribution as Master (applied to scaled quantity)
                    // FIX-B [Build 1102Z]: Pass dispatchTargetCount snapshot so all fleet accounts use the same
                    // target count regardless of any IPC update that may arrive mid-dispatch.
                    int ft1, ft2, ft3, ft4, ft5;
                    GetTargetDistribution(followerQty, out ft1, out ft2, out ft3, out ft4, out ft5, dispatchTargetCount);

                    string ocoId = tradeType + "_" + DateTime.Now.Ticks + "_" + i;
                    string fleetEntryName = "Fleet_" + acct.Name + "_" + tradeType + "_" + i;
                    string expectedKey = ExpKey(acct.Name);
                    int reservedDelta = 0;
                    bool registeredForCleanup = false;
                    bool syncPending = false;
                    try
                    {
                        SymmetryGuardRegisterFollower(symmetryDispatchId, fleetEntryName);

                        // V12.3: Entry uses caller-specified order type (Limit for RMA, Market for MOMO/TREND)
                        // [FIX-PP-01]: For StopMarket/StopLimit entries the activation price lives in stopPrice,
                        // not limitPrice. Passing stopPx=0 caused the follower to fire immediately at market.
                        double limitPx = (entryOrderType == OrderType.Limit || entryOrderType == OrderType.StopLimit) ? entryPrice : 0;
                        double stopPx  = (entryOrderType == OrderType.StopMarket || entryOrderType == OrderType.StopLimit) ? entryPrice : 0;
                        bool isMarketEntry = (entryOrderType == OrderType.Market);
                        // StopMarket stays isMarketEntry=false: bracket handled by SymmetryGuardOnFollowerFill anchor flow.
                        Order entry = acct.CreateOrder(Instrument, action, entryOrderType, TimeInForce.Gtc, followerQty, limitPx, stopPx, ocoId, fleetEntryName, null);
                        if (entry == null)
                        {
                            dispatchLog.AppendLine($"[DISPATCH] Entry create failed on {acct.Name} for {fleetEntryName}");
                            continue;
                        }

                        // V12.1: Track follower position for active trailing/target management
                        // V12.1101E: Full 5-target distribution mirrors Master
                        PositionInfo fleetPos = new PositionInfo
                        {
                            SignalName = fleetEntryName,
                            Direction = action == OrderAction.Buy ? MarketPosition.Long : MarketPosition.Short,
                            TotalContracts = followerQty,
                            RemainingContracts = followerQty,
                            EntryPrice = entryPrice,
                            InitialStopPrice = stopPrice,
                            CurrentStopPrice = stopPrice,
                            Target1Price = t1TargetPrice,
                            Target2Price = t2TargetPrice,
                            Target3Price = t3TargetPrice,
                            Target4Price = t4TargetPrice,
                            Target5Price = t5TargetPrice,
                            T1Contracts = ft1,
                            T2Contracts = ft2,
                            T3Contracts = ft3,
                            T4Contracts = ft4,
                            T5Contracts = ft5,
                            ExecutingAccount = acct,
                            IsFollower = true,
                            IsRMATrade = true,          // Enforce Point-Based Trailing for all followers
                            IsTRENDTrade = (tradeType == "TREND"),
                            IsRetestTrade = (tradeType == "RETEST"),
                            EntryOrderType = entryOrderType,
                            EntryFilled = isMarketEntry, // V12.3: Only true for Market entries; Limit waits for fill
                            BracketSubmitted = isMarketEntry, // V12.7: Brackets deferred for Limit entries
                            TicksSinceEntry = 0,
                            ExtremePriceSinceEntry = entryPrice,
                            CurrentTrailLevel = 0,
                            // Build 936 [FIX-2]: Deterministic bracket OCO group ID for broker-native stop+target linking.
                            OcoGroupId = "V12_" + fleetEntryName.GetHashCode().ToString("X8"),
                        };

                        // V12.7: Submit only entry for Limit; market entries include stop + non-runner targets.
                        if (isMarketEntry)
                        {
                            var ordersToSubmit = new List<Order> { entry };
                            OrderAction exitAction = action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover;
                            double validatedStop = ValidateStopPrice(fleetPos.Direction, fleetPos.CurrentStopPrice);

                            string stopSig = SymmetryTrim("Stop_" + fleetEntryName, 40);
                            Order stop = acct.CreateOrder(
                                Instrument,
                                exitAction,
                                OrderType.StopMarket,
                                TimeInForce.Gtc,
                                Math.Max(1, fleetPos.TotalContracts),
                                0,
                                validatedStop,
                                ocoId,
                                stopSig,
                                null);

                            ordersToSubmit.Add(stop);

                            int nonRunnerLimitQty = 0;
                            int runnerQty = 0;
                            var stagedTargets = new List<StagedTarget>(5);

                            // V12.Phase8.3: Use activeTargetCount from dashboard to restrict number of targets submitted
                            // FIX-B [Build 1102Z]: Use dispatchTargetCount snapshot (captured before loop) ??" not live global.
                            for (int targetNum = 1; targetNum <= dispatchTargetCount; targetNum++)
                            {
                                int targetQty = GetTargetContracts(fleetPos, targetNum);
                                if (targetQty <= 0) continue;

                                if (IsRunnerTarget(targetNum))
                                {
                                    runnerQty += targetQty;
                                    continue;
                                }

                                double targetPrice = GetTargetPrice(fleetPos, targetNum);
                                if (targetPrice <= 0)
                                {
                                    dispatchLog.AppendLine($"[SIMA TARGET_SKIP] T{targetNum} for {fleetEntryName} has qty={targetQty} but invalid price={targetPrice:F2}; skipped");
                                    continue;
                                }

                                string targetSig = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);
                                Order target = acct.CreateOrder(
                                    Instrument,
                                    exitAction,
                                    OrderType.Limit,
                                    TimeInForce.Gtc,
                                    targetQty,
                                    targetPrice,
                                    0,
                                    ocoId,
                                    targetSig,
                                    null);

                                // V12.Phase8 [F-01/F-02]: Stage target orders locally; commit after Submit.
                                stagedTargets.Add(new StagedTarget { Num = targetNum, Price = targetPrice, Order = target });

                                ordersToSubmit.Add(target);
                                nonRunnerLimitQty += targetQty;
                            }

                            // Build 935: Register local dictionaries before reserve/submit so REAPER never
                            // observes Expected!=0 without entry/stop/targets tracking state.
                            lock (stateLock)
                            {
                                activePositions[fleetEntryName] = fleetPos;
                                entryOrders[fleetEntryName] = entry;
                                stopOrders[fleetEntryName] = stop;
                                foreach (var st in stagedTargets)
                                {
                                    var targetDict = GetTargetOrdersDictionary(st.Num);
                                    if (targetDict != null)
                                        targetDict[fleetEntryName] = st.Order;
                                }
                            }
                            registeredForCleanup = true;
                            MarkDispatchSyncPending(expectedKey);
                            syncPending = true;

                            // Build 935: Reserve follower-sized expected quantity only.
                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);

                            // [Build 936 FIX-1]: Enqueue for async TriggerCustomEvent pump instead of blocking Submit.
                            // Pump handler (PumpFleetDispatch) owns: Submit, ClearDispatchSyncPending, delta rollback, dict cleanup.
                            // Transfer ownership flags so the per-account catch block does not double-cleanup.
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = ordersToSubmit.ToArray(),
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta
                            });
                            syncPending         = false;
                            reservedDelta       = 0;
                            registeredForCleanup = false;

                            dispatchLog.AppendLine($"  QUEUE | {acct.Name,-28} | Market+{ordersToSubmit.Count}orders | PENDING");
                            dispatchLog.AppendLine($"[SIMA STOP_AUDIT] QUEUED {fleetEntryName}: StopQty={fleetPos.TotalContracts} NonRunnerLimits={nonRunnerLimitQty} RunnerQty={runnerQty}");
                        }
                        else
                        {
                            // V12.Phantom-Fix [FIX-1]: Register tracking dicts BEFORE updating expectedPositions.
                            // REAPER runs on a background thread; if it fires between the expectedPositions
                            // update and the dict commit (the old T1??'T3 race), it observes non-zero expected
                            // with no entry in entryOrders ??' hasWorkingEntry=false ??' phantom repair queued.
                            // Registering dicts first guarantees REAPER always finds the blocking entry.
                            lock (stateLock)
                            {
                                activePositions[fleetEntryName] = fleetPos;
                                entryOrders[fleetEntryName] = entry; // V12.3: Track entry for CIT chase
                            }
                            registeredForCleanup = true;
                            MarkDispatchSyncPending(expectedKey);
                            syncPending = true;

                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);

                            // [Build 936 FIX-1]: Enqueue for async TriggerCustomEvent pump instead of blocking Submit.
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = new[] { entry },
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta
                            });
                            syncPending         = false;
                            reservedDelta       = 0;
                            registeredForCleanup = false;

                            dispatchLog.AppendLine($"  QUEUE | {acct.Name,-28} | Limit        | PENDING");
                        }

                        rmaCount++;
                    }
                    catch (Exception ex)
                    {
                        if (syncPending)
                        {
                            ClearDispatchSyncPending(expectedKey);
                            syncPending = false;
                        }

                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);

                        if (registeredForCleanup)
                        {
                            // V12.Phase8 [F-01]: Full tracking-dict cleanup on Submit failure.
                            activePositions.TryRemove(fleetEntryName, out _);
                            entryOrders.TryRemove(fleetEntryName, out _);
                            stopOrders.TryRemove(fleetEntryName, out _);
                            for (int tNum = 1; tNum <= 5; tNum++)
                            {
                                var targetDict = GetTargetOrdersDictionary(tNum);
                                if (targetDict != null)
                                    targetDict.TryRemove(fleetEntryName, out _);
                            }
                        }

                        dispatchLog.AppendLine($"[DISPATCH] [X] FAILED on {acct.Name}: {ex.Message}");
                    }
                }

                // [Build 936 FIX-1]: Prime the TriggerCustomEvent pump - one account Submit per strategy-thread cycle.
                if (!_pendingFleetDispatches.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }

                // [Phase 7.2 LATENCY] T_Final: Fleet loop complete (setup+enqueue only; no blocking Submit) ??" stop clock, flush forensic report.
                sw.Stop();
                long tFinalTicks = sw.ElapsedTicks;
                double totalMs  = tFinalTicks        * 1000.0 / Stopwatch.Frequency;
                double setupMs  = (tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency;
                double loopMs   = (tFinalTicks - tLoopStartTicks) * 1000.0 / Stopwatch.Frequency;

                var report = new StringBuilder(1024);
                report.AppendLine("+==============================================================+");
                report.AppendLine("|          (+/-)  FORENSIC PULSE REPORT  Phase 7.2 Latency       |");
                report.AppendLine("+==============================================================+");
                report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   |   RTT  |");
                report.AppendLine("+==============================================================+");
                report.Append(dispatchLog.ToString());
                report.AppendLine("+==============================================================+");
                Print(report.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Print($"[DISPATCH] CRITICAL ERROR in ExecuteSmartDispatchEntry: {ex.Message}");
            }
            finally
            {
                // V12.Phase8 [F-03]: Always release the SIMA toggle semaphore.
                _simaToggleSem.Release();
            }
        }

        /// <summary>
        /// Build 936 [FIX-1]: Processes ONE pending fleet dispatch request per invocation.
        /// Called via TriggerCustomEvent -- always runs on the strategy thread (NT8 thread-safe).
        /// Separates acct.Submit() calls across strategy-thread cycles, eliminating the synchronous
        /// 7-second freeze caused by submitting the full fleet in one tight loop.
        /// Error handling mirrors ExecuteSmartDispatchEntry catch block: dict cleanup + delta rollback.
        /// </summary>
        private void PumpFleetDispatch()
        {
            if (!_pendingFleetDispatches.TryDequeue(out var req))
                return;

            bool syncCleared = false;
            try
            {
                req.Account.Submit(req.Orders);
                ClearDispatchSyncPending(req.ExpectedKey);
                syncCleared = true;
                Print($"[PUMP] Submitted {req.Orders.Length} orders for {req.FleetEntryName} | {req.Account.Name}");
            }
            catch (Exception ex)
            {
                Print($"[PUMP] Submit FAILED for {req.FleetEntryName} ({req.Account.Name}): {ex.Message}");
                if (!syncCleared)
                    ClearDispatchSyncPending(req.ExpectedKey);
                if (req.ReservedDelta != 0)
                    AddExpectedPositionDeltaLocked(req.ExpectedKey, -req.ReservedDelta);
                // Full tracking-dict cleanup -- mirrors ExecuteSmartDispatchEntry [F-01] catch block.
                activePositions.TryRemove(req.FleetEntryName, out _);
                entryOrders.TryRemove(req.FleetEntryName, out _);
                stopOrders.TryRemove(req.FleetEntryName, out _);
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(req.FleetEntryName, out _);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _pendingFleetDispatchCount);
                // Chain next pump cycle if more requests remain in the queue.
                if (!_pendingFleetDispatches.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
            }
        }

        // Build 935 [SIMA-B935-001]: Skip-logic extracted from ExecuteSmartDispatchEntry fleet loop.
        // Returns true if the account should be skipped for this dispatch cycle.
        // Threading: strategy thread only. stateLock usage identical to original inline code.
        private bool ShouldSkipFleetAccount(Account acct, AccountRankInfo rankInfo,
            System.Collections.Generic.HashSet<string> activeAccountSnapshot, System.Text.StringBuilder dispatchLog)
        {
            // Step 1: Inactive check -- prevents UI toggle race.
            if (!activeAccountSnapshot.Contains(acct.Name))
            {
                dispatchLog.AppendLine($"[SIMA] {acct.Name} SKIPPED (Inactive)");
                return true;
            }

            // Step 2: H-13 stale expectedPositions reconciliation.
            try
            {
                // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);
                int expected;
                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(acct.Name), out expected); }

                if (brokerFlat && Math.Abs(expected) > 0)
                {
                    bool hasPendingRepairOrder = false;
                    foreach (var kvp in entryOrders.ToArray())
                    {
                        var ord = kvp.Value;
                        if (ord != null && !IsOrderTerminal(ord.OrderState)
                            && activePositions.TryGetValue(kvp.Key, out var pos)
                            && pos.IsFollower && pos.ExecutingAccount != null
                            && pos.ExecutingAccount.Name == acct.Name)
                        { hasPendingRepairOrder = true; break; }
                    }

                    bool hasActivePositionForAcct = activePositions.Values.Any(
                        p => p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);

                    bool isMasterWaiting = false;
                    foreach (var kvp in entryOrders.ToArray())
                    {
                        if (activePositions.TryGetValue(kvp.Key, out var pi) && !pi.IsFollower && pi.ExecutingAccount == this.Account
                            && kvp.Value != null && (kvp.Value.OrderState == OrderState.Working
                                || kvp.Value.OrderState == OrderState.Submitted || kvp.Value.OrderState == OrderState.Accepted))
                        { isMasterWaiting = true; break; }
                    }

                    if (hasPendingRepairOrder || hasActivePositionForAcct || isMasterWaiting)
                        dispatchLog.AppendLine($"[DISPATCH] H-13 SKIP: {acct.Name} Flat but {(isMasterWaiting ? "Master working" : (hasPendingRepairOrder ? "repair in-flight" : "activePos present"))} -- not resetting");
                    else
                    {
                        SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                        dispatchLog.AppendLine($"[DISPATCH] H-13: Stale expectedPos cleared for {acct.Name} (broker Flat)");
                    }
                }
            }
            catch { }

            // Step 3: Consistency Lock -- skip if daily P&L cap hit.
            if (EnableConsistencyLock && rankInfo.DailyPL >= MaxDailyProfitCap)
            {
                dispatchLog.AppendLine($"[DISPATCH] {acct.Name} SKIPPED - Consistency Lock ({rankInfo.DailyPL:C})");
                return true;
            }

            return false;
        }


        /// <summary>
        /// V12.1101E [A-4]: Idempotent unsubscribe ??" removes all SIMA event handlers before
        /// re-subscribing. Prevents handler accumulation on repeated SIMA toggle cycles.
        /// V12.Phase6 [UNSUB-TRACK]: Deterministic unsubscribe ??" uses tracked set of subscribed accounts
        /// instead of re-scanning Account.All, which may have changed since subscribe time.
        /// </summary>
        private void UnsubscribeFromFleetAccounts()
        {
            // First: unsubscribe from tracked set (deterministic ??" guaranteed to match subscribe)
            foreach (string acctName in _subscribedAccountNames)
            {
                foreach (Account acct in Account.All)
                {
                    if (acct.Name == acctName)
                    {
                        acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                        acct.OrderUpdate     -= OnAccountOrderUpdate;
                        break;
                    }
                }
            }
            // Fallback: also sweep Account.All for any handlers from untracked subscribe paths
            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                    acct.OrderUpdate     -= OnAccountOrderUpdate;
                }
            }
            _subscribedAccountNames.Clear();
        }

        /// <summary>
        /// V12.Phase6 [LIFECYCLE]: Centralized SIMA state transition. Handles full lifecycle:
        /// enable ??' enumerate accounts + subscribe handlers + hydrate positions + start Reaper
        /// disable ??' stop Reaper + unsubscribe handlers + clear fleet state
        /// Replaces raw EnableSIMA flag toggles to prevent handler leaks and Reaper state mismatches.
        /// </summary>
        private void ApplySimaState(bool enabled)
        {
            // V12.Audit [H-10]: If a previous toggle timed out, attempt retry now.
            // We re-enter with the same `enabled` argument that was pending.
            // If the semaphore is still held this call will time out again, setting the flag once more.
            if (_simaTogglePending)
                Print("[SIMA LIFECYCLE] Retrying previously timed-out toggle (pending retry flag was set).");

            // V12.Phase7 [H-10]: Serialize enable/disable transitions to prevent race between
            // concurrent IPC commands and UI toggles leaving SIMA in a partially initialized state.
            if (!_simaToggleSem.Wait(500))
            {
                // V12.Audit [H-10]: Record that this toggle did not complete so the next caller can retry.
                _simaTogglePending = true;
                Print("[SIMA_WARN] ApplySimaState timed out waiting for semaphore -- toggle pending, retry.");
                return;
            }
            try
            {
                if (enabled)
                {
                    EnumerateApexAccounts(); // Unsubs first (idempotent), then re-subscribes + hydrates
                    if (ReaperAuditEnabled)
                        StartReaperAudit();
                    Print("[SIMA LIFECYCLE] SIMA ENABLED -- fleet enumerated, Reaper started");
                }
                else
                {
                    CancelAllV12GtcOrders(false); // [BUILD 948] GTC sweep before teardown -- skip accounts with open positions
                    StopReaperAudit();
                    UnsubscribeFromFleetAccounts();
                    Print("[SIMA LIFECYCLE] SIMA DISABLED -- Reaper stopped, handlers unsubscribed");
                }
                EnableSIMA = enabled;
                // V12.Audit [H-10]: Toggle completed successfully ??" clear any pending-retry flag.
                _simaTogglePending = false;
            }
            finally
            {
                _simaToggleSem.Release();
            }
        }

        private void EnumerateApexAccounts()
        {
            UnsubscribeFromFleetAccounts(); // V12.1101E [A-4]: Always unsub first ??" idempotent guard against handler accumulation
            simaAccountCount = 0;
            Print("[SIMA] ===================================================");
            Print("[SIMA] V12.12 - Fleet Symmetry & Safety Hardening Initializing");
            Print($"[SIMA] Account Prefix Filter: \"{AccountPrefix}\"");
            Print("[SIMA] ---------------------------------------------------");

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    simaAccountCount++;
                    SetExpectedPositionLocked(ExpKey(acct.Name), 0); // Initialize expected position as flat
                    accountDailyProfit[acct.Name] = 0; // Initialize daily profit
                    EnsureAccountComplianceTracking(acct.Name, GetComplianceNow());
                    activeFleetAccounts[acct.Name] = false; // V12.8 SIMA: Default to INACTIVE ??" wait for Fleet Manager / IPC to enable

                    // V12.7: Always subscribe to execution updates for fleet bracket management
                    // (Also used by ComplianceHub for P/L tracking)
                    acct.ExecutionUpdate += OnAccountExecutionUpdate;
                    acct.OrderUpdate += OnAccountOrderUpdate;
                    _subscribedAccountNames.Add(acct.Name); // V12.Phase6 [UNSUB-TRACK]: Track for deterministic unsubscribe
                    if (EnableComplianceHub)
                    {
                        Print($"[SIMA] [OK] {acct.Name} | COMPLIANCE MONITORING ACTIVE");
                    }
                    else
                    {
                        Print($"[SIMA] #{simaAccountCount}: {acct.Name} | Connected: {acct.Connection?.Status == ConnectionStatus.Connected} | Fleet: INACTIVE (awaiting IPC enable)");
                    }
                }
            }

            Print("[SIMA] ---------------------------------------------------");
            Print($"[SIMA] TOTAL ACCOUNTS DETECTED: {simaAccountCount} | ALL INACTIVE by default");
            Print("[SIMA] FLEET INACTIVE - MANUAL ENABLE REQUIRED"); // V12.Phase10 [DEFAULT-FIX]
            Print("[SIMA] ===================================================");

            // V12.Phase6 [HYDRATE]: Seed expectedPositions from live broker state
            HydrateExpectedPositionsFromBroker();

            // [BUILD 948] Adopt any working broker orders into tracking dicts; sets _orderAdoptionComplete = true
            HydrateWorkingOrdersFromBroker();
        }

        /// <summary>
        /// V12.Phase6 [HYDRATE]: Reads actual broker positions for each fleet account and seeds
        /// expectedPositions accordingly. Prevents false Reaper CRITICAL DESYNC alerts when the
        /// strategy restarts while accounts hold open positions.
        /// </summary>
        private void HydrateExpectedPositionsFromBroker()
        {
            int hydratedCount = 0;
            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;

                try
                {
                    // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                    foreach (Position pos in acct.Positions.ToArray())
                    {
                        if (pos != null && pos.Instrument != null
                            && pos.Instrument.FullName == Instrument.FullName
                            && pos.MarketPosition != MarketPosition.Flat)
                        {
                            int qty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                            // V12.Phase7 [M-10]: Use AddOrUpdate instead of direct assignment to prevent
                            // overwriting if called multiple times or during concurrent access.
                            AddOrUpdateExpectedPositionLocked(ExpKey(acct.Name), qty, v => qty);
                            Print($"[SIMA HYDRATE] {acct.Name}: Seeded expected={qty} from broker ({pos.MarketPosition} {pos.Quantity})");
                            hydratedCount++;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"[SIMA HYDRATE] WARNING: Could not read positions for {acct.Name}: {ex.Message}");
                }
            }
            if (hydratedCount > 0)
                Print($"[SIMA HYDRATE] Hydrated {hydratedCount} account(s) with live broker positions");
        }

        /// <summary>
        /// Build 948 [FIX-B]: Re-adopt working broker orders into tracking dicts after restart or reconnect.
        /// Derives the original entry key by stripping the well-known order-name prefix (e.g. "Stop_" -> stopOrders).
        /// Sets _orderAdoptionComplete = true when done so REAPER can resume auditing.
        /// MUST be called on the strategy thread (via TriggerCustomEvent when initiated from a callback).
        /// All dict writes are guarded by stateLock per the StateLock Rule.
        /// </summary>
        private void HydrateWorkingOrdersFromBroker()
        {
            int adoptedCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        // [Codex P2] Include all live in-flight states -- Submitted/ChangePending/ChangeSubmitted
                        // can be active during an in-flight FSM replace at reconnect time.
                        // Setting _orderAdoptionComplete=true while these are skipped leaves REAPER
                        // auditing against incomplete order tracking and can fire false repair cycles.
                        if (ord.OrderState != OrderState.Working    &&
                            ord.OrderState != OrderState.Accepted   &&
                            ord.OrderState != OrderState.Submitted  &&
                            ord.OrderState != OrderState.ChangePending &&
                            ord.OrderState != OrderState.ChangeSubmitted) continue;

                        string name = ord.Name ?? string.Empty;
                        ConcurrentDictionary<string, Order> targetDict = null;
                        string key     = null;
                        string dictName = null;

                        if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = stopOrders;   key = name.Substring(5); dictName = "stopOrders"; }
                        else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = stopOrders;   key = name.Substring(2); dictName = "stopOrders"; }
                        else if (name.StartsWith("T1_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target1Orders; key = name.Substring(3); dictName = "target1Orders"; }
                        else if (name.StartsWith("T2_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target2Orders; key = name.Substring(3); dictName = "target2Orders"; }
                        else if (name.StartsWith("T3_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target3Orders; key = name.Substring(3); dictName = "target3Orders"; }
                        else if (name.StartsWith("T4_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target4Orders; key = name.Substring(3); dictName = "target4Orders"; }
                        else if (name.StartsWith("T5_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target5Orders; key = name.Substring(3); dictName = "target5Orders"; }
                        // [Codex P1] Adopt Fleet_ prefixed follower entry orders into entryOrders.
                        // Without this, broker-resident follower entries are invisible after reconnect.
                        // ProcessQueuedExecution finds them by object ref in entryOrders, so a missed
                        // adoption means SymmetryGuardOnFollowerFill is bypassed and the new filled
                        // position launches without its protective bracket orders.
                        else if (name.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = entryOrders; key = name; dictName = "entryOrders"; }

                        if (targetDict == null || key == null) continue;

                        lock (stateLock) { targetDict[key] = ord; }
                        Print($"[SIMA HYDRATE] Adopted working order {name} into {dictName}");
                        adoptedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Print($"[SIMA HYDRATE] WARNING: Could not read orders for {acct.Name}: {ex.Message}");
                }
            }

            _orderAdoptionComplete = true;
            if (adoptedCount > 0)
                Print($"[SIMA HYDRATE] Adopted {adoptedCount} working order(s) from broker -- adoption complete.");
            else
                Print("[SIMA HYDRATE] No working orders to adopt -- adoption complete.");
        }

        /// <summary>
        /// Build 948 [FIX-A]: Sweep and cancel all V12-managed GTC orders before SIMA disable or strategy terminate.
        /// Phase 1 scans tracked order dicts; Phase 2 scans broker order lists for any V12-prefixed orders.
        /// force=true: cancel regardless of open positions (strategy terminate).
        /// force=false: skip accounts that have an open position for this instrument (SIMA disable -- prevent naked accounts).
        /// </summary>
        private void CancelAllV12GtcOrders(bool force)
        {
            int trackedCancels = SweepTrackedOrders(force);
            int brokerCancels  = SweepBrokerOrders(force);
            Print($"[BUILD 948] GTC sweep: cancelled {trackedCancels} tracked + {brokerCancels} broker-scanned orders");
        }

        /// <summary>Phase 1: cancel orders held in strategy tracking dictionaries.</summary>
        private int SweepTrackedOrders(bool force)
        {
            int trackedCancels = 0;
            var trackedDicts = new ConcurrentDictionary<string, Order>[]
            {
                entryOrders, stopOrders,
                target1Orders, target2Orders, target3Orders, target4Orders, target5Orders
            };
            foreach (var dict in trackedDicts)
            {
                if (dict == null) continue;
                foreach (var kvp in dict.ToArray())
                {
                    Order ord = kvp.Value;
                    if (ord == null) continue;
                    if (ord.OrderState != OrderState.Working && ord.OrderState != OrderState.Accepted) continue;
                    try
                    {
                        bool isFleet = ord.Account != null &&
                            ord.Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !string.Equals(ord.Account.Name, Account.Name, StringComparison.OrdinalIgnoreCase);
                        if (isFleet)
                            ord.Account.Cancel(new[] { ord });
                        else
                            CancelOrder(ord);
                        trackedCancels++;
                    }
                    catch { }
                }
            }
            return trackedCancels;
        }

        /// <summary>
        /// Phase 2: broker-level scan to catch V12 orders not held in tracking dicts.
        /// [P1 LIFECYCLE SAFETY]: skips accounts with open positions when force=false
        /// to avoid leaving them naked after entry-order cancellation.
        /// </summary>
        private int SweepBrokerOrders(bool force)
        {
            int brokerCancels = 0;
            var v12Prefixes = new[] { "Stop_", "S_", "T1_", "T2_", "T3_", "T4_", "T5_", "Fleet_" };
            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
                // [P1 LIFECYCLE SAFETY]: If not a forced teardown, skip accounts with open positions
                // to avoid leaving them naked (no bracket/stop) after their entry orders are cancelled.
                if (!force)
                {
                    bool hasPosition = false;
                    try
                    {
                        foreach (Position pos in acct.Positions)
                        {
                            if (pos.Instrument?.FullName == Instrument?.FullName && pos.Quantity != 0)
                            { hasPosition = true; break; }
                        }
                    }
                    catch { }
                    if (hasPosition)
                    {
                        Print($"[BUILD 948] GTC sweep: SKIPPING {acct.Name} -- open position detected (force=false)");
                        continue;
                    }
                }
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        if (ord.OrderState != OrderState.Working && ord.OrderState != OrderState.Accepted) continue;
                        string ordName = ord.Name ?? string.Empty;
                        bool isV12 = false;
                        for (int pi = 0; pi < v12Prefixes.Length; pi++)
                        {
                            if (ordName.StartsWith(v12Prefixes[pi], StringComparison.OrdinalIgnoreCase))
                            { isV12 = true; break; }
                        }
                        if (!isV12) continue;
                        try { acct.Cancel(new[] { ord }); brokerCancels++; } catch { }
                    }
                }
                catch { }
            }
            return brokerCancels;
        }

        /// <summary>
        /// V12 SIMA: Execute a market order across ALL accounts matching the prefix
        /// </summary>
        private void ExecuteMultiAccountMarket(OrderAction action, int quantity, string signalName)
        {
            if (!EnableSIMA) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            int successCount = 0;
            int failCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // V12.8: Fleet Active Check ??" skip accounts NOT registered or disabled
                    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                    {
                        Print($"[SIMA] Fleet Dispatch: {acct.Name} SKIPPED (Inactive in Fleet Manager)");
                        continue;
                    }

                    int reservedDelta = 0;
                    try
                    {
                        // V12.1: Consistency Lock Check
                        if (EnableConsistencyLock)
                        {
                            double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                            if (dailyPL >= MaxDailyProfitCap)
                            {
                                Print($"[SIMA] (!) SKIPPING {acct.Name} - Consistency Lock Active (Day P/L: ${dailyPL:F2})");
                                continue;
                            }
                        }

                        Order order = acct.CreateOrder(Instrument, action, OrderType.Market,
                            TimeInForce.Gtc, quantity, 0, 0, "", signalName, null);

                        if (order != null)
                        {
                            // V12.Phase7 [C-02/H-07]: Reserve expectedPositions BEFORE Submit to eliminate
                            // Reaper false-desync race. Rolled back in catch block on failure.
                            reservedDelta = (action == OrderAction.Buy || action == OrderAction.BuyToCover) ? quantity : -quantity;
                            AddExpectedPositionDeltaLocked(ExpKey(acct.Name), reservedDelta);
                            acct.Submit(new[] { order });
                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // V12.Phase7 [GAP-3]: Undo expectedPositions reservation if submission failed.
                        // Delta may or may not have been applied (depends on where exception occurred),
                        // so rollback is conditional on whether reserve completed.
                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(ExpKey(acct.Name), -reservedDelta);
                        failCount++;
                        Print($"[SIMA] ??-- FAILED on {acct.Name}: {ex.Message}");
                    }
                }
            }
            Print($"[SIMA] BROADCAST: {action} {quantity} | {successCount} OK / {failCount} FAIL");
        }

        /// <summary>
        /// V12 SIMA: Execute a Market Entry + Fixed Target/Stop across ALL accounts (Path B)
        /// Uses true broker-side OCO brackets for each account
        /// </summary>
        private void ExecuteMultiAccountBracket(OrderAction action, int quantity, string signalName, double stopPoints, double targetPoints)
        {
            if (!EnableSIMA) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            int successCount = 0;
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int reservedDelta = 0;
                    try
                    {
                        // V12.1: Consistency Lock Check
                        if (EnableConsistencyLock)
                        {
                            double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                            if (dailyPL >= MaxDailyProfitCap)
                            {
                                Print($"[PATH B] (!) SKIPPING {acct.Name} - Consistency Lock Active (Day P/L: ${dailyPL:F2})");
                                continue;
                            }
                        }

                        // 1. Calculate Prices
                        double stopPrice = action == OrderAction.Buy ? currentPrice - stopPoints : currentPrice + stopPoints;
                        double targetPrice = action == OrderAction.Buy ? currentPrice + targetPoints : currentPrice - targetPoints;

                        // V12.Phase6 [TICK-01]: Standardized tick rounding via MasterInstrument API
                        stopPrice  = Instrument.MasterInstrument.RoundToTickSize(stopPrice);
                        targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice);

                        // 2. Create Bracket
                        string ocoId = action.ToString() + "_" + DateTime.Now.Ticks;

                        Order entry = acct.CreateOrder(Instrument, action, OrderType.Market, TimeInForce.Gtc, quantity, 0, 0, ocoId, signalName, null);

                        Order stop = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, ocoId, "Stop_" + signalName, null);

                        Order target = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Limit, TimeInForce.Gtc, quantity, targetPrice, 0, ocoId, "Target_" + signalName, null);

                        // V12.Phase7 [C-02/GAP-2]: Reserve expectedPositions BEFORE Submit to eliminate
                        // Reaper race window. Rolled back in catch block on failure.
                        reservedDelta = (action == OrderAction.Buy) ? quantity : -quantity;
                        AddExpectedPositionDeltaLocked(ExpKey(acct.Name), reservedDelta);

                        // 3. Submit as Atomic Group (Broker OCO)
                        acct.Submit(new[] { entry, stop, target });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // V12.Phase7 [C-02/GAP-2]: Undo expectedPositions reservation if submission failed.
                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(ExpKey(acct.Name), -reservedDelta);
                        Print($"[SIMA] ??-- BRACKET FAILED on {acct.Name}: {ex.Message}");
                    }
                }
            }
            Print($"[SIMA] PATH B BROADCAST: {successCount} Brackets Submitted");
        }

        /// <summary>
        /// V12 SIMA: Master Flatten - closes local position and broadcasts to the entire fleet
        /// </summary>
        // Duplicate FlattenAll removed - consolidated into line 4387 version

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Places limit entry + bracket on the local chart account,
        /// then iterates Account.All to place the same order on every fleet account matching AccountPrefix.
        /// CRITICAL: Every account's entry order is registered in entryOrders AND activePositions
        /// with a unique key (accountName + "_RMA") so ManageCIT can chase the entire fleet.
        /// </summary>
        private void ExecuteRMAEntryV2(double price, MarketPosition direction, int contracts)
        {
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            // [A1]: Defensive guard ??" caller must pre-calculate a valid quantity.
            if (contracts <= 0)
            {
                Print($"[RMA] ExecuteRMAEntryV2 received invalid contracts={contracts}. Aborting entry.");
                return;
            }

            // [923B-FIX-A]: Zero-price guard ??" a Limit order at price=0 is treated as a Market order
            // by Apex/Tradovate, causing an immediate fill without price ever touching the RMA level.
            // Root cause: IPC path (UI.IPC.cs) can pass currentPrice=0 if lastKnownPrice<=0 AND
            // Close[0] is not yet initialized (strategy just loaded, pre-session bars not formed).
            if (price <= 0)
            {
                Print($"[RMA V2] ABORT: price={price:F2} is zero or negative. Refusing to submit Limit @ 0 -- would fill as Market. Ensure lastKnownPrice is valid before dispatching.");
                return;
            }

            try
            {
                // Calculate stop and 5 targets using RMA profile.
                bool useRmaTargetProfile = true;
                // [LEAK-01]: Use centralized ATR calculator (ceiling + min/max guards, fleet-ready).
                double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                // [A1]: contracts parameter used directly ??" CalculatePositionSize removed from this method.
                // stopDist is retained to compute actual bracket stop price below.
                int qty = contracts;
                double stopPrice = (direction == MarketPosition.Long) ? price - stopDist : price + stopDist;
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double t1Price = CalculateTargetPrice(direction, price, 1);
                double t2Price = CalculateTargetPrice(direction, price, 2);
                double t3Price = CalculateTargetPrice(direction, price, 3);
                double t4Price = CalculateTargetPrice(direction, price, 4);
                double t5Price = CalculateTargetPrice(direction, price, 5);

                // V12.1101E FLEET PARITY: calculate full 5-target distribution for both Master and Fleet.
                int rt1, rt2, rt3, rt4, rt5;
                GetTargetDistribution(qty, out rt1, out rt2, out rt3, out rt4, out rt5);

                string baseSignal = $"RMA_{DateTime.Now.Ticks}";
                OrderAction entryAction = (direction == MarketPosition.Long) ? OrderAction.Buy : OrderAction.SellShort;
                string symmetryDispatchId = SymmetryGuardBeginDispatch("RMA", entryAction, qty, price);

                Print($"[SIMA RMA V2] {direction} @ {price} | Stop: {stopPrice} | T1: {t1Price} | T2: {t2Price} | T3: {t3Price} | T4: {t4Price} | T5: {t5Price} | Qty: {qty}");

                // =======================================================
                // 1. LOCAL ACCOUNT: SubmitOrderUnmanaged (chart-visible)
                // =======================================================
                string localKey = baseSignal;
                Order entryOrder = SubmitOrderUnmanaged(0, entryAction, OrderType.Limit, qty, price, 0, "", localKey);
                if (entryOrder != null)
                {
                    SymmetryGuardRegisterMasterEntry(symmetryDispatchId, localKey);
                    entryOrders[localKey] = entryOrder;

                    PositionInfo pos = new PositionInfo
                    {
                        SignalName = localKey,
                        Direction = direction,
                        TotalContracts = qty,
                        T1Contracts = rt1,
                        T2Contracts = rt2,
                        T3Contracts = rt3,
                        T4Contracts = rt4,
                        T5Contracts = rt5,
                        RemainingContracts = qty,
                        EntryPrice = price,
                        InitialStopPrice = stopPrice,
                        CurrentStopPrice = stopPrice,
                        Target1Price = t1Price,
                        Target2Price = t2Price,
                        Target3Price = t3Price,
                        Target4Price = t4Price,
                        Target5Price = t5Price,
                        EntryOrderType = OrderType.Limit,
                        EntryFilled = false,
                        BracketSubmitted = false, // V12.7: Brackets deferred until entry fills
                        IsRMATrade = true
                    };
                    activePositions[localKey] = pos;

                    // V12.12: Register Master account in expectedPositions (was missing ??" caused false Reaper desyncs)
                    int localDelta = (direction == MarketPosition.Long) ? qty : -qty;
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), localDelta);
                    Print($"[SIMA] Master expectedPositions updated: {Account.Name} delta={localDelta}");

                    // V12.7: Do NOT submit stop/target here ??" they will be submitted by
                    // SubmitBracketOrders() when the entry limit fills in OnOrderUpdate.
                    // Submitting them now would cause instant fills on marketable targets.

                    Print($"[SIMA RMA V2] LOCAL ENTRY ONLY (Limit): {localKey} | Brackets deferred until fill");
                }
                else
                {
                    Print("[SIMA RMA V2] ERROR: Local entry returned null");
                }

                // =======================================================
                // 2. SIMA FLEET: Iterate Account.All for followers
                // =======================================================
                if (!EnableSIMA)
                {
                    Print("[SIMA RMA V2] ?????? EnableSIMA is FALSE - Fleet dispatch SKIPPED. Enable SIMA in strategy parameters or send SET_SIMA|ON via IPC.");
                    return;
                }

                int fleetOk = 0;
                int fleetSkip = 0;
                var dispatchLog = new StringBuilder();

                foreach (Account acct in Account.All)
                {
                    if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (acct == this.Account) continue; // local already done

                    // V12.8: Fleet Manager toggle ??" skip if account NOT registered or explicitly disabled
                    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                    {
                        Print($"[SIMA] Fleet Dispatch: {acct.Name} SKIPPED (Inactive in Fleet Manager)");
                        fleetSkip++;
                        continue;
                    }

                    // Consistency Lock
                    if (EnableConsistencyLock)
                    {
                        double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                        if (dailyPL >= MaxDailyProfitCap)
                        {
                            Print($"[SIMA RMA V2] SKIP {acct.Name} - ConsistencyLock (${dailyPL:F2})");
                            fleetSkip++;
                            continue;
                        }
                    }

                    // [923B-FIX-B]: fleetKey declared outside try so catch can access it for dict rollback.
                    string fleetKey = $"{acct.Name}_RMA_{baseSignal}";
                    string expectedKey = ExpKey(acct.Name);
                    int reservedDelta = 0;
                    bool syncPending = false;
                    try
                    {
                        SymmetryGuardRegisterFollower(symmetryDispatchId, fleetKey);
                        string ocoId = fleetKey;

                        // V12.10: Submit ENTRY ONLY ??" brackets deferred until fill (unified with leader)
                        Order fEntry = acct.CreateOrder(Instrument, entryAction, OrderType.Limit,
                            TimeInForce.Gtc, qty, price, 0, ocoId, fleetKey, null);

                        // [M8.1 NRE-01]: CreateOrder returns null for disconnected or invalid account/instrument pairs.
                        // Guard before reservation ??" expectedPositions not yet incremented, no rollback needed.
                        if (fEntry == null)
                        {
                            dispatchLog.AppendLine($"[SIMA RMA V2] WARN {fleetKey} on {acct.Name}: " +
                                "CreateOrder returned null -- account may be disconnected. Skipping.");
                            continue;
                        }

                        // [923B-FIX-B]: Phantom-Fix FIX-1 backport ??" register tracking dicts BEFORE
                        // updating expectedPositions. Mirrors the fix already applied to ExecuteSmartDispatchEntry
                        // (SIMA.cs Phantom-Fix comment at ~line 554).
                        //
                        // OLD (broken) order: expectedPositions FIRST ??' Submit ??' entryOrders/activePositions LAST.
                        // Race: REAPER background thread fires between steps 1 and 3, observes non-zero
                        //       expectedPositions with no entry in entryOrders ??' hasWorkingEntry=false
                        //       ??' phantom repair queued ??' second Limit order submitted at same price
                        //       ??' original entry orphaned ??' double fill or naked position on price touch.
                        //
                        // FIXED order: build PositionInfo ??' register dicts atomically (stateLock) FIRST
                        //              ??' expectedPositions SECOND ??' Submit LAST.
                        // V12.1101E: Full 5-target distribution mirrors Master exactly.
                        PositionInfo fleetFollowerPos = new PositionInfo
                        {
                            SignalName = fleetKey,
                            Direction = direction,
                            TotalContracts = qty,
                            RemainingContracts = qty,
                            EntryPrice = price,
                            InitialStopPrice = stopPrice,
                            CurrentStopPrice = stopPrice,
                            Target1Price = t1Price,
                            Target2Price = t2Price,
                            Target3Price = t3Price,
                            Target4Price = t4Price,
                            Target5Price = t5Price,
                            T1Contracts = rt1,
                            T2Contracts = rt2,
                            T3Contracts = rt3,
                            T4Contracts = rt4,
                            T5Contracts = rt5,
                            EntryOrderType = OrderType.Limit,
                            EntryFilled = false,
                            IsRMATrade = true,
                            IsFollower = true,
                            ExecutingAccount = acct,
                            BracketSubmitted = false,   // V12.10: deferred ??" OnAccountExecutionUpdate submits on fill
                            ExtremePriceSinceEntry = price,
                            CurrentTrailLevel = 0,
                            // Build 936 [FIX-2]: Deterministic bracket OCO group ID for broker-native stop+target linking.
                            OcoGroupId = "V12_" + fleetKey.GetHashCode().ToString("X8"),
                        };
                        lock (stateLock)
                        {
                            activePositions[fleetKey] = fleetFollowerPos; // FIRST: dicts registered atomically
                            entryOrders[fleetKey] = fEntry;               // REAPER hasWorkingEntry check reads these
                        }

                        MarkDispatchSyncPending(expectedKey);
                        syncPending = true;

                        reservedDelta = (direction == MarketPosition.Long) ? qty : -qty;
                        AddExpectedPositionDeltaLocked(expectedKey, reservedDelta); // SECOND: expectedPositions

                        acct.Submit(new[] { fEntry }); // LAST ??" stateLock not held here
                        ClearDispatchSyncPending(expectedKey);
                        syncPending = false;
                        // stopOrders/target1..target5 are set by follower bracket submission on fill

                        fleetOk++;
                    }
                    catch (Exception ex)
                    {
                        if (syncPending)
                        {
                            ClearDispatchSyncPending(expectedKey);
                            syncPending = false;
                        }

                        // [923B-FIX-B]: Full rollback ??" dicts were registered before expectedPositions,
                        // so both must be cleaned up on Submit failure (mirrors ExecuteSmartDispatchEntry catch).
                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                        activePositions.TryRemove(fleetKey, out _);
                        entryOrders.TryRemove(fleetKey, out _);
                        Print($"[SIMA RMA V2] FAIL {acct.Name}: {ex.Message}");
                    }
                }

                if (dispatchLog.Length > 0)
                {
                    Print("== SIMA RMA V2 WARNINGS ==");
                    Print(dispatchLog.ToString().TrimEnd());
                    Print("==========================");
                }

                Print($"[SIMA RMA V2] Fleet: {fleetOk} dispatched, {fleetSkip} skipped");
            }
            catch (Exception ex)
            {
                Print($"[SIMA RMA V2] ERROR: {ex.Message}");
            }
        }

        private void FlattenAllApexAccounts()
        {
            if (!EnableSIMA)
            {
                Print("[SIMA] DISABLED - Using single-account flatten");
                FlattenAll(); // Call consolidated flatten
                return;
            }

            isFlattenRunning = true; // V12.8: Guard for Reaper + OnAccountExecutionUpdate
            try
            {
                Print("[SIMA] ====== GLOBAL FLATTEN START ======");
                int flattenCount = 0;
                int totalCount = 0;

                // V12.9: Flatten ALL matching accounts regardless of Fleet Manager status.
                // This is a safety mechanism ??" "Flatten All" must always be able to close everything.
                foreach (Account acct in Account.All)
                {
                    if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        totalCount++;
                        try
                        {
                            // [V12.12] Cancel all working orders for this instrument first.
                            // acct.Flatten() is a managed API and silently no-ops in IsUnmanaged=true strategies.
                            List<Order> ordersToCancel = new List<Order>();
                            foreach (Order order in acct.Orders)
                            {
                                if (order.Instrument.FullName == Instrument.FullName &&
                                    (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                     order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending ||
                                     order.OrderState == OrderState.ChangeSubmitted))
                                {
                                    ordersToCancel.Add(order);
                                }
                            }
                            if (ordersToCancel.Count > 0)
                            {
                                acct.Cancel(ordersToCancel);
                                Print($"[SIMA] Cancelled {ordersToCancel.Count} working order(s) on {acct.Name}");
                            }

                            // Submit Market close orders for each open position
                            int closedCount = 0;
                            foreach (Position position in acct.Positions)
                            {
                                if (position.MarketPosition == MarketPosition.Flat) continue;
                                int qty = position.Quantity;
                                OrderAction closeAction = position.MarketPosition == MarketPosition.Long
                                    ? OrderAction.Sell
                                    : OrderAction.BuyToCover;
                                string signalName = "Flatten_" + position.MarketPosition.ToString();
                                Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market,
                                    TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                                acct.Submit(new[] { closeOrder });
                                closedCount++;
                            }
                            if (closedCount > 0)
                            {
                                flattenCount++;
                                Print($"[SIMA] [OK] Flattened {closedCount} position(s) on {acct.Name}");
                            }

                            // Reset expected position
                            SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                        }
                        catch (Exception ex)
                        {
                            Print($"[SIMA] ??-- FLATTEN FAILED on {acct.Name}: {ex.Message}");
                        }
                    }
                }

                // V12.12: Explicitly flatten the Master account if it was NOT covered by the prefix filter.
                // Bug fix: If Master is "Sim101" and AccountPrefix is "Apex", the loop above skips it entirely.
                bool masterCovered = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!masterCovered)
                {
                    totalCount++;
                    try
                    {
                        // [V12.12] Cancel all working master orders before closing position.
                        List<Order> masterOrdersToCancel = new List<Order>();
                        foreach (Order order in Account.Orders)
                        {
                            if (order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                 order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending ||
                                 order.OrderState == OrderState.ChangeSubmitted))
                            {
                                masterOrdersToCancel.Add(order);
                            }
                        }
                        if (masterOrdersToCancel.Count > 0)
                        {
                            Account.Cancel(masterOrdersToCancel);
                            Print($"[SIMA] Cancelled {masterOrdersToCancel.Count} working order(s) on {Account.Name}");
                        }

                        // Submit Market close orders via SubmitOrderUnmanaged for the master account
                        int masterClosedCount = 0;
                        foreach (Position position in Account.Positions)
                        {
                            if (position.MarketPosition == MarketPosition.Flat) continue;
                            int qty = position.Quantity;
                            string signalName = position.MarketPosition == MarketPosition.Long
                                ? "Flatten_MasterLong"
                                : "Flatten_MasterShort";
                            Order masterClose = position.MarketPosition == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", signalName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", signalName);
                            if (masterClose != null)
                                masterClosedCount++;
                            else
                                Print($"[SIMA] ??-- Master close FAILED (SubmitOrderUnmanaged returned null): {position.MarketPosition} {qty}");
                        }
                        if (masterClosedCount > 0)
                        {
                            flattenCount++;
                            Print($"[SIMA] V12.12 Master flatten: {masterClosedCount} position(s) on {Account.Name} (outside prefix filter)");
                        }

                        SetExpectedPositionLocked(ExpKey(Account.Name), 0);
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] V12.12 Master FLATTEN FAILED on {Account.Name}: {ex.Message}");
                    }
                }

                Print($"[SIMA] ====== GLOBAL FLATTEN COMPLETE: {flattenCount} flattened across {totalCount} accounts ======");
            }
            finally
            {
                // V1101E HOT-PATCH: If FlattenAll holds stateLock, it owns guard release at the true end of the global flatten.
                if (!Monitor.IsEntered(stateLock))
                    isFlattenRunning = false; // V12.8: Always release guard, even on exception
            }
        }

        /// <summary>
        /// DEAD-01: Emergency single-account fleet kill. Called when a follower entry fills
        /// AFTER the master order is cancelled (CASCADE-FILLED path). Cancels all working orders
        /// on the instrument for this account, then submits a Market close if a position exists.
        /// Must be called on strategy thread (via TriggerCustomEvent).
        /// </summary>
        private void EmergencyFlattenSingleFleetAccount(Account acct)
        {
            if (acct == null) return;
            Print($"[DEAD-01] EmergencyFlatten: Initiating kill for {acct.Name}");

            try
            {
                // [938-EF-GUARD] Confirm bracket cancellation precedes market close.
                Print($"[938-EF-GUARD] EF cancelling bracket first: {acct.Name}");

                // Step 1: Cancel ALL working orders on this instrument for this account.
                var ordersToCancel = new List<Order>();
                foreach (Order o in acct.Orders)
                {
                    if (o.Instrument.FullName == Instrument.FullName &&
                        (o.OrderState == OrderState.Working    ||
                         o.OrderState == OrderState.Submitted  ||
                         o.OrderState == OrderState.Accepted   ||
                         o.OrderState == OrderState.ChangePending ||
                         o.OrderState == OrderState.ChangeSubmitted))
                    {
                        ordersToCancel.Add(o);
                    }
                }
                if (ordersToCancel.Count > 0)
                {
                    acct.Cancel(ordersToCancel);
                    Print($"[DEAD-01] EmergencyFlatten: Cancelled {ordersToCancel.Count} working order(s) on {acct.Name}.");
                }

                // Step 2: Close any live position with a Market order.
                Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName &&
                                                                    p.MarketPosition != MarketPosition.Flat);
                if (pos != null)
                {
                    OrderAction closeAction = pos.MarketPosition == MarketPosition.Long
                        ? OrderAction.Sell          // Close long
                        : OrderAction.BuyToCover;   // Close short

                    Order closeOrder = acct.CreateOrder(
                        Instrument,
                        closeAction,
                        OrderType.Market,
                        TimeInForce.Day,
                        pos.Quantity,
                        0, 0,
                        string.Empty,
                        "Emergency_Flatten_DEAD01",
                        null);
                    acct.Submit(new[] { closeOrder });
                    Print($"[DEAD-01] EmergencyFlatten: Market {closeAction} {pos.Quantity} submitted on {acct.Name}.");
                }
                else
                {
                    Print($"[DEAD-01] EmergencyFlatten: {acct.Name} already flat -- no close order needed.");
                }

                // Step 3: Clear ghost memory so REAPER does not trigger a second flatten.
                SetExpectedPositionLocked(ExpKey(acct.Name), 0);
            }
            catch (Exception ex)
            {
                Print($"[DEAD-01] EmergencyFlatten ERROR on {acct.Name}: {ex.Message}");
            }
        }

        private void ClosePositionsOnlyApexAccounts()
        {
            if (!EnableSIMA) return;

            // V12.Phase10 [ZOMBIE-STOP-FIX]: Set isFlattenRunning to suppress REAPER background thread
            // during the naked window between zombie stop cancellation and Market close fill.
            // REAPER.cs L97: `if (isFlattenRunning) continue;` ??" guard already exists; we activate it here.
            // Previously this method did NOT set isFlattenRunning (V12.21 comment). Now it must, because
            // the zombie sweep below creates a transient naked-position window the REAPER would self-heal.
            isFlattenRunning = true;
            try
            {
                Print("[SIMA] ====== GLOBAL POSITIONS CLOSE START (System Protection Orders Swept; Limit/Stop Brackets Preserved) ======");
                int closeCount = 0;
                int totalCount = 0;

                foreach (Account acct in Account.All)
                {
                    if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    totalCount++;
                    try
                    {
                        // -- V12.Phase10 [ZOMBIE-STOP-FIX]: Zombie Sweep ------------------------------
                        // EMERGENCY_STOP_ orders are submitted by REAPER to guard naked positions.
                        // They are NOT OCO-linked and NOT part of any bracket structure, so they survive
                        // FLATTEN_ONLY and become Zombies ??" reversal-fill risk after the position closes.
                        // Stop_*, S_*, T*_ are legitimate bracket stops/targets: intentionally preserved.
                        List<Order> zombieOrders = new List<Order>();
                        foreach (Order order in acct.Orders)
                        {
                            if (order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working       ||
                                 order.OrderState == OrderState.Submitted     ||
                                 order.OrderState == OrderState.Accepted      ||
                                 order.OrderState == OrderState.ChangePending ||
                                 order.OrderState == OrderState.ChangeSubmitted) &&
                                order.Name.StartsWith("EMERGENCY_STOP_", StringComparison.OrdinalIgnoreCase))
                            {
                                zombieOrders.Add(order);
                            }
                        }
                        if (zombieOrders.Count > 0)
                        {
                            acct.Cancel(zombieOrders); // V12.Phase10 [ZOMBIE-STOP-FIX]
                            Print($"[SIMA][ZOMBIE-STOP-FIX] {acct.Name}: swept {zombieOrders.Count} system protection order(s). Deck cleared.");
                        }
                        // -----------------------------------------------------------------------------

                        foreach (Position position in acct.Positions)
                        {
                            if (position.Instrument.FullName != Instrument.FullName) continue;
                            if (position.MarketPosition == MarketPosition.Flat) continue;

                            int qty = position.Quantity;
                            OrderAction closeAction = position.MarketPosition == MarketPosition.Long
                                ? OrderAction.Sell
                                : OrderAction.BuyToCover;
                            string signalName = "GracefulClose_" + position.MarketPosition.ToString();
                            Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market,
                                TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                            acct.Submit(new[] { closeOrder });
                            closeCount++;
                            Print($"[SIMA] [OK] Graceful Close: {qty} {position.MarketPosition} on {acct.Name}");
                        }

                        SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] (!) CLOSE FAILED on {acct.Name}: {ex.Message}");
                    }
                }

                // Master account fallback (if not covered by AccountPrefix filter)
                bool masterCovered = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!masterCovered && Account.Positions.Count > 0)
                {
                    // V12.Phase10 [ZOMBIE-STOP-FIX]: Same zombie sweep for master account path.
                    List<Order> masterZombieOrders = new List<Order>();
                    foreach (Order order in Account.Orders)
                    {
                        if (order.Instrument.FullName == Instrument.FullName &&
                            (order.OrderState == OrderState.Working       ||
                             order.OrderState == OrderState.Submitted     ||
                             order.OrderState == OrderState.Accepted      ||
                             order.OrderState == OrderState.ChangePending ||
                             order.OrderState == OrderState.ChangeSubmitted) &&
                            order.Name.StartsWith("EMERGENCY_STOP_", StringComparison.OrdinalIgnoreCase))
                        {
                            masterZombieOrders.Add(order);
                        }
                    }
                    if (masterZombieOrders.Count > 0)
                    {
                        Account.Cancel(masterZombieOrders); // V12.Phase10 [ZOMBIE-STOP-FIX]
                        Print($"[SIMA][ZOMBIE-STOP-FIX] {Account.Name} (master): swept {masterZombieOrders.Count} system protection order(s). Deck cleared.");
                    }

                    foreach (Position position in Account.Positions)
                    {
                        if (position.Instrument.FullName != Instrument.FullName) continue;
                        if (position.MarketPosition == MarketPosition.Flat) continue;

                        int qty = position.Quantity;
                        Order masterClose = position.MarketPosition == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell,       OrderType.Market, qty, 0, 0, "", "GracefulClose_MasterLong")
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", "GracefulClose_MasterShort");
                        if (masterClose != null)
                        {
                            closeCount++;
                            Print($"[SIMA] [OK] Graceful Close: Master {qty} {position.MarketPosition}");
                        }
                        else
                        {
                            Print($"[SIMA] ??-- Graceful Close FAILED: Master {qty} {position.MarketPosition} (SubmitOrderUnmanaged returned null)");
                        }
                    }
                    SetExpectedPositionLocked(ExpKey(Account.Name), 0);
                }

                Print($"[SIMA] ====== GLOBAL POSITIONS CLOSE COMPLETE: {closeCount} positions closed ======");
            }
            finally
            {
                // V12.Phase10 [ZOMBIE-STOP-FIX]: Always release REAPER suppression, even on exception.
                // Mirrors FlattenAllApexAccounts() finally pattern (SIMA.cs L1274).
                isFlattenRunning = false;
            }
        }

        #endregion
    }
}

