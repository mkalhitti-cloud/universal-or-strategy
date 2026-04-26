namespace Morpheus.Api
{
    public interface IRtdSidecar
    {
        IAsyncEnumerable<double> StartStreamingAsync(string symbol, CancellationToken cancellationToken = default);
    }
}
