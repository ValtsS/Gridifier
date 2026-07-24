using Gridifier.Shared.Data;
using Gridifier.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connString = builder.Configuration.GetConnectionString("Gridifier")
                 ?? "Data Source=gridifier.db";

var dbFactory = new DbConnectionFactory(connString);

using (var conn = dbFactory.CreateConnection())
{
    DatabaseInitializer.Initialize(conn);
}

builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<StationRepository>();
builder.Services.AddHostedService<SampleWorker>();

var host = builder.Build();
host.Run();