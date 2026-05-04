namespace GeoSlayer.Domain.DTOs.Journey.Requests;

public class SyncRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int PlayerId { get; set; }

    /// <summary>
    /// Client-side timestamp of the GPS reading (Unix milliseconds).
    /// Used as a secondary anti-cheat check against replay attacks.
    /// </summary>
    public long TimestampMs { get; set; }
}
