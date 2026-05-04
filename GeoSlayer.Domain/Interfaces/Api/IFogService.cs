using GeoSlayer.Domain.Services;

namespace GeoSlayer.Domain.Interfaces.Api;

public interface IFogService
{
    Task<FogRevealResult> Reveal(int playerId, double latitude, double longitude, long timestampMs, CancellationToken ct);
    Task<List<CellDto>> GetAllRevealed(int playerId, CancellationToken ct);
}
