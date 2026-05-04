using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.Services;

namespace GeoSlayer.Domain.Interfaces.Api;

public interface IJourneyService
{
    Task<SyncResponse> Sync(SyncRequest request, CancellationToken ct);
    Task<List<CellDto>> GetRevealedCells(int playerId, CancellationToken ct);
}
