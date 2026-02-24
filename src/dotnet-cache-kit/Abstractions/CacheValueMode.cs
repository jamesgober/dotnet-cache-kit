namespace JG.CacheKit.Abstractions;

/// <summary>
/// Describes how cached values are represented by a cache provider.
/// </summary>
public enum CacheValueMode
{
    /// <summary>
    /// Values are stored as in-memory objects.
    /// </summary>
    InMemory = 0,

    /// <summary>
    /// Values are stored as serialized binary payloads.
    /// </summary>
    Binary = 1
}
