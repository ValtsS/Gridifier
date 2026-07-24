using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gridifier.Tests;

public class PskMessageHandlerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly StationRepository _repo;
    private readonly PskMessageHandler _handler;

    public PskMessageHandlerTests()
    {
        _dbPath = Path.GetTempFileName();
        var factory = new DbConnectionFactory($"Data Source={_dbPath}");
        using (var conn = factory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        _repo = new StationRepository(factory);
        _handler = new PskMessageHandler(_repo, NullLogger.Instance);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { }
    }

    [Fact]
    public void HandleMessage_upserts_receiver()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var station = _repo.GetByCallsign("DL1ABC");
        Assert.NotNull(station);
        Assert.Equal("JO20AA", station.Grid);
    }

    [Fact]
    public void HandleMessage_handles_missing_locator()
    {
        var json = """{"rc":"DL1ABC","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var station = _repo.GetByCallsign("DL1ABC");
        Assert.NotNull(station);
        Assert.Equal("", station.Grid);
    }

    [Fact]
    public void HandleMessage_skips_missing_receiver()
    {
        var json = """{"rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void HandleMessage_ignores_invalid_json()
    {
        _handler.HandleMessage("not json");
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void HandleMessage_truncates_long_grid()
    {
        var json = """{"rc":"DL1ABC","rl":"ABCDEFGHIJKLMNOPQRSTUVWXYZ","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var station = _repo.GetByCallsign("DL1ABC");
        Assert.NotNull(station);
        Assert.Equal("", station.Grid);
    }

    [Fact]
    public void HandleMessage_normalizes_callsign_case()
    {
        var json = """{"rc":"dl1abc","rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var station = _repo.GetByCallsign("DL1ABC");
        Assert.NotNull(station);
    }

    [Fact]
    public void HandleMessage_processes_full_psk_report()
    {
        var json = """{"sq":71033069835,"f":50313393,"md":"FT8","rp":-7,"t":1784910315,"t_tx":1784910300,"sc":"WT9Q","sl":"EN44GB","rc":"K4BYN","rl":"FM05QU","sa":291,"ra":291,"b":"6m"}""";
        _handler.HandleMessage(json);

        var station = _repo.GetByCallsign("K4BYN");
        Assert.NotNull(station);
        Assert.Equal("FM05QU", station.Grid);
    }

    [Fact]
    public void HandleMessage_upserts_each_receiver_in_batch()
    {
        var json1 = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST"}""";
        var json2 = """{"rc":"DL1XYZ","rl":"JO30BB","sc":"TEST"}""";
        _handler.HandleMessage(json1);
        _handler.HandleMessage(json2);

        Assert.Equal(2, _repo.GetAll().Count());
        Assert.NotNull(_repo.GetByCallsign("DL1ABC"));
        Assert.NotNull(_repo.GetByCallsign("DL1XYZ"));
    }
}