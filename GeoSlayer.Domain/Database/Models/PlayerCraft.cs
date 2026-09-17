using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A queued craft (DESIGN.md §4.2, Stage 06 task 2).
    ///
    /// <para>Completion is evaluated <b>lazily on next sync</b>, exactly like worker accrual
    /// (§5.3) — there is deliberately no recurring job. Inputs are consumed at queue time
    /// rather than completion, which is what prevents queueing everything and spending the
    /// materials elsewhere before it finishes.</para>
    /// </summary>
    public class PlayerCraft
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public int RecipeId { get; set; }

        [Required, MaxLength(64)]
        public string RecipeKey { get; set; } = null!;

        public DateTime StartedUtc { get; set; }

        public DateTime CompletesUtc { get; set; }

        public bool Collected { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;

        [ForeignKey(nameof(RecipeId))]
        public virtual Recipe Recipe { get; set; } = null!;
    }
}
