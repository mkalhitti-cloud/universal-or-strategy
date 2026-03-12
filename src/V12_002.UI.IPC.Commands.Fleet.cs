// Build 971: UI.IPC.Commands.Fleet -- TryHandleFleetCommand [Build 971] Single method >400 lines -- future refactor candidate
// V12 UI.IPC Module (Extracted)
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
        #region IPC Commands Fleet

        private bool TryHandleFleetCommand(string action, string[] parts)
        {
            if (action == "TRIM_25" || action == "TRIM_50")
            {
                HandleTrimCommand(action, parts);
                return true;
            }
            if (action == "LOCK_50")
            {
                // [1102Z-F]: IPC LOCK_50 -- Lock 50% of unrealized profit on all active positions.
                // Delegates to ExecuteRunnerAction which already handles all account routing.
                Print("[IPC LOCK_50] Received -- routing to ExecuteRunnerAction(lock50)");
                ExecuteRunnerAction("lock50");
                return true;
            }
            if (action == "FLATTEN_ONLY")
            {
                // V12.21: Flatten Only (Close Positions) - preserve pending orders
                if (EnableSIMA)
                {
                    Print("[SIMA] IPC FLATTEN_ONLY -> Closing all open positions (Pending orders preserved)");
                    ClosePositionsOnlyApexAccounts(); // V12.21: Use new non-cancelling helper
                }
                else
                {
                    Print("[V12] FLATTEN_ONLY -> Closing all open positions (Pending orders preserved)");
                    // CloseAllPositions(); // Native NT8 method closes positions and cancels orders usually?
                    // NT8 Flatten() cancels orders. We must use Close() on each position instead.

                    foreach (Position pos in Account.Positions)
                    {
                        if (pos.Instrument.FullName == Instrument.FullName && pos.MarketPosition != MarketPosition.Flat)
                        {
                            if (pos.MarketPosition == MarketPosition.Long)
                                SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.Quantity, 0, 0, "", "FlattenOnly_ExitLong");
                            else
                                SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.Quantity, 0, 0, "", "FlattenOnly_ExitShort");
                        }
                    }
                }
                return true;
            }
            if (action == "FLATTEN")
            {
                // V12 SIMA: Use multi-account flatten when enabled
                if (EnableSIMA)
                {
                    Print("[SIMA] IPC FLATTEN -> Broadcasting to all Apex accounts");
                    FlattenAllApexAccounts();
                }
                else
                {
                    FlattenAll();
                }
                return true;
            }
            if (action == "CANCEL_ALL")
            {
                // V12.13c: Only cancels pending entry orders (stops/targets on active positions are preserved)
                if (EnableSIMA)
                {
                    int cancelled = 0;

                    // V12.10: Cancel local account orders first.
                    foreach (Order order in Account.Orders)
                    {
                        if (order != null && order.Instrument.FullName == Instrument.FullName &&
                            (order.OrderState == OrderState.Working ||
                             order.OrderState == OrderState.Accepted ||
                             order.OrderState == OrderState.Submitted ||
                             order.OrderState == OrderState.ChangePending ||
                             order.OrderState == OrderState.ChangeSubmitted))
                        {
                            // V12.13c: Skip stops and targets on active positions -- only cancel pending entries
                            string oName = order.Name;
                            if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_") ||
                                oName.Contains("_RMA_") || oName.StartsWith("RMA_") ||
                                oName.Contains("_CIT_") || oName.StartsWith("CIT_"))
                                continue;

                            CancelOrder(order);
                            cancelled++;
                        }
                    }

                    // Fleet accounts.
                    foreach (Account acct in Account.All)
                    {
                        if (IsFleetAccount(acct))
                        {
                            if (acct == this.Account) continue; // already cancelled above
                            foreach (Order order in acct.Orders)
                            {
                                if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                    (order.OrderState == OrderState.Working ||
                                     order.OrderState == OrderState.Accepted ||
                                     order.OrderState == OrderState.Submitted ||
                                     order.OrderState == OrderState.ChangePending ||
                                     order.OrderState == OrderState.ChangeSubmitted))
                                {
                                    // V12.13c: Skip stops and targets -- only cancel pending entries
                                    string oName = order.Name;
                                    if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                        oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                        oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_") ||
                                        oName.Contains("_RMA_") || oName.StartsWith("RMA_") ||
                                        oName.Contains("_CIT_") || oName.StartsWith("CIT_"))
                                        continue;

                                    acct.Cancel(new[] { order });
                                    cancelled++;
                                }
                            }
                        }
                    }
                    Print($"[SIMA] CANCEL_ALL -> Cancelled {cancelled} pending entry orders (local + fleet)");
                }
                else
                {
                    int cancelled = 0;
                    foreach (Order order in Account.Orders)
                    {
                        if (order != null && order.Instrument.FullName == Instrument.FullName &&
                            (order.OrderState == OrderState.Working ||
                             order.OrderState == OrderState.Accepted ||
                             order.OrderState == OrderState.Submitted ||
                             order.OrderState == OrderState.ChangePending ||
                             order.OrderState == OrderState.ChangeSubmitted))
                        {
                            // V12.13c: Skip stops and targets -- only cancel pending entries
                            string oName = order.Name;
                            if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_") ||
                                oName.Contains("_RMA_") || oName.StartsWith("RMA_") ||
                                oName.Contains("_CIT_") || oName.StartsWith("CIT_"))
                                continue;

                            CancelOrder(order);
                            cancelled++;
                        }
                    }
                    Print($"[V12] CANCEL_ALL -> Cancelled {cancelled} pending entry orders");
                }

                // V1102Z-HARDEN: Ghost Memory Teardown
                // We must sweep ALL matching accounts and zero their expectedPositions for THIS instrument.
                // Relying on activePositions.Values iteration is insufficient as failed dispatches leave entries in
                // expectedPositions with no corresponding activePositions object.
                int resetAcctCount = 0;
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct) || acct == this.Account)
                    {
                        SetExpectedPosition(ExpKey(acct.Name), 0);
                        resetAcctCount++;
                    }
                }
                Print($"[V1102Z] Ghost Memory Purge: Zeroed expectedPositions for {resetAcctCount} accounts on {Instrument.FullName}");

                // Clean up local position objects for anything not filled
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!kvp.Value.EntryFilled)
                    {
                        CleanupPosition(kvp.Key);
                        Print(string.Format("V12.13b: CANCEL_ALL cleaned unfilled memory entry: {0}", kvp.Key));
                    }
                }
                return true;
            }
            if (action == "RESET_MEMORY")
            {
                // V1102Z: Manual emergency reset of all expectedPositions for this instrument
                int resetAcctCount = 0;
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct) || acct == this.Account)
                    {
                        SetExpectedPosition(ExpKey(acct.Name), 0);
                        resetAcctCount++;
                    }
                }
                Print($"[V1102Z] RESET_MEMORY: Zeroed all fleet/master expectedPositions for {Instrument.FullName} across {resetAcctCount} accounts.");
                SendResponseToRemote("MSG|Memory Reset Complete");
                return true;
            }
            if (action == "LONG" || action == "SHORT")
            {
                // V12.2: Handle Sync Mode
                if (isTosSyncMode)
                {
                    bool armed = (action == "LONG") ? isLongArmed : isShortArmed;
                    if (!armed)
                    {
                        Print($"[SYNC] ToS Signal IGNORED: {action} received but {action} is not ARMED locally.");
                        return true;
                    }
                    else
                    {
                        Print($"[SYNC] ToS Handshake Received -> Executing {action} Fleet Entry");
                        // Reset armed flag after firing
                        if (action == "LONG") isLongArmed = false; else isShortArmed = false;
                    }
                }

                // V12 SIMA: Broadcast to all Apex accounts when enabled
                if (EnableSIMA)
                {
                    OrderAction orderAction = action == "LONG" ? OrderAction.Buy : OrderAction.SellShort;

                    // [Phase 8.2 Part 3 - IPC SIZING]: Calculate ATR-sized quantity to match
                    // what ExecuteRMAEntryV2 would use, instead of defaulting to minContracts (= 1).
                    // This ensures manual LONG/SHORT button entries enter at the correct fleet size.
                    int qty;
                    try
                    {
                        // [Phase 8.2 Part 4 - IPC SIZING FIX]: Use RMAStopATRMultiplier to match
                        // the actual RMA engine risk model. StopMultiplier caused incorrect stop
                        // distances on high-value instruments (ES/NQ), flooring qty to 1.
                        double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                        if (stopDist <= 0)
                        {
                            stopDist = MinimumStop;
                            Print($"[IPC SIZING] ATR latency detected. Falling back to MinimumStop={MinimumStop:F4}");
                        }
                        qty = stopDist > 0 ? CalculatePositionSize(stopDist) : Math.Max(1, minContracts);
                        Print($"[IPC SIZING] Calculation: StopDist={stopDist:F4}, Risk={MaxRiskAmount}, TargetQty={qty}");
                    }
                    catch
                    {
                        qty = Math.Max(1, minContracts);
                    }
                    qty = Math.Max(1, qty); // safety floor

                    if (EnablePathB)
                    {
                        Print($"[SIMA] PATH B {action} -> Broadcasting {qty} contracts with FIXED BRACKETS to all Apex accounts");
                        ExecuteMultiAccountBracket(orderAction, qty, "PATHB_" + action, PathBStopPoints, PathBTargetPoints);
                    }
                    else
                    {
                        Print($"[SIMA] IPC {action} -> Broadcasting {qty} contracts to all Apex accounts");
                        ExecuteMultiAccountMarket(orderAction, qty, "SIMA_" + action);
                    }
                }
                else
                {
                    // Original single-account logic
                    MarketPosition direction = action == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    // [923B-FIX-C]: Guard against zero price -- Close[0] returns 0 if the strategy
                    // has just loaded and bars have not yet been initialized (pre-session or fresh attach).
                    // Passing currentPrice=0 to ExecuteRMAEntryV2 would submit a Limit @ 0, which
                    // Apex/Tradovate treats as a Market order -> instant fill without price touching level.
                    if (currentPrice <= 0)
                    {
                        Print("[IPC] ABORT RMA dispatch: currentPrice=0 -- lastKnownPrice and Close[0] both invalid. Skipping command, continuing queue drain.");
                        return true; // Build 929 Fix1 [P2]: skip bad-price command, keep draining queue
                    }
                    double stopDist  = CalculateATRStopDistance(RMAStopATRMultiplier);
                    int contracts    = CalculatePositionSize(stopDist);
                    ExecuteRMAEntryV2(currentPrice, direction, contracts);
                }
                return true;
            }
            // V10.3: OR Breakout Entry Commands
            if (action == "OR_LONG")
            {
                // V12.2: Handle Sync Mode
                if (isTosSyncMode)
                {
                    if (isLongArmed)
                    {
                        Print("[SYNC] ToS Handshake Received -> Executing OR_LONG");
                        double orStopDist = CalculateORStopDistance();
                        int orContracts   = CalculatePositionSize(orStopDist);
                        ExecuteLong(orContracts);
                        isLongArmed = false;
                    }
                    else
                    {
                        Print("[SYNC] ToS Signal IGNORED: OR_LONG received but Long is not ARMED locally.");
                    }
                }
                else
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts   = CalculatePositionSize(orStopDist);
                    ExecuteLong(orContracts);
                    Print("V10.3: OR_LONG executed via IPC");
                }
                return true;
            }
            if (action == "OR_SHORT")
            {
                // V12.2: Handle Sync Mode
                if (isTosSyncMode)
                {
                    if (isShortArmed)
                    {
                        Print("[SYNC] ToS Handshake Received -> Executing OR_SHORT");
                        double orStopDist = CalculateORStopDistance();
                        int orContracts   = CalculatePositionSize(orStopDist);
                        ExecuteShort(orContracts);
                        isShortArmed = false;
                    }
                    else
                    {
                        Print("[SYNC] ToS Signal IGNORED: OR_SHORT received but Short is not ARMED locally.");
                    }
                }
                else
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts   = CalculatePositionSize(orStopDist);
                    ExecuteShort(orContracts);
                    Print("V10.3: OR_SHORT executed via IPC");
                }
                return true;
            }
            // V12.27: Manual entry commands from Contextual UI Submit button
            if (action == "TREND_MANUAL_LIMIT")
            {
                // Format: TREND_MANUAL_LIMIT|<symbol>|<direction>|<price>  (symbol in parts[1] for router)
                if (parts.Length > 3)
                {
                    string dir = parts[2].Trim().ToUpperInvariant();
                    MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                    if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                    {
                        Print(string.Format("V12.27 IPC: TREND_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                        double trendDist   = CalculateTRENDStopDistance();
                        int trendContracts = CalculatePositionSize(trendDist);
                        ExecuteTRENDManualEntry(price, mp, trendContracts);
                    }
                    else
                    {
                        Print(string.Format("V12.27 IPC: TREND_MANUAL_LIMIT invalid price: {0}", string.Join("|", parts)));
                    }
                }
                return true;
            }
            if (action == "RETEST_MANUAL_LIMIT")
            {
                // Format: RETEST_MANUAL_LIMIT|<symbol>|<direction>|<price>  (symbol in parts[1] for router)
                if (parts.Length > 3)
                {
                    string dir = parts[2].Trim().ToUpperInvariant();
                    MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                    if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                    {
                        Print(string.Format("V12.27 IPC: RETEST_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                        double retestDist   = CalculateRetestStopDistance();
                        int retestContracts = CalculatePositionSize(retestDist);
                        ExecuteRetestManualEntry(price, mp, retestContracts);
                    }
                    else
                    {
                        Print(string.Format("V12.27 IPC: RETEST_MANUAL_LIMIT invalid price: {0}", string.Join("|", parts)));
                    }
                }
                return true;
            }
            if (action == "FFMA_MANUAL_LIMIT")
            {
                // Format: FFMA_MANUAL_LIMIT|<symbol>|<direction>|<price>  (symbol in parts[1] for router)
                if (parts.Length > 3)
                {
                    string dir = parts[2].Trim().ToUpperInvariant();
                    MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                    if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                    {
                        Print(string.Format("V12.27 IPC: FFMA_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                        double ffmaStopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                        if (ffmaStopDist <= 0) ffmaStopDist = MinimumStop;
                        int contracts = CalculatePositionSize(ffmaStopDist);
                        ExecuteFFMALimitEntry(price, mp, contracts);
                    }
                    else
                    {
                        Print(string.Format("V12.27 IPC: FFMA_MANUAL_LIMIT invalid price: {0}", string.Join("|", parts)));
                    }
                }
                return true;
            }
            if (action == "FFMA_MANUAL_MARKET")
            {
                // V12.27: M.FFMA button -- instant market, direction toward 9 EMA
                Print("V12.27 IPC: FFMA_MANUAL_MARKET -- auto-direction toward EMA9");
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                double ema9Value = ema9[0];
                MarketPosition direction = currentPrice < ema9Value ? MarketPosition.Long : MarketPosition.Short;
                double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                double ffmaStopDist = Math.Min(Math.Abs(currentPrice - stopPrice), MaximumStop);
                if (ffmaStopDist < tickSize * 2) ffmaStopDist = tickSize * 2;
                int contracts = CalculatePositionSize(ffmaStopDist);
                ExecuteFFMAManualMarketEntry(contracts);
                return true;
            }
            // V10.3: Target-Specific Close Commands
            if (action.StartsWith("CLOSE_T"))
            {
                int targetNum = 0;
                if (action.Length > 7 && int.TryParse(action.Substring(7, 1), out targetNum))
                {
                    FlattenSpecificTarget(targetNum);
                }
                return true;
            }
            // V14: MOVE_TARGET command - Surgical target price adjustment
            if (action.StartsWith("MOVE_TARGET"))
            {
                // Format: MOVE_TARGET|T1|1pt  or  MOVE_TARGET|T2|2pt
                if (parts.Length >= 3)
                {
                    string targetId = parts[1].Trim().ToUpperInvariant(); // "T1", "T2", etc.
                    string distance = parts[2].Trim().ToLowerInvariant(); // "1pt" or "2pt"

                    // Parse distance
                    double profitPoints = 0;
                    if (distance == "1pt") profitPoints = 1.0;
                    else if (distance == "2pt") profitPoints = 2.0;
                    else
                    {
                        Print($"[V14] MOVE_TARGET: Invalid distance '{distance}' - expected '1pt' or '2pt'");
                        return true;
                    }

                    // Extract target number (T1 -> 1, T2 -> 2, etc.)
                    int targetNum = 0;
                    if (targetId.Length >= 2 && targetId.StartsWith("T"))
                    {
                        if (!int.TryParse(targetId.Substring(1), out targetNum) || targetNum < 1 || targetNum > 5)
                        {
                            Print($"[V14] MOVE_TARGET: Invalid target '{targetId}' - expected T1-T5");
                            return true;
                        }
                    }
                    else
                    {
                        Print($"[V14] MOVE_TARGET: Invalid target format '{targetId}'");
                        return true;
                    }

                    Print($"[V14] MOVE_TARGET: Command received for {targetId} to +{profitPoints}pt profit");

                    // Call the move handler (implemented in Orders.cs)
                    MoveSpecificTarget(targetNum, profitPoints);
                }
                else
                {
                    Print("[V14] MOVE_TARGET: Invalid format - expected MOVE_TARGET|TX|1pt or MOVE_TARGET|TX|2pt");
                }
                return true;
            }
            if (action.StartsWith("GET_FLEET"))
            {
                HandleFleetCommand(action, parts);
                return true;
            }
            if (action.StartsWith("TOGGLE_ACCOUNT"))
            {
                HandleToggleAccountCommand(parts);
                return true;
            }
            // V12.6: SET_SIMA|ON or SET_SIMA|OFF - Remote SIMA toggle from external panel
            // V12.Phase6 [LIFECYCLE]: Uses centralized ApplySimaState for full lifecycle management
            if (action == "SET_SIMA")
            {
                HandleFleetCommand(action, parts);
                return true;
            }
            // V12.25: SET_LEADER_ACCOUNT|accountName -- Panel tells strategy which account is the leader
            if (action == "SET_LEADER_ACCOUNT")
            {
                HandleFleetCommand(action, parts);
                return true;
            }
            if (action == "REQUEST_FLEET_STATE")
            {
                HandleFleetCommand(action, parts);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handles configuration sync commands: CONFIG (full target/risk sync), GET_LAYOUT (fallback logger).
        /// </summary>

        #endregion
    }
}
