using Morpheus.UI;

namespace Morpheus.Tests
{
    public class ChartWebSocketServerTests
    {
        [Fact]
        public async Task ServerStartAsyncShouldCompleteSuccessfully()
        {
            ChartWebSocketServer server = new ChartWebSocketServer();
            // Start the server and immediately stop it for test
            var startTask = server.StartAsync(8080);
            await server.StopAsync();
            await startTask;

            Assert.False(server.IsRunning);
        }
    }
}
