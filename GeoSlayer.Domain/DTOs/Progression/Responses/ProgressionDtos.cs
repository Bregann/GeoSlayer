using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Progression.Responses;

/// <summary>What one XP grant actually did. Returned so the app can animate it.</summary>
public class XpGrantResult
{
    public SkillType? Skill { get; set; }

    /// <summary>Skill XP awarded, after upgrade modifiers.</summary>
    public long SkillXpEarned { get; set; }

    /// <summary>Adventurer XP awarded — the flat cut, or the milestone value.</summary>
    public long AdventurerXpEarned { get; set; }

    public int SkillLevel { get; set; }
    public bool SkillLevelledUp { get; set; }

    public int AdventurerLevel { get; set; }
    public long AdventurerXp { get; set; }
    public bool AdventurerLevelledUp { get; set; }

    /// <summary>Bonus Points granted by this level-up — one per level (§3.0a).</summary>
    public int BonusPointsGranted { get; set; }

    /// <summary>Unlocks crossed by this grant, for the celebration screen (§3.1c).</summary>
    public List<UnlockEventDto> Unlocks { get; set; } = [];
}

/// <summary>A ladder rung the player just crossed.</summary>
public class UnlockEventDto
{
    public int AdventurerLevel { get; set; }
    public UnlockType UnlockType { get; set; }
    public string Payload { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}

public class SkillDto
{
    public SkillType SkillType { get; set; }
    public string Name { get; set; } = null!;
    public long Xp { get; set; }
    public int Level { get; set; }

    /// <summary>Cumulative XP at the start of the current level, for the progress bar.</summary>
    public long XpForCurrentLevel { get; set; }
    public long XpForNextLevel { get; set; }
}

/// <summary>A skill not yet unlocked, shown greyed with its unlock level (§3.1c).</summary>
public class LockedSkillDto
{
    public string Name { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public UnlockType UnlockType { get; set; }
    public int UnlocksAtAdventurerLevel { get; set; }
}

public class PlayerSkillsDto
{
    public int AdventurerLevel { get; set; }
    public long AdventurerXp { get; set; }
    public long AdventurerXpForCurrentLevel { get; set; }
    public long AdventurerXpForNextLevel { get; set; }

    public List<SkillDto> Unlocked { get; set; } = [];

    /// <summary>The road ahead, ascending. Locked skills and systems both.</summary>
    public List<LockedSkillDto> Locked { get; set; } = [];
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

public class PlayerUpgradesDto
{
    public int BonusPointsEarned { get; set; }
    public int BonusPointsSpent { get; set; }
    public int BonusPointsAvailable { get; set; }

    /// <summary>What the next respec will cost (§3.0a: cheap first, then escalating).</summary>
    public int RespecCost { get; set; }

    public List<UpgradeDto> Upgrades { get; set; } = [];
}
