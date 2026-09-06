// PTT-COPIER-B17-T1 -- TradeCopierPanel.cs
// B17 T1 CHANGES:
//   1. Added _b17DiagDone volatile bool field (fire-once diagnostic gate).
//   2. Added EnumerateAllChartPanels(ChartControl cc): walks visual tree, probes Charts via Reflection, shows MessageBox.
//   3. Modified OnChartMouseDown: calls EnumerateAllChartPanels once via _b17DiagDone gate.
//   4. Added GetRefPrice() fallback for rawPrice when ClickTrader price returns 0.
//   [AMEND] TradeCopierAddOn.cs: MouseDown -> PreviewMouseDown (Director auth, DW-B17-02).

// PTT-COPIER-B15-T2 -- TradeCopierPanel.cs
// B15 T2 CHANGES:
//   1. Removed _chartDiagDone volatile bool field (T1 diagnostic cleanup).
//   2. Removed DumpReflectionPath(ChartControl cc, StringBuilder sb) method (T1 diagnostic cleanup).
//   3. Removed DumpVisualTree(ChartControl cc, StringBuilder sb) method (T1 diagnostic cleanup).
//   4. Removed DumpChartControlTree(ChartControl cc) method (T1 diagnostic cleanup).
//   5. Reverted SetChart(Chart chart) to CYC=1 (removed DumpChartControlTree call).
//   6. Added GetPriceAtY(ChartControl cc, double y) private static method (CYC=4).
//   7. Modified OnChartMouseDown: replaced 0.0 stub + suppression line with real lookup.
//      Final CYC=6. Click-trader Y-to-price lookup CLOSED (B15-T2).
// PTT-COPIER-B15-T1 -- TradeCopierPanel.cs (CLEANUP -- diagnostic removed in T2)
// PTT-COPIER-B14-T1 -- TradeCopierPanel.cs
// B14 T1 CHANGES:
//   1. Modified OnBeConnected: added ArmTrailBe call after BreakEven (CYC=3).
//   2. Modified OnBeClick Connected case: added DisarmTrailBe alongside DisarmPendingBe.
//   3. Modified Detach(): added DisarmTrailBe alongside DisarmPendingBe (cleanup path).
// PTT-COPIER-B12-T3 -- TradeCopierPanel.cs
// B12 T3 CHANGES:
//   1. Added _maxRiskDollars, _atrFraction (plain double), _riskDollarsBox, _atrFractionBox fields.
//   2. Added BuildRiskAtrRow() -- Risk $ + ATR % spinners inside _contentPanel (last row).
//   3. Added OnRiskUp/Down, OnRiskTextLostFocus, OnAtrFractionUp/Down, OnAtrFractionTextLostFocus.
//   4. Added NotifyRiskChanged(), NotifyAtrFractionChanged().
//   5. Modified BuildUI() to call BuildRiskAtrRow at end of _contentPanel.
// PTT-COPIER-B12-T1 -- TradeCopierPanel.cs
// B12 T1 CHANGES:
//   1. Added _trimBuffer, _flattenBuffer, _beBuffer (plain int), _beState (BeState), new Button refs.
//   2. Added BeState enum (Idle/Armed/Connected) -- 3-state FSM.
//   3. Added BrushConnected frozen brush (blue, RGB 59/130/246).
//   4. Added BuildBufferedButtonsRow() -- 3-row buffered button section inside _contentPanel.
//   5. Added FormatBuffer() static helper.
//   6. Added OnTrimUp/Down/Click, OnFlattenUp/Down/Click, OnBeUp/Down/Click handlers.
//   7. Added UpdateBeLabel(), UpdateBeVisuals(BeState), OnBeConnected(string), GetRefPrice().
//   8. Added OnCopyToggle, OnCancel2.
//   9. Removed _beArmBtn/_beArmState/_beArmBufferBox; removed BuildBeArmRow/OnBEArmClick/
//      UpdateBEArmVisuals/FlashBeFired; replaced OnPendingBeFiredDispatch target.
//  10. Modified BuildUI() to wrap rows in _contentPanel; adds BufferedButtonsRow at [4.0].
//  11. Modified DispatchShortcut Key.T/Key.F to pass GetRefPrice() and buffer.
//  12. Added _isCollapsed, _collapseToggleBtn, _contentPanel (T2 fields, declared here for T2).
//      NOTE: _contentPanel referenced by T2 (BuildCollapsibleHeader/OnCollapseClick) -- T2 fields
//      are declared here so that T1 can wrap rows in _contentPanel.
// PTT-COPIER-B11-T1 -- TradeCopierPanel.cs
// ChartTrader row injection surface. UserControl embedded in ChartTrader Grid (B7).
// Zero order creation. All order flow through CopyEngine.
// Jane Street rules: JS-001, JS-021, JS-023 -- no lock, Dispatcher.InvokeAsync only.
//
// B7 CHANGES:
//   1. Followers ListBox + ScrollViewer + "Followers" label REMOVED.
//   2. Replaced with a checkmark ComboBox (_followersDropDown).
//      Row layout (left to right): account name | daily P&L | checkmark.
//      Header shows "N selected" live count.
//      Design Pillar "Live Map": label is always the live state, never a prompt.
//   3. FollowerItem nested class: Account + IsSelected + live DailyPnlText/DailyPnlColor.
//      INotifyPropertyChanged drives P&L TextBlock binding -- no polling, no timer.
//      P&L color: green (+), red (-), dim ($0) per Live Map pillar Layer 2.
//   4. GetSelectedFollowers() iterates _followerItems -- no ListBox.SelectedItems.
//   5. Leader ComboBox absent by design -- ChartTrader Account IS the leader (B7-FIX6).
//   6. acc.AccountItemUpdate fires live P&L push from NT8 (AccountItem.RealizedProfitLoss).
//      Subscribed in OnLoaded, unsubscribed in Detach(). Dispatcher.InvokeAsync on callback.
//   7. B7-F1: Semantic button color coding (Layer 2 + Layer 3 live state via PositionStateChanged).
//      V08: canonical RGB per PTT_DESIGN_PILLAR. MakeBrush(r,g,b) -- no hex literals.
//
// B8 T1 CHANGES:
//   1. FollowerItem: added Multiplier int property (default 1, range [1,10]).
//   2. BuildCheckItemTemplate: added [mult TextBox w=30] before [checkmark].
//   3. OnFollowerMultiplierChanged: new handler -- parses int, clamps [1,10], sets item.Multiplier.
//   4. OnApplyRule: collects multipliers[] from _followerItems; calls engine.AddRule 5-arg overload.
//   5. ParseAtmModeNameLocal: pre-declared for T2; T1 passes ImmutableDictionary.Empty via atmMap.
//
// B8 T2 CHANGES:
//   1. FollowerItem: added AtmModeName string property (default "Inherit").
//   2. BuildCheckItemTemplate: added [ATM ComboBox w=80] before [checkmark].
//   3. OnFollowerAtmComboLoaded: new handler -- populates items synchronously.
//   4. OnFollowerAtmModeChanged: new handler -- sets item.AtmModeName.
//   5. OnApplyRule: collects AtmModeName per follower; builds ImmutableDictionary<string, FollowerAtmMode>.
//
// B9 T2 CHANGES:
//   1. Added _clickArmed, _clickBuy volatile bool fields (JS-023).
//   2. Added _currentChart, _armBtn, _buyToggle, _sellToggle UI fields.
//   3. SetChart(): stores chart reference for click trader arm/disarm.
//   4. BuildClickTraderRow(): appends [Buy] [Sell] [Arm] row to root StackPanel.
//   5. OnArmClick: toggles _clickArmed, calls RegisterClickTrader/UnregisterClickTrader.
//   6. UpdateArmVisuals: updates Arm button label + background color (MakeBrush, no hex).
//   7. OnChartMouseDown: fires limit order on chart click when armed (CYC=4).
//   8. OnBuyToggleClick / OnSellToggleClick: set _clickBuy volatile flag.
//   9. Detach(): unregisters click trader on panel teardown.
//
// B11 T1 CHANGES:
//   1. SetStatusText(): internal helper for SIM101 diag text display.
//   2. OnChartKeyDown(): PreviewKeyDown handler for Ctrl+Shift shortcut dispatch.
//   3. DispatchShortcut(): switch-based dispatch to engine methods (T/F/C/B).
//   4. Removed BuildDiagRow, OnDiagGap001d, OnDiagGap002 (DW-B10-01 CLOSED).
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Chart;

namespace PropTraderTools
{
    public class TradeCopierPanel : UserControl, IPttHostContext
    {
        // -- state ----------------------------------------------------------------
        private CopyEngine _engine;
        private Instrument _instrument;
        private Account _leaderAccount; // Set by TradeCopierAddOn from ChartTrader.Account

        // B33 T7 -- IPttHostContext implementation (for module Execute() calls)
        // AllAccounts populated in OnLoaded (UI thread). LeaderAccount + Instrument delegate to fields.
        private readonly List<Account> _allAccounts = new List<Account>();
        private readonly List<IPttModule> _modules = new List<IPttModule>();

        // IPttHostContext
        Account IPttHostContext.LeaderAccount
        {
            get { return _leaderAccount; }
        }
        Instrument IPttHostContext.Instrument
        {
            get { return _instrument; }
        }
        IReadOnlyList<Account> IPttHostContext.AllAccounts
        {
            get { return _allAccounts; }
        }

        // B34 T2 -- Buffer props and market quote props wired to existing private fields/methods.
        int IPttHostContext.BeBuffer
        {
            get { return _beBuffer; }
        }
        int IPttHostContext.TrimBuffer
        {
            get { return _trimBuffer; }
        }
        int IPttHostContext.FlatBuffer
        {
            get { return _flattenBuffer; }
        }
        double IPttHostContext.Ask
        {
            get { return GetAsk(); }
        }
        double IPttHostContext.Bid
        {
            get { return GetBid(); }
        }

        void IPttHostContext.WarnUser(string message)
        {
            if (_statusText != null)
                _statusText.Text = message;
        }

        // B33 T7 -- License bools (default: all enabled). Wire to module.SetEnabled() in OnLoaded.
        public bool IsBeLicensed { get; set; } = true;
        public bool IsTrimLicensed { get; set; } = true;
        public bool IsFlattenLicensed { get; set; } = true;
        public bool IsCancelLicensed { get; set; } = true;
        public bool IsCopierLicensed { get; set; } = true;

        // B33 T7 -- Module registry helper. CYC=1.
        private void AddModule(IPttModule m)
        {
            _modules.Add(m);
        }

        // B33 T7 -- DispatchModule: finds the module by ID and calls Execute(this). CYC=3.
        // UI-thread only -- called from WPF button handlers.
        // JS-021: no lock. IPttHostContext is (this) -- panel satisfies context contract.
        private void DispatchModule(string moduleId)
        {
            foreach (IPttModule m in _modules)
            {
                if (m.ModuleId == moduleId)
                {
                    m.Execute(this);
                    return;
                }
            }
        }

        private ComboBox _accountCombo; // B30-B: stored at WireAccountCombo for Detach unsubscribe
        private SelectionChangedEventHandler _accountComboSelectionChanged; // B30-B: named handler for leak-free Detach
        private TextBlock _statusText;
        private bool _copyEnabled;
        private TextBox _beBufferBox;

        // Checkmark dropdown
        private ComboBox _followersDropDown;
        private readonly List<FollowerItem> _followerItems = new List<FollowerItem>();

        // B47 T1-B: Inline followers ScrollViewer (replaces _followersDropDown in visual tree)
        private ScrollViewer _followerScrollViewer = null;
        private StackPanel _followerScrollViewerPanel = null;

        // B47 T3-B: Collapsible Copier header
        private Button _copierCollapseBtn = null;
        private bool _copierCollapsed = false; // default: Copier section expanded

        // B9 T2 -- Click trader (JS-023: volatile cross-thread fields)
        private volatile bool _clickArmed = false;
        private volatile bool _clickBuy = true; // true=Buy, false=SellShort
        private Chart _currentChart = null; // single-writer UI thread
        private Button _armBtn = null;
        private ToggleButton _buyToggle = null;
        private ToggleButton _sellToggle = null;

        // B9 T3 -- Copy mode selector radio buttons
        private RadioButton _signalModeBtn = null;
        private RadioButton _mirrorModeBtn = null;
        private RadioButton _cloneModeBtn = null; // B50: Clone mode radio button

        // B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
        // B52: WeakReference<ComboBox> prevents detached combo accumulation on panel rebuild.
        // Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
        private readonly System.Collections.Generic.List<
            WeakReference<System.Windows.Controls.ComboBox>
        > _atmComboRefs = new System.Collections.Generic.List<
            WeakReference<System.Windows.Controls.ComboBox>
        >();

        // B10 T3 -- Tighten Stop fields (UI-thread-only)
        private Button _tightenBtn = null;
        private TextBox _tightenTicksBox = null;

        // B12 T1 -- Buffered button state (plain int; UI-thread-only; no volatile per NT8-003)
        private int _trimBuffer = 0; // HOTFIX-F5: default market (0 ticks), not limit (+1)
        private int _flattenBuffer = 0; // HOTFIX-F5: default market (0 ticks), not limit (+1)
        private int _beBuffer = 1;

        // B12 T1 -- BE 3-state FSM (UI-thread-only; no volatile)
        private BeState _beState = BeState.Idle;

        // B12 T1 -- Button refs for buffered section
        private Button _trimBtn2;
        private Button _flattenBtn2;
        private Button _beBtn2;
        private Button _cancelBtn2;
        private Button _copyToggleBtn2;

        // B41: Quick Exit button refs (UI-thread-only; no volatile per NT8-003)
        private Button _quickBtn = null;
        private Button _quickAllBtn = null;
        private StackPanel _quickT3Row = null;

        // B41: Quick tick display values (session-only, not persisted to CopyRule)
        private int _quickT1 = 4; // default MES: 4t
        private int _quickT2 = 8; // default MES: 8t

        // B47 T5-B: Root-level BE and Quick row panels (extracted from _contentPanel in T6-B)
        private UniformGrid _beRowPanel = null; // 2-col: BE cluster | BE ALL cluster
        private UniformGrid _quickRowPanel = null; // 2-col: Quick cluster | Quick ALL cluster

        // B129: Instrument-scoped row panel and button refs (UI-thread-only; no volatile per NT8-003)
        private Button _instr2tBtn = null;
        private Button _instrQAll2tBtn = null;
        private UniformGrid _instrRowPanel = null;

        // BGTM-1: Feature-flag-gated row panels. Assigned in Build* methods; toggled in ApplyFeatureFlags.
        private StackPanel _clickTraderRow = null;
        private UniformGrid _atrRow = null;

        // HOTFIX-QUICKALL-SINGLETON-01: Quick ALL buffer is now a CopyEngine singleton.
        // _quickAllT1 per-panel field removed. Read CopyEngine.Instance.GlobalQuickAllT1 instead.

        // B39: BE ALL button reference for green-flash update.
        private Button _globalBeBtn2;

        // B39: Frozen static brush for the purple BE ALL button (JS-008 compliant).
        // MakeBrush(r,g,b) calls .Freeze() internally.
        private static readonly SolidColorBrush BrushPurple = MakeBrush(168, 85, 247);

        // DW-B72-02: _globalBeState removed. Truth source is CopyEngine.Instance.IsPendingSlotsEmpty().
        // All panels read the shared _pendingBeSlots dict -- no per-panel shadow state needed.

        // B12 T2 -- Collapse state and refs (plain bool; UI-thread-only; no volatile per NT8-003)
        private bool _isCollapsed = false;
        private Button _collapseToggleBtn;
        private StackPanel _contentPanel;

        // B12 T3 -- Risk/ATR spinners (plain double; UI-thread-only; no volatile per NT8-003)
        private double _maxRiskDollars = 200.0;
        private double _atrFraction = 0.75;
        private TextBox _riskDollarsBox;
        private TextBox _atrFractionBox;

        // B20-LANE-C T5 -- ATR display label (owned by Panel; set in BuildRiskAtrRow; nulled on GC after purge)
        private TextBlock _atrDisplayLabel;

        // B12 T1 -- Frozen semantic brush for BE CONNECTED border (MakeBrush = Freeze()d, JS-008)
        // RGB (59, 130, 246) = blue. No hex string literal (JS-008).
        private static readonly SolidColorBrush BrushConnected = MakeBrush(59, 130, 246);

