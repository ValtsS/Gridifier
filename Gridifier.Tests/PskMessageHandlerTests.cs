using System.Threading.Channels;
using Gridifier.Shared.Models;
using Gridifier.Worker;

namespace Gridifier.Tests;

public class PskMessageHandlerTests
{
    private readonly Channel<Station> _channel;
    private readonly PskMessageHandler _handler;

    public PskMessageHandlerTests()
    {
        _channel = Channel.CreateUnbounded<Station>();
        _handler = new PskMessageHandler(_channel);
    }

    private List<Station> ReadAll()
    {
        var results = new List<Station>();
        while (_channel.Reader.TryRead(out var s))
            results.Add(s);
        return results;
    }

    [Fact]
    public void HandleMessage_queues_receiver()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Grid == "JO20AA");
    }

    [Fact]
    public void HandleMessage_queues_sender()
    {
        var json = """{"sc":"DL1ABC","sl":"JO20AA","rc":"OTHER"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Grid == "JO20AA");
    }

    [Fact]
    public void HandleMessage_queues_both_receiver_and_sender()
    {
        var json = """{"rc":"K4BYN","rl":"FM05QU","sc":"WT9Q","sl":"EN44GB"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Equal(2, stations.Count);
        Assert.Contains(stations, s => s.Callsign == "K4BYN" && s.Grid == "FM05QU");
        Assert.Contains(stations, s => s.Callsign == "WT9Q" && s.Grid == "EN44GB");
    }

    [Fact]
    public void HandleMessage_handles_missing_locator()
    {
        var json = """{"rc":"DL1ABC","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        var station = stations.Single(s => s.Callsign == "DL1ABC");
        Assert.Equal("", station.Grid);
    }

    [Fact]
    public void HandleMessage_skips_missing_receiver_and_sender()
    {
        var json = """{"rl":"JO20AA","sl":"EN44GB"}""";
        _handler.HandleMessage(json);

        Assert.Empty(ReadAll());
    }

    [Fact]
    public void HandleMessage_ignores_invalid_json()
    {
        _handler.HandleMessage("not json");
        Assert.Empty(ReadAll());
    }

    [Fact]
    public void HandleMessage_rejects_invalid_grid()
    {
        var json = """{"rc":"DL1ABC","rl":"ABCDEFGHIJKLMNOPQRSTUVWXYZ","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        var station = stations.Single(s => s.Callsign == "DL1ABC");
        Assert.Equal("", station.Grid);
    }

    [Fact]
    public void HandleMessage_normalizes_callsign_case()
    {
        var json = """{"rc":"dl1abc","rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC");
    }

    [Fact]
    public void HandleMessage_processes_full_psk_report()
    {
        var json = """{"sq":71033069835,"f":50313393,"md":"FT8","rp":-7,"t":1784910315,"t_tx":1784910300,"sc":"WT9Q","sl":"EN44GB","rc":"K4BYN","rl":"FM05QU","sa":291,"ra":291,"b":"6m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Equal(2, stations.Count);
        Assert.Contains(stations, s => s.Callsign == "K4BYN" && s.Grid == "FM05QU");
        Assert.Contains(stations, s => s.Callsign == "WT9Q" && s.Grid == "EN44GB");
    }

    [Fact]
    public void HandleMessage_queues_multiple_messages()
    {
        var json1 = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST"}""";
        var json2 = """{"rc":"DL1XYZ","rl":"JO30BB","sc":"TEST"}""";
        _handler.HandleMessage(json1);
        _handler.HandleMessage(json2);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC");
        Assert.Contains(stations, s => s.Callsign == "DL1XYZ");
    }
}