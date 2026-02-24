using JG.CacheKit.Abstractions;
using JG.CacheKit.Internal;
using Microsoft.Extensions.Caching.Distributed;

namespace JG.CacheKit.Providers;

/// <summary>
/// Provides a distributed cache provider backed by <see cref="IDistributedCache"/>.
/// </summary>
/// <remarks>This type is thread-safe. Thread safety of individual operations depends on the underlying <see cref="IDistributedCache"/>.</remarks>
public sealed class DistributedCacheProvider : ICacheProvider
{
    private readonly IDistributedCache _distributedCache;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheProvider"/> class.
    /// </summary>
    /// <param name="distributedCache">The distributed cache.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distributedCache"/> or <paramref name="timeProvider"/> is <c>null</c>.</exception>
    public DistributedCacheProvider(IDistributedCache distributedCache, TimeProvider timeProvider)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public CacheValueMode ValueMode => CacheValueMode.Binary;

    /// <inheritdoc />
    public async ValueTask<CacheGetResult> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var payload = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return new CacheGetResult(CacheEntryState.Miss, null);
        }

        if (!CacheEnvelopeSerializer.TryDeserialize(payload, out var entry))
        {
            await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return new CacheGetResult(CacheEntryState.Expired, null);
        }

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var metadata = entry.Metadata;

        if (metadata.IsExpired(nowTicks))
        {
            await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return new CacheGetResult(CacheEntryState.Expired, null);
        }

        if (metadata.IsStale(nowTicks))
        {
            return new CacheGetResult(CacheEntryState.Stale, entry);
        }

        if (metadata.SlidingExpirationTicks != 0)
        {
            var refreshed = metadata.RefreshSliding(nowTicks);
            if (refreshed.AbsoluteExpirationUtcTicks != metadata.AbsoluteExpirationUtcTicks)
            {
                entry = new CacheEntry(entry.Payload, refreshed);
                await SetEnvelopeAsync(key, entry, nowTicks, cancellationToken).ConfigureAwait(false);
            }
        }

        return new CacheGetResult(CacheEntryState.Hit, entry);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the entry contains an empty payload.</exception>
    public async ValueTask SetAsync(string key, CacheEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Payload.IsEmpty)
        {
            throw new ArgumentException("Distributed cache provider expects binary payloads.", nameof(entry));
        }

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        await SetEnvelopeAsync(key, entry, nowTicks, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask(_distributedCache.RemoveAsync(key, cancellationToken));
    }

    private async ValueTask SetEnvelopeAsync(string key, CacheEntry entry, long nowTicks, CancellationToken cancellationToken)
    {
        var envelope = CacheEnvelopeSerializer.Serialize(entry.Metadata, entry.Payload);
        var options = CreateOptions(entry.Metadata, nowTicks);
        await _distributedCache.SetAsync(key, envelope, options, cancellationToken).ConfigureAwait(false);
    }

    private static DistributedCacheEntryOptions CreateOptions(CacheEntryMetadata metadata, long nowTicks)
    {
        var options = new DistributedCacheEntryOptions();
        if (metadata.AbsoluteExpirationUtcTicks == 0)
        {
            return options;
        }

        var staleExpiration = metadata.StaleExpirationUtcTicks;
        var expirationTicks = staleExpiration == 0 ? metadata.AbsoluteExpirationUtcTicks : staleExpiration;
        var relativeTicks = expirationTicks - nowTicks;
        if (relativeTicks <= 0)
        {
            relativeTicks = TimeSpan.FromSeconds(1).Ticks;
        }

        options.AbsoluteExpirationRelativeToNow = TimeSpan.FromTicks(relativeTicks);
        return options;
    }
}
