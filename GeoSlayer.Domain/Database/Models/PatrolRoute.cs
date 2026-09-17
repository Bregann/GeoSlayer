using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A saved loop the player walks regularly (DESIGN.md §5.7).
    ///
    /// <para>Solves the problem that diminishing returns make the daily walk progressively
    /// less rewarding — and the daily walk is the habit the whole game depends on.</para>
    ///
    /// <para>The split that makes it work: <b>novelty stays the only route to progress;
    /// routine becomes the route to maintenance.</b> Re-walking grants no cell XP (it is not
    /// new ground) but completing the circuit supplies worker upkeep, which otherwise demands
    /// constant novelty to sustain.</para>
    /// </summary>
    public class PatrolRoute
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        [Required, MaxLength(64)]
        public string Name { get; set; } = null!;

        public DateTime CreatedUtc { get; set; }

        public DateTime? LastCompletedUtc { get; set; }

        public int CompletionCount { get; set; }

        public virtual ICollection<PatrolWaypoint> Waypoints { get; set; } = new List<PatrolWaypoint>();

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;
    }
}
