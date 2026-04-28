using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Tracks which geographic grid cells have already had their streets
/// imported from OSM.  The world is divided into cells of a fixed size
/// (currently 0.05° ≈ 5–6 km).  Each cell is identified by the
/// truncated lat/lng of its south-west corner.
/// </summary>
public class ImportedRegion
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Grid-cell latitude key (south edge, truncated to cell size).</summary>
    public double CellLat { get; set; }

    /// <summary>Grid-cell longitude key (west edge, truncated to cell size).</summary>
    public double CellLng { get; set; }

    /// <summary>When the streets for this cell were last fetched.</summary>
    public DateTime ImportedAtUtc { get; set; }

    /// <summary>Number of streets that were imported for this cell.</summary>
    public int StreetCount { get; set; }
}
