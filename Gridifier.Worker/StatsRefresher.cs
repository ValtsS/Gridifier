namespace Gridifier.Worker;

public class StatsRefresher(
    ILogger<StatsRefresher> logger,
    StationCache cache,
    AppStats stats,
    TimeSpan interval,
    TimeSpan activeWindow)
    : BackgroundService
{
    private long _lastMessageCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Refresh();
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stats refresh failed");
            }
        }
    }

    public void Refresh()
    {
        var cutoff = (uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - activeWindow.TotalSeconds);
        stats.ActiveStations = cache.CountActive(cutoff);
        stats.StationsByBand = cache.GetCountByBand();
        stats.CacheSize = (int)cache.Count;

        stats.RefreshProcessDiagnostics();

        var current = stats.TotalMessagesReceived;
        stats.MessagesPerSecond = (current - _lastMessageCount) / interval.TotalSeconds;
        _lastMessageCount = current;
    }
}
