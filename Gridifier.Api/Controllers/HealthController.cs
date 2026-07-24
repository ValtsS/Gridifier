using Gridifier.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new HealthResponse());
    }
}