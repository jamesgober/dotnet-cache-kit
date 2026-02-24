# API Reference

## Registration

```csharp
services.AddCacheKit(options =>
{
    options.UseMemory();
    options.DefaultTtl = TimeSpan.FromMinutes(5);
    options.EnableStampedeProtection = true;
    options.EnableStaleWhileRevalidate = true;
});
```

### Provider Configuration

| Method | Description |
|---|---|
| `UseMemory()` | In-memory cache (default). Suitable for single-instance deployments. |
| `UseDistributed()` | Uses a registered `IDistributedCache` (Redis, SQL Server, etc.). |
| `UseProvider(factory)` | Registers a custom `ICacheProvider` implementation. |

### Distributed Cache (Redis)

Register a distributed cache before calling `AddCacheKit`:

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

services.AddCacheKit(options => options.UseDistributed());
```

### Serialization

The default serializer uses `System.Text.Json`. To use a custom serializer:

```csharp
options.UseSerializer(new MyCustomSerializer());
// or
options.UseSerializer(sp => sp.GetRequiredService<MySerializer>());
```

Custom serializers implement `ICacheSerializer`:

```csharp
public interface ICacheSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
```

---

## ICacheKit

All methods accept an optional `CancellationToken`.

### GetAsync\<T\>

Returns the cached value for a key, or `default` if not found or expired.

```csharp
var product = await cache.GetAsync<Product>("product:123");
```

### ExistsAsync

Returns `true` if a key exists and has not expired.

```csharp
if (await cache.ExistsAsync("product:123"))
{
    // key is present
}
```

### SetAsync\<T\>

Stores a value with optional per-entry configuration.

```csharp
await cache.SetAsync("product:123", product, new CacheEntryOptions
{
    Ttl = TimeSpan.FromMinutes(10),
    Tags = new[] { "products" }
});
```

### GetOrSetAsync\<T\>

Returns the cached value or invokes the factory to populate it. Stampede protection ensures only one caller executes the factory for a given key.

```csharp
var product = await cache.GetOrSetAsync(
    "product:123",
    async ct => await db.Products.FindAsync(id, ct),
    new CacheEntryOptions { Ttl = TimeSpan.FromMinutes(10) });
```

### RemoveAsync

Removes a cached entry by key. Idempotent — removing a non-existent key is a no-op.

```csharp
await cache.RemoveAsync("product:123");
```

### InvalidateTagAsync / InvalidateTagsAsync

Removes all entries associated with one or more tags.

```csharp
await cache.InvalidateTagAsync("products");
await cache.InvalidateTagsAsync(new[] { "products", "featured" });
```

---

## CacheEntryOptions

Per-entry configuration passed to `SetAsync` and `GetOrSetAsync`.

| Property | Type | Description |
|---|---|---|
| `Ttl` | `TimeSpan?` | Absolute time-to-live. |
| `SlidingExpiration` | `TimeSpan?` | Sliding expiration window. Mutually exclusive with `Ttl`. |
| `StaleTtl` | `TimeSpan?` | Duration after expiration during which stale data is served while refreshing. |
| `Tags` | `IReadOnlyCollection<string>?` | Tags for bulk invalidation. |
| `Category` | `string?` | Category name for resolving per-category defaults. |

---

## CacheKitOptions

Global defaults configured via `AddCacheKit`.

| Property | Type | Default | Description |
|---|---|---|---|
| `DefaultTtl` | `TimeSpan` | 5 minutes | Default TTL when no per-entry TTL is specified. |
| `DefaultSlidingExpiration` | `TimeSpan?` | `null` | Default sliding expiration. |
| `DefaultStaleTtl` | `TimeSpan?` | `null` | Default stale window. |
| `EnableStampedeProtection` | `bool` | `true` | Lock-based cache population to prevent thundering herd. |
| `EnableStaleWhileRevalidate` | `bool` | `true` | Serve stale data while refreshing in the background. |
| `TimeProvider` | `TimeProvider` | `TimeProvider.System` | Clock used for expiration. Override for testing. |

### Per-Category Defaults

Define default options for groups of cache entries:

```csharp
options.AddCategoryDefaults("products", new CacheEntryOptions
{
    Ttl = TimeSpan.FromMinutes(30)
});
```

Then reference the category in individual operations:

```csharp
await cache.SetAsync("product:123", product, new CacheEntryOptions
{
    Category = "products"
});
```

Per-key options override category defaults. Category defaults override global defaults.

---

## Metrics

Access metrics via `cache.Metrics.Snapshot`:

```csharp
var snapshot = cache.Metrics.Snapshot;
Console.WriteLine($"Hits: {snapshot.Hits}, Misses: {snapshot.Misses}, Size: {snapshot.Size}");
```

| Field | Description |
|---|---|
| `Hits` | Cache hits. |
| `Misses` | Cache misses. |
| `StaleHits` | Stale values served during revalidation. |
| `Sets` | Set operations. |
| `Removals` | Explicit removals. |
| `Evictions` | Entries removed due to expiration. |
| `Size` | Current number of tracked entries. |
