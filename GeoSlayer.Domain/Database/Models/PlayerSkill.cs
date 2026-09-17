using GeoSlayer.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A skill a player has unlocked, and their progress in it (DESIGN.md §3.1).
    ///
    /// The row existing <b>is</b> the unlock: there is no level-0 state to special-case.
    /// Skill levels use the RS curve undivided, unlike Adventurer level.
    /// </summary>
    public class PlayerSkill
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public SkillType SkillType { get; set; }

        /// <summary>Cumulative lifetime XP in this skill.</summary>
        public long Xp { get; set; }

        /// <summary>Cached level derived from <see cref="Xp"/>.</summary>
        public int Level { get; set; } = 1;

        public DateTime UnlockedAtUtc { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;
    }
}
