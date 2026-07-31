using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gridifier.Tests;

public class DatabaseBackupTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly string _connString;

    public DatabaseBackupTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"gridifier-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        _connString = $"Data Source={_dbPath};Pooling=False";
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { }
    }

    private void CreateDbWithRow(string callsign = "TEST1")
    {
        var factory = new DbConnectionFactory(_connString);
        using (var conn = factory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        new StationRepository(factory).Upsert(
            new Station { Callsign = callsign, Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 });
    }

    [Fact]
    public void TakeSnapshot_creates_backup_and_rotates_previous_generation()
    {
        CreateDbWithRow();
        DatabaseBackup.TakeSnapshot(_dbPath);

        Assert.True(File.Exists(DatabaseBackup.SnapshotPath(_dbPath)));
        Assert.False(File.Exists(DatabaseBackup.PreviousSnapshotPath(_dbPath)));

        CreateDbWithRow(callsign: "TEST2");
        DatabaseBackup.TakeSnapshot(_dbPath);

        Assert.True(File.Exists(DatabaseBackup.PreviousSnapshotPath(_dbPath)));
    }

    [Fact]
    public void IsHealthy_returns_true_for_valid_db()
    {
        CreateDbWithRow();
        Assert.True(DatabaseBackup.IsHealthy(_dbPath));
    }

    [Fact]
    public void IsHealthy_returns_false_for_corrupt_db()
    {
        File.WriteAllBytes(_dbPath, [1, 2, 3, 4, 5]);
        Assert.False(DatabaseBackup.IsHealthy(_dbPath));
    }

    [Fact]
    public void TryRecoverFromSnapshot_restores_data_after_corruption()
    {
        CreateDbWithRow();
        DatabaseBackup.TakeSnapshot(_dbPath);

        File.WriteAllBytes(_dbPath, [9, 9, 9]);
        Assert.False(DatabaseBackup.IsHealthy(_dbPath));

        Assert.True(DatabaseBackup.TryRecoverFromSnapshot(_dbPath));

        var repo = new StationRepository(new DbConnectionFactory(_connString));
        Assert.NotNull(repo.GetByCallsignAndBand("TEST1", "15m"));
    }

    [Fact]
    public void TryRecoverFromSnapshot_returns_false_and_preserves_file_when_no_snapshot()
    {
        File.WriteAllBytes(_dbPath, [9, 9, 9]);

        Assert.False(DatabaseBackup.TryRecoverFromSnapshot(_dbPath));

        Assert.NotEmpty(Directory.GetFiles(_dir, "test.db.corrupt-*"));
    }

    [Fact]
    public async Task DatabaseBackupWorker_takes_snapshot_on_shutdown()
    {
        CreateDbWithRow();
        var worker = new DatabaseBackupWorker(
            NullLogger<DatabaseBackupWorker>.Instance,
            _dbPath,
            TimeSpan.FromHours(6));

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(File.Exists(DatabaseBackup.SnapshotPath(_dbPath)));
    }
}
