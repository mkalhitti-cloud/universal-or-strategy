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
    public class UniversalOR_V9_006_V1 : Strategy
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

        // V9_006: ATR Indicator for dynamic targets
        private ATR atrIndicator;
        private double currentATR = 0;

        // V9_006: Position tracking for multi-target management
        private Dictionary<string, PositionInfo> activePositions = new Dictionary<string, PositionInfo>();
        #endregion

        #region V9_006: Position Info Class
        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;
            public int T2Contracts;
            public int T3Contracts;
            public int T4Contracts;
            public int T5Contracts; // V9_006: 5th target for 5-target mode
            public double EntryPrice;
            public double StopPrice;
            public double Target1Price;
            public double Target2Price;
            public double Target3Price;
            public double Target4Price; // V9_006: T4 for 5-target mode (T4 in 4-target is runner)
            public double Target5Price; // V9_006: T5 runner for 5-target mode
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;
            public bool T4Filled;
            public bool T5Filled;
        }
        #endregion

        #region Properties - Target Mode (V9_006)
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
        [Display(Name = "T4 ATR Multiplier", Description = "V9_006: Multiplier of ATR for T4 (5-Target Mode only, 1.5x)", Order = 4, GroupName = "3. Profit Targets")]
        public double Target4Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T5 ATR Multiplier", Description = "V9_006: Multiplier of ATR for T5 (5-Target Mode only, 2.0x)", Order = 5, GroupName = "3. Profit Targets")]
        public double Target5Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Period", Description = "Period for ATR calculation", Order = 6, GroupName = "3. Profit Targets")]
        [Range(5, 50)]
        public int ATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T1 Contract %", Description = "20% for quick scalp", Order = 10, GroupName = "3. Profit Targets")]
        public int T1ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Contract %", Description = "30%", Order = 11, GroupName = "3. Profit Targets")]
        public int T2ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Contract %", Description = "30%", Order = 12, GroupName = "3. Profit Targets")]
        public int T3ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 Contract % (Runner/Target)", Description = "20% runner (4-mode) or target (5-mode)", Order = 13, GroupName = "3. Profit Targets")]
        public int T4ContractPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T5 Contract % (Runner)", Description = "V9_006: Runner % for 5-Target Mode", Order = 14, GroupName = "3. Profit Targets")]
        public int T5ContractPercent { get; set; }
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
                Description = "V9_006 V1 - Multi-Target Architect: 4/5 Target Mode Toggle with ATR-Based Targets";
                Name = "UniversalOR_V9_006_V1";
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

                // V9_006: Target Mode Default (4-Target)
                TargetMode = 4;

                // V9_006: ATR Period
                ATRPeriod = 14;

                // 4-Target Mode Defaults (20/30/30/20)
                T1ContractPercent = 20;
                T2ContractPercent = 30;
                T3ContractPercent = 30;
                T4ContractPercent = 20;
                T5ContractPercent = 10; // Only used in 5-target mode

                // Target Multipliers
                Target1FixedPoints = 1.0;
                Target2Multiplier = 0.5;  // 0.5x ATR
                Target3Multiplier = 1.0;  // 1.0x ATR
                Target4Multiplier = 1.5;  // 1.5x ATR (5-target mode)
                Target5Multiplier = 2.0;  // 2.0x ATR (5-target mode runner)

                // V9_007: Tight Trail Defaults
                TightTrailEnabled = true;
                TightTrailTrigger = 3.0; // Activate at +3.0 pts
                TightTrailOffset = 1.0;  // Trail 1.0 pt behind
            }
            else if (State == State.Configure)
            {
                // V9_006: Add ATR indicator
                atrIndicator = ATR(ATRPeriod);
            }
            else if (State == State.DataLoaded)
            {
                StartIpcServer();
                string modeDesc = TargetMode == 4 ? "4-Target (20/30/30/20)" : "5-Target (Customizable)";
                Print("V9_006 V1: Loaded | Mode=" + modeDesc + " | TightTrail=" + TightTrailEnabled);
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
            // V9_006: Update ATR
            if (CurrentBar > ATRPeriod)
                currentATR = atrIndicator[0];

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
            serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_006_IPC_Server" };
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

        #region Order Execution (V9_006: Multi-Target)
        private void ExecuteOrder(string symbol, OrderAction action)
        {
            Instrument instrument = Instrument.GetInstrument(symbol);
            if (instrument == null) return;

            // V9_006: Get ATR-based stop distance (capped at MaximumStop)
            double stopDistance = Math.Min(MaximumStop, Math.Max(MinimumStop, currentATR * StopMultiplier));
            if (currentATR <= 0) stopDistance = MinimumStop; // Fallback if ATR not ready

            foreach (Account account in Account.All)
            {
                if (account.Name.Contains(AccountPrefix))
                {
                    double entryPrice = instrument.MarketData.Last.Price;
                    double pointValue = instrument.MasterInstrument.PointValue;

                    // V9_006: Determine min/max contracts based on instrument
                    int minContracts = symbol.Contains("MES") ? MESMinimum : MGCMinimum;
                    int maxContracts = symbol.Contains("MES") ? MESMaximum : MGCMaximum;

                    // V9_006: Calculate position size
                    double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                    double stopDistanceInDollars = stopDistance * pointValue;
                    int totalContracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);
                    totalContracts = Math.Max(minContracts, Math.Min(totalContracts, maxContracts));

                    // V9_006: Calculate target prices using ATR
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

                    // V9_006: Calculate contract distribution based on TargetMode
                    int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                    CalculateContractDistribution(totalContracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                    // Create position info
                    string timestamp = DateTime.Now.ToString("HHmmss");
                    string signalName = action == OrderAction.Buy ? "V9_006_Long" : "V9_006_Short";
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

                    // Submit Entry Order
                    Order entryOrder = account.CreateOrder(instrument, action, OrderType.Market, TimeInForce.Gtc, totalContracts, 0, 0, "", entryName, null);
                    account.Submit(new[] { entryOrder });

                    // Submit Stop Order
                    OrderAction exitAction = action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover;
                    Order stopOrder = account.CreateOrder(instrument, exitAction, OrderType.StopMarket, TimeInForce.Gtc, totalContracts, 0, stopPrice, "", "V9_006_Stop", null);
                    account.Submit(new[] { stopOrder });

                    // V9_006: Submit Target Orders Based on Mode
                    SubmitTargetOrders(account, instrument, exitAction, pos);

                    // Register for Tight Trail tracking
                    string key = account.Name + "_" + symbol;
                    tightTrailActive[key] = false;

                    // Log the trade
                    string modeStr = TargetMode == 4 ? "4-TARGET" : "5-TARGET";
                    Print(string.Format("V9_006 V1 [{0}]: {1} | {2}x @ {3:F2} | Stop={4:F2} | ATR={5:F2}",
                        modeStr, account.Name, totalContracts, entryPrice, stopPrice, currentATR));
                    Print(string.Format("  Targets: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2}",
                        t1Qty, t1Price, t2Qty, t2Price, t3Qty, t3Price));

                    if (TargetMode == 5)
                        Print(string.Format("  Extended: T4:{0}@{1:F2} | T5:{2}@trail", t4Qty, t4Price, t5Qty));
                    else
                        Print(string.Format("  Runner: T4:{0}@trail", t4Qty));
                }
            }
        }

        private void CalculateContractDistribution(int totalContracts, out int t1, out int t2, out int t3, out int t4, out int t5)
        {
            t5 = 0; // Default: not used in 4-target mode

            if (TargetMode == 4)
            {
                // 4-Target Mode: 20/30/30/20 (T4 is runner)
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
                // 5-Target Mode: User Customizable (T5 is runner)
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
            // T1: Always submit if quantity > 0
            if (pos.T1Contracts > 0)
            {
                Order t1Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T1Contracts, pos.Target1Price, 0, "", "V9_006_T1", null);
                account.Submit(new[] { t1Order });
            }

            // T2: Submit if quantity > 0
            if (pos.T2Contracts > 0)
            {
                Order t2Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T2Contracts, pos.Target2Price, 0, "", "V9_006_T2", null);
                account.Submit(new[] { t2Order });
            }

            // T3: Submit if quantity > 0
            if (pos.T3Contracts > 0)
            {
                Order t3Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T3Contracts, pos.Target3Price, 0, "", "V9_006_T3", null);
                account.Submit(new[] { t3Order });
            }

            // V9_006: T4 behavior depends on mode
            if (TargetMode == 5 && pos.T4Contracts > 0)
            {
                // 5-Target Mode: T4 is a limit target, T5 is the runner
                Order t4Order = account.CreateOrder(instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    pos.T4Contracts, pos.Target4Price, 0, "", "V9_006_T4", null);
                account.Submit(new[] { t4Order });
            }
            // In 4-Target Mode, T4 is the runner (no limit order, trails with stop)
            // In 5-Target Mode, T5 is the runner (no limit order, trails with stop)
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
                        Print("V9_006 V1: ACTIVATING TIGHT TRAIL for " + account.Name);
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
                    order.Name == "V9_006_Stop" &&
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
                        Order newStopOrder = account.CreateOrder(instrument, order.OrderAction, OrderType.StopMarket, TimeInForce.Gtc, order.Quantity, 0, roundedStop, "", "V9_006_Stop_Update", null);
                        account.Submit(new[] { newStopOrder });
                    }
                }
            }
        }
        #endregion
    }
}
