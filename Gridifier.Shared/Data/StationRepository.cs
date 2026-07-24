using Gridifier.Shared.Models;
using Microsoft.Data.Sqlite;

namespace Gridifier.Shared.Data;

public class StationRepository(DbConnectionFactory connectionFactory)
{
    public Station? GetByCallsign(string callsign)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT callsign, grid, last_update FROM stations WHERE callsign = @callsign";
        cmd.Parameters.AddWithValue("@callsign", callsign);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new Station
        {
            Callsign = reader.GetString(0),
            Grid = reader.GetString(1),
            LastUpdate = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.AssumeUniversal)
        };
    }

    public void Upsert(Station station)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stations (callsign, grid, last_update)
            VALUES (@callsign, @grid, datetime('now'))
            ON CONFLICT(callsign) DO UPDATE SET
                grid = @grid,
                last_update = datetime('now');
            """;
        cmd.Parameters.AddWithValue("@callsign", station.Callsign);
        cmd.Parameters.AddWithValue("@grid", station.Grid);
        cmd.ExecuteNonQuery();
    }

    public void UpsertMany(IReadOnlyList<Station> stations)
    {
        if (stations.Count == 0) return;

        using var conn = connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stations (callsign, grid, last_update)
            VALUES (@callsign, @grid, datetime('now'))
            ON CONFLICT(callsign) DO UPDATE SET
                grid = @grid,
                last_update = datetime('now');
            """;

        var callsignParam = cmd.Parameters.Add("@callsign", Microsoft.Data.Sqlite.SqliteType.Text);
        var gridParam = cmd.Parameters.Add("@grid", Microsoft.Data.Sqlite.SqliteType.Text);

        foreach (var station in stations)
        {
            callsignParam.Value = station.Callsign;
            gridParam.Value = station.Grid;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public IEnumerable<Station> GetAll()
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT callsign, grid, last_update FROM stations ORDER BY callsign";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new Station
            {
                Callsign = reader.GetString(0),
                Grid = reader.GetString(1),
                LastUpdate = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.AssumeUniversal)
            };
        }
    }
}