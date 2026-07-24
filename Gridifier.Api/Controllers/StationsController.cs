using Gridifier.Shared.Data;
using Gridifier.Shared.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/stations")]
public class StationsController(StationRepository repo) : ControllerBase
{
    [HttpGet("{*callsign}")]
    public IActionResult Get(string callsign)
    {
        var normalized = CallsignValidator.Normalize(callsign);

        if (!CallsignValidator.IsValid(normalized))
            return BadRequest("Invalid callsign");

        var station = repo.GetByCallsign(normalized);
        if (station is null)
            return NotFound();

        return Ok(new { Grid = GridValidator.Shorten(station.Grid), station.LastUpdate });
    }
}