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
            new() { Callsign = "ALPHA", Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 },
            new() { Callsign = "BETA", Band = "20m", Grid = "JO30BB", LastUpdate = 1_000 },
        };

        _repo.UpsertMany(stations);

        Assert.NotNull(_repo.GetByCallsignAndBand("ALPHA", "15m"));
        Assert.NotNull(_repo.GetByCallsignAndBand("BETA", "20m"));
    }

    [Fact]
    public void UpsertMany_updates_existing()
    {
        _repo.Upsert(new Station { Callsign = "ALPHA", Band = "15m", Grid = "JO20AA", LastUpdate = 1_000 });

        _repo.UpsertMany(new List<Station>
        {
            new() { Callsign = "ALPHA", Band = "15m", Grid = "JO99ZZ", LastUpdate = 2_000 },
            new() { Callsign = "BETA", Band = "20m", Grid = "JO30BB", LastUpdate = 2_000 },
        });

        var alpha = _repo.GetByCallsignAndBand("ALPHA", "15m");
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
    private readonly AppStats _stats;
    private readonly DatabaseWriter _writer;

    public DatabaseWriterTests()
    {
        _dbPath = Path.GetTempFileName();
        var factory = new DbConnectionFactory($"Data Source={_dbPath}");
        using (var conn = factory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        _repo = new StationRepository(factory);
        _channel = Channel.CreateBounded<Station>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _cache = new StationCache();
        _stats = new AppStats();
        _writer = new DatabaseWriter(NullLogger<DatabaseWriter>.Instance, _channel, _repo, _cache, _stats);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { }
    }

    private async Task StartWriter()
    {
        _ = _writer.StartAsync(CancellationToken.None);
        await Task.Yield();
    }

    private async Task WaitUntil(Func<bool> predicate, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(25);
        }
        Assert.Fail($"Condition not met within {timeoutMs}ms");
    }

    [Fact]
    public async Task DatabaseWriter_persists_new_stations()
    {
        await StartWriter();

        for (int i = 0; i < 100; i++)
        {
            _channel.Writer.TryWrite(new Station { Callsign = $"TEST{i}", Band = "15m", Grid = "JO20AA" });
        }

        await WaitUntil(() => _repo.Count() == 100);
        Assert.All(Enumerable.Range(0, 100), i =>
            Assert.NotNull(_repo.GetByCallsignAndBand($"TEST{i}", "15m")));

        await _writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseWriter_dedups_repeated_reports_of_same_grid()
    {
        await StartWriter();

        for (int i = 0; i < 50; i++)
        {
            _channel.Writer.TryWrite(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        }

        await WaitUntil(() => _repo.Count() == 1);
        Assert.True(_cache.TryGet("TEST1", "15m", out var grid, out _));
        Assert.Equal("JO20", grid);

        await _writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseWriter_persists_grid_change()
    {
        await StartWriter();

        _channel.Writer.TryWrite(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        await WaitUntil(() => _repo.GetByCallsignAndBand("TEST1", "15m") != null);

        await Task.Delay(1100); // advance past the 1s timestamp resolution

        _channel.Writer.TryWrite(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB" });

        await WaitUntil(() => _repo.GetByCallsignAndBand("TEST1", "15m")?.Grid == "JO30BB");

        await _writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseWriter_writer_updates_cache_even_when_no_db_write()
    {
        await StartWriter();

        _channel.Writer.TryWrite(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        await WaitUntil(() => _repo.Count() == 1);

        Assert.True(_cache.TryGet("TEST1", "15m", out var grid, out var lastHeard));
        Assert.Equal("JO20", grid);
        Assert.True(lastHeard > 0);

        await _writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseWriter_flushes_channel_on_shutdown()
    {
        await StartWriter();

        for (int i = 0; i < 10; i++)
        {
            _channel.Writer.TryWrite(new Station { Callsign = $"TEST{i}", Band = "15m", Grid = "JO20AA" });
        }

        await _writer.StopAsync(CancellationToken.None);

        await WaitUntil(() => _repo.Count() == 10);
    }
}