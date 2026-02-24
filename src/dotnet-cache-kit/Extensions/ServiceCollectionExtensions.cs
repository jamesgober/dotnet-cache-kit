using JG.CacheKit.Abstractions;
using JG.CacheKit.Internal;
using JG.CacheKit.Policies;
using JG.CacheKit.Providers;
using JG.CacheKit.Serialization;
using JG.CacheKit.Tagging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JG.CacheKit.Extensions;

/// <summary>
/// Provides dependency injection registration for CacheKit.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CacheKit to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCacheKit(this IServiceCollection services, Action<CacheKitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<CacheKitOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.PostConfigure(options => options.Validate());

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CacheKitOptions>>().Value.TimeProvider);

        services.AddSingleton<ICacheSerializer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CacheKitOptions>>().Value;
            if (options.SerializerFactory is not null)
            {
                return options.SerializerFactory(sp);
            }

            return new JsonCacheSerializer();
        });

        services.AddSingleton<ICacheProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CacheKitOptions>>().Value;
            var timeProvider = sp.GetRequiredService<TimeProvider>();

            return options.ProviderKind switch
            {
                CacheProviderKind.Memory => new MemoryCacheProvider(timeProvider),
                CacheProviderKind.Distributed => new DistributedCacheProvider(
                    sp.GetRequiredService<IDistributedCache>(),
                    timeProvider),
                CacheProviderKind.Custom => options.ProviderFactory?.Invoke(sp)
                    ?? throw new InvalidOperationException("Custom cache provider factory was not configured."),
                _ => throw new InvalidOperationException("Unsupported cache provider configuration.")
            };
        });

        services.AddSingleton<ICacheTagIndex, InMemoryTagIndex>();
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<ICacheMetrics>(sp => sp.GetRequiredService<CacheMetrics>());
        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<ICacheProvider>();
            var serializer = sp.GetRequiredService<ICacheSerializer>();
            var opts = sp.GetRequiredService<IOptions<CacheKitOptions>>();
            var metrics = sp.GetRequiredService<CacheMetrics>();
            var tagIndex = sp.GetRequiredService<ICacheTagIndex>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var logger = sp.GetService<ILogger<CacheKit>>();
            return new CacheKit(provider, serializer, opts, metrics, tagIndex, timeProvider, logger);
        });
        services.AddSingleton<ICacheKit>(sp => sp.GetRequiredService<CacheKit>());

        return services;
    }
}
