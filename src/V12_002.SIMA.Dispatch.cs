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
            // V12.Phase8 [F-03]: Semaphore guard -- non-blocking (Build 1109 freeze-proof).            // Wait(0) returns instantly. If contended, defer to next strategy-thread cycle.            if (!_simaToggleSem.Wait(0))            {
                Print("[DISPATCH] Semaphore contended -- deferring dispatch (non-blocking)");
                string _defTradeType = tradeType;
                OrderAction _defAction = action;
                int _defQty = quantity;
                double _defPrice = entryPrice;
                OrderType _defOrderType = entryOrderType;
                string[] _defMasterNames = masterEntryNames;
                try                {
                    TriggerCustomEvent(o => ExecuteSmartDispatchEntry(                        _defTradeType, _defAction, _defQty, _defPrice,                        _defOrderType, _defMasterNames), null);
                }
                catch {
 Print("[DISPATCH] Deferred retry scheduling failed");
 }
                return;
            }
            // [Phase 7.2 LATENCY] T0: Start immediately after semaphore acquired, before any work.            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;
            try            {
                // V12.2: Diagnostic logging for copy trading troubleshooting                Print($"[DISPATCH] ExecuteSmartDispatchEntry called: {
tradeType}
 | EnableSIMA={
EnableSIMA}
 | OrderType={
entryOrderType}
");
                if (!EnableSIMA)                {
                    Print("[DISPATCH] [ERR] SIMA DISABLED - Enable in strategy parameters to copy trade");
                    return;
                }
                // EMERGENCY FIX [H-12]: Abort dispatch if flatten is in progress to prevent re-entry race.                if (isFlattenRunning)                {
                    Print("[DISPATCH] (!) Aborting dispatch -- flatten in progress (isFlattenRunning=true)");
                    return;
 // finally block releases _simaToggleSem                }
                // Phase 6 [MG-D1]: MetadataGuard -- reject duplicate dispatch signals.                // Composite fingerprint prevents the same trade from dispatching twice within 10s.                string dispatchSig = string.Format("SD_{
