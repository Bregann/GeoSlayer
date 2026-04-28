namespace GeoSlayer.Domain.DTOs.Journey.Requests;

public class SyncRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int PlayerId { get; set; }
}
