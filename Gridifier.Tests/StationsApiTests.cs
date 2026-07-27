using System.Net;
using System.Net.Http.Json;
using Gridifier.Shared.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Gridifier.Tests;

public class StationsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly StationRepository _repo;

    public StationsApiTests(WebApplicationFactory<Program> factory)
    {
        var dbPath = Path.GetTempFileName();
        var dbFactory = new DbConnectionFactory($"Data Source={dbPath}");
        using (var conn = dbFactory.CreateConnection())
            DatabaseInitializer.Initialize(conn);

        _repo = new StationRepository(dbFactory);

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(dbFactory);
                services.AddSingleton<StationRepository>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Get_returns_station_when_found()
    {
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

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
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });

        var response = await _client.GetAsync("/api/v1/grid/15m/%20%20test1%20%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_handles_callsign_with_slash()
    {
        _repo.Upsert(new Shared.Models.Station { Callsign = "OH1AA/MM", Band = "15m", Grid = "KP20" });

        var response = await _client.GetAsync("/api/v1/grid/15m/OH1AA/MM");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.Equal("KP20", body!["g"]!.ToString());
    }

    [Fact]
    public async Task Get_distinguishes_same_callsign_different_bands()
    {
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Band = "15m", Grid = "JO20AA" });
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Band = "20m", Grid = "JO30BB" });

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