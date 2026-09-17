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
