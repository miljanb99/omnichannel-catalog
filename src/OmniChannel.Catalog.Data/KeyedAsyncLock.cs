namespace OmniChannel.Catalog.Data;

public sealed class KeyedAsyncLock
{
    private readonly SemaphoreSlim[] _stripes;

    public KeyedAsyncLock(int stripes = 256)
    {
        _stripes = new SemaphoreSlim[stripes];
        for (var i = 0; i < stripes; i++)
        {
            _stripes[i] = new SemaphoreSlim(1, 1);
        }
    }

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var stripe = _stripes[(uint)key.GetHashCode() % (uint)_stripes.Length];
        await stripe.WaitAsync(cancellationToken);
        return new Releaser(stripe);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}