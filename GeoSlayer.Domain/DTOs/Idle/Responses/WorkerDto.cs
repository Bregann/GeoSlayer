using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses
{
    public class WorkerDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int Tier { get; set; }
        public int? ClaimId { get; set; }
        public string? ClaimName { get; set; }
        public SkillType? AssignedSkill { get; set; }
        public string? AssignedSkillName { get; set; }

        public DateTime LastCollectedAtUtc { get; set; }

        /// <summary>Current XP per hour, including tier and terrain multipliers.</summary>
        public double XpPerHour { get; set; }
        public double MaterialsPerHour { get; set; }

        /// <summary>1.0 on mismatched terrain — base rate, never zero (§5.2).</summary>
        public double TerrainMultiplier { get; set; }

        /// <summary>True when this worker's skill suits its Claim's terrain.</summary>
        public bool TerrainMatches { get; set; }

        /// <summary>When accrual stops paying, so the player can plan a return.</summary>
        public DateTime CapReachedAtUtc { get; set; }
        public bool IsAtCap { get; set; }
        public bool IsIdle { get; set; }
    }
}
