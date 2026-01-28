using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Controls; // For Button
using System.Windows; // For Thickness, etc.

namespace V9_ExternalRemote
{
    public partial class MainWindow : Window
    {
        private string hubIp = "127.0.0.1";
        private int hubPort = 5000;
        private TcpClient client;
        private TosRtdClient _rtdClient;
        
        // Multi-Symbol Logic
        private Dictionary<string, SymbolData> _symbolCache = new Dictionary<string, SymbolData>();
        private string _activeSymbol = "MES"; // Currently DISPLAYED symbol
        
        public class SymbolData
        {
            public string Last { get; set; } = "...";
            public string Ema9 { get; set; } = "---";
            public string Ema15 { get; set; } = "---";
            public string Ema30 { get; set; } = "---";
            public string Ema65 { get; set; } = "---";
            public string Ema200 { get; set; } = "---";
            public string OrHigh { get; set; } = "---";
            public string OrLow { get; set; } = "---";
            public string Or15High { get; set; } = "---";
            public string Or15Low { get; set; } = "---";
            public string Flag5m { get; set; } = "---";
            public string Flag15m { get; set; } = "---";
            public string Flag1h { get; set; } = "---";
            public double LastVal { get; set; } = 0;
            public Button TabButton { get; set; }
        }

        private string _logPath = "v9_remote_log.txt";
        private V9_ExternalRemote_TCP_Server _server;

        public MainWindow()
        {
            InitializeComponent();
            
            // Global Safety: Prevent "disappearing" app on any unhandled error
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                MessageBox.Show("V9 Remote Error: " + e.ExceptionObject.ToString());
            };

            // V9_010 FINAL: NinjaTrader is now the Server. WPF app is the Client.
            // Disabling internal server to prevent port conflict.
            /*
            try
            {
                _server = new V9_ExternalRemote_TCP_Server(hubPort);
                _server.Start();
                LogToFile($"TCP Server started on port {hubPort}");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to start TCP Server: {ex.Message}");
                MessageBox.Show("Failed to start Hub Server: " + ex.Message);
            }
            */

            InitializeTosRtd();
            HubStatusLed.Background = Brushes.Gray; // Neutral until first command
        }

