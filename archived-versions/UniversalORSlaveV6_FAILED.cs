using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// V6 Slave Strategy - Listens for signals from Master and executes orders on assigned account.
    /// Each slave instance manages ONE account independently.
    /// </summary>
    public class UniversalORSlaveV6 : Strategy
    {
        #region Variables

        // Account assignment
        private Account assignedAccount;
        private bool accountValidated;

        // Position tracking
        private Dictionary<string, PositionInfo> activePositions;
        private Dictionary<string, Order> entryOrders;
        private Dictionary<string, Order> stopOrders;
        private Dictionary<string, Order> target1Orders;
        private Dictionary<string, Order> target2Orders;

        // Instrument info
        private double tickSize;
        private double pointValue;
        private int minContracts;
        private int maxContracts;

        // Risk management
        private double dailyPnL;
        private DateTime lastPnLReset;

        // Error handling
        private DateTime lastHeartbeat;
        private int consecutiveErrors;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // RAM optimization
        private StringBuilder sbPool;

        #endregion

        #region Position Info Class

        private class PositionInfo
        {
            public string SignalId;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;
            public int T2Contracts;
            public int T3Contracts;
            public int RemainingContracts;
            public double EntryPrice;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price;
            public double Target2Price;
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public bool IsRMATrade;
            public DateTime EntryTime;
            public bool BracketFailureDetected;
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Assigned Account Name", Description = "Name of the account this slave will trade (must match exactly)", Order = 1, GroupName = "1. Account Settings")]
        public string AssignedAccountName { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade ($)", Description = "Maximum dollar risk per trade", Order = 2, GroupName = "1. Account Settings")]
        public double RiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reduced Risk ($)", Description = "Reduced risk when stop > threshold", Order = 3, GroupName = "1. Account Settings")]
        public double ReducedRiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Threshold (Points)", Description = "Stop distance above which reduced risk is used", Order = 4, GroupName = "1. Account Settings")]
        public double StopThresholdPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Loss Limit ($)", Description = "Maximum daily loss before trading stops", Order = 5, GroupName = "1. Account Settings")]
        public double DailyLossLimit { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Min Contracts", Order = 6, GroupName = "2. Risk Management")]
        public int MESMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MES Max Contracts", Order = 7, GroupName = "2. Risk Management")]
        public int MESMaximum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Min Contracts", Order = 8, GroupName = "2. Risk Management")]
        public int MGCMinimum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MGC Max Contracts", Order = 9, GroupName = "2. Risk Management")]
        public int MGCMaximum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BE Trigger (Points)", Order = 1, GroupName = "3. Trailing Stops")]
        public double BreakEvenTriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BE Offset (Ticks)", Order = 2, GroupName = "3. Trailing Stops")]
        public int BreakEvenOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Trigger (Points)", Order = 3, GroupName = "3. Trailing Stops")]
        public double Trail1TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Distance (Points)", Order = 4, GroupName = "3. Trailing Stops")]
        public double Trail1DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Trigger (Points)", Order = 5, GroupName = "3. Trailing Stops")]
        public double Trail2TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Distance (Points)", Order = 6, GroupName = "3. Trailing Stops")]
        public double Trail2DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Trigger (Points)", Order = 7, GroupName = "3. Trailing Stops")]
        public double Trail3TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Distance (Points)", Order = 8, GroupName = "3. Trailing Stops")]
        public double Trail3DistancePoints { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V6 Slave Strategy - Executes signals from Master on assigned account";
                Name = "UniversalORSlaveV6";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 10;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.ImmediatelySubmit;
                TimeInForce = TimeInForce.Gtc;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                IsUnmanaged = true;

                // Account defaults
                AssignedAccountName = "Sim101";
                RiskPerTrade = 200;
                ReducedRiskPerTrade = 200;
                StopThresholdPoints = 5.0;
                DailyLossLimit = 500;

                // Risk defaults
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                // Trailing stop defaults
                BreakEvenTriggerPoints = 2.0;
                BreakEvenOffsetTicks = 1;
                Trail1TriggerPoints = 3.0;
                Trail1DistancePoints = 2.0;
                Trail2TriggerPoints = 4.0;
                Trail2DistancePoints = 1.5;
                Trail3TriggerPoints = 5.0;
                Trail3DistancePoints = 1.0;
            }
            else if (State == State.Configure)
            {
                activePositions = new Dictionary<string, PositionInfo>(4);
                entryOrders = new Dictionary<string, Order>(4);
                stopOrders = new Dictionary<string, Order>(4);
                target1Orders = new Dictionary<string, Order>(4);
                target2Orders = new Dictionary<string, Order>(4);
                sbPool = new StringBuilder(256);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;

                string symbol = Instrument.MasterInstrument.Name;
                if (symbol.Contains("MES") || symbol.Contains("ES"))
                {
                    minContracts = MESMinimum;
                    maxContracts = MESMaximum;
                }
                else if (symbol.Contains("MGC") || symbol.Contains("GC"))
                {
                    minContracts = MGCMinimum;
                    maxContracts = MGCMaximum;
                }
                else
                {
                    minContracts = 3;
                    maxContracts = 15;
                }

                // Validate account
                ValidateAccount();

                Print($"UniversalORSlaveV6 | Assigned Account: {AssignedAccountName} | {symbol} | Tick: {tickSize} | PV: ${pointValue}");
                Print($"Risk: ${RiskPerTrade} | Daily Loss Limit: ${DailyLossLimit}");
                Print("SLAVE MODE: Listening for signals from Master");
            }
            else if (State == State.Realtime)
            {
                if (accountValidated)
                {
                    // Subscribe to Master signals
                    SignalBroadcaster.OnTradeSignal += HandleTradeSignal;
                    SignalBroadcaster.OnFlattenAll += HandleFlattenSignal;
                    SignalBroadcaster.OnBreakevenRequest += HandleBreakevenSignal;

                    Print($"SLAVE REALTIME: Subscribed to Master signals | Account: {AssignedAccountName}");
                    Print(SignalBroadcaster.GetSubscriberCounts());

                    lastHeartbeat = DateTime.Now;
                    lastPnLReset = DateTime.Today;
                }
                else
                {
                    Print($"ERROR: Account '{AssignedAccountName}' not validated - slave will NOT trade");
                }
            }
            else if (State == State.Terminated)
            {
                // Unsubscribe from signals
                SignalBroadcaster.OnTradeSignal -= HandleTradeSignal;
                SignalBroadcaster.OnFlattenAll -= HandleFlattenSignal;
                SignalBroadcaster.OnBreakevenRequest -= HandleBreakevenSignal;

                // Clear references
                activePositions?.Clear();
                entryOrders?.Clear();
                stopOrders?.Clear();
                target1Orders?.Clear();
                target2Orders?.Clear();

                Print($"SLAVE TERMINATED: Unsubscribed from signals | Account: {AssignedAccountName}");
            }
        }

        #endregion

        #region Account Validation

        private void ValidateAccount()
        {
            try
            {
                // Find account by name
                foreach (Account acct in Account.All)
                {
                    if (acct.Name == AssignedAccountName)
                    {
                        assignedAccount = acct;
                        accountValidated = true;
                        Print($"Account validated: {AssignedAccountName} | Connection: {acct.Connection.Status}");
                        return;
                    }
                }

                Print($"ERROR: Account '{AssignedAccountName}' not found in NinjaTrader");
                Print("Available accounts:");
                foreach (Account acct in Account.All)
                {
                    Print($"  - {acct.Name}");
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR ValidateAccount: {ex.Message}");
            }
        }

        #endregion

        #region Signal Handlers

        private void HandleTradeSignal(object sender, SignalBroadcaster.TradeSignal signal)
        {
            try
            {
                // Check daily loss limit
                if (dailyPnL <= -DailyLossLimit)
                {
                    Print($"BLOCKED: Daily loss limit reached (${dailyPnL:F2}) - signal ignored");
                    return;
                }

                // Check account connection
                if (assignedAccount.Connection.Status != ConnectionStatus.Connected)
                {
                    Print($"BLOCKED: Account disconnected - signal ignored");
                    return;
                }

                Print($"SLAVE RECEIVED: {signal.SignalId} | {signal.Direction} @ {signal.EntryPrice:F2}");

                // Calculate position size for this account
                double stopDistance = Math.Abs(signal.EntryPrice - signal.StopPrice);
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);
                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // Calculate contract distribution
                int t1Qty, t2Qty, t3Qty;
                if (contracts == 1)
                {
                    t1Qty = 0;
                    t2Qty = 0;
                    t3Qty = 1;
                }
                else if (contracts == 2)
                {
                    t1Qty = 1;
                    t2Qty = 0;
                    t3Qty = 1;
                }
                else
                {
                    t1Qty = (int)Math.Floor(contracts * signal.T1Contracts / 100.0);
                    t2Qty = (int)Math.Floor(contracts * signal.T2Contracts / 100.0);
                    t3Qty = contracts - t1Qty - t2Qty;

                    if (t1Qty < 1 || t2Qty < 1 || t3Qty < 1)
                    {
                        t1Qty = 1;
                        t2Qty = 1;
                        t3Qty = contracts - 2;
                    }
                }

                // Create position info
                PositionInfo pos = new PositionInfo
                {
                    SignalId = signal.SignalId,
                    Direction = signal.Direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    RemainingContracts = contracts,
                    EntryPrice = signal.EntryPrice,
                    InitialStopPrice = signal.StopPrice,
                    CurrentStopPrice = signal.StopPrice,
                    Target1Price = signal.Target1Price,
                    Target2Price = signal.Target2Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = signal.EntryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = signal.IsRMA,
                    EntryTime = DateTime.Now,
                    BracketFailureDetected = false
                };

                activePositions[signal.SignalId] = pos;

                // Submit entry order (uses chart's assigned account)
                Order entryOrder;
                if (signal.IsRMA)
                {
                    // RMA = Market order
                    entryOrder = signal.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", signal.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", signal.SignalId);
                }
                else
                {
                    // OR = Stop Market (breakout entry)
                    entryOrder = signal.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, signal.EntryPrice, "", signal.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, signal.EntryPrice, "", signal.SignalId);
                }

                if (entryOrder != null)
                {
                    entryOrders[signal.SignalId] = entryOrder;
                    Print($"ENTRY ORDER SUBMITTED: {contracts} @ {signal.EntryPrice:F2} | Account: {AssignedAccountName}");
                }
                else
                {
                    Print($"ERROR: Entry order submission failed for {signal.SignalId}");
                    activePositions.Remove(signal.SignalId);
                    LogError("Entry order submission returned null");
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR HandleTradeSignal: {ex.Message}");
                LogError($"HandleTradeSignal exception: {ex.Message}");
                consecutiveErrors++;
            }
        }

        private void HandleFlattenSignal(object sender, SignalBroadcaster.FlattenSignal signal)
        {
            try
            {
                Print($"FLATTEN SIGNAL RECEIVED: {signal.Reason}");
                FlattenAllPositions();
            }
            catch (Exception ex)
            {
                Print($"ERROR HandleFlattenSignal: {ex.Message}");
            }
        }

        private void HandleBreakevenSignal(object sender, SignalBroadcaster.BreakevenSignal signal)
        {
            try
            {
                Print($"BREAKEVEN SIGNAL RECEIVED");
                // Move all active positions to breakeven
                foreach (var kvp in activePositions)
                {
                    MoveToBreakeven(kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR HandleBreakevenSignal: {ex.Message}");
            }
        }

        #endregion

        #region OnExecutionUpdate

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order == null) return;

            try
            {
                string signalId = execution.Order.Name;

                // Entry fill
                if (activePositions.ContainsKey(signalId) && !activePositions[signalId].EntryFilled)
                {
                    PositionInfo pos = activePositions[signalId];

                    if (execution.Order == entryOrders[signalId])
                    {
                        pos.EntryFilled = true;
                        pos.EntryPrice = execution.Price;
                        pos.ExtremePriceSinceEntry = execution.Price;

                        Print($"ENTRY FILLED: {signalId} | {quantity} @ {execution.Price:F2} | Account: {AssignedAccountName}");

                        // CRITICAL: Submit bracket orders immediately
                        SubmitBracketOrders(pos);
                    }
                }

                // Target fills
                if (activePositions.ContainsKey(signalId))
                {
                    PositionInfo pos = activePositions[signalId];

                    if (target1Orders.ContainsKey(signalId) && execution.Order == target1Orders[signalId])
                    {
                        pos.T1Filled = true;
                        pos.RemainingContracts -= quantity;
                        Print($"T1 FILLED: {quantity} @ {execution.Price:F2} | Remaining: {pos.RemainingContracts}");
                    }

                    if (target2Orders.ContainsKey(signalId) && execution.Order == target2Orders[signalId])
                    {
                        pos.T2Filled = true;
                        pos.RemainingContracts -= quantity;
                        Print($"T2 FILLED: {quantity} @ {execution.Price:F2} | Remaining: {pos.RemainingContracts}");
                    }

                    // Stop fill - position closed
                    if (stopOrders.ContainsKey(signalId) && execution.Order == stopOrders[signalId])
                    {
                        Print($"STOP FILLED: {quantity} @ {execution.Price:F2} | Position closed");
                        CleanupPosition(signalId);
                    }

                    // If all contracts filled, cleanup
                    if (pos.RemainingContracts <= 0)
                    {
                        Print($"All targets filled for {signalId} - cleaning up");
                        CleanupPosition(signalId);
                    }
                }

                consecutiveErrors = 0; // Reset error counter on successful execution
            }
            catch (Exception ex)
            {
                Print($"ERROR OnExecutionUpdate: {ex.Message}");
                LogError($"OnExecutionUpdate exception: {ex.Message}");
            }
        }

        #endregion

        #region Bracket Order Submission

        private void SubmitBracketOrders(PositionInfo pos)
        {
            try
            {
                // Submit stop order
                Order stopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, pos.InitialStopPrice, "", "Stop_" + pos.SignalId)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, pos.InitialStopPrice, "", "Stop_" + pos.SignalId);

                if (stopOrder == null)
                {
                    // CRITICAL: Stop order failed - emergency flatten
                    Print($"CRITICAL: Stop order failed for {pos.SignalId} - EMERGENCY FLATTEN");
                    EmergencyFlatten(pos);
                    return;
                }

                stopOrders[pos.SignalId] = stopOrder;

                // Submit T1 if applicable
                if (pos.T1Contracts > 0)
                {
                    Order t1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + pos.SignalId);

                    if (t1Order != null)
                        target1Orders[pos.SignalId] = t1Order;
                }

                // Submit T2 if applicable
                if (pos.T2Contracts > 0)
                {
                    Order t2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + pos.SignalId);

                    if (t2Order != null)
                        target2Orders[pos.SignalId] = t2Order;
                }

                pos.BracketSubmitted = true;
                Print($"BRACKET ORDERS SUBMITTED: Stop @ {pos.InitialStopPrice:F2} | T1: {pos.T1Contracts}@{pos.Target1Price:F2} | T2: {pos.T2Contracts}@{pos.Target2Price:F2}");
            }
            catch (Exception ex)
            {
                Print($"ERROR SubmitBracketOrders: {ex.Message}");
                LogError($"SubmitBracketOrders exception: {ex.Message}");
                EmergencyFlatten(pos);
            }
        }

        #endregion

        #region Emergency Flatten

        private void EmergencyFlatten(PositionInfo pos)
        {
            try
            {
                Print($"EMERGENCY FLATTEN: {pos.SignalId} | {pos.TotalContracts} contracts");

                Order emergencyOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.TotalContracts, 0, 0, "", "Emergency_" + pos.SignalId)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.TotalContracts, 0, 0, "", "Emergency_" + pos.SignalId);

                if (emergencyOrder != null)
                {
                    Print($"Emergency exit order submitted");
                    LogCriticalError($"Emergency flatten executed for {pos.SignalId} - bracket order failure");
                }

                CleanupPosition(pos.SignalId);
            }
            catch (Exception ex)
            {
                Print($"ERROR EmergencyFlatten: {ex.Message}");
                LogCriticalError($"EmergencyFlatten failed: {ex.Message}");
            }
        }

        #endregion

        #region Position Management

        private void FlattenAllPositions()
        {
            foreach (var kvp in new Dictionary<string, PositionInfo>(activePositions))
            {
                FlattenPosition(kvp.Value);
            }
        }

        private void FlattenPosition(PositionInfo pos)
        {
            try
            {
                if (pos.RemainingContracts > 0)
                {
                    Order flattenOrder = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalId);

                    Print($"FLATTEN: {pos.SignalId} | {pos.RemainingContracts} contracts");
                }

                CleanupPosition(pos.SignalId);
            }
            catch (Exception ex)
            {
                Print($"ERROR FlattenPosition: {ex.Message}");
            }
        }

        private void CleanupPosition(string signalId)
        {
            // Cancel all pending orders
            if (entryOrders.ContainsKey(signalId))
            {
                CancelOrder(entryOrders[signalId]);
                entryOrders.Remove(signalId);
            }

            if (stopOrders.ContainsKey(signalId))
            {
                CancelOrder(stopOrders[signalId]);
                stopOrders.Remove(signalId);
            }

            if (target1Orders.ContainsKey(signalId))
            {
                CancelOrder(target1Orders[signalId]);
                target1Orders.Remove(signalId);
            }

            if (target2Orders.ContainsKey(signalId))
            {
                CancelOrder(target2Orders[signalId]);
                target2Orders.Remove(signalId);
            }

            activePositions.Remove(signalId);
        }

        private void MoveToBreakeven(PositionInfo pos)
        {
            if (!pos.EntryFilled || !stopOrders.ContainsKey(pos.SignalId)) return;

            try
            {
                double bePrice = pos.EntryPrice + (pos.Direction == MarketPosition.Long ? BreakEvenOffsetTicks * tickSize : -BreakEvenOffsetTicks * tickSize);

                ChangeOrder(stopOrders[pos.SignalId], pos.RemainingContracts, 0, bePrice);
                pos.CurrentStopPrice = bePrice;
                Print($"BREAKEVEN: {pos.SignalId} | Stop moved to {bePrice:F2}");
            }
            catch (Exception ex)
            {
                Print($"ERROR MoveToBreakeven: {ex.Message}");
            }
        }

        #endregion

        #region OnBarUpdate - Trailing Stops

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                // Update heartbeat
                lastHeartbeat = DateTime.Now;

                // Reset daily P&L if new day
                if (DateTime.Today != lastPnLReset)
                {
                    dailyPnL = 0;
                    lastPnLReset = DateTime.Today;
                    Print($"Daily P&L reset for new trading day");
                }

                // Manage trailing stops
                foreach (var kvp in activePositions)
                {
                    ManageTrailingStop(kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR OnBarUpdate: {ex.Message}");
            }
        }

        private void ManageTrailingStop(PositionInfo pos)
        {
            if (!pos.EntryFilled || !stopOrders.ContainsKey(pos.SignalId)) return;

            try
            {
                double currentPrice = Close[0];

                // Update extreme price
                if (pos.Direction == MarketPosition.Long)
                {
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, currentPrice);
                }
                else
                {
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, currentPrice);
                }

                double profitPoints = pos.Direction == MarketPosition.Long
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                // Breakeven
                if (pos.CurrentTrailLevel == 0 && profitPoints >= BreakEvenTriggerPoints)
                {
                    double bePrice = pos.EntryPrice + (pos.Direction == MarketPosition.Long ? BreakEvenOffsetTicks * tickSize : -BreakEvenOffsetTicks * tickSize);
                    ChangeOrder(stopOrders[pos.SignalId], pos.RemainingContracts, 0, bePrice);
                    pos.CurrentStopPrice = bePrice;
                    pos.CurrentTrailLevel = 1;
                    Print($"TRAIL BE: {pos.SignalId} | Stop @ {bePrice:F2}");
                }
                // Trail 1
                else if (pos.CurrentTrailLevel == 1 && profitPoints >= Trail1TriggerPoints)
                {
                    double newStop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;

                    if ((pos.Direction == MarketPosition.Long && newStop > pos.CurrentStopPrice) ||
                        (pos.Direction == MarketPosition.Short && newStop < pos.CurrentStopPrice))
                    {
                        ChangeOrder(stopOrders[pos.SignalId], pos.RemainingContracts, 0, newStop);
                        pos.CurrentStopPrice = newStop;
                        pos.CurrentTrailLevel = 2;
                        Print($"TRAIL 1: {pos.SignalId} | Stop @ {newStop:F2}");
                    }
                }
                // Trail 2
                else if (pos.CurrentTrailLevel == 2 && profitPoints >= Trail2TriggerPoints)
                {
                    double newStop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;

                    if ((pos.Direction == MarketPosition.Long && newStop > pos.CurrentStopPrice) ||
                        (pos.Direction == MarketPosition.Short && newStop < pos.CurrentStopPrice))
                    {
                        ChangeOrder(stopOrders[pos.SignalId], pos.RemainingContracts, 0, newStop);
                        pos.CurrentStopPrice = newStop;
                        pos.CurrentTrailLevel = 3;
                        Print($"TRAIL 2: {pos.SignalId} | Stop @ {newStop:F2}");
                    }
                }
                // Trail 3
                else if (pos.CurrentTrailLevel == 3 && profitPoints >= Trail3TriggerPoints)
                {
                    double newStop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;

                    if ((pos.Direction == MarketPosition.Long && newStop > pos.CurrentStopPrice) ||
                        (pos.Direction == MarketPosition.Short && newStop < pos.CurrentStopPrice))
                    {
                        ChangeOrder(stopOrders[pos.SignalId], pos.RemainingContracts, 0, newStop);
                        pos.CurrentStopPrice = newStop;
                        Print($"TRAIL 3: {pos.SignalId} | Stop @ {newStop:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"ERROR ManageTrailingStop: {ex.Message}");
            }
        }

        #endregion

        #region Error Logging

        private void LogError(string message)
        {
            Print($"ERROR LOG: {message}");
            // Could write to file here if needed
        }

        private void LogCriticalError(string message)
        {
            Print($"CRITICAL ERROR: {message}");
            // Could send alert/email here
        }

        #endregion
    }
}
