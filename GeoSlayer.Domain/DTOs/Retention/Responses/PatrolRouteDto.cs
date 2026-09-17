using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class PatrolRouteDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int WaypointCount { get; set; }
        public int CompletionCount { get; set; }
        public DateTime? LastCompletedUtc { get; set; }
        public int UpkeepReward { get; set; }
        public List<PatrolWaypointDto> Waypoints { get; set; } = [];
    }

    public class PatrolWaypointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
