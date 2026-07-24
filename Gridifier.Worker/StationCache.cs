using Gridifier.Shared.Models;

namespace Gridifier.Worker;

public class StationCache
{
    private const int MaxSize = 10_000;
    private static readonly TimeSpan DbWriteCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(2);

    private record Entry(string Grid, DateTime LastDbWrite);

    private readonly Dictionary<string, Entry> _cache = new(MaxSize);
    private readonly PriorityQueue<string, DateTime> _evictionQueue = new();
    private int _flushCount;

    public int Count => _cache.Count;

    public bool TryGet(string callsign, out string? grid, out DateTime? lastUpdate)
    {
        if (_cache.TryGetValue(callsign, out var entry))
        {
            grid = entry.Grid;
            lastUpdate = entry.LastDbWrite;
            return true;
        }

        grid = null;
        lastUpdate = null;
        return false;
    }

    public bool ShouldSkip(Station station)
    {
        if (!_cache.TryGetValue(station.Callsign, out var entry))
            return false;

        if (entry.Grid != station.Grid)
            return false;

        return DateTime.UtcNow - entry.LastDbWrite < DbWriteCooldown;
    }

    public void MarkWritten(Station station)
    {
        var now = DateTime.UtcNow;
        _cache[station.Callsign] = new Entry(station.Grid, now);
        _evictionQueue.Enqueue(station.Callsign, now);
    }

    public void MaybeEvict()
    {
        _flushCount++;

        if (_flushCount % 50 != 0 || _cache.Count == 0)
            return;

        LazyEvict();
    }

    private void LazyEvict()
    {
        var now = DateTime.UtcNow;
        var cutoff = now - EntryTtl;

        while (_evictionQueue.TryPeek(out var callsign, out var timestamp))
        {
            if (_cache.TryGetValue(callsign, out var entry) && entry.LastDbWrite == timestamp)
            {
                if (entry.LastDbWrite >= cutoff && _cache.Count <= MaxSize)
                    break;

                _cache.Remove(callsign);
            }

            _evictionQueue.Dequeue();
        }
    }
}