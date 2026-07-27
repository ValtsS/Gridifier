using Gridifier.Shared.Data;
using Gridifier.Shared.Validation;
using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/v1/grid/{band}/{*callsign}")]
public class StationsController(StationRepository repo, StationCache cache) : ControllerBase
{
    [HttpGet]
    public IActionResult Get(string band, string callsign)
    {
        band = BandValidator.Normalize(band);
        if (!BandValidator.IsValid(band))
            return BadRequest("Invalid band");

        var normalized = CallsignValidator.Normalize(callsign);

        if (!CallsignValidator.IsValid(normalized))
            return BadRequest("Invalid callsign");

        if (cache.TryGet(normalized, band, out var grid, out var lastUpdate))
            return Ok(new { g = GridValidator.Shorten(grid!), t = new DateTimeOffset(lastUpdate!.Value).ToUnixTimeSeconds() });

        var station = repo.GetByCallsignAndBand(normalized, band);
        if (station is null)
            return NotFound();

        return Ok(new { g = GridValidator.Shorten(station.Grid), t = new DateTimeOffset(station.LastUpdate).ToUnixTimeSeconds() });
    }
}