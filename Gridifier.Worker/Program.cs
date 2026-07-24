using Gridifier.Shared.Data;
using Gridifier.Shared.Models;
using Gridifier.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connString = builder.Configuration.GetConnectionString("Gridifier")
                 ?? "Data Source=gridifier.db";

var mqttSettings = builder.Configuration.GetSection("Mqtt").Get<MqttSettings>()
                   ?? new MqttSettings();

var dbFactory = new DbConnectionFactory(connString);

using (var conn = dbFactory.CreateConnection())
{
    DatabaseInitializer.Initialize(conn);
}

builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<StationRepository>();
builder.Services.AddSingleton(mqttSettings);
builder.Services.AddHostedService<PskReporterWorker>();

var host = builder.Build();
host.Run();