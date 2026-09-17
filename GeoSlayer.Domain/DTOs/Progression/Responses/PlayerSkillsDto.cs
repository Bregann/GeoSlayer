using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Progression.Responses
{
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

    public class SkillDto
    {
        public SkillType SkillType { get; set; }
        public string Name { get; set; } = null!;
        public long Xp { get; set; }
        public int Level { get; set; }

        /// <summary>Cumulative XP at the start of the current level, for the progress bar.</summary>
        public long XpForCurrentLevel { get; set; }
        public long XpForNextLevel { get; set; }

        /// <summary>
        /// The next material tier this skill unlocks, so there is always something in view
        /// (SKILL-TEMPLATE.md). Null once every tier is unlocked.
        /// </summary>
        public string? NextTierName { get; set; }

        /// <summary>Skill level at which <see cref="NextTierName"/> becomes obtainable.</summary>
        public int? NextTierLevel { get; set; }
    }

    /// <summary>A skill not yet unlocked, shown greyed with its unlock level (§3.1c).</summary>
    public class LockedSkillDto
    {
        /// <summary>The ladder's display label. Named to match <see cref="UnlockEventDto"/>,
        /// which carries the same value — the two differing was a live source of blank
        /// labels in the app.</summary>
        public string DisplayName { get; set; } = null!;
        public string Payload { get; set; } = null!;
        public UnlockType UnlockType { get; set; }
        public int UnlocksAtAdventurerLevel { get; set; }
    }
}
