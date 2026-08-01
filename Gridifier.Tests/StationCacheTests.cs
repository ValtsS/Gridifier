using Gridifier.Worker;

namespace Gridifier.Tests;

public class StationCacheTests
{
    private static ushort Grid(string g) => Shared.Validation.GridCodec.Encode(g);

    [Fact]
    public void TryUpdate_returns_true_for_new_callsign()
    {
        var cache = new StationCache();
        Assert.True(cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100));
    }

    [Fact]
    public void TryUpdate_returns_false_for_same_grid_newer_report()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.False(cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 200));
    }

    [Fact]
    public void TryUpdate_returns_true_when_grid_changed()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.True(cache.TryUpdate("TEST1", "15m", Grid("JO30BB"), 200));
    }

    [Fact]
    public void TryUpdate_ignores_stale_report()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 200);

        Assert.False(cache.TryUpdate("TEST1", "15m", Grid("JO30BB"), 100));
    }

    [Fact]
    public void TryUpdate_distinguishes_by_band()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.True(cache.TryUpdate("TEST1", "20m", Grid("JO20AA"), 100));
    }

    [Fact]
    public void TryUpdate_refreshes_lastHeard_for_later_report()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 250);

        Assert.True(cache.TryGet("TEST1", "15m", out _, out var lastHeard));
        Assert.Equal(250u, lastHeard);
    }

    [Fact]
    public void TryUpdate_stale_report_does_not_clobber_lastHeard()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 200);
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.True(cache.TryGet("TEST1", "15m", out _, out var lastHeard));
        Assert.Equal(200u, lastHeard);
    }

    [Fact]
    public void Count_tracks_entries()
    {
        var cache = new StationCache();
        Assert.Equal(0, cache.Count);

        cache.TryUpdate("A", "15m", Grid("JO20AA"), 100);
        cache.TryUpdate("B", "20m", Grid("JO30BB"), 100);

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Same_callsign_different_bands_are_separate()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);
        cache.TryUpdate("TEST1", "20m", Grid("JO30BB"), 100);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet("TEST1", "15m", out _, out _));
        Assert.True(cache.TryGet("TEST1", "20m", out _, out _));
    }

    [Fact]
    public void TryGet_returns_entry_by_callsign_and_band()
    {
        var cache = new StationCache();
        cache.TryUpdate("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.True(cache.TryGet("TEST1", "15m", out var grid, out _));
        Assert.Equal("JO20", grid);

        Assert.False(cache.TryGet("TEST1", "20m", out _, out _));
    }

    [Fact]
    public void Seed_populates_cache()
    {
        var cache = new StationCache();
        cache.Seed("TEST1", "15m", Grid("JO20AA"), 100);

        Assert.True(cache.TryGet("TEST1", "15m", out var grid, out var lastHeard));
        Assert.Equal("JO20", grid);
        Assert.Equal(100u, lastHeard);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Seed_keeps_newer_entry()
    {
        var cache = new StationCache();
        cache.Seed("TEST1", "15m", Grid("JO20AA"), 100);
        cache.Seed("TEST1", "15m", Grid("JO30BB"), 200);

        Assert.True(cache.TryGet("TEST1", "15m", out var grid, out var lastHeard));
        Assert.Equal("JO30", grid);
        Assert.Equal(200u, lastHeard);
    }

    [Fact]
    public void Seed_does_not_overwrite_newer_entry()
    {
        var cache = new StationCache();
        cache.Seed("TEST1", "15m", Grid("JO20AA"), 200);
        cache.Seed("TEST1", "15m", Grid("JO30BB"), 100);

        Assert.True(cache.TryGet("TEST1", "15m", out var grid, out var lastHeard));
        Assert.Equal("JO20", grid);
        Assert.Equal(200u, lastHeard);
    }

    [Fact]
    public void GetQuiet_returns_only_quiet_entries()
    {
        var cache = new StationCache();
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 50);
        cache.TryUpdate("ACTIVE", "15m", Grid("JO30BB"), 1_000);

        var quiet = cache.GetQuiet(cutoff: 100).ToList();

        Assert.Single(quiet);
        Assert.Equal("QUIET", quiet[0].Callsign);
        Assert.Equal(50u, quiet[0].LastHeard);
    }

    [Fact]
    public void GetQuiet_excludes_persisted_entries()
    {
        var cache = new StationCache();
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 50);
        cache.MarkPersisted("QUIET", "15m", lastHeard: 50);

        Assert.Empty(cache.GetQuiet(cutoff: 100));
    }

    [Fact]
    public void MarkPersisted_does_not_clear_newer_report()
    {
        var cache = new StationCache();
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 50);
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 60);
        cache.MarkPersisted("QUIET", "15m", lastHeard: 50);

        var quiet = cache.GetQuiet(cutoff: 100).ToList();
        Assert.Single(quiet);
        Assert.Equal(60u, quiet[0].LastHeard);
    }

    [Fact]
    public void MarkPersisted_then_new_report_goes_dirty_again()
    {
        var cache = new StationCache();
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 50);
        cache.MarkPersisted("QUIET", "15m", lastHeard: 50);
        cache.TryUpdate("QUIET", "15m", Grid("JO20AA"), 70);

        var quiet = cache.GetQuiet(cutoff: 100).ToList();
        Assert.Single(quiet);
        Assert.Equal(70u, quiet[0].LastHeard);
    }

    [Fact]
    public void Seed_entries_are_not_dirty()
    {
        var cache = new StationCache();
        cache.Seed("SEEDED", "15m", Grid("JO20AA"), 50);

        Assert.Empty(cache.GetQuiet(cutoff: 100));
    }

    [Fact]
    public void GetCountByBand_groups_counts()
    {
        var cache = new StationCache();
        cache.TryUpdate("A", "15m", Grid("JO20AA"), 100);
        cache.TryUpdate("B", "15m", Grid("JO20AA"), 100);
        cache.TryUpdate("C", "20m", Grid("JO30BB"), 100);

        var counts = cache.GetCountByBand();

        Assert.Equal(2, counts["15m"]);
        Assert.Equal(1, counts["20m"]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void CountActive_counts_only_entries_at_or_after_cutoff()
    {
        var cache = new StationCache();
        cache.TryUpdate("OLD", "15m", Grid("JO20AA"), 50);
        cache.TryUpdate("RECENT", "15m", Grid("JO30BB"), 1_000);

        Assert.Equal(1, cache.CountActive(cutoff: 100));
        Assert.Equal(2, cache.CountActive(cutoff: 0));
    }
}
