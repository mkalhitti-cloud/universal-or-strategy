// src/PropTraderTools/Features/PttBeSwapOne.cs
// B180 DW-B180-BE-SWAP-ALL: per-chart immediate BE bracket swap.
// Replaces PttBreakEven module (DispatchModule("BE") path).
// Pattern: mirrors PttBeSwapAll.ExecuteOne but scoped to one account.
// DW-B182-02: snapshot stop BEFORE CancelQxBrackets (QX-pattern).
// CYC=7: (1) flag-gate, (2) instr null, (3) tickSize ?. , (4) tickSize ??, (5) flat guard, (6) ??-pos-qty, (7) PttBreakEvenSwap.
// B189-T1: direction = isLong ? +1.0 : -1.0 (long -> above entry; short -> below entry).
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBeSwapOne
    {
        internal static void Execute(Account acc, Instrument instr, int bufferTicks)
        {
            if (!CopyEngine.Instance.Flags.BreakEven)          // (1) feature gate
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] Blocked: Break Even requires Pro tier",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }
            if (instr == null) return;                         // (2)
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0) return;                       // (3)
            var pos = CopyEngine.Instance.FindPositionPublic(acc, instr);
            if (pos == null || pos.Quantity == 0)              // (4)
            {
                NinjaTrader.Code.Output.Process(
                    acc.Name + ": BE flat skip",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double direction = isLong ? +1.0 : -1.0;
            double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
            double entryPrice = Math.Round(raw / tickSize) * tickSize;
            // DW-B182-02: snapshot stop BEFORE cancel (QX-pattern).
            double snapshotStop = PttQuickExit.SnapshotStopPrice(acc, instr);
            var targets = CopyEngine.Instance.SnapshotBeTargetsPublic(acc, instr); // (5)
            NinjaTrader.Code.Output.Process(
                "[BE] swap: " + acc.Name + " entry=" + entryPrice
                    + " targets=" + targets.Count
                    + " snapshotStop=" + snapshotStop,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            PttBreakEvenSwap.Execute(acc, instr, entryPrice, snapshotStop, targets);
        }
    }
}
