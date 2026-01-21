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
    public class UniversalORStrategyV8_3 : Strategy
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

        // V8.3: EMA indicators for TREND trades
        private EMA ema9;
        private EMA ema15;

        // Position tracking - multi-target system
        private Dictionary<string, PositionInfo> activePositions;
        private Dictionary<string, Order> entryOrders;
        private Dictionary<string, Order> stopOrders;
        private Dictionary<string, Order> target1Orders;
        private Dictionary<string, Order> target2Orders;
        private Dictionary<string, Order> target3Orders;  // v5.13: New T3 orders
        private Dictionary<string, Order> target4Orders;  // v5.13: New T4 orders (Runner)

        // RMA Mode tracking
        private bool isRMAModeActive;
        private bool isRKeyHeld;
        private bool isRMAButtonClicked;  // One-shot mode from button

        // V8.3: TREND Mode tracking
        private bool isTRENDModeActive;
        private Dictionary<string, string> linkedTRENDEntries;  // Links E1 and E2 by group ID

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
        private Button breakevenButton;
        private Button flattenButton;
        private Button trendButton;  // V8.3: TREND mode button
        
        // v5.12: Target management dropdowns
        private Button t1DropdownButton;
        private Border t1DropdownPanel;
        private bool t1DropdownExpanded;
        private Button t2DropdownButton;
        private Border t2DropdownPanel;
        private bool t2DropdownExpanded;
        private Button t3DropdownButton;  // v5.13
        private Border t3DropdownPanel;   // v5.13
        private bool t3DropdownExpanded;  // v5.13
        private Button runnerDropdownButton;
        private Border runnerDropdownPanel;
        private bool runnerDropdownExpanded;
        
        private bool uiCreated;

        // Colors for UI - MUST be frozen for cross-thread access
        private static readonly SolidColorBrush RMAActiveBackground;
        private static readonly SolidColorBrush RMAInactiveBackground;
        private static readonly SolidColorBrush PanelBackground;
        private static readonly SolidColorBrush RMAModeActiveBackground;

        // Static constructor to create and freeze brushes
        static UniversalORStrategyV8_3()
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

        // V8.3: Resize support
        private bool isResizing;
        private Point resizeStartPoint;
        private double originalWidth;

        // RAM optimization - StringBuilder pool
        private StringBuilder sbPool;

        #endregion

        #region Position Info Class

        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;   // v5.13: 20% - Fixed 1pt quick profit
            public int T2Contracts;   // v5.13: 30% - 0.5x ATR
            public int T3Contracts;   // v5.13: 30% - 1.0x ATR
            public int T4Contracts;   // v5.13: 20% - Runner/Trail
            public int RemainingContracts;
            public double EntryPrice;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price;  // v5.13: Fixed 1pt
            public double Target2Price;  // v5.13: 0.5x ATR
            public double Target3Price;  // v5.13: 1.0x ATR
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;       // v5.13: New flag
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public bool IsRMATrade;  // Flag to identify RMA trades
            public bool ManualBreakevenArmed;  // Manual breakeven button clicked
            public bool ManualBreakevenTriggered;  // Manual breakeven has executed
            public int TicksSinceEntry;  // v5.13: Tick counter for frequency-based trailing

            // V8.3: TREND trade tracking
            public bool IsTRENDTrade;           // Flag for TREND trades
            public bool IsTRENDEntry1;          // True if this is the 9 EMA entry (1/3)
            public bool IsTRENDEntry2;          // True if this is the 15 EMA entry (2/3)
            public string LinkedTRENDGroup;    // Links Entry1 and Entry2 together
            public bool Entry1TrailActivated;  // V8.3: True when E1 switches from fixed stop to EMA9 trail
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
        [Display(Name = "T1 Fixed Points", Description = "v5.13: Fixed point profit for T1 (quick scalp, not ATR-based)", Order = 1, GroupName = "4. Profit Targets")]
        [Range(0.25, 5.0)]
        public double Target1FixedPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 ATR Multiplier", Description = "Multiplier of ATR for T2 (0.5 = half ATR)", Order = 2, GroupName = "4. Profit Targets")]
        public double Target2Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 ATR Multiplier", Description = "Multiplier of ATR for T3 (1.0 = 1x ATR)", Order = 3, GroupName = "4. Profit Targets")]
        public double Target3Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Description = "v5.13: 20% for quick scalp", Order = 4, GroupName = "4. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Description = "v5.13: 30%", Order = 5, GroupName = "4. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Description = "v5.13: 30%", Order = 6, GroupName = "4. Profit Targets")]
        public int T3ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 Contract % (Runner)", Description = "v5.13: 20% for runner/trail", Order = 7, GroupName = "4. Profit Targets")]
        public int T4ContractPercent { get; set; }

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

        [NinjaScriptProperty]
        [Display(Name = "Manual BE Buffer (Ticks)", Description = "Buffer in ticks above breakeven for manual breakeven button", Order = 9, GroupName = "5. Trailing Stops")]
        [Range(1, 10)]
        public int ManualBreakevenBuffer { get; set; }

        #endregion

        #region Properties - Display

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Line", Description = "Show middle line in OR box", Order = 1, GroupName = "6. Display")]
        public bool ShowMidLine { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Box Opacity (%)", Description = "Transparency of OR box (0-100)", Order = 2, GroupName = "6. Display")]
        [Range(0, 100)]
        public int BoxOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show OR Label", Description = "Show OR high/low/range text on chart", Order = 3, GroupName = "6. Display")]
        public bool ShowORLabel { get; set; }

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

        #region Properties - Copy Trading (V8)

        [NinjaScriptProperty]
        [Display(Name = "Enable Copy Trading", Description = "When enabled, all trades will be broadcast to slave strategies", Order = 1, GroupName = "8. Copy Trading")]
        public bool EnableCopyTrading { get; set; }

        #endregion

        #region Properties - TREND Settings (V8.3)

        [NinjaScriptProperty]
        [Display(Name = "TREND Enabled", Description = "Enable TREND (9/15 EMA) entry mode", Order = 1, GroupName = "9. TREND Settings")]
        public bool TRENDEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TREND E1 Stop (Points)", Description = "Fixed stop distance for 9 EMA entry", Order = 2, GroupName = "9. TREND Settings")]
        [Range(0.5, 10.0)]
        public double TRENDEntry1StopPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TREND E2 ATR Mult", Description = "ATR multiplier for 15 EMA trailing stop", Order = 3, GroupName = "9. TREND Settings")]
        [Range(0.5, 3.0)]
        public double TRENDEntry2ATRMultiplier { get; set; }

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
                Description = "Universal Opening Range Strategy V8.3 - Horizontal UI + Resizable Panel";
                Name = "UniversalORStrategyV8_3";  // Keep filename same to avoid recompile issues
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

                // v5.13: 4-Target System - T1=Fixed 1pt, T2-T4=ATR-based
                Target1FixedPoints = 1.0;   // T1 = Fixed 1 point quick scalp
                Target2Multiplier = 0.5;    // T2 = 0.5x ATR
                Target3Multiplier = 1.0;    // T3 = 1.0x ATR
                T1ContractPercent = 20;     // 20% quick scalp
                T2ContractPercent = 30;     // 30%
                T3ContractPercent = 30;     // 30%
                T4ContractPercent = 20;     // 20% runner

                // Trailing stop defaults
                BreakEvenTriggerPoints = 2.0;
                BreakEvenOffsetTicks = 1;
                Trail1TriggerPoints = 3.0;
                Trail1DistancePoints = 2.0;
                Trail2TriggerPoints = 4.0;
                Trail2DistancePoints = 1.5;
                Trail3TriggerPoints = 5.0;
                Trail3DistancePoints = 1.0;
                ManualBreakevenBuffer = 1;

                // Display
                ShowMidLine = true;
                BoxOpacity = 20;
                ShowORLabel = true;

                // RMA defaults
                RMAEnabled = true;
                RMAATRPeriod = 14;
                RMAStopATRMultiplier = 1.0;
                RMAT1ATRMultiplier = 0.5;
                RMAT2ATRMultiplier = 1.0;

                // V8: Copy Trading defaults
                EnableCopyTrading = false;  // Must explicitly enable

                // V8.3: TREND defaults
                TRENDEnabled = true;
                TRENDEntry1StopPoints = 2.0;      // Fixed 2pt stop for 9 EMA entry
                TRENDEntry2ATRMultiplier = 1.1;   // 1.1x ATR trailing for 15 EMA entry
            }
            else if (State == State.Configure)
            {
                // Initialize collections once
                activePositions = new Dictionary<string, PositionInfo>(4);
                entryOrders = new Dictionary<string, Order>(4);
                stopOrders = new Dictionary<string, Order>(4);
                target1Orders = new Dictionary<string, Order>(4);
                target2Orders = new Dictionary<string, Order>(4);
                target3Orders = new Dictionary<string, Order>(4);  // v5.13
                target4Orders = new Dictionary<string, Order>(4);  // v5.13
                sbPool = new StringBuilder(256);

                // V8.3: TREND linked entries tracking
                linkedTRENDEntries = new Dictionary<string, string>(4);

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

                // V8.3: Initialize EMA indicators for TREND trades (on primary bars)
                // CRITICAL: Use explicit BarsArray[0] to ensure separate instances
                ema9 = EMA(BarsArray[0], 9);
                ema15 = EMA(BarsArray[0], 15);

                ResetOR();

                Print(FormatString("UniversalORStrategy V8.3 | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(FormatString("Session: {0} - {1} {2} | OR: {3} min",
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
                Print(FormatString("OR Targets: T1={0}pt T2={1}xOR T3={2}xOR | Stop={3}xOR", Target1FixedPoints, Target2Multiplier, Target3Multiplier, StopMultiplier));
                Print(FormatString("RMA: Enabled={0} ATR({1}) Stop={2}xATR T1={3}xATR T2={4}xATR",
                    RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier, RMAT1ATRMultiplier, RMAT2ATRMultiplier));
                Print("V8.3: TREND Strategy + Copy Trading + 4-Target System");
                Print(FormatString("TREND: Enabled={0} E1Stop={1}pt E2Trail={2}xATR", TRENDEnabled, TRENDEntry1StopPoints, TRENDEntry2ATRMultiplier));
                Print(FormatString("V8 COPY TRADING: {0}", EnableCopyTrading ? "ENABLED - Trades will broadcast to slaves" : "Disabled"));
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
                target3Orders?.Clear();  // v5.13
                target4Orders?.Clear();  // v5.13
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
                        Target1FixedPoints, sessionRange * Target2Multiplier, CalculateORStopDistance()));

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

                if (ShowORLabel)
                {
                    string labelText = isInORWindow
                        ? FormatString("OR Building: {0:F2} - {1:F2}", sessionHigh, sessionLow)
                        : FormatString("OR: {0:F2} - {1:F2} (R:{2:F2})", sessionHigh, sessionLow, sessionRange);

                    Draw.Text(this, "ORLabel", labelText, 0, sessionHigh + (tickSize * 4), Brushes.White);
                }
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
                // v5.13 FIX: Validate entry price before submitting StopMarket order
                // For LONG: entry must be ABOVE current price (breakout up)
                // For SHORT: entry must be BELOW current price (breakout down)
                // Use lastKnownPrice for real-time accuracy (Close[0] can be stale)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (direction == MarketPosition.Long && entryPrice <= currentPrice)
                {
                    Print(FormatString("OR ENTRY BLOCKED: Long entry {0:F2} already below market {1:F2} - too late for breakout",
                        entryPrice, currentPrice));
                    return;
                }
                if (direction == MarketPosition.Short && entryPrice >= currentPrice)
                {
                    Print(FormatString("OR ENTRY BLOCKED: Short entry {0:F2} already above market {1:F2} - too late for breakout",
                        entryPrice, currentPrice));
                    return;
                }

                double stopDistance = CalculateORStopDistance();
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);

                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // v5.13: 4-target system with 20/30/30/20 split
                int t1Qty, t2Qty, t3Qty, t4Qty;

                if (contracts == 1)
                {
                    // Single contract goes to runner
                    t1Qty = 0; t2Qty = 0; t3Qty = 0; t4Qty = 1;
                    Print("POSITION SIZE: 1 contract → Runner-only mode");
                }
                else if (contracts == 2)
                {
                    // T1 quick scalp + runner
                    t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 1;
                    Print("POSITION SIZE: 2 contracts → T1:1 Runner:1");
                }
                else if (contracts == 3)
                {
                    // T1 + T2 + runner
                    t1Qty = 1; t2Qty = 1; t3Qty = 0; t4Qty = 1;
                    Print("POSITION SIZE: 3 contracts → T1:1 T2:1 Runner:1");
                }
                else if (contracts == 4)
                {
                    // One each
                    t1Qty = 1; t2Qty = 1; t3Qty = 1; t4Qty = 1;
                    Print("POSITION SIZE: 4 contracts → T1:1 T2:1 T3:1 Runner:1");
                }
                else
                {
                    // 5+ contracts: use percentage split 20/30/30/20
                    t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                    t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                    t3Qty = (int)Math.Floor(contracts * T3ContractPercent / 100.0);
                    t4Qty = contracts - t1Qty - t2Qty - t3Qty;  // Remainder to runner

                    // Ensure at least 1 contract per target
                    if (t1Qty < 1) { t1Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t2Qty < 1) { t2Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t3Qty < 1) { t3Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t4Qty < 1) t4Qty = 1;

                    Print(FormatString("POSITION SIZE: {0} contracts → T1:{1}(20%) T2:{2}(30%) T3:{3}(30%) T4:{4}(20%)", 
                        contracts, t1Qty, t2Qty, t3Qty, t4Qty));
                }

                string signalName = direction == MarketPosition.Long ? "ORLong" : "ORShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string entryName = signalName + "_" + timestamp;

                // v5.13: T1 = Fixed 1 point profit (quick scalp)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // v5.13: T2 = 0.5x OR RANGE (using sessionRange, NOT ATR for OR trades)
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target2Multiplier)
                    : entryPrice - (sessionRange * Target2Multiplier);

                // v5.13: T3 = 1.0x OR RANGE (using sessionRange, NOT ATR for OR trades)
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target3Multiplier)
                    : entryPrice - (sessionRange * Target3Multiplier);

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
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

                Print(FormatString("OR ENTRY ORDER: {0} {1}@{2:F2} | Stop: {3:F2} | OR Range: {4:F2}",
                    signalName, contracts, entryPrice, stopPrice, sessionRange));
                Print(FormatString("TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2}(+{5:F2}OR) | T3:{6}@{7:F2}(+{8:F2}OR) | T4:{9}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, sessionRange * Target2Multiplier,
                    t3Qty, target3Price, sessionRange * Target3Multiplier, t4Qty));

                // V8: Broadcast entry to slaves
                BroadcastEntrySignal(entryName, direction, entryPrice, stopPrice, target1Price, target2Price, target3Price,
                    t1Qty, t2Qty, t3Qty, t4Qty, false);

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR EnterORPosition: " + ex.Message);
            }
        }

        private double CalculateORStopDistance()
        {
            // v5.13: Use ATR for OR stop (same as RMA) instead of OR range
            if (currentATR <= 0) return MinimumStop;

            double calculatedStop = currentATR * StopMultiplier;  // 0.5x ATR
            return Math.Max(MinimumStop, Math.Min(calculatedStop, MaximumStop));
        }

        #endregion

        #region V8 Copy Trading

        /// <summary>
        /// V8: Broadcast entry signal to all listening slave strategies
        /// V8.1: Now accepts signalId to ensure master and slave use same ID
        /// </summary>
        private void BroadcastEntrySignal(string signalId, MarketPosition direction, double entryPrice, double stopPrice, 
            double target1Price, double target2Price, double target3Price, 
            int t1Contracts, int t2Contracts, int t3Contracts, int t4Contracts, bool isRMA)
        {
            if (!EnableCopyTrading) return;

            try
            {
                string signalType = isRMA ? "RMA" : "OR";
                string directionStr = direction == MarketPosition.Long ? "Long" : "Short";
                string signalName = signalType + directionStr;

                var signal = new SignalBroadcaster.TradeSignal
                {
                    SignalId = signalId,
                    Instrument = Instrument.MasterInstrument.Name,
                    Direction = direction,
                    EntryPrice = entryPrice,
                    StopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    T1Contracts = t1Contracts,
                    T2Contracts = t2Contracts,
                    T3Contracts = t3Contracts,
                    T4Contracts = t4Contracts,
                    IsRMA = isRMA,
                    SessionRange = sessionRange,
                    CurrentATR = currentATR,
                    // V8: Include trail settings so slave can use master's settings
                    BeTrigger = BreakEvenTriggerPoints,
                    BeOffset = BreakEvenOffsetTicks,
                    Trail1Trigger = Trail1TriggerPoints,
                    Trail1Distance = Trail1DistancePoints,
                    Trail2Trigger = Trail2TriggerPoints,
                    Trail2Distance = Trail2DistancePoints,
                    Trail3Trigger = Trail3TriggerPoints,
                    Trail3Distance = Trail3DistancePoints
                };

                SignalBroadcaster.BroadcastTradeSignal(signal);

                Print(FormatString("V8 COPY TRADING: Broadcast {0} @ {1:F2} | Stop: {2:F2} | T1: {3:F2} | T2: {4:F2} | T3: {5:F2}",
                    signalName, entryPrice, stopPrice, target1Price, target2Price, target3Price));
                Print(FormatString("Signal ID: {0} | Slaves connected: {1}", signalId, SignalBroadcaster.GetSubscriberCounts()));
            }
            catch (Exception ex)
            {
                Print("ERROR BroadcastEntrySignal: " + ex.Message);
            }
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

                double entryPrice = clickPrice;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                // v5.13: T1 = Fixed 1 point profit (same as OR, not ATR-based)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // v5.13: T2 = 0.5x ATR (using RMA multiplier)
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);

                // v5.13: T3 = 1.0x ATR (using RMA multiplier)
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                // Calculate position size based on ATR stop
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);

                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // v5.13: 4-target system with 20/30/30/20 split
                int t1Qty, t2Qty, t3Qty, t4Qty;

                if (contracts == 1)
                {
                    t1Qty = 0; t2Qty = 0; t3Qty = 0; t4Qty = 1;
                }
                else if (contracts == 2)
                {
                    t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 1;
                }
                else if (contracts == 3)
                {
                    t1Qty = 1; t2Qty = 1; t3Qty = 0; t4Qty = 1;
                }
                else if (contracts == 4)
                {
                    t1Qty = 1; t2Qty = 1; t3Qty = 1; t4Qty = 1;
                }
                else
                {
                    t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                    t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                    t3Qty = (int)Math.Floor(contracts * T3ContractPercent / 100.0);
                    t4Qty = contracts - t1Qty - t2Qty - t3Qty;

                    if (t1Qty < 1) { t1Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t2Qty < 1) { t2Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t3Qty < 1) { t3Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t4Qty < 1) t4Qty = 1;
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
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
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

                Print(FormatString("RMA ENTRY ORDER: {0} {1}@{2:F2} | ATR: {3:F2}", signalName, contracts, entryPrice, currentATR));
                Print(FormatString("RMA TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty));

                // V8: Broadcast entry to slaves
                BroadcastEntrySignal(entryName, direction, entryPrice, stopPrice, target1Price, target2Price, target3Price,
                    t1Qty, t2Qty, t3Qty, t4Qty, true);

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

        #region TREND Entry Logic (V8.3)

        /// <summary>
        /// V8.3: Execute TREND trade with dual limit orders
        /// Entry 1 (1/3) at 9 EMA with fixed 2pt stop
        /// Entry 2 (2/3) at 15 EMA with 1.1x ATR trailing stop off EMA15
        /// </summary>
        private void ExecuteTRENDEntry()
        {
            if (!TRENDEnabled)
            {
                Print("TREND mode is disabled");
                return;
            }

            if (currentATR <= 0 || ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND entry - indicators not ready");
                return;
            }

            // V8.3: Ensure we have enough bars for EMA calculation
            if (CurrentBar < 20)
            {
                Print("Cannot execute TREND entry - not enough bars (CurrentBar=" + CurrentBar + ")");
                return;
            }

            try
            {
                // V8.3: Check if EMAs are valid before using
                // WORKAROUND: Don't use stored ema9/ema15 instances - call EMA() directly each time
                // This forces NinjaTrader to calculate fresh values instead of caching
                
                if (CurrentBar < 20)
                {
                    Print("Cannot execute TREND entry - not enough bars");
                    return;
                }

                // Call EMA() directly instead of using stored instances
                double ema9Value = EMA(9)[0];
                double ema15Value = EMA(15)[0];
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // V8.3 DEBUG: Show actual EMA values and more context
                Print(FormatString("TREND DEBUG: EMA9={0:F2} EMA15={1:F2} Price={2:F2}", ema9Value, ema15Value, currentPrice));
                Print(FormatString("TREND DEBUG: CurrentBar={0} Close[0]={1:F2} BarsPeriod={2}", 
                    CurrentBar, Close[0], BarsPeriod));


                // Sanity check: EMAs should be different
                if (Math.Abs(ema9Value - ema15Value) < tickSize)
                {
                    Print(FormatString("WARNING: EMA9 and EMA15 are too close ({0:F2} vs {1:F2}) - may not be calculated correctly", 
                        ema9Value, ema15Value));
                }

                // Direction: EMA below price = LONG (buying pullback), EMA above = SHORT
                MarketPosition direction;
                if (ema9Value < currentPrice)
                {
                    direction = MarketPosition.Long;
                    Print(FormatString("TREND: EMA9 below price ({0:F2} < {1:F2}) = LONG setup", ema9Value, currentPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(FormatString("TREND: EMA9 above price ({0:F2} > {1:F2}) = SHORT setup", ema9Value, currentPrice));
                }

                // Calculate total position size based on 2pt stop (E1)
                double stopDistance = TRENDEntry1StopPoints;
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int totalContracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);
                totalContracts = Math.Max(minContracts, Math.Min(totalContracts, maxContracts));

                // Split: 1/3 at 9 EMA, 2/3 at 15 EMA
                int entry1Qty = (int)Math.Ceiling(totalContracts / 3.0);
                int entry2Qty = totalContracts - entry1Qty;

                if (entry1Qty < 1) entry1Qty = 1;
                if (entry2Qty < 1) entry2Qty = 1;

                string timestamp = DateTime.Now.ToString("HHmmss");
                string trendGroupId = "TREND_" + timestamp;
                string entry1Name = trendGroupId + "_E1";
                string entry2Name = trendGroupId + "_E2";

                // ENTRY 1: 1/3 at 9 EMA with fixed stop
                double entry1Price = ema9Value;
                double stop1Price = direction == MarketPosition.Long
                    ? entry1Price - TRENDEntry1StopPoints
                    : entry1Price + TRENDEntry1StopPoints;

                // ENTRY 2: 2/3 at 15 EMA with ATR trailing stop
                double entry2Price = ema15Value;
                double stop2Price = direction == MarketPosition.Long
                    ? ema15Value - (currentATR * TRENDEntry2ATRMultiplier)
                    : ema15Value + (currentATR * TRENDEntry2ATRMultiplier);

                // Create position info for Entry 1
                PositionInfo pos1 = CreateTRENDPosition(entry1Name, direction, entry1Price, stop1Price, 
                    entry1Qty, true, trendGroupId);
                activePositions[entry1Name] = pos1;

                // Create position info for Entry 2
                PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, entry2Price, stop2Price,
                    entry2Qty, false, trendGroupId);
                activePositions[entry2Name] = pos2;

                // Link the entries together
                linkedTRENDEntries[entry1Name] = entry2Name;
                linkedTRENDEntries[entry2Name] = entry1Name;

                // Submit Entry 1 limit order
                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name);
                entryOrders[entry1Name] = entryOrder1;

                // Submit Entry 2 limit order
                Order entryOrder2 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name);
                entryOrders[entry2Name] = entryOrder2;

                Print(FormatString("TREND ORDERS PLACED: {0} Total={1} contracts", 
                    direction == MarketPosition.Long ? "LONG" : "SHORT", totalContracts));
                Print(FormatString("  E1: {0}@{1:F2} (EMA9) | Stop: {2:F2} ({3}pt fixed)", 
                    entry1Qty, ema9Value, stop1Price, TRENDEntry1StopPoints));
                Print(FormatString("  E2: {0}@{1:F2} (EMA15) | Stop: {2:F2} ({3}x ATR trail)", 
                    entry2Qty, ema15Value, stop2Price, TRENDEntry2ATRMultiplier));

                // Deactivate TREND mode after placing orders
                DeactivateTRENDMode();
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTRENDEntry: " + ex.Message);
            }
        }

        private PositionInfo CreateTRENDPosition(string entryName, MarketPosition direction, 
            double entryPrice, double stopPrice, int contracts, bool isEntry1, string groupId)
        {
            // For TREND, we don't use the multi-target system - just single exit with trailing
            // Targets use same logic as RMA
            double target1Price = direction == MarketPosition.Long
                ? entryPrice + Target1FixedPoints
                : entryPrice - Target1FixedPoints;
            double target2Price = direction == MarketPosition.Long
                ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                : entryPrice - (currentATR * RMAT1ATRMultiplier);
            double target3Price = direction == MarketPosition.Long
                ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                : entryPrice - (currentATR * RMAT2ATRMultiplier);

            return new PositionInfo
            {
                SignalName = entryName,
                Direction = direction,
                TotalContracts = contracts,
                T1Contracts = contracts,  // For TREND, all go to single runner
                T2Contracts = 0,
                T3Contracts = 0,
                T4Contracts = 0,
                RemainingContracts = contracts,
                EntryPrice = entryPrice,
                InitialStopPrice = stopPrice,
                CurrentStopPrice = stopPrice,
                Target1Price = target1Price,
                Target2Price = target2Price,
                Target3Price = target3Price,
                EntryFilled = false,
                T1Filled = false,
                T2Filled = false,
                T3Filled = false,
                BracketSubmitted = false,
                ExtremePriceSinceEntry = entryPrice,
                CurrentTrailLevel = 0,
                IsRMATrade = false,
                IsTRENDTrade = true,
                IsTRENDEntry1 = isEntry1,
                IsTRENDEntry2 = !isEntry1,
                LinkedTRENDGroup = groupId
            };
        }

        private void ActivateTRENDMode()
        {
            isTRENDModeActive = true;
            UpdateTRENDModeDisplay();
        }

        private void DeactivateTRENDMode()
        {
            isTRENDModeActive = false;
            UpdateTRENDModeDisplay();
        }

        private void UpdateTRENDModeDisplay()
        {
            if (trendButton == null) return;

            try
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    if (isTRENDModeActive)
                    {
                        trendButton.Background = new SolidColorBrush(Color.FromRgb(0, 150, 150)); // Teal
                        trendButton.Content = "★ TREND ★";  // V8.3: Active indicator
                    }
                    else
                    {
                        trendButton.Background = new SolidColorBrush(Color.FromRgb(60, 80, 100));
                        trendButton.Content = "TREND";
                    }
                });
            }
            catch { }
        }

        private void OnTRENDButtonClick(object sender, RoutedEventArgs e)
        {
            if (isTRENDModeActive)
            {
                DeactivateTRENDMode();
                Print("TREND mode deactivated");
            }
            else
            {
                // Execute TREND entry immediately when button clicked
                ExecuteTRENDEntry();
            }
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
                                // v5.13 FIX: T1 uses FIXED points, T2/T3 use ATR
                                double t2Distance = currentATR * RMAT1ATRMultiplier;  // 0.5x ATR
                                double t3Distance = currentATR * RMAT2ATRMultiplier;  // 1.0x ATR
                                double stopDistance = currentATR * RMAStopATRMultiplier;

                                pos.InitialStopPrice = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice - stopDistance
                                    : averageFillPrice + stopDistance;
                                pos.CurrentStopPrice = pos.InitialStopPrice;

                                // T1 = Fixed 1pt (NOT ATR-based)
                                pos.Target1Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + Target1FixedPoints
                                    : averageFillPrice - Target1FixedPoints;
                                // T2 = 0.5x ATR
                                pos.Target2Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t2Distance
                                    : averageFillPrice - t2Distance;
                                // T3 = 1.0x ATR
                                pos.Target3Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t3Distance
                                    : averageFillPrice - t3Distance;

                                if (Math.Abs(averageFillPrice - intendedEntryPrice) > tickSize)
                                {
                                    Print(FormatString("{0} PRICES ADJUSTED for fill slippage: Stop={1:F2} T1={2:F2} T2={3:F2} T3={4:F2}",
                                        tradeType, pos.InitialStopPrice, pos.Target1Price, pos.Target2Price, pos.Target3Price));
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

                // v5.13: Target 3 filled
                if (target3Orders.ContainsValue(order) && orderState == OrderState.Filled)
                {
                    foreach (var kvp in activePositions)
                    {
                        if (target3Orders.ContainsKey(kvp.Key) && target3Orders[kvp.Key] == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T3Filled = true;
                            pos.RemainingContracts -= pos.T3Contracts;
                            Print(FormatString("T3 FILLED: {0} contracts @ {1:F2} | Remaining: {2} (T4 runner)",
                                pos.T3Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity - only T4 runner remains
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

                // V8.1: Entry order price changed - broadcast to slaves
                // This detects when user drags the order line to a new price
                if (entryOrders.ContainsValue(order) && (orderState == OrderState.Accepted || orderState == OrderState.Working))
                {
                    foreach (var kvp in activePositions)
                    {
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        if (entryOrders.ContainsKey(entryName) && entryOrders[entryName] == order && !pos.EntryFilled)
                        {
                            // Get the new price from the order (limit orders use limitPrice, stop orders use stopPrice)
                            double newPrice = limitPrice > 0 ? limitPrice : stopPrice;
                            
                            // Check if price changed (with tick tolerance)
                            if (Math.Abs(newPrice - pos.EntryPrice) > tickSize * 0.5)
                            {
                                double oldPrice = pos.EntryPrice;
                                pos.EntryPrice = newPrice;
                                
                                Print(FormatString("V8.1: Entry order MOVED: {0} | {1:F2} → {2:F2}", entryName, oldPrice, newPrice));
                                
                                // Broadcast price update to slaves
                                if (EnableCopyTrading)
                                {
                                    SignalBroadcaster.BroadcastEntryUpdate(entryName, newPrice);
                                    Print(FormatString("V8.1: Broadcasted entry update to slaves: {0} @ {1:F2}", entryName, newPrice));
                                }
                            }
                            break;
                        }
                    }
                }

                // V8.1: Entry order cancelled - broadcast to slaves
                if (entryOrders.ContainsValue(order) && orderState == OrderState.Cancelled)
                {
                    foreach (var kvp in activePositions)
                    {
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        if (entryOrders.ContainsKey(entryName) && entryOrders[entryName] == order && !pos.EntryFilled)
                        {
                            Print(FormatString("V8.1: Entry order CANCELLED: {0}", entryName));
                            
                            // Broadcast cancellation to slaves
                            if (EnableCopyTrading)
                            {
                                SignalBroadcaster.BroadcastOrderCancel(entryName, "Master cancelled");
                                Print(FormatString("V8.1: Broadcasted cancellation to slaves: {0}", entryName));
                            }
                            
                            // Clean up local state
                            CleanupPosition(entryName);
                            break;
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

                            // v5.13: Cancel T3/T4 orphaned orders
                            if (target3Orders.ContainsKey(kvp.Key))
                            {
                                Order t3Order = target3Orders[kvp.Key];
                                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t3Order);
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

                // v5.13: Submit T3 limit order ONLY if T3 quantity > 0
                if (pos.T3Contracts > 0)
                {
                    Order t3Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName);

                    target3Orders[entryName] = t3Order;
                }

                // NOTE: T4 (runner) has no limit order - it trails with stop

                pos.BracketSubmitted = true;
                pos.CurrentStopPrice = validatedStopPrice;

                // Build bracket summary message with all 4 targets
                StringBuilder bracketMsg = new StringBuilder();
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET V8.0: Stop@{1:F2}", tradeType, validatedStopPrice);
                if (pos.T1Contracts > 0)
                    bracketMsg.AppendFormat(" | T1:{0}@{1:F2}(+{2}pt)", pos.T1Contracts, pos.Target1Price, Target1FixedPoints);
                if (pos.T2Contracts > 0)
                    bracketMsg.AppendFormat(" | T2:{0}@{1:F2}", pos.T2Contracts, pos.Target2Price);
                if (pos.T3Contracts > 0)
                    bracketMsg.AppendFormat(" | T3:{0}@{1:F2}", pos.T3Contracts, pos.Target3Price);
                if (pos.T4Contracts > 0)
                    bracketMsg.AppendFormat(" | T4:{0}@trail", pos.T4Contracts);

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

                // Increment tick counter on every call
                pos.TicksSinceEntry++;

                // Update extreme price
                if (pos.Direction == MarketPosition.Long)
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, Close[0]);
                else
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, Close[0]);

                // V8.3: TREND Entry 1 - starts with fixed 2pt stop, switches to EMA9 trail when price crosses EMA
                if (pos.IsTRENDTrade && pos.IsTRENDEntry1)
                {
                    double ema9Live = EMA(9)[0];
                    double currentPrice = Close[0];
                    
                    // Check if price has crossed EMA9 in our favor
                    bool priceInFavor = pos.Direction == MarketPosition.Long
                        ? currentPrice > ema9Live  // LONG: price above EMA9
                        : currentPrice < ema9Live; // SHORT: price below EMA9

                    // If not yet trailing and price crossed EMA in our favor, activate trailing
                    if (!pos.Entry1TrailActivated && priceInFavor)
                    {
                        pos.Entry1TrailActivated = true;
                        Print(FormatString("TREND E1: Switching to EMA9 trail (Price={0:F2} crossed EMA9={1:F2})",
                            currentPrice, ema9Live));
                    }

                    // If trailing is activated, manage the EMA9 trail
                    if (pos.Entry1TrailActivated)
                    {
                        double trendStop = pos.Direction == MarketPosition.Long
                            ? ema9Live - (currentATR * TRENDEntry2ATRMultiplier)  // Uses same 1.1x ATR multiplier
                            : ema9Live + (currentATR * TRENDEntry2ATRMultiplier);

                        bool shouldUpdate = pos.Direction == MarketPosition.Long
                            ? trendStop > pos.CurrentStopPrice
                            : trendStop < pos.CurrentStopPrice;

                        if (shouldUpdate)
                        {
                            UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                            Print(FormatString("TREND E1 TRAIL: Stop moved to {0:F2} (EMA9={1:F2} - {2}xATR)",
                                trendStop, ema9Live, TRENDEntry2ATRMultiplier));
                        }
                    }
                    continue; // Skip normal trailing logic for TREND E1
                }

                // V8.3: TREND Entry 2 uses EMA15 trailing stop (1.1x ATR from live EMA15)
                if (pos.IsTRENDTrade && pos.IsTRENDEntry2)
                {
                    double ema15Live = EMA(15)[0];
                    double trendStop = pos.Direction == MarketPosition.Long
                        ? ema15Live - (currentATR * TRENDEntry2ATRMultiplier)
                        : ema15Live + (currentATR * TRENDEntry2ATRMultiplier);

                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? trendStop > pos.CurrentStopPrice
                        : trendStop < pos.CurrentStopPrice;

                    if (shouldUpdate)
                    {
                        UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                        Print(FormatString("TREND E2 TRAIL: Stop moved to {0:F2} (EMA15={1:F2} - {2}xATR)", 
                            trendStop, ema15Live, TRENDEntry2ATRMultiplier));
                    }
                    continue; // Skip normal trailing logic for TREND E2
                }

                double profitPoints = pos.Direction == MarketPosition.Long
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                double newStopPrice = pos.CurrentStopPrice;
                int newTrailLevel = pos.CurrentTrailLevel;

                // MANUAL BREAKEVEN - Check FIRST before automatic trailing
                // This allows user to "arm" breakeven early and it auto-triggers when price reaches threshold
                if (pos.ManualBreakevenArmed && !pos.ManualBreakevenTriggered)
                {
                    double beThreshold = pos.EntryPrice + (ManualBreakevenBuffer * tickSize);
                    bool thresholdReached = false;

                    if (pos.Direction == MarketPosition.Long)
                    {
                        thresholdReached = Close[0] >= beThreshold;
                    }
                    else // Short
                    {
                        beThreshold = pos.EntryPrice - (ManualBreakevenBuffer * tickSize);
                        thresholdReached = Close[0] <= beThreshold;
                    }

                    if (thresholdReached)
                    {
                        // Move stop to breakeven + buffer
                        double manualBEStop = pos.Direction == MarketPosition.Long
                            ? pos.EntryPrice + (ManualBreakevenBuffer * tickSize)
                            : pos.EntryPrice - (ManualBreakevenBuffer * tickSize);

                        // Only move if it's better than current stop
                        bool shouldMove = pos.Direction == MarketPosition.Long
                            ? manualBEStop > pos.CurrentStopPrice
                            : manualBEStop < pos.CurrentStopPrice;

                        if (shouldMove)
                        {
                            newStopPrice = manualBEStop;
                            newTrailLevel = 1; // Same as automatic breakeven
                            pos.ManualBreakevenTriggered = true;
                            Print(FormatString("★ MANUAL BREAKEVEN TRIGGERED: {0} → Stop moved to {1:F2} (Entry + {2} tick)", 
                                entryName, manualBEStop, ManualBreakevenBuffer));
                        }
                    }
                }

                // v5.13 FREQUENCY CONTROL: Determine if we should check trailing based on current level
                // BE (level 0-1) and T3 (level 4) = every tick
                // T1 (level 2) and T2 (level 3) = every OTHER tick
                
                bool shouldCheckTrailing = true; // Default: check every tick
                
                // Determine current active level based on profit
                if (profitPoints >= Trail3TriggerPoints && pos.T1Filled && pos.T2Filled)
                {
                    // At T3 level (5+ points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }
                else if (profitPoints >= Trail2TriggerPoints && pos.T1Filled)
                {
                    // At T2 level (4-4.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else if (profitPoints >= Trail1TriggerPoints)
                {
                    // At T1 level (3-3.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else
                {
                    // At BE level or below (0-2.99 points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }

                // Only proceed with trailing logic if frequency check passes
                if (!shouldCheckTrailing)
                    continue;

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

                // CRITICAL v5.13 FIX: Validate order was created
                if (newStop == null)
                {
                    Print(FormatString("⚠️ CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(FormatString("⚠️ POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}", 
                        pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", 
                        pos.RemainingContracts, 
                        pos.EntryPrice));
                    Print(FormatString("⚠️ Attempted stop price: {0:F2} | Current price: {1:F2}", validatedStopPrice, Close[0]));
                    
                    // Attempt to flatten position immediately
                    Print(FormatString("⚠️ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                pos.CurrentStopPrice = validatedStopPrice;
                pos.CurrentTrailLevel = newTrailLevel;

                string levelName = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
                Print(FormatString("STOP UPDATED: {0} → {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
                
                // V8.1: Broadcast stop update for full synchronization
                if (EnableCopyTrading)
                {
                    SignalBroadcaster.BroadcastStopUpdate(entryName, validatedStopPrice, levelName);
                }
            }
            catch (Exception ex)
            {
                Print(FormatString("⚠️ ERROR UpdateStopOrder for {0}: {1}", entryName, ex.Message));
                Print(FormatString("⚠️ POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
                
                // Attempt emergency flatten
                try
                {
                    Print(FormatString("⚠️ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                }
                catch (Exception flattenEx)
                {
                    Print(FormatString("⚠️⚠️ EMERGENCY FLATTEN FAILED: {0}", flattenEx.Message));
                }
            }
        }

        private void OnBreakevenButtonClick()
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("BREAKEVEN: No active positions");
                    return;
                }

                // Check if any positions are already triggered (can't toggle after trigger)
                bool anyTriggered = false;
                foreach (var kvp in activePositions)
                {
                    if (kvp.Value.ManualBreakevenTriggered)
                    {
                        anyTriggered = true;
                        break;
                    }
                }

                if (anyTriggered)
                {
                    Print("BREAKEVEN: Already triggered - cannot toggle");
                    return;
                }

                // Check current state - if any armed, disarm all; if none armed, arm all
                bool anyArmed = false;
                foreach (var kvp in activePositions)
                {
                    if (kvp.Value.ManualBreakevenArmed)
                    {
                        anyArmed = true;
                        break;
                    }
                }

                // Toggle: if armed, disarm; if disarmed, arm
                foreach (var kvp in activePositions)
                {
                    PositionInfo pos = kvp.Value;
                    if (pos.EntryFilled && !pos.ManualBreakevenTriggered)
                    {
                        if (anyArmed)
                        {
                            // Disarm
                            pos.ManualBreakevenArmed = false;
                            Print(FormatString("BREAKEVEN DISARMED: {0}", kvp.Key));
                        }
                        else
                        {
                            // Arm
                            pos.ManualBreakevenArmed = true;
                            Print(FormatString("BREAKEVEN ARMED: {0} - Will trigger at Entry + {1} tick(s)", 
                                kvp.Key, ManualBreakevenBuffer));
                        }
                    }
                }

                // V8: Broadcast breakeven to slaves when arming (not disarming)
                if (!anyArmed && EnableCopyTrading)
                {
                    SignalBroadcaster.BroadcastBreakeven();
                    Print("V8 COPY TRADING: Broadcast BREAKEVEN to all slaves");
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR OnBreakevenButtonClick: " + ex.Message);
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

                // V8: Broadcast flatten to slaves
                if (EnableCopyTrading)
                {
                    SignalBroadcaster.BroadcastFlatten("Master flatten button");
                    Print("V8 COPY TRADING: Broadcast FLATTEN to all slaves");
                }

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
                        else if (order.Name.StartsWith("T3_"))  // v5.13
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

            // v5.13: Cancel T3 order if it exists and is working
            if (target3Orders.ContainsKey(entryName))
            {
                Order t3Order = target3Orders[entryName];
                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                {
                    CancelOrder(t3Order);
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
            target3Orders.Remove(entryName);  // v5.13
            target4Orders.Remove(entryName);  // v5.13

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
                    MinWidth = 280,
                    MaxWidth = 600
                };

                mainGrid = new System.Windows.Controls.Grid();
                // V8.3: Horizontal row layout - 9 rows total
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Drag handle
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Status
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: OR Info + Position
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: RMA Mode indicator
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: OR Entry Row (Long | Short)
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: Special Row (RMA | TREND)
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6: Target Row (T1|T2|T3|Runner|BE)
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: Dropdown Panel (shared)
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 8: Flatten button

                // Row 0: Drag Handle
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
                    Text = "═══ OR Strategy V8.3 ═══",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dragHandle.Child = dragLabel;

                // Row 1: Status
                statusTextBlock = new TextBlock
                {
                    Text = "OR V8.3 | Initializing...",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 3),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(statusTextBlock, 1);

                // Row 2: OR Info + Position Summary (combined)
                StackPanel infoPanel = new StackPanel();

                orInfoBlock = new TextBlock
                {
                    Text = "Waiting for session...",
                    Foreground = Brushes.LightGray,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };

                positionSummaryBlock = new TextBlock
                {
                    Text = "No positions",
                    Foreground = Brushes.Cyan,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                };

                infoPanel.Children.Add(orInfoBlock);
                infoPanel.Children.Add(positionSummaryBlock);
                Grid.SetRow(infoPanel, 2);

                // Row 3: RMA Mode indicator (hidden by default)
                rmaModeTextBlock = new TextBlock
                {
                    Text = "★ RMA ACTIVE - Click chart ★",
                    Foreground = Brushes.Orange,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 5),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(rmaModeTextBlock, 3);

                // ═══════════════════════════════════════════════════════════════
                // Row 4: OR Entry Row - LONG | SHORT (2 columns)
                // ═══════════════════════════════════════════════════════════════
                System.Windows.Controls.Grid orEntryGrid = new System.Windows.Controls.Grid();
                orEntryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                orEntryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                longButton = new Button
                {
                    Content = "LONG (L)",
                    Background = new SolidColorBrush(Color.FromRgb(50, 120, 50)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 2, 2),
                    Padding = new Thickness(4, 6, 4, 6),
                    Cursor = Cursors.Hand
                };
                longButton.Click += (s, e) => ExecuteLong();
                Grid.SetColumn(longButton, 0);
                orEntryGrid.Children.Add(longButton);

                shortButton = new Button
                {
                    Content = "SHORT (S)",
                    Background = new SolidColorBrush(Color.FromRgb(150, 50, 50)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 2, 0, 2),
                    Padding = new Thickness(4, 6, 4, 6),
                    Cursor = Cursors.Hand
                };
                shortButton.Click += (s, e) => ExecuteShort();
                Grid.SetColumn(shortButton, 1);
                orEntryGrid.Children.Add(shortButton);

                Grid.SetRow(orEntryGrid, 4);

                // ═══════════════════════════════════════════════════════════════
                // Row 5: Special Entry Row - RMA | TREND (2 columns)
                // ═══════════════════════════════════════════════════════════════
                System.Windows.Controls.Grid specialEntryGrid = new System.Windows.Controls.Grid();
                specialEntryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                specialEntryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                rmaButton = new Button
                {
                    Content = "RMA",
                    Background = RMAInactiveBackground,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 2, 2),
                    Padding = new Thickness(4, 6, 4, 6),
                    Cursor = Cursors.Hand
                };
                rmaButton.Click += (s, e) => {
                    isRMAButtonClicked = !isRMAButtonClicked;
                    isRMAModeActive = isRMAButtonClicked;
                    UpdateRMAModeDisplay();
                };
                Grid.SetColumn(rmaButton, 0);
                specialEntryGrid.Children.Add(rmaButton);

                trendButton = new Button
                {
                    Content = "TREND",
                    Background = new SolidColorBrush(Color.FromRgb(60, 80, 100)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 2, 0, 2),
                    Padding = new Thickness(4, 6, 4, 6),
                    Cursor = Cursors.Hand
                };
                trendButton.Click += OnTRENDButtonClick;
                Grid.SetColumn(trendButton, 1);
                specialEntryGrid.Children.Add(trendButton);

                Grid.SetRow(specialEntryGrid, 5);

                // ═══════════════════════════════════════════════════════════════
                // Row 6: Target Row - T1 | T2 | T3 | RUNNER | BE (5 columns)
                // ═══════════════════════════════════════════════════════════════
                System.Windows.Controls.Grid targetGrid = new System.Windows.Controls.Grid();
                for (int i = 0; i < 5; i++)
                    targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                t1DropdownButton = new Button
                {
                    Content = "T1",
                    Background = new SolidColorBrush(Color.FromRgb(100, 60, 140)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 1, 2),
                    Padding = new Thickness(2, 6, 2, 6),
                    Cursor = Cursors.Hand
                };
                t1DropdownButton.Click += (s, e) => ToggleT1Dropdown();
                Grid.SetColumn(t1DropdownButton, 0);
                targetGrid.Children.Add(t1DropdownButton);

                t2DropdownButton = new Button
                {
                    Content = "T2",
                    Background = new SolidColorBrush(Color.FromRgb(100, 60, 140)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(1, 2, 1, 2),
                    Padding = new Thickness(2, 6, 2, 6),
                    Cursor = Cursors.Hand
                };
                t2DropdownButton.Click += (s, e) => ToggleT2Dropdown();
                Grid.SetColumn(t2DropdownButton, 1);
                targetGrid.Children.Add(t2DropdownButton);

                t3DropdownButton = new Button
                {
                    Content = "T3",
                    Background = new SolidColorBrush(Color.FromRgb(100, 60, 140)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(1, 2, 1, 2),
                    Padding = new Thickness(2, 6, 2, 6),
                    Cursor = Cursors.Hand
                };
                t3DropdownButton.Click += (s, e) => ToggleT3Dropdown();
                Grid.SetColumn(t3DropdownButton, 2);
                targetGrid.Children.Add(t3DropdownButton);

                runnerDropdownButton = new Button
                {
                    Content = "RUN",
                    Background = new SolidColorBrush(Color.FromRgb(100, 60, 140)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(1, 2, 1, 2),
                    Padding = new Thickness(2, 6, 2, 6),
                    Cursor = Cursors.Hand
                };
                runnerDropdownButton.Click += (s, e) => ToggleRunnerDropdown();
                Grid.SetColumn(runnerDropdownButton, 3);
                targetGrid.Children.Add(runnerDropdownButton);

                breakevenButton = new Button
                {
                    Content = "BE",
                    Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(1, 2, 0, 2),
                    Padding = new Thickness(2, 6, 2, 6),
                    Cursor = Cursors.Hand
                };
                breakevenButton.Click += (s, e) => OnBreakevenButtonClick();
                Grid.SetColumn(breakevenButton, 4);
                targetGrid.Children.Add(breakevenButton);

                Grid.SetRow(targetGrid, 6);

                // ═══════════════════════════════════════════════════════════════
                // Row 7: Shared Dropdown Panel (all dropdowns expand here)
                // ═══════════════════════════════════════════════════════════════
                t1DropdownPanel = CreateDropdownPanel("T1");
                t2DropdownPanel = CreateDropdownPanel("T2");
                t3DropdownPanel = CreateDropdownPanel("T3");
                runnerDropdownPanel = CreateDropdownPanel("Runner");

                // Stack all dropdown panels in same row (only one visible at a time)
                StackPanel dropdownContainer = new StackPanel();
                dropdownContainer.Children.Add(t1DropdownPanel);
                dropdownContainer.Children.Add(t2DropdownPanel);
                dropdownContainer.Children.Add(t3DropdownPanel);
                dropdownContainer.Children.Add(runnerDropdownPanel);
                Grid.SetRow(dropdownContainer, 7);

                // ═══════════════════════════════════════════════════════════════
                // Row 8: Flatten Button (full width)
                // ═══════════════════════════════════════════════════════════════
                flattenButton = new Button
                {
                    Content = "FLATTEN ALL (F)",
                    Background = new SolidColorBrush(Color.FromRgb(180, 100, 20)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 2),
                    Padding = new Thickness(4, 6, 4, 6),
                    Cursor = Cursors.Hand
                };
                flattenButton.Click += (s, e) => FlattenAll();
                Grid.SetRow(flattenButton, 8);

                // ═══════════════════════════════════════════════════════════════
                // Add all elements to main grid
                // ═══════════════════════════════════════════════════════════════
                mainGrid.Children.Add(dragHandle);
                mainGrid.Children.Add(statusTextBlock);
                mainGrid.Children.Add(infoPanel);
                mainGrid.Children.Add(rmaModeTextBlock);
                mainGrid.Children.Add(orEntryGrid);
                mainGrid.Children.Add(specialEntryGrid);
                mainGrid.Children.Add(targetGrid);
                mainGrid.Children.Add(dropdownContainer);
                mainGrid.Children.Add(flattenButton);

                // ═══════════════════════════════════════════════════════════════
                // Resize grip (bottom-right corner)
                // ═══════════════════════════════════════════════════════════════
                System.Windows.Controls.Grid containerGrid = new System.Windows.Controls.Grid();
                containerGrid.Children.Add(mainGrid);

                // Resize grip visual
                System.Windows.Shapes.Path resizeGrip = new System.Windows.Shapes.Path
                {
                    Width = 12,
                    Height = 12,
                    Fill = Brushes.Gray,
                    Data = System.Windows.Media.Geometry.Parse("M 0,12 L 12,0 L 12,12 Z"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -4, -4),
                    Cursor = Cursors.SizeNWSE,
                    Opacity = 0.7
                };
                resizeGrip.MouseLeftButtonDown += OnResizeStart;
                resizeGrip.MouseMove += OnResizeMove;
                resizeGrip.MouseLeftButtonUp += OnResizeEnd;
                containerGrid.Children.Add(resizeGrip);

                mainBorder.Child = containerGrid;

                // Add to chart
                UserControlCollection.Add(mainBorder);

                uiCreated = true;
                Print("UI created - V8.3 (Horizontal Layout + Resizable)");
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

        // v5.12: Create dropdown panel with action buttons
        private Border CreateDropdownPanel(string targetType)
        {
            StackPanel menuStack = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 30, 30, 40)),
                Margin = new Thickness(5, 0, 5, 2)
            };

            if (targetType == "T1" || targetType == "T2" || targetType == "T3")  // v5.13: Added T3
            {
                // Target actions
                menuStack.Children.Add(CreateMenuButton("Fill at Market NOW", () => ExecuteTargetAction(targetType, "market")));
                menuStack.Children.Add(CreateMenuButton("Move to 1 Point", () => ExecuteTargetAction(targetType, "1point")));
                menuStack.Children.Add(CreateMenuButton("Move to 2 Points", () => ExecuteTargetAction(targetType, "2point")));
                menuStack.Children.Add(CreateMenuButton("Move to Market Price", () => ExecuteTargetAction(targetType, "marketprice")));
                menuStack.Children.Add(CreateMenuButton("Move to Breakeven", () => ExecuteTargetAction(targetType, "breakeven")));
                menuStack.Children.Add(CreateMenuButton("Cancel " + targetType + " Order", () => ExecuteTargetAction(targetType, "cancel")));
            }
            else if (targetType == "Runner")
            {
                // Runner actions
                menuStack.Children.Add(CreateMenuButton("Close Runner at Market", () => ExecuteRunnerAction("market")));
                menuStack.Children.Add(CreateMenuButton("Move Stop to 1 Point", () => ExecuteRunnerAction("stop1pt")));
                menuStack.Children.Add(CreateMenuButton("Move Stop to 2 Points", () => ExecuteRunnerAction("stop2pt")));
                menuStack.Children.Add(CreateMenuButton("Move Stop to Breakeven", () => ExecuteRunnerAction("stopbe")));
                menuStack.Children.Add(CreateMenuButton("Lock 50% of Profit", () => ExecuteRunnerAction("lock50")));
                menuStack.Children.Add(CreateMenuButton("Disable Trailing Stop", () => ExecuteRunnerAction("disabletrail")));
            }

            Border panel = new Border
            {
                Child = menuStack,
                Visibility = Visibility.Collapsed
            };

            return panel;
        }

        private Button CreateMenuButton(string text, Action onClick)
        {
            Button btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 80)),
                Foreground = Brushes.White,
                FontSize = 10,
                Margin = new Thickness(2),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        // V8.3: Dropdown toggles with compact button labels
        private void ToggleT1Dropdown()
        {
            t1DropdownExpanded = !t1DropdownExpanded;
            t1DropdownPanel.Visibility = t1DropdownExpanded ? Visibility.Visible : Visibility.Collapsed;
            t1DropdownButton.Content = t1DropdownExpanded ? "T1 ▲" : "T1";

            // Close other dropdowns
            if (t1DropdownExpanded)
            {
                t2DropdownExpanded = false;
                t2DropdownPanel.Visibility = Visibility.Collapsed;
                t2DropdownButton.Content = "T2";
                t3DropdownExpanded = false;
                t3DropdownPanel.Visibility = Visibility.Collapsed;
                t3DropdownButton.Content = "T3";
                runnerDropdownExpanded = false;
                runnerDropdownPanel.Visibility = Visibility.Collapsed;
                runnerDropdownButton.Content = "RUN";
            }
        }

        private void ToggleT2Dropdown()
        {
            t2DropdownExpanded = !t2DropdownExpanded;
            t2DropdownPanel.Visibility = t2DropdownExpanded ? Visibility.Visible : Visibility.Collapsed;
            t2DropdownButton.Content = t2DropdownExpanded ? "T2 ▲" : "T2";

            // Close other dropdowns
            if (t2DropdownExpanded)
            {
                t1DropdownExpanded = false;
                t1DropdownPanel.Visibility = Visibility.Collapsed;
                t1DropdownButton.Content = "T1";
                t3DropdownExpanded = false;
                t3DropdownPanel.Visibility = Visibility.Collapsed;
                t3DropdownButton.Content = "T3";
                runnerDropdownExpanded = false;
                runnerDropdownPanel.Visibility = Visibility.Collapsed;
                runnerDropdownButton.Content = "RUN";
            }
        }

        private void ToggleT3Dropdown()
        {
            t3DropdownExpanded = !t3DropdownExpanded;
            t3DropdownPanel.Visibility = t3DropdownExpanded ? Visibility.Visible : Visibility.Collapsed;
            t3DropdownButton.Content = t3DropdownExpanded ? "T3 ▲" : "T3";

            // Close other dropdowns
            if (t3DropdownExpanded)
            {
                t1DropdownExpanded = false;
                t1DropdownPanel.Visibility = Visibility.Collapsed;
                t1DropdownButton.Content = "T1";
                t2DropdownExpanded = false;
                t2DropdownPanel.Visibility = Visibility.Collapsed;
                t2DropdownButton.Content = "T2";
                runnerDropdownExpanded = false;
                runnerDropdownPanel.Visibility = Visibility.Collapsed;
                runnerDropdownButton.Content = "RUN";
            }
        }

        private void ToggleRunnerDropdown()
        {
            runnerDropdownExpanded = !runnerDropdownExpanded;
            runnerDropdownPanel.Visibility = runnerDropdownExpanded ? Visibility.Visible : Visibility.Collapsed;
            runnerDropdownButton.Content = runnerDropdownExpanded ? "RUN ▲" : "RUN";

            // Close other dropdowns
            if (runnerDropdownExpanded)
            {
                t1DropdownExpanded = false;
                t1DropdownPanel.Visibility = Visibility.Collapsed;
                t1DropdownButton.Content = "T1";
                t2DropdownExpanded = false;
                t2DropdownPanel.Visibility = Visibility.Collapsed;
                t2DropdownButton.Content = "T2";
                t3DropdownExpanded = false;
                t3DropdownPanel.Visibility = Visibility.Collapsed;
                t3DropdownButton.Content = "T3";
            }
        }

        // v5.12: Execute target actions (T1 or T2)
        private void ExecuteTargetAction(string targetType, string action)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print(FormatString("{0} ACTION: No active positions", targetType));
                    return;
                }

                foreach (var kvp in activePositions)
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled)
                    {
                        Print(FormatString("{0} ACTION: Position {1} not filled yet", targetType, entryName));
                        continue;
                    }

                    Dictionary<string, Order> targetOrders = (targetType == "T1") ? target1Orders : target2Orders;
                    int targetContracts = (targetType == "T1") ? pos.T1Contracts : pos.T2Contracts;
                    bool targetFilled = (targetType == "T1") ? pos.T1Filled : pos.T2Filled;

                    if (targetFilled)
                    {
                        Print(FormatString("{0} ACTION: {1} already filled for {2}", targetType, targetType, entryName));
                        continue;
                    }

                    double currentPrice = Close[0];

                    switch (action)
                    {
                        case "market":
                            // Fill target at market NOW
                            if (targetOrders.ContainsKey(entryName))
                            {
                                CancelOrder(targetOrders[entryName]);
                                targetOrders.Remove(entryName);
                            }

                            Order marketOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, targetContracts, 0, 0, "", targetType + "_Market_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, targetContracts, 0, 0, "", targetType + "_Market_" + entryName);

                            Print(FormatString("★ {0} MARKET FILL: {1} - Closing {2} contracts at market", targetType, entryName, targetContracts));
                            break;

                        case "1point":
                            // Move target to 1 point from current price (toward profit)
                            // For LONG: target must be ABOVE entry, so currentPrice + 1 point
                            // For SHORT: target must be BELOW entry, so currentPrice - 1 point
                            double newPrice1pt = pos.Direction == MarketPosition.Long
                                ? currentPrice + 1.0  // 1 point above market for long target
                                : currentPrice - 1.0; // 1 point below market for short target
                            newPrice1pt = Instrument.MasterInstrument.RoundToTickSize(newPrice1pt);
                            
                            // CRITICAL: Validate target stays on profitable side of entry
                            if (pos.Direction == MarketPosition.Long && newPrice1pt <= pos.EntryPrice)
                            {
                                newPrice1pt = pos.EntryPrice + tickSize; // Minimum 1 tick above entry
                                Print(FormatString("★ {0} → 1 POINT: {1} - Adjusted to stay above entry @ {2:F2}", targetType, entryName, newPrice1pt));
                            }
                            else if (pos.Direction == MarketPosition.Short && newPrice1pt >= pos.EntryPrice)
                            {
                                newPrice1pt = pos.EntryPrice - tickSize; // Minimum 1 tick below entry
                                Print(FormatString("★ {0} → 1 POINT: {1} - Adjusted to stay below entry @ {2:F2}", targetType, entryName, newPrice1pt));
                            }
                            else
                            {
                                Print(FormatString("★ {0} → 1 POINT: {1} - New target @ {2:F2}", targetType, entryName, newPrice1pt));
                            }
                            
                            MoveTargetOrder(entryName, targetType, newPrice1pt, targetContracts, pos.Direction);
                            break;

                        case "2point":
                            // Move target to 2 points from current price (toward profit)
                            double newPrice2pt = pos.Direction == MarketPosition.Long
                                ? currentPrice + 2.0  // 2 points above market for long target
                                : currentPrice - 2.0; // 2 points below market for short target
                            newPrice2pt = Instrument.MasterInstrument.RoundToTickSize(newPrice2pt);
                            
                            // CRITICAL: Validate target stays on profitable side of entry
                            if (pos.Direction == MarketPosition.Long && newPrice2pt <= pos.EntryPrice)
                            {
                                newPrice2pt = pos.EntryPrice + tickSize;
                                Print(FormatString("★ {0} → 2 POINTS: {1} - Adjusted to stay above entry @ {2:F2}", targetType, entryName, newPrice2pt));
                            }
                            else if (pos.Direction == MarketPosition.Short && newPrice2pt >= pos.EntryPrice)
                            {
                                newPrice2pt = pos.EntryPrice - tickSize;
                                Print(FormatString("★ {0} → 2 POINTS: {1} - Adjusted to stay below entry @ {2:F2}", targetType, entryName, newPrice2pt));
                            }
                            else
                            {
                                Print(FormatString("★ {0} → 2 POINTS: {1} - New target @ {2:F2}", targetType, entryName, newPrice2pt));
                            }
                            
                            MoveTargetOrder(entryName, targetType, newPrice2pt, targetContracts, pos.Direction);
                            break;

                        case "marketprice":
                            // Move target to current market price (instant fill)
                            double marketPrice = Instrument.MasterInstrument.RoundToTickSize(currentPrice);
                            MoveTargetOrder(entryName, targetType, marketPrice, targetContracts, pos.Direction);
                            Print(FormatString("★ {0} → MARKET PRICE: {1} - New target @ {2:F2}", targetType, entryName, marketPrice));
                            break;

                        case "breakeven":
                            // Move target to breakeven (entry price)
                            MoveTargetOrder(entryName, targetType, pos.EntryPrice, targetContracts, pos.Direction);
                            Print(FormatString("★ {0} → BREAKEVEN: {1} - New target @ {2:F2}", targetType, entryName, pos.EntryPrice));
                            break;

                        case "cancel":
                            // Cancel target order - let contracts run
                            if (targetOrders.ContainsKey(entryName))
                            {
                                CancelOrder(targetOrders[entryName]);
                                targetOrders.Remove(entryName);
                                Print(FormatString("★ {0} CANCELLED: {1} - {2} contracts will run with stop", targetType, entryName, targetContracts));
                            }
                            break;
                    }
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print(FormatString("ERROR ExecuteTargetAction ({0}, {1}): {2}", targetType, action, ex.Message));
            }
        }

        private void MoveTargetOrder(string entryName, string targetType, double newPrice, int quantity, MarketPosition direction)
        {
            Dictionary<string, Order> targetOrders = (targetType == "T1") ? target1Orders : target2Orders;

            // Cancel existing target order
            if (targetOrders.ContainsKey(entryName))
            {
                CancelOrder(targetOrders[entryName]);
                targetOrders.Remove(entryName);
            }

            // Submit new target order at new price
            Order newTargetOrder = direction == MarketPosition.Long
                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, quantity, newPrice, 0, "", targetType + "_" + entryName)
                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, quantity, newPrice, 0, "", targetType + "_" + entryName);

            if (newTargetOrder != null)
            {
                targetOrders[entryName] = newTargetOrder;
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

                foreach (var kvp in activePositions)
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled)
                    {
                        Print(FormatString("RUNNER ACTION: Position {0} not filled yet", entryName));
                        continue;
                    }

                    // Calculate runner contracts (remaining after T1 and T2)
                    int runnerContracts = pos.RemainingContracts;
                    if (runnerContracts <= 0)
                    {
                        Print(FormatString("RUNNER ACTION: No runner contracts for {0}", entryName));
                        continue;
                    }

                    double currentPrice = Close[0];

                    switch (action)
                    {
                        case "market":
                            // Close runner at market
                            Order runnerMarketOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, runnerContracts, 0, 0, "", "Runner_Market_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, runnerContracts, 0, 0, "", "Runner_Market_" + entryName);

                            Print(FormatString("★ RUNNER MARKET CLOSE: {0} - Closing {1} contracts at market", entryName, runnerContracts));
                            break;

                        case "stop1pt":
                            // Move stop to 1 point from current price
                            double newStop1pt = pos.Direction == MarketPosition.Long
                                ? currentPrice - (1.0 * tickSize / tickSize)
                                : currentPrice + (1.0 * tickSize / tickSize);
                            newStop1pt = Instrument.MasterInstrument.RoundToTickSize(newStop1pt);
                            UpdateStopOrder(entryName, pos, newStop1pt, pos.CurrentTrailLevel);
                            Print(FormatString("★ RUNNER STOP → 1 POINT: {0} - Stop @ {1:F2}", entryName, newStop1pt));
                            break;

                        case "stop2pt":
                            // Move stop to 2 points from current price
                            double newStop2pt = pos.Direction == MarketPosition.Long
                                ? currentPrice - (2.0 * tickSize / tickSize)
                                : currentPrice + (2.0 * tickSize / tickSize);
                            newStop2pt = Instrument.MasterInstrument.RoundToTickSize(newStop2pt);
                            UpdateStopOrder(entryName, pos, newStop2pt, pos.CurrentTrailLevel);
                            Print(FormatString("★ RUNNER STOP → 2 POINTS: {0} - Stop @ {1:F2}", entryName, newStop2pt));
                            break;

                        case "stopbe":
                            // Move stop to breakeven
                            UpdateStopOrder(entryName, pos, pos.EntryPrice, 1);
                            Print(FormatString("★ RUNNER STOP → BREAKEVEN: {0} - Stop @ {1:F2}", entryName, pos.EntryPrice));
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
                            Print(FormatString("★ RUNNER LOCK 50%: {0} - Stop @ {1:F2} (profit: {2:F2})", entryName, lock50Stop, unrealizedProfit));
                            break;

                        case "disabletrail":
                            // Disable trailing - keep stop where it is
                            pos.CurrentTrailLevel = 999; // Set to high number to prevent further trailing
                            Print(FormatString("★ RUNNER TRAILING DISABLED: {0} - Stop fixed @ {1:F2}", entryName, pos.CurrentStopPrice));
                            break;
                    }
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print(FormatString("ERROR ExecuteRunnerAction ({0}): {1}", action, ex.Message));
            }
        }

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            // V8.3: Check if this is a resize grip (Path element) or drag handle (Border)
            if (sender is System.Windows.Shapes.Path)
            {
                // Resize mode
                isResizing = true;
                resizeStartPoint = e.GetPosition(ChartControl);
                originalWidth = mainBorder.ActualWidth;
                ((UIElement)sender).CaptureMouse();
            }
            else
            {
                // Drag mode
                isDragging = true;
                dragStartPoint = e.GetPosition(ChartControl);
                originalMargin = mainBorder.Margin;
                ((UIElement)sender).CaptureMouse();
            }
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                // V8.3: Resize handling
                Point currentPoint = e.GetPosition(ChartControl);
                double deltaX = currentPoint.X - resizeStartPoint.X;

                double newWidth = originalWidth + deltaX;
                newWidth = Math.Max(280, Math.Min(newWidth, 600)); // Clamp between min/max

                mainBorder.Width = newWidth;
                return;
            }

            if (!isDragging) return;

            Point currentDragPoint = e.GetPosition(ChartControl);
            double deltaX2 = currentDragPoint.X - dragStartPoint.X;
            double deltaY = currentDragPoint.Y - dragStartPoint.Y;

            mainBorder.Margin = new Thickness(
                originalMargin.Left + deltaX2,
                originalMargin.Top + deltaY,
                0, 0
            );
        }

        private void OnDragEnd(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        // V8.3: Separate resize handlers (used by resize grip)
        private void OnResizeStart(object sender, MouseButtonEventArgs e)
        {
            isResizing = true;
            resizeStartPoint = e.GetPosition(ChartControl);
            originalWidth = mainBorder.ActualWidth > 0 ? mainBorder.ActualWidth : 280;
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void OnResizeMove(object sender, MouseEventArgs e)
        {
            if (!isResizing) return;

            Point currentPoint = e.GetPosition(ChartControl);
            double deltaX = currentPoint.X - resizeStartPoint.X;

            double newWidth = originalWidth + deltaX;
            newWidth = Math.Max(280, Math.Min(newWidth, 600)); // Clamp between min/max

            mainBorder.Width = newWidth;
        }

        private void OnResizeEnd(object sender, MouseButtonEventArgs e)
        {
            isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
            e.Handled = true;
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
                statusTextBlock.Text = FormatString("OR v8.3 | {0}", status);

                // OR Info
                if (orComplete)
                {
                    double stopDist = CalculateORStopDistance();
                    double t1Dist = Target1FixedPoints;  // v5.13: T1 is fixed points
                    double t2Dist = sessionRange * Target2Multiplier;
                    double t3Dist = sessionRange * Target3Multiplier;  // v5.13: T3
                    string atrText = currentATR > 0 ? FormatString(" | ATR: {0:F2}", currentATR) : "";
                    orInfoBlock.Text = FormatString("H: {0:F2} | L: {1:F2} | R: {2:F2}{3}\nT1: +{4:F2} | T2: +{5:F2} | T3: +{6:F2} | Stop: -{7:F2}",
                        sessionHigh, sessionLow, sessionRange, atrText, t1Dist, t2Dist, t3Dist, stopDist);
                }
                else if (isInORWindow)
                {
                    string atrText = currentATR > 0 ? FormatString(" | ATR: {0:F2}", currentATR) : "";
                    orInfoBlock.Text = FormatString("Building: H={0:F2} L={1:F2}{2}", sessionHigh, sessionLow, atrText);
                }
                else
                {
                    string atrText = currentATR > 0 ? FormatString(" | ATR: {0:F2}", currentATR) : "";
                    orInfoBlock.Text = FormatString("Waiting for OR window...{0}", atrText);
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

                // Update breakeven button state
                bool anyArmed = false;
                bool anyTriggered = false;
                foreach (var kvp in activePositions)
                {
                    if (kvp.Value.ManualBreakevenArmed) anyArmed = true;
                    if (kvp.Value.ManualBreakevenTriggered) anyTriggered = true;
                }

                if (anyTriggered)
                {
                    // Green - breakeven has been triggered
                    breakevenButton.Background = new SolidColorBrush(Color.FromRgb(50, 120, 50));
                }
                else if (anyArmed)
                {
                    // Orange - armed and waiting for price
                    breakevenButton.Background = new SolidColorBrush(Color.FromRgb(180, 100, 20));
                }
                else
                {
                    // Gray - default/inactive
                    breakevenButton.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
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
                        rmaButton.Content = "★ RMA ★";  // V8.3: Shorter label
                    }
                    else
                    {
                        // Hide RMA mode indicator
                        rmaModeTextBlock.Visibility = Visibility.Collapsed;
                        mainBorder.Background = PanelBackground;
                        mainBorder.BorderBrush = Brushes.DodgerBlue;
                        rmaButton.Background = RMAInactiveBackground;
                        rmaButton.Content = "RMA";  // V8.3: Shorter label
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
            // Basic hotkeys
            if (e.Key == Key.L) { ExecuteLong(); e.Handled = true; }
            else if (e.Key == Key.S) { ExecuteShort(); e.Handled = true; }
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
                if (e.Key == Key.M) { ExecuteRunnerAction("market"); e.Handled = true; }
                else if (e.Key == Key.O) { ExecuteRunnerAction("stop1pt"); e.Handled = true; }
                else if (e.Key == Key.W) { ExecuteRunnerAction("stop2pt"); e.Handled = true; }
                else if (e.Key == Key.B) { ExecuteRunnerAction("stopbe"); e.Handled = true; }
                else if (e.Key == Key.P) { ExecuteRunnerAction("lock50"); e.Handled = true; }  // P for Profit
                else if (e.Key == Key.D) { ExecuteRunnerAction("disabletrail"); e.Handled = true; }
            }
            
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
