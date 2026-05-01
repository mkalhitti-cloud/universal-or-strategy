using System;
using System.Collections.Immutable;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public interface ISymmetrySnapshot
    {
        string DispatchId { get; init; }
        string TradeType { get; init; }
        MarketPosition Direction { get; init; }
        int ExpectedQuantity { get; init; }
        DateTime CreatedUtc { get; init; }
        double MasterWeightedFill { get; init; }
        int MasterFilledQuantity { get; init; }
        double MasterAnchorPrice { get; init; }
        bool IsResolved { get; init; }
        ImmutableHashSet<string> FollowerEntries { get; init; }
    }
}
