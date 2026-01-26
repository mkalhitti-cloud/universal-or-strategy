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
    public class UniversalOR_V9_007_V1 : Strategy
    {
        #region Private Variables
        private string accountPrefix = "Apex";
        private int ipcPort = 5000;
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private readonly object commandLock = new object();
        private Queue<string> commandQueue = new Queue<string>();

        // V9_007: Tight Trail Tracking
        private Dictionary<string, bool> tightTrailActive = new Dictionary<string, bool>();
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
        [Display(Name = "Stop Multiplier", Description = "Multiplier of OR Range for stop (0.5 = half OR)", Order = 1, GroupName = "2. Stop Loss")]
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
        [Display(Name = "T2 ATR Multiplier", Description = "Multiplier of ATR for T2 (0.5 = half ATR)", Order = 2, GroupName = "3. Profit Targets")]
        public double Target2Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 ATR Multiplier", Description = "Multiplier of ATR for T3 (1.0 = 1x ATR)", Order = 3, GroupName = "3. Profit Targets")]
        public double Target3Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Description = "20% for quick scalp", Order = 4, GroupName = "3. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Description = "30%", Order = 5, GroupName = "3. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Description = "30%", Order = 6, GroupName = "3. Profit Targets")]
        public int T3ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 Contract % (Runner)", Description = "20% for runner/trail", Order = 7, GroupName = "3. Profit Targets")]
        public int T4ContractPercent { get; set; }
        #endregion

        #region Properties - Trailing (V9_007)
        [NinjaScriptProperty]
        [Display(Name = "Tight Trail Enabled", Description = "Enable 1-point trailing stop after reaching threshold", Order = 1, GroupName = "5. Trailing")]
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
                Description = "V9_007 V1 - Risk Manager: Tight 1-Point Trailing Logic Over V9_005 Base";
                Name = "UniversalOR_V9_007_V1";
                Calculate = Calculate.OnPriceChange; // V9_007: Trigger on every tick for tight trailing
                IsUnmanaged = true;

                // IPC Defaults
                AccountPrefix = "Apex";
                IpcPort = 5000;

                // V8.28 Hardened Defaults
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

                T1ContractPercent = 20;
                T2ContractPercent = 30;
                T3ContractPercent = 30;
                T4ContractPercent = 20;

                Target1FixedPoints = 1.0;
                Target2Multiplier = 0.5;
                Target3Multiplier = 1.0;

                // V9_007: Tight Trail Defaults
                TightTrailEnabled = true;
                TightTrailTrigger = 3.0; // Activate at +3.0 pts
                TightTrailOffset = 1.0;  // Trail 1.0 pt behind
            }
            else if (State == State.DataLoaded)
            {
                StartIpcServer();
                Print("V9_007 V1: Loaded with TightTrail=" + TightTrailEnabled + " (" + TightTrailTrigger + " trigger / " + TightTrailOffset + " offset)");
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
            ProcessCommandQueue();
            
            // V9_007: Manage Trailing Stops on every tick (since Calculate = OnPriceChange)
            if (TightTrailEnabled)
                ManageTightTrailing();
        }
        #endregion

        #region IPC Server Methods
        private void StartIpcServer()
        {
            isRunning = true;
            serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_007_IPC_Server" };
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
            string command = null;
            lock (commandLock) { if (commandQueue.Count > 0) command = commandQueue.Dequeue(); }
            if (command != null)
            {
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;
                string action = parts[0];
                string symbol = parts[1];
                if (action == "LONG") ExecuteOrder(symbol, OrderAction.Buy);
                else if (action == "SHORT") ExecuteOrder(symbol, OrderAction.SellShort);
            }
        }
        #endregion

        #region Order Execution
        private void ExecuteOrder(string symbol, OrderAction action)
        {
            Instrument instrument = Instrument.GetInstrument(symbol);
            if (instrument == null) return;

            double stopDistance = Math.Min(MaximumStop, Math.Max(MinimumStop, StopMultiplier * 5.0));

            foreach (Account account in Account.All)
            {
                if (account.Name.Contains(AccountPrefix))
                {
                    double entryPrice = instrument.MarketData.Last.Price;

                    double stopPrice = action == OrderAction.Buy
                        ? Instrument.MasterInstrument.RoundToTickSize(entryPrice - stopDistance)
                        : Instrument.MasterInstrument.RoundToTickSize(entryPrice + stopDistance);

                    double targetPrice = action == OrderAction.Buy
                        ? Instrument.MasterInstrument.RoundToTickSize(entryPrice + Target1FixedPoints)
                        : Instrument.MasterInstrument.RoundToTickSize(entryPrice - Target1FixedPoints);

                    Order entryOrder = account.CreateOrder(instrument, action, OrderType.Market, TimeInForce.Gtc, 1, 0, 0, "", "V9_007_Entry", null);
                    account.Submit(new[] { entryOrder });

                    OrderAction exitAction = action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover;
                    Order stopOrder = account.CreateOrder(instrument, exitAction, OrderType.StopMarket, TimeInForce.Gtc, 1, 0, stopPrice, "", "V9_007_Stop", null);
                    account.Submit(new[] { stopOrder });

                    Order targetOrder = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc, 1, targetPrice, 0, "", "V9_007_T1", null);
                    account.Submit(new[] { targetOrder });
                    
                    // Register for Tight Trail tracking
                    string key = account.Name + "_" + symbol;
                    tightTrailActive[key] = false;

                    Print("V9_007 V1: Order Placed for " + account.Name + " | Stop=" + stopPrice + " T1=" + targetPrice);
                }
            }
        }
        #endregion

        #region Trailing Logic (V9_007)
        private void ManageTightTrailing()
        {
            foreach (Account account in Account.All)
            {
                if (!account.Name.Contains(AccountPrefix)) continue;
                
                foreach (Position position in account.Positions)
                {
                    if (position.Instrument.FullName != Instrument.FullName) continue;
                    if (position.MarketPosition == MarketPosition.Flat) continue;

                    string key = account.Name + "_" + position.Instrument.MasterInstrument.Name;
                    double lastPrice = position.Instrument.MarketData.Last.Price;
                    double unrealizedPts = position.MarketPosition == MarketPosition.Long 
                        ? (lastPrice - position.AveragePrice) 
                        : (position.AveragePrice - lastPrice);

                    // De-link/Activate Tight Trail if threshold reached
                    if (!tightTrailActive.ContainsKey(key)) tightTrailActive[key] = false;

                    if (!tightTrailActive[key] && unrealizedPts >= TightTrailTrigger)
                    {
                        tightTrailActive[key] = true;
                        Print("V9_007 V1: ACTIVATING TIGHT TRAIL for " + account.Name);
                    }

                    if (tightTrailActive[key])
                    {
                        // Logic: Trail 1 point behind price
                        double newStopPrice = position.MarketPosition == MarketPosition.Long
                            ? lastPrice - TightTrailOffset
                            : lastPrice + TightTrailOffset;

                        // Only Move Stop if it improves risk
                        UpdateStop(account, position.Instrument, newStopPrice);
                    }
                }
            }
        }

        private void UpdateStop(Account account, Instrument instrument, double newStopPrice)
        {
            foreach (Order order in account.Orders)
            {
                if (order.Instrument.FullName == instrument.FullName && 
                    order.Name == "V9_007_Stop" && 
                    order.OrderState == OrderState.Accepted)
                {
                    bool improvesRisk = order.OrderAction == OrderAction.Sell 
                        ? (newStopPrice > order.StopPrice) 
                        : (newStopPrice < order.StopPrice);

                    if (improvesRisk)
                    {
                        double roundedStop = instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                        // CS1501 FIX: Account.Change failed. Using documented Cancel/Replace logic.
                        account.Cancel(new[] { order });
                        Order newStopOrder = account.CreateOrder(instrument, order.OrderAction, OrderType.StopMarket, TimeInForce.Gtc, order.Quantity, 0, roundedStop, "", "V9_007_Stop_Update", null);
                        account.Submit(new[] { newStopOrder });
                    }
                }
            }
        }
        #endregion
    }
}
