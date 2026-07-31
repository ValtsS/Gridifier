using Microsoft.Data.Sqlite;

namespace Gridifier.Shared.Data;

public static class DatabaseInitializer
{
    public static void Initialize(SqliteConnection connection)
    {
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS stations (
                    callsign     TEXT NOT NULL,
                    band         TEXT NOT NULL DEFAULT '',
                    grid         TEXT NOT NULL,
                    last_update  INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (callsign, band)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateLastUpdateType(connection);
    }

    private static void MigrateLastUpdateType(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT type FROM pragma_table_info('stations') WHERE name = 'last_update'";
        var columnType = check.ExecuteScalar() as string;

        if (columnType is "INTEGER" or "INT" or "INT8" or "BIGINT" or "UNSIGNED BIG INT")
            return;

        using var rebuild = connection.CreateCommand();
        rebuild.CommandText = """
            BEGIN;
            DROP TABLE stations;
            CREATE TABLE stations (
                callsign     TEXT NOT NULL,
                band         TEXT NOT NULL DEFAULT '',
                grid         TEXT NOT NULL,
                last_update  INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (callsign, band)
            );
            COMMIT;
            """;
        rebuild.ExecuteNonQuery();
    }
}
