using Morpheus.Execution;

namespace Morpheus.Tests
{
    public class DirectExecutionRouterTests
    {
        [Fact]
        public async Task RouteOrderAsyncShouldFanOutToAllAccounts()
        {
            DirectExecutionRouter router = new DirectExecutionRouter();
            List<string> accounts = new List<string> { "ACC1", "ACC2", "ACC3" };

            var results = await router.RouteOrderAsync("NQZ4", 1, true, accounts);

            Assert.Equal(3, results.Count);
            Assert.Contains(results, r => r.Account == "ACC1" && r.Success);
        }

        [Fact]
        public async Task RouteOrderAsyncFaultToleranceShouldNotFailAllIfOneFails()
        {
            DirectExecutionRouter router = new DirectExecutionRouter(failAccount: "ACC2");
            List<string> accounts = new List<string> { "ACC1", "ACC2", "ACC3" };

            var results = await router.RouteOrderAsync("NQZ4", 1, true, accounts);

            Assert.Equal(3, results.Count);
            Assert.Contains(results, r => r.Account == "ACC1" && r.Success);
            Assert.Contains(results, r => r.Account == "ACC2" && !r.Success);
            Assert.Contains(results, r => r.Account == "ACC3" && r.Success);
        }
    }
}
