using GeoSlayer.Domain.DTOs.Materials.Responses;
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

    public List<NearbyPoiDto> NearbyPois { get; set; } = [];
}