0}
_{
1}
_{
2}
_{
3:F2}
", tradeType, action, quantity, entryPrice);
                if (!MetadataGuardDuplicate(dispatchSig, "SmartDispatch"))                {
                    Print("[DISPATCH] (!) Duplicate dispatch rejected by MetadataGuard");
                    return;
                }
                Dispatch_ResolveFleetSnapshot(                    tradeType, action, quantity, entryPrice, masterEntryNames,                    out var fleet, out var activeAccountSnapshot, out var dispatchTargetCount, out var symmetryDispatchId);
                if (fleet.Count == 0) return;
                int rmaCount = 0;
                // [Phase 7.2 LATENCY] T_LoopStart + batch log buffer (flushed once after loop).                long tLoopStartTicks = sw.ElapsedTicks;
                var dispatchLog = new StringBuilder(512);
                dispatchLog.AppendLine(string.Format("[LATENCY] Loop start at {
0:F3}
 ms from entry",                    (tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency));
                for (int i = 0;
 i < fleet.Count;
 i++)                {
                    Account acct = fleet[i].Account;
                    // V12.1: Skip Master account if its order was already placed by the caller                    if (acct == this.Account) continue;
                    // Build 935 [SIMA-B935-001]: Inactive + H-13 + consistency lock delegated to ShouldSkipFleetAccount.                    if (ShouldSkipFleetAccount(acct, fleet[i], activeAccountSnapshot, dispatchLog)) continue;
                    int reservedDelta = 0;
                    bool registeredForCleanup = false;
                    bool syncPending = false;
                    string fleetEntryName = null;
                    string expectedKey = null;
                    try                    {
                        bool _builtOk = Dispatch_BuildFollowerOrders(                            tradeType, action, quantity, entryPrice, entryOrderType, acct, i, symmetryDispatchId, dispatchTargetCount, dispatchLog,                            out PositionInfo fleetPos, out Order entry, out fleetEntryName, out expectedKey, out string ocoId, out int followerQty, out int ft1, out int ft2, out int ft3, out int ft4, out int ft5,                            out double stopPrice, out double t1TargetPrice, out double t2TargetPrice, out double t3TargetPrice, out double t4TargetPrice, out double t5TargetPrice);
                        if (!_builtOk) continue;
                        bool isMarketEntry = (entryOrderType == OrderType.Market);
                        // V12.7: Submit only entry for Limit;
 market entries include stop + non-runner targets.                        if (isMarketEntry)                        {
                            Dispatch_PublishMarketBracketToPhoton(                                acct,                                action,                                entry,                                fleetPos,                                fleetEntryName,                                expectedKey,                                ocoId,                                followerQty,                                entryPrice,                                stopPrice,                                dispatchTargetCount,                                dispatchLog,                                ref syncPending,                                ref reservedDelta,                                ref registeredForCleanup);
                        }
                        else                        {
                            Dispatch_PublishLimitEntryToPhoton(                                acct,                                action,                                entry,                                fleetPos,                                fleetEntryName,                                expectedKey,                                ocoId,                                followerQty,                                dispatchLog,                                ref syncPending,                                ref reservedDelta,                                ref registeredForCleanup);
                        }
                        rmaCount++;
                    }
                    catch (Exception ex)                    {
                        if (syncPending)                        {
                            ClearDispatchSyncPending(expectedKey);
                            syncPending = false;
                        }
                        if (reservedDelta != 0)                            AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                        if (registeredForCleanup)                        {
                            // V12.Phase8 [F-01]: Full tracking-dict cleanup on Submit failure.                            activePositions.TryRemove(fleetEntryName, out _);
                            entryOrders.TryRemove(fleetEntryName, out _);
                            stopOrders.TryRemove(fleetEntryName, out _);
                            for (int tNum = 1;
 tNum <= 5;
 tNum++)                            {
                                var targetDict = GetTargetOrdersDictionary(tNum);
                                if (targetDict != null)                                    targetDict.TryRemove(fleetEntryName, out _);
                            }
                        }
                        // Phase 6: Clean up proactive FSM on dispatch failure (no-op if not yet created)
                        if (!string.IsNullOrEmpty(fleetEntryName))
                            _followerBrackets.TryRemove(fleetEntryName, out _);
                        dispatchLog.AppendLine($"[DISPATCH] [X] FAILED on {
acct.Name}
: {
ex.Message}
");
                    }
                }
                // V14.2 FIX-F7: Pump prime checks BOTH ring and legacy queue                if ((_photonDispatchRing != null && !_photonDispatchRing.IsEmpty) || !_pendingFleetDispatches.IsEmpty)                    try {
 TriggerCustomEvent(o => PumpFleetDispatch(), null);
 }
 catch {
 }
                // [Phase 7.2 LATENCY] T_Final: Fleet loop complete (setup+enqueue only;
 no blocking Submit) -- stop clock, flush forensic report.                sw.Stop();
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
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine("|  TIMING SUMMARY                                              |");
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine(string.Format("|  Setup Phase:  {
0,8:F3}
 ms  |  Fleet Loop:  {
1,8:F3}
 ms       |", setupMs, loopMs));
                report.AppendLine(string.Format("|  Total Elapsed: {
0,8:F3}
 ms                                  |", totalMs));
                report.AppendLine("+==============================================================+");
                Print(report.ToString().TrimEnd());
            }
            catch (Exception ex)            {
                Print("[DISPATCH] CRITICAL ERROR in ExecuteSmartDispatchEntry: " + ex.Message);
            }
            finally            {
                // V12.Phase8 [F-03]: Always release the SIMA toggle semaphore.                _simaToggleSem.Release();
            }
                }


        private void Dispatch_ResolveFleetSnapshot(string tradeType, OrderAction action, int quantity, double entryPrice, string[] masterEntryNames, out List<AccountRankInfo> fleet, out HashSet<string> activeAccountSnapshot, out int dispatchTargetCount, out string symmetryDispatchId)
        {
            // V12.Audit [Q3-002]: Snapshot fleet active state under stateLock to prevent UI race.
            // The UI/IPC thread can toggle activeFleetAccounts between TryGetValue and Submit,
            // so we capture a consistent set of active account names once before the dispatch loop.
            // FIX-B [Build 1102Z]: Snapshot activeTargetCount atomically with the fleet snapshot.
            // The IPC SET_TARGET_COUNT command writes activeTargetCount on the TCP listener thread,
            // so a live read inside the fleet loop (line below) can produce a different bound for
            // different accounts. Capturing once here ensures all fleet accounts submit identical
            // target counts for this dispatch.
            activeAccountSnapshot = new HashSet<string>(
                activeFleetAccounts
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key));
            dispatchTargetCount = Math.Max(1, Math.Min(5, activeTargetCount));

            fleet = GetSortedAccountFleet();
            int activeCount = activeAccountSnapshot.Count;

            // V12.2: Log fleet state for diagnostics
            Print($"[DISPATCH] Fleet: {fleet.Count} total accounts | {activeCount} ACTIVE in Fleet Manager");
            if (fleet.Count == 0)
            {
                Print("[DISPATCH] [ERR] NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");
                symmetryDispatchId = null;
                return;
            }

            if (activeCount == 0)
            {
                Print("[DISPATCH] [ERR] NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");
            }

            symmetryDispatchId = SymmetryGuardBeginDispatch(tradeType, action, quantity, entryPrice);
            if (masterEntryNames != null)
            {
                foreach (string masterEntryName in masterEntryNames)
                {
                    if (!string.IsNullOrEmpty(masterEntryName))
                        SymmetryGuardRegisterMasterEntry(symmetryDispatchId, masterEntryName);
                }
            }
        }

        private bool Dispatch_BuildFollowerOrders(
            string tradeType, OrderAction action, int quantity, double entryPrice, OrderType entryOrderType, Account acct, int i, string symmetryDispatchId, int dispatchTargetCount, StringBuilder dispatchLog,
            out PositionInfo fleetPos, out Order entry, out string fleetEntryName, out string expectedKey, out string ocoId, out int followerQty, out int ft1, out int ft2, out int ft3, out int ft4, out int ft5,
            out double stopPrice, out double t1TargetPrice, out double t2TargetPrice, out double t3TargetPrice, out double t4TargetPrice, out double t5TargetPrice)
        {
            fleetEntryName = "Fleet_" + acct.Name + "_" + tradeType + "_" + i;
            expectedKey = ExpKey(acct.Name);
            ocoId = tradeType + "_" + DateTime.UtcNow.Ticks + "_" + i;

            fleetPos = null;
            entry = null;
            followerQty = 0;
            ft1 = 0;
            ft2 = 0;
            ft3 = 0;
            ft4 = 0;
            ft5 = 0;
            stopPrice = 0;
            t1TargetPrice = 0;
            t2TargetPrice = 0;
            t3TargetPrice = 0;
            t4TargetPrice = 0;
            t5TargetPrice = 0;

            // V12: Followers ALWAYS use RMA multipliers for point-based trails (User Req)
            
            MarketPosition followerDirection = action == OrderAction.Buy ? MarketPosition.Long : MarketPosition.Short;

            // [LEAK-01]: Use centralized ATR calculator (ceiling + min/max guards, fleet-ready).
            double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);

            stopPrice = (action == OrderAction.Buy) ? entryPrice - stopDist : entryPrice + stopDist;
            // Universal Ladder: T(n)Type dropdown drives all target pricing.
            t1TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 1);
            t2TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 2);
            t3TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 3);
            t4TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 4);
            t5TargetPrice = CalculateTargetPrice(followerDirection, entryPrice, 5);

            // Rounding
            stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

            // V1102Q [PARITY-01]: Scale quantity for Micro accounts (e.g. ES->MES 10x parity)
            // [923A-P2c-OVF]: checked{} prevents silent int overflow on parity multiply (cf. Callbacks.cs same pattern)
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
            GetTargetDistribution(followerQty, out ft1, out ft2, out ft3, out ft4, out ft5, dispatchTargetCount);

            SymmetryGuardRegisterFollower(symmetryDispatchId, fleetEntryName);

            // V12.3: Entry uses caller-specified order type (Limit for RMA, Market for MOMO/TREND)
            // [FIX-PP-01]: For StopMarket/StopLimit entries the activation price lives in stopPrice,
            // not limitPrice. Passing stopPx=0 caused the follower to fire immediately at market.
            double limitPx = (entryOrderType == OrderType.Limit || entryOrderType == OrderType.StopLimit) ? entryPrice : 0;
            double stopPx  = (entryOrderType == OrderType.StopMarket || entryOrderType == OrderType.StopLimit) ? entryPrice : 0;
            bool isMarketEntry = (entryOrderType == OrderType.Market);
            // StopMarket stays isMarketEntry=false: bracket handled by SymmetryGuardOnFollowerFill anchor flow.
            entry = acct.CreateOrder(Instrument, action, entryOrderType, TimeInForce.Gtc, followerQty, limitPx, stopPx, ocoId, fleetEntryName, null);
            if (entry == null)
            {
                dispatchLog.AppendLine($"[DISPATCH] Entry create failed on {acct.Name} for {fleetEntryName}");
                return false;
            }

            // V12.1: Track follower position for active trailing/target management
            // V12.1101E: Full 5-target distribution mirrors Master
            fleetPos = new PositionInfo
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

            return true;
        }


        #endregion
    

        private void Dispatch_ResolveFleetSnapshot(string tradeType, OrderAction action, int quantity, double entryPrice, string[] masterEntryNames, out List<AccountRankInfo> fleet, out HashSet<string> activeAccountSnapshot, out int dispatchTargetCount, out string symmetryDispatchId)        {
            // V12.Audit [Q3-002]: Snapshot fleet active state under stateLock to prevent UI race.            // The UI/IPC thread can toggle activeFleetAccounts between TryGetValue and Submit,            // so we capture a consistent set of active account names once before the dispatch loop.            // FIX-B [Build 1102Z]: Snapshot activeTargetCount atomically with the fleet snapshot.            // The IPC SET_TARGET_COUNT command writes activeTargetCount on the TCP listener thread,            // so a live read inside the fleet loop (line below) can produce a different bound for            // different accounts. Capturing once here ensures all fleet accounts submit identical            // target counts for this dispatch.            activeAccountSnapshot = new HashSet<string>(                activeFleetAccounts                    .Where(kvp => kvp.Value)                    .Select(kvp => kvp.Key));
            dispatchTargetCount = Math.Max(1, Math.Min(5, activeTargetCount));
            fleet = GetSortedAccountFleet();
            int activeCount = activeAccountSnapshot.Count;
            // V12.2: Log fleet state for diagnostics            Print($"[DISPATCH] Fleet: {
fleet.Count}
 total accounts | {
activeCount}
 ACTIVE in Fleet Manager");
            if (fleet.Count == 0)            {
                Print("[DISPATCH] [ERR] NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");
                symmetryDispatchId = null;
                return;
            }
            if (activeCount == 0)            {
                Print("[DISPATCH] [ERR] NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");
            }
            symmetryDispatchId = SymmetryGuardBeginDispatch(tradeType, action, quantity, entryPrice);
            if (masterEntryNames != null)            {
                foreach (string masterEntryName in masterEntryNames)                {
                    if (!string.IsNullOrEmpty(masterEntryName))                        SymmetryGuardRegisterMasterEntry(symmetryDispatchId, masterEntryName);
                }
            }
        }

        private void Dispatch_PublishMarketBracketToPhoton(            Account acct,            OrderAction action,            Order entry,            PositionInfo fleetPos,            string fleetEntryName,            string expectedKey,            string ocoId,            int followerQty,            double entryPrice,            double stopPrice,            int dispatchTargetCount,            StringBuilder dispatchLog,            ref bool syncPending,            ref int reservedDelta,            ref bool registeredForCleanup)        {
            var ordersToSubmit = new List<Order> {
 entry }
;
            OrderAction exitAction = action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover;
            double validatedStop = ValidateStopPrice(fleetPos.Direction, fleetPos.CurrentStopPrice);
            string stopSig = SymmetryTrim("Stop_" + fleetEntryName, 40);
            Order stop = acct.CreateOrder(                Instrument,                exitAction,                OrderType.StopMarket,                TimeInForce.Gtc,                Math.Max(1, fleetPos.TotalContracts),                0,                validatedStop,                ocoId,                stopSig,                null);
            ordersToSubmit.Add(stop);
            int nonRunnerLimitQty = 0;
            int runnerQty = 0;
            var stagedTargets = new List<StagedTarget>(5);
            // V12.Phase8.3: Use activeTargetCount from dashboard to restrict number of targets submitted            // FIX-B [Build 1102Z]: Use dispatchTargetCount snapshot (captured before loop) -- not live global.            for (int targetNum = 1;
 targetNum <= dispatchTargetCount;
 targetNum++)            {
                int targetQty = GetTargetContracts(fleetPos, targetNum);
                if (targetQty <= 0) continue;
                if (IsRunnerTarget(targetNum))                {
                    runnerQty += targetQty;
                    continue;
                }
                double targetPrice = GetTargetPrice(fleetPos, targetNum);
                if (targetPrice <= 0)                {
                    dispatchLog.AppendLine(string.Format("[SIMA TARGET_SKIP] T{
0}
 for {
1}
 has qty={
2}
 but invalid price={
3:F2}
;
 skipped",                        targetNum, fleetEntryName, targetQty, targetPrice));
                    continue;
                }
                string targetSig = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);
                Order target = acct.CreateOrder(                    Instrument,                    exitAction,                    OrderType.Limit,                    TimeInForce.Gtc,                    targetQty,                    targetPrice,                    0,                    ocoId,                    targetSig,                    null);
                // V12.Phase8 [F-01/F-02]: Stage target orders locally;
 commit after Submit.                stagedTargets.Add(new StagedTarget {
 Num = targetNum, Price = targetPrice, Order = target }
);
                ordersToSubmit.Add(target);
                nonRunnerLimitQty += targetQty;
            }
            // Build 935: Register local dictionaries before reserve/submit so REAPER never            // observes Expected!=0 without entry/stop/targets tracking state.            // B966: Enqueue NOT applied here -- ordering invariant requires dict registration            // to happen BEFORE AddExpectedPositionDeltaLocked (L495). Deferring via Enqueue            // from within an existing drain would break this ordering. ConcurrentDictionary            // single-writes are thread-safe;
 PumpFleetDispatch runs on strategy thread via            // TriggerCustomEvent so no background thread access occurs at this point.            activePositions[fleetEntryName] = fleetPos;
            entryOrders[fleetEntryName] = entry;
            stopOrders[fleetEntryName] = stop;
            foreach (var st in stagedTargets)            {
                var targetDict = GetTargetOrdersDictionary(st.Num);
                if (targetDict != null)                    targetDict[fleetEntryName] = st.Order;
            }
            registeredForCleanup = true;
            MarkDispatchSyncPending(expectedKey);
            syncPending = true;
            // Phase 6 [FSM-P1]: Proactive FSM -- eliminates Gap of Unknowing            // between enqueue and PumpFleetDispatch. State = PendingSubmit until            // pump promotes to Submitted after successful acct.Submit().            if (!_followerBrackets.ContainsKey(fleetEntryName))            {
                var proFsm = new FollowerBracketFSM                {
                    AccountName = acct.Name,                    EntryName = fleetEntryName,                    State = FollowerBracketState.PendingSubmit,                    RemainingContracts = followerQty,                    EntryOrder = entry,                    ExpectedEntryPrice = entry.LimitPrice > 0 ? entry.LimitPrice : 0,                    StopOrder = stop,                    ExpectedStopPrice = stop != null ? stop.StopPrice : 0,                    OcoGroupId = ocoId,                    LastUpdateUtc = DateTime.UtcNow                }
