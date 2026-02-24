using System.Collections.Concurrent;

namespace JG.CacheKit.Internal;

internal sealed class KeyedSemaphore
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

    public async ValueTask<SemaphoreLease> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new SemaphoreLease(this, key, semaphore);
    }

    public bool TryAcquire(string key, out SemaphoreLease lease)
    {
        var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!semaphore.Wait(0))
        {
            lease = default;
            return false;
        }

        lease = new SemaphoreLease(this, key, semaphore);
        return true;
    }

    private void Release(string key, SemaphoreSlim semaphore)
    {
        semaphore.Release();

        if (semaphore.CurrentCount == 1 && _semaphores.TryRemove(key, out var removed) && ReferenceEquals(removed, semaphore))
        {
            removed.Dispose();
        }
    }

    internal readonly struct SemaphoreLease : IDisposable
    {
        private readonly KeyedSemaphore? _owner;
        private readonly string? _key;
        private readonly SemaphoreSlim? _semaphore;

        public SemaphoreLease(KeyedSemaphore owner, string key, SemaphoreSlim semaphore)
        {
            _owner = owner;
            _key = key;
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_owner is null || _key is null || _semaphore is null)
            {
                return;
            }

            _owner.Release(_key, _semaphore);
        }
    }
}
