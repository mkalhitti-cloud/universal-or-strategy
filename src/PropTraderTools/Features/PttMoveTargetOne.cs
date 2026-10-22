// src/PropTraderTools/Features/PttMoveTargetOne.cs
// B211: Move-target Ask/Bid -- per-account cancel+resubmit all Working Limit exits.
// B212-MAFIX2: moves ALL working targets (was: last target only). Cancels all in one batch
//   call, then submits one new order per target at the new Ask/Bid price.
// B212-STOP-02: OCO pairs (mirrors BE pattern). One OCO id per target slot.
//   Stop slice + Limit target share the same ocoId so fill of either cancels the other.
//   Root of STOP-01 failure: standalone StopMarket with empty ocoId goes Accepted but
//   never links to targets -- target fill leaves orphaned stop that flips position.
//   Fix: per-target OCO pair, reusing CopyEngine.NextBeOcoSeq() for uniqueness.
// JS-021: no lock anywhere. JS-001: no throw -- NT8 calls wrapped try/catch log-and-swallow.
// JS-002: no null return from any method. JS-033: synchronous void.
// ASCII-only identifiers and string literals.

using System;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
{
    internal static class PttMoveTargetOne
    {
        // JS-023: Interlocked.Increment -- no lock. Unique sequence per order name.
        private static int _maSeq = 0;

        // CYC=7. Cancels ALL Working Limit exits, submits one OCO pair per target via SubmitTargetPair.
        // B212-MAFIX2: was single last-match; now collects all targets and moves each.
        //   Root: only the last match was returned -- with Target1 + Target2 both Working
        //   only one was moved per click. Fix: collect all, cancel batch, submit one per target.
        // B212-STOP-02: OCO pairs extracted to SubmitTargetPair -- Execute CYC reduced from 14->7.
        // JS-021: no lock. JS-001: no throw new. JS-033: synchronous void. ASCII-only.
        internal static void Execute(Account acc, Instrument instr, bool useAsk, int bufferTicks)
        {
            // (1) instr null guard
            if (instr == null) return;

            // (2) Compute exit price -- false if no market data
            if (!TryGetExitPrice(instr, useAsk, bufferTicks, out double exitPrice)) return;

            // (3) Position guard -- no-op if no open position or zero quantity
            var pos = CopyEngine.Instance.FindPositionPublic(acc, instr);
            if (pos == null || pos.Quantity == 0)
            {
                NinjaTrader.Code.Output.Process("[PTT-MA] no position: " + acc.Name, PrintTo.OutputTab1);
                return;
            }

            // (4) Collect ALL working targets -- no-op if none
            var targets = CollectWorkingTargets(acc, instr);
            if (targets.Length == 0)
            {
                NinjaTrader.Code.Output.Process("[PTT-MA] no target found for " + acc.Name, PrintTo.OutputTab1);
                return;
            }

            bool isLong = pos.MarketPosition == MarketPosition.Long;

            // B212-STOP-02: snapshot stop BEFORE cancel -- ATM bracket stop orphaned by target cancel.
            double snapshotStop = PttQuickExit.SnapshotStopPrice(acc, instr);

            NinjaTrader.Code.Output.Process(
                "[PTT-MA] " + (useAsk ? "Ask" : "Bid") + ": " + acc.Name
                + " price=" + exitPrice + " targets=" + targets.Length
                + " stop=" + snapshotStop,
                PrintTo.OutputTab1);

            // (5) Cancel all targets in one batch, then submit one OCO pair per target.
            //     B212-STOP-02: OCO pair logic (stop + target, same ocoId) lives in SubmitTargetPair.
            //     If snapshotStop==0: submit targets only (no bracket stop was present).
            try
            {
                acc.Cancel(targets);
                for (int pairIdx = 0; pairIdx < targets.Length; pairIdx++)
                {
                    SubmitTargetPair(acc, instr, targets[pairIdx], isLong, useAsk, exitPrice, snapshotStop, pairIdx);
                }
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[PTT-MA] Execute exception: " + ex.Message, PrintTo.OutputTab1);
            }
        }

        // CYC=8. Per-target OCO pair submission: stop (StopMarket) + target (Limit), same ocoId.
        // Extracted from Execute() to reduce Execute CYC from 14 to 7 (Jane Street CYC<=8).
        // B212-STOP-02: mirrors BE SubmitSwapPair pattern exactly.
        // JS-021: no lock. JS-001: no throw new. JS-033: synchronous void. ASCII-only.
        private static void SubmitTargetPair(
            Account acc, Instrument instr,
            Order target, bool isLong, bool useAsk,
            double exitPrice, double snapshotStop,
            int pairIdx)
        {
            // B211-PARTIAL-FILL: remaining unfilled quantity only
            int qty = target.Quantity - target.Filled;
            if (qty <= 0) return;

            OrderAction action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            string prefix = useAsk ? PttOrderNames.MaAskPrefix : PttOrderNames.MaBidPrefix;
            string targetName = prefix + Interlocked.Increment(ref _maSeq).ToString("D5");

            if (snapshotStop > 0)
            {
                // B212-STOP-02: OCO pair -- stop slice + target share same ocoId.
                // CopyEngine.NextBeOcoSeq() is a monotonic counter seeded to avoid
                // post-recompile collision (same guarantee needed here). Prefix "PTT-MA-"
                // distinguishes from BE ids ("PTT-BE-...") -- NT8 OCO is string equality.
                string ocoId = "PTT-MA-"
                    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
                    + "-" + CopyEngine.Instance.NextBeOcoSeq().ToString("D7")
                    + "-" + pairIdx;

                // Stop slice: StopMarket at snapshotStop, GTC, ocoId
                string stopName = "PTT-MA-Stop-" + Interlocked.Increment(ref _maSeq).ToString("D5");
                try
                {
                    var stopOrd = acc.CreateOrder(
                        instr,
                        action,                   // Sell (long) or BuyToCover (short)
                        OrderType.StopMarket,
                        OrderEntry.Manual,
                        TimeInForce.Gtc,
                        qty,
                        0.0,                      // arg6: limitPrice=0  (NT8-049)
                        snapshotStop,             // arg7: stopPrice     (NT8-049)
                        ocoId,                    // arg8: OCO pair
                        stopName,
                        NinjaTrader.Core.Globals.MaxDate,
                        (NinjaTrader.Cbi.CustomOrder)null);
                    if (stopOrd != null) acc.Submit(new[] { stopOrd });
                }
                catch (Exception sx)
                {
                    NinjaTrader.Code.Output.Process("[PTT-MA] stop submit ex: " + sx.Message, PrintTo.OutputTab1);
                }

                // Target: Limit at exitPrice, Day, same ocoId
                try
                {
                    var newOrder = acc.CreateOrder(
                        instr,
                        action,
                        OrderType.Limit,
                        OrderEntry.Manual,
                        TimeInForce.Day,
                        qty,
                        exitPrice,                // arg6: limitPrice    (NT8-049)
                        0.0,                      // arg7: stopPrice=0   (NT8-049)
                        ocoId,                    // arg8: OCO pair
                        targetName,
                        NinjaTrader.Core.Globals.MaxDate,
                        (NinjaTrader.Cbi.CustomOrder)null);
                    if (newOrder != null) acc.Submit(new[] { newOrder });
                }
                catch (Exception tx)
                {
                    NinjaTrader.Code.Output.Process("[PTT-MA] target submit ex: " + tx.Message, PrintTo.OutputTab1);
                }

                NinjaTrader.Code.Output.Process(
                    "[PTT-MA] pair " + pairIdx + ": " + acc.Name
                    + " target=" + targetName + " stop=" + stopName
                    + " oco=" + ocoId + " qty=" + qty,
                    PrintTo.OutputTab1);
            }
            else
            {
                // No bracket stop present -- submit target only, no OCO.
                try
                {
                    var newOrder = acc.CreateOrder(
                        instr,
                        action,
                        OrderType.Limit,
                        OrderEntry.Manual,
                        TimeInForce.Day,
                        qty,
                        exitPrice,
                        0.0,
                        string.Empty,
                        targetName,
                        NinjaTrader.Core.Globals.MaxDate,
                        (NinjaTrader.Cbi.CustomOrder)null);
                    if (newOrder != null) acc.Submit(new[] { newOrder });
                }
                catch (Exception tx)
                {
                    NinjaTrader.Code.Output.Process("[PTT-MA] target submit ex: " + tx.Message, PrintTo.OutputTab1);
                }
            }
        }

        // CYC=7. Computes Ask/Bid + (bufferTicks * tickSize) rounded to tick grid.
        // Returns false on no market data.
        // JS-002: TryGet pattern -- out double, returns bool. JS-001: no throw.
        // TESTABILITY: internal static so B211Tests can reach it via InternalsVisibleTo.
        internal static bool TryGetExitPrice(Instrument instr, bool useAsk, int bufferTicks, out double price)
        {
            // (1) tickSize null-coalescing branch
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            // (2) tickSize <= 0 guard
            if (tickSize <= 0.0) { price = 0.0; return false; }
            // (3) md == null guard
            var md = instr.MarketData;
            if (md == null) { price = 0.0; return false; }
            // (4) ternary: select Ask or Bid side
            var side = useAsk ? md.Ask : md.Bid;
            // (5) side == null guard
            if (side == null) { price = 0.0; return false; }
            double basePrice = side.Price;
            // (6) basePrice <= 0 guard
            if (basePrice <= 0.0) { price = 0.0; return false; }
            // Price rounded to tick grid
            double raw = basePrice + bufferTicks * tickSize;
            price = Math.Round(raw / tickSize) * tickSize;
            return true;
        }

        // CYC=5. Collects ALL Working Limit exits on instrument for acc.
        // B212-MAFIX2: replaces single-last-match TryFindWorkingTarget.
        //   Accepts PTT-prefixed exits (PTT-MA-*, PTT-BE-Target-*, PTT-QX-T*, PTT-TGT-*)
        //   AND native NT8 ATM targets (Target1..Target9).
        //   Returns Order[] -- never null; caller cancels all and submits one per entry.
        // Uses Order[] (array) not List<> -- NinjaScript environment lacks System.Collections.Generic
        //   in implicit scope; Order[] avoids the CS0246 'List<>' not found compile error.
        // JS-002: returns array (never null). JS-021: index-based for loop -- lock-free read.
        // JS-001: no throw. ASCII-only.
        private static NinjaTrader.Cbi.Order[] CollectWorkingTargets(Account acc, Instrument instr)
        {
            // Two-pass: count then fill (avoids List<> dependency in NinjaScript)
            int count = acc.Orders.Count;
            int found = 0;
            for (int i = 0; i < count; i++)  // pass 1: count
            {
                if (IsWorkingTarget(acc.Orders[i], instr)) found++;
            }
            if (found == 0) return new NinjaTrader.Cbi.Order[0];
            var result = new NinjaTrader.Cbi.Order[found];
            int idx = 0;
            for (int i = 0; i < count; i++)  // pass 2: fill
            {
                var o = acc.Orders[i];
                if (IsWorkingTarget(o, instr)) result[idx++] = o;
            }
            return result;
        }

        // CYC=5. Predicate: true when order is a Working Limit exit for the instrument.
        // Extracted to keep CollectWorkingTargets CYC low and share with two-pass loop.
        // JS-001: no throw. JS-021: no lock. ASCII-only.
        private static bool IsWorkingTarget(NinjaTrader.Cbi.Order o, Instrument instr)
        {
            if (o.Instrument?.FullName != instr.FullName) return false;          // (1)
            if (o.OrderState != OrderState.Working) return false;                // (2)
            if (o.OrderType != OrderType.Limit) return false;                   // (3)
            if (o.Name == null) return false;
            bool isPttExit      = o.Name.StartsWith("PTT-", StringComparison.Ordinal);
            bool isNativeTarget = o.Name.StartsWith("Target", StringComparison.Ordinal)
                && o.Name.Length > 6 && char.IsDigit(o.Name[6]);
            if (!isPttExit && !isNativeTarget) return false;                    // (4)
            if (o.OrderAction != OrderAction.Sell
                && o.OrderAction != OrderAction.BuyToCover) return false;       // (5)
            return true;
        }
    }
}
