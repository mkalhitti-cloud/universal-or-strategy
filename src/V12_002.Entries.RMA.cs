// V12.Phase7 MODULAR: RMA Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteTrendSplitEntry, DeactivateRMAMode
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
        // SignalBroadcaster is retained for ClearAllSubscribers teardown (Lifecycle.cs).

        #region Trend Split Entry

        // V11: Trend RMA (9/15 Split) Logic
        private void ExecuteTrendSplitEntry(int contracts)
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

                // totalQty extracted directly from passed in parameter (contracts) rather than dynamic calculation
                int totalQty = contracts;
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

                List<string> masterEntryNames = new List<string> { entry1Name };

                int masterDeltaE1 = (direction == MarketPosition.Long) ? qty9 : -qty9;
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaE1); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }

                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty9, e9, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty9, e9, 0, "", entry1Name);

                // A1-1/A2-1: Null-abort + stateLock wrap for E1 (Build 960 audit fix)
                if (entryOrder1 == null)
                {
                    { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaE1); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }
                    Print("[ENTRY_ABORT] TrendSplit E1 SubmitOrderUnmanaged returned null for " + entry1Name + ". Rolling back.");
                    return;
                }
                { var _en966 = entry1Name; var _p966 = pos1; var _eo966 = entryOrder1;
                Enqueue(ctx => { ctx.activePositions[_en966] = _p966; ctx.entryOrders[_en966] = _eo966; }); }

                if (qty15 > 0)
                {
                    double stop2Price = Instrument.MasterInstrument.RoundToTickSize(
                        direction == MarketPosition.Long ? e15 - stop15Dist : e15 + stop15Dist);
                    PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, e15, stop2Price, qty15, false, trendGroupId, true);

                    linkedTRENDEntries[entry1Name] = entry2Name;
                    linkedTRENDEntries[entry2Name] = entry1Name;

                    int masterDeltaE2 = (direction == MarketPosition.Long) ? qty15 : -qty15;
                    { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaE2); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }

                    Order entryOrder2 = direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty15, e15, 0, "", entry2Name)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty15, e15, 0, "", entry2Name);

                    // A1-1/A2-1: Null-abort + stateLock wrap for E2 (Build 960 audit fix)
                    if (entryOrder2 == null)
                    {
                        { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaE2); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }
                        // Remove partnership references; HandleOrderCancelled will teardown E1 state naturally.
                        string removedPartner;
                        linkedTRENDEntries.TryRemove(entry1Name, out removedPartner);
                        linkedTRENDEntries.TryRemove(entry2Name, out removedPartner);
                        if (entryOrder1 != null && !IsOrderTerminal(entryOrder1.OrderState)) CancelOrder(entryOrder1);
                        Print("[ENTRY_ABORT] TrendSplit E2 NULL -- E1 cancel issued for " + entry1Name + "; teardown deferred to cancel callback.");
                        return;
                    }
                    { var _en966 = entry2Name; var _p966 = pos2; var _eo966 = entryOrder2;
                    Enqueue(ctx => { ctx.activePositions[_en966] = _p966; ctx.entryOrders[_en966] = _eo966; }); }
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

        #endregion

        #region RMA Entry Logic


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
