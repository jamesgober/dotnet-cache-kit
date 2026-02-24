using JG.CacheKit.Abstractions;
using JG.CacheKit.Policies;

namespace JG.CacheKit.Internal;

internal static class CacheEntryFactory
{
    public static CacheEntry CreateObjectEntry<T>(T value, CacheKitOptions defaults, CacheEntryOptions? options, TimeProvider timeProvider)
    {
        var metadata = CreateMetadata(defaults, options, timeProvider);
        return new CacheEntry(value, metadata);
    }

    public static CacheEntry CreateBinaryEntry(ReadOnlyMemory<byte> payload, CacheKitOptions defaults, CacheEntryOptions? options, TimeProvider timeProvider)
    {
        var metadata = CreateMetadata(defaults, options, timeProvider);
        return new CacheEntry(payload, metadata);
    }

    public static CacheEntryMetadata CreateMetadata(CacheKitOptions defaults, CacheEntryOptions? options, TimeProvider timeProvider)
    {
        options?.Validate();

        var categoryDefaults = ResolveCategoryDefaults(defaults, options?.Category);
        var now = timeProvider.GetUtcNow().UtcTicks;

        TimeSpan? ttl;
        TimeSpan? sliding;

        if (options?.Ttl is not null || options?.SlidingExpiration is not null)
        {
            ttl = options.Ttl;
            sliding = options.SlidingExpiration;
        }
        else if (categoryDefaults?.Ttl is not null || categoryDefaults?.SlidingExpiration is not null)
        {
            ttl = categoryDefaults.Ttl;
            sliding = categoryDefaults.SlidingExpiration;
        }
        else
        {
            ttl = null;
            sliding = defaults.DefaultSlidingExpiration;
        }

        if (ttl is null && sliding is null)
        {
            ttl = defaults.DefaultTtl;
        }

        long absoluteTicks = 0;
        long slidingTicks = 0;
        if (sliding is not null)
        {
            slidingTicks = sliding.Value.Ticks;
            absoluteTicks = checked(now + slidingTicks);
        }
        else if (ttl is not null)
        {
            absoluteTicks = checked(now + ttl.Value.Ticks);
        }

        var staleTtl = options?.StaleTtl ?? categoryDefaults?.StaleTtl ?? defaults.DefaultStaleTtl;
        var staleTicks = staleTtl?.Ticks ?? 0;

        return new CacheEntryMetadata(now, absoluteTicks, slidingTicks, staleTicks);
    }

    private static CacheEntryOptions? ResolveCategoryDefaults(CacheKitOptions defaults, string? category)
    {
        if (category is null || defaults.CategoryDefaults is null)
        {
            return null;
        }

        defaults.CategoryDefaults.TryGetValue(category, out var categoryOptions);
        return categoryOptions;
    }
}
