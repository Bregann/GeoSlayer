using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A cell the player passed through above walking pace (DESIGN.md §7.1).
    ///
    /// <para>"Places you passed but did not see." A commuter's journey is not wasted — it
    /// becomes a <b>reason to walk</b>, seeded along routes they already travel. §7.1 calls
    /// this converting the game's biggest exploit into its best retention mechanic.</para>
    ///
    /// <para>Unredeemed transit decays after about a week, so it is an opportunity rather
    /// than an obligation.</para>
    /// </summary>
    public class BankedTransit
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public int GridLat { get; set; }
        public int GridLng { get; set; }

        public DateTime BankedUtc { get; set; }

        /// <summary>
        /// How much of a reveal this is worth when redeemed. A cycling-pace cell banks
        /// partially rather than being all-or-nothing, which is how §7.1 handles cyclists
        /// "without a special case".
        /// </summary>
        public double Weight { get; set; } = 1;

        public bool Redeemed { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;
    }
}
