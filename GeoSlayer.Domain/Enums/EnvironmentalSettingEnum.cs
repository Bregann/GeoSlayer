namespace GeoSlayer.Domain.Enums
{
    public enum EnvironmentalSettingEnum
    {
        HangfireUsername,
        HangfirePassword,

        /// <summary>Fraction of skill XP that also lands as Adventurer XP (DESIGN.md §3.0b).
        /// The single most important balance constant in the game.</summary>
        GlobalXpRatio,

        /// <summary>Multiplier applied to <see cref="GlobalXpRatio"/> for idle sources (§3.3).</summary>
        IdleXpRatioMultiplier,

        /// <summary>Adventurer XP for each milestone in <c>MilestoneType</c> (§3.0b).</summary>
        MilestoneXpNewCell,
        MilestoneXpFirstPoiVisit,
        MilestoneXpNewRegion,
        MilestoneXpClaimTerritory,
        MilestoneXpCraftComplete,
        MilestoneXpSkillUnlock,

        /// <summary>Bonus Points charged by the first respec; escalates per use (§3.0a).</summary>
        RespecBaseCost
    }
}
