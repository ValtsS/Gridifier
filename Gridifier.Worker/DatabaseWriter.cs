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
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Station>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            var reader = stationChannel.Reader;

            while (batch.Count < BatchSize && reader.TryRead(out var station))
            {
                batch.Add(station);
            }

            if (batch.Count > 0)
            {
                try
                {
                    repo.UpsertMany(batch);
                    logger.LogDebug("Flushed {Count} stations", batch.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error flushing {Count} stations", batch.Count);
                }
                batch.Clear();
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

        // drain remaining on shutdown
        while (stationChannel.Reader.TryRead(out var station))
        {
            batch.Add(station);
        }

        if (batch.Count > 0)
        {
            try { repo.UpsertMany(batch); }
            catch (Exception ex) { logger.LogError(ex, "Error flushing final {Count} stations", batch.Count); }
        }
    }
}