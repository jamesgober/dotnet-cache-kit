namespace JG.CacheKit.Tagging;

internal interface ICacheTagIndex
{
    ValueTask AddAsync(string key, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyCollection<string>> GetKeysAsync(string tag, CancellationToken cancellationToken = default);
}
