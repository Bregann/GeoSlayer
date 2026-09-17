using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GeoSlayer.Domain.Interfaces.Api.Museum;

namespace GeoSlayer.Core.Controllers
{
    /// <summary>The Museum (Stage 12, DESIGN.md §5A).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class MuseumController(
        AppDbContext db,
        IMuseumService museum,
        IUserContextHelper userContextHelper) : ControllerBase
    {
        /// <summary>
        /// Every wing, with empty plinths included — §5A.1 is explicit that empty plinths
        /// pull harder than empty checkboxes, so they are not filtered out.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<MuseumDto>> GetMuseum(CancellationToken ct) =>
            Ok(await museum.GetMuseum(await CurrentPlayerId(ct), ct));

        /// <summary>Donate spares for Curation. The entry itself is never removed.</summary>
        [HttpPost]
        public async Task<ActionResult<DonationResultDto>> Donate(
            [FromQuery] string key, [FromBody] DonateRequest request, CancellationToken ct) =>
            Ok(await museum.DonateDuplicates(
                await CurrentPlayerId(ct), key, request.Quantity, ct));

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

    public class DonateRequest
    {
        public int Quantity { get; set; } = 1;
    }
}
