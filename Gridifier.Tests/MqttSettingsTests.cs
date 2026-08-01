using Gridifier.Shared.Models;

namespace Gridifier.Tests;

public class MqttSettingsTests
{
    [Fact]
    public void GetSubscriptions_returns_default_all_bands_when_list_empty()
    {
        var settings = new MqttSettings();

        var subs = settings.GetSubscriptions().ToList();

        Assert.Single(subs);
        Assert.Equal("mqtt.pskreporter.info", subs[0].Host);
        Assert.Equal(1883, subs[0].Port);
        Assert.Equal("pskr/filter/v2/+/+/+/+/+/+/+", subs[0].Topic);
    }

    [Fact]
    public void GetSubscriptions_inherits_host_and_port()
    {
        var settings = new MqttSettings
        {
            Host = "broker.example.com",
            Port = 2883,
            Subscriptions =
            {
                new MqttSubscription { Topic = "pskr/filter/v2/20m/#" }
            }
        };

        var subs = settings.GetSubscriptions().ToList();

        Assert.Single(subs);
        Assert.Equal("broker.example.com", subs[0].Host);
        Assert.Equal(2883, subs[0].Port);
        Assert.Equal("pskr/filter/v2/20m/#", subs[0].Topic);
    }

    [Fact]
    public void GetSubscriptions_preserves_per_sub_overrides()
    {
        var settings = new MqttSettings
        {
            Host = "broker.example.com",
            Port = 2883,
            Subscriptions =
            {
                new MqttSubscription { Topic = "pskr/filter/v2/20m/#", Host = "other.example.com", Port = 4883 }
            }
        };

        var subs = settings.GetSubscriptions().ToList();

        Assert.Equal("other.example.com", subs[0].Host);
        Assert.Equal(4883, subs[0].Port);
    }

    [Fact]
    public void GetSubscriptions_supports_multiple_bands()
    {
        var settings = new MqttSettings
        {
            Subscriptions =
            {
                new MqttSubscription { Topic = "pskr/filter/v2/20m/#" },
                new MqttSubscription { Topic = "pskr/filter/v2/40m/#" }
            }
        };

        var subs = settings.GetSubscriptions().ToList();

        Assert.Equal(2, subs.Count);
        Assert.Equal("pskr/filter/v2/20m/#", subs[0].Topic);
        Assert.Equal("pskr/filter/v2/40m/#", subs[1].Topic);
    }
}
