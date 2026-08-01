using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc;

namespace Gridifier.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(AppStats stats, IConfiguration config) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        if (!config.GetValue("Stats:Enabled", false))
            return NotFound();

        return Ok(new
        {
            uptime = DateTime.UtcNow - stats.Uptime,
            mqttConnected = stats.MqttConnected,
            connectedSubscriptions = stats.ConnectedSubscriptions,
            subscriptions = stats.Subscriptions.Select(s => new
            {
                topic = s.Topic,
                connected = s.Connected,
                lastConnectAt = s.LastConnectAt,
                lastDisconnectAt = s.LastDisconnectAt,
                lastDisconnectReason = s.LastDisconnectReason
            }),
            totalMessagesReceived = stats.TotalMessagesReceived,
            messagesPerSecond = Math.Round(stats.MessagesPerSecond, 1),
            droppedMessages = stats.DroppedMessages,
            totalWritten = stats.TotalWritten,
            cacheSize = stats.CacheSize,
            activeStations = stats.ActiveStations,
            stationsByBand = stats.StationsByBand,
            databaseCount = stats.DatabaseCount,
            totalConnects = stats.TotalConnects,
            totalDisconnects = stats.TotalDisconnects,
            lastConnectAt = stats.LastConnectAt,
            lastDisconnectAt = stats.LastDisconnectAt,
            lastDisconnectReason = stats.LastDisconnectReason,
            lastSweepAt = stats.LastSweepAt,
            lastSweepPersisted = stats.LastSweepPersisted
        });
    }
}
