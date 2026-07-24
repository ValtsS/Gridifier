using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using MQTTnet;

namespace Gridifier.Worker;

public class PskReporterWorker(
    ILogger<PskReporterWorker> logger,
    StationRepository repo,
    MqttSettings settings)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Connecting to {Host}:{Port}, subscribing to {Topic}",
            settings.Host, settings.Port, settings.Topic);

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        var connOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.Host, settings.Port)
            .Build();

        try
        {
            await client.ConnectAsync(connOptions, stoppingToken);
            logger.LogInformation("Connected");

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(settings.Topic)
                .Build();

            await client.SubscribeAsync(subscribeOptions, stoppingToken);
            logger.LogInformation("Subscribed to {Topic}", settings.Topic);

            var handler = new PskMessageHandler(repo, logger);

            client.ApplicationMessageReceivedAsync += args =>
            {
                try
                {
                    var text = args.ApplicationMessage.ConvertPayloadToString();
                    handler.HandleMessage(text);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message");
                }

                return Task.CompletedTask;
            };

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Stopping");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection error");
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync();
        }
    }
}