// src/PropTraderTools/Features/PttBeSwapAll.cs
// B180 DW-B180-BE-SWAP-ALL: immediate BE bracket swap for all accounts.
// Replaces PttGlobalBreakEven.Execute(int) deferred-arm path.
// Pattern: identical to PttGlobalQuickExit.Execute() loop structure.
// DW-B182-02: snapshot stop BEFORE CancelQxBrackets in ExecuteOne (QX-pattern).
// Description: Instrument-scoped BE swap for all accounts.
// B189-T1: direction = isLong ? +1.0 : -1.0 in ExecuteOne (long -> above entry; short -> below entry).
// B208-FIX-A: removed IsFollowerAccount skip -- Clone/ATM followers have Target1/Target2/SimStop1/SimStop2
//   ATM-named brackets that CancelQxBrackets handles correctly via IsAtmBracketName. Skipping followers
//   left their ATM brackets live and never placed PTT-BE-Stop-N/PTT-BE-Target-N on them.
// CYC=5: combined-gate-||(1), acc-loop-foreach(2), pos-foreach(3), flat-skip-||(4), instr-filter(5).
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBeSwapAll
    {
        internal static void Execute(int bufferTicks, Instrument instrument)
        {
            if (!CopyEngine.Instance.Flags.BreakEven || instrument == null)  // (1) combined gate
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ALL] Blocked: BreakEven=false or instrument null",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] PttBeSwapAll: instr=" + instrument.FullName + " buf=" + bufferTicks + "t",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            foreach (Account acc in Account.All)               // (2)
            {
                foreach (Position pos in acc.Positions)        // (3)
                {
                    if (pos.Instrument?.FullName != instrument.FullName)  // (5) instrument filter
                        continue;
                    if (pos == null || pos.Quantity == 0)      // (4) flat skip
                        continue;
                    ExecuteOne(acc, pos, bufferTicks);
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
            double direction = isLong ? +1.0 : -1.0;
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
