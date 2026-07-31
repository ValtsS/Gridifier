using Gridifier.Shared.Data;
using Gridifier.Shared.Models;

namespace Gridifier.Tests;

public class StationRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbConnectionFactory _factory;
    private readonly StationRepository _repo;

    public StationRepositoryTests()
    {
        _dbPath = Path.GetTempFileName();
        _factory = new DbConnectionFactory($"Data Source={_dbPath}");
        using var conn = _factory.CreateConnection();
        DatabaseInitializer.Initialize(conn);
        _repo = new StationRepository(_factory);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void Upsert_creates_new_record()
    {
        var station = new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 1_784_910_000 };
        _repo.Upsert(station);

        var result = _repo.GetByCallsignAndBand("TEST1", "15m");
        Assert.NotNull(result);
        Assert.Equal("TEST1", result.Callsign);
        Assert.Equal("15m", result.Band);
        Assert.Equal("JO20AA", result.Grid);
    }

    [Fact]
    public void Upsert_updates_existing_record()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 });
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB", LastUpdate = 2_000 });

        var result = _repo.GetByCallsignAndBand("TEST1", "15m");
        Assert.NotNull(result);
        Assert.Equal("JO30BB", result.Grid);
    }

    [Fact]
    public void Upsert_does_not_update_with_stale_timestamp()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 2_000 });
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB", LastUpdate = 1_000 });

        var result = _repo.GetByCallsignAndBand("TEST1", "15m");
        Assert.NotNull(result);
        Assert.Equal("JO20AA", result.Grid);
    }

    [Fact]
    public void Upsert_updates_last_update_timestamp()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 });
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 2_000 });

        var second = _repo.GetByCallsignAndBand("TEST1", "15m")!;
        Assert.Equal(2_000, second.LastUpdate);
    }

    [Fact]
    public void GetByCallsign_returns_null_when_not_found()
    {
        var result = _repo.GetByCallsignAndBand("NONEXISTENT", "15m");
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_returns_all_stations_ordered_by_callsign()
    {
        _repo.Upsert(new Station { Callsign = "BETA", Band = "15m", Grid = "JO10AA", LastUpdate = 1_000 });
        _repo.Upsert(new Station { Callsign = "ALPHA", Band = "20m", Grid = "JO20BB", LastUpdate = 1_000 });
        _repo.Upsert(new Station { Callsign = "GAMMA", Band = "15m", Grid = "JO30CC", LastUpdate = 1_000 });

        var all = _repo.GetAll().ToList();

        Assert.Equal(3, all.Count);
        Assert.Equal("ALPHA", all[0].Callsign);
        Assert.Equal("BETA", all[1].Callsign);
        Assert.Equal("GAMMA", all[2].Callsign);
    }

    [Fact]
    public void GetAll_returns_empty_when_no_records()
    {
        var all = _repo.GetAll().ToList();
        Assert.Empty(all);
    }

    [Fact]
    public void Same_callsign_different_bands_are_separate()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 });
        _repo.Upsert(new Station { Callsign = "TEST1", Band = "20m", Grid = "JO30BB", LastUpdate = 1_000 });

        var r1 = _repo.GetByCallsignAndBand("TEST1", "15m");
        var r2 = _repo.GetByCallsignAndBand("TEST1", "20m");
        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.Equal("JO20AA", r1.Grid);
        Assert.Equal("JO30BB", r2.Grid);
    }
}

public class DbConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_sets_WAL_mode()
    {
        var dbPath = Path.GetTempFileName();
        try
        {
            using var conn = new DbConnectionFactory($"Data Source={dbPath}").CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            var result = cmd.ExecuteScalar()!.ToString();
            Assert.Equal("wal", result);
        }
        finally
        {
            try { File.Delete(dbPath); }
            catch { }
        }
    }

    [Fact]
    public void CreateConnection_can_open_multiple_times()
    {
        var factory = new DbConnectionFactory("Data Source=:memory:");
        using var conn1 = factory.CreateConnection();
        using var conn2 = factory.CreateConnection();
        Assert.True(conn1.State == System.Data.ConnectionState.Open);
        Assert.True(conn2.State == System.Data.ConnectionState.Open);
    }
}

public class DatabaseInitializerTests
{
    [Fact]
    public void Initialize_creates_stations_table()
    {
        using var conn = new DbConnectionFactory("Data Source=:memory:").CreateConnection();
        DatabaseInitializer.Initialize(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='stations';";
        var result = cmd.ExecuteScalar()!.ToString();
        Assert.Equal("stations", result);
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        using var conn = new DbConnectionFactory("Data Source=:memory:").CreateConnection();
        DatabaseInitializer.Initialize(conn);
        DatabaseInitializer.Initialize(conn);
        DatabaseInitializer.Initialize(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='stations';";
        var count = Convert.ToInt32(cmd.ExecuteScalar()!);
        Assert.Equal(1, count);
    }
}