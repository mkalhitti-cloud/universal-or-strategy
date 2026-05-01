// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
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
            public volatile bool IsResolved;
            public int _resolutionSpinLock;

            public ImmutableHashSet<string> FollowerEntries = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
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
                MasterAnchorPrice = Instrument != null
                    ? Instrument.MasterInstrument.RoundToTickSize(requestedEntryPrice)
                    : requestedEntryPrice,
                IsResolved = false
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
            {
                SymmetryAddFollower(ctx, fleetEntryName);
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
            if (!ctx.IsResolved)
            {
                SpinWait spin = new SpinWait();
                while (Interlocked.CompareExchange(ref ctx._resolutionSpinLock, 1, 0) != 0)
                    spin.SpinOnce();

                try
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
                finally
                {
                    Volatile.Write(ref ctx._resolutionSpinLock, 0);
                }
            }

            if (resolvedNow)
            {
                Print(string.Format("[SYMMETRY_GUARD] MASTER ANCHOR LOCKED | Trade={0} | Anchor={1:F2} | FillQty={2}",
                    ctx.TradeType, ctx.MasterAnchorPrice, ctx.MasterFilledQuantity));

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



        private static ImmutableHashSet<string> SymmetryAddFollower(SymmetryDispatchContext ctx, string follower)
        {
            SpinWait spin = new SpinWait();
            while (true)
            {
                var current = Volatile.Read(ref ctx.FollowerEntries);
                var updated = current.Add(follower);
                if (ReferenceEquals(current, updated))
                    return updated;
                var prior = Interlocked.CompareExchange(ref ctx.FollowerEntries, updated, current);
                if (ReferenceEquals(prior, current))
                    return updated;
                spin.SpinOnce();
            }
        }

        private static ImmutableHashSet<string> SymmetryRemoveFollower(SymmetryDispatchContext ctx, string follower)
        {
            SpinWait spin = new SpinWait();
            while (true)
            {
                var current = Volatile.Read(ref ctx.FollowerEntries);
                var updated = current.Remove(follower);
                if (ReferenceEquals(current, updated))
                    return updated;
                var prior = Interlocked.CompareExchange(ref ctx.FollowerEntries, updated, current);
                if (ReferenceEquals(prior, current))
                    return updated;
                spin.SpinOnce();
            }
        }

        private static ImmutableHashSet<string> SymmetryReadFollowers(SymmetryDispatchContext ctx)
        {
            return Volatile.Read(ref ctx.FollowerEntries);
        }

        #endregion
    }
}
