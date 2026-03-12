// Build 971: SIMA Dispatch -- ExecuteSmartDispatchEntry
// V12 SIMA Module (Extracted)
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
        #region V12 SIMA Dispatch

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
                activeAccountSnapshot = new HashSet<string>(
                    activeFleetAccounts
                        .Where(kvp => kvp.Value)
                        .Select(kvp => kvp.Key));
                dispatchTargetCount = Math.Max(1, Math.Min(5, activeTargetCount));

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
                dispatchLog.AppendLine(string.Format("[LATENCY] Loop start at {0:F3} ms from entry",
                    (tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency));

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
                        Print(string.Format("[923A-OVF] SIMA parity overflow qty={0} x mult={1} -- clamping to maxContracts ({2})", quantity, FleetParityMultiplier, maxContracts));
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
                            OcoGroupId = "V12_" + GetStableHash(fleetEntryName),
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
                                    dispatchLog.AppendLine(string.Format("[SIMA TARGET_SKIP] T{0} for {1} has qty={2} but invalid price={3:F2}; skipped",
                                        targetNum, fleetEntryName, targetQty, targetPrice));
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
                            // B966: Enqueue NOT applied here -- ordering invariant requires dict registration
                            // to happen BEFORE AddExpectedPositionDelta (L495). Deferring via Enqueue
                            // from within an existing drain would break this ordering. ConcurrentDictionary
                            // single-writes are thread-safe; PumpFleetDispatch runs on strategy thread via
                            // TriggerCustomEvent so no reaperThread access occurs at this point.
                            activePositions[fleetEntryName] = fleetPos;
                            entryOrders[fleetEntryName] = entry;
                            stopOrders[fleetEntryName] = stop;
                            foreach (var st in stagedTargets)
                            {
                                var targetDict = GetTargetOrdersDictionary(st.Num);
                                if (targetDict != null)
                                    targetDict[fleetEntryName] = st.Order;
                            }
                            registeredForCleanup = true;
                            MarkDispatchSyncPending(expectedKey);
                            syncPending = true;

                            // Build 935: Reserve follower-sized expected quantity only.
                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDelta(expectedKey, reservedDelta);

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

                            dispatchLog.AppendLine(string.Format("  QUEUE | {0,-28} | Market+{1}orders | PENDING",
                                acct.Name, ordersToSubmit.Count));
                            dispatchLog.AppendLine(string.Format("[SIMA STOP_AUDIT] QUEUED {0}: StopQty={1} NonRunnerLimits={2} RunnerQty={3}",
                                fleetEntryName, fleetPos.TotalContracts, nonRunnerLimitQty, runnerQty));
                        }
                        else
                        {
                            // V12.Phantom-Fix [FIX-1]: Register tracking dicts BEFORE updating expectedPositions.
                            // REAPER runs on a background thread; if it fires between the expectedPositions
                            // update and the dict commit (the old T1??'T3 race), it observes non-zero expected
                            // with no entry in entryOrders ??' hasWorkingEntry=false ??' phantom repair queued.
                            // Registering dicts first guarantees REAPER always finds the blocking entry.
                            // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (Phantom-Fix).
                            // ConcurrentDictionary single-writes are thread-safe here.
                            activePositions[fleetEntryName] = fleetPos;
                            entryOrders[fleetEntryName] = entry; // V12.3: Track entry for CIT chase
                            registeredForCleanup = true;
                            MarkDispatchSyncPending(expectedKey);
                            syncPending = true;

                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDelta(expectedKey, reservedDelta);

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

                            dispatchLog.AppendLine(string.Format("  QUEUE | {0,-28} | Limit        | PENDING",
                                acct.Name));
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
                            AddExpectedPositionDelta(expectedKey, -reservedDelta);

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
                Print("[DISPATCH] CRITICAL ERROR in ExecuteSmartDispatchEntry: " + ex.Message);
            }
            finally
            {
                // V12.Phase8 [F-03]: Always release the SIMA toggle semaphore.
                _simaToggleSem.Release();
            }
        }


        #endregion
    }
}