        private void SymbolInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetSymbol_Click(null, null);
            }
        }

        private void InitializeTosRtd()
        {
            _rtdClient = new TosRtdClient(this.Dispatcher);
            
            _rtdClient.OnDataUpdate += (key, value) => {
                UpdatePriceDisplay(key, value);
            };

            _rtdClient.OnConnectionStatusChanged += (connected) => {
                this.Dispatcher.Invoke(() => {
                    TosStatusLed.Background = connected ? Brushes.Lime : Brushes.Red;
                    LogToFile($"TOS STATUS: {(connected ? "CONNECTED" : "DISCONNECTED")}");
                });
            };

            _rtdClient.Start();
            
            // Initial subscription
            SubscribeToSymbol(_activeSymbol);
        }

        private void SubscribeToSymbol(string symbol)
        {
            // Normalize
            symbol = symbol.ToUpper();
            if (_symbolCache.ContainsKey(symbol))
            {
                SwitchToSymbol(symbol);
                return;
            }

            LogToFile($"Adding symbol: {symbol}");

            // Create Data Entry
            var data = new SymbolData();
            
            // Create UI Tab
            var btn = new Button
            {
                Content = symbol,
                FontSize = 9,
                Margin = new Thickness(0, 0, 2, 0),
                Padding = new Thickness(5, 2, 5, 2),
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                BorderThickness = new Thickness(0)
            };
            
            // Style hack: apply basic props, we'll handle active state manually
            btn.Click += (s, e) => SwitchToSymbol(symbol);
            
            data.TabButton = btn;
            _symbolCache[symbol] = data;
            WatchlistPanel.Children.Add(btn);

            // Subscribe
            string exchange = GetExchange(symbol);
            string fullSymbol = $"/{symbol}:{exchange}";
            
            LogToFile($"Subscribing all studies for {symbol}. Full={fullSymbol}");

            // ===== RTD CUSTOM FIELD MAPPING (V10 Verified) =====
            // EMA9  -> CUSTOM4   (confirmed via RECV log: CUSTOM4 returns live data; CUSTOM1 returns "loading")
            // EMA15 -> CUSTOM6   (confirmed via RECV log: CUSTOM6 returns live data; CUSTOM2 returns "loading")
            // ORHIGH -> CUSTOM9  (confirmed working in 12:22 session log)
            // ORLOW  -> CUSTOM11 (confirmed working in 12:22 session log)

            // ALL subscriptions must use fullSymbol (e.g., /MES:XCME)
            // The "loading" issue is now handled by the "Sticky Data" filter in UpdatePriceDisplay.

            _rtdClient.Subscribe($"{symbol}:LAST", new object[] { "LAST", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA9", new object[] { "CUSTOM4", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA15", new object[] { "CUSTOM6", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA30", new object[] { "CUSTOM8", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA65", new object[] { "CUSTOM19", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA200", new object[] { "CUSTOM18", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:ORHIGH", new object[] { "CUSTOM9", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:ORLOW", new object[] { "CUSTOM11", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:OR15HIGH", new object[] { "CUSTOM13", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:OR15LOW", new object[] { "CUSTOM15", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG5M", new object[] { "CUSTOM14", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG15M", new object[] { "CUSTOM16", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG1H", new object[] { "CUSTOM20", fullSymbol });

            // Safety catch: Try XNYM for metals if XCEC fails (Gold/Silver have odd routing sometimes)
            // But log indicated MGC worked on XCEC for Discovery. Sticking to plan.

            SwitchToSymbol(symbol);
        }

        private void SwitchToSymbol(string symbol)
        {
            _activeSymbol = symbol;
            
            // Update Tabs UI
            foreach (var kvp in _symbolCache)
            {
                if (kvp.Value.TabButton != null)
                {
                    bool isActive = kvp.Key == symbol;
                    kvp.Value.TabButton.Foreground = isActive ? Brushes.Cyan : Brushes.Gray;
                    kvp.Value.TabButton.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
                }
            }

            // Refresh Main Display from Cache
            if (_symbolCache.TryGetValue(symbol, out var data))
            {
                LastPriceTxt.Text = data.Last;
                Ema9Txt.Text = data.Ema9;
                Ema15Txt.Text = data.Ema15;
                Ema30Txt.Text = data.Ema30;
                Ema65Txt.Text = data.Ema65;
                Ema200Txt.Text = data.Ema200;
                OrHighTxt.Text = data.OrHigh;
                OrLowTxt.Text = data.OrLow;
                Flag5mVal.Text = data.Flag5m;
                Flag15mVal.Text = data.Flag15m;
                Flag1hVal.Text = data.Flag1h;
            }
        }

        private void UpdatePriceDisplay(string key, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (value == null) return;
                    string valStr = value.ToString();
                    
                    // Trace every single update at the UI level
                    // LogToFile($"UI RECV: {key} = {valStr}"); 
                    
                    if (key.Contains("DISCO"))
                    {
                         LogToFile($"DISCOVERY: {key} = {valStr}");
                    }

                    if (valStr == "#N/A" || string.IsNullOrWhiteSpace(valStr) || valStr.ToLower().Contains("loading")) return;

                    // Parse Key: SYMBOL:FIELD
                    string[] parts = key.Split(':');
                    if (parts.Length < 2) return;
                    
                    string symbol = parts[0];
                    string field = parts[1];

                    if (!_symbolCache.ContainsKey(symbol)) return;
                    var data = _symbolCache[symbol];

                    if (!double.TryParse(valStr, out double val)) return;
                    
                    // Specific check for 0.00 - indicators like EMA/OR rarely hit exactly zero.
                    // If it's 0.00, it might be an uninitialized state in TOS.
                    if (val == 0 && (field != "LAST")) return; 

                    string fmtVal = val.ToString("F2");

                    // Update Cache
                    if (field == "LAST") { data.Last = fmtVal; data.LastVal = val; }
                    else if (field == "EMA9") data.Ema9 = fmtVal;
                    else if (field == "EMA15") data.Ema15 = fmtVal;
                    else if (field == "EMA30") data.Ema30 = fmtVal;
                    else if (field == "EMA65") data.Ema65 = fmtVal;
                    else if (field == "EMA200") data.Ema200 = fmtVal;
                    else if (field == "ORHIGH") data.OrHigh = fmtVal;
                    else if (field == "ORLOW") data.OrLow = fmtVal;
                    else if (field == "OR15HIGH") data.Or15High = fmtVal;
                    else if (field == "OR15LOW") data.Or15Low = fmtVal;
                    else if (field == "FLAG5M") data.Flag5m = fmtVal;
                    else if (field == "FLAG15M") data.Flag15m = fmtVal;
                    else if (field == "FLAG1H") data.Flag1h = fmtVal;

                    // Update UI ONLY if active
                    if (symbol == _activeSymbol)
                    {
                         if (field == "LAST") LastPriceTxt.Text = fmtVal;
                         else if (field == "EMA9") Ema9Txt.Text = fmtVal;
                          else if (field == "EMA15") Ema15Txt.Text = fmtVal;
                          else if (field == "EMA30") Ema30Txt.Text = fmtVal;
                          else if (field == "EMA65") Ema65Txt.Text = fmtVal;
                          else if (field == "EMA200") Ema200Txt.Text = fmtVal;
                          else if (field == "ORHIGH") OrHighTxt.Text = fmtVal;
                          else if (field == "ORLOW") OrLowTxt.Text = fmtVal;
                          else if (field == "OR15HIGH") Or15HighTxt.Text = fmtVal;
                          else if (field == "OR15LOW") Or15LowTxt.Text = fmtVal;
                          else if (field == "FLAG5M") UpdateFlag(Flag5m, Flag5mVal, val);
                          else if (field == "FLAG15M") UpdateFlag(Flag15m, Flag15mVal, val);
                          else if (field == "FLAG1H") Update1HTrend(val);
                    }
                    
                    // MTF Logic (simplified for now, attached to active symbol mostly or global)
                    // For now, let's skip complex MTF flag parsing unless it's strictly required
                }
                catch { }
            });
        }
        
        private static readonly Brush RedFlag = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush GreenFlag = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));

        private void Update1HTrend(double level)
        {
            try
            {
                double lastPrice = 0;
                double.TryParse(LastPriceTxt.Text, out lastPrice);
                
                // Update the Flag Cluster (Right Sidebar)
                Flag1hVal.Text = level.ToString("F2");
                Flag1h.Background = (lastPrice > level) ? GreenFlag : RedFlag;

                // Update the Central Multi-Row Indicator
                if (lastPrice > level)
                {
                    Trend1hVal.Text = "BULLISH (UP)";
                    Trend1hVal.Foreground = Brushes.Lime;
                    Trend1hBorder.Background = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0));
                }
                else
                {
                    Trend1hVal.Text = "BEARISH (DOWN)";
                    Trend1hVal.Foreground = Brushes.Red;
                    Trend1hBorder.Background = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0));
                }
            }
            catch { }
        }

        private void UpdateFlag(System.Windows.Controls.Border flagBorder, System.Windows.Controls.TextBlock flagText, double level)
        {
            try
            {
                double lastPrice = 0;
                double.TryParse(LastPriceTxt.Text, out lastPrice);
                
                flagText.Text = level.ToString("F2");
                
                // Color logic: Green if price is above, Red if below
                flagBorder.Background = (lastPrice > level) ? GreenFlag : RedFlag;
            }
            catch { }
        }

        private void SetSymbol_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(SymbolInput.Text)) return;
                
                string newSym = SymbolInput.Text.Trim().ToUpper();
                SubscribeToSymbol(newSym);
                TriggerGlow(Brushes.Cyan);
                SymbolInput.Text = ""; // Clear input after adding
            }
            catch { }
        }
        
        private string GetExchange(string symbol)
        {
            // Map common futures roots to their exchanges based on successful Shotgun Test V3 results
            string root = symbol.Length >= 2 ? symbol.Substring(0, 2).ToUpper() : symbol.ToUpper();
            
            // CME (Equities like S&P, Nasdaq)
            if (root == "ES" || root == "ME" || root == "NQ" || root == "MN" || root == "RT") 
                return "XCME";
            
            // COMEX (Gold, Silver, Copper)
            if (root == "GC" || root == "MG" || root == "SI" || root == "HG")
                return "XCEC";
            
            // NYMEX (Crude Oil)
            if (root == "CL" || root == "QM")
                return "XNYM";

            // CBOT (Dow, Treasuries, Grains)
            if (root == "YM" || root == "ZN" || root == "ZB" || root == "ZC" || root == "ZS" || root == "ZW")
                return "XCBT";
            
            // Default to CME for unknown
            return "XCME";
        }

        private async Task<bool> TestHubConnection()
        {
            try
            {
                using (var testClient = new TcpClient())
                {
                    var connectTask = testClient.ConnectAsync(hubIp, hubPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(500)) == connectTask)
                    {
                        await connectTask;
                        return true;
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        private async Task SendCommand(string cmd)
        {
            try
            {
                using (TcpClient quickClient = new TcpClient())
                {
                    // High-speed timeout for local connection
                    var connectTask = quickClient.ConnectAsync(hubIp, hubPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(1000)) != connectTask)
                    {
                        throw new Exception("Timeout");
                    }
                    
                    await connectTask;
                    
                    byte[] data = Encoding.UTF8.GetBytes(cmd);
                    await quickClient.GetStream().WriteAsync(data, 0, data.Length);
                    
                    HubStatusLed.Background = Brushes.Lime;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"CMD FAIL: {cmd} | {ex.Message}");
                HubStatusLed.Background = Brushes.Red;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _server?.Stop();
            Close();
        }

        private async void Long_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"LONG|{_activeSymbol}|1");
            TriggerGlow(Brushes.Lime);
        }

        private async void Short_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"SHORT|{_activeSymbol}|1");
            TriggerGlow(Brushes.Red);
        }

        private async void Flatten_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"FLATTEN|{_activeSymbol}");
            TriggerGlow(Brushes.White);
        }

        private async void Trim25_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"TRIM_25|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void Trim50_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"TRIM_50|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void BEPlus1_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"BE_PLUS_1|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        // V10.3: OR Breakout Entry Handlers
        private async void OrLong_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"OR_LONG|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        private async void OrShort_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"OR_SHORT|{_activeSymbol}");
            TriggerGlow(Brushes.Magenta);
        }

        // V10.3: Target-Specific Close Handlers
        private async void CloseT1_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T1|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void CloseT2_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T2|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void CloseT3_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T3|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void CloseT4_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T4|{_activeSymbol}");
            TriggerGlow(Brushes.OrangeRed);
        }

        // V10.4: Advanced Runner Control Handlers
        private async void RunBE_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_BE|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        private async void Run1pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_1PT|{_activeSymbol}");
            TriggerGlow(Brushes.Lime);
        }

        private async void Run2pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_2PT|{_activeSymbol}");
            TriggerGlow(Brushes.Green);
        }

        private async void Run50_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_50|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void RunOff_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_OFF|{_activeSymbol}");
            TriggerGlow(Brushes.Gray);
        }

        private async void Rma_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_RMA|{_activeSymbol}");
            TriggerGlow(Brushes.SandyBrown);
        }

        private async void Momo_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_MOMO|{_activeSymbol}");
            TriggerGlow(Brushes.MediumPurple);
        }

        private async void Trend_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"EXEC_TREND|{_activeSymbol}");
            TriggerGlow(Brushes.Teal);
        }

        private async void Ffma_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_FFMA|{_activeSymbol}");
            TriggerGlow(Brushes.DeepSkyBlue);
        }

        private async void Retest_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"EXEC_RETEST|{_activeSymbol}");
            TriggerGlow(Brushes.Gold);
        }

        private void GhostMode_Changed(object sender, RoutedEventArgs e)
        {
            if (GhostModeCheck.IsChecked == true)
            {
                this.Opacity = 0.5;
            }
            else
            {
                this.Opacity = 1.0;
            }
        }

        private async void TriggerGlow(Brush color)
        {
            GlowBorder.BorderBrush = color;
            await Task.Delay(500);
            GlowBorder.BorderBrush = Brushes.Transparent;
        }

        private void LogToFile(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} | {msg}\r\n");
            }
            catch { }
        }
    }
}
