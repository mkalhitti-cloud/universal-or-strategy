// V12_002.Photon.Telemetry.cs -- M7 Dispatch Telemetry (Hybrid Arena Phase 2)
// Build 1112.001-v29.0
// Ultra-low-overhead, zero-allocation per-cycle dispatch counters. Reuses the
// existing _dispatchInvocationCount / _dispatchPeakElapsedTicks /
// _dispatchTotalElapsedTicks fields declared on V12_002 (V12_002.cs:574-578)
// and adds _lastDispatchTicks for per-call sampling. Stopwatch.Frequency is
// cached at static-class init to avoid repeated property dispatch on the hot path.
//
// Hot-path write (in V12_002.SIMA.Dispatch.cs after sw.Stop()):
//     Volatile.Write(ref _lastDispatchTicks, tFinalTicks);
//
// Read-side (cold path -- State.Terminated):
//     PrintPhotonTelemetryReport() -> StringBuilder with zero LINQ, zero $-interpolation.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region M7 State

        // Frequency snapshot taken once at class init. Stopwatch.Frequency is a static
        // readonly property today, but caching avoids any future JIT-load overhead and
        // makes the conversion arithmetic auditable.
        private static readonly long _stopwatchFrequency = Stopwatch.Frequency;

        // Per-cycle sample. Written by the dispatch thread at the end of
        // ExecuteSmartDispatchEntry. Read by the terminate-thread report emitter.
        // Plain long; cross-thread visibility enforced via Volatile.Read/Write.
        private long _lastDispatchTicks = 0;

        // Recycled StringBuilder. 1 KiB initial capacity covers the 4-line report
        // with headroom. Reused across calls -- never replaced, never grown beyond
        // the report size.
        private readonly StringBuilder _telemetryReportBuilder = new StringBuilder(1024);

        #endregion

        #region M7 Hot-Path Sample Helper

        /// <summary>
        /// Single inlined accessor that the dispatch path uses to publish the
        /// final tick sample. Provided as a helper for clarity; the dispatch site
        /// may call Volatile.Write directly (see STEP 7).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordPhotonDispatchSample(long elapsedTicks)
        {
            Volatile.Write(ref _lastDispatchTicks, elapsedTicks);
        }

        #endregion

        #region M7 Report Emitter

        /// <summary>
        /// Build the [MORPHEUS TELEMETRY] report into the recycled StringBuilder
        /// and return the formatted string. Cold path only -- never invoked from
        /// OnBarUpdate or the dispatch loop.
        /// Format (exact per M5-M7 brief):
        ///   [MORPHEUS TELEMETRY] BUILD 1112.001-v29.0
        ///     Total Dispatches : {N}
        ///     Peak Ticks       : {peak} ({peak_us} us)
        ///     Avg Ticks        : {avg} ({avg_us} us)
        /// </summary>
        public string GetPhotonTelemetryReport()
        {
            long total = Interlocked.Read(ref _dispatchInvocationCount);
            long peak  = Volatile.Read(ref _dispatchPeakElapsedTicks);
            long acc   = Interlocked.Read(ref _dispatchTotalElapsedTicks);
            long last  = Volatile.Read(ref _lastDispatchTicks);

            long avg = total > 0 ? acc / total : 0;

            // Microsecond conversion: ticks * 1_000_000 / Stopwatch.Frequency.
            // Stopwatch.Frequency on Windows is typically 10_000_000 (100ns ticks),
            // which yields integer-precise microseconds. On platforms with
            // non-10MHz frequencies the result is integer-truncated.
            long freq = _stopwatchFrequency;
            long peakUs = freq > 0 ? (peak * 1000000L) / freq : 0;
            long avgUs  = freq > 0 ? (avg  * 1000000L) / freq : 0;
            long lastUs = freq > 0 ? (last * 1000000L) / freq : 0;

            StringBuilder sb = _telemetryReportBuilder;
            sb.Length = 0;
            sb.Append("[MORPHEUS TELEMETRY] BUILD ");
            sb.Append(BUILD_TAG);
            sb.Append('\n');
            sb.Append("  Total Dispatches : ");
            sb.Append(total);
            sb.Append('\n');
            sb.Append("  Peak Ticks       : ");
            sb.Append(peak);
            sb.Append(" (");
            sb.Append(peakUs);
            sb.Append(" us)");
            sb.Append('\n');
            sb.Append("  Avg Ticks        : ");
            sb.Append(avg);
            sb.Append(" (");
            sb.Append(avgUs);
            sb.Append(" us)");
            sb.Append('\n');
            sb.Append("  Last Ticks       : ");
            sb.Append(last);
            sb.Append(" (");
            sb.Append(lastUs);
            sb.Append(" us)");
            return sb.ToString();
        }

        /// <summary>
        /// Print the telemetry report to the NinjaTrader output window. Called from
        /// State.Terminated AFTER EmitMetricsSummary so the two reports do not
        /// interleave.
        /// </summary>
        private void PrintPhotonTelemetryReport()
        {
            try
            {
                string report = GetPhotonTelemetryReport();
                Print(report);
            }
            catch (Exception ex)
            {
                // Telemetry emit must NEVER throw from terminate teardown.
                Print("[MORPHEUS TELEMETRY] emit failed: " + ex.Message);
            }
        }

        #endregion
    }
}
