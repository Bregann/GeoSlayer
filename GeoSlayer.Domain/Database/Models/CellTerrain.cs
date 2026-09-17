using GeoSlayer.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// Cached terrain for one grid cell, shared by every player (Stage 03 task 3).
    ///
    /// Deliberately keyed on the grid cell rather than hung off <c>RevealedCell</c>: terrain is
    /// a property of the world, not of one player's visit, so the second player to walk a cell
    /// must reuse this row rather than reclassifying it.
    /// </summary>
    public class CellTerrain
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int GridLat { get; set; }

        public int GridLng { get; set; }

        public TerrainType Terrain { get; set; }

        public DateTime ClassifiedAtUtc { get; set; }
    }
}
