using System.Threading.Channels;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;

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
    private const int StatsInterval = 1000;
    private static readonly TimeSpan StatsMinInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Station>(BatchSize);
        var totalWritten = 0L;
        var totalSkipped = 0L;
        var lastStatsTime = DateTime.UtcNow;
        var lastStatsWritten = 0L;
        var lastStatsSkipped = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            var reader = stationChannel.Reader;

            while (batch.Count < BatchSize && reader.TryRead(out var station))
            {
                if (cache.ShouldSkip(station))
                {
                    totalSkipped++;
                    continue;
                }

                cache.MarkWritten(station);
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
            }

            cache.MaybeEvict();

            if (totalWritten + totalSkipped - lastStatsWritten - lastStatsSkipped >= StatsInterval)
            {
                var elapsed = DateTime.UtcNow - lastStatsTime;
                if (elapsed >= StatsMinInterval)
                {
                    var rate = (totalWritten - lastStatsWritten) / elapsed.TotalSeconds;
                    logger.LogInformation(
                        "Stats: {Written} written, {Skipped} deduped ({Rate:F0} writes/s), cache {Cache}",
                        totalWritten, totalSkipped, rate, cache.Count);

                    stats.TotalWritten = totalWritten;
                    stats.TotalSkipped = totalSkipped;
                    stats.CacheSize = cache.Count;
                    stats.MessagesPerSecond = rate;
                    stats.DatabaseCount = repo.Count();
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
            if (!cache.ShouldSkip(station))
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
}