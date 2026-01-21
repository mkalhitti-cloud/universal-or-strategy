using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// V7 Slave Strategy - Ultra-lightweight trade copier
    /// Receives signals from V7 Master and executes identical trades
    /// NO UI, NO indicators, NO calculations - just order execution
    /// Designed to run from Strategies tab (headless, low RAM)
    /// </summary>
    public class UniversalORSlaveV7 : Strategy
    {
        #region Variables

        // Instrument info
        private double tickSize;
        private double pointValue;

        // Position tracking - simplified
        private Dictionary<string, SlavePosition> activePositions;

        // Slave identification
        private string slaveId;

        #endregion

        #region Position Class

        private class SlavePosition
        {
            public string SignalId;
            public MarketPosition Direction;
            public int Contracts;
            public double EntryPrice;
            public double StopPrice;
            public double Target1Price;
            public double Target2Price;
            public bool EntryFilled;
            public Order EntryOrder;
            public Order StopOrder;
            public Order Target1Order;
            public Order Target2Order;
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade ($)", Description = "Dollar risk per trade", Order = 1, GroupName = "1. Risk")]
        public double RiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Contracts", Order = 2, GroupName = "1. Risk")]
        public int MinContracts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Contracts", Order = 3, GroupName = "1. Risk")]
        public int MaxContracts { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V7 Slave - Ultra-light trade copier (no UI, runs headless)";
                Name = "UniversalORSlaveV7";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 5;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = false;
                StartBehavior = StartBehavior.ImmediatelySubmit;
                TimeInForce = TimeInForce.Gtc;
                IsUnmanaged = true;

                // Risk defaults
                RiskPerTrade = 200;
                MinContracts = 1;
                MaxContracts = 10;
            }
            else if (State == State.Configure)
            {
                activePositions = new Dictionary<string, SlavePosition>(4);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;
                slaveId = Account.Name + "_" + Instrument.MasterInstrument.Name;

                Print($"SLAVE V7 | {slaveId} | Tick: {tickSize} | PV: ${pointValue}");
                Print($"SLAVE V7 | Risk: ${RiskPerTrade} | Contracts: {MinContracts}-{MaxContracts}");
            }
            else if (State == State.Realtime)
            {
                // Subscribe to signals using event handlers
                SignalBroadcaster.OnTradeSignal += OnTradeSignalHandler;
                SignalBroadcaster.OnFlattenAll += OnFlattenHandler;
                SignalBroadcaster.OnBreakevenRequest += OnBreakevenHandler;

                Print($"SLAVE V7 | {slaveId} | SUBSCRIBED - Listening for Master signals");
                Print(SignalBroadcaster.GetSubscriberCounts());
            }
            else if (State == State.Terminated)
            {
                // Unsubscribe from events
                SignalBroadcaster.OnTradeSignal -= OnTradeSignalHandler;
                SignalBroadcaster.OnFlattenAll -= OnFlattenHandler;
                SignalBroadcaster.OnBreakevenRequest -= OnBreakevenHandler;

                activePositions?.Clear();
                Print($"SLAVE V7 | {slaveId} | TERMINATED");
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            // Minimal processing - just keep strategy alive
            if (BarsInProgress != 0) return;
        }

        #endregion

        #region Signal Handlers

        private void OnTradeSignalHandler(object sender, SignalBroadcaster.TradeSignal signal)
        {
            try
            {
                // V7.1: Filter by instrument - only process signals for our instrument
                string myInstrument = Instrument.MasterInstrument.Name;
                if (!string.IsNullOrEmpty(signal.Instrument) && signal.Instrument != myInstrument)
                {
                    Print($"SLAVE V7 | {slaveId} | IGNORED signal for {signal.Instrument} (I trade {myInstrument})");
                    return;
                }

                Print($"SLAVE V7 | {slaveId} | SIGNAL RECEIVED: {signal.SignalId}");

                // Calculate position size based on risk
                double stopDistance = Math.Abs(signal.EntryPrice - signal.StopPrice);
                double stopDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(RiskPerTrade / stopDollars);
                contracts = Math.Max(MinContracts, Math.Min(contracts, MaxContracts));

                // Create position record
                var pos = new SlavePosition
                {
                    SignalId = signal.SignalId,
                    Direction = signal.Direction,
                    Contracts = contracts,
                    EntryPrice = signal.EntryPrice,
                    StopPrice = signal.StopPrice,
                    Target1Price = signal.Target1Price,
                    Target2Price = signal.Target2Price,
                    EntryFilled = false
                };

                activePositions[signal.SignalId] = pos;

                // Submit entry order
                Order entryOrder;
                if (signal.IsRMA)
                {
                    // RMA uses limit orders
                    entryOrder = signal.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, signal.EntryPrice, 0, "", signal.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, signal.EntryPrice, 0, "", signal.SignalId);
                }
                else
                {
                    // OR uses stop market (breakout)
                    entryOrder = signal.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, signal.EntryPrice, "", signal.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, signal.EntryPrice, "", signal.SignalId);
                }

                pos.EntryOrder = entryOrder;

                Print($"SLAVE V7 | {slaveId} | ORDER SUBMITTED: {signal.SignalId} | {contracts} @ {signal.EntryPrice:F2}");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V7 ERROR OnTradeSignalHandler: {ex.Message}");
            }
        }

        private void OnFlattenHandler(object sender, SignalBroadcaster.FlattenSignal signal)
        {
            try
            {
                Print($"SLAVE V7 | {slaveId} | FLATTEN RECEIVED: {signal.Reason}");

                // Cancel all pending orders
                foreach (var kvp in activePositions)
                {
                    var pos = kvp.Value;
                    if (pos.EntryOrder != null && pos.EntryOrder.OrderState == OrderState.Working)
                        CancelOrder(pos.EntryOrder);
                    if (pos.StopOrder != null && pos.StopOrder.OrderState == OrderState.Working)
                        CancelOrder(pos.StopOrder);
                    if (pos.Target1Order != null && pos.Target1Order.OrderState == OrderState.Working)
                        CancelOrder(pos.Target1Order);
                    if (pos.Target2Order != null && pos.Target2Order.OrderState == OrderState.Working)
                        CancelOrder(pos.Target2Order);
                }

                // Close any open positions
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, Position.Quantity, 0, 0, "", "FlattenSlave");
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, Position.Quantity, 0, 0, "", "FlattenSlave");
                }

                activePositions.Clear();
                Print($"SLAVE V7 | {slaveId} | FLATTENED");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V7 ERROR OnFlattenHandler: {ex.Message}");
            }
        }

        private void OnBreakevenHandler(object sender, SignalBroadcaster.BreakevenSignal signal)
        {
            try
            {
                Print($"SLAVE V7 | {slaveId} | BREAKEVEN RECEIVED");

                foreach (var kvp in activePositions)
                {
                    var pos = kvp.Value;
                    if (!pos.EntryFilled) continue;

                    // Move stop to breakeven (entry price + 1 tick buffer)
                    double bePrice = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + tickSize
                        : pos.EntryPrice - tickSize;

                    // Cancel current stop and submit new one at breakeven
                    if (pos.StopOrder != null && pos.StopOrder.OrderState == OrderState.Working)
                    {
                        CancelOrder(pos.StopOrder);

                        int qty = pos.Direction == MarketPosition.Long
                            ? Position.Quantity
                            : Position.Quantity;

                        Order newStop = pos.Direction == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, qty, 0, bePrice, "", "BEStop_" + pos.SignalId)
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, qty, 0, bePrice, "", "BEStop_" + pos.SignalId);

                        pos.StopOrder = newStop;
                        pos.StopPrice = bePrice;

                        Print($"SLAVE V7 | {slaveId} | STOP MOVED TO BE: {bePrice:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V7 ERROR OnBreakevenHandler: {ex.Message}");
            }
        }

        #endregion

        #region Order Management

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            if (order == null) return;

            try
            {
                // Find the position this order belongs to
                foreach (var kvp in activePositions)
                {
                    var pos = kvp.Value;

                    // Entry fill
                    if (order == pos.EntryOrder && orderState == OrderState.Filled)
                    {
                        pos.EntryFilled = true;
                        pos.EntryPrice = averageFillPrice;

                        Print($"SLAVE V7 | {slaveId} | ENTRY FILLED: {pos.SignalId} @ {averageFillPrice:F2}");

                        // Submit stop and targets
                        SubmitBracketOrders(pos);
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V7 ERROR OnOrderUpdate: {ex.Message}");
            }
        }

        private void SubmitBracketOrders(SlavePosition pos)
        {
            try
            {
                int qty = pos.Contracts;

                // Submit stop
                pos.StopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, qty, 0, pos.StopPrice, "", "Stop_" + pos.SignalId)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, qty, 0, pos.StopPrice, "", "Stop_" + pos.SignalId);

                // Calculate target quantities (simple 50/50 split for targets, rest trails)
                int t1Qty = Math.Max(1, qty / 3);
                int remainingAfterT1 = qty - t1Qty;
                int t2Qty = Math.Max(1, remainingAfterT1 / 2);

                // Submit T1
                if (t1Qty > 0)
                {
                    pos.Target1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, t1Qty, pos.Target1Price, 0, "", "T1_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, t1Qty, pos.Target1Price, 0, "", "T1_" + pos.SignalId);
                }

                // Submit T2
                if (t2Qty > 0)
                {
                    pos.Target2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, t2Qty, pos.Target2Price, 0, "", "T2_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, t2Qty, pos.Target2Price, 0, "", "T2_" + pos.SignalId);
                }

                Print($"SLAVE V7 | {slaveId} | BRACKETS SUBMITTED: Stop={pos.StopPrice:F2} T1={pos.Target1Price:F2} T2={pos.Target2Price:F2}");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V7 ERROR SubmitBracketOrders: {ex.Message}");
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            // Position closed - cleanup
            if (marketPosition == MarketPosition.Flat && activePositions.Count > 0)
            {
                Print($"SLAVE V7 | {slaveId} | POSITION CLOSED");
                
                // Cancel any remaining orders
                foreach (var kvp in activePositions)
                {
                    var pos = kvp.Value;
                    if (pos.StopOrder != null && pos.StopOrder.OrderState == OrderState.Working)
                        CancelOrder(pos.StopOrder);
                    if (pos.Target1Order != null && pos.Target1Order.OrderState == OrderState.Working)
                        CancelOrder(pos.Target1Order);
                    if (pos.Target2Order != null && pos.Target2Order.OrderState == OrderState.Working)
                        CancelOrder(pos.Target2Order);
                }

                activePositions.Clear();
            }
        }

        #endregion
    }
}
