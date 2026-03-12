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

            int successCount = 0;
            int failCount = 0;

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
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
                            AddExpectedPositionDelta(ExpKey(acct.Name), reservedDelta);
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
                            AddExpectedPositionDelta(ExpKey(acct.Name), -reservedDelta);
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
                        AddExpectedPositionDelta(ExpKey(acct.Name), reservedDelta);

                        // 3. Submit as Atomic Group (Broker OCO)
                        acct.Submit(new[] { entry, stop, target });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // V12.Phase7 [C-02/GAP-2]: Undo expectedPositions reservation if submission failed.
                        if (reservedDelta != 0)
                            AddExpectedPositionDelta(ExpKey(acct.Name), -reservedDelta);
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
                Print(string.Format("[RMA] ExecuteRMAEntryV2 received invalid contracts={0}. Aborting entry.", contracts));
                return;
            }

            // [923B-FIX-A]: Zero-price guard ??" a Limit order at price=0 is treated as a Market order
            // by Apex/Tradovate, causing an immediate fill without price ever touching the RMA level.
            // Root cause: IPC path (UI.IPC.cs) can pass currentPrice=0 if lastKnownPrice<=0 AND
            // Close[0] is not yet initialized (strategy just loaded, pre-session bars not formed).
            if (price <= 0)
            {
                Print(string.Format("[RMA V2] ABORT: price={0:F2} is zero or negative. Refusing to submit Limit @ 0 -- would fill as Market. Ensure lastKnownPrice is valid before dispatching.", price));
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

                string baseSignal = "RMA_" + DateTime.Now.Ticks;
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
                    // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (L1345).
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
                    // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (L1345).
                    activePositions[localKey] = pos;

                    // V12.12: Register Master account in expectedPositions (was missing ??" caused false Reaper desyncs)
                    int localDelta = (direction == MarketPosition.Long) ? qty : -qty;
                    AddExpectedPositionDelta(ExpKey(Account.Name), localDelta);
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
                    if (!IsFleetAccount(acct)) continue;
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
                    string fleetKey = acct.Name + "_RMA_" + baseSignal;
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
                            OcoGroupId = "V12_" + GetStableHash(fleetKey),
                        };
                        // B966: Enqueue NOT applied -- ordering invariant: dicts BEFORE expectedPositions (L1479).
                        activePositions[fleetKey] = fleetFollowerPos; // FIRST: dicts registered atomically
                        entryOrders[fleetKey] = fEntry;               // REAPER hasWorkingEntry check reads these

                        MarkDispatchSyncPending(expectedKey);
                        syncPending = true;

                        reservedDelta = (direction == MarketPosition.Long) ? qty : -qty;
                        AddExpectedPositionDelta(expectedKey, reservedDelta); // SECOND: expectedPositions

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
                            AddExpectedPositionDelta(expectedKey, -reservedDelta);
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


        #endregion
    }
}
