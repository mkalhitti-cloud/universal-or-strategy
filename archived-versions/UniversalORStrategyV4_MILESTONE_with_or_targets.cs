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
        [Display(Name = "Target 1 Multiplier", Description = "Multiplier of OR range for T1 (0.25 = 1/4 OR)", Order = 1, GroupName = "4. Profit Targets")]
        public double Target1Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 2 Multiplier", Description = "Multiplier of OR range for T2 (0.5 = half OR)", Order = 2, GroupName = "4. Profit Targets")]
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
                Description = "Universal Opening Range Strategy v4.0.2 - OR-BASED TARGETS";
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
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 4.0;
                MaximumStop = 8.0;

                // Target defaults - NOW OR-BASED
                Target1Multiplier = 0.25;  // T1 = 0.25 x OR Range
                Target2Multiplier = 0.5;   // T2 = 0.5 x OR Range
                T1ContractPercent = 33;
                T2ContractPercent = 33;
                T3ContractPercent = 34;

                // Trailing stop defaults - per your specification
                BreakEvenTriggerPoints = 2.0;   // At 2 points profit
                BreakEvenOffsetTicks = 1;       // Move stop to BE +1 tick
                Trail1TriggerPoints = 3.0;     // At 3 points profit
                Trail1DistancePoints = 2.0;    // Trail by 2 points
                Trail2TriggerPoints = 4.0;     // At 4 points profit
                Trail2DistancePoints = 1.5;    // Trail by 1.5 points
                Trail3TriggerPoints = 5.0;     // At 5 points profit
                Trail3DistancePoints = 1.0;    // Trail by 1 point

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

                Print(FormatString("UniversalORStrategy v4.0.2 OR-BASED TARGETS | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(FormatString("Session: {0} - {1} {2} | OR: {3} min", 
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
                Print(FormatString("Targets: T1={0}xOR T2={1}xOR T3=Trail | Stop={2}xOR", Target1Multiplier, Target2Multiplier, StopMultiplier));
                Print(FormatString("PC Timezone: {0}", TimeZoneInfo.Local.DisplayName));
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
                // CRITICAL FIX: Convert from LOCAL timezone (PC) to selected timezone
                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;
                
                // Calculate OR end time based on session start + timeframe
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                // Detect if session crosses midnight (e.g. 21:00 to 07:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;
                
                // Smart reset logic - only reset at NEW SESSION START
                bool shouldReset = false;
                
                if (sessionCrossesMidnight)
                {
                    // For overnight sessions: only reset at session start
                    if (currentTime >= sessionStartTime && currentTime < sessionStartTime.Add(TimeSpan.FromMinutes(10)))
                    {
                        if (barTimeInZone.Date != lastResetDate)
                        {
                            shouldReset = true;
                        }
                    }
                }
                else
                {
                    // For regular sessions: reset when date changes AFTER session ends
                    if (barTimeInZone.Date != lastResetDate && currentTime >= sessionStartTime)
                    {
                        shouldReset = true;
                    }
                }
                
                if (shouldReset)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print(FormatString("Session Reset: {0} at {1} {2}", 
                        barTimeInZone.Date.ToShortDateString(), currentTime, SelectedTimeZone));
                }

                // CRITICAL FIX: Use > and <= to start OR AFTER session start bar closes
                // This ensures the 9:30-9:35 bar is the FIRST bar of OR, not the 9:25-9:30 bar
                // Build OR during window (bars that close AFTER session start, up to and including OR end time)
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (!isInORWindow)
                    {
                        // First bar of OR window
                        Print(FormatString("OR WINDOW START: {0} (Bar time in {1})", 
                            barTimeInZone.ToString("MM/dd/yyyy HH:mm:ss"), SelectedTimeZone));
                    }
                    
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
                        Print(FormatString("OR Start tracked - Bar {0}", CurrentBar));
                    }
                }
                
                // Mark OR complete when the last bar of the window closes (at OR end time)
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(FormatString("OR COMPLETE at {0}: H={1:F2} L={2:F2} M={3:F2} R={4:F2}",
                        barTimeInZone.ToString("HH:mm:ss"), sessionHigh, sessionLow, sessionMid, sessionRange));
                    Print(FormatString("Targets: T1=+{0:F2} T2=+{1:F2} Stop=-{2:F2}", 
                        sessionRange * Target1Multiplier, sessionRange * Target2Multiplier, CalculateStopDistance()));

                    DrawORBox();
                }

                // Update box if OR complete (extends to current bar until session end)
                bool inActiveSession = false;
                if (sessionCrossesMidnight)
                {
                    // Overnight session: we're in session if time >= start OR time <= end
                    inActiveSession = (currentTime >= sessionStartTime || currentTime <= sessionEndTime);
                }
                else
                {
                    // Regular session: we're in session if time is between start and end
                    inActiveSession = (currentTime >= sessionStartTime && currentTime <= sessionEndTime);
                }
                
                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
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
            if (orStartDateTime == DateTime.MinValue || orEndDateTime == DateTime.MinValue) return;

            try
            {
                // Calculate opacity for area fill
                int areaOpacity = BoxOpacity;
                
                // Calculate session end DateTime for box endpoint
                // Convert orStartDateTime to selected timezone to get the date
                DateTime orStartInZone = ConvertToSelectedTimeZone(orStartDateTime);
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;
                
                // Create DateTime for session end on the same date as OR start
                DateTime sessionEndInZone = new DateTime(
                    orStartInZone.Year,
                    orStartInZone.Month, 
                    orStartInZone.Day,
                    sessionEndTime.Hours,
                    sessionEndTime.Minutes,
                    sessionEndTime.Seconds
                );
                
                // Convert back to local time for drawing
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
                    default:
                        targetZone = TimeZoneInfo.Local;
                        break;
                }
                
                DateTime boxEndTime = TimeZoneInfo.ConvertTime(sessionEndInZone, targetZone, TimeZoneInfo.Local);
                
                // Use DateTime anchoring - box spans from OR start to SESSION END
                Draw.Rectangle(this, "ORBox", false, 
                    orStartDateTime, sessionHigh,
                    boxEndTime, sessionLow,
                    Brushes.DodgerBlue, Brushes.DodgerBlue, areaOpacity);

                // Optional mid line
                if (ShowMidLine)
                {
                    Draw.Line(this, "ORMid", false, 
                        orStartDateTime, sessionMid, 
                        boxEndTime, sessionMid, 
                        Brushes.Yellow, DashStyleHelper.Dash, 1);
                }

                // Text label
                string labelText = isInORWindow 
                    ? FormatString("OR Building: {0:F2} - {1:F2}", sessionHigh, sessionLow)
                    : FormatString("OR: {0:F2} - {1:F2} (R:{2:F2})", sessionHigh, sessionLow, sessionRange);
                
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

            RemoveDrawObjects();
        }

        #endregion

        #region Helpers

        // CRITICAL FIX: Correctly convert from LOCAL (PC) timezone to selected timezone
        private DateTime ConvertToSelectedTimeZone(DateTime localTime)
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
                        return localTime; // Return unchanged if timezone unknown
                }
                
                // Convert from LOCAL timezone to TARGET timezone
                // Time[0] is in PC's local timezone, NOT UTC
                return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, targetZone);
            }
            catch (Exception ex)
            {
                Print("ERROR ConvertToSelectedTimeZone: " + ex.Message);
                return localTime; // Fallback to original time
            }
        }

        private string FormatString(string format, params object[] args)
        {
            return string.Format(format, args);
        }

        private void RemoveDrawObjects()
        {
            RemoveDrawObject("ORBox");
            RemoveDrawObject("ORMid");
            RemoveDrawObject("ORLabel");
        }

        #endregion

        #region Entry Logic

        private void ExecuteLong()
        {
            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Long - OR not ready");
                return;
            }

            double entryPrice = sessionHigh + (3 * tickSize);
            double stopDistance = CalculateStopDistance();
            double stopPrice = entryPrice - stopDistance;

            EnterPosition(MarketPosition.Long, entryPrice, stopPrice);
        }

        private void ExecuteShort()
        {
            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Short - OR not ready");
                return;
            }

            double entryPrice = sessionLow - (3 * tickSize);
            double stopDistance = CalculateStopDistance();
            double stopPrice = entryPrice + stopDistance;

            EnterPosition(MarketPosition.Short, entryPrice, stopPrice);
        }

        private void EnterPosition(MarketPosition direction, double entryPrice, double stopPrice)
        {
            try
            {
                // Calculate position size
                double stopDistance = CalculateStopDistance();
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);
                
                // Apply min/max limits
                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // DYNAMIC TARGET ALLOCATION based on contract size
                int t1Qty, t2Qty, t3Qty;

                if (contracts == 1)
                {
                    // 1 contract: Trail only (no fixed targets)
                    t1Qty = 0;
                    t2Qty = 0;
                    t3Qty = 1;
                    Print("POSITION SIZE: 1 contract â†’ Trail-only mode");
                }
                else if (contracts == 2)
                {
                    // 2 contracts: One at T1, one trails
                    t1Qty = 1;
                    t2Qty = 0;
                    t3Qty = 1;
                    Print("POSITION SIZE: 2 contracts â†’ T1:1 Trail:1");
                }
                else
                {
                    // 3+ contracts: Standard percentage split
                    t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                    t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                    t3Qty = contracts - t1Qty - t2Qty;
                    
                    // Ensure minimum 1 each for small sizes (3-5 contracts)
                    if (t1Qty < 1 || t2Qty < 1 || t3Qty < 1)
                    {
                        t1Qty = 1;
                        t2Qty = 1;
                        t3Qty = contracts - 2;
                        Print(FormatString("POSITION SIZE ADJUSTED: {0} contracts â†’ T1:1 T2:1 T3:{1}", contracts, t3Qty));
                    }
                }

                string signalName = direction == MarketPosition.Long ? "Long" : "Short";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string entryName = signalName + "_" + timestamp;

                // Calculate targets - NOW BOTH OR-BASED
                double target1Price = direction == MarketPosition.Long 
                    ? entryPrice + (sessionRange * Target1Multiplier) 
                    : entryPrice - (sessionRange * Target1Multiplier);

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target2Multiplier)
                    : entryPrice - (sessionRange * Target2Multiplier);

                // Create position info
                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0
                };

                activePositions[entryName] = pos;

                // Submit entry order
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(FormatString("ENTRY ORDER: {0} {1} @ {2:F2} | Stop: {3:F2} (-{4:F2})", 
                    signalName, contracts, entryPrice, stopPrice, stopDistance));
                Print(FormatString("TARGETS: T1: {0}@{1:F2} (+{2:F2}) | T2: {3}@{4:F2} (+{5:F2}) | T3: {6}@trail",
                    t1Qty, target1Price, sessionRange * Target1Multiplier, 
                    t2Qty, target2Price, sessionRange * Target2Multiplier, t3Qty));

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR EnterPosition: " + ex.Message);
            }
        }

        private double CalculateStopDistance()
        {
            if (sessionRange == 0) return MinimumStop;
            
            double calculatedStop = sessionRange * StopMultiplier;
            return Math.Max(MinimumStop, Math.Min(calculatedStop, MaximumStop));
        }

        #endregion

        #region Order Management

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (order == null) return;

            try
            {
                string orderName = order.Name;

                // Entry order filled
                if (entryOrders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    string entryName = orderName;
                    if (activePositions.ContainsKey(entryName))
                    {
                        PositionInfo pos = activePositions[entryName];
                        pos.EntryFilled = true;
                        pos.EntryPrice = averageFillPrice;
                        pos.ExtremePriceSinceEntry = averageFillPrice;

                        Print(FormatString("ENTRY FILLED: {0} {1} @ {2:F2}", 
                            pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", 
                            pos.TotalContracts, averageFillPrice));

                        SubmitBracketOrders(entryName, pos);
                    }
                }

                // Target 1 filled
                if (target1Orders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    foreach (var kvp in activePositions)
                    {
                        if (target1Orders.ContainsKey(kvp.Key) && target1Orders[kvp.Key] == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T1Filled = true;
                            pos.RemainingContracts -= pos.T1Contracts;
                            Print(FormatString("T1 FILLED: {0} contracts @ {1:F2} | Remaining: {2}", 
                                pos.T1Contracts, averageFillPrice, pos.RemainingContracts));
                            break;
                        }
                    }
                }

                // Target 2 filled
                if (target2Orders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    foreach (var kvp in activePositions)
                    {
                        if (target2Orders.ContainsKey(kvp.Key) && target2Orders[kvp.Key] == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T2Filled = true;
                            pos.RemainingContracts -= pos.T2Contracts;
                            Print(FormatString("T2 FILLED: {0} contracts @ {1:F2} | Remaining: {2}", 
                                pos.T2Contracts, averageFillPrice, pos.RemainingContracts));
                            break;
                        }
                    }
                }

                // Stop filled - position closed
                if (stopOrders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    foreach (var kvp in activePositions)
                    {
                        if (stopOrders.ContainsKey(kvp.Key) && stopOrders[kvp.Key] == order)
                        {
                            PositionInfo pos = kvp.Value;
                            Print(FormatString("STOP FILLED: {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                            CleanupPosition(kvp.Key);
                            break;
                        }
                    }
                }

                // Order rejected
                if (orderState == OrderState.Rejected)
                {
                    Print(FormatString("ORDER REJECTED: {0} | Error: {1}", orderName, nativeError));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        private void SubmitBracketOrders(string entryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;

            try
            {
                // Validate stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);

                // Submit initial stop for all contracts
                Order stopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName);

                stopOrders[entryName] = stopOrder;

                // Submit T1 limit order ONLY if T1 quantity > 0
                if (pos.T1Contracts > 0)
                {
                    Order t1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName);

                    target1Orders[entryName] = t1Order;
                }

                // Submit T2 limit order ONLY if T2 quantity > 0
                if (pos.T2Contracts > 0)
                {
                    Order t2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName);

                    target2Orders[entryName] = t2Order;
                }

                pos.BracketSubmitted = true;
                pos.CurrentStopPrice = validatedStopPrice;

                // Build bracket summary message
                StringBuilder bracketMsg = new StringBuilder();
                bracketMsg.AppendFormat("BRACKET SUBMITTED: Stop@{0:F2}", validatedStopPrice);
                if (pos.T1Contracts > 0)
                    bracketMsg.AppendFormat(" | T1:{0}@{1:F2}", pos.T1Contracts, pos.Target1Price);
                if (pos.T2Contracts > 0)
                    bracketMsg.AppendFormat(" | T2:{0}@{1:F2}", pos.T2Contracts, pos.Target2Price);
                if (pos.T3Contracts > 0)
                    bracketMsg.AppendFormat(" | T3:{0}@trail", pos.T3Contracts);
                
                Print(bracketMsg.ToString());
            }
            catch (Exception ex)
            {
                Print("ERROR SubmitBracketOrders: " + ex.Message);
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice)
        {
            double currentPrice = Close[0];
            double minDistance = 2 * tickSize;

            if (direction == MarketPosition.Long)
            {
                // Long: stop must be below current price
                if (desiredStopPrice >= currentPrice)
                {
                    double validStop = currentPrice - minDistance;
                    Print(FormatString("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (was at/above market)", 
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }
            else
            {
                // Short: stop must be above current price
                if (desiredStopPrice <= currentPrice)
                {
                    double validStop = currentPrice + minDistance;
                    Print(FormatString("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (was at/below market)", 
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }

            return desiredStopPrice;
        }

        #endregion

        #region Trailing Stops

        private void ManageTrailingStops()
        {
            foreach (var kvp in activePositions)
            {
                string entryName = kvp.Key;
                PositionInfo pos = kvp.Value;

                if (!pos.EntryFilled || !pos.BracketSubmitted) continue;

                // Update extreme price
                if (pos.Direction == MarketPosition.Long)
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, Close[0]);
                else
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, Close[0]);

                double profitPoints = pos.Direction == MarketPosition.Long
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                double newStopPrice = pos.CurrentStopPrice;
                int newTrailLevel = pos.CurrentTrailLevel;

                // Trail 3 (highest priority) - At 5 points, trail by 1 point
                if (profitPoints >= Trail3TriggerPoints && pos.T1Filled && pos.T2Filled)
                {
                    double trail3Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail3Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4;
                    }
                    else if (pos.Direction == MarketPosition.Short && trail3Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4;
                    }
                }
                // Trail 2 - At 4 points, trail by 1.5 points
                else if (profitPoints >= Trail2TriggerPoints && pos.T1Filled && pos.CurrentTrailLevel < 3)
                {
                    double trail2Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail2Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3;
                    }
                    else if (pos.Direction == MarketPosition.Short && trail2Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3;
                    }
                }
                // Trail 1 - At 3 points, trail by 2 points
                else if (profitPoints >= Trail1TriggerPoints && pos.CurrentTrailLevel < 2)
                {
                    double trail1Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail1Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2;
                    }
                    else if (pos.Direction == MarketPosition.Short && trail1Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2;
                    }
                }
                // Break-even - At 2 points, move to BE +1 tick
                else if (profitPoints >= BreakEvenTriggerPoints && pos.CurrentTrailLevel < 1)
                {
                    double beStop = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);

                    if (pos.Direction == MarketPosition.Long && beStop > pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                    }
                    else if (pos.Direction == MarketPosition.Short && beStop < pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                    }
                }

                // Update stop if needed
                if (newStopPrice != pos.CurrentStopPrice)
                {
                    UpdateStopOrder(entryName, pos, newStopPrice, newTrailLevel);
                }
            }
        }

        private void UpdateStopOrder(string entryName, PositionInfo pos, double newStopPrice, int newTrailLevel)
        {
            if (!stopOrders.ContainsKey(entryName)) return;

            try
            {
                Order currentStop = stopOrders[entryName];
                
                // Validate new stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice);

                // Cancel old stop
                CancelOrder(currentStop);

                // Submit new stop
                Order newStop = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks);

                stopOrders[entryName] = newStop;
                pos.CurrentStopPrice = validatedStopPrice;
                pos.CurrentTrailLevel = newTrailLevel;

                string levelName = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
                Print(FormatString("STOP UPDATED: {0} â†’ {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
            }
            catch (Exception ex)
            {
                Print("ERROR UpdateStopOrder: " + ex.Message);
            }
        }

        #endregion

        #region Position Sync

        private void SyncPositionState()
        {
            // Check if position was closed externally
            List<string> toRemove = new List<string>();

            foreach (var kvp in activePositions)
            {
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && Position.MarketPosition == MarketPosition.Flat)
                {
                    Print(FormatString("EXTERNAL CLOSE detected for {0}", kvp.Key));
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                CleanupPosition(key);
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            if (marketPosition == MarketPosition.Flat && activePositions.Count > 0)
            {
                Print("POSITION FLATTENED - Cleaning up all tracking");
                List<string> allKeys = new List<string>(activePositions.Keys);
                foreach (string key in allKeys)
                {
                    CleanupPosition(key);
                }
            }
        }

        #endregion

        #region Flatten

        private void FlattenAll()
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("No active positions to flatten");
                    return;
                }

                Print("FLATTEN ALL initiated");

                // Only flatten FILLED positions, keep pending entry orders active
                List<string> positionsToCleanup = new List<string>();
                
                foreach (var kvp in activePositions)
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;
                    
                    if (pos.EntryFilled)
                    {
                        // Position is filled - cancel bracket orders and close position
                        Print(FormatString("FLATTEN: Closing filled {0} position", 
                            pos.Direction == MarketPosition.Long ? "LONG" : "SHORT"));
                        
                        // Cancel stop order
                        if (stopOrders.ContainsKey(entryName))
                        {
                            Order stopOrder = stopOrders[entryName];
                            if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                CancelOrder(stopOrder);
                        }
                        
                        // Cancel T1 order
                        if (target1Orders.ContainsKey(entryName))
                        {
                            Order t1Order = target1Orders[entryName];
                            if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                                CancelOrder(t1Order);
                        }
                        
                        // Cancel T2 order
                        if (target2Orders.ContainsKey(entryName))
                        {
                            Order t2Order = target2Orders[entryName];
                            if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                                CancelOrder(t2Order);
                        }
                        
                        // Submit market order to close position
                        if (pos.RemainingContracts > 0)
                        {
                            Order flattenOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + entryName);

                            Print(FormatString("FLATTEN: {0} {1} contracts at MARKET", 
                                pos.Direction == MarketPosition.Long ? "SELL" : "BUY", pos.RemainingContracts));
                        }
                        
                        // Mark for cleanup
                        positionsToCleanup.Add(entryName);
                    }
                    else
                    {
                        // Position not filled yet - KEEP the pending entry order active
                        Print(FormatString("FLATTEN: Keeping pending {0} entry order active @ {1:F2}", 
                            pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", pos.EntryPrice));
                    }
                }
                
                // Clean up filled positions only
                foreach (string key in positionsToCleanup)
                {
                    CleanupPosition(key);
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR FlattenAll: " + ex.Message);
            }
        }

        private void CleanupPosition(string entryName)
        {
            activePositions.Remove(entryName);
            entryOrders.Remove(entryName);
            stopOrders.Remove(entryName);
            target1Orders.Remove(entryName);
            target2Orders.Remove(entryName);
            UpdateDisplay();
        }

        #endregion

        #region UI

        private void CreateUI()
        {
            if (ChartControl == null || uiCreated) return;

            try
            {
                mainBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 20, 20, 30)),
                    BorderBrush = Brushes.DodgerBlue,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 10, 0, 0),
                    MinWidth = 220
                };

                mainGrid = new System.Windows.Controls.Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border dragHandle = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(100, 70, 130, 180)),
                    Height = 24,
                    Margin = new Thickness(-8, -8, -8, 5),
                    Cursor = Cursors.SizeAll
                };
                dragHandle.MouseLeftButtonDown += OnDragStart;
                dragHandle.MouseMove += OnDragMove;
                dragHandle.MouseLeftButtonUp += OnDragEnd;
                Grid.SetRow(dragHandle, 0);

                TextBlock dragLabel = new TextBlock
                {
                    Text = "â•â•â• OR Strategy v4.0.2 â•â•â•",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dragHandle.Child = dragLabel;

                statusTextBlock = new TextBlock
                {
                    Text = "OR v4 | Initializing...",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Margin = new Thickness(0, 5, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(statusTextBlock, 1);

                orInfoBlock = new TextBlock
                {
                    Text = "Waiting for session...",
                    Foreground = Brushes.LightGray,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 8),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(orInfoBlock, 2);

                positionSummaryBlock = new TextBlock
                {
                    Text = "No positions",
                    Foreground = Brushes.Cyan,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 8),
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

                Print("UI created - v4.0.2 OR-BASED TARGETS");
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
                    double t1Dist = sessionRange * Target1Multiplier;
                    double t2Dist = sessionRange * Target2Multiplier;
                    orInfoBlock.Text = FormatString("H:{0:F2} L:{1:F2} R:{2:F2}\nStop:{3:F2} T1:+{4:F2} T2:+{5:F2}",
                        sessionHigh, sessionLow, sessionRange, stopDist, t1Dist, t2Dist);
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
