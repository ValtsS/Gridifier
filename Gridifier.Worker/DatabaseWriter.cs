using System.Threading.Channels;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Shared.Validation;

namespace Gridifier.Worker;

public class DatabaseWriter(
    ILogger<DatabaseWriter> logger,
    Channel<Station> stationChannel,
    StationRepository repo,
    StationCache cache,
    AppStats stats)
    : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan StatsInterval = TimeSpan.FromMinutes(1);

    private void TryProcess(Station station, List<Station> batch)
    {
        var grid = GridCodec.Encode(GridValidator.Shorten(station.Grid));
        var lastHeard = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (cache.TryUpdate(station.Callsign, station.Band, grid, lastHeard))
        {
            station.LastUpdate = lastHeard;
            batch.Add(station);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Station>(BatchSize);
        var totalWritten = 0L;
        var lastStatsTime = DateTime.UtcNow;
        var lastStatsWritten = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            var reader = stationChannel.Reader;

            while (batch.Count < BatchSize && reader.TryRead(out var station))
            {
                TryProcess(station, batch);
            }

            if (batch.Count > 0)
            {
                try
                {
                    repo.UpsertMany(batch);
                    foreach (var station in batch)
                        cache.MarkPersisted(station.Callsign, station.Band, (uint)station.LastUpdate);
                    totalWritten += batch.Count;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error flushing {Count} stations", batch.Count);
                }
                batch.Clear();
            }

            var elapsed = DateTime.UtcNow - lastStatsTime;
            if (elapsed >= StatsInterval)
            {
                var rate = (totalWritten - lastStatsWritten) / elapsed.TotalSeconds;
                logger.LogInformation(
                    "Stats: {Written} written ({Rate:F0} writes/s), cache {Cache}",
                    totalWritten, rate, cache.Count);

                stats.TotalWritten = totalWritten;
                stats.CacheSize = (int)cache.Count;
                stats.DatabaseCount = repo.Count();
                lastStatsTime = DateTime.UtcNow;
                lastStatsWritten = totalWritten;
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
            TryProcess(station, batch);
        }

        if (batch.Count > 0)
        {
            try
            {
                repo.UpsertMany(batch);
                foreach (var station in batch)
                    cache.MarkPersisted(station.Callsign, station.Band, (uint)station.LastUpdate);
                totalWritten += batch.Count;
                logger.LogInformation("Final flush: {Count} stations (total {Total})", batch.Count, totalWritten);
            }
            catch (Exception ex) { logger.LogError(ex, "Error flushing final {Count} stations", batch.Count); }
        }

        logger.LogInformation("Shutdown complete. Total: {Written} written", totalWritten);
    }
}
