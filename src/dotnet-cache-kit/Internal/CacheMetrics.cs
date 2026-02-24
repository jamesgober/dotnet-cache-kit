using JG.CacheKit.Abstractions;

namespace JG.CacheKit.Internal;

internal sealed class CacheMetrics : ICacheMetrics
{
    private long _hits;
    private long _misses;
    private long _staleHits;
    private long _sets;
    private long _removals;
    private long _evictions;
    private long _size;

    public CacheMetricsSnapshot Snapshot => new(
        Interlocked.Read(ref _hits),
        Interlocked.Read(ref _misses),
        Interlocked.Read(ref _staleHits),
        Interlocked.Read(ref _sets),
        Interlocked.Read(ref _removals),
        Interlocked.Read(ref _evictions),
        Interlocked.Read(ref _size));

    public void RecordHit() => Interlocked.Increment(ref _hits);

    public void RecordMiss() => Interlocked.Increment(ref _misses);

    public void RecordStaleHit() => Interlocked.Increment(ref _staleHits);

    public void RecordSet(bool keyAdded)
    {
        Interlocked.Increment(ref _sets);
        if (keyAdded)
        {
            Interlocked.Increment(ref _size);
        }
    }

    public void RecordRemoval(bool keyRemoved)
    {
        Interlocked.Increment(ref _removals);
        if (keyRemoved)
        {
            Interlocked.Decrement(ref _size);
        }
    }

    public void RecordEviction(bool keyRemoved)
    {
        Interlocked.Increment(ref _evictions);
        if (keyRemoved)
        {
            Interlocked.Decrement(ref _size);
        }
    }
}
