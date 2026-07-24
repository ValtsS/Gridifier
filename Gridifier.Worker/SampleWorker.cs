namespace Gridifier.Worker;

public class SampleWorker(ILogger<SampleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SampleWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("SampleWorker heartbeat at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        logger.LogInformation("SampleWorker stopped");
    }
}