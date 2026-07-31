using Microsoft.Data.Sqlite;

namespace Gridifier.Shared.Data;

public static class DatabaseBackup
{
    public static string GetPath(string connectionString) =>
        new SqliteConnectionStringBuilder(connectionString).DataSource;

    private static string ConnString(string dbPath) => $"Data Source={dbPath};Pooling=False";

    public static string SnapshotPath(string dbPath) => dbPath + ".bak";

    public static string PreviousSnapshotPath(string dbPath) => dbPath + ".bak.1";

    public static bool IsHealthy(string dbPath)
    {
        try
        {
            using var conn = new SqliteConnection(ConnString(dbPath));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            return cmd.ExecuteScalar()?.ToString() == "ok";
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public static void TakeSnapshot(string dbPath)
    {
        var snapshot = SnapshotPath(dbPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(snapshot));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(snapshot))
            File.Copy(snapshot, PreviousSnapshotPath(dbPath), overwrite: true);

        using var source = new SqliteConnection(ConnString(dbPath));
        source.Open();
        using var destination = new SqliteConnection(ConnString(snapshot));
        destination.Open();
        source.BackupDatabase(destination);
    }

    public static bool TryRecoverFromSnapshot(string dbPath)
    {
        if (File.Exists(SnapshotPath(dbPath)) && TryRestore(dbPath, SnapshotPath(dbPath)))
            return true;

        if (File.Exists(PreviousSnapshotPath(dbPath)) && TryRestore(dbPath, PreviousSnapshotPath(dbPath)))
            return true;

        if (File.Exists(dbPath))
            PreserveCorruptFile(dbPath);

        return false;
    }

    private static bool TryRestore(string dbPath, string snapshotPath)
    {
        PreserveCorruptFile(dbPath);
        File.Copy(snapshotPath, dbPath, overwrite: true);
        return IsHealthy(dbPath);
    }

    public static void PreserveCorruptFile(string dbPath)
    {
        if (File.Exists(dbPath))
        {
            var corruptPath = $"{dbPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(dbPath, corruptPath, overwrite: true);
        }

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            try { File.Delete(dbPath + suffix); }
            catch (IOException) { /* best effort cleanup */ }
        }
    }
}
