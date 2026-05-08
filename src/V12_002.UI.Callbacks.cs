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
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region UI

        // [Build 1108.002-HF1] Overlay rectangle for click-trader border warning (no layout reflow)
        private System.Windows.Shapes.Rectangle _chartHoverOverlay;
        private Grid _chartOverlayParentGrid;

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
                ChartControl.MouseMove += OnChartMouseMove;
                ChartControl.MouseLeave += OnChartMouseLeave;

                // [Build 1108.002-HF1] Create transparent overlay for border warning.
                // Overlay avoids layout reflow from border-width mutation and caches
                // the parent reference to avoid repeated visual-tree walks.
                try
                {
                    DependencyObject parent = VisualTreeHelper.GetParent(ChartControl);
                    while (parent != null && !(parent is Grid))
                        parent = VisualTreeHelper.GetParent(parent);

                    if (parent is Grid parentGrid)
                    {
                        _chartHoverOverlay = new System.Windows.Shapes.Rectangle
                        {
                            Fill = Brushes.Transparent,
                            Stroke = Brushes.Red,
                            StrokeThickness = 4,
                            IsHitTestVisible = false,
                            Visibility = Visibility.Collapsed
                        };

                        int colSpan = Math.Max(1, parentGrid.ColumnDefinitions.Count);
                        int rowSpan = Math.Max(1, parentGrid.RowDefinitions.Count);
                        Grid.SetColumnSpan(_chartHoverOverlay, colSpan);
                        Grid.SetRowSpan(_chartHoverOverlay, rowSpan);
                        System.Windows.Controls.Panel.SetZIndex(_chartHoverOverlay, 9999);

                        parentGrid.Children.Add(_chartHoverOverlay);
                        _chartOverlayParentGrid = parentGrid;
                    }
                }
                catch (Exception ex)
                {
                    Print("[V12] Overlay creation failed: " + ex.Message);
                }
            }
        }

        private void DetachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewMouseLeftButtonDown -= OnChartClick;
                ChartControl.MouseMove -= OnChartMouseMove;
                ChartControl.MouseLeave -= OnChartMouseLeave;
                ClearClickTraderBorderIfActive();
            }

            // [Build 1108.002-HF1] Remove overlay from visual tree on teardown
            if (_chartHoverOverlay != null && _chartOverlayParentGrid != null)
            {
                try
                {
                    _chartOverlayParentGrid.Children.Remove(_chartHoverOverlay);
                }
                catch { }
            }
            _chartHoverOverlay = null;
            _chartOverlayParentGrid = null;
        }

        private bool IsClickTraderArmed()
        {
            return (RMAEnabled && isRMAModeActive) || (MOMOEnabled && isMOMOModeActive);
        }

        private bool IsPointerInPriceArea(MouseEventArgs e)
        {
            if (ChartPanel == null || e == null) return false;

            Point mouseInPanel = e.GetPosition(ChartPanel as System.Windows.IInputElement);
            if (mouseInPanel.X < 0 || mouseInPanel.X > ChartPanel.W || mouseInPanel.Y < 0 || mouseInPanel.Y > ChartPanel.H)
                return false;

            double effectivePriceHeight = ChartPanel.H * 0.667;
            return mouseInPanel.Y <= effectivePriceHeight;
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            if (_isTerminating) return;

            bool shouldWarn = IsClickTraderArmed() && IsPointerInPriceArea(e);
            if (shouldWarn == _chartHoverRedActive) return;

            if (shouldWarn)
            {
                SetChartBorderWarning(true);
                _chartHoverRedActive = true;
            }
            else
            {
                SetChartBorderWarning(false);
                _chartHoverRedActive = false;
            }
        }

        private void OnChartMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_chartHoverRedActive) return;

            SetChartBorderWarning(false);
            _chartHoverRedActive = false;
        }

        private void SetChartBorderWarning(bool active)
        {
            if (_chartHoverOverlay == null) return;
            _chartHoverOverlay.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearClickTraderBorderIfActive()
        {
            if (!_chartHoverRedActive) return;

            if (ChartControl == null)
            {
                _chartHoverRedActive = false;
                return;
            }

            Action clearWarning = () =>
            {
                if (!_chartHoverRedActive) return;
                SetChartBorderWarning(false);
                _chartHoverRedActive = false;
            };

            if (ChartControl.Dispatcher.CheckAccess())
                clearWarning();
            else
                ChartControl.Dispatcher.InvokeAsync(clearWarning);
        }

        private void ClearClickTraderBorderIfInactive()
        {
            if (IsClickTraderArmed()) return;
            ClearClickTraderBorderIfActive();
        }

        /// <summary>
        /// V8.6: Click-to-Price handler for RMA and MOMO entries
        /// RMA uses Limit orders (click above = short, click below = long)
        /// MOMO uses Stop Market orders (click above = long, click below = short)
        /// </summary>
        private void OnChartClick(object sender, MouseButtonEventArgs e)
        {
            if (!HandleChartClick_ValidateMode(out bool rmaActive, out bool momoActive)) return;

            try
            {
                if (ChartControl == null || ChartPanel == null) return;

                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (!HandleChartClick_ConvertPrice(e, momoActive, currentPrice, out double clickPrice))
                    return;

                if (momoActive)
                {
                    HandleChartClick_ExecuteMomo(clickPrice);
                }
                else
                {
                    HandleChartClick_ExecuteRma(clickPrice, currentPrice);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Print("ERROR OnChartClick: " + ex.Message);
            }
        }

        private bool HandleChartClick_ValidateMode(out bool rmaActive, out bool momoActive)
        {
            // Check if Shift is held OR RMA/MOMO button mode is active
            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            rmaActive = (RMAEnabled && (shiftHeld || isRMAModeActive));
            momoActive = (MOMOEnabled && isMOMOModeActive);

            return rmaActive || momoActive;
        }

        private bool HandleChartClick_ConvertPrice(
            MouseButtonEventArgs e,
            bool momoActive,
            double currentPrice,
            out double clickPrice)
        {
            clickPrice = 0;

            // ###################################################################
            // V12.4: ChartPanel-based price conversion (PROVEN WORKING)
            // ChartPanel.H includes time axis - effective price area is ~67% of height
            // ###################################################################
            Point mouseInPanel = e.GetPosition(ChartPanel as System.Windows.IInputElement);

            // Build 1102Z: UI Safety Fence -- Ignore clicks outside the actual price plotting area
            // This prevents trades from triggering when clicking on the side panel, price axis, or scrollbars.
            if (mouseInPanel.X < 0 || mouseInPanel.X > ChartPanel.W || mouseInPanel.Y < 0 || mouseInPanel.Y > ChartPanel.H)
            {
                return false;
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
            clickPrice = maxPrice - (yRatio * priceRange);

            string modeLabel = momoActive ? "MOMO" : "RMA";
            Print(string.Format("{0} v12.4 CLICK: x={1:F1}, y={2:F1}, w={3:F1}, h={4:F1}, ratio={5:F3}, price={6:F2} (Market={7:F2})",
                modeLabel, mouseInPanel.X, mouseInPanel.Y, ChartPanel.W, panelHeight, yRatio, clickPrice, currentPrice));

            // Round to tick size
            clickPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);

            // Validate price is within chart range
            if (clickPrice < minPrice - priceRange || clickPrice > maxPrice + priceRange)
            {
                Print(string.Format("{0}: Click price {1:F2} outside valid range [{2:F2} - {3:F2}]",
                    modeLabel, clickPrice, minPrice, maxPrice));
                return false;
            }

            return true;
        }

        private void HandleChartClick_ExecuteMomo(double clickPrice)
        {
            // MOMO uses a fixed-points stop: Math.Min(MOMOStopPoints, MaximumStop)
            double momoStopDist = Math.Min(MOMOStopPoints, MaximumStop);
            int momoContracts   = CalculatePositionSize(momoStopDist);
            double capturedMomoPrice = clickPrice; int capturedMomoContracts = momoContracts;
            Enqueue(ctx => ctx.ExecuteMOMOEntry(capturedMomoPrice, capturedMomoContracts));
        }

        private void HandleChartClick_ExecuteRma(double clickPrice, double currentPrice)
        {
            MarketPosition direction = (clickPrice > currentPrice) ? MarketPosition.Short : MarketPosition.Long;
            double rmaStopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
            int rmaContracts   = CalculatePositionSize(rmaStopDist);
            double capturedRmaPrice = clickPrice; MarketPosition capturedDir = direction; int capturedRmaContracts = rmaContracts;
            Enqueue(ctx => ctx.ExecuteRMAEntryV2(capturedRmaPrice, capturedDir, capturedRmaContracts));

            if (isRMAButtonClicked)
                HandleChartClick_DeactivateRma();
        }

        private void HandleChartClick_DeactivateRma()
        {
            isRMAButtonClicked = false;
            isRMAModeActive = false;
            ClearClickTraderBorderIfInactive();

            // V12.43: Lightweight deactivation -- only signal mode change, don't clobber config
            SendResponseToRemote("SET_RMA_MODE|OFF");
            Print("V12.43: RMA auto-deactivated after entry (lightweight signal, no CONFIG clobber)");
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Basic hotkeys
            if (e.Key == Key.L) { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteLong(orContracts)); e.Handled = true; }
            else if (e.Key == Key.S) { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteShort(orContracts)); e.Handled = true; }
            // V12.1101E [PH5-COLLIDE-01]: Panic hotkey routes through lifecycle-safe flatten pipeline.
            else if (e.Key == Key.F) { FlattenAll(); e.Handled = true; }

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
                if (e.Key == Key.M) { Enqueue(ctx => ctx.ExecuteRunnerAction("market")); e.Handled = true; }
                else if (e.Key == Key.O) { Enqueue(ctx => ctx.ExecuteRunnerAction("stop1pt")); e.Handled = true; }
                else if (e.Key == Key.W) { Enqueue(ctx => ctx.ExecuteRunnerAction("stop2pt")); e.Handled = true; }
                else if (e.Key == Key.B) { Enqueue(ctx => ctx.ExecuteRunnerAction("stopbe")); e.Handled = true; }
                else if (e.Key == Key.P) { Enqueue(ctx => ctx.ExecuteRunnerAction("lock50")); e.Handled = true; }  // P for Profit
                else if (e.Key == Key.D) { Enqueue(ctx => ctx.ExecuteRunnerAction("disabletrail")); e.Handled = true; }
            }

            // RMA uses Shift+Click (R conflicts with NT search, Ctrl conflicts with chart drag)
        }

        #endregion

        #region Target & Runner Actions

        #region Primary Actions

        // v5.12: Execute target actions (T1..T5)
        private void ExecuteTargetAction(string targetType, string action)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print(string.Format("{0} ACTION: No active positions", targetType));
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
                        Print(string.Format("{0} ACTION: Position {1} not filled yet", targetType, entryName));
                        continue;
                    }

                    if (!ExecuteTarget_ValidateContext(pos, entryName, targetType, out int targetNumber, out var targetOrders, out int targetContracts))
                        continue;

                    if (IsRunnerTarget(targetNumber) && action != "market" && action != "cancel")
                    {
                        Print(string.Format("{0} ACTION: Target is configured as Runner (trail-only), action {1} skipped for {2}",
                            targetType, action, entryName));
                        continue;
                    }

                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                    switch (action)
                    {
                        case "market":
                            ExecuteTarget_Market(entryName, pos, targetType, targetOrders, targetContracts);
                            break;

                        case "1point":
                            ExecuteTarget_OnePoint(entryName, pos, targetType, targetContracts);
                            break;

                        case "2point":
                            ExecuteTarget_TwoPoint(entryName, pos, targetType, targetContracts);
                            break;

                        case "marketprice":
                            ExecuteTarget_MarketPrice(entryName, pos, targetType, targetContracts, currentPrice);
                            break;

                        case "breakeven":
                            ExecuteTarget_Breakeven(entryName, pos, targetType, targetContracts);
                            break;

                        case "cancel":
                            ExecuteTarget_Cancel(entryName, pos, targetType, targetOrders, targetContracts);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("ERROR ExecuteTargetAction ({0}, {1}): {2}", targetType, action, ex.Message));
            }
        }

        private bool ExecuteTarget_ValidateContext(
            PositionInfo pos,
            string entryName,
            string targetType,
            out int targetNumber,
            out ConcurrentDictionary<string, Order> targetOrders,
            out int targetContracts)
        {
            targetNumber = 0;
            targetOrders = null;
            targetContracts = 0;

            if (!TryResolveTargetContext(pos, targetType, out targetNumber, out targetOrders, out targetContracts, out bool targetFilled))
            {
                Print(string.Format("{0} ACTION: Invalid target identifier", targetType));
                return false;
            }

            if (targetContracts <= 0)
            {
                Print(string.Format("{0} ACTION: No contracts assigned for {1}", targetType, entryName));
                return false;
            }

            if (targetFilled)
            {
                Print(string.Format("{0} ACTION: {1} already filled for {2}", targetType, targetType, entryName));
                return false;
            }

            return true;
        }

        private void ExecuteTarget_Market(
            string entryName,
            PositionInfo pos,
            string targetType,
            ConcurrentDictionary<string, Order> targetOrders,
            int targetContracts)
        {
            // Fill target at market NOW
            // V8.30: Thread-safe removal
            if (targetOrders.TryGetValue(entryName, out var existingOrder))
            {
                if (existingOrder != null && !IsOrderTerminal(existingOrder.OrderState))
                    CancelOrderSafe(existingOrder, pos);
                else
                    targetOrders.TryRemove(entryName, out _);
            }

            Order marketOrder = SubmitExitOrderForPosition(
                pos, targetContracts, OrderType.Market, 0, targetType + "_Market_" + entryName);

            if (marketOrder != null)
                Print(string.Format("? {0} MARKET FILL: {1} - Closing {2} contracts at market", targetType, entryName, targetContracts));
            else
                Print(string.Format("ERROR {0} MARKET FILL FAILED: {1} - Could not close {2} contracts", targetType, entryName, targetContracts));
        }

        private void ExecuteTarget_OnePoint(string entryName, PositionInfo pos, string targetType, int targetContracts)
        {
            // V8.18: Absolute profit target (Entry + 1 point)
            double newPrice1pt = pos.Direction == MarketPosition.Long
                ? pos.EntryPrice + 1.0
                : pos.EntryPrice - 1.0;
            newPrice1pt = Instrument.MasterInstrument.RoundToTickSize(newPrice1pt);

            Print(string.Format("? {0} -> 1 POINT PROFIT: {1} - New target @ {2:F2} (Entry was {3:F2})",
                targetType, entryName, newPrice1pt, pos.EntryPrice));

            MoveTargetOrder(entryName, pos, targetType, newPrice1pt, targetContracts);
        }

        private void ExecuteTarget_TwoPoint(string entryName, PositionInfo pos, string targetType, int targetContracts)
        {
            // V8.18: Absolute profit target (Entry + 2 points)
            double newPrice2pt = pos.Direction == MarketPosition.Long
                ? pos.EntryPrice + 2.0
                : pos.EntryPrice - 2.0;
            newPrice2pt = Instrument.MasterInstrument.RoundToTickSize(newPrice2pt);

            Print(string.Format("? {0} -> 2 POINTS PROFIT: {1} - New target @ {2:F2} (Entry was {3:F2})",
                targetType, entryName, newPrice2pt, pos.EntryPrice));

            MoveTargetOrder(entryName, pos, targetType, newPrice2pt, targetContracts);
        }

        private void ExecuteTarget_MarketPrice(
            string entryName,
            PositionInfo pos,
            string targetType,
            int targetContracts,
            double currentPrice)
        {
            // Move target to current market price (instant fill)
            double marketPrice = Instrument.MasterInstrument.RoundToTickSize(currentPrice);
            MoveTargetOrder(entryName, pos, targetType, marketPrice, targetContracts);
            Print(string.Format("? {0} -> MARKET PRICE: {1} - New target @ {2:F2}", targetType, entryName, marketPrice));
        }

        private void ExecuteTarget_Breakeven(string entryName, PositionInfo pos, string targetType, int targetContracts)
        {
            // Move target to breakeven (entry price)
            MoveTargetOrder(entryName, pos, targetType, pos.EntryPrice, targetContracts);
            Print(string.Format("? {0} -> BREAKEVEN: {1} - New target @ {2:F2}", targetType, entryName, pos.EntryPrice));
        }

        private void ExecuteTarget_Cancel(
            string entryName,
            PositionInfo pos,
            string targetType,
            ConcurrentDictionary<string, Order> targetOrders,
            int targetContracts)
        {
            // Cancel target order - let contracts run
            // V8.30: Thread-safe removal
            if (targetOrders.TryGetValue(entryName, out var cancelOrder))
            {
                if (cancelOrder != null && !IsOrderTerminal(cancelOrder.OrderState))
                {
                    CancelOrderSafe(cancelOrder, pos);
                    Print(string.Format("? {0} CANCELLED: {1} - {2} contracts will run with stop", targetType, entryName, targetContracts));
                }
                else
                    targetOrders.TryRemove(entryName, out _);
            }
        }

        private void MoveTargetOrder(string entryName, PositionInfo pos, string targetType, double newPrice, int quantity)
        {
            if (!MoveTargetOrder_Validate(targetType, quantity, out int targetNumber, out ConcurrentDictionary<string, Order> targetOrders))
                return;

            Order existingTarget;
            if (targetOrders.TryGetValue(entryName, out existingTarget) && existingTarget != null)
            {
                if (MoveTargetOrder_PrepareFollowerReplace(entryName, pos, targetNumber, newPrice, quantity, existingTarget))
                    return;

                MoveTargetOrder_CancelExisting(entryName, pos, targetOrders, existingTarget);
            }

            MoveTargetOrder_SubmitReplacement(entryName, pos, targetType, newPrice, quantity, targetOrders);
        }

        private bool MoveTargetOrder_Validate(
            string targetType,
            int quantity,
            out int targetNumber,
            out ConcurrentDictionary<string, Order> targetOrders)
        {
            targetNumber = 0;
            targetOrders = null;

            if (!TryParseTargetNumber(targetType, out targetNumber))
                return false;

            // Runner targets are trail-only: do not submit limit orders.
            if (IsRunnerTarget(targetNumber))
            {
                Print(string.Format("MoveTargetOrder SKIPPED: {0} is configured as Runner (trail-only)", targetType));
                return false;
            }

            if (quantity <= 0) return false;

            targetOrders = GetTargetOrdersDictionary(targetNumber);
            return targetOrders != null;
        }

        private bool MoveTargetOrder_PrepareFollowerReplace(
            string entryName,
            PositionInfo pos,
            int targetNumber,
            double newPrice,
            int quantity,
            Order existingTarget)
        {
            if (IsOrderTerminal(existingTarget.OrderState))
                return false;

            if (pos == null || !pos.IsFollower || pos.ExecutingAccount == null)
                return false;

            OrderAction exitAct = pos.Direction == MarketPosition.Long
                ? OrderAction.Sell : OrderAction.BuyToCover;
            string targetOrderName = "T" + targetNumber + "_" + entryName;
            var tSpec = new FollowerTargetReplaceSpec
            {
                EntryName = entryName,
                TargetNum = targetNumber,
                NewTargetPrice = newPrice,
                Quantity = quantity,
                ExitAction = exitAct,
                TargetAccount = pos.ExecutingAccount,
                CancellingOrderId = existingTarget.OrderId
            };
            _followerTargetReplaceSpecs[targetOrderName] = tSpec;
            StampReaperMoveGrace();
            pos.ExecutingAccount.Cancel(new[] { existingTarget });
            Print(string.Format("[UI_TGT] Follower target replace queued: T{0} {1} on {2} -> {3:F2}",
                targetNumber, entryName, pos.ExecutingAccount.Name, newPrice));
            return true;
        }

        private void MoveTargetOrder_CancelExisting(
            string entryName,
            PositionInfo pos,
            ConcurrentDictionary<string, Order> targetOrders,
            Order existingTarget)
        {
            if (IsOrderTerminal(existingTarget.OrderState))
            {
                targetOrders.TryRemove(entryName, out _);
            }
            else if (targetOrders.TryRemove(entryName, out existingTarget))
            {
                CancelOrderSafe(existingTarget, pos);
            }
        }

        private void MoveTargetOrder_SubmitReplacement(
            string entryName,
            PositionInfo pos,
            string targetType,
            double newPrice,
            int quantity,
            ConcurrentDictionary<string, Order> targetOrders)
        {
            // Submit new target order at new price
            Order newTargetOrder = SubmitExitOrderForPosition(pos, quantity, OrderType.Limit, newPrice, targetType + "_" + entryName);

            if (newTargetOrder != null)
            {
                targetOrders[entryName] = newTargetOrder;
            }
        }

        private Order SubmitExitOrderForPosition(PositionInfo pos, int quantity, OrderType orderType, double limitPrice, string signalName)
        {
            if (pos == null || quantity <= 0) return null;

            OrderAction exitAction = pos.Direction == MarketPosition.Long
                ? OrderAction.Sell : OrderAction.BuyToCover;
            double limit = orderType == OrderType.Limit ? limitPrice : 0;

            if (pos.IsFollower && pos.ExecutingAccount != null)
            {
                Order exitOrder = pos.ExecutingAccount.CreateOrder(
                    Instrument, exitAction, orderType, TimeInForce.Gtc,
                    quantity, limit, 0, "", signalName, null);
                if (exitOrder == null)
                    return null;

                pos.ExecutingAccount.Submit(new[] { exitOrder });
                return exitOrder;
            }

            return SubmitOrderUnmanaged(0, exitAction, orderType, quantity, limit, 0, "", signalName);
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

        private ConcurrentDictionary<string, Order> GetTargetOrdersDictionary(int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return target1Orders;
                case 2: return target2Orders;
                case 3: return target3Orders;
                case 4: return target4Orders;
                case 5: return target5Orders;
                default: return null;
            }
        }

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
                        Print(string.Format("RUNNER ACTION: Position {0} not filled yet", entryName));
                        continue;
                    }

                    // Calculate runner contracts (remaining after T1 and T2)
                    int runnerContracts = pos.RemainingContracts;
                    if (runnerContracts <= 0)
                    {
                        Print(string.Format("RUNNER ACTION: No runner contracts for {0}", entryName));
                        continue;
                    }

                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                    switch (action)
                    {
                        case "market":
                            ExecuteRunner_Market(entryName, pos, runnerContracts);
                            break;

                        case "stop1pt":
                            ExecuteRunner_StopOnePoint(entryName, pos);
                            break;

                        case "stop2pt":
                            ExecuteRunner_StopTwoPoint(entryName, pos);
                            break;

                        case "stopbe":
                            ExecuteRunner_Breakeven(entryName, pos, currentPrice);
                            break;

                        case "lock50":
                            ExecuteRunner_Lock50(entryName, pos, currentPrice);
                            break;

                        case "disabletrail":
                            ExecuteRunner_DisableTrail(entryName, pos);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("ERROR ExecuteRunnerAction ({0}): {1}", action, ex.Message));
            }
        }

        private void ExecuteRunner_Market(string entryName, PositionInfo pos, int runnerContracts)
        {
            // Close runner at market
            Order runnerMarketOrder = SubmitExitOrderForPosition(
                pos, runnerContracts, OrderType.Market, 0, "Runner_Market_" + entryName);

            if (runnerMarketOrder != null)
                Print(string.Format("? RUNNER MARKET CLOSE: {0} - Closing {1} contracts at market", entryName, runnerContracts));
            else
                Print(string.Format("ERROR RUNNER MARKET CLOSE FAILED: {0} - Could not close {1} contracts", entryName, runnerContracts));
        }

        private void ExecuteRunner_StopOnePoint(string entryName, PositionInfo pos)
        {
            // V8.19: Absolute profit lock (Entry + 1 point)
            double newStop1pt = pos.Direction == MarketPosition.Long
                ? pos.EntryPrice + 1.0
                : pos.EntryPrice - 1.0;
            newStop1pt = Instrument.MasterInstrument.RoundToTickSize(newStop1pt);

            // Safety: Only move if it's better than current stop or entry-relative profit-lock
            UpdateStopOrder(entryName, pos, newStop1pt, pos.CurrentTrailLevel);
            Print(string.Format("? RUNNER STOP -> 1 PT PROFIT LOCK: {0} - Stop @ {1:F2} (Entry was {2:F2})", entryName, newStop1pt, pos.EntryPrice));
        }

        private void ExecuteRunner_StopTwoPoint(string entryName, PositionInfo pos)
        {
            // V8.19: Absolute profit lock (Entry + 2 points)
            double newStop2pt = pos.Direction == MarketPosition.Long
                ? pos.EntryPrice + 2.0
                : pos.EntryPrice - 2.0;
            newStop2pt = Instrument.MasterInstrument.RoundToTickSize(newStop2pt);

            UpdateStopOrder(entryName, pos, newStop2pt, pos.CurrentTrailLevel);
            Print(string.Format("? RUNNER STOP -> 2 PT PROFIT LOCK: {0} - Stop @ {1:F2} (Entry was {2:F2})", entryName, newStop2pt, pos.EntryPrice));
        }

        private void ExecuteRunner_Breakeven(string entryName, PositionInfo pos, double currentPrice)
        {
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
                Print(string.Format("? BE SHIELD: {0} price {1:F2} not at BE level {2:F2} -- armed for auto-trigger",
                    entryName, currentPrice, beStopTarget));
                return;
            }
            UpdateStopOrder(entryName, pos, beStopTarget, 1);
            // [Build 1102K] Mark triggered so ManageTrailingStops armed path does not re-fire.
            pos.ManualBreakevenTriggered = true;
            Print(string.Format("? RUNNER STOP -> BREAKEVEN: {0} - Stop @ {1:F2} (Entry +/- {2} ticks)",
                entryName, beStopTarget, BreakEvenOffsetTicks));
        }

        private void ExecuteRunner_Lock50(string entryName, PositionInfo pos, double currentPrice)
        {
            // Lock 50% of current profit
            double unrealizedProfit = pos.Direction == MarketPosition.Long
                ? currentPrice - pos.EntryPrice
                : pos.EntryPrice - currentPrice;
            double lock50Stop = pos.Direction == MarketPosition.Long
                ? pos.EntryPrice + (unrealizedProfit * 0.5)
                : pos.EntryPrice - (unrealizedProfit * 0.5);
            lock50Stop = Instrument.MasterInstrument.RoundToTickSize(lock50Stop);
            UpdateStopOrder(entryName, pos, lock50Stop, pos.CurrentTrailLevel);
            Print(string.Format("? RUNNER LOCK 50%: {0} - Stop @ {1:F2} (profit: {2:F2})", entryName, lock50Stop, unrealizedProfit));
        }

        private void ExecuteRunner_DisableTrail(string entryName, PositionInfo pos)
        {
            // Disable trailing - keep stop where it is
            pos.CurrentTrailLevel = 999; // Set to high number to prevent further trailing
            Print(string.Format("? RUNNER TRAILING DISABLED: {0} - Stop fixed @ {1:F2}", entryName, pos.CurrentStopPrice));
        }
        #endregion

        #endregion
    }
}
