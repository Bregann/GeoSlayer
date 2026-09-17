using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// An encounter waiting at a POI for one player (DESIGN.md §5C).
    ///
    /// <para>Per-player rather than shared, unlike a Resource Surge: two people can each
    /// fight the garrison at the same ruin without racing for it. The <i>spawn</i> is
    /// deterministic per cell and window, so they still see the same encounters offered in
    /// the same place — it is the resolution that is private.</para>
    /// </summary>
    public class PlayerEncounter
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }
        public virtual Player Player { get; set; } = null!;

        [Required, MaxLength(64)]
        public required string DefinitionKey { get; set; }

        public int PoiId { get; set; }
        public virtual PointOfInterest Poi { get; set; } = null!;

        public DateTime SpawnedUtc { get; set; }

        /// <summary>
        /// When a roaming encounter lapses, or null for a training ground.
        ///
        /// <para>Null is the whole distinction: permanence is what makes historic ground the
        /// reliable route (§5C.1). Missing a roaming encounter costs nothing (§7.4).</para>
        /// </summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>Set once fought, win or lose. A resolved encounter is never re-fought.</summary>
        public DateTime? ResolvedUtc { get; set; }

        /// <summary>Whether the player won. Losing costs time, never materials (§5C.3).</summary>
        public bool? Won { get; set; }
    }
}
