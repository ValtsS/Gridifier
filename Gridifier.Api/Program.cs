using Gridifier.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connString = builder.Configuration.GetConnectionString("Gridifier")
                 ?? "Data Source=gridifier.db";

var dbFactory = new DbConnectionFactory(connString);

using (var conn = dbFactory.CreateConnection())
{
    DatabaseInitializer.Initialize(conn);
}

builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<StationRepository>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }