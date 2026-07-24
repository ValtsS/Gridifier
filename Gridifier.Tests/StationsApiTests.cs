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
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Grid = "JO20AA" });

        var response = await _client.GetAsync("/api/stations/TEST1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.Contains("grid", body!.Keys);
        Assert.Contains("lastUpdate", body.Keys);
    }

    [Fact]
    public async Task Get_returns_404_when_not_found()
    {
        var response = await _client.GetAsync("/api/stations/NONEXISTENT");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_invalid_callsign()
    {
        var response = await _client.GetAsync("/api/stations/ab%20c");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_for_too_long_callsign()
    {
        var response = await _client.GetAsync("/api/stations/" + new string('X', 17));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_normalizes_whitespace_and_case()
    {
        _repo.Upsert(new Shared.Models.Station { Callsign = "TEST1", Grid = "JO20AA" });

        var response = await _client.GetAsync("/api/stations/%20%20test1%20%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}