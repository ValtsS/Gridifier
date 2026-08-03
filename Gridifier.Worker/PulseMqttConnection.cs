using Pulse.Mqtt;
using Pulse.Mqtt.Connection;
using Pulse.Mqtt.Packets;
using Pulse.Mqtt.Protocol;
using Pulse.Mqtt.Transport;

namespace Gridifier.Worker;

// Pulse.Mqtt transport adapter implementing IMqttConnection. Uses the raw client
// (single connection, no resilience wrapper) so reconnect handling stays identical
// to MqttnetConnection — isolating the comparison to the wire codec / allocations.
public sealed class PulseMqttConnection : IMqttConnection
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private readonly string _clientId;
    private readonly string _host;
    private readonly int _port;

    public bool IsConnected { get; private set; }

    public event Action? Connected;
    public event Action<string?>? Disconnected;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;

    public PulseMqttConnection(string clientId, string host, int port)
    {
        _clientId = clientId;
        _host = host;
        _port = port;
    }

    public async Task RunAsync(string topic, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunSession(topic, cancellationToken);
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

    private async Task RunSession(string topic, CancellationToken cancellationToken)
    {
        var transportFactory = new TcpTransportFactory(new TcpTransportOptions
        {
            Host = _host,
            Port = _port,
        });

        await using var client = new RawMqttClient(transportFactory, new RawMqttClientOptions
        {
            ConnAckTimeout = TimeSpan.FromSeconds(30),
        });

        var connect = new MqttConnectPacket
        {
            ClientId = _clientId,
            KeepAliveSeconds = 30,
        };

        var connAck = await client.ConnectAsync(connect, cancellationToken);
        if (connAck.ReasonCode != MqttReasonCode.Success)
            throw new InvalidOperationException($"MQTT connect rejected: {connAck.ReasonCode}");

        IsConnected = true;
        Connected?.Invoke();

        await client.SubscribeAsync([new MqttTopicFilter(topic)], cancellationToken);

        await foreach (var message in client.Messages.ReadAllAsync(cancellationToken))
        {
            MessageReceived?.Invoke(message.Payload);
        }
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}