using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A real-world Point of Interest pulled from OpenStreetMap,
/// mapped to a game skill that players can train by visiting it.
/// </summary>
public class PointOfInterest
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>OSM node/way ID for deduplication.</summary>
    [Required]
    public long OsmId { get; set; }

    /// <summary>OSM element type ("node" or "way").</summary>
    [Required]
    public string OsmType { get; set; } = "node";

    [Required]
    public string Name { get; set; } = "";

    /// <summary>The game skill this POI lets players train.</summary>
    [Required]
    public SkillType Skill { get; set; }

    /// <summary>Location as a WGS 84 point.</summary>
    [Required]
    [Column(TypeName = "geometry (point, 4326)")]
    public Point Location { get; set; } = null!;

    /// <summary>
    /// How much XP per visit (can vary by POI importance).
    /// Default is 10; larger/rarer POIs can give more.
    /// </summary>
    public int XpReward { get; set; } = 10;
}
