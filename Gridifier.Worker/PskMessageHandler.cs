using System.Text.Json;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;

namespace Gridifier.Worker;

public class PskMessageHandler(StationRepository repo, ILogger logger)
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

            if (root.TryGetProperty("rc", out var receiverEl))
            {
                var callsign = receiverEl.GetString();
                var grid = root.TryGetProperty("rl", out var gridEl)
                    ? gridEl.GetString() ?? ""
                    : "";

                if (!string.IsNullOrWhiteSpace(callsign))
                {
                    callsign = callsign.Trim().ToUpperInvariant();
                    if (grid.Length > 16)
                        grid = grid[..16];

                    repo.Upsert(new Station { Callsign = callsign, Grid = grid });
                    logger.LogDebug("Upserted receiver {Callsign} with grid {Grid}", callsign, grid);
                }
            }
        }
    }
}