;
                foreach (var st in stagedTargets)                {
                    if (st.Num >= 1 && st.Num <= 5)                    {
                        proFsm.Targets[st.Num - 1] = st.Order;
                        proFsm.ExpectedTargetPrices[st.Num - 1] = st.Price;
                    }
                }
                _followerBrackets.TryAdd(fleetEntryName, proFsm);
            }
            // Build 935: Reserve follower-sized expected quantity only.            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
            // V14.2 [ADR-012]: Zero-allocation dispatch via PhotonPool + SPSC ring            int _poolSlotIndex = -1;
            Order[] _proxyOrders = null;
            {
                var _claimed = _photonPool.Claim();
                if (_claimed.Orders != null)                {
                    _proxyOrders = _claimed.Orders;
                    _poolSlotIndex = _claimed.SlotIndex;
                }
                else                {
                    Print("[PHOTON] Pool exhausted -- fallback to heap alloc");
                    _proxyOrders = new Order[MaxOrdersPerSlot];
                    _poolSlotIndex = -1;
                }
            }
            int _orderIdx = 0;
            _proxyOrders[_orderIdx++] = entry;
            _proxyOrders[_orderIdx++] = stop;
            foreach (var _st in stagedTargets)                _proxyOrders[_orderIdx++] = _st.Order;
            FleetDispatchSlot _slot = new FleetDispatchSlot            {
                EntryPrice    = entryPrice,                StopPrice     = stopPrice,                SignalTicks   = DateTime.UtcNow.Ticks,                PoolSlotIndex = _poolSlotIndex,                OrderCount    = _orderIdx,                Quantity      = followerQty,                TargetCount   = dispatchTargetCount,                Action        = (int)action,                ReservedDelta = reservedDelta            }
