using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Represents an OSM street path stored as a PostGIS LineString.
/// </summary>
public class Street
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// OpenStreetMap way ID – used for deduplication during imports.
    /// </summary>
    [Required]
    public long OsmId { get; set; }

    [Required]
    public string Name { get; set; } = "";

    /// <summary>
    /// The geographic path of the street (SRID 4326 = WGS 84).
    /// </summary>
    [Required]
    [Column(TypeName = "geometry (linestring, 4326)")]
    public LineString Path { get; set; } = null!;
}
