using System.Threading.Channels;
using Gridifier.Api.Endpoints;
using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Shared.Validation;
using Gridifier.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connString = builder.Configuration.GetConnectionString("Gridifier")
                 ?? "Data Source=gridifier.db";

var mqttSettings = builder.Configuration.GetSection("Mqtt").Get<MqttSettings>()
                   ?? new MqttSettings();

var subscriptions = mqttSettings.GetSubscriptions().ToList();

var dbPath = DatabaseBackup.GetPath(connString);

using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var startupLogger = startupLoggerFactory.CreateLogger("Startup");

if (!DatabaseBackup.IsHealthy(dbPath))
{
    startupLogger.LogWarning("Database failed integrity check; attempting restore from snapshot");
    if (!DatabaseBackup.TryRecoverFromSnapshot(dbPath))
        startupLogger.LogCritical("No usable snapshot; preserved corrupt database and starting fresh");
}

var dbFactory = new DbConnectionFactory(connString);

using (var conn = dbFactory.CreateConnection())
{
    DatabaseInitializer.Initialize(conn);
}

// Registered first so it stops last (snapshot runs after DatabaseWriter's final flush).
var backupEnabled = builder.Configuration.GetValue("Backup:Enabled", true);
if (backupEnabled)
{
    var backupInterval = TimeSpan.FromHours(
        builder.Configuration.GetValue("Backup:IntervalHours", 6));
    builder.Services.AddHostedService(sp => new DatabaseBackupWorker(
        sp.GetRequiredService<ILogger<DatabaseBackupWorker>>(),
        dbPath,
        backupInterval));
}

var stationChannel = Channel.CreateBounded<Station>(new BoundedChannelOptions(PskMessageHandler.StationChannelCapacity)
{
    FullMode = BoundedChannelFullMode.DropOldest
});

var stationRepository = new StationRepository(dbFactory);
var stationCache = new StationCache();

foreach (var station in stationRepository.GetAll())
{
    stationCache.Seed(
        station.Callsign,
        station.Band,
        GridCodec.Encode(GridValidator.Shorten(station.Grid)),
        (uint)station.LastUpdate);
}

builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<StationRepository>();
builder.Services.AddSingleton(stationCache);
var appStats = new AppStats(subscriptions.Count);
appStats.DatabaseCount = stationRepository.Count();
appStats.InitializeProcessDiagnostics();
builder.Services.AddSingleton(appStats);
builder.Services.AddSingleton(mqttSettings);
builder.Services.AddSingleton(stationChannel);
for (var i = 0; i < subscriptions.Count; i++)
{
    var index = i;
    var sub = subscriptions[i];
    // NOTE: register as IHostedService directly (not AddHostedService<T>) —
    // AddHostedService<T> deduplicates registrations by type, so a second
    // PskReporterWorker would silently never start.
    var connection = MqttConnectionFactory.Create(
        index,
        sub.Host!,
        sub.Port!.Value,
        builder.Configuration.GetValue("Mqtt:Transport", "mqttnet"));
    builder.Services.AddSingleton(connection);
    builder.Services.AddSingleton<IHostedService>(sp => new PskReporterWorker(
        sp.GetRequiredService<ILogger<PskReporterWorker>>(),
        connection,
        stationChannel,
        appStats,
        index,
        sub.Topic));
}
builder.Services.AddHostedService<DatabaseWriter>();

var sweepInterval = TimeSpan.FromMinutes(
    builder.Configuration.GetValue("Cache:SweepIntervalMinutes", 5));
builder.Services.AddHostedService(sp => new StationSweeper(
    sp.GetRequiredService<ILogger<StationSweeper>>(),
    stationCache,
    stationRepository,
    appStats,
    sweepInterval));

var statsRefreshInterval = TimeSpan.FromSeconds(
    builder.Configuration.GetValue("Stats:RefreshIntervalSeconds", 30));
var statsActiveWindow = TimeSpan.FromMinutes(
    builder.Configuration.GetValue("Stats:ActiveWindowMinutes", 10));
builder.Services.AddHostedService(sp => new StatsRefresher(
    sp.GetRequiredService<ILogger<StatsRefresher>>(),
    stationCache,
    appStats,
    statsRefreshInterval,
    statsActiveWindow));

var app = builder.Build();

app.UseAuthorization();
app.MapGet("/api/v1/grid/{band}/{*callsign}", GridEndpoint.Get);
app.MapControllers();

app.Run();

public partial class Program { }
