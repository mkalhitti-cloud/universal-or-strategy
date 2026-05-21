// <copyright file="V12_002.Symmetry.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12.50 Symmetry Guard

        // ADR-019: Atomic-publish snapshot of master-fill anchor state.
        // Immutable; mutated only via Interlocked.CompareExchange on the parent context.
        private sealed class AnchorSnapshot
        {
            public static readonly AnchorSnapshot Pending = new AnchorSnapshot(false, 0d, 0d, 0);

            public readonly bool   IsResolved;
            public readonly double MasterAnchorPrice;
            public readonly double MasterWeightedFill;
            public readonly int    MasterFilledQuantity;

            public AnchorSnapshot(bool isResolved, double anchorPrice, double weightedFill, int filledQty)
            {
                IsResolved           = isResolved;
                MasterAnchorPrice    = anchorPrice;
                MasterWeightedFill   = weightedFill;
                MasterFilledQuantity = filledQty;
            }
        }

        private sealed class SymmetryDispatchContext
        {
            public string DispatchId;
            public string TradeType;
            public MarketPosition Direction;
            public int ExpectedQuantity;
            public DateTime CreatedUtc;

            // Initial requested anchor seeded by SymmetryGuardBeginDispatch; immutable thereafter.
            public double RequestedAnchorPrice;

            // ADR-019: anchor state replaces { IsResolved, MasterAnchorPrice, MasterWeightedFill, MasterFilledQuantity }
            // and the prior monitor-backed mutation path. Single reference field, swapped via Interlocked.CompareExchange.
            private AnchorSnapshot _anchor = AnchorSnapshot.Pending;
            public AnchorSnapshot Anchor { get { return Volatile.Read(ref _anchor); } }
            public bool TryPublishAnchor(AnchorSnapshot expected, AnchorSnapshot updated)
            {
                return Interlocked.CompareExchange(ref _anchor, updated, expected) == expected;
            }

            // ADR-019: follower membership held as an immutable string[] snapshot.
            // Hot-path readers do a single Volatile.Read; iteration is index-based and zero-alloc.
            // Mutators allocate one fresh array per change (cold path: register/forget per dispatch).
            private string[] _followers = Array.Empty<string>();
            public string[] Followers { get { return Volatile.Read(ref _followers); } }

            public void AddFollower(string name)
            {
                if (string.IsNullOrEmpty(name)) return;
                while (true)
                {
                    string[] cur = Volatile.Read(ref _followers);
                    if (Array.IndexOf(cur, name) >= 0) return;
                    string[] next = new string[cur.Length + 1];
                    if (cur.Length > 0) Array.Copy(cur, 0, next, 0, cur.Length);
                    next[cur.Length] = name;
                    if (Interlocked.CompareExchange(ref _followers, next, cur) == cur) return;
                }
            }

            public void RemoveFollower(string name)
            {
                if (string.IsNullOrEmpty(name)) return;
                while (true)
                {
                    string[] cur = Volatile.Read(ref _followers);
                    int idx = Array.IndexOf(cur, name);
                    if (idx < 0) return;
                    string[] next = new string[cur.Length - 1];
                    if (idx > 0) Array.Copy(cur, 0, next, 0, idx);
                    if (idx < cur.Length - 1) Array.Copy(cur, idx + 1, next, idx, cur.Length - idx - 1);
                    if (Interlocked.CompareExchange(ref _followers, next, cur) == cur) return;
                }
            }
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

            // ADR-019: Duplicate dispatch guard is lock-free. Iteration over symmetryDispatchById
            // (ConcurrentDictionary) is snapshot-safe. The CAS-loop publisher in PublishFollowers/
            // PublishAnchor ensures atomic visibility without stateLock.
            DateTime now = DateTime.UtcNow;

            // V12.Phase7 [H-11]: Prevent duplicate dispatches for the same signal+direction.
            // If an active (non-expired, unresolved) dispatch already exists for this trade type and direction,
            // return the existing ID instead of creating a second one that would double fleet entries.
            foreach (var kvp in symmetryDispatchById)
            {
                var existing = kvp.Value;
                if (existing.TradeType == normalizedType &&
                    existing.Direction == direction &&
                    !existing.Anchor.IsResolved &&
                    (now - existing.CreatedUtc) < SymmetryDispatchTtl)
                {
                    Print(string.Format("[SYMMETRY] Duplicate dispatch suppressed: {0} {1} -- reusing {2}", normalizedType, direction, existing.DispatchId));
                    return existing.DispatchId;
                }
            }

            string dispatchId = string.Format("SG_{0}_{1}_{2}",
                now.Ticks,
                normalizedType,
                (int)action);

            var ctx = new SymmetryDispatchContext
            {
                DispatchId = dispatchId,
                TradeType = normalizedType,
                Direction = direction,
                ExpectedQuantity = Math.Max(1, quantity),
                CreatedUtc = now,
                RequestedAnchorPrice = Instrument != null
                    ? Instrument.MasterInstrument.RoundToTickSize(requestedEntryPrice)
                    : requestedEntryPrice
            };

            symmetryDispatchById[dispatchId] = ctx;
            return dispatchId;
        }

        private void SymmetryGuardRegisterFollower(string dispatchId, string fleetEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(fleetEntryName))
                return;

            symmetryFleetEntryToDispatch[fleetEntryName] = dispatchId;

            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
                ctx.AddFollower(fleetEntryName);
        }

        private void SymmetryGuardRegisterMasterEntry(string dispatchId, string masterEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(masterEntryName))
                return;
            symmetryMasterEntryToDispatch[masterEntryName] = dispatchId;
        }

        /// <summary>
        /// Rolls back a symmetry dispatch registration when order submission fails.
        /// Removes the dispatch context and all associated mappings to prevent orphaned state.
        /// </summary>
        private void SymmetryGuardRollbackDispatch(string dispatchId)
        {
            if (string.IsNullOrEmpty(dispatchId))
                return;

            // Remove the dispatch context
            if (symmetryDispatchById.TryRemove(dispatchId, out var ctx))
            {
                // Clean up any registered followers
                string[] followers = ctx.Followers;
                for (int i = 0; i < followers.Length; i++)
                {
                    symmetryFleetEntryToDispatch.TryRemove(followers[i], out _);
                }

                // Clean up master entry mapping if it exists
                var masterToRemove = symmetryMasterEntryToDispatch
                    .Where(kvp => kvp.Value == dispatchId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var masterKey in masterToRemove)
                {
                    symmetryMasterEntryToDispatch.TryRemove(masterKey, out _);
                }

                Print(string.Format("[SYMMETRY_GUARD] Dispatch {0} rolled back due to order submission failure", dispatchId));
            }
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

            // ADR-019: CAS loop over AnchorSnapshot. First writer to publish IsResolved=true wins.
            // Losing CAS retries; on retry the IsResolved guard short-circuits (idempotent).
            AnchorSnapshot resolvedSnap = null;
            while (true)
            {
                AnchorSnapshot cur = ctx.Anchor;
                if (cur.IsResolved)
                    break;

                double weighted = cur.MasterWeightedFill + averageFillPrice * fillQty;
                int qty = cur.MasterFilledQuantity + fillQty;
                double avg = weighted / Math.Max(1, qty);
                double anchor = Instrument.MasterInstrument.RoundToTickSize(avg);

                AnchorSnapshot next = new AnchorSnapshot(true, anchor, weighted, qty);
                if (ctx.TryPublishAnchor(cur, next))
                {
                    resolvedSnap = next;
                    break;
                }
            }

            if (resolvedSnap != null)
            {
                Print(string.Format("[SYMMETRY_GUARD] MASTER ANCHOR LOCKED | Trade={0} | Anchor={1:F2} | FillQty={2}",
                    ctx.TradeType, resolvedSnap.MasterAnchorPrice, resolvedSnap.MasterFilledQuantity));

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
                if (ctx == null || ctx.Anchor.IsResolved)
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

        #endregion
    }
}
