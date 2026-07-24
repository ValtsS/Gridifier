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
        var station = new Station { Callsign = "TEST1", Grid = "JO20AA" };
        _repo.Upsert(station);

        var result = _repo.GetByCallsign("TEST1");
        Assert.NotNull(result);
        Assert.Equal("TEST1", result.Callsign);
        Assert.Equal("JO20AA", result.Grid);
    }

    [Fact]
    public void Upsert_updates_existing_record()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Grid = "JO20AA" });
        _repo.Upsert(new Station { Callsign = "TEST1", Grid = "JO30BB" });

        var result = _repo.GetByCallsign("TEST1");
        Assert.NotNull(result);
        Assert.Equal("JO30BB", result.Grid);
    }

    [Fact]
    public void Upsert_updates_last_update_timestamp()
    {
        _repo.Upsert(new Station { Callsign = "TEST1", Grid = "JO20AA" });
        var first = _repo.GetByCallsign("TEST1")!;
        var firstUpdate = first.LastUpdate;

        Thread.Sleep(1500);
        _repo.Upsert(new Station { Callsign = "TEST1", Grid = "JO20AA" });
        var second = _repo.GetByCallsign("TEST1")!;

        Assert.True(second.LastUpdate > firstUpdate);
    }

    [Fact]
    public void GetByCallsign_returns_null_when_not_found()
    {
        var result = _repo.GetByCallsign("NONEXISTENT");
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_returns_all_stations_ordered_by_callsign()
    {
        _repo.Upsert(new Station { Callsign = "BETA", Grid = "JO10AA" });
        _repo.Upsert(new Station { Callsign = "ALPHA", Grid = "JO20BB" });
        _repo.Upsert(new Station { Callsign = "GAMMA", Grid = "JO30CC" });

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