;
            _slot.Shadow = ComputeFleetDispatchShadow(ref _slot, _photonShadowSalt);
            Interlocked.Increment(ref _pendingFleetDispatchCount);
            // v28.0 blittable slot + sideband-first publish            if (_poolSlotIndex >= 0)            {
                _photonSideband[_poolSlotIndex].Account        = acct;
                _photonSideband[_poolSlotIndex].FleetEntryName = fleetEntryName;
                _photonSideband[_poolSlotIndex].ExpectedKey    = expectedKey;
                Thread.MemoryBarrier();
 // sideband writes visible before ring publish            }
            if (_poolSlotIndex >= 0 && _photonDispatchRing.TryEnqueue(ref _slot))            {
                // Success: slot in ring, pool + sideband linked by PoolSlotIndex.                // MMIO mirror is a best-effort write-through -- never blocks or fails hot path.                if (_photonMmioMirror != null)                {
                    try {
 _photonMmioMirror.TryPublish(ref _slot);
 }
 catch {
 }
                }
            }
            else            {
                // Ring full or pool exhausted -- fallback to ConcurrentQueue                if (_poolSlotIndex >= 0)                {
                    // Pool succeeded but ring full -- release pool, clear sideband, heap-copy                    Print("[PHOTON] Ring full -- fallback to ConcurrentQueue");
                    Order[] legacyOrders = new Order[_orderIdx];
                    Array.Copy(_proxyOrders, legacyOrders, _orderIdx);
                    _photonPool.ReleaseByIndex(_poolSlotIndex);
                    _photonSideband[_poolSlotIndex] = default(FleetDispatchSideband);
                    _proxyOrders = legacyOrders;
                }
                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest                {
                    Account = acct,                    Orders = _proxyOrders,                    FleetEntryName = fleetEntryName,                    ExpectedKey = expectedKey,                    ReservedDelta = reservedDelta,                    SignalTicks = DateTime.UtcNow.Ticks                }
);
            }
            syncPending         = false;
            reservedDelta       = 0;
            registeredForCleanup = false;
            dispatchLog.AppendLine(string.Format("  QUEUE | {
0,-28}
 | Market+{
1}
orders | PENDING",                acct.Name, ordersToSubmit.Count));
            dispatchLog.AppendLine(string.Format("[SIMA STOP_AUDIT] QUEUED {
0}
: StopQty={
1}
 NonRunnerLimits={
2}
 RunnerQty={
3}
",                fleetEntryName, fleetPos.TotalContracts, nonRunnerLimitQty, runnerQty));
        }

        private void Dispatch_PublishLimitEntryToPhoton(            Account acct,            OrderAction action,            Order entry,            PositionInfo fleetPos,            string fleetEntryName,            string expectedKey,            string ocoId,            int followerQty,            StringBuilder dispatchLog,            ref bool syncPending,            ref int reservedDelta,            ref bool registeredForCleanup)        {
            // V12.Phantom-Fix [FIX-1]: Register tracking dicts BEFORE updating expectedPositions.            // REAPER runs on a background thread;
 if it fires between the expectedPositions            // update and the dict commit (the old T1->T3 race), it observes non-zero expected            // with no entry in entryOrders -> hasWorkingEntry=false -> phantom repair queued.            // Registering dicts first guarantees REAPER always finds the blocking entry.            // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (Phantom-Fix).            // ConcurrentDictionary single-writes are thread-safe here.            activePositions[fleetEntryName] = fleetPos;
            entryOrders[fleetEntryName] = entry;
 // V12.3: Track entry for CIT chase            registeredForCleanup = true;
            MarkDispatchSyncPending(expectedKey);
            syncPending = true;
            // Phase 6 [FSM-P1]: Proactive FSM for limit entry (entry-only, no brackets).            if (!_followerBrackets.ContainsKey(fleetEntryName))            {
                var proFsm = new FollowerBracketFSM                {
                    AccountName = acct.Name,                    EntryName = fleetEntryName,                    State = FollowerBracketState.PendingSubmit,                    RemainingContracts = followerQty,                    EntryOrder = entry,                    ExpectedEntryPrice = entry.LimitPrice > 0 ? entry.LimitPrice : 0,                    LastUpdateUtc = DateTime.UtcNow                }
;
                _followerBrackets.TryAdd(fleetEntryName, proFsm);
            }
            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
            int _poolSlotIndexLmt = -1;
            Order[] _proxyOrdersLmt = null;
            {
                var _claimedLmt = _photonPool.Claim();
                if (_claimedLmt.Orders != null)                {
                    _proxyOrdersLmt = _claimedLmt.Orders;
                    _poolSlotIndexLmt = _claimedLmt.SlotIndex;
                }
                else                {
                    _proxyOrdersLmt = new Order[MaxOrdersPerSlot];
                    _poolSlotIndexLmt = -1;
                }
            }
            _proxyOrdersLmt[0] = entry;
            if (_poolSlotIndexLmt >= 0)            {
                _photonSideband[_poolSlotIndexLmt].Account        = acct;
                _photonSideband[_poolSlotIndexLmt].FleetEntryName = fleetEntryName;
                _photonSideband[_poolSlotIndexLmt].ExpectedKey    = expectedKey;
                Thread.MemoryBarrier();
            }
            FleetDispatchSlot _slotLmt = new FleetDispatchSlot            {
                EntryPrice    = entry.LimitPrice > 0 ? entry.LimitPrice : 0,                StopPrice     = 0,                SignalTicks   = DateTime.UtcNow.Ticks,                PoolSlotIndex = _poolSlotIndexLmt,                OrderCount    = 1,                Quantity      = followerQty,                TargetCount   = 0,                Action        = (int)action,                ReservedDelta = reservedDelta            }
;
            _slotLmt.Shadow = ComputeFleetDispatchShadow(ref _slotLmt, _photonShadowSalt);
            Interlocked.Increment(ref _pendingFleetDispatchCount);
            if (_poolSlotIndexLmt >= 0 && _photonDispatchRing.TryEnqueue(ref _slotLmt))            {
                if (_photonMmioMirror != null)                {
                    try {
 _photonMmioMirror.TryPublish(ref _slotLmt);
 }
 catch {
 }
                }
            }
            else            {
                if (_poolSlotIndexLmt >= 0)                {
                    Order[] legacyOrdersLmt = new Order[] {
 entry }
;
                    _photonPool.ReleaseByIndex(_poolSlotIndexLmt);
                    _photonSideband[_poolSlotIndexLmt] = default(FleetDispatchSideband);
                    _proxyOrdersLmt = legacyOrdersLmt;
                }
                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest                {
                    Account = acct,                    Orders = _proxyOrdersLmt,                    FleetEntryName = fleetEntryName,                    ExpectedKey = expectedKey,                    ReservedDelta = reservedDelta,                    SignalTicks = DateTime.UtcNow.Ticks                }
);
            }
            syncPending         = false;
            reservedDelta       = 0;
            registeredForCleanup = false;
            dispatchLog.AppendLine(string.Format("  QUEUE | {
0,-28}
 | Limit        | PENDING",                acct.Name));
        }
}
}
