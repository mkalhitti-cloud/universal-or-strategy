// V12.Harness.Shim -- NinjaTrader.NinjaScript.Strategies.Strategy stub
// Build 980 Proof-of-Work Harness
// TriggerCustomEvent is SYNCHRONOUS in the test shim (no strategy thread needed).
using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Minimal Strategy base class stub for headless testing.
    /// TriggerCustomEvent executes synchronously inline.
    /// Print() appends to PrintLog for assertion.
    /// </summary>
    public abstract class Strategy
    {
        // -- NinjaTrader API surface (used by V12_002 code paths) -----

        public Instrument Instrument { get; protected set; } = NinjaTrader.Cbi.Instrument.MES;
        public Account    Account    { get; protected set; }

        /// <summary>
        /// Synchronous shim: executes the delegate immediately on the calling thread.
        /// This models the single-threaded strategy thread guarantee without NT8.
        /// </summary>
        public void TriggerCustomEvent(Action<object> action, object state)
        {
            try { action?.Invoke(state); }
            catch (Exception ex) { PrintLog.Add("[TriggerCustomEvent-SHIM-EX] " + ex.Message); }
        }

        /// <summary>
        /// All Print() calls accumulate here for test assertion.
        /// </summary>
        public List<string> PrintLog { get; } = new List<string>();

        protected void Print(string msg)
        {
            PrintLog.Add(msg);
        }

        // -- Helpers used by harness sub-paths ------------------------

        protected static bool IsOrderTerminal(OrderState state)
        {
            return state == OrderState.Cancelled
                || state == OrderState.Filled
                || state == OrderState.Rejected
                || state == OrderState.Unknown;
        }
    }
}
