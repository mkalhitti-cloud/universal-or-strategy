namespace Morpheus.MarketData.Rithmic
{
    public sealed class RithmicSessionManager
    {
        private static readonly Lazy<RithmicSessionManager> _instance =
            new(() => new RithmicSessionManager(), LazyThreadSafetyMode.PublicationOnly);

        public static RithmicSessionManager Instance => _instance.Value;

        private int _isActive;

        public bool IsActive => Interlocked.CompareExchange(ref _isActive, 0, 0) == 1;

        private RithmicSessionManager() { }

        public Task StartSessionAsync()
        {
            _ = Interlocked.Exchange(ref _isActive, 1);
            return Task.CompletedTask;
        }
    }
}
