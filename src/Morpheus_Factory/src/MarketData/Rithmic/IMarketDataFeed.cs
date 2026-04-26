namespace Morpheus.MarketData.Rithmic
{
    public interface IMarketDataFeed
    {
        Task<bool> ConnectAsync();
        void SubscribeTicks(CanonicalSymbol symbol);
    }
}
