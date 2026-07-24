using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(AppStats stats) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            uptime = DateTime.UtcNow - stats.Uptime,
            mqttConnected = stats.MqttConnected,
            totalMessagesReceived = stats.TotalMessagesReceived,
            totalWritten = stats.TotalWritten,
            totalSkipped = stats.TotalSkipped,
            messagesPerSecond = Math.Round(stats.MessagesPerSecond, 1),
            cacheSize = stats.CacheSize,
            databaseCount = stats.DatabaseCount
        });
    }
}