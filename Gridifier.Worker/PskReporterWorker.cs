using System.Threading.Channels;
using Gridifier.Shared.Models;
using MQTTnet;

namespace Gridifier.Worker;

public class PskReporterWorker(
    ILogger<PskReporterWorker> logger,
    Channel<Station> stationChannel,
    AppStats stats,
    int subscriptionIndex,
    string host,
    int port,
    string topic)
    : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan KeepaliveLog = TimeSpan.FromMinutes(5);
    // Unique per process (stable across reconnects); per-subscription suffix
    // prevents session takeover between the N workers sharing the broker.
    private static readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly MqttClientOptions _connOptions = new MqttClientOptionsBuilder()
        .WithClientId($"gridifier-{InstanceId}-{subscriptionIndex}")
        .WithTcpServer(host, port)
        .Build();

    private readonly string _host = host;
    private readonly int _port = port;
    private readonly string _topic = topic;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSession(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MQTT session crashed, reconnecting in {Delay}s", ReconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(ReconnectDelay, stoppingToken);
        }
    }

    private async Task RunSession(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        client.DisconnectedAsync += args =>
        {
            stats.SetSubscriptionConnected(subscriptionIndex, connected: false);
            stats.IncrementDisconnects();
            stats.LastDisconnectAt = DateTime.UtcNow;
            stats.LastDisconnectReason = args.Reason.ToString();
            logger.LogWarning("Disconnected ({Topic}, reason: {Reason}), will reconnect", _topic, args.Reason);
            return Task.CompletedTask;
        };

        logger.LogInformation("Connecting to {Host}:{Port} for {Topic}", _host, _port, _topic);
        await client.ConnectAsync(_connOptions, stoppingToken);
        stats.SetSubscriptionConnected(subscriptionIndex, connected: true, topic: _topic);
        stats.IncrementConnects();
        stats.LastConnectAt = DateTime.UtcNow;
        logger.LogInformation("Connected for {Topic}", _topic);

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(_topic)
            .Build();

        await client.SubscribeAsync(subscribeOptions, stoppingToken);
        logger.LogInformation("Subscribed to {Topic}", _topic);

        var handler = new PskMessageHandler(stationChannel, stats);
        var lastKeepalive = DateTime.UtcNow;

        client.ApplicationMessageReceivedAsync += args =>
        {
            try
            {
                var payload = args.ApplicationMessage.Payload;
                if (payload.IsSingleSegment)
                {
                    handler.HandleMessage(payload.FirstSpan);
                }
                else
                {
                    var bytes = new byte[payload.Length];
                    var writer = new System.Buffers.SequenceReader<byte>(payload);
                    writer.TryCopyTo(bytes);
                    handler.HandleMessage(bytes);
                }
                stats.IncrementMessages();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
            }

            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);

            var now = DateTime.UtcNow;

            if (now - lastKeepalive >= KeepaliveLog)
            {
                logger.LogInformation(
                    "Still listening ({Topic}) - received {Total} messages so far",
                    _topic, stats.TotalMessagesReceived);
                lastKeepalive = now;
            }

            if (!client.IsConnected)
            {
                logger.LogWarning("MQTT client disconnected ({Topic}), exiting session to reconnect", _topic);
                break;
            }
        }
    }
}
