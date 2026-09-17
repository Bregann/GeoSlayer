using GeoSlayer.Domain.DTOs.Retention.Responses;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Interfaces.Api
{
    /// <summary>
    /// The retention systems (Stage 14, DESIGN.md §5.4–5.7, §7.1).
    /// </summary>
    public interface IRetentionService
    {
        /// <summary>Bank cells crossed above walking pace. Returns how many banked.</summary>
        Task<int> BankTransit(
            int playerId, IReadOnlyList<GridCell> cells, double speedMetresPerSecond, CancellationToken ct);

        /// <summary>Walking redeems banked transit nearby, and sweeps anything expired.</summary>
        Task<TransitRedemptionDto> RedeemTransit(
            int playerId, IReadOnlyList<GridCell> walkedCells, CancellationToken ct);

        Task<List<BankedTransitDto>> GetBankedTransit(int playerId, CancellationToken ct);

        /// <summary>Send a worker to a POI the player has personally visited (§5.4).</summary>
        Task<ExpeditionDto> DispatchExpedition(int playerId, int workerId, int poiId, CancellationToken ct);

        Task<List<ExpeditionDto>> GetExpeditions(int playerId, CancellationToken ct);

        /// <summary>
        /// POIs the player has visited, as dispatch destinations — the visit log turned into
        /// a menu. Ordered by distance, since that is what decides the trade-off.
        /// </summary>
        Task<List<ExpeditionDestinationDto>> GetExpeditionDestinations(int playerId, CancellationToken ct);

        /// <summary>Collect returned expeditions, lazily on sync.</summary>
        Task<ExpeditionCollectionDto> CollectExpeditions(int playerId, CancellationToken ct);

        Task<PatrolRouteDto> CreatePatrolRoute(
            int playerId, string name, IReadOnlyList<(double Lat, double Lng)> waypoints, CancellationToken ct);

        Task<List<PatrolRouteDto>> GetPatrolRoutes(int playerId, CancellationToken ct);

        /// <summary>Award upkeep for any circuit this path completed (§5.7).</summary>
        Task<List<PatrolCompletionDto>> CheckPatrolCompletion(
            int playerId, IReadOnlyList<(double Lat, double Lng)> path, CancellationToken ct);

        Task<DistrictStatusDto> GetDistrictStatus(int playerId, CancellationToken ct);

        Task<List<SurgeDto>> GetActiveSurges(double latitude, double longitude, CancellationToken ct);

        /// <summary>Create a surge for a region if none is active. Deterministic per cell per day.</summary>
        Task<SurgeDto?> EnsureSurgeFor(double latitude, double longitude, CancellationToken ct);
    }
}
