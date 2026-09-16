using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Idle.Responses;

public class ClaimDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CentreGridLat { get; set; }
    public int CentreGridLng { get; set; }
    public int Size { get; set; }
    public TerrainType TerrainProfile { get; set; }

    /// <summary>Terrain flags as names, so the app need not decode a bitmask.</summary>
    public List<string> Terrains { get; set; } = [];

    public DateTime ClaimedUtc { get; set; }

    /// <summary>Bounding box, for outlining the Claim on the map.</summary>
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }

    public int WorkerCount { get; set; }
}

public class ClaimEligibilityDto
{
    public bool IsEligible { get; set; }

    /// <summary>Why not, when <see cref="IsEligible"/> is false.</summary>
    public string? Reason { get; set; }

    /// <summary>How many of the 3×3 block's cells are revealed.</summary>
    public int RevealedCells { get; set; }
    public int RequiredCells { get; set; }

    /// <summary>Materials the claim would cost.</summary>
    public List<MaterialCostDto> Cost { get; set; } = [];

    public bool CanAfford { get; set; }

    /// <summary>Claims held against the density and slot caps.</summary>
    public int ClaimsHeld { get; set; }
    public int ClaimLimit { get; set; }
}

public class MaterialCostDto
{
    public int MaterialId { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Quantity { get; set; }
    public long Held { get; set; }
}

public class WorkerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Tier { get; set; }
    public int? ClaimId { get; set; }
    public string? ClaimName { get; set; }
    public SkillType? AssignedSkill { get; set; }
    public string? AssignedSkillName { get; set; }

    public DateTime LastCollectedAtUtc { get; set; }

    /// <summary>Current XP per hour, including tier and terrain multipliers.</summary>
    public double XpPerHour { get; set; }
    public double MaterialsPerHour { get; set; }

    /// <summary>1.0 on mismatched terrain — base rate, never zero (§5.2).</summary>
    public double TerrainMultiplier { get; set; }

    /// <summary>True when this worker's skill suits its Claim's terrain.</summary>
    public bool TerrainMatches { get; set; }

    /// <summary>When accrual stops paying, so the player can plan a return.</summary>
    public DateTime CapReachedAtUtc { get; set; }
    public bool IsAtCap { get; set; }
    public bool IsIdle { get; set; }
}

/// <summary>
/// The welcome-back payload (Stage 05 task 4) — what accrued while away.
/// </summary>
public class OfflineAccrualDto
{
    /// <summary>False when nothing meaningful accrued, so the screen stays quiet.</summary>
    public bool HasAccrual { get; set; }

    public double HoursAccrued { get; set; }

    /// <summary>True when the cap was hit, so the app can suggest a longer cap.</summary>
    public bool WasCapped { get; set; }

    public double OfflineCapHours { get; set; }

    public List<MaterialGainDto> Materials { get; set; } = [];
    public List<SkillAccrualDto> Skills { get; set; } = [];
    public List<UnlockEventDto> Unlocks { get; set; } = [];

    public long AdventurerXpEarned { get; set; }
    public int BonusPointsGranted { get; set; }
}

public class SkillAccrualDto
{
    public SkillType SkillType { get; set; }
    public string Name { get; set; } = null!;
    public long XpEarned { get; set; }
    public int Level { get; set; }
    public bool LevelledUp { get; set; }
}
