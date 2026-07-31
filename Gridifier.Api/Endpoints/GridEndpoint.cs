using Gridifier.Shared.Validation;
using Gridifier.Worker;

namespace Gridifier.Api.Endpoints;

public static class GridEndpoint
{
    public static IResult Get(StationCache cache, string band, string callsign)
    {
        band = BandValidator.Normalize(band);
        if (!BandValidator.IsValid(band))
            return Results.BadRequest("Invalid band");

        var normalized = CallsignValidator.Normalize(callsign);

        if (!CallsignValidator.IsValid(normalized))
            return Results.BadRequest("Invalid callsign");

        if (cache.TryGet(normalized, band, out var grid, out var lastHeard))
            return Results.Text($"{{\"g\":\"{grid}\",\"t\":{lastHeard}}}", "application/json");

        return Results.NotFound();
    }
}
