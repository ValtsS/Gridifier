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

        return connection;
    }
}