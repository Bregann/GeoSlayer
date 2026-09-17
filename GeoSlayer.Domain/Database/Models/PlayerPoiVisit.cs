using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// One player's history with one POI (Stage 04 task 2).
    ///
    /// <para>Stage 12 (Museum) and Stage 14 (Expeditions) both read this log, so it records
    /// first-visit time separately from the running count rather than deriving one from the
    /// other — a decayed count must not lose when the player first found the place.</para>
    /// </summary>
    public class PlayerPoiVisit
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public int PoiId { get; set; }

        /// <summary>
        /// Visits counted for decay purposes (§3.4). Reduced by one charge per 24 hours
        /// elapsed, so this is not a lifetime total — <see cref="TotalVisits"/> is.
        /// </summary>
        public int VisitCount { get; set; }

        /// <summary>Lifetime visits, never decayed. For the Museum and statistics.</summary>
        public int TotalVisits { get; set; }

        public DateTime LastVisitUtc { get; set; }

        public DateTime FirstVisitUtc { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;

        [ForeignKey(nameof(PoiId))]
        public virtual PointOfInterest Poi { get; set; } = null!;
    }
}
