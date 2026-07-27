using System.Text.Json;
using System.Threading.Channels;
using Gridifier.Shared.Models;
using Gridifier.Shared.Validation;

namespace Gridifier.Worker;

public class PskMessageHandler(Channel<Station> stationChannel)
{
    public void HandleMessage(string payload)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;

            var band = root.TryGetProperty("b", out var bEl)
                ? BandValidator.Normalize(bEl.GetString() ?? "")
                : "";

            if (!BandValidator.IsValid(band))
                return;

            if (root.TryGetProperty("rc", out var rcEl))
                TryQueue(rcEl, root, "rl", band);

            if (root.TryGetProperty("sc", out var scEl))
                TryQueue(scEl, root, "sl", band);
        }
    }

    private void TryQueue(JsonElement callsignEl, JsonElement root, string gridKey, string band)
    {
        var callsign = callsignEl.GetString();
        if (string.IsNullOrWhiteSpace(callsign))
            return;

        callsign = CallsignValidator.Normalize(callsign);

        var grid = root.TryGetProperty(gridKey, out var gridEl)
            ? GridValidator.Normalize(gridEl.GetString() ?? "")
            : "";

        if (!GridValidator.IsValid(grid))
            return;

        grid = GridValidator.Shorten(grid);

        stationChannel.Writer.TryWrite(new Station { Callsign = callsign, Band = band, Grid = grid });
    }
}