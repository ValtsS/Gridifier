using Gridifier.Shared.Validation;
using Gridifier.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gridifier.Tests;

public class StatsRefresherTests
{
    [Fact]
    public void Refresh_precomputes_active_stations_and_band_counts()
    {
        var now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cache = new StationCache();
        cache.TryUpdate("A", "15m", GridCodec.Encode("JO20"), now);
        cache.TryUpdate("B", "20m", GridCodec.Encode("JO30"), now - 60);
        cache.TryUpdate("OLD", "15m", GridCodec.Encode("JO20"), now - 3600);

        var stats = new AppStats();
        var refresher = new StatsRefresher(
            NullLogger<StatsRefresher>.Instance,
            cache,
            stats,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10));

        refresher.Refresh();

        Assert.Equal(2, stats.ActiveStations);
        Assert.Equal(2, stats.StationsByBand["15m"]);
        Assert.Equal(1, stats.StationsByBand["20m"]);
        Assert.Equal(3, stats.CacheSize);
    }

    [Fact]
    public void Refresh_handles_empty_cache()
    {
        var stats = new AppStats();
        var refresher = new StatsRefresher(
            NullLogger<StatsRefresher>.Instance,
            new StationCache(),
            stats,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10));

        refresher.Refresh();

        Assert.Equal(0, stats.ActiveStations);
        Assert.Empty(stats.StationsByBand);
        Assert.Equal(0, stats.CacheSize);
    }
}
