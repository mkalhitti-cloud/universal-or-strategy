// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12.50 Symmetry Guard

        private sealed class SymmetryDispatchContext
        {
            public string DispatchId;
            public string TradeType;
            public MarketPosition Direction;
            public int ExpectedQuantity;
            public DateTime CreatedUtc;

            public double MasterWeightedFill;
            public int MasterFilledQuantity;
            public double MasterAnchorPrice;
            public bool IsResolved;

            public readonly object Sync = new object();
            public readonly HashSet<string> FollowerEntries = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class PendingFollowerFill
        {
            public string FleetEntryName;
            public double FleetFillPrice;
            public DateTime QueuedUtc;
        }

        private readonly ConcurrentDictionary<string, SymmetryDispatchContext> symmetryDispatchById =
            new ConcurrentDictionary<string, SymmetryDispatchContext>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, string> symmetryFleetEntryToDispatch =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, string> symmetryMasterEntryToDispatch =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, PendingFollowerFill> symmetryPendingFollowerFills =
            new ConcurrentDictionary<string, PendingFollowerFill>(StringComparer.Ordinal);

        private const int SymmetryMaxSlippageTicks = 4;
        private const double SymmetryMaxSlippageUsdPerContract = 20.0;
        private static readonly TimeSpan SymmetryAnchorWait = TimeSpan.FromMilliseconds(2000);
        private static readonly TimeSpan SymmetryDispatchTtl = TimeSpan.FromMinutes(5);

        private string SymmetryGuardBeginDispatch(string tradeType, OrderAction action, int quantity, double requestedEntryPrice)
        {
            string normalizedType = SymmetryNormalizeTradeType(tradeType);
            MarketPosition direction = (action == OrderAction.Buy || action == OrderAction.BuyToCover)
                ? MarketPosition.Long : MarketPosition.Short;

            // V12.Audit [Q4-001]: Atomic read-check-write to eliminate TOCTOU in duplicate dispatch guard.
            // Phase 7 [H-11] left the loop and insertion unguarded -- two concurrent callers could both
            // pass the "no existing dispatch" check and insert competing contexts. The entire compound
            // check-then-insert is now serialised under stateLock so the operation is atomic.
            lock (stateLock)
            {
                DateTime now = DateTime.UtcNow;

                // V12.Phase7 [H-11]: Prevent duplicate dispatches for the same signal+direction.
                // If an active (non-expired, unresolved) dispatch already exists for this trade type and direction,
                // return the existing ID instead of creating a second one that would double fleet entries.
                foreach (var kvp in symmetryDispatchById)
                {
                    var existing = kvp.Value;
                    if (existing.TradeType == normalizedType &&
                        existing.Direction == direction &&
                        !existing.IsResolved &&
                        (now - existing.CreatedUtc) < SymmetryDispatchTtl)
                    {
                        Print($"[SYMMETRY] Duplicate dispatch suppressed: {normalizedType} {direction} -- reusing {existing.DispatchId}");
                        return existing.DispatchId;
                    }
                }

                string dispatchId = $"SG_{now.Ticks}_{normalizedType}_{(int)action}";

                var ctx = new SymmetryDispatchContext
                {
                    DispatchId = dispatchId,
                    TradeType = normalizedType,
                    Direction = direction,
                    ExpectedQuantity = Math.Max(1, quantity),
                    CreatedUtc = now,
                    MasterAnchorPrice = Instrument != null
                        ? Instrument.MasterInstrument.RoundToTickSize(requestedEntryPrice)
                        : requestedEntryPrice,
                    IsResolved = false
                };

                symmetryDispatchById[dispatchId] = ctx;
                return dispatchId;
            }
        }

        private void SymmetryGuardRegisterFollower(string dispatchId, string fleetEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(fleetEntryName))
                return;

            symmetryFleetEntryToDispatch[fleetEntryName] = dispatchId;

            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Add(fleetEntryName);
            }
        }

        private void SymmetryGuardRegisterMasterEntry(string dispatchId, string masterEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(masterEntryName))
                return;
            symmetryMasterEntryToDispatch[masterEntryName] = dispatchId;
        }

        private void SymmetryGuardOnMasterFill(string entryName, PositionInfo masterPos, double averageFillPrice, int fillQty, DateTime fillTimeUtc)
        {
            if (masterPos == null || masterPos.IsFollower || averageFillPrice <= 0 || fillQty <= 0)
                return;

            SymmetryDispatchContext ctx = null;

            if (!string.IsNullOrEmpty(entryName) &&
                symmetryMasterEntryToDispatch.TryGetValue(entryName, out var mappedDispatch) &&
                symmetryDispatchById.TryGetValue(mappedDispatch, out var mappedCtx))
            {
                ctx = mappedCtx;
            }

            if (ctx == null)
            {
                string tradeType = SymmetryInferTradeType(entryName, masterPos);
                ctx = SymmetryFindDispatchForMasterFill(tradeType, masterPos.Direction, fillTimeUtc);
            }

            if (ctx == null)
                return;

            bool resolvedNow = false;
            lock (ctx.Sync)
            {
                if (!ctx.IsResolved)
                {
                    ctx.MasterWeightedFill += averageFillPrice * fillQty;
                    ctx.MasterFilledQuantity += fillQty;

                    double avg = ctx.MasterWeightedFill / Math.Max(1, ctx.MasterFilledQuantity);
                    ctx.MasterAnchorPrice = Instrument.MasterInstrument.RoundToTickSize(avg);
                    ctx.IsResolved = true;
                    resolvedNow = true;
                }
            }

            if (resolvedNow)
            {
                Print($"[SYMMETRY_GUARD] MASTER ANCHOR LOCKED | Trade={ctx.TradeType} | Anchor={ctx.MasterAnchorPrice:F2} | FillQty={ctx.MasterFilledQuantity}");

                SymmetryGuardTryResolveFollowersForDispatch(ctx.DispatchId, DateTime.UtcNow);
            }
        }

        private SymmetryDispatchContext SymmetryFindDispatchForMasterFill(string tradeType, MarketPosition direction, DateTime fillTimeUtc)
        {
            string norm = SymmetryNormalizeTradeType(tradeType);
            SymmetryDispatchContext best = null;

            foreach (var kvp in symmetryDispatchById.ToArray())
            {
                SymmetryDispatchContext ctx = kvp.Value;
                if (ctx == null || ctx.IsResolved)
                    continue;
                if (ctx.Direction != direction)
                    continue;
                if (!string.Equals(ctx.TradeType, norm, StringComparison.Ordinal))
                    continue;
                if (fillTimeUtc - ctx.CreatedUtc > SymmetryDispatchTtl)
                    continue;

                if (best == null || ctx.CreatedUtc < best.CreatedUtc)
                    best = ctx;
            }

            return best;
        }

        private bool SymmetryGuardOnFollowerFill(string fleetEntryName, PositionInfo followerPos, double followerFillPrice)
        {
            if (followerPos == null || !followerPos.IsFollower)
                return false;

            followerPos.EntryFilled = true;
            lock (stateLock)
            {
                if (followerPos.RemainingContracts <= 0)
                    followerPos.RemainingContracts = Math.Max(1, followerPos.TotalContracts);
            }

            if (!followerPos.BracketSubmitted)
            {
                bool shouldSubmitImmediately = false;
                // [ANCHOR-01] V12.Phase7.1: Pre-check master anchor before initial bracket submission.
                // If master already filled (anchor resolved), apply it now so the broker receives
                // master-anchored prices on the FIRST submission -- eliminates the "wrong-prices-first
                // + retarget" double round-trip that causes transient drift in volatile bursts.
                if (symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var preCheckId) &&
                    symmetryDispatchById.TryGetValue(preCheckId, out var preCheckCtx))
                {
                    bool anchorReady;
                    double preCheckAnchor;
                    lock (preCheckCtx.Sync)
                    {
                        anchorReady   = preCheckCtx.IsResolved;
                        preCheckAnchor = preCheckCtx.MasterAnchorPrice;
                    }
                    if (anchorReady && preCheckAnchor > 0)
                    {
                        Print($"[ANCHOR-01] Pre-applying master anchor {preCheckAnchor:F2} for {fleetEntryName} -- bracket will use master fill price");
                        SymmetryGuardApplyMasterAnchor(followerPos, preCheckAnchor);
                        shouldSubmitImmediately = true;
                    }
                }
                if (shouldSubmitImmediately)
                {
                    SymmetryGuardSubmitFollowerBracket(fleetEntryName, followerPos);
                }
                else
                {
                    Print($"[ANCHOR-GATE] Delaying follower bracket for {fleetEntryName} until master anchor resolves.");
                }
            }

            var pending = new PendingFollowerFill
            {
                FleetEntryName = fleetEntryName,
                FleetFillPrice = followerFillPrice > 0 ? followerFillPrice : followerPos.EntryPrice,
                QueuedUtc = DateTime.UtcNow
            };

            symmetryPendingFollowerFills[fleetEntryName] = pending;

            if (SymmetryGuardTryResolveFollower(fleetEntryName, followerPos, pending, DateTime.UtcNow))
                symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);

            return true;
        }

        private bool SymmetryGuardIsAnchorPending(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return false;
            return symmetryPendingFollowerFills.ContainsKey(entryName);
        }

        private void SymmetryGuardProcessPendingFollowerFills()
        {
            if (symmetryPendingFollowerFills.IsEmpty)
            {
                SymmetryGuardPruneDispatches();
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            foreach (var kvp in symmetryPendingFollowerFills.ToArray())
            {
                string fleetEntryName = kvp.Key;
                PendingFollowerFill pending = kvp.Value;

                // V12.Phase8 [F-04]: Guard activePositions read with stateLock to prevent
                // torn observations concurrent with ExecuteSmartDispatchEntry commits/removals.
                PositionInfo pos = null;
                lock (stateLock) { activePositions.TryGetValue(fleetEntryName, out pos); }
                if (pos == null || !pos.IsFollower)
                {
                    symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
                    SymmetryGuardForgetEntry(fleetEntryName);
                    continue;
                }

                if (SymmetryGuardTryResolveFollower(fleetEntryName, pos, pending, nowUtc))
                    symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
            }

            SymmetryGuardPruneDispatches();
        }

        private bool SymmetryGuardTryResolveFollower(string fleetEntryName, PositionInfo pos, PendingFollowerFill pending, DateTime nowUtc)
        {
            SymmetryDispatchContext ctx = null;
            if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var dispatchId) ||
                !symmetryDispatchById.TryGetValue(dispatchId, out ctx) ||
                ctx == null)
            {
                if (nowUtc - pending.QueuedUtc >= SymmetryAnchorWait)
                {
                    SymmetryGuardSkipFollower(fleetEntryName, pos, pending.FleetFillPrice, 0, 0, "Missing dispatch context");
                    return true;
                }
                return false;
            }

            bool isResolved;
            double masterAnchor;
            lock (ctx.Sync)
            {
                // V1101E HOT-PATCH: Snapshot dispatch state under ctx.Sync, then release before any stateLock path.
                isResolved = ctx.IsResolved;
                masterAnchor = ctx.MasterAnchorPrice;
            }

            if (!isResolved)
            {
                if (nowUtc - pending.QueuedUtc >= SymmetryAnchorWait)
                {
                    SymmetryGuardSkipFollower(fleetEntryName, pos, pending.FleetFillPrice, 0, 0, "Master anchor timeout");
                    return true;
                }
                return false;
            }

            double slippagePoints = Math.Abs(pending.FleetFillPrice - masterAnchor);
            double slippageTicks = tickSize > 0 ? slippagePoints / tickSize : 0.0;
            double slippageUsdPerContract = pointValue > 0 ? slippagePoints * pointValue : 0.0;

            bool breach = slippageTicks > SymmetryMaxSlippageTicks ||
                          slippageUsdPerContract > SymmetryMaxSlippageUsdPerContract;
            if (breach)
            {
                SymmetryGuardSkipFollower(
                    fleetEntryName,
                    pos,
                    pending.FleetFillPrice,
                    slippageTicks,
                    slippageUsdPerContract,
                    $"Slippage Buffer breach vs Master {masterAnchor:F2}");
                return true;
            }

            // [ANCHOR-02] V12.Phase7.1: Capture entry price before anchor application to detect
            // whether ANCHOR-01 already submitted the bracket with master-anchored prices.
            // If priorEntryPrice ? masterAnchor (within 1 tick), the bracket is already correct
            // and the retarget cancel+replace round-trip can be skipped.
            double priorEntryPrice;
            lock (stateLock) { priorEntryPrice = pos.EntryPrice; }

            SymmetryGuardApplyMasterAnchor(pos, masterAnchor);

            if (pos.BracketSubmitted)
            {
                bool alreadyAnchored = tickSize > 0 && Math.Abs(priorEntryPrice - masterAnchor) < tickSize;
                if (alreadyAnchored)
                {
                    Print($"[ANCHOR-02] Bracket already anchor-aligned for {fleetEntryName} (prior={priorEntryPrice:F2} anchor={masterAnchor:F2}) -- retarget skipped");
                }
                else
                {
                    SymmetryGuardRetargetExistingFollowerBracket(fleetEntryName, pos);
                }
            }
            else
            {
                SymmetryGuardSubmitFollowerBracket(fleetEntryName, pos);
            }

            Print($"[SYMMETRY_GUARD] ANCHORED | {fleetEntryName} | Master={masterAnchor:F2} Fleet={pending.FleetFillPrice:F2} Slip={slippageTicks:F1} ticks (${slippageUsdPerContract:F2}/ct) | Scalp Anchor T1={pos.Target1Price:F2} | Runner Targets=Trail");

            return true;
        }

        private void SymmetryGuardApplyMasterAnchor(PositionInfo pos, double masterAnchor)
        {
            double anchor = Instrument.MasterInstrument.RoundToTickSize(masterAnchor);

            // V12.Phase8 [F-04]: Acquire stateLock for the entire anchor update to prevent
            // torn reads from Trailing.cs observing partial price state (e.g., new stop but old targets).
            lock (stateLock)
            {
                double oldBase = pos.EntryPrice > 0 ? pos.EntryPrice : anchor;

                double stopDist = Math.Abs(oldBase - pos.InitialStopPrice);
                if (stopDist <= 0)
                    stopDist = Math.Abs(oldBase - pos.CurrentStopPrice);

                double t1Dist = Math.Abs(pos.Target1Price - oldBase);
                double t2Dist = Math.Abs(pos.Target2Price - oldBase);
                double t3Dist = Math.Abs(pos.Target3Price - oldBase);
                double t4Dist = Math.Abs(pos.Target4Price - oldBase);
                double t5Dist = Math.Abs(pos.Target5Price - oldBase);

                double stop = pos.Direction == MarketPosition.Long ? anchor - stopDist : anchor + stopDist;
                double t1 = pos.Direction == MarketPosition.Long ? anchor + t1Dist : anchor - t1Dist;
                double t2 = pos.Direction == MarketPosition.Long ? anchor + t2Dist : anchor - t2Dist;
                double t3 = pos.Direction == MarketPosition.Long ? anchor + t3Dist : anchor - t3Dist;
                double t4 = pos.Direction == MarketPosition.Long ? anchor + t4Dist : anchor - t4Dist;
                double t5 = pos.Direction == MarketPosition.Long ? anchor + t5Dist : anchor - t5Dist;

                pos.EntryPrice = anchor;
                pos.ExtremePriceSinceEntry = anchor;

                pos.InitialStopPrice = Instrument.MasterInstrument.RoundToTickSize(stop);
                pos.CurrentStopPrice = pos.InitialStopPrice;
                pos.Target1Price = Instrument.MasterInstrument.RoundToTickSize(t1);
                pos.Target2Price = Instrument.MasterInstrument.RoundToTickSize(t2);
                pos.Target3Price = Instrument.MasterInstrument.RoundToTickSize(t3);
                pos.Target4Price = Instrument.MasterInstrument.RoundToTickSize(t4);
                pos.Target5Price = Instrument.MasterInstrument.RoundToTickSize(t5);
            }
        }

        private void SymmetryGuardSubmitFollowerBracket(string fleetEntryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;
            Account acct = pos.ExecutingAccount;
            if (acct == null) return;

            OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            double validatedStop = ValidateStopPrice(pos.Direction, pos.CurrentStopPrice);
            // Build 936 [FIX-2]: Use deterministic OcoGroupId from PositionInfo for broker-native OCO bracket protection.
            // Previously "SG_" + ticks was non-deterministic -- changed on every NT8 restart, preventing broker re-linkage.
            // pos.OcoGroupId = "V12_" + fleetEntryName hash, set at position creation in ExecuteSmartDispatchEntry.
            string ocoId = !string.IsNullOrEmpty(pos.OcoGroupId) ? pos.OcoGroupId : ("SG_" + DateTime.UtcNow.Ticks.ToString());

            var ordersToSubmit = new List<Order>();

            string stopSig = SymmetryTrim("Stop_" + fleetEntryName, 40);
            Order stop = acct.CreateOrder(Instrument, exitAction, OrderType.StopMarket,
                TimeInForce.Gtc, Math.Max(1, pos.TotalContracts), 0, validatedStop, ocoId, stopSig, null);

            int nonRunnerLimitQty = 0;
            int runnerQty = 0;

            // Stage orders locally; commit atomically under stateLock.
            var stagedTargets = new List<(int targetNum, Order order)>();

            for (int targetNum = 1; targetNum <= 5; targetNum++)
            {
                int targetQty = GetTargetContracts(pos, targetNum);
                if (targetQty <= 0) continue;

                if (IsRunnerTarget(targetNum))
                {
                    runnerQty += targetQty;
                    continue;
                }

                double targetPrice = GetTargetPrice(pos, targetNum);
                if (targetPrice <= 0)
                {
                    Print($"[SYMMETRY TARGET_SKIP] T{targetNum} for {fleetEntryName} has qty={targetQty} but invalid price={targetPrice:F2}; skipped");
                    continue;
                }

                // [PARITY-01] V12.Phase7.1: Explicit tick rounding on limit price -- defensive guard
                // against broker "Price Rejected" when target arithmetic crosses a tick boundary
                // (e.g., MYM 1.0-tick or cross-instrument parity adjustments).
                double roundedTargetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice);
                string targetSig = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);
                Order target = acct.CreateOrder(
                    Instrument,
                    exitAction,
                    OrderType.Limit,
                    TimeInForce.Gtc,
                    targetQty,
                    roundedTargetPrice,
                    0,
                    ocoId,
                    targetSig,
                    null);

                stagedTargets.Add((targetNum, target));
                ordersToSubmit.Add(target);
                nonRunnerLimitQty += targetQty;
            }

            // Atomic commit before broker submission prevents REAPER race.
            ordersToSubmit.Insert(0, stop);
            lock (stateLock)
            {
                stopOrders[fleetEntryName] = stop;
                foreach (var (targetNum, order) in stagedTargets)
                    GetTargetOrdersDictionary(targetNum)[fleetEntryName] = order;
            }

            acct.Submit(ordersToSubmit.ToArray());
            pos.BracketSubmitted = true;
            Print($"[SYMMETRY STOP_AUDIT] OK {fleetEntryName}: StopQty={pos.TotalContracts} NonRunnerLimits={nonRunnerLimitQty} RunnerQty={runnerQty}");
        }

        private void SymmetryGuardRetargetExistingFollowerBracket(string fleetEntryName, PositionInfo pos)
        {
            UpdateStopOrder(fleetEntryName, pos, pos.CurrentStopPrice, pos.CurrentTrailLevel);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 1, target1Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 2, target2Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 3, target3Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 4, target4Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 5, target5Orders);
        }

        private void SymmetryGuardReplaceExistingFollowerTarget(
            string fleetEntryName,
            PositionInfo pos,
            int targetNumber,
            ConcurrentDictionary<string, Order> dict)
        {
            if (pos.ExecutingAccount == null) return;
            string targetTag = "T" + targetNumber;
            bool isRunner = IsRunnerTarget(targetNumber);
            bool isFilled = IsTargetFilled(pos, targetNumber);
            int qty = GetTargetContracts(pos, targetNumber);

            if (isFilled || isRunner || qty <= 0)
            {
                if (dict.TryGetValue(fleetEntryName, out var staleTarget) && staleTarget != null)
                {
                    if (staleTarget.OrderState == OrderState.Working ||
                        staleTarget.OrderState == OrderState.Accepted ||
                        staleTarget.OrderState == OrderState.Submitted ||
                        staleTarget.OrderState == OrderState.ChangePending)
                    {
                        pos.ExecutingAccount.Cancel(new[] { staleTarget });
                    }
                    dict.TryRemove(fleetEntryName, out _);
                }
                return;
            }

            if (!dict.TryGetValue(fleetEntryName, out var oldTarget) || oldTarget == null)
                return;

            if (oldTarget.OrderState == OrderState.Working ||
                oldTarget.OrderState == OrderState.Accepted ||
                oldTarget.OrderState == OrderState.Submitted ||
                oldTarget.OrderState == OrderState.ChangePending)
            {
                pos.ExecutingAccount.Cancel(new[] { oldTarget });
            }

            double newPrice = GetTargetPrice(pos, targetNumber);
            if (newPrice <= 0) return;

            OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            string signalName = SymmetryTrim(targetTag + "_" + fleetEntryName, 40);

            Order replacement = pos.ExecutingAccount.CreateOrder(
                Instrument,
                exitAction,
                OrderType.Limit,
                TimeInForce.Gtc,
                qty,
                Instrument.MasterInstrument.RoundToTickSize(newPrice),
                0,
                "SGT_" + DateTime.UtcNow.Ticks.ToString(),
                signalName,
                null);

            pos.ExecutingAccount.Submit(new[] { replacement });
            dict[fleetEntryName] = replacement;
        }

        private void SymmetryGuardSkipFollower(
            string fleetEntryName,
            PositionInfo pos,
            double fleetFillPrice,
            double slippageTicks,
            double slippageUsdPerContract,
            string reason)
        {
            Print($"[SYMMETRY_GUARD] SKIP | {fleetEntryName} | {reason} | FleetFill={fleetFillPrice:F2} | Slip={slippageTicks:F1} ticks (${slippageUsdPerContract:F2}/ct)");

            pos.EntryFilled = true;
            lock (stateLock)
            {
                if (pos.RemainingContracts <= 0)
                    pos.RemainingContracts = Math.Max(1, pos.TotalContracts);
            }

            FlattenPositionByName(fleetEntryName);
            CleanupPosition(fleetEntryName);
            SymmetryGuardForgetEntry(fleetEntryName);
        }

        private void SymmetryGuardTryResolveFollowersForDispatch(string dispatchId, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(dispatchId))
                return;

            var followersToResolve = new List<string>();

            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx) && ctx != null)
            {
                lock (ctx.Sync)
                {
                    // V1101E HOT-PATCH: Build follower worklist under ctx.Sync only; never call stateLock paths while holding ctx.Sync.
                    foreach (string fleetEntryName in ctx.FollowerEntries)
                    {
                        if (string.IsNullOrEmpty(fleetEntryName))
                            continue;

                        if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                            continue;
                        if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                            continue;
                        if (!symmetryPendingFollowerFills.ContainsKey(fleetEntryName))
                            continue;

                        followersToResolve.Add(fleetEntryName);
                    }
                }
            }

            // V1101E HOT-PATCH: Preserve legacy dispatch-map scan to catch followers missing from ctx.FollowerEntries.
            foreach (var kvp in symmetryPendingFollowerFills.ToArray())
            {
                string fleetEntryName = kvp.Key;
                if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                    continue;
                if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                    continue;
                if (followersToResolve.Contains(fleetEntryName))
                    continue;

                followersToResolve.Add(fleetEntryName);
            }

            foreach (string fleetEntryName in followersToResolve)
            {
                if (!symmetryPendingFollowerFills.TryGetValue(fleetEntryName, out var pending))
                    continue;

                // V12.Phase8 [F-04]: Guard activePositions read with stateLock to prevent
                // torn observations concurrent with ExecuteSmartDispatchEntry commits/removals.
                PositionInfo pos = null;
                lock (stateLock) { activePositions.TryGetValue(fleetEntryName, out pos); }
                if (pos != null && pos.IsFollower)
                {
                    if (SymmetryGuardTryResolveFollower(fleetEntryName, pos, pending, nowUtc))
                        symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
                }
            }
        }

        /// <summary>
        /// Build 929 Fix3 [P1]: PR #2 Image 3 -- Capture follower list before cleanup.
        /// Cancels all follower entry orders linked to this master BEFORE CleanupPosition
        /// destroys the dispatch map. Without this, followers stay alive as zombie Limit orders.
        /// </summary>
        private void SymmetryGuardCascadeFollowerCleanup(string masterEntryName)
        {
            if (!symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId)) return;
            if (!symmetryDispatchById.TryGetValue(dispatchId, out var ctx)) return;

            string[] followers;
            lock (ctx.Sync) { followers = ctx.FollowerEntries.ToArray(); }

            Print($"[CASCADE] Master {masterEntryName} cancelled -- terminating {followers.Length} linked follower(s).");

            foreach (string followerName in followers)
            {
                if (!activePositions.TryGetValue(followerName, out var pos)) continue;
                if (!entryOrders.TryGetValue(followerName, out var order)) continue;
                if (order == null) continue;

                if (order.OrderState == OrderState.Working  ||
                    order.OrderState == OrderState.Submitted ||
                    order.OrderState == OrderState.Accepted)
                {
                    Print($"[CASCADE] Cancelling follower entry: {followerName} (Acc: {pos.ExecutingAccount?.Name ?? "Master"})");
                    if (pos.ExecutingAccount != null)
                        pos.ExecutingAccount.Cancel(new[] { order });
                    else
                        CancelOrder(order);

                    // Build 930.1 P1: Direction-aware delta rollback.
                    // expectedPositions is signed (Long=+, Short=-). Cancelling a Short must add back.
                    string acctKey = pos.ExecutingAccount != null ? pos.ExecutingAccount.Name : Account.Name;
                    int delta = (pos.Direction == MarketPosition.Long) ? -pos.TotalContracts : pos.TotalContracts;
                    DeltaExpectedPositionLocked(ExpKey(acctKey), delta);
                }
            }
        }

        private void SymmetryGuardForgetEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;

            symmetryPendingFollowerFills.TryRemove(entryName, out _);
            symmetryMasterEntryToDispatch.TryRemove(entryName, out _);

            if (symmetryFleetEntryToDispatch.TryRemove(entryName, out var dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Remove(entryName);
            }
        }

        private void SymmetryGuardPruneDispatches()
        {
            DateTime nowUtc = DateTime.UtcNow;

            foreach (var kvp in symmetryDispatchById.ToArray())
            {
                SymmetryDispatchContext ctx = kvp.Value;
                if (ctx == null) continue;

                bool remove = false;

                if (nowUtc - ctx.CreatedUtc > SymmetryDispatchTtl)
                {
                    remove = true;
                }
                else if (ctx.IsResolved)
                {
                    bool hasActiveFollowers = false;
                    lock (ctx.Sync)
                    {
                        foreach (string follower in ctx.FollowerEntries)
                        {
                            // V12.Phase8 [F-04]: activePositions is a ConcurrentDictionary but
                            // ContainsKey here is used alongside ctx.FollowerEntries iteration under
                            // ctx.Sync -- acquire stateLock for the read to prevent torn observations
                            // when ExecuteSmartDispatchEntry commits or removes entries concurrently.
                            bool exists;
                            lock (stateLock) { exists = activePositions.ContainsKey(follower); }
                            if (exists)
                            {
                                hasActiveFollowers = true;
                                break;
                            }
                        }
                    }
                    if (!hasActiveFollowers) remove = true;
                }

                if (remove)
                    symmetryDispatchById.TryRemove(kvp.Key, out _);
            }
        }

        private string SymmetryInferTradeType(string entryName, PositionInfo pos)
        {
            if (pos != null)
            {
                if (pos.IsTRENDTrade) return "TREND";
                if (pos.IsRetestTrade) return "RETEST";
                if (pos.IsRMATrade) return "RMA";
            }
            return SymmetryNormalizeTradeType(entryName);
        }

        private string SymmetryNormalizeTradeType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "GENERIC";

            string t = raw.ToUpperInvariant();
            if (t.StartsWith("TREND", StringComparison.Ordinal)) return "TREND";
            if (t.StartsWith("RETEST", StringComparison.Ordinal)) return "RETEST";
            if (t.StartsWith("FFMA", StringComparison.Ordinal)) return "FFMA";
            if (t.StartsWith("MOMO", StringComparison.Ordinal)) return "MOMO";
            if (t.StartsWith("RMA", StringComparison.Ordinal)) return "RMA";
            if (t.StartsWith("OR", StringComparison.Ordinal) || t.Contains("ORLONG") || t.Contains("ORSHORT")) return "OR";
            return "GENERIC";
        }

        private static string SymmetryTrim(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen);
        }

        #endregion
    }
}
