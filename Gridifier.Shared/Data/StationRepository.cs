using Gridifier.Shared.Models;
using Microsoft.Data.Sqlite;

namespace Gridifier.Shared.Data;

public class StationRepository(DbConnectionFactory connectionFactory)
{
    public Station? GetByCallsignAndBand(string callsign, string band)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT callsign, band, grid, last_update FROM stations WHERE callsign = @callsign AND band = @band";
        cmd.Parameters.AddWithValue("@callsign", callsign);
        cmd.Parameters.AddWithValue("@band", band);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return ReadStation(reader);
    }

    public void Upsert(Station station)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stations (callsign, band, grid, last_update)
            VALUES (@callsign, @band, @grid, @last_update)
            ON CONFLICT(callsign, band) DO UPDATE SET
                grid = @grid,
                last_update = @last_update
            WHERE excluded.last_update > stations.last_update;
            """;
        cmd.Parameters.AddWithValue("@callsign", station.Callsign);
        cmd.Parameters.AddWithValue("@band", station.Band);
        cmd.Parameters.AddWithValue("@grid", station.Grid);
        cmd.Parameters.AddWithValue("@last_update", station.LastUpdate);
        cmd.ExecuteNonQuery();
    }

    public void UpsertMany(IReadOnlyList<Station> stations)
    {
        if (stations.Count == 0) return;

        using var conn = connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stations (callsign, band, grid, last_update)
            VALUES (@callsign, @band, @grid, @last_update)
            ON CONFLICT(callsign, band) DO UPDATE SET
                grid = @grid,
                last_update = @last_update
            WHERE excluded.last_update > stations.last_update;
            """;

        var callsignParam = cmd.Parameters.Add("@callsign", SqliteType.Text);
        var bandParam = cmd.Parameters.Add("@band", SqliteType.Text);
        var gridParam = cmd.Parameters.Add("@grid", SqliteType.Text);
        var lastUpdateParam = cmd.Parameters.Add("@last_update", SqliteType.Integer);

        foreach (var station in stations)
        {
            callsignParam.Value = station.Callsign;
            bandParam.Value = station.Band;
            gridParam.Value = station.Grid;
            lastUpdateParam.Value = station.LastUpdate;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public IEnumerable<Station> GetAll()
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT callsign, band, grid, last_update FROM stations ORDER BY callsign, band";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadStation(reader);
        }
    }

    public long Count()
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM stations";
        return (long)cmd.ExecuteScalar()!;
    }

    private static Station ReadStation(SqliteDataReader reader)
    {
        return new Station
        {
            Callsign = reader.GetString(0),
            Band = reader.GetString(1),
            Grid = reader.GetString(2),
            LastUpdate = reader.GetInt64(3)
        };
    }
}
