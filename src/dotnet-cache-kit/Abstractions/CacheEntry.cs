namespace JG.CacheKit.Abstractions;

/// <summary>
/// Metadata describing cache entry lifetime and expiration behavior.
/// </summary>
public readonly struct CacheEntryMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheEntryMetadata"/> struct.
    /// </summary>
    /// <param name="createdUtcTicks">The UTC creation time in ticks.</param>
    /// <param name="absoluteExpirationUtcTicks">The absolute expiration UTC time in ticks, or 0 when not set.</param>
    /// <param name="slidingExpirationTicks">The sliding expiration duration in ticks, or 0 when not set.</param>
    /// <param name="staleTtlTicks">The stale window duration in ticks, or 0 when not set.</param>
    public CacheEntryMetadata(long createdUtcTicks, long absoluteExpirationUtcTicks, long slidingExpirationTicks, long staleTtlTicks)
    {
        CreatedUtcTicks = createdUtcTicks;
        AbsoluteExpirationUtcTicks = absoluteExpirationUtcTicks;
        SlidingExpirationTicks = slidingExpirationTicks;
        StaleTtlTicks = staleTtlTicks;
    }

    /// <summary>
    /// Gets the UTC creation time in ticks.
    /// </summary>
    public long CreatedUtcTicks { get; }

    /// <summary>
    /// Gets the absolute expiration UTC time in ticks. Returns 0 when not set.
    /// </summary>
    public long AbsoluteExpirationUtcTicks { get; }

    /// <summary>
    /// Gets the sliding expiration duration in ticks. Returns 0 when not set.
    /// </summary>
    public long SlidingExpirationTicks { get; }

    /// <summary>
    /// Gets the stale window duration in ticks. Returns 0 when not set.
    /// </summary>
    public long StaleTtlTicks { get; }

    /// <summary>
    /// Gets the UTC stale expiration time in ticks. Returns 0 when not set.
    /// </summary>
    public long StaleExpirationUtcTicks => StaleTtlTicks == 0 || AbsoluteExpirationUtcTicks == 0
        ? 0
        : unchecked(AbsoluteExpirationUtcTicks + StaleTtlTicks);

    /// <summary>
    /// Returns <c>true</c> if the entry is expired and outside its stale window.
    /// </summary>
    /// <param name="nowUtcTicks">The current UTC time in ticks.</param>
    public bool IsExpired(long nowUtcTicks)
    {
        if (AbsoluteExpirationUtcTicks == 0)
        {
            return false;
        }

        if (nowUtcTicks <= AbsoluteExpirationUtcTicks)
        {
            return false;
        }

        var staleExpiration = StaleExpirationUtcTicks;
        if (staleExpiration == 0)
        {
            return true;
        }

        return nowUtcTicks > staleExpiration;
    }

    /// <summary>
    /// Returns <c>true</c> if the entry is stale but still within its stale window.
    /// </summary>
    /// <param name="nowUtcTicks">The current UTC time in ticks.</param>
    public bool IsStale(long nowUtcTicks)
    {
        if (AbsoluteExpirationUtcTicks == 0 || StaleTtlTicks == 0)
        {
            return false;
        }

        if (nowUtcTicks <= AbsoluteExpirationUtcTicks)
        {
            return false;
        }

        return nowUtcTicks <= StaleExpirationUtcTicks;
    }

    /// <summary>
    /// Returns a copy of the metadata with refreshed sliding expiration.
    /// </summary>
    /// <param name="nowUtcTicks">The current UTC time in ticks.</param>
    public CacheEntryMetadata RefreshSliding(long nowUtcTicks)
    {
        if (SlidingExpirationTicks == 0)
        {
            return this;
        }

        var refreshedAbsolute = unchecked(nowUtcTicks + SlidingExpirationTicks);
        return new CacheEntryMetadata(CreatedUtcTicks, refreshedAbsolute, SlidingExpirationTicks, StaleTtlTicks);
    }
}

/// <summary>
/// Represents a cached value and its lifetime metadata.
/// </summary>
public sealed class CacheEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheEntry"/> class with an in-memory value.
    /// </summary>
    /// <param name="value">The cached value.</param>
    /// <param name="metadata">The metadata describing expiration behavior.</param>
    public CacheEntry(object? value, CacheEntryMetadata metadata)
    {
        Value = value;
        Metadata = metadata;
        Payload = ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheEntry"/> class with a serialized payload.
    /// </summary>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="metadata">The metadata describing expiration behavior.</param>
    public CacheEntry(ReadOnlyMemory<byte> payload, CacheEntryMetadata metadata)
    {
        Payload = payload;
        Metadata = metadata;
        Value = null;
    }

    /// <summary>
    /// Gets the cached value when using object storage.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the serialized payload when using binary storage.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Gets the metadata describing expiration behavior.
    /// </summary>
    public CacheEntryMetadata Metadata { get; }
}
