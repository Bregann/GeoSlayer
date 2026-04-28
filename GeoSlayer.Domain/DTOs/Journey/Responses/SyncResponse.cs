namespace GeoSlayer.Domain.DTOs.Journey.Responses;

public class SyncResponse
{
    public string? StreetName { get; set; }
    public double PercentComplete { get; set; }
    public bool JustConquered { get; set; }
    public int Xp { get; set; }
    public int Level { get; set; }
    public List<NearbyPoiDto> NearbyPois { get; set; } = [];
}
