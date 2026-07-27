using Microsoft.Data.Sqlite;
using Gridifier.Shared.Data;

namespace Gridifier.Shared.Data;

public static class DatabaseInitializer
{
    public static void Initialize(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS stations (
                callsign     TEXT NOT NULL,
                band         TEXT NOT NULL DEFAULT '',
                grid         TEXT NOT NULL,
                last_update  TEXT NOT NULL DEFAULT (datetime('now')),
                PRIMARY KEY (callsign, band)
            );
            """;
        cmd.ExecuteNonQuery();
    }
}