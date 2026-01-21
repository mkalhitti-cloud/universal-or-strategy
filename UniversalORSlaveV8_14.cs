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
    /// V8 Slave Strategy - Ultra-lightweight trade copier with 4-target system
    /// Receives signals from V8 Master and executes identical trades
    /// NO UI, NO indicators, NO calculations - just order execution
    /// Designed to run from Strategies tab (headless, low RAM)
    /// V8: 4-target system + built-in trailing stops + optional master trail settings
    /// </summary>
    public class UniversalORSlaveV8_14 : Strategy
    {
        #region Variables

        // Instrument info
        private double tickSize;
        private double pointValue;

        // Position tracking - 4-target system
        private Dictionary<string, SlavePosition> activePositions;

        // Slave identification
        private string slaveId;

        #endregion

        #region Position Class

        private class SlavePosition
        {
            public string SignalId;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;
            public int T2Contracts;
            public int T3Contracts;
            public int T4Contracts;  // Runner
            public int RemainingContracts;
            public double EntryPrice;
            public double StopPrice;
            public double Target1Price;
            public double Target2Price;
            public double Target3Price;
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;
            public Order EntryOrder;
            public Order StopOrder;
            public Order Target1Order;
            public Order Target2Order;
            public Order Target3Order;
            
            // Trail settings (from master or own)
            public double BeTrigger;
            public double BeOffset;
            public double Trail1Trigger;
            public double Trail1Distance;
            public double Trail2Trigger;
            public double Trail2Distance;
            public double Trail3Trigger;
            public double Trail3Distance;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public int TicksSinceEntry;  // For frequency-based trailing
            public bool IsRMA;  // V8.1: Track order type for sync
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

        [NinjaScriptProperty]
        [Display(Name = "Use Master Stop Sync", Description = "V8.1: When true, mirrors master's exact stop prices in real-time. When false, manages own trailing stops independently.", Order = 1, GroupName = "2. Trailing")]
        public bool UseMasterStopSync { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Master Trail Settings", Description = "When true, uses master's trailing stop settings. When false, uses own settings below.", Order = 2, GroupName = "2. Trailing")]
        public bool UseMasterTrailSettings { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BE Trigger (Points)", Order = 2, GroupName = "2. Trailing")]
        public double BreakEvenTriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BE Offset (Ticks)", Order = 3, GroupName = "2. Trailing")]
        public int BreakEvenOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Trigger (Points)", Order = 4, GroupName = "2. Trailing")]
        public double Trail1TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Distance (Points)", Order = 5, GroupName = "2. Trailing")]
        public double Trail1DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Trigger (Points)", Order = 6, GroupName = "2. Trailing")]
        public double Trail2TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Distance (Points)", Order = 7, GroupName = "2. Trailing")]
        public double Trail2DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Trigger (Points)", Order = 8, GroupName = "2. Trailing")]
        public double Trail3TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Distance (Points)", Order = 9, GroupName = "2. Trailing")]
        public double Trail3DistancePoints { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V8 Slave - Ultra-light trade copier with 4-target system (no UI, runs headless)";
                Name = "UniversalORSlaveV8_14";
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

                // Trail settings defaults (same as V8 Master)
                UseMasterStopSync = true;  // V8.1: Default: sync with master's stops
                UseMasterTrailSettings = true;  // Default: use master's settings
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
                activePositions = new Dictionary<string, SlavePosition>(4);
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;
                
                // V8.13 FIX: Robust safety checks for Account and Instrument
                if (Account == null || string.IsNullOrEmpty(Account.Name))
                {
                    Print("SLAVE V8.13 | WARNING: Account object not ready. Waiting for Realtime...");
                    slaveId = "WAITING_FOR_ACCOUNT_" + Instrument.MasterInstrument.Name;
                }
                else
                {
                    slaveId = Account.Name + "_" + Instrument.MasterInstrument.Name;
                }

                Print($"SLAVE V8.13 | {slaveId} | Tick: {tickSize} | PV: ${pointValue}");
                Print($"SLAVE V8.13 | Risk: ${RiskPerTrade} | Contracts: {MinContracts}-{MaxContracts}");
                Print($"SLAVE V8.13 | Stop Sync: {UseMasterStopSync} | Trail Settings: {UseMasterTrailSettings}");
            }
            else if (State == State.Realtime)
            {
                // Subscribe to signals using event handlers
                SignalBroadcaster.OnTradeSignal += OnTradeSignalHandler;
                SignalBroadcaster.OnFlattenAll += OnFlattenHandler;
                SignalBroadcaster.OnBreakevenRequest += OnBreakevenHandler;
                
                // V8.1: Subscribe to stop updates if sync enabled
                if (UseMasterStopSync)
                {
                    SignalBroadcaster.OnStopUpdate += OnStopUpdateHandler;
                    SignalBroadcaster.OnEntryUpdate += OnEntryUpdateHandler;
                    SignalBroadcaster.OnOrderCancel += OnOrderCancelHandler;
                }

                Print($"SLAVE V8.1 | {slaveId} | SUBSCRIBED - Listening for Master signals");
                Print(SignalBroadcaster.GetSubscriberCounts());
            }
            else if (State == State.Terminated)
            {
                // Unsubscribe from events
                SignalBroadcaster.OnTradeSignal -= OnTradeSignalHandler;
                SignalBroadcaster.OnFlattenAll -= OnFlattenHandler;
                SignalBroadcaster.OnBreakevenRequest -= OnBreakevenHandler;
                
                // V8.1: Unsubscribe from sync events
                if (UseMasterStopSync)
                {
                    SignalBroadcaster.OnStopUpdate -= OnStopUpdateHandler;
                    SignalBroadcaster.OnEntryUpdate -= OnEntryUpdateHandler;
                    SignalBroadcaster.OnOrderCancel -= OnOrderCancelHandler;
                }

                activePositions?.Clear();
                Print($"SLAVE V8.13 | {slaveId} | TERMINATED");
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            // Only process primary series
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            // V8.1: Only manage independent trailing if NOT syncing with master
            if (!UseMasterStopSync && activePositions.Count > 0)
            {
                ManageTrailingStops();
            }
        }

        #endregion

        #region Signal Handlers

        private void OnTradeSignalHandler(object sender, SignalBroadcaster.TradeSignal signal)
        {
            try
            {
                // V8: Filter by instrument - only process signals for our instrument
                string myInstrument = Instrument.MasterInstrument.Name;
                if (!string.IsNullOrEmpty(signal.Instrument) && signal.Instrument != myInstrument)
                {
                    Print($"SLAVE V8 | {slaveId} | IGNORED signal for {signal.Instrument} (I trade {myInstrument})");
                    return;
                }

                Print($"SLAVE V8 | {slaveId} | SIGNAL RECEIVED: {signal.SignalId}");

                // Calculate position size based on risk
                double stopDistance = Math.Abs(signal.EntryPrice - signal.StopPrice);
                double stopDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(RiskPerTrade / stopDollars);
                contracts = Math.Max(MinContracts, Math.Min(contracts, MaxContracts));

                // V8: Calculate 4-target allocation
                int t1Qty, t2Qty, t3Qty, t4Qty;
                if (contracts == 1) { t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 0; }
                else if (contracts == 2) { t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 1; }
                else if (contracts == 3) { t1Qty = 1; t2Qty = 1; t3Qty = 0; t4Qty = 1; }
                else if (contracts == 4) { t1Qty = 1; t2Qty = 1; t3Qty = 1; t4Qty = 1; }
                else
                {
                    // 5+: use 20/30/30/20 split
                    t1Qty = (int)Math.Floor(contracts * 0.20);
                    t2Qty = (int)Math.Floor(contracts * 0.30);
                    t3Qty = (int)Math.Floor(contracts * 0.30);
                    t4Qty = contracts - t1Qty - t2Qty - t3Qty;
                    if (t1Qty < 1) { t1Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t2Qty < 1) { t2Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t3Qty < 1) { t3Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                    if (t4Qty < 1) t4Qty = 1;
                }

                // Create position record
                var pos = new SlavePosition
                {
                    SignalId = signal.SignalId,
                    Direction = signal.Direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = signal.EntryPrice,
                    StopPrice = signal.StopPrice,
                    Target1Price = signal.Target1Price,
                    Target2Price = signal.Target2Price,
                    Target3Price = signal.Target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    ExtremePriceSinceEntry = signal.EntryPrice,
                    CurrentTrailLevel = 0,
                    TicksSinceEntry = 0,
                    IsRMA = signal.IsRMA  // V8.1: Track order type
                };

                // V8: Use master's trail settings or own
                if (UseMasterTrailSettings)
                {
                    pos.BeTrigger = signal.BeTrigger;
                    pos.BeOffset = signal.BeOffset;
                    pos.Trail1Trigger = signal.Trail1Trigger;
                    pos.Trail1Distance = signal.Trail1Distance;
                    pos.Trail2Trigger = signal.Trail2Trigger;
                    pos.Trail2Distance = signal.Trail2Distance;
                    pos.Trail3Trigger = signal.Trail3Trigger;
                    pos.Trail3Distance = signal.Trail3Distance;
                }
                else
                {
                    pos.BeTrigger = BreakEvenTriggerPoints;
                    pos.BeOffset = BreakEvenOffsetTicks;
                    pos.Trail1Trigger = Trail1TriggerPoints;
                    pos.Trail1Distance = Trail1DistancePoints;
                    pos.Trail2Trigger = Trail2TriggerPoints;
                    pos.Trail2Distance = Trail2DistancePoints;
                    pos.Trail3Trigger = Trail3TriggerPoints;
                    pos.Trail3Distance = Trail3DistancePoints;
                }

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

                Print($"SLAVE V8 | {slaveId} | ORDER SUBMITTED: {signal.SignalId} | {contracts} @ {signal.EntryPrice:F2}");
                Print($"SLAVE V8 | Targets: T1:{t1Qty}@{signal.Target1Price:F2} T2:{t2Qty}@{signal.Target2Price:F2} T3:{t3Qty}@{signal.Target3Price:F2} T4:{t4Qty}@trail");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR OnTradeSignalHandler: {ex.Message}");
            }
        }

        private void OnFlattenHandler(object sender, SignalBroadcaster.FlattenSignal signal)
        {
            try
            {
                Print($"SLAVE V8 | {slaveId} | FLATTEN RECEIVED: {signal.Reason}");

                // Cancel all pending orders (check multiple states - Working, Accepted, or Submitted)
        foreach (var kvp in activePositions)
        {
            var pos = kvp.Value;
            
            // Helper check for active order states
            bool IsActive(Order o) => o != null && (o.OrderState == OrderState.Working || 
                                                     o.OrderState == OrderState.Accepted || 
                                                     o.OrderState == OrderState.Submitted);
            
            if (IsActive(pos.EntryOrder))
            {
                Print($"SLAVE V8 | {slaveId} | Cancelling pending entry: {kvp.Key}");
                CancelOrder(pos.EntryOrder);
            }
            if (IsActive(pos.StopOrder))
                CancelOrder(pos.StopOrder);
            if (IsActive(pos.Target1Order))
                CancelOrder(pos.Target1Order);
            if (IsActive(pos.Target2Order))
                CancelOrder(pos.Target2Order);
            if (IsActive(pos.Target3Order))
                CancelOrder(pos.Target3Order);
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
                Print($"SLAVE V8 | {slaveId} | FLATTENED");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR OnFlattenHandler: {ex.Message}");
            }
        }

        /// <summary>
        /// V8.1: Handle stop update from master for full synchronization
        /// </summary>
        private void OnStopUpdateHandler(object sender, SignalBroadcaster.StopUpdateSignal signal)
        {
            try
            {
                // Find the position matching this trade ID
                SlavePosition pos = null;
                foreach (var kvp in activePositions)
                {
                    if (kvp.Key == signal.TradeId)
                    {
                        pos = kvp.Value;
                        break;
                    }
                }

                if (pos == null || !pos.EntryFilled)
                    return;

                double newStopPrice = signal.NewStopPrice;
                
                // Round to tick size
                newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);

                // Cancel current stop and submit new one at master's price
                if (pos.StopOrder != null && pos.StopOrder.OrderState == OrderState.Working)
                {
                    CancelOrder(pos.StopOrder);

                    Order newStop = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, newStopPrice, "", "SyncStop_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, newStopPrice, "", "SyncStop_" + pos.SignalId);

                    pos.StopOrder = newStop;
                    pos.StopPrice = newStopPrice;

                    Print($"SLAVE V8.1 | {slaveId} | STOP SYNCED: {newStopPrice:F2} (Level: {signal.StopLevel})");
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8.1 ERROR OnStopUpdateHandler: {ex.Message}");
            }
        }

        /// <summary>
        /// V8.1: Handle order cancellation from master
        /// Cancels slave's pending entry when master cancels theirs
        /// </summary>
        private void OnOrderCancelHandler(object sender, SignalBroadcaster.OrderCancelSignal signal)
        {
            try
            {
                if (signal == null) return;
                
                string signalId = signal.TradeId;
                
                // Find matching position
                if (activePositions.ContainsKey(signalId))
                {
                    SlavePosition pos = activePositions[signalId];
                    
                    // Only cancel if entry not filled yet
                    if (!pos.EntryFilled && pos.EntryOrder != null)
                    {
                        Print($"SLAVE V8.1 | {slaveId} | CANCEL SYNC: {signalId} | Reason: {signal.Reason}");
                        
                        // Cancel the pending entry order
                        CancelOrder(pos.EntryOrder);
                        
                        // Clean up position
                        activePositions.Remove(signalId);
                        
                        Print($"SLAVE V8.1 | {slaveId} | Entry order cancelled and position removed");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8.1 ERROR OnOrderCancelHandler: {ex.Message}");
            }
        }

        /// <summary>
        /// V8.1: Handle entry price update from master
        /// Cancels slave's pending entry and resubmits at master's new price
        /// </summary>
        private void OnEntryUpdateHandler(object sender, SignalBroadcaster.EntryUpdateSignal signal)
        {
            try
            {
                if (signal == null) return;
                
                string signalId = signal.TradeId;
                double newPrice = signal.NewEntryPrice;
                
                // Find matching position
                if (activePositions.ContainsKey(signalId))
                {
                    SlavePosition pos = activePositions[signalId];
                    
                    // Only update if entry not filled yet
                    if (!pos.EntryFilled && pos.EntryOrder != null)
                    {
                        double oldPrice = pos.EntryPrice;
                        
                        Print($"SLAVE V8.1 | {slaveId} | ENTRY SYNC: {signalId} | {oldPrice:F2} → {newPrice:F2}");
                        
                        // Cancel old entry order
                        CancelOrder(pos.EntryOrder);
                        
                        // Update stored price
                        pos.EntryPrice = newPrice;
                        
                        // Resubmit entry at new price using correct order type
                        Order newEntryOrder;
                        if (pos.IsRMA)
                        {
                            // RMA uses limit orders
                            newEntryOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, pos.TotalContracts, newPrice, 0, "", signalId)
                                : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, pos.TotalContracts, newPrice, 0, "", signalId);
                        }
                        else
                        {
                            // OR uses stop market (breakout)
                            newEntryOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, pos.TotalContracts, 0, newPrice, "", signalId)
                                : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, pos.TotalContracts, 0, newPrice, "", signalId);
                        }
                        
                        pos.EntryOrder = newEntryOrder;
                        
                        Print($"SLAVE V8.1 | {slaveId} | Entry order updated to new price: {newPrice:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8.1 ERROR OnEntryUpdateHandler: {ex.Message}");
            }
        }




        private void OnBreakevenHandler(object sender, SignalBroadcaster.BreakevenSignal signal)
        {
            try
            {
                Print($"SLAVE V8 | {slaveId} | BREAKEVEN RECEIVED");

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

                        Order newStop = pos.Direction == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, bePrice, "", "BEStop_" + pos.SignalId)
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, bePrice, "", "BEStop_" + pos.SignalId);

                        pos.StopOrder = newStop;
                        pos.StopPrice = bePrice;

                        Print($"SLAVE V8 | {slaveId} | STOP MOVED TO BE: {bePrice:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR OnBreakevenHandler: {ex.Message}");
            }
        }

        #endregion

        #region Trailing Stops

        private void ManageTrailingStops()
        {
            try
            {
                double currentPrice = Close[0];

                foreach (var kvp in activePositions)
                {
                    var pos = kvp.Value;
                    if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                    pos.TicksSinceEntry++;

                    // Update extreme price
                    if (pos.Direction == MarketPosition.Long)
                        pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, currentPrice);
                    else
                        pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, currentPrice);

                    double profitPoints = pos.Direction == MarketPosition.Long
                        ? currentPrice - pos.EntryPrice
                        : pos.EntryPrice - currentPrice;

                    // V8: Frequency-based trailing - BE/T3 every tick, T1/T2 every other tick
                    bool checkNow = true;
                    if (profitPoints >= pos.Trail1Trigger && profitPoints < pos.Trail3Trigger)
                    {
                        checkNow = (pos.TicksSinceEntry % 2 == 0);
                    }

                    if (!checkNow) continue;

                    double newStopPrice = pos.StopPrice;
                    int newTrailLevel = pos.CurrentTrailLevel;

                    // Check trail levels from highest to lowest
                    if (profitPoints >= pos.Trail3Trigger && pos.CurrentTrailLevel < 4)
                    {
                        // Trail 3: Tight trailing
                        newStopPrice = pos.Direction == MarketPosition.Long
                            ? pos.ExtremePriceSinceEntry - pos.Trail3Distance
                            : pos.ExtremePriceSinceEntry + pos.Trail3Distance;
                        newTrailLevel = 4;
                    }
                    else if (profitPoints >= pos.Trail2Trigger && pos.CurrentTrailLevel < 3)
                    {
                        // Trail 2
                        newStopPrice = pos.Direction == MarketPosition.Long
                            ? pos.ExtremePriceSinceEntry - pos.Trail2Distance
                            : pos.ExtremePriceSinceEntry + pos.Trail2Distance;
                        newTrailLevel = 3;
                    }
                    else if (profitPoints >= pos.Trail1Trigger && pos.CurrentTrailLevel < 2)
                    {
                        // Trail 1
                        newStopPrice = pos.Direction == MarketPosition.Long
                            ? pos.ExtremePriceSinceEntry - pos.Trail1Distance
                            : pos.ExtremePriceSinceEntry + pos.Trail1Distance;
                        newTrailLevel = 2;
                    }
                    else if (profitPoints >= pos.BeTrigger && pos.CurrentTrailLevel < 1)
                    {
                        // Breakeven
                        newStopPrice = pos.Direction == MarketPosition.Long
                            ? pos.EntryPrice + (tickSize * pos.BeOffset)
                            : pos.EntryPrice - (tickSize * pos.BeOffset);
                        newTrailLevel = 1;
                    }

                    // Update stop if price improved
                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? newStopPrice > pos.StopPrice
                        : newStopPrice < pos.StopPrice;

                    if (shouldUpdate && newTrailLevel > pos.CurrentTrailLevel)
                    {
                        // Round to tick
                        newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);

                        // Cancel old stop and submit new
                        if (pos.StopOrder != null && pos.StopOrder.OrderState == OrderState.Working)
                        {
                            CancelOrder(pos.StopOrder);

                            Order newStop = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, newStopPrice, "", "Trail_" + pos.SignalId)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, newStopPrice, "", "Trail_" + pos.SignalId);

                            pos.StopOrder = newStop;
                            pos.StopPrice = newStopPrice;
                            pos.CurrentTrailLevel = newTrailLevel;

                            Print($"SLAVE V8 | {slaveId} | TRAIL LEVEL {newTrailLevel}: Stop → {newStopPrice:F2}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR ManageTrailingStops: {ex.Message}");
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
                        pos.ExtremePriceSinceEntry = averageFillPrice;

                        Print($"SLAVE V8 | {slaveId} | ENTRY FILLED: {pos.SignalId} @ {averageFillPrice:F2}");

                        // Submit stop and targets
                        SubmitBracketOrders(pos);
                    }

                    // T1 fill
                    if (order == pos.Target1Order && orderState == OrderState.Filled)
                    {
                        pos.T1Filled = true;
                        pos.RemainingContracts -= pos.T1Contracts;
                        UpdateStopQuantity(pos);
                        Print($"SLAVE V8 | {slaveId} | T1 FILLED: Remaining {pos.RemainingContracts}");
                    }

                    // T2 fill
                    if (order == pos.Target2Order && orderState == OrderState.Filled)
                    {
                        pos.T2Filled = true;
                        pos.RemainingContracts -= pos.T2Contracts;
                        UpdateStopQuantity(pos);
                        Print($"SLAVE V8 | {slaveId} | T2 FILLED: Remaining {pos.RemainingContracts}");
                    }

                    // T3 fill
                    if (order == pos.Target3Order && orderState == OrderState.Filled)
                    {
                        pos.T3Filled = true;
                        pos.RemainingContracts -= pos.T3Contracts;
                        UpdateStopQuantity(pos);
                        Print($"SLAVE V8 | {slaveId} | T3 FILLED: Remaining {pos.RemainingContracts}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR OnOrderUpdate: {ex.Message}");
            }
        }

        private void SubmitBracketOrders(SlavePosition pos)
        {
            try
            {
                // Submit stop for all contracts
                pos.StopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, pos.StopPrice, "", "Stop_" + pos.SignalId)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, pos.StopPrice, "", "Stop_" + pos.SignalId);

                // Submit T1
                if (pos.T1Contracts > 0)
                {
                    pos.Target1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + pos.SignalId);
                }

                // Submit T2
                if (pos.T2Contracts > 0)
                {
                    pos.Target2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + pos.SignalId);
                }

                // Submit T3
                if (pos.T3Contracts > 0)
                {
                    pos.Target3Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + pos.SignalId)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + pos.SignalId);
                }

                // T4 (runner) has no target - trails only

                Print($"SLAVE V8 | {slaveId} | BRACKETS SUBMITTED: Stop={pos.StopPrice:F2} T1={pos.Target1Price:F2} T2={pos.Target2Price:F2} T3={pos.Target3Price:F2}");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR SubmitBracketOrders: {ex.Message}");
            }
        }

        private void UpdateStopQuantity(SlavePosition pos)
        {
            try
            {
                if (pos.RemainingContracts <= 0) return;
                if (pos.StopOrder == null || pos.StopOrder.OrderState != OrderState.Working) return;

                // Cancel and resubmit stop with new quantity
                CancelOrder(pos.StopOrder);

                pos.StopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.RemainingContracts, 0, pos.StopPrice, "", "Stop_" + pos.SignalId)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.RemainingContracts, 0, pos.StopPrice, "", "Stop_" + pos.SignalId);

                Print($"SLAVE V8 | {slaveId} | STOP QTY UPDATED: {pos.RemainingContracts}");
            }
            catch (Exception ex)
            {
                Print($"SLAVE V8 ERROR UpdateStopQuantity: {ex.Message}");
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            // Position closed - cleanup
            if (marketPosition == MarketPosition.Flat && activePositions.Count > 0)
            {
                Print($"SLAVE V8 | {slaveId} | POSITION CLOSED");
                
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
                    if (pos.Target3Order != null && pos.Target3Order.OrderState == OrderState.Working)
                        CancelOrder(pos.Target3Order);
                }

                activePositions.Clear();
            }
        }

        #endregion
    }
}
