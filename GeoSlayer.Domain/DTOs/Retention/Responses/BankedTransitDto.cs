using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    /// <summary>A cell passed through above walking pace, awaiting redemption (§7.1).</summary>
    public class BankedTransitDto
    {
        public int GridLat { get; set; }
        public int GridLng { get; set; }

        /// <summary>Bounds, so the map can draw it as a distinct visual state.</summary>
        public double South { get; set; }
        public double West { get; set; }
        public double North { get; set; }
        public double East { get; set; }

        /// <summary>Under 1 for a cycling-pace cell, which banked only partially.</summary>
        public double Weight { get; set; }

        /// <summary>An opportunity, not an obligation — it lapses if unclaimed.</summary>
        public DateTime ExpiresUtc { get; set; }
    }
}
