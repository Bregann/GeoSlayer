using GeoSlayer.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A bonus for contiguous Claims of <b>varied</b> terrain (DESIGN.md §5.5), seeded.
    ///
    /// <para>Without this, the optimal play is nine identical cells of your best terrain.
    /// With it, you read the actual map — which is the point of a game about real places.</para>
    /// </summary>
    public class DistrictDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string Key { get; set; } = null!;

        [Required, MaxLength(128)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(256)]
        public string Description { get; set; } = null!;

        /// <summary>
        /// Terrain flags that must all be present across the District's Claims.
        ///
        /// Stored as the bitmask union; a District matches when every flag here appears
        /// somewhere in the group.
        /// </summary>
        public TerrainType RequiredTerrains { get; set; }

        /// <summary>Minimum contiguous Claims needed.</summary>
        public int MinimumClaims { get; set; } = 2;

        /// <summary>Fractional output bonus, e.g. 0.15 for +15%.</summary>
        public double OutputBonus { get; set; }
    }
}
