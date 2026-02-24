namespace JG.CacheKit.Abstractions;

/// <summary>
/// Represents cache metrics for instrumentation.
/// </summary>
public interface ICacheMetrics
{
    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    CacheMetricsSnapshot Snapshot { get; }
}

/// <summary>
/// Represents a point-in-time snapshot of cache metrics.
/// </summary>
public readonly struct CacheMetricsSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheMetricsSnapshot"/> struct.
    /// </summary>
    /// <param name="hits">The number of cache hits.</param>
    /// <param name="misses">The number of cache misses.</param>
    /// <param name="staleHits">The number of stale hits served.</param>
    /// <param name="sets">The number of set operations.</param>
    /// <param name="removals">The number of removal operations.</param>
    /// <param name="evictions">The number of evictions due to expiration.</param>
    /// <param name="size">The current number of tracked entries.</param>
    public CacheMetricsSnapshot(long hits, long misses, long staleHits, long sets, long removals, long evictions, long size)
    {
        Hits = hits;
        Misses = misses;
        StaleHits = staleHits;
        Sets = sets;
        Removals = removals;
        Evictions = evictions;
        Size = size;
    }

    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public long Hits { get; }

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public long Misses { get; }

    /// <summary>
    /// Gets the number of stale hits returned.
    /// </summary>
    public long StaleHits { get; }

    /// <summary>
    /// Gets the number of cache set operations.
    /// </summary>
    public long Sets { get; }

    /// <summary>
    /// Gets the number of cache removal operations.
    /// </summary>
    public long Removals { get; }

    /// <summary>
    /// Gets the number of evictions due to expiration.
    /// </summary>
    public long Evictions { get; }

    /// <summary>
    /// Gets the current cache size tracked by this instance.
    /// </summary>
    public long Size { get; }
}
