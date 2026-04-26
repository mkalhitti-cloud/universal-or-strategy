using Morpheus.Api;

namespace Morpheus.Tests
{
    public class RtdSidecarTests
    {
        [Fact]
        public async Task StartStreamingAsyncShouldProduceQuotes()
        {
            DummyRtdSidecar sidecar = new DummyRtdSidecar();
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            int quoteCount = 0;

            try
            {
                await foreach (var quote in sidecar.StartStreamingAsync("NQZ4", cts.Token))
                {
                    quoteCount++;
                    if (quoteCount >= 2)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected if we didn't get enough quotes before timeout
            }

            Assert.True(quoteCount >= 1);
        }

        private sealed class DummyRtdSidecar : IRtdSidecar
        {
            public async IAsyncEnumerable<double> StartStreamingAsync(string symbol, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield return 15000.50;
                await Task.Delay(10, cancellationToken);
                yield return 15000.75;
            }
        }
    }
}
