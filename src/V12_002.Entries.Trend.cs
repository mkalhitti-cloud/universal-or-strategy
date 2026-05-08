// V12.Phase7 MODULAR: TREND Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteTRENDEntry, CreateTRENDPosition,
//           DeactivateTRENDMode, ExecuteTRENDManualEntry
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
        #region TREND Entry Logic (V8.2)

        /// <summary>
        /// Calculates the weighted-average ATR stop distance used for TREND position sizing.
        /// E1 uses TRENDEntry1ATRMultiplier (or RMAStopATRMultiplier in RMA mode), weighted 1/3.
        /// E2 uses TRENDEntry2ATRMultiplier (or RMAStopATRMultiplier in RMA mode), weighted 2/3.
        /// Pure math on indicator/property values -- no side effects, safe to call from UI layer.
        /// </summary>
        private double CalculateTRENDStopDistance()
        {
            double e1Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
            double e2Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
            double e1StopDist = CalculateATRStopDistance(e1Mult);
            double e2StopDist = CalculateATRStopDistance(e2Mult);
            return (e1StopDist * (1.0 / 3.0)) + (e2StopDist * (2.0 / 3.0));
        }

        /// <summary>
        /// V8.2: Execute TREND trade with dual limit orders
        /// Entry 1 (1/3) at 9 EMA with fixed 2pt stop
        /// Entry 2 (2/3) at 15 EMA with 1.1x ATR trailing stop off EMA15
        /// </summary>
        private void ExecuteTRENDEntry(int contracts)
        {
            if (!ExecuteTREND_Preflight(contracts))
            {
                return;
            }

            // V11: Trend RMA (9/15 Split) Mode
            if (isTrendRmaMode)
            {
                Print(string.Format("V12.20: TREND Multiplier -> Mode=RMA (9/15 Split) ATR={0:F2}", currentATR));
                ExecuteTrendSplitEntry(contracts);
                return;
            }

            // V8.2: Ensure we have enough bars for EMA calculation
            if (CurrentBar < 20)
            {
                Print("Cannot execute TREND entry - not enough bars (CurrentBar=" + CurrentBar + ")");
                return;
            }
            try
            {
                MarketPosition direction;
                double currentPrice;
                double ema9Value;
                double ema15Value;
                if (!ExecuteTREND_ResolveDirection(out currentPrice, out ema9Value, out ema15Value, out direction))
                {
                    return;
                }

                int totalContracts;
                int entry1Qty;
                int entry2Qty;
                string entry1Name;
                string entry2Name;
                double entry1Price;
                double entry2Price;
                double stop1Price;
                double stop2Price;
                PositionInfo pos1;
                PositionInfo pos2;
                ExecuteTREND_CalculateLegs(
                    contracts,
                    direction,
                    ema9Value,
                    ema15Value,
                    out totalContracts,
                    out entry1Qty,
                    out entry2Qty,
                    out entry1Name,
                    out entry2Name,
                    out entry1Price,
                    out entry2Price,
                    out stop1Price,
                    out stop2Price,
                    out pos1,
                    out pos2);

                Order entryOrder1;
                if (!ExecuteTREND_SubmitLeg1(direction, entry1Qty, entry1Price, entry1Name, pos1, out entryOrder1))
                {
                    return;
                }
                if (!ExecuteTREND_SubmitLeg2(direction, entry2Qty, entry2Price, entry1Name, entry2Name, pos2, entryOrder1))
                {
                    return;
                }

                Print(string.Format("TREND ORDERS PLACED: {0} Total={1} contracts",
                    direction == MarketPosition.Long ? "LONG" : "SHORT", totalContracts));
                Print(string.Format("  E1: {0}@{1:F2} (EMA9) | Stop: {2:F2} ({3}xATR from EMA9)",
                    entry1Qty, ema9Value, stop1Price, TRENDEntry1ATRMultiplier));
                Print(string.Format("  E2: {0}@{1:F2} (EMA15) | Stop: {2:F2} ({3}xATR trail)",
                    entry2Qty, ema15Value, stop2Price, TRENDEntry2ATRMultiplier));
                ExecuteTREND_DispatchSima(direction, totalContracts, currentPrice, entry1Name, entry2Name);
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTRENDEntry: " + ex.Message);
            }
        }

        private bool ExecuteTREND_Preflight(int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed())
            {
                return false;
            }
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning)
            {
                return false;
            }

            if (contracts <= 0)
            {
                Print(string.Format("[TREND] ExecuteTRENDEntry received invalid contracts={0}. Aborting entry.", contracts));
                return false;
            }

            // V8.2 FIX: Only execute when on primary series (BarsInProgress=0)
            // This ensures we get correct EMA values from BarsArray[0]
            if (BarsInProgress != 0)
            {
                pendingTRENDEntry = true;
                Print("TREND entry deferred to next primary bar update (BarsInProgress=" + BarsInProgress + ")");
                return false;
            }

            // Clear pending flag since we're executing now
            pendingTRENDEntry = false;

            if (!TRENDEnabled)
            {
                Print("TREND mode is disabled");
                return false;
            }

            if (currentATR <= 0 || ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND entry - indicators not ready");
                return false;
            }

            return true;
        }

        private bool ExecuteTREND_ResolveDirection(out double currentPrice, out double ema9Value, out double ema15Value, out MarketPosition direction)
        {
            // Get current tick price for direction determination
            currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

            // V8.2: Use stored EMA instances (now guaranteed BarsInProgress=0)
            if (ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND entry - EMA indicators not initialized");
                ema9Value = 0;
                ema15Value = 0;
                direction = MarketPosition.Flat;
                return false;
            }

            // V8.10: Use [0] (live tick) for real-time EMA values since Calculate.OnPriceChange updates EMAs on every tick
            ema9Value = ema9[0];
            ema15Value = ema15[0];

            // V8.10 DEBUG
            Print(string.Format("TREND DEBUG: ema9[0]={0:F2} ema15[0]={1:F2} Price={2:F2}", ema9Value, ema15Value, currentPrice));
            Print(string.Format("TREND DEBUG: Close[0]={0:F2} CurrentBar={1} BarsInProgress={2}",
                Close[0], CurrentBar, BarsInProgress));

            // Sanity check: EMAs should be different
            if (Math.Abs(ema9Value - ema15Value) < tickSize * 2)
            {
                Print(string.Format("WARNING: EMAs very close ({0:F2} vs {1:F2})", ema9Value, ema15Value));
            }

            // Direction: EMA below price = LONG (buying pullback), EMA above = SHORT
            if (ema9Value < currentPrice)
            {
                direction = MarketPosition.Long;
                Print(string.Format("TREND: EMA9 below price ({0:F2} < {1:F2}) = LONG setup", ema9Value, currentPrice));
            }
            else
            {
                direction = MarketPosition.Short;
                Print(string.Format("TREND: EMA9 above price ({0:F2} > {1:F2}) = SHORT setup", ema9Value, currentPrice));
            }

            return true;
        }

        private void ExecuteTREND_CalculateLegs(
            int contracts,
            MarketPosition direction,
            double ema9Value,
            double ema15Value,
            out int totalContracts,
            out int entry1Qty,
            out int entry2Qty,
            out string entry1Name,
            out string entry2Name,
            out double entry1Price,
            out double entry2Price,
            out double stop1Price,
            out double stop2Price,
            out PositionInfo pos1,
            out PositionInfo pos2)
        {
            // V8.31: Both E1 and E2 now use ATR-based stops from live EMAs
            double e1MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
            double e2MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
            Print(string.Format("V12.20: TREND Multiplier -> Mode={0} E1={1:F2}x E2={2:F2}x",
                isTrendRmaMode ? "RMA" : "STD", e1MultTrend, e2MultTrend));

            double e1StopDist = CalculateATRStopDistance(e1MultTrend); // V12.30: Ceiling-rounded
            double e2StopDist = CalculateATRStopDistance(e2MultTrend); // V12.30: Ceiling-rounded

            // Weighted average stop distance for the group (used for logging only; sizing comes from caller)
            double weightedStopDist = (e1StopDist * (1.0/3.0)) + (e2StopDist * (2.0/3.0));

            totalContracts = contracts;

            // TREND-SPLIT-FIX: Strict floor -- E1 (EMA9) gets ?Total/3?, E2 (EMA15) gets remainder.
            // Prevents risk budget overrun when Math.Ceiling pushes E1 past 1/3 of total contracts.
            entry1Qty = Math.Max(1, totalContracts / 3);
            entry2Qty = Math.Max(1, totalContracts - entry1Qty);

            // Final validation: totalContracts = sum of entries
            totalContracts = entry1Qty + entry2Qty;

            Print(string.Format("TREND RISK: Risk=${0} | E1Stop={1:F2} | E2Stop={2:F2} | WeightedDist={3:F2} | TotalQty={4}",
                MaxRiskAmount, e1StopDist, e2StopDist, weightedStopDist, totalContracts));
            Print(string.Format("TREND SPLIT: E1Qty={0} (1/3) | E2Qty={1} (2/3)", entry1Qty, entry2Qty));

            string timestamp = DateTime.UtcNow.ToString("HHmmssffff", CultureInfo.InvariantCulture);
            string trendGroupId = "TREND_" + timestamp;
            entry1Name = trendGroupId + "_E1";
            entry2Name = trendGroupId + "_E2";

            // V8.31: ENTRY 1: 1/3 at 9 EMA with ATR-based stop from live EMA9
            // V12.Phase6 [TICK-01]: Round EMA to valid tick increment before broker submission
            entry1Price = Instrument.MasterInstrument.RoundToTickSize(ema9Value);
            double e1AtrStop = CalculateATRStopDistance(e1MultTrend);  // V12.30: Ceiling-rounded
            stop1Price = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                ? entry1Price - e1AtrStop  // V8.31: Stop is 1.1x ATR below live EMA9
                : entry1Price + e1AtrStop); // V8.31: Stop is 1.1x ATR above live EMA9

            // ENTRY 2: 2/3 at 15 EMA with ATR trailing stop
            // V12.Phase6 [TICK-01]: Round EMA to valid tick increment before broker submission
            entry2Price = Instrument.MasterInstrument.RoundToTickSize(ema15Value);
            stop2Price = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                ? entry2Price - CalculateATRStopDistance(e2MultTrend)
                : entry2Price + CalculateATRStopDistance(e2MultTrend));

            // Create position info for Entry 1
            pos1 = CreateTRENDPosition(entry1Name, direction, entry1Price, stop1Price,
                entry1Qty, true, trendGroupId, isTrendRmaMode);
            // Build 1102Y-V3 [LG-01]: Enforce staircase rule on E1.
            ApplyTargetLadderGuard(pos1);

            // Create position info for Entry 2
            pos2 = CreateTRENDPosition(entry2Name, direction, entry2Price, stop2Price,
                entry2Qty, false, trendGroupId, isTrendRmaMode);
            // Build 1102Y-V3 [LG-01]: Enforce staircase rule on E2.
            ApplyTargetLadderGuard(pos2);
        }

        private bool ExecuteTREND_SubmitLeg1(MarketPosition direction, int entry1Qty, double entry1Price, string entry1Name, PositionInfo pos1, out Order entryOrder1)
        {
            // Build 1102Y-V3 [MS-04a]: Register Master expected for E1 BEFORE submit.
            int masterDeltaE1 = (direction == MarketPosition.Long) ? entry1Qty : -entry1Qty;
            { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaE1); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }

            // Submit Entry 1 limit order
            entryOrder1 = direction == MarketPosition.Long
                ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name)
                : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name);

            // A1-1/A2-1: Null-abort rollback + stateLock wrap for E1 (Build 960 audit fix)
            if (entryOrder1 == null)
            {
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaE1); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }
                Print("[ENTRY_ABORT] TREND E1 SubmitOrderUnmanaged NULL for " + entry1Name + " -- rolled back.");
                return false;
            }
            { var _en966 = entry1Name; var _p966 = pos1; var _eo966 = entryOrder1;
            Enqueue(ctx => { ctx.activePositions[_en966] = _p966; ctx.entryOrders[_en966] = _eo966; }); }

            return true;
        }

        private bool ExecuteTREND_SubmitLeg2(MarketPosition direction, int entry2Qty, double entry2Price, string entry1Name, string entry2Name, PositionInfo pos2, Order entryOrder1)
        {
            // Only link the two legs after E1 is confirmed to have a live order handle.
            linkedTRENDEntries[entry1Name] = entry2Name;
            linkedTRENDEntries[entry2Name] = entry1Name;

            // Build 1102Y-V3 [MS-04b]: Register Master expected for E2 BEFORE submit.
            int masterDeltaE2 = (direction == MarketPosition.Long) ? entry2Qty : -entry2Qty;
            { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaE2); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }

            // Submit Entry 2 limit order
            Order entryOrder2 = direction == MarketPosition.Long
                ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name)
                : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name);

            // A1-1/A2-1: Null-abort rollback + stateLock wrap for E2 (Build 960 audit fix)
            if (entryOrder2 == null)
            {
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaE2); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }
                // Remove partnership references; HandleOrderCancelled will teardown E1 state naturally.
                string removedPartner;
                linkedTRENDEntries.TryRemove(entry1Name, out removedPartner);
                linkedTRENDEntries.TryRemove(entry2Name, out removedPartner);
                if (entryOrder1 != null && !IsOrderTerminal(entryOrder1.OrderState)) CancelOrderSafe(entryOrder1, null);
                Print("[ENTRY_ABORT] TREND E2 NULL -- E1 cancel issued for " + entry1Name + "; teardown deferred to cancel callback.");
                return false;
            }
            { var _en966 = entry2Name; var _p966 = pos2; var _eo966 = entryOrder2;
            Enqueue(ctx => { ctx.activePositions[_en966] = _p966; ctx.entryOrders[_en966] = _eo966; }); }

            return true;
        }

        private void ExecuteTREND_DispatchSima(MarketPosition direction, int totalContracts, double currentPrice, string entry1Name, string entry2Name)
        {
            // V12.1: Smart Dispatch to SIMA Fleet
            if (EnableSIMA)
            {
                // For Trend trades, followers get the full totalContracts qty split by the dispatcher
                ExecuteSmartDispatchEntry(
                    "TREND",
                    direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                    totalContracts,
                    currentPrice,
                    OrderType.Limit,  // 1102Z-A F1: followers use Limit to match leader pullback price
                    entry1Name,
                    entry2Name);
            }

            // Deactivate TREND mode after placing orders
            DeactivateTRENDMode();
        }

        private PositionInfo CreateTRENDPosition(string entryName, MarketPosition direction,
            double entryPrice, double stopPrice, int contracts, bool isEntry1, string groupId, bool isRma)
        {
            double target1Price;
            double target2Price;
            double target3Price;
            double target4Price;
            double target5Price;
            int t1Qty;
            int t2Qty;
            int t3Qty;
            int t4Qty;
            int t5Qty;
            CreateTRENDPosition_CalculateTargets(
                direction,
                entryPrice,
                contracts,
                out target1Price,
                out target2Price,
                out target3Price,
                out target4Price,
                out target5Price,
                out t1Qty,
                out t2Qty,
                out t3Qty,
                out t4Qty,
                out t5Qty);

            return CreateTRENDPosition_BuildInfo(
                entryName,
                direction,
                entryPrice,
                stopPrice,
                contracts,
                isEntry1,
                groupId,
                isRma,
                target1Price,
                target2Price,
                target3Price,
                target4Price,
                target5Price,
                t1Qty,
                t2Qty,
                t3Qty,
                t4Qty,
                t5Qty);
        }

        private void CreateTRENDPosition_CalculateTargets(
            MarketPosition direction,
            double entryPrice,
            int contracts,
            out double target1Price,
            out double target2Price,
            out double target3Price,
            out double target4Price,
            out double target5Price,
            out int t1Qty,
            out int t2Qty,
            out int t3Qty,
            out int t4Qty,
            out int t5Qty)
        {
            // Universal Ladder: T(n)Type dropdown drives all target pricing.
            target1Price = CalculateTargetPrice(direction, entryPrice, 1);
            target2Price = CalculateTargetPrice(direction, entryPrice, 2);
            target3Price = CalculateTargetPrice(direction, entryPrice, 3);
            target4Price = CalculateTargetPrice(direction, entryPrice, 4);
            target5Price = CalculateTargetPrice(direction, entryPrice, 5);

            GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

            Print(string.Format("TREND POSITION: {0} contracts -> T1:{1} T2:{2} T3:{3} T4:{4} T5:{5}",
                contracts, t1Qty, t2Qty, t3Qty, t4Qty, t5Qty));
        }

        private PositionInfo CreateTRENDPosition_BuildInfo(
            string entryName,
            MarketPosition direction,
            double entryPrice,
            double stopPrice,
            int contracts,
            bool isEntry1,
            string groupId,
            bool isRma,
            double target1Price,
            double target2Price,
            double target3Price,
            double target4Price,
            double target5Price,
            int t1Qty,
            int t2Qty,
            int t3Qty,
            int t4Qty,
            int t5Qty)
        {
            var tPos = new PositionInfo
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
                T4Filled = false,
                T5Filled = false,
                BracketSubmitted = false,
                ExtremePriceSinceEntry = entryPrice,
                CurrentTrailLevel = 0,
                EntryOrderType = OrderType.Limit,
                IsRMATrade = isRma,
                IsTRENDTrade = true,
                IsTRENDEntry1 = isEntry1,
                IsTRENDEntry2 = !isEntry1,
                LinkedTRENDGroup = groupId,
                OcoGroupId = "V12_" + GetStableHash(entryName)
            };
            return tPos;
        }


        private void DeactivateTRENDMode()
        {
            isTRENDModeActive = false;
        }

        #endregion

        #region TREND Manual Entry Methods (V12.27)

        /// <summary>
        /// V12.27: TREND manual entry at user-specified price with 100% risk allocation.
        /// Uses full MaxRiskAmount (no 1/3 + 2/3 split like standard TREND).
        /// Submits a single limit order at the manual price.
        /// </summary>
        private void ExecuteTRENDManualEntry(double manualPrice, MarketPosition direction, int contracts)
        {
            if (!ExecuteTRENDManual_Preflight(contracts))
            {
                return;
            }

            try
            {
                double entryPrice;
                double stopPrice;
                int t1Qty;
                int t2Qty;
                int t3Qty;
                int t4Qty;
                int t5Qty;
                string entryName;
                PositionInfo pos;
                ExecuteTRENDManual_BuildPosition(
                    manualPrice,
                    direction,
                    contracts,
                    out entryPrice,
                    out stopPrice,
                    out t1Qty,
                    out t2Qty,
                    out t3Qty,
                    out t4Qty,
                    out t5Qty,
                    out entryName,
                    out pos);

                if (!ExecuteTRENDManual_SubmitEntry(direction, contracts, entryPrice, entryName, pos))
                {
                    return;
                }

                Print(string.Format("V12.27 TREND_MANUAL: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | 100% Risk",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 TREND_MANUAL TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@{7:F2} | T5:{8}@{9:F2}",
                    t1Qty, pos.Target1Price, t2Qty, pos.Target2Price, t3Qty, pos.Target3Price, t4Qty, pos.Target4Price, t5Qty, pos.Target5Price));
                ExecuteTRENDManual_DispatchSima(direction, contracts, entryPrice, entryName);
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTRENDManualEntry: " + ex.Message);
            }
        }

        private bool ExecuteTRENDManual_Preflight(int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed())
            {
                return false;
            }
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning)
            {
                return false;
            }

            if (contracts <= 0)
            {
                Print(string.Format("[TREND] ExecuteTRENDManualEntry received invalid contracts={0}. Aborting entry.", contracts));
                return false;
            }

            if (currentATR <= 0)
            {
                Print("V12.27 TREND_MANUAL: Ignored - ATR not available");
                return false;
            }

            return true;
        }

        private void ExecuteTRENDManual_BuildPosition(
            double manualPrice,
            MarketPosition direction,
            int contracts,
            out double entryPrice,
            out double stopPrice,
            out int t1Qty,
            out int t2Qty,
            out int t3Qty,
            out int t4Qty,
            out int t5Qty,
            out string entryName,
            out PositionInfo pos)
        {
            entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

            // V12.27: 100% risk allocation - single position at manual price
            // Stop uses RMA multiplier (Trend RMA Mode forced)
            double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
            // V12.Phase6 [TICK-01]: All prices rounded to valid tick increments
            stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                ? entryPrice - stopDistance
                : entryPrice + stopDistance);

            // V12.27: 100% risk - full position size supplied by caller (no split)
            GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

            string signalName = direction == MarketPosition.Long ? "TrendMnlLong" : "TrendMnlShort";
            entryName = signalName + "_" + DateTime.UtcNow.ToString("HHmmssffff", CultureInfo.InvariantCulture);

            pos = CreateTRENDPosition(entryName, direction, entryPrice, stopPrice,
                contracts, true, "TMNL_" + DateTime.UtcNow.Ticks, true);

            // Build 1102Y-V3 [LG-01]: Enforce staircase rule.
            ApplyTargetLadderGuard(pos);
        }

        private bool ExecuteTRENDManual_SubmitEntry(MarketPosition direction, int contracts, double entryPrice, string entryName, PositionInfo pos)
        {
            // Build 1102Y-V3 [MS-05]: Register Master expected BEFORE submit.
            int masterDeltaTMNL = (direction == MarketPosition.Long) ? contracts : -contracts;
            { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaTMNL); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }

            // Submit LIMIT order at manual price
            Order entryOrder = direction == MarketPosition.Long
                ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

            // A1-1/A2-1: Null-abort rollback + stateLock wrap (Build 960 audit fix)
            if (entryOrder == null)
            {
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaTMNL); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }
                Print("[ENTRY_ABORT] TRENDManual SubmitOrderUnmanaged NULL for " + entryName + " -- rolled back.");
                return false;
            }
            { var _en966ap = entryName; var _p966ap = pos; Enqueue(ctx => { ctx.activePositions[_en966ap] = _p966ap; }); }
            { var _en966 = entryName; var _eo966 = entryOrder; Enqueue(ctx => { ctx.entryOrders[_en966] = _eo966; }); }

            return true;
        }

        private void ExecuteTRENDManual_DispatchSima(MarketPosition direction, int contracts, double entryPrice, string entryName)
        {
            if (EnableSIMA)
            {
                ExecuteSmartDispatchEntry(
                    "TREND_MNL",
                    direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                    contracts,
                    entryPrice,
                    OrderType.Limit,
                    entryName);
            }
        }

        #endregion
    }
}
