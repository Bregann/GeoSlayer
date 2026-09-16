using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Persistent, productive territory (DESIGN.md §5.1).
///
/// <para>Revealed cells are ephemeral progress; a Claim is what makes exploration have a
/// goal state — you are not just painting the map, you are annexing it. A Claim is a
/// rectangle of <see cref="RevealedCell"/>s, so it composes with the existing grid rather
/// than needing new geometry.</para>
/// </summary>
public class Claim
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    /// <summary>Centre cell of the claimed block.</summary>
    public int CentreGridLat { get; set; }
    public int CentreGridLng { get; set; }

    /// <summary>Edge length in cells. 3 means the 3×3 block centred on the coordinates.</summary>
    public int Size { get; set; } = 3;

    /// <summary>
    /// Union of the constituent cells' terrain flags, cached at claim time.
    ///
    /// Determines what the Claim produces well — but never what it can produce at all
    /// (§5.2). Terrain multiplies; it does not gate.
    /// </summary>
    public TerrainType TerrainProfile { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = null!;

    public DateTime ClaimedUtc { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;
}
