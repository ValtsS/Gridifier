using System.Collections.Concurrent;
using Gridifier.Shared.Validation;

namespace Gridifier.Worker;

public class StationCache
{
    // Dirty = cache has data not yet reflected in the DB (a repeat same-grid report
    // bumped LastHeard but wasn't written). Sweeper only flushes dirty quiet stations.
    // Field order keeps the struct at 8 bytes: uint(4) + ushort(2) + bool(1) -> pad 8.
    private readonly record struct Entry(uint LastHeard, ushort Grid, bool Dirty);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Entry>> _byBand = new();

    public long Count
    {
        get
        {
            long total = 0;
            foreach (var (_, band) in _byBand)
                total += band.Count;
            return total;
        }
    }

    public bool TryGet(string callsign, string band, out string? grid, out uint lastHeard)
    {
        if (_byBand.TryGetValue(band, out var bandDict) && bandDict.TryGetValue(callsign, out var entry))
        {
            grid = GridCodec.Decode(entry.Grid);
            lastHeard = entry.LastHeard;
            return true;
        }

        grid = null;
        lastHeard = 0;
        return false;
    }

    // Returns true if this report requires an immediate DB write
    // (new station or grid change). Always refreshes lastHeard and marks dirty.
    public bool TryUpdate(string callsign, string band, ushort grid, uint lastHeard)
    {
        var bandDict = _byBand.GetOrAdd(band, _ => new ConcurrentDictionary<string, Entry>());

        while (true)
        {
            if (!bandDict.TryGetValue(callsign, out var existing))
            {
                if (bandDict.TryAdd(callsign, new Entry(LastHeard: lastHeard, Grid: grid, Dirty: true)))
                    return true;
                continue;
            }

            if (lastHeard <= existing.LastHeard)
                return false; // out-of-order/stale report

            var needsWrite = existing.Grid != grid;
            if (bandDict.TryUpdate(callsign, new Entry(LastHeard: lastHeard, Grid: grid, Dirty: true), existing))
                return needsWrite;
        }
    }

    public void Seed(string callsign, string band, ushort grid, uint lastHeard)
    {
        var bandDict = _byBand.GetOrAdd(band, _ => new ConcurrentDictionary<string, Entry>());
        var entry = new Entry(LastHeard: lastHeard, Grid: grid, Dirty: false); // came from DB, already persisted

        bandDict.AddOrUpdate(callsign,
            entry,
            (_, existing) => existing.LastHeard >= entry.LastHeard ? existing : entry);
    }

    // Clears the dirty flag after a successful DB write. Only clears if the cache
    // still holds the written lastHeard; a newer concurrent report keeps it dirty.
    public void MarkPersisted(string callsign, string band, uint lastHeard)
    {
        if (!_byBand.TryGetValue(band, out var bandDict) || !bandDict.TryGetValue(callsign, out var existing))
            return;

        if (existing.LastHeard == lastHeard)
            bandDict.TryUpdate(callsign, existing with { Dirty = false }, existing);
    }

    public IEnumerable<(string Callsign, string Band, ushort Grid, uint LastHeard)> GetQuiet(uint cutoff)
    {
        foreach (var (band, bandDict) in _byBand)
        {
            foreach (var (callsign, entry) in bandDict)
            {
                if (entry.Dirty && entry.LastHeard <= cutoff)
                    yield return (callsign, band, entry.Grid, entry.LastHeard);
            }
        }
    }

    public Dictionary<string, long> GetCountByBand()
    {
        var counts = new Dictionary<string, long>(_byBand.Count);
        foreach (var (band, bandDict) in _byBand)
            counts[band] = bandDict.Count;
        return counts;
    }

    public long CountActive(uint cutoff)
    {
        long total = 0;
        foreach (var (_, bandDict) in _byBand)
        {
            foreach (var (_, entry) in bandDict)
            {
                if (entry.LastHeard >= cutoff)
                    total++;
            }
        }
        return total;
    }
}
