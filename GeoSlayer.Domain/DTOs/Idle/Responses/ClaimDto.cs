using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class ClaimDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int CentreGridLat { get; set; }
        public int CentreGridLng { get; set; }
        public int Size { get; set; }
        public TerrainType TerrainProfile { get; set; }

        /// <summary>Terrain flags as names, so the app need not decode a bitmask.</summary>
        public List<string> Terrains { get; set; } = [];

        public DateTime ClaimedUtc { get; set; }

        /// <summary>Bounding box, for outlining the Claim on the map.</summary>
        public double South { get; set; }
        public double West { get; set; }
        public double North { get; set; }
        public double East { get; set; }

        public int WorkerCount { get; set; }
    }
}
