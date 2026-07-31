using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gridifier.Tests;

public class StatsApiTests
{
    private static WebApplicationFactory<Program> CreateFactory(bool enabled)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stats:Enabled"] = enabled ? "true" : "false"
                })));
    }

    [Fact]
    public async Task Get_returns_expected_shape_when_enabled()
    {
        using var factory = CreateFactory(enabled: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);

        Assert.Contains("totalConnects", body!.Keys);
        Assert.Contains("totalDisconnects", body.Keys);
        Assert.Contains("lastConnectAt", body.Keys);
        Assert.Contains("lastDisconnectAt", body.Keys);
        Assert.Contains("lastDisconnectReason", body.Keys);
        Assert.Contains("activeStations", body.Keys);
        Assert.Contains("stationsByBand", body.Keys);
        Assert.Contains("droppedMessages", body.Keys);
    }

    [Fact]
    public async Task Get_returns_404_when_disabled_by_default()
    {
        using var factory = CreateFactory(enabled: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
