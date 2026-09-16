using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// One possible drop from a terrain's table (Stage 03 task 4), seeded.
///
/// There is no separate <c>DropTable</c> row: a table is just the set of entries sharing a
/// <see cref="Terrain"/>, and an extra parent table would add a join without adding
/// meaning.
/// </summary>
public class DropTableEntry
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The terrain this entry belongs to. <see cref="TerrainType.Open"/> is the base
    /// table every cell can roll, which is what guarantees no cell yields nothing.
    /// </summary>
    public TerrainType Terrain { get; set; }

    public int MaterialId { get; set; }

    /// <summary>Relative weight within its tier band. Higher is likelier.</summary>
    public int Weight { get; set; } = 1;

    public int MinQuantity { get; set; } = 1;

    public int MaxQuantity { get; set; } = 1;

    [ForeignKey(nameof(MaterialId))]
    public virtual Material Material { get; set; } = null!;
}
