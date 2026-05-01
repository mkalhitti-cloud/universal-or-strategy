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
}
