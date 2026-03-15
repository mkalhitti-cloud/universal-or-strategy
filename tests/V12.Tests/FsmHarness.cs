// V12.Tests -- FsmHarness
// Build 980 Proof-of-Work: Self-contained FSM test harness
// Extracted logic: FollowerReplaceState, FollowerReplaceSpec, B3/B4 suppression gate
// ASCII-only strings throughout.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace V12.Tests
{
    // ---------------------------------------------------------------
    // FSM Types (extracted verbatim from src/V12_002.cs L339-L355)
    // ---------------------------------------------------------------

    internal enum FollowerReplaceState
    {
        Idle,
        PendingCancel,
        Submitting,
        SubmitFailed
    }

    internal class FollowerReplaceSpec
    {
        public FollowerReplaceState State;
        public string CancellingOrderId;
        public int    PendingQty;
        public double PendingLimitPrice;
        public double PendingStopPrice;
        public string AccountName;
        public string SignalName;
        public string MasterSignalName;
        public OrderAction EntryAction;
        public OrderType   EntryOrderType;
        public bool        IsStopType;
        public string LastSubmitError;
    }

    // ---------------------------------------------------------------
    // PositionInfo stub (mirrors src/V12_002.PositionInfo.cs fields
    // used by the test paths)
    // ---------------------------------------------------------------

    internal class PositionInfoStub
    {
        public bool           IsFollower          { get; set; }
        public bool           EntryFilled         { get; set; }
        public int            RemainingContracts  { get; set; }
        public int            TotalContracts      { get; set; }
        public MarketPosition Direction           { get; set; }
        public Account        ExecutingAccount    { get; set; }
    }

    // ---------------------------------------------------------------
    // FsmHarness -- subclass of Strategy shim exposing internal state
    // ---------------------------------------------------------------

    /// <summary>
    /// Headless FSM harness for V12_002 regression suites T-RC-02 and T-PL-01.
    /// Inherits Strategy shim (synchronous TriggerCustomEvent, PrintLog list).
    /// </summary>
    public class FsmHarness : Strategy
    {
        // -- State dictionaries (mirrors V12_002 field declarations) --

        internal readonly ConcurrentDictionary<string, FollowerReplaceSpec>
            _followerReplaceSpecs = new ConcurrentDictionary<string, FollowerReplaceSpec>();

        internal readonly ConcurrentDictionary<string, long>
            _fleetSuppressionMap = new ConcurrentDictionary<string, long>();

        internal readonly ConcurrentDictionary<string, PositionInfoStub>
            activePositions = new ConcurrentDictionary<string, PositionInfoStub>();

        internal readonly ConcurrentDictionary<string, int>
            expectedPositions = new ConcurrentDictionary<string, int>();

        internal bool isFlattenRunning = false;

        // -- Capture fields for assertion ----------------------------

        /// <summary>Last spec passed to SubmitFollowerReplacement stub.</summary>
        internal FollowerReplaceSpec LastSubmitSpec { get; private set; }

        /// <summary>Last signal name passed to SubmitFollowerReplacement stub.</summary>
        internal string LastSubmitSignal { get; private set; }

        /// <summary>Whether SubmitFollowerReplacement was called at all.</summary>
        internal bool SubmitCalled { get; private set; }

        // -- Factory -------------------------------------------------

        public static FsmHarness Create(string masterAccountName = "Master")
        {
            var h = new FsmHarness();
            h.Account    = new Account { Name = masterAccountName };
            h.Instrument = NinjaTrader.Cbi.Instrument.MES;
            return h;
        }

        // ---------------------------------------------------------------
        // Build 980 [Repair B]: B3/B4 suppression gate
        // Ported verbatim from ProcessQueuedAccountOrderCore L569-L577
        // Returns true if the order should be PROCESSED (gate open).
        // Returns false if the order should be BLOCKED (gate closed / suppressed).
        // ---------------------------------------------------------------

        /// <summary>
        /// Simulates the suppression gate logic from ProcessQueuedAccountOrderCore.
        /// Equivalent to the B3/B4 gate check at AccountOrders.cs L569-L577.
        /// </summary>
        internal bool SimulateAccountOrderSuppression(string acctName, string orderName)
        {
            long suppressTicks;
            if (_fleetSuppressionMap.TryGetValue(acctName, out suppressTicks))
            {
                if (DateTime.UtcNow.Ticks - suppressTicks < TimeSpan.TicksPerSecond * 2)
                {
                    Print($"[SUPPRESSION] B3/B4 Gate blocking trailing cancel for {orderName} on {acctName}");
                    return false; // BLOCKED
                }
            }
            return true; // PROCESSED
        }

        // ---------------------------------------------------------------
        // B4 Lambda gate: mirrors the inner TriggerCustomEvent lambda check
        // in HandleMatchedFollowerOrder L311-L316 (Build 980 [Repair B]).
        // ---------------------------------------------------------------

        /// <summary>
        /// Simulates dispatching the FSM replacement lambda for a given spec.
        /// The lambda checks the B4 gate (suppression map) before submitting.
        /// </summary>
        internal void SimulateFsmReplacementLambda(string signalName, string acctName, FollowerReplaceSpec spec)
        {
            TriggerCustomEvent(o =>
            {
                if (_fleetSuppressionMap.ContainsKey(acctName))
                {
                    Print("[SUPPRESSION-B4] Lambda abort for " + signalName + " -- CANCEL_ALL fired post gate-pass");
                    _followerReplaceSpecs.TryRemove(signalName, out _);
                    return;
                }

                // Re-read prices from spec at execution time (P2 FSM Consistency)
                bool ok = SubmitFollowerReplacement(signalName, acctName, spec);
                if (ok)
                    _followerReplaceSpecs.TryRemove(signalName, out _);
            }, null);
        }

        // ---------------------------------------------------------------
        // SubmitFollowerReplacement stub
        // Records the call for assertion; does NOT send to broker.
        // Mirrors the real method signature in V12_002.Symmetry.Replace.cs.
        // ---------------------------------------------------------------

        internal bool SubmitFollowerReplacement(string signalName, string acctName, FollowerReplaceSpec spec)
        {
            LastSubmitSignal = signalName;
            LastSubmitSpec   = spec;
            SubmitCalled     = true;
            Print("[SUBMIT_STUB] SubmitFollowerReplacement called for " + signalName
                + " acct=" + acctName
                + " LimitPrice=" + spec.PendingLimitPrice.ToString("F2")
                + " StopPrice="  + spec.PendingStopPrice.ToString("F2")
                + " Qty="        + spec.PendingQty
                + " OrderType="  + spec.EntryOrderType);
            return true; // stub always succeeds
        }

        // ---------------------------------------------------------------
        // Suppression map helpers (mirror V12_002 SIMA.cs helpers)
        // ---------------------------------------------------------------

        /// <summary>Stamp suppression for an account -- equivalent to CANCEL_ALL handler.</summary>
        internal void StampSuppression(string acctName)
        {
            _fleetSuppressionMap[acctName] = DateTime.UtcNow.Ticks;
        }

        /// <summary>Stamp suppression with a specific UTC tick (for time-travel tests).</summary>
        internal void StampSuppressionAt(string acctName, long utcTicks)
        {
            _fleetSuppressionMap[acctName] = utcTicks;
        }

        /// <summary>Clears all test state for reuse.</summary>
        internal void Reset()
        {
            _followerReplaceSpecs.Clear();
            _fleetSuppressionMap.Clear();
            activePositions.Clear();
            expectedPositions.Clear();
            PrintLog.Clear();
            LastSubmitSpec   = null;
            LastSubmitSignal = null;
            SubmitCalled     = false;
        }
    }
}
