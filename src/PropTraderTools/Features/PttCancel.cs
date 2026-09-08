// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs
// B33 -- PttCancel module: cancel all working entry orders for leader + instrument.
// IPttModule implementation. ModuleId = "CANCEL".
// Dependencies: Core/PttContracts.cs + NinjaTrader.Cbi ONLY. NO CopyEngine import.
// JS-021: no lock. JS-033: synchronous void. NT8-006: no LINQ -- explicit foreach.

using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Cancel module. Cancels all Working/Initialized orders for the leader account
    /// and instrument. Fires PttBus.CancelFired after execution.
    /// </summary>
    public class PttCancel : IPttModule
    {
        public string ModuleId { get; private set; }
        public bool IsEnabled { get; private set; }

        public PttCancel()
        {
            ModuleId = "CANCEL";
            IsEnabled = true;
        }

        /// <summary>Set enabled state (wired by TradeCopierPanel license bool). CYC=1.</summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>No PttBus subscriptions. CYC=1.</summary>
        public void Initialize(IPttHostContext ctx) { }

        /// <summary>No subscriptions to unsubscribe. CYC=1.</summary>
        public void Teardown() { }

        /// <summary>
        /// Cancel all working entry orders for leader + instrument.
        /// CYC=3: (1) IsEnabled guard, (2) instrument null guard, (3) CancelWorkingEntriesLocal + RaiseCancel.
        /// JS-021: no lock. JS-033: synchronous void.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled)
                return; // (1)
            if (ctx.LeaderAccount == null || ctx.Instrument == null)
                return; // (2)

            CancelWorkingEntriesLocal(ctx.LeaderAccount, ctx.Instrument); // (3a)

            PttBus.RaiseCancel(this, new CancelEventArgs(ctx.Instrument)); // (3b)
        }

        // ---------------------------------------------------------------------

        /// <summary>
        /// Cancel all Working/Initialized orders for given account + instrument.
        /// NT8-006: NO LINQ -- explicit foreach to build cancel list.
        /// NT8-031: only Working + Initialized states (no PendingSubmit).
        /// CYC=6: (1)(||) null guard, (2) foreach, (3) IsWorkingEntryOrder, (4) Count==0 return,
        ///         (5) try/catch.
        /// JS-021: no lock.
        /// </summary>
        private static void CancelWorkingEntriesLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null)
                return; // (1)

            var toCancel = new List<Order>();
            foreach (Order o in acc.Orders) // (2)
            {
                if (o == null)
                    continue;
                if (IsWorkingEntryOrder(o, instr)) // (3)
                    toCancel.Add(o);
            }
            if (toCancel.Count == 0)
                return; // (4)
            try // (5)
            {
                acc.Cancel(toCancel.ToArray());
                NinjaTrader.Code.Output.Process(
                    "[CANCEL] CancelWorkingEntriesLocal: " + toCancel.Count + " orders cancelled",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
            catch
            { /* cancel on already-filled orders is non-fatal */
            }
        }

        /// <summary>
        /// Returns true if the order state is one of the five active/live states that
        /// should be cancelled: Working, Initialized, Submitted, Accepted, TriggerPending.
        /// CYC=5: base(1) + four || branches.
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsCancellableState(OrderState s)
        {
            return s == OrderState.Working
                || s == OrderState.Initialized
                || s == OrderState.Submitted
                || s == OrderState.Accepted
                || s == OrderState.TriggerPending;
        }

        /// <summary>
        /// Returns true if order o should be added to the cancel list: non-null, correct
        /// instrument, active cancellable state, and an entry order action (Buy or SellShort).
        /// Protective stop/target orders (Sell, BuyToCover) are excluded by the actionOk guard.
        /// CYC=4: (1) o null check, (2) IsCancellableState delegate,
        ///         (3) instrOk: o.Instrument!=null &amp;&amp; FullName, (4) actionOk: Buy||SellShort.
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsWorkingEntryOrder(Order o, Instrument instr)
        {
            if (o == null)
                return false; // (1)
            bool stateOk = IsCancellableState(o.OrderState); // (2)
            bool instrOk = o.Instrument != null && o.Instrument.FullName == instr.FullName; // (3)
            bool actionOk =
                o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.SellShort; // (4)
            return stateOk && instrOk && actionOk;
        }
    }
}
