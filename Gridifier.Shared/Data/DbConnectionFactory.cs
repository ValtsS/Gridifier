using Microsoft.Data.Sqlite;

namespace Gridifier.Shared.Data;

public class DbConnectionFactory(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var walCmd = connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL;";
        walCmd.ExecuteNonQuery();

        // synchronous=FULL (default) fsyncs on every commit. NORMAL only syncs
        // the WAL at checkpoint, which is safe here: RAM is the source of truth,
        // SQLite is just a recovery log, so a crash may lose recent commits but
        // never corrupt. Big win on slow (J1900/eMMC) disks.
        using var syncCmd = connection.CreateCommand();
        syncCmd.CommandText = "PRAGMA synchronous=NORMAL;";
        syncCmd.ExecuteNonQuery();

        using var busyCmd = connection.CreateCommand();
        busyCmd.CommandText = "PRAGMA busy_timeout=10000;";
        busyCmd.ExecuteNonQuery();

        return connection;
    }
}