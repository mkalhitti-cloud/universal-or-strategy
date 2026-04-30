// V12_002.Photon.JitWarmup.cs -- M6 JIT Warm-up Protocol (Hybrid Arena Phase 2)
// Build 1112.001-v29.0
// Force the .NET CLR to JIT-compile the dispatch pipeline before the first
// OnBarUpdate is processed, eliminating cold-IL latency spikes when the market
// opens. Targets: ExecuteSmartDispatchEntry, ProcessFleetSlot, PumpFleetDispatch,
// and every method on V12_002 decorated with [MethodImpl(AggressiveInlining)].
//
// Cold path: invoked once from State.DataLoaded AFTER BindCpuAffinity().
// Allocates during init only -- not reachable from any hot path.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region M6 Cold-Path Entry

        // Idempotent guard.
        private int _jitWarmupAttempted = 0;

        // Methods named explicitly per the M5-M7 brief. Reflection scan covers
        // additional [MethodImpl(AggressiveInlining)] methods at runtime.
        private static readonly string[] _jitWarmupTargetNames = new[]
        {
            "ExecuteSmartDispatchEntry",
            "ProcessFleetSlot",
            "PumpFleetDispatch",
            "ComputeFleetDispatchShadow",
            "RecordPhotonDispatchSample"
        };

        /// <summary>
        /// Force JIT compilation of the dispatch pipeline. Runs once during cold init.
        /// </summary>
        private void WarmupJit()
        {
            if (Interlocked.CompareExchange(ref _jitWarmupAttempted, 1, 0) != 0)
            {
                Print("[MORPHEUS] JIT warm-up already attempted -- skip.");
                return;
            }

            int prepared = 0;
            int failed = 0;
            Type t = typeof(V12_002);
            const BindingFlags scanFlags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            MethodInfo[] all = t.GetMethods(scanFlags);

            // Pass 1: explicit name list (brief targets + dispatch pipeline core).
            for (int i = 0; i < _jitWarmupTargetNames.Length; i++)
            {
                string targetName = _jitWarmupTargetNames[i];
                for (int j = 0; j < all.Length; j++)
                {
                    MethodInfo m = all[j];
                    if (m.Name != targetName) continue;
                    if (TryPrepare(m)) prepared++;
                    else failed++;
                }
            }

            // Pass 2: every method decorated with [MethodImpl(AggressiveInlining)].
            for (int j = 0; j < all.Length; j++)
            {
                MethodInfo m = all[j];
                MethodImplAttributes impl = m.GetMethodImplementationFlags();
                if ((impl & MethodImplAttributes.AggressiveInlining) == 0) continue;
                if (TryPrepare(m)) prepared++;
                else failed++;
            }

            // Dry run synthetic loop -- warms branch predictor + L1 cache without
            // touching brokers, orders, or expectedPositions. Reads membrane arrays
            // by index only; XOR checksum is discarded.
            int dryRunIterations = RunJitDryLoop();

            Print(string.Format("[MORPHEUS] JIT warm-up complete. {0} methods prepared.", prepared));
            if (failed > 0)
                Print(string.Format("[MORPHEUS] JIT warm-up: {0} method(s) skipped (generic/abstract/unavailable).", failed));
            Print(string.Format("[MORPHEUS] JIT dry-run loop iterated {0} membrane slots (no orders submitted).", dryRunIterations));
        }

        /// <summary>
        /// Attempt to JIT-prepare a single method. Skips abstract, generic, and
        /// generic-parameter-bearing methods which RuntimeHelpers.PrepareMethod
        /// cannot handle without a concrete generic instantiation.
        /// </summary>
        private static bool TryPrepare(MethodInfo m)
        {
            try
            {
                if (m == null) return false;
                if (m.IsAbstract) return false;
                if (m.IsGenericMethodDefinition) return false;
                if (m.ContainsGenericParameters) return false;
                RuntimeHelpers.PrepareMethod(m.MethodHandle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Iterate every AccountIndex in the frozen membrane, reading slot data and
        /// XORing into a discarded checksum. Warms the branch predictor and L1 lines
        /// for the dispatch loop. NO orders submitted, NO state mutated.
        /// </summary>
        private int RunJitDryLoop()
        {
            FlattenedSubstrateState membrane = Volatile.Read(ref _membrane);
            if (membrane == null || membrane.AccountByIndex == null)
            {
                Print("[MORPHEUS] JIT dry-run skipped -- membrane not yet frozen.");
                return 0;
            }

            int accountCount = membrane.AccountCount;
            long checksum = 0;
            for (int i = 0; i < accountCount; i++)
            {
                if (i < membrane.AccountNameHashByIndex.Length)
                    checksum ^= unchecked((long)membrane.AccountNameHashByIndex[i]);
                if (i < membrane.ExpectedPositionByIndex.Length)
                    checksum ^= Volatile.Read(ref membrane.ExpectedPositionByIndex[i]);
                if (i < membrane.DispatchSyncPendingByIndex.Length)
                    checksum ^= Volatile.Read(ref membrane.DispatchSyncPendingByIndex[i]);
            }

            // Discarded sink -- prevents the JIT dead-code-elimination pass from removing the loop.
            Volatile.Write(ref _jitDryRunSink, checksum);
            return accountCount;
        }

        // Discard sink for the dry-run XOR checksum. Write-only; never read by hot path.
        private long _jitDryRunSink = 0;

        #endregion
    }
}
