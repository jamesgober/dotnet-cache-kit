# dotnet-cache-kit

[![NuGet](https://img.shields.io/nuget/v/JG.CacheKit?logo=nuget)](https://www.nuget.org/packages/JG.CacheKit)
[![Downloads](https://img.shields.io/nuget/dt/JG.CacheKit?color=%230099ff&logo=nuget)](https://www.nuget.org/packages/JG.CacheKit)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](./LICENSE)
[![CI](https://github.com/jamesgober/dotnet-cache-kit/actions/workflows/ci.yml/badge.svg)](https://github.com/jamesgober/dotnet-cache-kit/actions)

---

In-memory and distributed caching abstraction for .NET. Unified API across memory and Redis backends, with stampede protection, TTL policies, cache-aside pattern, and stale-while-revalidate support — designed for high-throughput services where cache misses are expensive.


## Features

- **Unified API** — Same interface for in-memory and Redis; swap backends without code changes
- **Stampede Protection** — Lock-based cache population prevents thundering herd on cold keys
- **Stale-While-Revalidate** — Serve stale data while refreshing in the background; never block on cache miss
- **TTL Policies** — Absolute and sliding expiration with per-key and per-category defaults
- **Cache-Aside** — Built-in `GetOrSetAsync` with factory delegate for clean cache-aside patterns
- **Serialization** — Pluggable serialization (System.Text.Json default, MessagePack optional)
- **Tagging** — Tag cache entries for bulk invalidation (e.g., invalidate all entries tagged "user:123")
- **Metrics** — Hit/miss counters, eviction tracking, and cache size reporting
- **Single Registration** — `services.AddCacheKit()`

## Installation

```bash
dotnet add package JG.CacheKit
```

## Quick Start

```csharp
builder.Services.AddCacheKit(options =>
{
    options.UseMemory();                    // In-memory for dev
    // options.UseRedis("localhost:6379"); // Redis for production
    options.DefaultTtl = TimeSpan.FromMinutes(5);
    options.EnableStampedeProtection = true;
});

// Usage
public class ProductService(ICacheKit cache)
{
    public async Task<Product> GetProduct(string id) =>
        await cache.GetOrSetAsync($"product:{id}",
            () => _db.Products.FindAsync(id),
            ttl: TimeSpan.FromMinutes(10));
}
```

## Documentation

- **[API Reference](./docs/API.md)** — Full API documentation and examples

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

Licensed under the Apache License 2.0. See [LICENSE](./LICENSE) for details.

---

**Ready to get started?** Install via NuGet and check out the [API reference](./docs/API.md).
