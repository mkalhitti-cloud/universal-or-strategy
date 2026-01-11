using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class UniversalORStrategyV4 : Strategy
    {
        #region Variables
        
        // OR tracking
        private double sessionHigh;
        private double sessionLow;
        private double sessionMid;
        private double sessionRange;
        private bool isInORWindow;
        private bool orComplete;
        private DateTime orStartDateTime;
        private DateTime orEndDateTime;
        private DateTime sessionStartDateTime;
        private DateTime lastResetDate;
        private int orStartBarIndex;
        private int orEndBarIndex;
        
        // Instrument info
        private double tickSize;
        private double pointValue;
        private int minContracts;
        private int maxContracts;
        
        // Position tracking - multi-target system
        private Dictionary<string, PositionInfo> activePositions;
        private Dictionary<string, Order> entryOrders;
        private Dictionary<string, Order> stopOrders;
        private Dictionary<string, Order> target1Orders;
        private Dictionary<string, Order> target2Orders;
        
        // UI Components
        private Border mainBorder;
        private System.Windows.Controls.Grid mainGrid;
        private TextBlock statusTextBlock;
        private TextBlock orInfoBlock;
        private TextBlock positionSummaryBlock;
        private Button longButton;
        private Button shortButton;
        private Button flattenButton;
        private bool uiCreated;
        
        // Drag support
        private bool isDragging;
        private Point dragStartPoint;
        private Thickness originalMargin;
        
        // RAM optimization - StringBuilder pool
        private StringBuilder sbPool;
        
        #endregion

        #region Position Info Class

        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;
            public int T2Contracts;
            public int T3Contracts;
            public int RemainingContracts;
            public double EntryPrice;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price;
            public double Target2Price;
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
        }

        #endregion

        #region Enums

        public enum ORTimeframeType
        {
            Minutes_1 = 1,
            Minutes_5 = 5,
            Minutes_10 = 10,
            Minutes_15 = 15
        }

        #endregion

        #region Properties - Session Settings

        [NinjaScriptProperty]
        [Display(Name = "Session Start", Description = "Trading session start time (OR begins here)", Order = 1, GroupName = "1. Session Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime SessionStart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Session End", Description = "Trading session end time (box ends here)", Order = 2, GroupName = "1. Session Settings")]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        public DateTime SessionEnd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "OR Timeframe", Description = "Duration of Opening Range window", Order = 3, GroupName = "1. Session Settings")]
        public ORTimeframeType ORTimeframe { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Time Zone", Description = "Time zone for session times", Order = 4, GroupName = "1. Session Settings")]
        [TypeConverter(typeof(TimeZoneConverter))]
        public string SelectedTimeZone { get; set; }

        #endregion

        #region Properties - Risk Management

        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade ($)", Description = "Maximum dollar risk per trade (when stop â‰¤ threshold)", Order = 1, GroupName = "2. Risk Management")]
        public double RiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reduced Risk ($)", Description = "Reduced risk when stop > threshold", Order = 2, GroupName = "2. Risk Management")]
        public double ReducedRiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Threshold (Points)", Description = "Stop distance above which reduced risk is used", Order = 3, GroupName = "2. Risk Management")]
        public double StopThresholdPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Min Contracts", Order = 4, GroupName = "2. Risk Management")]
        public int MESMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Max Contracts", Order = 5, GroupName = "2. Risk Management")]
        public int MESMaximum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Min Contracts", Order = 6, GroupName = "2. Risk Management")]
        public int MGCMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Max Contracts", Order = 7, GroupName = "2. Risk Management")]
        public int MGCMaximum { get; set; }

        #endregion

        #region Properties - Stop Loss

        [NinjaScriptProperty]
        [Display(Name = "Stop Multiplier", Description = "Multiplier of OR Range for stop (0.5 = half OR)", Order = 1, GroupName = "3. Stop Loss")]
        public double StopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Stop (Points)", Order = 2, GroupName = "3. Stop Loss")]
        public double MinimumStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (Points)", Order = 3, GroupName = "3. Stop Loss")]
        public double MaximumStop { get; set; }

        #endregion

        #region Properties - Profit Targets

        [NinjaScriptProperty]
        [Display(Name = "Target 1 (Points)", Description = "First profit target - fixed points", Order = 1, GroupName = "4. Profit Targets")]
        public double Target1Points { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 2 Multiplier", Description = "Multiplier of OR range for T2", Order = 2, GroupName = "4. Profit Targets")]
        public double Target2Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Order = 3, GroupName = "4. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Order = 4, GroupName = "4. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Order = 5, GroupName = "4. Profit Targets")]
        public int T3ContractPercent { get; set; }

        #endregion

        #region Properties - Trailing Stops

        [NinjaScriptProperty]
        [Display(Name = "BE Trigger (Points)", Order = 1, GroupName = "5. Trailing Stops")]
        public double BreakEvenTriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BE Offset (Ticks)", Order = 2, GroupName = "5. Trailing Stops")]
        public int BreakEvenOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Trigger (Points)", Order = 3, GroupName = "5. Trailing Stops")]
        public double Trail1TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Distance (Points)", Order = 4, GroupName = "5. Trailing Stops")]
        public double Trail1DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Trigger (Points)", Order = 5, GroupName = "5. Trailing Stops")]
        public double Trail2TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Distance (Points)", Order = 6, GroupName = "5. Trailing Stops")]
        public double Trail2DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Trigger (Points)", Order = 7, GroupName = "5. Trailing Stops")]
        public double Trail3TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Distance (Points)", Order = 8, GroupName = "5. Trailing Stops")]
        public double Trail3DistancePoints { get; set; }

        #endregion

        #region Properties - Display

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Line", Description = "Show middle line in OR box", Order = 1, GroupName = "6. Display")]
        public bool ShowMidLine { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Box Opacity (%)", Description = "Transparency of OR box (0-100)", Order = 2, GroupName = "6. Display")]
        [Range(0, 100)]
        public int BoxOpacity { get; set; }

        #endregion

        #region Time Zone Converter

        public class TimeZoneConverter : TypeConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(new[] { "Eastern", "Central", "Mountain", "Pacific", "UTC" });
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                return value is string str ? str : base.ConvertFrom(context, culture, value);
            }
        }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Universal Opening Range Strategy v4.0 - Box Visualization";
                Name = "UniversalORStrategyV4";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 10;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.ImmediatelySubmit;
                TimeInForce = TimeInForce.Gtc;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                IsUnmanaged = true;

                // Session defaults (NY Open)
                SessionStart = DateTime.Parse("09:30");
                SessionEnd = DateTime.Parse("16:00");
                ORTimeframe = ORTimeframeType.Minutes_5;
                SelectedTimeZone = "Eastern";

                // Risk defaults
                RiskPerTrade = 400;
                ReducedRiskPerTrade = 160;
                StopThresholdPoints = 5.0;
                MESMinimum = 6;
                MESMaximum = 30;
                MGCMinimum = 3;
                MGCMaximum = 15;

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 4.0;
                MaximumStop = 8.0;

                // Target defaults
                Target1Points = 2.0;
                Target2Multiplier = 0.5;
                T1ContractPercent = 33;
                T2ContractPercent = 33;
                T3ContractPercent = 34;

                // Trailing stop defaults
                BreakEvenTriggerPoints = 2.0;
                BreakEvenOffsetTicks = 1;
                Trail1TriggerPoints = 3.0;
                Trail1DistancePoints = 2.0;
                Trail2TriggerPoints = 4.0;
                Trail2DistancePoints = 1.5;
                Trail3TriggerPoints = 5.0;
                Trail3DistancePoints = 1.0;

                // Display
                ShowMidLine = true;
                BoxOpacity = 20;
            }
            else if (State == State.Configure)
            {
                // Initialize collections once
                activePositions = new Dictionary<string, PositionInfo>(4);
                entryOrders = new Dictionary<string, Order>(4);
                stopOrders = new Dictionary<string, Order>(4);
                target1Orders = new Dictionary<string, Order>(4);
                target2Orders = new Dictionary<string, Order>(4);
                sbPool = new StringBuilder(256);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;

                string symbol = Instrument.MasterInstrument.Name;
                if (symbol.Contains("MES"))
                {
                    minContracts = MESMinimum;
                    maxContracts = MESMaximum;
                }
                else if (symbol.Contains("MGC"))
                {
                    minContracts = MGCMinimum;
                    maxContracts = MGCMaximum;
                }
                else
                {
                    minContracts = 3;
                    maxContracts = 15;
                }

                ResetOR();

                Print(FormatString("UniversalORStrategy v4.0 BOX | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(FormatString("Session: {0} - {1} {2} | OR: {3} min", 
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
            }
            else if (State == State.Historical)
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.InvokeAsync(CreateUI);
            }
            else if (State == State.Realtime)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        AttachHotkeys();
                        UpdateDisplayInternal();
                        Print("REALTIME - Hotkeys: L=Long, S=Short, F=Flatten");
                    });
                }
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        DetachHotkeys();
                        RemoveUI();
                    });
                }
                
                // Clear references
                activePositions?.Clear();
                entryOrders?.Clear();
                stopOrders?.Clear();
                target1Orders?.Clear();
                target2Orders?.Clear();
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 5) return;

            try
            {
                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;
                
                // Calculate OR end time based on session start + timeframe
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                // New day reset
                if (barTimeInZone.Date != lastResetDate)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print(FormatString("New day: {0} - OR Reset", barTimeInZone.Date.ToShortDateString()));
                }

                // Build OR during window (from session start to session start + OR timeframe)
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    isInORWindow = true;
                    sessionHigh = Math.Max(sessionHigh, High[0]);
                    sessionLow = Math.Min(sessionLow, Low[0]);
                    sessionRange = sessionHigh - sessionLow;
                    sessionMid = (sessionHigh + sessionLow) / 2.0;

                    if (orStartDateTime == DateTime.MinValue)
                    {
                        orStartDateTime = Time[0];
                        sessionStartDateTime = Time[0];
                        orStartBarIndex = CurrentBar;
                        Print(FormatString("OR Window started - {0}, Bar: {1}", Time[0], CurrentBar));
                    }
                }
                
                // Mark OR complete when timeframe ends
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(FormatString("OR COMPLETE: H={0:F2} L={1:F2} M={2:F2} R={3:F2}",
                        sessionHigh, sessionLow, sessionMid, sessionRange));

                    DrawORBox();
                }

                // Update box if OR complete (extends to current bar until session end)
                if (orComplete && sessionHigh != double.MinValue && currentTime <= sessionEndTime)
                {
                    DrawORBox();
                }

                // Position sync check
                SyncPositionState();

                // Manage trailing stops
                if (activePositions.Count > 0)
                    ManageTrailingStops();

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR OnBarUpdate: " + ex.Message);
            }
        }

        #endregion

        #region Drawing - Box Instead of Rays

        private void DrawORBox()
        {
            if (sessionHigh == double.MinValue || sessionLow == double.MaxValue) return;
            if (orStartBarIndex == 0) return;

            try
            {
                // Calculate bar positions
                int startBarsAgo = CurrentBar - orStartBarIndex;
                if (startBarsAgo < 0) startBarsAgo = 0;

                // Calculate opacity for brush
                byte alpha = (byte)(255 * BoxOpacity / 100);
                
                // Create semi-transparent fill brush
                Brush fillBrush = new SolidColorBrush(Color.FromArgb(alpha, 70, 130, 180));  // Steel blue
                fillBrush.Freeze();  // Optimize for performance
                
                // Draw single rectangle from OR start to current bar
                // This replaces 3+ ray lines with 1 rectangle
                Draw.Rectangle(this, "ORBox", false, startBarsAgo, sessionHigh, 0, sessionLow, 
                    Brushes.DodgerBlue, fillBrush, 1);

                // Optional mid line (single line, not a ray)
                if (ShowMidLine)
                {
                    Draw.Line(this, "ORMid", false, startBarsAgo, sessionMid, 0, sessionMid, 
                        Brushes.Yellow, DashStyleHelper.Dash, 1);
                }

                // Single text label at current bar (not multiple labels)
                string labelText = FormatString("OR: {0:F2} - {1:F2} (R:{2:F2})", sessionHigh, sessionLow, sessionRange);
                Draw.Text(this, "ORLabel", labelText, 0, sessionHigh + (tickSize * 4), Brushes.White);
            }
            catch (Exception ex)
            {
                Print("ERROR DrawORBox: " + ex.Message);
            }
        }

        private void ResetOR()
        {
            sessionHigh = double.MinValue;
            sessionLow = double.MaxValue;
            sessionMid = 0;
            sessionRange = 0;
            isInORWindow = false;
            orComplete = false;
            orStartDateTime = DateTime.MinValue;
            orEndDateTime = DateTime.MinValue;
            sessionStartDateTime = DateTime.MinValue;
            orStartBarIndex = 0;
            orEndBarIndex = 0;

            // Remove draw objects (only 3 now instead of 6+)
            RemoveDrawObject("ORBox");
            RemoveDrawObject("ORMid");
            RemoveDrawObject("ORLabel");
        }

        #endregion

        #region Position Sync

        private void SyncPositionState()
        {
            if (activePositions.Count > 0 && Position.MarketPosition == MarketPosition.Flat)
            {
                Print("SYNC: Position flat but tracking active - cleaning up");
                
                foreach (var pos in activePositions.Values)
                {
                    CancelOrderByName("Stop_" + pos.SignalName);
                    CancelOrderByName("T1_" + pos.SignalName);
                    CancelOrderByName("T2_" + pos.SignalName);
                }
                
                activePositions.Clear();
                entryOrders.Clear();
                stopOrders.Clear();
                target1Orders.Clear();
                target2Orders.Clear();
                
                Print("SYNC: Internal state cleared");
            }
        }

        #endregion

        #region Stop Validation

        private bool SubmitValidatedStop(string stopName, PositionInfo pos)
        {
            bool isLong = pos.Direction == MarketPosition.Long;
            double currentPrice = Close[0];
            double stopPrice = pos.CurrentStopPrice;
            int qty = pos.RemainingContracts;
            
            if (qty <= 0) return false;
            
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                Print(FormatString("No position - skipping stop for {0}", stopName));
                return false;
            }
            
            if ((isLong && Position.MarketPosition != MarketPosition.Long) ||
                (!isLong && Position.MarketPosition != MarketPosition.Short))
            {
                Print(FormatString("Position mismatch - Expected {0}, Actual {1}", 
                    isLong ? "Long" : "Short", Position.MarketPosition));
                return false;
            }
            
            double minBuffer = 2 * tickSize;
            
            if (isLong)
            {
                if (stopPrice >= currentPrice - minBuffer)
                {
                    Print(FormatString("STOP INVALID: {0:F2} >= Market {1:F2} - EXIT MARKET", stopPrice, currentPrice));
                    CancelOrderByName("T1_" + pos.SignalName);
                    CancelOrderByName("T2_" + pos.SignalName);
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", "Exit_" + pos.SignalName);
                    return false;
                }
                SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, qty, 0, stopPrice, "", stopName);
                return true;
            }
            else
            {
                if (stopPrice <= currentPrice + minBuffer)
                {
                    Print(FormatString("STOP INVALID: {0:F2} <= Market {1:F2} - EXIT MARKET", stopPrice, currentPrice));
                    CancelOrderByName("T1_" + pos.SignalName);
                    CancelOrderByName("T2_" + pos.SignalName);
                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", "Exit_" + pos.SignalName);
                    return false;
                }
                SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, qty, 0, stopPrice, "", stopName);
                return true;
            }
        }

        #endregion

        #region OnPositionUpdate

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                Print(FormatString("POSITION: {0} | Qty: {1} | Avg: {2:F2}", marketPosition, quantity, averagePrice));
                
                if (marketPosition == MarketPosition.Flat && activePositions.Count > 0)
                {
                    Print("EXTERNAL CLOSE - Cleaning up");
                    
                    foreach (var pos in activePositions.Values)
                    {
                        CancelOrderByName("Stop_" + pos.SignalName);
                        CancelOrderByName("T1_" + pos.SignalName);
                        CancelOrderByName("T2_" + pos.SignalName);
                    }
                    
                    activePositions.Clear();
                    entryOrders.Clear();
                    stopOrders.Clear();
                    target1Orders.Clear();
                    target2Orders.Clear();
                    
                    UpdateDisplay();
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
            }
        }

        #endregion

        #region OnOrderUpdate

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            try
            {
                string orderName = order.Name;
                
                if (orderState == OrderState.Working)
                {
                    if (orderName.StartsWith("Stop_"))
                        stopOrders[orderName] = order;
                    else if (orderName.StartsWith("T1_"))
                        target1Orders[orderName] = order;
                    else if (orderName.StartsWith("T2_"))
                        target2Orders[orderName] = order;
                    else if (orderName.StartsWith("Long_") || orderName.StartsWith("Short_"))
                        entryOrders[orderName] = order;
                        
                    Print(FormatString("WORKING: {0} @ {1:F2}", orderName, stopPrice > 0 ? stopPrice : limitPrice));
                }
                else if (orderState == OrderState.Filled)
                {
                    Print(FormatString("FILLED: {0} @ {1:F2} x{2}", orderName, averageFillPrice, filled));
                    
                    if (orderName.StartsWith("Long_") || orderName.StartsWith("Short_"))
                        HandleEntryFill(orderName, averageFillPrice);
                    else if (orderName.StartsWith("T1_"))
                        HandleTarget1Fill(orderName, filled);
                    else if (orderName.StartsWith("T2_"))
                        HandleTarget2Fill(orderName, filled);
                    else if (orderName.StartsWith("Stop_") || orderName.StartsWith("Exit_") || orderName.StartsWith("Flatten_"))
                        HandleStopFill(orderName);
                }
                else if (orderState == OrderState.Cancelled)
                {
                    Print(FormatString("CANCELLED: {0}", orderName));
                    CleanupOrder(orderName);
                }
                else if (orderState == OrderState.Rejected)
                {
                    Print(FormatString("REJECTED: {0} - {1}", orderName, nativeError));
                    CleanupOrder(orderName);
                    
                    if (orderName.StartsWith("Stop_"))
                        HandleStopRejection(orderName);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        private void HandleEntryFill(string entryName, double fillPrice)
        {
            if (!activePositions.ContainsKey(entryName)) return;
            
            PositionInfo pos = activePositions[entryName];
            if (pos.BracketSubmitted) return;
            
            pos.BracketSubmitted = true;
            pos.EntryFilled = true;
            pos.EntryPrice = fillPrice;
            pos.ExtremePriceSinceEntry = fillPrice;
            
            bool isLong = pos.Direction == MarketPosition.Long;
            
            if (isLong)
            {
                pos.Target1Price = fillPrice + Target1Points;
                pos.Target2Price = fillPrice + (sessionRange * Target2Multiplier);
            }
            else
            {
                pos.Target1Price = fillPrice - Target1Points;
                pos.Target2Price = fillPrice - (sessionRange * Target2Multiplier);
            }
            
            SubmitValidatedStop("Stop_" + entryName, pos);
            
            if (pos.T1Contracts > 0)
            {
                if (isLong)
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName);
                else
                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName);
            }
            
            if (pos.T2Contracts > 0)
            {
                if (isLong)
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName);
                else
                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName);
            }
            
            Print(FormatString("BRACKETS: Stop@{0:F2} | T1@{1:F2}({2}) | T2@{3:F2}({4}) | T3=Trail({5})",
                pos.CurrentStopPrice, pos.Target1Price, pos.T1Contracts, pos.Target2Price, pos.T2Contracts, pos.T3Contracts));
        }

        private void HandleTarget1Fill(string t1Name, int filledQty)
        {
            string entryName = t1Name.Replace("T1_", "");
            if (!activePositions.ContainsKey(entryName)) return;
            
            PositionInfo pos = activePositions[entryName];
            pos.T1Filled = true;
            pos.RemainingContracts -= filledQty;
            
            Print(FormatString("T1 HIT: {0} cts | Remaining: {1}", filledQty, pos.RemainingContracts));
            ModifyStopQuantity(entryName, pos);
            target1Orders.Remove(t1Name);
        }

        private void HandleTarget2Fill(string t2Name, int filledQty)
        {
            string entryName = t2Name.Replace("T2_", "");
            if (!activePositions.ContainsKey(entryName)) return;
            
            PositionInfo pos = activePositions[entryName];
            pos.T2Filled = true;
            pos.RemainingContracts -= filledQty;
            
            Print(FormatString("T2 HIT: {0} cts | Remaining: {1}", filledQty, pos.RemainingContracts));
            ModifyStopQuantity(entryName, pos);
            target2Orders.Remove(t2Name);
        }

        private void HandleStopFill(string stopName)
        {
            string entryName = stopName.Replace("Stop_", "").Replace("Exit_", "").Replace("Flatten_", "");
            
            CancelOrderByName("T1_" + entryName);
            CancelOrderByName("T2_" + entryName);
            
            stopOrders.Remove("Stop_" + entryName);
            target1Orders.Remove("T1_" + entryName);
            target2Orders.Remove("T2_" + entryName);
            activePositions.Remove(entryName);
            
            Print(FormatString("CLOSED: {0}", entryName));
        }

        private void HandleStopRejection(string stopName)
        {
            string entryName = stopName.Replace("Stop_", "");
            if (!activePositions.ContainsKey(entryName)) return;
            
            PositionInfo pos = activePositions[entryName];
            
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                Print("Stop rejected - attempting market exit");
                bool isLong = pos.Direction == MarketPosition.Long;
                if (isLong)
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Exit_" + entryName);
                else
                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Exit_" + entryName);
            }
            else
            {
                CancelOrderByName("T1_" + entryName);
                CancelOrderByName("T2_" + entryName);
                activePositions.Remove(entryName);
            }
        }

        private void ModifyStopQuantity(string entryName, PositionInfo pos)
        {
            if (pos.RemainingContracts <= 0)
            {
                CancelOrderByName("Stop_" + entryName);
                activePositions.Remove(entryName);
                Print("All targets hit - closed");
                return;
            }
            
            CancelOrderByName("Stop_" + entryName);
            SubmitValidatedStop("Stop_" + entryName, pos);
            Print(FormatString("Stop modified: {0} cts @ {1:F2}", pos.RemainingContracts, pos.CurrentStopPrice));
        }

        private void CancelOrderByName(string orderName)
        {
            foreach (Order o in Orders)
            {
                if (o.Name == orderName && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted || o.OrderState == OrderState.Submitted))
                {
                    CancelOrder(o);
                    break;
                }
            }
        }

        private void CleanupOrder(string orderName)
        {
            if (orderName.StartsWith("Long_") || orderName.StartsWith("Short_"))
            {
                activePositions.Remove(orderName);
                entryOrders.Remove(orderName);
            }
            else if (orderName.StartsWith("Stop_"))
                stopOrders.Remove(orderName);
            else if (orderName.StartsWith("T1_"))
                target1Orders.Remove(orderName);
            else if (orderName.StartsWith("T2_"))
                target2Orders.Remove(orderName);
        }

        #endregion

        #region Trailing Stop Management

        private void ManageTrailingStops()
        {
            List<string> keys = new List<string>(activePositions.Keys);
            
            foreach (string entryName in keys)
            {
                if (!activePositions.ContainsKey(entryName)) continue;
                
                PositionInfo pos = activePositions[entryName];
                if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;
                
                bool isLong = pos.Direction == MarketPosition.Long;
                double currentPrice = Close[0];
                
                if (isLong)
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, High[0]);
                else
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, Low[0]);
                
                double profitPoints = isLong 
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice 
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;
                
                int targetLevel = 0;
                double newStopPrice = pos.InitialStopPrice;
                
                if (profitPoints >= Trail3TriggerPoints)
                {
                    targetLevel = 4;
                    newStopPrice = isLong 
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints 
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;
                }
                else if (profitPoints >= Trail2TriggerPoints)
                {
                    targetLevel = 3;
                    newStopPrice = isLong 
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints 
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;
                }
                else if (profitPoints >= Trail1TriggerPoints)
                {
                    targetLevel = 2;
                    newStopPrice = isLong 
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints 
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;
                }
                else if (profitPoints >= BreakEvenTriggerPoints)
                {
                    targetLevel = 1;
                    newStopPrice = isLong 
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize) 
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);
                }
                
                // Buffer from market
                double minStopBuffer = 4 * tickSize;
                if (isLong && newStopPrice > currentPrice - minStopBuffer)
                    newStopPrice = currentPrice - minStopBuffer;
                else if (!isLong && newStopPrice < currentPrice + minStopBuffer)
                    newStopPrice = currentPrice + minStopBuffer;
                
                if (targetLevel > pos.CurrentTrailLevel)
                {
                    bool isBetterStop = isLong 
                        ? newStopPrice > pos.CurrentStopPrice 
                        : newStopPrice < pos.CurrentStopPrice;
                    
                    if (isBetterStop)
                    {
                        pos.CurrentTrailLevel = targetLevel;
                        pos.CurrentStopPrice = RoundToTickSize(newStopPrice);
                        
                        CancelOrderByName("Stop_" + entryName);
                        SubmitValidatedStop("Stop_" + entryName, pos);
                        
                        string levelName = targetLevel == 1 ? "BE" : "T" + (targetLevel - 1);
                        Print(FormatString("TRAIL [{0}]: +{1:F2}pts | Stop={2:F2} | Level={3}",
                            entryName, profitPoints, pos.CurrentStopPrice, levelName));
                    }
                }
            }
        }

        private double RoundToTickSize(double price)
        {
            return Math.Round(price / tickSize) * tickSize;
        }

        #endregion

        #region Time Zone Conversion

        private DateTime ConvertToSelectedTimeZone(DateTime barTime)
        {
            try
            {
                TimeZoneInfo targetZone;
                switch (SelectedTimeZone)
                {
                    case "Eastern":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                    case "Central":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        break;
                    case "Mountain":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        break;
                    case "Pacific":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        break;
                    case "UTC":
                        targetZone = TimeZoneInfo.Utc;
                        break;
                    default:
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                }
                return TimeZoneInfo.ConvertTime(barTime, TimeZoneInfo.Local, targetZone);
            }
            catch
            {
                return barTime;
            }
        }

        #endregion

        #region Entry Methods

        private void ExecuteLong()
        {
            if (!orComplete)
            {
                Print("OR not complete");
                return;
            }

            if (sessionRange <= 0)
            {
                Print("Invalid OR range");
                return;
            }

            double entryPrice = sessionHigh + (3 * tickSize);
            double stopDistance = CalculateStopDistance();
            double stopPrice = entryPrice - stopDistance;
            double target1Price = entryPrice + Target1Points;
            double target2Price = entryPrice + (sessionRange * Target2Multiplier);
            int totalContracts = CalculateContracts(stopDistance);

            if (totalContracts < minContracts)
            {
                Print(FormatString("Contracts {0} < min {1}", totalContracts, minContracts));
                return;
            }

            int t1Contracts, t2Contracts, t3Contracts;
            CalculateContractSplit(totalContracts, out t1Contracts, out t2Contracts, out t3Contracts);

            string signalName = "Long_" + DateTime.Now.Ticks;

            PositionInfo pos = new PositionInfo
            {
                SignalName = signalName,
                Direction = MarketPosition.Long,
                TotalContracts = totalContracts,
                T1Contracts = t1Contracts,
                T2Contracts = t2Contracts,
                T3Contracts = t3Contracts,
                RemainingContracts = totalContracts,
                EntryPrice = entryPrice,
                InitialStopPrice = stopPrice,
                CurrentStopPrice = stopPrice,
                Target1Price = target1Price,
                Target2Price = target2Price,
                ExtremePriceSinceEntry = entryPrice,
                CurrentTrailLevel = 0
            };
            activePositions[signalName] = pos;

            SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, totalContracts, 0, entryPrice, "", signalName);

            Print(FormatString("LONG: {0} cts @ {1:F2} | Stop: {2:F2} | T1: {3:F2} | T2: {4:F2}",
                totalContracts, entryPrice, stopPrice, target1Price, target2Price));
            UpdateDisplay();
        }

        private void ExecuteShort()
        {
            if (!orComplete)
            {
                Print("OR not complete");
                return;
            }

            if (sessionRange <= 0)
            {
                Print("Invalid OR range");
                return;
            }

            double entryPrice = sessionLow - (3 * tickSize);
            double stopDistance = CalculateStopDistance();
            double stopPrice = entryPrice + stopDistance;
            double target1Price = entryPrice - Target1Points;
            double target2Price = entryPrice - (sessionRange * Target2Multiplier);
            int totalContracts = CalculateContracts(stopDistance);

            if (totalContracts < minContracts)
            {
                Print(FormatString("Contracts {0} < min {1}", totalContracts, minContracts));
                return;
            }

            int t1Contracts, t2Contracts, t3Contracts;
            CalculateContractSplit(totalContracts, out t1Contracts, out t2Contracts, out t3Contracts);

            string signalName = "Short_" + DateTime.Now.Ticks;

            PositionInfo pos = new PositionInfo
            {
                SignalName = signalName,
                Direction = MarketPosition.Short,
                TotalContracts = totalContracts,
                T1Contracts = t1Contracts,
                T2Contracts = t2Contracts,
                T3Contracts = t3Contracts,
                RemainingContracts = totalContracts,
                EntryPrice = entryPrice,
                InitialStopPrice = stopPrice,
                CurrentStopPrice = stopPrice,
                Target1Price = target1Price,
                Target2Price = target2Price,
                ExtremePriceSinceEntry = entryPrice,
                CurrentTrailLevel = 0
            };
            activePositions[signalName] = pos;

            SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, totalContracts, 0, entryPrice, "", signalName);

            Print(FormatString("SHORT: {0} cts @ {1:F2} | Stop: {2:F2} | T1: {3:F2} | T2: {4:F2}",
                totalContracts, entryPrice, stopPrice, target1Price, target2Price));
            UpdateDisplay();
        }

        private void FlattenAll()
        {
            Print("FLATTEN ALL");
            
            foreach (var entry in new List<string>(entryOrders.Keys))
                CancelOrderByName(entry);
            
            foreach (var pos in new List<PositionInfo>(activePositions.Values))
            {
                CancelOrderByName("Stop_" + pos.SignalName);
                CancelOrderByName("T1_" + pos.SignalName);
                CancelOrderByName("T2_" + pos.SignalName);
                
                if (pos.RemainingContracts > 0 && Position.MarketPosition != MarketPosition.Flat)
                {
                    bool isLong = pos.Direction == MarketPosition.Long;
                    if (isLong)
                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalName);
                    else
                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalName);
                }
            }
            
            activePositions.Clear();
            entryOrders.Clear();
            stopOrders.Clear();
            target1Orders.Clear();
            target2Orders.Clear();
            
            Print("FLATTEN complete");
            UpdateDisplay();
        }

        private double CalculateStopDistance()
        {
            double stop = sessionRange * StopMultiplier;
            stop = Math.Max(MinimumStop, stop);
            stop = Math.Min(MaximumStop, stop);
            return stop;
        }

        private int CalculateContracts(double stopDistance)
        {
            double effectiveRisk = stopDistance > StopThresholdPoints ? ReducedRiskPerTrade : RiskPerTrade;
            double riskPerContract = stopDistance * pointValue;
            int contracts = (int)Math.Floor(effectiveRisk / riskPerContract);
            
            // Round to multiple of 3 for even split
            int remainder = contracts % 3;
            if (remainder != 0 && contracts > 3)
                contracts -= remainder;
            
            contracts = Math.Max(minContracts, contracts);
            contracts = Math.Min(maxContracts, contracts);
            return contracts;
        }

        private void CalculateContractSplit(int total, out int t1, out int t2, out int t3)
        {
            t1 = (int)Math.Floor(total * T1ContractPercent / 100.0);
            t2 = (int)Math.Floor(total * T2ContractPercent / 100.0);
            t3 = total - t1 - t2;
            
            if (total >= 3)
            {
                if (t1 < 1) t1 = 1;
                if (t2 < 1) t2 = 1;
                if (t3 < 1) t3 = 1;
                
                while (t1 + t2 + t3 > total)
                {
                    if (t3 > 1) t3--;
                    else if (t2 > 1) t2--;
                    else t1--;
                }
            }
        }

        #endregion

        #region Utility - String Builder Pool

        private string FormatString(string format, params object[] args)
        {
            // Reuse StringBuilder to reduce allocations
            sbPool.Clear();
            sbPool.AppendFormat(format, args);
            return sbPool.ToString();
        }

        #endregion

        #region UI

        private void CreateUI()
        {
            if (uiCreated) return;

            try
            {
                mainBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(240, 30, 30, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 10, 0, 0),
                    Padding = new Thickness(0),
                    MinWidth = 200,
                    MaxWidth = 260,
                    IsHitTestVisible = true
                };

                mainGrid = new System.Windows.Controls.Grid { IsHitTestVisible = true };
                for (int i = 0; i < 7; i++)
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Drag handle
                Border dragHandle = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(50, 50, 70)),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    Padding = new Thickness(5, 3, 5, 3),
                    Cursor = Cursors.SizeAll,
                    IsHitTestVisible = true
                };
                TextBlock dragLabel = new TextBlock
                {
                    Text = "â‰¡ OR Strategy v4 (drag)",
                    Foreground = Brushes.Gray,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                dragHandle.Child = dragLabel;
                Grid.SetRow(dragHandle, 0);
                
                dragHandle.MouseLeftButtonDown += OnDragStart;
                dragHandle.MouseMove += OnDragMove;
                dragHandle.MouseLeftButtonUp += OnDragEnd;
                dragHandle.MouseLeave += OnDragEnd;

                statusTextBlock = new TextBlock
                {
                    Text = "OR Strategy v4.0",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5, 5, 5, 2)
                };
                Grid.SetRow(statusTextBlock, 1);

                orInfoBlock = new TextBlock
                {
                    Text = "Waiting for OR...",
                    Foreground = Brushes.LightGray,
                    FontSize = 10,
                    Margin = new Thickness(5, 2, 5, 5),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(orInfoBlock, 2);

                positionSummaryBlock = new TextBlock
                {
                    Text = "No positions",
                    Foreground = Brushes.Gray,
                    FontSize = 9,
                    Margin = new Thickness(5, 2, 5, 5),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(positionSummaryBlock, 3);

                longButton = new Button
                {
                    Content = "LONG (L)",
                    Height = 32,
                    Margin = new Thickness(5, 3, 5, 3),
                    Background = new SolidColorBrush(Color.FromRgb(40, 120, 40)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    IsEnabled = false,
                    Cursor = Cursors.Hand
                };
                longButton.PreviewMouseLeftButtonDown += (s, e) => { ExecuteLong(); e.Handled = true; };
                Grid.SetRow(longButton, 4);

                shortButton = new Button
                {
                    Content = "SHORT (S)",
                    Height = 32,
                    Margin = new Thickness(5, 3, 5, 3),
                    Background = new SolidColorBrush(Color.FromRgb(150, 40, 40)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    IsEnabled = false,
                    Cursor = Cursors.Hand
                };
                shortButton.PreviewMouseLeftButtonDown += (s, e) => { ExecuteShort(); e.Handled = true; };
                Grid.SetRow(shortButton, 5);

                flattenButton = new Button
                {
                    Content = "FLATTEN (F)",
                    Height = 28,
                    Margin = new Thickness(5, 3, 5, 5),
                    Background = new SolidColorBrush(Color.FromRgb(180, 100, 20)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    IsEnabled = true,
                    Cursor = Cursors.Hand
                };
                flattenButton.PreviewMouseLeftButtonDown += (s, e) => { FlattenAll(); e.Handled = true; };
                Grid.SetRow(flattenButton, 6);

                mainGrid.Children.Add(dragHandle);
                mainGrid.Children.Add(statusTextBlock);
                mainGrid.Children.Add(orInfoBlock);
                mainGrid.Children.Add(positionSummaryBlock);
                mainGrid.Children.Add(longButton);
                mainGrid.Children.Add(shortButton);
                mainGrid.Children.Add(flattenButton);

                mainBorder.Child = mainGrid;
                UserControlCollection.Add(mainBorder);
                uiCreated = true;

                Print("UI created - v4.0 Box Visualization");
                UpdateDisplayInternal();
            }
            catch (Exception ex)
            {
                Print("ERROR CreateUI: " + ex.Message);
            }
        }

        private void RemoveUI()
        {
            if (mainBorder != null && uiCreated)
            {
                UserControlCollection.Remove(mainBorder);
                uiCreated = false;
            }
        }

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            if (mainBorder == null) return;
            isDragging = true;
            dragStartPoint = e.GetPosition(ChartControl);
            originalMargin = mainBorder.Margin;
            ((Border)sender).CaptureMouse();
            e.Handled = true;
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || mainBorder == null) return;
            
            Point currentPoint = e.GetPosition(ChartControl);
            double newLeft = originalMargin.Left + currentPoint.X - dragStartPoint.X;
            double newTop = originalMargin.Top + currentPoint.Y - dragStartPoint.Y;
            
            if (newLeft < 0) newLeft = 0;
            if (newTop < 0) newTop = 0;
            if (ChartControl != null)
            {
                if (newLeft > ChartControl.ActualWidth - 100) newLeft = ChartControl.ActualWidth - 100;
                if (newTop > ChartControl.ActualHeight - 100) newTop = ChartControl.ActualHeight - 100;
            }
            
            mainBorder.Margin = new Thickness(newLeft, newTop, 0, 0);
        }

        private void OnDragEnd(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                ((Border)sender).ReleaseMouseCapture();
            }
        }

        private void UpdateDisplay()
        {
            if (!uiCreated) return;
            ChartControl.Dispatcher.InvokeAsync(UpdateDisplayInternal);
        }
        
        private void UpdateDisplayInternal()
        {
            if (!uiCreated) return;
            
            try
            {
                string status = isInORWindow ? "BUILDING..." : (orComplete ? "OR COMPLETE" : "Waiting");
                statusTextBlock.Text = "OR v4 | " + status;
                statusTextBlock.Foreground = isInORWindow ? Brushes.Yellow : (orComplete ? Brushes.LimeGreen : Brushes.White);

                if (orComplete && sessionHigh != double.MinValue)
                {
                    double stopDist = CalculateStopDistance();
                    orInfoBlock.Text = FormatString("H:{0:F2} L:{1:F2} R:{2:F2}\nStop:{3:F2} T1:+{4} T2:+{5:F2}",
                        sessionHigh, sessionLow, sessionRange, stopDist, Target1Points, sessionRange * Target2Multiplier);
                }
                else if (isInORWindow && sessionHigh != double.MinValue)
                {
                    orInfoBlock.Text = FormatString("Building: H={0:F2} L={1:F2}", sessionHigh, sessionLow != double.MaxValue ? sessionLow : 0);
                }
                else
                {
                    orInfoBlock.Text = FormatString("{0} - {1} {2} ({3}m)", 
                        SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe);
                }

                bool canTrade = orComplete && sessionRange > 0;
                longButton.IsEnabled = canTrade;
                shortButton.IsEnabled = canTrade;

                if (canTrade)
                {
                    longButton.Content = FormatString("LONG @ {0:F2} (L)", sessionHigh + (3 * tickSize));
                    shortButton.Content = FormatString("SHORT @ {0:F2} (S)", sessionLow - (3 * tickSize));
                }
                else
                {
                    longButton.Content = "LONG (L)";
                    shortButton.Content = "SHORT (S)";
                }

                if (activePositions.Count > 0)
                {
                    sbPool.Clear();
                    foreach (var pos in activePositions.Values)
                    {
                        if (pos.EntryFilled)
                        {
                            string dir = pos.Direction == MarketPosition.Long ? "L" : "S";
                            string lvl = pos.CurrentTrailLevel == 0 ? "Init" : pos.CurrentTrailLevel == 1 ? "BE" : "T" + (pos.CurrentTrailLevel - 1);
                            sbPool.AppendFormat("{0}:{1} @{2:F2} S:{3:F2}({4})\n", dir, pos.RemainingContracts, pos.EntryPrice, pos.CurrentStopPrice, lvl);
                        }
                        else
                        {
                            string dir = pos.Direction == MarketPosition.Long ? "L" : "S";
                            sbPool.AppendFormat("{0}:{1} PENDING\n", dir, pos.TotalContracts);
                        }
                    }
                    positionSummaryBlock.Text = sbPool.ToString().TrimEnd('\n');
                }
                else
                {
                    positionSummaryBlock.Text = "No positions";
                }
            }
            catch { }
        }

        private void AttachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
                ChartControl.OwnerChart.PreviewKeyDown += OnKeyDown;
        }

        private void DetachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
                ChartControl.OwnerChart.PreviewKeyDown -= OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L) { ExecuteLong(); e.Handled = true; }
            else if (e.Key == Key.S) { ExecuteShort(); e.Handled = true; }
            else if (e.Key == Key.F) { FlattenAll(); e.Handled = true; }
        }

        #endregion
    }
}
