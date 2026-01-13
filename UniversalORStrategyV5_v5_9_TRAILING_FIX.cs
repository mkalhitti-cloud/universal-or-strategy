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
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class UniversalORStrategyV5 : Strategy
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

        // ATR Indicator for RMA
        private ATR atrIndicator;
        private double currentATR;
        private double lastKnownPrice;  // Track current price for UI events

        // Position tracking - multi-target system
        private Dictionary<string, PositionInfo> activePositions;
        private Dictionary<string, Order> entryOrders;
        private Dictionary<string, Order> stopOrders;
        private Dictionary<string, Order> target1Orders;
        private Dictionary<string, Order> target2Orders;

        // RMA Mode tracking
        private bool isRMAModeActive;
        private bool isRKeyHeld;
        private bool isRMAButtonClicked;  // One-shot mode from button

        // UI Components
        private Border mainBorder;
        private System.Windows.Controls.Grid mainGrid;
        private TextBlock statusTextBlock;
        private TextBlock orInfoBlock;
        private TextBlock positionSummaryBlock;
        private TextBlock rmaModeTextBlock;
        private Button longButton;
        private Button shortButton;
        private Button rmaButton;
        private Button flattenButton;
        private bool uiCreated;

        // Colors for UI - MUST be frozen for cross-thread access
        private static readonly SolidColorBrush RMAActiveBackground;
        private static readonly SolidColorBrush RMAInactiveBackground;
        private static readonly SolidColorBrush PanelBackground;
        private static readonly SolidColorBrush RMAModeActiveBackground;

        // Static constructor to create and freeze brushes
        static UniversalORStrategyV5()
        {
            RMAActiveBackground = new SolidColorBrush(Color.FromRgb(180, 100, 20));
            RMAActiveBackground.Freeze();

            RMAInactiveBackground = new SolidColorBrush(Color.FromRgb(80, 80, 100));
            RMAInactiveBackground.Freeze();

            PanelBackground = new SolidColorBrush(Color.FromArgb(230, 20, 20, 30));
            PanelBackground.Freeze();

            RMAModeActiveBackground = new SolidColorBrush(Color.FromArgb(230, 50, 30, 10));
            RMAModeActiveBackground.Freeze();
        }

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
            public bool IsRMATrade;  // Flag to identify RMA trades
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
        [Display(Name = "Risk Per Trade ($)", Description = "Maximum dollar risk per trade (when stop ≤ threshold)", Order = 1, GroupName = "2. Risk Management")]
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

        #region Properties - RMA Settings

        [NinjaScriptProperty]
        [Display(Name = "RMA Enabled", Description = "Enable RMA (Shift+Click) entry mode", Order = 1, GroupName = "7. RMA Settings")]
        public bool RMAEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA ATR Period", Description = "ATR period for RMA calculations (default 14)", Order = 2, GroupName = "7. RMA Settings")]
        [Range(1, 100)]
        public int RMAATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA Stop ATR Multiplier", Description = "Multiplier of ATR for RMA stop (default 1.0)", Order = 3, GroupName = "7. RMA Settings")]
        [Range(0.1, 5.0)]
        public double RMAStopATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA T1 ATR Multiplier", Description = "Multiplier of ATR for RMA Target 1 (default 0.5)", Order = 4, GroupName = "7. RMA Settings")]
        [Range(0.1, 5.0)]
        public double RMAT1ATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA T2 ATR Multiplier", Description = "Multiplier of ATR for RMA Target 2 (default 1.0)", Order = 5, GroupName = "7. RMA Settings")]
        [Range(0.1, 5.0)]
        public double RMAT2ATRMultiplier { get; set; }

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
                Description = "Universal Opening Range Strategy v5.8 STOP_VALIDATION - Enhanced Stop Order Protection";
                Name = "UniversalORStrategyV5";
                Calculate = Calculate.OnPriceChange;  // CRITICAL FIX: Updates on every price tick for real-time trailing
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
                RiskPerTrade = 200;
                ReducedRiskPerTrade = 200;
                StopThresholdPoints = 5.0;
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 1.0;
                MaximumStop = 8.0;

                // Target defaults - OR-BASED
                Target1Multiplier = 0.25;  // T1 = 0.25 x OR Range
                Target2Multiplier = 0.5;   // T2 = 0.5 x OR Range
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

                // RMA defaults
                RMAEnabled = true;
                RMAATRPeriod = 14;
                RMAStopATRMultiplier = 1.0;
                RMAT1ATRMultiplier = 0.5;
                RMAT2ATRMultiplier = 1.0;
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

                // Add 5-min data series for ATR (index 1)
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;

                string symbol = Instrument.MasterInstrument.Name;
                if (symbol.Contains("MES") || symbol.Contains("ES"))
                {
                    minContracts = MESMinimum;
                    maxContracts = MESMaximum;
                }
                else if (symbol.Contains("MGC") || symbol.Contains("GC"))
                {
                    minContracts = MGCMinimum;
                    maxContracts = MGCMaximum;
                }
                else
                {
                    minContracts = 3;
                    maxContracts = 15;
                }

                // Initialize ATR indicator on 5-min bars (BarsArray[1])
                atrIndicator = ATR(BarsArray[1], RMAATRPeriod);

                ResetOR();

                Print(FormatString("UniversalORStrategy v5.8 STOP_VALIDATION | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(FormatString("Session: {0} - {1} {2} | OR: {3} min",
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
                Print(FormatString("OR Targets: T1={0}xOR T2={1}xOR T3=Trail | Stop={2}xOR", Target1Multiplier, Target2Multiplier, StopMultiplier));
                Print(FormatString("RMA: Enabled={0} ATR({1}) Stop={2}xATR T1={3}xATR T2={4}xATR",
                    RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier, RMAT1ATRMultiplier, RMAT2ATRMultiplier));
                Print("v5.8 STOP_VALIDATION: Enhanced stop protection with validation + Risk=$200/200, MinStop=1pt");
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
                        AttachChartClickHandler();
                        UpdateDisplayInternal();
                        Print("REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
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
                        DetachChartClickHandler();
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
            // Only process primary series
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                // Update last known price for UI events
                lastKnownPrice = Close[0];

                // Update ATR value from 5-min bars
                if (BarsArray[1] != null && BarsArray[1].Count > RMAATRPeriod)
                {
                    currentATR = atrIndicator[0];
                }

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

                // Build OR during window
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (!isInORWindow)
                    {
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

                // Mark OR complete when the last bar of the window closes
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(FormatString("OR COMPLETE at {0}: H={1:F2} L={2:F2} M={3:F2} R={4:F2}",
                        barTimeInZone.ToString("HH:mm:ss"), sessionHigh, sessionLow, sessionMid, sessionRange));
                    Print(FormatString("OR Targets: T1=+{0:F2} T2=+{1:F2} Stop=-{2:F2}",
                        sessionRange * Target1Multiplier, sessionRange * Target2Multiplier, CalculateORStopDistance()));

                    DrawORBox();
                }

                // Update box if OR complete
                bool inActiveSession = false;
                if (sessionCrossesMidnight)
                {
                    inActiveSession = (currentTime >= sessionStartTime || currentTime <= sessionEndTime);
                }
                else
                {
                    inActiveSession = (currentTime >= sessionStartTime && currentTime <= sessionEndTime);
                }

                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
                {
                    DrawORBox();
                }

                // Position sync check
                SyncPositionState();

                // Manage trailing stops - NOW CALLED ON EVERY PRICE CHANGE!
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
                int areaOpacity = BoxOpacity;

                DateTime orStartInZone = ConvertToSelectedTimeZone(orStartDateTime);
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Detect overnight session (e.g., 21:00 to 16:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // Calculate session end date
                DateTime sessionEndInZone;
                if (sessionCrossesMidnight)
                {
                    // Overnight session: end time is NEXT day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    ).AddDays(1);  // ADD ONE DAY for overnight sessions!
                }
                else
                {
                    // Same-day session: end time is same day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    );
                }

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

                Draw.Rectangle(this, "ORBox", false,
                    orStartDateTime, sessionHigh,
                    boxEndTime, sessionLow,
                    Brushes.DodgerBlue, Brushes.DodgerBlue, areaOpacity);

                if (ShowMidLine)
                {
                    Draw.Line(this, "ORMid", false,
                        orStartDateTime, sessionMid,
                        boxEndTime, sessionMid,
                        Brushes.Yellow, DashStyleHelper.Dash, 1);
                }

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
                        return localTime;
                }

                return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, targetZone);
            }
            catch (Exception ex)
            {
                Print("ERROR ConvertToSelectedTimeZone: " + ex.Message);
                return localTime;
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

        #region OR Entry Logic

        private void ExecuteLong()
        {
            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Long - OR not ready");
                return;
            }

            double entryPrice = sessionHigh + (3 * tickSize);
            double stopDistance = CalculateORStopDistance();
            double stopPrice = entryPrice - stopDistance;

            EnterORPosition(MarketPosition.Long, entryPrice, stopPrice);
        }

        private void ExecuteShort()
        {
            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Short - OR not ready");
                return;
            }

            double entryPrice = sessionLow - (3 * tickSize);
            double stopDistance = CalculateORStopDistance();
            double stopPrice = entryPrice + stopDistance;

            EnterORPosition(MarketPosition.Short, entryPrice, stopPrice);
        }

        private void EnterORPosition(MarketPosition direction, double entryPrice, double stopPrice)
        {
            try
            {
                double stopDistance = CalculateORStopDistance();
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);

                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                int t1Qty, t2Qty, t3Qty;

                if (contracts == 1)
                {
                    t1Qty = 0;
                    t2Qty = 0;
                    t3Qty = 1;
                    Print("POSITION SIZE: 1 contract → Trail-only mode");
                }
                else if (contracts == 2)
                {
                    t1Qty = 1;
                    t2Qty = 0;
                    t3Qty = 1;
                    Print("POSITION SIZE: 2 contracts → T1:1 Trail:1");
                }
                else
                {
                    t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                    t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                    t3Qty = contracts - t1Qty - t2Qty;

                    if (t1Qty < 1 || t2Qty < 1 || t3Qty < 1)
                    {
                        t1Qty = 1;
                        t2Qty = 1;
                        t3Qty = contracts - 2;
                        Print(FormatString("POSITION SIZE ADJUSTED: {0} contracts → T1:1 T2:1 T3:{1}", contracts, t3Qty));
                    }
                }

                string signalName = direction == MarketPosition.Long ? "ORLong" : "ORShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string entryName = signalName + "_" + timestamp;

                // OR-based targets
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target1Multiplier)
                    : entryPrice - (sessionRange * Target1Multiplier);

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target2Multiplier)
                    : entryPrice - (sessionRange * Target2Multiplier);

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
                    CurrentTrailLevel = 0,
                    IsRMATrade = false
                };

                activePositions[entryName] = pos;

                // Submit entry order as stop market (breakout entry)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(FormatString("OR ENTRY ORDER: {0} {1} @ {2:F2} | Stop: {3:F2} (-{4:F2})",
                    signalName, contracts, entryPrice, stopPrice, stopDistance));
                Print(FormatString("OR TARGETS: T1: {0}@{1:F2} (+{2:F2}) | T2: {3}@{4:F2} (+{5:F2}) | T3: {6}@trail",
                    t1Qty, target1Price, sessionRange * Target1Multiplier,
                    t2Qty, target2Price, sessionRange * Target2Multiplier, t3Qty));

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR EnterORPosition: " + ex.Message);
            }
        }

        private double CalculateORStopDistance()
        {
            if (sessionRange == 0) return MinimumStop;

            double calculatedStop = sessionRange * StopMultiplier;
            return Math.Max(MinimumStop, Math.Min(calculatedStop, MaximumStop));
        }

        #endregion

        #region RMA Entry Logic

        private void ExecuteRMAEntry(double clickPrice)
        {
            if (!RMAEnabled)
            {
                Print("RMA mode is disabled");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute RMA entry - ATR not available yet");
                return;
            }

            try
            {
                // Use last known price from OnBarUpdate (Close[0] may be stale in UI events)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // Auto-direction: Click ABOVE current price = SHORT, Click BELOW = LONG
                MarketPosition direction;
                if (clickPrice > currentPrice)
                {
                    direction = MarketPosition.Short;
                    Print(FormatString("RMA: Click above price ({0:F2} > {1:F2}) = SHORT entry", clickPrice, currentPrice));
                }
                else
                {
                    direction = MarketPosition.Long;
                    Print(FormatString("RMA: Click below price ({0:F2} < {1:F2}) = LONG entry", clickPrice, currentPrice));
                }

                // Calculate RMA stop and targets using ATR
                double stopDistance = currentATR * RMAStopATRMultiplier;
                double t1Distance = currentATR * RMAT1ATRMultiplier;
                double t2Distance = currentATR * RMAT2ATRMultiplier;

                double entryPrice = clickPrice;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + t1Distance
                    : entryPrice - t1Distance;

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + t2Distance
                    : entryPrice - t2Distance;

                // Calculate position size based on ATR stop
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);

                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // Contract allocation (same as OR)
                int t1Qty, t2Qty, t3Qty;

                if (contracts == 1)
                {
                    t1Qty = 0;
                    t2Qty = 0;
                    t3Qty = 1;
                }
                else if (contracts == 2)
                {
                    t1Qty = 1;
                    t2Qty = 0;
                    t3Qty = 1;
                }
                else
                {
                    t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                    t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                    t3Qty = contracts - t1Qty - t2Qty;

                    if (t1Qty < 1 || t2Qty < 1 || t3Qty < 1)
                    {
                        t1Qty = 1;
                        t2Qty = 1;
                        t3Qty = contracts - 2;
                    }
                }

                string signalName = direction == MarketPosition.Long ? "RMALong" : "RMAShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string entryName = signalName + "_" + timestamp;

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
                    CurrentTrailLevel = 0,
                    IsRMATrade = true
                };

                activePositions[entryName] = pos;

                // Submit LIMIT order at clicked price (RMA uses limit entries)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(FormatString("RMA ENTRY ORDER: {0} {1} @ {2:F2} (LIMIT)", signalName, contracts, entryPrice));
                Print(FormatString("RMA ATR({0})={1:F2} | Stop: {2:F2} (-{3:F2})",
                    RMAATRPeriod, currentATR, stopPrice, stopDistance));
                Print(FormatString("RMA TARGETS: T1: {0}@{1:F2} (+{2:F2}) | T2: {3}@{4:F2} (+{5:F2}) | T3: {6}@trail",
                    t1Qty, target1Price, t1Distance, t2Qty, target2Price, t2Distance, t3Qty));

                // Deactivate RMA mode after entry (one-shot)
                DeactivateRMAMode();
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRMAEntry: " + ex.Message);
            }
        }

        private void ActivateRMAMode()
        {
            isRMAModeActive = true;
            UpdateRMAModeDisplay();
        }

        private void DeactivateRMAMode()
        {
            isRMAModeActive = false;
            isRMAButtonClicked = false;
            isRKeyHeld = false;
            UpdateRMAModeDisplay();
        }

        #endregion

        #region Order Management

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
        {
            try
            {
                string orderName = order.Name;

                // Entry filled
                if (entryOrders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    // Find matching position
                    foreach (var kvp in activePositions)
                    {
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        if (entryOrders.ContainsKey(entryName) && entryOrders[entryName] == order && !pos.EntryFilled)
                        {
                            pos.EntryFilled = true;

                            // Store intended entry price for slippage calculation
                            double intendedEntryPrice = pos.EntryPrice;

                            string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                            Print(FormatString("{0} ENTRY FILLED: {1} {2} @ {3:F2} (intended: {4:F2})",
                                tradeType,
                                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                pos.TotalContracts,
                                averageFillPrice,
                                intendedEntryPrice));

                            // For RMA trades, adjust targets based on actual fill price
                            if (pos.IsRMATrade)
                            {
                                double t1Distance = currentATR * RMAT1ATRMultiplier;
                                double t2Distance = currentATR * RMAT2ATRMultiplier;
                                double stopDistance = currentATR * RMAStopATRMultiplier;

                                pos.InitialStopPrice = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice - stopDistance
                                    : averageFillPrice + stopDistance;
                                pos.CurrentStopPrice = pos.InitialStopPrice;

                                pos.Target1Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t1Distance
                                    : averageFillPrice - t1Distance;
                                pos.Target2Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t2Distance
                                    : averageFillPrice - t2Distance;

                                if (Math.Abs(averageFillPrice - intendedEntryPrice) > tickSize)
                                {
                                    Print(FormatString("{0} PRICES ADJUSTED for fill slippage: Stop={1:F2} T1={2:F2} T2={3:F2}",
                                        tradeType, pos.InitialStopPrice, pos.Target1Price, pos.Target2Price));
                                }
                            }

                            // Update to actual fill price
                            pos.EntryPrice = averageFillPrice;
                            pos.ExtremePriceSinceEntry = averageFillPrice;

                            SubmitBracketOrders(entryName, pos);
                        }
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

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
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

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
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
                    
                    // CRITICAL v5.8: Check if this was a stop order rejection
                    if (stopOrders.ContainsValue(order))
                    {
                        Print(FormatString("⚠️ CRITICAL: Stop order REJECTED: {0}", orderName));
                        
                        // Find which position this stop belongs to
                        foreach (var kvp in activePositions)
                        {
                            if (stopOrders.ContainsKey(kvp.Key) && stopOrders[kvp.Key] == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(FormatString("⚠️ Position {0} is UNPROTECTED: {1} {2} contracts @ {3:F2}", 
                                    kvp.Key,
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                    pos.RemainingContracts,
                                    pos.EntryPrice));
                                
                                // Attempt to re-submit stop with adjusted price
                                Print(FormatString("Attempting to re-submit stop for {0}...", kvp.Key));
                                UpdateStopQuantity(kvp.Key, pos);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                // Check for EXTERNAL close (position went flat from outside strategy)
                if (marketPosition == MarketPosition.Flat && activePositions.Count > 0)
                {
                    // Check if we still have any positions that think they're filled
                    List<string> positionsToCleanup = new List<string>();

                    foreach (var kvp in activePositions)
                    {
                        PositionInfo pos = kvp.Value;
                        if (pos.EntryFilled && pos.RemainingContracts > 0)
                        {
                            Print("EXTERNAL CLOSE DETECTED - Position went flat. Cancelling orphaned orders...");

                            // Cancel orphaned stop orders
                            if (stopOrders.ContainsKey(kvp.Key))
                            {
                                Order stopOrder = stopOrders[kvp.Key];
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                }
                            }

                            // Cancel orphaned target orders
                            if (target1Orders.ContainsKey(kvp.Key))
                            {
                                Order t1Order = target1Orders[kvp.Key];
                                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t1Order);
                                }
                            }

                            if (target2Orders.ContainsKey(kvp.Key))
                            {
                                Order t2Order = target2Orders[kvp.Key];
                                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t2Order);
                                }
                            }

                            positionsToCleanup.Add(kvp.Key);
                        }
                    }

                    // REMOVED v5.7: DO NOT cancel unrelated pending entry orders!
                    // The old logic here cancelled ALL pending entries when position went flat,
                    // which incorrectly cancelled opposite-side OR entries (e.g., ORShort when ORLong closed)
                    // Pending entries should remain active - they are independent trades!

                    // Clean up positions
                    foreach (string key in positionsToCleanup)
                    {
                        CleanupPosition(key);
                    }

                    if (positionsToCleanup.Count > 0)
                    {
                        Print("Cleanup complete - Strategy still running, ready for new entries.");
                    }
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
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
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET SUBMITTED: Stop@{1:F2}", tradeType, validatedStopPrice);
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

        private void UpdateStopQuantity(string entryName, PositionInfo pos)
        {
            if (!stopOrders.ContainsKey(entryName)) return;
            if (pos.RemainingContracts <= 0) return;

            try
            {
                Order currentStop = stopOrders[entryName];

                // Cancel old stop
                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    CancelOrder(currentStop);
                }

                // Submit new stop with updated quantity
                Order newStop = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, pos.CurrentStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, pos.CurrentStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks);

                // CRITICAL v5.8: Validate order was created
                if (newStop == null)
                {
                    Print(FormatString("⚠️ CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(FormatString("⚠️ POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}", 
                        pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", 
                        pos.RemainingContracts, 
                        pos.EntryPrice));
                    
                    // Attempt to flatten position immediately
                    Print(FormatString("⚠️ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                Print(FormatString("STOP QTY UPDATED: {0} contracts @ {1:F2} (Order: {2})", 
                    pos.RemainingContracts, 
                    pos.CurrentStopPrice,
                    newStop.Name));
            }
            catch (Exception ex)
            {
                Print(FormatString("⚠️ ERROR UpdateStopQuantity for {0}: {1}", entryName, ex.Message));
                Print(FormatString("⚠️ POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice)
        {
            double currentPrice = Close[0];
            double minDistance = 2 * tickSize;

            if (direction == MarketPosition.Long)
            {
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

                double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice);

                CancelOrder(currentStop);

                Order newStop = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", "Stop_" + entryName + "_" + DateTime.Now.Ticks);

                stopOrders[entryName] = newStop;
                pos.CurrentStopPrice = validatedStopPrice;
                pos.CurrentTrailLevel = newTrailLevel;

                string levelName = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
                Print(FormatString("STOP UPDATED: {0} → {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
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
            List<string> toRemove = new List<string>();

            foreach (var kvp in activePositions)
            {
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && pos.RemainingContracts <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                CleanupPosition(key);
            }
        }

        private void FlattenAll()
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("FLATTEN: No active positions to close");
                    return;
                }

                Print("FLATTEN: Closing all positions...");

                List<string> positionsToCleanup = new List<string>();

                foreach (var kvp in activePositions)
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (pos.EntryFilled)
                    {
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

                        positionsToCleanup.Add(entryName);
                    }
                    else
                    {
                        // Cancel pending entry order
                        if (entryOrders.ContainsKey(entryName))
                        {
                            Order entryOrder = entryOrders[entryName];
                            if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(entryOrder);
                                Print(FormatString("FLATTEN: Cancelled pending {0} entry order @ {1:F2}",
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", pos.EntryPrice));
                            }
                        }
                        positionsToCleanup.Add(entryName);
                    }
                }

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

        private void FlattenPositionByName(string entryName)
        {
            if (!activePositions.ContainsKey(entryName)) return;
            
            PositionInfo pos = activePositions[entryName];
            
            if (pos.EntryFilled && pos.RemainingContracts > 0)
            {
                Print(FormatString("⚠️ EMERGENCY FLATTEN: Closing {0} position due to stop order failure", entryName));
                
                Order flattenOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "EmergencyFlatten_" + entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "EmergencyFlatten_" + entryName);
                
                if (flattenOrder != null)
                {
                    Print(FormatString("Emergency flatten order submitted: {0} {1} contracts at MARKET", 
                        pos.Direction == MarketPosition.Long ? "SELL" : "BUY", 
                        pos.RemainingContracts));
                }
                else
                {
                    Print(FormatString("⚠️⚠️⚠️ CRITICAL: Emergency flatten order FAILED for {0}!", entryName));
                    Print("⚠️⚠️⚠️ MANUAL INTERVENTION REQUIRED - Close position manually in NinjaTrader!");
                }
            }
        }


        private void CleanupPosition(string entryName)
        {
            // CRITICAL FIX v5.5: Cancel ALL working orders matching entry name pattern
            // This prevents stranded stop orders when trailing stops create multiple stop orders

            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // ENHANCED: Cancel ALL stop orders matching this entry name pattern
            // This handles cases where UpdateStopOrder created multiple stop orders with timestamps
            if (Account != null && Account.Orders != null)
            {
                foreach (Order order in Account.Orders)
                {
                    if (order == null) continue;
                    
                    // CRITICAL FIX v5.6: Use StartsWith() instead of Contains() to prevent cancelling unrelated orders
            // Example: "ORLong" should NOT match "ORShort" - they are different trades!
            // This matches: "ORLong_123", "Stop_ORLong_123", "T1_ORLong_123" but NOT "ORShort_456"
            if ((order.Name.StartsWith(entryName) || order.Name.Contains("_" + entryName)) && 
                (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted))
                    {
                        CancelOrder(order);
                        
                        // Track what we cancelled for logging
                        if (order.Name.StartsWith("Stop_"))
                            cancelledStops++;
                        else if (order.Name.StartsWith("T1_"))
                            cancelledTargets++;
                        else if (order.Name.StartsWith("T2_"))
                            cancelledTargets++;
                        else if (order.Name == entryName)
                            cancelledEntries++;
                        
                        Print(FormatString("CLEANUP: Cancelled {0} for {1}", order.Name, entryName));
                    }
                }
            }

            // LEGACY: Also cancel orders tracked in dictionaries (defensive redundancy)
            // Cancel stop order if it exists and is working
            if (stopOrders.ContainsKey(entryName))
            {
                Order stopOrder = stopOrders[entryName];
                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                {
                    CancelOrder(stopOrder);
                    // Don't increment counter - already counted above if it was working
                }
            }

            // Cancel T1 order if it exists and is working
            if (target1Orders.ContainsKey(entryName))
            {
                Order t1Order = target1Orders[entryName];
                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                {
                    CancelOrder(t1Order);
                    // Don't increment counter - already counted above if it was working
                }
            }

            // Cancel T2 order if it exists and is working
            if (target2Orders.ContainsKey(entryName))
            {
                Order t2Order = target2Orders[entryName];
                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                {
                    CancelOrder(t2Order);
                    // Don't increment counter - already counted above if it was working
                }
            }

            // Cancel entry order if it exists and is still pending
            if (entryOrders.ContainsKey(entryName))
            {
                Order entryOrder = entryOrders[entryName];
                if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                {
                    CancelOrder(entryOrder);
                    // Don't increment counter - already counted above if it was working
                }
            }

            // Now remove from dictionaries
            activePositions.Remove(entryName);
            entryOrders.Remove(entryName);
            stopOrders.Remove(entryName);
            target1Orders.Remove(entryName);
            target2Orders.Remove(entryName);

            // Log cleanup summary
            if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
            {
                Print(FormatString("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}", 
                    entryName, cancelledStops, cancelledTargets, cancelledEntries));
            }

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
                    Background = PanelBackground,
                    BorderBrush = Brushes.DodgerBlue,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 10, 0, 0),
                    MinWidth = 240
                };

                mainGrid = new System.Windows.Controls.Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Drag handle
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Status
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: OR Info
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Position Summary
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: RMA Mode indicator
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: Long button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6: Short button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: RMA button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 8: Flatten button

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
                    Text = "═══ OR Strategy v5.8 STOP_VALIDATION ═══",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dragHandle.Child = dragLabel;

                statusTextBlock = new TextBlock
                {
                    Text = "OR v5.8 STOP_VALIDATION | Initializing...",
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

                // RMA Mode indicator (hidden by default)
                rmaModeTextBlock = new TextBlock
                {
                    Text = "★ RMA MODE ACTIVE - Click chart to place limit entry ★",
                    Foreground = Brushes.Orange,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 8),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(rmaModeTextBlock, 4);

                // Entry buttons
                longButton = new Button
                {
                    Content = "LONG (L)",
                    Background = new SolidColorBrush(Color.FromRgb(50, 120, 50)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand
                };
                longButton.Click += (s, e) => ExecuteLong();
                Grid.SetRow(longButton, 5);

                shortButton = new Button
                {
                    Content = "SHORT (S)",
                    Background = new SolidColorBrush(Color.FromRgb(150, 50, 50)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand
                };
                shortButton.Click += (s, e) => ExecuteShort();
                Grid.SetRow(shortButton, 6);

                // RMA button (toggle mode for limit entries)
                rmaButton = new Button
                {
                    Content = "RMA (Shift+Click)",
                    Background = RMAInactiveBackground,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand
                };
                rmaButton.Click += (s, e) => {
                    isRMAButtonClicked = !isRMAButtonClicked;
                    isRMAModeActive = isRMAButtonClicked;
                    UpdateRMAModeDisplay();
                };
                Grid.SetRow(rmaButton, 7);

                flattenButton = new Button
                {
                    Content = "FLATTEN ALL (F)",
                    Background = new SolidColorBrush(Color.FromRgb(180, 100, 20)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 2),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand
                };
                flattenButton.Click += (s, e) => FlattenAll();
                Grid.SetRow(flattenButton, 8);

                mainGrid.Children.Add(dragHandle);
                mainGrid.Children.Add(statusTextBlock);
                mainGrid.Children.Add(orInfoBlock);
                mainGrid.Children.Add(positionSummaryBlock);
                mainGrid.Children.Add(rmaModeTextBlock);
                mainGrid.Children.Add(longButton);
                mainGrid.Children.Add(shortButton);
                mainGrid.Children.Add(rmaButton);
                mainGrid.Children.Add(flattenButton);

                mainBorder.Child = mainGrid;

                // Add to chart
                UserControlCollection.Add(mainBorder);

                uiCreated = true;
                Print("UI created - v5.8 STOP_VALIDATION (Enhanced Stop Protection)");
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
                try
                {
                    UserControlCollection.Remove(mainBorder);
                }
                catch { }
            }
            uiCreated = false;
        }

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            dragStartPoint = e.GetPosition(ChartControl);
            originalMargin = mainBorder.Margin;
            ((UIElement)sender).CaptureMouse();
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            Point currentPoint = e.GetPosition(ChartControl);
            double deltaX = currentPoint.X - dragStartPoint.X;
            double deltaY = currentPoint.Y - dragStartPoint.Y;

            mainBorder.Margin = new Thickness(
                originalMargin.Left + deltaX,
                originalMargin.Top + deltaY,
                0, 0
            );
        }

        private void OnDragEnd(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();
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
                // Status
                string status = orComplete ? "OR COMPLETE" : (isInORWindow ? "OR BUILDING" : "WAITING");
                statusTextBlock.Text = FormatString("OR v5.4 PERFORMANCE | {0}", status);

                // OR Info
                if (orComplete)
                {
                    double stopDist = CalculateORStopDistance();
                    double t1Dist = sessionRange * Target1Multiplier;
                    double t2Dist = sessionRange * Target2Multiplier;
                    orInfoBlock.Text = FormatString("H: {0:F2} | L: {1:F2} | R: {2:F2}\nT1: +{3:F2} | T2: +{4:F2} | Stop: -{5:F2}",
                        sessionHigh, sessionLow, sessionRange, t1Dist, t2Dist, stopDist);
                }
                else if (isInORWindow)
                {
                    orInfoBlock.Text = FormatString("Building: H={0:F2} L={1:F2}", sessionHigh, sessionLow);
                }
                else
                {
                    orInfoBlock.Text = "Waiting for OR window...";
                }

                // Position summary
                if (activePositions.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var kvp in activePositions)
                    {
                        PositionInfo pos = kvp.Value;
                        string dir = pos.Direction == MarketPosition.Long ? "L" : "S";
                        string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                        if (pos.EntryFilled)
                        {
                            sb.AppendFormat("{0}[{1}] @ {2:F2} | Stop: {3:F2} | Rem: {4}\n",
                                tradeType, dir, pos.EntryPrice, pos.CurrentStopPrice, pos.RemainingContracts);
                        }
                        else
                        {
                            sb.AppendFormat("{0}[{1}] PENDING @ {2:F2}\n", tradeType, dir, pos.EntryPrice);
                        }
                    }
                    positionSummaryBlock.Text = sb.ToString().TrimEnd();
                    positionSummaryBlock.Foreground = Brushes.Lime;
                }
                else
                {
                    positionSummaryBlock.Text = "No positions";
                    positionSummaryBlock.Foreground = Brushes.Cyan;
                }
            }
            catch { }
        }

        private void UpdateRMAModeDisplay()
        {
            if (!uiCreated) return;

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (isRMAModeActive)
                    {
                        // Show RMA mode indicator
                        rmaModeTextBlock.Visibility = Visibility.Visible;
                        mainBorder.Background = RMAModeActiveBackground;
                        mainBorder.BorderBrush = Brushes.Orange;
                        rmaButton.Background = RMAActiveBackground;
                        rmaButton.Content = "★ RMA ACTIVE ★";
                    }
                    else
                    {
                        // Hide RMA mode indicator
                        rmaModeTextBlock.Visibility = Visibility.Collapsed;
                        mainBorder.Background = PanelBackground;
                        mainBorder.BorderBrush = Brushes.DodgerBlue;
                        rmaButton.Background = RMAInactiveBackground;
                        rmaButton.Content = "RMA (Shift+Click)";
                    }
                }
                catch { }
            });
        }

        private void AttachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
            {
                ChartControl.OwnerChart.PreviewKeyDown += OnKeyDown;
                ChartControl.OwnerChart.PreviewKeyUp += OnKeyUp;
            }
        }

        private void DetachHotkeys()
        {
            if (ChartControl?.OwnerChart != null)
            {
                ChartControl.OwnerChart.PreviewKeyDown -= OnKeyDown;
                ChartControl.OwnerChart.PreviewKeyUp -= OnKeyUp;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L) { ExecuteLong(); e.Handled = true; }
            else if (e.Key == Key.S) { ExecuteShort(); e.Handled = true; }
            else if (e.Key == Key.F) { FlattenAll(); e.Handled = true; }
            // RMA uses Shift+Click (R conflicts with NT search, Ctrl conflicts with chart drag)
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            // No longer using R key for RMA
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
        /// RMA Click-to-Price v5.4: Uses NinjaTrader's NATIVE ChartScale.GetValueByY()
        /// NO CALIBRATION NEEDED - works with any chart size, zoom, or instrument!
        /// </summary>
        private void OnChartClick(object sender, MouseButtonEventArgs e)
        {
            // Check if Shift is held OR RMA button mode is active
            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (!RMAEnabled) return;
            if (!shiftHeld && !isRMAModeActive) return;

            try
            {
                // Don't process clicks on the strategy panel UI
                if (mainBorder != null)
                {
                    Point panelPoint = e.GetPosition(mainBorder);
                    if (panelPoint.X >= 0 && panelPoint.X <= mainBorder.ActualWidth &&
                        panelPoint.Y >= 0 && panelPoint.Y <= mainBorder.ActualHeight)
                    {
                        return; // Click was on panel, ignore
                    }
                }

                // Null checks
                if (ChartControl == null)
                {
                    Print("RMA Error: ChartControl is null");
                    return;
                }

                if (ChartPanel == null)
                {
                    Print("RMA Error: ChartPanel is null");
                    return;
                }

                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // ═══════════════════════════════════════════════════════════════════
                // v5.4: Direct ChartPanel-based price conversion
                // ChartPanel.H includes time axis - effective price area is ~67% of height
                // ═══════════════════════════════════════════════════════════════════

                double clickPrice = 0;

                // Get mouse position directly relative to ChartPanel
                Point mouseInPanel = e.GetPosition(ChartPanel as System.Windows.IInputElement);

                double panelHeight = ChartPanel.H;
                double maxPrice = ChartPanel.MaxValue;
                double minPrice = ChartPanel.MinValue;
                double priceRange = maxPrice - minPrice;

                // CRITICAL: ChartPanel.H includes time axis at bottom
                // The actual price plotting area is approximately 67% of total panel height
                // This percentage is consistent regardless of chart size
                double effectivePriceHeight = panelHeight * 0.667;

                // Clamp Y to valid range
                double yInPanel = mouseInPanel.Y;
                if (yInPanel < 0) yInPanel = 0;
                if (yInPanel > effectivePriceHeight) yInPanel = effectivePriceHeight;

                // Convert: Y=0 is top (maxPrice), Y=effectivePriceHeight is bottom (minPrice)
                double yRatio = yInPanel / effectivePriceHeight;
                clickPrice = maxPrice - (yRatio * priceRange);

                Print(FormatString("RMA v5.4 PERFORMANCE: panelY={0:F1}, panelH={1:F1}, effH={2:F1}, ratio={3:F3}, price={4:F2} (Market={5:F2})",
                    mouseInPanel.Y, panelHeight, effectivePriceHeight, yRatio, clickPrice, currentPrice));

                // Round to tick size
                clickPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);

                // Validate price is reasonable
                double validationMaxPrice = ChartPanel.MaxValue;
                double validationMinPrice = ChartPanel.MinValue;
                double validationPriceRange = validationMaxPrice - validationMinPrice;

                if (clickPrice < validationMinPrice - validationPriceRange || clickPrice > validationMaxPrice + validationPriceRange)
                {
                    Print(FormatString("RMA: Click price {0:F2} outside valid range [{1:F2} - {2:F2}]",
                        clickPrice, validationMinPrice, validationMaxPrice));
                    return;
                }

                Print(FormatString("RMA ENTRY: Price={0:F2} (Market={1:F2})", clickPrice, currentPrice));

                // Execute RMA entry
                ExecuteRMAEntry(clickPrice);

                // If one-shot button mode, deactivate after order
                if (isRMAButtonClicked)
                {
                    isRMAButtonClicked = false;
                    isRMAModeActive = false;
                    UpdateRMAModeDisplay();
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Print("ERROR OnChartClick: " + ex.Message);
                Print("ERROR Stack: " + ex.StackTrace);
            }
        }

        #endregion
    }
}
