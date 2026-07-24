using Gridifier.Shared.Models;

namespace Gridifier.Worker;

public class StationCache
{
    private const int MaxSize = 10_000;
    private static readonly TimeSpan DbWriteCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(2);

    private record Entry(string Grid, DateTime LastDbWrite);

    private readonly Dictionary<string, Entry> _cache = new(MaxSize);
    private int _flushCount;

    public int Count => _cache.Count;

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
        _cache[station.Callsign] = new Entry(station.Grid, DateTime.UtcNow);
    }

    public void MaybeEvict()
    {
        _flushCount++;

        if (_flushCount % 50 != 0 || _cache.Count == 0)
            return;

        var cutoff = DateTime.UtcNow - EntryTtl;

        if (_cache.Count > MaxSize)
        {
            var toRemove = _cache
                .OrderBy(x => x.Value.LastDbWrite)
                .Take(_cache.Count - MaxSize)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in toRemove)
                _cache.Remove(key);
        }
        else
        {
            var stale = _cache.Where(x => x.Value.LastDbWrite < cutoff).ToList();
            foreach (var kv in stale)
                _cache.Remove(kv.Key);
        }
    }
}