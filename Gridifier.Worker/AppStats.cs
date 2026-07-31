namespace Gridifier.Worker;

public class AppStats
{
    public bool MqttConnected { get; set; }
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

    private long _messagesReceived;
    private long _dropped;
    private long _connects;
    private long _disconnects;

    public void IncrementMessages() => Interlocked.Increment(ref _messagesReceived);

    public void IncrementDropped() => Interlocked.Increment(ref _dropped);

    public void IncrementConnects() => Interlocked.Increment(ref _connects);

    public void IncrementDisconnects() => Interlocked.Increment(ref _disconnects);
}
