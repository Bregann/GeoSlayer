using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// One plinth in the Museum (DESIGN.md §5A), seeded.
    ///
    /// <para>Definitions exist for entries the player has <i>not</i> found, which is the
    /// whole point: §5A.1 is explicit that "empty plinths are a stronger pull than empty
    /// checkboxes". A wing that only listed what you already have would be a receipt.</para>
    /// </summary>
    public class MuseumEntryDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(96)]
        public string Key { get; set; } = null!;

        public MuseumWing Wing { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(512)]
        public string Description { get; set; } = null!;

        public MuseumRarity Rarity { get; set; }

        /// <summary>
        /// How this entry is earned, in words. Shown on an unfound plinth so a gap is a
        /// direction to walk rather than a mystery.
        /// </summary>
        [Required, MaxLength(256)]
        public string UnlockCondition { get; set; } = null!;

        /// <summary>Ordering within a wing.</summary>
        public int SortOrder { get; set; }
    }
}
