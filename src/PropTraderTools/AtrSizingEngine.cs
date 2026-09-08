// AtrSizingEngine.cs -- B9 T1 / B10 T4 / B12 T3 / B21-LANE-A T1
// B12 T3 CHANGES:
//   1. Added _atrFraction field (plain double; no volatile per NT8-003; single-writer UI thread).
//   2. Added SetAtrFraction(double) -- CYC=1: stores fraction for OnBarUpdate. CYC=1.
//   3. Added UpdateMaxRisk(double) -- CYC=1: standalone risk setter (avoids SetParameters roundtrip).
//   4. Modified OnBarUpdate: passes atr * _atrFraction to CalcContracts.
// AtrSizingEngine.cs -- B9 T1 / B10 T4
// Detached NT8 Indicator providing ATR-based contract sizing.
// Managed by TradeCopierAddOn (one instance per chart).
// Jane Street: JS-021 (no lock), JS-023 (volatile cross-thread fields where CLR permits)
// CYC <= 8 all methods.
// NT8 Roslyn constraints:
//   - volatile double forbidden (CLR only allows volatile on <= 32-bit types and refs)
//   - State.XXX must be qualified as NinjaTrader.NinjaScript.State.XXX in Indicator subclass
//   - Add() for child indicators not needed for headless engine; ATR() called directly in OnBarUpdate
// B10 T4 additions:
//   - AtrUpdated event: fires formatted "ATR=N.NN pts -> stopTicks=T -> qty=Q" string after each bar
//   - ManualOnBarUpdate(): public shim for event-based fallback attachment path
using System;
using NinjaTrader.NinjaScript.Indicators;

namespace PropTraderTools
{
    public class AtrSizingEngine : Indicator
    {
        // Test-only seam. Do NOT use in production code.
        // Bypasses NT8 Indicator base-ctor lifecycle for unit tests.
        internal AtrSizingEngine(int testContracts)
        {
            _lastContracts = testContracts;
            _hasData = true;
        }

        // Parameterless constructor for NT8 (required by NinjaScript)
        public AtrSizingEngine() { }

        // Cross-thread fields -- written on data thread, read on UI thread (JS-023)
        // NT8 constraint: volatile is forbidden on double (64-bit); _lastAtr uses non-volatile with
        // understood staleness tolerance (sizing hint, not order-safety critical path).
        private volatile int _lastContracts = 1;
        private double _lastAtr = 0.0; // non-volatile: sizing hint only
        private volatile bool _hasData = false;

        // Configuration -- single-writer UI thread, set before attachment
        private double _maxRiskDollars = 200.0;
        private double _tickDollarValue = 5.0;

        // B12 T3 -- ATR fraction multiplier. Plain double; single-writer UI thread.
        // No volatile: NT8-003 bans volatile double. Same staleness-tolerance pattern as _lastAtr.
        private double _atrFraction = 0.75; // DW-ATR-DEFAULTS-01: default matches StartAtrEngine call-site

        [NinjaTrader.NinjaScript.NinjaScriptProperty]
        public int Period { get; set; } = 14;

        // CYC=4 (SetDefaults, Configure, DataLoaded, Terminated -- four decision branches)
        protected override void OnStateChange()
        {
            if (State == NinjaTrader.NinjaScript.State.SetDefaults)
            {
                Description = "PTT ATR Sizing Engine";
                Name = "AtrSizingEngine";
                Period = 14;
            }
            else if (State == NinjaTrader.NinjaScript.State.Configure)
            {
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 1);
            }
            else if (State == NinjaTrader.NinjaScript.State.DataLoaded)
            {
                // NT8 constraint: Add() is not valid here; ATR() accessed directly in OnBarUpdate.
            }
            else if (State == NinjaTrader.NinjaScript.State.Terminated)
            {
                _hasData = false;
                _lastContracts = 1;
                _lastAtr = 0.0;
            }
        }

        // B10 T4: AtrUpdated event fires formatted display string after each bar computation.
        // Fired on bar-close thread. Callers must marshal to UI thread via Dispatcher.InvokeAsync.
        internal event Action<string> AtrUpdated;

        // CYC=2 (CurrentBar guard + straight-line body)
        protected override void OnBarUpdate()
        {
            if (CurrentBar < Period)
                return;
            double atr = ATR(Period)[0];
            _lastAtr = atr;
            int qty = CalcContracts(atr * _atrFraction, _maxRiskDollars, _tickDollarValue); // B12 T3: scale by _atrFraction
            _lastContracts = qty;
            _hasData = true;
            FireAtrUpdated(atr, qty);
        }

        // B10 T4: public shim for event-based fallback attach path.
        // Allows TradeCopierAddOn to subscribe chart.BarsArray[0].Bars.BarUpdate
        // and forward the call when chart.NinjaScripts/Indicators.Add is unavailable.
        // CYC=1: straight-line delegation to OnBarUpdate().
        public void ManualOnBarUpdate()
        {
            OnBarUpdate();
        }

        // CYC=2: ternary guard on _tickDollarValue(1) + null-conditional invoke(2).
        // stopTicks = max_risk_dollars / tick_dollar_value (risk budget expressed as tick count).
        private void FireAtrUpdated(double atr, int qty)
        {
            int stopTicks = (int)
                Math.Round(_maxRiskDollars / (_tickDollarValue > 0 ? _tickDollarValue : 1.0));
            string display = string.Format(
                "ATR={0:F2} pts -> stopTicks={1} -> qty={2}",
                atr,
                stopTicks,
                qty
            );
            AtrUpdated?.Invoke(display);
        }

        // B12 T3 -- SetAtrFraction: stores fraction for use in next OnBarUpdate. CYC=1.
        // Single-writer UI thread. Reader (OnBarUpdate) on bar-close thread.
        // Non-volatile: sizing hint only, not order-safety critical (same as _lastAtr). NT8-003 PASS.
        internal void SetAtrFraction(double fraction)
        {
            _atrFraction = fraction;
        }

        // B12 T3 -- UpdateMaxRisk: standalone setter for _maxRiskDollars. CYC=1.
        // Allows panel to update risk budget without a full SetParameters roundtrip.
        internal void UpdateMaxRisk(double maxRiskDollars)
        {
            _maxRiskDollars = maxRiskDollars;
        }

        // CYC=1 -- straight-line, single-writer UI thread
        internal void SetParameters(double maxRiskDollars, double tickDollarValue)
        {
            _maxRiskDollars = maxRiskDollars;
            _tickDollarValue = tickDollarValue;
        }

        // CYC=2 -- guard (!_hasData) + return path
        internal int GetSuggestedQty()
        {
            if (!_hasData)
                return 1;
            return _lastContracts;
        }

        // CYC=1 -- straight-line read
        internal double GetLastAtr() => _lastAtr;

        // Pure static math -- unit-testable without NT8 context. CYC=3.
        internal static int CalcContracts(double atrPoints, double maxRisk, double tickDollarValue)
        {
            if (atrPoints <= 0)
                return 1; // guard (1): zero or negative ATR
            if (tickDollarValue <= 0)
                return 1; // guard (2): zero tick dollar value
            double riskPerContract = atrPoints * tickDollarValue;
            int contracts = (int)Math.Floor(maxRisk / riskPerContract);
            return contracts < 1 ? 1 : contracts; // guard (3): clamp minimum to 1
        }
    }
}
