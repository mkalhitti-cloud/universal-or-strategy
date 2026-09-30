// src/PropTraderTools/Features/PttBeSwapAll.cs
// B180 DW-B180-BE-SWAP-ALL: immediate BE bracket swap for all accounts.
// Replaces PttGlobalBreakEven.Execute(int) deferred-arm path.
// Pattern: identical to PttGlobalQuickExit.Execute() loop structure.
// DW-B182-02: snapshot stop BEFORE CancelQxBrackets in ExecuteOne (QX-pattern).
// CYC=6: flag-gate(1), acc-loop-foreach(2), follower-skip(3), &&-in-follower-check(4), flat-skip(5), flat-skip-||null(6).
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBeSwapAll
    {
        internal static void Execute(int bufferTicks)
        {
            if (!CopyEngine.Instance.Flags.BreakEven)         // (1) feature gate
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ALL] Blocked: Break Even requires Pro tier",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] PttBeSwapAll: firing buf=" + bufferTicks + "t",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            var engine = CopyEngine.Instance;
            foreach (Account acc in Account.All)               // (2)
            {
                if (engine != null && engine.IsFollowerAccount(acc))
                    continue;                                  // (3)
                foreach (Position pos in acc.Positions)
                {
                    if (pos == null || pos.Quantity == 0)
                        continue;                              // (4) flat skip
                    ExecuteOne(acc, pos, bufferTicks);         // (5)
                }
            }
        }

        // CYC=5: (1) instr null, (2) tickSize ?., (3) tickSize ??, (4) tickSize<=0, (5) RoundToTick+swap.
        // DW-B182-02: snapshot stop BEFORE cancel (QX-pattern).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private static void ExecuteOne(Account acc, Position pos, int bufferTicks)
        {
            var instr = pos.Instrument;
            if (instr == null) return;                         // (1)
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0) return;
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double direction = isLong ? -1.0 : +1.0;
            double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
            double entryPrice = Math.Round(raw / tickSize) * tickSize; // (2) RoundToTick inline
            // DW-B182-02: snapshot stop BEFORE cancel (QX-pattern).
            double snapshotStop = PttQuickExit.SnapshotStopPrice(acc, instr);
            var targets = CopyEngine.Instance.SnapshotBeTargetsPublic(acc, instr); // (3)
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] swap: " + acc.Name + " entry=" + entryPrice
                    + " targets=" + targets.Count
                    + " snapshotStop=" + snapshotStop,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            PttBreakEvenSwap.Execute(acc, instr, entryPrice, snapshotStop, targets);
        }
    }
}
