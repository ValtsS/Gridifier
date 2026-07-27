namespace Gridifier.Shared.Models;

public class MqttSettings
{
    public string Host { get; set; } = "mqtt.pskreporter.info";
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = "pskr/filter/v2/+/+/+/+/+/+/+";
}