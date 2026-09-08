// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs
// B34 -- PttBreakEven module: cancel stale brackets + submit BE stop for ALL accounts.
// IPttModule implementation. ModuleId = "BE".
// DW-B36-01 FIX: AllAccounts loop applies BE stop to leader AND all followers.
// DW-B33-05/06/07 FIXED: per-account isLong, bePrice, CancelStaleBrackets.
// DW-B35-TARGETS-01 FIX (B36-LaneB): SnapshotTargetsLocal + SubmitBeTargetsLocal + OCO group.
// DW-B36-OCO-01 FIX (B37-LaneA): per-pair OCO groups -- each target[i] paired with its own stop[i].
// DW-B40-OCO-02 FIX: BuildBeOcoId replaced price-int component with monotonic _beOcoSeq counter.
//   Old formula: "PTT-BE-"+accPrefix+"-"+priceInt+"-"+pairIndex  (collides on same-price re-entry)
//   New formula: "PTT-BE-"+accPrefix+"-"+seq.ToString("D7")+"-"+pairIndex  (always unique)
// Dependencies: Core/PttContracts.cs + NinjaTrader.Cbi ONLY. NO CopyEngine import.
// JS-021: no lock anywhere. JS-023: volatile int ok. JS-033: synchronous void only.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Break-even module. Cancels stale ATM brackets then submits a StopMarket
    /// order at each account's own entry price (+/- buffer ticks) for every account
    /// in ctx.AllAccounts.
    /// DW-B36-01: AllAccounts loop ensures leader AND followers both receive BE stop.
    /// DW-B33-05/06/07: per-account isLong, bePrice, and CancelStaleBrackets.
    /// </summary>
    public class PttBreakEven : IPttModule
    {
        public string ModuleId { get; private set; }
        public bool IsEnabled { get; private set; }

        // DW-B40-OCO-02: monotonic sequence counter -- now delegated to CopyEngine.NextBeOcoSeq().
        // HOTFIX-BEALL-OCO-SEQ-SHARED-01: removed per-instance _beOcoSeq. Both MoveStopToBreakEven
        // (CopyEngine) and PttBreakEven.Execute now share the same counter via CopyEngine.NextBeOcoSeq()
        // so OCO IDs are globally unique across all BE code paths. Per-instance counter caused collision:
        // first press via BE ALL set _mstbeOcoSeq=1; second press via per-chart BE set _beOcoSeq=1 on a new
        // instance => same OCO ID "PTT-BE-Sim101-00001-0" => NT8 OCO reuse error.

        public PttBreakEven()
        {
            ModuleId = "BE";
            IsEnabled = true;
        }

        /// <summary>Set enabled state (wired by TradeCopierPanel license bool). CYC=1.</summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>No PttBus subscriptions needed -- module fires BeFired, never listens. CYC=1.</summary>
        public void Initialize(IPttHostContext ctx) { }

        /// <summary>No subscriptions to unsubscribe. CYC=1.</summary>
        public void Teardown() { }

        /// <summary>
        /// Execute break-even for all accounts in ctx.AllAccounts, skipping followers.
        /// CYC=6: (1) IsEnabled guard, (2) leader null||qty, (3) foreach,
        ///        (4) IsFollowerAccount guard, (5) ExecuteOneAccount delegate, (6) RaiseBeNotify.
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped via CopyEngine.IsFollowerAccount.
        /// Called on UI thread from TradeCopierPanel button handler.
        /// JS-021: no lock. JS-033: synchronous void. JS-001: no throw -- try/catch in helpers.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled)
                return; // (1) guard
            // HOTFIX-BEALL-OCO-SEQ-SHARED-01: use global counter shared with MoveStopToBreakEven.
            int seq = CopyEngine.Instance?.NextBeOcoSeq() ?? 1;

            Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
            if (leaderPos == null || leaderPos.Quantity == 0)
                return; // (2) leader guard

            double tickSize = ctx.Instrument.MasterInstrument.TickSize;
            double buf = (double)ctx.BeBuffer;

            foreach (Account acc in ctx.AllAccounts) // (3) foreach
            {
                if (CopyEngine.Instance != null && CopyEngine.Instance.IsFollowerAccount(acc))
                    continue; // (4) follower skip
                ExecuteOneAccount(acc, ctx, buf, tickSize, seq); // (5) delegate
            }

            RaiseBeNotify(ctx, leaderPos, buf, tickSize); // (6) bus notify
        }

        /// <summary>
        /// Per-account break-even logic: position check, price validation, submit.
        /// CYC=5: (1) pos null||qty guard, (2) priceOk check, (3) reject path, (4) snapshot, (5) submit.
        /// Price validity delegated to IsBePriceOk (DW-B35-SILENT-REJECT).
        /// DW-B33-05/06/07: per-account isLong, bePrice, CancelStaleBracketsLocal.
        /// JS-021: no lock. JS-001: no throw -- try/catch in helpers.
        /// </summary>
        private void ExecuteOneAccount(
            Account acc,
            IPttHostContext ctx,
            double buf,
            double tickSize,
            int seq
        )
        {
            Position pos = FindPositionLocal(acc, ctx.Instrument);
            if (pos == null || pos.Quantity == 0)
                return; // (1)
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            // HOTFIX-BUG-BE-STOP-SHORT: Long stop goes AT/BELOW entry (fires when price drops back).
            //   Short stop goes AT/ABOVE entry (fires when price rises back).
            //   Old: (isLong ? +buf : -buf) placed short stop BELOW entry -- immediately executable.
            //   New: (isLong ? -buf : +buf) -- long stop = entry - buf*tick, short stop = entry + buf*tick.
            double bePrice = pos.AveragePrice + (isLong ? -buf : +buf) * tickSize;
            if (!IsBePriceOk(isLong, bePrice, ctx.Ask, ctx.Bid)) // (2)
            {
                string msg = BuildBeRejectMsg(acc.Name, bePrice, isLong, ctx.Ask, ctx.Bid);
                NinjaTrader.Code.Output.Process(msg, NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                ctx.WarnUser(
                    acc.Name
                        + ": BE stop rejected ("
                        + (isLong ? "above ask " : "below bid ")
                        + (isLong ? ctx.Ask : ctx.Bid).ToString("F2")
                        + ")"
                ); // (3)
                return;
            }
            var targets = SnapshotTargetsLocal(acc, ctx.Instrument); // (4)
            CancelStaleBracketsLocal(acc, ctx.Instrument);
            SubmitBeTargetsLocal(acc, ctx.Instrument, bePrice, isLong, tickSize, targets, seq); // (5)
        }

        /// <summary>
        /// Returns true if bePrice is valid to submit against live market.
        /// NT8 rule: Sell stop must be <= Ask; BuyToCover stop must be >= Bid.
        /// ask/bid <= 0.0 means no market data yet -- allow submission, NT8 handles it.
        /// CYC=3: (1) isLong branch, (2) long priceOk expr, (3) short priceOk expr.
        /// JS-001: no throw. JS-002: returns bool.
        /// </summary>
        private static bool IsBePriceOk(bool isLong, double bePrice, double ask, double bid)
        {
            if (isLong)
                return ask <= 0.0 || bePrice <= ask; // (1)(2)
            return bid <= 0.0 || bePrice >= bid; // (3)
        }

        /// <summary>
        /// Build the BE price-rejection warning string.
        /// CYC=3: (1) isLong ternary for side, (2) isLong ternary for market price, (3) string concat.
        /// JS-001: no throw. JS-002: string concat always non-null.
        /// </summary>
        private static string BuildBeRejectMsg(
            string accName,
            double bePrice,
            bool isLong,
            double ask,
            double bid
        )
        {
            string side = isLong ? "above ask" : "below bid"; // (1)
            string market = isLong ? ask.ToString("F2") : bid.ToString("F2"); // (2)
            return "[BE] WARNING: "
                + accName
                + " BE stop @ " // (3)
                + bePrice.ToString("F2")
                + " rejected -- stop "
                + side
                + " market "
                + market
                + " -- position UNPROTECTED";
        }

        /// <summary>
        /// Raise PttBus BE event with leader account context.
        /// NOTE DW-B34-RAISE-01: carries leader values only (mixed-direction deferred).
        /// CYC=2: (1) leaderIsLong ternary, (2) PttBus.RaiseBe call.
        /// JS-021: no lock. JS-001: no throw.
        /// </summary>
        private void RaiseBeNotify(
            IPttHostContext ctx,
            Position leaderPos,
            double buf,
            double tickSize
        )
        {
            bool leaderIsLong = leaderPos.MarketPosition == MarketPosition.Long; // (1)
            // HOTFIX-BUG-BE-STOP-SHORT: keep in sync with ExecuteOneAccount sign fix.
            // Long: entry - buf*tick (stop below entry). Short: entry + buf*tick (stop above entry).
            double leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? -buf : +buf) * tickSize;
            PttBus.RaiseBe(
                this,
                new BeEventArgs( // (2)
                    ctx.Instrument,
                    leaderBePrice,
                    leaderPos.AveragePrice,
                    leaderIsLong,
                    string.Empty
                )
            );
        }

        // ---------------------------------------------------------------------
        // PRIVATE HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Cancel stale Working/Initialized orders (excluding PTT-BE-* prefix) for given account.
        /// NT8-051: NT8 sim does not auto-cancel ATM brackets when position goes flat.
        /// NT8-006: NO LINQ -- explicit foreach instead of .Where().
        /// CYC=3: (1) null guard, (2) foreach+conditions, (3) Count==0 early return.
        /// HOTFIX-QX-DOUBLE-01: Added TriggerPending, Submitted, Accepted to match CancelQxBrackets coverage.
        ///   Without this, BE button pressed quickly after ATM fill misses brackets in pre-Working states.
        ///   NT8_FULL_REFERENCE.md line 946: TriggerPending = "Order is pending submission."
        /// HOTFIX-ATM-T3-CANCEL-01: notBe changed from exact-match to StartsWith prefix guard.
        /// JS-021: no lock.
        /// </summary>
        private static void CancelStaleBracketsLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null)
                return;

            var stale = new List<Order>();
            foreach (Order o in acc.Orders)
            {
                if (o == null)
                    continue;
                if (IsStaleOrder(o, instr))
                    stale.Add(o);
            }
            if (stale.Count == 0)
                return; // (3)
            try
            {
                stale.RemoveAll(o =>
                    o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled
                ); // DW-B79-09: race guard
                acc.Cancel(stale.ToArray());
                NinjaTrader.Code.Output.Process(
                    "[BE] CancelStaleBracketsLocal: " + stale.Count + " orders cancelled",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
            catch
            { /* cancel on already-filled orders is non-fatal */
            }
        }

        /// <summary>
        /// Submit StopMarket order at bePrice for the given account.
        /// NT8-049: arg6=limitPrice=0, arg7=stopPrice=bePrice -- CRITICAL, never swap.
        /// NT8-007: arg11 = (NinjaTrader.Cbi.CustomOrder)null -- NOT a string.
        /// NT8-013: DateTime.MaxValue for GTC -- NOT DateTime.Now.
        /// NT8-014: signal "PTT-BE-Stop" -- must start with "PTT-".
        /// NT8-050: uses FindPositionLocal -- NEVER acc.Positions[instr].
        /// CYC=3: (1) null guard, (2) position guard, (3) CreateOrder+Submit try/catch.
        /// </summary>
        private static void SubmitBeStopLocal(
            Account acc,
            Instrument instr,
            double bePrice,
            bool isLong,
            string ocoId
        )
        {
            if (IsInvalidInput(acc, instr))
                return;

            Position pos = FindPositionLocal(acc, instr);
            if (pos == null || pos.Quantity == 0)
                return;

            OrderAction direction = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

            try
            {
                var order = acc.CreateOrder(
                    instr,
                    direction,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    pos.Quantity, // qty from live position
                    0, // arg6: limitPrice=0 (NT8-049)
                    bePrice, // arg7: stopPrice=bePrice (NT8-049)
                    ocoId, // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)
                    "PTT-BE-Stop", // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: gtd (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: not a string (NT8-007)
                if (order != null)
                {
                    acc.Submit(new[] { order });
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeStopLocal "
                            + direction
                            + " "
                            + pos.Quantity
                            + " @ "
                            + bePrice.ToString("F2")
                            + " on "
                            + SafeName(acc),
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] SubmitBeStopLocal EXCEPTION on " + SafeName(acc) + ": " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Returns true if order state is cancellable (Working|Initialized|Submitted|Accepted|TriggerPending).
        /// Extracted from CancelStaleBracketsLocal stateOk expression.
        /// CYC=5: five || terms in the boolean expression (one per state).
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
        /// Returns true if order o should be cancelled: stateOk, instrOk, and not a PTT-BE order.
        /// Extracted from CancelStaleBracketsLocal compound filter.
        /// CYC=3: (1) IsCancellableState call, (2) instrOk, (3) notBe.
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsStaleOrder(Order o, Instrument instr)
        {
            if (!IsCancellableState(o.OrderState))
                return false;
            if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                return false;
            return o.Name == null || !o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns true if order state is eligible for ATM target snapshot.
        /// Extracted from SnapshotTargetsLocal stateOk expression.
        /// CYC=5: five || terms (Working|Accepted|Submitted|Initialized|TriggerPending).
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsSnapshotEligibleState(OrderState s)
        {
            return s == OrderState.Working
                || s == OrderState.Accepted
                || s == OrderState.Submitted
                || s == OrderState.Initialized
                || s == OrderState.TriggerPending;
        }

        /// <summary>
        /// Returns true if acc and instr are both non-null (input validity check).
        /// Extracted from SubmitBeStopLocal null guard to remove the || operator from CCN count.
        /// CYC=1: simple &&-based positive check (no || branches for lizard to count).
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsInvalidInput(Account acc, Instrument instr)
        {
            return acc == null || instr == null;
        }

        /// <summary>
        /// Returns acc.Name or "null" when acc is null -- eliminates inline ternary from catch blocks.
        /// Extracted to reduce CCN in SubmitBeStopLocal catch block.
        /// CYC=1: single ternary. JS-002: returns string (never null -- "null" literal as sentinel).
        /// JS-021: no lock. ASCII-only.
        /// </summary>
        private static string SafeName(Account acc)
        {
            return acc != null ? acc.Name : "null";
        }

        /// <summary>
        /// Submit single bare StopMarket stop for 0-targets case in SubmitBeTargetsLocal.
        /// Extracted from the targets.Count==0 branch (lines 487-521).
        /// CYC=3: (1) barePos null|qty guard, (2) try/catch (3) bareStop null check.
        /// JS-001: no throw -- try/catch. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: arg6=0, arg7=bePrice. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue.
        /// NT8-014: "PTT-BE-Stop".
        /// </summary>
        private static void SubmitBareStop(
            Account acc,
            Instrument instr,
            OrderAction stopDirection,
            double bePrice
        )
        {
            Position barePos = FindPositionLocal(acc, instr);
            if (barePos == null || barePos.Quantity == 0)
                return;
            try
            {
                var bareStop = acc.CreateOrder(
                    instr,
                    stopDirection,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    barePos.Quantity,
                    0, // arg6: limitPrice=0 (NT8-049)
                    bePrice, // arg7: stopPrice (NT8-049)
                    string.Empty, // arg8: no OCO
                    "PTT-BE-Stop", // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: GTC (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast (NT8-007)
                if (bareStop != null)
                    acc.Submit(new[] { bareStop });
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] SubmitBareStop EXCEPTION: " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
            NinjaTrader.Code.Output.Process(
                "[BE] SubmitBeTargetsLocal: 0 targets -- bare stop submitted for " + SafeName(acc),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        /// <summary>
        /// Submit one OCO stop+target pair in SubmitBeTargetsLocal for-loop body.
        /// Extracted from per-pair block (lines 530-628).
        /// CYC=3: (1) stop-ord null check, (2) target-ord null check, (3) try/catch counted once for pair.
        /// JS-001: no throw -- try/catch per order. JS-002: void. JS-021: no lock. ASCII-only.
        /// NT8-049: stop arg6=0, arg7=bePrice; target arg6=t.Price, arg7=0.
        /// NT8-007: (NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: PTT-BE- prefix.
        /// </summary>
        private static void SubmitBePair(
            Account acc,
            Instrument instr,
            OrderAction stopDirection,
            double bePrice,
            string ocoIdI,
            int i,
            (double Price, int Qty, OrderAction Action) t
        )
        {
            // Submit PTT-BE-Stop-{i+1}
            try
            {
                var sOrd = acc.CreateOrder(
                    instr,
                    stopDirection,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    t.Qty,
                    0, // arg6: limitPrice=0 (NT8-049)
                    bePrice, // arg7: stopPrice (NT8-049)
                    ocoIdI, // arg8: OCO pair i
                    "PTT-BE-Stop-" + (i + 1), // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: GTC (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast (NT8-007)
                if (sOrd != null)
                {
                    acc.Submit(new[] { sOrd });
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeTargetsLocal Stop-"
                            + (i + 1)
                            + " "
                            + stopDirection
                            + " "
                            + t.Qty
                            + " @ "
                            + bePrice.ToString("F2")
                            + " ocoId="
                            + ocoIdI,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
                else
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeTargetsLocal Stop-" + (i + 1) + " CreateOrder null -- skip",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] SubmitBeTargetsLocal EXCEPTION Stop-" + (i + 1) + ": " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // Submit PTT-BE-Target-{i+1}
            try
            {
                var tOrd = acc.CreateOrder(
                    instr,
                    t.Action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    t.Qty,
                    t.Price, // arg6: limitPrice (NT8-049)
                    0, // arg7: stopPrice=0 (NT8-049)
                    ocoIdI, // arg8: OCO pair i
                    "PTT-BE-Target-" + (i + 1), // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: GTC (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11: cast (NT8-007)
                if (tOrd != null)
                {
                    acc.Submit(new[] { tOrd });
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeTargetsLocal Target-"
                            + (i + 1)
                            + " "
                            + t.Action
                            + " "
                            + t.Qty
                            + " @ "
                            + t.Price.ToString("F2")
                            + " ocoId="
                            + ocoIdI,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
                else
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeTargetsLocal Target-" + (i + 1) + " CreateOrder null -- skip",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] SubmitBeTargetsLocal EXCEPTION Target-" + (i + 1) + ": " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Find position for account + instrument without Positions[Instrument] indexer.
        /// NT8-050: acc.Positions[Instrument] is CS1503 in NT8 -- use foreach.
        /// CYC=2: foreach(1), if(2). Returns null if flat -- callers have null guards.
        /// </summary>
        private static Position FindPositionLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null)
                return null;
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }

        // ---------------------------------------------------------------------
        // B36-LaneB DW-B35-TARGETS-01: New helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Return true if name is an ATM Target order name (Target1..Target9, not Target0).
        /// Local private copy of CopyEngine.IsAtmTargetName pattern.
        /// Adds name[6] != '0' guard to satisfy test T2 (Target0=false).
        /// CYC=2: (1) length guard, (2) prefix+digit check.
        /// JS-021: no lock. JS-002: returns bool.
        /// </summary>
        private static bool IsAtmTargetName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 7)
                return false; // (1)
            return name.StartsWith("Target", StringComparison.Ordinal)
                && char.IsDigit(name[6])
                && name[6] != '0'; // (2)
        }

        /// <summary>
        /// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
        /// These are plain Limit orders -- LimitPrice and Quantity are readable.
        /// BUG-B42-QX-BE-01 FIX (Direction 1): BE All after Quick All must recognise QX targets.
        /// CYC=2: (1) length+null guard, (2) char-index body.
        /// JS-021: no lock. JS-002: returns bool. NT8-006: no LINQ, char primitives only.
        /// </summary>
        private static bool IsPttQxTarget(string name)
        {
            if (name == null || name.Length != 9)
                return false;
            return name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && name[8] >= '1'
                && name[8] <= '3';
        }

        /// <summary>
        /// Returns true if order o is an ATM or PTT-QX target for the given instrument.
        /// Consolidates instrOk check (&&) and name filter (||) from SnapshotTargetsLocal.
        /// CYC=3: (1) null guard, (2) instrOk &&, (3) IsAtmTargetName || IsPttQxTarget.
        /// JS-002: returns false for null inputs. JS-021: no lock. ASCII-only.
        /// IsAtmTargetName and IsPttQxTarget: calls existing helpers -- no reimplementation.
        /// </summary>
        private static bool IsSnapshotTargetOrder(Order o, Instrument instr)
        {
            if (o == null || instr == null)
                return false;
            if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                return false;
            return IsAtmTargetName(o.Name) || IsPttQxTarget(o.Name);
        }

        /// <summary>
        /// Read Working/Accepted/Submitted/Initialized/TriggerPending ATM Target orders
        /// from acc for the given instrument.
        /// NT8-006: NO LINQ -- foreach only, no .ToList()/.Where()/.Select()/.Any().
        /// Must be called BEFORE CancelStaleBracketsLocal so targets are still in a live state.
        /// REPAIR-08 DW-B79-03 Gap2: widened stateOk to match MoveStopToBreakEven Step A.
        ///   Old: Working|Accepted only -- missed targets in Submitted/Initialized/TriggerPending
        ///   on rapid ATM-fill -> BE press, producing targets=0 -> bare-stop path on BE button.
        ///   New: Working|Accepted|Submitted|Initialized|TriggerPending -- symmetric with BE-ALL.
        /// JS-002: returns empty list, never null.
        /// CYC=7: (1) null guard, (2) foreach, (3) o==null skip, (4) stateOk,
        ///        (5) IsSnapshotTargetOrder, (6) stateOk||, (7) result.Add path.
        /// E-3 extraction: IsSnapshotTargetOrder extracted (net -2 vs prior CCN=9).
        /// </summary>
        private static List<(double Price, int Qty, OrderAction Action)> SnapshotTargetsLocal(
            Account acc,
            Instrument instr
        )
        {
            var result = new List<(double, int, OrderAction)>();
            if (acc == null || instr == null)
                return result;
            foreach (Order o in acc.Orders)
            {
                if (o == null)
                    continue;
                bool stateOk = IsSnapshotEligibleState(o.OrderState);
                if (!stateOk || !IsSnapshotTargetOrder(o, instr))
                    continue; // BUG-B42-QX-BE-01
                result.Add((o.LimitPrice, o.Quantity, o.OrderAction));
                NinjaTrader.Code.Output.Process(
                    "[BE] Snapshot target: "
                        + o.Name
                        + " "
                        + o.OrderAction
                        + " "
                        + o.Quantity
                        + " @ "
                        + o.LimitPrice.ToString("F2"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
            return result;
        }

        /// <summary>
        /// Build a unique OCO group ID for a specific stop+target pair at index pairIndex.
        /// DW-B36-OCO-01 FIX: pairIndex makes each pair's ocoId distinct so Target-1 fill
        ///   cancels only Stop-1, leaving Stop-2 + Target-2 (OCO-1) intact.
        /// HOTFIX-BUG-BE-OCO-REUSE: accName.Substring(0,4) on "Sim101" and "Sim102" both
        ///   produce "Sim1" -- identical OCO ID across accounts. Fix: use full accName.
        ///   Old: prefix = accName[0..3] -> "Sim1" for Sim101 AND Sim102 -> collision.
        ///   New: prefix = full accName, truncated to 8 chars to keep ID under NT8 limits.
        /// CYC=2: (1) length guard for prefix. Pure computation, no side effects.
        /// </summary>
        // DW-B40-OCO-02 FIX: seq replaces priceInt -- monotonic, never reused.
        // HOTFIX-BUG-BE-OCO-REUSE: full accName (up to 8 chars) replaces 4-char prefix.
        private static string BuildBeOcoId(string accName, int seq, int pairIndex)
        {
            // Use up to 8 chars of accName to differentiate Sim101 vs Sim102.
            // "Sim101" -> "Sim101", "Sim102" -> "Sim102" -- no collision.
            string prefix = accName.Length >= 8 ? accName.Substring(0, 8) : accName; // (1)
            return "PTT-BE-" + prefix + "-" + seq.ToString("D7") + "-" + pairIndex.ToString();
        }

        /// <summary>
        /// Submit pre-snapshotted ATM targets as GTC Limit orders named PTT-BE-Target-N,
        /// each paired with its own StopMarket order in a dedicated OCO group.
        /// DW-B36-OCO-01 FIX: one OCO pair per target -- Target-i fill cancels only Stop-i,
        ///   never Stop-j (j != i). Mirrors NT8 ATM's own bracket layout exactly.
        /// 0-targets edge case: submit one bare stop for pos.Quantity with empty ocoId.
        /// NT8-049: StopMarket arg6=0, arg7=bePrice; Limit arg6=t.Price, arg7=0. DO NOT SWAP.
        /// NT8-007: arg11=(NinjaTrader.Cbi.CustomOrder)null.
        /// NT8-013: DateTime.MaxValue + TimeInForce.Gtc for all stops (B38 fix).
        /// NT8-014: signal names start with "PTT-".
        /// try/catch is PER-ORDER (inside loop) -- non-fatal, remaining pairs still protect.
        /// CYC=6: (1) acc/instr null guard, (2) targets null guard,
        ///        (3) 0-targets bare stop branch, (4) for loop,
        ///        (5) stop ord null check, (6) target ord null check.
        /// JS-021: no lock. JS-033: synchronous void.
        /// </summary>
        private static void SubmitBeTargetsLocal(
            Account acc,
            Instrument instr,
            double bePrice,
            bool isLong,
            double tickSize,
            List<(double Price, int Qty, OrderAction Action)> targets,
            int seq
        )
        {
            if (acc == null || instr == null)
                return;
            if (targets == null)
                return;

            OrderAction stopDirection = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

            // 0-targets edge case: single bare stop for full position qty, no OCO
            if (targets.Count == 0)
            {
                SubmitBareStop(acc, instr, stopDirection, bePrice);
                return;
            }

            // Per-pair loop: each target[i] paired with its own stop[i] in ocoIdI
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                string ocoIdI = BuildBeOcoId(acc.Name, seq, i); // DW-B40-OCO-02: seq-based, never reused
                SubmitBePair(acc, instr, stopDirection, bePrice, ocoIdI, i, t);
            }
            NinjaTrader.Code.Output.Process(
                "[BE] SubmitBeTargetsLocal: " + targets.Count + " OCO pairs for " + acc.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
    }
}
