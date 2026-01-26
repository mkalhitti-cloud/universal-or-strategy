using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using V9_ExternalControl.Services;
using V9_ExternalControl.Models;

namespace V9_ExternalControl
{
    public partial class MainWindow : Window
    {
        private StatusFileReader _statusReader;
        private DispatcherTimer _refreshTimer;
        private const string REPO_ROOT = @"c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy";

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            StartAutoRefresh();
        }

        private void InitializeServices()
        {
            _statusReader = new StatusFileReader(REPO_ROOT);
            Log("Services Initialized. Reading from: " + REPO_ROOT);
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();
        }

        private async void OnRefreshTick(object sender, EventArgs e)
        {
            try
            {
                // Update TOS RTD
                var tosStatus = await _statusReader.ReadTosRtdStatusAsync();
                UpdateTosPanel(tosStatus);

                // Update Copy Trading
                var copyStatus = await _statusReader.ReadCopyTradingStatusAsync();
                UpdateCopyPanel(copyStatus);
            }
            catch (Exception ex)
            {
                Log($"Error refreshing data: {ex.Message}");
            }
        }

        private void UpdateTosPanel(V9TosRtdStatus status)
        {
            if (status == null)
            {
                TosStatusLed.Fill = Brushes.Gray;
                TosConnectionText.Text = "No Data";
                TxtOrHigh.Text = "0.00";
                TxtOrLow.Text = "0.00";
                return;
            }

            TosStatusLed.Fill = status.IsConnected ? Brushes.LimeGreen : Brushes.Red;
            TosConnectionText.Text = status.IsConnected ? "Connected" : "Disconnected";
            
            TxtSymbol.Text = status.Symbol;
            TxtLastPrice.Text = status.LastPrice.ToString("F2");
            TxtEma9.Text = status.Ema9.ToString("F2");
            TxtEma15.Text = status.Ema15.ToString("F2");
            TxtOrHigh.Text = status.OrHigh.ToString("F2");
            TxtOrLow.Text = status.OrLow.ToString("F2");
            TxtTosLastUpdate.Text = $"Last Update: {status.LastUpdate.ToString("HH:mm:ss")}";
        }

        private void UpdateCopyPanel(V9CopyTradingStatus status)
        {
            if (status == null)
            {
                TxtHeartbeat.Text = "No Data";
                TxtHeartbeat.Foreground = Brushes.Gray;
                return;
            }

            TxtHeartbeat.Text = status.Heartbeat ? "OK" : "FAILED";
            TxtHeartbeat.Foreground = status.Heartbeat ? Brushes.LimeGreen : Brushes.Red;
            
            CopyStatusLed.Fill = status.IsActive ? Brushes.LimeGreen : Brushes.Red;
            TxtCopyStatus.Text = status.IsActive ? "ACTIVE" : "INACTIVE";
            TxtCopyStatus.Foreground = status.IsActive ? Brushes.LimeGreen : Brushes.Red;

            TxtAccountsConnected.Text = $"{status.AccountsConnected}/20";
            TxtAggregatePnL.Text = status.AggregatePnL.ToString("C2");
            TxtAggregatePnL.Foreground = status.AggregatePnL >= 0 ? Brushes.LimeGreen : Brushes.Red;
            
            TxtErrorCount.Text = status.ErrorCount.ToString();
            
            GridTopAccounts.ItemsSource = status.TopAccounts;
            
            if (status.AccountsList.Count > 0)
            {
                Log($"Connected Accounts: {string.Join(", ", status.AccountsList)}");
            }
        }

        // Manual Controls (Placeholders for Phase 3)
        private void Button_Click_Long(object sender, RoutedEventArgs e)
        {
            Log($"[CMD] LONG Position Requested (Qty: {InputQty.Text})");
        }

        private void Button_Click_Short(object sender, RoutedEventArgs e)
        {
            Log($"[CMD] SHORT Position Requested (Qty: {InputQty.Text})");
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            Log("[CMD] CLOSE ALL Positions Requested");
        }

        private void Button_Click_Stop(object sender, RoutedEventArgs e)
        {
            Log("[CMD] EMERGENCY STOP Triggered!");
        }

        private void Log(string message)
        {
            TxtStatusBar.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
            // In a real app, perhaps append to a log file or text box
        }
    }
}
