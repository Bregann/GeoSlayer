using GeoSlayer.Domain.DTOs.Crafting.Responses;
using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Services;

namespace GeoSlayer.Domain.DTOs.Journey.Responses;

public class SyncResponse
{
    public List<CellDto> NewCells { get; set; } = [];
    public long Xp { get; set; }
    public int Level { get; set; }
    /// <summary>Ladder rungs crossed by this sync, so the app can celebrate them (§3.1c).</summary>
    public List<UnlockEventDto> Unlocks { get; set; } = [];

    /// <summary>Bonus Points granted by this sync's level-ups.</summary>
    public int BonusPointsGranted { get; set; }

    /// <summary>Materials gained this sync, so the app can show pickups (Stage 03).</summary>
    public List<MaterialGainDto> Materials { get; set; } = [];

    /// <summary>Per-skill XP earned this sync (Stage 04).</summary>
    public List<SkillTrainingDto> SkillTraining { get; set; } = [];

    /// <summary>
    /// What workers produced while away (Stage 05 task 4). Null when nothing accrued,
    /// so the welcome-back screen never nags.
    /// </summary>
    public OfflineAccrualDto? OfflineAccrual { get; set; }

    /// <summary>Crafts that finished while away, or null when none did (Stage 06).</summary>
    public CraftCollectionDto? CraftCollection { get; set; }

    public List<NearbyPoiDto> NearbyPois { get; set; } = [];
}
