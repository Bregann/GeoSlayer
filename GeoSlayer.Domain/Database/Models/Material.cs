using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A thing a player can hold (DESIGN.md §4.1, §4.1a).
    ///
    /// Seeded rather than hardcoded, like the rest of the game data, so the pools and tiers
    /// can be retuned without a deploy.
    /// </summary>
    public class Material
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string Key { get; set; } = null!;

        [Required, MaxLength(128)]
        public string Name { get; set; } = null!;

        /// <summary>Tier 1 upward. Roughly one new tier per 10–15 skill levels.</summary>
        public int Tier { get; set; } = 1;

        public MaterialCategory Category { get; set; }

        /// <summary>The gathering skill this belongs to, if any.</summary>
        public SkillType? SkillType { get; set; }

        /// <summary>
        /// Per-material cap (§7.4). Caps are per material, never a global inventory limit —
        /// a global limit makes offline workers stall overnight.
        /// </summary>
        public int StackCap { get; set; } = 1000;

        /// <summary>
        /// True for the small reserved set of named materials attached to the rarest POIs.
        /// Exclusivity means nothing if everything is exclusive (§7.4).
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Skill level needed to obtain this <b>at all</b> (§4.1a). Below it the material is
        /// not obtainable — not rare, not slow. Absolutely gated.
        /// </summary>
        public int LevelRequired { get; set; } = 1;

        /// <summary>
        /// Seconds to gather one unit. Rises with tier, which is what makes higher tiers read
        /// as reliably slower rather than as a lottery.
        /// </summary>
        public double BaseGatherSeconds { get; set; } = 3;

        /// <summary>
        /// XP per unit. Rises proportionally with <see cref="BaseGatherSeconds"/> so XP/hour
        /// stays roughly flat across tiers — a player is never punished for advancing.
        /// </summary>
        public double XpPerUnit { get; set; } = 1;

        /// <summary>
        /// Units of Dust one overflowing unit converts to (§7.4). Deliberately poor: overflow
        /// should create pressure to return, not reward ignoring caps.
        /// </summary>
        public int DustPerOverflow { get; set; } = 1;
    }
}
