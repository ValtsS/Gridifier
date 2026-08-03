using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Gridifier.Shared.Models;
using Gridifier.Shared.Validation;

namespace Gridifier.Worker;

public class PskMessageHandler
{
    public const int StationChannelCapacity = 10_000;

    private readonly Channel<Station> _stationChannel;
    private readonly AppStats _stats;
    private readonly int _channelCapacity;

    public PskMessageHandler(
        Channel<Station> stationChannel,
        AppStats stats,
        int channelCapacity = StationChannelCapacity)
    {
        _stationChannel = stationChannel;
        _stats = stats;
        _channelCapacity = channelCapacity;
    }

    public void HandleMessage(string payload)
        => HandleMessage(Encoding.UTF8.GetBytes(payload));

    public void HandleMessage(ReadOnlySpan<byte> payload)
    {
        var reader = new Utf8JsonReader(payload);
        string? receiver = null;
        string? receiverGrid = null;
        string? sender = null;
        string? senderGrid = null;
        string? band = null;

        try
        {
            // Streaming scan, no JsonDocument DOM allocation. Only the fields we
            // need are read; everything else (sq, f, md, rp, sa, ...) is skipped.
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var prop = reader.ValueSpan;
                if (!reader.Read())
                    break;

                if (prop.Length == 1 && prop[0] == (byte)'b')
                {
                    band = reader.GetString();
                }
                else if (prop.Length == 2 && prop[0] == (byte)'r')
                {
                    if (prop[1] == (byte)'c') receiver = reader.GetString();
                    else if (prop[1] == (byte)'l') receiverGrid = reader.GetString();
                }
                else if (prop.Length == 2 && prop[0] == (byte)'s')
                {
                    if (prop[1] == (byte)'c') sender = reader.GetString();
                    else if (prop[1] == (byte)'l') senderGrid = reader.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return;
        }

        if (band is null)
            return;

        band = BandValidator.Normalize(band);
        if (!BandValidator.IsValid(band))
            return;

        if (receiver is not null)
            TryQueue(receiver, receiverGrid, band);

        if (sender is not null)
            TryQueue(sender, senderGrid, band);
    }

    private void TryQueue(string callsign, string? grid, string band)
    {
        callsign = CallsignValidator.Normalize(callsign);
        if (callsign.Length == 0)
            return;

        if (grid is null || !GridValidator.IsValid(grid))
            return;

        grid = GridValidator.Shorten(GridValidator.Normalize(grid));

        if (_stationChannel.Reader.CanCount && _stationChannel.Reader.Count >= _channelCapacity)
            _stats.IncrementDropped();

        _stationChannel.Writer.TryWrite(new Station { Callsign = callsign, Band = band, Grid = grid });
    }
}
