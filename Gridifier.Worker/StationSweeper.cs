using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Shared.Validation;

namespace Gridifier.Worker;

public class StationSweeper(
    ILogger<StationSweeper> logger,
    StationCache cache,
    StationRepository repo,
    AppStats stats,
    TimeSpan interval)
    : BackgroundService
{
    private const int ChunkSize = 5_000;
    private readonly uint _quietSeconds = (uint)interval.TotalSeconds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                SweepOnce();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sweep failed");
            }
        }
    }

    private void SweepOnce()
    {
        var now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cutoff = now - _quietSeconds;
        var chunk = new List<Station>(ChunkSize);
        var written = 0L;

        foreach (var (callsign, band, grid, lastHeard) in cache.GetQuiet(cutoff))
        {
            chunk.Add(new Station
            {
                Callsign = callsign,
                Band = band,
                Grid = GridCodec.Decode(grid),
                LastUpdate = lastHeard
            });

            if (chunk.Count >= ChunkSize)
            {
                repo.UpsertMany(chunk);
                written += chunk.Count;
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            repo.UpsertMany(chunk);
            written += chunk.Count;
        }

        if (written > 0)
            logger.LogInformation("Sweep persisted {Count} quiet stations", written);

        stats.LastSweepAt = DateTime.UtcNow;
        stats.LastSweepPersisted = written;
        stats.DatabaseCount = repo.Count();
    }
}
