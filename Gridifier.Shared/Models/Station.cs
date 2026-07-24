namespace Gridifier.Shared.Models;

public class Station
{
    public string Callsign { get; set; } = string.Empty;
    public string Grid { get; set; } = string.Empty;
    public DateTime LastUpdate { get; set; }
}