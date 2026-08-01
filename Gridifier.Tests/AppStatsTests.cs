using Gridifier.Worker;

namespace Gridifier.Tests;

public class AppStatsTests
{
    [Fact]
    public void MqttConnected_reflects_any_subscription()
    {
        var stats = new AppStats(subscriptionCount: 2);

        Assert.False(stats.MqttConnected);
        Assert.Equal(0, stats.ConnectedSubscriptions);

        stats.SetSubscriptionConnected(0, connected: true, topic: "pskr/filter/v2/20m/#");

        Assert.True(stats.MqttConnected);
        Assert.Equal(1, stats.ConnectedSubscriptions);
        Assert.True(stats.Subscriptions[0].Connected);
        Assert.Equal("pskr/filter/v2/20m/#", stats.Subscriptions[0].Topic);
    }

    [Fact]
    public void MqttConnected_false_when_all_subscriptions_disconnect()
    {
        var stats = new AppStats(subscriptionCount: 1);
        stats.SetSubscriptionConnected(0, connected: true);

        stats.SetSubscriptionConnected(0, connected: false);

        Assert.False(stats.MqttConnected);
        Assert.Equal(0, stats.ConnectedSubscriptions);
        Assert.NotNull(stats.Subscriptions[0].LastDisconnectAt);
    }

    [Fact]
    public void OutOfRange_index_is_ignored()
    {
        var stats = new AppStats(subscriptionCount: 1);

        stats.SetSubscriptionConnected(5, connected: true);

        Assert.Equal(0, stats.ConnectedSubscriptions);
    }
}
