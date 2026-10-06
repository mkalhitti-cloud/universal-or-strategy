// PTT-COPIER-B41 -- PttGlobalQuickExit.cs
// Quick Exit: all-accounts bracket swap (global scope).
// B41: operates on Account.All x Positions -- every account, every instrument with a non-flat position.
// Jane Street rules: JS-001 (no throw), JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// NT8-003: volatile int (NOT volatile double). NT8-021: Account.All in Loaded handler, not constructor.

using System;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// PttGlobalQuickExit: all-accounts Quick Exit bracket swap.
    /// Button scope: Account.All x every non-flat position.
    /// Works without CopyRule -- InstrumentDefaults provides fallback ticks.
    /// </summary>
    internal sealed class PttGlobalQuickExit
    {
        /// <summary>
        /// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts in the leader loop.
        /// CYC=8: combined gate(1), acc loop(2), follower guard(3), pos loop(4),
        ///        null/flat continue(5), instrument filter(6), NeedsLeaderFallbackFlatten guard(7),
        ///        delegate via ExecuteFollowers(8).
        /// B186 DW-B186-QX-ALL-SCOPE-01: instrument scope applied. Filter skips positions not on _instrument.
        /// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
        /// HOTFIX-QUICK-T3-01: snapshot target orders before cancel to determine N (targetCount).
        /// Pass targets snapshot to ExecuteOne for N-bracket submission.
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in leader loop via IsFollowerAccount.
        /// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
        ///   Pass leaderStop + targets.Count to follower ExecuteOne so follower resolves the correct
        ///   stop price and target count even when its own ATM brackets have not yet arrived.
        /// B120 DW-B129: NeedsLeaderFallbackFlatten check after SnapshotTargetOrders.
        ///   When B118 cancelled BE orders and snapshot is empty, acc.Flatten called, continue skips ExecuteOne.
        ///   After extraction CYC <= 8 (flatten guard replaces follower block in Execute budget).
        /// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
        /// </summary>
        internal void Execute(Instrument instrument)
        {
            if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-ALL] GlobalQuickExit fired instr=" + instrument.FullName,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            var engine = CopyEngine.Instance; // capture once
            foreach (Account acc in Account.All) // (2)
            {
                if (engine != null && engine.IsFollowerAccount(acc))
                    continue; // (3) follower skip
                foreach (Position pos in acc.Positions) // (4)
                {
                    if (pos == null || pos.Quantity == 0)
                        continue; // (5)
                    if (pos.Instrument?.FullName != instrument.FullName) continue; // (6) B186 DW-B186-QX-ALL-SCOPE-01
                    // B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
                    // PTT-BE-Stop-* are the stop reference when BE ALL has fired first; cancel destroys them.
                    // SnapshotTargetOrders still runs after cancel (DW-B126 race prevention maintained).
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    // B118 DW-B126: cancel PTT-BE-* BEFORE SnapshotTargetOrders to eliminate BE/QX target race.
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    // PTT-BE-* are now terminal -- target snapshot sees clean order book.
                    var targets = SnapshotTargetOrders(acc, pos.Instrument);
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    NinjaTrader.Code.Output.Process(
                        "[PTT-QX-ALL] leader: "
                            + acc.Name
                            + " "
                            + pos.Instrument.FullName
                            + " qty="
                            + pos.Quantity
                            + " t1="
                            + ticks.t1
                            + " stop="
                            + leaderStop,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    // B120 DW-B129: flatten guard -- when B118 cancelled BE orders AND
                    // snapshot is empty AND leader has open position, flatten at market.
                    if (NeedsLeaderFallbackFlatten(_beCancelCount, targets.Count, pos.Quantity)) // (7)
                    {
                        NinjaTrader.Code.Output.Process(
                            "[PTT-QX-FLATTEN] leader fallback flatten: "
                                + acc.Name
                                + " "
                                + pos.Instrument.FullName
                                + " qty="
                                + pos.Quantity,
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                        acc.Flatten(new[] { pos.Instrument });
                        continue; // skip ExecuteOne -- flatten handles the exit
                    }
                    var effectiveTargets = ResolveEffectiveTargets(targets, 3);
                    ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets,
                        leaderStop: leaderStop, leaderTargetCount: 3);
                    // B71 DW-B71-04: place PTT-QX on every follower that has an open position
                    ExecuteFollowers(acc, pos, targets, ticks, leaderStop); // (8)
                }
            }
        }

        /// <summary>
        /// Execute (forced 2-target): global Quick Exit with caller-supplied target list.
        /// Skips SnapshotTargetOrders -- forcedTargets are used directly.
        /// DW-B133: QAll2t button path. Logs "[PTT-QX-2T-ALL]" to distinguish from no-arg path.
        /// CYC=8: combined-gate(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
        ///        pos-loop(5), null/flat/instr-continue(6), flatten-guard(7), ExecuteFollowers-call(8).
        /// B186 DW-B186-QX-ALL-SCOPE-02: instrument scope applied. Filter folded into branch (6).
        /// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
        /// JS-021: no lock. JS-001: no throw. JS-002: early return not null.
        /// JS-033: synchronous void. ASCII-only.
        /// </summary>
        internal void Execute(
            System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
            Instrument instrument
        )
        {
            if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
            if (forcedTargets == null || forcedTargets.Count < 2) // (2)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-2T-ALL] forcedTargets null or empty -- aborting",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            var engine = CopyEngine.Instance;
            foreach (Account acc in Account.All) // (3)
            {
                if (engine != null && engine.IsFollowerAccount(acc))
                    continue; // (4)
                foreach (Position pos in acc.Positions) // (5)
                {
                    if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
                        continue; // (6) extended -- instrument filter folded in
                    // B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    NinjaTrader.Code.Output.Process(
                        "[PTT-QX-2T-ALL] leader: "
                            + acc.Name
                            + " "
                            + pos.Instrument.FullName
                            + " qty="
                            + pos.Quantity
                            + " forcedTargetCount="
                            + forcedTargets.Count,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    if (
                        NeedsLeaderFallbackFlatten(
                            _beCancelCount,
                            forcedTargets.Count,
                            pos.Quantity
                        )
                    ) // (7)
                    {
                        NinjaTrader.Code.Output.Process(
                            "[PTT-QX-2T-FLATTEN] leader fallback flatten: "
                                + acc.Name
                                + " "
                                + pos.Instrument.FullName
                                + " qty="
                                + pos.Quantity,
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                        acc.Flatten(new[] { pos.Instrument });
                        continue;
                    }
                    // B191-DIRECT: rebuild 2-target list from this account's actual position qty.
                    // forcedTargets was built from the leader panel's qty and must not be used verbatim
                    // for accounts with different contract sizes (e.g. PA-01 at 5 vs PAAPEX10 at 20).
                    var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity);
                    ExecuteOne(acc, pos.Instrument, ticks.t1, perAccTargets, leaderStop: leaderStop);
                    ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop); // (8)
                }
            }
        }

        /// <summary>
        /// ExecuteFollowers: dispatch Quick Exit to all follower accounts for the given leader position.
        /// Extracted from Execute() by B120 to maintain CYC <= 8 in Execute() after DW-B129 guard.
        /// CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).
        /// B118 DW-B126: CancelPttBeOrders + WaitForPttBeCancelled on each follower path (unchanged).
        /// JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.
        /// </summary>
        private void ExecuteFollowers(
            Account acc,
            Position pos,
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            (int t1, int t2) ticks,
            double leaderStop
        )
        {
            var engine = CopyEngine.Instance;
            var rule = engine?.FindRule(pos.Instrument); // (1)
            if (rule != null) // (1 guard)
                foreach (var follower in rule.Value.FollowerAccounts) // (2)
                {
                    if (follower == null)
                        continue; // (3)
                    // B118 DW-B126: cancel follower PTT-BE-* BEFORE snapshot (same race applies to followers).
                    int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument);
                    WaitForPttBeCancelled(follower, pos.Instrument, _fBeCancelCount, 1000);
                    var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
                    // DW-B124: when follower snapshot is empty (BE-ALL consumed native brackets),
                    // derive qty array from leader snapshot scaled by posQty ratio.
                    // Prevents CalcTNQty arithmetic fallback from wrong tranche split.
                    // B191-T3: restore followerPosQty from live position (DW-B187-01 fix).
                    // With followerPosQty>0, ScaleLeaderTargets fires when follower has no snapshot.
                    // Zero fallback preserved: flat follower -> ScaleLeaderTargets skipped -> empty list correct.
                    int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
                    followerTargets = ResolveFollowerTargets(
                        followerTargets,
                        targets,
                        followerPosQty,
                        pos.Quantity
                    );
                    NinjaTrader.Code.Output.Process(
                        "[PTT-QX-ALL] follower: "
                            + follower.Name
                            + " "
                            + pos.Instrument.FullName
                            + " leaderStop="
                            + leaderStop
                            + " leaderTargets="
                            + targets.Count,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    ExecuteOne( // (7)
                        follower,
                        pos.Instrument,
                        ticks.t1,
                        followerTargets,
                        skipIfFollower: false,
                        leaderStop: leaderStop,
                        leaderTargetCount: targets.Count
                    );
                }
        }

        /// <summary>
        /// NeedsLeaderFallbackFlatten: returns true when B118 cancelled BE orders AND snapshot is
        /// empty AND leader still has an open position. Account.Flatten is the only reliable exit.
        /// B120 DW-B129: true when B118 cancelled BE orders AND snapshot is empty AND
        ///          leader still has open position. Account.Flatten is the only reliable exit.
        /// CYC=2: one &amp;&amp; chain. JS-021: no lock. JS-001: no throw. ASCII-only.
        /// </summary>
        internal static bool NeedsLeaderFallbackFlatten(
            int beCancelCount,
            int snapshotCount,
            int posQty
        )
        {
            return beCancelCount > 0 && snapshotCount == 0 && posQty > 0;
        }

        /// <summary>
        /// ResolveEffectiveTargets: when snapshot count is less than minCount, returns empty list
        /// so ResolveTargetCount in PttQuickExit falls back to leaderTargetCount instead of own count.
        /// When count is greater than or equal to minCount (e.g. PTT-QX re-click with 3 live targets),
        /// returns targets unchanged.
        /// CYC=2: (1) base + (1) ternary. JS-002 compliant: never returns null. JS-021: no lock.
        /// </summary>
        internal static System.Collections.Generic.List<(double Price, int Qty)> ResolveEffectiveTargets(
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            int minCount
        )
        {
            return targets.Count >= minCount
                ? targets
                : new System.Collections.Generic.List<(double Price, int Qty)>();
        }

        /// <summary>
        /// ResolveQuickTicks: returns (T1, T2) from CopyRule if found, else InstrumentDefaults.
        /// CYC=2: engine null guard(1), rule found check(2).
        /// </summary>
        private static (int t1, int t2) ResolveQuickTicks(Instrument instr)
        {
            var engine = CopyEngine.Instance;
            if (engine == null)
                return InstrumentDefaults.GetQuickTicks(
                    instr?.MasterInstrument?.Name ?? string.Empty
                ); // (1)
            int t1 = engine.GlobalQuickAllT1; // HOTFIX-QUICKALL-SINGLETON-01: use shared singleton value
            int t2 = t1 * 2;
            return (t1, t2);
        }

        /// <summary>
        /// ExecuteOne: per-account Quick Exit bracket swap.
        /// HOTFIX-QUICK-T3-01: accepts targets snapshot for N-bracket submission.
        /// B78 DW-B63-01: leaderStop + leaderTargetCount forwarded to PttQuickExit.Execute.
        /// DW-B79-03: pre-cancel follower ATM+PTT-* brackets BEFORE constructing PttQuickExit
        ///   so the follower account is clean when PttQuickExit.Execute runs its own cancel step.
        ///   Mirrors the leader path: cancel first, then submit PTT-QX.
        ///   Only fires on the follower path (skipIfFollower=false).
        ///   Leader path (skipIfFollower=true) unchanged -- leader's own ATM brackets are
        ///   already Working and cancelled by PttQuickExit.Execute's internal snapshot logic.
        /// CYC=2: follower guard(1) + delegate(2).
        /// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
        /// </summary>
        private void ExecuteOne(
            Account acc,
            Instrument instr,
            int t1Ticks,
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            bool skipIfFollower = true,
            double leaderStop = 0,
            int leaderTargetCount = 0
        )
        {
            // DW-B79-03: pre-cancel follower ATM + prior PTT-* brackets BEFORE PttQuickExit snapshot.
            // When follower ATM brackets exist in any cancellable state (Working/Accepted/Submitted/
            // Initialized/TriggerPending), this cancel fires first -- identical to what the leader
            // path does naturally (leader ATM brackets are always Working at QX-ALL fire time and
            // cancelled by PttQuickExit.Execute's BuildQxSnapshot/CancelQxBrackets).
            // After this call, follower brackets enter CancelSubmitted (excluded from
            // BuildQxSnapshot's stateOk) -- PttQuickExit's internal cancel is a no-op.
            // NT8 sim confirms the cancel before PTT-QX Submit completes, preventing the conflict.
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                // B114 DW-B119: arm cancel-after cleanup BEFORE executor.Execute so that
                // OnOrderUpdate finds the map entry when PTT-QX-T* goes Working.
                // In NT8 Sim, SubmitOrder dispatches OnOrderUpdate synchronously on the same
                // call stack -- TryAdd after Execute is too late (map empty when Working fires).
                // DW-B121: TTL increased 2s -> 10s. Execute() loops 4 accounts sequentially;
                // by the time NT8 fires OnOrderUpdate(Working) for Sim102 residual brackets,
                // the 2s TTL had elapsed. 10s covers the full sequential loop under SIM load.
                CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                    acc.Name,
                    (instr, DateTime.UtcNow.AddSeconds(10))
                );
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
            // Leader path (skipIfFollower=true): submit directly, no cancel-after needed.
            var leaderExecutor = new PttQuickExit(); // (2)
            leaderExecutor.Execute(
                acc,
                instr,
                t1Ticks,
                targets,
                skipIfFollower,
                leaderStop,
                leaderTargetCount
            );
        }

        /// <summary>
        /// SnapshotTargetOrders: returns list of (LimitPrice, Quantity) for active target orders
        /// on acc for instr. Covers ATM targets (Target1-Target9), PTT-QX-T* targets, and
        /// PTT-BE-Target-* targets (re-arm after prior BE). Reference: CopyEngine.MoveStopToBreakEven Step A.
        /// CYC=5: null guard(1), foreach(2), stateOk(3), isTarget(4), dedup loop(5).
        /// JS-002: returns list (never null). ASCII-only. JS-021: no lock.
        /// DW-B123: dedup nativeTargets by limit price, keeping highest qty per price level.
        /// NT8 partial-fill entries (DAY TimeInForce) create new bracket objects per fill stage,
        /// leaving stale gen-1 Target1(qty=1) Working alongside valid gen-3 Target1(qty=3).
        /// Without dedup, count inflates (e.g. 4 instead of 3) and qty split is garbage.
        /// Option B chosen: O(N) dictionary pass, stable for standard ATM templates where
        /// all bracket generations target the same limit price.
        /// </summary>
        private static System.Collections.Generic.List<(
            double Price,
            int Qty
        )> SnapshotTargetOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            var nativeTargets = new System.Collections.Generic.List<(double Price, int Qty)>();
            var pttTargets = new System.Collections.Generic.List<(double Price, int Qty)>();
            if (acc == null || instr == null)
                return nativeTargets; // JS-002: empty list, never null
            foreach (NinjaTrader.Cbi.Order o in acc.Orders)
            {
                if (o == null)
                    continue;
                if (!IsTargetOrder(o, instr))
                    continue;
                bool isNative =
                    o.Name.StartsWith("Target", StringComparison.Ordinal)
                    && o.Name.Length > 6
                    && char.IsDigit(o.Name[6]);
                bool isPtt =
                    (
                        o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && o.Name.Length > 8
                        && char.IsDigit(o.Name[8])
                    ) || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
                if (isNative)
                    nativeTargets.Add((o.LimitPrice, o.Quantity));
                else if (isPtt)
                    pttTargets.Add((o.LimitPrice, o.Quantity));
            }
            // DW-B106: if ANY native ATM targets exist, use only those for the count.
            if (nativeTargets.Count == 0)
                return pttTargets;
            // DW-B123: deduplicate nativeTargets by limit price -- keep highest qty per price.
            return DeduplicateByPrice(nativeTargets);
        }

        /// <summary>
        /// Determine if an order is a valid target for the given instrument.
        /// Extracted from SnapshotTargetOrders inner filter block (lines 449-470).
        /// CYC=3: (1) stateOk (||), (2) instrOk, (3) name non-empty + Limit type check.
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsTargetOrder(NinjaTrader.Cbi.Order o, NinjaTrader.Cbi.Instrument instr)
        {
            bool stateOk =
                o.OrderState == NinjaTrader.Cbi.OrderState.Working
                || o.OrderState == NinjaTrader.Cbi.OrderState.Accepted;
            if (!stateOk)
                return false;
            bool instrOk = o.Instrument != null && o.Instrument.FullName == instr.FullName;
            if (!instrOk || o.OrderType != NinjaTrader.Cbi.OrderType.Limit)
                return false;
            return !string.IsNullOrEmpty(o.Name);
        }

        /// <summary>
        /// Deduplicate (Price, Qty) list by Price, keeping highest Qty per price level.
        /// Extracted from SnapshotTargetOrders dedup dictionary loop (lines 482-493).
        /// CYC=2: (1) foreach, (2) TryGetValue branch.
        /// JS-002: returns non-null List. JS-021: no lock. NT8-006: no LINQ. ASCII-only.
        /// </summary>
        private static System.Collections.Generic.List<(double Price, int Qty)> DeduplicateByPrice(
            System.Collections.Generic.List<(double Price, int Qty)> targets
        )
        {
            var deduped = new System.Collections.Generic.Dictionary<double, int>();
            foreach (var t in targets)
            {
                if (!deduped.TryGetValue(t.Price, out int existing) || t.Qty > existing)
                    deduped[t.Price] = t.Qty;
            }
            var result = new System.Collections.Generic.List<(double Price, int Qty)>(
                deduped.Count
            );
            foreach (var kv in deduped)
                result.Add((kv.Key, kv.Value));
            return result;
        }

        /// <summary>
        /// Returns true if order o is a non-terminal PTT-BE order for the given instrument.
        /// Used by both WaitForPttBeCancelled (collect count) and CancelPttBeOrders (filter).
        /// CYC=4: (1) o null check, (2) instrOk, (3) IsPttBeOrder, (4) IsNonTerminalPttBeState.
        /// JS-002: returns bool. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsNonTerminalForInstr(
            NinjaTrader.Cbi.Order o,
            NinjaTrader.Cbi.Instrument instr
        )
        {
            if (o == null)
                return false;
            if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                return false;
            if (!IsPttBeOrder(o.Name))
                return false;
            return IsNonTerminalPttBeState(o.OrderState);
        }

        /// <summary>
        /// ScaleLeaderTargets: scales leader target qty array to follower position size.
        /// Uses proportional rounding with last-tranche residual correction to ensure sum == followerPosQty.
        /// CYC=3: leaderPosQty guard(1), last-tranche branch(2), loop(3).
        /// JS-002: never returns null -- returns initialized list.
        /// JS-021: no lock. JS-001: no throw. JS-033: synchronous static. ASCII-only.
        /// </summary>
        internal static System.Collections.Generic.List<(double Price, int Qty)> ScaleLeaderTargets(
            System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
            int followerPosQty,
            int leaderPosQty
        )
        {
            var result = new System.Collections.Generic.List<(double Price, int Qty)>(
                leaderTargets.Count
            );
            if (leaderPosQty <= 0)
                return result;
            int allocated = 0;
            for (int i = 0; i < leaderTargets.Count; i++)
            {
                int qty;
                if (i == leaderTargets.Count - 1)
                    qty = Math.Max(1, followerPosQty - allocated);
                else
                    qty = Math.Max(
                        1,
                        (int)
                            Math.Round((double)leaderTargets[i].Qty * followerPosQty / leaderPosQty)
                    );
                allocated += qty;
                result.Add((leaderTargets[i].Price, qty));
            }
            return result;
        }

        /// <summary>
        /// ResolveFollowerTargets: returns follower snapshot if non-empty; otherwise scales leader targets.
        /// Partial snapshot (0 &lt; count &lt; leaderCount) falls through to ScaleLeaderTargets (DW-B125 fix).
        /// Preserves DW-B120 CalcTNQty fallback path when both snapshot and leader are empty.
        /// CYC=4: partial-reject guard(1a), count-match guard(1b), empty-leader/zero-qty guard(2), delegate(3).
        /// JS-002: never returns null. JS-021: no lock. JS-001: no throw. ASCII-only.
        /// </summary>
        internal static System.Collections.Generic.List<(
            double Price,
            int Qty
        )> ResolveFollowerTargets(
            System.Collections.Generic.List<(double Price, int Qty)> followerSnapshot,
            System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
            int followerPosQty,
            int leaderPosQty
        )
        {
            // DW-B125: reject partial snapshots -- only trust follower snapshot
            // when it has the same count as the leader snapshot.
            // Partial count (0 < count < leaderCount) means some PTT-BE-Target-*
            // orders are still in-flight; treat as empty and scale from leader.
            if (
                followerSnapshot.Count > 0
                && (leaderTargets.Count == 0 || followerSnapshot.Count == leaderTargets.Count)
            )
                return followerSnapshot; // (1) full match or no leader baseline
            if (leaderTargets.Count == 0 || followerPosQty <= 0)
                return followerSnapshot;
            return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);
        }

        /// <summary>
        /// CancelPttBeOrders: cancel all PTT-BE-Target-* and PTT-BE-Stop-* orders in
        /// non-terminal states on acc for instr. Returns count of orders submitted for cancel.
        /// Called before SnapshotTargetOrders on both leader and follower paths in Execute()
        /// to eliminate the DW-B126 race condition.
        /// CYC=7: acc null(1), instr null(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), stateOk(7).
        /// JS-021: no lock. JS-001: no throw. JS-002: returns int (not null). ASCII-only.
        /// NT8: Account.Cancel(IEnumerable&lt;Order&gt;) -- NT8_FULL_REFERENCE.md lines 2408-2451.
        /// </summary>
        internal static int CancelPttBeOrders(
            NinjaTrader.Cbi.Account acc,
            NinjaTrader.Cbi.Instrument instr
        )
        {
            if (acc == null || instr == null)
                return 0;
            var toCancel = new System.Collections.Generic.List<NinjaTrader.Cbi.Order>();
            foreach (NinjaTrader.Cbi.Order o in acc.Orders.ToList())
            {
                if (!IsNonTerminalForInstr(o, instr))
                    continue;
                toCancel.Add(o);
            }
            if (toCancel.Count == 0)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-ALL] CancelPttBeOrders: acc="
                        + acc.Name
                        + " count=0 (no active PTT-BE orders)",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return 0;
            }
            acc.Cancel(toCancel);
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-ALL] CancelPttBeOrders: acc=" + acc.Name + " count=" + toCancel.Count,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return toCancel.Count;
        }

        /// <summary>
        /// WaitForPttBeCancelled: poll acc.Orders until all PTT-BE-* orders for instr
        /// reach terminal state or maxWaitMs elapses. Synchronous, dispatcher-safe.
        /// Called immediately after CancelPttBeOrders when expectedCount &gt; 0.
        /// CYC=7: acc/count guard(1), while(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), nonTerminal(7).
        /// JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.
        /// </summary>
        internal static void WaitForPttBeCancelled(
            NinjaTrader.Cbi.Account acc,
            NinjaTrader.Cbi.Instrument instr,
            int expectedCount,
            int maxWaitMs
        )
        {
            if (acc == null || expectedCount <= 0)
                return; // (1)
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-ALL] WaitForPttBeCancelled: acc="
                    + acc.Name
                    + " waiting count="
                    + expectedCount,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                int nonTerminal = 0;
                foreach (NinjaTrader.Cbi.Order o in acc.Orders.ToList())
                {
                    if (IsNonTerminalForInstr(o, instr))
                        nonTerminal++;
                }
                if (nonTerminal == 0)
                {
                    NinjaTrader.Code.Output.Process(
                        "[PTT-QX-ALL] WaitForPttBeCancelled: acc=" + acc.Name + " completed",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    return;
                }
                Thread.Sleep(20);
            }
            NinjaTrader.Code.Output.Process(
                "[PTT-QX-ALL] WaitForPttBeCancelled: acc="
                    + acc.Name
                    + " TIMEOUT after "
                    + maxWaitMs
                    + "ms -- proceeding",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        /// <summary>
        /// IsPttBeOrder: returns true if order name is a PTT-BE bracket order (Target or Stop).
        /// CYC=1. JS-002: no null return. JS-021: no lock. ASCII-only.
        /// StringComparison.Ordinal: deterministic, locale-independent, fastest for ASCII prefix match.
        /// </summary>
        private static bool IsPttBeOrder(string name)
        {
            return !string.IsNullOrEmpty(name)
                && (
                    name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                    || name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)
                );
        }

        /// <summary>
        /// IsNonTerminalPttBeState: returns true if order state is non-terminal (cancellable).
        /// Terminal states: Cancelled, Filled, PartFilled, Rejected, Unknown.
        /// CancelPending and CancelSubmitted are NON-terminal (cancel not yet confirmed by exchange).
        /// Source: NT8_FULL_REFERENCE.md lines 976-997.
        /// CYC=1. JS-001: no throw. JS-021: no lock. ASCII-only.
        /// </summary>
        private static bool IsNonTerminalPttBeState(NinjaTrader.Cbi.OrderState s)
        {
            return s != NinjaTrader.Cbi.OrderState.Cancelled
                && s != NinjaTrader.Cbi.OrderState.Filled
                && s != NinjaTrader.Cbi.OrderState.Rejected
                && s != NinjaTrader.Cbi.OrderState.PartFilled
                && s != NinjaTrader.Cbi.OrderState.Unknown;
        }
    }
}
