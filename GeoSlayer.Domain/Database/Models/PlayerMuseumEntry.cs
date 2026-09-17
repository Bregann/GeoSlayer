using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A plinth a player has filled (DESIGN.md §5A).
    ///
    /// <para><see cref="AcquiredAtPoiId"/> and <see cref="FirstAcquiredUtc"/> are what make
    /// this "a diary rather than a tally" — "found at Durham Cathedral, 3 May" is the entry;
    /// a count is not. Stage 12 says explicitly not to omit them.</para>
    ///
    /// <para>Entries are <b>permanent</b>: nothing in the codebase deletes one. §5A frames
    /// permanence as the point — the Museum is the only system that never decays, caps or
    /// resets.</para>
    /// </summary>
    public class PlayerMuseumEntry
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        [Required, MaxLength(96)]
        public string EntryKey { get; set; } = null!;

        /// <summary>When this was first found. Never updated by a later find.</summary>
        public DateTime FirstAcquiredUtc { get; set; }

        /// <summary>
        /// Total found. <b>First-find is what counts</b> for completion (§5A.3); this is for
        /// the diary and for donating duplicates.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Where it was found, when that is a POI. The heart of the diary.</summary>
        public int? AcquiredAtPoiId { get; set; }

        /// <summary>Human-readable place, cached so a deleted POI does not erase the memory.</summary>
        [MaxLength(128)]
        public string? AcquiredAtName { get; set; }

        /// <summary>Duplicates already donated, so the same spare cannot be donated twice.</summary>
        public int DonatedQuantity { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;
    }
}
