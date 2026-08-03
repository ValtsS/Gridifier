using System.Diagnostics;
using MQTTnet;

namespace Gridifier.Worker;

// MQTTnet transport adapter implementing IMqttConnection. Owns the connect,
// subscribe, and reconnect loop.
public sealed class MqttnetConnection : IMqttConnection
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    private readonly string _clientId;
    private readonly string _host;
    private readonly int _port;

    public bool IsConnected { get; private set; }

    public event Action? Connected;
    public event Action<string?>? Disconnected;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;

    public MqttnetConnection(string clientId, string host, int port)
    {
        _clientId = clientId;
        _host = host;
        _port = port;
    }

    private MqttClientOptions ConnectionOptions(string clientId)
        => new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_host, _port)
            .Build();

    public async Task RunAsync(string topic, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunSession(topic, ConnectionOptions(_clientId), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Disconnected?.Invoke(ex.Message);
            }

            if (!cancellationToken.IsCancellationRequested)
                await Task.Delay(ReconnectDelay, cancellationToken);
        }
    }

    private async Task RunSession(string topic, MqttClientOptions options, CancellationToken cancellationToken)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        client.DisconnectedAsync += args =>
        {
            IsConnected = false;
            Disconnected?.Invoke(args.Reason.ToString());
            return Task.CompletedTask;
        };

        await client.ConnectAsync(options, cancellationToken);
        IsConnected = true;
        Connected?.Invoke();

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();

        await client.SubscribeAsync(subscribeOptions, cancellationToken);

        client.ApplicationMessageReceivedAsync += args =>
        {
            var payload = args.ApplicationMessage.Payload;
            ReadOnlyMemory<byte> memory;
            if (payload.IsSingleSegment)
            {
                memory = payload.First;
            }
            else
            {
                var bytes = new byte[payload.Length];
                var writer = new System.Buffers.SequenceReader<byte>(payload);
                writer.TryCopyTo(bytes);
                memory = bytes;
            }
            MessageReceived?.Invoke(memory);
            return Task.CompletedTask;
        };

        // Keep the session alive; exit only when the token fires or we disconnect.
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
            if (!client.IsConnected)
            {
                IsConnected = false;
                break;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}