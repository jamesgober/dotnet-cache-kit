namespace JG.CacheKit.Policies;

/// <summary>
/// Provides cache entry configuration for a specific operation.
/// </summary>
public sealed class CacheEntryOptions
{
    /// <summary>
    /// Gets or sets the absolute time-to-live for the entry.
    /// </summary>
    public TimeSpan? Ttl { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration duration for the entry.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the stale window duration for stale-while-revalidate.
    /// </summary>
    public TimeSpan? StaleTtl { get; set; }

    /// <summary>
    /// Gets or sets the tags associated with the entry.
    /// </summary>
    public IReadOnlyCollection<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the category for resolving per-category default options.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when TTL, sliding expiration, or stale TTL is not greater than zero.</exception>
    /// <exception cref="ArgumentException">Thrown when both TTL and sliding expiration are specified, or when a tag is null or whitespace.</exception>
    public void Validate()
    {
        if (Ttl is { } ttl && ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Ttl), "TTL must be greater than zero.");
        }

        if (SlidingExpiration is { } sliding && sliding <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(SlidingExpiration), "Sliding expiration must be greater than zero.");
        }

        if (StaleTtl is { } stale && stale <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(StaleTtl), "Stale TTL must be greater than zero.");
        }

        if (Ttl is not null && SlidingExpiration is not null)
        {
            throw new ArgumentException("Configure either absolute TTL or sliding expiration, not both.");
        }

        if (Tags is { Count: > 0 })
        {
            foreach (var tag in Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    throw new ArgumentException("Tags cannot be null or whitespace.");
                }
            }
        }
    }
}
