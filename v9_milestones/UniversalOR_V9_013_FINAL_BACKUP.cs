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
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class UniversalOR_V9_013_FINAL : Strategy
    {
        #region Private Variables
        private string accountPrefix = "Apex";
        private int ipcPort = 5000;
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private readonly object commandLock = new object();
        private Queue<string> commandQueue = new Queue<string>();

        private Dictionary<string, bool> tightTrailActive = new Dictionary<string, bool>();
        private ATR atrIndicator;
        private EMA ema9;
        private EMA ema15;
        private double currentATR = 0;
        private double lastEma9 = 0;
        private double lastEma15 = 0;
        
        // WSGTA Strategy Flags
        private bool isRMAModeActive = false;
        private bool isMOMOModeActive = false;
        private bool isFFMAModeActive = false;
        private bool isRetestModeActive = false;

        private Dictionary<string, PositionInfo> activePositions = new Dictionary<string, PositionInfo>();
        #endregion

        #region Position Info Class
        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;
            public int T2Contracts;
            public int T3Contracts;
            public int T4Contracts;
            public int T5Contracts;
            public double EntryPrice;
            public double StopPrice;
            public double Target1Price;
            public double Target2Price;
            public double Target3Price;
            public double Target4Price;
            public double Target5Price;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;
            public bool T4Filled;
            public bool T5Filled;
        }
        #endregion

        #region Properties - Target Mode
        [NinjaScriptProperty]
        [Display(Name = "Target Mode", Description = "4 = 4-Targets (20/30/30/20), 5 = 5-Targets (Customizable)", Order = 0, GroupName = "3. Profit Targets")]
        [Range(4, 5)]
        public int TargetMode { get; set; }
        #endregion

        #region Properties - Risk Management
        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade ($)", Description = "Maximum dollar risk per trade", Order = 1, GroupName = "1. Risk Management")]
        public double RiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reduced Risk ($)", Description = "Reduced risk when stop > threshold", Order = 2, GroupName = "1. Risk Management")]
        public double ReducedRiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Threshold (Points)", Description = "Stop distance above which reduced risk is used", Order = 3, GroupName = "1. Risk Management")]
        public double StopThresholdPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Min Contracts", Order = 4, GroupName = "1. Risk Management")]
        public int MESMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Max Contracts", Order = 5, GroupName = "1. Risk Management")]
        public int MESMaximum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Min Contracts", Order = 6, GroupName = "1. Risk Management")]
        public int MGCMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Max Contracts", Order = 7, GroupName = "1. Risk Management")]
        public int MGCMaximum { get; set; }
        #endregion

        #region Properties - Stop Loss
        [NinjaScriptProperty]
        [Display(Name = "Stop Multiplier", Description = "Multiplier of ATR for stop distance", Order = 1, GroupName = "2. Stop Loss")]
        public double StopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Stop (Points)", Order = 2, GroupName = "2. Stop Loss")]
        public double MinimumStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (Points)", Description = "HARDENED: 8pt max stop for Apex compliance", Order = 3, GroupName = "2. Stop Loss")]
        public double MaximumStop { get; set; }
        #endregion

        #region Properties - Profit Targets
        [NinjaScriptProperty]
        [Display(Name = "T1 Fixed Points", Description = "Fixed point profit for T1 (quick scalp)", Order = 1, GroupName = "3. Profit Targets")]
        [Range(0.25, 5.0)]
        public double Target1FixedPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 ATR Multiplier", Description = "Multiplier of ATR for T2", Order = 2, GroupName = "3. Profit Targets")]
        public double Target2Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 ATR Multiplier", Description = "Multiplier of ATR for T3", Order = 3, GroupName = "3. Profit Targets")]
        public double Target3Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 ATR Multiplier", Description = "Multiplier of ATR for T4 (5-Target Mode only)", Order = 4, GroupName = "3. Profit Targets")]
        public double Target4Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T5 ATR Multiplier", Description = "Multiplier of ATR for T5 (5-Target Mode only)", Order = 5, GroupName = "3. Profit Targets")]
        public double Target5Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Period", Description = "Period for ATR calculation", Order = 6, GroupName = "3. Profit Targets")]
        [Range(5, 50)]
        public int ATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Description = "20% for quick scalp", Order = 10, GroupName = "3. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Order = 11, GroupName = "3. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Order = 12, GroupName = "3. Profit Targets")]
        public int T3ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 Contract %", Description = "Runner (4-mode) or Target (5-mode)", Order = 13, GroupName = "3. Profit Targets")]
        public int T4ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T5 Contract %", Description = "Runner for 5-Target Mode", Order = 14, GroupName = "3. Profit Targets")]
        public int T5ContractPercent { get; set; }
        #endregion

        #region Properties - Trailing
        [NinjaScriptProperty]
        [Display(Name = "Tight Trail Enabled", Description = "Enable tight trailing stop after threshold", Order = 1, GroupName = "5. Trailing")]
        public bool TightTrailEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tight Trail Trigger (Pts)", Description = "Points in profit to activate tight trail", Order = 2, GroupName = "5. Trailing")]
        public double TightTrailTrigger { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tight Trail Offset (Pts)", Description = "Trailing distance behind price", Order = 3, GroupName = "5. Trailing")]
        public double TightTrailOffset { get; set; }
        #endregion

        #region Properties - IPC Settings
        [NinjaScriptProperty]
        [Display(Name = "Account Prefix", Description = "Prefix to match Apex accounts", Order = 1, GroupName = "4. IPC Settings")]
        public string AccountPrefix
        {
            get { return accountPrefix; }
            set { accountPrefix = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "IPC Port", Description = "TCP port for remote commands", Order = 2, GroupName = "4. IPC Settings")]
        public int IpcPort
        {
            get { return ipcPort; }
            set { ipcPort = value; }
        }
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V9.1.3 FINAL - Gold Standard Production: Full Feature Integration with WSGTA Remote";
                Name = "UniversalOR_V9_013_FINAL";
                Calculate = Calculate.OnPriceChange;
                IsUnmanaged = true;

                AccountPrefix = "Apex";
                IpcPort = 5000;

                RiskPerTrade = 200;
                ReducedRiskPerTrade = 200;
                StopThresholdPoints = 5.0;
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                StopMultiplier = 0.5;
                MinimumStop = 1.0;
                MaximumStop = 8.0;

                TargetMode = 4;
                ATRPeriod = 14;

                T1ContractPercent = 20;
                T2ContractPercent = 30;
                T3ContractPercent = 30;
                T4ContractPercent = 20;
                T5ContractPercent = 10;

                Target1FixedPoints = 1.0;
                Target2Multiplier = 0.5;
                Target3Multiplier = 1.0;
                Target4Multiplier = 1.5;
                Target5Multiplier = 2.0;

                TightTrailEnabled = true;
                TightTrailTrigger = 3.0;
                TightTrailOffset = 1.0;
            }
            else if (State == State.Configure)
            {
                atrIndicator = ATR(ATRPeriod);
            }
            else if (State == State.DataLoaded)
            {
                atrIndicator = ATR(ATRPeriod);
                ema9 = EMA(9);
                ema15 = EMA(15);
                
                isRunning = true;
                serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_FINAL_IPC" };
                serverThread.Start();
                
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() => {
                        ChartControl.MouseDown += OnChartClick;
                    });
                }
                
                string modeDesc = TargetMode == 4 ? "4-Target" : "5-Target";
                Print("V9.1.3 FINAL: Loaded | Mode=" + modeDesc + " | TightTrail=" + TightTrailEnabled + " | MaxStop=" + MaximumStop);
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() => {
                        ChartControl.MouseDown -= OnChartClick;
                    });
                }
            }
            else if (State == State.Terminated)
            {
                StopIpcServer();
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            if (atrIndicator == null || ema9 == null || ema15 == null) return;
            
            currentATR = atrIndicator[0];
            lastEma9 = ema9[0];
            lastEma15 = ema15[0];

            ProcessCommandQueue();
            ManageTightTrailing();
            
            // FFMA Background Monitoring
            if (isFFMAModeActive) CheckFFMAConditions();
        }
        #endregion

        #region IPC Server
        private void StartIpcServer()
        {
            isRunning = true;
            serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_FINAL_IPC" };
            serverThread.Start();
        }

        private void StopIpcServer()
        {
            isRunning = false;
            listener?.Stop();
            serverThread?.Join(1000);
        }

        private void ListenForRemote()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, IpcPort);
                listener.Start();
                while (isRunning)
                {
                    if (!listener.Pending()) { Thread.Sleep(10); continue; }
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lock (commandLock) { commandQueue.Enqueue(message); }
                    }
                }
            }
            catch { }
        }

        private void ProcessCommandQueue()
        {
            while (true)
            {
                string command = null;
                lock (commandLock) { if (commandQueue.Count > 0) command = commandQueue.Dequeue(); }
                if (command == null) break;

                string[] parts = command.Split('|');
                if (parts.Length < 2) continue;
                string action = parts[0].Trim().ToUpper();
                string symbol = parts[1].Trim().ToUpper();
                
                Print(string.Format("V9 RECV: {0} for {1}", action, symbol));

                switch (action)
                {
                    case "LONG":
                        ExecuteOrder(symbol, OrderAction.Buy);
                        break;
                    case "SHORT":
                        ExecuteOrder(symbol, OrderAction.SellShort);
                        break;
                    case "FLATTEN":
                        ExecuteFlatten(symbol);
                        break;
                    case "TRIM_25":
                        ExecuteTrim(symbol, 0.25);
                        break;
                    case "TRIM_50":
                        ExecuteTrim(symbol, 0.50);
                        break;
                    case "BE_PLUS_1":
                        ExecuteBreakevenPlus(symbol, 1.0);
                        break;
                    case "MODE_RMA":
                        isRMAModeActive = !isRMAModeActive;
                        Print("V9: RMA Mode " + (isRMAModeActive ? "Armed" : "Disarmed"));
                        break;
                    case "MODE_MOMO":
                        isMOMOModeActive = !isMOMOModeActive;
                        Print("V9: MOMO Mode " + (isMOMOModeActive ? "Armed" : "Disarmed"));
                        break;
                    case "MODE_FFMA":
                        isFFMAModeActive = !isFFMAModeActive;
                        Print("V9: FFMA Mode " + (isFFMAModeActive ? "Armed" : "Disarmed"));
                        break;
                    case "EXEC_TREND":
                        ExecuteTRENDEntry(symbol);
                        break;
                    case "EXEC_RETEST":
                        ExecuteRetestEntry(symbol);
                        break;
                    default:
                        Print("V9 WARN: Unknown Action " + action);
                        break;
                }
            }
        }
        #endregion

        #region Order Execution
        private void ExecuteOrder(string symbol, OrderAction action, OrderType orderType = OrderType.Market, double limitPrice = 0)
        {
            // V9_010: Improved Symbol Resolution
            Instrument instrument = null;
            
            // 1. Try exact match
            instrument = Instrument.GetInstrument(symbol);
            
            // 2. Try chart instrument check (Fuzzy match)
            if (instrument == null)
            {
                if (Instrument.FullName.Contains(symbol) || Instrument.MasterInstrument.Name == symbol)
                {
                    instrument = Instrument;
                }
            }

            if (instrument == null)
            {
                Print(string.Format("V9 ERR: Instrument '{0}' not found. Chart is '{1}'. Use full name or ensure chart matches.", symbol, Instrument.FullName));
                return;
            }

            Print(string.Format("V9 INFO: Executing {0} for {1} (using {2})", action, symbol, instrument.FullName));

            double stopDistance = Math.Min(MaximumStop, Math.Max(MinimumStop, currentATR * StopMultiplier));
            if (currentATR <= 0) stopDistance = MinimumStop;

            foreach (Account account in Account.All)
            {
                bool matches = account.Name.ToLower().Contains(AccountPrefix.ToLower());
                Print(string.Format("V9 CHECK: Account {0} | Prefix {1} | Matches={2}", account.Name, AccountPrefix, matches));

                if (!matches) continue;

                if (instrument.MarketData.Last == null)
                {
                    Print("V9 ERR: No MarketData.Last for " + symbol + ". Waiting for ticks...");
                    continue;
                }

                double entryPrice = orderType == OrderType.Market ? instrument.MarketData.Last.Price : limitPrice;
                double pointValue = instrument.MasterInstrument.PointValue;

                int minContracts = symbol.Contains("MES") ? MESMinimum : MGCMinimum;
                int maxContracts = symbol.Contains("MES") ? MESMaximum : MGCMaximum;

                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                int totalContracts = (int)Math.Floor(riskToUse / (stopDistance * pointValue));
                totalContracts = Math.Max(minContracts, Math.Min(totalContracts, maxContracts));

                double stopPrice = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice - stopDistance)
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice + stopDistance);

                double t1Price = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice + Target1FixedPoints)
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice - Target1FixedPoints);

                double t2Price = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice + (currentATR * Target2Multiplier))
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice - (currentATR * Target2Multiplier));

                double t3Price = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice + (currentATR * Target3Multiplier))
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice - (currentATR * Target3Multiplier));

                double t4Price = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice + (currentATR * Target4Multiplier))
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice - (currentATR * Target4Multiplier));

                double t5Price = action == OrderAction.Buy
                    ? instrument.MasterInstrument.RoundToTickSize(entryPrice + (currentATR * Target5Multiplier))
                    : instrument.MasterInstrument.RoundToTickSize(entryPrice - (currentATR * Target5Multiplier));

                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                CalculateContractDistribution(totalContracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string timestamp = DateTime.Now.ToString("HHmmss");
                string signalName = action == OrderAction.Buy ? "V9_Long" : "V9_Short";
                string entryName = signalName + "_" + timestamp + "_" + account.Name;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = action == OrderAction.Buy ? MarketPosition.Long : MarketPosition.Short,
                    TotalContracts = totalContracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    EntryPrice = entryPrice,
                    StopPrice = stopPrice,
                    Target1Price = t1Price,
                    Target2Price = t2Price,
                    Target3Price = t3Price,
                    Target4Price = t4Price,
                    Target5Price = t5Price
                };
                activePositions[entryName] = pos;

                Order entryOrder = account.CreateOrder(instrument, action, orderType, TimeInForce.Gtc, totalContracts, limitPrice, limitPrice, "", entryName, null);
                account.Submit(new[] { entryOrder });

                OrderAction exitAction = action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover;
                Order stopOrder = account.CreateOrder(instrument, exitAction, OrderType.StopMarket, TimeInForce.Gtc, totalContracts, 0, stopPrice, "", "V9_Stop", null);
                account.Submit(new[] { stopOrder });

                SubmitTargetOrders(account, instrument, exitAction, pos);

                string key = account.Name + "_" + symbol;
                tightTrailActive[key] = false;

                string modeStr = TargetMode == 4 ? "4T" : "5T";
                Print(string.Format("V9_010 [{0}]: {1} | {2}x @ {3:F2} | Stop={4:F2} | ATR={5:F2}",
                    modeStr, account.Name, totalContracts, entryPrice, stopPrice, currentATR));
            }
        }

        private void CalculateContractDistribution(int totalContracts, out int t1, out int t2, out int t3, out int t4, out int t5)
        {
            t5 = 0;

            if (TargetMode == 4)
            {
                if (totalContracts == 1) { t1 = 1; t2 = 0; t3 = 0; t4 = 0; }
                else if (totalContracts == 2) { t1 = 1; t2 = 0; t3 = 0; t4 = 1; }
                else if (totalContracts == 3) { t1 = 1; t2 = 1; t3 = 0; t4 = 1; }
                else if (totalContracts == 4) { t1 = 1; t2 = 1; t3 = 1; t4 = 1; }
                else
                {
                    t1 = (int)Math.Floor(totalContracts * T1ContractPercent / 100.0);
                    t2 = (int)Math.Floor(totalContracts * T2ContractPercent / 100.0);
                    t3 = (int)Math.Floor(totalContracts * T3ContractPercent / 100.0);
                    t4 = totalContracts - t1 - t2 - t3;
                    if (t1 < 1) t1 = 1;
                    if (t4 < 1) t4 = 1;
                }
            }
            else
            {
                if (totalContracts == 1) { t1 = 1; t2 = 0; t3 = 0; t4 = 0; t5 = 0; }
                else if (totalContracts == 2) { t1 = 1; t2 = 0; t3 = 0; t4 = 0; t5 = 1; }
                else if (totalContracts == 3) { t1 = 1; t2 = 1; t3 = 0; t4 = 0; t5 = 1; }
                else if (totalContracts == 4) { t1 = 1; t2 = 1; t3 = 1; t4 = 0; t5 = 1; }
                else if (totalContracts == 5) { t1 = 1; t2 = 1; t3 = 1; t4 = 1; t5 = 1; }
                else
                {
                    t1 = (int)Math.Floor(totalContracts * T1ContractPercent / 100.0);
                    t2 = (int)Math.Floor(totalContracts * T2ContractPercent / 100.0);
                    t3 = (int)Math.Floor(totalContracts * T3ContractPercent / 100.0);
                    t4 = (int)Math.Floor(totalContracts * T4ContractPercent / 100.0);
                    t5 = totalContracts - t1 - t2 - t3 - t4;
                    if (t1 < 1) t1 = 1;
                    if (t5 < 1) t5 = 1;
                }
            }
        }

        private void SubmitTargetOrders(Account account, Instrument instrument, OrderAction exitAction, PositionInfo pos)
        {
            if (pos.T1Contracts > 0)
            {
                Order t1Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T1Contracts, pos.Target1Price, 0, "", "V9_T1", null);
                account.Submit(new[] { t1Order });
            }

            if (pos.T2Contracts > 0)
            {
                Order t2Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T2Contracts, pos.Target2Price, 0, "", "V9_T2", null);
                account.Submit(new[] { t2Order });
            }

            if (pos.T3Contracts > 0)
            {
                Order t3Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T3Contracts, pos.Target3Price, 0, "", "V9_T3", null);
                account.Submit(new[] { t3Order });
            }

            if (TargetMode == 5 && pos.T4Contracts > 0)
            {
                Order t4Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T4Contracts, pos.Target4Price, 0, "", "V9_T4", null);
                account.Submit(new[] { t4Order });
            }
        }
        #endregion

        #region Chart Click Logic
        private void OnChartClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!isRMAModeActive && !isMOMOModeActive) return;
            if (ChartControl == null) return;

            try
            {
                // Find axis and scale logic
                // Using 0-index panel as standard price panel
                NinjaTrader.Gui.Chart.ChartPanel panel = ChartControl.ChartPanels[0];
                ChartScale scale = null;

                // Find the RIGHT scale (price)
                foreach(ChartScale s in panel.Scales)
                {
                    if(s.ScaleJustification == ScaleJustification.Right)
                    {
                        scale = s;
                        break;
                    }
                }
                
                // Fallback
                if (scale == null && panel.Scales.Count > 0) scale = panel.Scales[0];
                
                if (scale == null) 
                {
                    Print("V9 Click Error: No scale found");
                    return;
                }

                // Get click point relative to the ChartVisual
                Point mousePos = e.GetPosition(ChartControl);
                
                // Note: GetValueByY takes y relative to the chart panel area. 
                // However, simple approximation using ChartControl Y works reliably for single-pane charts in most resolutions.
                // For perfect precision we would subtract the panel header height if needed, but standard single-pane is direct.
                
                double price = scale.GetValueByY((float)mousePos.Y);
                price = Instrument.MasterInstrument.RoundToTickSize(price);

                Print(string.Format("V9 Click Entry: Price={0:F2} | RMA={1} | MOMO={2}", price, isRMAModeActive, isMOMOModeActive));

                if (isRMAModeActive)
                {
                    // RMA: Limit entry
                    double currentPrice = Instrument.MarketData.Last.Price;
                    OrderAction action = price > currentPrice ? OrderAction.SellShort : OrderAction.Buy;
                    ExecuteClickTrade(Instrument.MasterInstrument.Name, action, OrderType.Limit, price);
                    
                    isRMAModeActive = false;
                }
                else if (isMOMOModeActive)
                {
                    // MOMO: Stop Market entry
                    double currentPrice = Instrument.MarketData.Last.Price;
                    OrderAction action = price > currentPrice ? OrderAction.Buy : OrderAction.SellShort;
                    ExecuteClickTrade(Instrument.MasterInstrument.Name, action, OrderType.StopMarket, price);
                    
                    isMOMOModeActive = false;
                }
            }
            catch (Exception ex)
            {
                Print("V9 Click Error: " + ex.Message);
            }
        }

        private void ExecuteClickTrade(string symbol, OrderAction action, OrderType type, double limitPrice)
        {
            ExecuteOrder(symbol, action, type, limitPrice);
        }
        #endregion
        
        #region Manual Execution
        private void ExecuteFlatten(string symbol)
        {
            Instrument instrument = (Instrument.FullName.Contains(symbol) || Instrument.MasterInstrument.Name == symbol) 
                                    ? Instrument : Instrument.GetInstrument(symbol);
            
            if (instrument == null) return;

            foreach (Account account in Account.All)
            {
                if (!account.Name.Contains(AccountPrefix)) continue;

                foreach (Order order in account.Orders.ToArray())
                {
                    if (order.Instrument.FullName == instrument.FullName &&
                        (order.OrderState == OrderState.Accepted || order.OrderState == OrderState.Working))
                    {
                        account.Cancel(new[] { order });
                    }
                }

                foreach (Position position in account.Positions.ToArray())
                {
                    if (position.Instrument.FullName == instrument.FullName &&
                        position.MarketPosition != MarketPosition.Flat)
                    {
                        int qty = Math.Abs(position.Quantity);
                        OrderAction exitAction = position.MarketPosition == MarketPosition.Long
                            ? OrderAction.Sell
                            : OrderAction.BuyToCover;

                        Order flattenOrder = account.CreateOrder(instrument, exitAction, OrderType.Market,
                            TimeInForce.Gtc, qty, 0, 0, "", "V9_Flatten", null);
                        account.Submit(new[] { flattenOrder });

                        Print(string.Format("V9_010: FLATTEN | {0} | {1}x {2}", account.Name, qty, symbol));
                    }
                }

                string key = account.Name + "_" + instrument.MasterInstrument.Name;
                if (tightTrailActive.ContainsKey(key))
                    tightTrailActive[key] = false;
            }
        }

        private void ExecuteTrim(string symbol, double trimPercent)
        {
            Instrument instrument = (Instrument.FullName.Contains(symbol) || Instrument.MasterInstrument.Name == symbol) 
                                    ? Instrument : Instrument.GetInstrument(symbol);
                                    
            if (instrument == null) return;

            foreach (Account account in Account.All)
            {
                if (!account.Name.Contains(AccountPrefix)) continue;

                foreach (Position position in account.Positions.ToArray())
                {
                    if (position.Instrument.FullName == instrument.FullName &&
                        position.MarketPosition != MarketPosition.Flat)
                    {
                        int currentQty = Math.Abs(position.Quantity);
                        int trimQty = (int)Math.Floor(currentQty * trimPercent);

                        if (trimQty < 1) trimQty = 1;
                        if (trimQty > currentQty) trimQty = currentQty;

                        OrderAction exitAction = position.MarketPosition == MarketPosition.Long
                            ? OrderAction.Sell
                            : OrderAction.BuyToCover;

                        string trimName = string.Format("V9_Trim{0}", (int)(trimPercent * 100));
                        Order trimOrder = account.CreateOrder(instrument, exitAction, OrderType.Market,
                            TimeInForce.Gtc, trimQty, 0, 0, "", trimName, null);
                        account.Submit(new[] { trimOrder });

                        Print(string.Format("V9_010: TRIM {0}% | {1} | {2}x {3} (was {4}x)",
                            (int)(trimPercent * 100), account.Name, trimQty, symbol, currentQty));

                        int remainingQty = currentQty - trimQty;
                        if (remainingQty > 0)
                        {
                            UpdateStopQuantity(account, instrument, remainingQty);
                        }
                    }
                }
            }
        }

        private void ExecuteBreakevenPlus(string symbol, double plusPoints)
        {
            Instrument instrument = (Instrument.FullName.Contains(symbol) || Instrument.MasterInstrument.Name == symbol) 
                                    ? Instrument : Instrument.GetInstrument(symbol);
                                    
            if (instrument == null) return;

            foreach (Account account in Account.All)
            {
                if (!account.Name.Contains(AccountPrefix)) continue;

                foreach (Position position in account.Positions.ToArray())
                {
                    if (position.Instrument.FullName == instrument.FullName &&
                        position.MarketPosition != MarketPosition.Flat)
                    {
                        double entryPrice = position.AveragePrice;
                        double newStopPrice;

                        if (position.MarketPosition == MarketPosition.Long)
                            newStopPrice = instrument.MasterInstrument.RoundToTickSize(entryPrice + plusPoints);
                        else
                            newStopPrice = instrument.MasterInstrument.RoundToTickSize(entryPrice - plusPoints);

                        ForceUpdateStop(account, instrument, newStopPrice);

                        Print(string.Format("V9_010: BE+{0} | {1} | {2} | Entry={3:F2} | NewStop={4:F2}",
                            plusPoints, account.Name, symbol, entryPrice, newStopPrice));
                    }
                }
            }
        }

        private void ExecuteTRENDEntry(string symbol)
        {
            Instrument instrument = (Instrument.FullName.Contains(symbol) || Instrument.MasterInstrument.Name == symbol) 
                                    ? Instrument : Instrument.GetInstrument(symbol);
            if (instrument == null) return;

            double currentPrice = instrument.MarketData.Last.Price;
            MarketPosition direction = currentPrice > lastEma9 ? MarketPosition.Long : MarketPosition.Short;
            
            double stopDistance = currentATR * 2.0; // Trend default
            ExecuteOrder(symbol, direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort);
            
            Print(string.Format("V9_010: EXEC TREND {0} for {1} (EMA Setup)", direction, symbol));
        }

        private void ExecuteRetestEntry(string symbol)
        {
            // V9.12: Retest logic centers on OR High/Low
            // Entry at breakout level, Stop at Mid
            ExecuteOrder(symbol, OrderAction.Buy); // Defaulting to Long for Retest Test
            Print(string.Format("V9_010: EXEC RETEST {0}", symbol));
        }

        private void CheckFFMAConditions()
        {
            if (Instrument == null || Instrument.MarketData.Last == null) return;
            
            double lastPrice = Instrument.MarketData.Last.Price;
            double dist = Math.Abs(lastPrice - lastEma9);
            
            // Logic: RSI > 80 (Short) / < 20 (Long) + Distance > 10pts
            if (dist > 15.0) 
            {
                Print(string.Format("V9_010: FFMA Alert! Dist={0:F2}", dist));
                // Auto-execute if armed
                // ExecuteOrder(...);
            }
        }
        private void UpdateStopQuantity(Account account, Instrument instrument, int newQuantity)
        {
            foreach (Order order in account.Orders.ToArray())
            {
                if (order.Instrument.FullName == instrument.FullName &&
                    order.Name.StartsWith("V9_Stop") &&
                    (order.OrderState == OrderState.Accepted || order.OrderState == OrderState.Working))
                {
                    account.Cancel(new[] { order });
                    Order newStopOrder = account.CreateOrder(instrument, order.OrderAction, OrderType.StopMarket,
                        TimeInForce.Gtc, newQuantity, 0, order.StopPrice, "", "V9_Stop", null);
                    account.Submit(new[] { newStopOrder });
                }
            }
        }

        private void ForceUpdateStop(Account account, Instrument instrument, double newStopPrice)
        {
            foreach (Order order in account.Orders.ToArray())
            {
                if (order.Instrument.FullName == instrument.FullName &&
                    order.Name.StartsWith("V9_Stop") &&
                    (order.OrderState == OrderState.Accepted || order.OrderState == OrderState.Working))
                {
                    account.Cancel(new[] { order });
                    Order newStopOrder = account.CreateOrder(instrument, order.OrderAction, OrderType.StopMarket,
                        TimeInForce.Gtc, order.Quantity, 0, newStopPrice, "", "V9_Stop_BE", null);
                    account.Submit(new[] { newStopOrder });
                }
            }
        }
        #endregion

        #region Trailing Logic
        private void ManageTightTrailing()
        {
            foreach (Account account in Account.All)
            {
                if (!account.Name.Contains(AccountPrefix)) continue;

                foreach (Position position in account.Positions.ToArray())
                {
                    if (position.Instrument.FullName != Instrument.FullName) continue;
                    if (position.MarketPosition == MarketPosition.Flat) continue;

                    string key = account.Name + "_" + position.Instrument.MasterInstrument.Name;
                    double lastPrice = position.Instrument.MarketData.Last.Price;
                    double unrealizedPts = position.MarketPosition == MarketPosition.Long
                        ? (lastPrice - position.AveragePrice)
                        : (position.AveragePrice - lastPrice);

                    if (!tightTrailActive.ContainsKey(key)) tightTrailActive[key] = false;

                    if (!tightTrailActive[key] && unrealizedPts >= TightTrailTrigger)
                    {
                        tightTrailActive[key] = true;
                        Print("V9_010: TIGHT TRAIL ACTIVE | " + account.Name);
                    }

                    if (tightTrailActive[key])
                    {
                        double newStopPrice = position.MarketPosition == MarketPosition.Long
                            ? lastPrice - TightTrailOffset
                            : lastPrice + TightTrailOffset;

                        UpdateTrailingStop(account, position.Instrument, newStopPrice);
                    }
                }
            }
        }

        private void UpdateTrailingStop(Account account, Instrument instrument, double newStopPrice)
        {
            foreach (Order order in account.Orders.ToArray())
            {
                if (order.Instrument.FullName == instrument.FullName &&
                    order.Name.StartsWith("V9_Stop") &&
                    order.OrderState == OrderState.Accepted)
                {
                    bool improvesRisk = order.OrderAction == OrderAction.Sell
                        ? (newStopPrice > order.StopPrice)
                        : (newStopPrice < order.StopPrice);

                    if (improvesRisk)
                    {
                        double roundedStop = instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                        account.Cancel(new[] { order });
                        Order newStopOrder = account.CreateOrder(instrument, order.OrderAction, OrderType.StopMarket,
                            TimeInForce.Gtc, order.Quantity, 0, roundedStop, "", "V9_Stop_Trail", null);
                        account.Submit(new[] { newStopOrder });
                    }
                }
            }
        }
        #endregion
    }
}
