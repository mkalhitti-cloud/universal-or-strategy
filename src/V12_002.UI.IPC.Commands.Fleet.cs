// Build 1001: UI.IPC.Commands.Fleet -- TryHandleFleetCommand [Build 1001 Repair V2]
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

        private bool TryHandleFleetCommand(string action, string[] parts, long senderTicks)
        {
            string cmdId = senderTicks > 0
                ? action + "|" + senderTicks.ToString()
                : action + "|" + (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute).ToString();

            if (TryHandleFleet_Trim(action, parts)) return true;
            if (TryHandleFleet_Lock50(action)) return true;
            if (TryHandleFleet_FlattenOnly(action)) return true;
            if (TryHandleFleet_Flatten(action, cmdId)) return true;
            if (TryHandleFleet_CancelAll(action, cmdId)) return true;
            if (TryHandleFleet_ResetMemory(action)) return true;
            if (TryHandleFleet_LongShort(action, cmdId)) return true;
            if (TryHandleFleet_OrLong(action, cmdId)) return true;
            if (TryHandleFleet_OrShort(action, cmdId)) return true;
            if (TryHandleFleet_TrendManualLimit(action, parts, cmdId)) return true;
            if (TryHandleFleet_RetestManualLimit(action, parts, cmdId)) return true;
            if (TryHandleFleet_FfmaManualLimit(action, parts, cmdId)) return true;
            if (TryHandleFleet_FfmaManualMarket(action, cmdId)) return true;
            if (TryHandleFleet_CloseTarget(action)) return true;
            if (TryHandleFleet_MoveTarget(action, parts)) return true;
            if (TryHandleFleet_FleetState(action, parts)) return true;
            if (TryHandleFleet_ToggleAccount(action, parts)) return true;
            if (TryHandleFleet_SetShadow(action, parts)) return true;
            return false;
        }

        private bool TryHandleFleet_Trim(string action, string[] parts)
        {
            if (action == "TRIM_25" || action == "TRIM_50")
            {
                HandleTrimCommand(action, parts);
                return true;
            }

            return false;
        }

        private bool TryHandleFleet_Lock50(string action)
        {
            if (action != "LOCK_50")
                return false;

            // [1102Z-F]: IPC LOCK_50 -- Lock 50% of unrealized profit on all active positions.
            // Delegates to ExecuteRunnerAction which already handles all account routing.
            Print("[IPC LOCK_50] Received -- routing to ExecuteRunnerAction(lock50)");
            Enqueue(ctx => ctx.ExecuteRunnerAction("lock50"));
            return true;
        }

        private bool TryHandleFleet_FlattenOnly(string action)
        {
            if (action != "FLATTEN_ONLY")
                return false;

            // V12.21: Flatten Only (Close Positions) - preserve pending orders
            if (EnableSIMA)
            {
                Print("[SIMA] IPC FLATTEN_ONLY -> Closing all open positions (Pending orders preserved)");
                ClosePositionsOnlyApexAccounts(); // V12.21: Use new non-cancelling helper
            }
            else
            {
                Print("[V12] FLATTEN_ONLY -> Closing all open positions (Pending orders preserved)");
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

        private bool TryHandleFleet_Flatten(string action, string cmdId)
        {
            if (action != "FLATTEN")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

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

        private bool TryHandleFleet_CancelAll(string action, string cmdId)
        {
            if (action != "CANCEL_ALL")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            // V12.13c: Only cancels pending entry orders (stops/targets on active positions are preserved)
            if (EnableSIMA)
            {
                int cancelled = 0;

                // Build 1001: Use broker truth (Account.Positions) for master -- expectedPositions[master]
                // is not updated on entry fill, making it stale as a liveness gate. Broker truth is authoritative.
                bool masterHasPosition = Account.Positions
                    .Any(p => p.Instrument != null && p.Instrument.FullName == Instrument.FullName
                              && p.MarketPosition != MarketPosition.Flat);

                Account masterBroker996c = Account;
                foreach (Order order in masterBroker996c.Orders.ToArray())
                {
                    if (order == null || order.Instrument?.FullName != Instrument?.FullName) continue;
                    if (order.OrderState == OrderState.Cancelled       ||
                        order.OrderState == OrderState.CancelPending   ||
                        order.OrderState == OrderState.CancelSubmitted ||
                        order.OrderState == OrderState.Filled          ||
                        order.OrderState == OrderState.Rejected) continue;
                    if (masterHasPosition) continue; // Master has live position: preserve all.
                    CancelOrderOnAccount(order, masterBroker996c);
                    cancelled++;
                }

                // Fleet accounts
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct))
                    {
                        if (acct == this.Account) continue; // already processed above
                        var acctFsms = _followerBrackets.Values.Where(f => f.AccountName == acct.Name).ToList();
                        bool acctHasActiveFsm = acctFsms.Any(f => f.State == FollowerBracketState.Active);
                        foreach (Order order in acct.Orders)
                        {
                            if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working ||
                                 order.OrderState == OrderState.Accepted ||
                                 order.OrderState == OrderState.Submitted ||
                                 order.OrderState == OrderState.ChangePending ||
                                 order.OrderState == OrderState.ChangeSubmitted))
                            {
                                string oName = order.Name;
                                if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                    oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                    oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_"))
                                {
                                    // Build 1104.1: Preserve brackets ONLY if FSM is active AND Master has position.
                                    // If Master is FLAT, orphaned follower brackets MUST be swept regardless of FSM state.
                                    if (acctHasActiveFsm && masterHasPosition) continue;
                                }

                                CancelOrderOnAccount(order, acct);
                                cancelled++;
                            }
                        }
                    }
                }
                Print($"[SIMA] CANCEL_ALL -> Cancelled {cancelled} orders (Entries + Orphaned Brackets) (local + fleet) [1001]");
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
                        string oName = order.Name;
                        if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                            oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                            oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_"))
                            continue;

                        CancelOrderOnAccount(order, order.Account);
                        cancelled++;
                    }
                }
                Print($"[V12] CANCEL_ALL -> Cancelled {cancelled} pending entry orders");
            }

            // V1102Z-HARDEN: Ghost Memory Teardown removed (V2 Forensic Fix)
            // We no longer zero expectedPositions immediately upon command launch.
            // State mutation is now reactive to broker confirmation via OnAccountOrderUpdate.

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

        private bool TryHandleFleet_ResetMemory(string action)
        {
            if (action != "RESET_MEMORY")
                return false;

            int resetAcctCount = 0;
            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct) || acct == this.Account)
                {
                    SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                    resetAcctCount++;
                }
            }
            Print($"[V1102Z] RESET_MEMORY: Zeroed all fleet/master expectedPositions for {Instrument.FullName} across {resetAcctCount} accounts.");
            SendResponseToRemote("MSG|Memory Reset Complete");
            return true;
        }

        private bool TryHandleFleet_LongShort(string action, string cmdId)
        {
            if (action != "LONG" && action != "SHORT")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

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
                    if (action == "LONG") isLongArmed = false; else isShortArmed = false;
                }
            }

            if (EnableSIMA)
            {
                OrderAction orderAction = action == "LONG" ? OrderAction.Buy : OrderAction.SellShort;
                int qty;
                try
                {
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
                qty = Math.Max(1, qty);

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
                MarketPosition direction = action == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (currentPrice <= 0)
                {
                    Print("[IPC] ABORT RMA dispatch: currentPrice=0. Skipping command.");
                    return true;
                }
                double stopDist  = CalculateATRStopDistance(RMAStopATRMultiplier);
                int contracts    = CalculatePositionSize(stopDist);
                Enqueue(ctx => ctx.ExecuteRMAEntryV2(currentPrice, direction, contracts));
            }

            return true;
        }

        private bool TryHandleFleet_OrLong(string action, string cmdId)
        {
            if (action != "OR_LONG")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            if (isTosSyncMode)
            {
                if (isLongArmed)
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts   = CalculatePositionSize(orStopDist);
                    Enqueue(ctx => ctx.ExecuteLong(orContracts));
                    isLongArmed = false;
                }
            }
            else
            {
                double orStopDist = CalculateORStopDistance();
                int orContracts   = CalculatePositionSize(orStopDist);
                Enqueue(ctx => ctx.ExecuteLong(orContracts));
            }

            return true;
        }

        private bool TryHandleFleet_OrShort(string action, string cmdId)
        {
            if (action != "OR_SHORT")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            if (isTosSyncMode)
            {
                if (isShortArmed)
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts   = CalculatePositionSize(orStopDist);
                    Enqueue(ctx => ctx.ExecuteShort(orContracts));
                    isShortArmed = false;
                }
            }
            else
            {
                double orStopDist = CalculateORStopDistance();
                int orContracts   = CalculatePositionSize(orStopDist);
                Enqueue(ctx => ctx.ExecuteShort(orContracts));
            }

            return true;
        }

        private bool TryHandleFleet_TrendManualLimit(string action, string[] parts, string cmdId)
        {
            if (action != "TREND_MANUAL_LIMIT")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            if (parts.Length > 3)
            {
                string dir = parts[2].Trim().ToUpperInvariant();
                MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                {
                    double trendDist   = CalculateTRENDStopDistance();
                    int trendContracts = CalculatePositionSize(trendDist);
                    Enqueue(ctx => ctx.ExecuteTRENDManualEntry(price, mp, trendContracts));
                }
            }

            return true;
        }

        private bool TryHandleFleet_RetestManualLimit(string action, string[] parts, string cmdId)
        {
            if (action != "RETEST_MANUAL_LIMIT")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            if (parts.Length > 3)
            {
                string dir = parts[2].Trim().ToUpperInvariant();
                MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                {
                    double retestDist   = CalculateRetestStopDistance();
                    int retestContracts = CalculatePositionSize(retestDist);
                    Enqueue(ctx => ctx.ExecuteRetestManualEntry(price, mp, retestContracts));
                }
            }

            return true;
        }

        private bool TryHandleFleet_FfmaManualLimit(string action, string[] parts, string cmdId)
        {
            if (action != "FFMA_MANUAL_LIMIT")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            if (parts.Length > 3)
            {
                string dir = parts[2].Trim().ToUpperInvariant();
                MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 0)
                {
                    double ffmaStopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                    if (ffmaStopDist <= 0) ffmaStopDist = MinimumStop;
                    int contracts = CalculatePositionSize(ffmaStopDist);
                    Enqueue(ctx => ctx.ExecuteFFMALimitEntry(price, mp, contracts));
                }
            }

            return true;
        }

        private bool TryHandleFleet_FfmaManualMarket(string action, string cmdId)
        {
            if (action != "FFMA_MANUAL_MARKET")
                return false;

            if (!MetadataGuardDuplicate(cmdId, action)) return true;

            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
            double ema9Value = ema9[0];
            MarketPosition direction = currentPrice < ema9Value ? MarketPosition.Long : MarketPosition.Short;
            double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
            double ffmaStopDist = Math.Min(Math.Abs(currentPrice - stopPrice), MaximumStop);
            if (ffmaStopDist < tickSize * 2) ffmaStopDist = tickSize * 2;
            int contracts = CalculatePositionSize(ffmaStopDist);
            Enqueue(ctx => ctx.ExecuteFFMAManualMarketEntry(contracts));
            return true;
        }

        private bool TryHandleFleet_CloseTarget(string action)
        {
            if (!action.StartsWith("CLOSE_T"))
                return false;

            int targetNum = 0;
            if (action.Length > 7 && int.TryParse(action.Substring(7, 1), out targetNum))
            {
                FlattenSpecificTarget(targetNum);
            }

            return true;
        }

        private bool TryHandleFleet_MoveTarget(string action, string[] parts)
        {
            if (!action.StartsWith("MOVE_TARGET") && action != "SET_TARGET_PRICE")
                return false;

            if (parts.Length >= 3)
            {
                string targetId = parts[1].Trim().ToUpperInvariant();
                string priceStr = parts[2].Trim();
                int targetNum = 0;
                if (targetId.Length >= 2 && targetId.StartsWith("T")
                    && int.TryParse(targetId.Substring(1), out targetNum)
                    && targetNum >= 1 && targetNum <= 5)
                {
                    if (action == "SET_TARGET_PRICE")
                    {
                        // Build 1107: Absolute price move (from live control center)
                        double absPrice;
                        if (double.TryParse(priceStr, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out absPrice) && absPrice > 0)
                        {
                            absPrice = Instrument.MasterInstrument.RoundToTickSize(absPrice);
                            MoveSpecificTargetAbsolute(targetNum, absPrice);
                        }
                    }
                    else
                    {
                        // Relative offset move (from context menu)
                        string distance = priceStr.ToLowerInvariant();
                        double profitPoints = 0;
                        if (distance == "1pt") profitPoints = 1.0;
                        else if (distance == "2pt") profitPoints = 2.0;
                        else return true;
                        MoveSpecificTarget(targetNum, profitPoints);
                    }
                }
            }

            return true;
        }

        private bool TryHandleFleet_FleetState(string action, string[] parts)
        {
            if (action.StartsWith("GET_FLEET") || action == "SET_SIMA" || action == "SET_LEADER_ACCOUNT" || action == "REQUEST_FLEET_STATE")
            {
                HandleFleetCommand(action, parts);
                return true;
            }

            return false;
        }

        private bool TryHandleFleet_ToggleAccount(string action, string[] parts)
        {
            if (!action.StartsWith("TOGGLE_ACCOUNT"))
                return false;

            HandleToggleAccountCommand(parts);
            return true;
        }

        private bool TryHandleFleet_SetShadow(string action, string[] parts)
        {
            if (action != "SET_SHADOW")
                return false;

            if (parts.Length >= 2)
            {
                bool enable = parts[1].Trim() == "true" || parts[1].Trim() == "1";
                ShadowModeEnabled = enable;
                Print(string.Format("[IPC] Shadow Mode {0}", enable ? "ENABLED" : "DISABLED"));
            }

            return true;
        }

        #endregion
    }
}
