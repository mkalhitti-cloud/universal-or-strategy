using Morpheus.MarketData.Rithmic;

namespace Morpheus.Tests
{
    public class MarketDataFeedTests
    {
        [Fact]
        public async Task ConnectShouldEstablishConnection()
        {
            IMarketDataFeed feed = new DummyFeed();
            bool result = await feed.ConnectAsync();
            Assert.True(result);
        }

        [Fact]
        public void SubscribeTicksShouldRegisterSubscription()
        {
            IMarketDataFeed feed = new DummyFeed();
            CanonicalSymbol symbol = CanonicalSymbol.Parse("NQZ4");
            feed.SubscribeTicks(symbol);
            Assert.True(((DummyFeed)feed).IsSubscribed);
        }

        private sealed class DummyFeed : IMarketDataFeed
        {
            public bool IsSubscribed { get; private set; }

            public Task<bool> ConnectAsync()
            {
                return Task.FromResult(true);
            }

            public void SubscribeTicks(CanonicalSymbol symbol)
            {
                IsSubscribed = true;
            }
        }
    }
}
