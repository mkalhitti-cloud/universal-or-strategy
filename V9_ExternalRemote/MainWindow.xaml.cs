using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

namespace V9_ExternalRemote
{
    public partial class MainWindow : Window
    {
        private string hubIp = "127.0.0.1";
        private int hubPort = 5000;
        private TcpClient client;
        private TosRtdClient _tosRtd;
        private string _activeSymbol = "MES";

        public MainWindow()
        {
            InitializeComponent();
            
            // Global Safety: Prevent "disappearing" app on any unhandled error
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                MessageBox.Show("V9 Remote Error: " + e.ExceptionObject.ToString());
            };

            InitializeTosRtd();
            ConnectToHub();
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
            _tosRtd = new TosRtdClient(this.Dispatcher);
            // Force heartbeat logic
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += (s, e) => {
                if (_tosRtd.IsConnected) _tosRtd.Heartbeat();
            };
            timer.Start();

            _tosRtd.OnConnectionStatusChanged += (connected) => {
                this.Dispatcher.BeginInvoke(new Action(() => {
                    TosStatusLed.Background = connected ? Brushes.Lime : Brushes.Yellow;
                }));
            };
            _tosRtd.OnDataUpdate += (key, value) => {
                UpdatePriceDisplay(key, value);
            };
            
            _tosRtd.Start();
            
            if (_tosRtd.IsConnected)
            {
                SubscribeToSymbol(_activeSymbol);
            }
        }

        private void SubscribeToSymbol(string symbol)
        {
            try
            {
                // Safety: Unsubscribe before switching
                if (_tosRtd != null) _tosRtd.UnsubscribeAll();
                
                // Clear the UI to prevent stale data confusion
                ClearPriceDisplay();

                // Build the full TOS symbol with exchange suffix
                // TOS RTD requires format: /MGC:XCEC for gold
                string rawSymbol = symbol.TrimStart('/');
                
                // Determine exchange based on symbol root
                string exchange = GetExchange(rawSymbol);
                string tosSymbol = "/" + rawSymbol + ":" + exchange;
                
                // Standard built-in fields (2-parameter: Field, Symbol)
                _tosRtd.Subscribe(tosSymbol + ":LAST", new object[] { "LAST", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":BID", new object[] { "BID", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":ASK", new object[] { "ASK", tosSymbol });
                
                // If V9_RTD_Link study is available, add those too
                string study = "V9_RTD_Link";
                _tosRtd.Subscribe(tosSymbol + ":EMA9", new object[] { study, "EMA9", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":EMA15", new object[] { study, "EMA15", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":ORHIGH", new object[] { study, "ORHIGH", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":ORLOW", new object[] { study, "ORLOW", tosSymbol });
            }
            catch { }
        }



        private void ClearPriceDisplay()
        {
            this.Dispatcher.Invoke(() =>
            {
                LastPriceTxt.Text = "...";
                Ema9Txt.Text = "---";
                Ema15Txt.Text = "---";
                OrHighTxt.Text = "---";
                OrLowTxt.Text = "---";
            });
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

        private void UpdatePriceDisplay(string key, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (value == null) return;
                    string valStr = value.ToString();
                    if (valStr == "#N/A" || string.IsNullOrWhiteSpace(valStr) || valStr == "0.00") return;

                    double val = 0;
                    double.TryParse(valStr, out val);

                    if (key.EndsWith(":LAST")) LastPriceTxt.Text = val.ToString("F2");
                    else if (key.EndsWith(":EMA9")) Ema9Txt.Text = val.ToString("F2");
                    else if (key.EndsWith(":EMA15")) Ema15Txt.Text = val.ToString("F2");
                    else if (key.EndsWith(":ORHIGH")) OrHighTxt.Text = val.ToString("F2");
                    else if (key.EndsWith(":ORLOW")) OrLowTxt.Text = val.ToString("F2");
                    
                    // MTF Flags Logic
                    else if (key.EndsWith(":EMA9_5M")) UpdateFlag(Flag5m, Flag5mVal, val);
                    else if (key.EndsWith(":EMA9_15M")) UpdateFlag(Flag15m, Flag15mVal, val);
                    else if (key.EndsWith(":EMA9_60M")) UpdateFlag(Flag60m, Flag60mVal, val);
                }
                catch { }
            });
        }

        private static readonly Brush RedFlag = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush GreenFlag = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));

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
                
                _activeSymbol = SymbolInput.Text.Trim().ToUpper();
                SubscribeToSymbol(_activeSymbol);
                TriggerGlow(Brushes.Cyan);
            }
            catch { }
        }

        private async void ConnectToHub()
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(hubIp, hubPort);
                HubStatusLed.Background = Brushes.Lime;
            }
            catch (Exception)
            {
                HubStatusLed.Background = Brushes.Red;
            }
        }

        private async Task SendCommand(string cmd)
        {
            if (client == null || !client.Connected)
            {
                ConnectToHub();
                if (client == null || !client.Connected) return;
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(cmd);
                await client.GetStream().WriteAsync(data, 0, data.Length);
            }
            catch (Exception)
            {
                HubStatusLed.Background = Brushes.Red;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
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
            await SendCommand("FLATTEN|ALL|0");
        }

        private void GhostMode_Changed(object sender, RoutedEventArgs e)
        {
            if (GhostModeCheck.IsChecked == true)
            {
                this.Opacity = 0.5;
                // In a real WPF app, you'd use Win32 SetWindowLong to make it click-through
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
    }
}
