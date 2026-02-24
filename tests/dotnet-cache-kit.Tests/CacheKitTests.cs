namespace JG.CacheKit.Tests;

using System.Collections.Concurrent;
using FluentAssertions;
using JG.CacheKit.Abstractions;
using JG.CacheKit.Extensions;
using JG.CacheKit.Policies;
using Microsoft.Extensions.DependencyInjection;

public class CacheKitTests
{
    private static readonly string[] ProductTags = ["products"];
    private static readonly string[] ProductFeatureTags = ["products", "featured"];
    private static readonly string[] FeaturedTags = ["featured"];
    private static readonly string[] WhitespaceTags = [" "];
    private static readonly string[] EmptyStringTags = [""];

    [Fact]
    public async Task SetAsync_ValueStored_ReturnsValueAndRecordsMetrics()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("item", "value");
        var value = await cache.GetAsync<string>("item");

        value.Should().Be("value");
        var snapshot = cache.Metrics.Snapshot;
        snapshot.Sets.Should().Be(1);
        snapshot.Hits.Should().Be(1);
        snapshot.Size.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_Miss_ReturnsNullAndRecordsMiss()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var value = await cache.GetAsync<string>("missing");

        value.Should().BeNull();
        cache.Metrics.Snapshot.Misses.Should().Be(1);
        cache.Metrics.Snapshot.Size.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentCalls_UsesSingleFactoryInvocation()
    {
        using var provider = BuildProvider(options => options.EnableStampedeProtection = true);
        var cache = provider.GetRequiredService<ICacheKit>();

        var calls = 0;
        async ValueTask<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50, ct);
            return 42;
        }

        var task1 = cache.GetOrSetAsync("stampede", Factory).AsTask();
        var task2 = cache.GetOrSetAsync("stampede", Factory).AsTask();
        var results = await Task.WhenAll(task1, task2);

        results.Should().AllBeEquivalentTo(42);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrSetAsync_StaleEntry_ReturnsStaleAndRefreshesInBackground()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options =>
        {
            options.TimeProvider = timeProvider;
            options.EnableStaleWhileRevalidate = true;
        });
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("stale", 1, new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(5),
            StaleTtl = TimeSpan.FromSeconds(30)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(6));

        var refreshed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        ValueTask<int> Factory(CancellationToken _)
        {
            refreshed.TrySetResult(2);
            return new ValueTask<int>(2);
        }

        var staleValue = await cache.GetOrSetAsync("stale", Factory, new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(5),
            StaleTtl = TimeSpan.FromSeconds(30)
        });

        staleValue.Should().Be(1);
        await refreshed.Task;

        var updated = await WaitForValueAsync(cache, "stale", 2);
        updated.Should().Be(2);
    }

    [Fact]
    public async Task GetOrSetAsync_StaleEntryWithoutRefresh_RefreshesSynchronously()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options =>
        {
            options.TimeProvider = timeProvider;
            options.EnableStaleWhileRevalidate = false;
        });
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("stale-sync", 1, new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(5),
            StaleTtl = TimeSpan.FromSeconds(30)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(6));

        var value = await cache.GetOrSetAsync("stale-sync", _ => new ValueTask<int>(3), new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(5),
            StaleTtl = TimeSpan.FromSeconds(30)
        });

        value.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_RemovesEntryAndTracksEviction()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options => options.TimeProvider = timeProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("expire", "value", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(1)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        var value = await cache.GetAsync<string>("expire");

        value.Should().BeNull();
        cache.Metrics.Snapshot.Evictions.Should().Be(1);
        cache.Metrics.Snapshot.Size.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_SlidingExpiration_RefreshesExpirationOnAccess()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options => options.TimeProvider = timeProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("sliding", "value", new CacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(5)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(3));
        (await cache.GetAsync<string>("sliding")).Should().Be("value");

        timeProvider.Advance(TimeSpan.FromSeconds(4));
        (await cache.GetAsync<string>("sliding")).Should().Be("value");
    }

    [Fact]
    public async Task InvalidateTagAsync_TaggedEntry_RemovesEntry()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("tagged", "value", new CacheEntryOptions
        {
            Tags = ProductTags
        });

        await cache.InvalidateTagAsync("products");
        var value = await cache.GetAsync<string>("tagged");

        value.Should().BeNull();
        cache.Metrics.Snapshot.Size.Should().Be(0);
    }

    [Fact]
    public async Task InvalidateTagsAsync_MultipleTags_RemovesEntries()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("tagged-1", "value", new CacheEntryOptions
        {
            Tags = ProductTags
        });
        await cache.SetAsync("tagged-2", "value", new CacheEntryOptions
        {
            Tags = ProductFeatureTags
        });

        await cache.InvalidateTagsAsync(FeaturedTags);

        (await cache.GetAsync<string>("tagged-2")).Should().BeNull();
        (await cache.GetAsync<string>("tagged-1")).Should().Be("value");
    }

    [Fact]
    public async Task InvalidateTagsAsync_NullTags_ThrowsArgumentNullException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.InvalidateTagsAsync(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_InvalidOptions_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var options = new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(1),
            SlidingExpiration = TimeSpan.FromSeconds(1)
        };

        var act = () => cache.SetAsync("invalid", "value", options).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_InvalidKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.GetAsync<string>(" ").AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_CustomBinaryProvider_SerializesPayload()
    {
        var binaryProvider = new TestBinaryProvider();
        using var provider = BuildProvider(options => options.UseProvider(_ => binaryProvider), binaryProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        var person = new Person("Ada");
        await cache.SetAsync("person", person);

        binaryProvider.LastEntry.Should().NotBeNull();
        binaryProvider.LastEntry!.Payload.IsEmpty.Should().BeFalse();

        var result = await cache.GetAsync<Person>("person");
        result.Should().BeEquivalentTo(person);
    }

    [Fact]
    public async Task ExistsAsync_ExistingKey_ReturnsTrue()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("exists", "value");

        (await cache.ExistsAsync("exists")).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_MissingKey_ReturnsFalse()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        (await cache.ExistsAsync("nope")).Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ExpiredKey_ReturnsFalse()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options => options.TimeProvider = timeProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("expires", "value", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(1)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        (await cache.ExistsAsync("expires")).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_RemovesAndDecrementsSize()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("remove-me", "value");
        cache.Metrics.Snapshot.Size.Should().Be(1);

        await cache.RemoveAsync("remove-me");

        (await cache.GetAsync<string>("remove-me")).Should().BeNull();
        cache.Metrics.Snapshot.Size.Should().Be(0);
        cache.Metrics.Snapshot.Removals.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentKey_CompletesWithoutError()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.RemoveAsync("ghost");

        cache.Metrics.Snapshot.Size.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_NullFactory_ThrowsArgumentNullException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.GetOrSetAsync<string>("key", null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InvalidateTagAsync_WhitespaceTag_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.InvalidateTagAsync(" ").AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_OverwriteKey_DoesNotDoubleCountSize()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("dup", "first");
        await cache.SetAsync("dup", "second");

        cache.Metrics.Snapshot.Size.Should().Be(1);
        (await cache.GetAsync<string>("dup")).Should().Be("second");
    }

    [Fact]
    public async Task SetAsync_CategoryDefaults_UsesCategoryTtl()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options =>
        {
            options.TimeProvider = timeProvider;
            options.AddCategoryDefaults("products", new CacheEntryOptions
            {
                Ttl = TimeSpan.FromSeconds(3)
            });
        });
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("product:1", "value", new CacheEntryOptions
        {
            Category = "products"
        });

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        (await cache.GetAsync<string>("product:1")).Should().Be("value");

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        (await cache.GetAsync<string>("product:1")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_PerKeyOverridesCategory_UsesPerKeyTtl()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options =>
        {
            options.TimeProvider = timeProvider;
            options.AddCategoryDefaults("products", new CacheEntryOptions
            {
                Ttl = TimeSpan.FromSeconds(10)
            });
        });
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("product:1", "value", new CacheEntryOptions
        {
            Category = "products",
            Ttl = TimeSpan.FromSeconds(2)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(3));
        (await cache.GetAsync<string>("product:1")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_NullKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync<string>(null!, "value").AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_EmptyKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync("", "value").AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoveAsync_NullKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.RemoveAsync(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExistsAsync_NullKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.ExistsAsync(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetOrSetAsync_WhitespaceKey_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.GetOrSetAsync(" ", _ => new ValueTask<string>("x")).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InvalidateTagAsync_NullTag_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.InvalidateTagAsync(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InvalidateTagsAsync_EmptyCollection_ReturnsWithoutError()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.InvalidateTagsAsync(Array.Empty<string>());
    }

    [Fact]
    public async Task InvalidateTagsAsync_WhitespaceTag_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.InvalidateTagsAsync(WhitespaceTags).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_NegativeTtl_ThrowsArgumentOutOfRangeException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync("key", "value", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(-1)
        }).AsTask();

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetAsync_NegativeSlidingExpiration_ThrowsArgumentOutOfRangeException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync("key", "value", new CacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(-1)
        }).AsTask();

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetAsync_NegativeStaleTtl_ThrowsArgumentOutOfRangeException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync("key", "value", new CacheEntryOptions
        {
            StaleTtl = TimeSpan.FromSeconds(-1)
        }).AsTask();

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetAsync_WhitespaceTag_ThrowsArgumentException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.SetAsync("key", "value", new CacheEntryOptions
        {
            Tags = EmptyStringTags
        }).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetOrSetAsync_ExpiredEntry_CallsFactoryAndReturnsNewValue()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options => options.TimeProvider = timeProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("expired", "old", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(1)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        var value = await cache.GetOrSetAsync("expired", _ => new ValueTask<string>("new"));

        value.Should().Be("new");
    }

    [Fact]
    public async Task GetOrSetAsync_CachedEntry_DoesNotCallFactory()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("cached", "existing");

        var called = false;
        var value = await cache.GetOrSetAsync("cached", _ =>
        {
            called = true;
            return new ValueTask<string>("new");
        });

        value.Should().Be("existing");
        called.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateTagAsync_UnknownTag_CompletesWithoutError()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.InvalidateTagAsync("nonexistent");
    }

    [Fact]
    public async Task SetAsync_UpdateTags_ReplacesOldTags()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("item", "v1", new CacheEntryOptions
        {
            Tags = ProductTags
        });

        await cache.SetAsync("item", "v2", new CacheEntryOptions
        {
            Tags = FeaturedTags
        });

        await cache.InvalidateTagAsync("products");
        (await cache.GetAsync<string>("item")).Should().Be("v2");

        await cache.InvalidateTagAsync("featured");
        (await cache.GetAsync<string>("item")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_NullValue_StoresAndReturnsNull()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync<string?>("nullable", null);

        var result = await cache.GetAsync<string>("nullable");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_FactoryThrows_PropagatesException()
    {
        using var provider = BuildProvider();
        var cache = provider.GetRequiredService<ICacheKit>();

        var act = () => cache.GetOrSetAsync<string>("fail", _ =>
            throw new InvalidOperationException("boom")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task ExistsAsync_StaleKey_ReturnsTrue()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        using var provider = BuildProvider(options => options.TimeProvider = timeProvider);
        var cache = provider.GetRequiredService<ICacheKit>();

        await cache.SetAsync("stale-exists", "value", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(2),
            StaleTtl = TimeSpan.FromSeconds(10)
        });

        timeProvider.Advance(TimeSpan.FromSeconds(3));

        (await cache.ExistsAsync("stale-exists")).Should().BeTrue();
    }

    [Fact]
    public void AddCacheKit_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddCacheKit();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCacheKit_InvalidDefaultTtl_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddCacheKit(options => options.DefaultTtl = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<ICacheKit>();

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void AddCategoryDefaults_NullCategory_ThrowsArgumentException()
    {
        var options = new CacheKitOptions();

        var act = () => options.AddCategoryDefaults(null!, new CacheEntryOptions());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCategoryDefaults_InvalidOptions_ThrowsException()
    {
        var options = new CacheKitOptions();

        var act = () => options.AddCategoryDefaults("cat", new CacheEntryOptions
        {
            Ttl = TimeSpan.FromSeconds(-1)
        });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ServiceProvider BuildProvider(Action<CacheKitOptions>? configure = null, TestBinaryProvider? binaryProvider = null)
    {
        var services = new ServiceCollection();
        if (binaryProvider is not null)
        {
            services.AddSingleton(binaryProvider);
        }

        services.AddCacheKit(options =>
        {
            configure?.Invoke(options);
        });

        return services.BuildServiceProvider();
    }

    private static async Task<int?> WaitForValueAsync(ICacheKit cache, string key, int expected)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var current = await cache.GetAsync<int>(key);
            if (current == expected)
            {
                return current;
            }

            await Task.Delay(10);
        }

        return await cache.GetAsync<int>(key);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class TestBinaryProvider : ICacheProvider
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

        public CacheEntry? LastEntry { get; private set; }

        public CacheValueMode ValueMode => CacheValueMode.Binary;

        public ValueTask<CacheGetResult> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Hit, entry));
            }

            return ValueTask.FromResult(new CacheGetResult(CacheEntryState.Miss, null));
        }

        public ValueTask SetAsync(string key, CacheEntry entry, CancellationToken cancellationToken = default)
        {
            LastEntry = entry;
            _entries[key] = entry;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }
    }

    private sealed record Person(string Name);
}
