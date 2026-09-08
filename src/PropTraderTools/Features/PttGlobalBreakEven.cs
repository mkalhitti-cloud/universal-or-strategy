// src/PropTraderTools/Features/PttGlobalBreakEven.cs
// B39 -- Global BE All: fires SubmitBeStop for every account x every open position.
// No copy rule required. No armed state. Fires immediately on Execute().
// JS-021: no lock(). JS-023: volatile int ok. JS-002: no return null.
// JS-033: synchronous void only. NT8-003: no volatile double.
// CYC targets: Execute=5, ExecuteOne=4, IncrementBuffer=2, DecrementBuffer=2.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
        // JS-023: volatile int is allowed. NT8-003: volatile double is BANNED -- not used here.
        // _globalBeBuffer: UI-thread only (set from button handlers on dispatcher).
        private volatile int _globalBeBuffer = 0; // default 0 = exact entry price

        // B40: execution counter. Incremented in Execute() via Interlocked.Increment.
        // JS-023: volatile int allowed. NT8-003: volatile double banned -- not used here.
        // Used as a test assertion hook: test can verify Execute() was called N times.
        private volatile int _ocoSeq = 0;

        // Test seam: injectable delegate. Default calls internal CopyEngine.Instance.SubmitBeStop.
        // Production: default constructor. Tests: pass fake lambda.
        // B66: delegate updated to 4-arg to match SubmitBeStop(acc, instr, bePrice, isLong).
        // DW-B66-BE-01 fix: isLong passed at call-site read time, not re-read inside SubmitBeStop.
        private readonly Action<Account, Instrument, double, bool> _submitBeStop;

        // Production constructor -- delegates to injection constructor using inline lambda.
        // The lambda captures nothing at construction time; CopyEngine.Instance is resolved at call time.
        // B66: lambda extended to accept isLong (4th arg) and forward to SubmitBeStop.
        internal PttGlobalBreakEven()
            : this(
                (acc, instr, price, lng) => CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)
            ) { }

        // Test injection constructor. B66: delegate updated to 4-arg Action.
        internal PttGlobalBreakEven(Action<Account, Instrument, double, bool> submitBeStop)
        {
            _submitBeStop = submitBeStop;
        }

        // Production entry point -- delegates to ArmAllPendingBe via CopyEngine. CYC=1 (2 lines, no branches).
        // B40: Execute now arms/waits instead of firing immediately. Old inner loop removed.
        internal void Execute(int bufferTicks)
        {
            System.Threading.Interlocked.Increment(ref _ocoSeq);
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] GlobalBreakEven: ArmAllPendingBe buf=" + bufferTicks + "t",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            CopyEngine.Instance.ArmAllPendingBe(bufferTicks);
        }

        // Test-seam overload -- accepts injected IEnumerable<Account> so tests bypass Account.All. CYC=5.
        // Identical loop body to the production overload above; no delegation -- no CYC change to Execute(int).
        internal void Execute(IEnumerable<Account> accounts, int bufferTicks)
        {
            foreach (var acc in accounts)
            {
                foreach (var pos in acc.Positions)
                {
                    if (pos == null || pos.Quantity == 0)
                        continue;
                    ExecuteOne(acc, pos, bufferTicks);
                }
            }
        }

        // Direction-aware bePrice calculation. B35 guard inherited from SubmitBeStop.
        // CYC=4 (1 base + if + || + ternary direction). JS-002: early return void (not return null).
        private void ExecuteOne(Account acc, Position pos, int bufferTicks)
        {
            if (pos == null || pos.Quantity == 0)
                return; // defensive re-check
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double tickSize = pos.Instrument.MasterInstrument?.TickSize ?? 0.25;
            double bePrice =
                Math.Round(
                    (pos.AveragePrice + (isLong ? bufferTicks : -bufferTicks) * tickSize) / tickSize
                ) * tickSize;
            _submitBeStop(acc, pos.Instrument, bePrice, isLong);
        }

        // B40 DW-B39-OCO-01 FIX: globally unique OCO group ID prefix for BE ALL path.
        // Format: "PTT-BEG-{seq:D5}-{accIdx}-{pairIndex}"
        // seq       = monotonic per-ArmAllPendingBe-call (Interlocked.Increment on _beAllOcoSeq in engine)
        // accIdx    = index of account in the Account.All iteration (0, 1, 2...)
        // pairIndex = i from the beTargets loop in SubmitBeStop (appended as ocoOverride+"-"+i in caller)
        // CYC=1: pure expression. ASCII-only. No hex. No FontFamily.
        // internal static so CopyEngine.ArmAllPendingBe can call it without circular dependency.
        internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex) =>
            "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex;

        internal int GlobalBeBuffer => _globalBeBuffer; // CYC=1

        internal void IncrementBuffer() // CYC=2
        {
            if (_globalBeBuffer < 10)
                _globalBeBuffer++;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer); // HOTFIX-CS0070: relay -- CS0070 forbids external event invoke
        }

        internal void DecrementBuffer() // CYC=2
        {
            if (_globalBeBuffer > -10)
                _globalBeBuffer--;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer); // HOTFIX-CS0070: relay -- CS0070 forbids external event invoke
        }
    }
}
