// V12.Phase7 MODULAR: RETEST Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteRetestEntry, DeactivateRetestMode, ExecuteRetestManualEntry
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
        #region RETEST Entry Logic (V8.4)

        /// <summary>
        /// A5: Returns the stop distance for an auto-detected RETEST entry.
        /// Uses RMAStopATRMultiplier when isRetestRmaMode is active, otherwise RetestATRMultiplier.
        /// Callers (A7 UI layer) should invoke this before calling ExecuteRetestEntry to pre-calculate contracts.
        /// For manual RETEST entries call CalculateATRStopDistance(RMAStopATRMultiplier) directly.
        /// </summary>
        private double CalculateRetestStopDistance()
        {
            double multToUse = isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier;
            return CalculateATRStopDistance(multToUse);
        }

        // V8.4: Execute RETEST entry - auto-detects direction based on price vs OR Mid
        private void ExecuteRetestEntry(int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (!RetestEnabled)
            {
                Print("RETEST mode is disabled");
                return;
            }

            // V12.1101E [B-2]: Session-scoped latch -- one RETEST entry per OR session maximum.
            // Resets automatically in ResetOR() at the start of each new session.
            if (retestFiredThisSession)
            {
                Print("RETEST: Already fired this session -- latch active, ignoring duplicate arm");
                return;
            }

            if (!orComplete)
            {
                Print("Cannot execute RETEST - OR not complete yet");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute RETEST entry - ATR not available yet");
                return;
            }

            if (contracts <= 0)
            {
                Print(string.Format("[RETEST] ExecuteRetestEntry received invalid contracts={0}. Aborting entry.", contracts));
                return;
            }

            try
            {
                // Use last known price for direction determination
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // Auto-detect direction: Price > OR Mid = LONG, Price < OR Mid = SHORT
                MarketPosition direction;
                double entryPrice;

                if (currentPrice > sessionMid)
                {
                    direction = MarketPosition.Long;
                    entryPrice = sessionHigh;  // Entry at OR High (NO buffer)
                    Print(string.Format("RETEST: Price above OR Mid ({0:F2} > {1:F2}) = LONG at OR High {2:F2}",
                        currentPrice, sessionMid, entryPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    entryPrice = sessionLow;   // Entry at OR Low (NO buffer)
                    Print(string.Format("RETEST: Price below OR Mid ({0:F2} < {1:F2}) = SHORT at OR Low {2:F2}",
                        currentPrice, sessionMid, entryPrice));
                }

                // Calculate stop and targets using ATR
                double multToUse = isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier;
                Print(string.Format("V12.20: RETEST Multiplier -> Mode={0} Using={1:F2}x",
                    isRetestRmaMode ? "RMA" : "STD", multToUse));
                double stopDistance = CalculateATRStopDistance(multToUse); // V12.30: Ceiling-rounded

                // V12.Phase6 [TICK-01]: All prices rounded to valid tick increments
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance);

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "RetestLong" : "RetestShort";
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    Target4Price = target4Price,
                    Target5Price = target5Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    EntryOrderType = OrderType.Limit,
                    IsRMATrade = isRetestRmaMode,
                    IsTRENDTrade = false,
                    IsRetestTrade = true,              // V8.4: Mark as retest trade
                    RetestTrailActivated = false,      // V8.4: Trail not activated yet
                    OcoGroupId = "V12_" + GetStableHash(entryName)
                };
                ApplyTargetLadderGuard(pos);

                { var _en966 = entryName; var _p966 = pos; Enqueue(ctx => { ctx.activePositions[_en966] = _p966; }); }

                // Build 1102Y-V3 [MS-07]: Register Master expected BEFORE Limit entry.
                int masterDeltaRetest = (direction == MarketPosition.Long) ? contracts : -contracts;
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaRetest); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }

                // Submit LIMIT order at OR High/Low (NO buffer)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                if (entryOrder == null)
                {
                    { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaRetest); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }
                    activePositions.TryRemove(entryName, out _); // [Build 956]: Clean pre-registered state on null submit.
                    Print("[ERROR][1102Y-V3] RETEST SubmitOrderUnmanaged NULL for " + entryName + " -- rolled back.");
                    return; // [Build 954]: Do not latch session or dispatch SIMA for a failed order.
                }

                { var _en966 = entryName; var _eo966 = entryOrder; Enqueue(ctx => { ctx.entryOrders[_en966] = _eo966; }); }
                retestFiredThisSession = true;  // V12.1101E [B-2]: Arm latch -- no further RETEST entries this session

                Print(string.Format("RETEST ENTRY ORDER: {0} {1}@{2:F2} | ATR: {3:F2}", signalName, contracts, entryPrice, currentATR));
                Print(string.Format("RETEST STOP: {0:F2} ({1:F2}x ATR = {2:F2}pts)",
                    stopPrice, RetestATRMultiplier, stopDistance));
                Print(string.Format("RETEST TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@{8:F2} | T5:{9}@{10:F2} (Runner targets trail-only)",
                    t1Qty, target1Price, target1Price - entryPrice,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                // V12.1: Smart Dispatch to SIMA Fleet
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "RETEST",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        contracts,
                        entryPrice,
                        OrderType.Limit,
                        entryName);
                }

                // Deactivate RETEST mode after entry (one-shot)
                DeactivateRetestMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRetestEntry: " + ex.Message);
            }
        }


        private void DeactivateRetestMode()
        {
            isRetestModeActive = false;
        }

        /// <summary>
        /// V12.27: RETEST manual entry at user-specified price using Limit Order with RMA targets.
        /// Uses RMA stop multiplier regardless of the R toggle state.
        /// </summary>
        private void ExecuteRetestManualEntry(double manualPrice, MarketPosition direction, int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print("V12.27 RETEST_MANUAL: Ignored - ATR not available");
                return;
            }

            if (contracts <= 0)
            {
                Print(string.Format("[RETEST] ExecuteRetestManualEntry received invalid contracts={0}. Aborting entry.", contracts));
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: Always uses RMA multiplier for manual retest entries
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                // V12.Phase6 [TICK-01]: All prices rounded to valid tick increments
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance);

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "RetestMnlLong" : "RetestMnlShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    Target4Price = target4Price,
                    Target5Price = target5Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    EntryOrderType = OrderType.Limit,
                    IsRMATrade = true,  // Uses RMA targets
                    IsRetestTrade = true,
                    RetestTrailActivated = false,
                    OcoGroupId = "V12_" + GetStableHash(entryName)
                };
                ApplyTargetLadderGuard(pos);

                { var _en966 = entryName; var _p966 = pos; Enqueue(ctx => { ctx.activePositions[_en966] = _p966; }); }

                // Build 1102Y-V3 [MS-08]: Register Master expected BEFORE Limit entry.
                int masterDeltaRetestMnl = (direction == MarketPosition.Long) ? contracts : -contracts;
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaRetestMnl); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }

                // Submit LIMIT order at manual price
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                if (entryOrder == null)
                {
                    { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaRetestMnl); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }
                    activePositions.TryRemove(entryName, out _); // [Build 956]: Clean pre-registered state on null submit.
                    Print("[ERROR][1102Y-V3] RETEST_MANUAL SubmitOrderUnmanaged NULL for " + entryName + " -- rolled back.");
                    return; // [Build 956]: Do not assign null entryOrder or dispatch SIMA for a failed order.
                }
                { var _en966 = entryName; var _eo966 = entryOrder; Enqueue(ctx => { ctx.entryOrders[_en966] = _eo966; }); }

                Print(string.Format("V12.27 RETEST_MANUAL: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | RMA Targets",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 RETEST_MANUAL TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@{7:F2} | T5:{8}@{9:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "RETEST_MNL",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        contracts,
                        entryPrice,
                        OrderType.Limit,
                        entryName);
                }

            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRetestManualEntry: " + ex.Message);
            }
        }

        #endregion
    }
}
