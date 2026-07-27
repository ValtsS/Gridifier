using Gridifier.Shared.Models;
using Gridifier.Worker;

namespace Gridifier.Tests;

public class StationCacheTests
{
    [Fact]
    public void ShouldSkip_returns_false_for_new_callsign()
    {
        var cache = new StationCache();
        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" }));
    }

    [Fact]
    public void ShouldSkip_returns_true_for_same_grid_within_cooldown()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" }));
    }

    [Fact]
    public void ShouldSkip_returns_false_when_grid_changed()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB" }));
    }

    [Fact]
    public void ShouldSkip_distinguishes_by_band()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "20m", Grid = "JO20AA" }));
    }

    [Fact]
    public void MarkWritten_updates_grid_and_resets_cooldown()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB" });

        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO30BB" }));
        Assert.False(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" }));
    }

    [Fact]
    public void Count_tracks_entries()
    {
        var cache = new StationCache();
        Assert.Equal(0, cache.Count);

        cache.MarkWritten(new Station { Callsign = "A", Band = "15m", Grid = "JO20AA" });
        cache.MarkWritten(new Station { Callsign = "B", Band = "20m", Grid = "JO30BB" });

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void MaybeEvict_does_not_remove_recent_entries()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "KEEP", Band = "15m", Grid = "JO20AA" });

        for (int i = 0; i < 100; i++)
            cache.MaybeEvict();

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Same_callsign_different_bands_are_separate()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "20m", Grid = "JO30BB" });

        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" }));
        Assert.True(cache.ShouldSkip(new Station { Callsign = "TEST1", Band = "20m", Grid = "JO30BB" }));
    }

    [Fact]
    public void TryGet_returns_entry_by_callsign_and_band()
    {
        var cache = new StationCache();
        cache.MarkWritten(new Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

        Assert.True(cache.TryGet("TEST1", "15m", out var grid, out _));
        Assert.Equal("JO20AA", grid);

        Assert.False(cache.TryGet("TEST1", "20m", out _, out _));
    }
}