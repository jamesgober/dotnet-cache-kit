namespace JG.CacheKit.Abstractions;

/// <summary>
/// Represents the result of a cache lookup.
/// </summary>
public readonly struct CacheGetResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheGetResult"/> struct.
    /// </summary>
    /// <param name="state">The cache entry state.</param>
    /// <param name="entry">The cache entry when present.</param>
    public CacheGetResult(CacheEntryState state, CacheEntry? entry)
    {
        State = state;
        Entry = entry;
    }

    /// <summary>
    /// Gets the cache entry state.
    /// </summary>
    public CacheEntryState State { get; }

    /// <summary>
    /// Gets the cache entry when present.
    /// </summary>
    public CacheEntry? Entry { get; }
}
