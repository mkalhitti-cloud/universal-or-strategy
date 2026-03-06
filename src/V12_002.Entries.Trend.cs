// V12.Phase7 MODULAR: TREND Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteTRENDEntry, CreateTRENDPosition, ActivateTRENDMode,
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
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (contracts <= 0)
            {
                Print($"[TREND] ExecuteTRENDEntry received invalid contracts={contracts}. Aborting entry.");
                return;
            }

            // V8.2 FIX: Only execute when on primary series (BarsInProgress=0)
            // This ensures we get correct EMA values from BarsArray[0]
            if (BarsInProgress != 0)
            {
                pendingTRENDEntry = true;
                Print($"TREND entry deferred to next primary bar update (BarsInProgress={BarsInProgress})");
                return;
            }

            // Clear pending flag since we're executing now
            pendingTRENDEntry = false;

            if (!TRENDEnabled)
            {
                Print("TREND mode is disabled");
                return;
            }

            if (currentATR <= 0 || ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND entry - indicators not ready");
                return;
            }

            // V11: Trend RMA (9/15 Split) Mode
            if (isTrendRmaMode)
            {
                Print($"V12.20: TREND Multiplier -> Mode=RMA (9/15 Split) ATR={currentATR:F2}");
                ExecuteTrendSplitEntry();
                return;
            }

            // V8.2: Ensure we have enough bars for EMA calculation
            if (CurrentBar < 20)
            {
                Print($"Cannot execute TREND entry - not enough bars (CurrentBar={CurrentBar})");
                return;
            }
            try
            {
                // V8.2: Simple check for enough bars
                if (CurrentBar < 20)
                {
                    Print($"Cannot execute TREND entry - not enough bars (CurrentBar={CurrentBar})");
                    return;
                }

                // Get current tick price for direction determination
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // V8.2: Use stored EMA instances (now guaranteed BarsInProgress=0)
                if (ema9 == null || ema15 == null)
                {
                    Print("Cannot execute TREND entry - EMA indicators not initialized");
                    return;
                }

                // V8.10: Use [0] (live tick) for real-time EMA values since Calculate.OnPriceChange updates EMAs on every tick
                double ema9Value = ema9[0];
                double ema15Value = ema15[0];

                // V8.10 DEBUG
                Print($"TREND DEBUG: ema9[0]={ema9Value:F2} ema15[0]={ema15Value:F2} Price={currentPrice:F2}");
                Print($"TREND DEBUG: Close[0]={Close[0]:F2} CurrentBar={CurrentBar} BarsInProgress={BarsInProgress}");

                // Sanity check: EMAs should be different
                if (Math.Abs(ema9Value - ema15Value) < tickSize * 2)
                {
                    Print($"WARNING: EMAs very close ({ema9Value:F2} vs {ema15Value:F2})");
                }

                // Direction: EMA below price = LONG (buying pullback), EMA above = SHORT
                MarketPosition direction;
                if (ema9Value < currentPrice)
                {
                    direction = MarketPosition.Long;
                    Print($"TREND: EMA9 below price ({ema9Value:F2} < {currentPrice:F2}) = LONG setup");
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print($"TREND: EMA9 above price ({ema9Value:F2} > {currentPrice:F2}) = SHORT setup");
                }

                // V8.31: Both E1 and E2 now use ATR-based stops from live EMAs
                double e1MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                double e2MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
                Print($"V12.20: TREND Multiplier -> Mode={(isTrendRmaMode ? "RMA" : "STD")} E1={e1MultTrend:F2}x E2={e2MultTrend:F2}x");

                double e1StopDist = CalculateATRStopDistance(e1MultTrend); // V12.30: Ceiling-rounded
                double e2StopDist = CalculateATRStopDistance(e2MultTrend); // V12.30: Ceiling-rounded

                // Weighted average stop distance for the group (used for logging only; sizing comes from caller)
                double weightedStopDist = (e1StopDist * (1.0/3.0)) + (e2StopDist * (2.0/3.0));

                int totalContracts = contracts;

                // TREND-SPLIT-FIX: Strict floor -- E1 (EMA9) gets ?Total/3?, E2 (EMA15) gets remainder.
                // Prevents risk budget overrun when Math.Ceiling pushes E1 past 1/3 of total contracts.
                int entry1Qty = Math.Max(1, totalContracts / 3);
                int entry2Qty = Math.Max(1, totalContracts - entry1Qty);

                // Final validation: totalContracts = sum of entries
                totalContracts = entry1Qty + entry2Qty;

                Print($"TREND RISK: Risk=${MaxRiskAmount} | E1Stop={e1StopDist:F2} | E2Stop={e2StopDist:F2} | WeightedDist={weightedStopDist:F2} | TotalQty={totalContracts}");
                Print($"TREND SPLIT: E1Qty={entry1Qty} (1/3) | E2Qty={entry2Qty} (2/3)");

                string trendGroupId = $"TREND_{DateTime.Now:HHmmssffff}";
                string entry1Name = $"{trendGroupId}_E1";
                string entry2Name = $"{trendGroupId}_E2";

                // V8.31: ENTRY 1: 1/3 at 9 EMA with ATR-based stop from live EMA9
                // V12.Phase6 [TICK-01]: Round EMA to valid tick increment before broker submission
                double entry1Price = Instrument.MasterInstrument.RoundToTickSize(ema9Value);
                double e1AtrStop = CalculateATRStopDistance(e1MultTrend);  // V12.30: Ceiling-rounded
                double stop1Price = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entry1Price - e1AtrStop  // V8.31: Stop is 1.1x ATR below live EMA9
                    : entry1Price + e1AtrStop); // V8.31: Stop is 1.1x ATR above live EMA9

                // ENTRY 2: 2/3 at 15 EMA with ATR trailing stop
                // V12.Phase6 [TICK-01]: Round EMA to valid tick increment before broker submission
                double entry2Price = Instrument.MasterInstrument.RoundToTickSize(ema15Value);
                double stop2Price = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entry2Price - CalculateATRStopDistance(e2MultTrend)
                    : entry2Price + CalculateATRStopDistance(e2MultTrend));

                // Create position info for Entry 1
                PositionInfo pos1 = CreateTRENDPosition(entry1Name, direction, entry1Price, stop1Price,
                    entry1Qty, true, trendGroupId, isTrendRmaMode);
                // Build 1102Y-V3 [LG-01]: Enforce staircase rule on E1.
                ApplyTargetLadderGuard(pos1);
                activePositions[entry1Name] = pos1;

                // Create position info for Entry 2
                PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, entry2Price, stop2Price,
                    entry2Qty, false, trendGroupId, isTrendRmaMode);
                // Build 1102Y-V3 [LG-01]: Enforce staircase rule on E2.
                ApplyTargetLadderGuard(pos2);
                activePositions[entry2Name] = pos2;

                // Link the entries together
                linkedTRENDEntries[entry1Name] = entry2Name;
                linkedTRENDEntries[entry2Name] = entry1Name;

                // Build 1102Y-V3 [MS-04a]: Register Master expected for E1 BEFORE submit.
                int masterDeltaE1 = (direction == MarketPosition.Long) ? entry1Qty : -entry1Qty;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaE1);

                // Submit Entry 1 limit order
                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name);

                if (entryOrder1 == null)
                {
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), -masterDeltaE1);
                    Print($"[ERROR][1102Y-V3] TREND E1 SubmitOrderUnmanaged NULL for {entry1Name} -- rolled back.");
                }
                entryOrders[entry1Name] = entryOrder1;

                // Build 1102Y-V3 [MS-04b]: Register Master expected for E2 BEFORE submit.
                int masterDeltaE2 = (direction == MarketPosition.Long) ? entry2Qty : -entry2Qty;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaE2);

                // Submit Entry 2 limit order
                Order entryOrder2 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name);

                if (entryOrder2 == null)
                {
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), -masterDeltaE2);
                    Print($"[ERROR][1102Y-V3] TREND E2 SubmitOrderUnmanaged NULL for {entry2Name} -- rolled back.");
                }
                entryOrders[entry2Name] = entryOrder2;

                Print($"TREND ORDERS PLACED: {(direction == MarketPosition.Long ? "LONG" : "SHORT")} Total={totalContracts} contracts");
                Print($"  E1: {entry1Qty}@{ema9Value:F2} (EMA9) | Stop: {stop1Price:F2} ({TRENDEntry1ATRMultiplier}xATR from EMA9)");
                Print($"  E2: {entry2Qty}@{ema15Value:F2} (EMA15) | Stop: {stop2Price:F2} ({TRENDEntry2ATRMultiplier}xATR trail)");

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
            catch (Exception ex)
            {
                Print($"ERROR ExecuteTRENDEntry: {ex.Message}");
            }
        }

        private PositionInfo CreateTRENDPosition(string entryName, MarketPosition direction,
            double entryPrice, double stopPrice, int contracts, bool isEntry1, string groupId, bool isRma)
        {
            // Universal Ladder: T(n)Type dropdown drives all target pricing.
            double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
            double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
            double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
            double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
            double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

            int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
            GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

            Print($"TREND POSITION: {contracts} contracts -> T1:{t1Qty} T2:{t2Qty} T3:{t3Qty} T4:{t4Qty} T5:{t5Qty}");

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
                // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                OcoGroupId = $"V12_{entryName.GetHashCode():X8}"
            };
            return tPos;
        }

        private void ActivateTRENDMode() => isTRENDModeActive = true;

        private void DeactivateTRENDMode() => isTRENDModeActive = false;

        #endregion

        #region TREND Manual Entry Methods (V12.27)

        /// <summary>
        /// V12.27: TREND manual entry at user-specified price with 100% risk allocation.
        /// Uses full MaxRiskAmount (no 1/3 + 2/3 split like standard TREND).
        /// Submits a single limit order at the manual price.
        /// </summary>
        private void ExecuteTRENDManualEntry(double manualPrice, MarketPosition direction, int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (contracts <= 0)
            {
                Print($"[TREND] ExecuteTRENDManualEntry received invalid contracts={contracts}. Aborting entry.");
                return;
            }

            if (currentATR <= 0)
            {
                Print("V12.27 TREND_MANUAL: Ignored - ATR not available");
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: 100% risk allocation - single position at manual price
                // Stop uses RMA multiplier (Trend RMA Mode forced)
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                // V12.Phase6 [TICK-01]: All prices rounded to valid tick increments
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance);

                // V12.27: 100% risk - full position size supplied by caller (no split)
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "TrendMnlLong" : "TrendMnlShort";
                string entryName = $"{signalName}_{DateTime.Now:HHmmssffff}";

                PositionInfo pos = CreateTRENDPosition(entryName, direction, entryPrice, stopPrice,
                    contracts, true, "TMNL_" + DateTime.Now.Ticks, true);

                // Build 1102Y-V3 [LG-01]: Enforce staircase rule.
                ApplyTargetLadderGuard(pos);
                activePositions[entryName] = pos;

                // Build 1102Y-V3 [MS-05]: Register Master expected BEFORE submit.
                int masterDeltaTMNL = (direction == MarketPosition.Long) ? contracts : -contracts;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaTMNL);

                // Submit LIMIT order at manual price
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                if (entryOrder == null)
                {
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), -masterDeltaTMNL);
                    Print($"[ERROR][1102Y-V3] TRENDManual SubmitOrderUnmanaged NULL for {entryName} -- rolled back.");
                }
                entryOrders[entryName] = entryOrder;

                Print($"V12.27 TREND_MANUAL: {direction} {contracts}@{entryPrice:F2} LIMIT | Stop: {stopPrice:F2} | 100% Risk");
                Print($"V12.27 TREND_MANUAL TARGETS: T1:{t1Qty}@{pos.Target1Price:F2} | T2:{t2Qty}@{pos.Target2Price:F2} | T3:{t3Qty}@{pos.Target3Price:F2} | T4:{t4Qty}@{pos.Target4Price:F2} | T5:{t5Qty}@{pos.Target5Price:F2}");

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
            catch (Exception ex)
            {
                Print($"ERROR ExecuteTRENDManualEntry: {ex.Message}");
            }
        }

        #endregion
    }
}
