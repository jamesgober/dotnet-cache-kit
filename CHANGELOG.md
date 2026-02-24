# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]


## [1.0.0] - 2026-02-24

### Added
- `ICacheKit` unified cache API with `GetAsync`, `SetAsync`, `GetOrSetAsync`, `ExistsAsync`, `RemoveAsync`.
- `ICacheProvider` abstraction with `MemoryCacheProvider` and `DistributedCacheProvider` implementations.
- `ICacheSerializer` abstraction with `JsonCacheSerializer` (System.Text.Json) default.
- Stampede protection via keyed semaphore in `GetOrSetAsync`.
- Stale-while-revalidate with background refresh and `LoggerMessage`-based error logging.
- Absolute and sliding TTL expiration with per-key, per-category, and global defaults.
- `AddCategoryDefaults` on `CacheKitOptions` for category-level TTL policies.
- `CacheEntryOptions.Category` for resolving per-category defaults.
- Tag-based bulk invalidation via `InvalidateTagAsync` and `InvalidateTagsAsync`.
- `CacheMetrics` with hit, miss, stale hit, set, removal, eviction, and size counters.
- `ExistsAsync` for checking key existence without retrieving the value.
- `AddCacheKit()` single-call DI registration with `UseMemory()`, `UseDistributed()`, and `UseProvider()`.
- Pluggable serialization via `UseSerializer()`.
- Binary envelope format for distributed cache entries with `CacheEnvelopeSerializer`.
- `CacheEntryOptions` validation (negative TTL, conflicting TTL+sliding, whitespace tags).
- `<exception>` XML documentation on all public API methods.
- Thread safety `<remarks>` on all shared types.
- `docs/API.md` with full API reference and examples.
- CI workflow with multi-OS matrix, code coverage collection, and warnings-as-errors.
- SourceLink support (`PublishRepositoryUrl`, `EmbedUntrackedSources`).
- 46 unit tests covering happy paths, edge cases, error paths, and concurrency.


[Unreleased]: https://github.com/jamesgober/dotnet-cache-kit/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/jamesgober/dotnet-cache-kit/releases/tag/1.0.0
