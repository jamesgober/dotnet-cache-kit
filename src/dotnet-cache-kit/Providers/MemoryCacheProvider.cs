using System.Collections.Concurrent;
using JG.CacheKit.Abstractions;

namespace JG.CacheKit.Providers;

/// <summary>
/// Provides an in-memory cache provider.
/// </summary>
/// <remarks>This type is thread-safe and intended for use as a singleton.</remarks>
public sealed class MemoryCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCacheProvider"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <c>null</c>.</exception>
    public MemoryCacheProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public CacheValueMode ValueMode => CacheValueMode.InMemory;

    /// <inheritdoc />
    public ValueTask<CacheGetResult> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Miss, null));
        }

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var metadata = entry.Metadata;

        if (metadata.IsExpired(nowTicks))
        {
            _entries.TryRemove(key, out _);
            return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Expired, null));
        }

        if (metadata.IsStale(nowTicks))
        {
            return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Stale, entry));
        }

        if (metadata.SlidingExpirationTicks != 0)
        {
            var refreshed = metadata.RefreshSliding(nowTicks);
            if (refreshed.AbsoluteExpirationUtcTicks != metadata.AbsoluteExpirationUtcTicks)
            {
                entry = new CacheEntry(entry.Value, refreshed);
                _entries[key] = entry;
            }
        }

        return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Hit, entry));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the entry contains a non-empty payload.</exception>
    public ValueTask SetAsync(string key, CacheEntry entry, CancellationToken cancellationToken = default)
    {
        if (!entry.Payload.IsEmpty)
        {
            throw new ArgumentException("Memory cache provider expects object values.", nameof(entry));
        }

        _entries[key] = entry;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