        // -- frozen semantic brushes (JS-008: MakeBrush calls Freeze()) --
        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Canonical semantic button brushes (V08: corrected RGB per PTT_DESIGN_PILLAR lines 192-198)
        // JS-008: all Freeze()d via MakeBrush(), static readonly = zero allocation on re-render
        private static readonly SolidColorBrush BrushActive = MakeBrush(34, 197, 94); // green  #22c55e
        private static readonly SolidColorBrush BrushDanger = MakeBrush(239, 68, 68); // red    #ef4444
        private static readonly SolidColorBrush BrushCaution = MakeBrush(245, 158, 11); // amber  #f59e0b
        private static readonly SolidColorBrush BrushInactive = MakeBrush(55, 65, 81); // grey   #4b5563

        // DW-B73-B-02: teal border/foreground for BE/Quick buttons -- cached per JS-008
        private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136); // teal-600 #0d9488

        // -- nested type ----------------------------------------------------------
        private sealed class FollowerItem : INotifyPropertyChanged
        {
            // Frozen brush constants (JS-008) -- shared across all instances
            private static readonly SolidColorBrush BrushPos = MakeBrush(34, 197, 94); // green
            private static readonly SolidColorBrush BrushNeg = MakeBrush(239, 68, 68); // red
            private static readonly SolidColorBrush BrushDim = MakeBrush(107, 114, 128); // grey

            // Cached PropertyChangedEventArgs -- zero alloc per fire
            private static readonly PropertyChangedEventArgs PnlTextArgs =
                new PropertyChangedEventArgs(nameof(DailyPnlText));
            private static readonly PropertyChangedEventArgs PnlColorArgs =
                new PropertyChangedEventArgs(nameof(DailyPnlColor));

            public Account Account { get; set; }
            public bool IsSelected { get; set; }

            // B8 T1: per-follower quantity multiplier -- default 1x, range [1,10]
            public int Multiplier { get; set; } = 1;

            // B8 T2: per-follower ATM mode name -- default "Inherit"
            public string AtmModeName { get; set; } = "Inherit";

            private string _dailyPnlText = "$0.00";
            private Brush _dailyPnlColor; // set in constructor

            public FollowerItem()
            {
                _dailyPnlColor = BrushDim; // dim until first AccountItemUpdate fires
            }

            public string DailyPnlText
            {
                get => _dailyPnlText;
                private set
                {
                    _dailyPnlText = value;
                    PropertyChanged?.Invoke(this, PnlTextArgs);
                }
            }

            public Brush DailyPnlColor
            {
                get => _dailyPnlColor;
                private set
                {
                    _dailyPnlColor = value;
                    PropertyChanged?.Invoke(this, PnlColorArgs);
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            // Called on UI thread via Dispatcher.InvokeAsync -- no lock needed
            public void UpdatePnl(double value)
            {
                string sign = value > 0 ? "+" : "";
                DailyPnlText = sign + "$" + value.ToString("0.00");
                DailyPnlColor =
                    value > 0 ? BrushPos
                    : value < 0 ? BrushNeg
                    : (Brush)BrushDim;
            }

            // B20-LANE-C T3 -- DW-B17-ACCOUNT-NAME-01: strip !<suffix> at display layer only.
            // Raw Account.Name is never modified. ?[0] guards null propagation when Account or Name
            // is null. Split("!")[0] without ?[0] is UNSAFE (NullReferenceException). CYC=1.
            public override string ToString() => Account?.Name?.Split('!')?[0] ?? "";
        }

        // B32 -- BE 2-state FSM enum. Connected state removed (DW-B32-04).
        // Idle  -> click -> price check:
        //   already at/past BE? -> MoveStopToBreakEven immediately -> stay Idle
        //   in drawdown?        -> ArmPendingBe -> Armed (amber)
        // Armed -> price crosses entry+buffer -> MoveStopToBreakEven once -> Idle
        // Armed -> click again  -> DisarmPendingBe -> Idle (cancel)
        // ATM trail owns stop after BE placement. PTT does not trail. (DW-B32-05)
        internal enum BeState
        {
            Idle, // BE button shows "BE +N" -- inactive
            Armed, // Watching price; fires once when entry+buffer crossed; amber border
        }

        // -- construction ---------------------------------------------------------
        public TradeCopierPanel()
        {
            _engine = CopyEngine.Instance;
            _engine.StatusUpdate += OnStatusUpdate;
            BuildUI();
            // Defer Account.All population -- NT8 may not have it ready at construct time
            Loaded += OnLoaded;
        }

        // -- public surface (called by TradeCopierAddOn) --------------------------

        // B9 T2: Store chart reference for click trader. CYC=1 (straight-line).
        public void SetChart(Chart chart)
        {
            _currentChart = chart;
        }

        // B17 T2: Linear interpolation via ChartPanel.MaxValue / MinValue / ActualHeight.
        // B17 fix: FindPriceCanvasPanel replaces FindVisualChild<ChartPanel> (DFS first-match
        // returned ChartTrader sidebar: Width~139, MaxValue=0 -> rawPrice=0 -> no order placed).
        // FindPriceCanvasPanel selects widest ChartPanel with MaxValue>0 = price canvas.
        // CORRECTION_FACTOR = 1.0 (B16 T1 confirmed ContentPresenter.ActualHeight = ChartPanel.ActualHeight).
        // NT8-029 replacement: RoundToTickSize absent -- AlignToTick via Math.Round AwayFromZero.
        // CYC=5: cc null(1), panel null(2), height<=0(3), raw<=0(4), instrument null(5).
        private static double GetPriceAtY(ChartControl cc, double y, Instrument instrument)
        {
            if (cc == null)
                return 0.0; // guard (1)

            var panel = FindPriceCanvasPanel(cc); // B17 T2: heuristic selects widest ChartPanel with MaxValue>0
            if (panel == null)
                return 0.0; // guard (2)

            double panelH = panel.ActualHeight;
            if (panelH <= 0.0)
                return 0.0; // guard (3): no divide by zero

            // CORRECTION_FACTOR = 1.0: T1 confirmed ContentPresenter fills full ChartPanel height.
            const double CORRECTION_FACTOR = 1.0;

            double maxVal = panel.MaxValue;
            double minVal = panel.MinValue;
            double yRatio = y / (panelH * CORRECTION_FACTOR);
            double rawPrice = maxVal - yRatio * (maxVal - minVal);

            if (rawPrice <= 0.0)
                return 0.0; // guard (4): sanity

            if (instrument == null)
                return 0.0; // guard (5)
            return AlignToTick(rawPrice, instrument.MasterInstrument.TickSize);
        }

        // B16 T2: Pure-math linear Y-to-price interpolation helper.
        // Internal static for xUnit test access via Reflection.
        // Formula: rawPrice = maxVal - (y / (panelH * correctionFactor)) * (maxVal - minVal)
        // CYC=2: height guard(1), raw guard(2).
        internal static double LinearYToPrice(
            double y,
            double panelH,
            double maxVal,
            double minVal,
            double correctionFactor
        )
        {
            if (panelH <= 0.0)
                return 0.0; // guard (1)
            double yRatio = y / (panelH * correctionFactor);
            double rawPrice = maxVal - yRatio * (maxVal - minVal);
            if (rawPrice <= 0.0)
                return 0.0; // guard (2)
            return rawPrice;
        }

        // B16 T2: Pure-math tick alignment helper.
        // Mirrors NT8-native RoundToTickSize semantics via Math.Round AwayFromZero.
        // Internal static for xUnit test access via Reflection.
        // CYC=2: tickSize guard(1), straight-line(2).
        internal static double AlignToTick(double raw, double tickSize)
        {
            if (tickSize <= 0.0)
                return raw; // guard (1)
            return Math.Round(raw / tickSize, MidpointRounding.AwayFromZero) * tickSize;
        }

        // B17 T2 Option A: Walk full visual tree under root; return the ChartPanel with
        // MaxValue > 0 and largest ActualWidth. Reliably selects the price canvas panel
        // rather than the ChartTrader sidebar (Width~139, MaxValue=0 -- DFS first-match victim).
        // T1 F5 confirmed: only one ChartPanel exists (W=931.33, Max=7633.34) -- returns it directly.
        // CYC=5: root null(1), while loop(2), type+predicate(3), for loop(4), child null(5).
        private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
        {
            if (root == null)
                return null; // guard (1)
            ChartPanel best = null;
            double bestW = 0.0;
            var stack = new Stack<DependencyObject>();
            stack.Push(root);

            while (stack.Count > 0) // branch (2): loop
            {
                var node = stack.Pop();
                var cp = node as ChartPanel;
                if (cp != null && cp.MaxValue > 0 && cp.ActualWidth > bestW) // branch (3): predicate
                {
                    best = cp;
                    bestW = cp.ActualWidth;
                }
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++) // branch (4): child loop
                {
                    var child = VisualTreeHelper.GetChild(node, i) as DependencyObject;
                    if (child != null)
                        stack.Push(child); // branch (5): null guard
                }
            }
            return best;
        }

        public void SetInstrument(Instrument instrument)
        {
            _instrument = instrument;
            if (_statusText != null && instrument != null)
                _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
        }

        public void SetLeaderAccount(Account account)
        {
            _leaderAccount = account;
        }

        // B30-B: WireAccountCombo -- called by TradeCopierAddOn.WireLeaderAccount instead of
        // anonymous lambda. Stores the ComboBox ref and named handler so Detach() can unsubscribe.
        // Fixes memory leak DW-B30-03: anonymous lambda captured panel, preventing GC.
        // CYC=1 (straight-line assignment + subscription, no branches). JS-021: no lock.
        public void WireAccountCombo(ComboBox combo)
        {
            _accountCombo = combo;
            _accountComboSelectionChanged = (s, e) =>
                _leaderAccount = _accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
            combo.SelectionChanged += _accountComboSelectionChanged;
        }

        // B30-B: TryResolveLeaderAccount -- late-resolve the leader account when _leaderAccount
        // was null at inject time (ComboBox not yet populated). Uses stored _accountCombo ref.
        // HOTFIX-B30-F1: NT8 ComboBox.SelectedItem is sometimes a string (account name) when
        // data-bound -- Account cast returns null even though an account IS selected.
        // Fallback: match by name against Account.All.
        // CYC=4: combo null(1), Account cast(2), string name empty(3), Account.All loop(4).
        // JS-002: returns null (not throw) -- callers use null as a no-op sentinel.
        private NinjaTrader.Cbi.Account TryResolveLeaderAccount()
        {
            if (_accountCombo == null)
                return null; // (1)
            if (_accountCombo.SelectedItem is NinjaTrader.Cbi.Account acc)
                return acc; // (2)
            var name = _accountCombo.SelectedItem as string ?? _accountCombo.Text; // NT8 string fallback
            if (string.IsNullOrEmpty(name))
                return null; // (3)
            foreach (var a in NinjaTrader.Cbi.Account.All) // (4)
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                    return a;
            return null;
        }

        public void Detach()
        {
            _engine.Unsubscribe(); // B44: unsubscribe from order events before teardown
            // B9 T2: unregister click trader before clearing state
            if (_currentChart != null)
                TradeCopierAddOn.UnregisterClickTrader(_currentChart);
            _engine.StatusUpdate -= OnStatusUpdate;
            _engine.PositionStateChanged -= OnPositionStateChanged;
            _engine.PendingBeFired -= OnPendingBeFiredDispatch;
            _engine.PendingBeArmed -= OnPendingBeArmedDispatch; // HOTFIX-BEALL-SYNC-01
            _engine.GlobalBeBufferChanged -= OnGlobalBeBufferChanged; // HOTFIX-BEALL-BUFFER-SYNC-01
            _engine.GlobalQuickAllBufferChanged -= OnQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01
            _engine.GlobalBeAllDisarmed -= OnGlobalBeAllDisarmed; // HOTFIX-BEALL-DISARM-SYNC-01
            UnsubscribeFollowerItems();
            _engine.DisarmPendingBe(_leaderAccount);
            // DW-C39-20: last-panel-close guard -- clear remaining global pending BE slots.
            // TradeCopierAddOn.TryRemove ran before Detach(), so _panels is already empty if last panel.
            if (TradeCopierAddOn.IsPanelsEmpty())
                _engine.ClearAllPendingBeSlots();
            // B32: DisarmTrailBe removed -- PTT no longer runs trail after BE (DW-B32-05).
            _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
            // B41: unsubscribe leader order/position update handlers (memory-leak prevention).
            if (_leaderAccount != null)
            {
                _leaderAccount.OrderUpdate -= OnLeaderOrderUpdate;
                _leaderAccount.PositionUpdate -= OnLeaderPositionUpdate;
            }
            // B30-B: unsubscribe ComboBox SelectionChanged to prevent memory leak (DW-B30-03).
            if (_accountCombo != null && _accountComboSelectionChanged != null)
                _accountCombo.SelectionChanged -= _accountComboSelectionChanged;
            _accountCombo = null;
            _accountComboSelectionChanged = null;
            _instrument = null;
            _leaderAccount = null;

            // DW-C38-03: DisarmAllAccounts() call removed -- was disarming sibling panels' BE state (bug).
            // Leader-account disarm already performed at line 591 (_engine.DisarmPendingBe(_leaderAccount)).
            // No visual update here -- panel is being destroyed.

            // B33 T7 -- Teardown all IPttModules (unsubscribes all PttBus events).
            foreach (IPttModule m in _modules)
                m.Teardown();
            _modules.Clear();
            _allAccounts.Clear();

            // BGTM-1: Unsubscribe feature-flag handler.
            CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
        }

        // R10: extracted from Detach() to eliminate Bumpy Road foreach-within-foreach pattern.
        // MUST only be called from Detach() on UI thread (_followerItems is UI-thread-owned).
        // JS-021: no lock. JS-002: no return null (void). ASCII-only. CYC=2.
        private void UnsubscribeFollowerItems()
        {
            foreach (var item in _followerItems)
                if (item.Account != null)
                    item.Account.AccountItemUpdate -= OnAccountItemUpdate;
        }

        // -- Layer 3 live state (V04) -- called on UI thread only -----------------
        // B12 T1: updated to use new _copyToggleBtn2, _flattenBtn2, _cancelBtn2, _trimBtn2, _beBtn2.
        // CYC=5: 5 ternary branches, no control flow.
        // HOTFIX-F3: when position goes flat, force BE FSM back to Idle regardless of prior state.
        // Previously Armed/Connected states were never cleared by PositionStateChanged -- button
        // stayed amber/blue after ATM or native Close flattened the position.
        // BWAVE-CYC T1: extracted helpers for UpdateButtonColors.
        // ApplyCopyToggleBackground: sets copy toggle button background. CCN=2.
        private void ApplyCopyToggleBackground()
        {
            if (_copyToggleBtn2 != null)
                _copyToggleBtn2.Background = _copyEnabled ? BrushActive : BrushInactive;
        }

        // ApplyPositionButtons: sets flatten/cancel/trim backgrounds based on position/entries. CCN=5.
        private void ApplyPositionButtons(bool hasPosition, bool hasEntries)
        {
            if (_flattenBtn2 != null)
                _flattenBtn2.Background = hasPosition ? BrushDanger : BrushInactive;
            if (_cancelBtn2 != null)
                _cancelBtn2.Background = hasEntries ? BrushDanger : BrushInactive;
            if (_trimBtn2 != null)
                _trimBtn2.Background = hasPosition ? BrushCaution : BrushInactive;
        }

        // ApplyButtonBackgrounds: sets Background on the 4 control buttons. CCN=3.
        private void ApplyButtonBackgrounds(bool hasPosition, bool hasEntries)
        {
            ApplyCopyToggleBackground();
            ApplyPositionButtons(hasPosition, hasEntries);
        }

        // HOTFIX-F3: reset per-chart BE state when position goes flat. CCN=3.
        private void ResetBeStateOnFlat(bool hasPosition)
        {
            if (!hasPosition && _beState != BeState.Idle)
            {
                _beState = BeState.Idle;
                UpdateBeVisuals(BeState.Idle);
                if (_leaderAccount != null)
                    CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
            }
        }

        // HOTFIX-BEALL-FLAT-RESET: disarm BE ALL when position goes flat. CCN=3.
        private void DisarmBeAllOnFlat(bool hasPosition)
        {
            if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                if (_leaderAccount != null)
                    CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
                CopyEngine.Instance.RaiseBeAllDisarmed();
            }
        }

