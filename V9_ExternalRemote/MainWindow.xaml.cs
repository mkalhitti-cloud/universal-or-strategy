using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace V9_ExternalRemote
{
    public partial class MainWindow : Window
    {
        private string hubIp = "127.0.0.1";
        private int hubPort = 5000;
        private TcpClient client;
        private TosRtdClient _tosRtd;
        private ExcelRtdReader? _excelReader;
        private string _activeSymbol = "MES";
        public ObservableCollection<SymbolData> Symbols { get; set; } = new ObservableCollection<SymbolData>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            
            // Global Safety: Prevent "disappearing" app on any unhandled error
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                MessageBox.Show("V9 Remote Error: " + e.ExceptionObject.ToString());
            };

            InitializeTosRtd();
            InitializeExcelBridge();
            ConnectToHub();
            StartDataBroadcast();
        }

        private void StartDataBroadcast()
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += async (s, e) => {
                foreach (var symbol in Symbols)
                {
                    // Format: DATA|SYMBOL|LAST|EMA9|EMA15|ORH|ORL
                    string cmd = $"DATA|{symbol.Symbol}|{symbol.LastPrice}|{symbol.Ema9}|{symbol.Ema15}|{symbol.OrHigh}|{symbol.OrLow}";
                    await SendCommand(cmd);
                }
            };
            timer.Start();
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

        private void InitializeExcelBridge()
        {
            // Path to Excel workbook with TOS RTD formulas
            string excelPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TOS_RTD_Bridge.xlsx");
            
            // Also try the source directory if not found in bin
            if (!System.IO.File.Exists(excelPath))
            {
                excelPath = @"C:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\V9_ExternalRemote\TOS_RTD_Bridge.xlsx";
            }

            _excelReader = new ExcelRtdReader(
                excelPath,
                OnExcelDataReceived,
                msg => { if (_tosRtd != null) _tosRtd.Log(msg); else Console.WriteLine(msg); }
            );

            // Try to connect (will fail gracefully if Excel/file not ready)
            _excelReader.Connect();
        }

        private void OnExcelDataReceived(string symbol, string dataType, double ema9, double ema15)
        {
            this.Dispatcher.BeginInvoke(new Action(() => {
                // Find or create symbol data
                var data = Symbols.FirstOrDefault(s => s.Symbol == symbol);
                if (data == null)
                {
                    data = new SymbolData { Symbol = symbol };
                    Symbols.Add(data);
                }

                // Update EMA values from Excel
                data.Ema9 = ema9.ToString("F2");
                data.Ema15 = ema15.ToString("F2");
            }));
        }

        private void SubscribeToSymbol(string symbol)
        {
            try
            {
                string rawSymbol = symbol.TrimStart('/').ToUpper();
                
                // Check if already subscribed
                if (Symbols.Any(s => s.Symbol == rawSymbol)) return;

                // Create new symbol data row
                var symbolData = new SymbolData { Symbol = rawSymbol };
                Symbols.Add(symbolData);

                // Build the full TOS symbol with exchange suffix
                string exchange = GetExchange(rawSymbol);
                string tosSymbol = "/" + rawSymbol + ":" + exchange;
                string rawTos = "/" + rawSymbol;
                
                // === STANDARD PRICE FIELDS (WORKING) ===
                _tosRtd.Subscribe(tosSymbol + ":LAST", new object[] { "LAST", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":BID", new object[] { "BID", tosSymbol });
                _tosRtd.Subscribe(tosSymbol + ":ASK", new object[] { "ASK", tosSymbol });
                
                // === CUSTOM COLUMNS FROM TOS WATCHLIST ===
                // DISCOVERED: TOS internally uses CUSTOM3, CUSTOM4, etc. (not CUSTOM1, CUSTOM2)
                // From Excel export: =RTD("tos.rtd",,"CUSTOM3","/MGC:XCEC")
                // Custom 1 (EMA9) = CUSTOM3, Custom 2 (EMA15) = CUSTOM4
                
                // Use ONLY the exact format from TOS export: /SYMBOL:EXCHANGE
                // Try CUSTOM1-CUSTOM10 to cover all possibilities
                for (int i = 1; i <= 10; i++)
                {
                    string customField = $"CUSTOM{i}";
                    
                    // Subscribe with exact format from TOS: ["CUSTOMX", "/SYMBOL:EXCHANGE"]
                    _tosRtd.Subscribe($"{tosSymbol}:{customField}", new object[] { customField, tosSymbol });
                }
                
                _tosRtd.Log($"Subscribed to CUSTOM1-10 for {rawSymbol}");

            }
            catch { }
        }

        private void ClearPriceDisplay()
        {
            // No longer clearing individual textboxes as we use per-symbol rows
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
                    if (valStr.Contains("N/A") || string.IsNullOrWhiteSpace(valStr)) return;

                    double val = 0;
                    if (!double.TryParse(valStr, out val)) return;

                    // Find which symbol this update belongs to
                    // Key format: /SYMBOL:EXCHANGE:FIELD
                    string[] parts = key.Split(':');
                    if (parts.Length < 3) return;
                    
                    string symbol = parts[0].TrimStart('/');
                    var data = Symbols.FirstOrDefault(s => s.Symbol == symbol);
                    if (data == null) return;

                    string field = parts[parts.Length - 1];

                    // Map fields based on shotgun keys and common names
                    if (field == "LAST") data.LastPrice = val.ToString("F2");
                    else if (field == "BID") data.BidPrice = val.ToString("F2");
                    else if (field == "ASK") data.AskPrice = val.ToString("F2");

                    // Indicators: Check for CUSTOM columns or any EMA/OR pattern  
                    bool isIndicator = field.Contains("CUSTOM") || field.Contains("EMA") || field.Contains("OR");
                    
                    if (isIndicator)
                    {
                        // Log any valid non-zero hit
                        if (val != 0)
                        {
                            System.IO.File.AppendAllText("v9_shotgun_hits.txt", $"HIT: {key} = {val}\n");
                        }

                        // User's TOS Configuration: Custom 4 = EMA9, Custom 6 = EMA15
                        // EMA9 - CUSTOM4
                        if (key.Contains("CUSTOM4")) 
                            data.Ema9 = val.ToString("F2");
                        
                        // EMA15 - CUSTOM6
                        else if (key.Contains("CUSTOM6")) 
                            data.Ema15 = val.ToString("F2");
                        
                        // OR HIGH - CUSTOM7 or CUSTOM8 (future)
                        else if (key.Contains("CUSTOM7")) 
                            data.OrHigh = val.ToString("F2");
                        
                        // OR LOW - CUSTOM8 or CUSTOM9 (future)
                        else if (key.Contains("CUSTOM8")) 
                            data.OrLow = val.ToString("F2");
                    }
                    
                    // MTF Flags Logic
                    else if (field.Contains("EMA9_5M")) data.Flag5m = GetFlagBrush(val, data.LastPrice);
                    else if (field.Contains("EMA9_15M")) data.Flag15m = GetFlagBrush(val, data.LastPrice);
                    else if (field.Contains("EMA9_60M")) data.Flag60m = GetFlagBrush(val, data.LastPrice);
                }
                catch { }
            });
        }

        private static readonly Brush RedFlag = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush GreenFlag = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));

        private Brush GetFlagBrush(double level, string lastPriceStr)
        {
            double lastPrice = 0;
            double.TryParse(lastPriceStr, out lastPrice);
            return (lastPrice > level) ? GreenFlag : RedFlag;
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

    public class SymbolData : INotifyPropertyChanged
    {
        private string _lastPrice = "0.00";
        private string _bidPrice = "0.00";
        private string _askPrice = "0.00";
        private string _ema9 = "0.0";
        private string _ema15 = "0.0";
        private string _orHigh = "0.00";
        private string _orLow = "0.00";
        private Brush _flag5m = Brushes.Gray;
        private Brush _flag15m = Brushes.Gray;
        private Brush _flag60m = Brushes.Gray;

        public string Symbol { get; set; }
        public string LastPrice { get => _lastPrice; set { _lastPrice = value; OnPropertyChanged(); } }
        public string BidPrice { get => _bidPrice; set { _bidPrice = value; OnPropertyChanged(); } }
        public string AskPrice { get => _askPrice; set { _askPrice = value; OnPropertyChanged(); } }
        public string Ema9 { get => _ema9; set { _ema9 = value; OnPropertyChanged(); } }
        public string Ema15 { get => _ema15; set { _ema15 = value; OnPropertyChanged(); } }
        public string OrHigh { get => _orHigh; set { _orHigh = value; OnPropertyChanged(); } }
        public string OrLow { get => _orLow; set { _orLow = value; OnPropertyChanged(); } }
        public Brush Flag5m { get => _flag5m; set { _flag5m = value; OnPropertyChanged(); } }
        public Brush Flag15m { get => _flag15m; set { _flag15m = value; OnPropertyChanged(); } }
        public Brush Flag60m { get => _flag60m; set { _flag60m = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
