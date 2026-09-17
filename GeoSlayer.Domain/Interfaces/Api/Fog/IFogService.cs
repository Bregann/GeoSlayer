using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Interfaces.Api.Fog
{
    public interface IFogService
    {
        /// <summary>
        /// Reveal every cell swept by the given path, oldest position first.
        /// </summary>
        Task<FogRevealResult> Reveal(int playerId, IReadOnlyList<SyncPosition> path, CancellationToken ct);
        Task<List<CellDto>> GetAllRevealed(int playerId, CancellationToken ct);
    }
}
