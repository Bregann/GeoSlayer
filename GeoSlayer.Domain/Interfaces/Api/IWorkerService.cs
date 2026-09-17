using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api
{
    /// <summary>
    /// Claims, workers and offline accrual (Stage 05, DESIGN.md §5.1–5.3).
    /// </summary>
    public interface IWorkerService
    {
        /// <summary>
        /// Collect everything workers produced while away, lazily (§5.3).
        ///
        /// Called on sync. Returns an empty result when nothing meaningful accrued, so the
        /// welcome-back screen can stay quiet rather than nagging.
        /// </summary>
        Task<OfflineAccrualDto> CollectOfflineAccrual(int playerId, CancellationToken ct);

        /// <summary>Whether the 3×3 block centred here is fully revealed and claimable.</summary>
        Task<ClaimEligibilityDto> CheckClaimEligibility(
            int playerId, int centreGridLat, int centreGridLng, CancellationToken ct);

        /// <summary>Claim territory. Costs materials and is permanent (§5.1).</summary>
        Task<ClaimDto> CreateClaim(
            int playerId, int centreGridLat, int centreGridLng, string? name, CancellationToken ct);

        Task<List<ClaimDto>> GetClaims(int playerId, CancellationToken ct);

        Task<List<WorkerDto>> GetWorkers(int playerId, CancellationToken ct);

        /// <summary>
        /// Station a worker on a Claim and set what it trains.
        ///
        /// Any unlocked skill on any Claim — terrain changes the rate, never the
        /// eligibility (§5.2).
        /// </summary>
        Task<WorkerDto> AssignWorker(
            int playerId, int workerId, int? claimId, SkillType? skill, CancellationToken ct);

        /// <summary>
        /// Grant a worker, up to the player's slot capacity (one plus Worker Slot ranks).
        /// </summary>
        Task<WorkerDto> HireWorker(int playerId, CancellationToken ct);
    }
}
