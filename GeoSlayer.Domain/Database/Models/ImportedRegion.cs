using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Tracks which Overpass grid cells have had their POIs imported,
/// so we don't re-fetch on every sync.
/// </summary>
public class ImportedRegion
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public double CellLat { get; set; }
    public double CellLng { get; set; }
    public DateTime ImportedAtUtc { get; set; }
}
