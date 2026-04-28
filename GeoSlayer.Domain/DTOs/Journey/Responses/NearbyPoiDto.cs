namespace GeoSlayer.Domain.DTOs.Journey.Responses;

public class NearbyPoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Skill { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int XpReward { get; set; }
    /// <summary>Distance from the player in metres.</summary>
    public double DistanceMetres { get; set; }
    /// <summary>True if the player is within interaction range.</summary>
    public bool InRange { get; set; }
}
