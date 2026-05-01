using System;
using System.Collections.Immutable;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public interface ISymmetrySnapshot
    {
        string DispatchId { get; }
        string TradeType { get; }
        MarketPosition Direction { get; }
        int ExpectedQuantity { get; }
        DateTime CreatedUtc { get; }
        double MasterWeightedFill { get; }
        int MasterFilledQuantity { get; }
        double MasterAnchorPrice { get; }
        bool IsResolved { get; }
        ImmutableHashSet<string> FollowerEntries { get; }
    }

    public sealed class FillStateSnapshot : ISymmetrySnapshot
    {
        public string DispatchId { get; }
        public string TradeType { get; }
        public MarketPosition Direction { get; }
        public int ExpectedQuantity { get; }
        public DateTime CreatedUtc { get; }
        public double MasterWeightedFill { get; }
        public int MasterFilledQuantity { get; }
        public double MasterAnchorPrice { get; }
        public bool IsResolved { get; }
        public ImmutableHashSet<string> FollowerEntries { get; }
        public long FillSequence { get; }

        public FillStateSnapshot(
            string dispatchId,
            string tradeType,
            MarketPosition direction,
            int expectedQuantity,
            DateTime createdUtc,
            double masterWeightedFill,
            int masterFilledQuantity,
            double masterAnchorPrice,
            bool isResolved,
            ImmutableHashSet<string> followerEntries,
            long fillSequence)
        {
            DispatchId = dispatchId;
            TradeType = tradeType;
            Direction = direction;
            ExpectedQuantity = expectedQuantity;
            CreatedUtc = createdUtc;
            MasterWeightedFill = masterWeightedFill;
            MasterFilledQuantity = masterFilledQuantity;
            MasterAnchorPrice = masterAnchorPrice;
            IsResolved = isResolved;
            FollowerEntries = followerEntries ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
            FillSequence = fillSequence;
        }
    }
}
