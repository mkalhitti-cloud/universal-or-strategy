// PTT-COPIER-B11-T2 -- TradeCopierWindow.cs
// B11 T2 CHANGES:
//   1. Added _armBeBtns List<Button> field (DW-B10-03).
//   2. OnRuleArmBe(): Arm BE click handler for rule rows. CYC=4.
//   3. BuildRuleRow(): added Col 11 -- [Arm BE] button + buffer TextBox + "tks" label.
//   4. BuildDynamicRuleRow(): added Col 11 -- same cluster as BuildRuleRow.
// PTT-COPIER-B10-T3 -- TradeCopierWindow.cs
// Plain WPF Window Add-On surface. Rule management, status log, global on/off.
// FIX: Account.All removed from constructor/BuildUI -- only bound in Loaded handler.
// FIX: Shift+B KeyBinding removed -- WPF KeyGesture rejects Shift+letter in NT8 host.
// All order submission routes through CopyEngine. No order calls in this file.
// Jane Street rules: JS-021 (no lock), JS-023 (volatile via engine), SCAN-01..07
// B7-F1: Semantic button color coding (Layer 2 + Layer 3 live state via PositionStateChanged).
// B7-F5: ScrollViewer wrapping _rulesPanel (MaxHeight=400).
// V08: canonical RGB per PTT_DESIGN_PILLAR. MakeWinBrush(r,g,b) -- no hex literals.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
{
    public class TradeCopierWindow : Window
    {
        private CopyEngine _engine;
        private Button _globalToggleBtn;
        private StackPanel _logPanel;
        private StackPanel _rulesPanel;
        private bool _copyEnabled;
        private const int MaxLogLines = 50;

        // All rule-row account controls collected here so Loaded can bind Account.All
        private readonly List<ComboBox> _leaderBoxes = new List<ComboBox>();
        private readonly List<ListBox> _followerBoxes = new List<ListBox>();

        // Per-rule button tracking for UpdateButtonColors iteration (Engineer Note #3)
        // Precedent: _leaderBoxes / _followerBoxes (existing pattern in this file)
        // Accessed exclusively on UI thread -- no locking required (JS-021)
        private readonly List<Button> _flattenBtns = new List<Button>();
        private readonly List<Button> _cancelBtns = new List<Button>();
        private readonly List<Button> _trimBtns = new List<Button>();
        private readonly List<Button> _beBtns = new List<Button>();

        // B10 T3: tighten stop button tracking -- not position-state-colored; tracked for cleanup.
        private readonly List<Button> _tightenBtns = new List<Button>();

        // B11 T2: Arm BE button tracking (DW-B10-03) -- accessed exclusively on UI thread (JS-021 compliant).
        private readonly List<Button> _armBeBtns = new List<Button>();

        // BGTM-1: Add Rule button field (promoted from local so ApplyFeatureFlags can gate it)
        private Button _addRuleBtn;

        // BGTM-1: Copy mode ComboBox field (promoted from local so ApplyFeatureFlags can gate Mirror)
        private ComboBox _modeCb;

        // BGTM-1: License UI controls
        private System.Windows.Controls.TextBox _licenseKeyBox;
        private System.Windows.Controls.TextBlock _licenseStatusText;
        private System.Windows.Controls.Button _activateBtn;

        private static readonly string LicenseTxtPath = System.IO.Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir,
            "PropTraderTools",
            "license.txt"
        );

        // -- frozen semantic brushes (JS-008: MakeWinBrush calls Freeze()) --------
        // "Win" prefix avoids collision with potential Window base-class members
        private static SolidColorBrush MakeWinBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Canonical semantic brushes (V08: corrected RGB per PTT_DESIGN_PILLAR lines 192-198)
        private static readonly SolidColorBrush WBrushActive = MakeWinBrush(34, 197, 94); // green  #22c55e
        private static readonly SolidColorBrush WBrushDanger = MakeWinBrush(239, 68, 68); // red    #ef4444
        private static readonly SolidColorBrush WBrushCaution = MakeWinBrush(245, 158, 11); // amber  #f59e0b
        private static readonly SolidColorBrush WBrushInactive = MakeWinBrush(55, 65, 81); // grey   #4b5563

        public TradeCopierWindow()
        {
            Title = "Trade Copier";
            Width = 720;
            Height = 520;
            MinWidth = 540;
            MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            _engine = CopyEngine.Instance;

            try
            {
                BuildUI();
            }
            catch (Exception ex)
            {
                // BuildUI must never throw -- if it does surface it immediately
                MessageBox.Show(
                    "PTT BuildUI error:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "Trade Copier"
                );
                return;
            }

            Loaded += OnLoaded;
            Closed += OnWindowClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Bind Account.All now -- NT8 guarantees accounts are populated by Loaded
            try
            {
                foreach (var cb in _leaderBoxes)
                    cb.ItemsSource = Account.All;
                foreach (var lb in _followerBoxes)
                    lb.ItemsSource = Account.All;
            }
            catch (Exception ex)
            {
                MessageBox.Show("PTT account bind error:\n\n" + ex.Message, "Trade Copier");
            }

            try
            {
                _engine.StatusUpdate -= OnStatusUpdate;
                _engine.PositionStateChanged -= OnPositionStateChanged;
                _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
                _engine.StatusUpdate += OnStatusUpdate;
                _engine.PositionStateChanged += OnPositionStateChanged;
                _engine.CopyEnabledChanged += OnCopyEnabledChanged;
                CopyEngine.Instance.LoadRules();
                RefreshRuleRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "PTT init error:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "Trade Copier"
                );
            }

            // BGTM-1: subscribe to flag changes, apply current flags, populate key display
            CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
            ApplyFeatureFlags(CopyEngine.Instance.Flags);
            LoadLicenseKeyDisplay();
        }

        // B56-LaneB: CYC=3 -- rebuild rule rows from saved engine state after LoadRules.
        // JS-021: no lock. JS-033: private void (not async void). Dispatcher.InvokeAsync inside.
        // JS-002: guard against empty instruments (keeps default MES row).
        // NT8-006: NO System.Linq -- ToList() banned. Manual foreach into List<string>.
        private void RefreshRuleRows()
        {
            var instruments = new System.Collections.Generic.List<string>();
            foreach (var instr in CopyEngine.Instance.GetRuleInstruments())
                instruments.Add(instr);
            if (instruments.Count == 0)
                return; // CYC branch (1): no saved rules -- keep default MES row
            Dispatcher.InvokeAsync(() =>
            {
                _rulesPanel.Children.Clear();
                foreach (var instr in instruments) // CYC branch (2): iterate instruments
                    _rulesPanel.Children.Add(BuildRuleRow(instr));
                ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built
            });
        }

        // V04: unsubscribe PositionStateChanged on close to prevent ghost callbacks / memory leaks
        private void OnWindowClosed(object sender, EventArgs e)
        {
            _engine.PositionStateChanged -= OnPositionStateChanged;
            _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
            // BGTM-1: unsubscribe feature flag listener
            CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                CopyEngine.Instance.SaveRules();
            }
            catch { }
            _engine.StatusUpdate -= OnStatusUpdate;
            _engine.PositionStateChanged -= OnPositionStateChanged;
            _engine.CopyEnabledChanged -= OnCopyEnabledChanged;
            base.OnClosed(e);
        }

        // -- Layer 3 live state (V04) -- called on UI thread only -----------------
        // CYC=5: global toggle + 4 foreach iterations (one branch each).
        // Must run on UI thread -- always invoked via Dispatcher.InvokeAsync from OnPositionStateChanged.
        private void UpdateButtonColors(bool hasPosition, bool hasEntries)
        {
            _globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive;
            foreach (var btn in _flattenBtns)
                btn.Background = hasPosition ? WBrushDanger : WBrushInactive;
            foreach (var btn in _cancelBtns)
                btn.Background = hasEntries ? WBrushDanger : WBrushInactive;
            foreach (var btn in _trimBtns)
                btn.Background = hasPosition ? WBrushCaution : WBrushInactive;
            foreach (var btn in _beBtns)
                btn.Background = hasPosition ? WBrushActive : WBrushInactive;
        }

        // CYC=1: single null guard -- Window shows all rules, no per-instrument filter.
        // JS-023: marshals onto UI thread via Dispatcher.InvokeAsync.
        // JS-003: PositionState is a readonly struct -- captured by value in closure.
        private void OnPositionStateChanged(string instr, PositionState state)
        {
            if (instr == null)
                return;
            Dispatcher.InvokeAsync(() =>
                UpdateButtonColors(state.HasOpenPosition, state.HasWorkingEntries)
            );
        }

        // C-09: BuildUI extracted into 6 private helpers. CYC=1. JS-021/001/002/033 compliant.
        private void BuildUI()
        {
            var root = new DockPanel { LastChildFill = true };
            var titleBlock = BuildWindowTitleBlock();
            DockPanel.SetDock(titleBlock, Dock.Top);
            root.Children.Add(titleBlock);
            var toggleBtn = BuildGlobalToggleButton();
            DockPanel.SetDock(toggleBtn, Dock.Top);
            root.Children.Add(toggleBtn);
            var modeSection = BuildCopyModeSection();
            DockPanel.SetDock(modeSection, Dock.Top);
            root.Children.Add(modeSection);
            var sep1 = new Separator { Margin = new Thickness(0, 2, 0, 2) };
            DockPanel.SetDock(sep1, Dock.Top);
            root.Children.Add(sep1);
            var rulesScroll = BuildRulesScrollSection();
            DockPanel.SetDock(rulesScroll, Dock.Top);
            root.Children.Add(rulesScroll);
            var addRuleBtn = BuildAddRuleButton();
            DockPanel.SetDock(addRuleBtn, Dock.Top);
            root.Children.Add(addRuleBtn);
            var sep2 = new Separator { Margin = new Thickness(0, 2, 0, 2) };
            DockPanel.SetDock(sep2, Dock.Top);
            root.Children.Add(sep2);
            BuildLicenseRow(root);
            root.Children.Add(BuildLogScrollSection());
            Content = root;
            UpdateButtonColors(false, false);
        }

        // C-09: Creates bold title TextBlock. CYC=1. JS-002: no return null. ASCII-only.
        private TextBlock BuildWindowTitleBlock()
        {
            return new TextBlock
            {
                Text = "Prop Trader Tools -- Trade Copier",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 4, 4, 2),
            };
        }

        // C-09: Creates global copy toggle Button, assigns _globalToggleBtn. CYC=1. JS-002: no return null.
        private Button BuildGlobalToggleButton()
        {
            _globalToggleBtn = new Button
            {
                Content = "Copy All OFF",
                Margin = new Thickness(6, 2, 6, 2),
                Padding = new Thickness(8, 3, 8, 3),
                Background = WBrushInactive,
            };
            _globalToggleBtn.Click += OnGlobalToggle;
            return _globalToggleBtn;
        }

        // C-09: Creates Copy Mode label + ComboBox row. CYC=1. JS-002: no return null. ASCII-only.
        private StackPanel BuildCopyModeSection()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(6, 2, 6, 2),
            };
            var modeLabel = new Label
            {
                Content = "Copy Mode:",
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 0, 4, 0),
            };
            _modeCb = new ComboBox { Width = 120, VerticalAlignment = VerticalAlignment.Center };
            _modeCb.Items.Add("Signal (default)");
            _modeCb.Items.Add("Mirror");
            _modeCb.Items.Add("Clone");
            _modeCb.SelectedIndex = 0;
            _modeCb.SelectionChanged += OnCopyModeComboChanged;
            row.Children.Add(modeLabel);
            row.Children.Add(_modeCb);
            return row;
        }

        // C-09: Creates _rulesPanel StackPanel wrapped in ScrollViewer. CYC=1. JS-002: no return null.
        // MaxHeight=400 per B7-F5 spec.
        private ScrollViewer BuildRulesScrollSection()
        {
            _rulesPanel = new StackPanel();
            _rulesPanel.Children.Add(BuildRuleRow("MES"));
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400,
                Content = _rulesPanel,
            };
        }

        // C-09: Creates "+ Add Rule" Button, assigns _addRuleBtn. CYC=1. JS-002: no return null.
        private Button BuildAddRuleButton()
        {
            _addRuleBtn = new Button
            {
                Content = "+ Add Rule",
                Margin = new Thickness(6, 2, 6, 2),
                Padding = new Thickness(8, 3, 8, 3),
            };
            _addRuleBtn.Click += OnAddRule;
            return _addRuleBtn;
        }

        // C-09: Creates _logPanel StackPanel wrapped in ScrollViewer. CYC=1. JS-002: no return null. ASCII-only.
        private ScrollViewer BuildLogScrollSection()
        {
            _logPanel = new StackPanel { Orientation = Orientation.Vertical };
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _logPanel,
                Margin = new Thickness(4),
            };
        }

        // BGTM-1: Builds the license key input row and appends to parent DockPanel. CYC=1.
        // JS-001: no throw. JS-021: no lock. No hex colors (MakeWinBrush not needed -- plain controls).
        // No FontFamily. ASCII-only strings.
        private void BuildLicenseRow(System.Windows.Controls.Panel parent)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(6, 4, 6, 4),
            };

            var label = new Label
            {
                Content = "LICENSE",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 70,
            };

            _licenseKeyBox = new TextBox
            {
                Width = 200,
                Margin = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            _activateBtn = new Button
            {
                Content = "Activate",
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
            };
            _activateBtn.Click += OnActivateClick;

            _licenseStatusText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 0),
            };

            row.Children.Add(label);
            row.Children.Add(_licenseKeyBox);
            row.Children.Add(_activateBtn);
            row.Children.Add(_licenseStatusText);

            DockPanel.SetDock(row, Dock.Top);
            parent.Children.Add(row);
        }

        // BGTM-1: Activate button click -- validate license and apply flags. CYC=1. JS-001: no throw.
        private void OnActivateClick(object sender, RoutedEventArgs e)
        {
            string key = _licenseKeyBox?.Text?.Trim() ?? string.Empty;
            try
            {
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(LicenseTxtPath)
                );
                System.IO.File.WriteAllText(LicenseTxtPath, key);
            }
            catch (Exception) { }
            var flags = LicenseClient.Validate(key);
            CopyEngine.Instance.SetFlags(flags);
            ApplyFeatureFlags(flags);
            _licenseStatusText.Text = GetStatusText(flags);
        }

        // BWAVE-CYC T7: extracted helper for TradeCopierWindow::ApplyFeatureFlags.

        // ApplyButtonGroupFlag: sets IsEnabled and ToolTip on a collection of buttons. CCN=2.
        private static void ApplyButtonGroupFlag(
            System.Collections.Generic.IEnumerable<System.Windows.Controls.Button> btns,
            bool enabled,
            string disabledMessage
        )
        {
            foreach (var btn in btns)
            {
                btn.IsEnabled = enabled;
                btn.ToolTip = enabled ? null : disabledMessage;
            }
        }

        // BGTM-1: Apply feature flags to all gated UI elements.
        // TradeCopierWindow::ApplyFeatureFlags after extraction. CCN=5.
        private void ApplyFeatureFlags(FeatureFlags f)
        {
            ApplyButtonGroupFlag(_trimBtns, f.TrimFlatten, "Trim requires Pro tier");
            ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            ApplyButtonGroupFlag(_cancelBtns, f.TrimFlatten, "Cancel requires Pro tier");
            ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
            ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, "Arm Break-Even not available on this plan");
            ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, "Tighten Stop not available on this plan");
            if (_modeCb != null)
            {
                _modeCb.IsEnabled = f.MirrorMode;
                _modeCb.ToolTip = f.MirrorMode ? null : "Mirror mode requires Elite tier";
            }
            if (_addRuleBtn != null)
            {
                _addRuleBtn.IsEnabled = f.MultiRule;
                _addRuleBtn.ToolTip = f.MultiRule ? null : "Multi-rule requires Pro tier";
            }
        }

        // BGTM-1: Populate license key box from file on window load. CYC=2.
        private void LoadLicenseKeyDisplay()
        {
            try
            {
                _licenseKeyBox.Text = System.IO.File.Exists(LicenseTxtPath)
                    ? System.IO.File.ReadAllText(LicenseTxtPath).Trim()
                    : string.Empty;
            }
            catch (Exception)
            {
                _licenseKeyBox.Text = string.Empty;
            }
            _licenseStatusText.Text = GetStatusText(CopyEngine.Instance.Flags);
        }

        // BGTM-1: Handle CopyEngine.FeatureFlagsChanged -- always on UI thread (per architecture plan). CYC=1.
        private void OnFeatureFlagsChanged(FeatureFlags f)
        {
            ApplyFeatureFlags(f);
            _licenseStatusText.Text = GetStatusText(f);
        }

        // BGTM-1: Return tier name string for license status display. CYC=3.
        private static string GetStatusText(FeatureFlags f)
        {
            if (f.AtrSizing)
                return "ELITE";
            if (f.MultiRule)
                return "PRO";
            return "STARTER";
        }

        // BWAVE-CYC R1: BuildRuleRow refactored to use shared helpers. LoC before=202 after=36.
        // CYC=1 (straight-line construction; no branches in parent).
        private Grid BuildRuleRow(string instrumentName)
        {
            var grid = new Grid { Margin = new Thickness(2) };
            BuildGridColumnDefinitions(grid, false);

            // Col 0: fixed instrument label
            var instrLabel = new TextBlock
            {
                Text = instrumentName,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
            };
            Grid.SetColumn(instrLabel, 0);
            grid.Children.Add(instrLabel);

            // Col 1: leader ComboBox -- ItemsSource set in Loaded
            var leaderCb = new ComboBox { Margin = new Thickness(2) };
            _leaderBoxes.Add(leaderCb);
            leaderCb.ItemTemplate = BuildAccountDisplayTemplate();
            Grid.SetColumn(leaderCb, 1);
            grid.Children.Add(leaderCb);

            // Col 2: follower ListBox -- ItemsSource set in Loaded
            var followerLb = BuildFollowerListBox();
            _followerBoxes.Add(followerLb);
            Grid.SetColumn(followerLb, 2);
            grid.Children.Add(followerLb);

            var atmPanel = BuildAtmColumnPanel();
            BuildActionButtons(instrumentName, leaderCb, followerLb, atmPanel, grid);

            var beCluster = BuildBeCluster(instrumentName);
            Grid.SetColumn(beCluster, 8);
            grid.Children.Add(beCluster);

            Grid.SetColumn(atmPanel, 9);
            grid.Children.Add(atmPanel);

            var tightenCluster = BuildTightenCluster(instrumentName);
            Grid.SetColumn(tightenCluster, 10);
            grid.Children.Add(tightenCluster);

            var armBeCluster = BuildArmBeCluster(instrumentName, leaderCb);
            Grid.SetColumn(armBeCluster, 11);
            grid.Children.Add(armBeCluster);

            return grid;
        }

        // BWAVE-CYC R1: BuildDynamicRuleRow refactored to use shared helpers. LoC before=210 after=28.
        // CYC=1 (straight-line construction; no branches in parent).
        private Grid BuildDynamicRuleRow()
        {
            var grid = new Grid { Margin = new Thickness(2) };
            BuildGridColumnDefinitions(grid, true);

            // Col 0: editable instrument TextBox
            var instrTextBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
                MinWidth = 45,
            };
            Grid.SetColumn(instrTextBox, 0);
            grid.Children.Add(instrTextBox);

            // Col 1: leader ComboBox -- ItemsSource bound immediately (window already loaded)
            var leaderCb = new ComboBox { ItemsSource = Account.All, Margin = new Thickness(2) };
            leaderCb.ItemTemplate = BuildAccountDisplayTemplate();
            Grid.SetColumn(leaderCb, 1);
            grid.Children.Add(leaderCb);

            // Col 2: follower ListBox -- bound immediately
            var followerLb = BuildFollowerListBox();
            followerLb.ItemsSource = Account.All;
            Grid.SetColumn(followerLb, 2);
            grid.Children.Add(followerLb);

            var atmPanel = BuildAtmColumnPanel();
            BuildActionButtons(instrTextBox, leaderCb, followerLb, atmPanel, grid);

            var beCluster = BuildBeCluster(instrTextBox);
            Grid.SetColumn(beCluster, 8);
            grid.Children.Add(beCluster);

            Grid.SetColumn(atmPanel, 9);
            grid.Children.Add(atmPanel);

            var tightenCluster = BuildTightenCluster(instrTextBox);
            Grid.SetColumn(tightenCluster, 10);
            grid.Children.Add(tightenCluster);

            var armBeCluster = BuildArmBeCluster(instrTextBox, leaderCb);
            Grid.SetColumn(armBeCluster, 11);
            grid.Children.Add(armBeCluster);

            return grid;
        }

        // BWAVE-CYC R1: 6 shared private helpers extracted from BuildRuleRow / BuildDynamicRuleRow.
        // All helpers: private instance, UI-thread only, CYC <= 2, no lock(), no async void, no return null.

        // CCN=2: branch on dynamicFirstCol for col-0 width.
        private static void BuildGridColumnDefinitions(Grid grid, bool dynamicFirstCol)
        {
            var col0Width = dynamicFirstCol
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(45);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = col0Width });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B8 T2: ATM ComboBox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B10 T3: Tighten cluster
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B11 T2: Arm BE cluster
        }

        // CCN=1: straight-line follower ListBox construction shared by both row builders.
        // B18 T2: outer ScrollViewer removed; Height=100 fixed. NT8 WPF host suppresses
        // ListBox internal scrollbar by default -- disable virtualization + force scrollbar Visible.
        private static ListBox BuildFollowerListBox()
        {
            var lb = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                Height = 100,
                Margin = new Thickness(2),
            };
            VirtualizingStackPanel.SetIsVirtualizing(lb, false);
            ScrollViewer.SetVerticalScrollBarVisibility(lb, ScrollBarVisibility.Visible);
            lb.ItemTemplate = BuildAccountDisplayTemplate();
            return lb;
        }

        // CCN=1: Break Even cluster ([BE] button + TextBox + "tks" label).
        // tag0 = instrumentName (string) for static rows, instrTextBox for dynamic rows.
        // Adds beBtn to _beBtns for UpdateButtonColors iteration.
        private StackPanel BuildBeCluster(object tag0)
        {
            var cluster = new StackPanel { Orientation = Orientation.Horizontal };
            var beBox = new TextBox
            {
                Text = "2",
                Width = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
            };
            var beBtn = new Button
            {
                Content = "[BE]",
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            var tksLabel = new TextBlock
            {
                Text = "tks",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 2, 0),
            };
            beBtn.Tag = new object[] { tag0, beBox };
            beBtn.Click += OnRuleBreakEven;
            _beBtns.Add(beBtn);
            cluster.Children.Add(beBtn);
            cluster.Children.Add(beBox);
            cluster.Children.Add(tksLabel);
            return cluster;
        }

        // CCN=1: Tighten Stop cluster ([~] button + TextBox + "tks" label).
        // tag0 = instrumentName (string) or instrTextBox. Adds tightenBtn to _tightenBtns.
        private StackPanel BuildTightenCluster(object tag0)
        {
            var cluster = new StackPanel { Orientation = Orientation.Horizontal };
            var ticksBox = new TextBox
            {
                Text = "5",
                Width = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
            };
            var btn = new Button
            {
                Content = "[~]",
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            var tksLabel = new TextBlock
            {
                Text = "tks",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 2, 0),
            };
            btn.Tag = new object[] { tag0, ticksBox };
            btn.Click += OnRuleTightenStop;
            _tightenBtns.Add(btn);
            cluster.Children.Add(btn);
            cluster.Children.Add(ticksBox);
            cluster.Children.Add(tksLabel);
            return cluster;
        }

        // CCN=1: Arm BE cluster ([Arm BE] button + buffer TextBox + "tks" label).
        // tag0 = instrumentName (string) or instrTextBox. Adds armBeBtn to _armBeBtns.
        private StackPanel BuildArmBeCluster(object tag0, ComboBox leaderCb)
        {
            var cluster = new StackPanel { Orientation = Orientation.Horizontal };
            var armBeBox = new TextBox
            {
                Text = "2",
                Width = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
            };
            var btn = new Button
            {
                Content = "[Arm BE]",
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            var tksLabel = new TextBlock
            {
                Text = "tks",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 2, 0),
            };
            btn.Tag = new object[] { tag0, leaderCb, armBeBox };
            btn.Click += OnRuleArmBe;
            _armBeBtns.Add(btn);
            cluster.Children.Add(btn);
            cluster.Children.Add(armBeBox);
            cluster.Children.Add(tksLabel);
            return cluster;
        }

        // CCN=2: ATM ComboBox (Inherit/Market/Named) + namedBox TextBox + SelectionChanged lambda.
        // Branch: SelectionChanged lambda tests sel == "Named" (CCN +1 vs base 1).
        private static StackPanel BuildAtmColumnPanel()
        {
            var atmCb = new ComboBox { Width = 80, Margin = new Thickness(2) };
            atmCb.Items.Add("Inherit");
            atmCb.Items.Add("Market");
            atmCb.Items.Add("Named");
            atmCb.SelectedIndex = 0;
            var namedBox = new TextBox
            {
                Width = 80,
                Visibility = Visibility.Collapsed,
                ToolTip = "ATM template name",
                Margin = new Thickness(2),
            };
            atmCb.SelectionChanged += (s, e2) =>
            {
                var sel = (s as ComboBox)?.SelectedItem?.ToString() ?? string.Empty;
                namedBox.Visibility = sel == "Named" ? Visibility.Visible : Visibility.Collapsed;
                if (sel != "Named")
                    namedBox.Text = string.Empty;
            };
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(atmCb);
            panel.Children.Add(namedBox);
            return panel;
        }

        // CCN=1: Action buttons (Trim/Flatten/Cancel/Toggle/Apply) -- cols 3-7.
        // tag0 = instrumentName (string) or instrTextBox. Adds trim/flatten/cancel to tracking lists.
        // atmPanel: Children[0]=atmCb, Children[1]=namedBox -- passed to OnRowApply tag array.
        // Adds all 5 buttons to grid at their respective columns.
        // C-04: CCN=1 -- straight-line delegation to 5 private helpers.
        // All helpers: private instance, UI-thread only, CYC<=1, no lock(), no async void, no return null.
        private void BuildActionButtons(
            object tag0,
            ComboBox leaderCb,
            ListBox followerLb,
            StackPanel atmPanel,
            Grid grid)
        {
            var atmCb = (ComboBox)atmPanel.Children[0];
            var namedBox = (TextBox)atmPanel.Children[1];
            BuildTrimActionButton(tag0, grid);
            BuildFlattenActionButton(tag0, grid);
            BuildCancelActionButton(tag0, grid);
            BuildToggleActionButton(tag0, grid);
            BuildApplyActionButton(tag0, leaderCb, followerLb, atmCb, namedBox, grid);
        }

        // CCN=1: no branches.
        private void BuildTrimActionButton(object tag, Grid grid)
        {
            var btn = new Button
            {
                Content = "[1/2]",
                Tag = tag,
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            btn.Click += OnRuleTrim;
            _trimBtns.Add(btn);
            Grid.SetColumn(btn, 3);
            grid.Children.Add(btn);
        }

        // CCN=1: no branches.
        private void BuildFlattenActionButton(object tag, Grid grid)
        {
            var btn = new Button
            {
                Content = "[=]",
                Tag = tag,
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            btn.Click += OnRuleFlatten;
            _flattenBtns.Add(btn);
            Grid.SetColumn(btn, 4);
            grid.Children.Add(btn);
        }

        // CCN=1: no branches.
        private void BuildCancelActionButton(object tag, Grid grid)
        {
            var btn = new Button
            {
                Content = "[x]",
                Tag = tag,
                Margin = new Thickness(2),
                Background = WBrushInactive,
            };
            btn.Click += OnRuleCancel;
            _cancelBtns.Add(btn);
            Grid.SetColumn(btn, 5);
            grid.Children.Add(btn);
        }

        // CCN=1: no branches.
        private void BuildToggleActionButton(object tag, Grid grid)
        {
            var btn = new Button
            {
                Content = "[ON]",
                Tag = tag,
                Margin = new Thickness(2),
                Background = WBrushActive,
            };
            btn.Click += OnRuleToggle;
            Grid.SetColumn(btn, 6);
            grid.Children.Add(btn);
        }

        // CCN=1: no branches.
        private void BuildApplyActionButton(
            object tag,
            ComboBox leaderCb,
            ListBox followerLb,
            ComboBox atmCb,
            TextBox namedBox,
            Grid grid)
        {
            var btn = new Button { Content = "Apply", Margin = new Thickness(2) };
            btn.Tag = new object[] { tag, leaderCb, followerLb, atmCb, namedBox };
            btn.Click += OnRowApply;
            Grid.SetColumn(btn, 7);
            grid.Children.Add(btn);
        }

        // B56-LaneB: CYC=4 -- null guard (1) + 3-way if-chain for index 0/1/2 (branches 2/3/4)
        private void OnCopyModeComboChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null)
                return; // guard (1)
            if (cb.SelectedIndex == 1)
                CopyEngine.Instance.SetCopyMode(CopyMode.Mirror); // branch (2)
            else if (cb.SelectedIndex == 2)
                CopyEngine.Instance.SetCopyMode(CopyMode.Clone); // branch (3)
            else
                CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // branch (4)
        }

        private void OnGlobalToggle(object sender, RoutedEventArgs e)
        {
            _copyEnabled = !_copyEnabled;
            _engine.SetEnabled(_copyEnabled);
            _globalToggleBtn.Content = _copyEnabled ? "Copy All ON" : "Copy All OFF";
            _globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive;
        }

        // B20-LANE-C T3 -- OnCopyEnabledChanged: syncs Window copy state from engine event.
        // CYC=1: straight-line Dispatcher.InvokeAsync (constructor guarantee: _globalToggleBtn != null).
        // JS-021: no lock. JS-023: Dispatcher.InvokeAsync for UI thread marshaling.
        private void OnCopyEnabledChanged(bool enabled)
        {
            _copyEnabled = enabled;
            Dispatcher.InvokeAsync(() =>
            {
                _globalToggleBtn.Content = enabled ? "Copy All ON" : "Copy All OFF";
                _globalToggleBtn.Background = enabled ? WBrushActive : WBrushInactive;
            });
        }

        // B20-LANE-C T3 -- AccountDisplayConverter: strips !<broker-suffix> for display.
        // IValueConverter.Convert: "Acct!Apex!Apex" -> "Acct". CYC=1.
        // IValueConverter.ConvertBack: one-way binding only; never called by WPF.
        private sealed class AccountDisplayConverter : IValueConverter
        {
            public object Convert(
                object value,
                Type targetType,
                object parameter,
                CultureInfo culture
            )
            {
                return (value as string)?.Split('!')?[0] ?? value?.ToString() ?? string.Empty;
            }

            public object ConvertBack(
                object value,
                Type targetType,
                object parameter,
                CultureInfo culture
            )
            {
                throw new NotImplementedException("AccountDisplayConverter is one-way only");
            }
        }

        private static readonly AccountDisplayConverter _accountDisplayConverter =
            new AccountDisplayConverter();

        // B20-LANE-C T3 -- BuildAccountDisplayTemplate: builds the shared DataTemplate that
        // strips !<suffix> from Account.Name for display in ComboBox and ListBox items.
        // Uses FrameworkElementFactory (code-only WPF; no XAML in this codebase).
        // CYC=1: straight-line, no branches.
        // JS-021: no lock. JS-033: not async.
        private static DataTemplate BuildAccountDisplayTemplate()
        {
            var template = new DataTemplate(typeof(Account));
            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            var binding = new System.Windows.Data.Binding("Name")
            {
                Mode = System.Windows.Data.BindingMode.OneWay,
                Converter = _accountDisplayConverter,
            };
            tbFactory.SetBinding(TextBlock.TextProperty, binding);
            tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = tbFactory;
            return template;
        }

        // DW-C39-05: re-gate new row buttons immediately after adding the row.
        private void OnAddRule(object sender, RoutedEventArgs e)
        {
            _rulesPanel.Children.Add(BuildDynamicRuleRow());
            ApplyFeatureFlags(CopyEngine.Instance.Flags); // gate newly-added buttons
            CopyEngine.Instance.SaveRules();              // DW-C39-09: persist immediately
        }

        private void OnRuleTrim(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string name = btn?.Tag is TextBox tb ? tb.Text : btn?.Tag as string;
            var instr = FindInstrument(name);
            if (instr != null)
                _engine.Trim(instr);
        }

        private void OnRuleFlatten(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string name = btn?.Tag is TextBox tb ? tb.Text : btn?.Tag as string;
            var instr = FindInstrument(name);
            if (instr != null)
                _engine.Flatten(instr);
        }

        private void OnRuleCancel(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string name = btn?.Tag is TextBox tb ? tb.Text : btn?.Tag as string;
            var instr = FindInstrument(name);
            if (instr != null)
                _engine.CancelPendingEntries(instr);
        }

        private void OnRuleToggle(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            string name = btn.Tag is TextBox tb ? tb.Text : btn.Tag as string;
            bool newState = (string)btn.Content == "[ON]" ? false : true;
            btn.Content = newState ? "[ON]" : "[OFF]";
            btn.Background = newState ? WBrushActive : WBrushInactive;
            _engine.SetRuleEnabled(name, newState);
        }

        // BWAVE-CYC T6: extracted helpers for OnRuleBreakEven, OnRuleArmBe, OnRuleTightenStop.

        // TryParseBeTicksFromTag: parses BE ticks from tag[1] TextBox. Default=2. CCN=4.
        private static int TryParseBeTicksFromTag(object[] tag)
        {
            int ticks = 2;
            if (tag.Length > 1 && tag[1] is TextBox beBox)
                if (int.TryParse(beBox.Text?.Trim(), out int parsed) && parsed >= 0)
                    ticks = parsed;
            return ticks;
        }

        // OnRuleBreakEven after extraction. CCN=5.
        private void OnRuleBreakEven(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as object[];
            if (tag == null)
                return;
            string name = tag[0] is TextBox tb ? tb.Text : tag[0] as string;
            if (string.IsNullOrEmpty(name))
                return;
            int ticks = TryParseBeTicksFromTag(tag);
            var instr = FindInstrument(name);
            if (instr != null)
                _engine.BreakEven(instr, ticks);
        }

        // TryParseArmBeBuffer: parses buffer ticks from tag[2] TextBox. Default=2. CCN=2.
        private static int TryParseArmBeBuffer(object[] tag)
        {
            int buf = 2;
            var bufBox = tag.Length > 2 ? tag[2] as TextBox : null;
            if (bufBox != null)
                int.TryParse(bufBox.Text, out buf);
            return buf;
        }

        // OnRuleArmBe after extraction. CCN=5.
        // TryGetLeaderFromTag: extracts leader Account from tag[1] ComboBox. CCN=2.
        private static Account TryGetLeaderFromTag(object[] tag)
        {
            var leaderCb = tag.Length > 1 ? tag[1] as ComboBox : null;
            return leaderCb?.SelectedItem as Account;
        }

        // OnRuleArmBe after extraction. CCN=7.
        private void OnRuleArmBe(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as object[];
            if (tag == null)
                return;
            string name = ExtractNameFromTag(tag);
            if (string.IsNullOrEmpty(name))
                return;
            var instr = FindInstrument(name);
            if (instr == null)
                return;
            var leaderAcc = TryGetLeaderFromTag(tag);
            if (leaderAcc == null)
                return;
            int buf = TryParseArmBeBuffer(tag);
            _engine.ArmPendingBe(instr, leaderAcc, buf);
        }

        // TryParseTightenTicksFromTag: parses ticks from tag[1] TextBox. Default=5, clamped 1-500. CCN=3.
        private static int TryParseTightenTicksFromTag(object[] tag)
        {
            int ticks = 5;
            if (tag.Length > 1 && tag[1] is TextBox ticksBox)
                if (int.TryParse(ticksBox.Text?.Trim(), out int parsed))
                    ticks = Math.Max(1, Math.Min(500, parsed));
            return ticks;
        }

        // OnRuleTightenStop after extraction. CCN=5.
        private void OnRuleTightenStop(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as object[];
            if (tag == null)
                return;
            string name = tag[0] is TextBox tb0 ? tb0.Text : tag[0] as string;
            if (string.IsNullOrEmpty(name))
                return;
            var instr = FindInstrument(name);
            if (instr == null)
                return;
            int ticks = TryParseTightenTicksFromTag(tag);
            _engine.TightenStop(instr, ticks);
        }

        // BWAVE-CYC T5: extracted helpers for OnRowApply.

        // ExtractNameFromTag: gets rule name from tag[0] (TextBox or string). CCN=2.
        private static string ExtractNameFromTag(object[] tag)
        {
            return tag[0] is TextBox tb ? tb.Text : tag[0] as string ?? string.Empty;
        }

        // CollectFollowersFromTag: collects selected Account items from tag[2] ListBox. CCN=3.
        // Returns empty list (never null) when ListBox is null or has no Account items.
        private static List<Account> CollectFollowersFromTag(object[] tag)
        {
            var followers = new List<Account>();
            var followerLb = tag[2] as ListBox;
            if (followerLb == null)
                return followers;
            foreach (var item in followerLb.SelectedItems)
                if (item is Account acc)
                    followers.Add(acc);
            return followers;
        }

        // BuildAtmMapFromTag: reads ATM mode from tag[3] ComboBox and builds per-follower map. CCN=4.
        // Returns empty dictionary when tag is too short or ATM selection is absent.
        private static Dictionary<string, FollowerAtmMode> BuildAtmMapFromTag(
            object[] tag,
            List<Account> followers
        )
        {
            var atmMap = new Dictionary<string, FollowerAtmMode>();
            if (tag.Length > 3 && tag[3] is ComboBox atmCb && atmCb.SelectedItem is string atmSel)
            {
                string atmMode = atmSel;
                if (
                    atmMode == "Named"
                    && tag.Length > 4
                    && tag[4] is TextBox namedBox
                    && namedBox.Text.Length > 0
                )
                    atmMode = "Named:" + namedBox.Text;
                var mode = CopyEngine.ParseAtmModeName(atmMode);
                foreach (var acc in followers)
                    atmMap[acc.Name] = mode;
            }
            return atmMap;
        }

        // BuildDefaultMultipliers: returns int array of all-1s. CCN=1.
        private static int[] BuildDefaultMultipliers(int count)
        {
            var m = new int[count];
            for (int i = 0; i < count; i++)
                m[i] = 1;
            return m;
        }

        // OnRowApply after extraction. CCN=7. _engine.AddRule MUST stay here.
        private void OnRowApply(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as object[];
            if (tag == null)
                return;
            string name = ExtractNameFromTag(tag);
            if (string.IsNullOrEmpty(name))
                return;
            var leaderCb = tag[1] as ComboBox;
            var leader = leaderCb?.SelectedItem as Account;
            var followers = CollectFollowersFromTag(tag);
            if (leader == null || followers.Count == 0)
                return;
            var atmMap = BuildAtmMapFromTag(tag, followers);
            var multipliers = BuildDefaultMultipliers(followers.Count);
            _engine.AddRule(name, leader, followers.ToArray(), multipliers, atmMap);
        }

        private void OnStatusUpdate(string line)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_logPanel == null)
                    return;
                var tb = new TextBlock
                {
                    Text = DateTime.UtcNow.ToString("HH:mm:ss") + "  " + line,
                };
                _logPanel.Children.Insert(0, tb);
                while (_logPanel.Children.Count > MaxLogLines)
                    _logPanel.Children.RemoveAt(_logPanel.Children.Count - 1);
            });
        }

        private Instrument FindInstrument(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            try
            {
                return Instrument.GetInstrument(name);
            }
            catch
            {
                return null;
            }
        }
    }
}
