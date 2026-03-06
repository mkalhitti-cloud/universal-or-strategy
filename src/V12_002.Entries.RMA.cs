// V12.Phase7 MODULAR: RMA Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteTrendSplitEntry, GetRmaAnchorPrice, ExecuteRMAEntry,
//           ExecuteRMAEntryCustom, ActivateRMAMode, DeactivateRMAMode
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
        // V12 SIMA: BroadcastEntrySignal and V8 Copy Trading region removed.
        // Trade copying is replaced by direct Account.All iteration in ExecuteSmartDispatchEntry.

        // V11: Trend RMA (9/15 Split) Logic
        private void ExecuteTrendSplitEntry()
        {
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print("Cannot execute TREND RMA - ATR not ready");
                return;
            }

            if (ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND RMA - EMA indicators not ready");
                return;
            }

            try
            {
                // Logic: EMA 9 vs EMA 15 alignment determines trend direction.
                double e9 = Instrument.MasterInstrument.RoundToTickSize(ema9[0]);
                double e15 = Instrument.MasterInstrument.RoundToTickSize(ema15[0]);
                bool isLongTrend = e9 > e15;
                MarketPosition direction = isLongTrend ? MarketPosition.Long : MarketPosition.Short;
                OrderAction entryAction = isLongTrend ? OrderAction.Buy : OrderAction.SellShort;

                // TREND_RMA is risk-sized from MaxRiskAmount (default $200), then split across EMA9/EMA15.
                // V12.1101E [B-1]: Decouple per-leg multipliers -- mirror the standard TREND entry logic.
                // E1 (EMA9 leg) uses TRENDEntry1ATRMultiplier; E2 (EMA15 leg) uses TRENDEntry2ATRMultiplier.
                // When isTrendRmaMode is ON, both legs fall back to RMAStopATRMultiplier (same as standard TREND).
                double e1Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                double e2Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
                double stop9Dist  = CalculateATRStopDistance(e1Mult);  // EMA9 leg stop distance
                double stop15Dist = CalculateATRStopDistance(e2Mult);  // EMA15 leg stop distance
                double weightedStopDist = (stop9Dist * (1.0 / 3.0)) + (stop15Dist * (2.0 / 3.0));

                int totalQty = CalculatePositionSize(weightedStopDist);
                // TREND-SPLIT-FIX: Strict floor -- EMA9 gets ?Total/3?, EMA15 gets remainder.
                // Matches the (1/3, 2/3) weights in weightedStopDist; prevents risk budget overrun.
                int qty9  = Math.Max(1, totalQty / 3);
                int qty15 = Math.Max(0, totalQty - qty9);
                if (totalQty >= 2 && qty15 < 1) { qty15 = 1; qty9 = Math.Max(1, totalQty - qty15); }

                int finalTotalQty = qty9 + qty15;
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string trendGroupId = "TRMA_" + timestamp;
                string entry1Name = trendGroupId + "_E1";
                string entry2Name = trendGroupId + "_E2";

                double stop1Price = Instrument.MasterInstrument.RoundToTickSize(
                    direction == MarketPosition.Long ? e9 - stop9Dist : e9 + stop9Dist);
                PositionInfo pos1 = CreateTRENDPosition(entry1Name, direction, e9, stop1Price, qty9, true, trendGroupId, true);
                activePositions[entry1Name] = pos1;

                List<string> masterEntryNames = new List<string> { entry1Name };

                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty9, e9, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty9, e9, 0, "", entry1Name);
                entryOrders[entry1Name] = entryOrder1;

                if (qty15 > 0)
                {
                    double stop2Price = Instrument.MasterInstrument.RoundToTickSize(
                        direction == MarketPosition.Long ? e15 - stop15Dist : e15 + stop15Dist);
                    PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, e15, stop2Price, qty15, false, trendGroupId, true);
                    activePositions[entry2Name] = pos2;

                    linkedTRENDEntries[entry1Name] = entry2Name;
                    linkedTRENDEntries[entry2Name] = entry1Name;

                    Order entryOrder2 = direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty15, e15, 0, "", entry2Name)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty15, e15, 0, "", entry2Name);
                    entryOrders[entry2Name] = entryOrder2;
                    masterEntryNames.Add(entry2Name);
                }

                double weightedEntryPrice = ((e9 * qty9) + (e15 * qty15)) / Math.Max(1, finalTotalQty);
                weightedEntryPrice = Instrument.MasterInstrument.RoundToTickSize(weightedEntryPrice);

                Print(string.Format("TREND RMA SPLIT: {0} | Qty={1} (EMA9={2}, EMA15={3}) | EMA9={4:F2} EMA15={5:F2} | Anchor={6:F2}",
                    direction == MarketPosition.Long ? "LONG" : "SHORT",
                    finalTotalQty,
                    qty9,
                    qty15,
                    e9,
                    e15,
                    weightedEntryPrice));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "TREND_RMA",
                        entryAction,
                        finalTotalQty,
                        weightedEntryPrice,
                        OrderType.Limit,
                        masterEntryNames.ToArray());
                }

                DeactivateTRENDMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTrendSplitEntry: " + ex.Message);
            }
        }

        #region RMA Entry Logic

        // V11: Helper to get price of currently selected RMA Anchor
        private double GetRmaAnchorPrice()
        {
            switch (currentRmaAnchor)
            {
                case RmaAnchorType.Ema30: return ema30[0];
                case RmaAnchorType.Ema65: return ema65[0];
                case RmaAnchorType.Ema200: return ema200[0];
                case RmaAnchorType.OrHigh: return sessionHigh;
                case RmaAnchorType.OrLow: return sessionLow;
                case RmaAnchorType.Manual:
                    // Use thread-safe cache
                    return cachedMnlPrice;
            }
            return ema65[0]; // Default
        }

        private void ExecuteRMAEntry(double clickPrice, MarketPosition? forcedDirection = null)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print(string.Format("[RMA REJECT] ATR not ready. Check if 5-min bars (BarsArray[1]) are loaded and strategy has been running for {0} bars.", RMAATRPeriod));
                return;
            }

            try
            {
                // V12.Phase9.2: RMA Intelligence Exhaustion Guard
                if (!IsRmaSetupExhausted(clickPrice, forcedDirection ?? MarketPosition.Long))
                {
                    Print("[RMA REJECT] Setup is not exhausted or is too fresh. Entry blocked.");
                    return;
                }

                // V12.Phase9.2: MTF Confluence Scoring
                double confluenceScore = GetRmaConfluenceScore(clickPrice);
                if (RmaUseMtfConfluence && confluenceScore < 0.2)
                {
                    Print(string.Format("[RMA WARNING] Low Confluence Score ({0:F2}). Proceeding with caution.", confluenceScore));
                }

                // V11 FIX: Robust Check for Stale Price
                double currentPrice = Close[0];
                if (lastKnownPrice > 0)
                {
                     double diff = Math.Abs(lastKnownPrice - currentPrice);
                     if (currentPrice > 0 && diff / currentPrice < 0.05) currentPrice = lastKnownPrice;
                }

                // V12.1101E [D-01]: Removed unused legacy anchor shadow values (behavior unchanged).

                MarketPosition direction;

                // V11 SAFEGUARD: Always enforce Limit Order Logic relative to Market
                // If Click > Market -> Short (Sell Limit Above)
                // If Click < Market -> Long (Buy Limit Below)
                // This prevents "Accidental Market Fills" if Anchor logic or stale data gets confused
                if (clickPrice > currentPrice) direction = MarketPosition.Short;
                else direction = MarketPosition.Long;

                // Only use forcedDirection if it MATCHES the Safe Logic (or if prices are super close)
                if (forcedDirection.HasValue && forcedDirection.Value != direction)
                {
                    Print(string.Format("RMA SAFEGUARD: Ignoring forced {0} because Click {1} vs Market {2} implies {3}",
                        forcedDirection.Value, clickPrice, currentPrice, direction));
                }

                Print(string.Format("RMA Entry: Click={0:F2}, Market={1:F2}, Direction={2}",
                    clickPrice, currentPrice, direction));

                // Calculate RMA stop and targets using ATR
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded, MaximumStop cap

                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance);

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "RMALong" : "RMAShort";
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
                    IsRMATrade = true,
                    // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                    OcoGroupId = "V12_" + GetStableHash(entryName)
                };
                ApplyTargetLadderGuard(pos);

                // Build 1102Y-V3 [MS-01]: Register Master's expected position in the Order Ledger
                // BEFORE SubmitOrderUnmanaged to close the Reaper's 1-5 second zero-window.
                int masterDeltaRMA = (direction == MarketPosition.Long) ? contracts : -contracts;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaRMA);

                // Submit LIMIT order at clicked price (RMA uses limit entries)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                if (entryOrder != null)
                {
                    entryOrders[entryName] = entryOrder;
                    activePositions[entryName] = pos; // Only add to panel if order submitted

                    // DEBUG: Visual Confirmation
                    Draw.Text(this, "Debug_" + entryName, "ORDER SUBMITTED", 0, entryPrice, Brushes.Yellow);
                    Draw.Line(this, "Line_" + entryName, 0, entryPrice, 10, entryPrice, Brushes.Yellow);
                }
                else
                {
                    // Build 1102Y-V3 [MS-01 ROLLBACK]: Submit failed -- undo reservation to prevent ghost position.
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), -masterDeltaRMA);
                    Print("[ERROR][1102Y-V3] SubmitOrderUnmanaged returned NULL for " + entryName + " -- Master expected rolled back.");
                    Draw.Text(this, "Debug_Fail_" + entryName, "ORDER FAILED", 0, entryPrice, Brushes.Red);
                }

                Print(string.Format("RMA ENTRY ORDER: {0} {1}@{2:F2} | ATR: {3:F2}", signalName, contracts, entryPrice, currentATR));
                Print(string.Format("RMA TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@{8:F2} | T5:{9}@{10:F2} (Runner targets trail-only)",
                    t1Qty, target1Price, target1Price - entryPrice,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("RMA", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit);
                }

                DeactivateRMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRMAEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V10.1: Custom RMA entry for IPC commands - forces direction and uses specified price
        /// </summary>
        private void ExecuteRMAEntryCustom(double price, MarketPosition direction)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print("IPC RMACustom Ignored: ATR not available");
                return;
            }

            try
            {
                // V12.Phase9.2: RMA Intelligence Exhaustion Guard (IPC Path)
                if (!IsRmaSetupExhausted(price, direction))
                {
                    Print("[IPC RMACustom REJECT] Setup not exhausted. Entry blocked.");
                    return;
                }

                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded, MaximumStop cap

                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(price);
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

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "IPCLong" : "IPCShort";
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
                    IsRMATrade = true,
                    // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                    OcoGroupId = "V12_" + GetStableHash(entryName)
                };
                ApplyTargetLadderGuard(pos);

                activePositions[entryName] = pos;

                // Build 1102Y-V3 [MS-02]: Register Master's expected position in the Order Ledger BEFORE submit.
                int masterDeltaRMACustom = (direction == MarketPosition.Long) ? contracts : -contracts;
                AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaRMACustom);

                // Execute as MARKET order for IPC commands to ensure immediate fill (V9 style)
                Order entryOrderCustom = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);

                if (entryOrderCustom == null)
                {
                    // Build 1102Y-V3 [MS-02 ROLLBACK]: Submit failed -- undo reservation.
                    AddExpectedPositionDeltaLocked(ExpKey(Account.Name), -masterDeltaRMACustom);
                    Print("[ERROR][1102Y-V3] RMACustom SubmitOrderUnmanaged returned NULL for " + entryName + " -- Master expected rolled back.");
                }

                Print(string.Format("IPC EXEC: {0} {1} contracts at MKT (Ref: {2:F2})", direction, contracts, entryPrice));

                // V12.1: Smart Dispatch to SIMA Fleet
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("RMA_IPC", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit);
                }
            }
            catch (Exception ex)
            {
                Print("Error ExecuteRMAEntryCustom: " + ex.Message);
            }
        }

        private void ActivateRMAMode()
        {
            isRMAModeActive = true;
        }

        private void DeactivateRMAMode()
        {
            isRMAModeActive = false;
            isRMAButtonClicked = false;

            // V12.14: Broadcast RMA deactivation to panel
            string deactivateConfig = string.Format(
                "CONFIG|OR|COUNT:{0};T1:{1};T1TYPE:{2};T2:{3};T2TYPE:{4};T3:{5};T3TYPE:{6};T4:{7};T4TYPE:{8};T5:{9};T5TYPE:{10};STR:{11};MAX:{12};",
                minContracts,
                Target1Value, ToIpcTargetMode(T1Type),
                Target2Value, ToIpcTargetMode(T2Type),
                Target3Value, ToIpcTargetMode(T3Type),
                Target4Value, ToIpcTargetMode(T4Type),
                Target5Value, ToIpcTargetMode(T5Type),
                StopMultiplier, MaxRiskAmount);
            SendResponseToRemote(deactivateConfig);
            Print("V12.14: DeactivateRMAMode - CONFIG broadcast sent");
        }

        #endregion
        #region RMA Intelligence (Phase 9.2)

        /// <summary>
        /// Expert logic to verify if a level is "Exhausted" and safe to trade as a reversal.
        /// </summary>
        private bool IsRmaSetupExhausted(double level, MarketPosition direction)
        {
            if (!RmaIntelligenceEnabled) return true; // Bypass if disabled

            // 1. Exhaustion Pulse (2.0 ATR move over 5 bars)
            if (BarsArray[1].Count < 6) return false;
            double moveDist = Math.Abs(Close[0] - Highs[1][5]); // Comparison against 5 blocks ago on 5-min
            if (direction == MarketPosition.Long) moveDist = Math.Abs(Close[0] - Lows[1][5]);
            
            double exhaustionThreshold = currentATR * RmaExhaustionAtrMultiplier;
            if (moveDist < exhaustionThreshold)
            {
                Print(string.Format("[REJECT] No Exhaustion: Move={0:F2} vs Threshold={1:F2}", moveDist, exhaustionThreshold));
                return false;
            }

            // 2. Stretched Candle Sense (Height > 1.0 ATR)
            double candleHeight = High[0] - Low[0];
            if (candleHeight < (currentATR * RmaStretchedCandleMultiplier))
            {
                Print(string.Format("[REJECT] Not Stretched: Height={0:F2} vs Threshold={1:F2}", candleHeight, currentATR * RmaStretchedCandleMultiplier));
                return false;
            }

            // 3. Fresh Candle Shield (Opened too close to level)
            double openDist = Math.Abs(Open[0] - level);
            if (openDist < (currentATR * RmaFreshCandleBufferAtr))
            {
                Print(string.Format("[REJECT] Fresh Candle: Open={0:F2} is within {1:F2} of level", Open[0], currentATR * RmaFreshCandleBufferAtr));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a confluence score (0.0 to 1.0) based on higher timeframe levels and EMA/Fib alignment.
        /// </summary>
        private double GetRmaConfluenceScore(double level)
        {
            if (!RmaUseMtfConfluence) return 1.0;

            double score = 0;
            double tickThreshold = 2 * tickSize;

            // EMA Alignment (30, 65, 200)
            if (Math.Abs(ema30[0] - level) <= tickThreshold) score += 0.2;
            if (Math.Abs(ema65[0] - level) <= tickThreshold) score += 0.2;
            if (Math.Abs(ema200[0] - level) <= tickThreshold) score += 0.2;

            // Fibonacci Confluence (0.5, 0.618 of Session Range)
            double fib05 = sessionLow + (sessionRange * 0.5);
            double fib618 = sessionLow + (sessionRange * 0.618);
            if (Math.Abs(fib05 - level) <= tickThreshold) score += 0.2;
            if (Math.Abs(fib618 - level) <= tickThreshold) score += 0.2;

            return score;
        }

        private void MonitorRmaProximity()
        {
            if (!RmaIntelligenceEnabled) return;

            foreach (var kvp in entryOrders)
            {
                Order order = kvp.Value;
                if (order == null || order.OrderState != OrderState.Working) continue;

                PositionInfo pos;
                if (!activePositions.TryGetValue(kvp.Key, out pos) || !pos.IsRMATrade) continue;

                double currentPrice = Close[0];
                double level = pos.EntryPrice;
                double distTicks = Math.Abs(currentPrice - level) / tickSize;

                // Check for Proximity Miss
                // If we were in proximity (< RmaProximityTicks) and now we've retreated (> RmaCancellationTicks)
                if (distTicks <= RmaProximityTicks)
                {
                    // Track that we were in proximity
                    Draw.Dot(this, "Prox_" + kvp.Key, false, 0, level, Brushes.Cyan);
                }
                else if (distTicks >= RmaCancellationTicks)
                {
                    // If we see a Cyan dot (meaning we were close) and now we are far, we cancel
                    if (GetDrawObject("Prox_" + kvp.Key) != null)
                    {
                        Print(string.Format("[SENTINEL] Proximity Miss detected for {0}. Cancelling and rotating.", kvp.Key));
                        CancelOrder(order);
                        RemoveDrawObject("Prox_" + kvp.Key);
                        
                        // Speak it
                        SendResponseToRemote("SOUND|SENTINEL_PROXIMITY_CANCEL");
                    }
                }
            }
        }

        #endregion
    }
}
