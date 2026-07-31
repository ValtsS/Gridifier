using Gridifier.Shared.Validation;
using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/v1/grid/{band}/{*callsign}")]
public class StationsController(StationCache cache) : ControllerBase
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

        if (cache.TryGet(normalized, band, out var grid, out var lastHeard))
            return Ok(new { g = grid, t = lastHeard });

        return NotFound();
    }
}
