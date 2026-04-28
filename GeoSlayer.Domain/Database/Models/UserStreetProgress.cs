using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Tracks a player's walking progress along a specific street.
/// </summary>
public class UserStreetProgress
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int PlayerId { get; set; }

    [Required]
    public int StreetId { get; set; }

    /// <summary>
    /// Lowest fraction (0–1) the player has reached on the street LineString.
    /// </summary>
    public double CoveredMinFraction { get; set; }

    /// <summary>
    /// Highest fraction (0–1) the player has reached on the street LineString.
    /// </summary>
    public double CoveredMaxFraction { get; set; }

    /// <summary>
    /// (CoveredMaxFraction − CoveredMinFraction) × 100.
    /// </summary>
    public double PercentComplete { get; set; }

    public bool IsConquered { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;

    [ForeignKey(nameof(StreetId))]
    public virtual Street Street { get; set; } = null!;
}
