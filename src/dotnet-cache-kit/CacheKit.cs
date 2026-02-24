using JG.CacheKit.Abstractions;
using JG.CacheKit.Internal;
using JG.CacheKit.Policies;
using JG.CacheKit.Tagging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace JG.CacheKit;

/// <summary>
/// Provides a unified cache API across memory and distributed providers.
/// </summary>
/// <remarks>This type is thread-safe and intended for use as a singleton.</remarks>
public sealed partial class CacheKit : ICacheKit
{
    private readonly ICacheProvider _provider;
    private readonly ICacheSerializer _serializer;
    private readonly CacheKitOptions _options;
    private readonly CacheMetrics _metrics;
    private readonly ICacheTagIndex _tagIndex;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CacheKit> _logger;
    private readonly KeyedSemaphore _semaphore = new();
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheKit"/> class.
    /// </summary>
    /// <param name="provider">The cache provider.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="options">The cache options.</param>
    /// <param name="metrics">The cache metrics.</param>
    /// <param name="tagIndex">The tag index.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    internal CacheKit(
        ICacheProvider provider,
        ICacheSerializer serializer,
        IOptions<CacheKitOptions> options,
        CacheMetrics metrics,
        ICacheTagIndex tagIndex,
        TimeProvider timeProvider,
        ILogger<CacheKit>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _tagIndex = tagIndex ?? throw new ArgumentNullException(nameof(tagIndex));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? NullLogger<CacheKit>.Instance;
    }

    /// <inheritdoc />
    public ICacheMetrics Metrics => _metrics;

    /// <inheritdoc />
    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        CacheKey.Validate(key);

        var result = await _provider.GetAsync(key, cancellationToken).ConfigureAwait(false);

        if (result.Entry is null)
        {
            if (result.State == CacheEntryState.Expired)
            {
                var removed = _keys.TryRemove(key, out _);
                _metrics.RecordEviction(removed);
                await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _metrics.RecordMiss();
            }

            return default;
        }

        switch (result.State)
        {
            case CacheEntryState.Hit:
                _metrics.RecordHit();
                return GetValue<T>(result.Entry);
            case CacheEntryState.Stale:
                _metrics.RecordStaleHit();
                return GetValue<T>(result.Entry);
            case CacheEntryState.Expired:
            {
                var removed = _keys.TryRemove(key, out _);
                _metrics.RecordEviction(removed);
                await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                return default;
            }
            default:
                _metrics.RecordMiss();
                return default;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        CacheKey.Validate(key);

        var result = await _provider.GetAsync(key, cancellationToken).ConfigureAwait(false);

        if (result.State == CacheEntryState.Expired)
        {
            var removed = _keys.TryRemove(key, out _);
            _metrics.RecordEviction(removed);
            await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return false;
        }

        return result.State is CacheEntryState.Hit or CacheEntryState.Stale;
    }

    /// <inheritdoc />
    public async ValueTask SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        CacheKey.Validate(key);

        var entry = CreateEntry(value, options);
        await _provider.SetAsync(key, entry, cancellationToken).ConfigureAwait(false);
        _metrics.RecordSet(_keys.TryAdd(key, 0));

        await UpdateTagsAsync(key, options?.Tags, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        CacheKey.Validate(key);
        ArgumentNullException.ThrowIfNull(factory);

        var result = await _provider.GetAsync(key, cancellationToken).ConfigureAwait(false);

        if (result.Entry is not null)
        {
            if (result.State == CacheEntryState.Hit)
            {
                _metrics.RecordHit();
                return GetValue<T>(result.Entry)!;
            }

            if (result.State == CacheEntryState.Stale)
            {
                var staleValue = GetValue<T>(result.Entry);
                _metrics.RecordStaleHit();

                if (_options.EnableStaleWhileRevalidate)
                {
                    StartBackgroundRefresh(key, factory, options);
                    return staleValue!;
                }
            }
        }
        else if (result.State == CacheEntryState.Expired)
        {
            var removed = _keys.TryRemove(key, out _);
            _metrics.RecordEviction(removed);
            await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }

        return await PopulateAsync(key, factory, options, result.State == CacheEntryState.Expired, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        CacheKey.Validate(key);

        await _provider.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        _metrics.RecordRemoval(_keys.TryRemove(key, out _));
        await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be null or whitespace.", nameof(tag));
        }

        var keys = await _tagIndex.GetKeysAsync(tag, cancellationToken).ConfigureAwait(false);
        await RemoveKeysAsync(keys, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Tags cannot be null or whitespace.", nameof(tags));
            }

            var tagKeys = await _tagIndex.GetKeysAsync(tag, cancellationToken).ConfigureAwait(false);
            foreach (var key in tagKeys)
            {
                keys.Add(key);
            }
        }

        await RemoveKeysAsync(keys, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RemoveKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            await _provider.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            _metrics.RecordRemoval(_keys.TryRemove(key, out _));
            await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<T> PopulateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryOptions? options,
        bool evictionRecorded,
        CancellationToken cancellationToken)
    {
        if (_options.EnableStampedeProtection)
        {
            using var lease = await _semaphore.AcquireAsync(key, cancellationToken).ConfigureAwait(false);
            var result = await _provider.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (result.Entry is not null && result.State == CacheEntryState.Hit)
            {
                _metrics.RecordHit();
                return GetValue<T>(result.Entry)!;
            }

            if (result.State == CacheEntryState.Expired && !evictionRecorded)
            {
                var removed = _keys.TryRemove(key, out _);
                _metrics.RecordEviction(removed);
                await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }

            _metrics.RecordMiss();
            return await CreateAndStoreAsync(key, factory, options, cancellationToken).ConfigureAwait(false);
        }

        _metrics.RecordMiss();
        return await CreateAndStoreAsync(key, factory, options, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<T> CreateAndStoreAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryOptions? options,
        CancellationToken cancellationToken)
    {
        var value = await factory(cancellationToken).ConfigureAwait(false);
        var entry = CreateEntry(value, options);
        await _provider.SetAsync(key, entry, cancellationToken).ConfigureAwait(false);
        _metrics.RecordSet(_keys.TryAdd(key, 0));
        await UpdateTagsAsync(key, options?.Tags, cancellationToken).ConfigureAwait(false);
        return value!;
    }

    private CacheEntry CreateEntry<T>(T value, CacheEntryOptions? options)
    {
        return _provider.ValueMode == CacheValueMode.InMemory
            ? CacheEntryFactory.CreateObjectEntry(value, _options, options, _timeProvider)
            : CacheEntryFactory.CreateBinaryEntry(_serializer.Serialize(value), _options, options, _timeProvider);
    }

    private async ValueTask UpdateTagsAsync(string key, IReadOnlyCollection<string>? tags, CancellationToken cancellationToken)
    {
        await _tagIndex.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        if (tags is { Count: > 0 })
        {
            await _tagIndex.AddAsync(key, tags, cancellationToken).ConfigureAwait(false);
        }
    }

    private T? GetValue<T>(CacheEntry entry)
    {
        return _provider.ValueMode == CacheValueMode.InMemory
            ? (T?)entry.Value
            : _serializer.Deserialize<T>(entry.Payload);
    }

    private void StartBackgroundRefresh<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options)
    {
        if (_options.EnableStampedeProtection)
        {
            if (!_semaphore.TryAcquire(key, out var lease))
            {
                return;
            }

            var refreshTask = RefreshAsync(key, factory, options, lease);
            _ = refreshTask.ContinueWith(
                static (task, state) => LogBackgroundRefreshFailed((ILogger)state!, task.Exception),
                _logger,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return;
        }

        var fallbackTask = RefreshAsync(key, factory, options, default);
        _ = fallbackTask.ContinueWith(
            static (task, state) => LogBackgroundRefreshFailed((ILogger)state!, task.Exception),
            _logger,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Background cache refresh failed.")]
    private static partial void LogBackgroundRefreshFailed(ILogger logger, Exception? exception);

    private async Task RefreshAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryOptions? options,
        KeyedSemaphore.SemaphoreLease lease)
    {
        try
        {
            await CreateAndStoreAsync(key, factory, options, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
        }
    }
}
