using System.Net;
using System.Net.Http.Json;
using Gridifier.Shared.Validation;
using Gridifier.Worker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Gridifier.Tests;

public class StationsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly StationCache _cache;

    public StationsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _cache = factory.Services.GetRequiredService<StationCache>();
    }

    private void Seed(string callsign, string band, string grid, uint lastHeard)
    {
        _cache.Seed(callsign, band, GridCodec.Encode(GridValidator.Shorten(grid)), lastHeard);
    }

    [Fact]
    public async Task Get_returns_station_when_found()
    {
        Seed("TEST1", "15m", "JO20AA", 1_784_910_000);

        var response = await _client.GetAsync("/api/v1/grid/15m/TEST1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.Contains("g", body!.Keys);
        Assert.Contains("t", body.Keys);
    }

    [Fact]
    public async Task Get_returns_404_when_not_found()
    {
        var response = await _client.GetAsync("/api/v1/grid/15m/NONEXISTENT");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_invalid_band()
    {
        var response = await _client.GetAsync("/api/v1/grid/invalid/TEST1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_invalid_callsign()
    {
        var response = await _client.GetAsync("/api/v1/grid/15m/ab%20c");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_too_long_callsign()
    {
        var response = await _client.GetAsync("/api/v1/grid/15m/" + new string('X', 17));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_normalizes_whitespace_and_case()
    {
        Seed("TEST1", "15m", "JO20AA", 1_784_910_000);

        var response = await _client.GetAsync("/api/v1/grid/15m/%20%20test1%20%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_normalizes_lowercase_callsign()
    {
        Seed("TEST1", "15m", "JO20AA", 1_784_910_000);

        var response = await _client.GetAsync("/api/v1/grid/15m/test1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("JO20", body!["g"]!.ToString());
    }

    [Fact]
    public async Task Get_normalizes_uppercase_band()
    {
        Seed("TEST1", "20m", "JO20AA", 1_784_910_000);

        var response = await _client.GetAsync("/api/v1/grid/20M/TEST1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("15")]       // no unit suffix
    [InlineData("15ghz")]    // unknown unit
    [InlineData("xm")]
    [InlineData("1 5m")]     // space
    public async Task Get_returns_400_for_malformed_band(string band)
    {
        var response = await _client.GetAsync($"/api/v1/grid/{Uri.EscapeDataString(band)}/TEST1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("TEST1!")]    // illegal character
    [InlineData("TE!ST1")]
    [InlineData("TEST 1")]    // space
    [InlineData("TEST*1")]
    public async Task Get_returns_400_for_malformed_callsign(string callsign)
    {
        var response = await _client.GetAsync($"/api/v1/grid/15m/{Uri.EscapeDataString(callsign)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_empty_callsign()
    {
        var response = await _client.GetAsync("/api/v1/grid/15m/");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_handles_callsign_with_slash()
    {
        Seed("OH1AA/MM", "15m", "KP20", 1_784_910_000);

        var response = await _client.GetAsync("/api/v1/grid/15m/OH1AA/MM");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.Equal("KP20", body!["g"]!.ToString());
    }

    [Fact]
    public async Task Get_returns_unix_timestamp()
    {
        const uint lastHeard = 1_784_910_000;
        Seed("TEST1", "15m", "JO20AA", lastHeard);

        var response = await _client.GetAsync("/api/v1/grid/15m/TEST1");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        var tEl = (System.Text.Json.JsonElement)body!["t"]!;
        Assert.Equal(lastHeard, tEl.GetUInt64());
    }

    [Fact]
    public async Task Get_distinguishes_same_callsign_different_bands()
    {
        Seed("TEST1", "15m", "JO20AA", 1_784_910_000);
        Seed("TEST1", "20m", "JO30BB", 1_784_910_000);

        var r1 = await _client.GetAsync("/api/v1/grid/15m/TEST1");
        var r2 = await _client.GetAsync("/api/v1/grid/20m/TEST1");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        var b1 = await r1.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var b2 = await r2.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("JO20", b1!["g"]!.ToString());
        Assert.Equal("JO30", b2!["g"]!.ToString());
    }
}
