using System.Threading.Channels;
using Gridifier.Shared.Models;
using Gridifier.Worker;

namespace Gridifier.Tests;

public class PskMessageHandlerTests
{
    private readonly Channel<Station> _channel;
    private readonly PskMessageHandler _handler;
    private readonly AppStats _stats;

    public PskMessageHandlerTests()
    {
        _channel = Channel.CreateUnbounded<Station>();
        _stats = new AppStats();
        _handler = new PskMessageHandler(_channel, _stats);
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
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Grid == "JO20" && s.Band == "15m");
    }

    [Fact]
    public void HandleMessage_queues_sender()
    {
        var json = """{"sc":"DL1ABC","sl":"JO20AA","rc":"OTHER","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Grid == "JO20" && s.Band == "15m");
    }

    [Fact]
    public void HandleMessage_queues_both_receiver_and_sender()
    {
        var json = """{"rc":"K4BYN","rl":"FM05QU","sc":"WT9Q","sl":"EN44GB","b":"20m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Equal(2, stations.Count);
        Assert.Contains(stations, s => s.Callsign == "K4BYN" && s.Grid == "FM05" && s.Band == "20m");
        Assert.Contains(stations, s => s.Callsign == "WT9Q" && s.Grid == "EN44" && s.Band == "20m");
    }

    [Fact]
    public void HandleMessage_skips_missing_locator()
    {
        var json = """{"rc":"DL1ABC","sc":"TEST","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.DoesNotContain(stations, s => s.Callsign == "DL1ABC");
    }

    [Fact]
    public void HandleMessage_skips_missing_receiver_and_sender()
    {
        var json = """{"rl":"JO20AA","sl":"EN44GB","b":"15m"}""";
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
    public void HandleMessage_skips_invalid_grid()
    {
        var json = """{"rc":"DL1ABC","rl":"ZZ99AA","sc":"TEST","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.DoesNotContain(stations, s => s.Callsign == "DL1ABC");
    }

    [Fact]
    public void HandleMessage_truncates_grid_to_4_chars()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA88","sc":"TEST","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Grid == "JO20" && s.Band == "15m");
    }

    [Fact]
    public void HandleMessage_normalizes_callsign_case()
    {
        var json = """{"rc":"dl1abc","rl":"JO20AA","sc":"TEST","b":"15m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Band == "15m");
    }

    [Fact]
    public void HandleMessage_processes_full_psk_report()
    {
        var json = """{"sq":71033069835,"f":50313393,"md":"FT8","rp":-7,"t":1784910315,"t_tx":1784910300,"sc":"WT9Q","sl":"EN44GB","rc":"K4BYN","rl":"FM05QU","sa":291,"ra":291,"b":"6m"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Equal(2, stations.Count);
        Assert.Contains(stations, s => s.Callsign == "K4BYN" && s.Grid == "FM05" && s.Band == "6m");
        Assert.Contains(stations, s => s.Callsign == "WT9Q" && s.Grid == "EN44" && s.Band == "6m");
    }

    [Fact]
    public void HandleMessage_queues_multiple_messages()
    {
        var json1 = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST","b":"15m"}""";
        var json2 = """{"rc":"DL1XYZ","rl":"JO30BB","sc":"TEST","b":"20m"}""";
        _handler.HandleMessage(json1);
        _handler.HandleMessage(json2);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC");
        Assert.Contains(stations, s => s.Callsign == "DL1XYZ");
    }

    [Fact]
    public void HandleMessage_skips_missing_band()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST"}""";
        _handler.HandleMessage(json);

        Assert.Empty(ReadAll());
    }

    [Fact]
    public void HandleMessage_skips_invalid_band()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST","b":"invalid"}""";
        _handler.HandleMessage(json);

        Assert.Empty(ReadAll());
    }

    [Fact]
    public void HandleMessage_normalizes_band_case()
    {
        var json = """{"rc":"DL1ABC","rl":"JO20AA","sc":"TEST","b":"15M"}""";
        _handler.HandleMessage(json);

        var stations = ReadAll();
        Assert.Contains(stations, s => s.Callsign == "DL1ABC" && s.Band == "15m");
    }

    [Fact]
    public void HandleMessage_counts_dropped_when_channel_full()
    {
        var bounded = Channel.CreateBounded<Station>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var handler = new PskMessageHandler(bounded, _stats, channelCapacity: 2);

        for (int i = 0; i < 10; i++)
            handler.HandleMessage($$"""{"rc":"TEST{{i}}","rl":"JO20AA","b":"15m"}""");

        Assert.Equal(8, _stats.DroppedMessages);
    }

    [Fact]
    public void HandleMessage_does_not_count_drops_when_not_full()
    {
        var bounded = Channel.CreateBounded<Station>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var handler = new PskMessageHandler(bounded, _stats, channelCapacity: 10);

        handler.HandleMessage("""{"rc":"TEST1","rl":"JO20AA","b":"15m"}""");

        Assert.Equal(0, _stats.DroppedMessages);
    }
}