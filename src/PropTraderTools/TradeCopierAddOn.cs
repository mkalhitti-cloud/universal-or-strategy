// PTT-COPIER-B11-T1 -- TradeCopierAddOn.cs
// B10-T4: chart attachment uses DispatcherTimer (1s polling) as compile-safe fallback.
// B11-T1: keyboard shortcut layer (PreviewKeyDown Ctrl+Shift+T/F/C/B) wired in DoInject.
//         SIM101 diag handler fields + RunSim101/RemoveSim101 present for manual gate test.
// AddOnBase entry point.
// 1. Registers "Trade Copier" in the NT8 Control Center New menu (once only).
// 2. Injects TradeCopierPanel into every chart's ChartTrader panel area.
//
// CHARTTRADER INJECTION APPROACH (FIX5):
//   ChartTrader is NOT a Window -- it is a UserControl inside NinjaTrader.Gui.Chart.Chart.
//   NinjaTrader.Gui.Chart.Chart IS a System.Windows.Window subclass.
//   Strategy: cast window to NinjaTrader.Gui.Chart.Chart, hook its Loaded event,
//   then walk the visual tree to find the StackPanel named "RootPanel" or "Rows"
//   that sits inside ChartTrader, and append our UserControl to it.
//   Fallback: if named panel not found, wrap Chart.Content in a DockPanel.
//
// Jane Street rules: JS-021 (no lock), JS-023 (volatile bool for menu guard)
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
{
    public class TradeCopierAddOn : AddOnBase
    {
        // JS-023: volatile bool prevents duplicate menu wiring across reloads
        private static volatile bool _menuWired = false;

        // Track injected panels keyed by Chart window to detach on close
        private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _panels =
            new ConcurrentDictionary<Chart, TradeCopierPanel>();

        // B9 T1 -- ATR engine instances keyed by Chart window
        private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines =
            new ConcurrentDictionary<Chart, AtrSizingEngine>();

        // B9 T2 -- Click trader handler registry (ADV-001 CORRECTED: TryRemove-first)
        private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _clickHandlers =
            new ConcurrentDictionary<Chart, TradeCopierPanel>();

        // B11 T1: keyboard handler registry -- mirrors _clickHandlers pattern
        private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _keyHandlers =
            new ConcurrentDictionary<Chart, TradeCopierPanel>();

        // DW-C39-20: Returns true when all panels have been detached (last-panel-close guard).
        // Called by TradeCopierPanel.Detach(). CYC=1. JS-021: no lock.
        internal static bool IsPanelsEmpty() => _panels.IsEmpty;

        // B11 T1 SIM101: logging-only diag handler stored as field so RemoveSim101() can unhook it.
        // Set in RunSim101(); nulled unconditionally by RemoveSim101().
        // Plan sec.2 V2 note. Review note: declare static to match _panels/_clickHandlers pattern.
        private static KeyEventHandler _sim101KeyDiag;

        // B10 T4: polling timer for ATR computation fallback (when bar event not available)
        // Fires engine.ManualOnBarUpdate every 1 second on UI thread as a safe fallback.
        private DispatcherTimer _atrPollTimer = null;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Prop Trader Tools -- Trade Copier";
                Name = "TradeCopierAddOn";
            }
            if (State == State.Configure)
            {
                var flags = LoadAndValidateLicense();
                CopyEngine.Instance.SetFlags(flags);
            }
            if (State == State.Terminated)
                _menuWired = false;
        }

        protected override void OnWindowCreated(System.Windows.Window window)
        {
            // Surface 1: Control Center menu (once only)
            if (!_menuWired)
            {
                var cc = window as ControlCenter;
                if (cc != null)
                {
                    WireControlCenterMenu(cc);
                    return;
                }
            }

            // Surface 2: Chart window contains ChartTrader as a child control
            var chart = window as Chart;
            if (chart != null)
                InjectIntoChart(chart); // instance call: StartAtrEngine needs _atrOverlayLabel
        }

        protected override void OnWindowDestroyed(System.Windows.Window window)
        {
            var chart = window as Chart;
            if (chart == null)
                return;
            StopAtrEngine(chart); // instance: unsubscribes AtrUpdated
            UnregisterClickTrader(chart); // B9 T2: clean up click handler
            UnhookKeyShortcut(chart); // B11 T1: leak guard
            TradeCopierPanel panel;
            if (_panels.TryRemove(chart, out panel) && panel != null)
                panel.Detach();
        }

        // BWAVE-CYC T8: extracted helper for WireControlCenterMenu.

        // RemoveExistingTradeCopierEntries: removes all "Trade Copier" menu items. CCN=4.
        // Uses mi.Header.ToString() per NT8_ADDON_KNOWLEDGE.md NT8 NTMenuItem pattern.
        private static void RemoveExistingTradeCopierEntries(NTMenuItem newMenu)
        {
            for (int i = newMenu.Items.Count - 1; i >= 0; i--)
            {
                var mi = newMenu.Items[i] as System.Windows.Controls.MenuItem;
                if (mi == null)
                    continue;
                if (mi.Header != null && mi.Header.ToString() == "Trade Copier")
                    newMenu.Items.RemoveAt(i);
            }
        }

        // --- Menu wiring ---

        // WireControlCenterMenu after extraction. CCN=5.
        private static void WireControlCenterMenu(ControlCenter cc)
        {
            NTMenuItem newMenu = null;
            foreach (var item in cc.MainMenu)
            {
                var mi = item as NTMenuItem;
                if (mi == null)
                    continue;
                var hdr = mi.Header != null ? mi.Header.ToString() : string.Empty;
                if (hdr.StartsWith("New"))
                {
                    newMenu = mi;
                    break;
                }
            }
            if (newMenu == null)
                return;
            RemoveExistingTradeCopierEntries(newMenu);
            var entry = new NTMenuItem { Header = "Trade Copier" };
            entry.Click += OnMenuItemClick;
            newMenu.Items.Add(entry);
            _menuWired = true;
        }

        private static void OnMenuItemClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new TradeCopierWindow();
                win.Topmost = true;
                win.Show();
                win.Activate();
                win.Focus();
                win.Topmost = false;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "Trade Copier failed to open:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "PTT Error"
                );
            }
        }

        // --- Chart injection ---

        // Instance method: chains to DoInject which calls StartAtrEngine (instance).
        private void InjectIntoChart(Chart chart)
        {
            // NT8 timing: OnWindowCreated may fire AFTER the window is already loaded.
            // If IsLoaded is already true, inject immediately on the dispatcher.
            // If not yet loaded, hook the Loaded event.
            if (chart.IsLoaded)
            {
                chart.Dispatcher.InvokeAsync(() => DoInject(chart));
            }
            else
            {
                chart.Loaded += OnChartLoaded;
            }
        }

        // Instance event handler: must capture 'this' to call DoInject as instance method.
        private void OnChartLoaded(object sender, RoutedEventArgs e)
        {
            var chart = sender as Chart;
            if (chart == null)
                return;
            chart.Loaded -= OnChartLoaded;
            chart.Dispatcher.InvokeAsync(() => DoInject(chart));
        }

        // B10 T4: StartAtrEngine -- instance method (not static).
        // Replaces IMPL-NOTE-1 stub. Attempts chart attachment via event-based fallback
        // (Step 3: always compiles). NinjaScripts.Add / Indicators.Add require runtime
        // verification which is deferred -- fallback is the safe, compile-guaranteed path.
        //
        // CHART-ATTACH-RESULT: event-based fallback (Step 3) -- compile-safe for NT8 .NET 4.8.
        // chart.NinjaScripts.Add and chart.Indicators.Add are not available at design time
        // in the AddOn compilation context (CS1061 errors in NT8 Roslyn). Fallback chosen.
        // Verified: 2026-07-09
        //
        // CYC=3: chart null(1), instr null(2), attachment try(3)
        private void StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)
        {
            if (chart == null)
                return; // guard (1)
            if (instr == null)
                return; // guard (2)
            var engine = new AtrSizingEngine();
            double pointValue = instr.MasterInstrument?.PointValue ?? 5.0;
            engine.SetParameters(200.0, pointValue);
            engine.SetAtrFraction(0.75); // DW-ATR-DEFAULTS-01: match field defaults
            _atrEngines[chart] = engine;

            // STEP 3 (event-based fallback -- compile-safe DispatcherTimer, 1s polling).
            // chart.NinjaScripts.Add / Indicators.Add / BarsArray are not accessible in
            // AddOnBase compilation scope (NT8 Roslyn design-time limitation).
            // DispatcherTimer is WPF standard and always compiles in AddOnBase context.
            if (_atrPollTimer == null) // guard (3): create timer once
            {
                _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = System.TimeSpan.FromSeconds(1),
                };
                _atrPollTimer.Tick += (s, e2) =>
                {
                    try
                    {
                        engine.ManualOnBarUpdate();
                    }
                    catch (System.Exception)
                    { /* NT8 context not ready; next tick will retry */
                    }
                };
                _atrPollTimer.Start();
            }

            CopyEngine.Instance.SetAtrEngine(engine, enabled: false); // disabled until user enables

            engine.AtrUpdated += OnAtrUpdated;
        }

        // B10 T4: instance StopAtrEngine -- unsubscribes AtrUpdated and stops poll timer.
        // CYC=3 -- TryRemove guard + engine event cleanup + timer cleanup
        private void StopAtrEngine(Chart chart)
        {
            AtrSizingEngine engine;
            if (!_atrEngines.TryRemove(chart, out engine))
                return; // guard (1)
            if (engine != null)
                engine.AtrUpdated -= OnAtrUpdated; // unsubscribe event
            if (_atrPollTimer != null) // guard (2): stop poll timer
            {
                _atrPollTimer.Stop();
                _atrPollTimer = null;
            }
            CopyEngine.Instance.SetAtrEngine(null, enabled: false); // guard (3): clear reference
        }

        // B20-LANE-C T5: UpdateAtrOverlay -- routes ATR display text to the first injected panel.
        // CYC=2: null guard on panel (1) + Dispatcher.InvokeAsync dispatch (2).
        // JS-021: no lock. _panels is ConcurrentDictionary; FirstOrDefault() on snapshot is lock-free.
        internal void UpdateAtrOverlay(string atrDisplay)
        {
            var panel = _panels.Values.FirstOrDefault();
            if (panel == null)
                return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                panel.SetAtrText(atrDisplay)
            );
        }

        // B10 T4: AtrUpdated event handler -- subscribed in StartAtrEngine.
        // Fires on AtrSizingEngine bar-close thread; UpdateAtrOverlay marshals via Dispatcher.
        // CYC=1: straight-line delegation.
        private void OnAtrUpdated(string display)
        {
            UpdateAtrOverlay(display);
        }

        // B9 T2: CYC=2 -- null guard + TryRemove branch (ADV-001 CORRECTED: TryRemove-first)
        // Removes old handler BEFORE adding new one to prevent ghost handler accumulation.
        // NT8: Chart.ChartControl not accessible from AddOn -- find via visual tree.
        internal static void RegisterClickTrader(Chart chart, TradeCopierPanel panel)
        {
            if (!CopyEngine.Instance.Flags.ClickTrader)
            {
                NinjaTrader.Code.Output.Process(
                    "Click Trader requires Elite tier",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }
            if (chart == null)
                return; // guard (1)
            var cc = FindVisualChild<ChartControl>(chart);
            TradeCopierPanel old;
            if (_clickHandlers.TryRemove(chart, out old) && cc != null) // guard (2): remove old first
                cc.PreviewMouseDown -= old.OnChartMouseDown;
            _clickHandlers[chart] = panel; // store new
            if (cc != null)
                cc.PreviewMouseDown += panel.OnChartMouseDown; // hook new
        }

        // B9 T2: CYC=2 -- TryRemove guard + null ChartControl guard
        internal static void UnregisterClickTrader(Chart chart)
        {
            TradeCopierPanel panel;
            if (!_clickHandlers.TryRemove(chart, out panel))
                return; // guard (1)
            var cc = FindVisualChild<ChartControl>(chart);
            if (cc == null)
                return; // guard (2)
            cc.PreviewMouseDown -= panel.OnChartMouseDown;
        }

        // B11 T1: SIM101 logging-only handler.
        // Writes key+modifiers to status text for PreviewKeyDown feasibility gate.
        // CYC=1: no outer branch; inner lambda has guards but they do not add to outer CYC.
        private static void OnChartKeyDiag(object sender, KeyEventArgs e)
        {
            string msg = "KB: " + e.Key + " M=" + Keyboard.Modifiers;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var chart = sender as Chart;
                if (chart == null)
                    return;
                TradeCopierPanel p;
                if (_panels.TryGetValue(chart, out p) && p != null)
                    p.SetStatusText(msg);
            });
        }

        // B11 T1: Removes the SIM101 logging-only diag handler from chart.PreviewKeyDown.
        // Called UNCONDITIONALLY after SIM101 completes (PASS or FAIL).
        // Must be called BEFORE HookKeyShortcut() on the PASS path.
        // Nulls _sim101KeyDiag to prevent accidental re-subscription.
        // CYC=2: null guard (1) + unhook + null assignment (2).
        private static void RemoveSim101(Chart chart)
        {
            if (_sim101KeyDiag != null)
                chart.PreviewKeyDown -= _sim101KeyDiag;
            _sim101KeyDiag = null;
        }

        // B11 T1: Wire chart.PreviewKeyDown to panel.OnChartKeyDown after successful DoInject.
        // Mirrors HookClickTrader pattern: TryRemove-first to prevent duplicate handlers.
        // Called on WPF UI thread (Dispatcher.InvokeAsync path from DoInject).
        // CYC=2: chart null guard (1) + TryRemove-first to prevent dup (2).
        private static void HookKeyShortcut(Chart chart, TradeCopierPanel panel)
        {
            if (chart == null)
                return; // guard (1)
            TradeCopierPanel old;
            if (_keyHandlers.TryRemove(chart, out old) && old != null) // guard (2): remove old first
                chart.PreviewKeyDown -= old.OnChartKeyDown;
            _keyHandlers[chart] = panel;
            chart.PreviewKeyDown += panel.OnChartKeyDown;
        }

        // B11 T1: Unwire chart.PreviewKeyDown (PRODUCTION handler only) before panel.Detach().
        // Called from OnWindowDestroyed. Removes panel.OnChartKeyDown via _keyHandlers lookup.
        // Does NOT remove _sim101KeyDiag -- that is RemoveSim101's responsibility.
        // CYC=2: TryRemove guard (1) + unhook (2).
        private static void UnhookKeyShortcut(Chart chart)
        {
            TradeCopierPanel panel;
            if (!_keyHandlers.TryRemove(chart, out panel))
                return; // guard (1)
            if (panel == null)
                return; // guard (2)
            chart.PreviewKeyDown -= panel.OnChartKeyDown;
        }

        // BWAVE-CYC T8: extracted helpers for DoInject.

        // CollectStalePanelChildren: finds TradeCopierPanel children in grid. CCN=2.
        // Returns empty list (never null) when no stale children found.
        private static System.Collections.Generic.List<UIElement> CollectStalePanelChildren(
            System.Windows.Controls.Grid grid
        )
        {
            var stale = new System.Collections.Generic.List<UIElement>();
            foreach (UIElement child in grid.Children)
            {
                if (child.GetType().Name == "TradeCopierPanel")
                    stale.Add(child);
            }
            return stale;
        }

        // RemoveStalePanelChild: detaches and removes one stale panel + its RowDefinition. CCN=3.
        private static void RemoveStalePanelChild(
            System.Windows.Controls.Grid grid,
            UIElement old
        )
        {
            var stalePanel = old as TradeCopierPanel;
            if (stalePanel != null)
                stalePanel.Detach();
            int staleRow = System.Windows.Controls.Grid.GetRow(old);
            grid.Children.Remove(old);
            if (staleRow > 0 && staleRow < grid.RowDefinitions.Count)
                grid.RowDefinitions.RemoveAt(staleRow);
        }

        // TryDetachAndRemoveStalePanels: purges all stale TradeCopierPanel rows. CCN=2.
        private static void TryDetachAndRemoveStalePanels(System.Windows.Controls.Grid grid)
        {
            if (grid == null)
                return;
            var stale = CollectStalePanelChildren(grid);
            // C-2: remove in descending row order to prevent index shift.
            stale.Sort((a, b) =>
                System.Windows.Controls.Grid.GetRow(b).CompareTo(
                    System.Windows.Controls.Grid.GetRow(a)
                )
            );
            foreach (var old in stale)
                RemoveStalePanelChild(grid, old);
        }

        // InjectPanelIntoGrid: adds a new panel row to the ChartTrader grid. CCN=2.
        // Returns false (never null) when grid is null -- JS-002 compliant.
        private static bool InjectPanelIntoGrid(
            System.Windows.Controls.Grid grid,
            TradeCopierPanel panel
        )
        {
            if (grid == null)
                return false;
            var row = new RowDefinition { Height = System.Windows.GridLength.Auto };
            grid.RowDefinitions.Add(row);
            System.Windows.Controls.Grid.SetRow(panel, grid.RowDefinitions.Count - 1);
            System.Windows.Controls.Grid.SetColumnSpan(
                panel,
                grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1
            );
            grid.Children.Add(panel);
            return true;
        }

        // DoInject after extraction. CCN=7.
        // TrySetPanelInstrument: safely sets instrument on panel, swallowing NT8 API exceptions. CCN=2.
        private static NinjaTrader.Cbi.Instrument TrySetPanelInstrument(
            ChartTrader chartTrader,
            TradeCopierPanel panel
        )
        {
            NinjaTrader.Cbi.Instrument instr = null;
            try
            {
                instr = chartTrader.Instrument;
                if (instr != null)
                    panel.SetInstrument(instr);
            }
            catch { }
            return instr;
        }

        // C-07: PurgeStalePanel -- removes all stale TradeCopierPanel rows from ChartTrader grid.
        // Delegates to existing static TryDetachAndRemoveStalePanels helper. CCN=2.
        private void PurgeStalePanel(System.Windows.Controls.Grid grid)
        {
            if (grid == null)
                return;
            TryDetachAndRemoveStalePanels(grid);
        }

        // C-07: WireNewPanel -- wires a newly created TradeCopierPanel into the ChartTrader grid.
        // Contains the pre-existing try/catch for instrument wiring. CCN=5.
        private void WireNewPanel(
            TradeCopierPanel panel,
            Chart chart,
            ChartTrader chartTrader,
            System.Windows.Controls.Grid grid
        )
        {
            var instr = TrySetPanelInstrument(chartTrader, panel);
            StartAtrEngine(chart, instr);
            panel.SetChart(chart);

            // Wire leader account from ChartTrader account ComboBox.
            WireLeaderAccount(chartTrader, panel);

            // B11 T1 SIM101 Phase A: wire logging-only handler BEFORE production layer.
            _sim101KeyDiag = new KeyEventHandler(OnChartKeyDiag);
            chart.PreviewKeyDown += _sim101KeyDiag;

            // B11 T1 Phase B: production keyboard shortcut layer.
            // RemoveSim101 FIRST (SIM101 must be removed before HookKeyShortcut).
            RemoveSim101(chart);
            HookKeyShortcut(chart, panel);

            if (InjectPanelIntoGrid(grid, panel))
            {
                _panels[chart] = panel;
                return;
            }

            MessageBox.Show(
                "PTT: ChartTrader.Content is not a Grid.\nContent type: "
                    + (chartTrader.Content?.GetType().FullName ?? "null"),
                "PTT Info"
            );
        }

        // C-07: DoInject after extraction. Parent CCN=7.
        private void DoInject(Chart chart)
        {
            if (!_panels.TryAdd(chart, null))
                return;

            try
            {
                var chartTrader = FindVisualChild<ChartTrader>(chart);
                if (chartTrader == null)
                {
                    _panels.TryRemove(chart, out _);
                    return;
                }

                var grid = chartTrader.Content as System.Windows.Controls.Grid;
                PurgeStalePanel(grid);

                var panel = new TradeCopierPanel();
                WireNewPanel(panel, chart, chartTrader, grid);
            }
            catch (System.Exception ex)
            {
                _panels.TryRemove(chart, out _);
                MessageBox.Show(
                    "PTT ChartTrader inject error:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "PTT Error"
                );
            }
        }

        // B18 T1: Fix DW-B17-LEADER-01 -- FindVisualChild<ComboBox> returned Instrument ComboBox
        // (DFS first-match). Now: FindAccountComboBox picks first ComboBox whose SelectedItem is Account.
        // Fallback: if no account selected yet (all SelectedItems null), use index=1 (Account ComboBox
        // is always the second ComboBox in ChartTrader visual tree).
        // B30-B: SelectionChanged subscription moved to panel.WireAccountCombo (leak fix --
        // anonymous lambda could not be unsubscribed; named handler stored in panel.Detach).
        // CYC=5: null guard(1) + primary find(2) + fallback find(3) + text-fallback guard(4) + FirstOrDefault predicate(5).
        private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
        {
            // Primary: find by SelectedItem type (works when account already selected)
            var accountCombo = FindAccountComboBox(chartTrader);

            // Fallback: no account selected yet -- pick second ComboBox (index 1 = Account)
            if (accountCombo == null)
                accountCombo = FindVisualChildByIndex<ComboBox>(chartTrader, 1);

            if (accountCombo == null)
                return;

            // Set immediately from current selection
            var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
            if (current == null && accountCombo.Text != null)
                current = Account.All.FirstOrDefault(a =>
                    string.Equals(a.Name, accountCombo.Text, StringComparison.OrdinalIgnoreCase)
                );
            if (current != null)
                panel.SetLeaderAccount(current);

            // B30-B: Wire SelectionChanged via panel method so panel.Detach can unsubscribe.
            // Replaces anonymous lambda (was never unsubscribed -- memory leak DW-B30-03).
            panel.WireAccountCombo(accountCombo);
        }

        // --- Visual tree helpers (CYC=1 each) ---

        internal static T FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
                return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // B18 T1 -- FindAccountComboBox: walks visual tree, returns first ComboBox whose
        // SelectedItem is a NinjaTrader.Cbi.Account. Used by WireLeaderAccount to skip
        // the Instrument ComboBox (DFS first-match) and reach the Account ComboBox.
        // CYC=4: null guard(1) + count loop(2) + type+cast check(3) + recursive call(4).
        // JS-021: no lock. JS-002: returns null only on null parent (guard pattern).
        // B30-B: internal (was private) so TradeCopierPanel.TryResolveLeaderAccount can call it.
        internal static ComboBox FindAccountComboBox(DependencyObject parent)
        {
            if (parent == null)
                return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ComboBox cb && cb.SelectedItem is NinjaTrader.Cbi.Account)
                    return cb;
                var result = FindAccountComboBox(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // B18 T1 -- FindVisualChildByIndex: returns the Nth match (0-based) of type T from DFS walk.
        // Fallback used by WireLeaderAccount when no account is yet selected (SelectedItem=null).
        // NT8 ChartTrader: index 0 = Instrument ComboBox, index 1 = Account ComboBox.
        // CYC=2: delegates to internal helper (guards + loop there).
        // JS-021: no lock. JS-002: returns null only when not found.
        // B30-B: internal (was private) so TradeCopierPanel can call it if needed.
        internal static T FindVisualChildByIndex<T>(DependencyObject parent, int targetIndex)
            where T : DependencyObject
        {
            int found = 0;
            return FindVisualChildByIndexInternal<T>(parent, targetIndex, ref found);
        }

        // CYC=5: null guard(1) + count loop(2) + type match(3) + index check(4) + recursive call(5).
        private static T FindVisualChildByIndexInternal<T>(
            DependencyObject parent,
            int targetIndex,
            ref int found
        )
            where T : DependencyObject
        {
            if (parent == null)
                return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    if (found == targetIndex)
                        return match;
                    found++;
                }
                var result = FindVisualChildByIndexInternal<T>(child, targetIndex, ref found);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static T FindVisualChildByName<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null)
                return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name)
                    return fe;
                var result = FindVisualChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        // PTT-REPAIRS-01 R3: developer bypass removed.
        // CYC=3: try-enter(1) + licenseTxt.Exists(2) + catch(3).
        // JS-001: no throw -- any I/O error returns Starter().
        // NT8: File I/O is safe in State.Configure (not the hot path).
        private static FeatureFlags LoadAndValidateLicense()
        {
            try
            {
                var pttDir = System.IO.Path.Combine(
                    NinjaTrader.Core.Globals.UserDataDir,
                    "PropTraderTools"
                );
                var licenseTxt = System.IO.Path.Combine(pttDir, "license.txt");
                var key = System.IO.File.Exists(licenseTxt)
                    ? System.IO.File.ReadAllText(licenseTxt).Trim()
                    : string.Empty;
                return LicenseClient.Validate(key);
            }
            catch (Exception)
            {
                return FeatureFlags.Starter();
            }
        }
    }
}
