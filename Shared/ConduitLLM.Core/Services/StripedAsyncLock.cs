namespace ConduitLLM.Core.Services;

/// <summary>
/// A bounded set of async locks used to serialize work by key without managing
/// removable per-key semaphore lifetimes.
/// </summary>
internal sealed class StripedAsyncLock
{
    private const int DefaultStripeCount = 256;
    private readonly SemaphoreSlim[] _stripes;

    public StripedAsyncLock(int stripeCount = DefaultStripeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stripeCount);
        _stripes = Enumerable.Range(0, stripeCount)
            .Select(_ => new SemaphoreSlim(1, 1))
            .ToArray();
    }

    public async ValueTask<IDisposable> AcquireAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetStripe(key);
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    public async ValueTask<IDisposable?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetStripe(key);
        return await semaphore.WaitAsync(timeout, cancellationToken)
            ? new Releaser(semaphore)
            : null;
    }

    private SemaphoreSlim GetStripe(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var hash = unchecked((uint)StringComparer.Ordinal.GetHashCode(key));
        return _stripes[hash % (uint)_stripes.Length];
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}
