// Build 971: UI.IPC.Commands.Misc -- TryHandleConfigCommand, TryHandleComplianceCommand, HandleFleetCommand, SendResponseToRemote, FlattenSpecificTarget, ToggleStrategyMode
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
        #region IPC Commands Misc

        private bool TryHandleConfigCommand(string action, string[] parts)
        {
            if (action == "CONFIG")
            {
                HandleConfigCommand(parts);
                return true;
            }
            // V12: GET_LAYOUT handler (primary response is in ListenForRemote, this is fallback logging)
            if (action == "GET_LAYOUT")
            {
                string mode = isRMAModeActive ? "RMA" : "OR";
                Print(string.Format("V12 GET_LAYOUT: Mode={0} Count={1} T1={2}({3}) T2={4}({5}) T3={6}({7}) T4={8}({9}) T5={10}({11})",
                    mode, activeTargetCount,
                    Target1Value, T1Type,
                    Target2Value, T2Type,
                    Target3Value, T3Type,
                    Target4Value, T4Type,
                    Target5Value, T5Type));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stub for future compliance commands (GET_COMPLIANCE, etc.).
        /// Build 943: Established per router architecture.
        /// </summary>
        private bool TryHandleComplianceCommand(string action, string[] parts)
        {
            return false;
        }

        /// <summary>
        /// Handles fleet-level commands: GET_FLEET, SET_SIMA, DIAG_FLEET,
        /// SET_LEADER_ACCOUNT, REQUEST_FLEET_STATE.
        /// </summary>
        private void HandleFleetCommand(string action, string[] parts)
        {
            if (action.StartsWith("GET_FLEET", StringComparison.OrdinalIgnoreCase))
            {
                var fleetAccounts = GetFleetAccountsSnapshot();
                var aliasMap = BuildFleetAliasMap(fleetAccounts);
                StringBuilder sb = new StringBuilder("CONFIG|FLEET");
                sb.Append("|COUNT:").Append(fleetAccounts.Count);
                foreach (var acct in fleetAccounts)
                    sb.Append('|').Append(GetIpcFleetIdentity(acct.Name, aliasMap));
                SendResponseToRemote(sb.ToString());
                Print("[SIMA] GET_FLEET -> Responded with account list");
            }
            else if (action == "SET_SIMA")
            {
                if (parts.Length > 1)
                {
                    bool enable = parts[1].Trim().ToUpperInvariant() == "ON";
                    ProcessApplySimaState(enable);
                    Print($"V12.Phase6: SET_SIMA = {enable} (lifecycle applied)");
                }
            }
            else if (action == "DIAG_FLEET")
            {
                Print("[DIAG] ##################################################");
                Print($"[DIAG] EnableSIMA = {EnableSIMA}");
                Print($"[DIAG] AccountPrefix = \"{AccountPrefix}\"");
                int total = 0;
                int active = 0;
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct))
                    {
                        total++;
                        bool isActive = false;
                        activeFleetAccounts.TryGetValue(acct.Name, out isActive);
                        if (isActive) active++;
                        Print($"[DIAG]   {acct.Name} -> {(isActive ? "[OK] ACTIVE" : "[X] INACTIVE")}");
                    }
                }
                Print($"[DIAG] TOTAL: {total} accounts | {active} ACTIVE");
                Print("[DIAG] ##################################################");
            }
            else if (action == "SET_LEADER_ACCOUNT")
            {
                if (parts.Length > 1)
                {
                    string newLeader = parts[1].Trim();
                    Print($"V12.25 IPC: Leader Account synced to [{newLeader}]");
                }
            }
            else if (action == "REQUEST_FLEET_STATE")
            {
                StringBuilder fsb = new StringBuilder("FLEET_STATE|");
                fsb.Append(Instrument.FullName).Append("|");
                fsb.Append(Position.MarketPosition).Append("|");

                var fleetAccounts = GetFleetAccountsSnapshot();
                var aliasMap = BuildFleetAliasMap(fleetAccounts);
                List<string> acctStates = new List<string>();
                foreach (Account acct in fleetAccounts)
                {
                    var bPos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                    int act = 0;
                    if (bPos != null && bPos.MarketPosition != MarketPosition.Flat)
                    {
                        act = (bPos.MarketPosition == MarketPosition.Long) ? (int)bPos.Quantity : -(int)bPos.Quantity;
                    }
                    int exp = 0;
                    expectedPositions?.TryGetValue(ExpKey(acct.Name), out exp);
                    acctStates.Add($"{GetIpcFleetIdentity(acct.Name, aliasMap)}:{act}:{exp}");
                }
                fsb.Append(string.Join(";", acctStates));
                SendResponseToRemote(fsb.ToString());
            }
        }

        // -------------------------------------------------------------------------

        private void SendResponseToRemote(string response)
        {
            if (connectedClients == null) return;

            // Diagnostic: Log what we are sending and to how many clients
            if (response.Contains("SYNC_TARGET_STATE"))
                 Print($"V14 IPC: Broadcasting SYNC_TARGET_STATE to {connectedClients.Count} clients");

            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
            List<int> disconnectedClientIds = new List<int>();

            foreach (var kvp in connectedClients.ToArray())
            {
                int clientId = kvp.Key;
                TcpClient client = kvp.Value;
                try
                {
                    if (client.Connected && client.GetStream().CanWrite)
                    {
                        client.GetStream().Write(responseBytes, 0, responseBytes.Length);
                        client.GetStream().Flush();
                    }
                    else
                    {
                        disconnectedClientIds.Add(clientId);
                    }
                }
                catch (Exception ex)
                {
                    Print($"V14 IPC: Send Error - {ex.Message}");
                    disconnectedClientIds.Add(clientId);
                }
            }

            foreach (int clientId in disconnectedClientIds)
            {
                if (connectedClients.TryRemove(clientId, out var staleClient))
                {
                    try { staleClient.Close(); } catch { }
                }
            }
        }

        // V12.13-D: SendToExternalApp REMOVED -- it connected to port 5001 (the strategy's own listener),
        // causing infinite flood loops. All callers now use SendResponseToRemote() or direct client stream writes.
        // V12.44: MoveStopsToBreakevenPlusOne() removed -- dead code, replaced by MoveStopsToBreakevenWithOffset()

        /// <summary>
        /// V10.3: Close a specific target (T1..T5) at market for all active positions
        /// Cancels working limit order and submits market order to close
        /// </summary>
        private void FlattenSpecificTarget(int targetNumber)
        {
            try
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                    int qtyToClose = 0;
                    ConcurrentDictionary<string, Order> targetDict = null;
                    string targetName = "";

                    switch (targetNumber)
                    {
                        case 1: qtyToClose = pos.T1Contracts; targetDict = target1Orders; targetName = "T1"; break;
                        case 2: qtyToClose = pos.T2Contracts; targetDict = target2Orders; targetName = "T2"; break;
                        case 3: qtyToClose = pos.T3Contracts; targetDict = target3Orders; targetName = "T3"; break;
                        case 4: qtyToClose = pos.T4Contracts; targetDict = target4Orders; targetName = "T4"; break;
                        case 5: qtyToClose = pos.T5Contracts; targetDict = target5Orders; targetName = "T5"; break;
                        default:
                            Print(string.Format("V10.3: Invalid target number {0}", targetNumber));
                            return;
                    }

                    if (qtyToClose <= 0)
                    {
                        Print(string.Format("V10.3: {0} has no contracts to close for {1}", targetName, entryName));
                        continue;
                    }

                    // Cancel existing limit order if working
                    if (targetDict != null && targetDict.TryGetValue(entryName, out Order targetOrder))
                    {
                        if (targetOrder != null && (targetOrder.OrderState == OrderState.Working ||
                            targetOrder.OrderState == OrderState.Accepted ||
                            targetOrder.OrderState == OrderState.Submitted))
                        {
                            CancelOrder(targetOrder);
                            Print(string.Format("V10.3: Cancelled {0} limit order for {1}", targetName, entryName));
                        }
                    }

                    // Submit market order to close the target contracts
                    if (pos.Direction == MarketPosition.Long)
                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qtyToClose, 0, 0, "",
                            string.Format("Close{0}_{1}", targetName, entryName));
                    else
                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qtyToClose, 0, 0, "",
                            string.Format("Close{0}_{1}", targetName, entryName));

                    Print(string.Format("V10.3: Closing {0} ({1} contracts) at market for {2}", targetName, qtyToClose, entryName));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR FlattenSpecificTarget: " + ex.Message);
            }
        }

        private void ToggleStrategyMode(string action)
        {
             // V12.20: Atomic flag mutations
             if (action == "MODE_RMA") isRMAModeActive = !isRMAModeActive;
             else if (action == "MODE_MOMO") isMOMOModeActive = !isMOMOModeActive;
             else if (action == "MODE_FFMA")
             {
                 isFFMAModeArmed = true;
                 Print("V12.24: FFMA AUTO armed -- reversal scanner active");
             }
             else if (action == "MODE_M")
             {
                 Print("V12.24: MODE_M received -- immediate FFMA entry pending");
             }
             else if (action == "FFMA_DISARM")
             {
                 isFFMAModeArmed = false;
                 Print("V12.24: FFMA disarmed via panel ResetExecutionMode");
             }
             else if (action == "MODE_TREND_RMA")
             {
                 isTrendRmaMode = true;
                 Print("IPC: TREND RMA Mode Enabled");
             }
             else if (action == "MODE_TREND_STD")
             {
                 isTrendRmaMode = false;
                 Print("IPC: TREND Standard Mode Enabled");
             }
             else if (action == "MODE_RETEST_RMA")
             {
                 isRetestRmaMode = true;
                 Print("IPC: RETEST RMA Mode Enabled");
             }
             else if (action == "MODE_RETEST_STD")
             {
                 isRetestRmaMode = false;
                 Print("IPC: RETEST Standard Mode Enabled");
             }

             // IPC commands already run inside the actor, so execute directly here.
             if (action == "EXEC_TREND" || action == "EXEC_TREND_RMA")
             {
                 double trendDist   = CalculateTRENDStopDistance();
                 int trendContracts = CalculatePositionSize(trendDist);
                 ExecuteTRENDEntry(trendContracts);
             }
             else if (action == "EXEC_RETEST" || action == "EXEC_RETEST_PLUS" || action == "EXEC_RETEST_MINUS")
             {
                 double retestDist   = CalculateRetestStopDistance();
                 int retestContracts = CalculatePositionSize(retestDist);
                 ExecuteRetestEntry(retestContracts);
             }
             else if (action == "EXEC_MOMO")
            {
                double momoStopDist = Math.Min(MOMOStopPoints, MaximumStop);
                int momoContracts   = CalculatePositionSize(momoStopDist);
                double capturedMomoPrice = lastKnownPrice;
                ExecuteMOMOEntry(capturedMomoPrice, momoContracts);
            }
             else if (action == "MODE_M")
             {
                 // V12.24: Immediate market entry using FFMA trade DNA
                 double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                 double ema9Value = _ema9Val;
                 MarketPosition direction = currentPrice > ema9Value ? MarketPosition.Short : MarketPosition.Long;
                 Print(string.Format("V12.24: MODE_M firing -- Price={0:F2} vs EMA9={1:F2} -> {2}", currentPrice, ema9Value, direction));
                 double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                 double ffmaStopDist = Math.Min(Math.Abs(currentPrice - stopPrice), MaximumStop);
                 if (ffmaStopDist < tickSize * 2) ffmaStopDist = tickSize * 2;
                 int ffmaContracts = CalculatePositionSize(ffmaStopDist);
                 ExecuteFFMAEntry(direction, ffmaContracts);
             }

             Print(string.Format("IPC Mode Toggle: {0} | RMA={1} MOMO={2} TrendRMA={3} RetestRMA={4} FFMA={5}",
                action, isRMAModeActive, isMOMOModeActive, isTrendRmaMode, isRetestRmaMode, isFFMAModeArmed));
        }



        #endregion
    }
}
