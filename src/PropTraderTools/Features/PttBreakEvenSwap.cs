// src/PropTraderTools/Features/PttBreakEvenSwap.cs
// DW-B88 -- unified cancel+resubmit for BE-ALL trigger path.
// Replaces both the follower acc.Change() block and the leader Step B/C block
// in CopyEngine.MoveStopToBreakEven. Called once per account (leader and each follower).
// JS-001: no throw (try/catch each submit). JS-002: no return null. JS-021: no lock.
// JS-033: synchronous void only. ASCII-only strings.
// NT8-049: StopMarket arg6=0 arg7=stopPrice. Limit arg6=limitPrice arg7=0. NEVER swap.
// NT8-007: arg11=(CustomOrder)null. NT8-013: DateTime.MaxValue for GTC.
// NT8-014: signal names start with PTT-.
// CYC target: <= 8.
// DW-B89-01: D5->D7 format string. DW-B89-02: IsStopPriceSubmittable + [BE-ERR] logging.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBreakEvenSwap
    {
        // Execute: unified cancel+resubmit for BE-ALL trigger path.
        // Steps:
        //   1. Flat guard -- return if no open position.
        //   2. CancelQxBrackets(acc, instr) -- cancel ATM + PTT-QX-* + PTT-BE-* + PTT-Copy*.
        //   3. isLong from position.MarketPosition.
        //   4. If targets.Count == 0: submit bare PTT-BE-Stop for full pos.Quantity (0-targets path).
        //   5. If targets.Count > 0: for each (price, qty, action) in targets:
        //        submit PTT-BE-Stop-{i+1} StopMarket at newStop
        //        submit PTT-BE-Target-{i+1} Limit at price
        //        both on same OCO id: PTT-BE-{acc.Name[..8]}-{seq:D7}-{i}
        //        seq = CopyEngine.Instance.NextBeOcoSeq()
        // Signal names: PTT-BE-Stop-1/2/N and PTT-BE-Target-1/2/N -- UNCHANGED from today.
        // OCO id format: PTT-BE-{acc[..8]}-{seq:D7}-{i} -- DW-B89-01: D5->D7 for wider namespace.
        // CYC=8: (1) null guard, (2) flat guard, (3) isLong ternary,
        //        (4) targets==0 branch, (5) IsStopPriceSubmittable[0-targets],
        //        (6) for-loop, (7) IsStopPriceSubmittable[with-targets], (8) target-submit absorbed.
        // IsStopPriceSubmittable CYC=3: (1) isLong, (2) ask==0, (3) compare.

        // DW-B89-02: guard stop submits -- BuyToCover StopMarket is rejected if price <= ask (short pos).
        // isLong path: Sell StopMarket below market is valid for NT8 -- always allow.
        // ask==0 path: no market data available -- fail-open, let NT8 log if needed.
        // short path: stopPrice must be >= ask or NT8 rejects with "below market" error.
        /// <summary>
        /// Returns true if targets list is null or empty (0-targets guard).
        /// Extracted from Execute null||Count==0 check (net -1 from Execute CCN).
        /// CYC=2: (1) null check, (2) Count==0. JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool HasNoTargets(
            System.Collections.Generic.List<(double Price, int Qty, NinjaTrader.Cbi.OrderAction Action)> targets
        )
        {
            return targets == null || targets.Count == 0;
        }

        private static bool IsStopPriceSubmittable(Instrument instr, bool isLong, double stopPrice)
        {
            if (isLong)
                return true; // Sell StopMarket below market is fine for NT8  (1)
            double ask = instr.MarketData?.Ask?.Price ?? 0.0;
            if (ask == 0.0)
                return true; // no market data -- fail-open, let NT8 log   (2)
            return stopPrice >= ask; // (3)
        }

        internal static void Execute(
            Account acc,
            Instrument instr,
            double newStop,
            List<(double Price, int Qty, OrderAction Action)> targets
        )
        {
            // (1) null guard
            if (acc == null || instr == null)
                return;

            // (2) flat guard -- no open position, nothing to protect
            var pos = CopyEngine.Instance.FindPositionPublic(acc, instr);
            if (pos == null || pos.Quantity == 0)
                return;

            // (3) cancel ATM + PTT-QX-* + PTT-BE-* + PTT-Copy* brackets
            CopyEngine.Instance.CancelQxBrackets(acc, instr);

            // (4) direction
            bool isLong = pos.MarketPosition == MarketPosition.Long; // (3)
            OrderAction stopDir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

            // (5) 0-targets path: submit one bare PTT-BE-Stop, no OCO
            if (HasNoTargets(targets)) // (4) E-3 extraction: HasNoTargets extracted
            {
                SubmitBareStopSwap(acc, instr, isLong, stopDir, newStop, pos.Quantity);
                return;
            }

            // (6) with-targets path: submit OCO pairs
            int seq = CopyEngine.Instance.NextBeOcoSeq();
            for (int i = 0; i < targets.Count; i++) // (6)
            {
                var t = targets[i];
                string ocoId_i =
                    "PTT-BE-"
                    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
                    + "-"
                    + seq.ToString("D7")
                    + "-"
                    + i; // DW-B89-01: D5->D7
                SubmitSwapPair(acc, instr, isLong, stopDir, newStop, ocoId_i, i, t);
            }
        }

        /// <summary>
        /// Submit bare PTT-BE-Stop StopMarket for 0-targets path.
        /// Extracted from PttBreakEvenSwap.Execute 0-targets block (lines 78-119).
        /// CYC=4: (1) IsStopPriceSubmittable check, (2) try/catch, (3) bareStop null guard in submit, (4) else-log.
        /// JS-001: no throw -- try/catch. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: arg6=0, arg7=newStop. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue.
        /// NT8-014: "PTT-BE-Stop" starts with PTT-.
        /// </summary>
        private static void SubmitBareStopSwap(
            Account acc,
            Instrument instr,
            bool isLong,
            OrderAction stopDir,
            double newStop,
            int posQty
        )
        {
            if (IsStopPriceSubmittable(instr, isLong, newStop)) // (1)
            {
                try // (2)
                {
                    var bareStop = acc.CreateOrder(
                        instr,
                        stopDir,
                        OrderType.StopMarket,
                        OrderEntry.Manual,
                        TimeInForce.Gtc,
                        posQty,
                        0, // arg6: limitPrice=0  (NT8-049)
                        newStop, // arg7: stopPrice     (NT8-049)
                        string.Empty, // arg8: no OCO
                        "PTT-BE-Stop", // arg9: signal name   (NT8-014)
                        DateTime.MaxValue, // arg10: GTC          (NT8-013)
                        (NinjaTrader.Cbi.CustomOrder)null
                    ); // arg11: cast         (NT8-007)
                    if (bareStop != null) // (3)
                        acc.Submit(new[] { bareStop });
                }
                catch (Exception ex)
                {
                    NinjaTrader.Code.Output.Process(
                        "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
            else // (4)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ERR] "
                        + acc.Name
                        + " PTT-BE-Stop stop below market @ "
                        + newStop
                        + " -- skipping bare stop",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Submit one OCO stop+target pair for with-targets path.
        /// Extracted from PttBreakEvenSwap.Execute for-loop body (lines 122-204).
        /// CYC=4: (1) IsStopPriceSubmittable check, (2) stop try/catch, (3) else-log, (4) target try/catch.
        /// JS-001: no throw -- try/catch per order. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: stop arg6=0, arg7=newStop; target arg6=t.Price, arg7=0.
        /// NT8-007: (NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: PTT-BE- prefix.
        /// </summary>
        private static void SubmitSwapPair(
            Account acc,
            Instrument instr,
            bool isLong,
            OrderAction stopDir,
            double newStop,
            string ocoId_i,
            int i,
            (double Price, int Qty, OrderAction Action) t
        )
        {
            // Submit PTT-BE-Stop-{i+1}: StopMarket for this tranche qty.
            if (IsStopPriceSubmittable(instr, isLong, newStop)) // (1)
            {
                try // (2)
                {
                    var sOrd = acc.CreateOrder(
                        instr,
                        stopDir,
                        OrderType.StopMarket,
                        OrderEntry.Manual,
                        TimeInForce.Gtc,
                        t.Qty,
                        0, // arg6: limitPrice=0  (NT8-049)
                        newStop, // arg7: stopPrice     (NT8-049)
                        ocoId_i, // arg8: OCO pair i
                        "PTT-BE-Stop-" + (i + 1), // arg9: signal name   (NT8-014)
                        DateTime.MaxValue, // arg10: GTC          (NT8-013)
                        (NinjaTrader.Cbi.CustomOrder)null
                    ); // arg11: cast         (NT8-007)
                    acc.Submit(new[] { sOrd });
                }
                catch (Exception ex)
                {
                    NinjaTrader.Code.Output.Process(
                        "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
            else // (3)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ERR] "
                        + acc.Name
                        + " PTT-BE-Stop-"
                        + (i + 1)
                        + " stop below market @ "
                        + newStop
                        + " -- skipping tranche",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // Submit PTT-BE-Target-{i+1}: Limit order for this tranche.
            try // (4)
            {
                var tOrd = acc.CreateOrder(
                    instr,
                    t.Action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    t.Qty,
                    t.Price, // arg6: limitPrice    (NT8-049)
                    0, // arg7: stopPrice=0   (NT8-049)
                    ocoId_i, // arg8: OCO pair i
                    "PTT-BE-Target-" + (i + 1), // arg9: signal name   (NT8-014)
                    DateTime.MaxValue, // arg10: GTC          (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast         (NT8-007)
                if (tOrd != null)
                    acc.Submit(new[] { tOrd });
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }
    }
}
