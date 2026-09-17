using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Idle;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>
    /// Claims and workers (Stage 05).
    ///
    /// As elsewhere, the player comes from the JWT and never from the request.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class IdleController(
        AppDbContext db,
        IWorkerService workers,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ClaimDto>>> GetClaims(CancellationToken ct) =>
            Ok(await workers.GetClaims(await CurrentPlayerId(ct), ct));

        /// <summary>Whether the 3×3 block centred here can be claimed, and what it costs.</summary>
        [HttpGet]
        public async Task<ActionResult<ClaimEligibilityDto>> CheckEligibility(
            [FromQuery] int gridLat, [FromQuery] int gridLng, CancellationToken ct) =>
            Ok(await workers.CheckClaimEligibility(await CurrentPlayerId(ct), gridLat, gridLng, ct));

        [HttpPost]
        public async Task<ActionResult<ClaimDto>> CreateClaim(
            [FromBody] CreateClaimRequest request, CancellationToken ct) =>
            Ok(await workers.CreateClaim(
                await CurrentPlayerId(ct), request.GridLat, request.GridLng, request.Name, ct));

        [HttpGet]
        public async Task<ActionResult<List<WorkerDto>>> GetWorkers(CancellationToken ct) =>
            Ok(await workers.GetWorkers(await CurrentPlayerId(ct), ct));

        [HttpPost]
        public async Task<ActionResult<WorkerDto>> HireWorker(CancellationToken ct) =>
            Ok(await workers.HireWorker(await CurrentPlayerId(ct), ct));

        /// <summary>
        /// Station a worker and set what it trains. Any unlocked skill on any Claim —
        /// terrain changes the rate, never the eligibility (§5.2).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkerDto>> AssignWorker(
            [FromQuery] int id, [FromBody] AssignWorkerRequest request, CancellationToken ct) =>
            Ok(await workers.AssignWorker(
                await CurrentPlayerId(ct), id, request.ClaimId, request.Skill, ct));

        private async Task<int> CurrentPlayerId(CancellationToken ct)
        {
            var userId = userContextHelper.GetUserId();

            var playerId = await db.Players
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);

            return playerId ?? throw new NotFoundException("Player not found");
        }
    }

    public class CreateClaimRequest
    {
        public int GridLat { get; set; }
        public int GridLng { get; set; }
        public string? Name { get; set; }
    }

    public class AssignWorkerRequest
    {
        public int? ClaimId { get; set; }
        public SkillType? Skill { get; set; }
    }
}
