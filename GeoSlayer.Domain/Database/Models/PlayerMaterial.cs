using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>How much of one material a player is holding (DESIGN.md §4.1).</summary>
public class PlayerMaterial
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    public int MaterialId { get; set; }

    public long Quantity { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;

    [ForeignKey(nameof(MaterialId))]
    public virtual Material Material { get; set; } = null!;
}
