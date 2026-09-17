using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Retention.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Retention;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>The retention systems (Stage 14, DESIGN.md §5.4–5.7, §7.1).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class RetentionController(
        AppDbContext db,
        IRetentionService retention,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>Banked transit, for drawing as a distinct state on the map.</summary>
        [HttpGet]
        public async Task<ActionResult<List<BankedTransitDto>>> GetTransit(CancellationToken ct) =>
            Ok(await retention.GetBankedTransit(await CurrentPlayerId(ct), ct));

        [HttpGet]
        public async Task<ActionResult<List<ExpeditionDto>>> GetExpeditions(CancellationToken ct) =>
            Ok(await retention.GetExpeditions(await CurrentPlayerId(ct), ct));

        /// <summary>POIs you have visited, as dispatch destinations.</summary>
        [HttpGet]
        public async Task<ActionResult<List<ExpeditionDestinationDto>>> GetDestinations(CancellationToken ct) =>
            Ok(await retention.GetExpeditionDestinations(await CurrentPlayerId(ct), ct));

        /// <summary>Send a worker to a POI you have personally visited.</summary>
        [HttpPost]
        public async Task<ActionResult<ExpeditionDto>> Dispatch(
            [FromBody] DispatchRequest request, CancellationToken ct) =>
            Ok(await retention.DispatchExpedition(
                await CurrentPlayerId(ct), request.WorkerId, request.PoiId, ct));

        [HttpGet]
        public async Task<ActionResult<List<PatrolRouteDto>>> GetPatrols(CancellationToken ct) =>
            Ok(await retention.GetPatrolRoutes(await CurrentPlayerId(ct), ct));

        [HttpPost]
        public async Task<ActionResult<PatrolRouteDto>> CreatePatrol(
            [FromBody] CreatePatrolRequest request, CancellationToken ct) =>
            Ok(await retention.CreatePatrolRoute(
                await CurrentPlayerId(ct),
                request.Name,
                request.Waypoints.Select(w => (w.Latitude, w.Longitude)).ToList(),
                ct));

        [HttpGet]
        public async Task<ActionResult<DistrictStatusDto>> GetDistrict(CancellationToken ct) =>
            Ok(await retention.GetDistrictStatus(await CurrentPlayerId(ct), ct));

        /// <summary>Surges active where the caller last synced.</summary>
        [HttpGet]
        public async Task<ActionResult<List<SurgeDto>>> GetSurges(CancellationToken ct)
        {
            var playerId = await CurrentPlayerId(ct);

            var position = await db.Players
                .Where(p => p.Id == playerId)
                .Select(p => new { p.LastLatitude, p.LastLongitude, p.LastSyncAtUtc })
                .FirstAsync(ct);

            if (position.LastSyncAtUtc is null)
            {
                return Ok(new List<SurgeDto>());
            }

            return Ok(await retention.GetActiveSurges(position.LastLatitude, position.LastLongitude, ct));
        }

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

    public class DispatchRequest
    {
        public int WorkerId { get; set; }
        public int PoiId { get; set; }
    }

    public class CreatePatrolRequest
    {
        public string Name { get; set; } = "Patrol";
        public List<PatrolWaypointDto> Waypoints { get; set; } = [];
    }
}
