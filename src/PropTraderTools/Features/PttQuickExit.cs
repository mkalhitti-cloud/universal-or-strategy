// PTT-COPIER-B41 -- PttQuickExit.cs
// Quick Exit: per-instrument bracket swap (1-chart scope).
// B41: 2 classes -- PttQuickExit (per-chart execution) + InstrumentDefaults (static tick mappings).
// Jane Street rules: JS-001 (no throw), JS-002 (no return null), JS-021 (no lock),
// JS-033 (no async void). OCO counter delegated to CopyEngine.NextQxOcoId().

using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
{
    /// <summary>
    /// PttQuickExit: per-chart Quick Exit bracket swap.
    /// Button scope: leader + all followers for _instrument (this chart's instrument).
    /// Idempotent: press again any time to replace PTT-QX orders with new t1/t2 offsets.
    /// </summary>
    internal sealed class PttQuickExit
    {
        // B41: OCO sequence counter lives in CopyEngine.NextQxOcoId() -- the true NT8 AddOn singleton.
        // A static field here is insufficient: NT8 can isolate class loads per chart/panel context,
        // giving each context its own static _qxSeq starting at 0 -> always "PTT-QX-00001".
        // CopyEngine._instance is a static readonly field on the AddOn-level class -- one instance
        // for the entire NT8 process regardless of how many panels or charts are open.

        /// <summary>
        /// Execute: per-chart Quick Exit bracket swap.
        /// CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3)
        ///        + isLong(4) + for-loop(5) + stop-submit null check(6) + target-submit null check(7).
        ///        (fallback guard moved to ResolveTargetCount helper -- CYC=2)
        /// HOTFIX-QUICK-T3-01: accepts targets snapshot; submits N OCO pairs instead of always 2.
        /// B71 DW-B71-02: skipIfFollower param added -- default true rejects follower accounts.
        /// B78 DW-B63-01: leaderStop + leaderTargetCount fallbacks for follower accounts whose
        ///   ATM brackets have not yet arrived in acc.Orders at QX fire time (NT8 async lag).
        /// JS-001: no throw -- logs instead. JS-021: no lock -- CopyEngine.NextQxOcoId uses Interlocked.
        /// NT8-007: CreateOrder arg12 = (CustomOrder)null. NT8-013: DateTime.MaxValue for GTC.
        /// NT8-014: signal name = "PTT-QX-*". NT8-049: Limit arg6=limitPrice, arg7=0; StopMarket arg6=0, arg7=stopPrice.
        /// </summary>
        internal void Execute(
            Account leader,
            Instrument instr,
            int t1Ticks,
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            bool skipIfFollower = true,
            double leaderStop = 0,
            int leaderTargetCount = 0
        )
        {
            // Step 1: null/flat guard
            Position pos = null;
            if (leader != null)
                foreach (Position p in leader.Positions)
                    if (p.Instrument == instr)
                    {
                        pos = p;
                        break;
                    }
            if (pos == null || pos.Quantity == 0)
            {
                NinjaTrader.Code.Output.Process(
                    "PTT-QX: flat skip -- " + (leader != null ? leader.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            // B71 DW-B71-02: reject follower account on direct calls (skipIfFollower=true default)
            if (skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true)
            {
                NinjaTrader.Code.Output.Process(
                    "PTT-QX: follower guard -- skip " + (leader != null ? leader.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            // Step 2: snapshot stop price before cancel.
            // B78 DW-B63-01: ResolveStop falls back to leaderStop when follower has no working stop yet.
            double snapshotStop = ResolveStop(SnapshotStopPrice(leader, instr), leaderStop);
            NinjaTrader.Code.Output.Process(
                "[PTT-QX] stop resolved: "
                    + snapshotStop
                    + " on "
                    + (leader != null ? leader.Name : "NULL"),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );

            // Step 3: cancel ATM bracket + previous PTT-QX orders
            // B77 DW-B77-01: capture snapshot of current QX candidates BEFORE cancelling.
            // Orders submitted after this point (by the Submit loop below) are NOT in the snapshot
            // and will be skipped by the 3-param CancelQxBrackets overload -- no race cancellation.
            var snapshot = CopyEngine.BuildQxSnapshot(leader, instr);
            NinjaTrader.Code.Output.Process(
                "[PTT-QX] race-guard: snapshot=" + snapshot.Count + " orders on " + leader.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);

            // Step 4: compute direction and tick
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double entryPx = pos.AveragePrice;
            double tick = instr.MasterInstrument?.TickSize ?? 0.25;

            // Step 5: targetCount -- use snapshotted targets, else leader count, else 2.
            // B78 DW-B63-01: ResolveTargetCount absorbs the fallback logic (CYC=2 helper).
            int targetCount = ResolveTargetCount(targets, leaderTargetCount);

            // Step 6: submit N OCO pairs (one stop + one limit target per pair)
            // tN = t1 * N ticks from entry (T1=t1, T2=t1*2, T3=t1*3 ... TN=t1*N).
            // Each pair gets its own OCO ID so T1 fill only cancels Stop1, not Stop2/Stop3.
            string firstOcoId = string.Empty;
            for (int i = 0; i < targetCount; i++)
                SubmitQxOcoPair(
                    leader,
                    instr,
                    isLong,
                    entryPx,
                    snapshotStop,
                    tick,
                    t1Ticks,
                    i,
                    targetCount,
                    targets,
                    pos.Quantity,
                    ref firstOcoId
                );

            // Step 7: raise PttBus.QuickExitFired (Card B: back-calc using T1 and T2 prices)
            double t1Price = isLong ? entryPx + t1Ticks * tick : entryPx - t1Ticks * tick;
            double t2Price = isLong ? entryPx + t1Ticks * 2 * tick : entryPx - t1Ticks * 2 * tick;
            PttBus.RaiseQuickExit(
                this,
                new QuickExitEventArgs(instr, entryPx, t1Price, t2Price, isLong, firstOcoId, tick)
            );
        }

        /// <summary>
        /// Compute per-iteration OCO pair params and dispatch SubmitStopOrder + SubmitTargetOrder.
        /// Extracted from PttQuickExit.Execute for-loop body (lines 111-199, minus headers).
        /// CYC=6: base(1) + tNQty ternary (targets!=null &amp;&amp; i&lt;targets.Count)=2 + tNQty&lt;=0=1 + if(i==0)firstOcoId=1 + SubmitStopOrder(0) + SubmitTargetOrder(0).
        /// JS-002: void -- ref firstOcoId carries result out. JS-001: no throw. JS-021: no lock. ASCII-only.
        /// </summary>
        private void SubmitQxOcoPair(
            Account acc,
            Instrument instr,
            bool isLong,
            double entryPx,
            double snapshotStop,
            double tick,
            int t1Ticks,
            int i,
            int targetCount,
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            int posQty,
            ref string firstOcoId
        )
        {
            int tNTicks = t1Ticks * (i + 1);
            double rawTN = isLong ? entryPx + tNTicks * tick : entryPx - tNTicks * tick;
            double tNPrice = Math.Round(rawTN / tick) * tick;

            int tNQty =
                (targets != null && i < targets.Count)
                    ? targets[i].Qty
                    : CalcTNQty(posQty, targetCount, i);

            if (tNQty <= 0)
                return; // B129: skip T2 when posQty==1 and t2Qty==0

            string ocoId_i =
                CopyEngine.Instance?.NextQxOcoId()
                ?? ("PTT-QX-" + Guid.NewGuid().ToString("N").Substring(0, 8));

            if (i == 0)
                firstOcoId = ocoId_i;

            string stopName = i == 0 ? "PTT-QX-Stop" : "PTT-QX-Stop" + (i + 1);
            string targetName = "PTT-QX-T" + (i + 1);

            SubmitStopOrder(acc, instr, isLong, tNQty, snapshotStop, ocoId_i, stopName);
            SubmitTargetOrder(acc, instr, isLong, tNQty, tNPrice, ocoId_i, targetName);
        }

        /// <summary>
        /// Submit StopMarket order for one OCO pair.
        /// Extracted from PttQuickExit.Execute loop body (lines 134-167).
        /// CYC=2: (1) snapshotStop&gt;0 guard, (2) stopOrd null check.
        /// JS-001: no throw -- catch logs. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: StopMarket arg6=0, arg7=snapshotStop (NEVER swap).
        /// NT8-007: arg11=(CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: stopName starts PTT-.
        /// </summary>
        private void SubmitStopOrder(
            Account acc,
            Instrument instr,
            bool isLong,
            int qty,
            double snapshotStop,
            string ocoId,
            string stopName
        )
        {
            if (snapshotStop <= 0)
                return;
            try
            {
                var stopOrd = acc.CreateOrder(
                    instr,
                    isLong ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    qty,
                    0,
                    snapshotStop,
                    ocoId,
                    stopName,
                    DateTime.MaxValue,
                    (CustomOrder)null
                );
                if (stopOrd != null)
                    acc.Submit(new[] { stopOrd });
                else
                    NinjaTrader.Code.Output.Process(
                        "PTT-QX: " + stopName + " null",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "PTT-QX: " + stopName + " ex -- " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Submit Limit target order for one OCO pair.
        /// Extracted from PttQuickExit.Execute loop body (lines 168-199).
        /// CYC=2: (1) try/catch, (2) tNOrd null check.
        /// JS-001: no throw -- catch logs. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: Limit arg6=tNPrice, arg7=0 (NEVER swap).
        /// NT8-007: arg11=(CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: targetName starts PTT-.
        /// </summary>
        private void SubmitTargetOrder(
            Account acc,
            Instrument instr,
            bool isLong,
            int qty,
            double tNPrice,
            string ocoId,
            string targetName
        )
        {
            try
            {
                var tNOrd = acc.CreateOrder(
                    instr,
                    isLong ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    qty,
                    tNPrice,
                    0,
                    ocoId,
                    targetName,
                    DateTime.MaxValue,
                    (CustomOrder)null
                );
                if (tNOrd != null)
                    acc.Submit(new[] { tNOrd });
                else
                    NinjaTrader.Code.Output.Process(
                        "PTT-QX: " + targetName + " null",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "PTT-QX: " + targetName + " ex -- " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Execute (compat overload): per-chart single-scope call from TradeCopierPanel.OnQuickClick.
        /// Bridges the old (t1, t2) signature to the new targets-based Execute.
        /// Passes empty targets list -> Execute falls back to 2-target behavior (t1, t1*2).
        /// CYC=1: straight delegation. HOTFIX-QUICK-T3-01: TradeCopierPanel.cs is off-limits;
        /// this shim preserves its 4-arg call without modifying that file.
        /// </summary>
        internal void Execute(
            Account leader,
            Instrument instr,
            int t1Ticks,
            int t2Ticks,
            bool skipIfFollower = true
        )
        {
            Execute(
                leader,
                instr,
                t1Ticks,
                new System.Collections.Generic.List<(double Price, int Qty)>(),
                skipIfFollower
            );
        }

        /// <summary>
        /// ResolveStop: returns own stop if > 0, else fallback (leader stop for follower accounts).
        /// B78 DW-B63-01: follower ATM brackets may not be in acc.Orders at QX fire time.
        /// CYC=1: single ternary. JS-002: returns double (never null).
        /// </summary>
        private static double ResolveStop(double own, double fallback) => own > 0 ? own : fallback;

        /// <summary>
        /// ResolveTargetCount: returns own count if > 0, else leaderCount if > 0, else 3.
        /// Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets.
        /// DW-B106: cap prevents stale prior-session partial-fill residue inflating count.
        /// DW-B63-01: fallback default changed 2 -> 3 (3-target ATM is the standard).
        /// CYC=2: two ternaries. JS-002: returns int (never null).
        /// </summary>
        private static int ResolveTargetCount(
            System.Collections.Generic.List<(double Price, int Qty)> own,
            int leaderCount
        )
        {
            int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);
            return Math.Min(raw, 3); // DW-B106: QX-ALL contract -- always exactly 3 targets
        }

        /// <summary>
        /// CalcTNQty: compute per-pair qty for fallback path (no ATM snapshot).
        /// Last pair absorbs remainder so total bracketed qty equals pos.Quantity exactly.
        /// Guard: only applies remainder logic when pos.Quantity > targetCount (avoids negative).
        /// CYC = 3: (1) is-last-pair AND (2) qty-exceeds-count, (3) remainder vs floor.
        /// JS-001: no throw. JS-002: returns int. ASCII-only.
        /// DW-B104: fixes integer division gap where Math.Max(1, qty/n)*n < qty.
        /// Verified: CalcTNQty(7,3,0)=2, (7,3,1)=2, (7,3,2)=3 -- total=7.
        ///           CalcTNQty(6,3,2)=2 -- total=6. CalcTNQty(1,3,2)=1 -- pre-existing qty<n behavior unchanged.
        /// </summary>
        private static int CalcTNQty(int totalQty, int targetCount, int i)
        {
            int floorQty = Math.Max(1, totalQty / targetCount);
            if (i == targetCount - 1 && totalQty > targetCount)
                return Math.Max(1, totalQty - floorQty * (targetCount - 1)); // DW-B104: last pair absorbs remainder
            return floorQty;
        }

        /// <summary>
        /// SnapshotStopPrice: returns the stop price of any Working/Accepted stop order for this instrument.
        /// Promoted to internal (B78) so PttGlobalQuickExit.Execute can capture leader stop before cancel.
        /// CYC=2: foreach(1), stop-type check(2). JS-002: returns double 0.0 (not null).
        /// </summary>
        internal static double SnapshotStopPrice(Account acc, Instrument instr)
        {
            foreach (var o in acc.Orders)
            {
                if (o.Instrument == null || o.Instrument.FullName != instr?.FullName)
                    continue; // HOTFIX-SNAPSHOT-STOP-INSTRREF: FullName comparison (NT8 creates separate Instrument instances per account context)
                if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted)
                    continue;
                if (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) // (2)
                    return o.StopPrice;
            }
            return 0.0;
        }
    }

    /// <summary>
    /// InstrumentDefaults: per-instrument Quick Exit tick defaults.
    /// Called from CopyEngine.GetDefaultQuickTicks() and PttQuickExit fallback paths.
    /// </summary>
    internal static class InstrumentDefaults
    {
        /// <summary>
        /// GetQuickTicks: returns (T1 ticks, T2 ticks) for the given master instrument name.
        /// MES: (4, 8) = 1pt/2pt at 0.25 tick. MGC: (2, 4) = 0.2pt/0.4pt at 0.1 tick.
        /// Default: MES ticks (4, 8). CYC=3 (null/empty guard + MES check + MGC check).
        /// JS-002: returns tuple (never returns null). ASCII-only strings.
        /// </summary>
        internal static (int t1, int t2) GetQuickTicks(string masterName)
        {
            if (string.IsNullOrEmpty(masterName))
                return (4, 8); // (1)
            if (masterName.StartsWith("MES"))
                return (4, 8); // (2)
            if (masterName.StartsWith("MGC"))
                return (2, 4); // (3)
            return (4, 8);
        }
    }
}
