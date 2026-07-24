using System.Threading.Channels;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;

namespace Gridifier.Worker;

public class DatabaseWriter(
    ILogger<DatabaseWriter> logger,
    Channel<Station> stationChannel,
    StationRepository repo)
    : BackgroundService
{
    private const int BatchSize = 100;
    private const int StatsInterval = 1000;
    private const int CacheMaxSize = 10_000;
    private const int EvictionCheckInterval = 50;
    private static readonly TimeSpan StatsMinInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DbWriteCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan CacheEntryTtl = TimeSpan.FromHours(2);

    private record CacheEntry(string Grid, DateTime LastDbWrite);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Station>(BatchSize);
        var cache = new Dictionary<string, CacheEntry>(CacheMaxSize);
        var totalWritten = 0L;
        var totalSkipped = 0L;
        var flushCount = 0;
        var lastStatsTime = DateTime.UtcNow;
        var lastStatsWritten = 0L;
        var lastStatsSkipped = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            var reader = stationChannel.Reader;

            while (batch.Count < BatchSize && reader.TryRead(out var station))
            {
                if (ShouldSkip(cache, station))
                {
                    totalSkipped++;
                    continue;
                }

                cache[station.Callsign] = new CacheEntry(station.Grid, DateTime.UtcNow);
                batch.Add(station);
            }

            if (batch.Count > 0)
            {
                try
                {
                    repo.UpsertMany(batch);
                    totalWritten += batch.Count;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error flushing {Count} stations", batch.Count);
                }
                batch.Clear();
                flushCount++;
            }

            if (flushCount % EvictionCheckInterval == 0 && cache.Count > 0)
            {
                var cutoff = DateTime.UtcNow - CacheEntryTtl;
                var removals = 0;

                if (cache.Count > CacheMaxSize)
                {
                    var toRemove = cache
                        .OrderBy(x => x.Value.LastDbWrite)
                        .Take(cache.Count - CacheMaxSize)
                        .Select(x => x.Key)
                        .ToList();

                    foreach (var key in toRemove)
                    {
                        cache.Remove(key);
                        removals++;
                    }
                }
                else
                {
                    var stale = cache.Where(x => x.Value.LastDbWrite < cutoff).ToList();
                    foreach (var kv in stale)
                    {
                        cache.Remove(kv.Key);
                        removals++;
                    }
                }

                if (removals > 0)
                    logger.LogDebug("Evicted {Count} entries from cache ({CacheSize} remain)", removals, cache.Count);
            }

            if (totalWritten + totalSkipped - lastStatsWritten - lastStatsSkipped >= StatsInterval)
            {
                var elapsed = DateTime.UtcNow - lastStatsTime;
                if (elapsed >= StatsMinInterval)
                {
                    var rate = (totalWritten - lastStatsWritten) / elapsed.TotalSeconds;
                    logger.LogInformation(
                        "Stats: {Written} written, {Skipped} deduped ({Rate:F0} writes/s), cache {Cache}",
                        totalWritten, totalSkipped, rate, cache.Count);
                    lastStatsTime = DateTime.UtcNow;
                    lastStatsWritten = totalWritten;
                    lastStatsSkipped = totalSkipped;
                }
            }

            if (batch.Count == 0)
            {
                try
                {
                    await reader.WaitToReadAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        while (stationChannel.Reader.TryRead(out var station))
        {
            if (!ShouldSkip(cache, station))
                batch.Add(station);
        }

        if (batch.Count > 0)
        {
            try
            {
                repo.UpsertMany(batch);
                totalWritten += batch.Count;
                logger.LogInformation("Final flush: {Count} stations (total {Total})", batch.Count, totalWritten);
            }
            catch (Exception ex) { logger.LogError(ex, "Error flushing final {Count} stations", batch.Count); }
        }

        logger.LogInformation("Shutdown complete. Total: {Written} written, {Skipped} deduped",
            totalWritten, totalSkipped);
    }

    private static bool ShouldSkip(Dictionary<string, CacheEntry> cache, Station station)
    {
        if (!cache.TryGetValue(station.Callsign, out var entry))
            return false;

        if (entry.Grid != station.Grid)
            return false;

        return DateTime.UtcNow - entry.LastDbWrite < DbWriteCooldown;
    }
}