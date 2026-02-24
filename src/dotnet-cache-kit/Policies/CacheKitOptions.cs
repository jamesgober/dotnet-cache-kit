using JG.CacheKit.Abstractions;

namespace JG.CacheKit.Policies;

/// <summary>
/// Configures cache kit defaults and provider selection.
/// </summary>
public sealed class CacheKitOptions
{
    internal CacheProviderKind ProviderKind { get; private set; } = CacheProviderKind.Memory;
    internal Func<IServiceProvider, ICacheProvider>? ProviderFactory { get; private set; }
    internal Func<IServiceProvider, ICacheSerializer>? SerializerFactory { get; private set; }

    private Dictionary<string, CacheEntryOptions>? _categoryDefaults;
    internal IReadOnlyDictionary<string, CacheEntryOptions>? CategoryDefaults => _categoryDefaults;

    /// <summary>
    /// Gets or sets the default absolute TTL for cache entries.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default sliding expiration duration.
    /// </summary>
    public TimeSpan? DefaultSlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the default stale window duration.
    /// </summary>
    public TimeSpan? DefaultStaleTtl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stale-while-revalidate is enabled.
    /// </summary>
    public bool EnableStaleWhileRevalidate { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stampede protection is enabled.
    /// </summary>
    public bool EnableStampedeProtection { get; set; } = true;

    /// <summary>
    /// Gets or sets the time provider used for expiration checks.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Configures the cache to use the in-memory provider.
    /// </summary>
    public void UseMemory()
    {
        ProviderKind = CacheProviderKind.Memory;
        ProviderFactory = null;
    }

    /// <summary>
    /// Configures the cache to use a distributed provider via <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
    /// </summary>
    public void UseDistributed()
    {
        ProviderKind = CacheProviderKind.Distributed;
        ProviderFactory = null;
    }

    /// <summary>
    /// Registers default cache entry options for a category. When <see cref="CacheEntryOptions.Category"/>
    /// is set on an operation, the matching defaults are used for any unset properties.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <param name="defaults">The default options for the category.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="category"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="defaults"/> is <c>null</c>.</exception>
    public void AddCategoryDefaults(string category, CacheEntryOptions defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(defaults);
        defaults.Validate();

        _categoryDefaults ??= new Dictionary<string, CacheEntryOptions>(StringComparer.Ordinal);
        _categoryDefaults[category] = defaults;
    }

    /// <summary>
    /// Configures the cache to use a custom provider.
    /// </summary>
    /// <param name="providerFactory">The provider factory.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="providerFactory"/> is <c>null</c>.</exception>
    public void UseProvider(Func<IServiceProvider, ICacheProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        ProviderFactory = providerFactory;
        ProviderKind = CacheProviderKind.Custom;
    }

    /// <summary>
    /// Configures a custom serializer.
    /// </summary>
    /// <param name="serializer">The serializer instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serializer"/> is <c>null</c>.</exception>
    public void UseSerializer(ICacheSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        SerializerFactory = _ => serializer;
    }

    /// <summary>
    /// Configures a custom serializer factory.
    /// </summary>
    /// <param name="serializerFactory">The serializer factory.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serializerFactory"/> is <c>null</c>.</exception>
    public void UseSerializer(Func<IServiceProvider, ICacheSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(serializerFactory);
        SerializerFactory = serializerFactory;
    }

    internal void Validate()
    {
        if (DefaultTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultTtl), "Default TTL must be greater than zero.");
        }

        if (DefaultSlidingExpiration is { } sliding && sliding <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultSlidingExpiration), "Default sliding expiration must be greater than zero.");
        }

        if (DefaultStaleTtl is { } stale && stale <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultStaleTtl), "Default stale TTL must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(TimeProvider);
    }
}

internal enum CacheProviderKind
{
    Memory = 0,
    Distributed = 1,
    Custom = 2
}
