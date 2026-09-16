using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A player's purchased rank in one Bonus Point upgrade (DESIGN.md §3.0a).
/// </summary>
public class PlayerUpgrade
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [Required, MaxLength(64)]
    public string UpgradeKey { get; set; } = null!;

    public int Rank { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;
}
