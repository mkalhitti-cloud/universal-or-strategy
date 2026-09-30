// AtrCalc.cs
// B173: Pure static ATR calculator. No NinjaScript pipeline dependency.
// JS-001: both Compute overloads wrapped in try/catch, return 0.0 on exception.
// JS-021: no lock anywhere. Static class has no instance state.
// JS-023: no volatile fields (no fields at all).
// NT8-003: no volatile double.
// ASCII-only: no Unicode string literals.
using System;
using NinjaTrader.Gui.Chart;

namespace PropTraderTools
{
    // CYC=N/A -- static class, no instance branches
    internal static class AtrCalc
    {
        // --- Compute (pure math overload) ---
        // B175: Wilder's ATR. Seed = simple avg of first period TRs; then smooth.
        // Requires length >= 2*period+1 for full Wilder's; falls back to simple avg
        // when length < 2*period+1 (short history: period+1 bars minimum unchanged).
        // CYC=8: null(1)+period<1(2)+length<period+1(3)+seed loop(4)+
        //        Wilder's length check(5)+smooth loop(6)+simple-fallback path(7)+catch(8).
        // JS-001 PASS. JS-021: no lock. ASCII-only.
        internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
        {
            try
            {
                if (highs == null || lows == null || closes == null)
                    return 0.0;
                if (period < 1)
                    return 0.0;
                if (highs.Length < period + 1 || lows.Length < period + 1 || closes.Length < period + 1)
                    return 0.0;
                // Seed: simple average of first period TR values (indices 1..period)
                double atr = 0.0;
                for (int i = 1; i <= period; i++) // seed loop (4)
                {
                    double pc = closes[i - 1];
                    atr += Math.Max(
                        highs[i] - lows[i],
                        Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                    );
                }
                atr /= period;
                // Wilder's smoothing: apply if array is long enough (2*period+1 values)
                if (highs.Length >= 2 * period + 1) // Wilder's length check (5)
                {
                    for (int i = period + 1; i <= 2 * period; i++) // smooth loop (6)
                    {
                        double pc = closes[i - 1];
                        double tr = Math.Max(
                            highs[i] - lows[i],
                            Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                        );
                        atr = (atr * (period - 1) + tr) / period;
                    }
                }
                // else: simple average already computed -- return seed as-is (fallback)
                return atr;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        // --- Compute (NT8 runtime overload) ---
        // Extracts OHLC arrays from ChartControl and delegates to the pure math overload.
        // Array ordering: k=0 -> barsAgo=period (oldest), k=period -> barsAgo=0 (newest).
        //   For k = 0 to period (inclusive):
        //       barsAgo  = period - k
        //       highs[k] = bars.GetHigh(barsAgo)
        //       lows[k]  = bars.GetLow(barsAgo)
        //       closes[k]= bars.GetClose(barsAgo)
        // Guards:
        //   - cc == null                              -> return 0.0
        //   - cc.BarsArray == null or Count == 0      -> return 0.0
        //   - bars null or Count < period+1           -> return 0.0
        // B174: instrumentFullName param added. Scans BarsArray for matching instrument;
        // falls back to index-zero entry when instrumentFullName is null/empty (single-instrument window, correct behaviour).
        // CYC=8: cc null(1)+BarsArray null/empty(2)+idx loop(3)+candidate null(4)+
        //        hasName check(5)+bars null/count(6)+k loop(7)+catch(8). JS-001 PASS.
        // JS-021: no lock. ASCII-only.
        internal static double Compute(
            NinjaTrader.Gui.Chart.ChartControl cc,
            int period,
            string instrumentFullName = null)
        {
            try
            {
                if (cc == null)
                    return 0.0;
                if (cc.BarsArray == null || cc.BarsArray.Count == 0)
                    return 0.0;
                NinjaTrader.Data.Bars bars = null;
                bool hasName = !string.IsNullOrEmpty(instrumentFullName);
                for (int idx = 0; idx < cc.BarsArray.Count; idx++)
                {
                    var candidate = cc.BarsArray[idx]?.Bars;
                    if (candidate == null) continue;
                    if (!hasName || candidate.Instrument.FullName == instrumentFullName)
                    {
                        bars = candidate;
                        break;
                    }
                }
                // B175: read up to 2*period+1 bars for Wilder's warmup; fall back to
                // period+1 when chart has fewer bars loaded (Wilder's fallback to simple avg).
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;
                ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
                return Compute(highs, lows, closes, period);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        // B175: Extract bar OHLC into arrays. readLen bars, oldest-first (index 0 = oldest).
        // CYC=2 (base=1 + for=1). JS-001: called inside outer try/catch. ASCII-only.
        private static void ReadBarsToArrays(
            NinjaTrader.Data.Bars bars,
            int readLen,
            out double[] highs,
            out double[] lows,
            out double[] closes)
        {
            highs  = new double[readLen];
            lows   = new double[readLen];
            closes = new double[readLen];
            for (int k = 0; k < readLen; k++)
            {
                int barsAgo = readLen - 1 - k;
                highs[k]  = bars.GetHigh(barsAgo);
                lows[k]   = bars.GetLow(barsAgo);
                closes[k] = bars.GetClose(barsAgo);
            }
        }
    }
}
