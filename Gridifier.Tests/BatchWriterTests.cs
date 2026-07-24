using System.Threading.Channels;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gridifier.Tests;

public class StationRepositoryBatchTests : IDisposable
{
    private readonly string _dbPath;
    private readonly StationRepository _repo;

    public StationRepositoryBatchTests()
    {
        _dbPath = Path.GetTempFileName();
        var factory = new DbConnectionFactory($"Data Source={_dbPath}");
        using (var conn = factory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        _repo = new StationRepository(factory);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { }
    }

    [Fact]
    public void UpsertMany_inserts_multiple()
    {
        var stations = new List<Station>
        {
            new() { Callsign = "ALPHA", Grid = "JO20AA" },
            new() { Callsign = "BETA", Grid = "JO30BB" },
        };

        _repo.UpsertMany(stations);

        Assert.NotNull(_repo.GetByCallsign("ALPHA"));
        Assert.NotNull(_repo.GetByCallsign("BETA"));
    }

    [Fact]
    public void UpsertMany_updates_existing()
    {
        _repo.Upsert(new Station { Callsign = "ALPHA", Grid = "JO20AA" });

        _repo.UpsertMany(new List<Station>
        {
            new() { Callsign = "ALPHA", Grid = "JO99ZZ" },
            new() { Callsign = "BETA", Grid = "JO30BB" },
        });

        var alpha = _repo.GetByCallsign("ALPHA");
        Assert.NotNull(alpha);
        Assert.Equal("JO99ZZ", alpha.Grid);
    }

    [Fact]
    public void UpsertMany_handles_empty()
    {
        _repo.UpsertMany([]);
        Assert.Empty(_repo.GetAll());
    }
}

public class DatabaseWriterTests : IDisposable
{
    private readonly string _dbPath;
    private readonly StationRepository _repo;
    private readonly Channel<Station> _channel;
    private readonly StationCache _cache;

    public DatabaseWriterTests()
    {
        _dbPath = Path.GetTempFileName();
        var factory = new DbConnectionFactory($"Data Source={_dbPath}");
        using (var conn = factory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        _repo = new StationRepository(factory);
        _channel = Channel.CreateBounded<Station>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _cache = new StationCache();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { }
    }

    [Fact]
    public async Task DatabaseWriter_flushes_on_batch_size()
    {
        var writer = new DatabaseWriter(NullLogger<DatabaseWriter>.Instance, _channel, _repo, _cache);

        _ = writer.StartAsync(CancellationToken.None);

        for (int i = 0; i < 100; i++)
        {
            _channel.Writer.TryWrite(new Station { Callsign = $"TEST{i}", Grid = "JO20AA" });
        }

        await Task.Delay(200);

        var all = _repo.GetAll().ToList();
        Assert.Equal(100, all.Count);

        await writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseWriter_flushes_on_interval()
    {
        var writer = new DatabaseWriter(NullLogger<DatabaseWriter>.Instance, _channel, _repo, _cache);

        _ = writer.StartAsync(CancellationToken.None);

        _channel.Writer.TryWrite(new Station { Callsign = "TEST1", Grid = "JO20AA" });

        await Task.Delay(1500);

        Assert.NotNull(_repo.GetByCallsign("TEST1"));

        await writer.StopAsync(CancellationToken.None);
    }
}