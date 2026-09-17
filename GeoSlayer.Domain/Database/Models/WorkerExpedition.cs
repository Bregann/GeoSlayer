using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A worker sent to a POI the player has personally visited (DESIGN.md §5.4).
///
/// <para>Solves the problem that POI value expires: a cathedral visited on holiday is
/// otherwise dead to you forever, which is a shame given it is the most memorable thing
/// in the game. An expedition turns <b>every trip the player has ever taken into a
/// permanent asset</b>.</para>
///
/// <para>It also softens the rural/urban imbalance from a new angle — a rural player who
/// visits a city once gains lasting access to urban materials.</para>
/// </summary>
public class WorkerExpedition
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    public int WorkerId { get; set; }

    public int PoiId { get; set; }

    /// <summary>Cached so a deleted POI does not erase where the worker went.</summary>
    [Required, MaxLength(128)]
    public string PoiName { get; set; } = null!;

    public DateTime DispatchedUtc { get; set; }

    /// <summary>Scales with real-world distance — distant POIs are high-value, low-frequency.</summary>
    public DateTime ReturnsUtc { get; set; }

    public double DistanceMetres { get; set; }

    public bool Collected { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;

    [ForeignKey(nameof(WorkerId))]
    public virtual Worker Worker { get; set; } = null!;

    [ForeignKey(nameof(PoiId))]
    public virtual PointOfInterest Poi { get; set; } = null!;
}
