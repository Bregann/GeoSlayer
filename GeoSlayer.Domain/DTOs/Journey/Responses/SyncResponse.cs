using GeoSlayer.Domain.Services;

namespace GeoSlayer.Domain.DTOs.Journey.Responses;

public class SyncResponse
{
    public List<CellDto> NewCells { get; set; } = [];
    public int Xp { get; set; }
    public int Level { get; set; }
    public List<NearbyPoiDto> NearbyPois { get; set; } = [];
}
