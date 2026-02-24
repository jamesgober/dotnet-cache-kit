using JG.CacheKit.Policies;

namespace JG.CacheKit.Abstractions;

/// <summary>
/// Provides a unified cache API across memory and distributed providers.
/// </summary>
/// <remarks>Implementations are expected to be thread-safe for concurrent use.</remarks>
public interface ICacheKit
{
    /// <summary>
    /// Gets cache metrics for the current instance.
    /// </summary>
    ICacheMetrics Metrics { get; }

    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached value, or <c>default</c> if the key was not found or expired.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a cached value exists for the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the key exists and has not expired; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a cache value by key.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">The cache entry options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when TTL, sliding expiration, or stale TTL is not greater than zero.</exception>
    /// <exception cref="ArgumentException">Thrown when both TTL and sliding expiration are specified.</exception>
    ValueTask SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached value or uses the factory to populate it.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The value factory.</param>
    /// <param name="options">The cache entry options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached or newly computed value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <c>null</c>.</exception>
    ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cache entries associated with a tag.
    /// </summary>
    /// <param name="tag">The tag to invalidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tag"/> is null or whitespace.</exception>
    ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cache entries associated with the provided tags.
    /// </summary>
    /// <param name="tags">The tags to invalidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when any tag is null or whitespace.</exception>
    ValueTask InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
}
