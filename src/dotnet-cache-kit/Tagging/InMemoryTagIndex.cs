using System.Collections.Concurrent;

namespace JG.CacheKit.Tagging;

internal sealed class InMemoryTagIndex : ICacheTagIndex
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagToKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string[]> _keyToTags = new(StringComparer.Ordinal);

    public ValueTask AddAsync(string key, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        if (tags.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var tagArray = new string[tags.Count];
        var index = 0;
        foreach (var tag in tags)
        {
            tagArray[index++] = tag;
        }

        _keyToTags[key] = tagArray;

        foreach (var tag in tagArray)
        {
            var keys = _tagToKeys.GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            keys[key] = 0;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_keyToTags.TryRemove(key, out var tags))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var tag in tags)
        {
            if (_tagToKeys.TryGetValue(tag, out var keys))
            {
                keys.TryRemove(key, out _);
                if (keys.IsEmpty)
                {
                    _tagToKeys.TryRemove(tag, out _);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyCollection<string>> GetKeysAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (!_tagToKeys.TryGetValue(tag, out var keys) || keys.IsEmpty)
        {
            return ValueTask.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
        }

        var result = new string[keys.Count];
        var index = 0;
        foreach (var key in keys.Keys)
        {
            result[index++] = key;
        }

        return ValueTask.FromResult<IReadOnlyCollection<string>>(result);
    }
}
