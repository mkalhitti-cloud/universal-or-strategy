// Build 971: SIMA Execution -- ExecuteMultiAccountMarket, ExecuteMultiAccountBracket, ExecuteRMAEntryV2
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
        #region V12 SIMA Execution

        /// <summary>
        /// V12 SIMA: Execute a market order across ALL accounts matching the prefix
        /// </summary>
        private void ExecuteMultiAccountMarket(OrderAction action, int quantity, string signalName)
        {
            if (!EnableSIMA) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            // [Phase 9 LATENCY] T0: Start immediately after guards, before any work.
            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;

            int successCount = 0;
            int failCount = 0;

            // [Phase 9 LATENCY] T_LoopStart + batch log buffer (flushed once after loop).
            long tLoopStartTicks = sw.ElapsedTicks;
            var dispatchLog = new StringBuilder(512);

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    // V12.8: Fleet Active Check -- skip accounts NOT registered or disabled
                    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                    {
                        dispatchLog.AppendLine(string.Format("  SKIP | {0,-28} | Inactive", acct.Name));
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
                                dispatchLog.AppendLine(string.Format("  SKIP | {0,-28} | ConsistencyLock ${1:F2}", acct.Name, dailyPL));
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
                        dispatchLog.AppendLine(string.Format("    OK | {0,-28} | Market       | submitted", acct.Name));
                    }
                    catch (Exception ex)
                    {
                        // V12.Phase7 [GAP-3]: Undo expectedPositions reservation if submission failed.
                        // Delta may or may not have been applied (depends on where exception occurred),
                        // so rollback is conditional on whether reserve completed.
                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(ExpKey(acct.Name), -reservedDelta);
                        failCount++;
                        dispatchLog.AppendLine(string.Format("  FAIL | {0,-28} | {1}", acct.Name, ex.Message));
                    }
                }
            }

            // [Phase 9 LATENCY] T_Final: Fleet loop complete -- stop clock, flush forensic report.
            sw.Stop();
            long tFinalTicks = sw.ElapsedTicks;
            double totalMs = tFinalTicks * 1000.0 / Stopwatch.Frequency;
            double setupMs = (tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency;
            double loopMs = (tFinalTicks - tLoopStartTicks) * 1000.0 / Stopwatch.Frequency;

            var report = new StringBuilder(1024);
            report.AppendLine("+==============================================================+");
            report.AppendLine("|       FORENSIC PULSE REPORT  Phase 9 MULTI-ACCOUNT MARKET    |");
            report.AppendLine("+==============================================================+");
            report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   | STATUS |");
            report.AppendLine("+==============================================================+");
            report.Append(dispatchLog.ToString());
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine(string.Format("|  BROADCAST: {0} {1} | {2} OK / {3} FAIL", action, quantity, successCount, failCount));
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine("|  TIMING SUMMARY                                              |");
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine(string.Format("|  Setup Phase:  {0,8:F3} ms  |  Fleet Loop:  {1,8:F3} ms       |", setupMs, loopMs));
            report.AppendLine(string.Format("|  Total Elapsed: {0,8:F3} ms                                  |", totalMs));
            report.AppendLine("+==============================================================+");
            Print(report.ToString().TrimEnd());
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

            // [Phase 9 LATENCY] T0: Start immediately after guards, before any work.
            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;

            int successCount = 0;
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

            // [Phase 9 LATENCY] T_LoopStart + batch log buffer.
            long tLoopStartTicks = sw.ElapsedTicks;
            var dispatchLog = new StringBuilder(512);

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
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
                                dispatchLog.AppendLine(string.Format("  SKIP | {0,-28} | ConsistencyLock ${1:F2}", acct.Name, dailyPL));
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
                        dispatchLog.AppendLine(string.Format("    OK | {0,-28} | Bracket(3)   | submitted", acct.Name));
                    }
                    catch (Exception ex)
                    {
                        // V12.Phase7 [C-02/GAP-2]: Undo expectedPositions reservation if submission failed.
                        if (reservedDelta != 0)
                            AddExpectedPositionDeltaLocked(ExpKey(acct.Name), -reservedDelta);
                        dispatchLog.AppendLine(string.Format("  FAIL | {0,-28} | {1}", acct.Name, ex.Message));
                    }
                }
            }

            // [Phase 9 LATENCY] T_Final: Fleet loop complete -- stop clock, flush forensic report.
            sw.Stop();
            long tFinalTicks = sw.ElapsedTicks;
            double totalMs = tFinalTicks * 1000.0 / Stopwatch.Frequency;
            double setupMs = (tLoopStartTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency;
            double loopMs = (tFinalTicks - tLoopStartTicks) * 1000.0 / Stopwatch.Frequency;

            var report = new StringBuilder(1024);
            report.AppendLine("+==============================================================+");
            report.AppendLine("|       FORENSIC PULSE REPORT  Phase 9 MULTI-ACCOUNT BRACKET   |");
            report.AppendLine("+==============================================================+");
            report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   | STATUS |");
            report.AppendLine("+==============================================================+");
            report.Append(dispatchLog.ToString());
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine(string.Format("|  PATH B BROADCAST: {0} Brackets Submitted", successCount));
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine("|  TIMING SUMMARY                                              |");
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine(string.Format("|  Setup Phase:  {0,8:F3} ms  |  Fleet Loop:  {1,8:F3} ms       |", setupMs, loopMs));
            report.AppendLine(string.Format("|  Total Elapsed: {0,8:F3} ms                                  |", totalMs));
            report.AppendLine("+==============================================================+");
            Print(report.ToString().TrimEnd());
        }

        /// <summary>
        /// V12 SIMA: Master Flatten - closes local position and broadcasts to the entire fleet
        /// </summary>
        // Duplicate FlattenAll removed - consolidated into line 4387 version

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Helper 1: Validate all entry guards
        /// </summary>
        private bool ValidateRMAEntryGuards(double price, int contracts, MarketPosition direction)
        {
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten (INV-4.1)
            if (isFlattenRunning) return false;

            // [A1]: Defensive guard -- caller must pre-calculate a valid quantity.
            if (contracts <= 0)
            {
                Print(string.Format("[RMA] ExecuteRMAEntryV2 received invalid contracts={0}. Aborting entry.", contracts));
                return false;
            }

            // [923B-FIX-A]: Zero-price guard -- a Limit order at price=0 is treated as a Market order
            // by Apex/Tradovate, causing an immediate fill without price ever touching the RMA level.
            // Root cause: IPC path (UI.IPC.cs) can pass currentPrice=0 if lastKnownPrice<=0 AND
            // Close[0] is not yet initialized (strategy just loaded, pre-session bars not formed).
            if (price <= 0)
            {
                Print(string.Format("[RMA V2] ABORT: price={0:F2} is zero or negative. Refusing to submit Limit @ 0 -- would fill as Market. Ensure lastKnownPrice is valid before dispatching.", price));
                return false;
            }

            // Phase 6 [MG-D2]: MetadataGuard -- reject duplicate RMA dispatch signals.
            string rmaSig = string.Format("RMA_{0}_{1}_{2:F2}", direction, contracts, price);
            if (!MetadataGuardDuplicate(rmaSig, "RMA_V2"))
            {
                Print("[RMA V2] (!) Duplicate dispatch rejected by MetadataGuard");
                return false;
            }

            return true;
        }

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Helper 2: Calculate bracket prices and distribution
        /// </summary>
        private struct RMABracketPrices
        {
            public double StopPrice;
            public double T1Price, T2Price, T3Price, T4Price, T5Price;
            public int Rt1, Rt2, Rt3, Rt4, Rt5;
        }

        private RMABracketPrices CalculateRMABracketPrices(double price, MarketPosition direction, int qty)
        {
            // [LEAK-01]: Use centralized ATR calculator (ceiling + min/max guards, fleet-ready).
            double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
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

            return new RMABracketPrices
            {
                StopPrice = stopPrice,
                T1Price = t1Price,
                T2Price = t2Price,
                T3Price = t3Price,
                T4Price = t4Price,
                T5Price = t5Price,
                Rt1 = rt1,
                Rt2 = rt2,
                Rt3 = rt3,
                Rt4 = rt4,
                Rt5 = rt5
            };
        }

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Helper 3: Submit local account entry (ATOMIC: INV-4.3)
        /// </summary>
        private bool SubmitLocalRMAEntry(
            string baseSignal, OrderAction entryAction, int qty, double price,
            MarketPosition direction, RMABracketPrices prices, string symmetryDispatchId)
        {
            string localKey = baseSignal;
            Order entryOrder = null;
            
            try
            {
                entryOrder = SubmitOrderUnmanaged(0, entryAction, OrderType.Limit, qty, price, 0, "", localKey);
            }
            catch (Exception ex)
            {
                // H01: Roll back symmetry dispatch registration on order submission exception
                SymmetryGuardRollbackDispatch(symmetryDispatchId);
                Print(string.Format("[SIMA RMA V2] ORDER SUBMISSION EXCEPTION: {0} - Dispatch rolled back", ex.Message));
                throw;
            }
            
            if (entryOrder != null)
            {
                SymmetryGuardRegisterMasterEntry(symmetryDispatchId, localKey);
                // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (L1345).
                entryOrders[localKey] = entryOrder;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = localKey,
                    Direction = direction,
                    TotalContracts = qty,
                    T1Contracts = prices.Rt1,
                    T2Contracts = prices.Rt2,
                    T3Contracts = prices.Rt3,
                    T4Contracts = prices.Rt4,
                    T5Contracts = prices.Rt5,
                    RemainingContracts = qty,
                    EntryPrice = price,
                    InitialStopPrice = prices.StopPrice,
                    CurrentStopPrice = prices.StopPrice,
                    Target1Price = prices.T1Price,
                    Target2Price = prices.T2Price,
                    Target3Price = prices.T3Price,
                    Target4Price = prices.T4Price,
                    Target5Price = prices.T5Price,
                    EntryOrderType = OrderType.Limit,
                    EntryFilled = false,
                    BracketSubmitted = false, // V12.7: Brackets deferred until entry fills
                    IsRMATrade = true
                };
                // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (L1345).
                activePositions[localKey] = pos;

                // V12.12: Register Master account in expectedPositions (was missing -- caused false Reaper desyncs)
                int localDelta = (direction == MarketPosition.Long) ? qty : -qty;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), localDelta);
                Print(string.Format("[SIMA] Master expectedPositions updated: {0} delta={1}", Account.Name, localDelta));

                // V12.7: Do NOT submit stop/target here -- they will be submitted by
                // SubmitBracketOrders() when the entry limit fills in OnOrderUpdate.
                // Submitting them now would cause instant fills on marketable targets.

                Print(string.Format("[SIMA RMA V2] LOCAL ENTRY ONLY (Limit): {0} | Brackets deferred until fill", localKey));
                return true;
            }
            else
            {
                Print("[SIMA RMA V2] ERROR: Local entry returned null");
                return false;
            }
        }

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Helper 4: Process single fleet account (ATOMIC: INV-4.3)
        /// </summary>
        private bool ProcessSingleFleetRMAAccount(
            Account acct, string baseSignal, OrderAction entryAction, int qty, double price,
            MarketPosition direction, RMABracketPrices prices, string symmetryDispatchId,
            StringBuilder dispatchLog)
        {
            // V12.8: Fleet Manager toggle -- skip if account NOT registered or explicitly disabled
            if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
            {
                dispatchLog.AppendLine(string.Format("  SKIP | {0,-28} | Inactive", acct.Name));
                return false;
            }

            // Consistency Lock
            if (EnableConsistencyLock)
            {
                double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                if (dailyPL >= MaxDailyProfitCap)
                {
                    dispatchLog.AppendLine(string.Format("  SKIP | {0,-28} | ConsistencyLock ${1:F2}", acct.Name, dailyPL));
                    return false;
                }
            }

            // [923B-FIX-B]: fleetKey declared outside try so catch can access it for dict rollback.
            string fleetKey = acct.Name + "_RMA_" + baseSignal;
            string expectedKey = ExpKey(acct.Name);
            int reservedDelta = 0;
            bool syncPending = false;
            try
            {
                SymmetryGuardRegisterFollower(symmetryDispatchId, fleetKey);
                string ocoId = fleetKey;

                // V12.10: Submit ENTRY ONLY -- brackets deferred until fill (unified with leader)
                Order fEntry = acct.CreateOrder(Instrument, entryAction, OrderType.Limit,
                    TimeInForce.Gtc, qty, price, 0, ocoId, fleetKey, null);

                // [M8.1 NRE-01]: CreateOrder returns null for disconnected or invalid account/instrument pairs.
                // Guard before reservation -- expectedPositions not yet incremented, no rollback needed.
                if (fEntry == null)
                {
                    dispatchLog.AppendLine(string.Format("  FAIL | {0,-28} | CreateOrder returned null", acct.Name));
                    return false;
                }

                // [923B-FIX-B]: Phantom-Fix FIX-1 backport -- register tracking dicts BEFORE
                // updating expectedPositions. Mirrors the fix already applied to ExecuteSmartDispatchEntry
                // (SIMA.cs Phantom-Fix comment at ~line 554).
                //
                // OLD (broken) order: expectedPositions FIRST -> Submit -> entryOrders/activePositions LAST.
                // Race: REAPER background thread fires between steps 1 and 3, observes non-zero
                //       expectedPositions with no entry in entryOrders -> hasWorkingEntry=false
                //       -> phantom repair queued -> second Limit order submitted at same price
                //       -> original entry orphaned -> double fill or naked position on price touch.
                //
                // FIXED order: build PositionInfo -> register dicts atomically (stateLock) FIRST
                //              -> expectedPositions SECOND -> Submit LAST.
                // V12.1101E: Full 5-target distribution mirrors Master exactly.
                PositionInfo fleetFollowerPos = new PositionInfo
                {
                    SignalName = fleetKey,
                    Direction = direction,
                    TotalContracts = qty,
                    RemainingContracts = qty,
                    EntryPrice = price,
                    InitialStopPrice = prices.StopPrice,
                    CurrentStopPrice = prices.StopPrice,
                    Target1Price = prices.T1Price,
                    Target2Price = prices.T2Price,
                    Target3Price = prices.T3Price,
                    Target4Price = prices.T4Price,
                    Target5Price = prices.T5Price,
                    T1Contracts = prices.Rt1,
                    T2Contracts = prices.Rt2,
                    T3Contracts = prices.Rt3,
                    T4Contracts = prices.Rt4,
                    T5Contracts = prices.Rt5,
                    EntryOrderType = OrderType.Limit,
                    EntryFilled = false,
                    IsRMATrade = true,
                    IsFollower = true,
                    ExecutingAccount = acct,
                    BracketSubmitted = false,   // V12.10: deferred -- OnAccountExecutionUpdate submits on fill
                    ExtremePriceSinceEntry = price,
                    CurrentTrailLevel = 0,
                    // Build 936 [FIX-2]: Deterministic bracket OCO group ID for broker-native stop+target linking.
                    OcoGroupId = "V12_" + GetStableHash(fleetKey),
                };
                // B966: Enqueue NOT applied -- ordering invariant: dicts BEFORE expectedPositions (L1479).
                activePositions[fleetKey] = fleetFollowerPos; // FIRST: dicts registered atomically
                entryOrders[fleetKey] = fEntry;               // REAPER hasWorkingEntry check reads these

                MarkDispatchSyncPending(expectedKey);
                syncPending = true;

                // Phase 6 [FSM-P3]: Proactive FSM for RMA V2 fleet entries.
                // Entry-only (brackets deferred until fill via SymmetryGuard).
                // State = Submitted (direct submit, no pump queue).
                if (!_followerBrackets.ContainsKey(fleetKey))
                {
                    var rmaFsm = new FollowerBracketFSM
                    {
                        AccountName = acct.Name,
                        EntryName = fleetKey,
                        State = FollowerBracketState.Submitted,
                        RemainingContracts = qty,
                        EntryOrder = fEntry,
                        ExpectedEntryPrice = price,
                        LastUpdateUtc = DateTime.UtcNow
                    };
                    _followerBrackets.TryAdd(fleetKey, rmaFsm);
                }

                reservedDelta = (direction == MarketPosition.Long) ? qty : -qty;
                AddExpectedPositionDeltaLocked(expectedKey, reservedDelta); // SECOND: expectedPositions

                acct.Submit(new[] { fEntry }); // LAST -- stateLock not held here

                // Phase 6 [FSM-P3]: Register OrderId for O(1) FSM lookup (populated by Submit)
                if (fEntry != null && !string.IsNullOrEmpty(fEntry.OrderId))
                    _orderIdToFsmKey[fEntry.OrderId] = fleetKey;

                ClearDispatchSyncPending(expectedKey);
                syncPending = false;
                // stopOrders/target1..target5 are set by follower bracket submission on fill

                dispatchLog.AppendLine(string.Format("    OK | {0,-28} | Limit RMA    | submitted", acct.Name));
                return true;
            }
            catch (Exception ex)
            {
                if (syncPending)
                {
                    ClearDispatchSyncPending(expectedKey);
                    syncPending = false;
                }

                // [923B-FIX-B]: Full rollback -- dicts were registered before expectedPositions,
                // so both must be cleaned up on Submit failure (mirrors ExecuteSmartDispatchEntry catch).
                if (reservedDelta != 0)
                    AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                activePositions.TryRemove(fleetKey, out _);
                entryOrders.TryRemove(fleetKey, out _);
                // Phase 6: Clean up proactive FSM on dispatch failure
                _followerBrackets.TryRemove(fleetKey, out _);
                dispatchLog.AppendLine(string.Format("  FAIL | {0,-28} | {1}", acct.Name, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Places limit entry + bracket on the local chart account,
        /// then iterates Account.All to place the same order on every fleet account matching AccountPrefix.
        /// CRITICAL: Every account's entry order is registered in entryOrders AND activePositions
        /// with a unique key (accountName + "_RMA") so ManageCIT can chase the entire fleet.
        /// </summary>
        private void ExecuteRMAEntryV2(double price, MarketPosition direction, int contracts)
        {
            // Helper 1: Validate all entry guards
            if (!ValidateRMAEntryGuards(price, contracts, direction))
                return;

            // [Phase 9 LATENCY] T0: Start after validation guards pass, before setup work.
            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;

            try
            {
                // Helper 2: Calculate bracket prices and distribution
                RMABracketPrices prices = CalculateRMABracketPrices(price, direction, contracts);

                string baseSignal = "RMA_" + DateTime.Now.Ticks;
                OrderAction entryAction = (direction == MarketPosition.Long) ? OrderAction.Buy : OrderAction.SellShort;
                string symmetryDispatchId = SymmetryGuardBeginDispatch("RMA", entryAction, contracts, price);

                // [Phase 9 LATENCY] T_SetupDone: Calculation + metadata guard complete.
                long tSetupDoneTicks = sw.ElapsedTicks;

                Print(string.Format("[SIMA RMA V2] {0} @ {1} | Stop: {2} | T1: {3} | T2: {4} | T3: {5} | T4: {6} | T5: {7} | Qty: {8}",
                    direction, price, prices.StopPrice, prices.T1Price, prices.T2Price, prices.T3Price, prices.T4Price, prices.T5Price, contracts));

                // =======================================================
                // 1. LOCAL ACCOUNT: SubmitOrderUnmanaged (chart-visible)
                // =======================================================
                // Helper 3: Submit local account entry (ATOMIC: INV-4.3)
                bool localSubmitted;
                try
                {
                    localSubmitted = SubmitLocalRMAEntry(baseSignal, entryAction, contracts, price, direction, prices, symmetryDispatchId);
                }
                catch (Exception localEx)
                {
                    // V12.H01: Rollback symmetry dispatch on local entry failure to prevent orphaned followers
                    // Specific handling for local submission exceptions (margin, tick size, etc.)
                    SymmetryGuardRollbackDispatch(symmetryDispatchId);
                    Print(string.Format("[SIMA RMA V2] LOCAL ENTRY FAILED: {0} - Dispatch rolled back", localEx.Message));
                    return;
                }

                // P1-FIX (Iteration 3): Check boolean result - abort if local entry returned false (null order)
                if (!localSubmitted)
                {
                    SymmetryGuardRollbackDispatch(symmetryDispatchId);
                    Print("[SIMA RMA V2] LOCAL ENTRY NULL - Dispatch rolled back to prevent orphaned followers");
                    return;
                }

                // =======================================================
                // 2. SIMA FLEET: Iterate Account.All for followers
                // =======================================================
                // P2-FIX (Iteration 4): Dead code removed - EnableSIMA check is unreachable after early returns

                int fleetOk = 0;
                int fleetSkip = 0;
                // [Phase 9 LATENCY] T_LoopStart: Fleet iteration begins.
                long tLoopStartTicks = sw.ElapsedTicks;
                var dispatchLog = new StringBuilder(512);

                foreach (Account acct in Account.All)
                {
                    if (!IsFleetAccount(acct)) continue;
                    if (acct == this.Account) continue; // local already done

                    // Helper 4: Process single fleet account (ATOMIC: INV-4.3)
                    if (ProcessSingleFleetRMAAccount(acct, baseSignal, entryAction, contracts, price,
                        direction, prices, symmetryDispatchId, dispatchLog))
                    {
                        fleetOk++;
                    }
                    else
                    {
                        fleetSkip++;
                    }
                }

                // [Phase 9 LATENCY] T_Final: Fleet loop complete -- stop clock, flush forensic report.
                sw.Stop();
                long tFinalTicks = sw.ElapsedTicks;
                double totalMs = tFinalTicks * 1000.0 / Stopwatch.Frequency;
                double setupMs = (tSetupDoneTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency;
                double localMs = (tLoopStartTicks - tSetupDoneTicks) * 1000.0 / Stopwatch.Frequency;
                double loopMs = (tFinalTicks - tLoopStartTicks) * 1000.0 / Stopwatch.Frequency;

                var report = new StringBuilder(1024);
                report.AppendLine("+==============================================================+");
                report.AppendLine("|       FORENSIC PULSE REPORT  Phase 9 RMA ENTRY V2            |");
                report.AppendLine("+==============================================================+");
                report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   | STATUS |");
                report.AppendLine("+==============================================================+");
                report.Append(dispatchLog.ToString());
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine(string.Format("|  FLEET: {0} dispatched, {1} skipped", fleetOk, fleetSkip));
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine("|  TIMING SUMMARY (4-phase)                                    |");
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine(string.Format("|  Setup+Calc:   {0,8:F3} ms  |  Local Acct:  {1,8:F3} ms       |", setupMs, localMs));
                report.AppendLine(string.Format("|  Fleet Loop:   {0,8:F3} ms  |  Total:       {1,8:F3} ms       |", loopMs, totalMs));
                report.AppendLine("+==============================================================+");
                Print(report.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Print(string.Format("[SIMA RMA V2] ERROR: {0}", ex.Message));
            }
        }


        #endregion
    }
}
