using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses;

/// <summary>A cell passed through above walking pace, awaiting redemption (§7.1).</summary>
public class BankedTransitDto
{
    public int GridLat { get; set; }
    public int GridLng { get; set; }

    /// <summary>Bounds, so the map can draw it as a distinct visual state.</summary>
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }

    /// <summary>Under 1 for a cycling-pace cell, which banked only partially.</summary>
    public double Weight { get; set; }

    /// <summary>An opportunity, not an obligation — it lapses if unclaimed.</summary>
    public DateTime ExpiresUtc { get; set; }
}

public class TransitRedemptionDto
{
    public int Redeemed { get; set; }
    public int CellsRevealed { get; set; }
    public int Remaining { get; set; }

    /// <summary>Banked cells that lapsed before being walked.</summary>
    public int Expired { get; set; }
}

public class ExpeditionDto
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public int PoiId { get; set; }
    public string PoiName { get; set; } = null!;
    public DateTime DispatchedUtc { get; set; }
    public DateTime ReturnsUtc { get; set; }
    public double DistanceMetres { get; set; }
    public bool HasReturned { get; set; }
    public double SecondsRemaining { get; set; }
}

/// <summary>
/// A POI the player has visited, as an expedition destination (§5.4).
///
/// <para>This is the whole point of the system: every trip already taken is a permanent
/// asset. The list is the visit log from Stage 04, which is what made expeditions "nearly
/// free to implement".</para>
/// </summary>
public class ExpeditionDestinationDto
{
    public int PoiId { get; set; }
    public string Name { get; set; } = null!;
    public SkillType Skill { get; set; }
    public string SkillName { get; set; } = null!;

    /// <summary>When the player first found it — the reason it is on this list.</summary>
    public DateTime FirstVisitUtc { get; set; }

    public int TotalVisits { get; set; }

    /// <summary>Distance from the player's last verified position.</summary>
    public double DistanceMetres { get; set; }

    /// <summary>Round-trip duration, which scales with that distance.</summary>
    public double DurationHours { get; set; }

    /// <summary>Estimated material units, so the trade-off is visible before dispatching.</summary>
    public int EstimatedMaterials { get; set; }

    /// <summary>False when a worker is already there.</summary>
    public bool IsAvailable { get; set; }
}

public class ExpeditionCollectionDto
{
    public bool HasCollection { get; set; }
    public List<string> Returned { get; set; } = [];
    public List<MaterialGainDto> Materials { get; set; } = [];
    public long SkillXpEarned { get; set; }
}

public class PatrolWaypointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class PatrolRouteDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int WaypointCount { get; set; }
    public int CompletionCount { get; set; }
    public DateTime? LastCompletedUtc { get; set; }
    public int UpkeepReward { get; set; }
    public List<PatrolWaypointDto> Waypoints { get; set; } = [];
}

public class PatrolCompletionDto
{
    public int RouteId { get; set; }
    public string Name { get; set; } = null!;
    public int UpkeepAwarded { get; set; }
    public List<MaterialGainDto> Materials { get; set; } = [];
}

public class DistrictStatusDto
{
    public string? DistrictKey { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double OutputBonus { get; set; }
    public int ClaimCount { get; set; }

    /// <summary>
    /// The District this player is closest to forming, when they have none.
    ///
    /// <para>Without this, "no District" is a dead end: §5.5 deliberately requires varied
    /// terrain, so a player whose Claims are all one terrain has no way to learn that from
    /// the status alone. Named so the app can say what is actually missing.</para>
    /// </summary>
    public string? NearestName { get; set; }

    /// <summary>Terrains the nearest District needs that the player has not claimed.</summary>
    public List<string> MissingTerrains { get; set; } = [];

    /// <summary>Further Claims the nearest District needs, beyond terrain variety.</summary>
    public int MissingClaims { get; set; }
}

public class SurgeDto
{
    public int Id { get; set; }
    public string Description { get; set; } = null!;
    public TerrainType? TargetTerrain { get; set; }
    public SkillType? TargetSkill { get; set; }
    public double Multiplier { get; set; }
    public DateTime EndsUtc { get; set; }
}
