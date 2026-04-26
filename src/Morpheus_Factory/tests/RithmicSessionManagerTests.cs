using Morpheus.MarketData.Rithmic;

namespace Morpheus.Tests
{
    public class RithmicSessionManagerTests
    {
        [Fact]
        public void InstanceShouldReturnSameSingleton()
        {
            var instance1 = RithmicSessionManager.Instance;
            var instance2 = RithmicSessionManager.Instance;

            Assert.Same(instance1, instance2);
        }

        [Fact]
        public async Task StartSessionAsyncShouldMarkSessionActive()
        {
            var manager = RithmicSessionManager.Instance;
            await manager.StartSessionAsync();

            Assert.True(manager.IsActive);
        }
    }
}
