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
            public MarketPosition Direction { get; set; }
            public double EntryPrice { get; set; }
            public double StopPrice { get; set; }
            public double Target1Price { get; set; }
            public double Target2Price { get; set; }
            public int T1Contracts { get; set; }
            public int T2Contracts { get; set; }
            public int T3Contracts { get; set; }
            public bool IsRMA { get; set; }
            public DateTime Timestamp { get; set; }
            public double SessionRange { get; set; }  // For reference
            public double CurrentATR { get; set; }    // For RMA trades
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

            return $"Subscribers: Trade={tradeSignalCount}, Trail={trailUpdateCount}, " +
                   $"Target={targetActionCount}, Flatten={flattenCount}, BE={breakevenCount}";
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
        }

        #endregion
    }
}
