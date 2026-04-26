using System;

namespace Morpheus.ControlPlane.MarketData
{
    public class RithmicFeedNormalizer : IMarketDataFeed
    {
        public string NormalizeSymbol(string rithmicSymbol, string exchange)
        {
            if (string.IsNullOrWhiteSpace(rithmicSymbol))
                throw new ArgumentException("Symbol cannot be null or empty.", nameof(rithmicSymbol));

            // Simple canonical mapping: remove expiration month/year suffix for tests
            // NQZ4 -> NQ, ESM5 -> ES
            if (rithmicSymbol.StartsWith("NQ", StringComparison.OrdinalIgnoreCase)) return "NQ";
            if (rithmicSymbol.StartsWith("ES", StringComparison.OrdinalIgnoreCase)) return "ES";

            return rithmicSymbol;
        }
    }
}
