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
// DW-B182-01: BUG1 -- SubmitSwapPair target limitPrice was t.Price (original ATM), must be newStop (entry).
// DW-B182-02: BUG2 -- missing QX-pattern: snapshot stop BEFORE cancel; remove IsStopPriceSubmittable guard.

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
        // CYC=6: (1) null guard, (2) flat guard, (3) isLong ternary,
        //        (4) targets==0 branch, (5) for-loop, (6) target-submit absorbed.
        // DW-B182-02: IsStopPriceSubmittable guard removed -- QX-pattern: snapshotStop already safe.

        internal static void Execute(
            Account acc,
            Instrument instr,
            double newStop,
            double snapshotStop,
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
            // DW-B182-02: snapshot already taken by caller (QX-pattern). Pass no snapshot here.
            CopyEngine.Instance.CancelQxBrackets(acc, instr);

            // (4) direction
            bool isLong = pos.MarketPosition == MarketPosition.Long; // (3)
            OrderAction stopDir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

            // (5) 0-targets path: submit one bare PTT-BE-Stop, no OCO
            if (targets == null || targets.Count == 0) // (4)
            {
                SubmitBareStopSwap(acc, instr, stopDir, snapshotStop, pos.Quantity);
                return;
            }

            // (6) with-targets path: submit OCO pairs
            int seq = CopyEngine.Instance.NextBeOcoSeq();
            for (int i = 0; i < targets.Count; i++) // (5)
            {
                var t = targets[i];
                string ocoId_i =
                    "PTT-BE-"
                    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
                    + "-"
                    + seq.ToString("D7")
                    + "-"
                    + i; // DW-B89-01: D5->D7
                SubmitSwapPair(acc, instr, stopDir, snapshotStop, newStop, ocoId_i, i, t);
            }
        }

        /// <summary>
        /// Submit bare PTT-BE-Stop StopMarket for 0-targets path.
        /// DW-B182-02: IsStopPriceSubmittable guard removed -- snapshotStop is the pre-cancel ATM price,
        ///   already valid before cancel fired, safe to resubmit (mirrors QX pattern).
        /// CYC=3: (1) try/catch, (2) bareStop null guard in submit, (3) catch.
        /// JS-001: no throw -- try/catch. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: arg6=0, arg7=snapshotStop. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue.
        /// NT8-014: "PTT-BE-Stop" starts with PTT-.
        /// </summary>
        private static void SubmitBareStopSwap(
            Account acc,
            Instrument instr,
            OrderAction stopDir,
            double snapshotStop,
            int posQty
        )
        {
            try // (1)
            {
                var bareStop = acc.CreateOrder(
                    instr,
                    stopDir,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    posQty,
                    0,            // arg6: limitPrice=0  (NT8-049)
                    snapshotStop, // arg7: stopPrice     (NT8-049) DW-B182-02: snapshotStop not newStop
                    string.Empty, // arg8: no OCO
                    "PTT-BE-Stop", // arg9: signal name  (NT8-014)
                    DateTime.MaxValue, // arg10: GTC      (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast                        (NT8-007)
                if (bareStop != null) // (2)
                    acc.Submit(new[] { bareStop });
            }
            catch (Exception ex) // (3)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Submit one OCO stop+target pair for with-targets path.
        /// DW-B182-01: target limitPrice was t.Price (original ATM target) -- must be newStop (entry/BE level).
        /// DW-B182-02: IsStopPriceSubmittable guard removed; stop uses snapshotStop (pre-cancel ATM price).
        /// CYC=3: (1) stop try/catch, (2) stop sOrd null check, (3) target try/catch.
        /// JS-001: no throw -- try/catch per order. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: stop arg6=0, arg7=snapshotStop; target arg6=newStop, arg7=0.
        /// NT8-007: (NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: PTT-BE- prefix.
        /// </summary>
        private static void SubmitSwapPair(
            Account acc,
            Instrument instr,
            OrderAction stopDir,
            double snapshotStop,
            double newStop,
            string ocoId_i,
            int i,
            (double Price, int Qty, OrderAction Action) t
        )
        {
            // Submit PTT-BE-Stop-{i+1}: StopMarket at snapshotStop (pre-cancel ATM stop price).
            // DW-B182-02: no IsStopPriceSubmittable guard -- snapshotStop was valid before cancel.
            try // (1)
            {
                var sOrd = acc.CreateOrder(
                    instr,
                    stopDir,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    t.Qty,
                    0,              // arg6: limitPrice=0    (NT8-049)
                    snapshotStop,   // arg7: stopPrice       (NT8-049) DW-B182-02: snapshotStop
                    ocoId_i,        // arg8: OCO pair i
                    "PTT-BE-Stop-" + (i + 1), // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: GTC          (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast                            (NT8-007)
                if (sOrd != null) // (2)
                    acc.Submit(new[] { sOrd });
            }
            catch (Exception ex) // (3)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // Submit PTT-BE-Target-{i+1}: Limit at newStop (entry/BE level).
            // DW-B182-01: was t.Price (original ATM target price) -- now correctly newStop (entryPrice).
            try // (4)
            {
                var tOrd = acc.CreateOrder(
                    instr,
                    t.Action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    t.Qty,
                    newStop, // arg6: limitPrice = entryPrice (NT8-049) DW-B182-01
                    0,       // arg7: stopPrice=0             (NT8-049)
                    ocoId_i, // arg8: OCO pair i
                    "PTT-BE-Target-" + (i + 1), // arg9: signal name   (NT8-014)
                    DateTime.MaxValue, // arg10: GTC                    (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast                                        (NT8-007)
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
