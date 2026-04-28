using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Represents a player in the game world with a current geographic location.
/// </summary>
public class Player
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// The player's current GPS position (SRID 4326 = WGS 84).
    /// </summary>
    [Required]
    [Column(TypeName = "geometry (point, 4326)")]
    public Point Location { get; set; } = null!;

    public int Xp { get; set; }

    public int Level { get; set; } = 1;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
