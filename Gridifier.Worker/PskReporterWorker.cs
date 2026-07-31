using System.Threading.Channels;
using Gridifier.Shared.Models;
using MQTTnet;

namespace Gridifier.Worker;

public class PskReporterWorker(
    ILogger<PskReporterWorker> logger,
    Channel<Station> stationChannel,
    MqttSettings settings,
    AppStats stats)
    : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan KeepaliveLog = TimeSpan.FromMinutes(5);
    private readonly MqttClientOptions _connOptions = new MqttClientOptionsBuilder()
        .WithTcpServer(settings.Host, settings.Port)
        .Build();

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
            stats.MqttConnected = false;
            stats.IncrementDisconnects();
            stats.LastDisconnectAt = DateTime.UtcNow;
            stats.LastDisconnectReason = args.Reason.ToString();
            stats.MessagesPerSecond = 0;
            logger.LogWarning("Disconnected (reason: {Reason}), will reconnect", args.Reason);
            return Task.CompletedTask;
        };

        logger.LogInformation("Connecting to {Host}:{Port}", settings.Host, settings.Port);
        await client.ConnectAsync(_connOptions, stoppingToken);
        stats.MqttConnected = true;
        stats.IncrementConnects();
        stats.LastConnectAt = DateTime.UtcNow;
        logger.LogInformation("Connected");

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(settings.Topic)
            .Build();

        await client.SubscribeAsync(subscribeOptions, stoppingToken);
        logger.LogInformation("Subscribed to {Topic}", settings.Topic);

        var handler = new PskMessageHandler(stationChannel, stats);
        var lastKeepalive = DateTime.UtcNow;
        var lastRateAt = DateTime.UtcNow;
        var lastRateCount = 0L;

        client.ApplicationMessageReceivedAsync += args =>
        {
            try
            {
                var text = args.ApplicationMessage.ConvertPayloadToString();
                handler.HandleMessage(text);
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
            var elapsed = now - lastRateAt;
            if (elapsed >= TimeSpan.FromSeconds(1))
            {
                stats.MessagesPerSecond = (stats.TotalMessagesReceived - lastRateCount) / elapsed.TotalSeconds;
                lastRateCount = stats.TotalMessagesReceived;
                lastRateAt = now;
            }

            if (now - lastKeepalive >= KeepaliveLog)
            {
                logger.LogInformation("Still listening - received {Total} messages so far", stats.TotalMessagesReceived);
                lastKeepalive = now;
            }

            if (!client.IsConnected)
            {
                logger.LogWarning("MQTT client disconnected, exiting session to reconnect");
                break;
            }
        }
    }
}