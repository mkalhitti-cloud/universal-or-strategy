// Build 971: UI.IPC.Commands.Mode -- TryHandleDiagCommand, TryHandleModeCommand, TryHandleRiskCommand
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
        #region IPC Commands Mode

        private bool TryHandleModeCommand(string action, string[] parts)
        {
            if (action == "SET_RMA_MODE")
            {
                if (parts.Length > 1)
                {
                    bool enable = parts[1].Trim().ToUpperInvariant() == "ON";
                    isRMAModeActive = enable;
                    isRMAButtonClicked = enable;
                    Print(string.Format("V12.4: SET_RMA_MODE = {0} (Chart-Click RMA {1})", enable, enable ? "ENABLED" : "DISABLED"));
                }
                return true;
            }
            // V12.2: SYNC_MODE|{MODE} - Relay mode sync from chart panel to external app
            if (action == "SYNC_MODE")
            {
                if (parts.Length > 1)
                {
                    string syncMode = parts[1].Trim().ToUpperInvariant();
                    // V12.13-D: Broadcast SYNC_MODE to all connected panel clients
                    SendResponseToRemote($"SYNC_MODE|{syncMode}");
                    Print(string.Format("V12.2: SYNC_MODE Relay -> {0}", syncMode));
                }
                return true;
            }
            // Phase 9.1: MKT_SYNC -- Toggle ToS Armed Mode (Top button)
            if (action == "MKT_SYNC")
            {
                isTosSyncMode = !isTosSyncMode;
                Print(string.Format("[SYNC] ToS Sync Mode: {0}", isTosSyncMode));
                return true;
            }
            // Phase 9.1: SYNC_ALL -- Refresh active target orders to match current panel config (Bottom button)
            if (action == "SYNC_ALL")
            {
                Print("[SYNC_ALL] Refresh triggered -- recalculating active target orders");
                RefreshActivePositionOrders();
                return true;
            }
            // V12.5: SET_MODE|mode - Panel is sole source of truth
            if (action == "SET_MODE")
            {
                if (parts.Length > 1)
                {
                    string newMode = parts[1].Trim().ToUpperInvariant();

                    // V12.20: Atomic mode transition -- prevents partial state reads during switch
                    isRMAModeActive = false;
                    isRMAButtonClicked = false;
                    isRetestModeActive = false;
                    isTRENDModeActive = false;
                    isMOMOModeActive = false;
                    isFFMAModeArmed = false;

                    if (newMode == "RMA")
                    {
                        isRMAModeActive = true;
                        isRMAButtonClicked = true;
                    }
                    else if (newMode == "RETEST")
                    {
                        isRetestModeActive = true;
                    }
                    else if (newMode == "TREND")
                    {
                        isTRENDModeActive = true;
                    }
                    else if (newMode == "MOMO")
                    {
                        ActivateMOMOMode();
                    }
                    else if (newMode == "FFMA")
                    {
                        isFFMAModeArmed = true;
                    }
                    // ORB/OR = all modes off (already deactivated above)

                    Print(string.Format("V12.25: SET_MODE = {0} | RMA={1} RETEST={2} TREND={3} MOMO={4} FFMA={5} (no CONFIG echo)",
                        newMode, isRMAModeActive, isRetestModeActive, isTRENDModeActive, isMOMOModeActive, isFFMAModeArmed));

                    // V12.25: CONFIG broadcast REMOVED -- Panel is sole source of truth.
                    // Sending CONFIG back here caused the Ping-Pong overwrite bug.
                }
                return true;
            }
            if (action.StartsWith("MODE_") || action.StartsWith("EXEC_") || action == "FFMA_DISARM")
            {
                ToggleStrategyMode(action);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handles risk and position parameter commands: SET_TRAIL, SET_CIT,
        /// BE variants, SET_MAX_RISK, SET_ANCHOR, SET_TARGETS, SET_MANUAL_PRICE.
        /// </summary>
        private bool TryHandleRiskCommand(string action, string[] parts)
        {
            if (action == "SET_TRAIL")
            {
                // V12 PRO: Dynamic trail - move stop to current price +/- distance
                if (parts.Length >= 2 && double.TryParse(parts[1], out double trailDistance))
                {
                    if (activePositions.Count == 0)
                    {
                        Print("[V12] SET_TRAIL: No active positions");
                    }
                    else
                    {
                        double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                        int trailCount = 0;

                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            PositionInfo pos = kvp.Value;
                            string entryName = kvp.Key;

                            if (!pos.EntryFilled) continue;

                            // Calculate new stop: Longs = Price - Distance, Shorts = Price + Distance
                            double newStopPrice = pos.Direction == MarketPosition.Long
                                ? currentPrice - trailDistance
                                : currentPrice + trailDistance;

                            newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                            UpdateStopOrder(entryName, pos, newStopPrice, pos.CurrentTrailLevel);
                            trailCount++;
                            Print(string.Format("[V12] SET_TRAIL: {0} -> Stop @ {1:F2} (Price: {2:F2}, Dist: {3})",
                                entryName, newStopPrice, currentPrice, trailDistance));
                        }

                        Print(string.Format("[V12] SET_TRAIL COMPLETE: Updated {0} position(s) with {1} pt trail", trailCount, trailDistance));
                    }
                }
                else
                {
                    Print("[V12] SET_TRAIL: Invalid distance parameter");
                }
                return true;
            }
            if (action == "SET_CIT")
            {
                if (parts.Length >= 2)
                {
                    ChaseIfTouchPoints = parts[1].Trim();
                    Print($"[V12] CIT updated: {ChaseIfTouchPoints}");
                }
                return true;
            }
            if (action == "BE" || action == "BE_CUSTOM" || action == "BE_PLUS_2" || action == "BE_PLUS_1") // V12.23: +BE_CUSTOM with dynamic ticks
            {
                double beOffset;
                if (action == "BE_CUSTOM" && parts.Length >= 2)
                {
                    // V12.23: Dynamic ticks from panel input -- syncs auto-trail BE too
                    int customTicks;
                    if (!int.TryParse(parts[1].Trim(), out customTicks) || customTicks < 0)
                        customTicks = BreakEvenOffsetTicks; // fallback to default
                    BreakEvenOffsetTicks = customTicks; // V12.23: Sync auto-trail + fleet symmetry
                    beOffset = customTicks * tickSize;
                }
                else if (action == "BE" || action == "BE_PLUS_2")
                    beOffset = BreakEvenOffsetTicks * tickSize;
                else
                    beOffset = 1 * tickSize; // Legacy BE_PLUS_1
                MoveStopsToBreakevenWithOffset(beOffset);
                return true;
            }
            if (action.StartsWith("SET_MAX_RISK"))
            {
                if (parts.Length > 2 && double.TryParse(parts[2], out double val))
                {
                    MaxRiskAmount = val;
                    RiskPerTrade = val; // Sync legacy property
                    Print($"[V12.2] SET_MAX_RISK: {val}");
                }
                return true;
            }
            if (action.StartsWith("SET_ANCHOR"))
            {
                // V11: SET_ANCHOR|EMA30|Global
                if (parts.Length > 2)
                {
                    string anchorStr = parts[1];
                    SetRmaAnchorFromIpc(anchorStr);
                }
                return true;
            }
            // V12.5: SET_TARGETS|count - Panel is sole source of truth
            // V12.Phase8.3: Now writes to activeTargetCount -- minContracts is symbol-specific risk floor only
            if (action == "SET_TARGETS")
            {
                if (parts.Length > 1 && int.TryParse(parts[1], out int targetCount))
                {
                    // FIX-B [Build 1102Z]: Clamp inside the actor to prevent IPC race with SIMA dispatch loop.
                    int clamped = Math.Max(1, Math.Min(5, targetCount));
                    activeTargetCount = clamped;
                    Print(string.Format("V12.Phase8.3: SET_TARGETS = {0} targets (clamped from {1}; minContracts preserved at {2})", clamped, targetCount, minContracts));
                    // V12.25: CONFIG broadcast REMOVED -- Panel is sole source of truth.
                    // Sending CONFIG back here caused the Ping-Pong overwrite bug.
                    // Build 1102Y [U-02]: Immediately sync panel visibility -- panel needs the count, not a CONFIG echo.
                    SendResponseToRemote($"SYNC_TARGET_STATE|{clamped}");
                }
                return true;
            }
            if (action == "SET_MANUAL_PRICE")
            {
                // Format: SET_MANUAL_PRICE|<symbol>|<price>  (symbol in parts[1] for router, price in parts[2])
                // NOTE: External callers must use the new symbol-first format (updated Build 944).
                if (parts.Length > 2 && double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double manualPrice))
                {
                    cachedMnlPrice = manualPrice;
                    currentRmaAnchor = RmaAnchorType.Manual;
                    // V12.1101E [D-02]: Legacy isMnlArmed flag purged; cachedMnlPrice + anchor state is authoritative.

                    Print(string.Format("IPC SET_MANUAL_PRICE: {0:F2} | Anchor set to MANUAL", manualPrice));
                }
                else
                {
                    Print(string.Format("IPC SET_MANUAL_PRICE: Invalid price format in command: {0}", string.Join("|", parts)));
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handles fleet execution commands: TRIM variants, LOCK_50, FLATTEN variants, CANCEL_ALL,
        /// RESET_MEMORY, LONG/SHORT entries, OR entries, manual limit entries, CLOSE_T*, MOVE_TARGET*,
        /// GET_FLEET*, TOGGLE_ACCOUNT, SET_SIMA, SET_LEADER_ACCOUNT, REQUEST_FLEET_STATE.
        /// </summary>

        #endregion
    }
}
