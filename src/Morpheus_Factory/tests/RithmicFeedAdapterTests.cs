using Morpheus.MarketData.Rithmic;

namespace Morpheus.Tests
{
    public class RithmicFeedAdapterTests
    {
        [Fact]
        public async Task RithmicFeedAdapterConnectShouldReturnTrue()
        {
            IMarketDataFeed adapter = new RithmicFeedAdapter();
            bool result = await adapter.ConnectAsync();
            Assert.True(result);
        }

        [Fact]
        public void RithmicFeedAdapterSubscribeTicksDoesNotThrow()
        {
            IMarketDataFeed adapter = new RithmicFeedAdapter();
            CanonicalSymbol symbol = CanonicalSymbol.Parse("ESZ4");
            var ex = Record.Exception(() => adapter.SubscribeTicks(symbol));
            Assert.Null(ex);
        }
    }
}
