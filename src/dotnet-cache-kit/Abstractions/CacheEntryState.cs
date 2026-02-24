namespace JG.CacheKit.Abstractions;

/// <summary>
/// Represents the state of a cache lookup.
/// </summary>
public enum CacheEntryState
{
    /// <summary>
    /// The entry was not found.
    /// </summary>
    Miss = 0,

    /// <summary>
    /// The entry was found and is fresh.
    /// </summary>
    Hit = 1,

    /// <summary>
    /// The entry was found but is stale and still within its stale window.
    /// </summary>
    Stale = 2,

    /// <summary>
    /// The entry was found but expired and removed.
    /// </summary>
    Expired = 3
}
