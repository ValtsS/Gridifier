namespace Gridifier.Shared.Models;

public class MqttSettings
{
    public string Host { get; set; } = "mqtt.pskreporter.info";
    public int Port { get; set; } = 1883;
    public List<MqttSubscription> Subscriptions { get; set; } = new();

    // Resolves the effective connections: each subscription inherits the
    // default Host/Port unless it overrides them. Empty list -> one all-bands sub.
    public IEnumerable<MqttSubscription> GetSubscriptions()
    {
        var subs = Subscriptions.Count > 0 ? Subscriptions : new List<MqttSubscription> { new() };

        foreach (var sub in subs)
        {
            yield return new MqttSubscription
            {
                Topic = sub.Topic,
                Host = sub.Host ?? Host,
                Port = sub.Port ?? Port
            };
        }
    }
}

public class MqttSubscription
{
    public string Topic { get; set; } = "pskr/filter/v2/+/+/+/+/+/+/+";
    public string? Host { get; set; }
    public int? Port { get; set; }
}
