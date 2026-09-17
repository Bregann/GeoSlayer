using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Progression.Responses
{
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
}