        // HOTFIX-ORPHAN-STOP-CLEANUP: cancel orphan QX brackets when position goes flat. CCN=2.
        private void CancelOrphanBracketsOnFlat(bool hasPosition)
        {
            if (!hasPosition && _leaderAccount != null && _instrument != null)
                CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument);
        }

        // UpdateButtonColors after extraction. CCN=5: base(1) + 4 unconditional calls = 1; the
        // original ApplyButtonBackgrounds has 4 null-guard branches counted here as callee.
        // Actual parent CYC = 1 (all calls unconditional). Conservative target = 5.
        private void UpdateButtonColors(bool hasPosition, bool hasEntries)
        {
            ApplyButtonBackgrounds(hasPosition, hasEntries);
            ResetBeStateOnFlat(hasPosition);
            DisarmBeAllOnFlat(hasPosition);
            CancelOrphanBracketsOnFlat(hasPosition);
        }

        // CYC=1: single null+instrument filter guard.
        // JS-023: marshals onto UI thread via Dispatcher.InvokeAsync.
        // JS-003: PositionState is a readonly struct -- captured by value in closure.
        // B32-DIAG: emit instrument name comparison so we can confirm FullName matches panel _instrument.
        private void OnPositionStateChanged(string instr, PositionState state)
        {
            NinjaTrader.Code.Output.Process(
                "PositionStateChanged instr="
                    + instr
                    + " panel="
                    + (_instrument?.FullName ?? "null")
                    + " hasPos="
                    + state.HasOpenPosition,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            if (_instrument == null || _instrument.FullName != instr)
                return;
            Dispatcher.InvokeAsync(() =>
                UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries)
            );
        }

        // BWAVE-CYC T1: extracted helpers for OnLoaded. CCN of each <= 7.

        // PopulateFollowerItems: clears and repopulates _followerItems from Account.All. CCN=4.
        // SAFE: called on UI thread from OnLoaded (Account.All available in Loaded handlers per NT8-021).
        private void PopulateFollowerItems()
        {
            _followerItems.Clear();
            if (Account.All == null)
                return;
            foreach (var acc in Account.All)
            {
                _followerItems.Add(new FollowerItem { Account = acc, IsSelected = false });
                acc.AccountItemUpdate += OnAccountItemUpdate;
            }
            if (_followersDropDown != null)
                _followersDropDown.ItemsSource = _followerItems;
            UpdateDropDownHeader();
            LoadFollowers();
            _engine.LoadRules();
        }

        // RestoreSavedFollowers: restores IsSelected state from persisted follower names. CCN=5.
        private void RestoreSavedFollowers()
        {
            if (_instrument == null || _leaderAccount == null)
                return;
            var saved = _engine.GetSavedFollowerNames(_instrument.FullName, _leaderAccount.Name);
            if (saved.Count > 0)
            {
                foreach (var item in _followerItems)
                    if (item.Account != null && saved.Contains(item.Account.Name))
                        item.IsSelected = true;
                SortFollowerRows();
                TryAutoApply();
            }
        }

        // ApplyModuleLicenses: sets enabled state on each module from license bools. CCN=3.
        // BWAVE-CYC T1: dictionary approach replaces switch-over-5-cases (was CCN=7, now CCN=3).
        // Func<TradeCopierPanel, bool> reads license bool from the panel instance. Safe on UI thread.
        private static bool GetIsBeLicensed(TradeCopierPanel p) => p.IsBeLicensed;
        private static bool GetIsTrimLicensed(TradeCopierPanel p) => p.IsTrimLicensed;
        private static bool GetIsFlattenLicensed(TradeCopierPanel p) => p.IsFlattenLicensed;
        private static bool GetIsCancelLicensed(TradeCopierPanel p) => p.IsCancelLicensed;
        private static bool GetIsCopierLicensed(TradeCopierPanel p) => p.IsCopierLicensed;

        private static readonly System.Collections.Generic.Dictionary<string, Func<TradeCopierPanel, bool>> _licenseMap =
            new System.Collections.Generic.Dictionary<string, Func<TradeCopierPanel, bool>>
            {
                { "BE",     GetIsBeLicensed },
                { "TRIM",   GetIsTrimLicensed },
                { "FLAT",   GetIsFlattenLicensed },
                { "CANCEL", GetIsCancelLicensed },
                { "COPY",   GetIsCopierLicensed },
            };

        private void ApplyModuleLicenses()
        {
            foreach (IPttModule m in _modules)
            {
                if (_licenseMap.TryGetValue(m.ModuleId, out var fn))
                    m.SetEnabled(fn(this));
            }
        }

        // -- private: deferred account population ---------------------------------
        // OnLoaded after extraction. CCN=7.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _engine.PositionStateChanged += OnPositionStateChanged;
            _engine.PendingBeFired += OnPendingBeFiredDispatch;
            _engine.PendingBeArmed += OnPendingBeArmedDispatch;
            _engine.GlobalBeBufferChanged += OnGlobalBeBufferChanged;
            _engine.GlobalQuickAllBufferChanged += OnQuickAllBufferChanged;
            _engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed;
            PopulateFollowerItems();
            RestoreSavedFollowers();
            NotifyRiskChanged();
            NotifyAtrFractionChanged();
            _engine.CopyEnabledChanged += OnCopyEnabledChanged;
            ApplyCopyState(_engine.IsEnabled);

            // B33 T7 -- Build AllAccounts (leader + followers) for IPttHostContext.
            _allAccounts.Clear();
            if (_leaderAccount != null)
                _allAccounts.Add(_leaderAccount);
            foreach (var item in _followerItems)
                if (item.Account != null && item.Account != _leaderAccount)
                    _allAccounts.Add(item.Account);

            // B33 T7 -- Register and initialize all IPttModules.
            _modules.Clear();
            AddModule(new PttBreakEven());
            AddModule(new PttTrim());
            AddModule(new PttFlatten());
            AddModule(new PttCancel());
            AddModule(new PttCopier(_engine));
            foreach (IPttModule m in _modules)
                m.Initialize(this);

            ApplyModuleLicenses();
            _engine.Subscribe();

            // B41: Site 3 -- initial display sync after panel wires up.
            if (_leaderAccount != null)
            {
                _leaderAccount.OrderUpdate += OnLeaderOrderUpdate;
                _leaderAccount.PositionUpdate += OnLeaderPositionUpdate;
                RefreshQuickDisplay(_leaderAccount, _instrument);
            }

            // BGTM-1: Subscribe to feature-flag changes and apply current flags now.
            CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
            ApplyFeatureFlags(CopyEngine.Instance.Flags);
        }

        // -- live P&L push from NT8 -----------------------------------------------
        // Fires on background thread -- must Dispatcher.InvokeAsync before touching UI/items
        private void OnAccountItemUpdate(object sender, AccountItemEventArgs e)
        {
            if (e.AccountItem != AccountItem.RealizedProfitLoss)
                return;
            var acc = sender as Account;
            if (acc == null)
                return;
            double val = e.Value;
            foreach (var item in _followerItems)
            {
                if (item.Account != acc)
                    continue;
                Dispatcher.InvokeAsync(() => item.UpdatePnl(val));
                break;
            }
        }

        // -- UI construction -------------------------------------------------------
        // B12 T1: restructured -- rows wrapped in _contentPanel; buffered buttons at [4.0];
        //         old 4-column actionGrid and dead toggle buttons removed.
        // R3: Extracted from BuildUI to eliminate Large Method warning (77 LoC -> ~25 LoC).
        // CYC=1 (straight-line construction, no branches).
        private void BuildUI()
        {
            var root = new StackPanel { Margin = new Thickness(2) };

            // B47 T1-B: construct follower scroll section fields (no visual-tree insertion here).
            BuildFollowerScrollSection();

            // Apply button: HIDDEN (Visibility.Collapsed). Event handler OnApplyRule stays wired.
            var applyBtn = new Button
            {
                Content = "Add Followers",
                Margin = new Thickness(0, 2, 0, 2),
                Visibility = Visibility.Collapsed,
            };
            applyBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            applyBtn.Click += OnApplyRule;
            root.Children.Add(applyBtn); // in tree but invisible -- preserves OnApplyRule wiring

            // B12 T1/T2: _contentPanel wraps all collapsible content rows
            _contentPanel = new StackPanel();

            // [4.0] B12 T1: Buffered button section (Trim | Flatten | Cancel | BE | Copy toggle)
            BuildBufferedButtonsRow(_contentPanel);

            // --- Status line ---
            _statusText = new TextBlock
            {
                Text = "Open chart -- Trim/Flatten/Cancel/BE ready",
                Margin = new Thickness(0, 2, 0, 0),
            };
            _statusText.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
            // B47 T6-B: do NOT add _statusText to _contentPanel here.
            // It is added to root after BuildCopierSection (see tail of BuildUI).

            BuildClickTraderRow(_contentPanel); // B9 T2: Click Trader row
            _contentPanel.Children.Add(BuildTightenRow()); // B10 T3: Tighten Stop cluster
            BuildRiskAtrRow(_contentPanel); // B12 T3: Risk $ + ATR % spinner row

            // B49: Buttons first (BE/Quick rows), then Copier, then Position Tools.
            root.Children.Add(_beRowPanel);
            BuildInstrRow(); // B128: build instrument row before adding to root
            root.Children.Add(_instrRowPanel);
            root.Children.Add(_quickRowPanel);
            BuildCopierSection(root); // B49: Copier second (Mode row now inside)
            root.Children.Add(_statusText); // status below Copier
            BuildCollapsibleHeader(root); // B49: Position Tools moved to bottom
            root.Children.Add(_contentPanel); // B49: contentPanel follows its header
            Content = root;

            UpdateButtonColors(false, false); // V04: ensure consistent initial state
        }

        // R3 helper: constructs _followersDropDown, _followerScrollViewerPanel, _followerScrollViewer.
        // *** DO NOT add _followerScrollViewer to any visual tree here. ***
        // It enters the tree ONLY via BuildCopierSection(root) in T6-B. Adding it here would cause
        // WPF InvalidOperationException ("Element is already the child of another element").
        // CYC=1 (straight-line construction, no branches).
        private void BuildFollowerScrollSection()
        {
            // B47 T1-B: _followersDropDown kept as field (ItemsSource set in OnLoaded for compat).
            _followersDropDown = new ComboBox { IsEditable = false, Text = "0 selected" };
            _followersDropDown.ItemTemplate = BuildCheckItemTemplate();

            // B47 T1-B: Inline ScrollViewer replacing ComboBox in visual tree.
            _followerScrollViewerPanel = new StackPanel();
            _followerScrollViewer = new ScrollViewer
            {
                MaxHeight = 66,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _followerScrollViewerPanel,
                Margin = new Thickness(0, 0, 0, 2),
            };
        }

        // R3 helper: constructs the Tighten Stop cluster (button + ticks TextBox + label).
        // Returns the StackPanel with Visibility=Collapsed (B47 T5-B: hidden, not deleted).
        // CYC=1 (straight-line construction, no branches).
        private StackPanel BuildTightenRow()
        {
            var tightenRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0),
            };
            _tightenTicksBox = new TextBox
            {
                Text = "5",
                Width = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _tightenBtn = new Button
            {
                Content = "Tighten",
                Margin = new Thickness(0, 0, 4, 0),
                Background = BrushInactive,
            };
            _tightenBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _tightenBtn.Click += OnTightenStop;
            var tightenLabel = new TextBlock
            {
                Text = "tks",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
            };
            tightenLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
            tightenRow.Children.Add(_tightenBtn);
            tightenRow.Children.Add(_tightenTicksBox);
            tightenRow.Children.Add(tightenLabel);
            tightenRow.Visibility = Visibility.Collapsed; // B47 T5-B: HIDE NOT DELETE
            return tightenRow;
        }

        // B9 T2: Appends [Buy] [Sell] toggle pair and [Arm] button row to root StackPanel.
        // CYC=1 (straight-line widget construction, no branches).
        private void BuildClickTraderRow(StackPanel root)
        {
            _clickTraderRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0),
            };

            _buyToggle = new ToggleButton
            {
                Content = "Buy",
                IsChecked = true,
                Width = 45,
                Height = 22,
            };
            _buyToggle.SetResourceReference(Control.StyleProperty, "NTToggleButtonStyle");

            _sellToggle = new ToggleButton
            {
                Content = "Sell",
                Width = 45,
                Height = 22,
            };
            _sellToggle.SetResourceReference(Control.StyleProperty, "NTToggleButtonStyle");

            _buyToggle.Click += OnBuyToggleClick;
            _sellToggle.Click += OnSellToggleClick;

            _armBtn = new Button
            {
                Content = "Arm",
                Width = 48,
                Height = 22,
                Margin = new Thickness(6, 0, 0, 0),
                Background = MakeBrush(28, 33, 51),
            };
            _armBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _armBtn.Click += OnArmClick;

            // B41: Cancel button relocated from BuildBufferedButtonsRow Row 3 to Click Trader row.
            _cancelBtn2 = new Button
            {
                Content = "Cancel",
                Width = 48,
                Height = 22,
                Margin = new Thickness(6, 0, 0, 0),
                BorderBrush = BrushDanger,
                BorderThickness = new Thickness(2),
            };
            _cancelBtn2.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _cancelBtn2.Click += OnCancel2;

            _clickTraderRow.Children.Add(_buyToggle);
            _clickTraderRow.Children.Add(_sellToggle);
            _clickTraderRow.Children.Add(_armBtn);
            _clickTraderRow.Children.Add(_cancelBtn2);
            root.Children.Add(_clickTraderRow);
            _clickTraderRow.Visibility = Visibility.Collapsed; // B47 T5-B: HIDE NOT DELETE (handlers preserved)
        }

        // B12 T1 -- OnPendingBeFiredDispatch: marshals PendingBeFired from NT8 account bg thread to UI.
        // B12 T1: replaced FlashBeFired call with OnBeConnected call.
        // CYC=1: straight-line Dispatcher.InvokeAsync, no branches.
        // Called on NT8 account background thread -- never touch UI directly here.
        private void OnPendingBeFiredDispatch(string instr, string accountName)
        {
            Dispatcher.InvokeAsync(() =>
            {
                OnBeConnected(instr, accountName);
                // DW-B72-02: auto-reset BE ALL when last armed slot fires.
                // Truth source: IsPendingSlotsEmpty() -- no local shadow state.
                if (CopyEngine.Instance.IsPendingSlotsEmpty())
                    UpdateBeAllVisuals(BeState.Idle);
            });
        }

        // HOTFIX-BEALL-SYNC-01: marshals PendingBeArmed from NT8 bg thread to UI.
        // Fires on ALL panels when any account slot is armed -- keeps BE ALL visual synced.
        // CYC=1: straight-line Dispatcher.InvokeAsync, no branches.
        private void OnPendingBeArmedDispatch(string instr, string accountName)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!CopyEngine.Instance.IsPendingSlotsEmpty())
                    UpdateBeAllVisuals(BeState.Armed);
            });
        }

        // HOTFIX-BUFLABEL-02: wrap in panel-local Dispatcher.InvokeAsync.
        // Application.Current.Dispatcher (where event fires) != chart-window Dispatcher (where _globalBeBtn2 was created).
        // Reference pattern: OnGlobalBeAllDisarmed uses Dispatcher.InvokeAsync (this panel's own Dispatcher).
        private void OnGlobalBeBufferChanged(int newBuffer)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_globalBeBtn2 != null)
                    _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", newBuffer);
            });
        }

        // HOTFIX-BUFLABEL-02: wrap in panel-local Dispatcher.InvokeAsync (same reason as OnGlobalBeBufferChanged).
        // Also updates to use FormatQuickAllBuffer to append "t" unit suffix (QUICK-LABEL-UNIT-01).
        private void OnQuickAllBufferChanged(int newT1)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_quickAllBtn != null)
                    _quickAllBtn.Content = FormatQuickAllBuffer("Quick ALL", newT1);
            });
        }

        // HOTFIX-BEALL-DISARM-SYNC-01: all panels reset BE ALL visual when any panel disarms.
        // CYC=1: straight-line Dispatcher.InvokeAsync, no branches.
        private void OnGlobalBeAllDisarmed()
        {
            Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle));
        }

        // B40 -- UpdateBeAllVisuals: purple=Idle, amber=Armed. CYC=2.
        // UI-thread only -- no Dispatcher wrap needed (all callers are on UI thread).
        // BrushPurple and BrushCaution are pre-defined Panel brush fields.
        private void UpdateBeAllVisuals(BeState state)
        {
            if (_globalBeBtn2 == null)
                return;
            if (state == BeState.Idle)
            {
                _globalBeBtn2.BorderBrush = BrushTeal;
                _globalBeBtn2.Foreground = BrushTeal;
                _globalBeBtn2.Background = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                _globalBeBtn2.Background = BrushActive;
            }
        }

        // B47 T5-B: BuildBufferedButtonsRow -- restructured.
        // Row 1 (Trim|Flatten): kept but hidden (Visibility.Collapsed). Event handlers preserved.
        // _beRowPanel: UniformGrid 2-col [BE | BE ALL]. NOT added to root here (T6-B does that).
        // _quickRowPanel: UniformGrid 2-col [Quick | Quick ALL+spinner]. NOT added to root here.
        // _quickT3Row: kept Collapsed (B41 logic unchanged).
        // R11: data-driven loop replaces 6 structurally-identical section-builder methods.
        // Eliminates Code Duplication cluster (CodeScene L1212-L1282) and reduces function count by 6.
        // CYC: base(1) + foreach(1) = 2.
        private void BuildBufferedButtonsRow(StackPanel root)
        {
            var row1 = new UniformGrid
            {
                Columns = 2,
                Margin = new Thickness(0, 2, 0, 2),
                Visibility = Visibility.Collapsed,
            };
            _beRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
            // NOTE: _beRowPanel is NOT added to root here. T6-B adds it to root after BuildCopierSection.
            _quickRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
            // NOTE: _quickRowPanel is NOT added to root here. T6-B adds it to root.

            var specs = new (
                string Content,
                System.Windows.Media.Brush Bg,
                bool Teal,
                RoutedEventHandler Up,
                RoutedEventHandler Dn,
                RoutedEventHandler Main,
                System.Action<Button> Store,
                Panel Target
            )[]
            {
                (FormatBuffer("Trim",    _trimBuffer),                                        BrushInactive, false, OnTrimUp,     OnTrimDown,     OnTrimClick,     b => _trimBtn2     = b, row1),
                (FormatBuffer("Flatten", _flattenBuffer),                                     BrushInactive, false, OnFlattenUp,  OnFlattenDown,  OnFlattenClick,  b => _flattenBtn2  = b, row1),
                (FormatBuffer("BE",      _beBuffer),                                          BrushTeal,     true,  OnBeUp,       OnBeDown,       OnBeClick,       b => _beBtn2       = b, _beRowPanel),
                (FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer), BrushTeal,     true,  OnGlobalBeUp, OnGlobalBeDown, OnGlobalBeClick, b => _globalBeBtn2 = b, _beRowPanel),
                (FormatBuffer("Quick",   _quickT1),                                           BrushTeal,     true,  OnQuickUp,    OnQuickDown,    OnQuickClick,    b => _quickBtn     = b, _quickRowPanel),
                (FormatBuffer("Quick ALL", CopyEngine.Instance.GlobalQuickAllT1),             BrushTeal,     true,  OnQuickAllUp, OnQuickAllDown, OnQuickAllClick, b => _quickAllBtn  = b, _quickRowPanel),
            };
            foreach (var s in specs)
            {
                var cluster = new DockPanel { LastChildFill = true };
                var arrows = new Grid();
                arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
                arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
                var up = new System.Windows.Controls.Primitives.RepeatButton
                {
                    Content = "^",
                    Width = 18,
                    Height = 12,
                };
                var dn = new System.Windows.Controls.Primitives.RepeatButton
                {
                    Content = "v",
                    Width = 18,
                    Height = 12,
                };
                up.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                dn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                up.Click += s.Up;
                dn.Click += s.Dn;
                Grid.SetRow(up, 0);
                Grid.SetRow(dn, 1);
                arrows.Children.Add(up);
                arrows.Children.Add(dn);
                DockPanel.SetDock(arrows, Dock.Right);
                var btn = new Button { Content = s.Content };
                btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                if (s.Teal)
                {
                    btn.BorderBrush = BrushTeal;
                    btn.Foreground = System.Windows.Media.Brushes.White; // DW-BWAVE-UI-01: white text on teal bg (was BrushTeal -- invisible)
                    btn.BorderThickness = new Thickness(2);
                }
                btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)
                btn.Click += s.Main;
                cluster.Children.Add(arrows);
                cluster.Children.Add(btn);
                s.Store(btn);
                s.Target.Children.Add(cluster);
            }

            root.Children.Add(row1);

            _quickT3Row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2),
                Visibility = Visibility.Collapsed,
            };
            var quickT3Lbl = new TextBlock
            {
                Text = "T3 hidden",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
            };
            quickT3Lbl.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
            _quickT3Row.Children.Add(quickT3Lbl);
            root.Children.Add(_quickT3Row);
        }

        // B129: BuildInstrRow -- 2-col UniformGrid: "Quick2t" (left) + "QAll2t" (right).
        // No spinner. Fixed labels. CYC=1: sequential construction.
        // JS-021: no lock. JS-033: no async. ASCII-only labels.
        private void BuildInstrRow()
        {
            _instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
            _instr2tBtn = new Button
            {
                Content = "Quick2t",
                BorderBrush = BrushTeal,
                BorderThickness = new Thickness(2),
            };
            _instr2tBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _instr2tBtn.Foreground = System.Windows.Media.Brushes.White; // DW-BWAVE-UI-01: white text on teal bg
            _instr2tBtn.Background = BrushTeal; // AFTER style -- explicit brush wins
            _instr2tBtn.Click += OnInstr2tClick;
            _instrRowPanel.Children.Add(_instr2tBtn);

            _instrQAll2tBtn = new Button
            {
                Content = "QAll2t",
                BorderBrush = BrushTeal,
                BorderThickness = new Thickness(2),
            };
            _instrQAll2tBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _instrQAll2tBtn.Foreground = System.Windows.Media.Brushes.White; // DW-BWAVE-UI-01: white text on teal bg
            _instrQAll2tBtn.Background = BrushTeal; // AFTER style -- explicit brush wins
            _instrQAll2tBtn.Click += OnInstrQAll2tClick;
            _instrRowPanel.Children.Add(_instrQAll2tBtn);
        }

        // B129: Build2TargetList -- returns pre-built 2-entry targets list for Quick2t.
        // T1 gets ceiling qty (heavy side per Director spec). T2 gets floor qty.
        // Prices not used by Execute -- only Qty is read. Pass 0.0 for Price.
        // CYC=1. JS-002: never null. JS-021: no lock. internal static for xUnit direct test access.
        internal static List<(double Price, int Qty)> Build2TargetList(int totalQty)
        {
            int t1Qty = (totalQty + 1) / 2;
            int t2Qty = totalQty - t1Qty;
            return new List<(double, int)> { (0.0, t1Qty), (0.0, t2Qty) };
        }

        // B12 T1 -- FormatBuffer: formats buffer label for display on a button. CYC=1. Static, no state.
        // Example: FormatBuffer("Trim", 1) -> "Trim +1"
        private static string FormatBuffer(string name, int ticks)
        {
            return name + " +" + ticks;
        }

        // HOTFIX-BUFLABEL-02 / QUICK-LABEL-UNIT-01: Quick ALL label appends "t" to make tick unit explicit.
        // MES tick = $1.25, MGC tick = $0.10, MCL tick = $0.01 -- storing raw ticks; unit must be visible.
        // "Quick ALL +4t" not "Quick ALL +4".
        private static string FormatQuickAllBuffer(string name, int ticks)
        {
            return name + " +" + ticks + "t";
        }

        // B39: FormatGlobalBeBuffer -- handles 0 / positive / negative for BE ALL label.
        // 2-parameter form: caller supplies label ("BE ALL"). Plan SS5.5 authoritative.
        // Does NOT modify the existing FormatBuffer(string, int) method.
        // CYC=3 (1 base + 2 if branches).
        private static string FormatGlobalBeBuffer(string name, int ticks)
        {
            if (ticks == 0)
                return name;
            if (ticks > 0)
                return name + " +" + ticks;
            return name + " " + ticks; // int.ToString() of negative auto-includes "-"
        }

        // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
        // Idle->Armed: arm all pending; if at least one slot entered Armed state, turn amber.
        // Armed->Idle (manual disarm): loop Account.All and DisarmPendingBe for each; turn purple.
        // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
        // DW-B72-02: _globalBeState removed. IsPendingSlotsEmpty() is the truth source.
        // Idle (slots empty)  -> arm all, then show Armed visual if slots were taken.
        // Armed (slots exist) -> disarm all, show Idle visual.
        // Both panels read the same CopyEngine singleton so state is automatically shared.
        private void OnGlobalBeClick(object sender, RoutedEventArgs e)
        {
            if (CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                // Currently Idle -- arm
                NinjaTrader.Code.Output.Process(
                    "[BE-ALL] button: arm buf=" + CopyEngine.Instance.GlobalBe.GlobalBeBuffer,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
            }
            else
            {
                // Already armed -- guard: log and return (no disarm, no re-arm)
                NinjaTrader.Code.Output.Process(
                    "[PTT-BE-ALL] already armed, ignoring double-press",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }
        }

        // B39: OnGlobalBeUp -- increment shared buffer; label refresh handled by OnGlobalBeBufferChanged broadcast.
        // HOTFIX-BEALL-BUFFER-SYNC-01: removed per-panel label update here -- event fires to all panels.
        private void OnGlobalBeUp(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.GlobalBe.IncrementBuffer();
        }

        // B39: OnGlobalBeDown -- decrement shared buffer; label refresh handled by OnGlobalBeBufferChanged broadcast.
        // HOTFIX-BEALL-BUFFER-SYNC-01: removed per-panel label update here -- event fires to all panels.
        private void OnGlobalBeDown(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.GlobalBe.DecrementBuffer();
        }

        // B12 T1 -- OnTrimUp: increment _trimBuffer, clamp, update label. CYC=1.
        private void OnTrimUp(object sender, RoutedEventArgs e)
        {
            _trimBuffer = Math.Max(Math.Min(_trimBuffer + 1, 20), 0); // no Math.Clamp (NT8 .NET 4.8)
            if (_trimBtn2 != null)
                _trimBtn2.Content = FormatBuffer("Trim", _trimBuffer);
        }

        // B12 T1 -- OnTrimDown: decrement _trimBuffer, clamp, update label. CYC=1.
        private void OnTrimDown(object sender, RoutedEventArgs e)
        {
            _trimBuffer = Math.Max(Math.Min(_trimBuffer - 1, 20), 0);
            if (_trimBtn2 != null)
                _trimBtn2.Content = FormatBuffer("Trim", _trimBuffer);
        }

        // R7 -- LogAndDispatchModule: shared guard+log+dispatch helper for Trim/Flatten/Cancel click handlers.
        // CYC=2: (1) _instrument null guard, (2) ?? late-resolve expression.
        // MUST only be called from UI-thread Click handlers (no Dispatcher needed -- already on UI thread).
        // JS-021: no lock. JS-002: no return null. JS-033: not async void.
        private void LogAndDispatchModule(string logTag, string moduleId)
        {
            if (_instrument == null)
                return; // (1)
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // (2)
            NinjaTrader.Code.Output.Process(
                logTag + " button: "
                    + (_leaderAccount?.Name ?? "null")
                    + " "
                    + (_instrument?.FullName ?? "null"),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            DispatchModule(moduleId);
        }

        // B33 T7 -- OnTrimClick: dispatches to PttTrim module. CYC=1 (R7 dedup).
        // B30-B: leader resolved late via LogAndDispatchModule (DW-B30-03).
        private void OnTrimClick(object sender, RoutedEventArgs e)
        {
            LogAndDispatchModule("[TRIM]", "TRIM");
        }

        // B12 T1 -- OnFlattenUp: increment _flattenBuffer, clamp, update label. CYC=1.
        private void OnFlattenUp(object sender, RoutedEventArgs e)
        {
            _flattenBuffer = Math.Max(Math.Min(_flattenBuffer + 1, 20), 0);
            if (_flattenBtn2 != null)
                _flattenBtn2.Content = FormatBuffer("Flatten", _flattenBuffer);
        }

        // B12 T1 -- OnFlattenDown: decrement _flattenBuffer, clamp, update label. CYC=1.
        private void OnFlattenDown(object sender, RoutedEventArgs e)
        {
            _flattenBuffer = Math.Max(Math.Min(_flattenBuffer - 1, 20), 0);
            if (_flattenBtn2 != null)
                _flattenBtn2.Content = FormatBuffer("Flatten", _flattenBuffer);
        }

        // B33 T7 -- OnFlattenClick: dispatches to PttFlatten module. CYC=1 (R7 dedup).
        // B30-B: leader resolved late via LogAndDispatchModule (DW-B30-03).
        private void OnFlattenClick(object sender, RoutedEventArgs e)
        {
            LogAndDispatchModule("[FLAT]", "FLAT");
        }

        // B12 T1 -- OnBeUp: increment _beBuffer, clamp. CYC=1.
        // B32/B35-LaneB: Connected state removed -- buffer change no longer triggers live reprice (DW-B32-04b closed).
        private void OnBeUp(object sender, RoutedEventArgs e)
        {
            _beBuffer = Math.Max(Math.Min(_beBuffer + 1, 20), 0); // no Math.Clamp
            UpdateBeLabel();
        }

        // B12 T1 -- OnBeDown: decrement _beBuffer, clamp, live reprice if Connected. CYC=2.
        private void OnBeDown(object sender, RoutedEventArgs e)
        {
            _beBuffer = Math.Max(Math.Min(_beBuffer - 1, 20), 0);
            UpdateBeLabel();
            // B32: Connected state removed -- buffer change no longer triggers live reprice (DW-B32-04).
        }

        // B33 T7 -- OnBeClick: 2-state FSM. Idle-immediate path dispatches to PttBreakEven module.
        // Idle: price-at-BE -> DispatchModule("BE") (stays Idle). In drawdown -> ArmPendingBe (Armed).
        // Armed: cancel arm -> Idle.
        // CYC=5: (1) instrument null, (2) leader null, (3) Idle branch,
        //        (4) price-already-at-BE check, (5) Armed cancel.
        // B30-B: leader resolved late via _leaderAccount ?? TryResolveLeaderAccount() (DW-B30-03).
        private void OnBeClick(object sender, RoutedEventArgs e)
        {
            if (_instrument == null)
                return; // (1)
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // B30-B
            if (_leaderAccount == null)
                return; // (2)
            switch (_beState)
            {
                case BeState.Idle: // (3)
                    // DW-B32-04: if price already past BE target, fire immediately -- no arm needed.
                    // Otherwise arm and wait for price to cross.
                    if (IsPriceAlreadyAtBe(_leaderAccount, _instrument, _beBuffer)) // (4)
                    {
                        NinjaTrader.Code.Output.Process(
                            "[BE] button: immediate fire "
                                + _leaderAccount.Name
                                + " buf="
                                + _beBuffer,
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                        DispatchModule("BE");
                        // stay Idle -- ATM owns stop from here
                    }
                    else
                    {
                        NinjaTrader.Code.Output.Process(
                            "[BE] button: arming " + _leaderAccount.Name + " buf=" + _beBuffer,
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                        _engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer);
                        _beState = BeState.Armed;
                        UpdateBeVisuals(BeState.Armed);
                    }
                    break;
                case BeState.Armed: // (5)
                    NinjaTrader.Code.Output.Process(
                        "[BE] button: disarming " + _leaderAccount.Name,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    _engine.DisarmPendingBe(_leaderAccount);
                    _beState = BeState.Idle;
                    UpdateBeVisuals(BeState.Idle);
                    break;
            }
        }

        // BWAVE-CYC T4: extracted helpers for IsPriceAlreadyAtBe.

        // ComputeBeTargetPrice: computes break-even target price. CCN=2.
        private static double ComputeBeTargetPrice(
            double avgPrice,
            bool isLong,
            int bufferTicks,
            double tickSize
        )
        {
            return avgPrice + (isLong ? 1.0 : -1.0) * bufferTicks * tickSize;
        }

        // IsPriceAtOrPastTarget: compares ref price vs target for long/short. CCN=2.
        private static bool IsPriceAtOrPastTarget(bool isLong, double refPx, double targetPx)
        {
            return isLong ? (refPx >= targetPx) : (refPx <= targetPx);
        }

        // IsPriceAlreadyAtBe after extraction. CCN=5.
        private bool IsPriceAlreadyAtBe(Account leader, Instrument instrument, int bufferTicks)
        {
            var pos = _engine.FindPositionPublic(leader, instrument);
            if (pos == null)
                return false;
            double tickSize = instrument?.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0)
                return false;
            bool isLong = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long;
            double refPx = isLong ? GetBid() : GetAsk();
            if (refPx <= 0.0)
                return false;
            double target = ComputeBeTargetPrice(pos.AveragePrice, isLong, bufferTicks, tickSize);
            return IsPriceAtOrPastTarget(isLong, refPx, target);
        }

        // B12 T1 -- UpdateBeLabel: sets _beBtn2 label. CYC=1.
        private void UpdateBeLabel()
        {
            if (_beBtn2 != null)
                _beBtn2.Content = FormatBuffer("BE", _beBuffer);
        }

        // B32 -- UpdateBeVisuals: 2-state only. Connected removed (DW-B32-04). CYC=2.
        // FIX-A: Idle case now also resets Background -- previously only Content was reset,
        // leaving the amber BrushCaution background stuck on the button after disarm.
        private void UpdateBeVisuals(BeState state)
        {
            if (_beBtn2 == null)
                return;
            switch (state)
            {
                case BeState.Idle: // (1)
                    _beBtn2.Content = FormatBuffer("BE", _beBuffer);
                    _beBtn2.Background = BrushInactive; // FIX-A: clear amber
                    break;
                case BeState.Armed: // (2)
                    _beBtn2.Content = "BE Armed";
                    _beBtn2.Background = BrushCaution;
                    break;
            }
        }

        // B32 -- OnBeConnected: fires when ArmPendingBe price trigger crossed (DW-B32-04/05).
        // BreakEven() already called inside OnPendingBeAccountUpdate -- no duplicate call here.
        // No ArmTrailBe -- ATM owns the stop trail after BE placement. (DW-B32-05)
        // Reset FSM to Idle: one-shot complete. Button returns to grey/green per position state.
        // CYC=2: account name guard(1), Dispatcher marshal(2).
        private void OnBeConnected(string instr, string accountName)
        {
            if (_leaderAccount == null || _leaderAccount.Name != accountName)
                return; // (1)
            Dispatcher.InvokeAsync(() => // (2)
            {
                _beState = BeState.Idle;
                UpdateBeVisuals(BeState.Idle);
            });
        }

        // B19 T1 -- GetAsk: returns current ask price from _instrument.MarketData.Ask.Price.
        // NT8-032: MarketData.Ask is MarketDataEventArgs; .Price is the double value.
        // Replaces GetRefPrice() (which used md.Last.Price -- wrong anchor). CYC=4.
        private double GetAsk()
        {
            if (_instrument == null)
                return 0.0; // (1) guard
            var md = _instrument.MarketData;
            if (md == null)
                return 0.0; // (2) guard
            var ask = md.Ask;
            if (ask == null)
                return 0.0; // (3) guard
            return ask.Price; // (4) double
        }

        // B19 T1 -- GetBid: returns current bid price from _instrument.MarketData.Bid.Price.
        // NT8-032: MarketData.Bid is MarketDataEventArgs; .Price is the double value.
        // Mirrors GetAsk() null-guard chain exactly. CYC=4.
        private double GetBid()
        {
            if (_instrument == null)
                return 0.0; // (1) guard
            var md = _instrument.MarketData;
            if (md == null)
                return 0.0; // (2) guard
            var bid = md.Bid;
            if (bid == null)
                return 0.0; // (3) guard
            return bid.Price; // (4) double
        }

        // B54: engine owns the change -- SetEnabled fires CopyEnabledChanged -> ApplyCopyState.
        // No direct button mutation here. CYC=1: straight-line engine call.
        private void OnCopyToggle(object sender, RoutedEventArgs e)
        {
            _engine.SetEnabled(!_engine.IsEnabled);
        }

        // B54: ApplyCopyState -- single path for all button visual updates.
        // Called by: OnLoaded (snap to engine truth on surface create/F5).
        //            OnCopyEnabledChanged (snap when engine fires event).
        // NEVER called from toggle handlers -- engine event drives all visuals.
        // CYC=2: (1) Dispatcher.InvokeAsync, (2) null guard inside lambda.
        // JS-021: no lock. JS-033: not async void (void event-callback pattern).
        private void ApplyCopyState(bool enabled)
        {
            _copyEnabled = enabled;
            Dispatcher.InvokeAsync(() =>
            {
                if (_copyToggleBtn2 == null)
                    return;
                _copyToggleBtn2.Content = enabled ? "\u25CF COPY ON" : "\u25CF COPY OFF";
                _copyToggleBtn2.Background = enabled ? BrushActive : BrushInactive;
            });
        }

        // B54: delegate to ApplyCopyState -- single visual update path.
        // CYC=1: straight-line delegation.
        private void OnCopyEnabledChanged(bool enabled)
        {
            ApplyCopyState(enabled);
        }

        // B33 T7 -- OnCancel2: dispatches to PttCancel module. CYC=1 (R7 dedup).
        // B30-B: leader resolved late via LogAndDispatchModule (DW-B30-03).
        private void OnCancel2(object sender, RoutedEventArgs e)
        {
            LogAndDispatchModule("[CANCEL]", "CANCEL");
        }

        // B12 T2 -- BuildCollapsibleHeader: builds collapse header row. CYC=1.
        private void BuildCollapsibleHeader(StackPanel root)
        {
            _collapseToggleBtn = new Button
            {
                Content = "\u25BC Position Tools",
                Margin = new Thickness(0, 0, 0, 2),
            };
            _collapseToggleBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _collapseToggleBtn.Click += OnCollapseClick;
            root.Children.Add(_collapseToggleBtn);
        }

        // B12 T2 -- OnCollapseClick: toggles _isCollapsed and sets _contentPanel.Visibility. CYC=2.
        private void OnCollapseClick(object sender, RoutedEventArgs e)
        {
            _isCollapsed = !_isCollapsed; // (1)
            if (_contentPanel != null) // (2)
                _contentPanel.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
            if (_collapseToggleBtn != null)
                _collapseToggleBtn.Content = _isCollapsed
                    ? "\u25B2 Position Tools"
                    : "\u25BC Position Tools";
        }

        // B10 T3 -- OnTightenStop: tighten stop button click handler.
        // CYC=4: instrument null(1), parse fallback(2), leader null branch(3), engine overload(4).
        // B30-B: uses leader overload when leader is available; falls back to all-accounts overload.
        // NT8-034: no Math.Clamp (.NET 4.8 version constraint -- not the NT8-003 volatile ban).
        // JS-021: no lock -- _engine.TightenStop iterates ConcurrentBag (lock-free).
        private void OnTightenStop(object sender, RoutedEventArgs e)
        {
            if (_instrument == null) // (1)
                return;
            var leader = _leaderAccount ?? TryResolveLeaderAccount(); // B30-B: late resolve
            int ticks = int.TryParse(_tightenTicksBox?.Text, out var t) // (2)
                ? Math.Max(1, Math.Min(500, t)) // clamp 1-500: no Math.Clamp (.NET 4.8 ban)
                : 5;
            if (leader != null) // (3)
                _engine.TightenStop(leader, _instrument, ticks); // B30-A leader overload (4)
            else
                _engine.TightenStop(_instrument, ticks); // fallback: all accounts
        }

        // B9 T3: Appends "Mode: [Signal] [Mirror]" radio button row to root StackPanel.
        // CYC=1 (straight-line widget construction, no branches).
        private void BuildModeRow(StackPanel root)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0),
            };
            var lbl = new Label
            {
                Content = "Mode:",
                Width = 42,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _signalModeBtn = new RadioButton
            {
                Content = "Signal",
                IsChecked = true,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _mirrorModeBtn = new RadioButton
            {
                Content = "Mirror",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _signalModeBtn.Click += OnSignalModeClick;
            _mirrorModeBtn.Click += OnMirrorModeClick;

            // B41: COPY ON/OFF ToggleButton relocated from BuildBufferedButtonsRow Row 3 to Mode row.
            _copyToggleBtn2 = new Button
            {
                Content = "\u25CF COPY OFF",
                Margin = new Thickness(8, 0, 0, 0),
                BorderBrush = BrushInactive,
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _copyToggleBtn2.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _copyToggleBtn2.Click += OnCopyToggle;

            // B50: Clone radio button
            _cloneModeBtn = new RadioButton
            {
                Content = "Clone",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _cloneModeBtn.Click += OnCloneModeClick;

            row.Children.Add(lbl);
            row.Children.Add(_signalModeBtn);
            row.Children.Add(_mirrorModeBtn);
            row.Children.Add(_cloneModeBtn);
            row.Children.Add(_copyToggleBtn2);
            root.Children.Add(row);
        }

        // B9 T3: CYC=1 -- straight-line engine call
        private void OnSignalModeClick(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal);
            UpdateAtmComboVisibility(Visibility.Visible);
        }

        // B9 T3: CYC=1 -- straight-line engine call
        private void OnMirrorModeClick(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Mirror);
            UpdateAtmComboVisibility(Visibility.Visible);
        }

        // B50: OnCloneModeClick -- Clone radio button event handler. CYC=2.
        // JS-033: synchronous event handler (RoutedEventHandler) -- async void exemption NOT needed.
        // JS-021: no lock. Calls SetCopyMode (volatile int) + SetCloneAtmObjectCache (volatile ref) + SetCloneAtmCache (volatile string).
        // HOTFIX-B66-ATM-OBJ: capture ChartTrader.AtmStrategy OBJECT at click time (not .Name string).
        // .Name returns "AtmStrategy" (class name), not the template name. Object overload of
        // StartAtmStrategy(atm, order) is the correct path confirmed by NT8 community forum topic 5133.
        private void OnCloneModeClick(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            // Capture live AtmStrategy object from ChartTrader -- must be done on UI thread (we are).
            NinjaTrader.NinjaScript.AtmStrategy atmObj = null;
            if (_currentChart != null) // branch (1)
            {
                var ct = TradeCopierAddOn.FindVisualChild<ChartTrader>(_currentChart);
                atmObj = ct?.AtmStrategy;
            }
            CopyEngine.Instance.SetCloneAtmObjectCache(atmObj);
            string tpl = GetLeaderAtmTemplateName(_currentChart); // string for display only
            CopyEngine.Instance.SetCloneAtmCache(tpl);
            UpdateAtmComboVisibility(Visibility.Collapsed);
        }

        // B52: UpdateAtmComboVisibility -- sets Visibility on all tracked per-follower ATM combos.
        // B52: WeakReference<ComboBox> prunes dead refs in the same pass (prune-on-iterate pattern).
        // CYC=4: (1) for-loop body, (2) TryGetTarget true (apply), (3) TryGetTarget false (prune), (4) base.
        // JS-021: no lock. UI-thread-only -- called only from Click handlers (UI thread).
        private void UpdateAtmComboVisibility(Visibility v)
        {
            for (int i = _atmComboRefs.Count - 1; i >= 0; i--) // branch (1)
            {
                if (_atmComboRefs[i].TryGetTarget(out var cb)) // branch (2)
                    cb.Visibility = v;
                else
                    _atmComboRefs.RemoveAt(i); // branch (3): prune dead ref
            }
        }

        // B41: OnQuickClick -- fires per-chart Quick Exit bracket swap. CYC=2.
        // JS-033: synchronous void event handler. JS-021: no lock.
        private void OnQuickClick(object sender, RoutedEventArgs e)
        {
            if (_instrument == null)
                return; // (1)
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
            NinjaTrader.Code.Output.Process(
                "[PTT-QX] button: "
                    + (_leaderAccount?.Name ?? "null")
                    + " "
                    + (_instrument?.FullName ?? "null")
                    + " t1="
                    + _quickT1,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            var qx = new PttQuickExit();
            qx.Execute(_leaderAccount, _instrument, _quickT1, _quickT2); // (2)
        }

        // B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
        // JS-033: synchronous void event handler. JS-021: no lock.
        private void OnQuickAllClick(object sender, RoutedEventArgs e)
        {
            var gqx = new PttGlobalQuickExit();
            gqx.Execute();
        }

        // B41: OnQuickUp -- increment T1 by 1t, T2 by 2t (T2 = T1 x 2 invariant). CYC=2.
        private void OnQuickUp(object sender, RoutedEventArgs e)
        {
            _quickT1 = Math.Max(1, Math.Min(_quickT1 + 1, 100));
            _quickT2 = _quickT1 * 2;
            if (_quickBtn != null)
                _quickBtn.Content = FormatBuffer("Quick", _quickT1); // (2)
        }

        // B41: OnQuickDown -- decrement T1 by 1t (minimum 1), T2 = T1 x 2. CYC=2.
        private void OnQuickDown(object sender, RoutedEventArgs e)
        {
            _quickT1 = Math.Max(1, Math.Min(_quickT1 - 1, 100));
            _quickT2 = _quickT1 * 2;
            if (_quickBtn != null)
                _quickBtn.Content = FormatBuffer("Quick", _quickT1); // (2)
        }

        // R9: TryResolve2TargetContext -- shared guard + position-resolve helper for 2-target exit handlers.
        // Eliminates code duplication between OnInstr2tClick and OnInstrQAll2tClick (CodeScene L1998/L2030).
        // MUST only be called on UI thread (accesses Account.Positions).
        // JS-002: out params always assigned; targets sentinel = empty list (never null).
        // JS-021: no lock. ASCII-only.
        // CYC=4: (1)_instrument null, (2)_leaderAccount null after re-resolve, (3)FirstOrDefault lambda, (4)?? coalesce.
        private bool TryResolve2TargetContext(
            out int qty,
            out List<(double Price, int Qty)> targets
        )
        {
            qty = 0;
            targets = new List<(double Price, int Qty)>(); // empty sentinel, never null (JS-002)
            if (_instrument == null)
                return false; // (1)
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // (2)
            if (_leaderAccount == null)
                return false; // (2 cont.)
            var pos = _leaderAccount.Positions.FirstOrDefault(p =>
                p.Instrument?.FullName == _instrument.FullName
            ); // (3)
            qty = pos?.Quantity ?? 1; // (4)
            targets = Build2TargetList(qty);
            return true;
        }

        // R12: LogQxTwoTarget -- shared log helper for 2-target exit handlers.
        // Eliminates Code Duplication between OnInstr2tClick and OnInstrQAll2tClick (CodeScene L1921/L1944).
        // Called from UI-thread Click handlers only (after TryResolve2TargetContext returns true).
        // JS-021: no lock. JS-002: void, no return null. ASCII-only.
        // CYC=1: straight-line, no branches.
        private void LogQxTwoTarget(string prefix, int qty, List<(double Price, int Qty)> targets)
        {
            NinjaTrader.Code.Output.Process(
                prefix
                    + " button: "
                    + _leaderAccount.Name
                    + " "
                    + _instrument.FullName
                    + " qty="
                    + qty
                    + " T1="
                    + targets[0].Qty
                    + " T2="
                    + targets[1].Qty,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // B129/R9: OnInstr2tClick -- fires 2-target bracket exit on _leaderAccount + _instrument only.
        // CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
        // JS-021: no lock. JS-033: synchronous void event handler. ASCII-only labels.
        private void OnInstr2tClick(object sender, RoutedEventArgs e)
        {
            if (!TryResolve2TargetContext(out int qty, out var targets))
                return; // (1)
            LogQxTwoTarget("[PTT-QX-2T]", qty, targets);
            new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets); // (2)
        }

        // B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
        // CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
        // JS-021: no lock. ASCII-only.
        private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
        {
            if (!TryResolve2TargetContext(out int qty, out var targets))
                return; // (1)
            LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
            new PttGlobalQuickExit().Execute(targets); // (2)
        }

        // B47 T5-B: OnQuickAllUp -- increment singleton; label refresh via broadcast. CYC=1.
        // HOTFIX-QUICKALL-SINGLETON-01: _quickAllT1 removed; delegates to CopyEngine.IncrementQuickAll().
        private void OnQuickAllUp(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.IncrementQuickAll();
        }

        // B47 T5-B: OnQuickAllDown -- decrement singleton; label refresh via broadcast. CYC=1.
        // HOTFIX-QUICKALL-SINGLETON-01: _quickAllT1 removed; delegates to CopyEngine.DecrementQuickAll().
        private void OnQuickAllDown(object sender, RoutedEventArgs e)
        {
            CopyEngine.Instance.DecrementQuickAll();
        }

        // B41: RefreshQuickDisplay -- Card A: back-calc actual T1 ticks from live PTT-QX-T1 order.
        // Updates display only -- does NOT call SetQuickTicks (no persistence).
        // BWAVE-CYC T4: ComputeT1Ticks extracted from RefreshQuickDisplay. CCN=3.
        private static int ComputeT1Ticks(
            bool isLong,
            Order t1Ord,
            double avgPrice,
            double tickSize
        )
        {
            double rawDiff = isLong
                ? t1Ord.LimitPrice - avgPrice
                : avgPrice - t1Ord.LimitPrice;
            int liveT1 = (int)Math.Round(rawDiff / tickSize);
            if (liveT1 < 1)
                liveT1 = 1;
            return liveT1;
        }

        // B41: RefreshQuickDisplay after extraction. CCN=6.
        private void RefreshQuickDisplay(Account acc, Instrument instr)
        {
            var t1Ord = FindWorkingOrder(acc, instr, "PTT-QX-T1");
            if (t1Ord == null)
                return;
            var pos = CopyEngine.Instance?.FindPositionPublic(acc, instr);
            if (pos == null || pos.Quantity == 0)
                return;
            double tick = instr.MasterInstrument?.TickSize ?? 0.25;
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            _quickT1 = ComputeT1Ticks(isLong, t1Ord, pos.AveragePrice, tick);
            _quickT2 = _quickT1 * 2;
            if (_quickBtn != null)
                _quickBtn.Content = FormatBuffer("Quick", _quickT1);
        }

        // B41: UpdateT3Visibility -- MUST be called on UI thread (_quickT3Row is a UI element).
        // CYC=2: targets null(1), count >= 3(2).
        private void UpdateT3Visibility(Account acc, Instrument instr)
        {
            var targets = CopyEngine.Instance?.SnapshotTargetsPublic(acc, instr);
            bool show = targets != null && targets.Count >= 3; // (1)(2)
            if (_quickT3Row != null)
                _quickT3Row.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // B41: FindWorkingOrder -- returns first Working order matching instrument + name. CYC=2.
        // JS-002: returns null if none (null is valid sentinel, used in RefreshQuickDisplay null guard).
        private static Order FindWorkingOrder(Account acc, Instrument instr, string orderName)
        {
            if (acc == null || instr == null)
                return null;
            foreach (var o in acc.Orders) // (1)
            {
                if (o.Instrument != instr)
                    continue;
                if (o.Name != orderName)
                    continue;
                if (o.OrderState == OrderState.Working)
                    return o; // (2)
            }
            return null;
        }

        // B41: OnLeaderOrderUpdate -- NT8 fires on background thread; dispatch to UI thread.
        // CYC=3: null guard(1), name filter(2), state filter(3).
        private void OnLeaderOrderUpdate(object sender, OrderEventArgs e)
        {
            if (e == null || e.Order == null)
                return; // (1)
            if (e.Order.Name != "PTT-QX-T1")
                return; // (2)
            if (e.Order.OrderState != OrderState.Working)
                return; // (3)
            var acc = e.Order.Account;
            var instr = e.Order.Instrument;
            Dispatcher.InvokeAsync(() => RefreshQuickDisplay(acc, instr));
        }

        // B41: OnLeaderPositionUpdate -- NT8 fires on background thread; dispatch to UI thread.
        // BWAVE-CYC T4: IsRemoveEventForMyInstrument extracted from OnLeaderPositionUpdate. CCN=4.
        // Returns true when all conditions confirm this is a Remove event for this panel's instrument.
        private bool IsRemoveEventForMyInstrument(PositionEventArgs e)
        {
            if (e.Operation != Operation.Remove)
                return false;
            if (e.Position?.Instrument?.FullName == null)
                return false;
            if (_instrument == null)
                return false;
            if (e.Position.Instrument.FullName != _instrument.FullName)
                return false;
            return true;
        }

        // OnLeaderPositionUpdate after extraction. CCN=6.
        // Both Dispatcher.InvokeAsync calls MUST stay here (NT8 UI thread contract).
        private void OnLeaderPositionUpdate(object sender, PositionEventArgs e)
        {
            if (e == null || e.Position == null)
                return;
            if (e.Position.Instrument == null)
                return;
            var acc = e.Position.Account;
            var instr = e.Position.Instrument;
            Dispatcher.InvokeAsync(() =>
            {
                RefreshQuickDisplay(acc, instr);
                UpdateT3Visibility(acc, instr);
            });
            if (!IsRemoveEventForMyInstrument(e))
                return;
            Dispatcher.InvokeAsync(() => UpdateButtonColors(false, false));
        }

        // B47 T1-B: LoadFollowers -- build inline follower rows into _followerScrollViewerPanel.
        // CYC=2: null guard [1] + foreach [2].
        // Called from OnLoaded() after _followerItems is populated from Account.All.
        // JS-021: no lock. UI-thread only (called on Loaded event).
        // NT8-019: no async void. NT8-003: no volatile.
        private void LoadFollowers()
        {
            if (_followerScrollViewerPanel == null)
                return; // guard [1]
            _followerScrollViewerPanel.Children.Clear();
            foreach (var item in _followerItems) // loop [2]
                BuildInlineFollowerRow(item);
            SortFollowerRows(); // B47 T4-B: initial sort (checked first, alpha within group)
        }

        // B47 T1-B: BuildInlineFollowerRow -- imperative row construction, no DataTemplate.
        // CYC=1: straight-line. JS-021: no lock. NT8-012: no FrameworkElementFactory.
        // ATM ComboBox IsEnabled is set by CheckBox Checked/Unchecked handlers (code-behind).
        // Row: [CheckBox][account TextBlock][P&L TextBlock][ATM ComboBox]  -- 4 columns per spec.
        private void BuildInlineFollowerRow(FollowerItem item)
        {
            // HOTFIX-FOLLOWER-LABEL-CLIP-01: switched from StackPanel to DockPanel so the account
            // name label stretches to fill all remaining space. Fixed Width=90 was clipping long
            // PA-APEX account names (e.g. "PA-APEX-422136-01U" = 20 chars at ~8px/char needs ~160px).
            // PnL and ATM combo are docked Right so they never compete with the name.
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 1, 0, 1) };

            // Col 0: CheckBox -- docked Left, tracks IsSelected
            var chk = new CheckBox
            {
                IsChecked = item.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            DockPanel.SetDock(chk, Dock.Left);

            // Col 2 (docked Right): ATM ComboBox (NT8-045: populated from filesystem on Loaded event)
            // Docked before PnL so DockPanel processes right-most first.
            var atmCombo = new ComboBox
            {
                Width = 110,
                IsEnabled = item.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
            };
            atmCombo.AddHandler(
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded)
            );
            atmCombo.SelectionChanged += OnFollowerAtmTemplateComboChanged;
            atmCombo.DataContext = item;
            DockPanel.SetDock(atmCombo, Dock.Right);

            // Col 2 (docked Right): P&L TextBlock -- mirrors DailyPnlText/DailyPnlColor.
            // item.DailyPnlText: formatted string e.g. "+$125.00"
            // item.DailyPnlColor: SolidColorBrush -- green/red/neutral (already Freeze()d by FollowerItem)
            var pnlLabel = new TextBlock
            {
                Text = item.DailyPnlText,
                Width = 60,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0),
                Foreground = item.DailyPnlColor,
            };
            DockPanel.SetDock(pnlLabel, Dock.Right);

            // Col 1 (LastChildFill): Account name label -- fills all remaining space.
            // TextTrimming=CharacterEllipsis ensures long names degrade gracefully if panel is very narrow.
            var nameLabel = new TextBlock
            {
                Text = item.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 4, 0),
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");

            // CheckBox event handlers: toggle IsSelected + ATM IsEnabled + sort + auto-apply
            chk.Checked += (s, e) =>
            {
                item.IsSelected = true;
                atmCombo.IsEnabled = true;
                SortFollowerRows(); // B47 T4-B
                UpdateCopierHeader(); // B47 T3-B
                TryAutoApply(); // B47 T2-B
            };
            chk.Unchecked += (s, e) =>
            {
                item.IsSelected = false;
                atmCombo.IsEnabled = false;
                SortFollowerRows(); // B47 T4-B
                UpdateCopierHeader(); // B47 T3-B
                TryAutoApply(); // B47 T2-B
            };

            // DockPanel child order: Left-docked first, Right-docked next (ATM then PnL),
            // LastChildFill (name) added last so it fills the remaining centre space.
            row.Children.Add(chk);
            row.Children.Add(atmCombo);
            row.Children.Add(pnlLabel);
            row.Children.Add(nameLabel);
            _followerScrollViewerPanel.Children.Add(row);
        }

        // B47 T4-B: SortFollowerRows -- sort _followerItems and rebuild ScrollViewer panel children.
        // Sort order: checked items first; within each group, alpha by account Name.
        // Rebuilds _followerScrollViewerPanel.Children to match sorted _followerItems order.
        // CYC=3: null guard [1] + List.Sort call [2] + foreach rebuild [3].
        // JS-021: no lock. UI-thread only (called from CheckBox event handlers and LoadFollowers).
        private void SortFollowerRows()
        {
            if (_followerScrollViewerPanel == null)
                return; // guard [1]

            _followerItems.Sort(
                (a, b) => // [2]
                {
                    if (a.IsSelected != b.IsSelected)
                        return a.IsSelected ? -1 : 1; // checked first
                    string nameA = a.Account != null ? a.Account.Name : string.Empty;
                    string nameB = b.Account != null ? b.Account.Name : string.Empty;
                    return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                }
            );

            _followerScrollViewerPanel.Children.Clear();
            foreach (var item in _followerItems) // [3]
                BuildInlineFollowerRow(item);
        }

        // B47 T3-B: BuildCopierSection -- adds Copier header button + ScrollViewer to root.
        // CYC=1: straight-line construction.
        // _copierCollapseBtn text: "\u25BC Copier" (expanded) / "\u25B6 Copier  (N active)" (collapsed).
        // JS-021: no lock. NT8-019: no async void.
        private void BuildCopierSection(StackPanel root)
        {
            _copierCollapseBtn = new Button
            {
                Content = "\u25BC Copier",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 1),
            };
            _copierCollapseBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            _copierCollapseBtn.Click += OnCopierCollapseClick;
            root.Children.Add(_copierCollapseBtn);
            // B49: Mode row (Signal/Mirror/COPY OFF) moved inside Copier collapse box.
            // BuildModeRow appends directly to root -- it appears between the Copier header
            // and the follower scroll rows. Collapse click only hides _followerScrollViewer;
            // Mode row remains visible when Copier is collapsed (Director spec).
            BuildModeRow(root);
            root.Children.Add(_followerScrollViewer); // sole visual tree insertion point for _followerScrollViewer
        }

        // B47 T3-B: OnCopierCollapseClick -- toggles _followerScrollViewer Visibility.
        // CYC=2: null guard [1] + _copierCollapsed branch [2].
        // JS-021: no lock. NT8-019: no async void.
        private void OnCopierCollapseClick(object sender, RoutedEventArgs e)
        {
            if (_followerScrollViewer == null)
                return; // null guard [1]
            _copierCollapsed = !_copierCollapsed;
            _followerScrollViewer.Visibility = _copierCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateCopierHeader(); // [2]
        }

        // B47 T3-B: UpdateCopierHeader -- updates collapse button text to reflect current state.
        // Expanded:  "\u25BC Copier"
        // Collapsed: "\u25B6 Copier  (N active)" where N = checked follower count.
        // CYC=2: null guard [1] + _copierCollapsed branch [2].
        private void UpdateCopierHeader()
        {
            if (_copierCollapseBtn == null)
                return; // guard [1]
            if (_copierCollapsed) // [2]
                _copierCollapseBtn.Content = "\u25B6 Copier  (" + CountActiveFollowers() + " active)";
            else
                _copierCollapseBtn.Content = "\u25BC Copier";
        }

        // B47 T3-B: CountActiveFollowers -- count of _followerItems with IsSelected == true.
        // CYC=1: foreach loop.
        private int CountActiveFollowers()
        {
            int n = 0;
            foreach (var item in _followerItems)
                if (item.IsSelected)
                    n++;
            return n;
        }

        // B47 T2-B: TryAutoApply -- auto-applies copy rule on checkbox toggle or ATM change.
        // Called from: chk.Checked lambda, chk.Unchecked lambda (BuildInlineFollowerRow),
        //              OnFollowerAtmTemplateComboChanged.
        // CYC=3: leader-null guard [1], instrument-null guard [2], followers.Length==0 guard [3].
        // JS-021: no lock. JS-001: no throw. JS-002: no return null (all guard-returns).
        // JS-033: no async void -- synchronous void.
        private void TryAutoApply()
        {
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
            if (_leaderAccount == null)
                return; // guard [1]
            if (_instrument == null)
                return; // guard [2]
            var followers = GetSelectedFollowers();
            if (followers.Length == 0) // guard [3]
            {
                if (_statusText != null)
                    _statusText.Text = "No followers selected.";
                return;
            }
            var atmMap = BuildAtmMap(followers);
            var multipliers = BuildMultipliers(followers);
            _engine.AddRule(_instrument.FullName, _leaderAccount, followers, multipliers, atmMap);
            _engine.SaveRules();
            if (_statusText != null)
                _statusText.Text =
                    "Rule: " + _instrument.FullName + " leader=" + _leaderAccount.Name;
        }

        // R6: IsAccountInFollowers -- membership check extracted from BuildAtmMap(Account[]) Bumpy Road.
        // CYC=2: foreach(+1) + if(+1). JS-021: no lock. JS-002: no return null. Private static.
        private static bool IsAccountInFollowers(Account account, Account[] followers)
        {
            foreach (var f in followers)
                if (f == account)
                    return true;
            return false;
        }

        // B47 T2-B: BuildAtmMap -- build Dictionary<string, FollowerAtmMode> from selected followers.
        // Extracted from OnApplyRule inline code (same logic, same format).
        // R6: nested foreach replaced with IsAccountInFollowers helper. CYC=4. JS-021: no lock. JS-002: no return null.
        private Dictionary<string, FollowerAtmMode> BuildAtmMap(Account[] followers)
        {
            var map = new Dictionary<string, FollowerAtmMode>();
            foreach (var item in _followerItems)                                                    // +1
            {
                if (item.Account == null) continue;                                                 // +1
                if (!IsAccountInFollowers(item.Account, followers)) continue;                       // +1
                map[item.Account.Name] = ParseAtmModeNameLocal(item.AtmModeName ?? "Inherit");     // ?? = +1
            }
            return map;
        }

        // B47 T2-B: BuildMultipliers -- build int[] of per-follower multipliers.
        // Extracted from OnApplyRule inline code (same logic).
        // CYC=1: for loop only. JS-021: no lock. JS-002: no return null.
        private int[] BuildMultipliers(Account[] followers)
        {
            var multipliers = new int[followers.Length];
            for (int i = 0; i < followers.Length; i++)
            {
                foreach (var item in _followerItems)
                {
                    if (item.Account != followers[i])
                        continue;
                    multipliers[i] = item.Multiplier > 0 ? item.Multiplier : 1;
                    break;
                }
            }
            return multipliers;
        }

        // B43 T1: Row layout (left to right):
        //   [account name] [daily P&L] [mult TextBox w=30] [ATM template ComboBox w=120] [checkmark]
        // P&L text color: green(+) / red(-) / dim($0) per Live Map pillar Layer 2.
        // Binding: DailyPnlText + DailyPnlColor update via INotifyPropertyChanged on FollowerItem.
        // B10-UI-01: Row factory uses Grid (not StackPanel) so all 5 columns align
        // vertically across rows regardless of account name length.
        // ColumnDefinitions added at runtime via OnRowGridLoaded (WPF FEF limitation).
        // CYC=1 (no branches -- pure factory construction).
        private DataTemplate BuildCheckItemTemplate()
        {
            var template = new DataTemplate(typeof(FollowerItem));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.AddHandler(
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnRowGridLoaded)
            );

            // [1] Account name -- Col 0: star width, ellipsis trimming
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetValue(Grid.ColumnProperty, 0);
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding("Account.Name"));
            nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            // [2] Daily P&L -- Col 1: 62px fixed, right-aligned, color-coded
            var pnlFactory = new FrameworkElementFactory(typeof(TextBlock));
            pnlFactory.SetValue(Grid.ColumnProperty, 1);
            pnlFactory.SetBinding(TextBlock.TextProperty, new Binding("DailyPnlText"));
            pnlFactory.SetBinding(TextBlock.ForegroundProperty, new Binding("DailyPnlColor"));
            pnlFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            pnlFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            // [3] B8 T1: Multiplier TextBox -- Col 2: 30px fixed
            // Fires on WPF UI thread -- no Dispatcher needed (JS-023 compliant)
            var multFactory = new FrameworkElementFactory(typeof(TextBox));
            multFactory.SetValue(Grid.ColumnProperty, 2);
            multFactory.SetValue(TextBox.TextProperty, "1");
            multFactory.SetValue(
                TextBox.VerticalContentAlignmentProperty,
                VerticalAlignment.Center
            );
            multFactory.AddHandler(
                TextBox.TextChangedEvent,
                new TextChangedEventHandler(OnFollowerMultiplierChanged)
            );
            multFactory.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed);

            // [4] B43 T1: ATM template ComboBox (replaces Inherit/Market/Named ComboBox + namedBox TextBox).
            // Col 3. Width=120 to accommodate template names. Wired via FEF LoadedEvent + SelectionChangedEvent.
            // NT8-012: FEF AddHandler pattern for Loaded event -- mandatory for NT8 DataTemplate wiring.
            var atmTemplateFactory = new FrameworkElementFactory(typeof(ComboBox));
            atmTemplateFactory.SetValue(Grid.ColumnProperty, 3);
            atmTemplateFactory.SetValue(ComboBox.WidthProperty, 120.0);
            atmTemplateFactory.SetValue(ComboBox.MarginProperty, new Thickness(2));
            atmTemplateFactory.SetValue(ComboBox.ToolTipProperty, "ATM template for this follower");
            atmTemplateFactory.AddHandler(
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded)
            );
            atmTemplateFactory.AddHandler(
                Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler(OnFollowerAtmTemplateComboChanged)
            );

            // [5] Checkmark -- Col 4: 20px fixed, centered (was col 5 -- namedBox col removed)
            var chkFactory = new FrameworkElementFactory(typeof(CheckBox));
            chkFactory.SetValue(Grid.ColumnProperty, 4);
            chkFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding("IsSelected") { Mode = BindingMode.TwoWay }
            );
            chkFactory.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(OnFollowerChecked));
            chkFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            gridFactory.AppendChild(nameFactory);
            gridFactory.AppendChild(pnlFactory);
            gridFactory.AppendChild(multFactory);
            gridFactory.AppendChild(atmTemplateFactory);
            gridFactory.AppendChild(chkFactory);
            template.VisualTree = gridFactory;
            return template;
        }

        // B43 T1: Loaded handler for Grid rows materialized from BuildCheckItemTemplate.
        // Adds 5 ColumnDefinitions (was 6 -- namedBox col removed).
        // Tag=true guard prevents re-entry on re-layout (CYC branch 2).
        // CYC=2: type+null guard (branch 1) + already-configured guard (branch 2).
        // Col 0: Star, MinWidth 80 -- account name
        // Col 1: 62px fixed        -- daily P&L
        // Col 2: 30px fixed        -- multiplier TextBox
        // Col 3: 120px fixed       -- ATM template ComboBox (was 80px; wider for template names)
        // Col 4: 20px fixed        -- checkbox
        private void OnRowGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid grid)
                return; // branch 1: type + null guard
            if (grid.Tag is bool)
                return; // branch 2: already-configured guard
            grid.Tag = true;

            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 }
            );
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        }

        // B8 T1: handler for multiplier TextBox text change.
        // Fires on WPF UI thread. Parses int, clamps [1,10], sets item.Multiplier.
        // CYC=3 (sender null guard + parse guard + clamp). No Dispatcher needed.
        private void OnFollowerMultiplierChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null)
                return;
            var item = tb.DataContext as FollowerItem;
            if (item == null)
                return;
            if (!int.TryParse(tb.Text, out int parsed))
                return;
            item.Multiplier = parsed < 1 ? 1 : (parsed > 10 ? 10 : parsed);
        }

        // B43 T1: ATM template ComboBox Loaded handler.
        // Fires on WPF UI thread (DataTemplate instantiation). No Dispatcher needed.
        // Idempotency: Items.Count > 0 guard prevents double-population on re-layout.
        // Populates: "(none)" sentinel + AtmStrategyTemplates list.
        // Default: leader's current ChartTrader ATM template if found; else index 0.
        // CYC=4: (1) null guard, (2) idempotency guard, (3) foreach loop, (4) leader-default branch.
        // JS-021: no lock. JS-002: no return null. NT8-012: Loaded event via FEF AddHandler.
        private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null)
                return; // branch 1 -- null guard
            if (cb.Items.Count > 0)
                return; // branch 2 -- idempotency guard
            bool alreadyTracked = false;
            foreach (var wr in _atmComboRefs)
                if (wr.TryGetTarget(out var existing) && existing == cb)
                {
                    alreadyTracked = true;
                    break;
                }
            if (!alreadyTracked)
            {
                _atmComboRefs.Add(new WeakReference<ComboBox>(cb)); // B52: WeakReference prevents detached accumulation
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
            cb.Items.Add("(none)");
            string leaderTemplate = GetLeaderAtmTemplateName(_currentChart);
            PopulateAtmComboItems(cb, leaderTemplate, out int defaultIdx);
            cb.SelectedIndex = defaultIdx;
            ApplyAtmAutoSelect(cb, defaultIdx);
        }

        // DW-B51-03: extracted from OnFollowerAtmTemplateComboLoaded to reduce parent CYC.
        // Scans ATM template XML files and identifies the leader's default selection index.
        // CYC(Lizard)=4: dir-exists + foreach + leader-match + catch.
        private void PopulateAtmComboItems(ComboBox cb, string leaderTemplate, out int defaultIdx)
        {
            defaultIdx = 0;
            try
            {
                // NT8-045: AtmStrategyTemplates not available in Linting DLL -- use filesystem path.
                string atmDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8",
                    "templates",
                    "AtmStrategy"
                );
                if (System.IO.Directory.Exists(atmDir))
                {
                    foreach (var f in System.IO.Directory.GetFiles(atmDir, "*.xml"))
                    {
                        string tName = System.IO.Path.GetFileNameWithoutExtension(f);
                        cb.Items.Add(tName);
                        if (tName == leaderTemplate)
                            defaultIdx = cb.Items.Count - 1;
                    }
                }
            }
            catch
            {
                // Directory unavailable -- "(none)" only.
            }
        }

        // DW-B51-03: extracted from OnFollowerAtmTemplateComboLoaded to reduce parent CYC.
        // Applies auto-selection and writes AtmModeName on the FollowerItem if a named template was selected.
        // CYC(Lizard)=3: defaultIdx-guard + selName-guard + item-guard.
        private void ApplyAtmAutoSelect(ComboBox cb, int defaultIdx)
        {
            // B46 T2: write item.AtmModeName immediately on auto-select so OnApplyRule
            // picks up Named mode without requiring a manual ComboBox interaction.
            // defaultIdx == 0 means "(none)" was selected -- leave AtmModeName as "Inherit".
            if (defaultIdx > 0)
            {
                var selName = cb.Items[defaultIdx] as string;
                if (!string.IsNullOrEmpty(selName))
                {
                    var item =
                        (cb.DataContext as FollowerItem)
                        ?? FindAncestorDataContext<FollowerItem>(cb);
                    if (item != null)
                        item.AtmModeName = "Named:" + selName;
                }
            }
        }

        // B43 T1: ATM template ComboBox SelectionChanged handler.
        // Fires on WPF UI thread. Writes item.AtmModeName in "Inherit" or "Named:templateName" format.
        // Serialization format UNCHANGED -- CopyEngine.ParseAtmModeName parses both unchanged.
        // CYC=3: (1) cb null guard, (2) item null guard, (3) "(none)" branch.
        // JS-021: no lock. JS-002: no return null (guard-returns only -- not returning null values).
        private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null)
                return; // branch 1 -- guard
            var item =
                (cb.DataContext as FollowerItem) ?? FindAncestorDataContext<FollowerItem>(cb);
            if (item == null)
                return; // branch 2 -- guard
            var sel = cb.SelectedItem as string ?? string.Empty;
            item.AtmModeName =
                (sel == "(none)" || sel.Length == 0) // branch 3
                    ? "Inherit"
                    : "Named:" + sel;
            TryAutoApply();
        }

        // B43 T1: Reads the ATM template name currently selected in ChartTrader for the given chart.
        // Internal static for testability (T_B43_04 calls with null -- no WPF instantiation required).
        // NT8-008: Chart.ChartControl does not exist -- use FindVisualChild<ChartTrader> instead.
        // NT8-041: Reflection on ChartControl.Charts fails -- visual tree walk only.
        // HOTFIX-B66-ATM-TPL (v2): ChartTrader.AtmStrategy is a direct property (confirmed community
        //   NT8 forum: ChartControl.OwnerChart.ChartTrader.AtmStrategy, topic 5133 + 6060).
        //   Primary: ct.AtmStrategy?.Name -- zero child-walk, no index fragility.
        //   Class-name guard: if .Name == "AtmStrategy" (NT8 internal class, no template staged),
        //   fall through to Fallback-1 selector. Observed 2026-08-18 session.
        //   Fallback-1: FindVisualChild<AtmStrategySelector> (in case CT build differs).
        // BWAVE-CYC T2: extracted helpers for GetLeaderAtmTemplateName. CCN of each <= 3.

        // TryGetAtmNameFromStrategy: reads AtmStrategy.Name from ChartTrader. CCN=3.
        private static string TryGetAtmNameFromStrategy(ChartTrader ct)
        {
            if (ct.AtmStrategy == null)
                return string.Empty;
            var n = ct.AtmStrategy.Name ?? string.Empty;
            if (n.Length > 0 && n != "AtmStrategy")
                return n;
            return string.Empty;
        }

        // TryGetAtmNameFromSelector: finds AtmStrategySelector and reads SelectedItem. CCN=2.
        private static string TryGetAtmNameFromSelector(ChartTrader ct)
        {
            var sel =
                TradeCopierAddOn.FindVisualChild<
                    NinjaTrader.Gui.NinjaScript.AtmStrategy.AtmStrategySelector
                >(ct);
            if (sel == null)
                return string.Empty;
            return sel.SelectedItem as string ?? string.Empty;
        }

        // TryGetAtmNameFromComboBox: finds ComboBox at index 2 and reads SelectedItem. CCN=1.
        private static string TryGetAtmNameFromComboBox(ChartTrader ct)
        {
            var atmCb = TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2);
            return atmCb?.SelectedItem as string ?? string.Empty;
        }

        //   Fallback-2: FindVisualChildByIndex<ComboBox>(ct, 2) (legacy, pre-B66).
        // Returns string.Empty on any null/exception -- NEVER throws, NEVER returns null.
        // GetLeaderAtmTemplateName after extraction. CCN=5.
        internal static string GetLeaderAtmTemplateName(Chart currentChart)
        {
            if (currentChart == null)
                return string.Empty;
            try
            {
                var ct = TradeCopierAddOn.FindVisualChild<ChartTrader>(currentChart);
                if (ct == null)
                    return string.Empty;
                var name = TryGetAtmNameFromStrategy(ct);
                if (name.Length > 0)
                    return name;
                name = TryGetAtmNameFromSelector(ct);
                if (name.Length > 0)
                    return name;
                return TryGetAtmNameFromComboBox(ct);
            }
            catch
            {
                return string.Empty;
            }
        }

        // B43 T1: Walks the visual tree UPWARD from child, returning the DataContext of the first
        // ancestor whose DataContext is of type T. Fallback for FEF-instantiated templates where
        // DataContext is set on an ancestor Grid rather than the leaf control directly.
        // CYC=3: (1) child null guard, (2) while loop, (3) DataContext cast match.
        // JS-021: no lock. JS-002: returns default(T) -- not return null.
        // VisualTreeHelper.GetParent: must be called on WPF UI thread. Called only from UI-thread handlers.
        private static T FindAncestorDataContext<T>(DependencyObject child)
            where T : class
        {
            if (child == null)
                return default(T); // branch 1 -- null guard
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null) // branch 2 -- loop
            {
                var fe = parent as FrameworkElement;
                if (fe != null && fe.DataContext is T ctx)
                    return ctx; // branch 3 -- match found
                parent = VisualTreeHelper.GetParent(parent);
            }
            return default(T);
        }

        // -- B9 T2: Click trader event handlers ------------------------------------

        // CYC=1 -- straight-line volatile write
        private void OnBuyToggleClick(object sender, RoutedEventArgs e)
        {
            _clickBuy = true;
            _sellToggle.IsChecked = false;
        }

        // CYC=1 -- straight-line volatile write
        private void OnSellToggleClick(object sender, RoutedEventArgs e)
        {
            _clickBuy = false;
            _buyToggle.IsChecked = false;
        }

        // CYC=2 -- null guard (1) + _clickArmed branch (2)
        private void OnArmClick(object sender, RoutedEventArgs e)
        {
            if (_currentChart == null)
                return; // guard (1)
            _clickArmed = !_clickArmed; // volatile toggle
            if (_clickArmed) // branch (2)
                TradeCopierAddOn.RegisterClickTrader(_currentChart, this);
            else
                TradeCopierAddOn.UnregisterClickTrader(_currentChart);
            UpdateArmVisuals(_clickArmed);
        }

        // CYC=2 -- null guard (1) + armed branch (2)
        // Called on UI thread from OnArmClick -- no Dispatcher needed.
        private void UpdateArmVisuals(bool armed)
        {
            if (_armBtn == null)
                return; // guard (1)
            _armBtn.Content = armed ? "Disarm" : "Arm"; // branch (2)
            _armBtn.Background = armed
                ? MakeBrush(34, 197, 94) // green -- decimal RGB, no hex (JS-008)
                : MakeBrush(28, 33, 51); // dark surface color
        }

        // CYC=6 -- five guards + ternary; try/catch does NOT add CYC.
        // B17 T2: FindPriceCanvasPanel selects price canvas (MaxValue>0, widest panel).
        // BWAVE-CYC T4: ComputeTickAlignedPrice extracted from OnChartMouseDown. CCN=2.
        // Returns 0.0 as sentinel if raw price is invalid (JS-002: no return null).
        private double ComputeTickAlignedPrice(
            ChartControl chartControl,
            MouseButtonEventArgs e,
            Instrument instr
        )
        {
            Point mousePos = e.GetPosition(chartControl);
            double rawPrice = GetPriceAtY(chartControl, mousePos.Y, instr);
            if (rawPrice <= 0.0)
                return 0.0;
            double tickSize = instr.MasterInstrument.TickSize;
            return Math.Round(rawPrice / tickSize) * tickSize;
        }

        // OnChartMouseDown after extraction. CCN=7.
        // _leaderAccount.CreateOrder MUST stay here (NT8 Account API). Dispatcher.InvokeAsync stays.
        internal void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_clickArmed)
                return;
            if (_leaderAccount == null)
                return;
            if (_instrument == null)
                return;
            var chartControl = sender as ChartControl;
            if (chartControl == null)
                return;
            double price = ComputeTickAlignedPrice(chartControl, e, _instrument);
            if (price <= 0.0)
                return;
            bool isBuy = _clickBuy; // volatile read
            int qty = CopyEngine.Instance.GetSuggestedQty(_instrument);
            var action = isBuy ? OrderAction.Buy : OrderAction.SellShort;

            try
            {
                _leaderAccount.CreateOrder(
                    _instrument,
                    action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Day,
                    qty,
                    price,
                    0,
                    null,
                    "PTT-Click",
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() => SetStatus("PTT-Click error: " + ex.Message));
            }
        }

        // -- event handlers --------------------------------------------------------
        private void OnFollowerChecked(object sender, RoutedEventArgs e)
        {
            UpdateDropDownHeader();
        }

        private void UpdateDropDownHeader()
        {
            int count = 0;
            foreach (var item in _followerItems)
                if (item.IsSelected)
                    count++;
            if (_followersDropDown != null)
                _followersDropDown.Text = count + " selected";
        }

        private void OnTrim(object sender, RoutedEventArgs e)
        {
            if (_instrument != null)
                _engine.Trim(_leaderAccount, _instrument);
        }

        private void OnFlatten(object sender, RoutedEventArgs e)
        {
            if (_instrument != null)
                _engine.Flatten(_leaderAccount, _instrument);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_instrument != null)
                _engine.CancelPendingEntries(_leaderAccount, _instrument);
        }

        private Account[] GetSelectedFollowers()
        {
            var list = new List<Account>();
            foreach (var item in _followerItems)
                if (item.IsSelected && item.Account != null)
                    list.Add(item.Account);
            return list.ToArray();
        }

        // BWAVE-CYC T2: extracted helpers for OnApplyRule.

        // BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=5.
        // BWAVE-DW B-4: nested for+foreach replaced with inverted foreach + System.Array.IndexOf.
        // First-match semantics preserved: multipliers[idx]!=0 guard skips duplicate _followerItems entries.
        // JS-021: no lock. JS-002: no return null. JS-033: not async void.
        private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
        {
            var multipliers = new int[followers.Length];
            var atmNames = new string[followers.Length];
            foreach (var item in _followerItems)
            {
                if (item.Account == null) continue;
                int idx = System.Array.IndexOf(followers, item.Account);
                if (idx < 0 || multipliers[idx] != 0) continue;
                multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1;
                atmNames[idx] = item.AtmModeName ?? "Inherit";
            }
            return (multipliers, atmNames);
        }

        // BuildAtmMap: builds FollowerAtmMode dictionary from collected ATM names. CCN=2.
        private static Dictionary<string, FollowerAtmMode> BuildAtmMap(
            Account[] followers,
            string[] atmNames
        )
        {
            var atmMap = new Dictionary<string, FollowerAtmMode>();
            for (int i = 0; i < followers.Length; i++)
            {
                if (followers[i] != null)
                    atmMap[followers[i].Name] = ParseAtmModeNameLocal(atmNames[i]);
            }
            return atmMap;
        }

        // B8 T1+T2: OnApplyRule after extraction. CCN=8.
        // SetStatus: sets status text if control is not null. CCN=1.
        private void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.Text = text;
        }

        // OnApplyRule after extraction. CCN=7 (using SetStatus collapses 4 statusText guards).
        private void OnApplyRule(object sender, RoutedEventArgs e)
        {
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
            if (_leaderAccount == null)
            {
                SetStatus("No leader -- select account in ChartTrader.");
                return;
            }
            if (_instrument == null)
            {
                SetStatus("No instrument -- open a chart first.");
                return;
            }
            var followers = GetSelectedFollowers();
            if (followers.Length == 0)
            {
                SetStatus("Select follower account(s).");
                return;
            }
            var (multipliers, atmNames) = BuildFollowerMultipliers(followers);
            var atmMap = BuildAtmMap(followers, atmNames);
            _engine.AddRule(_instrument.FullName, _leaderAccount, followers, multipliers, atmMap);
            _engine.SaveRules();
            SetStatus("Rule: " + _instrument.FullName + " leader=" + _leaderAccount.Name);
        }

        // B8 T2: ParseAtmModeNameLocal -- private static helper that mirrors CopyEngine.ParseAtmModeName.
        // Keeps Panel self-contained without exposing engine internals. CYC=3.
        private static FollowerAtmMode ParseAtmModeNameLocal(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new FollowerAtmMode.Inherit();
            if (name == "Market")
                return new FollowerAtmMode.Market();
            if (name.StartsWith("Named:"))
                return new FollowerAtmMode.Named(name.Substring(6));
            return new FollowerAtmMode.Inherit();
        }

        private void OnStatusUpdate(string line)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_statusText != null)
                    _statusText.Text = line;
            });
        }

        // B11 T1: SIM101 temporary status text helper.
        // Called from TradeCopierAddOn.OnChartKeyDiag via Dispatcher.InvokeAsync.
        // Sets _statusText.Text directly on the UI thread.
        // CYC=1: null guard only.
        internal void SetStatusText(string text)
        {
            if (_statusText == null)
                return;
            _statusText.Text = text;
        }

        // B11 T1: chart.PreviewKeyDown handler wired by TradeCopierAddOn.HookKeyShortcut().
        // Fires on WPF UI thread -- no Dispatcher needed.
        // CYC=3: instrument null guard (1), modifier guard (2), delegate to DispatchShortcut (3).
        // Jane Street: guard-early, zero branches in the hot dispatch path.
        internal void OnChartKeyDown(object sender, KeyEventArgs e)
        {
            if (_instrument == null)
                return; // guard (1)
            if (
                (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift))
                != (ModifierKeys.Control | ModifierKeys.Shift)
            )
                return; // guard (2)
            DispatchShortcut(e.Key); // guard (3): delegate
        }

        // B11 T1: Jane Street switch preferred over if/else chain.
        // Cases: T=Trim, F=Flatten, C=CancelPendingEntries, B=BreakEven.
        // B19 T1 -- DispatchShortcut: keyboard shortcuts dispatch to engine methods.
        // Calls EXISTING CopyEngine public methods -- no new CopyEngine code added.
        // CYC=5: switch entry (1) + 4 case arms (2,3,4,5).
        // BE path reads _beBufferBox.Text for buffer ticks (UI-thread-safe; PreviewKeyDown is on UI thread).
        // Key.T: Trim limit @ ask + buffer*tick (long) or bid - buffer*tick (short). Falls back to market on zero ask/bid.
        // Key.F: Flatten limit @ ask + buffer*tick (long) or bid - buffer*tick (short). Same fallback.
        private void DispatchShortcut(Key key)
        {
            switch (key)
            {
                case Key.T:
                    _engine.Trim(_leaderAccount, _instrument, _trimBuffer, GetAsk(), GetBid());
                    break;
                case Key.F:
                    _engine.Flatten(
                        _leaderAccount,
                        _instrument,
                        _flattenBuffer,
                        GetAsk(),
                        GetBid()
                    );
                    break;
                case Key.C:
                    _engine.CancelPendingEntries(_leaderAccount, _instrument);
                    break;
                case Key.B:
                    int buf = 2;
                    int.TryParse(_beBufferBox.Text, out buf);
                    _engine.BreakEven(_leaderAccount, _instrument, buf);
                    break;
            }
        }

        // B12 T3 -- BuildRiskAtrRow: builds Risk $ + ATR % spinner row. CYC=1: straight-line construction.
        // R4 extraction: delegates spinner column construction to BuildSpinnerColumn and ATR display row to BuildAtrDisplayRow.
        // Called from BuildUI() at end of _contentPanel.
        private void BuildRiskAtrRow(StackPanel root)
        {
            _atrRow = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 0) };
            _riskDollarsBox = new TextBox
            {
                Text = _maxRiskDollars.ToString("F0"),
                Width = 55,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _riskDollarsBox.SetResourceReference(Control.StyleProperty, "NTTextBoxStyle");
            _riskDollarsBox.LostFocus += OnRiskTextLostFocus;
            _atrRow.Children.Add(BuildSpinnerColumn("Risk $", _riskDollarsBox, OnRiskUp, OnRiskDown));
            _atrFractionBox = new TextBox
            {
                Text = _atrFraction.ToString("F2"),
                Width = 55,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _atrFractionBox.SetResourceReference(Control.StyleProperty, "NTTextBoxStyle");
            _atrFractionBox.LostFocus += OnAtrFractionTextLostFocus;
            _atrRow.Children.Add(BuildSpinnerColumn("ATR %", _atrFractionBox, OnAtrFractionUp, OnAtrFractionDown));
            root.Children.Add(_atrRow);
            root.Children.Add(BuildAtrDisplayRow());
        }

        // R4 -- BuildSpinnerColumn: creates a horizontal StackPanel with a label, TextBox, and up/down arrow buttons.
        // CYC=1: straight-line construction. No return null. Private instance.
        private StackPanel BuildSpinnerColumn(
            string labelText,
            TextBox valueBox,
            RoutedEventHandler upClick,
            RoutedEventHandler downClick)
        {
            var col = new StackPanel { Orientation = Orientation.Horizontal };
            var label = new TextBlock
            {
                Text = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
            var arrows = new Grid();
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            var upBtn = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "\u25B2",
                Height = 12,
            };
            var dnBtn = new System.Windows.Controls.Primitives.RepeatButton
            {
                Content = "\u25BC",
                Height = 12,
            };
            upBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            dnBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
            upBtn.Click += upClick;
            dnBtn.Click += downClick;
            Grid.SetRow(upBtn, 0);
            Grid.SetRow(dnBtn, 1);
            arrows.Children.Add(upBtn);
            arrows.Children.Add(dnBtn);
            col.Children.Add(label);
            col.Children.Add(valueBox);
            col.Children.Add(arrows);
            return col;
        }

        // R4 -- BuildAtrDisplayRow: creates the ATR display Border with _atrDisplayLabel TextBlock.
        // CYC=1: straight-line construction. No return null. Private instance.
        private Border BuildAtrDisplayRow()
        {
            var atrRow = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2),
            };
            _atrDisplayLabel = new TextBlock { Text = "ATR=-.-- pts -> stopTicks=-- -> qty=--" };
            atrRow.Child = _atrDisplayLabel;
            return atrRow;
        }

        // B20-LANE-C T5 -- SetAtrText: updates ATR display label from UpdateAtrOverlay via Dispatcher.InvokeAsync.
        // CYC=2: null guard (1) + Text assignment (2). Runs on UI thread only (caller uses InvokeAsync).
        // JS-021: no lock. Caller (TradeCopierAddOn.UpdateAtrOverlay) dispatches to UI thread before calling.
        public void SetAtrText(string display)
        {
            if (_atrDisplayLabel == null)
                return;
            _atrDisplayLabel.Text = display;
        }

        // B12 T3 -- OnRiskUp: increment _maxRiskDollars, clamp, push. CYC=1.
        private void OnRiskUp(object sender, RoutedEventArgs e)
        {
            _maxRiskDollars = Math.Max(Math.Min(_maxRiskDollars + 25.0, 1000.0), 10.0); // no Math.Clamp (NT8 .NET 4.8)
            if (_riskDollarsBox != null)
                _riskDollarsBox.Text = _maxRiskDollars.ToString("F0");
            NotifyRiskChanged();
        }

        // B12 T3 -- OnRiskDown: decrement _maxRiskDollars, clamp, push. CYC=1.
        private void OnRiskDown(object sender, RoutedEventArgs e)
        {
            _maxRiskDollars = Math.Max(Math.Min(_maxRiskDollars - 25.0, 1000.0), 10.0); // no Math.Clamp
            if (_riskDollarsBox != null)
                _riskDollarsBox.Text = _maxRiskDollars.ToString("F0");
            NotifyRiskChanged();
        }

        // R8: TryParseAndClamp -- shared parse+clamp helper. CCN=2.
        private static bool TryParseAndClamp(string text, double min, double max, out double result)
        {
            if (!double.TryParse(text, out result))
                return false; // (1) parse guard
            result = Math.Max(Math.Min(result, max), min);
            return true;
        }

        // B12 T3 / R8 -- OnRiskTextLostFocus: parse + clamp + push. CCN=2.
        private void OnRiskTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (!TryParseAndClamp(_riskDollarsBox?.Text, 10.0, 1000.0, out double v))
                return; // (1)
            _maxRiskDollars = v;
            if (_riskDollarsBox != null)
                _riskDollarsBox.Text = v.ToString("F0"); // (2) normalise display
            NotifyRiskChanged();
        }

        // B12 T3 -- OnAtrFractionUp: increment _atrFraction, clamp, push. CYC=1.
        private void OnAtrFractionUp(object sender, RoutedEventArgs e)
        {
            _atrFraction = Math.Max(Math.Min(_atrFraction + 0.05, 3.00), 0.25); // no Math.Clamp
            if (_atrFractionBox != null)
                _atrFractionBox.Text = _atrFraction.ToString("F2");
            NotifyAtrFractionChanged();
        }

        // B12 T3 -- OnAtrFractionDown: decrement _atrFraction, clamp, push. CYC=1.
        private void OnAtrFractionDown(object sender, RoutedEventArgs e)
        {
            _atrFraction = Math.Max(Math.Min(_atrFraction - 0.05, 3.00), 0.25); // no Math.Clamp
            if (_atrFractionBox != null)
                _atrFractionBox.Text = _atrFraction.ToString("F2");
            NotifyAtrFractionChanged();
        }

        // B12 T3 / R8 -- OnAtrFractionTextLostFocus: parse + clamp + push. CCN=2.
        private void OnAtrFractionTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (!TryParseAndClamp(_atrFractionBox?.Text, 0.25, 3.00, out double v))
                return; // (1)
            _atrFraction = v;
            if (_atrFractionBox != null)
                _atrFractionBox.Text = v.ToString("F2"); // (2) normalise display
            NotifyAtrFractionChanged();
        }

        // B12 T3 -- NotifyRiskChanged: delegates to CopyEngine.UpdateMaxRisk. CYC=2.
        private void NotifyRiskChanged()
        {
            if (_engine == null)
                return; // (1)
            _engine.UpdateMaxRisk(_maxRiskDollars); // (2)
        }

        // B12 T3 -- NotifyAtrFractionChanged: delegates to CopyEngine.UpdateAtrFraction. CYC=2.
        private void NotifyAtrFractionChanged()
        {
            if (_engine == null)
                return; // (1)
            _engine.UpdateAtrFraction(_atrFraction); // (2)
        }

        // BWAVE-CYC T3: extracted helpers for ApplyFeatureFlags / ApplyFeatureFlagTooltips.

        // ApplyTrimFlattenFlags: sets IsEnabled on trim/flatten/cancel buttons. CCN=3.
        private void ApplyTrimFlattenFlags(FeatureFlags f)
        {
            if (_trimBtn2 != null)
                _trimBtn2.IsEnabled = f.TrimFlatten;
            if (_flattenBtn2 != null)
                _flattenBtn2.IsEnabled = f.TrimFlatten;
            if (_cancelBtn2 != null)
                _cancelBtn2.IsEnabled = f.TrimFlatten;
        }

        // ApplyPositionControlFlags: sets IsEnabled on BE and mirror-mode buttons. CCN=2.
        private void ApplyPositionControlFlags(FeatureFlags f)
        {
            if (_beBtn2 != null)
                _beBtn2.IsEnabled = f.BreakEven;
            if (_mirrorModeBtn != null)
                _mirrorModeBtn.IsEnabled = f.MirrorMode;
        }

        // ApplyRowVisibilityFlags: sets Visibility on click-trader and ATR rows. CCN=4.
        private void ApplyRowVisibilityFlags(FeatureFlags f)
        {
            if (_clickTraderRow != null)
                _clickTraderRow.Visibility = f.ClickTrader
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            if (_atrRow != null)
                _atrRow.Visibility = f.AtrSizing
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        }

        // SetButtonTooltip: sets or clears a button ToolTip based on feature enabled. CCN=2.
        // JS-002: btn parameter null guard -- no throw. Returns void.
        private static void SetButtonTooltip(
            System.Windows.Controls.Control btn,
            bool featureEnabled,
            string upgradeMessage
        )
        {
            if (btn != null)
                btn.ToolTip = featureEnabled ? null : upgradeMessage;
        }

        // BGTM-1: Enable/disable and show/hide panel controls per feature flags.
        // ApplyFeatureFlags after extraction. CCN=4.
        internal void ApplyFeatureFlags(FeatureFlags f)
        {
            ApplyTrimFlattenFlags(f);
            ApplyPositionControlFlags(f);
            ApplyRowVisibilityFlags(f);
            ApplyFeatureFlagTooltips(f);
        }

        // BGTM-1: Set ToolTip on disabled buttons to upgrade guidance.
        // ApplyFeatureFlagTooltips after extraction. CCN=2.
        private void ApplyFeatureFlagTooltips(FeatureFlags f)
        {
            SetButtonTooltip(_trimBtn2, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            SetButtonTooltip(_flattenBtn2, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            SetButtonTooltip(_cancelBtn2, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            SetButtonTooltip(_beBtn2, f.BreakEven, "Break Even requires Pro tier");
            SetButtonTooltip(_mirrorModeBtn, f.MirrorMode, "Mirror mode requires Elite tier");
        }

        // BGTM-1: Handle CopyEngine.FeatureFlagsChanged event. Fires on UI thread. CYC=1.
        // JS-021: no lock. Architecture plan Section 12: event fires on UI thread only.
        private void OnFeatureFlagsChanged(FeatureFlags f)
        {
            ApplyFeatureFlags(f);
        }
    }
}
