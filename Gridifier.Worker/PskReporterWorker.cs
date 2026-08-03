using System.Diagnostics;
using System.Threading.Channels;
using Gridifier.Shared.Models;

namespace Gridifier.Worker;

public class PskReporterWorker(
    ILogger<PskReporterWorker> logger,
    IMqttConnection connection,
    Channel<Station> stationChannel,
    AppStats stats,
    int subscriptionIndex,
    string topic)
    : BackgroundService
{
    private readonly string _topic = topic;
    private PskMessageHandler? _handler;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _handler = new PskMessageHandler(stationChannel: stationChannel, stats);

        connection.Connected += OnConnected;
        connection.Disconnected += OnDisconnected;
        connection.MessageReceived += OnMessageReceived;

        try
        {
            await connection.RunAsync(_topic, stoppingToken);
        }
        finally
        {
            connection.Connected -= OnConnected;
            connection.Disconnected -= OnDisconnected;
            connection.MessageReceived -= OnMessageReceived;
        }
    }

    private void OnConnected()
    {
        stats.SetSubscriptionConnected(subscriptionIndex, connected: true, topic: _topic);
        stats.IncrementConnects();
        stats.LastConnectAt = DateTime.UtcNow;
        logger.LogInformation("Connected for {Topic}", _topic);
    }

    private void OnDisconnected(string? reason)
    {
        stats.SetSubscriptionConnected(subscriptionIndex, connected: false);
        stats.IncrementDisconnects();
        stats.LastDisconnectAt = DateTime.UtcNow;
        stats.LastDisconnectReason = reason;
        logger.LogWarning("Disconnected ({Topic}, reason: {Reason}), will reconnect", _topic, reason);
    }

    private void OnMessageReceived(ReadOnlyMemory<byte> payload)
    {
        if (_handler is null)
            return;

        var sw = Stopwatch.GetTimestamp();
        try
        {
            _handler.HandleMessage(payload.Span);
            stats.IncrementMessages();
            stats.RecordHandlerTime(
                (Stopwatch.GetTimestamp() - sw) * 1_000_000_000L / Stopwatch.Frequency);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message");
        }
    }
}
