using Gridifier.Shared.Models;
using Gridifier.Worker;

namespace Gridifier.Tests;

public class StationCacheTests
{
    [Fact]
    public void ShouldSkip_returns_false_for_new_callsign()
    {
        var cache = new StationCache();
        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Grid = "JO20AA" }));
    }

    [Fact]
    public void ShouldSkip_returns_true_for_same_grid_within_cooldown()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Grid = "JO20AA" });

        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Grid = "JO20AA" }));
    }

    [Fact]
    public void ShouldSkip_returns_false_when_grid_changed()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Grid = "JO20AA" });

        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Grid = "JO30BB" }));
    }

    [Fact]
    public void MarkWritten_updates_grid_and_resets_cooldown()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Grid = "JO20AA" });
        cache.MarkWritten(new Station { Callsign = "TEST1", Grid = "JO30BB" });

        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Grid = "JO30BB" }));
        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Grid = "JO20AA" }));
    }

    [Fact]
    public void Count_tracks_entries()
    {
        var cache = new StationCache();
        Assert.Equal(0, cache.Count);

        cache.MarkWritten(new Station { Callsign = "A", Grid = "JO20AA" });
        cache.MarkWritten(new Station { Callsign = "B", Grid = "JO30BB" });

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void MaybeEvict_does_not_remove_recent_entries()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "KEEP", Grid = "JO20AA" });

        for (int i = 0; i < 100; i++)
            cache.MaybeEvict();

        Assert.Equal(1, cache.Count);
    }
}