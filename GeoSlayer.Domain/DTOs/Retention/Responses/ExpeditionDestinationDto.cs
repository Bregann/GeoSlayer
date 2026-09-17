using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    /// <summary>
    /// A POI the player has visited, as an expedition destination (§5.4).
    ///
    /// <para>This is the whole point of the system: every trip already taken is a permanent
    /// asset. The list is the visit log from Stage 04, which is what made expeditions "nearly
    /// free to implement".</para>
    /// </summary>
    public class ExpeditionDestinationDto
    {
        public int PoiId { get; set; }
        public required string Name { get; set; }
        public SkillType Skill { get; set; }
        public required string SkillName { get; set; }

        /// <summary>When the player first found it — the reason it is on this list.</summary>
        public DateTime FirstVisitUtc { get; set; }

        public int TotalVisits { get; set; }

        /// <summary>Distance from the player's last verified position.</summary>
        public double DistanceMetres { get; set; }

        /// <summary>Round-trip duration, which scales with that distance.</summary>
        public double DurationHours { get; set; }

        /// <summary>Estimated material units, so the trade-off is visible before dispatching.</summary>
        public int EstimatedMaterials { get; set; }

        /// <summary>False when a worker is already there.</summary>
        public bool IsAvailable { get; set; }
    }
}
