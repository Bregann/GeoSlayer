using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Progression.Responses
{
    public class PlayerUpgradesDto
    {
        public int BonusPointsEarned { get; set; }
        public int BonusPointsSpent { get; set; }
        public int BonusPointsAvailable { get; set; }

        /// <summary>What the next respec will cost (§3.0a: cheap first, then escalating).</summary>
        public int RespecCost { get; set; }

        public List<UpgradeDto> Upgrades { get; set; } = [];
    }

    public class UpgradeDto
    {
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int Rank { get; set; }
        public int MaxRank { get; set; }
        public double EffectPerRank { get; set; }

        /// <summary>Current total effect — <c>EffectPerRank × Rank</c>.</summary>
        public double CurrentEffect { get; set; }

        /// <summary>Cost of the next rank, or null at max rank.</summary>
        public int? NextRankCost { get; set; }

        public int MinAdventurerLevel { get; set; }

        /// <summary>False when below the minimum level — shown, but not purchasable.</summary>
        public bool IsAvailable { get; set; }

        public bool CanAfford { get; set; }
    }
}
