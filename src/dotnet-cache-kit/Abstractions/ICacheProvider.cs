namespace JG.CacheKit.Abstractions;

/// <summary>
/// Represents a cache provider implementation.
/// </summary>
/// <remarks>Implementations should be thread-safe for use as a singleton.</remarks>
public interface ICacheProvider
{
    /// <summary>
    /// Gets the value representation used by this provider.
    /// </summary>
    CacheValueMode ValueMode { get; }

    /// <summary>
    /// Gets a cache entry by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lookup result containing the entry state and value if found.</returns>
    ValueTask<CacheGetResult> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a cache entry by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="entry">The cache entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SetAsync(string key, CacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cache entry by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
