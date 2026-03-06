// V12.44 MODULAR: UI Callbacks Module (Split from UI.cs)
// Contains: Hotkey handlers, chart click handlers, target/runner action executors
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

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region UI

        // V12.1101E [D-01]: Removed legacy no-op UI stub remnants.

        private void AttachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
            {
                ChartControl.OwnerChart.PreviewKeyDown += OnKeyDown;
            }
        }

        private void DetachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
            {
                ChartControl.OwnerChart.PreviewKeyDown -= OnKeyDown;
            }
        }

        private void AttachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewMouseLeftButtonDown += OnChartClick;
            }
        }

        private void DetachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewMouseLeftButtonDown -= OnChartClick;
            }
        }

        /// <summary>
        /// V8.6: Click-to-Price handler for RMA entries.
        /// RMA uses Limit orders (click above = short, click below = long).
        /// </summary>
        private void OnChartClick(object sender, MouseButtonEventArgs e)
        {
            // Check if Shift is held OR RMA button mode is active
            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool rmaActive = (RMAEnabled && (shiftHeld || isRMAModeActive));

            if (!rmaActive) return;

            try
            {
                if (ChartControl == null || ChartPanel == null) return;

                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // ###################################################################
                // V12.4: ChartPanel-based price conversion (PROVEN WORKING)
                // ChartPanel.H includes time axis - effective price area is ~67% of height
                // ###################################################################
                Point mouseInPanel = e.GetPosition(ChartPanel as System.Windows.IInputElement);

                // Build 1102Z: UI Safety Fence -- Ignore clicks outside the actual price plotting area
                // This prevents trades from triggering when clicking on the side panel, price axis, or scrollbars.
                if (mouseInPanel.X < 0 || mouseInPanel.X > ChartPanel.W || mouseInPanel.Y < 0 || mouseInPanel.Y > ChartPanel.H)
                {
                    return;
                }

                double panelHeight = ChartPanel.H;
                double maxPrice = ChartPanel.MaxValue;
                double minPrice = ChartPanel.MinValue;
                double priceRange = maxPrice - minPrice;

                // CRITICAL: ChartPanel.H includes time axis at bottom
                // The actual price plotting area is approximately 67% of total panel height
                double effectivePriceHeight = panelHeight * 0.667;

                // Clamp Y to valid range
                double yInPanel = mouseInPanel.Y;
                if (yInPanel < 0) yInPanel = 0;
                if (yInPanel > effectivePriceHeight) yInPanel = effectivePriceHeight;

                // Convert: Y=0 is top (maxPrice), Y=effectivePriceHeight is bottom (minPrice)
                double yRatio = yInPanel / effectivePriceHeight;
                double clickPrice = maxPrice - (yRatio * priceRange);

                Print($"RMA v12.4 CLICK: x={mouseInPanel.X:F1}, y={mouseInPanel.Y:F1}, w={ChartPanel.W:F1}, h={panelHeight:F1}, ratio={yRatio:F3}, price={clickPrice:F2} (Market={currentPrice:F2})");

                // Round to tick size
                clickPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);

                // Validate price is within chart range
                if (clickPrice < minPrice - priceRange || clickPrice > maxPrice + priceRange)
                {
                    Print($"RMA: Click price {clickPrice:F2} outside valid range [{minPrice:F2} - {maxPrice:F2}]");
                    return;
                }

                MarketPosition direction = (clickPrice > currentPrice) ? MarketPosition.Short : MarketPosition.Long;
                double rmaStopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                int rmaContracts   = CalculatePositionSize(rmaStopDist);
                ExecuteRMAEntryV2(clickPrice, direction, rmaContracts);

                if (isRMAButtonClicked)
                {
                    isRMAButtonClicked = false;
                    isRMAModeActive = false;
                    Print("V12.43: RMA auto-deactivated after entry");
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Print($"ERROR OnChartClick: {ex.Message}");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // V12.1101E [PH5-COLLIDE-01]: Panic hotkey routes through lifecycle-safe flatten pipeline.
            if (e.Key == Key.F) { FlattenAll(); e.Handled = true; }

            // v5.12: T1 Actions (1 + letter)
            else if (Keyboard.IsKeyDown(Key.D1) || Keyboard.IsKeyDown(Key.NumPad1))
            {
                if (e.Key == Key.M) { ExecuteTargetAction("T1", "market"); e.Handled = true; }
                else if (e.Key == Key.O) { ExecuteTargetAction("T1", "1point"); e.Handled = true; }
                else if (e.Key == Key.W) { ExecuteTargetAction("T1", "2point"); e.Handled = true; }
                else if (e.Key == Key.K) { ExecuteTargetAction("T1", "marketprice"); e.Handled = true; }
                else if (e.Key == Key.B) { ExecuteTargetAction("T1", "breakeven"); e.Handled = true; }
                else if (e.Key == Key.C) { ExecuteTargetAction("T1", "cancel"); e.Handled = true; }
            }

            // v5.12: T2 Actions (2 + letter)
            else if (Keyboard.IsKeyDown(Key.D2) || Keyboard.IsKeyDown(Key.NumPad2))
            {
                if (e.Key == Key.M) { ExecuteTargetAction("T2", "market"); e.Handled = true; }
                else if (e.Key == Key.O) { ExecuteTargetAction("T2", "1point"); e.Handled = true; }
                else if (e.Key == Key.W) { ExecuteTargetAction("T2", "2point"); e.Handled = true; }
                else if (e.Key == Key.K) { ExecuteTargetAction("T2", "marketprice"); e.Handled = true; }
                else if (e.Key == Key.B) { ExecuteTargetAction("T2", "breakeven"); e.Handled = true; }
                else if (e.Key == Key.C) { ExecuteTargetAction("T2", "cancel"); e.Handled = true; }
            }

            // v5.12: Runner Actions (3 + letter)
            else if (Keyboard.IsKeyDown(Key.D3) || Keyboard.IsKeyDown(Key.NumPad3))
            {
                if (e.Key == Key.M) { ExecuteRunnerAction("market"); e.Handled = true; }
                else if (e.Key == Key.O) { ExecuteRunnerAction("stop1pt"); e.Handled = true; }
                else if (e.Key == Key.W) { ExecuteRunnerAction("stop2pt"); e.Handled = true; }
                else if (e.Key == Key.B) { ExecuteRunnerAction("stopbe"); e.Handled = true; }
                else if (e.Key == Key.P) { ExecuteRunnerAction("lock50"); e.Handled = true; }  // P for Profit
                else if (e.Key == Key.D) { ExecuteRunnerAction("disabletrail"); e.Handled = true; }
            }

            // RMA uses Shift+Click (R conflicts with NT search, Ctrl conflicts with chart drag)
        }

        #endregion

        #region Target & Runner Actions
        // v5.12: Execute target actions (T1..T5)
        private void ExecuteTargetAction(string targetType, string action)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print($"{targetType} ACTION: No active positions");
                    return;
                }

                // V8.30: Thread-safe snapshot iteration
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled)
                    {
                        Print($"{targetType} ACTION: Position {entryName} not filled yet");
                        continue;
                    }

                    if (!TryResolveTargetContext(pos, targetType, out int targetNumber, out var targetOrders, out int targetContracts, out bool targetFilled))
                    {
                        Print($"{targetType} ACTION: Invalid target identifier");
                        continue;
                    }

                    if (targetContracts <= 0)
                    {
                        Print($"{targetType} ACTION: No contracts assigned for {entryName}");
                        continue;
                    }

                    if (IsRunnerTarget(targetNumber) && action != "market" && action != "cancel")
                    {
                        Print($"{targetType} ACTION: Target is configured as Runner (trail-only), action {action} skipped for {entryName}");
                        continue;
                    }

                    if (targetFilled)
                    {
                        Print($"{targetType} ACTION: {targetType} already filled for {entryName}");
                        continue;
                    }

                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                    switch (action)
                    {
                        case "market":
                            // Fill target at market NOW
                            // V8.30: Thread-safe removal
                            if (targetOrders.TryRemove(entryName, out var existingOrder))
                            {
                                CancelOrder(existingOrder);
                            }

                            Order marketOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, targetContracts, 0, 0, "", $"{targetType}_Market_{entryName}")
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, targetContracts, 0, 0, "", $"{targetType}_Market_{entryName}");

                            Print($"? {targetType} MARKET FILL: {entryName} - Closing {targetContracts} contracts at market");
                            break;

                        case "1point":
                            // V8.18: Absolute profit target (Entry + 1 point)
                            double newPrice1pt = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + 1.0
                                : pos.EntryPrice - 1.0;
                            newPrice1pt = Instrument.MasterInstrument.RoundToTickSize(newPrice1pt);

                            Print($"? {targetType} -> 1 POINT PROFIT: {entryName} - New target @ {newPrice1pt:F2} (Entry was {pos.EntryPrice:F2})");

                            MoveTargetOrder(entryName, targetType, newPrice1pt, targetContracts, pos.Direction);
                            break;

                        case "2point":
                            // V8.18: Absolute profit target (Entry + 2 points)
                            double newPrice2pt = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + 2.0
                                : pos.EntryPrice - 2.0;
                            newPrice2pt = Instrument.MasterInstrument.RoundToTickSize(newPrice2pt);

                            Print($"? {targetType} -> 2 POINTS PROFIT: {entryName} - New target @ {newPrice2pt:F2} (Entry was {pos.EntryPrice:F2})");

                            MoveTargetOrder(entryName, targetType, newPrice2pt, targetContracts, pos.Direction);
                            break;

                        case "marketprice":
                            // Move target to current market price (instant fill)
                            double marketPrice = Instrument.MasterInstrument.RoundToTickSize(currentPrice);
                            MoveTargetOrder(entryName, targetType, marketPrice, targetContracts, pos.Direction);
                            Print($"? {targetType} -> MARKET PRICE: {entryName} - New target @ {marketPrice:F2}");
                            break;

                        case "breakeven":
                            // Move target to breakeven (entry price)
                            MoveTargetOrder(entryName, targetType, pos.EntryPrice, targetContracts, pos.Direction);
                            Print($"? {targetType} -> BREAKEVEN: {entryName} - New target @ {pos.EntryPrice:F2}");
                            break;

                        case "cancel":
                            // Cancel target order - let contracts run
                            // V8.30: Thread-safe removal
                            if (targetOrders.TryRemove(entryName, out var cancelOrder))
                            {
                                CancelOrder(cancelOrder);
                                Print($"? {targetType} CANCELLED: {entryName} - {targetContracts} contracts will run with stop");
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR ExecuteTargetAction ({targetType}, {action}): {ex.Message}");
            }
        }

        private void MoveTargetOrder(string entryName, string targetType, double newPrice, int quantity, MarketPosition direction)
        {
            if (!TryParseTargetNumber(targetType, out int targetNumber))
                return;

            // Runner targets are trail-only: do not submit limit orders.
            if (IsRunnerTarget(targetNumber))
            {
                Print($"MoveTargetOrder SKIPPED: {targetType} is configured as Runner (trail-only)");
                return;
            }

            if (quantity <= 0) return;

            ConcurrentDictionary<string, Order> targetOrders = GetTargetOrdersDictionary(targetNumber);
            if (targetOrders == null) return;

            // V8.30: Thread-safe cancel existing target order
            if (targetOrders.TryRemove(entryName, out var existingTarget))
            {
                CancelOrder(existingTarget);
            }

            // Submit new target order at new price
            Order newTargetOrder = direction == MarketPosition.Long
                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, quantity, newPrice, 0, "", $"{targetType}_{entryName}")
                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, quantity, newPrice, 0, "", $"{targetType}_{entryName}");

            if (newTargetOrder != null)
            {
                targetOrders[entryName] = newTargetOrder;
            }
        }

        private bool TryResolveTargetContext(
            PositionInfo pos,
            string targetType,
            out int targetNumber,
            out ConcurrentDictionary<string, Order> targetOrders,
            out int targetContracts,
            out bool targetFilled)
        {
            targetOrders = null;
            targetContracts = 0;
            targetFilled = false;

            if (!TryParseTargetNumber(targetType, out targetNumber))
                return false;

            targetOrders = GetTargetOrdersDictionary(targetNumber);
            targetContracts = GetTargetContracts(pos, targetNumber);
            targetFilled = IsTargetFilled(pos, targetNumber);
            return targetOrders != null;
        }

        private static bool TryParseTargetNumber(string targetType, out int targetNumber)
        {
            targetNumber = 0;
            if (string.IsNullOrWhiteSpace(targetType)) return false;

            string normalized = targetType.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("T")) return false;

            return int.TryParse(normalized.Substring(1), out targetNumber) &&
                   targetNumber >= 1 &&
                   targetNumber <= 5;
        }

        private ConcurrentDictionary<string, Order> GetTargetOrdersDictionary(int targetNumber) => targetNumber switch
        {
            1 => target1Orders,
            2 => target2Orders,
            3 => target3Orders,
            4 => target4Orders,
            5 => target5Orders,
            _ => null
        };

        // v5.12: Execute runner actions
        private void ExecuteRunnerAction(string action)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("RUNNER ACTION: No active positions");
                    return;
                }

                // V8.30: Thread-safe snapshot iteration
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled)
                    {
                        Print($"RUNNER ACTION: Position {entryName} not filled yet");
                        continue;
                    }

                    // Calculate runner contracts (remaining after T1 and T2)
                    int runnerContracts = pos.RemainingContracts;
                    if (runnerContracts <= 0)
                    {
                        Print($"RUNNER ACTION: No runner contracts for {entryName}");
                        continue;
                    }

                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                    switch (action)
                    {
                        case "market":
                            // Close runner at market
                            Order runnerMarketOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, runnerContracts, 0, 0, "", $"Runner_Market_{entryName}")
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, runnerContracts, 0, 0, "", $"Runner_Market_{entryName}");

                            Print($"? RUNNER MARKET CLOSE: {entryName} - Closing {runnerContracts} contracts at market");
                            break;

                        case "stop1pt":
                            // V8.19: Absolute profit lock (Entry + 1 point)
                            double newStop1pt = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + 1.0
                                : pos.EntryPrice - 1.0;
                            newStop1pt = Instrument.MasterInstrument.RoundToTickSize(newStop1pt);

                            // Safety: Only move if it's better than current stop or entry-relative profit-lock
                            UpdateStopOrder(entryName, pos, newStop1pt, pos.CurrentTrailLevel);
                            Print($"? RUNNER STOP -> 1 PT PROFIT LOCK: {entryName} - Stop @ {newStop1pt:F2} (Entry was {pos.EntryPrice:F2})");
                            break;

                        case "stop2pt":
                            // V8.19: Absolute profit lock (Entry + 2 points)
                            double newStop2pt = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + 2.0
                                : pos.EntryPrice - 2.0;
                            newStop2pt = Instrument.MasterInstrument.RoundToTickSize(newStop2pt);

                            UpdateStopOrder(entryName, pos, newStop2pt, pos.CurrentTrailLevel);
                            Print($"? RUNNER STOP -> 2 PT PROFIT LOCK: {entryName} - Stop @ {newStop2pt:F2} (Entry was {pos.EntryPrice:F2})");
                            break;

                        case "stopbe":
                            // [Build 1102I] Use correct BE stop formula: EntryPrice +/- BreakEvenOffsetTicks.
                            // Guard checks vs full beStopTarget, not raw entry, to prevent partial-offset execution.
                            double beStopTarget = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + (BreakEvenOffsetTicks * Instrument.MasterInstrument.TickSize)
                                : pos.EntryPrice - (BreakEvenOffsetTicks * Instrument.MasterInstrument.TickSize);
                            beStopTarget = Instrument.MasterInstrument.RoundToTickSize(beStopTarget);
                            bool beViable = pos.Direction == MarketPosition.Long
                                ? currentPrice >= beStopTarget
                                : currentPrice <= beStopTarget;
                            if (!beViable)
                            {
                                pos.ManualBreakevenArmed     = true;
                                pos.ManualBreakevenTriggered = false;
                                Print($"? BE SHIELD: {entryName} price {currentPrice:F2} not at BE level {beStopTarget:F2} -- armed for auto-trigger");
                                break;
                            }
                            UpdateStopOrder(entryName, pos, beStopTarget, 1);
                            // [Build 1102K] Mark triggered so ManageTrailingStops armed path does not re-fire.
                            pos.ManualBreakevenTriggered = true;
                            Print($"? RUNNER STOP -> BREAKEVEN: {entryName} - Stop @ {beStopTarget:F2} (Entry +/- {BreakEvenOffsetTicks} ticks)");
                            break;

                        case "lock50":
                            // Lock 50% of current profit
                            double unrealizedProfit = pos.Direction == MarketPosition.Long
                                ? currentPrice - pos.EntryPrice
                                : pos.EntryPrice - currentPrice;
                            double lock50Stop = pos.Direction == MarketPosition.Long
                                ? pos.EntryPrice + (unrealizedProfit * 0.5)
                                : pos.EntryPrice - (unrealizedProfit * 0.5);
                            lock50Stop = Instrument.MasterInstrument.RoundToTickSize(lock50Stop);
                            UpdateStopOrder(entryName, pos, lock50Stop, pos.CurrentTrailLevel);
                            Print($"? RUNNER LOCK 50%: {entryName} - Stop @ {lock50Stop:F2} (profit: {unrealizedProfit:F2})");
                            break;

                        case "disabletrail":
                            // Disable trailing - keep stop where it is
                            pos.CurrentTrailLevel = 999; // Set to high number to prevent further trailing
                            Print($"? RUNNER TRAILING DISABLED: {entryName} - Stop fixed @ {pos.CurrentStopPrice:F2}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR ExecuteRunnerAction ({action}): {ex.Message}");
            }
        }
        #endregion
    }
}
