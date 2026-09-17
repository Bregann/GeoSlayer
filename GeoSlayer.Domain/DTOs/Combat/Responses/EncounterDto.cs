namespace GeoSlayer.Domain.DTOs.Combat.Responses
{
    /// <summary>An encounter waiting at a POI (DESIGN.md §5C).</summary>
    public class EncounterDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public int PoiId { get; set; }
        public required string PoiName { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public int Tier { get; set; }
        public int MinCombatLevel { get; set; }

        /// <summary>Permanent when true — the reliable route (§5C.1).</summary>
        public bool IsTrainingGround { get; set; }

        /// <summary>Null for a training ground, which never lapses.</summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>Shown before committing to the walk, so the trade-off is visible.</summary>
        public double WinChance { get; set; }

        public double DistanceMetres { get; set; }

        /// <summary>False when the player is too far to fight it yet.</summary>
        public bool IsInRange { get; set; }
    }
}
