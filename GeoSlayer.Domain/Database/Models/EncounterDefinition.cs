using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A kind of combat encounter (DESIGN.md §5C.1), seeded rather than authored per-player.
    ///
    /// <para>Two kinds, and the difference is lifetime rather than reward: a <b>roaming</b>
    /// encounter spawns at any nearby POI and expires, while a <b>training ground</b> is
    /// fixed to a historic POI and never does.</para>
    ///
    /// <para>The geography rule from §5C.2 lives in the spawner, not here: roaming
    /// encounters must be able to appear at <i>any</i> POI category, because a player with
    /// no castle has to be able to train Combat at all.</para>
    /// </summary>
    public class EncounterDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public required string Key { get; set; }

        [Required, MaxLength(128)]
        public required string Name { get; set; }

        [Required, MaxLength(256)]
        public required string Description { get; set; }

        /// <summary>
        /// Combat level required to meet this encounter.
        ///
        /// <para>Gates <i>which</i> encounters appear, never whether any do — tier 1 sits at
        /// level 1 so a new player always has something to find.</para>
        /// </summary>
        public int MinCombatLevel { get; set; }

        /// <summary>Material tier awarded, matching the Combat ladder's seven rungs.</summary>
        public int Tier { get; set; }

        /// <summary>
        /// True for the permanent kind fixed to historic ground.
        ///
        /// <para>Training grounds are the reliable route (§5C.1) — no waiting, repeatable —
        /// which is the compensating advantage for knowing a ruin nearby.</para>
        /// </summary>
        public bool IsTrainingGround { get; set; }
    }
}
