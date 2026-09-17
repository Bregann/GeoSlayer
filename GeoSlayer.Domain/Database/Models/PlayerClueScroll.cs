using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A clue scroll a player is working through (DESIGN.md §5B).
    ///
    /// <para><b>No time limit</b>, deliberately — §5B.3: "clues are for savouring, not
    /// stressing", and they pair with weekend walks. There is no expiry column because there
    /// is no expiry.</para>
    /// </summary>
    public class PlayerClueScroll
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public ClueTier Tier { get; set; }

        /// <summary>Index of the step currently being sought.</summary>
        public int CurrentStep { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime? CompletedUtc { get; set; }

        /// <summary>
        /// One skip per scroll (§5B.3). A clue the player genuinely cannot solve must never
        /// permanently block their only scroll of that tier.
        /// </summary>
        public bool SkipUsed { get; set; }

        public virtual ICollection<ClueStep> Steps { get; set; } = new List<ClueStep>();

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;
    }
}
