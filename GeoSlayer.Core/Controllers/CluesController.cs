using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Clues.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Clues;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>Clue scrolls (Stage 13, DESIGN.md §5B).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class CluesController(
        AppDbContext db,
        IClueService clues,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ClueScrollDto>>> GetScrolls(CancellationToken ct) =>
            Ok(await clues.GetScrolls(await CurrentPlayerId(ct), ct));

        [HttpPost]
        public async Task<ActionResult<ClueScrollDto>> Generate([FromQuery] ClueTier tier, CancellationToken ct)
        {
            var scroll = await clues.GenerateScroll(await CurrentPlayerId(ct), tier, ct);

            // Too little territory to place steps. Better a clear "not yet" than a scroll
            // whose steps sit somewhere the player has never been.
            return scroll is null
                ? BadRequest("Explore a little more ground first — there is nowhere to send you yet.")
                : Ok(scroll);
        }

        /// <summary>
        /// Attempt the current step. Arrival is checked server-side against the player's last
        /// verified position; the request carries no coordinates.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ClueProgressDto>> Attempt([FromQuery] int id, CancellationToken ct) =>
            Ok(await clues.AttemptStep(await CurrentPlayerId(ct), id, ct));

        [HttpPost]
        public async Task<ActionResult<ClueProgressDto>> Skip([FromQuery] int id, CancellationToken ct) =>
            Ok(await clues.SkipStep(await CurrentPlayerId(ct), id, ct));

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
