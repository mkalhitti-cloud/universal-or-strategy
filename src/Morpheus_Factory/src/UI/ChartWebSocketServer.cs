namespace Morpheus.UI
{
    public class ChartWebSocketServer
    {
        private int _isRunning;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public Task StartAsync(int port)
        {
            _ = Interlocked.Exchange(ref _isRunning, 1);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _ = Interlocked.Exchange(ref _isRunning, 0);
            return Task.CompletedTask;
        }
    }
}
