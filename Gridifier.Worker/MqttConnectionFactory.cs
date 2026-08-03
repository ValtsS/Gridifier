namespace Gridifier.Worker;

// Creates MqttConnection instances from configured transport settings so the
// wiring (Program.cs) and the worker stay free of any concrete MQTT library.
public static class MqttConnectionFactory
{
    // Stable across reconnects per process; unique per process (random on start).
    internal static readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];

    // Creates a transport for a single subscription. Client IDs MUST be unique
    // per connection or the broker performs session takeover (kicks them off).
    public static IMqttConnection Create(
        int subscriptionIndex,
        string host,
        int port,
        string transport) =>
        Create($"{TransportPrefix}-{InstanceId}-{subscriptionIndex}", host, port, transport);

    public static IMqttConnection Create(
        string clientId,
        string host,
        int port,
        string transport)
    {
        return transport switch
        {
            "mqttnet" => new MqttnetConnection(clientId, host, port),
            "pulse" => new PulseMqttConnection(clientId, host, port),
            _ => throw new ArgumentOutOfRangeException(nameof(transport),
                $"Unknown MQTT transport '{transport}'")
        };
    }

    internal const string TransportPrefix = "gridifier";
}