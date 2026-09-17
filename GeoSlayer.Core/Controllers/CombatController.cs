using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Combat.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Combat;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>Combat encounters (Stage 16, DESIGN.md §5C).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class CombatController(
        AppDbContext db,
        IEncounterService encounters,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>Encounters waiting nearby, roaming and training grounds alike.</summary>
        [HttpGet]
        public async Task<ActionResult<List<EncounterDto>>> GetEncounters(CancellationToken ct) =>
            Ok(await encounters.GetEncounters(await CurrentPlayerId(ct), ct));

        /// <summary>Fight one. Range is checked server-side (§7.2).</summary>
        [HttpPost]
        public async Task<ActionResult<EncounterResultDto>> Resolve(
            [FromQuery] int id, CancellationToken ct) =>
            Ok(await encounters.Resolve(await CurrentPlayerId(ct), id, ct));

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
}
