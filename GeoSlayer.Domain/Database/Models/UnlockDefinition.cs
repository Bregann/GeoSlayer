using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// One rung of the unlock ladder (DESIGN.md §3.1), seeded rather than hardcoded.
    ///
    /// Keeping the ladder as data is what lets the whole progression spine be retuned
    /// without a deploy — and per DESIGN.md it will be, repeatedly.
    /// </summary>
    public class UnlockDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Adventurer level at which this unlock fires.</summary>
        public int AdventurerLevel { get; set; }

        public UnlockType UnlockType { get; set; }

        /// <summary>
        /// For <see cref="UnlockType.Skill"/> this is the <see cref="SkillType"/> name;
        /// for <see cref="UnlockType.System"/> a system key such as <c>Claims</c>.
        /// Stored as text so systems that have no enum yet can still be seeded.
        /// </summary>
        [Required, MaxLength(64)]
        public string Payload { get; set; } = null!;

        /// <summary>Shown in the roadmap and the unlock celebration (§3.1c).</summary>
        [Required, MaxLength(128)]
        public string DisplayName { get; set; } = null!;
    }
}
