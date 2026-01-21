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
    /// <summary>
    /// V6 Master Strategy - Calculates OR and broadcasts signals to slaves.
    /// Does NOT submit orders. Slaves handle all order execution.
    /// </summary>
    public class UniversalORMasterV6 : Strategy
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

        // ATR Indicator for RMA
        private ATR atrIndicator;
        private double currentATR;
        private double lastKnownPrice;

        // RMA Mode tracking
        private bool isRMAModeActive;
        private bool isRKeyHeld;
        private bool isRMAButtonClicked;

        // UI Components
        private Border mainBorder;
        private System.Windows.Controls.Grid mainGrid;
        private TextBlock statusTextBlock;
        private TextBlock orInfoBlock;
        private TextBlock slaveCountBlock;
        private TextBlock rmaModeTextBlock;
        private TextBlock atrTextBlock;
        private Button longButton;
        private Button shortButton;
        private Button rmaButton;
        private Button flattenButton;
        private Button breakevenButton;
        private bool uiCreated;

        // Colors for UI
        private static readonly SolidColorBrush RMAActiveBackground;
        private static readonly SolidColorBrush RMAInactiveBackground;
        private static readonly SolidColorBrush PanelBackground;
        private static readonly SolidColorBrush MasterBackground;

        static UniversalORMasterV6()
        {
            RMAActiveBackground = new SolidColorBrush(Color.FromRgb(180, 100, 20));
            RMAActiveBackground.Freeze();

            RMAInactiveBackground = new SolidColorBrush(Color.FromRgb(80, 80, 100));
            RMAInactiveBackground.Freeze();

            PanelBackground = new SolidColorBrush(Color.FromArgb(230, 20, 20, 30));
            PanelBackground.Freeze();

            MasterBackground = new SolidColorBrush(Color.FromArgb(230, 30, 60, 30));
            MasterBackground.Freeze();
        }

        // Drag support
        private bool isDragging;
        private Point dragStartPoint;
        private Thickness originalMargin;

        // RAM optimization
        private StringBuilder sbPool;

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

        #region Properties - Stop Loss

        [NinjaScriptProperty]
        [Display(Name = "Stop Multiplier", Description = "Multiplier of OR Range for stop (0.5 = half OR)", Order = 1, GroupName = "2. Stop Loss")]
        public double StopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Stop (Points)", Order = 2, GroupName = "2. Stop Loss")]
        public double MinimumStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (Points)", Order = 3, GroupName = "2. Stop Loss")]
        public double MaximumStop { get; set; }

        #endregion

        #region Properties - Profit Targets

        [NinjaScriptProperty]
        [Display(Name = "Target 1 Multiplier", Description = "Multiplier of OR range for T1 (0.25 = 1/4 OR)", Order = 1, GroupName = "3. Profit Targets")]
        public double Target1Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 2 Multiplier", Description = "Multiplier of OR range for T2 (0.5 = half OR)", Order = 2, GroupName = "3. Profit Targets")]
        public double Target2Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Order = 3, GroupName = "3. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Order = 4, GroupName = "3. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Order = 5, GroupName = "3. Profit Targets")]
        public int T3ContractPercent { get; set; }

        #endregion

        #region Properties - Display

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Line", Description = "Show middle line in OR box", Order = 1, GroupName = "4. Display")]
        public bool ShowMidLine { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Box Opacity (%)", Description = "Transparency of OR box (0-100)", Order = 2, GroupName = "4. Display")]
        [Range(0, 100)]
        public int BoxOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show OR Label", Description = "Show OR high/low/range text on chart", Order = 3, GroupName = "4. Display")]
        public bool ShowORLabel { get; set; }

        #endregion

        #region Properties - RMA Settings

        [NinjaScriptProperty]
        [Display(Name = "RMA Enabled", Description = "Enable RMA (Shift+Click) entry mode", Order = 1, GroupName = "5. RMA Settings")]
        public bool RMAEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA ATR Period", Description = "ATR period for RMA calculations (default 14)", Order = 2, GroupName = "5. RMA Settings")]
        [Range(1, 100)]
        public int RMAATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA Stop ATR Multiplier", Description = "Multiplier of ATR for RMA stop (default 1.0)", Order = 3, GroupName = "5. RMA Settings")]
        [Range(0.1, 5.0)]
        public double RMAStopATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA T1 ATR Multiplier", Description = "Multiplier of ATR for RMA Target 1 (default 0.5)", Order = 4, GroupName = "5. RMA Settings")]
        [Range(0.1, 5.0)]
        public double RMAT1ATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA T2 ATR Multiplier", Description = "Multiplier of ATR for RMA Target 2 (default 1.0)", Order = 5, GroupName = "5. RMA Settings")]
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
                Description = "V6 Master Strategy - Calculates OR and broadcasts signals to slaves";
                Name = "UniversalORMasterV6";
                Calculate = Calculate.OnPriceChange;
                IsUnmanaged = false;  // Master doesn't submit orders
                
                // Session defaults (NY Open)
                SessionStart = DateTime.Parse("09:30");
                SessionEnd = DateTime.Parse("16:00");
                ORTimeframe = ORTimeframeType.Minutes_5;
                SelectedTimeZone = "Eastern";

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 1.0;
                MaximumStop = 8.0;

                // Target defaults
                Target1Multiplier = 0.25;
                Target2Multiplier = 0.5;
                T1ContractPercent = 33;
                T2ContractPercent = 33;
                T3ContractPercent = 34;

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
            }
            else if (State == State.Configure)
            {
                sbPool = new StringBuilder(256);
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;
                atrIndicator = ATR(BarsArray[1], RMAATRPeriod);
                ResetOR();

                Print($"UniversalORMasterV6 | {Instrument.MasterInstrument.Name} | Tick: {tickSize} | PV: ${pointValue}");
                Print($"Session: {SessionStart:HH:mm} - {SessionEnd:HH:mm} {SelectedTimeZone} | OR: {(int)ORTimeframe} min");
                Print($"OR Targets: T1={Target1Multiplier}xOR T2={Target2Multiplier}xOR | Stop={StopMultiplier}xOR");
                Print($"RMA: Enabled={RMAEnabled} ATR({RMAATRPeriod}) Stop={RMAStopATRMultiplier}xATR");
                Print("MASTER MODE: Signals will be broadcast to slave strategies");
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
                        UpdateDisplay();
                        Print("MASTER REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
                        Print(SignalBroadcaster.GetSubscriberCounts());
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
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                lastKnownPrice = Close[0];

                // Update ATR
                if (BarsArray[1] != null && BarsArray[1].Count > RMAATRPeriod)
                {
                    currentATR = atrIndicator[0];
                }

                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // Smart reset logic
                bool shouldReset = false;
                if (sessionCrossesMidnight)
                {
                    if (currentTime >= sessionStartTime && currentTime < sessionStartTime.Add(TimeSpan.FromMinutes(10)))
                    {
                        if (barTimeInZone.Date != lastResetDate)
                            shouldReset = true;
                    }
                }
                else
                {
                    if (barTimeInZone.Date != lastResetDate && currentTime >= sessionStartTime)
                        shouldReset = true;
                }

                if (shouldReset)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print($"Session Reset: {barTimeInZone.Date.ToShortDateString()} at {currentTime} {SelectedTimeZone}");
                }

                // Build OR during window
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (!isInORWindow)
                    {
                        Print($"OR WINDOW START: {barTimeInZone:MM/dd/yyyy HH:mm:ss} ({SelectedTimeZone})");
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
                        Print($"OR Start tracked - Bar {CurrentBar}");
                    }
                }

                // Mark OR complete
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print($"OR COMPLETE at {barTimeInZone:HH:mm:ss}: H={sessionHigh:F2} L={sessionLow:F2} M={sessionMid:F2} R={sessionRange:F2}");
                    Print($"OR Targets: T1=+{sessionRange * Target1Multiplier:F2} T2=+{sessionRange * Target2Multiplier:F2} Stop=-{CalculateORStopDistance():F2}");

                    DrawORBox();
                }

                // Update box if OR complete
                bool inActiveSession = sessionCrossesMidnight
                    ? (currentTime >= sessionStartTime || currentTime <= sessionEndTime)
                    : (currentTime >= sessionStartTime && currentTime <= sessionEndTime);

                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
                {
                    DrawORBox();
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR OnBarUpdate: " + ex.Message);
            }
        }

        #endregion

        #region OR Entry Logic - BROADCAST ONLY

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

            BroadcastORSignal(MarketPosition.Long, entryPrice, stopPrice);
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

            BroadcastORSignal(MarketPosition.Short, entryPrice, stopPrice);
        }

        private void BroadcastORSignal(MarketPosition direction, double entryPrice, double stopPrice)
        {
            try
            {
                string signalName = direction == MarketPosition.Long ? "ORLong" : "ORShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string signalId = signalName + "_" + timestamp;

                // Calculate target prices
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target1Multiplier)
                    : entryPrice - (sessionRange * Target1Multiplier);

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target2Multiplier)
                    : entryPrice - (sessionRange * Target2Multiplier);

                // Create signal
                var signal = new SignalBroadcaster.TradeSignal
                {
                    SignalId = signalId,
                    Direction = direction,
                    EntryPrice = entryPrice,
                    StopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    T1Contracts = T1ContractPercent,
                    T2Contracts = T2ContractPercent,
                    T3Contracts = T3ContractPercent,
                    IsRMA = false,
                    SessionRange = sessionRange,
                    CurrentATR = currentATR
                };

                // BROADCAST to all slaves
                SignalBroadcaster.BroadcastTradeSignal(signal);

                Print($"MASTER BROADCAST: {signalName} @ {entryPrice:F2} | Stop: {stopPrice:F2} | T1: {target1Price:F2} | T2: {target2Price:F2}");
                Print($"Signal ID: {signalId} | Slaves will execute independently");
            }
            catch (Exception ex)
            {
                Print("ERROR BroadcastORSignal: " + ex.Message);
            }
        }

        private double CalculateORStopDistance()
        {
            if (sessionRange == 0) return MinimumStop;
            double calculatedStop = sessionRange * StopMultiplier;
            return Math.Max(MinimumStop, Math.Min(calculatedStop, MaximumStop));
        }

        #endregion

        #region RMA Entry Logic - BROADCAST ONLY

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
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // Auto-direction: Click ABOVE current price = SHORT, Click BELOW = LONG
                MarketPosition direction;
                if (clickPrice > currentPrice)
                {
                    direction = MarketPosition.Short;
                    Print($"RMA: Click above price ({clickPrice:F2} > {currentPrice:F2}) = SHORT entry");
                }
                else
                {
                    direction = MarketPosition.Long;
                    Print($"RMA: Click below price ({clickPrice:F2} < {currentPrice:F2}) = LONG entry");
                }

                // Entry at current market price
                double entryPrice = currentPrice;

                // RMA-based stop and targets
                double stopDistance = currentATR * RMAStopATRMultiplier;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                double target1Distance = currentATR * RMAT1ATRMultiplier;
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + target1Distance
                    : entryPrice - target1Distance;

                double target2Distance = currentATR * RMAT2ATRMultiplier;
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + target2Distance
                    : entryPrice - target2Distance;

                string signalName = direction == MarketPosition.Long ? "RMALong" : "RMAShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string signalId = signalName + "_" + timestamp;

                // Create RMA signal
                var signal = new SignalBroadcaster.TradeSignal
                {
                    SignalId = signalId,
                    Direction = direction,
                    EntryPrice = entryPrice,
                    StopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    T1Contracts = T1ContractPercent,
                    T2Contracts = T2ContractPercent,
                    T3Contracts = T3ContractPercent,
                    IsRMA = true,
                    SessionRange = sessionRange,
                    CurrentATR = currentATR
                };

                // BROADCAST to all slaves
                SignalBroadcaster.BroadcastTradeSignal(signal);

                Print($"MASTER BROADCAST RMA: {signalName} @ {entryPrice:F2} | Stop: {stopPrice:F2} (-{stopDistance:F2})");
                Print($"RMA Targets: T1: {target1Price:F2} (+{target1Distance:F2}) | T2: {target2Price:F2} (+{target2Distance:F2})");
                Print($"Signal ID: {signalId} | ATR: {currentATR:F2}");

                // Deactivate RMA mode after entry
                isRMAModeActive = false;
                isRMAButtonClicked = false;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRMAEntry: " + ex.Message);
            }
        }

        #endregion

        #region Flatten All - BROADCAST

        private void FlattenAll()
        {
            SignalBroadcaster.BroadcastFlatten("Master flatten command");
            Print("MASTER BROADCAST: Flatten all positions");
        }

        #endregion

        #region Drawing

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
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                DateTime sessionEndInZone;
                if (sessionCrossesMidnight)
                {
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    ).AddDays(1);
                }
                else
                {
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
                        ? $"OR Building: {sessionHigh:F2} - {sessionLow:F2}"
                        : $"OR: {sessionHigh:F2} - {sessionLow:F2} (R:{sessionRange:F2})";

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

            RemoveDrawObject("ORBox");
            RemoveDrawObject("ORMid");
            RemoveDrawObject("ORLabel");
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

        #endregion

        #region UI Creation

        private void CreateUI()
        {
            if (ChartControl == null || uiCreated) return;

            try
            {
                mainBorder = new Border
                {
                    Background = MasterBackground,
                    BorderBrush = Brushes.LimeGreen,
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
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: ATR Info
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Slave Count
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: RMA Mode indicator
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6: Long button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: Short button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 8: RMA button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 9: Breakeven button
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 10: Flatten button

                Border dragHandle = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(100, 50, 200, 50)),
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
                    Text = "═══ MASTER V6 ═══",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dragHandle.Child = dragLabel;

                statusTextBlock = new TextBlock
                {
                    Text = "MASTER V6 | Initializing...",
                    Foreground = Brushes.LimeGreen,
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
                    Margin = new Thickness(0, 2, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(orInfoBlock, 2);

                atrTextBlock = new TextBlock
                {
                    Text = "ATR: --",
                    Foreground = Brushes.Yellow,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 5)
                };
                Grid.SetRow(atrTextBlock, 3);

                slaveCountBlock = new TextBlock
                {
                    Text = "Slaves: 0",
                    Foreground = Brushes.Cyan,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 8),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(slaveCountBlock, 4);

                // RMA Mode indicator (hidden by default)
                rmaModeTextBlock = new TextBlock
                {
                    Text = "★ RMA MODE ACTIVE - Click chart to place entry ★",
                    Foreground = Brushes.Orange,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 8),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(rmaModeTextBlock, 5);

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
                Grid.SetRow(longButton, 6);

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
                Grid.SetRow(shortButton, 7);

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
                Grid.SetRow(rmaButton, 8);

                // Breakeven button
                breakevenButton = new Button
                {
                    Content = "BREAKEVEN (ALL)",
                    Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand
                };
                breakevenButton.Click += (s, e) =>
                {
                    SignalBroadcaster.BroadcastBreakeven();
                    Print("MASTER BROADCAST: Move all to breakeven");
                };
                Grid.SetRow(breakevenButton, 9);

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
                Grid.SetRow(flattenButton, 10);

                mainGrid.Children.Add(dragHandle);
                mainGrid.Children.Add(statusTextBlock);
                mainGrid.Children.Add(orInfoBlock);
                mainGrid.Children.Add(atrTextBlock);
                mainGrid.Children.Add(slaveCountBlock);
                mainGrid.Children.Add(rmaModeTextBlock);
                mainGrid.Children.Add(longButton);
                mainGrid.Children.Add(shortButton);
                mainGrid.Children.Add(rmaButton);
                mainGrid.Children.Add(breakevenButton);
                mainGrid.Children.Add(flattenButton);

                mainBorder.Child = mainGrid;

                // Add to chart
                UserControlCollection.Add(mainBorder);

                uiCreated = true;
                Print("MASTER UI created - v6 (Full V5-style UI)");
            }
            catch (Exception ex)
            {
                Print("ERROR CreateUI: " + ex.Message);
            }
        }

        private void AttachHotkeys()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewKeyDown += OnChartKeyDown;
                ChartControl.PreviewKeyUp += OnChartKeyUp;
            }
        }

        private void DetachHotkeys()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewKeyDown -= OnChartKeyDown;
                ChartControl.PreviewKeyUp -= OnChartKeyUp;
            }
        }

        private void OnChartKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L && !e.IsRepeat)
            {
                ExecuteLong();
                e.Handled = true;
            }
            else if (e.Key == Key.S && !e.IsRepeat)
            {
                ExecuteShort();
                e.Handled = true;
            }
            else if (e.Key == Key.F && !e.IsRepeat)
            {
                FlattenAll();
                e.Handled = true;
            }
        }

        private void OnChartKeyUp(object sender, KeyEventArgs e)
        {
            // Handle key releases if needed
        }

        private void AttachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewMouseDown += OnChartMouseDown;
            }
        }

        private void DetachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewMouseDown -= OnChartMouseDown;
            }
        }

        private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isRMAModeActive) return;

            try
            {
                // Get mouse position relative to ChartPanel
                Point mouseInPanel = e.GetPosition(ChartPanel as System.Windows.IInputElement);

                double panelHeight = ChartPanel.H;
                double maxPrice = ChartPanel.MaxValue;
                double minPrice = ChartPanel.MinValue;
                double priceRange = maxPrice - minPrice;

                // ChartPanel.H includes time axis - effective price area is ~67%
                double effectivePriceHeight = panelHeight * 0.667;

                // Clamp Y to valid range
                double yInPanel = mouseInPanel.Y;
                if (yInPanel < 0) yInPanel = 0;
                if (yInPanel > effectivePriceHeight) yInPanel = effectivePriceHeight;

                // Convert: Y=0 is top (maxPrice), Y=effectivePriceHeight is bottom (minPrice)
                double yRatio = yInPanel / effectivePriceHeight;
                double clickPrice = maxPrice - (yRatio * priceRange);

                // Round to tick size
                clickPrice = Instrument.MasterInstrument.RoundToTickSize(clickPrice);

                ExecuteRMAEntry(clickPrice);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Print("ERROR OnChartMouseDown: " + ex.Message);
            }
        }

        private void UpdateDisplay()
        {
            if (!uiCreated || ChartControl == null) return;

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Status
                    string status = orComplete ? "OR COMPLETE" : (isInORWindow ? "OR BUILDING" : "WAITING");
                    statusTextBlock.Text = $"MASTER V6 | {status}";

                    // OR Info with detailed targets
                    if (orComplete)
                    {
                        double stopDist = CalculateORStopDistance();
                        double t1Dist = sessionRange * Target1Multiplier;
                        double t2Dist = sessionRange * Target2Multiplier;
                        orInfoBlock.Text = $"H: {sessionHigh:F2} | L: {sessionLow:F2} | R: {sessionRange:F2}\nT1: +{t1Dist:F2} | T2: +{t2Dist:F2} | Stop: -{stopDist:F2}";
                    }
                    else if (isInORWindow)
                    {
                        orInfoBlock.Text = $"Building: H={sessionHigh:F2} L={sessionLow:F2}";
                    }
                    else
                    {
                        orInfoBlock.Text = "Waiting for OR window...";
                    }

                    // ATR display
                    if (currentATR > 0)
                    {
                        atrTextBlock.Text = $"ATR: {currentATR:F2}";
                        atrTextBlock.Foreground = Brushes.Yellow;
                    }
                    else
                    {
                        atrTextBlock.Text = "ATR: --";
                        atrTextBlock.Foreground = Brushes.Gray;
                    }

                    // Update slave count
                    string counts = SignalBroadcaster.GetSubscriberCounts();
                    int tradeCount = 0;
                    if (counts.Contains("Trade="))
                    {
                        int idx = counts.IndexOf("Trade=") + 6;
                        int endIdx = counts.IndexOf(",", idx);
                        if (endIdx > idx)
                        {
                            string numStr = counts.Substring(idx, endIdx - idx);
                            int.TryParse(numStr, out tradeCount);
                        }
                    }
                    slaveCountBlock.Text = $"Slaves: {tradeCount} connected";
                    slaveCountBlock.Foreground = tradeCount > 0 ? Brushes.Lime : Brushes.Cyan;
                }
                catch (Exception ex)
                {
                    Print("ERROR UpdateDisplay: " + ex.Message);
                }
            });
        }

        private void UpdateRMAModeDisplay()
        {
            if (!uiCreated || ChartControl == null) return;

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (isRMAModeActive)
                    {
                        // Show RMA mode indicator
                        rmaModeTextBlock.Visibility = Visibility.Visible;
                        mainBorder.Background = new SolidColorBrush(Color.FromArgb(230, 50, 30, 10));
                        mainBorder.BorderBrush = Brushes.Orange;
                        rmaButton.Background = RMAActiveBackground;
                        rmaButton.Content = "★ RMA ACTIVE ★";
                    }
                    else
                    {
                        // Hide RMA mode indicator
                        rmaModeTextBlock.Visibility = Visibility.Collapsed;
                        mainBorder.Background = MasterBackground;
                        mainBorder.BorderBrush = Brushes.LimeGreen;
                        rmaButton.Background = RMAInactiveBackground;
                        rmaButton.Content = "RMA (Shift+Click)";
                    }
                }
                catch { }
            });
        }

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            dragStartPoint = e.GetPosition(null);
            originalMargin = mainBorder.Margin;
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(null);
                double offsetX = currentPosition.X - dragStartPoint.X;
                double offsetY = currentPosition.Y - dragStartPoint.Y;
                mainBorder.Margin = new Thickness(
                    originalMargin.Left + offsetX,
                    originalMargin.Top + offsetY,
                    0, 0);
            }
        }

        private void OnDragEnd(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        private void RemoveUI()
        {
            if (uiCreated && UserControlCollection != null && mainBorder != null)
            {
                UserControlCollection.Remove(mainBorder);
                uiCreated = false;
            }
        }

        #endregion
    }
}
