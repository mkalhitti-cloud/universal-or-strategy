// V12.Phase7 MODULAR: MOMO Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteMOMOEntry, ActivateMOMOMode, DeactivateMOMOMode
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
        #region MOMO Entry Logic (V8.6)

        /// <summary>
        /// V8.6: Execute MOMO (Momentum) trade using Stop Market orders
        /// OPPOSITE direction from RMA:
        /// - Click ABOVE price = Stop Market LONG (buy when price rises to click level)
        /// - Click BELOW price = Stop Market SHORT (sell when price drops to click level)
        /// Uses same targets/trails as RMA but with fixed 0.5pt stop
        /// </summary>
        private void ExecuteMOMOEntry(double clickPrice, int contracts)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (!MOMOEnabled)
            {
                Print("MOMO mode is disabled");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute MOMO entry - ATR not available yet");
                return;
            }

            if (contracts <= 0)
            {
                Print(string.Format("[MOMO] ExecuteMOMOEntry received invalid contracts={0}. Aborting entry.", contracts));
                return;
            }

            try
            {
                // Use last known price from OnBarUpdate (Close[0] may be stale in UI events)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // MOMO Direction: OPPOSITE from RMA!
                // Click ABOVE current price = LONG (stop buy triggers when price rises)
                // Click BELOW current price = SHORT (stop sell triggers when price drops)
                MarketPosition direction;
                if (clickPrice > currentPrice)
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("MOMO: Click above price ({0:F2} > {1:F2}) = LONG stop entry", clickPrice, currentPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("MOMO: Click below price ({0:F2} < {1:F2}) = SHORT stop entry", clickPrice, currentPrice));
                }

                // MOMO uses FIXED 0.5pt stop (not ATR-based)
                double stopDistance = Math.Min(MOMOStopPoints, MaximumStop); // V8.31: Use MaximumStop

                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);
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

                string signalName = direction == MarketPosition.Long ? "MOMOLong" : "MOMOShort";
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
                    EntryOrderType = OrderType.StopMarket,
                    IsRMATrade = false,
                    IsMOMOTrade = true,  // V8.6: Mark as MOMO trade
                    OcoGroupId = "V12_" + GetStableHash(entryName)
                };
                ApplyTargetLadderGuard(pos);

                // Build 1102Y-V3 [MS-06]: Register Master expected BEFORE StopMarket entry.
                int masterDeltaMOMO = (direction == MarketPosition.Long) ? contracts : -contracts;
                { var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaMOMO); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }

                // V12.Hardening: Use StopMarket (was StopLimit with limitPrice==stopPrice -- never fills on fast breakouts)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName);

                // A1-1/A2-1: Null-abort rollback + stateLock wrap (Build 960 audit fix)
                if (entryOrder == null)
                {
                    { var _aek966 = ExpKey(Account.Name); var _aed966 = (-masterDeltaMOMO); Enqueue(ctx => ctx.AddExpectedPositionDelta(_aek966, _aed966)); }
                    Print("[ENTRY_ABORT] MOMO SubmitOrderUnmanaged returned null for " + entryName + ". Rolling back.");
                    return;
                }
                { var _en966ap = entryName; var _p966ap = pos; Enqueue(ctx => { ctx.activePositions[_en966ap] = _p966ap; }); }
                { var _en966 = entryName; var _eo966 = entryOrder; Enqueue(ctx => { ctx.entryOrders[_en966] = _eo966; }); }

                Print(string.Format("MOMO ENTRY ORDER: {0} {1}@{2:F2} STOP MKT | Stop: {3:F2}pt", signalName, contracts, entryPrice, stopDistance));
                Print(string.Format("MOMO TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@{8:F2} | T5:{9}@{10:F2} (Runner targets trail-only)",
                    t1Qty, target1Price, target1Price - entryPrice,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("MOMO", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.StopMarket, entryName);
                }

                // Deactivate MOMO mode after entry (one-shot)
                DeactivateMOMOMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteMOMOEntry: " + ex.Message);
            }
        }

        private void ActivateMOMOMode()
        {
            // Deactivate RMA if active (mutually exclusive)
            if (isRMAModeActive)
            {
                DeactivateRMAMode();
            }
            isMOMOModeActive = true;
        }

        private void DeactivateMOMOMode()
        {
            isMOMOModeActive = false;
        }

        #endregion
    }
}
