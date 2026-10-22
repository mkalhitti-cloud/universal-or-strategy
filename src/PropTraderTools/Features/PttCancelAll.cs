// src/PropTraderTools/Features/PttCancelAll.cs
// B216 -- PttCancelAll: cancel ALL Working/Initialized orders for ALL accounts + instrument.
// Static class. No IPttModule -- called directly from TradeCopierPanel.OnCancelAllClick.
// Does NOT go through CopyEngine dispatch or PttBus. No leader dependency.
// Dependencies: NinjaTrader.Cbi ONLY + NinjaTrader.Code.Output.
// JS-021: no lock. JS-033: synchronous void. NT8-006: no LINQ -- explicit foreach.

using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Cancel-All module. Cancels all Working/Initialized orders for ALL accounts
    /// and the given instrument. No leader dependency. No copier state dependency.
    /// Called directly from TradeCopierPanel.OnCancelAllClick.
    /// </summary>
    internal static class PttCancelAll
    {
        /// <summary>
        /// Cancel all Working/Initialized orders for all accounts for instr.
        /// NT8-006: NO LINQ -- explicit foreach.
        /// CYC=5: (1) instr null, (2) foreach Account.All, (3) foreach acc.Orders,
        ///         (4) IsWorkingOrderForInstr, (5) toCancel.Count>0.
        /// JS-021: no lock.
        /// </summary>
        internal static void Execute(Instrument instr)
        {
            if (instr == null)
                return; // (1)

            var toCancel = new List<Order>();
            int totalOrders = 0;
            int accountsActed = 0;

            foreach (Account acc in Account.All) // (2)
            {
                toCancel.Clear();
                foreach (Order o in acc.Orders) // (3)
                {
                    if (IsWorkingOrderForInstr(o, instr)) // (4)
                        toCancel.Add(o);
                }
                if (toCancel.Count > 0) // (5)
                {
                    totalOrders += toCancel.Count;
                    try
                    {
                        acc.Cancel(toCancel.ToArray());
                        accountsActed++;
                    }
                    catch
                    { /* cancel on already-filled orders is non-fatal */ }
                }
            }

            NinjaTrader.Code.Output.Process(
                "[CANCEL-ALL] " + totalOrders + " orders cancelled across "
                    + accountsActed + " accounts for " + instr.FullName,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        /// <summary>
        /// Returns true if order o is Working or Initialized for instr.
        /// Unrestricted -- cancels entry orders AND target orders (unlike PttCancel which
        /// is semantically entry-focused).
        /// CYC=4: (1) o null, (2) stateOk (||), (3) o.Instrument null, (4) FullName match.
        /// JS-002: returns bool. ASCII-only.
        /// </summary>
        private static bool IsWorkingOrderForInstr(Order o, Instrument instr)
        {
            if (o == null)
                return false; // (1)
            bool stateOk =
                o.OrderState == OrderState.Working
                || o.OrderState == OrderState.Initialized; // (2)
            bool instrOk =
                o.Instrument != null                        // (3)
                && o.Instrument.FullName == instr.FullName; // (4)
            return stateOk && instrOk;
        }
    }
}
