using Gridifier.Shared.Data;
using Gridifier.Shared.Validation;
using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/stations")]
public class StationsController(StationRepository repo, StationCache cache) : ControllerBase
{
    [HttpGet("{*callsign}")]
    public IActionResult Get(string callsign)
    {
        var normalized = CallsignValidator.Normalize(callsign);

        if (!CallsignValidator.IsValid(normalized))
            return BadRequest("Invalid callsign");

        if (cache.TryGet(normalized, out var grid, out var lastUpdate))
            return Ok(new { Grid = GridValidator.Shorten(grid!), LastUpdate = lastUpdate!.Value });

        var station = repo.GetByCallsign(normalized);
        if (station is null)
            return NotFound();

        return Ok(new { Grid = GridValidator.Shorten(station.Grid), station.LastUpdate });
    }
}