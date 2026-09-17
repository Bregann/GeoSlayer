using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>The progression ladder and upgrade tree (Stage 18 task 8).</summary>
    public class AdminProgressionDto
    {
        public List<AdminUnlockDto> Unlocks { get; set; } = [];
        public List<AdminUpgradeDto> Upgrades { get; set; } = [];

        /// <summary>Warnings about the ladder as a whole, such as the §3.1 cold start.</summary>
        public List<string> LadderWarnings { get; set; } = [];
    }

    /// <summary>One rung of the unlock ladder (§3.1).</summary>
    public class AdminUnlockDto
    {
        public int Id { get; set; }
        public int AdventurerLevel { get; set; }
        public UnlockType UnlockType { get; set; }
        public required string Payload { get; set; }
        public required string DisplayName { get; set; }
    }

    /// <summary>One Bonus Point upgrade (§3.0a).</summary>
    public class AdminUpgradeDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Category { get; set; }
        public required string Description { get; set; }

        public int MaxRank { get; set; }

        /// <summary>Comma-separated Bonus Point cost per rank, e.g. "1,2,4,7,11".</summary>
        public required string CostCurve { get; set; }

        public double EffectPerRank { get; set; }
        public int MinAdventurerLevel { get; set; }

        /// <summary>Total Bonus Points to max it — the number that is hard to eyeball.</summary>
        public int TotalCost { get; set; }
    }
}
