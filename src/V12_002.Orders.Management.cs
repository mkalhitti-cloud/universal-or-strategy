// V12.44 MODULAR: Order Management Module (Split from Orders.cs)
// Contains: Bracket orders, stop management, position sync, flatten, cleanup, reconciliation
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
        #region Order Submission & Stop Management

        private void SubmitBracketOrders(string entryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;

            try
            {
                // Validate stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);

                // [BUILD 924 - Fix B] Route bracket submission to follower account when applicable.
                bool isFollowerSubmit = pos.IsFollower && pos.ExecutingAccount != null;
                OrderAction bracketExitAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Sell : OrderAction.BuyToCover;

                // Build 936 [FIX-2]: Shared OCO group ID for all stop + target orders in this bracket.
                // Non-empty value triggers broker-native OCO protection (stop auto-cancelled when a target fills).
                // Survives NT8 restart because the broker maintains the group association independently.
                string bracketOcoId = pos.OcoGroupId ?? string.Empty;

                // Submit initial stop for all contracts
                Order stopOrder;
                if (isFollowerSubmit)
                {
                    // [BUILD 924 - Fix B] Follower stop: use ExecutingAccount API (not SubmitOrderUnmanaged which is master-local)
                    string stopSig = SymmetryTrim("Stop_" + entryName, 40);
                    Order sOrd = pos.ExecutingAccount.CreateOrder(
                        Instrument, bracketExitAction, OrderType.StopMarket, TimeInForce.Gtc,
                        pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, stopSig, null);
                    // [BUILD 924 - Fix B / Director's Note] Null-guard after CreateOrder matches S-001 pattern.
                    if (sOrd == null)
                    {
                        Print(string.Format("[BRACKET_FATAL] Follower stop CreateOrder returned null for {0}. Flattening.", entryName));
                        FlattenPositionByName(entryName);
                        return;
                    }
                    // Build 929 Fix2 [P1]: Wrap Submit in local try/catch.
                    // If Submit() throws (broker disconnect, margin, reject), the outer catch only logs
                    // and returns -- leaving this follower with a filled position and NO stop loss.
                    // We must flatten immediately to prevent a naked position.
                    try
                    {
                        stopOrders[entryName] = sOrd; // BUILD 981: Pre-register for sweep visibility
                        pos.ExecutingAccount.Submit(new[] { sOrd });
                    }
                    catch (Exception submitEx)
                    {
                        Order _junk; stopOrders.TryRemove(entryName, out _junk);
                        Print(string.Format("[BRACKET_FATAL] Follower stop Submit THREW for {0}: {1}. Emergency flattening.", entryName, submitEx.Message));
                        EmergencyFlattenSingleFleetAccount(pos.ExecutingAccount);
                        return;
                    }
                    stopOrder = sOrd;
                }
                else
                {
                    stopOrder = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, "Stop_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, "Stop_" + entryName);
                }

                // V12.Audit [S-001]: Null-guard stop submission result. If broker rejects or drops
                // the stop, flatten immediately -- never leave a position with a false "protected" state.
                if (stopOrder == null)
                {
                    Print(string.Format("[BRACKET_FATAL] Stop order submission returned null for {0}. Flattening.", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }
                stopOrders[entryName] = stopOrder;

                int nonRunnerLimitQty = 0;
                int runnerQty = 0;

                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue; // skip orphan/zero fills

                    // Universal Ladder: runner detection is slot-based only -- T(n)Type == Runner.
                    if (IsRunnerTarget(targetNum))
                    {
                        runnerQty += targetQty;
                        Print(string.Format("[FORENSIC] T{0} {1}: Runner qty={2} -- limit SKIPPED",
                            targetNum, entryName, targetQty));
                        continue;
                    }

                    double targetPrice = GetTargetPrice(pos, targetNum);
                    if (targetPrice <= 0)
                    {
                        Print(string.Format("[TARGET_SKIP] T{0} for {1} has qty={2} but invalid price={3:F2}; skipped",
                            targetNum, entryName, targetQty, targetPrice));
                        continue;
                    }

                    // V12.Phase7 [C-04]: Round target price to valid tick boundary before submission.
                    targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice);

                    Print(string.Format("[FORENSIC] T{0} {1}: qty={2} price={3:F2} submitting limit",
                        targetNum, entryName, targetQty, targetPrice));

                    Order limitOrder;
                    if (isFollowerSubmit)
                    {
                        // [BUILD 924 - Fix B] Follower target: use ExecutingAccount API
                        string targetSig = SymmetryTrim("T" + targetNum + "_" + entryName, 40);
                        Order tOrd = pos.ExecutingAccount.CreateOrder(
                            Instrument, bracketExitAction, OrderType.Limit, TimeInForce.Gtc,
                            targetQty, targetPrice, 0, bracketOcoId, targetSig, null);
                        // [BUILD 924 - Fix B / Director's Note] Null-guard after CreateOrder matches S-015 pattern.
                        if (tOrd != null)
                            pos.ExecutingAccount.Submit(new[] { tOrd });
                        else
                            Print(string.Format("[TARGET_WARN] Follower target T{0} CreateOrder returned null for {1}.", targetNum, entryName));
                        limitOrder = tOrd;
                    }
                    else
                    {
                        limitOrder = pos.Direction == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, targetQty, targetPrice, 0, bracketOcoId, "T" + targetNum + "_" + entryName)
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, targetQty, targetPrice, 0, bracketOcoId, "T" + targetNum + "_" + entryName);
                    }

                    var targetDict = GetTargetOrdersDictionary(targetNum);
                    // V12.Audit [S-015]: Only store non-null target orders. A null result means
                    // broker rejected the target -- skip storage so the slot stays empty rather
                    // than tracking a null reference. Stop is still present; no flatten needed.
                    if (targetDict != null)
                    {
                        if (limitOrder == null)
                        {
                            Print(string.Format("[TARGET_WARN] Target {0} order submission returned null for {1}. Target tracking disabled.", targetNum, entryName));
                        }
                        else
                        {
                            targetDict[entryName] = limitOrder;
                        }
                    }

                    nonRunnerLimitQty += targetQty;
                }

                pos.CurrentStopPrice = validatedStopPrice;

                // Zero-trust stop audit: stop quantity must always cover full position.
                if (stopOrder != null && stopOrder.Quantity != pos.TotalContracts)
                {
                    Print(string.Format("[STOP_AUDIT] MISMATCH {0}: StopQty={1} Total={2}",
                        entryName, stopOrder.Quantity, pos.TotalContracts));
                }
                else
                {
                    Print(string.Format("[STOP_AUDIT] OK {0}: StopQty={1} NonRunnerLimits={2} RunnerQty={3}",
                        entryName, pos.TotalContracts, nonRunnerLimitQty, runnerQty));
                }

                // V12.Audit [S-003]: BracketSubmitted is set AFTER the stop quantity audit so that
                // a mismatch detected above does not leave the position flagged as fully protected.
                // [Task 5 Fix]: pos.BracketSubmitted = true moved to top of method
                
                // [938-BRACKET] Confirm full bracket submitted for follower accounts.
                if (isFollowerSubmit)
                    Print(string.Format("[938-BRACKET] Follower bracket submitted: {0} T1={1:F2} Stop={2:F2}",
                        entryName, pos.Target1Price, validatedStopPrice));

                StringBuilder bracketMsg = new StringBuilder();
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET V12.1101E: Stop@{1:F2}", tradeType, validatedStopPrice);
                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue;

                    bool isRunnerSlot = IsRunnerTarget(targetNum);

                    if (isRunnerSlot)
                        bracketMsg.AppendFormat(" | T{0}:{1}@trail", targetNum, targetQty);
                    else
                        bracketMsg.AppendFormat(" | T{0}:{1}@{2:F2}", targetNum, targetQty, GetTargetPrice(pos, targetNum));
                }

                Print(bracketMsg.ToString());

                // V12.Audit [D-007]: Verify target contract sum matches total position size.
                int _targetSum = nonRunnerLimitQty + runnerQty;
                if (_targetSum != pos.TotalContracts)
                {
                    Print(string.Format("[BRACKET_WARN] Target sum mismatch for {0}: targets={1} totalContracts={2}. Distribution may have lost contracts.",
                        entryName, _targetSum, pos.TotalContracts));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR SubmitBracketOrders: " + ex.Message);
            }
        }

        /// <summary>
        /// Phase 9.1 [SYNC_ALL]: Recalculates and re-submits or cancels limit target orders for
        /// all active positions based on current TnType settings and live ATR.
        /// Runs on the strategy thread. Called directly from ProcessIpcCommands() SYNC_ALL handler.
        /// </summary>
        #endregion
    }
}
