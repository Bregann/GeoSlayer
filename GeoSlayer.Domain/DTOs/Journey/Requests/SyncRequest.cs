namespace GeoSlayer.Domain.DTOs.Journey.Requests;

public class SyncRequest
{
    /// <summary>
    /// Single-point path (legacy clients).  Ignored when <see cref="Positions"/> is populated.
    /// </summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>
    /// Client-side timestamp of the GPS reading (Unix milliseconds).
    /// Used as a secondary anti-cheat check against replay attacks.
    /// </summary>
    public long TimestampMs { get; set; }

    /// <summary>
    /// Batched path since the last sync, oldest first.  Preferred over the single-point
    /// fields above; a client that sends both gets the batch.
    /// </summary>
    public List<SyncPosition>? Positions { get; set; }

    /// <summary>
    /// The path as a normalised list, whichever shape the client used.
    /// </summary>
    public IReadOnlyList<SyncPosition> Path()
    {
        if (Positions is { Count: > 0 })
            return Positions;

        // A legacy client sends the single-point fields.  Null Island is not a
        // position anyone syncs from — treat an all-zero body as "no path given"
        // rather than sweeping cells off the coast of Africa.
        if (Latitude == 0 && Longitude == 0)
            return [];

        return
        [
            new SyncPosition
            {
                Latitude = Latitude,
                Longitude = Longitude,
                TimestampMs = TimestampMs,
            }
        ];
    }
}

public class SyncPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long TimestampMs { get; set; }

    /// <summary>
    /// Horizontal accuracy in metres, as reported by the device.  Null when the client
    /// does not report one — treated as unknown, not as perfect.
    /// </summary>
    public double? Accuracy { get; set; }
}
