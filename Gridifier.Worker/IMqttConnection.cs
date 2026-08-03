namespace Gridifier.Worker;

// Transport abstraction so MQTT implementations (MQTTnet, Pulse.Mqtt, ...) are
// pluggable. The worker only knows this contract; it never imports an MQTT lib.
public interface IMqttConnection : IAsyncDisposable
{
    // True while a session is actively connected. May flap during reconnects.
    bool IsConnected { get; }

    // Raised once a session is fully connected (initial connect and each reconnect).
    event Action? Connected;

    // Raised when a session drops. Reason is a human-readable string, or null.
    event Action<string?>? Disconnected;

    // Raised for each application message; payload is the raw UTF-8 JSON body.
    event Action<ReadOnlyMemory<byte>>? MessageReceived;

    // Runs the connection for the lifetime of the token: connect, subscribe to
    // the given topic, pump messages, reconnect per the transport's policy.
    Task RunAsync(string topic, CancellationToken cancellationToken);
}
