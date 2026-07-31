using Gridifier.Shared.Data;

namespace Gridifier.Worker;

public class DatabaseBackupWorker(
    ILogger<DatabaseBackupWorker> logger,
    string dbPath,
    TimeSpan interval)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                DatabaseBackup.TakeSnapshot(dbPath);
                logger.LogInformation("Database snapshot taken");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database snapshot failed");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var conn = new DbConnectionFactory($"Data Source={dbPath}").CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
            cmd.ExecuteScalar();

            DatabaseBackup.TakeSnapshot(dbPath);
            logger.LogInformation("Database checkpoint and snapshot on shutdown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shutdown snapshot failed");
        }

        await base.StopAsync(cancellationToken);
    }
}
