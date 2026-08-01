namespace Gridifier.Worker;

public class AppStats
{
    public bool MqttConnected => ConnectedSubscriptions > 0;
    public long TotalMessagesReceived => _messagesReceived;
    public long TotalWritten { get; set; }
    public int CacheSize { get; set; }
    public long DatabaseCount { get; set; }
    public double MessagesPerSecond { get; set; }
    public DateTime? LastSweepAt { get; set; }
    public long LastSweepPersisted { get; set; }
    public DateTime Uptime { get; } = DateTime.UtcNow;

    public long TotalConnects => _connects;
    public long TotalDisconnects => _disconnects;
    public DateTime? LastConnectAt { get; set; }
    public DateTime? LastDisconnectAt { get; set; }
    public string? LastDisconnectReason { get; set; }

    public long ActiveStations { get; set; }
    public IReadOnlyDictionary<string, long> StationsByBand { get; set; } =
        new Dictionary<string, long>();

    public long DroppedMessages => _dropped;

    public int ConnectedSubscriptions => _connectedSubscriptions;
    public SubscriptionStatus[] Subscriptions { get; } = new SubscriptionStatus[0];

    private long _messagesReceived;
    private long _dropped;
    private long _connects;
    private long _disconnects;
    private int _connectedSubscriptions;

    public AppStats(int subscriptionCount = 0)
    {
        Subscriptions = new SubscriptionStatus[subscriptionCount];
        for (var i = 0; i < subscriptionCount; i++)
            Subscriptions[i] = new SubscriptionStatus();
    }

    public void IncrementMessages() => Interlocked.Increment(ref _messagesReceived);

    public void IncrementDropped() => Interlocked.Increment(ref _dropped);

    public void IncrementConnects() => Interlocked.Increment(ref _connects);

    public void IncrementDisconnects() => Interlocked.Increment(ref _disconnects);

    public void SetSubscriptionConnected(int index, bool connected, string? topic = null)
    {
        if (index < 0 || index >= Subscriptions.Length)
            return;

        var status = Subscriptions[index];
        if (topic is not null)
            status.Topic = topic;

        status.Connected = connected;

        if (connected)
        {
            Interlocked.Increment(ref _connectedSubscriptions);
            status.LastConnectAt = DateTime.UtcNow;
        }
        else
        {
            Interlocked.Decrement(ref _connectedSubscriptions);
            status.LastDisconnectAt = DateTime.UtcNow;
        }
    }
}

public class SubscriptionStatus
{
    public string Topic { get; set; } = "";
    public bool Connected { get; set; }
    public DateTime? LastConnectAt { get; set; }
    public DateTime? LastDisconnectAt { get; set; }
    public string? LastDisconnectReason { get; set; }
}
