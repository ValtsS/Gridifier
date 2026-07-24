namespace Gridifier.Worker;

public class AppStats
{
    public bool MqttConnected { get; set; }
    public long TotalMessagesReceived => _messagesReceived;
    public long TotalWritten { get; set; }
    public long TotalSkipped { get; set; }
    public int CacheSize { get; set; }
    public long DatabaseCount { get; set; }
    public double MessagesPerSecond { get; set; }
    public DateTime Uptime { get; } = DateTime.UtcNow;

    private long _messagesReceived;

    public void IncrementMessages() => Interlocked.Increment(ref _messagesReceived);
}