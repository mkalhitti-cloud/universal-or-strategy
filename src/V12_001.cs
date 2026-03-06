// V12 STANDARD EDITION - NinjaTrader 8 Vertical Sidebar Control Surface
// RELIABILITY FIRST: Uses standard UserControlCollection API (no injection)
// Docks to RIGHT side of chart as a vertical sidebar (~240px wide)
//
// Version: V12 STANDARD - Vertical Sidebar (Mockup Match)
// Author: Claude Code + SIMA Architecture
//
// STANDARD APPROACH:
// 1. Uses UserControlCollection.Add() - NinjaTrader's documented API
// 2. No visual tree manipulation, no reflection, no injection
// 3. 100% reliable loading - GUARANTEED
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For Popup
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class V12_001 : Indicator
    {
        #region Variables

        /// <summary>Per-mode settings for panel persistence</summary>
        public class V12ModeSettings
        {
            public int TargetCount { get; set; }
            public string T1Val { get; set; }
            public string T1Type { get; set; }
            public string T2Val { get; set; }
            public string T2Type { get; set; }
            public string T3Val { get; set; }
            public string T3Type { get; set; }
            public string T4Val { get; set; }
            public string T4Type { get; set; }
            public string T5Val { get; set; }
            public string T5Type { get; set; }
            public string STR { get; set; }
            public string StopType { get; set; }
            public string MAX { get; set; }
            public string CIT { get; set; }

            public V12ModeSettings()
            {
                TargetCount = 3;
                T1Val = "1.0"; T1Type = "Pts";
                T2Val = "2.0"; T2Type = "ATR";
                T3Val = "3.0"; T3Type = "ATR";
                T4Val = "4.0"; T4Type = "ATR";
                T5Val = "5.0"; T5Type = "Runner";
                STR = "1.1"; StopType = "ATR"; MAX = "$1200"; CIT = "0";
            }

            public string ToLine()
            {
                return $"{TargetCount}|{T1Val}|{T1Type}|{T2Val}|{T2Type}|{T3Val}|{T3Type}|{T4Val}|{T4Type}|{T5Val}|{T5Type}|{STR}|{StopType}|{MAX}|{CIT}";
            }

            public static V12ModeSettings FromLine(string line)
            {
                var s = new V12ModeSettings();
                if (string.IsNullOrEmpty(line)) return s;
                var parts = line.Split('|');
                if (parts.Length >= 15)
                {
                    // V12 TOTAL RECALL format: 15 fields
                    int.TryParse(parts[0], out int tc); s.TargetCount = tc > 0 ? tc : 3;
                    s.T1Val = parts[1]; s.T1Type = parts[2];
                    s.T2Val = parts[3]; s.T2Type = parts[4];
                    s.T3Val = parts[5]; s.T3Type = parts[6];
                    s.T4Val = parts[7]; s.T4Type = parts[8];
                    s.T5Val = parts[9]; s.T5Type = parts[10];
                    s.STR = parts[11]; s.StopType = parts[12]; s.MAX = parts[13];
                    if (parts.Length >= 15) s.CIT = parts[14];
                }
                else if (parts.Length >= 10)
                {
                    // LEGACY format: 11 fields (no T4/T5)
                    int.TryParse(parts[0], out int tc); s.TargetCount = tc > 0 ? tc : 3;
                    s.T1Val = parts[1]; s.T1Type = parts[2];
                    s.T2Val = parts[3]; s.T2Type = parts[4];
                    s.T3Val = parts[5]; s.T3Type = parts[6];
                    s.STR = parts[7]; s.StopType = parts[8]; s.MAX = parts[9];
                    if (parts.Length >= 11) s.CIT = parts[10];
                }
                return s;
            }
        }

        /// <summary>Full config with Dictionary-based granular memory (composite key: MODE_COUNT)</summary>
        public class V12FullConfig
        {
            public string ActiveMode { get; set; }
            public int ActiveCount { get; set; }
            public Dictionary<string, V12ModeSettings> Slots { get; set; }
            public Dictionary<string, int> LastUsedCountPerMode { get; set; } // V12.18: Sticky count per mode

            public V12FullConfig()
            {
                ActiveMode = "ORB";
                ActiveCount = 3;
                Slots = new Dictionary<string, V12ModeSettings>();
                LastUsedCountPerMode = new Dictionary<string, int>();
            }

            /// <summary>Build composite key: e.g. "ORB_3"</summary>
            public static string MakeKey(string mode, int count)
            {
                return $"{(mode ?? "ORB").ToUpper()}_{count}";
            }

            public V12ModeSettings GetSettings(string mode, int count)
            {
                string key = MakeKey(mode, count);
                if (Slots.ContainsKey(key)) return Slots[key];
                // Return fresh defaults (with correct TargetCount baked in)
                var def = new V12ModeSettings { TargetCount = count };
                return def;
            }

            public void SetSettings(string mode, int count, V12ModeSettings settings)
            {
                string key = MakeKey(mode, count);
                Slots[key] = settings;
            }
        }

        // In-memory config store
        private V12FullConfig fullConfig;

        // Config file path for persistence
        private string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NinjaTrader 8", "V12_Std_Memory.json");

        // Panel state
        private bool panelCreated = false;
        private bool isApplyingSettings = false; // Prevents auto-save during mode switch
        private Border mainPanel;
        private ScrollViewer scrollViewer;
        private StackPanel mainStack;

        // TCP Connection
        private TcpClient tcpClient;
        private NetworkStream tcpStream;
        private readonly object tcpLock = new object();
        private Thread receiveThread;
        private volatile bool isConnected = false;
        private volatile bool isShuttingDown = false;
        private System.Threading.Timer reconnectTimer;
        private ConcurrentQueue<string> responseQueue = new ConcurrentQueue<string>();
        // [Build 934]: Throttle IPC retry log spam -- print on 1st failure then once per 60 s
        private int      _ipcRetryCount    = 0;
        private DateTime _lastRetryLogTime = DateTime.MinValue;

        // Symbol State
        private string activeSymbol = "MES";
        private double lastPrice = 0;

        // Mode Flags
        private bool isRetestRmaToggle = false;
        private bool isTrendRmaToggle = false;
        private bool isGhostMode = false;

        // Selected Target Count (3 default)
        private int selectedTargetCount = 3;
        // V12.45: Last SYNCED count per mode (only written on SyncAll_Click, not on button click)
        private Dictionary<string, int> lastSyncedCountPerMode = new Dictionary<string, int>();

        // Selected Config Mode
        private string selectedConfigMode = "ORB";

        // UI Components - Section 0: Identity
        private Border hubStatusLed;
        private ComboBox leaderAccountCombo;
        private ComboBox fleetAccountCombo; // Legacy - replaced by multi-select
        private Button fleetSelectButton; // Button to open fleet selection popup
        private Popup fleetPopup; // Popup for multi-select checkboxes
        private StackPanel fleetCheckboxPanel; // Container for account checkboxes
        private List<string> selectedFleetAccounts = new List<string>(); // Tracked selected accounts
        // UI Components - Section 1: Execution
        private Button retestButton, rmaButton;
        private int retestCycleState = 0;        // 0=RETEST, 1=RET+, 2=RET-
        private bool isRmaModeActive = false;    // RMA chart-click mode toggle
        private Button trendButton, trendRmaToggle, retestRmaToggle;
        private Button t1Button, t2Button, t3Button, t4Button, t5Button;
        private Button trim50Button, beButton, trailButton;
        private TextBox trailDistInput, beOffsetInput;
        private Button flattenButton, cancelButton;
        private TextBlock lastPriceText;

        // V12.27: Contextual UI - Grid references for visibility control
        private Grid execRetestRow;   // Retest + R toggle row
        private Grid execTrendRow;    // Trend + R toggle row

        // UI Components - Section 1.5: Risk Manager
        private TextBlock complianceSummaryText;
        private TextBlock complianceConsistencyText;
        private TextBlock compliancePayoutText;
        private TextBlock complianceDrawdownText;

        // UI Components - Section 2: Telemetry
        private TextBlock or5Text, or15Text;
        private TextBlock ema9Text, ema15Text, ema30Text, ema65Text, ema200Text;
        private TextBlock atrText;
        private Button mktSyncButton;
        private Border trendIndicator;
        private TextBlock trendText;

        // UI Components - Section 3: Config
        private Button modeRmaButton, modeRetestButton, modeTrendButton;
        private Button cnt1, cnt2, cnt3, cnt4, cnt5;
        private TextBox svT1Val, svT2Val, svT3Val, svT4Val, svT5Val;
        private ComboBox svT1Type, svT2Type, svT3Type, svT4Type, svT5Type, svStrType;
        private TextBox strVal, maxVal, citVal;
        private StackPanel t2Row, t3Row, t4Row, t5Row;  // For visibility control
        private Button syncAllButton;

        // Glow effect
        private DispatcherTimer glowTimer;

        #endregion

        #region V12 Color Palette (Frozen Brushes)

        private static readonly SolidColorBrush BgDeep;
        private static readonly SolidColorBrush BgSlate;
        private static readonly SolidColorBrush BorderSlate;
        private static readonly SolidColorBrush BtnBg;
        private static readonly SolidColorBrush BtnBorder;
        private static readonly SolidColorBrush TextPrimary;
        private static readonly SolidColorBrush TextMuted;
        private static readonly SolidColorBrush CyanAccent;
        private static readonly SolidColorBrush GreenBg, GreenFg, GreenBorder;
        private static readonly SolidColorBrush RedBg, RedFg, RedBorder;
        private static readonly SolidColorBrush OrangeBg, OrangeFg, OrangeBorder;
        private static readonly SolidColorBrush YellowBg, YellowFg, YellowBorder;
        private static readonly SolidColorBrush PinkBg, PinkFg, PinkBorder;
        private static readonly SolidColorBrush CyanBg, CyanFg, CyanBorder;
        private static readonly SolidColorBrush PurpleFg;

        static V12_001()
        {
            BgDeep = Freeze(new SolidColorBrush(Color.FromRgb(5, 5, 8)));
            BgSlate = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
            BorderSlate = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
            BtnBg = Freeze(new SolidColorBrush(Color.FromRgb(23, 23, 28)));
            BtnBorder = Freeze(new SolidColorBrush(Color.FromRgb(45, 45, 55)));
            TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(220, 220, 220)));
            TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(115, 115, 125)));
            CyanAccent = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));

            GreenBg = Freeze(new SolidColorBrush(Color.FromRgb(6, 78, 59)));
            GreenFg = Freeze(new SolidColorBrush(Color.FromRgb(74, 222, 128)));
            GreenBorder = Freeze(new SolidColorBrush(Color.FromRgb(5, 150, 105)));

            RedBg = Freeze(new SolidColorBrush(Color.FromRgb(127, 29, 29)));
            RedFg = Freeze(new SolidColorBrush(Color.FromRgb(252, 165, 165)));
            RedBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));

            OrangeBg = Freeze(new SolidColorBrush(Color.FromRgb(124, 45, 18)));
            OrangeFg = Freeze(new SolidColorBrush(Color.FromRgb(251, 146, 60)));
            OrangeBorder = Freeze(new SolidColorBrush(Color.FromRgb(234, 88, 12)));

            YellowBg = Freeze(new SolidColorBrush(Color.FromRgb(113, 63, 18)));
            YellowFg = Freeze(new SolidColorBrush(Color.FromRgb(250, 204, 21)));
            YellowBorder = Freeze(new SolidColorBrush(Color.FromRgb(202, 138, 4)));

            PinkBg = Freeze(new SolidColorBrush(Color.FromRgb(131, 24, 67)));
            PinkFg = Freeze(new SolidColorBrush(Color.FromRgb(244, 114, 182)));
            PinkBorder = Freeze(new SolidColorBrush(Color.FromRgb(219, 39, 119)));

            CyanBg = Freeze(new SolidColorBrush(Color.FromRgb(22, 78, 99)));
            CyanFg = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));
            CyanBorder = Freeze(new SolidColorBrush(Color.FromRgb(8, 145, 178)));

            PurpleFg = Freeze(new SolidColorBrush(Color.FromRgb(168, 85, 247)));
        }

        private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "IPC Port", Description = "TCP Port to connect to V12 Strategy", Order = 1, GroupName = "Connection")]
        public int IpcPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Connect", Description = "Automatically connect to IPC on load", Order = 2, GroupName = "Connection")]
        public bool AutoConnect { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Panel Width", Description = "Width of the sidebar panel (200-280)", Order = 1, GroupName = "Display")]
        public int PanelWidth { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V12 STANDARD - Vertical Sidebar Control Surface (Reliable)";
                Name = "V12_001";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = false;

                IpcPort = 5001; // V12.13-B: Match strategy's IPC port
                AutoConnect = true;
                PanelWidth = 210; // V12.21: Fluid UI - Standard NT Sidebar Width
            }
            else if (State == State.Configure)
            {
                Print("V12 STANDARD: BUILD 1013E (ChartTab Grid + Multi-Strategy FindChartTrader)"); 
            }
            else if (State == State.Historical)
            {
                // V12.13-C: Force-correct cached port 5000 -> 5001 for existing chart instances
                if (IpcPort == 5000)
                {
                    Print("V12 STANDARD: Correcting cached IPC port 5000 -> 5001");
                    IpcPort = 5001;
                }

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() => {
                        CreatePanel();
                        PopulateAccountCombos();
                    });
                }
            }
            else if (State == State.Realtime)
            {
                activeSymbol = Instrument.MasterInstrument.Name;
                if (AutoConnect) Task.Run(() => ConnectToStrategy());
            }
            else if (State == State.Terminated)
            {
                // V12.42: Final save before shutdown for crash safety
                try { SaveConfig(); } catch { }

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(RemovePanel);
                }
                DisconnectFromStrategy();
            }
        }

        #endregion

        #region OnBarUpdate / OnMarketData

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;
            lastPrice = Close[0];

            while (responseQueue.TryDequeue(out string response))
                ProcessStrategyResponse(response);
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e.MarketDataType == MarketDataType.Last)
            {
                lastPrice = e.Price;
                UpdatePriceDisplay();
            }
        }

        private void UpdatePriceDisplay()
        {
            if (ChartControl != null && lastPriceText != null)
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                {
                    lastPriceText.Text = lastPrice.ToString("F2");
                }));
            }
        }

        #endregion

        #region Panel Creation (SIDE-BY-SIDE DOCKING)

        // New Architecture Handles
        private Grid rootContainer;      // The main container hijacked into the slot
        private Border contentBody;      // The visible V12 Panel Body (Background + Controls)
        private Button floatingAnchor;   // The ALWAYS visible toggle button
        
        // Placement tracking
        private enum PlacementMode { None, Hijack, Injected, UserControlFallback }
        private PlacementMode placementMode = PlacementMode.None;
        private FrameworkElement chartTraderElement;
        private Grid rootGrid;

        private void CreatePanel()
        {
            if (panelCreated || ChartControl == null) return;

            try
            {
                // V12.13-E: Dump visual tree for diagnostics (runs once on panel creation)
                DumpVisualTree();

                rootContainer = new Grid { ClipToBounds = true, Width = double.NaN, HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.21: Fluid Shell

                // 2. Initialize V12 Content Body (The visual panel)
                // V12.12: Fixed width via rootContainer, left-aligned to prevent stretching
                contentBody = new Border
                {
                    Background = BgDeep,
                    BorderBrush = BorderSlate,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Width = double.NaN,
                    ClipToBounds = true
                };




                // Create Content - V12.7: Force horizontal stretch
                scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, // V12.7: Critical
                    PanningMode = PanningMode.VerticalOnly
                };

                mainStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7
                mainStack.Children.Add(CreateSection0_Identity());
                mainStack.Children.Add(CreateSection1_Execution());
                mainStack.Children.Add(CreateSection1_5_RiskManager());
                mainStack.Children.Add(CreateSection2_Telemetry());
                mainStack.Children.Add(CreateSection3_Config());

                scrollViewer.Content = mainStack;
                contentBody.Child = scrollViewer;

                // 3. Initialize Floating Anchor (Rescue Button)
                // MOVED to Bottom-Right to avoid covering Native "Sell Mkt" buttons
                floatingAnchor = CreateButton("[^]", 30, CyanBg, CyanFg, CyanBorder);
                floatingAnchor.FontSize = 14;
                floatingAnchor.HorizontalAlignment = HorizontalAlignment.Right;
                floatingAnchor.VerticalAlignment = VerticalAlignment.Bottom; // Moved from Top
                floatingAnchor.Margin = new Thickness(0, 0, 2, 2); // Bottom Right corner
                floatingAnchor.ToolTip = "Toggle V12 / Native Chart Trader";
                floatingAnchor.Click += ToggleLayout_Click; // Centralized Handler
                
                // V12: Final nudge to ensure combos are populated if NT state was weird during load
                PopulateAccountCombos();

                // Assemble Root
                rootContainer.Children.Add(contentBody);
                rootContainer.Children.Add(floatingAnchor); // Floating anchor on top

                // 4. PLACEMENT STRATEGY (V12.13-E)
                chartTraderElement = FindChartTrader();

                if (chartTraderElement != null && chartTraderElement.Parent is Grid traderGrid)
                {
                    // PATH 1: HIJACK - place in Chart Trader's grid slot
                    int col = Grid.GetColumn(chartTraderElement);
                    int row = Grid.GetRow(chartTraderElement);
                    int rSpan = Grid.GetRowSpan(chartTraderElement);
                    int cSpan = Grid.GetColumnSpan(chartTraderElement);

                    Grid.SetColumn(rootContainer, col);
                    Grid.SetRow(rootContainer, row);
                    if (rSpan > 1) Grid.SetRowSpan(rootContainer, rSpan);
                    if (cSpan > 1) Grid.SetColumnSpan(rootContainer, cSpan);

                    traderGrid.Children.Add(rootContainer);
                    rootGrid = traderGrid;

                    // Hide native AFTER placement is set up
                    chartTraderElement.Visibility = Visibility.Collapsed;
                    contentBody.Visibility = Visibility.Visible;
                    placementMode = PlacementMode.Hijack;
                    Print($"V12 STANDARD: Hijacked Native Slot (Col {col}, Row {row}, GridCols={traderGrid.ColumnDefinitions.Count})");
                }
                else
                {
                    // Clear stale reference if Parent was not a Grid
                    chartTraderElement = null;

                    // PATH 2: INJECT - add column to ChartTab grid (RIGHT of price axis)
                    Print("V12 STANDARD: Native Trader not found. Using Right-Column Injection.");
                    rootGrid = FindChartTabGrid(ChartControl);
                    if (rootGrid != null)
                    {
                        // Diagnostic: log existing columns before injection
                        Print($"V12 DIAG: Injecting into grid: Cols={rootGrid.ColumnDefinitions.Count}, Rows={rootGrid.RowDefinitions.Count}, Children={rootGrid.Children.Count}");
                        for (int ci = 0; ci < rootGrid.ColumnDefinitions.Count; ci++)
                        {
                            var cd = rootGrid.ColumnDefinitions[ci];
                            Print($"V12 DIAG:   Col[{ci}] Width={cd.Width} ActualWidth={cd.ActualWidth:F0}");
                        }

                        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelWidth) });
                        int panelCol = rootGrid.ColumnDefinitions.Count - 1;

                        Grid.SetColumn(rootContainer, panelCol);
                        Grid.SetRow(rootContainer, 0);
                        if (rootGrid.RowDefinitions.Count > 1)
                            Grid.SetRowSpan(rootContainer, rootGrid.RowDefinitions.Count);

                        rootContainer.HorizontalAlignment = HorizontalAlignment.Stretch;
                        rootContainer.Width = double.NaN;
                        contentBody.Width = double.NaN;

                        rootGrid.Children.Add(rootContainer);
                        placementMode = PlacementMode.Injected;
                        Print($"V12 STANDARD: Panel injected at Column {panelCol} (RIGHT of price axis)");
                    }
                    else
                    {
                        // PATH 3: FALLBACK - UserControlCollection (LEFT position)
                        Print("V12 STANDARD: No root grid found. Using UserControlCollection (LEFT fallback).");
                        UserControlCollection.Add(rootContainer);
                        placementMode = PlacementMode.UserControlFallback;
                    }
                }

                // Glow Timer
                glowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                glowTimer.Tick += (s, e) =>
                {
                    if (contentBody != null)
                    {
                        contentBody.BorderBrush = BorderSlate;
                        glowTimer.Stop();
                    }
                };

                Print("V12 STANDARD: Panel Created Successfully (Ghost Architecture)");

                // V12.42: Load saved configuration BEFORE panelCreated=true
                // This prevents TextChanged/SelectionChanged handlers from firing SaveConfig
                // with partially-loaded state during ApplySettings
                LoadConfig();
                panelCreated = true; // V12.42: AFTER LoadConfig - event handlers now safe
            }
            catch (Exception ex)
            {
                Print("V12 STANDARD: CreatePanel Error: " + ex.Message);
            }
        }

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            if (placementMode == PlacementMode.Hijack && chartTraderElement != null)
            {
                // Hijack path: toggle V12 <-> Native Chart Trader
                bool isV12Active = (contentBody.Visibility == Visibility.Visible);
                if (isV12Active)
                {
                    contentBody.Visibility = Visibility.Collapsed;
                    chartTraderElement.Visibility = Visibility.Visible;
                    Print("V12 STANDARD: Switched to Native View");
                }
                else
                {
                    contentBody.Visibility = Visibility.Visible;
                    chartTraderElement.Visibility = Visibility.Collapsed;
                    Print("V12 STANDARD: Switched to V12 View");
                }
            }
            else
            {
                // Inject/Fallback path: toggle panel visibility (minimize/restore)
                if (contentBody != null)
                {
                    bool isVisible = (contentBody.Visibility == Visibility.Visible);
                    contentBody.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                    Print($"V12 STANDARD: Panel {(isVisible ? "minimized" : "restored")}");
                }
            }
        }

        #region Config Persistence

        /// <summary>Captures current UI values into a ModeSettings object (includes T4/T5)</summary>
        private V12ModeSettings CaptureCurrentSettings()
        {
            return new V12ModeSettings
            {
                TargetCount = selectedTargetCount,
                T1Val = svT1Val?.Text ?? "1.0",
                T1Type = (svT1Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T2Val = svT2Val?.Text ?? "2.0",
                T2Type = (svT2Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T3Val = svT3Val?.Text ?? "3.0",
                T3Type = (svT3Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T4Val = svT4Val?.Text ?? "4.0",
                T4Type = (svT4Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                T5Val = svT5Val?.Text ?? "5.0",
                T5Type = (svT5Type?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                STR = strVal?.Text ?? "1.1",
                StopType = (svStrType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ATR",
                MAX = maxVal?.Text ?? "$1200",
                CIT = citVal?.Text ?? "0"
            };
        }

        /// <summary>Applies ModeSettings to UI controls (includes T4/T5)</summary>
        private void ApplySettings(V12ModeSettings settings)
        {
            if (settings == null) return;

            isApplyingSettings = true; // Prevent auto-save during UI updates
            try
            {
                if (strVal != null) strVal.Text = settings.STR ?? "1.1";
                if (maxVal != null) maxVal.Text = settings.MAX ?? "$1200";
                if (citVal != null) citVal.Text = settings.CIT ?? "0";

                if (svT1Val != null) svT1Val.Text = settings.T1Val ?? "1.0";
                if (svT2Val != null) svT2Val.Text = settings.T2Val ?? "2.0";
                if (svT3Val != null) svT3Val.Text = settings.T3Val ?? "3.0";
                if (svT4Val != null) svT4Val.Text = settings.T4Val ?? "4.0";
                if (svT5Val != null) svT5Val.Text = settings.T5Val ?? "5.0";

                SelectComboByValue(svT1Type, settings.T1Type);
                SelectComboByValue(svT2Type, settings.T2Type);
                SelectComboByValue(svT3Type, settings.T3Type);
                SelectComboByValue(svT4Type, settings.T4Type);
                SelectComboByValue(svT5Type, settings.T5Type);
                SelectComboByValue(svStrType, settings.StopType);

                selectedTargetCount = settings.TargetCount > 0 ? settings.TargetCount : 3;
                UpdateTargetCountVisual(selectedTargetCount);
                UpdateTargetVisibility(selectedTargetCount);

                // V12.27: Contextual UI - filter buttons/dropdown for active mode
                UpdateContextualUI(selectedConfigMode);
            }
            finally
            {
                isApplyingSettings = false;
            }
        }

        private void SaveConfig()
        {
            if (isApplyingSettings) return; // Don't save during mode switch

            try
            {
                if (fullConfig == null) fullConfig = new V12FullConfig();

                // Save current mode+count composite key
                fullConfig.ActiveMode = selectedConfigMode;
                fullConfig.ActiveCount = selectedTargetCount;
                fullConfig.SetSettings(selectedConfigMode, selectedTargetCount, CaptureCurrentSettings());

                // Write text file: header + all dictionary entries
                var sb = new StringBuilder();
                sb.AppendLine($"ActiveMode={fullConfig.ActiveMode}");
                sb.AppendLine($"ActiveCount={fullConfig.ActiveCount}");

                // V14 FIX: Persist LastUsedCountPerMode so modes remember their count on reload
                foreach (var kvp in fullConfig.LastUsedCountPerMode)
                {
                    sb.AppendLine($"LASTCOUNT_{kvp.Key}={kvp.Value}");
                }

                foreach (var kvp in fullConfig.Slots)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value.ToLine()}");
                }
                
                File.WriteAllText(ConfigFilePath, sb.ToString());
                Print($"V12 TOTAL RECALL: Saved {fullConfig.Slots.Count} slots, {fullConfig.LastUsedCountPerMode.Count} sticky counts (active: {selectedConfigMode}_{selectedTargetCount})");
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: SaveConfig error - {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Print("V12 STANDARD: No saved config found, using defaults");
                    fullConfig = new V12FullConfig();
                    return;
                }

                fullConfig = new V12FullConfig();
                var lines = File.ReadAllLines(ConfigFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    var idx = line.IndexOf('=');
                    var key = line.Substring(0, idx);
                    var val = line.Substring(idx + 1);

                    if (key == "ActiveMode")
                    {
                        fullConfig.ActiveMode = val;
                    }
                    else if (key == "ActiveCount")
                    {
                        int.TryParse(val, out int ac);
                        fullConfig.ActiveCount = ac > 0 ? ac : 3;
                    }
                    else if (key.StartsWith("LASTCOUNT_"))
                    {
                        // V14 FIX: Restore sticky count per mode
                        string modeKey = key.Substring(10); // Remove "LASTCOUNT_" prefix
                        if (int.TryParse(val, out int lastCount) && lastCount > 0)
                            fullConfig.LastUsedCountPerMode[modeKey] = lastCount;
                    }
                    else if (key.Contains("_"))
                    {
                        // TOTAL RECALL format: e.g. "ORB_3=..."
                        fullConfig.Slots[key] = V12ModeSettings.FromLine(val);
                    }
                    else if (key != "GLOBAL") // Skip stale/corrupt entries
                    {
                        // LEGACY format: bare mode name e.g. "ORB=..."
                        // Parse settings and store with default count from TargetCount field
                        var legacySettings = V12ModeSettings.FromLine(val);
                        string legacyKey = V12FullConfig.MakeKey(key, legacySettings.TargetCount);
                        if (!fullConfig.Slots.ContainsKey(legacyKey))
                            fullConfig.Slots[legacyKey] = legacySettings;
                    }
                }

                // Restore active mode + count
                string activeMode = fullConfig.ActiveMode ?? "ORB";
                
                // V14 FIX: Use the mode's sticky count (LastUsedCountPerMode) if available,
                // otherwise fall back to ActiveCount. This ensures mode-specific target counts
                // are correctly restored on reload (e.g., RMA remembers count=2, ORB remembers count=4).
                int activeCount;
                if (fullConfig.LastUsedCountPerMode.ContainsKey(activeMode))
                    activeCount = fullConfig.LastUsedCountPerMode[activeMode];
                else
                    activeCount = fullConfig.ActiveCount > 0 ? fullConfig.ActiveCount : 3;
                
                selectedConfigMode = activeMode;
                // Normalize: GetModeButton falls back to RMA for any unrecognized mode.
                // Sync selectedConfigMode to match so UI and execution engine stay consistent.
                string normalizedMode = activeMode.ToUpper();
                if (normalizedMode != "RMA" && normalizedMode != "RETEST" && normalizedMode != "TREND")
                    selectedConfigMode = "RMA";
                selectedTargetCount = activeCount;

                Button modeBtn = GetModeButton(activeMode);
                if (modeBtn != null) HighlightModeButton(modeBtn);

                // Apply active mode+count settings to UI
                ApplySettings(fullConfig.GetSettings(activeMode, activeCount));
                
                // V14 FIX: Also update count chip visuals to show the restored count
                UpdateTargetCountVisual(activeCount);

                // V12.45: Seed lastSyncedCountPerMode from persisted data so mode-switch logic works on reload
                foreach (var kvp in fullConfig.LastUsedCountPerMode)
                    lastSyncedCountPerMode[kvp.Key] = kvp.Value;

                Print($"V12 TOTAL RECALL: Loaded {fullConfig.Slots.Count} slots, {fullConfig.LastUsedCountPerMode.Count} sticky counts, active: {activeMode}_{activeCount}");
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: LoadConfig error - {ex.Message}");
                fullConfig = new V12FullConfig();
            }
        }

        private Button GetModeButton(string mode)
        {
            switch(mode.ToUpper())
            {
                case "RMA": return modeRmaButton;
                case "RETEST": return modeRetestButton;
                case "TREND": return modeTrendButton;
                default: return modeRmaButton;
            }
        }

        private void HighlightModeButton(Button btn)
        {
            foreach (var b in new[] { modeRmaButton, modeRetestButton, modeTrendButton })
            {
                if (b == null) continue;
                b.Background = BtnBg;
                b.Foreground = TextMuted;
                b.BorderBrush = BtnBorder;
            }
            if (btn != null)
            {
                btn.Background = CyanBg;
                btn.Foreground = CyanFg;
                btn.BorderBrush = CyanBorder;
            }
        }

        private void UpdateTargetCountVisual(int count)
        {
            // V14 FIX: Must run on UI thread ? may be called from TCP background thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateTargetCountVisual(count));
                return;
            }

            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                if (btn == null) continue;
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }
            
            Button targetBtn = null;
            switch(count)
            {
                case 1: targetBtn = cnt1; break;
                case 2: targetBtn = cnt2; break;
                case 3: targetBtn = cnt3; break;
                case 4: targetBtn = cnt4; break;
                case 5: targetBtn = cnt5; break;
            }
            
            if (targetBtn != null)
            {
                targetBtn.Background = CyanBg;
                targetBtn.Foreground = CyanFg;
                targetBtn.BorderBrush = CyanBorder;
            }
        }

        #endregion

        private void PopulateAccountCombos()
        {
            if (leaderAccountCombo == null) return;

            leaderAccountCombo.Items.Clear();
            selectedFleetAccounts.Clear();
            
            // Populate fleet checkbox panel if available
            if (fleetCheckboxPanel != null)
                fleetCheckboxPanel.Children.Clear();

            foreach (NinjaTrader.Cbi.Account account in NinjaTrader.Cbi.Account.All)
            {
                // Format with daily realized P/L
                double realizedPL = account.Get(NinjaTrader.Cbi.AccountItem.RealizedProfitLoss, NinjaTrader.Cbi.Currency.UsDollar);
                string plText = realizedPL >= 0 ? $"+${realizedPL:N0}" : $"-${Math.Abs(realizedPL):N0}";
                string displayName = $"{account.Name} [{plText}]";
                
                // Add to leader combo
                leaderAccountCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = account.Name });
                
                // Add checkbox to fleet panel
                if (fleetCheckboxPanel != null)
                {
                    CheckBox cb = new CheckBox
                    {
                        Content = displayName,
                        Tag = account.Name,
                        Foreground = TextPrimary,
                        FontSize = 10,
                        FontFamily = new FontFamily("Consolas"),
                        Margin = new Thickness(0, 1, 0, 1),
                        IsChecked = false // V12.Phase10 [DEFAULT-FIX]: Default all accounts to UNSELECTED
                    };
                    cb.Checked += (s, e) => {
                        if (!selectedFleetAccounts.Contains(account.Name))
                            selectedFleetAccounts.Add(account.Name);
                        UpdateFleetButtonText();
                        SendCommand($"TOGGLE_ACCOUNT|{account.Name}|1");
                    };
                    cb.Unchecked += (s, e) => {
                        selectedFleetAccounts.Remove(account.Name);
                        UpdateFleetButtonText();
                        SendCommand($"TOGGLE_ACCOUNT|{account.Name}|0");
                    };
                    fleetCheckboxPanel.Children.Add(cb);
                    
                    // V12.Phase10 [DEFAULT-FIX]: Do not add to selected list by default
                    // selectedFleetAccounts.Add(account.Name);
                }
            }

            if (leaderAccountCombo.Items.Count > 0) leaderAccountCombo.SelectedIndex = 0;
            
            UpdateFleetButtonText();
            Print($"V12 STANDARD: Populated {leaderAccountCombo.Items.Count} accounts with P/L.");
        }
        
        private void FleetSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (fleetPopup != null)
            {
                fleetPopup.IsOpen = !fleetPopup.IsOpen;
            }
        }
        
        private void SelectAllFleetAccounts(bool selectAll)
        {
            if (fleetCheckboxPanel == null) return;
            
            selectedFleetAccounts.Clear();
            foreach (var child in fleetCheckboxPanel.Children)
            {
                if (child is CheckBox cb)
                {
                    cb.IsChecked = selectAll;
                    if (selectAll && cb.Tag is string accountName)
                        selectedFleetAccounts.Add(accountName);
                }
            }
            UpdateFleetButtonText();
        }
        
        private void UpdateFleetButtonText()
        {
            if (fleetSelectButton == null) return;
            
            int count = selectedFleetAccounts.Count;
            int total = fleetCheckboxPanel?.Children.Count ?? 0;
            
            if (count == 0)
                fleetSelectButton.Content = "FLEET: None Selected [v]";
            else if (count == total)
                fleetSelectButton.Content = "FLEET: ALL Accounts [v]";
            else
                fleetSelectButton.Content = $"FLEET: {count} of {total} [v]";
        }

        private void RemovePanel()
        {
            if (!panelCreated) return;

            try
            {
                // Restore Native Trader visibility
                if (chartTraderElement != null)
                {
                    chartTraderElement.Visibility = Visibility.Visible;
                }

                // V12.13-E: Remove based on placement mode
                switch (placementMode)
                {
                    case PlacementMode.UserControlFallback:
                        if (rootContainer != null)
                        {
                            try { UserControlCollection.Remove(rootContainer); } catch { }
                        }
                        break;

                    case PlacementMode.Injected:
                        if (rootGrid != null && rootContainer != null)
                        {
                            if (rootGrid.Children.Contains(rootContainer))
                                rootGrid.Children.Remove(rootContainer);

                            // Remove the column we injected (always the LAST one)
                            if (rootGrid.ColumnDefinitions.Count > 0)
                            {
                                var lastCol = rootGrid.ColumnDefinitions[rootGrid.ColumnDefinitions.Count - 1];
                                if (lastCol.Width.IsAbsolute &&
                                    (Math.Abs(lastCol.Width.Value - PanelWidth) < 1 || Math.Abs(lastCol.Width.Value - 242) < 1))
                                {
                                    rootGrid.ColumnDefinitions.RemoveAt(rootGrid.ColumnDefinitions.Count - 1);
                                    Print("V12 STANDARD: Removed injected column");
                                }
                            }
                        }
                        break;

                    case PlacementMode.Hijack:
                        if (rootGrid != null && rootContainer != null)
                        {
                            if (rootGrid.Children.Contains(rootContainer))
                                rootGrid.Children.Remove(rootContainer);
                        }
                        break;
                }

                // Clean refs (Avoid memory leaks)
                rootContainer = null;
                contentBody = null;
                chartTraderElement = null;
                mainPanel = null;
                rootGrid = null;
                placementMode = PlacementMode.None;

                panelCreated = false;
                glowTimer?.Stop();
                Print("V12 STANDARD: Panel removed");
            }
            catch (Exception ex)
            {
                Print("V12 STANDARD: Removal error - " + ex.Message);
            }
        }

        // V12.13-E: Find the ChartTab-level Grid that holds [chart area | price axis | (chart trader)]
        private Grid FindChartTabGrid(DependencyObject child)
        {
            DependencyObject current = VisualTreeHelper.GetParent(child);
            Grid bestCandidate = null;

            while (current != null)
            {
                string typeName = current.GetType().Name;

                // Primary: ChartTab type -> its child Grid is the correct injection target
                if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                {
                    if (current is Grid chartTabGrid && chartTabGrid.ColumnDefinitions.Count >= 2)
                    {
                        Print($"V12 DIAG: Found ChartTab Grid directly: Cols={chartTabGrid.ColumnDefinitions.Count}, Children={chartTabGrid.Children.Count}");
                        return chartTabGrid;
                    }

                    int childCount = VisualTreeHelper.GetChildrenCount(current);
                    for (int i = 0; i < childCount; i++)
                    {
                        var ch = VisualTreeHelper.GetChild(current, i);
                        if (ch is Grid g && g.ColumnDefinitions.Count >= 2)
                        {
                            Print($"V12 DIAG: Found Grid inside ChartTab: Cols={g.ColumnDefinitions.Count}, Children={g.Children.Count}");
                            return g;
                        }
                    }
                }

                // Fallback: Track the HIGHEST Grid with 2+ columns (not the first)
                if (current is Grid grid && grid.ColumnDefinitions.Count >= 2)
                {
                    bestCandidate = grid;
                }

                if (current is Window) break;
                current = VisualTreeHelper.GetParent(current);
            }

            if (bestCandidate != null)
                Print($"V12 DIAG: Using highest candidate grid: Cols={bestCandidate.ColumnDefinitions.Count}");
            else
                Print("V12 DIAG: No suitable grid found in visual tree.");

            return bestCandidate;
        }

        // V12.13-E: Diagnostic - dump visual tree from ChartControl up to Window
        private void DumpVisualTree()
        {
            try
            {
                Print("=== V12 VISUAL TREE DUMP (ChartControl -> Window) ===");
                DependencyObject current = ChartControl;
                int depth = 0;

                while (current != null)
                {
                    string shortName = current.GetType().Name;
                    string info = $"  [{depth}] {shortName}";

                    if (current is FrameworkElement fe)
                        info += $" Name=\"{fe.Name}\" W={fe.ActualWidth:F0} H={fe.ActualHeight:F0} Vis={fe.Visibility}";

                    if (current is Grid grid)
                    {
                        info += $" Cols={grid.ColumnDefinitions.Count} Rows={grid.RowDefinitions.Count} Children={grid.Children.Count}";
                        for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
                        {
                            var cd = grid.ColumnDefinitions[i];
                            info += $"\n      Col[{i}]: Width={cd.Width} Actual={cd.ActualWidth:F0}";
                        }

                        // Flag ChartTrader children
                        for (int i = 0; i < grid.Children.Count; i++)
                        {
                            var ch = grid.Children[i];
                            if (ch.GetType().Name.Contains("ChartTrader"))
                                info += $"\n      ** ChartTrader child at index {i}: {ch.GetType().Name}";
                        }
                    }

                    Print(info);

                    if (current is Window) break;
                    current = VisualTreeHelper.GetParent(current);
                    depth++;
                }
                Print("=== END VISUAL TREE DUMP ===");
            }
            catch (Exception ex)
            {
                Print($"V12 DIAG: DumpVisualTree error - {ex.Message}");
            }
        }

        // Unused legacy methods kept for compilation safety if referenced elsewhere
        private void CreateOverlayOnChartTrader() { CreateFallbackOverlay(); }

        // V12.13-E: Multi-strategy ChartTrader finder. Does NOT set visibility - caller handles that.
        private FrameworkElement FindChartTrader()
        {
            try
            {
                // Strategy 1: ChartTab reflection (most reliable, proven in legacy backup)
                FrameworkElement result = FindChartTraderViaChartTab();
                if (result != null)
                {
                    Print($"V12 STANDARD: FindChartTrader Strategy 1 (ChartTab reflection) -> {result.GetType().Name}");
                    return result;
                }

                // Strategy 2: Sibling search - walk up from ChartControl to parent Grid, search siblings
                result = FindChartTraderBySiblingSearch();
                if (result != null)
                {
                    Print($"V12 STANDARD: FindChartTrader Strategy 2 (sibling search) -> {result.GetType().Name}");
                    return result;
                }

                // Strategy 3: Top-down type name search from Window
                result = FindChartTraderByTypeName();
                if (result != null)
                {
                    Print($"V12 STANDARD: FindChartTrader Strategy 3 (type name search) -> {result.GetType().Name}");
                    return result;
                }

                // Strategy 4: Button-based bottom-up (safe - stops at FIRST ChartTrader, no Grid fallback)
                result = FindChartTraderByButton();
                if (result != null)
                {
                    Print($"V12 STANDARD: FindChartTrader Strategy 4 (button search) -> {result.GetType().Name}");
                    return result;
                }

                Print("V12 STANDARD: FindChartTrader - all strategies failed. Chart Trader not present.");
                return null;
            }
            catch (Exception ex)
            {
                Print($"V12 STANDARD: FindChartTrader error - {ex.Message}");
                return null;
            }
        }

        // Strategy 1: Walk up to ChartTab, reflect to get ChartTrader property/field
        private FrameworkElement FindChartTraderViaChartTab()
        {
            try
            {
                DependencyObject current = ChartControl;
                object chartTab = null;

                // Visual tree walk
                while (current != null)
                {
                    string typeName = current.GetType().Name;
                    if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                    {
                        chartTab = current;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // Logical tree fallback
                if (chartTab == null)
                {
                    current = ChartControl;
                    while (current != null)
                    {
                        string typeName = current.GetType().Name;
                        if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                        {
                            chartTab = current;
                            break;
                        }
                        current = LogicalTreeHelper.GetParent(current);
                    }
                }

                if (chartTab == null) return null;

                Type tabType = chartTab.GetType();

                // Try property first
                PropertyInfo ctProp = tabType.GetProperty("ChartTrader",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ctProp != null)
                {
                    object ct = ctProp.GetValue(chartTab);
                    if (ct is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                        return fe;
                }

                // Try field names
                foreach (string fieldName in new[] { "chartTrader", "ChartTrader", "chartTraderControl", "_chartTrader" })
                {
                    FieldInfo fi = tabType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fi != null)
                    {
                        object ct = fi.GetValue(chartTab);
                        if (ct is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                            return fe;
                    }
                }

                // Last resort: search ChartTab's children for ChartTrader type
                if (chartTab is DependencyObject depObj)
                {
                    var found = FindChildElementByTypeName(depObj, "ChartTrader");
                    if (found != null && found.Visibility == Visibility.Visible)
                        return found;
                }
            }
            catch (Exception ex)
            {
                Print($"V12 DIAG: ChartTab reflection failed - {ex.Message}");
            }
            return null;
        }

        // Strategy 2: Walk up from ChartControl to nearest parent Grid, search siblings
        private FrameworkElement FindChartTraderBySiblingSearch()
        {
            try
            {
                DependencyObject current = ChartControl;
                while (current != null && !(current is Window))
                {
                    current = VisualTreeHelper.GetParent(current);
                    if (current is Grid grid)
                    {
                        foreach (UIElement child in grid.Children)
                        {
                            if (child is FrameworkElement fe && child.GetType().Name.Contains("ChartTrader")
                                && fe.Visibility == Visibility.Visible)
                                return fe;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // Strategy 3: Top-down search from Window for ChartTrader type
        private FrameworkElement FindChartTraderByTypeName()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null) return null;
                var found = FindChildElementByTypeName(chartWindow, "ChartTrader");
                if (found != null && found.Visibility == Visibility.Visible)
                    return found;
            }
            catch { }
            return null;
        }

        // Recursive helper: find first FrameworkElement with type name containing fragment
        private FrameworkElement FindChildElementByTypeName(DependencyObject parent, string typeNameFragment)
        {
            if (parent == null) return null;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.GetType().Name.Contains(typeNameFragment))
                    return fe;
                var result = FindChildElementByTypeName(child, typeNameFragment);
                if (result != null) return result;
            }
            return null;
        }

        // Strategy 4: Find "Buy Mkt" button, walk UP to FIRST ChartTrader match (not last)
        private FrameworkElement FindChartTraderByButton()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null) return null;

                var allBuyButtons = FindAllButtonsByText(chartWindow, "Buy Mkt");
                Print($"V12 DIAG: Button search found {allBuyButtons.Count} 'Buy Mkt' buttons");

                foreach (var btn in allBuyButtons)
                {
                    DependencyObject parent = VisualTreeHelper.GetParent(btn);
                    while (parent != null)
                    {
                        if (parent is FrameworkElement fe && fe.GetType().Name.Contains("ChartTrader"))
                        {
                            Print($"V12 DIAG: Found ChartTrader via button: {fe.GetType().Name} ({fe.Name})");
                            return fe;
                        }
                        if (parent is Window) break;
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"V12 DIAG: Button-based search error - {ex.Message}");
            }
            return null;
        }

        private List<Button> FindAllButtonsByText(DependencyObject parent, string text)
        {
            var list = new List<Button>();
            if (parent == null) return list;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button btn)
                {
                    if (btn.Content != null && btn.Content.ToString() == text)
                        list.Add(btn);
                }

                list.AddRange(FindAllButtonsByText(child, text));
            }
            return list;
        }

        private void CreateFallbackOverlay()
        {
            // Fallback: Right side overlay, positioned to NOT block price scale
            // Price scale is typically ~60px, so we position just inside that
            mainPanel = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanAccent,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Width = PanelWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)  // Far right edge
            };

            scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = BgDeep
            };

            mainStack = new StackPanel { Background = BgDeep };
            mainStack.Children.Add(CreateSection0_Identity());
            mainStack.Children.Add(CreateSection1_Execution());
            mainStack.Children.Add(CreateSection1_5_RiskManager());
            mainStack.Children.Add(CreateSection2_Telemetry());
            mainStack.Children.Add(CreateSection3_Config());

            scrollViewer.Content = mainStack;
            mainPanel.Child = scrollViewer;

            UserControlCollection.Add(mainPanel);
            panelCreated = true;
            Print("V12 STANDARD: Using fallback overlay (far right)");
        }

        // [Legacy RemovePanel removed to avoid duplication]

        #endregion

        #region Section 0: Identity

        private Border CreateSection0_Identity()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 1), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            // Section header
            stack.Children.Add(CreateSectionHeader("SECTION 0: IDENTITY"));

            // Row 1: Leader Account (Full Width)
            Grid row1 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row1.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.21: Fluid
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LED
            hubStatusLed = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = TextMuted,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "IPC Status"
            };
            Grid.SetColumn(hubStatusLed, 0);
            row1.Children.Add(hubStatusLed);
            leaderAccountCombo = CreateCombo(0, new[] { "LEADER: APEX_MA", "Sim101" });
            leaderAccountCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            leaderAccountCombo.SelectionChanged += LeaderAccount_SelectionChanged; // V12.6: Sync Logic
            Grid.SetColumn(leaderAccountCombo, 1);
            row1.Children.Add(leaderAccountCombo);
            stack.Children.Add(row1);

            // Row 2: Fleet Manager - Multi-Select with Checkboxes
            Grid fleetRow = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            fleetRow.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.21: Fluid
            fleetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Fleet selection button (shows count of selected accounts)
            fleetSelectButton = new Button
            {
                Content = "FLEET: Select Accounts [v]",
                Height = 22,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BorderSlate,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4, 0, 4, 0)
            };
            fleetSelectButton.Click += FleetSelectButton_Click;
            Grid.SetColumn(fleetSelectButton, 0);
            fleetRow.Children.Add(fleetSelectButton);
            
            // Create the popup for fleet selection
            fleetPopup = new Popup
            {
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = fleetSelectButton,
                AllowsTransparency = true
            };
            
            // Popup content: Border with checkbox list
            Border popupBorder = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanAccent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4),
                MinWidth = 180
            };
            
            StackPanel popupStack = new StackPanel();
            
            // "Select All" checkbox
            CheckBox selectAllCheck = new CheckBox
            {
                Content = "Select ALL Accounts",
                Foreground = CyanAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            selectAllCheck.Checked += (s, e) => { SelectAllFleetAccounts(true); };
            selectAllCheck.Unchecked += (s, e) => { SelectAllFleetAccounts(false); };
            popupStack.Children.Add(selectAllCheck);
            
            // Divider
            popupStack.Children.Add(new Border { Height = 1, Background = BorderSlate, Margin = new Thickness(0, 2, 0, 4) });
            
            // Account checkboxes container (populated dynamically)
            fleetCheckboxPanel = new StackPanel();
            popupStack.Children.Add(fleetCheckboxPanel);
            
            popupBorder.Child = popupStack;
            fleetPopup.Child = popupBorder;
            
            stack.Children.Add(fleetRow);




            section.Child = stack;
            return section;
        }

        #endregion

        #region Section 1: Execution

        private Border CreateSection1_Execution()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 2), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 1: EXECUTION"));

            // -------------------------------------------------------------------------------
            // 2-COLUMN LAYOUT - V12.7 RESTORED: Stretch to fill available width
            // LEFT:  Trade Entries | RIGHT: Management
            // -------------------------------------------------------------------------------
            Grid mainGrid = new Grid { Margin = new Thickness(2, 1, 2, 1) }; // V12.7: Small margins
            mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.7: Fill slot
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 

            // -------------------------------------------------------------------------------
            // LEFT COLUMN: Trade Entry Buttons
            // -------------------------------------------------------------------------------
            StackPanel leftCol = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            // RETEST row with R toggle
            // V12.3: Locked to 36px to match TRAIL input for perfect center-line
            execRetestRow = new Grid { Margin = new Thickness(0, 2, 0, 0) }; // V12.27: Store reference for contextual UI
            execRetestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            execRetestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches TrailDistInput
            
            retestButton = CreateButton("RETEST", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            retestButton.Click += (s, e) =>
            {
                retestCycleState = (retestCycleState + 1) % 3;
                switch(retestCycleState)
                {
                    case 0: retestButton.Content = "RETEST"; SendCommand("EXEC_RETEST"); break;
                    case 1: retestButton.Content = "RET +"; SendCommand("EXEC_RETEST_PLUS"); break;
                    case 2: retestButton.Content = "RET -"; SendCommand("EXEC_RETEST_MINUS"); break;
                }
                // V12.20: Reset after dispatch (retestCycleState already cycles, but RMA must reset)
                if (isRmaModeActive) { isRmaModeActive = false; SendCommand("SET_RMA_MODE|OFF"); UpdateRmaButtonVisual(false); }
                TriggerGlow(OrangeFg);
            };
            Grid.SetColumn(retestButton, 0);
            execRetestRow.Children.Add(retestButton);
            
            retestRmaToggle = CreateButton("R", 36, PurpleFg, TextPrimary, PurpleFg); // SYNC: Matches TrailDistInput
            retestRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            retestRmaToggle.Opacity = 0.5;
            retestRmaToggle.Click += (s, e) => { 
                isRetestRmaToggle = !isRetestRmaToggle;
                retestRmaToggle.Opacity = isRetestRmaToggle ? 1.0 : 0.5; 
                SendCommand(isRetestRmaToggle ? "MODE_RETEST_RMA" : "MODE_RETEST_STD");
            };
            Grid.SetColumn(retestRmaToggle, 1);
            execRetestRow.Children.Add(retestRmaToggle);
            leftCol.Children.Add(execRetestRow);

            // RMA button
            rmaButton = CreateButton("RMA", double.NaN, PurpleFg, TextPrimary, PurpleFg);
            rmaButton.Margin = new Thickness(0, 2, 0, 0);
            rmaButton.Click += (s, e) =>
            {
                isRmaModeActive = !isRmaModeActive;
                if (isRmaModeActive)
                {
                    rmaButton.Background = PurpleFg;
                    rmaButton.Foreground = Brushes.White;
                    rmaButton.Content = "RMA ON";
                    SendCommand("SET_RMA_MODE|ON");
                    
                    // V12.2: Switch config mode to RMA when activated
                    SelectConfigMode("RMA", modeRmaButton);
                    // V12.2: Sync external app to RMA mode
                    SendCommand("SYNC_MODE|RMA");
                }
                else
                {
                    rmaButton.Background = Brushes.Transparent;
                    rmaButton.Foreground = PurpleFg;
                    rmaButton.Content = "RMA";
                    SendCommand("SET_RMA_MODE|OFF");
                }
                TriggerGlow(PurpleFg);
            };
            leftCol.Children.Add(rmaButton);


            // TREND row with R toggle
            // V12.3: Locked to 36px to match TRAIL/RETEST for perfect center-line
            execTrendRow = new Grid { Margin = new Thickness(0, 2, 0, 0) }; // V12.27: Store reference for contextual UI
            execTrendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            execTrendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches TrailDistInput
            
            trendButton = CreateButton("TREND", double.NaN, BtnBg, TextPrimary, BtnBorder);
            trendButton.Click += (s, e) =>
            {
                string cmd = isTrendRmaToggle ? "EXEC_TREND_RMA" : "EXEC_TREND";
                SendCommand(cmd);
                ResetExecutionMode(); // V12.20: One Click = One Order
                TriggerGlow(CyanAccent);
                // V12.2: Switch config mode when entry button clicked
                SelectConfigMode("TREND", modeTrendButton);
                SendCommand("SYNC_MODE|TREND");
            };
            Grid.SetColumn(trendButton, 0);
            execTrendRow.Children.Add(trendButton);

            
            trendRmaToggle = CreateButton("R", 36, PurpleFg, TextPrimary, PurpleFg); // SYNC: Matches TrailDistInput
            trendRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            trendRmaToggle.Opacity = 0.5;
            trendRmaToggle.Click += (s, e) =>
            {
                isTrendRmaToggle = !isTrendRmaToggle;
                trendRmaToggle.Opacity = isTrendRmaToggle ? 1.0 : 0.5;
                SendCommand(isTrendRmaToggle ? "MODE_TREND_RMA" : "MODE_TREND_STD");
            };
            Grid.SetColumn(trendRmaToggle, 1);
            execTrendRow.Children.Add(trendRmaToggle);
            leftCol.Children.Add(execTrendRow);

            Grid.SetColumn(leftCol, 0);
            mainGrid.Children.Add(leftCol);

            // RIGHT COLUMN: Management Buttons
            // -------------------------------------------------------------------------------
            StackPanel rightCol = new StackPanel { Margin = new Thickness(1, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            // T1 button with dropdown menu
            t1Button = CreateButton("T1", double.NaN, GreenBg, GreenFg, GreenBorder);
            AttachTargetDropdown(t1Button, 1, GreenFg);
            rightCol.Children.Add(t1Button);

            // T2 button with dropdown menu
            t2Button = CreateButton("T2", double.NaN, YellowBg, YellowFg, YellowBorder);
            t2Button.Margin = new Thickness(0, 2, 0, 0);
            AttachTargetDropdown(t2Button, 2, YellowFg);
            rightCol.Children.Add(t2Button);

            // T3 button with dropdown menu
            t3Button = CreateButton("T3", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            t3Button.Margin = new Thickness(0, 2, 0, 0);
            AttachTargetDropdown(t3Button, 3, OrangeFg);
            rightCol.Children.Add(t3Button);

            // T4 button with dropdown menu
            t4Button = CreateButton("T4", double.NaN, RedBg, RedFg, RedBorder);
            t4Button.Margin = new Thickness(0, 2, 0, 0);
            AttachTargetDropdown(t4Button, 4, RedFg);
            rightCol.Children.Add(t4Button);

            // V14: T5 button with dropdown menu (NEW)
            t5Button = CreateButton("T5", double.NaN, PinkBg, PinkFg, PinkBorder);
            t5Button.Margin = new Thickness(0, 2, 0, 0);
            AttachTargetDropdown(t5Button, 5, PinkFg);
            rightCol.Children.Add(t5Button);



            // 50% button
            trim50Button = CreateButton("50%", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            trim50Button.Margin = new Thickness(0, 2, 0, 0);
            trim50Button.Click += (s, e) => { SendCommand("TRIM_50"); TriggerGlow(OrangeFg); };
            rightCol.Children.Add(trim50Button);

            // BE row: Input (ticks offset) + Button ? mirrors TRAIL row layout
            Grid beRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            beRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches trailDistInput width
            beRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            beOffsetInput = CreateTextBox(0, "2");
            beOffsetInput.Height = 22;
            beOffsetInput.FontSize = 10;
            beOffsetInput.ToolTip = "BE offset in ticks (e.g., 2 = move stop +2 ticks above entry)";
            Grid.SetColumn(beOffsetInput, 0);
            beRow.Children.Add(beOffsetInput);

            beButton = CreateButton("BE", double.NaN, CyanBg, CyanFg, CyanBorder);
            beButton.Margin = new Thickness(2, 0, 0, 0);
            beButton.Click += (s, e) =>
            {
                string ticks = beOffsetInput?.Text ?? "2";
                SendCommand($"BE_CUSTOM|{ticks}");
                TriggerGlow(CyanFg);
            };
            Grid.SetColumn(beButton, 1);
            beRow.Children.Add(beButton);

            rightCol.Children.Add(beRow);

            // TRAIL row: Input + Button
            // V12.3: Locked to 36px to match R-Toggle for perfect center-line
            Grid trailRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // SYNC: Matches retestRmaToggle
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            trailDistInput = CreateTextBox(0, "1.0");
            trailDistInput.Height = 22;
            trailDistInput.FontSize = 10;
            trailDistInput.ToolTip = "Trail distance in points";
            Grid.SetColumn(trailDistInput, 0);
            trailRow.Children.Add(trailDistInput);

            trailButton = CreateButton("TRAIL", double.NaN, BtnBg, TextPrimary, BtnBorder);
            trailButton.Margin = new Thickness(2, 0, 0, 0);
            trailButton.Click += (s, e) =>
            {
                string dist = trailDistInput?.Text ?? "1.0";
                SendCommand($"SET_TRAIL|{dist}");
                TriggerGlow(CyanFg);
            };
            Grid.SetColumn(trailButton, 1);
            trailRow.Children.Add(trailButton);

            rightCol.Children.Add(trailRow);

            // CANCEL + FLATTEN row (50/50 split)
            Grid cancelFlattenRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            cancelFlattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cancelFlattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            cancelButton = CreateButton("CANCEL", double.NaN, RedBg, RedFg, RedBorder);
            cancelButton.FontWeight = FontWeights.Bold;
            cancelButton.Click += (s, e) => { SendCommand("CANCEL_ALL"); TriggerGlow(RedFg); };
            Grid.SetColumn(cancelButton, 0);
            cancelFlattenRow.Children.Add(cancelButton);

            flattenButton = CreateButton("FLATTEN", double.NaN, RedBg, RedFg, RedBorder);
            flattenButton.FontWeight = FontWeights.Bold;
            flattenButton.Margin = new Thickness(2, 0, 0, 0);
            flattenButton.Click += (s, e) => { SendCommand("FLATTEN_ONLY"); TriggerGlow(RedFg); };
            Grid.SetColumn(flattenButton, 1);
            cancelFlattenRow.Children.Add(flattenButton);

            rightCol.Children.Add(cancelFlattenRow);

            Grid.SetColumn(rightCol, 1);
            mainGrid.Children.Add(rightCol);

            stack.Children.Add(mainGrid);

            // Price display row
            Grid priceRow = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            lastPriceText = new TextBlock
            {
                Text = "6961.25",
                Foreground = GreenFg,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            priceRow.Children.Add(lastPriceText);
            stack.Children.Add(priceRow);

            section.Child = stack;
            return section;
        }

        private Button CreateTargetButton(string text, Brush bg, Brush fg, Brush border, string command, int col)
        {
            Button btn = CreateButton(text, double.NaN, bg, fg, border);
            btn.Margin = col > 0 ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            btn.Click += (s, e) => { SendCommand(command); TriggerGlow(fg); };
            Grid.SetColumn(btn, col);
            return btn;
        }

        private Button CreateScaleButton(string text, Brush bg, Brush fg, Brush border, string command, int col)
        {
            Button btn = CreateButton(text, double.NaN, bg, fg, border);
            btn.Margin = col > 0 ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            btn.Click += (s, e) => { SendCommand(command); TriggerGlow(fg); };
            Grid.SetColumn(btn, col);
            return btn;
        }

        #endregion

        #region Section 1.5: Risk Manager

        private Border CreateSection1_5_RiskManager()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 1, 2, 1), HorizontalAlignment = HorizontalAlignment.Stretch };

            stack.Children.Add(CreateSectionHeader("SECTION 1.5: RISK MANAGER"));

            complianceSummaryText = new TextBlock
            {
                Text = "ACCT: -- / TRADES: 0 / DAYS: 0 / MAXDD: $0",
                Foreground = CyanAccent,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 1)
            };
            stack.Children.Add(complianceSummaryText);

            UniformGrid row = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 0) };

            complianceConsistencyText = new TextBlock
            {
                Text = "CONSISTENCY: --",
                Foreground = TextMuted,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            compliancePayoutText = new TextBlock
            {
                Text = "PAYOUT: --",
                Foreground = TextMuted,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            complianceDrawdownText = new TextBlock
            {
                Text = "DD BUFFER: --",
                Foreground = TextMuted,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            row.Children.Add(complianceConsistencyText);
            row.Children.Add(compliancePayoutText);
            row.Children.Add(complianceDrawdownText);

            stack.Children.Add(row);

            section.Child = stack;
            return section;
        }

        #endregion

        #region Section 2: Telemetry

        private Border CreateSection2_Telemetry()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 1), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

            // OR5 line
            or5Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 3, 0, 1)
            };
            or5Text.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6981.75") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6970.25") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 11.5)") { Foreground = TextMuted });
            stack.Children.Add(or5Text);

            // OR15 line
            or15Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 2)
            };
            or15Text.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6985.50") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6965.00") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 20.5)") { Foreground = TextMuted });
            stack.Children.Add(or15Text);

            // EMA line 1: 9, 15, 30
            StackPanel emaRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
            ema9Text = CreateEmaLabel("9:", "6972", TextPrimary);
            ema15Text = CreateEmaLabel("15:", "6975", TextPrimary);
            ema30Text = CreateEmaLabel("30:", "6959", GreenFg);
            emaRow1.Children.Add(ema9Text);
            emaRow1.Children.Add(ema15Text);
            emaRow1.Children.Add(ema30Text);
            stack.Children.Add(emaRow1);

            // EMA line 2: 65, 200, ATR
            StackPanel emaRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            ema65Text = CreateEmaLabel("65:", "6945", TextPrimary);
            ema200Text = CreateEmaLabel("200:", "6912", PurpleFg);
            atrText = new TextBlock
            {
                Text = "ATR: 12.5",
                Foreground = TextMuted,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            emaRow2.Children.Add(ema65Text);
            emaRow2.Children.Add(ema200Text);
            emaRow2.Children.Add(atrText);
            stack.Children.Add(emaRow2);

            // MKT SYNC + BULLISH/BEARISH
            Grid syncRow = new Grid();
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            mktSyncButton = CreateButton("MKT SYNC", 70, CyanBg, CyanFg, CyanBorder);
            mktSyncButton.Height = 24;
            mktSyncButton.FontSize = 9;
            mktSyncButton.Click += (s, e) => SendCommand("MKT_SYNC");
            Grid.SetColumn(mktSyncButton, 0);
            syncRow.Children.Add(mktSyncButton);

            trendIndicator = new Border
            {
                Background = GreenBg,
                BorderBrush = GreenBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(8, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Height = 24
            };
            trendText = new TextBlock
            {
                Text = "BULLISH",
                Foreground = GreenFg,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            };
            trendIndicator.Child = trendText;
            Grid.SetColumn(trendIndicator, 1);
            syncRow.Children.Add(trendIndicator);

            stack.Children.Add(syncRow);

            section.Child = stack;
            return section;
        }

        private TextBlock CreateEmaLabel(string label, string value, Brush valueColor)
        {
            TextBlock tb = new TextBlock
            {
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0)
            };
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = valueColor });
            return tb;
        }

        #endregion

        #region Section 3: Config

        private Border CreateSection3_Config()
        {
            Border section = CreateSectionBorder();
            section.BorderThickness = new Thickness(0); // No bottom border for last section
            StackPanel stack = new StackPanel { Margin = new Thickness(2, 2, 2, 4), HorizontalAlignment = HorizontalAlignment.Stretch }; // V12.7

            stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));

            // Two-column layout: Modes (left) | Counts (right)
            // V12.7 RESTORED: Stretch to fill available width
            Grid modeCountGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            modeCountGrid.HorizontalAlignment = HorizontalAlignment.Stretch; // V12.7: Fill slot
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 

            // LEFT COLUMN: Modes
            StackPanel modeColumn = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            modeRmaButton = CreateModeChip("RMA", false, -1);
            modeRetestButton = CreateModeChip("RETEST", false, -1);
            modeTrendButton = CreateModeChip("TREND", false, -1);

            modeColumn.Children.Add(modeRmaButton);
            modeColumn.Children.Add(modeRetestButton);
            modeColumn.Children.Add(modeTrendButton);
            Grid.SetColumn(modeColumn, 0);
            modeCountGrid.Children.Add(modeColumn);

            // RIGHT COLUMN: Counts
            StackPanel countColumn = new StackPanel { Margin = new Thickness(1, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            cnt1 = CreateCountChip("1", 1);
            cnt2 = CreateCountChip("2", 2);
            cnt3 = CreateCountChip("3", 3);
            cnt4 = CreateCountChip("4", 4);
            cnt5 = CreateCountChip("5", 5);

            // V12.42: No hardcoded default - LoadConfig calls UpdateTargetCountVisual to set active count

            countColumn.Children.Add(cnt1);
            countColumn.Children.Add(cnt2);
            countColumn.Children.Add(cnt3);
            countColumn.Children.Add(cnt4);
            countColumn.Children.Add(cnt5);
            Grid.SetColumn(countColumn, 1);
            modeCountGrid.Children.Add(countColumn);

            stack.Children.Add(modeCountGrid);

            // SV: T1 [1.0] ATR  T2 [2.0] ATR
            StackPanel svRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            svRow1.Children.Add(new TextBlock { Text = "SV:", Foreground = TextPrimary, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });

            svRow1.Children.Add(new TextBlock { Text = "T1", Foreground = GreenFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT1Val = CreateTextBox(30, "1.0"); svT1Val.Height = 20; svT1Val.FontSize = 9;
            svRow1.Children.Add(svT1Val);
            svT1Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts", "Runner" }); svT1Type.Height = 20; svT1Type.FontSize = 8; svT1Type.Margin = new Thickness(2, 0, 6, 0);
            svRow1.Children.Add(svT1Type);

            svRow1.Children.Add(new TextBlock { Text = "T2", Foreground = YellowFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT2Val = CreateTextBox(30, "2.0"); svT2Val.Height = 20; svT2Val.FontSize = 9;
            svRow1.Children.Add(svT2Val);
            svT2Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts", "Runner" }); svT2Type.Height = 20; svT2Type.FontSize = 8;
            svRow1.Children.Add(svT2Type);

            stack.Children.Add(svRow1);

            // T3 [3.0] ATR
            StackPanel svRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            svRow2.Children.Add(new TextBlock { Text = "       T3", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT3Val = CreateTextBox(30, "3.0"); svT3Val.Height = 20; svT3Val.FontSize = 9;
            svRow2.Children.Add(svT3Val);
            svT3Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts", "Runner" }); svT3Type.Height = 20; svT3Type.FontSize = 8; svT3Type.Margin = new Thickness(2, 0, 0, 0);
            svRow2.Children.Add(svT3Type);
            t3Row = svRow2;  // Store reference for visibility control
            stack.Children.Add(svRow2);

            // T4 [4.0] ATR (hidden by default)
            StackPanel svRow3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            svRow3.Children.Add(new TextBlock { Text = "       T4", Foreground = PurpleFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT4Val = CreateTextBox(30, "4.0"); svT4Val.Height = 20; svT4Val.FontSize = 9;
            svRow3.Children.Add(svT4Val);
            svT4Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts", "Runner" }); svT4Type.Height = 20; svT4Type.FontSize = 8; svT4Type.Margin = new Thickness(2, 0, 0, 0);
            svRow3.Children.Add(svT4Type);
            t4Row = svRow3;  // Store reference for visibility control
            stack.Children.Add(svRow3);

            // T5 [5.0] ATR (hidden by default)
            StackPanel svRow4 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3), Visibility = Visibility.Collapsed };
            svRow4.Children.Add(new TextBlock { Text = "       T5", Foreground = CyanFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT5Val = CreateTextBox(30, "5.0"); svT5Val.Height = 20; svT5Val.FontSize = 9;
            svRow4.Children.Add(svT5Val);
            svT5Type = CreateCombo(42, new[] { "ATR", "Ticks", "Pts", "Runner" }); svT5Type.Height = 20; svT5Type.FontSize = 8; svT5Type.Margin = new Thickness(2, 0, 0, 0);
            svRow4.Children.Add(svT5Type);
            t5Row = svRow4;  // Store reference for visibility control
            stack.Children.Add(svRow4);

            // STR: 1.1 ATR  MAX: $1200
            // V12: Force explicit width so Star columns work inside StackPanel
            // V12 TOTAL RECALL: Fixed-width grid to prevent STR/MAX stretching
            Grid riskRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            riskRow.HorizontalAlignment = HorizontalAlignment.Left;
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // "STR:"
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // STR input+combo
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // "MAX:"
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // MAX input

            TextBlock strLabel = new TextBlock { Text = "STR:", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
            Grid.SetColumn(strLabel, 0);
            riskRow.Children.Add(strLabel);

            strVal = CreateTextBox(33, "1.1"); strVal.Height = 20; strVal.FontSize = 9; strVal.Foreground = OrangeFg; // V12.25: Aligned with V12ModeSettings constructor (was "$150")
            
            // Stop Type Dropdown
            svStrType = CreateCombo(40, new[] { "ATR", "Ticks", "Pts", "OR" }); 
            svStrType.Height = 20; svStrType.FontSize = 8;
            svStrType.ToolTip = "Stop Type: ATR, Ticks, Points, or Open Range";
            svStrType.SelectionChanged += (s, e) =>
            {
                if (isApplyingSettings) return; // V12 TOTAL RECALL: Guard against save corruption
                string val = (svStrType.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(val)) SendCommand($"SET_STOP_TYPE|{val}");
            };

            StackPanel strStack = new StackPanel { Orientation = Orientation.Horizontal };
            strStack.Children.Add(strVal);
            svStrType.Margin = new Thickness(2, 0, 0, 0); 
            strStack.Children.Add(svStrType);
            
            Grid.SetColumn(strStack, 1);
            riskRow.Children.Add(strStack);

            TextBlock maxLabel = new TextBlock { Text = "MAX:", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) };
            Grid.SetColumn(maxLabel, 2);
            riskRow.Children.Add(maxLabel);

            maxVal = CreateTextBox(55, "$1200"); maxVal.Height = 20; maxVal.FontSize = 9; maxVal.Foreground = OrangeFg;
            Grid.SetColumn(maxVal, 3);
            riskRow.Children.Add(maxVal);

            stack.Children.Add(riskRow);

            // V12 TOTAL RECALL: CIT row with fixed width to prevent stretching
            Grid citRow = new Grid { Margin = new Thickness(0, 2, 0, 3) };
            citRow.HorizontalAlignment = HorizontalAlignment.Left;
            citRow.Height = 24;
            citRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // "CHASE:"
            citRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // CIT input

            TextBlock citLabel = new TextBlock { Text = "CHASE:", Foreground = OrangeFg, FontSize = 9, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            Grid.SetColumn(citLabel, 0);
            citRow.Children.Add(citLabel);

            citVal = CreateTextBox(55, "0"); citVal.Height = 20; citVal.FontSize = 10; citVal.Foreground = OrangeFg; citVal.FontWeight = FontWeights.Bold;
            citVal.ToolTip = "Chase If Touch: Points offset (0 = disabled)";
            citVal.LostFocus += (s, e) => { string v = citVal?.Text?.Trim(); if (!string.IsNullOrEmpty(v)) SendCommand($"SET_CIT|{v}"); };
            Grid.SetColumn(citVal, 1);
            citRow.Children.Add(citVal);

            stack.Children.Add(citRow);

            // SYNC ALL button
            syncAllButton = CreateButton("SYNC ALL", double.NaN, CyanBg, CyanFg, CyanBorder);
            syncAllButton.Height = 24;
            syncAllButton.FontWeight = FontWeights.Bold;
            syncAllButton.Click += SyncAll_Click;
            stack.Children.Add(syncAllButton);

            section.Child = stack;
            return section;
        }

        private Button CreateModeChip(string text, bool isActive, int col)
        {
            Button btn = new Button
            {
                Content = text,
                Background = isActive ? CyanBg : BtnBg,
                Foreground = isActive ? CyanFg : TextMuted,
                BorderBrush = isActive ? CyanBorder : BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 22,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            btn.Click += (s, e) => SelectConfigMode(text, btn);
            if (col >= 0) Grid.SetColumn(btn, col);
            return btn;
        }

        private Button CreateCountChip(string text, int count)
        {
            Button btn = CreateButton(text, double.NaN, BtnBg, TextPrimary, BtnBorder); 
            btn.Height = 22;
            btn.FontSize = 9;
            btn.Margin = new Thickness(0, 0, 0, 2);
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            btn.Click += (s, e) => SelectTargetCount(count, btn);
            return btn;
        }

        private void SelectConfigMode(string mode, Button clickedBtn)
        {
            // V12 TOTAL RECALL: Save current mode+count combo before switching
            if (fullConfig == null) fullConfig = new V12FullConfig();
            fullConfig.SetSettings(selectedConfigMode, selectedTargetCount, CaptureCurrentSettings());
            // V12.45: Save SYNCED count (not clicked count) as sticky for the OLD mode
            if (lastSyncedCountPerMode.ContainsKey(selectedConfigMode))
                fullConfig.LastUsedCountPerMode[selectedConfigMode] = lastSyncedCountPerMode[selectedConfigMode];
            // else: don't overwrite -- keep whatever was previously persisted

            // Switch to new mode
            selectedConfigMode = mode;
            fullConfig.ActiveMode = mode;
            SendCommand($"SET_MODE|{mode}");

            // Update mode button visuals
            HighlightModeButton(clickedBtn);

            // V12.18: Restore sticky count for the NEW mode (or keep current if first use)
            int stickyCount;
            if (!fullConfig.LastUsedCountPerMode.TryGetValue(mode, out stickyCount))
                stickyCount = selectedTargetCount;
            selectedTargetCount = stickyCount;
            fullConfig.ActiveCount = stickyCount;

            // Update count button visuals to match restored count
            var countButtons = new[] { cnt1, cnt2, cnt3, cnt4, cnt5 };
            for (int i = 0; i < countButtons.Length; i++)
            {
                if (countButtons[i] == null) continue;
                bool isSelected = (i + 1) == stickyCount;
                countButtons[i].Background = isSelected ? CyanBg : BtnBg;
                countButtons[i].Foreground = isSelected ? CyanFg : TextPrimary;
                countButtons[i].BorderBrush = isSelected ? CyanBorder : BtnBorder;
            }

            // Load mode's settings for the STICKY count
            ApplySettings(fullConfig.GetSettings(mode, stickyCount));
            SendCommand($"SET_TARGETS|{stickyCount}");

            // V12.27: Contextual UI - filter buttons/dropdown for active mode
            UpdateContextualUI(mode);

            // Persist the change
            SaveConfig();
        }

        private void SelectTargetCount(int count, Button clickedBtn)
        {
            // V12 TOTAL RECALL: Save current mode+count before switching count
            if (fullConfig == null) fullConfig = new V12FullConfig();
            fullConfig.SetSettings(selectedConfigMode, selectedTargetCount, CaptureCurrentSettings());

            // Switch count
            selectedTargetCount = count;
            // V12.45: Removed sticky write here -- count is only committed on SyncAll_Click
            SendCommand($"SET_TARGETS|{count}");

            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                if (btn == null) continue;
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }

            clickedBtn.Background = CyanBg;
            clickedBtn.Foreground = CyanFg;
            clickedBtn.BorderBrush = CyanBorder;

            // Load settings for current mode + NEW count
            ApplySettings(fullConfig.GetSettings(selectedConfigMode, count));

            // Persist the change
            SaveConfig();
        }

        public void UpdateTargetVisibility(int count)
        {
            // V14 FIX: Must run on UI thread ? called from TCP background thread via SYNC_TARGET_STATE
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateTargetVisibility(count));
                return;
            }

            // Config section (Section 3) - Enable/disable inputs and show/hide rows
            if (svT2Val != null) svT2Val.IsEnabled = count >= 2;
            if (svT2Type != null) svT2Type.IsEnabled = count >= 2;
            if (svT3Val != null) svT3Val.IsEnabled = count >= 3;
            if (svT3Type != null) svT3Type.IsEnabled = count >= 3;
            if (svT4Val != null) svT4Val.IsEnabled = count >= 4;
            if (svT4Type != null) svT4Type.IsEnabled = count >= 4;
            if (svT5Val != null) svT5Val.IsEnabled = count >= 5;
            if (svT5Type != null) svT5Type.IsEnabled = count >= 5;

            // Show/hide config rows based on count
            if (t2Row != null) t2Row.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            if (t3Row != null) t3Row.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            if (t4Row != null) t4Row.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
            if (t5Row != null) t5Row.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;

            // V14: Execution section (Section 1) - Show/hide target buttons
            // T1 always visible (every config has at least 1 target)
            if (t1Button != null) t1Button.Visibility = Visibility.Visible;
            
            // T2-T5 visibility based on selected count
            if (t2Button != null) t2Button.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            if (t3Button != null) t3Button.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            if (t4Button != null) t4Button.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
            if (t5Button != null) t5Button.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;
            
            Print($"V14: UpdateTargetVisibility({count}) - Execution buttons: T1=V, T2={count>=2}, T3={count>=3}, T4={count>=4}, T5={count>=5}");
        }

        #endregion

        #region UI Helpers

        private void UpdateContextualUI(string mode)
        {
            // V12.27: Must run on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateContextualUI(mode));
                return;
            }

            string upperMode = mode.ToUpper();

            // Hide all contextual entry buttons by default
            if (execRetestRow != null) execRetestRow.Visibility = Visibility.Collapsed;
            if (execTrendRow != null) execTrendRow.Visibility = Visibility.Collapsed;
            if (rmaButton != null) rmaButton.Visibility = Visibility.Collapsed;

            // Show elements relevant to the active mode
            switch (upperMode)
            {
                case "RMA":
                    if (rmaButton != null) rmaButton.Visibility = Visibility.Visible;
                    break;
                case "RETEST":
                    if (execRetestRow != null) execRetestRow.Visibility = Visibility.Visible;
                    break;
                case "TREND":
                    if (execTrendRow != null) execTrendRow.Visibility = Visibility.Visible;
                    break;
                default:
                    if (rmaButton != null) rmaButton.Visibility = Visibility.Visible;
                    break;
            }

            Print($"V12.27: Contextual UI updated for mode: {mode}");
        }

        private Border CreateSectionBorder()
        {
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 20, 25)),
                BorderThickness = new Thickness(0, 0, 0, 0.5),
                Background = BgDeep,
                HorizontalAlignment = HorizontalAlignment.Stretch, // V12.7: Fill width
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CyanAccent,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private Button CreateButton(string text, double width, Brush bg, Brush fg, Brush border)
        {
            Button btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10, // V12: Reduced from 11
                FontWeight = FontWeights.SemiBold,
                Height = 22, // V12: Confirmed height
                Padding = new Thickness(2, 0, 2, 0), // V12: Reduced padding
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (!double.IsNaN(width)) btn.Width = width;
            return btn;
        }

        /// <summary>
        /// V14: Creates a context menu for target buttons with Liquidate + Move options
        /// </summary>
        /// <param name="targetNum">Target number (1-5)</param>
        /// <param name="glowColor">Color to trigger glow effect</param>
        /// <summary>
        /// V14 FIX: Attach context menu AND wire click to open it with explicit PlacementTarget.
        /// NinjaTrader's WPF hosting sometimes swallows ContextMenu popups that lack a PlacementTarget.
        /// </summary>
        private void AttachTargetDropdown(Button btn, int targetNum, SolidColorBrush glowColor)
        {
            ContextMenu menu = CreateTargetContextMenu(targetNum, glowColor);
            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu = menu;
            btn.Click += (s, e) =>
            {
                if (btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            };
        }

        private ContextMenu CreateTargetContextMenu(int targetNum, SolidColorBrush glowColor)
        {
            ContextMenu menu = new ContextMenu
            {
                Background = BgSlate,
                BorderBrush = BorderSlate,
                Foreground = TextPrimary
            };

            // Option 1: Liquidate at Market
            MenuItem closeItem = new MenuItem
            {
                Header = $"Liquidate T{targetNum} at Market",
                Foreground = RedFg,
                Background = BgSlate,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };
            closeItem.Click += (s, e) =>
            {
                SendCommand($"CLOSE_T{targetNum}");
                TriggerGlow(glowColor);
                Print($"V14: Liquidate T{targetNum} requested");
            };
            menu.Items.Add(closeItem);

            // Separator
            menu.Items.Add(new Separator { Background = BorderSlate });

            // Option 2: Move to +1pt
            MenuItem move1PtItem = new MenuItem
            {
                Header = $"Move T{targetNum} to +1pt",
                Foreground = GreenFg,
                Background = BgSlate,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };
            move1PtItem.Click += (s, e) =>
            {
                SendCommand($"MOVE_TARGET|T{targetNum}|1pt");
                TriggerGlow(CyanAccent);
                Print($"V14: Move T{targetNum} to +1pt requested");
            };
            menu.Items.Add(move1PtItem);

            // Option 3: Move to +2pt
            MenuItem move2PtItem = new MenuItem
            {
                Header = $"Move T{targetNum} to +2pt",
                Foreground = YellowFg,
                Background = BgSlate,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };
            move2PtItem.Click += (s, e) =>
            {
                SendCommand($"MOVE_TARGET|T{targetNum}|2pt");
                TriggerGlow(CyanAccent);
                Print($"V14: Move T{targetNum} to +2pt requested");
            };
            menu.Items.Add(move2PtItem);

            return menu;
        }

        private Button CreateDashedButton(string text, Brush fg)
        {
            return new Button
            {
                Content = text,
                Background = BgSlate,
                Foreground = fg,
                BorderBrush = fg,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10, // V12: Reduced from 12
                FontWeight = FontWeights.Bold,
                Height = 22, // V12: Reduced from 24
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private TextBox CreateTextBox(double width, string defaultText)
        {
            TextBox tb = new TextBox
            {
                Text = defaultText,
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 20,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Prevent NinjaTrader from intercepting keyboard input (symbol lookup)
            // Mark as handled BEFORE the TextBox processes it to stop NT8 search dialog
            tb.PreviewKeyDown += (s, e) =>
            {
                // Let Tab/Enter/Escape bubble for navigation
                if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)
                    return;
                
                // Stop event from bubbling to NinjaTrader chart - prevents symbol search
                e.Handled = true;
                
                // Manually handle the key input for the TextBox
                TextBox textBox = s as TextBox;
                if (textBox == null) return;
                
                string keyChar = "";
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    keyChar = ((char)('0' + (e.Key - Key.D0))).ToString();
                else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    keyChar = ((char)('0' + (e.Key - Key.NumPad0))).ToString();
                else if (e.Key == Key.Back && textBox.Text.Length > 0 && textBox.SelectionStart > 0)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos - 1, 1);
                    textBox.SelectionStart = pos - 1;
                    return;
                }
                else if (e.Key == Key.Delete && textBox.SelectionStart < textBox.Text.Length)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos, 1);
                    textBox.SelectionStart = pos;
                    return;
                }
                else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                    keyChar = ".";
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    keyChar = "-";
                else if (e.Key == Key.Space)
                    keyChar = " ";
                else
                    return;  // Ignore other keys
                
                int caret = textBox.SelectionStart;
                textBox.Text = textBox.Text.Insert(caret, keyChar);
                textBox.SelectionStart = caret + 1;
            };
            tb.GotKeyboardFocus += (s, e) =>
            {
                // Stop bubbling to prevent NT8 chart keyboard shortcuts
                e.Handled = true;
            };

            // Auto-save on text change
            tb.TextChanged += (s, e) => { if (panelCreated && !isApplyingSettings) SaveConfig(); }; // V12.42: Match combo guard

            if (width > 0) tb.Width = width;
            return tb;
        }

        private ComboBox CreateCombo(double width, string[] items)
        {
            ComboBox combo = new ComboBox
            {
                Background = BtnBg,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                // Remove VerticalContentAlignment - let default handle it
            };

            if (width > 0) combo.Width = width;

            foreach (var item in items)
            {
                combo.Items.Add(new ComboBoxItem { Content = item });
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            // Auto-save on selection change
            combo.SelectionChanged += (s, e) => { if (panelCreated && !isApplyingSettings) SaveConfig(); }; // V12 TOTAL RECALL: Guard

            return combo;
        }

        private void TriggerGlow(Brush color)
        {
            if (mainPanel != null)
            {
                mainPanel.BorderBrush = color;
                mainPanel.BorderThickness = new Thickness(2, 0, 0, 0);
                glowTimer?.Stop();
                glowTimer?.Start();
            }
        }

        // V12.20: "One Click = One Order" ? Reset all execution mode toggles after any order dispatch
        // Trading Rule: Every single trade requires a fresh click of the mode button.
        private void ResetExecutionMode()
        {
            // Reset RMA mode
            if (isRmaModeActive)
            {
                isRmaModeActive = false;
                SendCommand("SET_RMA_MODE|OFF");
            }
            UpdateRmaButtonVisual(false);

            // Reset RETEST cycle
            retestCycleState = 0;
            if (retestButton != null) retestButton.Content = "RETEST";

            // V12.20-B: Reset TREND R toggle
            if (isTrendRmaToggle)
            {
                isTrendRmaToggle = false;
                SendCommand("MODE_TREND_STD");
                if (trendRmaToggle != null) trendRmaToggle.Opacity = 0.5;
            }

            // V12.20-B: Reset RETEST R toggle
            if (isRetestRmaToggle)
            {
                isRetestRmaToggle = false;
                SendCommand("MODE_RETEST_STD");
                if (retestRmaToggle != null) retestRmaToggle.Opacity = 0.5;
            }

            Print("V12.20: ResetExecutionMode ? all modes reset including R toggles (One Click = One Order)");
        }

        // V12.14: Helper to sync RMA button visual from IPC state
        private void UpdateRmaButtonVisual(bool active)
        {
            if (rmaButton == null) return;
            if (active)
            {
                rmaButton.Background = PurpleFg;
                rmaButton.Foreground = Brushes.White;
                rmaButton.Content = "RMA [>]";
            }
            else
            {
                rmaButton.Background = Brushes.Transparent;
                rmaButton.Foreground = PurpleFg;
                rmaButton.Content = "RMA";
            }
        }

        #endregion

        #region Event Handlers

        private void Fleet_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("GET_FLEET");
        }

        private void LeaderAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (leaderAccountCombo == null || !panelCreated) return;
            
            ComboBoxItem selected = leaderAccountCombo.SelectedItem as ComboBoxItem;
            if (selected != null && selected.Tag is string accountName)
            {
                // V12.25: Inform the strategy of the new leader account
                SendCommand($"SET_LEADER_ACCOUNT|{accountName}");
                Print($"V12.25: Leader Account changed ? {accountName}");
                
                // Sync the Native Chart Trader to this account
                SyncChartTraderAccount(accountName);
            }
        }

        private void SyncChartTraderAccount(string accountName)
        {
            // Goal: Find the 'AccountSelector' in the hidden chartTraderElement and set it
            if (chartTraderElement == null) return;
            
            try 
            {
                // Find the AccountSelector control (it's a ComboBox or Selector)
                Selector accountSelector = FindAccountSelector(chartTraderElement);
                if (accountSelector != null)
                {
                    foreach (var item in accountSelector.Items)
                    {
                        // NinjaTrader account items usually have a specific type, check string representation or Name
                        string itemText = item.ToString();
                        
                        if (itemText == accountName || (item is NinjaTrader.Cbi.Account acct && acct.Name == accountName))
                        {
                            accountSelector.SelectedItem = item;
                            Print($"V12 STANDARD: Synced Chart Trader to {accountName}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 Print($"V12 STANDARD: Sync Error - {ex.Message}");
            }
        }

        private Selector FindAccountSelector(DependencyObject parent)
        {
            if (parent == null) return null;
            
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for(int i=0; i<count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Selector selector)
                {
                    if (selector.Items.Count > 0)
                    {
                         var first = selector.Items[0];
                         if (first is NinjaTrader.Cbi.Account || first.ToString().Contains("Sim101"))
                            return selector;
                    }
                }
                
                var result = FindAccountSelector(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"CONFIG|{selectedConfigMode}|");
            sb.Append($"COUNT:{selectedTargetCount};");
            sb.Append($"T1:{svT1Val.Text};T1TYPE:{(svT1Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T2:{svT2Val.Text};T2TYPE:{(svT2Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T3:{svT3Val.Text};T3TYPE:{(svT3Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T4:{svT4Val.Text};T4TYPE:{(svT4Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};"); // V12.18: T4 was missing!
            sb.Append($"T5:{svT5Val.Text.Replace(" ", "").Replace("$", "")};T5TYPE:{(svT5Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};"); // V12.18: Sanitize $ prefix
            sb.Append($"STR:{strVal.Text};STRTYPE:{(svStrType.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};MAX:{maxVal.Text};");
            sb.Append($"CIT:{citVal.Text};");

            SendCommand(sb.ToString());

            // V12.45: Commit current count to sticky memory ONLY on explicit sync
            if (fullConfig != null)
            {
                fullConfig.LastUsedCountPerMode[selectedConfigMode] = selectedTargetCount;
                lastSyncedCountPerMode[selectedConfigMode] = selectedTargetCount;
            }
            SaveConfig();
            Print($"V12.45: SYNC committed -> {selectedConfigMode} sticky count = {selectedTargetCount}");
        }

        #endregion

        #region IPC Communication

        private void ConnectToStrategy()
        {
            try
            {
                lock (tcpLock)
                {
                    if (isConnected) return;

                    tcpClient = new TcpClient();
                    tcpClient.Connect("127.0.0.1", IpcPort);
                    tcpStream = tcpClient.GetStream();
                    isConnected = true;
                    // [Build 934]: Reset retry counters on successful connect
                    _ipcRetryCount    = 0;
                    _lastRetryLogTime = DateTime.MinValue;

                    Print($"V12 Panel: Strategy connected on port {IpcPort} ?");

                    if (ChartControl != null)
                    {
                        ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (hubStatusLed != null)
                            {
                                hubStatusLed.Background = GreenFg;
                                hubStatusLed.ToolTip = "IPC Connected";
                            }
                        }));
                    }

                    receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "V12_Std_Receive" };
                    receiveThread.Start();

                    SendCommand("GET_LAYOUT");
                }
            }
            catch (Exception)
            {
                isConnected = false;
                _ipcRetryCount++;

                // [Build 934]: Log only on first failure and then at most once per 60 seconds
                if (_ipcRetryCount == 1 || (DateTime.Now - _lastRetryLogTime).TotalSeconds >= 60)
                {
                    Print($"V12 Panel: Strategy offline -- retrying in background (attempt #{_ipcRetryCount})");
                    _lastRetryLogTime = DateTime.Now;
                }

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (hubStatusLed != null)
                        {
                            hubStatusLed.Background = TextMuted;
                            hubStatusLed.ToolTip = "Waiting for Strategy (retrying...)";
                        }
                    }));
                }

                // [Build 933]: Start retry loop on initial failure (market closed / Strategy not yet live).
                ScheduleReconnect();
            }
        }

        private void DisconnectFromStrategy()
        {
            isShuttingDown = true;
            reconnectTimer?.Dispose();
            lock (tcpLock)
            {
                isConnected = false;
                try { tcpStream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
                tcpStream = null;
                tcpClient = null;
            }
        }

        private void SendCommand(string command)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!isConnected) ConnectToStrategy();

                    lock (tcpLock)
                    {
                        if (tcpStream != null && tcpStream.CanWrite)
                        {
                            if (!command.Contains("|"))
                                command = $"{command}|{activeSymbol}";

                            byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                            tcpStream.Write(data, 0, data.Length);
                            tcpStream.Flush();

                            Print($"V12 STD: Sent -> {command}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"V12 STD: Send error - {ex.Message}");
                    isConnected = false;
                }
            });
        }

        private void ReceiveLoop()
        {
            StringBuilder buffer = new StringBuilder();
            byte[] readBuffer = new byte[4096];

            try
            {
                while (isConnected)
                {
                    try
                    {
                        if (tcpStream == null || !tcpStream.CanRead) break;

                        int bytesRead = tcpStream.Read(readBuffer, 0, readBuffer.Length);
                        if (bytesRead == 0)
                        {
                            Print("V12.14: ReceiveLoop - server closed connection (0 bytes)");
                            break;
                        }

                        buffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));

                        string data = buffer.ToString();
                        int newlineIdx;
                        while ((newlineIdx = data.IndexOf('\n')) >= 0)
                        {
                            string message = data.Substring(0, newlineIdx).Trim();
                            data = data.Substring(newlineIdx + 1);

                            if (!string.IsNullOrEmpty(message))
                            {
                                if (message.Length > 4096)
                                    Print("[V12 IPC REJECT] Malformed or oversized message discarded. Length=" + message.Length);
                                else
                                    responseQueue.Enqueue(message);
                            }
                        }
                        buffer.Clear();
                        buffer.Append(data);
                    }
                    catch (Exception ex)
                    {
                        Print($"V12.14: ReceiveLoop exception - {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                // V12.14: Cleanup - mark disconnected and schedule reconnect
                Print("V12.14: ReceiveLoop exited - marking disconnected");
                lock (tcpLock)
                {
                    isConnected = false;
                    try { tcpStream?.Close(); } catch { }
                    try { tcpClient?.Close(); } catch { }
                    tcpStream = null;
                    tcpClient = null;
                }

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (hubStatusLed != null)
                        {
                            hubStatusLed.Background = TextMuted;
                            hubStatusLed.ToolTip = "IPC Disconnected";
                        }
                    }));
                }

                ScheduleReconnect();
            }
        }

        // V12.14: Auto-reconnect after unexpected disconnect
        // V12.1101E [B-8]: Refactored to non-recursive -- reuses a single timer object via .Change()
        // instead of Dispose + new Timer on each failure. Prevents timer object accumulation and
        // eliminates recursive call stack growth under sustained disconnection.
        private readonly object _reconnectLock = new object();

        private void ScheduleReconnect()
        {
            if (isShuttingDown || isConnected) return;

            lock (_reconnectLock)
            {
                if (reconnectTimer != null)
                {
                    // Reset the existing timer to fire again in 3 s -- no new allocation needed
                    reconnectTimer.Change(3000, Timeout.Infinite);
                    return;
                }

                // First time: create the timer once; subsequent reconnect calls reuse it via .Change()
                reconnectTimer = new System.Threading.Timer(_ =>
                {
                    if (isShuttingDown || isConnected) return;

                    // [Build 934]: Removed per-attempt "Auto-reconnect attempting..." print -- now throttled in ConnectToStrategy() catch block
                    try
                    {
                        ConnectToStrategy();
                        if (isConnected)
                        {
                            Print("V12 Panel: Strategy came online -- connected ?");
                            lock (_reconnectLock) { reconnectTimer = null; }
                        }
                        else
                        {
                            ScheduleReconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        // [Build 934]: Logging is throttled inside ConnectToStrategy() catch -- no extra print here
                        ScheduleReconnect();
                    }
                }, null, 3000, Timeout.Infinite);
            }
        }

        private void ProcessStrategyResponse(string response)
        {
            string[] parts = response.Split('|');
            if (parts.Length == 0) return;

            if (parts[0] == "COMPLIANCE")
            {
                ParseCompliance(parts);
                return;
            }

            if (ChartControl != null)
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (parts[0] == "TELEMETRY" && parts.Length > 1)
                            ParseTelemetry(parts[1]);
                        else if (parts[0] == "CONFIG" && parts.Length > 2)
                        {
                            // V12.14: parts[1] = MODE (e.g. "RMA" or "OR"), parts[2] = key:value data
                            string mode = parts[1].Trim().ToUpper();
                            bool rmaFromServer = (mode == "RMA");
                            if (isRmaModeActive != rmaFromServer)
                            {
                                isRmaModeActive = rmaFromServer;
                                UpdateRmaButtonVisual(isRmaModeActive);
                                Print($"V12.14: RMA mode synced from CONFIG -> {isRmaModeActive}");
                            }
                            ParseConfig(parts[2]);
                            Print($"V12.14: CONFIG parsed - Mode={mode}, DataLen={parts[2].Length}");
                        }
                        else if (parts[0] == "CONFIG" && parts.Length == 2)
                        {
                            // V12.14: Legacy 2-part CONFIG (e.g. CONFIG|FLEET)
                            ParseConfig(parts[1]);
                        }
                        else if (parts[0] == "TREND" && parts.Length > 1)
                            UpdateTrendIndicator(parts[1]);
                        else if (parts[0] == "REQUEST_FLEET_STATE")
                        {
                            // V12.3: Strategy requests current fleet state after reconnect
                            foreach (string acctName in selectedFleetAccounts)
                            {
                                SendCommand($"TOGGLE_ACCOUNT|{acctName}|1");
                            }
                            Print($"V12 STD: Fleet state sent - {selectedFleetAccounts.Count} accounts active");
                        }
                        else if (parts[0] == "SYNC_TARGET_STATE" && parts.Length > 1)
                        {
                            // V14 ADAPTIVE VISIBILITY: Receive live position size and update button visibility
                            if (int.TryParse(parts[1], out int qty))
                            {
                                Print($"V14: SYNC_TARGET_STATE Received -> {qty}");
                                // UpdateTargetVisibility now auto-dispatches to UI thread internally
                                UpdateTargetVisibility(qty);
                                // Also update the count chip visuals (UpdateTargetVisibility handles its own dispatch)
                                UpdateTargetCountVisual(qty);
                            }
                        }
                        // V12.43: Handle lightweight RMA deactivation from strategy (replaces old CONFIG clobber)
                        else if (parts[0] == "SET_RMA_MODE" && parts.Length > 1)
                        {
                            bool rmaOn = parts[1].Trim().ToUpper() == "ON";
                            if (isRmaModeActive != rmaOn)
                            {
                                isRmaModeActive = rmaOn;
                                UpdateRmaButtonVisual(isRmaModeActive);
                                Print($"V12.43: RMA mode synced from strategy -> {isRmaModeActive}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Print($"V12 STD: Parse error - {ex.Message}");
                    }
                }));
            }
        }

        private void ParseTelemetry(string data)
        {
            // V12.3: Parse telemetry data from strategy
            // Format: EMA9:val;EMA15:val;EMA30:val;EMA65:val;EMA200:val;ORH:val;ORL:val;ATR:val
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "EMA9":
                        UpdateEmaText(ema9Text, "9:", value);
                        break;
                    case "EMA15":
                        UpdateEmaText(ema15Text, "15:", value);
                        break;
                    case "EMA30":
                        UpdateEmaText(ema30Text, "30:", value);
                        break;
                    case "EMA65":
                        UpdateEmaText(ema65Text, "65:", value);
                        break;
                    case "EMA200":
                        UpdateEmaText(ema200Text, "200:", value);
                        break;
                    case "ATR":
                        if (atrText != null) atrText.Text = "ATR: " + value;
                        break;
                    case "ORH":
                        UpdateORText(or5Text, or15Text, "ORH", value);
                        break;
                    case "ORL":
                        UpdateORText(or5Text, or15Text, "ORL", value);
                        break;
                }
            }
        }
        private void ParseCompliance(string[] parts)
        {
            if (parts == null || parts.Length < 5) return;
            if (ChartControl == null) return;

            string summary = parts[1];
            string consistency = parts[2];
            string payout = parts[3];
            string drawdown = parts[4];

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                if (complianceSummaryText != null) complianceSummaryText.Text = summary;
                if (complianceConsistencyText != null) complianceConsistencyText.Text = consistency;
                if (compliancePayoutText != null) compliancePayoutText.Text = payout;
                if (complianceDrawdownText != null) complianceDrawdownText.Text = drawdown;
            });
        }

        /// <summary>
        /// V12.3: Helper to update EMA TextBlock with label:value format
        /// </summary>
        private void UpdateEmaText(TextBlock tb, string label, string value)
        {
            if (tb == null) return;
            tb.Inlines.Clear();
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = TextPrimary });
        }

        // V12.3: Cache OR values for display
        private string cachedORH = "0.00";
        private string cachedORL = "0.00";

        /// <summary>
        /// V12.3: Helper to update OR TextBlocks with H/L values
        /// </summary>
        private void UpdateORText(TextBlock or5Block, TextBlock or15Block, string key, string value)
        {
            if (key == "ORH") cachedORH = value;
            else if (key == "ORL") cachedORL = value;

            // Calculate range
            double h = 0, l = 0;
            double.TryParse(cachedORH, out h);
            double.TryParse(cachedORL, out l);
            double range = h - l;

            // Update OR5 line (placeholder - actual 5-min OR would need separate tracking)
            if (or5Block != null)
            {
                or5Block.Inlines.Clear();
                or5Block.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
            }

            // Update OR15 line (uses same data for now - could be enhanced for 15-min OR)
            if (or15Block != null)
            {
                or15Block.Inlines.Clear();
                or15Block.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
            }
        }

        private void ParseConfig(string data)
        {
            Print($"V12.14 DBG: ParseConfig input = [{data}]");
            
            // V14 FIX: Suppress auto-save while ParseConfig updates UI fields.
            // Without this, every CONFIG broadcast from the strategy overwrites TextBoxes,
            // triggers TextChanged ? SaveConfig(), and permanently destroys saved settings.
            bool wasSuppressed = isApplyingSettings;
            isApplyingSettings = true;
            try
            {
                foreach (string pair in data.Split(';'))
                {
                    string[] kv = pair.Split(':');
                    if (kv.Length != 2) continue;

                    string key = kv[0].Trim().ToUpper();
                    string value = kv[1].Trim();

                    switch (key)
                    {
                        // V12.43: Guard against zero/empty clobbering from stale strategy broadcasts
                        case "T1": if (svT1Val != null && !string.IsNullOrEmpty(value) && value != "0") svT1Val.Text = value; break;
                        case "T2": if (svT2Val != null && !string.IsNullOrEmpty(value) && value != "0") svT2Val.Text = value; break;
                        case "T3": if (svT3Val != null && !string.IsNullOrEmpty(value) && value != "0") svT3Val.Text = value; break;
                        case "T4": if (svT4Val != null && !string.IsNullOrEmpty(value) && value != "0") svT4Val.Text = value; break;
                        case "T5": if (svT5Val != null && !string.IsNullOrEmpty(value) && value != "0") svT5Val.Text = value; break;
                        case "T1TYPE": if (svT1Type != null) SelectComboByValue(svT1Type, value); break;
                        case "T2TYPE": if (svT2Type != null) SelectComboByValue(svT2Type, value); break;
                        case "T3TYPE": if (svT3Type != null) SelectComboByValue(svT3Type, value); break;
                        case "T4TYPE": if (svT4Type != null) SelectComboByValue(svT4Type, value); break;
                        case "T5TYPE": if (svT5Type != null) SelectComboByValue(svT5Type, value); break;
                        case "STR": if (strVal != null && !string.IsNullOrEmpty(value) && value != "0") strVal.Text = value; break;
                        case "MAX": if (maxVal != null && !string.IsNullOrEmpty(value) && value != "0") maxVal.Text = value; break;
                        case "CIT": if (citVal != null) citVal.Text = value; break;
                        case "STRTYPE": if (svStrType != null) SelectComboByValue(svStrType, value); break;
                        case "COUNT":
                            if (int.TryParse(value, out int count))
                            {
                                // Update Visuals without sending command loop
                                selectedTargetCount = count;
                                UpdateTargetCountVisual(count);
                                UpdateTargetVisibility(count);
                            }
                            break;
                        // V12.5: Handle MODE sync from Strategy broadcast
                        case "MODE":
                            string incomingMode = value.ToUpper();
                            if (incomingMode != selectedConfigMode.ToUpper())
                            {
                                selectedConfigMode = incomingMode;
                                Button modeBtn = GetModeButton(incomingMode);
                                if (modeBtn != null)
                                {
                                    HighlightModeButton(modeBtn);
                                    // Load mode-specific settings if we have them
                                    if (fullConfig != null)
                                    {
                                        ApplySettings(fullConfig.GetSettings(incomingMode, selectedTargetCount));
                                    }
                                }
                                Print(string.Format("V12.5: MODE Synced -> {0}", incomingMode));
                            }
                            break;
                        // V12.20-C: Bidirectional sync for TREND/RETEST RMA toggles
                        case "TRMA":
                            bool trmaState = value == "1";
                            if (isTrendRmaToggle != trmaState)
                            {
                                isTrendRmaToggle = trmaState;
                                if (trendRmaToggle != null) trendRmaToggle.Opacity = trmaState ? 1.0 : 0.5;
                                Print(string.Format("V12.20: TRMA Synced -> {0}", trmaState ? "RMA" : "STD"));
                            }
                            break;
                        case "RRMA":
                            bool rrmaState = value == "1";
                            if (isRetestRmaToggle != rrmaState)
                            {
                                isRetestRmaToggle = rrmaState;
                                if (retestRmaToggle != null) retestRmaToggle.Opacity = rrmaState ? 1.0 : 0.5;
                                Print(string.Format("V12.20: RRMA Synced -> {0}", rrmaState ? "RMA" : "STD"));
                            }
                            break;
                    }
                }
            }
            finally
            {
                isApplyingSettings = wasSuppressed;
            }
        }

        private void SelectComboByValue(ComboBox combo, string value)
        {
             if (combo == null || string.IsNullOrEmpty(value)) return;
             
             string normalizedValue = value.ToUpper().Trim();
             
             // Mapping for User Friendliness / Legacy Support
             if (normalizedValue == "OR" || normalizedValue == "OPENRANGE" || normalizedValue == "OPEN RANGE") 
                 normalizedValue = "OR";
             else if (normalizedValue == "ATR" || normalizedValue == "A")
                 normalizedValue = "ATR";
             else if (normalizedValue == "TICKS" || normalizedValue == "T")
                 normalizedValue = "Ticks";
             else if (normalizedValue == "PTS" || normalizedValue == "POINTS" || normalizedValue == "P")
                 normalizedValue = "Pts";
             else if (normalizedValue == "RUNNER" || normalizedValue == "R")
                 normalizedValue = "Runner";

             foreach (ComboBoxItem item in combo.Items)
             {
                 if (item.Content.ToString().ToUpper() == normalizedValue.ToUpper() || 
                     (normalizedValue == "OR" && item.Content.ToString() == "OR") ||
                     (normalizedValue == "ATR" && item.Content.ToString() == "ATR"))
                 {
                     combo.SelectedItem = item;
                     return;
                 }
             }
        }

        private void UpdateTrendIndicator(string trend)
        {
            if (trendText == null || trendIndicator == null) return;

            bool bullish = trend.ToUpper().Contains("BULL") || trend == "1";
            trendText.Text = bullish ? "BULLISH" : "BEARISH";
            trendText.Foreground = bullish ? GreenFg : RedFg;
            trendIndicator.Background = bullish ? GreenBg : RedBg;
            trendIndicator.BorderBrush = bullish ? GreenBorder : RedBorder;
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private V12_001[] cacheV12_001;
		public V12_001 V12_001(int ipcPort, bool autoConnect, int panelWidth)
		{
			return V12_001(Input, ipcPort, autoConnect, panelWidth);
		}

		public V12_001 V12_001(ISeries<double> input, int ipcPort, bool autoConnect, int panelWidth)
		{
			if (cacheV12_001 != null)
				for (int idx = 0; idx < cacheV12_001.Length; idx++)
					if (cacheV12_001[idx] != null && cacheV12_001[idx].IpcPort == ipcPort && cacheV12_001[idx].AutoConnect == autoConnect && cacheV12_001[idx].PanelWidth == panelWidth && cacheV12_001[idx].EqualsInput(input))
						return cacheV12_001[idx];
			return CacheIndicator<V12_001>(new V12_001(){ IpcPort = ipcPort, AutoConnect = autoConnect, PanelWidth = panelWidth }, input, ref cacheV12_001);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.V12_001 V12_001(int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12_001(Input, ipcPort, autoConnect, panelWidth);
		}

		public Indicators.V12_001 V12_001(ISeries<double> input , int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12_001(input, ipcPort, autoConnect, panelWidth);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.V12_001 V12_001(int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12_001(Input, ipcPort, autoConnect, panelWidth);
		}

		public Indicators.V12_001 V12_001(ISeries<double> input , int ipcPort, bool autoConnect, int panelWidth)
		{
			return indicator.V12_001(input, ipcPort, autoConnect, panelWidth);
		}
	}
}

#endregion
