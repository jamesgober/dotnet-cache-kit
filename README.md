<div align="center">
    <img width="120px" height="auto" src="https://raw.githubusercontent.com/jamesgober/jamesgober/main/media/icons/hexagon-3.svg" alt="Triple Hexagon">
    <h1>
        <strong>dotnet-cache-kit</strong>
        <sup><br><sub>CACHING ABSTRACTION</sub></sup>
    </h1>
    <div>
        <a href="https://www.nuget.org/packages/dotnet-cache-kit"><img alt="NuGet" src="https://img.shields.io/nuget/v/dotnet-cache-kit"></a>
        <span>&nbsp;</span>
        <a href="https://www.nuget.org/packages/dotnet-cache-kit"><img alt="NuGet Downloads" src="https://img.shields.io/nuget/dt/dotnet-cache-kit?color=%230099ff"></a>
        <span>&nbsp;</span>
        <a href="./LICENSE" title="License"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg"></a>
        <span>&nbsp;</span>
        <a href="https://github.com/jamesgober/dotnet-cache-kit/actions"><img alt="GitHub CI" src="https://github.com/jamesgober/dotnet-cache-kit/actions/workflows/ci.yml/badge.svg"></a>
    </div>
</div>
<br>
<p>
    In-memory and distributed caching abstraction for .NET. Unified API across memory and Redis backends, with stampede protection, TTL policies, cache-aside pattern, and stale-while-revalidate support — designed for high-throughput services where cache misses are expensive.
</p>

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

<br>

## Installation

```bash
dotnet add package dotnet-cache-kit
```

<br>

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

<br>

## Documentation

- **[API Reference](./docs/API.md)** — Full API documentation and examples

<br>

## Contributing

Contributions welcome. Please:
1. Ensure all tests pass before submitting
2. Follow existing code style and patterns
3. Update documentation as needed

<br>

## Testing

```bash
dotnet test
```

<br>
<hr>
<br>

<div id="license">
    <h2>⚖️ License</h2>
    <p>Licensed under the <b>Apache License</b>, version 2.0 (the <b>"License"</b>); you may not use this software, including, but not limited to the source code, media files, ideas, techniques, or any other associated property or concept belonging to, associated with, or otherwise packaged with this software except in compliance with the <b>License</b>.</p>
    <p>You may obtain a copy of the <b>License</b> at: <a href="http://www.apache.org/licenses/LICENSE-2.0" title="Apache-2.0 License" target="_blank">http://www.apache.org/licenses/LICENSE-2.0</a>.</p>
    <p>Unless required by applicable law or agreed to in writing, software distributed under the <b>License</b> is distributed on an "<b>AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND</b>, either express or implied.</p>
    <p>See the <a href="./LICENSE" title="Software License file">LICENSE</a> file included with this project for the specific language governing permissions and limitations under the <b>License</b>.</p>
    <br>
</div>

<div align="center">
    <h2></h2>
    <sup>COPYRIGHT <small>&copy;</small> 2025 <strong>JAMES GOBER.</strong></sup>
</div>
