// src/PropTraderTools/Features/PttMoveTargetAll.cs
// B211: Move-target Ask-ALL/Bid-ALL -- iterates all accounts with position on instrument.
// Delegates per-account cancel+resubmit to PttMoveTargetOne.Execute.
// CYC=3: (1) instrument null, (2) foreach accounts.
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
{
    internal static class PttMoveTargetAll
    {
        // CYC=3. Iterates CopyEngine.Instance.AllAccounts(instrument) -- DO NOT re-implement AllAccounts.
        // Calls PttMoveTargetOne.Execute per account. JS-021: lock-free ConcurrentDictionary iteration.
        // JS-001: no throw. JS-033: synchronous void. ASCII-only.
        // Spec: B211-BEHAV-1 through B211-BEHAV-5 (propagates per-account to PttMoveTargetOne).
        internal static void Execute(Instrument instrument, bool useAsk, int bufferTicks)
        {
            // (1) instrument null guard -- defense-in-depth (caller already guards, but keep for standalone use)
            if (instrument == null)
            {
                NinjaTrader.Code.Output.Process("[PTT-MA-ALL] instrument null", PrintTo.OutputTab1);
                return;
            }
            NinjaTrader.Code.Output.Process(
                "[PTT-MA-ALL] useAsk=" + useAsk + " instr=" + instrument.FullName + " buf=" + bufferTicks + "t",
                PrintTo.OutputTab1);
            // (2) foreach all accounts with position on instrument
            int dispatched = 0;
            foreach (Account acc in CopyEngine.Instance.AllAccounts(instrument))
            {
                dispatched++;
                NinjaTrader.Code.Output.Process("[PTT-MA-ALL] acc=" + acc.Name, PrintTo.OutputTab1);
                PttMoveTargetOne.Execute(acc, instrument, useAsk, bufferTicks);
            }
            NinjaTrader.Code.Output.Process("[PTT-MA-ALL] done dispatched=" + dispatched, PrintTo.OutputTab1);
        }
    }
}
