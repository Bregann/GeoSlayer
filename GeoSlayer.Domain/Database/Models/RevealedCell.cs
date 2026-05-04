using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A single fog-of-war cell that a player has revealed by walking near it.
/// Cells use a fixed lat/lng grid (~150 m resolution).
/// </summary>
public class RevealedCell
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int PlayerId { get; set; }

    /// <summary>Grid row index: floor(latitude  / CellSize).</summary>
    public int GridLat { get; set; }

    /// <summary>Grid column index: floor(longitude / CellSize).</summary>
    public int GridLng { get; set; }

    public DateTime RevealedAtUtc { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;
}
