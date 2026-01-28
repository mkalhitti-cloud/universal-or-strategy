using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Static signal broadcasting service for Master/Slave multi-account architecture.
    /// Master strategy generates signals, Slave strategies listen and execute.
    /// </summary>
    public static class SignalBroadcaster
    {
        #region Signal Data Classes

        /// <summary>
        /// Complete trade signal with all bracket order details
        /// </summary>
        public class TradeSignal
        {
            public string SignalId { get; set; }
            public string Instrument { get; set; }        // V7.1: For instrument filtering
            public MarketPosition Direction { get; set; }
            public double EntryPrice { get; set; }
            public double StopPrice { get; set; }
            public double Target1Price { get; set; }
            public double Target2Price { get; set; }
            public double Target3Price { get; set; }      // V8: T3 price
            public int T1Contracts { get; set; }
            public int T2Contracts { get; set; }
            public int T3Contracts { get; set; }
            public int T4Contracts { get; set; }          // V8: Runner contracts
            public bool IsRMA { get; set; }
            public DateTime Timestamp { get; set; }
            public double SessionRange { get; set; }  // For reference
            public double CurrentATR { get; set; }    // For RMA trades
            
            // V8: Trail settings so slave can use master's configuration
            public double BeTrigger { get; set; }
            public double BeOffset { get; set; }
            public double Trail1Trigger { get; set; }
            public double Trail1Distance { get; set; }
            public double Trail2Trigger { get; set; }
            public double Trail2Distance { get; set; }
            public double Trail3Trigger { get; set; }
            public double Trail3Distance { get; set; }
        }

        /// <summary>
        /// Trailing stop update signal
        /// </summary>
        public class TrailUpdateSignal
        {
            public string SignalId { get; set; }
            public double NewStopPrice { get; set; }
            public int TrailLevel { get; set; }  // 0=BE, 1=Trail1, 2=Trail2, 3=Trail3
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Full stop synchronization signal
        /// Master broadcasts every stop update, slaves mirror exact price
        /// </summary>
        public class StopUpdateSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public double NewStopPrice { get; set; }   // Master's new stop price
            public string StopLevel { get; set; }      // "BE", "T1", "T2", "T3" for logging
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Entry order price update signal
        /// Master broadcasts when pending entry order price changes
        /// </summary>
        public class EntryUpdateSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public double NewEntryPrice { get; set; }  // Master's new entry price
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Order cancellation signal
        /// Master broadcasts when pending entry order is cancelled
        /// </summary>
        public class OrderCancelSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public string Reason { get; set; }         // Why cancelled
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Target management action signal (v5.12 feature)
        /// </summary>
        public class TargetActionSignal
        {
            public string SignalId { get; set; }
            public TargetType Target { get; set; }  // T1, T2, or Runner
            public TargetAction Action { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public enum TargetType
        {
            T1,
            T2,
            Runner
        }

        public enum TargetAction
        {
            FillAtMarket,
            MoveToBreakeven,
            MoveStopToEntry,
            CancelTarget
        }

        /// <summary>
        /// Flatten all positions signal
        /// </summary>
        public class FlattenSignal
        {
            public string Reason { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Manual breakeven signal
        /// </summary>
        public class BreakevenSignal
        {
            public string SignalId { get; set; }  // Empty = all positions
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V10.2: External command signal (from TCP Remote)
        /// Allows the TCP owner to broadcast commands to all other strategy instances
        /// </summary>
        public class ExternalCommandSignal
        {
            public string Command { get; set; }
            public string TargetSymbol { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when Master generates a new trade signal
        /// </summary>
        public static event EventHandler<TradeSignal> OnTradeSignal;

        /// <summary>
        /// Fired when Master updates trailing stop
        /// </summary>
        public static event EventHandler<TrailUpdateSignal> OnTrailUpdate;

        /// <summary>
        /// Fired when Master executes target management action
        /// </summary>
        public static event EventHandler<TargetActionSignal> OnTargetAction;

        /// <summary>
        /// Fired when Master requests flatten all
        /// </summary>
        public static event EventHandler<FlattenSignal> OnFlattenAll;

        /// <summary>
        /// Fired when Master requests manual breakeven
        /// </summary>
        public static event EventHandler<BreakevenSignal> OnBreakevenRequest;

        /// <summary>
        /// V8.1: Fired when Master updates any stop (for full synchronization)
        /// </summary>
        public static event EventHandler<StopUpdateSignal> OnStopUpdate;

        /// <summary>
        /// V8.1: Fired when Master updates pending entry order price
        /// </summary>
        public static event EventHandler<EntryUpdateSignal> OnEntryUpdate;

        /// <summary>
        /// V8.1: Fired when Master cancels a pending entry order
        /// </summary>
        public static event EventHandler<OrderCancelSignal> OnOrderCancel;

        /// <summary>
        /// V10.2: Fired when an external TCP command is received
        /// </summary>
        public static event EventHandler<ExternalCommandSignal> OnExternalCommand;

        #endregion

        #region Broadcasting Methods

        /// <summary>
        /// Broadcast a trade signal to all listening slaves
        /// </summary>
        public static void BroadcastTradeSignal(TradeSignal signal)
        {
            if (signal == null)
                throw new ArgumentNullException(nameof(signal));

            signal.Timestamp = DateTime.Now;

            // Invoke event - all slaves will receive this synchronously
            OnTradeSignal?.Invoke(null, signal);
        }

        /// <summary>
        /// Broadcast a trailing stop update to all slaves
        /// </summary>
        public static void BroadcastTrailUpdate(TrailUpdateSignal update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            update.Timestamp = DateTime.Now;
            OnTrailUpdate?.Invoke(null, update);
        }

        /// <summary>
        /// Broadcast a target management action to all slaves
        /// </summary>
        public static void BroadcastTargetAction(TargetActionSignal action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            action.Timestamp = DateTime.Now;
            OnTargetAction?.Invoke(null, action);
        }

        /// <summary>
        /// Broadcast flatten all positions command
        /// </summary>
        public static void BroadcastFlatten(string reason)
        {
            var signal = new FlattenSignal
            {
                Reason = reason ?? "Manual flatten",
                Timestamp = DateTime.Now
            };

            OnFlattenAll?.Invoke(null, signal);
        }

        /// <summary>
        /// Broadcast manual breakeven request
        /// </summary>
        public static void BroadcastBreakeven(string signalId = "")
        {
            var signal = new BreakevenSignal
            {
                SignalId = signalId,
                Timestamp = DateTime.Now
            };

            OnBreakevenRequest?.Invoke(null, signal);
        }

        /// <summary>
        /// V8.1: Broadcast stop update for full synchronization
        /// </summary>
        public static void BroadcastStopUpdate(string tradeId, double newStopPrice, string stopLevel)
        {
            var signal = new StopUpdateSignal
            {
                TradeId = tradeId,
                NewStopPrice = newStopPrice,
                StopLevel = stopLevel,
                Timestamp = DateTime.Now
            };

            OnStopUpdate?.Invoke(null, signal);
        }

        /// <summary>
        /// V8.1: Broadcast entry price update for full synchronization
        /// </summary>
        public static void BroadcastEntryUpdate(string tradeId, double newEntryPrice)
        {
            var signal = new EntryUpdateSignal
            {
                TradeId = tradeId,
                NewEntryPrice = newEntryPrice,
                Timestamp = DateTime.Now
            };

            OnEntryUpdate?.Invoke(null, signal);
        }

        /// <summary>
        /// V8.1: Broadcast order cancellation for full synchronization
        /// </summary>
        public static void BroadcastOrderCancel(string tradeId, string reason)
        {
            var signal = new OrderCancelSignal
            {
                TradeId = tradeId,
                Reason = reason ?? "Manual cancel",
                Timestamp = DateTime.Now
            };

            OnOrderCancel?.Invoke(null, signal);
        }

        /// <summary>
        /// V10.2: Broadcast an external command received via TCP
        /// </summary>
        public static void BroadcastExternalCommand(string command, string targetSymbol)
        {
            var signal = new ExternalCommandSignal
            {
                Command = command,
                TargetSymbol = targetSymbol,
                Timestamp = DateTime.Now
            };

            OnExternalCommand?.Invoke(null, signal);
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Get count of active subscribers for each event type
        /// </summary>
        public static string GetSubscriberCounts()
        {
            int tradeSignalCount = OnTradeSignal?.GetInvocationList().Length ?? 0;
            int trailUpdateCount = OnTrailUpdate?.GetInvocationList().Length ?? 0;
            int targetActionCount = OnTargetAction?.GetInvocationList().Length ?? 0;
            int flattenCount = OnFlattenAll?.GetInvocationList().Length ?? 0;
            int breakevenCount = OnBreakevenRequest?.GetInvocationList().Length ?? 0;
            int stopUpdateCount = OnStopUpdate?.GetInvocationList().Length ?? 0;
            int entryUpdateCount = OnEntryUpdate?.GetInvocationList().Length ?? 0;
            int orderCancelCount = OnOrderCancel?.GetInvocationList().Length ?? 0;

            return $"Subscribers: Trade={tradeSignalCount}, Stop={stopUpdateCount}, Entry={entryUpdateCount}, Cancel={orderCancelCount}";
        }

        /// <summary>
        /// Clear all event subscribers (for cleanup/testing)
        /// </summary>
        public static void ClearAllSubscribers()
        {
            OnTradeSignal = null;
            OnTrailUpdate = null;
            OnTargetAction = null;
            OnFlattenAll = null;
            OnBreakevenRequest = null;
            OnStopUpdate = null;
            OnEntryUpdate = null;
            OnOrderCancel = null;
            OnExternalCommand = null;
        }

        #endregion
    }
}
