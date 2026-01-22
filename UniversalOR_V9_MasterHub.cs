using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// V9 Master Hub: Single strategy instance managing 20+ accounts and multiple instruments.
    /// Integrated with External WPF Remote via TCP Sockets.
    /// </summary>
    public class UniversalOR_V9_MasterHub : Strategy
    {
        #region Variables & IPC

        private string accountPrefix = "Apex";
        private int ipcPort = 5000;
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private readonly object commandLock = new object();
        private Queue<string> commandQueue = new Queue<string>();

        // Multi-Instrument Tracking
        private List<Instrument> trackedInstruments = new List<Instrument>();
        private Dictionary<string, int> instrumentBarsIndex = new Dictionary<string, int>();
        private Dictionary<string, RemoteIndicatorData> remoteData = new Dictionary<string, RemoteIndicatorData>();

        private class RemoteIndicatorData
        {
            public double LastPrice { get; set; }
            public double Ema9 { get; set; }
            public double Ema15 { get; set; }
            public double OrHigh { get; set; }
            public double OrLow { get; set; }
            public DateTime LastUpdate { get; set; }
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Account Prefix", Description = "Only trade accounts containing this string", Order = 1, GroupName = "1. Master Settings")]
        public string AccountPrefix { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "IPC Port", Description = "TCP port for WPF Remote connection", Order = 2, GroupName = "1. Master Settings")]
        public int IpcPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Instruments (CSV)", Description = "Comma-separated list of additional instruments (e.g. MES 03-26, MGC 02-26)", Order = 3, GroupName = "1. Master Settings")]
        public string AdditionalInstruments { get; set; }

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Universal OR Strategy V9 - The Master Hub";
                Name = "UniversalOR_V9_MasterHub";
                Calculate = Calculate.OnEachTick;
                IsUnmanaged = true; // CRITICAL for multi-account / speed
                
                AccountPrefix = "Apex";
                IpcPort = 5000;
                AdditionalInstruments = "";
            }
            else if (State == State.Configure)
            {
                // Add additional instruments for monitoring
                if (!string.IsNullOrEmpty(AdditionalInstruments))
                {
                    string[] symbols = AdditionalInstruments.Split(',');
                    foreach (string symbol in symbols)
                    {
                        AddDataSeries(symbol.Trim(), BarsPeriodType.Minute, 1);
                    }
                }
            }
            else if (State == State.DataLoaded)
            {
                StartIpcServer();
            }
            else if (State == State.Terminated)
            {
                StopIpcServer();
            }
        }

        protected override void OnBarUpdate()
        {
            // Process commands from the WPF Remote
            ProcessCommandQueue();
            
            // Core logic (Ported from V8.15) for each instrument will go here
        }

        #region Multi-Account Execution Engine

        /// <summary>
        /// Executes an order across all matching Apex accounts.
        /// </summary>
        private void ExecuteMultiAccountOrder(Instrument instrument, OrderAction action, OrderType type, int quantity, double limitPrice, double stopPrice, string signalName)
        {
            foreach (Account account in Account.All)
            {
                if (account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Create unmanaged order for each account
                    Order order = account.CreateOrder(instrument, action, type, TimeInForce.Gtc, quantity, limitPrice, stopPrice, "", signalName, null);
                    account.Submit(new[] { order });
                    
                    Print(string.Format("V9 HUB: Submitted {0} {1} {2} to Account {3}", action, quantity, instrument.FullName, account.Name));
                }
            }
        }

        #endregion

        #region IPC Server Logic

        private void StartIpcServer()
        {
            isRunning = true;
            serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_IPC_Server" };
            serverThread.Start();
            Print("V9 HUB: IPC Server started on port " + IpcPort);
        }

        private void StopIpcServer()
        {
            isRunning = false;
            listener?.Stop();
            serverThread?.Join(1000);
            Print("V9 HUB: IPC Server stopped");
        }

        private void ListenForRemote()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, IpcPort);
                listener.Start();

                while (isRunning)
                {
                    if (!listener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        lock (commandLock)
                        {
                            commandQueue.Enqueue(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isRunning) Print("V9 HUB IPC Error: " + ex.Message);
            }
        }

        private void ProcessCommandQueue()
        {
            string command = null;
            lock (commandLock)
            {
                if (commandQueue.Count > 0)
                    command = commandQueue.Dequeue();
            }

            if (command != null)
            {
                // Format: "ACTION|SYMBOL|Q1|Q2|Q3|..."
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;

                string action = parts[0];
                string symbol = parts[1];

                if (action == "DATA")
                {
                    // Format: DATA|SYMBOL|LAST|EMA9|EMA15|ORH|ORL
                    if (parts.Length < 7) return;
                    
                    if (!remoteData.ContainsKey(symbol)) remoteData[symbol] = new RemoteIndicatorData();
                    var rd = remoteData[symbol];
                    
                    double.TryParse(parts[2], out double last);
                    double.TryParse(parts[3], out double ema9);
                    double.TryParse(parts[4], out double ema15);
                    double.TryParse(parts[5], out double orh);
                    double.TryParse(parts[6], out double orl);
                    
                    rd.LastPrice = last;
                    rd.Ema9 = ema9;
                    rd.Ema15 = ema15;
                    rd.OrHigh = orh;
                    rd.OrLow = orl;
                    rd.LastUpdate = DateTime.Now;
                    
                    return;
                }

                Print("V9 HUB: Received command: " + command);
                
                if (action == "FLATTEN")
                {
                    ExecuteFlattenAll();
                    return;
                }

                int qty = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                Instrument instrument = Instrument.GetInstrument(symbol);
                
                if (instrument == null)
                {
                    Print("V9 HUB ERROR: Instrument not found: " + symbol);
                    return;
                }

                if (action == "LONG")
                {
                    ExecuteMultiAccountOrder(instrument, OrderAction.Buy, OrderType.Market, qty, 0, 0, "V9_Remote_Long");
                }
                else if (action == "SHORT")
                {
                    ExecuteMultiAccountOrder(instrument, OrderAction.SellShort, OrderType.Market, qty, 0, 0, "V9_Remote_Short");
                }
            }
        }

        private void ExecuteFlattenAll()
        {
            foreach (Account account in Account.All)
            {
                if (account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Correct way to flatten an account: Account.Flatten expects an IEnumerable<Instrument>
                    List<Instrument> instrumentsToFlatten = new List<Instrument>();
                    foreach (Position position in account.Positions)
                    {
                        if (position.MarketPosition != MarketPosition.Flat)
                        {
                            instrumentsToFlatten.Add(position.Instrument);
                        }
                    }

                    if (instrumentsToFlatten.Count > 0)
                    {
                        account.Flatten(instrumentsToFlatten);
                    }
                    
                    Print("V9 HUB: Flattened Account " + account.Name);
                }
            }
        }

        #endregion
    }
}
