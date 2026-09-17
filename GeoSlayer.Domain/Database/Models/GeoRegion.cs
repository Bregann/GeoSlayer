using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A named region, cached per coarse grid cell (Stage 12 Cartography wing).
    ///
    /// <para><b>Why a cache table rather than per-player lookups.</b> Stage 12's design note
    /// warns that Nominatim's usage policy is restrictive for bulk use. One row per coarse
    /// cell, shared by every player, means a region is resolved at most once ever — and the
    /// second player to walk there triggers no lookup at all, which is what criterion 8
    /// requires.</para>
    /// </summary>
    public class GeoRegion
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Coarse grid cell (~5 km), matching the POI preloader's grid.</summary>
        public double CellLat { get; set; }
        public double CellLng { get; set; }

        /// <summary>Stable key for the Museum entry, e.g. <c>region:county-durham</c>.</summary>
        [Required, MaxLength(96)]
        public string RegionKey { get; set; } = null!;

        [Required, MaxLength(128)]
        public string Name { get; set; } = null!;

        /// <summary>County, country, or whatever the source could determine.</summary>
        [Required, MaxLength(32)]
        public string Kind { get; set; } = "Region";

        public DateTime ResolvedAtUtc { get; set; }
    }
}
