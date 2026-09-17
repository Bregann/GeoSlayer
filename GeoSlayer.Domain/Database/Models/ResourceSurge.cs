using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A temporary localised buff (DESIGN.md §5.6).
///
/// <para>Frequent, small, clearly signposted. <b>A player who ignores every surge must
/// still progress fine</b> — a nudge, not an obligation, which is why the multiplier is
/// modest and nothing expires unused.</para>
///
/// <para>Shared across players rather than per-player: a surge is a property of a place,
/// so two players in the same region see the same one.</para>
/// </summary>
public class ResourceSurge
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Coarse region cell this surge covers, matching the POI preloader grid.</summary>
    public double CellLat { get; set; }
    public double CellLng { get; set; }

    /// <summary>The terrain whose yield is boosted, or null when the surge targets a skill.</summary>
    public TerrainType? TargetTerrain { get; set; }

    /// <summary>The skill whose yield is boosted, or null when the surge targets terrain.</summary>
    public SkillType? TargetSkill { get; set; }

    /// <summary>Yield multiplier. Kept modest — a nudge, not an obligation.</summary>
    public double Multiplier { get; set; } = 1.5;

    [Required, MaxLength(128)]
    public string Description { get; set; } = null!;

    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
}
