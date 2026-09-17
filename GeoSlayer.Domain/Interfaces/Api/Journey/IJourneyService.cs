using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.DTOs.Journey.Responses;
using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Interfaces.Api.Journey
{
    public interface IJourneyService
    {
        Task<SyncResponse> Sync(SyncRequest request, CancellationToken ct);
        Task<List<CellDto>> GetRevealedCells(CancellationToken ct);

        /// <summary>
        /// Visit a POI in range (Stage 04 task 2). Range is validated server-side against the
        /// player's last verified position — the client never supplies one.
        /// </summary>
        Task<PoiVisitResultDto> VisitPoi(int poiId, CancellationToken ct);
    }
}
