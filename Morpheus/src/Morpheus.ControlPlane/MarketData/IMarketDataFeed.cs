namespace Morpheus.ControlPlane.MarketData
{
    public interface IMarketDataFeed
    {
        string NormalizeSymbol(string rithmicSymbol, string exchange);
    }
}
