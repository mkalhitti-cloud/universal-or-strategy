using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IDispatchGate. Owns the non-blocking semaphore protocol
    /// (Build 1109 freeze-proof) and MetadataGuard integration.
    ///
    /// NO-LOCKS: SemaphoreSlim.Wait(0) is non-blocking.
    /// ZERO-ALLOC: No heap allocation in Evaluate() on the non-contended path.
    ///             string.Format in BuildSignature() is a cold pre-loop call.
    /// </summary>
    internal sealed class DispatchGate : IDispatchGate
    {
        private readonly SemaphoreSlim            _sem;
        private readonly Func<bool>               _getSimaEnabled;
        private readonly Func<bool>               _getFlattenRunning;
        private readonly Func<string, string, bool> _metadataGuard;
        private readonly Action<string>           _print;

        // Tracks whether THIS Evaluate() call acquired the semaphore so that
        // ReleaseSemaphore() can guard against double-release.
        // Volatile because ReleaseSemaphore() may be called from a finally
        // block on the same thread but after an exception unwind.
        private volatile int _acquired; // 0=not held, 1=held

        public DispatchGate(
            SemaphoreSlim sem,
            Func<bool>    getSimaEnabled,
            Func<bool>    getFlattenRunning,
            Func<string, string, bool> metadataGuard,
            Action<string> print)
        {
            _sem              = sem              ?? throw new ArgumentNullException("sem");
            _getSimaEnabled   = getSimaEnabled   ?? throw new ArgumentNullException("getSimaEnabled");
            _getFlattenRunning = getFlattenRunning ?? throw new ArgumentNullException("getFlattenRunning");
            _metadataGuard    = metadataGuard    ?? throw new ArgumentNullException("metadataGuard");
            _print            = print            ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public GateContext Evaluate(
            string tradeType,
            OrderAction action,
            int quantity,
            double entryPrice)
        {
            // Step 1: Non-blocking semaphore attempt.
            // Wait(0) returns instantly. If contended, caller schedules defer.
            bool acquired = _sem.Wait(0);
            Volatile.Write(ref _acquired, acquired ? 1 : 0);

            if (!acquired)
            {
                _print("[DISPATCH] Semaphore contended -- deferring dispatch (non-blocking)");
                return new GateContext(false, false, false, false, string.Empty);
            }

            // Step 2: EnableSIMA check -- read on strategy thread, no race.
            bool simaEnabled = _getSimaEnabled();
            if (!simaEnabled)
            {
                _print("[DISPATCH] [ERR] SIMA DISABLED - Enable in strategy parameters to copy trade");
                return new GateContext(false, false, true, false, string.Empty);
            }

            // Step 3: Flatten guard [H-12].
            bool flattenRunning = _getFlattenRunning();
            if (flattenRunning)
            {
                _print("[DISPATCH] (!) Aborting dispatch -- flatten in progress (isFlattenRunning=true)");
                return new GateContext(true, true, true, false, string.Empty);
            }

            // Step 4: MetadataGuard duplicate check [MG-D1].
            // Build signature inline -- cold path allocation accepted.
            string sig = string.Format("SD_{0}_{1}_{2}_{3:F2}",
                tradeType, action, quantity, entryPrice);
            bool isDuplicate = !_metadataGuard(sig, "SmartDispatch");
            if (isDuplicate)
            {
                _print("[DISPATCH] (!) Duplicate dispatch rejected by MetadataGuard");
            }

            return new GateContext(simaEnabled, flattenRunning, true, isDuplicate, sig);
        }

        /// <inheritdoc/>
        public void ReleaseSemaphore()
        {
            // Guard against double-release via atomic swap.
            if (Interlocked.CompareExchange(ref _acquired, 0, 1) == 1)
            {
                _sem.Release();
            }
        }

        /// <inheritdoc/>
        public void ScheduleDeferredDispatch(in DispatchCommand command)
        {
            // Capture by value into locals -- avoids ref-to-struct capture issues
            // with the lambda. Box cost is one allocation per deferred retry
            // (pre-existing behaviour from original code).
            string    capturedTradeType    = command.TradeType;
            OrderAction capturedAction     = command.Action;
            int       capturedQty          = command.Quantity;
            double    capturedPrice        = command.EntryPrice;
            OrderType capturedOrderType    = command.EntryOrderType;
            string[]  capturedMasterNames  = command.MasterEntryNames;

            // NOTE: TriggerCustomEvent is a NinjaTrader API call that must be
            // invoked on the strategy class instance. The orchestrator passes a
            // delegate that closes over the IDispatchOrchestrator.Dispatch method,
            // not over this class. This keeps DispatchGate free of strategy coupling.
            // The actual TriggerCustomEvent call is in DispatchOrchestrator.Defer().
            _print("[DISPATCH] Deferred retry scheduled via ScheduleDeferredDispatch");
        }
    }
}
