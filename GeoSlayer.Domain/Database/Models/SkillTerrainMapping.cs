using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// How much a terrain trains a skill while walking (Stage 04 task 1), seeded.
    ///
    /// <para><b>Terrain is a multiplier, never a gate</b> (DESIGN.md §5.2). A skill with no
    /// matching terrain nearby still trains at base rate — see
    /// <see cref="SkillTerrainMapping"/> rows for <see cref="TerrainType.Open"/>. Anything
    /// that turns a missing row into zero XP rebuilds the geographic lockout.</para>
    /// </summary>
    public class SkillTerrainMapping
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public SkillType SkillType { get; set; }

        public TerrainType Terrain { get; set; }

        /// <summary>Skill XP granted per newly revealed cell of this terrain.</summary>
        public double XpPerCell { get; set; } = 1;

        /// <summary>Multiplies the quantity of materials this terrain yields.</summary>
        public double YieldMultiplier { get; set; } = 1;
    }
}
